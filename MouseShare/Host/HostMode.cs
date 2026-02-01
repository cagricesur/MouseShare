using System.Net;
using System.Net.Sockets;
using MS.Core;
using MS.Core.Models;
using MS.Core.Protocol;
using MouseShare.Connection;
using MouseShare.Windows;

namespace MouseShare.Host;

/// <summary>
/// Host mode: physical mouse is connected here. Captures mouse, detects edges, sends to Client.
/// </summary>
public sealed class HostMode : IDisposable
{
    private readonly ScreenInfo _screen;
    private readonly int _port;
    private TcpListener? _listener;
    private MouseShareConnection? _connection;
    private CancellationTokenSource? _pollCts;
    private MouseHook? _mouseHook;
    private RawMouseInput? _rawMouse;
    private volatile bool _cursorOnHost = true;
    private const int PollIntervalMs = 8; // ~120 Hz

    public event Action<string>? OnLog;
    public event Action? OnClientConnected;
    public event Action? OnClientDisconnected;

    public HostMode(int port = 38472)
    {
        _port = port;
        _screen = MouseCapture.GetPrimaryScreenInfo();
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        OnLog?.Invoke($"Host listening on port {_port}. Screen: {_screen.Width}x{_screen.Height}");
        OnLog?.Invoke("Waiting for Client to connect...");

        _connection = await MouseShareConnection.AcceptAsHostAsync(_listener, _screen);
        if (_connection == null) return;

        OnLog?.Invoke($"Client connected. Remote screen: {_connection.RemoteScreen.Width}x{_connection.RemoteScreen.Height}");
        OnClientConnected?.Invoke();

        _connection.OnEdgeTransition += HandleEdgeTransitionFromClient;
        _connection.OnDisconnected += () =>
        {
            _mouseHook?.Uninstall();
            OnClientDisconnected?.Invoke();
            OnLog?.Invoke("Client disconnected.");
        };

        _mouseHook = new MouseHook();
        _mouseHook.SetCallbacks(
            (btn, pressed) =>
            {
                if (!_cursorOnHost && _connection?.IsConnected == true)
                {
                    _connection.Send(MessageSerializer.SerializeMouseButton(btn, pressed));
                    return true; // consume
                }
                return false;
            },
            delta =>
            {
                if (!_cursorOnHost && _connection?.IsConnected == true)
                {
                    _connection.Send(MessageSerializer.SerializeMouseScroll(delta));
                    return true; // consume
                }
                return false;
            });
        _mouseHook.Install();

        _rawMouse = new RawMouseInput((dx, dy) =>
        {
            if (!_cursorOnHost && _connection?.IsConnected == true && (dx != 0 || dy != 0))
                _connection.Send(MessageSerializer.SerializeMouseDelta(dx, dy));
        });
        _rawMouse.Start();

        _pollCts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoopAsync(_pollCts.Token));
    }

    private void HandleEdgeTransitionFromClient(EdgeTransitionMessage msg)
    {
        // Client cursor hit its edge (e.g. left) - transition back to Host (right edge)
        _cursorOnHost = true;
        var pt = CoordinateMapping.MapClientEdgeToHost(msg.Edge, 0.5);
        var (px, py) = pt.ToPixel(_screen);
        MouseCapture.SetCursorPosition(px, py);
        OnLog?.Invoke($"Cursor returned to Host at ({px}, {py})");
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var lastSent = (double.NaN, double.NaN);
        while (!ct.IsCancellationRequested && _connection?.IsConnected == true)
        {
            try
            {
                var (x, y) = MouseCapture.GetCursorPosition();
                var nx = (double)x / _screen.Width;
                var ny = (double)y / _screen.Height;

                if (_cursorOnHost)
                {
                    var edge = CoordinateMapping.DetectEdge(nx, ny);
                    if (edge != Edge.None)
                    {
                        _cursorOnHost = false;
                        _connection.Send(MessageSerializer.SerializeEdgeTransition(edge));
                        var pt = CoordinateMapping.MapEdgeTransition(edge, (edge & (Edge.Left | Edge.Right)) != 0 ? ny : nx);
                        _connection.Send(MessageSerializer.SerializeMouseMove(pt.X, pt.Y));
                        lastSent = (pt.X, pt.Y);
                        OnLog?.Invoke($"Cursor moved to Client (edge {edge})");
                    }
                    else if (Math.Abs(nx - lastSent.Item1) > 0.001 || Math.Abs(ny - lastSent.Item2) > 0.001)
                    {
                        lastSent = (nx, ny);
                        _connection.Send(MessageSerializer.SerializeMouseMove(nx, ny));
                    }
                }
                else
                {
                    // Cursor on client - we send position when available; RawMouseInput sends deltas for movement
                    // (position updates help when host has multi-monitor; deltas handle single-monitor clamp)
                    if (Math.Abs(nx - lastSent.Item1) > 0.001 || Math.Abs(ny - lastSent.Item2) > 0.001)
                    {
                        lastSent = (nx, ny);
                        _connection.Send(MessageSerializer.SerializeMouseMove(nx, ny));
                    }
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Poll error: {ex.Message}");
            }

            await Task.Delay(PollIntervalMs, ct);
        }
    }

    public void Stop()
    {
        _pollCts?.Cancel();
        _rawMouse?.Dispose();
        _mouseHook?.Dispose();
        _connection?.Dispose();
        _listener?.Stop();
    }

    public void Dispose() => Stop();
}
