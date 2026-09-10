namespace DiepClone.Shared;

/// <summary>
/// One entity as it appears in a snapshot. A single flat DTO covers tanks, shapes and
/// bullets; unused fields stay at their defaults. Field names are kept terse because
/// snapshots are the bandwidth hot path (plan §6).
/// </summary>
public sealed record EntityDto
{
    public int Id { get; init; }

    /// <summary>"tank" | "shape" | "bullet".</summary>
    public string Kind { get; init; } = "";

    public float X { get; init; }
    public float Y { get; init; }

    /// <summary>Aim angle for tanks, spin angle for shapes (radians).</summary>
    public float Rot { get; init; }

    public float Hp { get; init; }
    public float MaxHp { get; init; }

    /// <summary>ShapeKind as int (shapes only).</summary>
    public int Shape { get; init; }

    /// <summary>Owner tank id (bullets only).</summary>
    public int Owner { get; init; }

    public int Team { get; init; }

    /// <summary>Display name (tanks only).</summary>
    public string? Name { get; init; }
}

public sealed record LeaderboardEntry
{
    public string Name { get; init; } = "";
    public int Score { get; init; }
}

/// <summary>
/// Server -> Client. The authoritative world state broadcast every tick.
/// (Prototype: one global snapshot for everyone. Viewport culling comes next — plan §6.)
/// </summary>
public sealed record Snapshot
{
    public long Tick { get; init; }

    /// <summary>Highest input Seq the server has processed (reserved for reconciliation).</summary>
    public int Ack { get; init; }

    public List<EntityDto> Entities { get; init; } = new();
    public List<LeaderboardEntry> Leaderboard { get; init; } = new();
}
