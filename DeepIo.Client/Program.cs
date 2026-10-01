using DeepIo.Client.Net;
using DeepIo.Client.Rendering;
using DeepIo.Client.Ui;

const int screenW = 1920;
const int screenH = 1080;
const string defaultServerUrl = "https://deepio.linux123123.com/game";

// Optional form defaults remain positional; --backend selects rendering implementation.
string backendName = "raylib";
var defaults = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--backend")
    {
        if (++i >= args.Length) throw new ArgumentException("--backend requires raylib or sdl3.");
        backendName = args[i];
    }
    else if (args[i].StartsWith("--backend=", StringComparison.Ordinal))
        backendName = args[i]["--backend=".Length..];
    else
        defaults.Add(args[i]);
}

IRenderBackend backend = backendName.ToLowerInvariant() switch
{
    "raylib" => new RaylibRenderBackend(),
    "sdl3" => new Sdl3RenderBackend(),
    _ => throw new ArgumentException($"Unknown backend '{backendName}'. Use raylib or sdl3."),
};
backend.OpenWindow(screenW, screenH, "deep.io", 300);

var session = new ClientSessionFacade();
var join = new JoinScreenController(defaults.Count > 0 ? defaults[0] : "",
    defaults.Count > 1 ? defaults[1] : defaultServerUrl, backend, session);
var gameplay = new GameplayController(backend, session);

try
{
    while (!backend.WindowShouldClose())
    {
        ClientSessionState oldState = session.State;
        session.Poll();
        if (oldState == ClientSessionState.Connecting && session.State == ClientSessionState.Playing)
            Console.WriteLine($"Connected as \"{session.PlayerName}\" — tank id {session.PlayerId}");

        if (session.State == ClientSessionState.Playing)
            gameplay.RunFrame();
        else
            join.RunFrame();
    }
}
finally
{
    try { session.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    finally { backend.CloseWindow(); }
}
