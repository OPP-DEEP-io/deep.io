using System.Collections.Concurrent;
using DiepClone.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace DiepClone.Client.Net;

/// <summary>
/// Wraps the SignalR connection to the server.
///
/// Thread-safety note (plan §3.1): SignalR raises "Snapshot" on a background thread, but
/// raylib is single-threaded and must only be called from the main thread. So snapshots are
/// pushed into a ConcurrentQueue here and drained by the render loop via <see cref="TakeLatest"/>.
/// We never draw from inside a SignalR callback.
/// </summary>
public sealed class NetworkClient : IAsyncDisposable
{
    private readonly HubConnection _conn;
    private readonly ConcurrentQueue<Snapshot> _queue = new();

    public NetworkClient(string url)
    {
        _conn = new HubConnectionBuilder()
            .WithUrl(url)
            .WithAutomaticReconnect()
            .Build();

        _conn.On<Snapshot>("Snapshot", snap => _queue.Enqueue(snap));
    }

    public Task ConnectAsync() => _conn.StartAsync();

    public Task<int> JoinAsync(string name) => _conn.InvokeAsync<int>("Join", name);

    /// <summary>Fire-and-forget input send; we never block the render loop on the network.</summary>
    public void SendInput(InputMessage input) => _ = _conn.SendAsync("SendInput", input);

    /// <summary>
    /// Returns the most recent snapshot and discards any older ones queued behind it.
    /// (Prototype: render the newest state directly. An interpolation buffer that keeps the
    /// last two snapshots is the documented next step — plan §6.)
    /// </summary>
    public Snapshot? TakeLatest()
    {
        Snapshot? latest = null;
        while (_queue.TryDequeue(out var s)) latest = s;
        return latest;
    }

    public async ValueTask DisposeAsync() => await _conn.DisposeAsync();
}
