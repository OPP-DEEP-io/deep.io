using DeepIo.Server.Net;
using DeepIo.Server.World;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<GameWorld>();       // single shared world (Singleton pattern later)
builder.Services.AddHostedService<GameLoop>();    // authoritative 25 Hz tick loop

var app = builder.Build();

app.MapGet("/", () => "deep.io server is running. Connect a client to the /game hub.");
app.MapHub<GameHub>("/game");

string listenUrl = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:5000";
app.Run(listenUrl);
