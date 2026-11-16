using System.Numerics;
using DeepIo.Client.Rendering;
using DeepIo.Shared;

namespace DeepIo.Client.Hud;

/// <summary>
/// Colleague: WASD, mouse aim and fire, sent to the server at 30 Hz. The mediator suspends it
/// while the console is open or the tank is dead.
/// </summary>
public sealed class TankInputComponent : HudComponent
{
    private const float InputInterval = 1f / 30f;

    private readonly IInputSource _input;
    private readonly IGameActions _actions;
    private int _sequence;
    private float _timer;
    private float _aim;

    public TankInputComponent(IHudMediator mediator, IInputSource input, IGameActions actions) : base(mediator)
    {
        _input = input;
        _actions = actions;
    }

    public bool IsSuspended { get; private set; }

    /// <summary>
    /// Stops reading the keyboard. The server repeats the last input it got, so one neutral
    /// input (no movement, trigger released) goes out right away or the tank would keep going.
    /// </summary>
    public void Suspend()
    {
        if (IsSuspended) return;
        IsSuspended = true;
        Send(Vector2.Zero, _aim, fire: false);
    }

    public void Resume()
    {
        if (!IsSuspended) return;
        IsSuspended = false;
        _timer = InputInterval;   // send on the next update
    }

    public override void Update(HudFrame frame)
    {
        if (IsSuspended) return;

        Vector2 move = Vector2.Zero;
        if (_input.IsKeyDown(ClientKey.W)) move.Y -= 1f;
        if (_input.IsKeyDown(ClientKey.S)) move.Y += 1f;
        if (_input.IsKeyDown(ClientKey.A)) move.X -= 1f;
        if (_input.IsKeyDown(ClientKey.D)) move.X += 1f;

        Vector2 worldMouse = frame.Camera.ScreenToWorld(_input.MousePosition);
        _aim = MathF.Atan2(worldMouse.Y - frame.PlayerPosition.Y, worldMouse.X - frame.PlayerPosition.X);
        bool fire = _input.IsPrimaryMouseButtonDown() || _input.IsKeyDown(ClientKey.Space);

        _timer += frame.Dt;
        if (_timer < InputInterval) return;
        _timer = 0f;
        Send(move, _aim, fire);
    }

    private void Send(Vector2 move, float aim, bool fire) =>
        _actions.SendInput(new InputMessage
        {
            Seq = ++_sequence,
            MoveX = move.X,
            MoveY = move.Y,
            Aim = aim,
            Fire = fire,
        });
}
