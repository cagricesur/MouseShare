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
    private bool _hostMustLeaveEdgeFirst; // debounce: must move away from edge before transitioning to client
    private const int PollIntervalMs = 8; // ~120 Hz

    public event Action<string>? OnLog;
    public event Action? OnClientConnected;
    public event Action? OnClientDisconnected;

    private readonly ClientPosition _layout;

    public HostMode(int port = 38472, ClientPosition layout = ClientPosition.Left)
    {
        _port = port;
        _layout = layout;
        _screen = MouseCapture.GetPrimaryScreenInfo();
    }

    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        OnLog?.Invoke($"Host listening on port {_port}. Screen: {_screen.Width}x{_screen.Height}, Layout: {_layout}");
        OnLog?.Invoke("Waiting for Client to connect...");

        _connection = await MouseShareConnection.AcceptAsHostAsync(_listener, _screen, _layout);
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

        _mouseHook = new MouseHook(
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
        // Client cursor hit its edge - transition back to Host at opposite edge
        _cursorOnHost = true;
        _hostMustLeaveEdgeFirst = true; // cursor at edge - must move away before we allow transition to client
        var pt = CoordinateMapping.MapClientEdgeToHost(msg.Edge, msg.Coord);
        var (px, py) = pt.ToPixel(_screen);
        MouseCapture.SetCursorPosition(px, py);
        OnLog?.Invoke($"Cursor returned to Host at ({px}, {py})");
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var lastSent = (double.NaN, double.NaN);
        var hostEdge = CoordinateMapping.GetHostTransitionEdge(_layout);
        while (!ct.IsCancellationRequested && _connection?.IsConnected == true)
        {
            try
            {
                var (x, y) = MouseCapture.GetCursorPosition();
                var nx = (double)x / _screen.Width;
                var ny = (double)y / _screen.Height;

                if (_cursorOnHost)
                {
                    if (_hostMustLeaveEdgeFirst)
                    {
                        if (CoordinateMapping.HasLeftEdge(nx, ny, hostEdge))
                            _hostMustLeaveEdgeFirst = false;
                    }
                    else if (CoordinateMapping.IsAtEdge(nx, ny, hostEdge))
                    {
                        _cursorOnHost = false;
                        var coord = (hostEdge & (Edge.Left | Edge.Right)) != 0 ? ny : nx;
                        _connection.Send(MessageSerializer.SerializeEdgeTransition(hostEdge, coord));
                        var pt = CoordinateMapping.MapHostEdgeToClient(hostEdge, coord);
                        _connection.Send(MessageSerializer.SerializeMouseMove(pt.X, pt.Y));
                        lastSent = (pt.X, pt.Y);
                        // Warp host cursor to center so Raw Input captures full 2D movement
                        // (when clamped at edge, Windows doesn't report horizontal deltas)
                        var (cx, cy) = (_screen.Width / 2, _screen.Height / 2);
                        MouseCapture.SetCursorPosition(cx, cy);
                        _hostMustLeaveEdgeFirst = true; // will be cleared when cursor returns and moves away
                        OnLog?.Invoke($"Cursor moved to Client (layout {_layout}, edge {hostEdge})");
                    }
                    if (!_hostMustLeaveEdgeFirst && (Math.Abs(nx - lastSent.Item1) > 0.001 || Math.Abs(ny - lastSent.Item2) > 0.001))
                    {
                        lastSent = (nx, ny);
                        _connection.Send(MessageSerializer.SerializeMouseMove(nx, ny));
                    }
                }
                // When cursor on client: only RawMouseInput sends deltas - host position is clamped at edge
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
