# MouseShare

Share one Bluetooth mouse between two PCs. When the cursor hits the edge of the HOST screen, it **transitions to the CLIENT** and the mouse fully controls the client cursor across its screen. Move to the opposite edge on the client to switch back. Works with different resolutions.

## Architecture

- **HOST**: The PC where the physical Bluetooth mouse is connected.
- **CLIENT**: The other PC that receives mouse control.
- When you push the cursor to the host edge (in the direction of the client), it switches to the client and the mouse movements control the client cursor anywhere on its screen.
- Use `--layout` to specify where the client screen is: left (default), right, top, or bottom of the host.

## Usage

### On the PC with the mouse (HOST)

```bash
MouseShare --host
# With layout (where client is relative to host)
MouseShare --host --layout left
MouseShare --host --layout left
MouseShare --host --layout top
MouseShare --host --layout bottom
# Optional port
MouseShare --host --layout left 38472
```

### On the other PC (CLIENT)

```bash
MouseShare --client 192.168.1.100
# Layout must match host
MouseShare --client 192.168.1.100 --layout left
MouseShare --client 192.168.1.100 --layout left 38472
```

### Layout options

| Layout  | Client position     | Host edge to push | Client edge to return |
|---------|---------------------|-------------------|------------------------|
| `left`  | To the left (default)| Left edge        | Right edge             |
| `right` | To the right of host| Right edge        | Left edge              |
| `top`   | Above host          | Top edge          | Bottom edge            |
| `bottom`| Below host          | Bottom edge       | Top edge               |

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
2. Both exchange screen dimensions and layout.
3. HOST polls mouse position and sends it to CLIENT while the cursor is on the host.
4. When the cursor hits the **layout-specific edge** on HOST (e.g. right edge when `--layout right`), the cursor transitions to CLIENT at the opposite edge.
5. From then on, **Raw Input** sends relative mouse deltas to the client so the mouse fully controls the client cursor across its entire screen.
6. Mouse clicks and scroll are forwarded when the cursor is on the remote PC.
7. Pushing the cursor to the **opposite edge** on CLIENT switches control back to HOST.
