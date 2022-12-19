using System;
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
        var client = new ButtbeeClient("localhost") { Name = "Buttbee Binary Code Example" };
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

        // Get the bytes for the message to send via vibrations
        var bytes = Encoding.ASCII.GetBytes(args.FirstOrDefault() ?? "Hello, buttplug!");
        foreach (var @byte in bytes) {
            // Loop through each bit in the byte
            for (var i = 1; i < 0x100; i <<= 1) {
                if (!device.IsConnected) {
                    Log.Error("Device disconnected! Exiting...");
                    return;
                }

                // If the bit is set, vibrate the device at 33% intensity.
                await device.Scalar(ButtplugDeviceActuatorType.Vibrate, (@byte & i) is not 0 ? 0.33f : 0).ConfigureAwait(true);

                // wait for 300ms
                await Task.Delay(300, cts.Token).ConfigureAwait(true);
            }
        }

        // Stop all devices.
        await device.Stop().ConfigureAwait(true);
    }
}
