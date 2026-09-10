using System.Diagnostics;
using DiepClone.Server.Net;
using DiepClone.Shared;
using Microsoft.AspNetCore.SignalR;

namespace DiepClone.Server.World;

/// <summary>
/// Drives the authoritative simulation at a fixed rate and broadcasts a snapshot each tick.
/// Runs as a hosted background service for the lifetime of the server (plan §6 tick loop).
/// </summary>
public sealed class GameLoop : BackgroundService
{
    private readonly GameWorld _world;
    private readonly IHubContext<GameHub> _hub;
    private readonly ILogger<GameLoop> _log;

    public GameLoop(GameWorld world, IHubContext<GameHub> hub, ILogger<GameLoop> log)
    {
        _world = world;
        _hub = hub;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Game loop starting at {Hz} Hz", GameConstants.TickRate);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(GameConstants.TickDelta));
        var clock = Stopwatch.StartNew();
        double last = clock.Elapsed.TotalSeconds;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            double now = clock.Elapsed.TotalSeconds;
            float dt = (float)(now - last);
            last = now;

            _world.Update(dt);
            Snapshot snapshot = _world.BuildSnapshot();

            try
            {
                await _hub.Clients.All.SendAsync("Snapshot", snapshot, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
