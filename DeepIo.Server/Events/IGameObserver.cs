namespace DeepIo.Server.Events;

/// <summary>
/// Observer. Anything that wants to react to what happens in the arena (XP, leaderboard,
/// achievements, kill feed) implements this and is attached to the subject; the code that
/// raises the event never learns who is listening.
/// </summary>
public interface IGameObserver
{
    void OnNotify(GameEvent gameEvent);
}

/// <summary>
/// Subject. Keeps the observer list and pushes every published event to all of them.
/// Deliberately an explicit interface rather than a C# <c>event</c>, so the class diagram and
/// the sequence diagram show exactly what the code does.
/// </summary>
public interface IGameSubject
{
    void Attach(IGameObserver observer);
    void Detach(IGameObserver observer);
    void Notify(GameEvent gameEvent);
}
