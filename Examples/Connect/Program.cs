using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Buttbee.Examples.Connect;

internal static class Program {
    public static async Task Main() {
        Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

        // Create a new client, set up a cancellation token so we can control the lifetime of the client
        using var cts = new CancellationTokenSource();
#pragma warning disable CA2000 // Dispose objects before losing scope, buggy: https://github.com/dotnet/roslyn-analyzers/issues/5712
        var client = new ButtbeeClient("localhost") { Name = "Buttbee Connect Example" };
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

        while (!cts.IsCancellationRequested) {
            // do nothing.
        }
    }
}
