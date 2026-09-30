using DeepIo.Shared;

namespace DeepIo.Client.Net;

/// <summary>Stores newest snapshot; adapter uses atomic exchange for callback-to-render handoff.</summary>
internal sealed class SnapshotMailbox
{
    private Snapshot? _latest;

    public void Publish(Snapshot snapshot) => Interlocked.Exchange(ref _latest, snapshot);
    public Snapshot? PeekLatest() => Volatile.Read(ref _latest);
    public Snapshot? TakeLatest() => Interlocked.Exchange(ref _latest, null);
    public void Clear() => Interlocked.Exchange(ref _latest, null);
}
