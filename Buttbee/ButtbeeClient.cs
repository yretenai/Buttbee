using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Events;
using Buttbee.Messages;

namespace Buttbee;

public class ButtbeeClient : IDisposable, IAsyncDisposable {
    private readonly object IdLock = new();

    public ButtbeeClient(string host, ushort port = 12345, IButtbeeLogger? logger = null) {
        WebSocket = new ClientWebSocket();
        Host = new Uri($"ws://{host}:{port}");
        Logger = logger?.AddContext<ButtbeeClient>();
    }

    protected IButtbeeLogger? Logger { get; }
    public bool Debug { get; set; }

    public string Name { get; init; } = "Buttbee.Client";

    public ClientWebSocket WebSocket { get; }
    public Uri Host { get; }
    public ButtplugServerInfo? ServerInfo { get; private set; }
    private Thread? RxThread { get; set; }
    private CancellationTokenSource? CancellationTokenSource { get; set; }
    private ConcurrentDictionary<uint, EventHandler<ButtbeeMessageEventArgs>> Callbacks { get; } = new();
    public ConcurrentDictionary<uint, ButtbeeDevice> Devices { get; } = new();
    private uint IdCursor { get; set; } = 1;
    private Timer? PingTimer { get; set; }

    public async ValueTask DisposeAsync() {
        await Close().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public event EventHandler<ButtbeeMessageEventArgs>? ReceiveMessage;
    public event EventHandler<ButtbeeDeviceEventArgs>? DeviceAdded;
    public event EventHandler<ButtbeeDeviceEventArgs>? DeviceRemoved;

    public async Task Connect(CancellationToken token = default) {
        if (CancellationTokenSource is not null || RxThread is not null) {
            Logger?.Warn("Already connected");
            return;
        }

        if (WebSocket.State is not WebSocketState.None) {
            Logger?.Warn("WebSocket is not in a valid state");
            return;
        }

        Logger?.Info("Connecting to {Host}", Host);
        await WebSocket.ConnectAsync(Host, token).ConfigureAwait(false);

        CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

        RxThread = new Thread(Rx) {
            Name = "ButtbeeClient.Rx",
            IsBackground = true,
        };

        RxThread.Start();

        var (msg, err, _) = await Send<ButtplugServerInfo, ButtplugRequestServerInfo>(new ButtplugRequestServerInfo { ClientName = $"Buttbee/0.1; {Name}" }).ConfigureAwait(false);
        if (err is not null) {
            Logger?.Critical("Failed to get server info: {Error}", err.ErrorMessage);
            await Close().ConfigureAwait(false);
            return;
        }

        ServerInfo = msg!;
        Logger?.Info("Connected to {ServerName} Message Version {ServerVersion}", ServerInfo.ServerName, ServerInfo.MessageVersion);

        StartPing();

        await Task.Delay(200, token).ConfigureAwait(false);
    }

    private void StartPing() {
        if (PingTimer is not null) {
            return;
        }

        if (ServerInfo is null) {
            Logger?.Error("Tried to start ping timer without server info");
            return;
        }

        if (ServerInfo.MaxPingTime is 0) {
            Logger?.Warn("Server does not support pings!");
            return;
        }

        PingTimer = new Timer(_ => {
                var err = Send(new ButtplugPing()).Result;
                if (err is not null) {
                    Logger?.Critical("Ping failed: {Error}", err.ErrorMessage);
                    DisposeAsync().AsTask().Wait(CancellationTokenSource!.Token);
                }
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(Math.Max(10, ServerInfo.MaxPingTime / 4)));
    }

    public async Task<ButtplugError?> Send<T>(T message, string? name = null) where T : ButtplugMessage => (await Send<ButtplugOk, T>(message, name).ConfigureAwait(false)).Error;

    public async Task<(TRx? Message, ButtplugError? Error, ButtbeeMessageEventArgs rawMessage)> Send<TRx, TTx>(TTx message, string? name = null) where TRx : ButtplugMessage where TTx : ButtplugMessage {
        lock (IdLock) {
            message.Id = IdCursor++;
        }

        var tcs = new TaskCompletionSource<(TRx? Message, ButtplugError? Error, ButtbeeMessageEventArgs rawMessage)>();

        void Callback(object? _, ButtbeeMessageEventArgs msg) {
            if (msg.Event is "Error") {
                tcs.SetResult((null, msg.Data.Deserialize<ButtplugError>(), msg));
            } else {
                var data = msg.Data.Deserialize<TRx>();
                if (data is null) {
                    tcs.SetResult((null, new ButtplugError { ErrorMessage = "Invalid message type", ErrorCode = ButtplugErrorCode.ButtbeeError }, msg));
                } else {
                    tcs.SetResult((data, null, msg));
                }
            }
        }

        if (!Callbacks.TryAdd(message.Id, Callback)) {
            throw new Exception("Failed to add callback");
        }

        if (string.IsNullOrEmpty(name)) {
            name = message.GetType().Name;
            if (message.GetType().FullName?.StartsWith("Buttbee.Messages.Buttplug") is true) {
                name = name[8..];
            }
        }

        var msg = JsonSerializer.SerializeToNode(message)!;
        var doc = new JsonArray { new JsonObject { { name, msg } } };

        if (Debug) {
            Logger?.Verbose("Sending message {Name} with data {Data}", name, msg.ToJsonString());
        }

        var buffer = Encoding.UTF8.GetBytes(doc.ToString());
        await WebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationTokenSource!.Token).ConfigureAwait(false);

        return await tcs.Task.ConfigureAwait(false);
    }

    private void Rx() {
        Logger?.Info("Starting Rx thread");
        var buffer = ArrayPool<byte>.Shared.Rent(0x1000);
        var segment = new Memory<byte>(buffer);

        try {
            var fullBuffer = Array.Empty<byte>();
            while (CancellationTokenSource is { IsCancellationRequested: false }) {
                if (WebSocket.State is not WebSocketState.Open) {
                    break;
                }

                var result = WebSocket.ReceiveAsync(segment[..0x1000], CancellationTokenSource.Token).AsTask().Result;

                if (CancellationTokenSource.IsCancellationRequested) {
                    break;
                }

                if (WebSocket.State is not WebSocketState.Open) {
                    break;
                }

                if (result.MessageType is WebSocketMessageType.Close) {
                    Close().Wait();
                    break;
                }

                if (!result.EndOfMessage) {
                    fullBuffer = new byte[result.Count + result.Count];
                    Array.Copy(buffer, 0, fullBuffer, 0, result.Count);
                    continue;
                }

                string json;
                if (fullBuffer != Array.Empty<byte>()) {
                    Array.Copy(buffer, 0, fullBuffer, result.Count, result.Count);
                    json = Encoding.UTF8.GetString(fullBuffer);
                    fullBuffer = Array.Empty<byte>();
                } else {
                    json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                }

                try {
                    var document = JsonDocument.Parse(json);
                    foreach (var message in document.RootElement.EnumerateArray()) {
                        foreach (var messageObject in message.EnumerateObject()) {
                            var name = messageObject.Name;
                            var id = messageObject.Value.GetProperty("Id").GetUInt32();
                            var evt = new ButtbeeMessageEventArgs(name, messageObject.Value);

                            if (Debug) {
                                Logger?.Verbose("Received message {Name} with data {Data}", name, messageObject.Value.ToString());
                            }

                            ReceiveMessage?.Invoke(this, new ButtbeeMessageEventArgs(name, messageObject.Value));

                            if (id is 0) {
                                RxEvent(name, messageObject);

                                continue;
                            }

                            if (Callbacks.TryGetValue(id, out var handler)) {
                                Task.Factory.StartNew(() => handler(this, evt), CancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                            }
                        }
                    }
                } catch (JsonException e) {
                    Logger?.Critical(e, "Failed to parse json");
                }
            }
        } catch (Exception e) {
            if (e is not TaskCanceledException && e.InnerException is not TaskCanceledException && !(e is AggregateException agg && agg.InnerExceptions.OfType<TaskCanceledException>().Any())) {
                Logger?.Critical("Failed to receive message: {Message}", e.Message);
                Close().Wait();
                throw;
            }
        } finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        Logger?.Warn("Exiting Rx thread");
    }

    protected virtual void RxEvent(string name, JsonProperty messageObject) {
        // publish announce message
        switch (name) {
            case "DeviceAdded": {
                var rawDevice = messageObject.Value.Deserialize<ButtplugDeviceAdded>();
                if (rawDevice is null) {
                    Logger?.Critical("Failed to parse device added message");
                    return;
                }

                var device = new ButtbeeDevice(rawDevice, this, Logger);
                Devices[device.Id] = device;
                Logger?.Info("Device \"{Name}\" connencted", device.DisplayName);
                DeviceAdded?.Invoke(this, new ButtbeeDeviceEventArgs(device));
                return;
            }
            case "DeviceRemoved": {
                var rawDevice = messageObject.Value.Deserialize<ButtplugDeviceRemoved>();
                if (rawDevice is null) {
                    Logger?.Critical("Failed to parse device added message");
                    return;
                }

                if (!Devices.TryRemove(rawDevice.DeviceIndex, out var device)) {
                    Logger?.Warn("Failed to cleanup device {Id}", rawDevice.DeviceIndex);
                    return;
                }

                Logger?.Info("Device \"{Name}\" disconnected", device.DisplayName);

                device.IsConnected = false;
                DeviceRemoved?.Invoke(this, new ButtbeeDeviceEventArgs(device));
                return;
            }
            case "ScanningFinished":
                Logger?.Info("Scanning finished");
                return;
            case "Error":
                Logger?.Warn("Received global error: {Error}", messageObject.Value.GetProperty("ErrorMessage").GetString());
                return;
            default:
                Logger?.Warn("Unhandled event: {Name}", name);
                return;
        }
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            // nothing.
        }

        Close().Wait();
    }

    ~ButtbeeClient() {
        Dispose(false);
    }

    public virtual async Task Close() {
        if (WebSocket.State is not WebSocketState.Open) {
            return;
        }

        Logger?.Info("Closing connection to {Host}", Host);

        if (RxThread is not null) {
            await StopDevices().ConfigureAwait(false);
        }

        if (PingTimer is not null) {
            await PingTimer.DisposeAsync().ConfigureAwait(false);
            PingTimer = null;
        }

        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;

        if (RxThread is not null) {
            RxThread.Join();
            RxThread = null;
        }
    }

    public async Task StartScanning() {
        var error = await Send(new ButtplugStartScanning()).ConfigureAwait(false);
        if (error is not null) {
            throw new ButtbeeException(error);
        }

        Logger?.Info("Started scanning");
    }

    public async Task StopScanning() {
        var error = await Send(new ButtplugStopScanning()).ConfigureAwait(false);
        if (error is not null) {
            throw new ButtbeeException(error);
        }

        Logger?.Info("Stopped scanning");
    }

    public async Task StopDevices() {
        var error = await Send(new ButtplugStopAllDevices()).ConfigureAwait(false);
        if (error is not null) {
            throw new ButtbeeException(error);
        }

        Logger?.Info("Stopped all devices");
    }

    public async Task RefreshDevices() {
        var (msg, error, _) = await Send<ButtplugDeviceList, ButtplugRequestDeviceList>(new ButtplugRequestDeviceList()).ConfigureAwait(false);
        if (error is not null) {
            throw new ButtbeeException(error);
        }

        var copy = Devices.Values.ToArray();
        Devices.Clear();

        foreach (var device in copy) {
            Logger?.Info("Device \"{Name}\" disconnected", device.DisplayName);
            device.IsConnected = false;
            DeviceRemoved?.Invoke(this, new ButtbeeDeviceEventArgs(device));
        }

        foreach (var rawDevice in msg!.Devices) {
            var device = new ButtbeeDevice(rawDevice, this, Logger);
            Devices[device.Id] = device;
            Logger?.Info("Device \"{Name}\" connencted", device.DisplayName);
            DeviceAdded?.Invoke(this, new ButtbeeDeviceEventArgs(device));
        }
    }

    public void ReplaceDevice(ButtbeeDevice srcDevice, ButtbeeDevice dstDevice) {
        Devices[srcDevice.Id] = dstDevice;

        if (!ReferenceEquals(srcDevice, dstDevice)) {
            srcDevice.IsConnected = false;
        }
    }
}
