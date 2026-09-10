using DiepClone.Server.Net;
using DiepClone.Server.World;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<GameWorld>();       // single shared world (Singleton pattern later)
builder.Services.AddHostedService<GameLoop>();    // authoritative 25 Hz tick loop

var app = builder.Build();

app.MapGet("/", () => "DiepClone server is running. Connect a client to the /game hub.");
app.MapHub<GameHub>("/game");

// Bind on all interfaces so a second machine on the LAN can join for the multiplayer demo.
app.Run("http://0.0.0.0:5000");
