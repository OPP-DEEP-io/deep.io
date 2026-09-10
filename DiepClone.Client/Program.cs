using System.Numerics;
using DiepClone.Client.Net;
using DiepClone.Client.Rendering;
using DiepClone.Shared;
using Raylib_cs;

// ---- Configuration -------------------------------------------------------------------
// Usage: dotnet run --project DiepClone.Client -- [name] [serverUrl]
const int screenW = 1280;
const int screenH = 720;

string playerName = args.Length > 0 ? args[0] : $"Player{Random.Shared.Next(100, 999)}";
string serverUrl  = args.Length > 1 ? args[1] : "http://localhost:5000/game";

// ---- Window --------------------------------------------------------------------------
// IMPORTANT: everything raylib touches must run on THIS (main) thread. We therefore keep
// Main synchronous — no `await` on the main path — because the first `await` in a console
// app resumes on a thread-pool thread, which would move the render loop off the main thread
// and leave the window unresponsive. All networking is pushed onto a background task below.
Raylib.SetTraceLogLevel(TraceLogLevel.Warning);   // silence raylib startup spam (plan §3.1)
Raylib.InitWindow(screenW, screenH, "DiepClone — Prototype");
Raylib.SetTargetFPS(60);

var net = new NetworkClient(serverUrl);
var renderer = new Renderer();
var cam = new Camera { ScreenWidth = screenW, ScreenHeight = screenH, Zoom = 1f };

// myId is written by the background connect task and read by the main loop. int reads/writes
// are atomic in .NET; Volatile keeps the loop from caching a stale value.
int myId = -1;

// Connect + join off the main thread so raylib keeps ownership of it. If the server isn't up
// yet the window still opens (showing "Connecting..."); WithAutomaticReconnect handles drops.
_ = Task.Run(async () =>
{
    try
    {
        await net.ConnectAsync();
        int id = await net.JoinAsync(playerName);
        Volatile.Write(ref myId, id);
        Console.WriteLine($"Connected as \"{playerName}\" — tank id {id}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Could not connect to {serverUrl}: {ex.Message}");
        Console.WriteLine("Start DiepClone.Server first; the client will keep showing 'Connecting...'.");
    }
});

Snapshot? snapshot = null;
Vector2 myPos = Vector2.Zero;
int seq = 0;
float inputTimer = 0f;
const float inputInterval = 1f / 30f;   // send intent ~30x/second (plan §6)

// ---- Main loop (fully synchronous — stays on the main thread) -------------------------
while (!Raylib.WindowShouldClose())
{
    float dt = Raylib.GetFrameTime();
    int id = Volatile.Read(ref myId);

    // 1. Pull the newest server state pushed in by the SignalR background thread.
    var latest = net.TakeLatest();
    if (latest is not null) snapshot = latest;

    // 2. Locate our own tank in the snapshot.
    if (snapshot is not null && id >= 0)
    {
        foreach (var e in snapshot.Entities)
        {
            if (e.Id == id && e.Kind == "tank")
            {
                myPos = new Vector2(e.X, e.Y);
                break;
            }
        }
    }

    // 3. Smoothly move the camera toward our tank (frame-rate independent easing).
    cam.Target = Vector2.Lerp(cam.Target, myPos, 1f - MathF.Exp(-10f * dt));

    // 4. Sample input.
    Vector2 move = Vector2.Zero;
    if (Raylib.IsKeyDown(KeyboardKey.W)) move.Y -= 1f;
    if (Raylib.IsKeyDown(KeyboardKey.S)) move.Y += 1f;
    if (Raylib.IsKeyDown(KeyboardKey.A)) move.X -= 1f;
    if (Raylib.IsKeyDown(KeyboardKey.D)) move.X += 1f;

    Vector2 worldMouse = cam.ScreenToWorld(Raylib.GetMousePosition());
    float aim = MathF.Atan2(worldMouse.Y - myPos.Y, worldMouse.X - myPos.X);
    bool fire = Raylib.IsMouseButtonDown(MouseButton.Left) || Raylib.IsKeyDown(KeyboardKey.Space);

    // 5. Throttle input to the network rate (fire-and-forget send; never awaited here).
    inputTimer += dt;
    if (inputTimer >= inputInterval && id >= 0)
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

    // 6. Render.
    renderer.Draw(snapshot, cam, id);
}

net.DisposeAsync().AsTask().GetAwaiter().GetResult();   // block briefly; window is closing
Raylib.CloseWindow();
