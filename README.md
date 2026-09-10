# DiepClone — Prototype

A 2D multiplayer top-down arena shooter (diep.io clone) built for the KTU
*Objektinis programų projektavimas* (T120B516) course.

**This is the "before patterns" prototype** required by the plan (§5, milestone 3): an
authoritative client–server game with real-time movement, aiming, shooting, collisions and
respawning shapes — and **no design patterns yet**. Tag this commit `prototype-before-patterns`
so the "before" UML diagrams stay reproducible.

## What works

- Authoritative server simulation on a fixed **25 Hz** tick loop.
- Any number of clients connect over **SignalR (JSON)** and see each other in real time.
- WASD movement, mouse aim, click/space to fire.
- Bullets collide with shapes and enemy tanks; HP and damage applied; score awarded.
- 55 passive polygon shapes (Square / Triangle / Pentagon) maintained and respawned.
- Tanks respawn on death.
- Rendered with raylib **primitives only** (circles, polygons, lines, rects) + a hand-rolled
  camera and hand-rolled collision (raylib's `Camera2D` and `CheckCollision*` are deliberately
  not used — see plan §3.1).

## Layout

| Project | Role |
|---|---|
| `DiepClone.Shared`  | Protocol DTOs (`InputMessage`, `Snapshot`, `EntityDto`), enums, constants. |
| `DiepClone.Server`  | ASP.NET Core + SignalR hub, authoritative `GameWorld`, 25 Hz `GameLoop`. Headless — **no raylib**. |
| `DiepClone.Client`  | raylib window + render loop, `NetworkClient`, hand-rolled `Camera`. |

## Requirements

- **.NET SDK 9 or newer** (the repo currently targets `net9.0`).
  The course plan states .NET 8 — to switch, change `<TargetFramework>` in
  `Directory.Build.props` to `net8.0` once the .NET 8 SDK is installed. All code is
  source-compatible with both.
- Native raylib ships inside the `Raylib-cs` NuGet package — nothing to install by hand.
  On Linux the client needs X11/Wayland present (the server does not — keep it that way).

Pinned versions (`Directory.Packages.props`):
- `Raylib-cs` **7.0.1** (native raylib **5.5**)
- `Microsoft.AspNetCore.SignalR.Client` **9.0.0**

## Run it

**1. Start the server** (one terminal):

```bash
dotnet run --project DiepClone.Server
```

It listens on `http://0.0.0.0:5000` and logs `Game loop starting at 25 Hz`.

**2. Start one or more clients** (a terminal each):

```bash
dotnet run --project DiepClone.Client
```

Optional args — `dotnet run --project DiepClone.Client -- <name> <serverUrl>`, e.g.

```bash
dotnet run --project DiepClone.Client -- Alice http://192.168.1.50:5000/game
```

Open two clients to see the multiplayer demo. For a LAN demo, point the second machine's
client at the server's IP as shown above (allow port 5000 through the firewall).

### Controls
- **WASD** — move
- **Mouse** — aim
- **Left click / Space** — fire

## Networking contract (JSON)

Client → Server: `Join(name) -> int tankId`, `SendInput({ Seq, MoveX, MoveY, Aim, Fire })`.
Server → Client: `Snapshot({ Tick, Entities[], Leaderboard[] })` every tick.

## Next steps (documented, not yet built)

- **Snapshot viewport culling** — currently one global snapshot for everyone (fine for the
  prototype's ~55 shapes; cull before scaling, plan §6).
- **Client interpolation + prediction** — the client renders the newest snapshot directly.
  Interpolating the last two snapshots and predicting local movement is the next quality pass
  (and the justification for the later `Command.Undo()` rollback work).
- **Bridge refactor** — move all raylib calls behind `IRenderApi` / `RaylibRenderApi` so raylib
  lives in exactly one file (plan §3.1 / §8). Today `Renderer.cs` and `Program.cs` call it directly.
- **Then patterns** — this codebase is the clean baseline the Part 1 patterns are applied to.
