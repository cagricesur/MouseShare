# MouseShare

Share one Bluetooth mouse between two PCs. When the cursor hits the edge of one screen, it transitions to the other PC's screen. Works with different resolutions.

## Architecture

- **HOST**: The PC where the physical Bluetooth mouse is connected. Runs `MouseShare --host`.
- **CLIENT**: The other PC. Runs `MouseShare --client <host-ip>`.
- Both PCs exchange screen dimensions and use normalized coordinates (0-1) for resolution-independent mapping.
- Edge detection: Move cursor to screen edge to "push" it to the other PC.

## Usage

### On the PC with the mouse (HOST)

```bash
MouseShare --host
# Optional: specify port (default 38472)
MouseShare --host 38472
```

### On the other PC (CLIENT)

```bash
MouseShare --client 192.168.1.100
# Optional: specify port
MouseShare --client 192.168.1.100 38472
```

## Requirements

- Windows 10/11
- .NET 10.0 SDK (or build with `dotnet build`)
- Both PCs on the same network (or reachable via IP)
- Firewall: allow inbound TCP on port 38472 for the HOST

## Build

```bash
dotnet build
```

## How it works

1. **HOST** starts a TCP listener and waits for the **CLIENT** to connect.
2. Both exchange screen dimensions (width × height).
3. HOST polls mouse position and sends normalized coordinates to CLIENT.
4. When cursor hits an edge on HOST (e.g. right edge), HOST sends an edge transition. CLIENT shows the cursor at its corresponding edge (left).
5. Raw Input captures relative mouse movement when the cursor is on the remote screen (handles single-monitor clamping).
6. Mouse clicks and scroll are forwarded when the cursor is on the remote PC.
7. Moving to the edge on CLIENT transitions back to HOST.

## Screen layout

- **Right edge of HOST** → **Left edge of CLIENT** (client to the right of host)
- **Left edge of HOST** → **Right edge of CLIENT** (client to the left)
- Same logic for top/bottom edges.
