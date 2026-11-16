using System.Text;
using DeepIo.Client.Rendering;

namespace DeepIo.Client.Hud;

/// <summary>
/// Colleague: the in-game console. ` or Enter opens it, Esc or ` closes it, Enter sends the
/// typed line and Up recalls the previous one. It only reports what happened; sending the line
/// and freeing the keyboard from the game is the mediator's job.
/// </summary>
public sealed class ConsolePanel : HudComponent
{
    public const int MaxLength = 200;
    private const int MaxLines = 14;
    private const int FontSize = 18;
    private const int LineHeight = 22;

    private static readonly RenderColor Back = new(18, 19, 27, 225);
    private static readonly RenderColor Edge = new(78, 82, 103, 255);
    private static readonly RenderColor Output = new(200, 202, 215, 255);
    private static readonly RenderColor Prompt = new(120, 190, 250, 255);

    private readonly IInputSource _input;
    private readonly List<string> _lines = new();
    private readonly StringBuilder _text = new();

    public ConsolePanel(IHudMediator mediator, IInputSource input) : base(mediator) => _input = input;

    public bool IsOpen { get; private set; }

    /// <summary>The line most recently sent with Enter.</summary>
    public string LastSubmitted { get; private set; } = "";

    public string Text => _text.ToString();

    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Adds output (possibly several lines) to the scrollback.</summary>
    public void AppendLine(string text)
    {
        _lines.AddRange(text.Split('\n'));
        if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
    }

    public override void Update(HudFrame frame)
    {
        if (!IsOpen)
        {
            if (_input.IsKeyPressed(ClientKey.Grave) || _input.IsKeyPressed(ClientKey.Enter))
                Open();
            return;
        }

        if (_input.IsKeyPressed(ClientKey.Escape) || _input.IsKeyPressed(ClientKey.Grave))
        {
            Close();
            return;
        }

        if (_input.IsKeyPressed(ClientKey.Enter))
        {
            Submit();
            return;
        }

        if (_input.IsKeyPressed(ClientKey.Up) && LastSubmitted.Length > 0)
            _text.Clear().Append(LastSubmitted);

        if ((_input.IsKeyPressed(ClientKey.Backspace) || _input.IsKeyPressedRepeat(ClientKey.Backspace)) && _text.Length > 0)
            _text.Length -= _text.Length >= 2 && char.IsLowSurrogate(_text[^1]) ? 2 : 1;

        for (int c = _input.GetCharPressed(); c != 0; c = _input.GetCharPressed())
            if (c >= ' ' && c != '`' && _text.Length < MaxLength)
                _text.Append(char.ConvertFromUtf32(c));
    }

    private void Open()
    {
        IsOpen = true;
        _text.Clear();
        while (_input.GetCharPressed() != 0) { }   // drop the ` that opened the console
        Mediator.Notify(this, HudEvent.ConsoleOpened);
    }

    private void Close()
    {
        IsOpen = false;
        while (_input.GetCharPressed() != 0) { }
        Mediator.Notify(this, HudEvent.ConsoleClosed);
    }

    private void Submit()
    {
        string line = _text.ToString().Trim();
        if (line.Length == 0)
        {
            Close();
            return;
        }

        LastSubmitted = line;
        _text.Clear();
        Mediator.Notify(this, HudEvent.ConsoleSubmitted);
    }

    public override void Draw(IRenderBackend backend)
    {
        if (!IsOpen) return;

        int width = Math.Min(900, backend.ScreenWidth - 40);
        int height = LineHeight * (MaxLines + 1) + 24;
        var bounds = new RenderRect((backend.ScreenWidth - width) / 2f, backend.ScreenHeight - height - 20, width, height);
        backend.FillRoundedRectangle(bounds, 0.04f, 8, Back);
        backend.OutlineRoundedRectangle(bounds, 0.04f, 8, 1.5f, Edge);

        int x = (int)bounds.X + 14;
        int y = (int)bounds.Y + 10 + LineHeight * (MaxLines - _lines.Count);
        foreach (string line in _lines)
        {
            backend.DrawText(line, x, y, FontSize, line.StartsWith('>') ? Prompt : Output);
            y += LineHeight;
        }

        bool caret = backend.Time % 1.0 < 0.5;
        backend.DrawText($"> {_text}{(caret ? "_" : "")}", x, (int)(bounds.Y + bounds.Height) - LineHeight - 8, FontSize, RenderColor.White);
    }
}
