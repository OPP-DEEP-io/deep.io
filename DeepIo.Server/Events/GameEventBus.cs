namespace DeepIo.Server.Events;

/// <summary>
/// The concrete subject: one bus per arena. Entities publish damage and destruction on it,
/// and the world publishes joins and leaves.
///
/// Notification is synchronous and happens on the game-loop thread. Observers may publish
/// follow-up events (a kill awards XP, which levels up, which unlocks an achievement) or
/// attach/detach while being notified, so <see cref="Notify"/> iterates over a copy-on-write
/// array instead of the live list.
/// </summary>
public sealed class GameEventBus : IGameSubject
{
    private readonly List<IGameObserver> _observers = new();
    private IGameObserver[] _notifyList = [];

    public IReadOnlyList<IGameObserver> Observers => _observers;

    public void Attach(IGameObserver observer)
    {
        if (_observers.Contains(observer)) return;
        _observers.Add(observer);
        _notifyList = _observers.ToArray();
    }

    public void Detach(IGameObserver observer)
    {
        if (_observers.Remove(observer))
            _notifyList = _observers.ToArray();
    }

    public void Notify(GameEvent gameEvent)
    {
        foreach (IGameObserver observer in _notifyList)
            observer.OnNotify(gameEvent);
    }
}
