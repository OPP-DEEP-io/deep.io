using DeepIo.Client.Rendering;
using DeepIo.Shared;

namespace DeepIo.Client.Hud;

/// <summary>
/// Colleague: watches this client's own tank state in each snapshot (State pattern on the
/// server) and covers the screen with a respawn countdown while the tank is dead. Reports the
/// moment of death and of respawn to the mediator.
/// </summary>
public sealed class DeathOverlay : HudComponent
{
    private static readonly RenderColor Shade = new(10, 10, 16, 170);
    private static readonly RenderColor Muted = new(170, 172, 190, 255);

    private float _secondsLeft;

    public DeathOverlay(IHudMediator mediator) : base(mediator) { }

    public bool IsShowing { get; private set; }

    public override void Update(HudFrame frame)
    {
        EntityDto? me = frame.Me();
        if (me is null) return;   // no snapshot yet: keep whatever we last knew

        bool dead = (TankLifeState)me.St == TankLifeState.Dead;
        _secondsLeft = me.StT;
        if (dead == IsShowing) return;

        IsShowing = dead;
        Mediator.Notify(this, dead ? HudEvent.PlayerDied : HudEvent.PlayerRespawned);
    }

    public override void Draw(IRenderBackend backend)
    {
        if (!IsShowing) return;

        backend.FillRectangle(new RenderRect(0, 0, backend.ScreenWidth, backend.ScreenHeight), Shade);
        DrawCentred(backend, "You were destroyed", backend.ScreenHeight / 2 - 60, 48, RenderColor.White);
        DrawCentred(backend, $"Respawning in {Math.Max(0f, _secondsLeft):F1} s", backend.ScreenHeight / 2 + 4, 28, RenderColor.White);
        DrawCentred(backend, "Esc: back to the join screen   |   `: console", backend.ScreenHeight / 2 + 48, 20, Muted);
    }

    private static void DrawCentred(IRenderBackend backend, string text, int y, int size, RenderColor color)
    {
        int width = backend.MeasureText(text, size);
        backend.DrawText(text, (backend.ScreenWidth - width) / 2, y, size, color);
    }
}
