using System.Collections.Concurrent;
using System.Numerics;
using DeepIo.Client.Net;
using DeepIo.Client.Rendering;
using DeepIo.Client.Ui;
using DeepIo.Shared;
using Raylib_cs;

// Command-line values are retained as optional form defaults for development and shortcuts.
// The player always confirms them from the join screen before a connection is attempted.
const int screenW = 1280;
const int screenH = 720;
const string defaultServerUrl = "http://localhost:5000/game";

string initialPlayerName = args.Length > 0 ? args[0] : "";
string initialServerUrl = args.Length > 1 ? args[1] : defaultServerUrl;

// Everything raylib touches must run on this main thread. Networking is started on a
// background task only after the player submits the join form.
Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
Raylib.InitWindow(screenW, screenH, "deep.io");
Raylib.SetTargetFPS(300);

var joinScreen = new JoinScreen(initialPlayerName, initialServerUrl);
var renderer = new Renderer();
var cam = new Camera { ScreenWidth = screenW, ScreenHeight = screenH, Zoom = 1f };
var connectionResults = new ConcurrentQueue<ConnectionResult>();

ClientState state = ClientState.JoinScreen;
NetworkClient? net = null;
CancellationTokenSource? connectionCts = null;
Task? connectionTask = null;
int myId = -1;

Snapshot? snapshot = null;
Vector2 myPos = Vector2.Zero;
int seq = 0;
float inputTimer = 0f;
const float inputInterval = 1f / 30f;

void BeginJoin()
{
    if (!joinScreen.TryGetConnectionSettings(out string playerName, out string serverUrl))
        return;

    state = ClientState.Connecting;
    joinScreen.ClearError();
    connectionCts = new CancellationTokenSource();
    CancellationToken cancellationToken = connectionCts.Token;

    connectionTask = Task.Run(async () =>
    {
        NetworkClient? candidate = null;
        try
        {
            candidate = new NetworkClient(serverUrl);
            await candidate.ConnectAsync(cancellationToken);
            int id = await candidate.JoinAsync(playerName, cancellationToken);
            connectionResults.Enqueue(ConnectionResult.Succeeded(candidate, id, playerName));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (candidate is not null)
                await candidate.DisposeAsync();
        }
        catch (Exception ex)
        {
            if (candidate is not null)
                await candidate.DisposeAsync();
            connectionResults.Enqueue(ConnectionResult.Failed(ex.Message));
        }
    });
}

while (!Raylib.WindowShouldClose())
{
    if (state != ClientState.Playing)
    {
        if (connectionResults.TryDequeue(out ConnectionResult result))
        {
            connectionCts?.Dispose();
            connectionCts = null;
            connectionTask = null;

            if (result.Client is not null)
            {
                net = result.Client;
                myId = result.PlayerId;
                state = ClientState.Playing;
                Console.WriteLine($"Connected as \"{result.PlayerName}\" — tank id {myId}");
            }
            else
            {
                state = ClientState.JoinScreen;
                joinScreen.SetError($"Could not join: {result.Error}");
            }
        }

        if (state != ClientState.Playing)
        {
            bool submit = joinScreen.Update(state == ClientState.Connecting);
            if (submit && state == ClientState.JoinScreen)
                BeginJoin();

            joinScreen.Draw(state == ClientState.Connecting);
            continue;
        }
    }

    float dt = Raylib.GetFrameTime();

    // Pull the newest server state pushed in by the SignalR background thread.
    var latest = net!.TakeLatest();
    if (latest is not null) snapshot = latest;

    // Locate our own tank in the snapshot.
    if (snapshot is not null)
    {
        foreach (var e in snapshot.Entities)
        {
            if (e.Id == myId && e.Kind == "tank")
            {
                myPos = new Vector2(e.X, e.Y);
                break;
            }
        }
    }

    // Smoothly move the camera toward our tank (frame-rate independent easing).
    cam.Target = Vector2.Lerp(cam.Target, myPos, 1f - MathF.Exp(-10f * dt));

    Vector2 move = Vector2.Zero;
    if (Raylib.IsKeyDown(KeyboardKey.W)) move.Y -= 1f;
    if (Raylib.IsKeyDown(KeyboardKey.S)) move.Y += 1f;
    if (Raylib.IsKeyDown(KeyboardKey.A)) move.X -= 1f;
    if (Raylib.IsKeyDown(KeyboardKey.D)) move.X += 1f;

    Vector2 worldMouse = cam.ScreenToWorld(Raylib.GetMousePosition());
    float aim = MathF.Atan2(worldMouse.Y - myPos.Y, worldMouse.X - myPos.X);
    bool fire = Raylib.IsMouseButtonDown(MouseButton.Left) || Raylib.IsKeyDown(KeyboardKey.Space);

    inputTimer += dt;
    if (inputTimer >= inputInterval)
    {
        inputTimer = 0f;
        net.SendInput(new InputMessage
        {
            Seq = ++seq,
            MoveX = move.X,
            MoveY = move.Y,
            Aim = aim,
            Fire = fire,
        });
    }

    renderer.Draw(snapshot, cam, myId);
}

connectionCts?.Cancel();
connectionTask?.GetAwaiter().GetResult();
connectionCts?.Dispose();
while (connectionResults.TryDequeue(out ConnectionResult pendingResult))
{
    if (pendingResult.Client is not null)
        pendingResult.Client.DisposeAsync().AsTask().GetAwaiter().GetResult();
}
if (net is not null)
    net.DisposeAsync().AsTask().GetAwaiter().GetResult();
Raylib.CloseWindow();

enum ClientState
{
    JoinScreen,
    Connecting,
    Playing,
}

readonly record struct ConnectionResult(NetworkClient? Client, int PlayerId, string PlayerName, string? Error)
{
    public static ConnectionResult Succeeded(NetworkClient client, int playerId, string playerName) =>
        new(client, playerId, playerName, null);

    public static ConnectionResult Failed(string error) => new(null, -1, "", error);
}
