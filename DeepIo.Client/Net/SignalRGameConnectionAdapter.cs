using DeepIo.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace DeepIo.Client.Net;

/// <summary>
/// Adapts SignalR's hub calls and callbacks to game connection operations.
///
/// SignalR raises "Snapshot" on a background thread, while rendering runs on the main thread.
/// SnapshotMailbox transfers newest state without retaining stale broadcasts.
/// </summary>
public sealed class SignalRGameConnectionAdapter : IGameConnection
{
    private readonly HubConnection _conn;
    private readonly SnapshotMailbox _snapshots = new();

    public event Action? Reconnecting;
    public event Func<Task>? Reconnected;
    public event Action<Exception?>? Closed;

    public SignalRGameConnectionAdapter(string url)
    {
        _conn = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        _conn.On<Snapshot>("Snapshot", _snapshots.Publish);
        _conn.Reconnecting += _ =>
        {
            Reconnecting?.Invoke();
            return Task.CompletedTask;
        };
        _conn.Reconnected += async _ =>
        {
            if (Reconnected is { } handlers)
                foreach (Func<Task> handler in handlers.GetInvocationList())
                    await handler();
        };
        _conn.Closed += error =>
        {
            Closed?.Invoke(error);
            return Task.CompletedTask;
        };
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default) =>
        _conn.StartAsync(cancellationToken);

    public Task<int> JoinAsync(string name, TankArchetype archetype,
        CancellationToken cancellationToken = default) =>
        _conn.InvokeAsync<int>("Join", name, archetype, cancellationToken);

    public Task SendInputAsync(InputMessage input) => _conn.SendAsync("SendInput", input);

    public Task UpgradeStatAsync(StatKind stat) => _conn.SendAsync("UpgradeStat", stat);

    public Task UndoUpgradeAsync() => _conn.SendAsync("UndoUpgrade");

    /// <summary>
    /// Returns and clears the most recent snapshot.
    /// Current client renders this state directly; interpolation could retain two snapshots later.
    /// </summary>
    public Snapshot? TakeLatest() => _snapshots.TakeLatest();

    public async ValueTask DisposeAsync() => await _conn.DisposeAsync();
}
