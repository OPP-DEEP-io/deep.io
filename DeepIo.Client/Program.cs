using System.Numerics;
using DeepIo.Client.Net;
using DeepIo.Client.Rendering;
using DeepIo.Client.Ui;
using DeepIo.Shared;

// Command-line values are retained as optional form defaults for development and shortcuts.
// The player always confirms them from the join screen before a connection is attempted.
const int screenW = 1920;
const int screenH = 1080;
const string defaultServerUrl = "http://localhost:5000/game";

string initialPlayerName = args.Length > 0 ? args[0] : "";
string initialServerUrl = args.Length > 1 ? args[1] : defaultServerUrl;

// Window and input operations stay on this thread. Networking starts on a background task.
IRenderBackend backend = new RaylibRenderBackend();
backend.OpenWindow(screenW, screenH, "deep.io", 300);

var joinScreen = new JoinScreen(initialPlayerName, initialServerUrl, backend);
var renderer = new Renderer(backend);
var cam = new Camera { ScreenWidth = screenW, ScreenHeight = screenH, Zoom = 1f };
var session = new ClientSessionFacade();
Vector2 myPos = Vector2.Zero;
int seq = 0;
float inputTimer = 0f;
const float inputInterval = 1f / 30f;

// Keys 1-8 spend a skill point on the StatKind with the same index.
ClientKey[] upgradeKeys =
[
    ClientKey.One, ClientKey.Two, ClientKey.Three, ClientKey.Four,
    ClientKey.Five, ClientKey.Six, ClientKey.Seven, ClientKey.Eight,
];

void BeginJoin()
{
    if (!joinScreen.TryGetConnectionSettings(out string playerName, out string serverUrl))
        return;

    joinScreen.ClearError();
    session.BeginJoin(playerName, serverUrl, joinScreen.SelectedArchetype);
}

while (!backend.WindowShouldClose())
{
    ClientSessionState oldState = session.State;
    session.Poll();
    if (oldState == ClientSessionState.Connecting && session.State == ClientSessionState.Playing)
        Console.WriteLine($"Connected as \"{session.PlayerName}\" — tank id {session.PlayerId}");
    if (oldState != ClientSessionState.JoinScreen &&
        session.State == ClientSessionState.JoinScreen && session.Error is not null)
        joinScreen.SetError($"Could not join: {session.Error}");

    if (session.State != ClientSessionState.Playing)
    {
        bool connecting = session.State == ClientSessionState.Connecting;
        bool submit = joinScreen.Update(connecting);
        if (submit && !connecting) BeginJoin();
        joinScreen.Draw(session.State == ClientSessionState.Connecting);
        continue;
    }

    float dt = backend.FrameTime;

    Snapshot? snapshot = session.LatestSnapshot;

    // Locate our own tank in the snapshot.
    if (snapshot is not null)
    {
        foreach (var e in snapshot.Entities)
        {
            if (e.Id == session.PlayerId && e.Kind == "tank")
            {
                myPos = new Vector2(e.X, e.Y);
                break;
            }
        }
    }

    // Smoothly move the camera toward our tank (frame-rate independent easing).
    cam.Target = Vector2.Lerp(cam.Target, myPos, 1f - MathF.Exp(-10f * dt));

    Vector2 move = Vector2.Zero;
    if (backend.IsKeyDown(ClientKey.W)) move.Y -= 1f;
    if (backend.IsKeyDown(ClientKey.S)) move.Y += 1f;
    if (backend.IsKeyDown(ClientKey.A)) move.X -= 1f;
    if (backend.IsKeyDown(ClientKey.D)) move.X += 1f;

    Vector2 worldMouse = cam.ScreenToWorld(backend.MousePosition);
    float aim = MathF.Atan2(worldMouse.Y - myPos.Y, worldMouse.X - myPos.X);
    bool fire = backend.IsPrimaryMouseButtonDown() || backend.IsKeyDown(ClientKey.Space);

    inputTimer += dt;
    if (inputTimer >= inputInterval)
    {
        inputTimer = 0f;
        session.SendInput(new InputMessage
        {
            Seq = ++seq,
            MoveX = move.X,
            MoveY = move.Y,
            Aim = aim,
            Fire = fire,
        });
    }

    for (int i = 0; i < upgradeKeys.Length; i++)
        if (backend.IsKeyPressed(upgradeKeys[i]))
            session.UpgradeStat((StatKind)i);
    if (backend.IsKeyPressed(ClientKey.Backspace))
        session.UndoUpgrade();

    renderer.Draw(snapshot, cam, session.PlayerId);
}

session.DisposeAsync().AsTask().GetAwaiter().GetResult();
backend.CloseWindow();
