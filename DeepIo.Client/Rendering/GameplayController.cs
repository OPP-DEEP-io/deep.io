using System.Numerics;
using DeepIo.Client.Net;
using DeepIo.Shared;

namespace DeepIo.Client.Rendering;

/// <summary>Gameplay client of the session facade; owns camera and local input cadence.</summary>
public sealed class GameplayController
{
    private static readonly ClientKey[] UpgradeKeys =
    [
        ClientKey.One, ClientKey.Two, ClientKey.Three, ClientKey.Four,
        ClientKey.Five, ClientKey.Six, ClientKey.Seven, ClientKey.Eight,
    ];

    private readonly IRenderBackend _backend;
    private readonly ClientSessionFacade _session;
    private readonly Renderer _renderer;
    private readonly Camera _camera;
    private Vector2 _playerPosition;
    private int _sequence;
    private float _inputTimer;
    private const float InputInterval = 1f / 30f;

    public GameplayController(IRenderBackend backend, ClientSessionFacade session)
    {
        _backend = backend;
        _session = session;
        _renderer = new Renderer(backend);
        _camera = new Camera { ScreenWidth = backend.ScreenWidth, ScreenHeight = backend.ScreenHeight, Zoom = 1f };
    }

    public void RunFrame()
    {
        float dt = _backend.FrameTime;
        Snapshot? snapshot = _session.LatestSnapshot;

        if (snapshot is not null)
            foreach (EntityDto entity in snapshot.Entities)
                if (entity.Id == _session.PlayerId && entity.Kind == "tank")
                {
                    _playerPosition = new Vector2(entity.X, entity.Y);
                    break;
                }

        _camera.Target = Vector2.Lerp(_camera.Target, _playerPosition, 1f - MathF.Exp(-10f * dt));

        Vector2 move = Vector2.Zero;
        if (_backend.IsKeyDown(ClientKey.W)) move.Y -= 1f;
        if (_backend.IsKeyDown(ClientKey.S)) move.Y += 1f;
        if (_backend.IsKeyDown(ClientKey.A)) move.X -= 1f;
        if (_backend.IsKeyDown(ClientKey.D)) move.X += 1f;

        Vector2 worldMouse = _camera.ScreenToWorld(_backend.MousePosition);
        float aim = MathF.Atan2(worldMouse.Y - _playerPosition.Y, worldMouse.X - _playerPosition.X);
        bool fire = _backend.IsPrimaryMouseButtonDown() || _backend.IsKeyDown(ClientKey.Space);

        _inputTimer += dt;
        if (_inputTimer >= InputInterval)
        {
            _inputTimer = 0f;
            _session.SendInput(new InputMessage
            {
                Seq = ++_sequence,
                MoveX = move.X,
                MoveY = move.Y,
                Aim = aim,
                Fire = fire,
            });
        }

        for (int i = 0; i < UpgradeKeys.Length; i++)
            if (_backend.IsKeyPressed(UpgradeKeys[i]))
                _session.UpgradeStat((StatKind)i);
        if (_backend.IsKeyPressed(ClientKey.Backspace))
            _session.UndoUpgrade();

        _renderer.Draw(new GameplayFrame(snapshot, _camera, _session.PlayerId));
    }
}
