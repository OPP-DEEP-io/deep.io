using DeepIo.Client.Rendering;
using DeepIo.Shared;

namespace DeepIo.Client.Hud;

/// <summary>
/// Colleague: keys 1-8 spend a skill point, Backspace undoes the last one. Disabled by the
/// mediator while the console needs those keys for typing, and while the tank is dead.
/// </summary>
public sealed class UpgradeHotkeys : HudComponent
{
    private static readonly ClientKey[] Keys =
    [
        ClientKey.One, ClientKey.Two, ClientKey.Three, ClientKey.Four,
        ClientKey.Five, ClientKey.Six, ClientKey.Seven, ClientKey.Eight,
    ];

    private readonly IInputSource _input;
    private readonly IGameActions _actions;

    public UpgradeHotkeys(IHudMediator mediator, IInputSource input, IGameActions actions) : base(mediator)
    {
        _input = input;
        _actions = actions;
    }

    public bool Enabled { get; set; } = true;

    public override void Update(HudFrame frame)
    {
        if (!Enabled) return;

        for (int i = 0; i < Keys.Length; i++)
            if (_input.IsKeyPressed(Keys[i]))
                _actions.UpgradeStat((StatKind)i);

        if (_input.IsKeyPressed(ClientKey.Backspace))
            _actions.UndoUpgrade();
    }
}
