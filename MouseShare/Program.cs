using MouseShare.Client;
using MouseShare.Host;

namespace MouseShare;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== MouseShare ===");
        Console.WriteLine("Share one Bluetooth mouse between two PCs. Move cursor to screen edge to switch.");
        Console.WriteLine();

        if (args.Length > 0 && args[0].Equals("--host", StringComparison.OrdinalIgnoreCase))
        {
            await RunHostAsync(args);
        }
        else if (args.Length > 0 && args[0].Equals("--client", StringComparison.OrdinalIgnoreCase))
        {
            await RunClientAsync(args);
        }
        else
        {
            PrintUsage();
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  MouseShare --host [port]           Run as HOST (mouse connected here)");
        Console.WriteLine("  MouseShare --client <host> [port]  Run as CLIENT (connect to host)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  On PC with mouse:  MouseShare --host");
        Console.WriteLine("  On other PC:       MouseShare --client 192.168.1.100");
        Console.WriteLine();
        Console.WriteLine("Default port: 38472");
    }

    static async Task RunHostAsync(string[] args)
    {
        var port = args.Length > 1 && int.TryParse(args[1], out var p) ? p : 38472;
        using var host = new HostMode(port);
        var exit = new TaskCompletionSource();
        host.OnLog += Console.WriteLine;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            host.Stop();
            exit.TrySetResult();
        };

        await host.StartAsync();
        Console.WriteLine("Press Ctrl+C to stop.");
        await exit.Task;
    }

    static async Task RunClientAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Error: --client requires host IP or hostname.");
            Console.WriteLine("Example: MouseShare --client 192.168.1.100");
            return;
        }
        var host = args[1];
        var port = args.Length > 2 && int.TryParse(args[2], out var p) ? p : 38472;

        using var client = new ClientMode();
        var exit = new TaskCompletionSource();
        client.OnLog += Console.WriteLine;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            client.Dispose();
            exit.TrySetResult();
        };

        try
        {
            await client.ConnectAsync(host, port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect: {ex.Message}");
            return;
        }

        Console.WriteLine("Connected. Move cursor to screen edge to switch between PCs. Press Ctrl+C to exit.");
        await exit.Task;
    }
}
