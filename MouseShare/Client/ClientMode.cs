using MS.Core;
using MS.Core.Models;
using MS.Core.Protocol;
using MouseShare.Connection;
using MouseShare.Windows;

namespace MouseShare.Client;

/// <summary>
/// Client mode: receives mouse data from Host, moves local cursor, detects edges for transition back.
/// </summary>
public sealed class ClientMode : IDisposable
{
    private readonly ScreenInfo _screen;
    private MouseShareConnection? _connection;
    private bool _cursorOnClient;
    private double _lastX, _lastY;
    private Edge _clientTransitionEdge;
    private bool _mustLeaveEdgeFirst; // debounce: must move away from edge before transitioning back
    private const int PollIntervalMs = 8;

    public event Action<string>? OnLog;
    public event Action? OnDisconnected;

    public ClientMode()
    {
        _screen = MouseCapture.GetPrimaryScreenInfo();
    }

    public async Task ConnectAsync(string host, int port = 38472, ClientPosition layout = ClientPosition.Left)
    {
        _connection = await MouseShareConnection.ConnectAsClientAsync(host, port, _screen, layout);
        OnLog?.Invoke($"Connected to Host. Local: {_screen.Width}x{_screen.Height}, Remote: {_connection.RemoteScreen.Width}x{_connection.RemoteScreen.Height}, Layout: {_connection.Layout}");

        _connection.OnMouseMove += HandleMouseMove;
        _connection.OnMouseDelta += HandleMouseDelta;
        _connection.OnMouseButton += HandleMouseButton;
        _connection.OnMouseScroll += HandleMouseScroll;
        _cursorOnClient = false;
        _clientTransitionEdge = CoordinateMapping.GetClientTransitionEdge(layout);

        _connection.OnEdgeTransition += HandleEdgeTransition;
        _connection.OnDisconnected += () =>
        {
            _cursorOnClient = false;
            OnDisconnected?.Invoke();
            OnLog?.Invoke("Disconnected from Host.");
        };
    }

    private void HandleEdgeTransition(EdgeTransitionMessage msg)
    {
        _cursorOnClient = true;
        _mustLeaveEdgeFirst = true; // cursor arrives at edge - must move away before we allow transition back
        var pt = CoordinateMapping.MapHostEdgeToClient(msg.Edge, msg.Coord);
        _lastX = pt.X;
        _lastY = pt.Y;
        MoveCursorTo(pt.X, pt.Y);
    }

    private void HandleMouseMove(MouseMoveMessage msg)
    {
        if (!_cursorOnClient) return;
        _lastX = msg.X;
        _lastY = msg.Y;
        MoveCursorTo(msg.X, msg.Y);

        if (_mustLeaveEdgeFirst)
        {
            if (CoordinateMapping.HasLeftEdge(msg.X, msg.Y, _clientTransitionEdge))
                _mustLeaveEdgeFirst = false;
            return;
        }
        if (CoordinateMapping.IsAtEdge(msg.X, msg.Y, _clientTransitionEdge))
        {
            _cursorOnClient = false;
            var coord = (_clientTransitionEdge & (Edge.Left | Edge.Right)) != 0 ? msg.Y : msg.X;
            _connection?.Send(MessageSerializer.SerializeEdgeTransition(_clientTransitionEdge, coord));
            OnLog?.Invoke($"Cursor returned to Host (edge {_clientTransitionEdge})");
        }
    }

    private void HandleMouseDelta(MouseDeltaMessage msg)
    {
        if (!_cursorOnClient) return;
        var (px, py) = new NormalizedPoint(_lastX, _lastY).ToPixel(_screen);
        px = Math.Clamp(px + msg.Dx, 0, _screen.Width - 1);
        py = Math.Clamp(py + msg.Dy, 0, _screen.Height - 1);
        _lastX = (double)px / _screen.Width;
        _lastY = (double)py / _screen.Height;
        MouseCapture.SetCursorPosition(px, py);

        if (_mustLeaveEdgeFirst)
        {
            if (CoordinateMapping.HasLeftEdge(_lastX, _lastY, _clientTransitionEdge))
                _mustLeaveEdgeFirst = false;
            return;
        }
        if (CoordinateMapping.IsAtEdge(_lastX, _lastY, _clientTransitionEdge))
        {
            _cursorOnClient = false;
            var coord = (_clientTransitionEdge & (Edge.Left | Edge.Right)) != 0 ? _lastY : _lastX;
            _connection?.Send(MessageSerializer.SerializeEdgeTransition(_clientTransitionEdge, coord));
            OnLog?.Invoke($"Cursor returned to Host (edge {_clientTransitionEdge})");
        }
    }

    private void HandleMouseButton(MouseButtonMessage msg)
    {
        if (!_cursorOnClient) return;
        if (msg.Pressed)
            MouseSimulation.MouseButtonDown(msg.Button);
        else
            MouseSimulation.MouseButtonUp(msg.Button);
    }

    private void HandleMouseScroll(MouseScrollMessage msg)
    {
        if (!_cursorOnClient) return;
        MouseSimulation.MouseScroll(msg.Delta);
    }

    private void MoveCursorTo(double normX, double normY)
    {
        var (px, py) = new NormalizedPoint(normX, normY).ToPixel(_screen);
        var (clampedX, clampedY) = MouseCapture.ClampToScreen(px, py, _screen);
        MouseCapture.SetCursorPosition(clampedX, clampedY);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
