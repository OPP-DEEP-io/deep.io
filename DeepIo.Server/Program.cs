using DeepIo.Server.Net;
using DeepIo.Server.World;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

// The world is a thread-safe Singleton (GameWorld.Instance). We register the existing
// instance instead of letting the container construct one, so DI cannot become a second
// way of creating an arena.
builder.Services.AddSingleton(GameWorld.Instance);
builder.Services.AddHostedService<GameLoop>();    // authoritative tick loop

var app = builder.Build();

app.MapGet("/", () => "deep.io server is running. Connect a client to the /game hub.");
app.MapHub<GameHub>("/game");

string listenUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:5000";
app.Run(listenUrl);
