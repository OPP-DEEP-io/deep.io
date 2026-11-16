using System.Numerics;
using DeepIo.Client.Hud;
using DeepIo.Client.Rendering;
using DeepIo.Shared;

namespace DeepIo.Client.Diagnostics;

/// <summary>
/// Mediator demonstration without a window or a server: scripted key presses go into the HUD
/// and the requests it would send are recorded and checked.
/// </summary>
public static class HudDemo
{
    private const int MyId = 7;
    private static int _checks;

    public static void Run()
    {
        _checks = 0;
        Console.WriteLine("MEDIATOR: HUD components coordinated by GameplayHudMediator");
        var input = new FakeInput();
        var server = new FakeActions();
        var hud = new GameplayHudMediator(input, server);
        var camera = new Camera { ScreenWidth = 1920, ScreenHeight = 1080 };
        var state = TankLifeState.Alive;

        void Frame()
        {
            var snapshot = new Snapshot
            {
                Entities = [new EntityDto { Id = MyId, Kind = "tank", St = (int)state, StT = 2.5f }],
            };
            hud.Update(new HudFrame(snapshot, MyId, camera, Vector2.Zero, 1f / 30f));
            input.EndFrame();
        }


        input.Hold(ClientKey.D);
        Frame();
        Check(server.Inputs[^1].MoveX == 1f, "Holding D drives the tank");
        input.Press(ClientKey.One);
        Frame();
        Check(server.Upgrades == 1, "Key 1 spends a point");

        Step("Press ` : the console opens");
        input.Press(ClientKey.Grave);
        input.Type("`");
        Frame();
        Check(hud.ConsolePanel.IsOpen && hud.ConsolePanel.Text == "", "Console is open and the ` was not typed");
        Check(hud.TankInput.IsSuspended && server.Inputs[^1] is { MoveX: 0f, Fire: false },
            "Mediator suspended the tank input and sent a stop");
        int sent = server.Inputs.Count;

        input.Type("kill me");
        input.Press(ClientKey.One);
        Frame();
        input.Press(ClientKey.Backspace);
        Frame();
        Check(hud.ConsolePanel.Text == "kill m", "Typing (and Backspace) go to the console");
        Check(server.Inputs.Count == sent && server.Upgrades == 1 && server.Undos == 0,
            "...not to the tank, the upgrade keys or the upgrade undo");

        input.Type("e");
        Frame();
        input.Press(ClientKey.Enter);
        Frame();
        Check(server.ConsoleLines.SequenceEqual(["kill me"]), "Enter: the mediator sends the line to the server");

        server.Replies.Enqueue("destroyed: Tomas (Lv 1)");
        Frame();
        Check(hud.ConsolePanel.Lines.SequenceEqual(["> kill me", "destroyed: Tomas (Lv 1)"]),
            "Mediator routes the server's reply into the console");

        Step("The tank dies while the console is open");
        state = TankLifeState.Dead;
        Frame();
        Check(hud.DeathOverlay.IsShowing && hud.ConsolePanel.IsOpen,
            "Death overlay noticed the Dead state; the console stays usable");

        Step("Esc closes the console while dead");
        input.Press(ClientKey.Escape);
        Frame();
        Check(!hud.ConsolePanel.IsOpen && server.Leaves == 0, "Esc only closes the console; it does not leave");
        Check(hud.TankInput.IsSuspended && !hud.UpgradeHotkeys.Enabled,
            "Still dead, so the mediator keeps input and hotkeys off");

        Step("Respawn");
        state = TankLifeState.SpawnProtected;
        Frame();
        Check(!hud.DeathOverlay.IsShowing && !hud.TankInput.IsSuspended && hud.UpgradeHotkeys.Enabled,
            "Respawned with the console closed: control is back");
        Frame();
        Check(server.Inputs[^1].MoveX == 1f, "D drives the tank again");

        Step("Respawn while the console is open");
        input.Press(ClientKey.Grave);
        Frame();
        state = TankLifeState.Dead;
        Frame();
        state = TankLifeState.Alive;
        Frame();
        Check(hud.TankInput.IsSuspended, "Respawning does not steal the keyboard from an open console");
        input.Press(ClientKey.Grave);
        Frame();
        Check(!hud.TankInput.IsSuspended && hud.UpgradeHotkeys.Enabled, "Closing it gives control back");

        Step("Esc with the console closed");
        input.Press(ClientKey.Escape);
        Frame();
        Check(server.Leaves == 1, "Esc leaves for the join screen");

        Console.WriteLine();
        Console.WriteLine($"All {_checks} checks passed.");
    }

    private static void Step(string text) => Console.WriteLine("  " + text);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {message}");
        _checks++;
        Console.WriteLine($"    ok  {message}");
    }

    /// <summary>Keyboard state for one frame: held keys stay down, pressed keys and typed text last one frame.</summary>
    private sealed class FakeInput : IInputSource
    {
        private readonly HashSet<ClientKey> _down = new();
        private readonly HashSet<ClientKey> _pressed = new();
        private readonly Queue<int> _chars = new();

        public Vector2 MousePosition => new(1100, 540);

        public void Hold(ClientKey key) => _down.Add(key);

        public void Press(ClientKey key) => _pressed.Add(key);

        public void Type(string text)
        {
            foreach (char c in text) _chars.Enqueue(c);
        }

        public void EndFrame()
        {
            _pressed.Clear();
            _chars.Clear();
        }

        public bool IsKeyDown(ClientKey key) => _down.Contains(key);
        public bool IsKeyPressed(ClientKey key) => _pressed.Contains(key);
        public bool IsKeyPressedRepeat(ClientKey key) => false;
        public bool IsPrimaryMouseButtonDown() => false;
        public bool IsPrimaryMouseButtonPressed() => false;
        public int GetCharPressed() => _chars.Count > 0 ? _chars.Dequeue() : 0;
    }

    /// <summary>Records what the HUD asks the server to do.</summary>
    private sealed class FakeActions : IGameActions
    {
        public List<InputMessage> Inputs { get; } = new();
        public List<string> ConsoleLines { get; } = new();
        public Queue<string> Replies { get; } = new();
        public int Upgrades { get; private set; }
        public int Undos { get; private set; }
        public int Leaves { get; private set; }

        public void SendInput(InputMessage input) => Inputs.Add(input);
        public void UpgradeStat(StatKind stat) => Upgrades++;
        public void UndoUpgrade() => Undos++;
        public void ExecuteConsole(string line) => ConsoleLines.Add(line);
        public bool TryTakeConsoleOutput(out string text) => Replies.TryDequeue(out text!);
        public void LeaveGame() => Leaves++;
    }
}
