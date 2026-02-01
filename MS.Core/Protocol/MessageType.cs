namespace MS.Core.Protocol;

public enum MessageType : byte
{
    Handshake = 1,
    MouseMove = 2,
    MouseDelta = 8,  // relative movement when cursor on remote screen
    MouseButton = 3,
    MouseScroll = 4,
    EdgeTransition = 5,
    KeepAlive = 6,
    ScreenInfo = 7
}
