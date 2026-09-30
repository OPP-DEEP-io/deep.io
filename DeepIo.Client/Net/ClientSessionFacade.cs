using DeepIo.Shared;

namespace DeepIo.Client.Net;

public enum ClientSessionState { JoinScreen, Connecting, Playing }

/// <summary>
/// Main-thread entry point for joining and playing. ConnectionAttempt, ActiveSession, and
/// SnapshotMailbox hide connection lifecycle, reconnection, and callback synchronization.
/// </summary>
public sealed class ClientSessionFacade : IAsyncDisposable
{
    private readonly Func<string, IGameConnection> _connectionFactory;
    private readonly SnapshotMailbox _snapshots = new();
    private readonly List<Task> _cleanupTasks = new();
    private ConnectionAttempt? _attempt;
    private ActiveSession? _active;
    private bool _disposed;

    public ClientSessionFacade(Func<string, IGameConnection>? connectionFactory = null) =>
        _connectionFactory = connectionFactory ?? (url => new SignalRGameConnectionAdapter(url));

    public ClientSessionState State { get; private set; } = ClientSessionState.JoinScreen;
    public int PlayerId { get; private set; } = -1;
    public string PlayerName { get; private set; } = "";
    public string? Error { get; private set; }
    public Snapshot? LatestSnapshot => _snapshots.PeekLatest();

    public void BeginJoin(string playerName, string serverUrl, TankArchetype archetype)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ClientSessionState.JoinScreen)
            throw new InvalidOperationException("A session is already connecting or playing.");

        Error = null;
        State = ClientSessionState.Connecting;
        _attempt = new ConnectionAttempt(_connectionFactory, playerName, serverUrl, archetype);
    }

    /// <summary>Transfers completed network work to render thread.</summary>
    public void Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_attempt is not null && _attempt.TryTakeResult(out JoinResult result))
        {
            _attempt.Complete();
            _attempt = null;
            if (result.Session is not null)
            {
                _active = result.Session;
                PlayerId = result.PlayerId;
                PlayerName = result.PlayerName;
                State = ClientSessionState.Playing;
            }
            else
            {
                Error = result.Error;
                State = ClientSessionState.JoinScreen;
            }
        }

        while (_active is { } active && active.TryTakeChange(out ConnectionChange change))
        {
            switch (change.Kind)
            {
                case ConnectionChangeKind.Reconnecting:
                    State = ClientSessionState.Connecting;
                    _snapshots.Clear();
                    break;
                case ConnectionChangeKind.Rejoined:
                    PlayerId = change.PlayerId;
                    _snapshots.Clear();
                    active.TakeLatest(); // discard snapshots from before the new join
                    State = ClientSessionState.Playing;
                    break;
                case ConnectionChangeKind.Failed:
                case ConnectionChangeKind.Closed:
                    DropActiveConnection(change.Error ?? "Connection closed.");
                    break;
            }
        }

        _cleanupTasks.RemoveAll(task => task.IsCompleted);
        if (State == ClientSessionState.Playing && _active?.TakeLatest() is { } snapshot)
            _snapshots.Publish(snapshot);
    }

    private void DropActiveConnection(string error)
    {
        ActiveSession active = _active!;
        _active = null;
        PlayerId = -1;
        _snapshots.Clear();
        Error = error;
        State = ClientSessionState.JoinScreen;
        _cleanupTasks.Add(ConnectionAttempt.DisposeQuietlyAsync(active));
    }

    public void SendInput(InputMessage input) => PlayingSession().SendInput(input);

    /// <summary>Spends one skill point; server runs an undoable UpgradeStatCommand.</summary>
    public void UpgradeStat(StatKind stat) => PlayingSession().UpgradeStat(stat);

    public void UndoUpgrade() => PlayingSession().UndoUpgrade();

    private ActiveSession PlayingSession()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ClientSessionState.Playing)
            throw new InvalidOperationException("No active game session.");
        return _active!;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_attempt is not null) await _attempt.DisposeAsync();
        if (_active is not null) await _active.DisposeAsync();
        await Task.WhenAll(_cleanupTasks);
    }
}
