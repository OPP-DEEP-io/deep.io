# deep.io

Small multiplayer diep.io style arena game.

## Requirements

- .NET 10 SDK
- Linux desktop session for client window

## Run

Start server:

```bash
dotnet run --project DeepIo.Server
```

Start client in another terminal:

```bash
dotnet run --project DeepIo.Client
```

Connect more clients to test multiplayer. Default server address: `http://localhost:5000/game`.

Client args:

```bash
dotnet run --project DeepIo.Client -- <name> <server-url>
```

## Controls

- WASD: move
- Mouse: aim
- Left click or Space: fire
