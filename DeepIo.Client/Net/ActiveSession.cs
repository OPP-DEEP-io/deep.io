using System.Collections.Concurrent;
using DeepIo.Shared;

namespace DeepIo.Client.Net;

internal enum ConnectionChangeKind { Reconnecting, Rejoined, Failed, Closed }

internal readonly record struct ConnectionChange(ConnectionChangeKind Kind, int PlayerId = -1, string? Error = null);

/// <summary>Owns live connection callbacks, rejoin, input sends, and cleanup.</summary>
internal sealed class ActiveSession : IAsyncDisposable
{
    private readonly IGameConnection _connection;
    private readonly string _name;
    private readonly TankArchetype _archetype;
    private readonly ConcurrentQueue<(long Version, ConnectionChange Change)> _changes = new();
    private long _version;

    public ActiveSession(IGameConnection connection, string name, TankArchetype archetype)
    {
        _connection = connection;
        _name = name;
        _archetype = archetype;
        _connection.Reconnecting += OnReconnecting;
        _connection.Reconnected += RejoinAsync;
        _connection.Closed += OnClosed;
    }

    public async Task<int> ConnectAndJoinAsync(CancellationToken token)
    {
        await _connection.ConnectAsync(token);
        return await _connection.JoinAsync(_name, _archetype, token);
    }

    private void OnReconnecting()
    {
        long version = Interlocked.Increment(ref _version);
        _changes.Enqueue((version, new ConnectionChange(ConnectionChangeKind.Reconnecting)));
    }

    private async Task RejoinAsync()
    {
        long version = Volatile.Read(ref _version);
        try
        {
            int id = await _connection.JoinAsync(_name, _archetype);
            _changes.Enqueue((version, new ConnectionChange(ConnectionChangeKind.Rejoined, id)));
        }
        catch (Exception ex)
        {
            _changes.Enqueue((version, new ConnectionChange(ConnectionChangeKind.Failed,
                Error: $"Could not rejoin: {ex.Message}")));
        }
    }

    private void OnClosed(Exception? error)
    {
        long version = Interlocked.Increment(ref _version);
        _changes.Enqueue((version, new ConnectionChange(ConnectionChangeKind.Closed,
            Error: error?.Message ?? "Connection closed.")));
    }

    public bool TryTakeChange(out ConnectionChange change)
    {
        while (_changes.TryDequeue(out var item))
        {
            if (item.Version != Volatile.Read(ref _version)) continue;
            change = item.Change;
            return true;
        }
        change = default;
        return false;
    }

    public Snapshot? TakeLatest() => _connection.TakeLatest();

    public void SendInput(InputMessage input) => Send(() => _connection.SendInputAsync(input), "Input send");
    public void UpgradeStat(StatKind stat) => Send(() => _connection.UpgradeStatAsync(stat), "Upgrade request");
    public void UndoUpgrade() => Send(_connection.UndoUpgradeAsync, "Upgrade request");

    private static void Send(Func<Task> send, string operation)
    {
        try
        {
            Task request = send();
            if (!request.IsCompletedSuccessfully)
                _ = ObserveAsync(request, operation);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{operation} failed: {ex.Message}");
        }
    }

    private static async Task ObserveAsync(Task request, string operation)
    {
        try { await request; }
        catch (Exception ex) { Console.Error.WriteLine($"{operation} failed: {ex.Message}"); }
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
