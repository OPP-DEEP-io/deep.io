using System.Numerics;
using DeepIo.Client.Hud;
using DeepIo.Client.Net;
using DeepIo.Shared;

namespace DeepIo.Client.Rendering;

/// <summary>
/// Gameplay client of the session facade; owns the camera. Keyboard and mouse handling lives
/// in the HUD components, coordinated by <see cref="GameplayHudMediator"/>.
/// </summary>
public sealed class GameplayController
{
    private readonly IRenderBackend _backend;
    private readonly ClientSessionFacade _session;
    private readonly Renderer _renderer;
    private readonly Camera _camera;
    private readonly GameplayHudMediator _hud;
    private Vector2 _playerPosition;

    public GameplayController(IRenderBackend backend, ClientSessionFacade session)
    {
        _backend = backend;
        _session = session;
        _renderer = new Renderer(backend);
        _camera = new Camera { ScreenWidth = backend.ScreenWidth, ScreenHeight = backend.ScreenHeight, Zoom = 1f };
        _hud = new GameplayHudMediator(backend, session);
    }

    public void RunFrame()
    {
        float dt = _backend.FrameTime;
        Snapshot? snapshot = _session.LatestSnapshot;
        var frame = new HudFrame(snapshot, _session.PlayerId, _camera, _playerPosition, dt);

        if (frame.Me() is { } me)
            _playerPosition = new Vector2(me.X, me.Y);

        _camera.Target = Vector2.Lerp(_camera.Target, _playerPosition, 1f - MathF.Exp(-10f * dt));

        _hud.Update(frame with { PlayerPosition = _playerPosition });
        _renderer.Draw(new GameplayFrame(snapshot, _camera, _session.PlayerId, () => _hud.Draw(_backend)));
    }
}
