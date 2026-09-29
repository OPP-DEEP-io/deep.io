using DeepIo.Server.World;
using DeepIo.Shared;
using Microsoft.AspNetCore.SignalR;

namespace DeepIo.Server.Net;

/// <summary>
/// The SignalR endpoint clients talk to. Deliberately thin: it just forwards requests into
/// the concurrent inboxes on <see cref="GameWorld"/>. Nothing here touches the entity
/// registry directly, which keeps all simulation state changes on the game-loop thread.
/// </summary>
public sealed class GameHub : Hub
{
    private readonly GameWorld _world;

    public GameHub(GameWorld world) => _world = world;

    /// <summary>
    /// Spawns a tank of the requested build for this connection and returns its entity id.
    /// The archetype only names a build; which concrete parts it maps to is decided by the
    /// matching Abstract Factory on the server.
    /// </summary>
    public int Join(string name, TankArchetype archetype) =>
        _world.CreateTankForConnection(Context.ConnectionId, name, archetype).Id;

    /// <summary>Records this connection's latest input for the next tick.</summary>
    public void SendInput(InputMessage input) => _world.SetInput(Context.ConnectionId, input);

    /// <summary>Spends one skill point on <paramref name="stat"/> (an UpgradeStatCommand on the next tick).</summary>
    public void UpgradeStat(StatKind stat)
    {
        // Validated here, on the hub thread: an out-of-range stat must never reach the game loop.
        if (Enum.IsDefined(stat))
            _world.EnqueueUpgrade(Context.ConnectionId, stat);
    }

    /// <summary>Undoes this player's most recent upgrade and refunds the point.</summary>
    public void UndoUpgrade() => _world.EnqueueUndoUpgrade(Context.ConnectionId);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _world.EnqueueLeave(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
