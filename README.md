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
- 1-8: spend a skill point; Backspace: undo the last stat upgrade

Tanks earn automatic chassis perks at levels 5 (armor, +20% base HP), 10
(regeneration, +2 HP/s), and 15 (turbo, +10% base speed). The perks stack and appear
above the upgrade panel. Respawning halves the score and removes perks above the
new level. Bots start at level 3 with one damage and one reload upgrade.
