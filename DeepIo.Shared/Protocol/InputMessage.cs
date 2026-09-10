namespace DeepIo.Shared;

/// <summary>
/// Client -> Server. Player intent for one tick. Sent ~30x/second.
/// Networking input: { moveX, moveY, aimAngle, firing }.
/// </summary>
public sealed record InputMessage
{
    /// <summary>Monotonic sequence number, for future server reconciliation (Command.Undo).</summary>
    public int Seq { get; init; }

    /// <summary>Movement intent on X, in [-1, 1]. Normalised server-side.</summary>
    public float MoveX { get; init; }

    /// <summary>Movement intent on Y, in [-1, 1]. Normalised server-side.</summary>
    public float MoveY { get; init; }

    /// <summary>Aim angle in radians (world space).</summary>
    public float Aim { get; init; }

    /// <summary>True while the fire button is held.</summary>
    public bool Fire { get; init; }
}
