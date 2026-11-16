using System.Numerics;
using DeepIo.Client.Rendering;
using DeepIo.Shared;

namespace DeepIo.Client.Hud;

/// <summary>What a HUD component reports to the mediator. The mediator decides what it means for the others.</summary>
public enum HudEvent { ConsoleOpened, ConsoleClosed, ConsoleSubmitted, PlayerDied, PlayerRespawned, LeaveRequested }

/// <summary>Everything a HUD component may look at during one frame.</summary>
public readonly record struct HudFrame(Snapshot? Snapshot, int PlayerId, Camera Camera, Vector2 PlayerPosition, float Dt)
{
    /// <summary>This client's own tank in the snapshot, if it is there.</summary>
    public EntityDto? Me()
    {
        if (Snapshot is null) return null;
        foreach (EntityDto e in Snapshot.Entities)
            if (e.Id == PlayerId && e.Kind == "tank")
                return e;
        return null;
    }
}

/// <summary>The requests HUD components send to the server. <c>ClientSessionFacade</c> implements it.</summary>
public interface IGameActions
{
    void SendInput(InputMessage input);
    void UpgradeStat(StatKind stat);
    void UndoUpgrade();
    void ExecuteConsole(string line);
    bool TryTakeConsoleOutput(out string text);

    /// <summary>Disconnects and returns to the join screen.</summary>
    void LeaveGame();
}

/// <summary>
/// Mediator. HUD components (colleagues) report what happened to them here and never talk to
/// each other; the mediator holds the rules for how one component's change affects the rest.
/// </summary>
public interface IHudMediator
{
    void Notify(HudComponent sender, HudEvent hudEvent);
}

/// <summary>
/// Colleague. Knows only its mediator, not the other components. <see cref="Update"/> holds the
/// logic and <see cref="Draw"/> the drawing, so the logic can run without a window.
/// </summary>
public abstract class HudComponent
{
    protected HudComponent(IHudMediator mediator) => Mediator = mediator;

    protected IHudMediator Mediator { get; }

    public abstract void Update(HudFrame frame);

    public virtual void Draw(IRenderBackend backend) { }
}

/// <summary>
/// Concrete mediator for the gameplay screen. It creates the five colleagues and is the only
/// place that knows how they depend on one another:
/// <list type="bullet">
/// <item>console open: the tank stops, keys 1-8, Backspace and Esc stop meaning "game";</item>
/// <item>tank destroyed: input and hotkeys stop, but the console and Esc still work;</item>
/// <item>console closed or respawned: control comes back only if neither is still in the way;</item>
/// <item>a submitted line goes to the server, and every reply goes back into the console;</item>
/// <item>Esc (console closed): leave for the join screen.</item>
/// </list>
/// </summary>
public sealed class GameplayHudMediator : IHudMediator
{
    private readonly IGameActions _actions;

    public GameplayHudMediator(IInputSource input, IGameActions actions)
    {
        _actions = actions;
        LeaveHotkey = new LeaveHotkey(this, input);
        ConsolePanel = new ConsolePanel(this, input);
        DeathOverlay = new DeathOverlay(this);
        UpgradeHotkeys = new UpgradeHotkeys(this, input, actions);
        TankInput = new TankInputComponent(this, input, actions);
    }

    public LeaveHotkey LeaveHotkey { get; }
    public ConsolePanel ConsolePanel { get; }
    public DeathOverlay DeathOverlay { get; }
    public UpgradeHotkeys UpgradeHotkeys { get; }
    public TankInputComponent TankInput { get; }

    /// <summary>
    /// Esc is checked before the console, so the Esc that closes the console is not also read
    /// as "leave". Console and death overlay go before the hotkeys and the tank input, so
    /// whatever they report already applies to those in the same frame.
    /// </summary>
    public void Update(HudFrame frame)
    {
        LeaveHotkey.Update(frame);
        ConsolePanel.Update(frame);
        DeathOverlay.Update(frame);
        UpgradeHotkeys.Update(frame);
        TankInput.Update(frame);

        while (_actions.TryTakeConsoleOutput(out string text))
            ConsolePanel.AppendLine(text);
    }

    public void Draw(IRenderBackend backend)
    {
        DeathOverlay.Draw(backend);
        ConsolePanel.Draw(backend);
    }

    public void Notify(HudComponent sender, HudEvent hudEvent)
    {
        switch (hudEvent)
        {
            case HudEvent.ConsoleOpened:
                TankInput.Suspend();
                UpgradeHotkeys.Enabled = false;
                LeaveHotkey.Enabled = false;
                break;

            case HudEvent.PlayerDied:
                TankInput.Suspend();
                UpgradeHotkeys.Enabled = false;
                break;

            case HudEvent.ConsoleClosed:
            case HudEvent.PlayerRespawned:
                LeaveHotkey.Enabled = !ConsolePanel.IsOpen;
                if (!ConsolePanel.IsOpen && !DeathOverlay.IsShowing)
                {
                    TankInput.Resume();
                    UpgradeHotkeys.Enabled = true;
                }
                break;

            case HudEvent.ConsoleSubmitted:
                ConsolePanel.AppendLine("> " + ConsolePanel.LastSubmitted);
                _actions.ExecuteConsole(ConsolePanel.LastSubmitted);
                break;

            case HudEvent.LeaveRequested:
                _actions.LeaveGame();
                break;
        }
    }
}
