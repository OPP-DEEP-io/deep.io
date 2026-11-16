namespace DeepIo.Server.World;

/// <summary>
/// The server's own console: every line typed on stdin goes to the same interpreter as the
/// in-game console, with admin rights, and the GameLoop prints the output. When there is no
/// stdin (a deployed service), the first read returns null and this service simply ends.
/// </summary>
public sealed class ServerConsoleService : BackgroundService
{
    private readonly GameWorld _world;

    public ServerConsoleService(GameWorld world) => _world = world;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        Console.WriteLine("Server console ready. Type 'help' for commands.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Console reads cannot be cancelled; WaitAsync lets shutdown move on without them.
                string? line = await Task.Run(Console.ReadLine, stoppingToken).WaitAsync(stoppingToken);
                if (line is null) return;
                if (!string.IsNullOrWhiteSpace(line))
                    _world.EnqueueConsole(GameWorld.ServerConsoleId, line);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}
