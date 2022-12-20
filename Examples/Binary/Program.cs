using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buttbee.Messages;
using Serilog;

namespace Buttbee.Examples.Binary;

internal static class Program {
    public static async Task Main(string[] args) {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

        // Create a new client, set up a cancellation token so we can control the lifetime of the client
        using var cts = new CancellationTokenSource();
#pragma warning disable CA2000 // Dispose objects before losing scope, buggy: https://github.com/dotnet/roslyn-analyzers/issues/5712
        var client = new ButtbeeClient("localhost", logger: new SerilogWrapper(Log.Logger)) { Name = "Buttbee Binary Code Example" };
#pragma warning restore CA2000
        await using var _ = client.ConfigureAwait(true);

        // Connect to the server, usually this is Intiface Desktop
        await client.Connect(cts.Token).ConfigureAwait(true);

        // Add a way to cleanly exit the program
        Console.CancelKeyPress += delegate {
            Log.Debug("Canceling...");
            // ReSharper disable once AccessToDisposedClosure
            cts.Cancel();
        };

        // Fetch all the devices from the server
        await client.RefreshDevices().ConfigureAwait(true);

        // If there aren't any devices start scanning.
        if (!client.Devices.Any()) {
            var tcs = new TaskCompletionSource();
            client.DeviceAdded += async (sender, device) => {
                if (sender is not ButtbeeClient buttbeeClient) {
                    return;
                }

                // If we find a device that cna vibrate, stop scanning
                if (device.Device.Capabilties.HasFlag(ButtplugDeviceActuatorType.Vibrate)) {
                    // ReSharper disable once AccessToDisposedClosure
                    await buttbeeClient.StopScanning().ConfigureAwait(true);
                    tcs.SetResult();
                }
            };

            await client.StartScanning().ConfigureAwait(true);
            await tcs.Task.ConfigureAwait(true);
        }

        // Get the first device that can vibrate
        var device = client.Devices.Values.FirstOrDefault(x => x.Capabilties.HasFlag(ButtplugDeviceActuatorType.Vibrate));
        if (device is null) {
            Log.Error("No device found");
            return;
        }

        // subscribe to battery events
        var batterySensor = device.Sensors.FirstOrDefault(x => x.Type == ButtplugDeviceSensorType.Battery);
        if (batterySensor is not null) {
            batterySensor.ValueChanged += (_, sensor) => {
                var batteryAbsolute = sensor.Values[0] + sensor.Sensor.Ranges[0].Start.Value;
                var batteryPercentage = (int) Math.Round(batteryAbsolute / (double) sensor.Sensor.Ranges[0].End.Value * 100d);
                Log.Information("{Device} is at {BatteryLevel}%", sensor.Sensor.Name, batteryPercentage);
            };

            await batterySensor.Poll();
        }

        // Get the bytes for the message to send via vibrations
        var bytes = Encoding.ASCII.GetBytes(args.FirstOrDefault() ?? "Hello, buttplug!");
        foreach (var @byte in bytes) {
            // Loop through each bit in the byte
            for (var i = 7; i >= 0; i--) {
                if (!device.IsConnected) {
                    Log.Error("Device disconnected! Exiting...");
                    return;
                }

                // If the bit is set, vibrate the device at 33% intensity.
                await device.Scalar(ButtplugDeviceActuatorType.Vibrate, (@byte & (1 << i)) is not 0 ? 0.33f : 0).ConfigureAwait(true);

                // wait for 300ms
                await Task.Delay(300, cts.Token).ConfigureAwait(true);
            }
        }

        // Stop all devices.
        await device.Stop().ConfigureAwait(true);
    }
}

[SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem")]
internal class SerilogWrapper : IButtbeeLogger {
    internal SerilogWrapper(ILogger logger) => Logger = logger;

    private ILogger Logger { get; }

    public void Verbose(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Info(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Warn(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Error(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Critical(string message, params object?[] values) {
        Logger.Debug(message, values);
    }

    public void Critical(Exception e, string message, params object?[] values) {
        Logger.Debug(e, message, values);
    }

    public IButtbeeLogger AddContext(string key, string? value) => new SerilogWrapper(Logger.ForContext(key, value));

    public IButtbeeLogger AddContext<T>() =>
        // ReSharper disable once ContextualLoggerProblem
        new SerilogWrapper(Logger.ForContext<T>());
}
