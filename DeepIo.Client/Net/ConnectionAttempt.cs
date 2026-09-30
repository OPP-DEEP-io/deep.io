using System.Collections.Concurrent;
using DeepIo.Shared;

namespace DeepIo.Client.Net;

internal readonly record struct JoinResult(ActiveSession? Session, int PlayerId, string PlayerName, string? Error);

/// <summary>Runs one cancellable connect/join attempt off the render thread.</summary>
internal sealed class ConnectionAttempt : IAsyncDisposable
{
    private readonly ConcurrentQueue<JoinResult> _results = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _task;

    public ConnectionAttempt(Func<string, IGameConnection> factory, string name, string url, TankArchetype archetype)
    {
        _task = Task.Run(() => RunAsync(factory, name, url, archetype, _cancellation.Token));
    }

    private async Task RunAsync(Func<string, IGameConnection> factory, string name,
        string url, TankArchetype archetype, CancellationToken token)
    {
        ActiveSession? session = null;
        try
        {
            token.ThrowIfCancellationRequested();
            session = new ActiveSession(factory(url), name, archetype);
            int id = await session.ConnectAndJoinAsync(token);
            token.ThrowIfCancellationRequested();
            _results.Enqueue(new JoinResult(session, id, name, null));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (session is not null) await DisposeQuietlyAsync(session);
        }
        catch (Exception ex)
        {
            string? cleanupError = session is null ? null : await TryDisposeAsync(session);
            string error = cleanupError is null ? ex.Message : $"{ex.Message} (cleanup failed: {cleanupError})";
            _results.Enqueue(new JoinResult(null, -1, "", error));
        }
    }

    public bool TryTakeResult(out JoinResult result) => _results.TryDequeue(out result);

    public void Complete() => _cancellation.Dispose();

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        await _task;
        _cancellation.Dispose();
        while (_results.TryDequeue(out JoinResult result))
            if (result.Session is not null) await DisposeQuietlyAsync(result.Session);
    }

    internal static async Task<string?> TryDisposeAsync(ActiveSession session)
    {
        try { await session.DisposeAsync(); return null; }
        catch (Exception ex) { return ex.Message; }
    }

    internal static async Task DisposeQuietlyAsync(ActiveSession session)
    {
        string? error = await TryDisposeAsync(session);
        if (error is not null) Console.Error.WriteLine($"Connection cleanup failed: {error}");
    }
}
