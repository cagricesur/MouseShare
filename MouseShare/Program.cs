using MS.Core.Models;
using MouseShare.Client;
using MouseShare.Host;

namespace MouseShare;

class Program
{
    static ClientPosition ParseLayout(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals("--layout", StringComparison.OrdinalIgnoreCase))
                return args[i + 1].ToLowerInvariant() switch
                {
                    "right" => ClientPosition.Right,
                    "left" => ClientPosition.Left,
                    "top" => ClientPosition.Top,
                    "bottom" => ClientPosition.Bottom,
                    _ => ClientPosition.Right
                };
        return ClientPosition.Right;
    }

    static int? ParsePort(string[] args, bool preferLast = false)
    {
        var candidates = preferLast ? args.Reverse() : args;
        foreach (var a in candidates)
            if (int.TryParse(a, out var p) && p > 0 && p < 65536)
                return p;
        return null;
    }

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
        Console.WriteLine("  MouseShare --host [--layout right|left|top|bottom] [port]");
        Console.WriteLine("  MouseShare --client <host> [--layout right|left|top|bottom] [port]");
        Console.WriteLine();
        Console.WriteLine("Layout: where the Client screen is relative to Host (default: right)");
        Console.WriteLine("  right  - Client to the right of Host (push cursor right to switch)");
        Console.WriteLine("  left   - Client to the left");
        Console.WriteLine("  top    - Client above Host");
        Console.WriteLine("  bottom - Client below Host");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  MouseShare --host --layout right");
        Console.WriteLine("  MouseShare --client 192.168.1.100 --layout right");
        Console.WriteLine();
        Console.WriteLine("Default port: 38472");
    }

    static async Task RunHostAsync(string[] args)
    {
        var layout = ParseLayout(args);
        var port = ParsePort(args) ?? 38472;
        using var host = new HostMode(port, layout);
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
        var hostArg = args.Skip(1).FirstOrDefault(a =>
            !a.Equals("--layout", StringComparison.OrdinalIgnoreCase) &&
            !a.Equals("--host", StringComparison.OrdinalIgnoreCase) &&
            !a.Equals("--client", StringComparison.OrdinalIgnoreCase) &&
            !a.Equals("right", StringComparison.OrdinalIgnoreCase) &&
            !a.Equals("left", StringComparison.OrdinalIgnoreCase) &&
            !a.Equals("top", StringComparison.OrdinalIgnoreCase) &&
            !a.Equals("bottom", StringComparison.OrdinalIgnoreCase) &&
            !int.TryParse(a, out _));
        if (string.IsNullOrEmpty(hostArg))
        {
            Console.WriteLine("Error: --client requires host IP or hostname.");
            Console.WriteLine("Example: MouseShare --client 192.168.1.100");
            return;
        }
        var layout = ParseLayout(args);
        var port = ParsePort(args, preferLast: true) ?? 38472;

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
            await client.ConnectAsync(hostArg, port, layout);
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
