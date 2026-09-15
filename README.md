# deep.io

Small multiplayer diep.io style arena game.

## Requirements

- .NET 10 SDK
- ASP.NET core

## Run

Start server:

```bash
dotnet run --project DeepIo.Server
```

Start client in another terminal:

```bash
dotnet run --project DeepIo.Client
```

Enter a player name in the client window and select **Join game**. The server address defaults
to `http://localhost:5000/game` and can be changed on the same screen. Connect more clients to
test multiplayer.

Optional client arguments prefill the join form but do not connect automatically:

```bash
dotnet run --project DeepIo.Client -- <name> <server-url>
```

## Controls

- WASD: move
- Mouse: aim
- Left click or Space: fire
