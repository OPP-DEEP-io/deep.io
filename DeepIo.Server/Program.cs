using DeepIo.Server.Diagnostics;
using DeepIo.Server.Net;
using DeepIo.Server.World;

if (args.Contains("--patterns-demo", StringComparer.Ordinal))
{
    PatternDemo.Run();
    return;
}

// --open-console lets players run console commands on any tank (lab demos); otherwise only
// the server console may touch tanks other than the caller's own.
GameWorld.Instance.OpenConsole = args.Contains("--open-console", StringComparer.Ordinal);
args = args.Where(a => a != "--open-console").ToArray();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// The world is a thread-safe Singleton (GameWorld.Instance). We register the existing
// instance instead of letting the container construct one, so DI cannot become a second
// way of creating an arena.
builder.Services.AddSingleton(GameWorld.Instance);
builder.Services.AddHostedService<GameLoop>();    // authoritative tick loop
builder.Services.AddHostedService<ServerConsoleService>();   // stdin -> console interpreter

var app = builder.Build();

app.MapGet("/", () => "deep.io server is running. Connect a client to the /game hub.");
app.MapHub<GameHub>("/game");

string listenUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:5000";
app.Run(listenUrl);
