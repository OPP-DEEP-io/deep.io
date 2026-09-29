using System.Globalization;
using System.Numerics;
using DeepIo.Shared;
using DeepIo.Client.Rendering;

namespace DeepIo.Client.Ui;

/// <summary>Collects connection details before the client contacts the game server.</summary>
public sealed class JoinScreen
{
    private readonly IRenderBackend _backend;

    private const int MaxNameLength = 20;
    private const int MaxUrlLength = 200;
    private const int FieldFontSize = 24;
    private const int LabelFontSize = 18;
    private const int BuildFontSize = 20;

    /// <summary>
    /// The builds offered to the player. Each entry names one Abstract Factory on the server;
    /// the client knows the label only, never the parts behind it.
    /// </summary>
    private static readonly (TankArchetype Archetype, string Label, string Blurb)[] Archetypes =
    {
        (TankArchetype.Basic, "BASIC", "balanced hull, steady single shots"),
        (TankArchetype.Sniper, "SNIPER", "heavy hull, slow long-range rounds"),
        (TankArchetype.MachineGun, "MACHINE GUN", "fast hull, rapid spray, low damage"),
    };

    private static readonly RenderColor Background = new(24, 25, 34, 255);
    private static readonly RenderColor Panel = new(35, 37, 49, 255);
    private static readonly RenderColor Field = new(26, 28, 38, 255);
    private static readonly RenderColor Border = new(78, 82, 103, 255);
    private static readonly RenderColor Accent = new(75, 156, 232, 255);
    private static readonly RenderColor AccentHover = new(91, 171, 245, 255);
    private static readonly RenderColor Muted = new(154, 158, 178, 255);
    private static readonly RenderColor Error = new(238, 113, 113, 255);

    private string _playerName;
    private string _serverUrl;
    private int _nameCaret;
    private int _urlCaret;
    private int _nameViewStart;
    private int _urlViewStart;
    private string? _error;
    private ActiveField _activeField = ActiveField.PlayerName;
    private int _archetypeIndex;

    public JoinScreen(string playerName, string serverUrl, IRenderBackend backend)
    {
        _backend = backend;
        _playerName = Truncate(playerName, MaxNameLength);
        _serverUrl = Truncate(serverUrl, MaxUrlLength);
        _nameCaret = _playerName.Length;
        _urlCaret = _serverUrl.Length;
    }

    /// <returns>True when the player clicked Join or pressed Enter.</returns>
    public bool Update(bool isConnecting)
    {
        if (isConnecting)
            return false;

        Layout layout = GetLayout();
        Vector2 mouse = _backend.MousePosition;

        if (_backend.IsPrimaryMouseButtonPressed())
        {
            if (layout.NameField.Contains(mouse))
            {
                _activeField = ActiveField.PlayerName;
                _nameCaret = CaretAtClick(_playerName, ref _nameViewStart, _nameCaret, layout.NameField, mouse.X);
            }
            else if (layout.UrlField.Contains(mouse))
            {
                _activeField = ActiveField.ServerUrl;
                _urlCaret = CaretAtClick(_serverUrl, ref _urlViewStart, _urlCaret, layout.UrlField, mouse.X);
            }
            else if (layout.JoinButton.Contains(mouse))
                return true;

            for (int i = 0; i < Archetypes.Length; i++)
            {
                if (ArchetypeButton(layout, i).Contains(mouse))
                    _archetypeIndex = i;
            }
        }

        if (PressedOrRepeated(ClientKey.Down))
            _archetypeIndex = (_archetypeIndex + 1) % Archetypes.Length;
        if (PressedOrRepeated(ClientKey.Up))
            _archetypeIndex = (_archetypeIndex + Archetypes.Length - 1) % Archetypes.Length;

        if (_backend.IsKeyPressed(ClientKey.Tab))
            _activeField = _activeField == ActiveField.PlayerName
                ? ActiveField.ServerUrl
                : ActiveField.PlayerName;

        ref string value = ref ActiveValue();
        ref int caret = ref ActiveCaret();
        int maxLength = _activeField == ActiveField.PlayerName ? MaxNameLength : MaxUrlLength;

        if (PressedOrRepeated(ClientKey.Left))
            caret = PreviousBoundary(value, caret);
        if (PressedOrRepeated(ClientKey.Right))
            caret = NextBoundary(value, caret);
        if (_backend.IsKeyPressed(ClientKey.Home))
            caret = 0;
        if (_backend.IsKeyPressed(ClientKey.End))
            caret = value.Length;

        if (PressedOrRepeated(ClientKey.Backspace) && caret > 0)
        {
            int start = PreviousBoundary(value, caret);
            value = value.Remove(start, caret - start);
            caret = start;
            _error = null;
        }
        if (PressedOrRepeated(ClientKey.Delete) && caret < value.Length)
        {
            value = value.Remove(caret, NextBoundary(value, caret) - caret);
            _error = null;
        }

        int codepoint;
        while ((codepoint = _backend.GetCharPressed()) > 0)
        {
            string character = char.ConvertFromUtf32(codepoint);
            if (!char.IsControl(character, 0) && value.Length + character.Length <= maxLength)
            {
                value = value.Insert(caret, character);
                caret += character.Length;
                _error = null;
            }
        }

        return _backend.IsKeyPressed(ClientKey.Enter);
    }

    /// <summary>The build the player picked, passed straight through to the server's Join call.</summary>
    public TankArchetype SelectedArchetype => Archetypes[_archetypeIndex].Archetype;

    public bool TryGetConnectionSettings(out string playerName, out string serverUrl)
    {
        playerName = _playerName.Trim();
        serverUrl = _serverUrl.Trim();

        if (playerName.Length == 0)
        {
            _error = "Enter a player name.";
            _activeField = ActiveField.PlayerName;
            return false;
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _error = "Enter a valid http:// or https:// server URL.";
            _activeField = ActiveField.ServerUrl;
            return false;
        }

        _playerName = playerName;
        _serverUrl = serverUrl;
        _nameCaret = Math.Min(_nameCaret, _playerName.Length);
        _urlCaret = Math.Min(_urlCaret, _serverUrl.Length);
        return true;
    }

    public void SetError(string message) => _error = message;

    public void ClearError() => _error = null;

    public void Draw(bool isConnecting)
    {
        Layout layout = GetLayout();
        Vector2 mouse = _backend.MousePosition;
        bool buttonHovered = !isConnecting && layout.JoinButton.Contains(mouse);

        _backend.BeginFrame();
        _backend.Clear(Background);

        DrawBackdrop();
        _backend.FillRoundedRectangle(layout.Panel, 0.08f, 10, Panel);
        _backend.OutlineRoundedRectangle(layout.Panel, 0.08f, 10, 1f, Border);

        DrawCentred("deep.io", (int)layout.Panel.Y + 44, 42, RenderColor.White, RenderTextStyle.Title);
        DrawCentred("Enter the arena", (int)layout.Panel.Y + 96, 24, Muted);

        DrawLabel("PLAYER NAME", layout.NameField);
        DrawField(layout.NameField, _playerName, _nameCaret, ref _nameViewStart,
            _activeField == ActiveField.PlayerName && !isConnecting);

        DrawLabel("SERVER URL", layout.UrlField);
        DrawField(layout.UrlField, _serverUrl, _urlCaret, ref _urlViewStart,
            _activeField == ActiveField.ServerUrl && !isConnecting);

        DrawLabel("TANK BUILD", layout.ArchetypeRow);
        for (int i = 0; i < Archetypes.Length; i++)
            DrawArchetypeButton(ArchetypeButton(layout, i), i, mouse, isConnecting);

        DrawCentred(Archetypes[_archetypeIndex].Blurb,
            (int)(layout.ArchetypeRow.Y + layout.ArchetypeRow.Height + 10), 19, Muted);

        RenderColor buttonColor = buttonHovered ? AccentHover : Accent;
        if (isConnecting) buttonColor = new RenderColor(68, 91, 116, 255);
        _backend.FillRoundedRectangle(layout.JoinButton, 0.18f, 8, buttonColor);
        DrawCentred(isConnecting ? "CONNECTING..." : "JOIN GAME",
            (int)layout.JoinButton.Y + 12, 25, RenderColor.White);

        if (_error is not null)
            DrawCentred(FitText(_error, 480, 19), (int)layout.JoinButton.Y + 70, 19, Error);
        else
            DrawCentred(isConnecting ? "Contacting the game server" : "Tab fields  |  Left/Right caret  |  Up/Down build",
                (int)layout.JoinButton.Y + 70, 18, Muted);

        _backend.EndFrame();
    }

    /// <summary>One third of the build row, with a small gutter between buttons.</summary>
    private static RenderRect ArchetypeButton(Layout layout, int index)
    {
        const float gap = 10f;
        float width = (layout.ArchetypeRow.Width - gap * (Archetypes.Length - 1)) / Archetypes.Length;
        return new RenderRect(
            layout.ArchetypeRow.X + index * (width + gap),
            layout.ArchetypeRow.Y,
            width,
            layout.ArchetypeRow.Height);
    }

    private void DrawArchetypeButton(RenderRect bounds, int index, Vector2 mouse, bool isConnecting)
    {
        bool selected = index == _archetypeIndex;
        bool hovered = !isConnecting && bounds.Contains(mouse);

        RenderColor fill = selected ? new RenderColor(45, 74, 105, 255) : Field;
        if (hovered && !selected) fill = new RenderColor(34, 37, 50, 255);

        _backend.FillRoundedRectangle(bounds, 0.18f, 8, fill);
        _backend.OutlineRoundedRectangle(bounds, 0.18f, 8, selected ? 2f : 1f, selected ? Accent : Border);

        string label = FitText(Archetypes[index].Label, (int)bounds.Width - 12, BuildFontSize);
        int textX = (int)(bounds.X + (bounds.Width - _backend.MeasureText(label, BuildFontSize)) * 0.5f);
        int textY = (int)(bounds.Y + (bounds.Height - BuildFontSize) * 0.5f);
        _backend.DrawText(label, textX, textY, BuildFontSize, selected ? RenderColor.White : Muted);
    }

    private ref string ActiveValue()
    {
        if (_activeField == ActiveField.PlayerName)
            return ref _playerName;
        return ref _serverUrl;
    }

    private ref int ActiveCaret()
    {
        if (_activeField == ActiveField.PlayerName)
            return ref _nameCaret;
        return ref _urlCaret;
    }

    private bool PressedOrRepeated(ClientKey key) =>
        _backend.IsKeyPressed(key) || _backend.IsKeyPressedRepeat(key);

    private void DrawField(RenderRect bounds, string value, int caret, ref int viewStart, bool active)
    {
        _backend.FillRoundedRectangle(bounds, 0.12f, 8, Field);
        _backend.OutlineRoundedRectangle(bounds, 0.12f, 8, active ? 2f : 1f, active ? Accent : Border);

        (int start, int end) = VisibleRange(value, caret, ref viewStart, (int)bounds.Width - 34);
        string visibleValue = value[start..end];
        int textY = (int)(bounds.Y + (bounds.Height - FieldFontSize) * 0.5f);
        _backend.DrawText(visibleValue, (int)bounds.X + 16, textY, FieldFontSize, RenderColor.White);

        if (active && (int)(_backend.Time * 2) % 2 == 0)
        {
            int cursorX = (int)bounds.X + 16 + _backend.MeasureText(value[start..caret], FieldFontSize) + 1;
            _backend.FillRectangle(new RenderRect(cursorX, (int)bounds.Y + 15, 2, (int)bounds.Height - 30), Accent);
        }
    }

    private void DrawLabel(string text, RenderRect field)
    {
        _backend.DrawText(text, (int)field.X, (int)field.Y - 28, LabelFontSize, Muted);
    }

    private void DrawCentred(string text, int y, int fontSize, RenderColor color,
        RenderTextStyle style = RenderTextStyle.Ui)
    {
        int x = (_backend.ScreenWidth - _backend.MeasureText(text, fontSize, style)) / 2;
        _backend.DrawText(text, x, y, fontSize, color, style);
    }

    private void DrawBackdrop()
    {
        int width = _backend.ScreenWidth;
        int height = _backend.ScreenHeight;
        const int step = 64;
        RenderColor grid = new(32, 34, 45, 255);

        for (int x = 0; x < width; x += step)
            _backend.DrawLine(new Vector2(x, 0), new Vector2(x, height), 1f, grid);
        for (int y = 0; y < height; y += step)
            _backend.DrawLine(new Vector2(0, y), new Vector2(width, y), 1f, grid);

        _backend.DrawCircle(new Vector2(width / 2 - 290, height / 2 - 210), 58, new RenderColor(214, 178, 70, 28));
        _backend.DrawPolygon(new Vector2(width / 2 + 310, height / 2 + 210), 3, 74, 12,
            new RenderColor(225, 101, 108, 24));
    }

    private Layout GetLayout()
    {
        const float panelWidth = 560;
        const float panelHeight = 620;
        float panelX = (_backend.ScreenWidth - panelWidth) * 0.5f;
        float panelY = (_backend.ScreenHeight - panelHeight) * 0.5f;

        return new Layout(
            new RenderRect(panelX, panelY, panelWidth, panelHeight),
            new RenderRect(panelX + 40, panelY + 160, panelWidth - 80, 54),
            new RenderRect(panelX + 40, panelY + 258, panelWidth - 80, 54),
            new RenderRect(panelX + 40, panelY + 356, panelWidth - 80, 44),
            new RenderRect(panelX + 40, panelY + 450, panelWidth - 80, 54));
    }

    private string FitText(string value, int maxWidth, int fontSize)
    {
        if (_backend.MeasureText(value, fontSize) <= maxWidth)
            return value;

        const string suffix = "...";
        while (value.Length > 0 && _backend.MeasureText(value + suffix, fontSize) > maxWidth)
            value = value[..^1];
        return value + suffix;
    }

    private (int Start, int End) VisibleRange(string value, int caret, ref int viewStart, int maxWidth)
    {
        if (_backend.MeasureText(value, FieldFontSize) <= maxWidth)
            viewStart = 0;
        if (viewStart > caret)
            viewStart = caret;

        while (viewStart < caret && _backend.MeasureText(value[viewStart..caret], FieldFontSize) > maxWidth)
            viewStart = NextBoundary(value, viewStart);

        int end = viewStart;
        while (end < value.Length)
        {
            int next = NextBoundary(value, end);
            if (_backend.MeasureText(value[viewStart..next], FieldFontSize) > maxWidth)
                break;
            end = next;
        }

        return (viewStart, end);
    }

    private int CaretAtClick(string value, ref int viewStart, int caret, RenderRect bounds, float mouseX)
    {
        (int start, int end) = VisibleRange(value, caret, ref viewStart, (int)bounds.Width - 34);
        float x = bounds.X + 16;
        for (int index = start; index < end;)
        {
            int next = NextBoundary(value, index);
            int leftWidth = _backend.MeasureText(value[start..index], FieldFontSize);
            int rightWidth = _backend.MeasureText(value[start..next], FieldFontSize);
            if (mouseX < x + (leftWidth + rightWidth) / 2f)
                return index;
            index = next;
        }
        return end;
    }

    private static int PreviousBoundary(string value, int index)
    {
        int previous = 0;
        foreach (int start in StringInfo.ParseCombiningCharacters(value))
        {
            if (start >= index)
                break;
            previous = start;
        }
        return previous;
    }

    private static int NextBoundary(string value, int index)
    {
        foreach (int start in StringInfo.ParseCombiningCharacters(value))
        {
            if (start > index)
                return start;
        }
        return value.Length;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        int end = 0;
        foreach (int start in StringInfo.ParseCombiningCharacters(value))
        {
            if (start > maxLength)
                break;
            end = start;
        }
        return value[..end];
    }

    private enum ActiveField
    {
        PlayerName,
        ServerUrl,
    }

    private readonly record struct Layout(
        RenderRect Panel,
        RenderRect NameField,
        RenderRect UrlField,
        RenderRect ArchetypeRow,
        RenderRect JoinButton);
}
