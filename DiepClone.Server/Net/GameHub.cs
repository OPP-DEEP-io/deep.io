using DiepClone.Server.World;
using DiepClone.Shared;
using Microsoft.AspNetCore.SignalR;

namespace DiepClone.Server.Net;

/// <summary>
/// The SignalR endpoint clients talk to. Deliberately thin: it just forwards requests into
/// the concurrent inboxes on <see cref="GameWorld"/>. Nothing here touches the entity
/// registry directly, which keeps all simulation state changes on the game-loop thread.
/// </summary>
public sealed class GameHub : Hub
{
    private readonly GameWorld _world;

    public GameHub(GameWorld world) => _world = world;

    /// <summary>Spawns a tank for this connection and returns its entity id.</summary>
    public int Join(string name) => _world.CreateTankForConnection(Context.ConnectionId, name).Id;

    /// <summary>Records this connection's latest input for the next tick.</summary>
    public void SendInput(InputMessage input) => _world.SetInput(Context.ConnectionId, input);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _world.EnqueueLeave(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
