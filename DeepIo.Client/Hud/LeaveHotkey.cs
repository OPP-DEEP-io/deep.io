using DeepIo.Client.Rendering;

namespace DeepIo.Client.Hud;

/// <summary>
/// Colleague: Esc asks to leave the arena. It does not leave by itself; the mediator decides
/// (and disables this while the console is open, where Esc only closes the console).
/// </summary>
public sealed class LeaveHotkey : HudComponent
{
    private readonly IInputSource _input;

    public LeaveHotkey(IHudMediator mediator, IInputSource input) : base(mediator) => _input = input;

    public bool Enabled { get; set; } = true;

    public override void Update(HudFrame frame)
    {
        if (Enabled && _input.IsKeyPressed(ClientKey.Escape))
            Mediator.Notify(this, HudEvent.LeaveRequested);
    }
}
