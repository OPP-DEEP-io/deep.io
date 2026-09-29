using DeepIo.Server.Entities;
using DeepIo.Shared;

namespace DeepIo.Server.Events.Observers;

/// <summary>
/// Keeps the top-10 table. Scores only change when a tank kills something or dies (respawn
/// halves the score), and the roster only changes on join/leave, so instead of re-sorting
/// every tick the table is marked stale by those events and rebuilt on the next snapshot.
/// </summary>
public sealed class LeaderboardObserver : IGameObserver
{
    public const int Size = 10;

    private List<LeaderboardEntry> _top = new();
    private bool _dirty = true;

    public void OnNotify(GameEvent gameEvent)
    {
        if (gameEvent is EntityDestroyedEvent { Killer: Tank }
            or EntityDestroyedEvent { Victim: Tank }
            or PlayerJoinedEvent
            or PlayerLeftEvent)
        {
            _dirty = true;
        }
    }

    /// <summary>
    /// The current table. Rebuilt only if something relevant happened since the last call;
    /// a rebuilt table is a new list, so a snapshot that is still being sent is never mutated.
    /// </summary>
    public List<LeaderboardEntry> Current(IEnumerable<Tank> tanks)
    {
        if (!_dirty) return _top;

        _top = tanks
            .OrderByDescending(t => t.Score)
            .Take(Size)
            .Select(t => new LeaderboardEntry { Name = t.Name, Score = t.Score })
            .ToList();
        _dirty = false;
        return _top;
    }
}
