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
    private const int PollIntervalMs = 8;

    public event Action<string>? OnLog;
    public event Action? OnDisconnected;

    public ClientMode()
    {
        _screen = MouseCapture.GetPrimaryScreenInfo();
    }

    public async Task ConnectAsync(string host, int port = 38472)
    {
        _connection = await MouseShareConnection.ConnectAsClientAsync(host, port, _screen);
        OnLog?.Invoke($"Connected to Host. Local screen: {_screen.Width}x{_screen.Height}, Remote: {_connection.RemoteScreen.Width}x{_connection.RemoteScreen.Height}");

        _connection.OnMouseMove += HandleMouseMove;
        _connection.OnMouseDelta += HandleMouseDelta;
        _connection.OnMouseButton += HandleMouseButton;
        _connection.OnMouseScroll += HandleMouseScroll;
        _connection.OnEdgeTransition += HandleEdgeTransition;
        _connection.OnDisconnected += () =>
        {
            _cursorOnClient = false;
            OnDisconnected?.Invoke();
            OnLog?.Invoke("Disconnected from Host.");
        };

        _cursorOnClient = false;
    }

    private void HandleEdgeTransition(EdgeTransitionMessage msg)
    {
        _cursorOnClient = true;
        var pt = CoordinateMapping.MapEdgeTransition(msg.Edge, 0.5);
        MoveCursorTo(pt.X, pt.Y);
    }

    private void HandleMouseMove(MouseMoveMessage msg)
    {
        if (!_cursorOnClient) return;
        _lastX = msg.X;
        _lastY = msg.Y;
        MoveCursorTo(msg.X, msg.Y);

        var edge = CoordinateMapping.DetectEdge(msg.X, msg.Y);
        if (edge != Edge.None)
        {
            _cursorOnClient = false;
            _connection?.Send(MessageSerializer.SerializeEdgeTransition(edge));
            OnLog?.Invoke($"Cursor returned to Host (edge {edge})");
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

        var edge = CoordinateMapping.DetectEdge(_lastX, _lastY);
        if (edge != Edge.None)
        {
            _cursorOnClient = false;
            _connection?.Send(MessageSerializer.SerializeEdgeTransition(edge));
            OnLog?.Invoke($"Cursor returned to Host (edge {edge})");
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
