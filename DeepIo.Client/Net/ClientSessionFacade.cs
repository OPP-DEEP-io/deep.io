using System.Collections.Concurrent;
using DeepIo.Shared;

namespace DeepIo.Client.Net;

public enum ClientSessionState { JoinScreen, Connecting, Playing }

/// <summary>
/// Owns one client's connection attempt and active session. Public state is accessed from
/// the render thread; network callbacks hand results across concurrent queues.
/// </summary>
public sealed class ClientSessionFacade : IAsyncDisposable
{
    private readonly Func<string, IGameConnection> _connectionFactory;
    private readonly ConcurrentQueue<JoinResult> _results = new();
    private readonly ConcurrentQueue<ConnectionEvent> _events = new();
    private readonly List<Task> _cleanupTasks = new();
    private CancellationTokenSource? _attemptCancellation;
    private Task? _attempt;
    private ConnectionRegistration? _active;
    private bool _disposed;

    public ClientSessionFacade(Func<string, IGameConnection>? connectionFactory = null) =>
        _connectionFactory = connectionFactory ?? (url => new SignalRGameConnectionAdapter(url));

    public ClientSessionState State { get; private set; } = ClientSessionState.JoinScreen;
    public int PlayerId { get; private set; } = -1;
    public string PlayerName { get; private set; } = "";
    public string? Error { get; private set; }
    public Snapshot? LatestSnapshot { get; private set; }

    public void BeginJoin(string playerName, string serverUrl, TankArchetype archetype)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ClientSessionState.JoinScreen)
            throw new InvalidOperationException("A session is already connecting or playing.");

        Error = null;
        State = ClientSessionState.Connecting;
        _attemptCancellation = new CancellationTokenSource();
        CancellationToken token = _attemptCancellation.Token;
        _attempt = Task.Run(() => ConnectAndJoinAsync(playerName, serverUrl, archetype, token));
    }

    private async Task ConnectAndJoinAsync(string name, string url, TankArchetype archetype, CancellationToken token)
    {
        IGameConnection? candidate = null;
        try
        {
            token.ThrowIfCancellationRequested();
            candidate = _connectionFactory(url);
            var registration = new ConnectionRegistration(candidate);
            candidate.Reconnecting += () => OnReconnecting(registration);
            candidate.Reconnected += () => RejoinAsync(registration, name, archetype);
            candidate.Closed += error => OnClosed(registration, error);

            await candidate.ConnectAsync(token);
            int id = await candidate.JoinAsync(name, archetype, token);
            token.ThrowIfCancellationRequested();
            _results.Enqueue(new JoinResult(registration, id, name, null));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            string? cleanupError = await TryDisposeAsync(candidate);
            if (cleanupError is not null) Console.Error.WriteLine($"Connection cleanup failed: {cleanupError}");
        }
        catch (Exception ex)
        {
            string? cleanupError = await TryDisposeAsync(candidate);
            string error = cleanupError is null ? ex.Message : $"{ex.Message} (cleanup failed: {cleanupError})";
            _results.Enqueue(new JoinResult(null, -1, "", error));
        }
    }

    private void OnReconnecting(ConnectionRegistration registration)
    {
        long version = Interlocked.Increment(ref registration.Version);
        _events.Enqueue(new ConnectionEvent(registration, version, ConnectionEventKind.Reconnecting));
    }

    private async Task RejoinAsync(ConnectionRegistration registration, string name, TankArchetype archetype)
    {
        long version = Volatile.Read(ref registration.Version);
        try
        {
            int id = await registration.Connection.JoinAsync(name, archetype);
            _events.Enqueue(new ConnectionEvent(registration, version, ConnectionEventKind.Rejoined, id));
        }
        catch (Exception ex)
        {
            _events.Enqueue(new ConnectionEvent(registration, version, ConnectionEventKind.Failed,
                Error: $"Could not rejoin: {ex.Message}"));
        }
    }

    private void OnClosed(ConnectionRegistration registration, Exception? error)
    {
        long version = Interlocked.Increment(ref registration.Version);
        _events.Enqueue(new ConnectionEvent(registration, version, ConnectionEventKind.Closed,
            Error: error?.Message ?? "Connection closed."));
    }

    /// <summary>Transfer completed work and connection changes to render thread.</summary>
    public void Poll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_results.TryDequeue(out JoinResult result))
        {
            _attemptCancellation?.Dispose();
            _attemptCancellation = null;
            _attempt = null;
            if (result.Registration is not null)
            {
                _active = result.Registration;
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

        while (_events.TryDequeue(out ConnectionEvent change))
        {
            if (!ReferenceEquals(_active, change.Registration) ||
                change.Version != Volatile.Read(ref change.Registration.Version))
                continue;

            switch (change.Kind)
            {
                case ConnectionEventKind.Reconnecting:
                    State = ClientSessionState.Connecting;
                    LatestSnapshot = null;
                    break;
                case ConnectionEventKind.Rejoined:
                    PlayerId = change.PlayerId;
                    LatestSnapshot = null;
                    _active.Connection.TakeLatest(); // discard snapshots from before the new join
                    State = ClientSessionState.Playing;
                    break;
                case ConnectionEventKind.Failed:
                case ConnectionEventKind.Closed:
                    DropActiveConnection(change.Error ?? "Connection closed.");
                    break;
            }
        }

        _cleanupTasks.RemoveAll(task => task.IsCompleted);
        if (State == ClientSessionState.Playing && _active?.Connection.TakeLatest() is { } snapshot)
            LatestSnapshot = snapshot;
    }

    private void DropActiveConnection(string error)
    {
        ConnectionRegistration registration = _active!;
        _active = null;
        PlayerId = -1;
        LatestSnapshot = null;
        Error = error;
        State = ClientSessionState.JoinScreen;
        _cleanupTasks.Add(DisposeQuietlyAsync(registration.Connection));
    }

    public void SendInput(InputMessage input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ClientSessionState.Playing)
            throw new InvalidOperationException("No active game session.");

        try
        {
            Task send = _active!.Connection.SendInputAsync(input);
            if (!send.IsCompletedSuccessfully)
                _ = ObserveSendAsync(send);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Input send failed: {ex.Message}");
        }
    }

    private static async Task ObserveSendAsync(Task send)
    {
        try
        {
            await send;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Input send failed: {ex.Message}");
        }
    }

    /// <summary>Spends one skill point; the server runs it as an undoable UpgradeStatCommand.</summary>
    public void UpgradeStat(StatKind stat) => SendUpgradeRequest(connection => connection.UpgradeStatAsync(stat));

    /// <summary>Undoes this player's most recent upgrade (respec) and refunds the point.</summary>
    public void UndoUpgrade() => SendUpgradeRequest(connection => connection.UndoUpgradeAsync());

    private void SendUpgradeRequest(Func<IGameConnection, Task> send)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (State != ClientSessionState.Playing)
            throw new InvalidOperationException("No active game session.");

        try
        {
            Task request = send(_active!.Connection);
            if (!request.IsCompletedSuccessfully)
                _ = ObserveUpgradeRequestAsync(request);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Upgrade request failed: {ex.Message}");
        }
    }

    private static async Task ObserveUpgradeRequestAsync(Task request)
    {
        try
        {
            await request;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Upgrade request failed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_attemptCancellation is not null) await _attemptCancellation.CancelAsync();
        if (_attempt is not null) await _attempt;
        _attemptCancellation?.Dispose();

        while (_results.TryDequeue(out JoinResult result))
            if (result.Registration is not null && !ReferenceEquals(result.Registration, _active))
                await DisposeQuietlyAsync(result.Registration.Connection);

        if (_active is not null) await _active.Connection.DisposeAsync();
        await Task.WhenAll(_cleanupTasks);
    }

    private static async Task<string?> TryDisposeAsync(IGameConnection? connection)
    {
        if (connection is null) return null;
        try
        {
            await connection.DisposeAsync();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static async Task DisposeQuietlyAsync(IGameConnection connection)
    {
        string? error = await TryDisposeAsync(connection);
        if (error is not null) Console.Error.WriteLine($"Connection cleanup failed: {error}");
    }

    private sealed class ConnectionRegistration(IGameConnection connection)
    {
        public IGameConnection Connection { get; } = connection;
        public long Version;
    }

    private enum ConnectionEventKind { Reconnecting, Rejoined, Failed, Closed }

    private readonly record struct JoinResult(ConnectionRegistration? Registration, int PlayerId, string PlayerName, string? Error);
    private readonly record struct ConnectionEvent(
        ConnectionRegistration Registration, long Version, ConnectionEventKind Kind,
        int PlayerId = -1, string? Error = null);
}
