using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using MS.Core;
using MS.Core.Models;
using MS.Core.Protocol;
using MouseShare.Windows;

namespace MouseShare.Connection;

/// <summary>
/// Manages the TCP connection and message exchange between Host and Client.
/// </summary>
public sealed class MouseShareConnection : IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cts = new();
    private readonly BlockingCollection<byte[]> _sendQueue = new();
    private readonly Task _sendTask;
    private readonly Task _receiveTask;

    public ScreenInfo RemoteScreen { get; private set; } = new(1920, 1080);
    public bool IsConnected => _client.Connected;
    public event Action<MouseMoveMessage>? OnMouseMove;
    public event Action<MouseDeltaMessage>? OnMouseDelta;
    public event Action<MouseButtonMessage>? OnMouseButton;
    public event Action<MouseScrollMessage>? OnMouseScroll;
    public event Action<EdgeTransitionMessage>? OnEdgeTransition;
    public event Action? OnDisconnected;

    private MouseShareConnection(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _sendTask = Task.Run(() => SendLoopAsync(_cts.Token));
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public static async Task<MouseShareConnection> ConnectAsClientAsync(string host, int port, ScreenInfo localScreen)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port);
        var conn = new MouseShareConnection(client);
        conn.Send(MessageSerializer.SerializeScreenInfo(localScreen));
        return conn;
    }

    public static async Task<MouseShareConnection?> AcceptAsHostAsync(TcpListener listener, ScreenInfo localScreen)
    {
        var client = await listener.AcceptTcpClientAsync();
        var conn = new MouseShareConnection(client);
        conn.Send(MessageSerializer.SerializeScreenInfo(localScreen));
        return conn;
    }

    public void Send(byte[] data)
    {
        try { _sendQueue.Add(data); } catch (InvalidOperationException) { }
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            foreach (var data in _sendQueue.GetConsumingEnumerable(ct))
            {
                var lenBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
                var combined = new byte[lenBytes.Length + data.Length];
                Buffer.BlockCopy(lenBytes, 0, combined, 0, lenBytes.Length);
                Buffer.BlockCopy(data, 0, combined, lenBytes.Length, data.Length);
                await _stream.WriteAsync(combined, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException) { }
        catch (Exception) { }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var lenBuffer = new byte[4];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (await ReadExactlyAsync(lenBuffer, ct) == 0) break;
                var len = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lenBuffer));
                if (len <= 0 || len > 1024 * 1024) break;
                var data = new byte[len];
                if (await ReadExactlyAsync(data, ct) < len) break;

                ProcessMessage(data);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
        finally { OnDisconnected?.Invoke(); }
    }

    private async Task<int> ReadExactlyAsync(byte[] buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var n = await _stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (n == 0) return 0;
            total += n;
        }
        return total;
    }

    private void ProcessMessage(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return;
        switch (MessageSerializer.GetMessageType(data))
        {
            case MessageType.MouseMove:
                var (x, y, _) = MessageSerializer.DeserializeMouseMove(data);
                OnMouseMove?.Invoke(new MouseMoveMessage(x, y));
                break;
            case MessageType.MouseDelta:
                var (dx, dy, _d) = MessageSerializer.DeserializeMouseDelta(data);
                OnMouseDelta?.Invoke(new MouseDeltaMessage(dx, dy));
                break;
            case MessageType.MouseButton:
                var (btn, pressed, __) = MessageSerializer.DeserializeMouseButton(data);
                OnMouseButton?.Invoke(new MouseButtonMessage(btn, pressed));
                break;
            case MessageType.MouseScroll:
                var (delta, ___) = MessageSerializer.DeserializeMouseScroll(data);
                OnMouseScroll?.Invoke(new MouseScrollMessage(delta));
                break;
            case MessageType.EdgeTransition:
                var (edge, ____) = MessageSerializer.DeserializeEdgeTransition(data);
                OnEdgeTransition?.Invoke(new EdgeTransitionMessage(edge));
                break;
            case MessageType.ScreenInfo:
                var (screen, _____) = MessageSerializer.DeserializeScreenInfo(data);
                RemoteScreen = screen;
                break;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _sendQueue.CompleteAdding();
        _client.Dispose();
    }
}

public record MouseMoveMessage(double X, double Y);
public record MouseDeltaMessage(int Dx, int Dy);
public record MouseButtonMessage(int Button, bool Pressed);
public record MouseScrollMessage(int Delta);
public record EdgeTransitionMessage(Edge Edge);
