using DeepIo.Server.Entities;

namespace DeepIo.Server.Events.Observers;

/// <summary>
/// Decides which events are worth telling every client about and turns them into kill-feed
/// lines. It does not talk to SignalR itself (GameLoop owns the broadcast); recent lines ride
/// along with every snapshot for a few seconds, so a client that skips a snapshot still sees
/// them.
/// </summary>
public sealed class NetworkBroadcastObserver : IGameObserver
{
    public const int MaxLines = 6;
    public const long LineLifetimeMs = 5000;
    private const int LevelAnnounceEvery = 5;

    private readonly Queue<(long At, string Text)> _lines = new();

    public void OnNotify(GameEvent gameEvent)
    {
        string? line = gameEvent switch
        {
            EntityDestroyedEvent { Victim: Tank victim, Killer: Tank killer } =>
                $"{killer.Name} destroyed {victim.Name}",
            PlayerLeveledUpEvent levelUp when levelUp.NewLevel / LevelAnnounceEvery > levelUp.OldLevel / LevelAnnounceEvery =>
                $"{levelUp.Tank.Name} reached level {levelUp.NewLevel}",
            AchievementUnlockedEvent achievement =>
                $"{achievement.Tank.Name} unlocked \"{achievement.Title}\"",
            PlayerJoinedEvent joined => $"{joined.Tank.Name} joined",
            PlayerLeftEvent left => $"{left.Tank.Name} left",
            _ => null,
        };
        if (line is null) return;

        _lines.Enqueue((Environment.TickCount64, line));
        while (_lines.Count > MaxLines) _lines.Dequeue();
    }

    /// <summary>Lines younger than <see cref="LineLifetimeMs"/>, oldest first.</summary>
    public List<string> RecentLines()
    {
        long cutoff = Environment.TickCount64 - LineLifetimeMs;
        while (_lines.Count > 0 && _lines.Peek().At < cutoff) _lines.Dequeue();
        return _lines.Select(l => l.Text).ToList();
    }
}
