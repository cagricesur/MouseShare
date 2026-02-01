using System.Buffers.Binary;
using System.Net;
using MS.Core.Models;

namespace MS.Core.Protocol;

/// <summary>
/// Binary message protocol for low-latency Host-Client communication.
/// </summary>
public static class MessageSerializer
{
    public static byte[] SerializeHandshake(ScreenInfo screen)
    {
        var buffer = new byte[1 + 4 + 4];
        buffer[0] = (byte)MessageType.Handshake;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(1), screen.Width);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(5), screen.Height);
        return buffer;
    }

    public static (ScreenInfo Screen, int Consumed) DeserializeHandshake(ReadOnlySpan<byte> data)
    {
        if (data.Length < 9) return default;
        var width = BinaryPrimitives.ReadInt32BigEndian(data[1..]);
        var height = BinaryPrimitives.ReadInt32BigEndian(data[5..]);
        return (new ScreenInfo(width, height), 9);
    }

    public static byte[] SerializeMouseMove(double x, double y)
    {
        var buffer = new byte[1 + 8 + 8];
        buffer[0] = (byte)MessageType.MouseMove;
        BinaryPrimitives.WriteDoubleBigEndian(buffer.AsSpan(1), x);
        BinaryPrimitives.WriteDoubleBigEndian(buffer.AsSpan(9), y);
        return buffer;
    }

    public static (double X, double Y, int Consumed) DeserializeMouseMove(ReadOnlySpan<byte> data)
    {
        if (data.Length < 17) return default;
        var x = BinaryPrimitives.ReadDoubleBigEndian(data[1..]);
        var y = BinaryPrimitives.ReadDoubleBigEndian(data[9..]);
        return (x, y, 17);
    }

    public static byte[] SerializeMouseDelta(int dx, int dy)
    {
        var buffer = new byte[1 + 4 + 4];
        buffer[0] = (byte)MessageType.MouseDelta;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(1), dx);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(5), dy);
        return buffer;
    }

    public static (int Dx, int Dy, int Consumed) DeserializeMouseDelta(ReadOnlySpan<byte> data)
    {
        if (data.Length < 9) return default;
        var dx = BinaryPrimitives.ReadInt32BigEndian(data[1..]);
        var dy = BinaryPrimitives.ReadInt32BigEndian(data[5..]);
        return (dx, dy, 9);
    }

    public static byte[] SerializeMouseButton(int button, bool pressed)
    {
        var buffer = new byte[1 + 4 + 1];
        buffer[0] = (byte)MessageType.MouseButton;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(1), button);
        buffer[5] = (byte)(pressed ? 1 : 0);
        return buffer;
    }

    public static (int Button, bool Pressed, int Consumed) DeserializeMouseButton(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6) return default;
        var button = BinaryPrimitives.ReadInt32BigEndian(data[1..]);
        var pressed = data[5] != 0;
        return (button, pressed, 6);
    }

    public static byte[] SerializeMouseScroll(int delta)
    {
        var buffer = new byte[1 + 4];
        buffer[0] = (byte)MessageType.MouseScroll;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(1), delta);
        return buffer;
    }

    public static (int Delta, int Consumed) DeserializeMouseScroll(ReadOnlySpan<byte> data)
    {
        if (data.Length < 5) return default;
        var delta = BinaryPrimitives.ReadInt32BigEndian(data[1..]);
        return (delta, 5);
    }

    public static byte[] SerializeEdgeTransition(Edge edge, double coord = 0.5)
    {
        var buffer = new byte[1 + 4 + 8];
        buffer[0] = (byte)MessageType.EdgeTransition;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(1), (int)edge);
        BinaryPrimitives.WriteDoubleBigEndian(buffer.AsSpan(5), coord);
        return buffer;
    }

    public static (Edge Edge, double Coord, int Consumed) DeserializeEdgeTransition(ReadOnlySpan<byte> data)
    {
        if (data.Length < 5) return default;
        var edge = (Edge)BinaryPrimitives.ReadInt32BigEndian(data[1..]);
        var coord = data.Length >= 13 ? BinaryPrimitives.ReadDoubleBigEndian(data[5..]) : 0.5;
        return (edge, coord, data.Length >= 13 ? 13 : 5);
    }

    public static byte[] SerializeKeepAlive() => [(byte)MessageType.KeepAlive];

    public static byte[] SerializeScreenInfo(ScreenInfo screen, ClientPosition layout = ClientPosition.Left)
    {
        var buffer = new byte[1 + 4 + 4 + 1];
        buffer[0] = (byte)MessageType.ScreenInfo;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(1), screen.Width);
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(5), screen.Height);
        buffer[9] = (byte)layout;
        return buffer;
    }

    public static (ScreenInfo Screen, ClientPosition Layout, int Consumed) DeserializeScreenInfo(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10) return default;
        var width = BinaryPrimitives.ReadInt32BigEndian(data[1..]);
        var height = BinaryPrimitives.ReadInt32BigEndian(data[5..]);
        var layout = (ClientPosition)Math.Clamp((int)data[9], 0, 3);
        return (new ScreenInfo(width, height), layout, 10);
    }

    public static MessageType GetMessageType(ReadOnlySpan<byte> data) =>
        data.Length > 0 ? (MessageType)data[0] : MessageType.KeepAlive;
}
