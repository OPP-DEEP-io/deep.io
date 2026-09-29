using DeepIo.Shared;

namespace DeepIo.Client.Net;

/// <summary>Game operations required by a client session, independent of transport.</summary>
public interface IGameConnection : IAsyncDisposable
{
    event Action? Reconnecting;
    event Func<Task>? Reconnected;
    event Action<Exception?>? Closed;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<int> JoinAsync(string name, TankArchetype archetype, CancellationToken cancellationToken = default);
    Task SendInputAsync(InputMessage input);
    Snapshot? TakeLatest();
}
