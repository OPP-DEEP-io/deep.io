using System.Numerics;
using DeepIo.Shared;
using Raylib_cs;

namespace DeepIo.Client.Ui;

/// <summary>Collects connection details before the client contacts the game server.</summary>
public sealed class JoinScreen
{
    private const int MaxNameLength = 20;
    private const int MaxUrlLength = 200;

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

    private static readonly Color Background = new(24, 25, 34, 255);
    private static readonly Color Panel = new(35, 37, 49, 255);
    private static readonly Color Field = new(26, 28, 38, 255);
    private static readonly Color Border = new(78, 82, 103, 255);
    private static readonly Color Accent = new(75, 156, 232, 255);
    private static readonly Color AccentHover = new(91, 171, 245, 255);
    private static readonly Color Muted = new(154, 158, 178, 255);
    private static readonly Color Error = new(238, 113, 113, 255);

    private string _playerName;
    private string _serverUrl;
    private string? _error;
    private ActiveField _activeField = ActiveField.PlayerName;
    private int _archetypeIndex;

    public JoinScreen(string playerName, string serverUrl)
    {
        _playerName = Truncate(playerName, MaxNameLength);
        _serverUrl = Truncate(serverUrl, MaxUrlLength);
    }

    /// <returns>True when the player clicked Join or pressed Enter.</returns>
    public bool Update(bool isConnecting)
    {
        if (isConnecting)
            return false;

        Layout layout = GetLayout();
        Vector2 mouse = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mouse, layout.NameField))
                _activeField = ActiveField.PlayerName;
            else if (Raylib.CheckCollisionPointRec(mouse, layout.UrlField))
                _activeField = ActiveField.ServerUrl;
            else if (Raylib.CheckCollisionPointRec(mouse, layout.JoinButton))
                return true;

            for (int i = 0; i < Archetypes.Length; i++)
            {
                if (Raylib.CheckCollisionPointRec(mouse, ArchetypeButton(layout, i)))
                    _archetypeIndex = i;
            }
        }

        // Arrow keys cycle the build without stealing characters from the text fields.
        if (Raylib.IsKeyPressed(KeyboardKey.Right))
            _archetypeIndex = (_archetypeIndex + 1) % Archetypes.Length;
        if (Raylib.IsKeyPressed(KeyboardKey.Left))
            _archetypeIndex = (_archetypeIndex + Archetypes.Length - 1) % Archetypes.Length;

        if (Raylib.IsKeyPressed(KeyboardKey.Tab))
            _activeField = _activeField == ActiveField.PlayerName
                ? ActiveField.ServerUrl
                : ActiveField.PlayerName;

        ref string value = ref ActiveValue();
        int maxLength = _activeField == ActiveField.PlayerName ? MaxNameLength : MaxUrlLength;

        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) ||
             Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && value.Length > 0)
        {
            value = value[..^1];
            _error = null;
        }

        int codepoint;
        while ((codepoint = Raylib.GetCharPressed()) > 0)
        {
            string character = char.ConvertFromUtf32(codepoint);
            if (!char.IsControl(character, 0) && value.Length + character.Length <= maxLength)
            {
                value += character;
                _error = null;
            }
        }

        return Raylib.IsKeyPressed(KeyboardKey.Enter);
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
        return true;
    }

    public void SetError(string message) => _error = message;

    public void ClearError() => _error = null;

    public void Draw(bool isConnecting)
    {
        Layout layout = GetLayout();
        Vector2 mouse = Raylib.GetMousePosition();
        bool buttonHovered = !isConnecting && Raylib.CheckCollisionPointRec(mouse, layout.JoinButton);

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Background);

        DrawBackdrop();
        Raylib.DrawRectangleRounded(layout.Panel, 0.08f, 10, Panel);
        Raylib.DrawRectangleRoundedLinesEx(layout.Panel, 0.08f, 10, 1f, Border);

        DrawCentred("deep.io", (int)layout.Panel.Y + 44, 42, Color.RayWhite);
        DrawCentred("Enter the arena", (int)layout.Panel.Y + 96, 19, Muted);

        DrawLabel("PLAYER NAME", layout.NameField);
        DrawField(layout.NameField, _playerName, _activeField == ActiveField.PlayerName && !isConnecting);

        DrawLabel("SERVER URL", layout.UrlField);
        DrawField(layout.UrlField, _serverUrl, _activeField == ActiveField.ServerUrl && !isConnecting);

        DrawLabel("TANK BUILD", layout.ArchetypeRow);
        for (int i = 0; i < Archetypes.Length; i++)
            DrawArchetypeButton(ArchetypeButton(layout, i), i, mouse, isConnecting);

        DrawCentred(Archetypes[_archetypeIndex].Blurb,
            (int)(layout.ArchetypeRow.Y + layout.ArchetypeRow.Height + 10), 15, Muted);

        Color buttonColor = buttonHovered ? AccentHover : Accent;
        if (isConnecting) buttonColor = new Color(68, 91, 116, 255);
        Raylib.DrawRectangleRounded(layout.JoinButton, 0.18f, 8, buttonColor);
        DrawCentred(isConnecting ? "CONNECTING..." : "JOIN GAME",
            (int)layout.JoinButton.Y + 15, 20, Color.RayWhite);

        if (_error is not null)
            DrawCentred(FitText(_error, 480, 16), (int)layout.JoinButton.Y + 70, 16, Error);
        else
            DrawCentred(isConnecting ? "Contacting the game server" : "Enter to join   |   arrows pick a build",
                (int)layout.JoinButton.Y + 70, 16, Muted);

        Raylib.EndDrawing();
    }

    /// <summary>One third of the build row, with a small gutter between buttons.</summary>
    private static Rectangle ArchetypeButton(Layout layout, int index)
    {
        const float gap = 10f;
        float width = (layout.ArchetypeRow.Width - gap * (Archetypes.Length - 1)) / Archetypes.Length;
        return new Rectangle(
            layout.ArchetypeRow.X + index * (width + gap),
            layout.ArchetypeRow.Y,
            width,
            layout.ArchetypeRow.Height);
    }

    private void DrawArchetypeButton(Rectangle bounds, int index, Vector2 mouse, bool isConnecting)
    {
        bool selected = index == _archetypeIndex;
        bool hovered = !isConnecting && Raylib.CheckCollisionPointRec(mouse, bounds);

        Color fill = selected ? new Color(45, 74, 105, 255) : Field;
        if (hovered && !selected) fill = new Color(34, 37, 50, 255);

        Raylib.DrawRectangleRounded(bounds, 0.18f, 8, fill);
        Raylib.DrawRectangleRoundedLinesEx(bounds, 0.18f, 8, selected ? 2f : 1f, selected ? Accent : Border);

        string label = FitText(Archetypes[index].Label, (int)bounds.Width - 12, 15);
        int textX = (int)(bounds.X + (bounds.Width - Raylib.MeasureText(label, 15)) * 0.5f);
        int textY = (int)(bounds.Y + (bounds.Height - 15) * 0.5f);
        Raylib.DrawText(label, textX, textY, 15, selected ? Color.RayWhite : Muted);
    }

    private ref string ActiveValue()
    {
        if (_activeField == ActiveField.PlayerName)
            return ref _playerName;
        return ref _serverUrl;
    }

    private static void DrawField(Rectangle bounds, string value, bool active)
    {
        Raylib.DrawRectangleRounded(bounds, 0.12f, 8, Field);
        Raylib.DrawRectangleRoundedLinesEx(bounds, 0.12f, 8, active ? 2f : 1f, active ? Accent : Border);

        string visibleValue = FitTextFromEnd(value, (int)bounds.Width - 32, 19);
        int textY = (int)(bounds.Y + (bounds.Height - 19) * 0.5f);
        Raylib.DrawText(visibleValue, (int)bounds.X + 16, textY, 19, Color.RayWhite);

        if (active && (int)(Raylib.GetTime() * 2) % 2 == 0)
        {
            int cursorX = (int)bounds.X + 16 + Raylib.MeasureText(visibleValue, 19) + 1;
            Raylib.DrawRectangle(cursorX, (int)bounds.Y + 15, 2, (int)bounds.Height - 30, Accent);
        }
    }

    private static void DrawLabel(string text, Rectangle field)
    {
        Raylib.DrawText(text, (int)field.X, (int)field.Y - 25, 14, Muted);
    }

    private static void DrawCentred(string text, int y, int fontSize, Color color)
    {
        int x = (Raylib.GetScreenWidth() - Raylib.MeasureText(text, fontSize)) / 2;
        Raylib.DrawText(text, x, y, fontSize, color);
    }

    private static void DrawBackdrop()
    {
        int width = Raylib.GetScreenWidth();
        int height = Raylib.GetScreenHeight();
        const int step = 64;
        Color grid = new(32, 34, 45, 255);

        for (int x = 0; x < width; x += step)
            Raylib.DrawLine(x, 0, x, height, grid);
        for (int y = 0; y < height; y += step)
            Raylib.DrawLine(0, y, width, y, grid);

        Raylib.DrawCircle(width / 2 - 290, height / 2 - 210, 58, new Color(214, 178, 70, 28));
        Raylib.DrawPoly(new Vector2(width / 2 + 310, height / 2 + 210), 3, 74, 12,
            new Color(225, 101, 108, 24));
    }

    private static Layout GetLayout()
    {
        const float panelWidth = 560;
        const float panelHeight = 620;
        float panelX = (Raylib.GetScreenWidth() - panelWidth) * 0.5f;
        float panelY = (Raylib.GetScreenHeight() - panelHeight) * 0.5f;

        return new Layout(
            new Rectangle(panelX, panelY, panelWidth, panelHeight),
            new Rectangle(panelX + 40, panelY + 160, panelWidth - 80, 54),
            new Rectangle(panelX + 40, panelY + 258, panelWidth - 80, 54),
            new Rectangle(panelX + 40, panelY + 356, panelWidth - 80, 44),
            new Rectangle(panelX + 40, panelY + 450, panelWidth - 80, 54));
    }

    private static string FitText(string value, int maxWidth, int fontSize)
    {
        if (Raylib.MeasureText(value, fontSize) <= maxWidth)
            return value;

        const string suffix = "...";
        while (value.Length > 0 && Raylib.MeasureText(value + suffix, fontSize) > maxWidth)
            value = value[..^1];
        return value + suffix;
    }

    private static string FitTextFromEnd(string value, int maxWidth, int fontSize)
    {
        if (Raylib.MeasureText(value, fontSize) <= maxWidth)
            return value;

        const string prefix = "...";
        while (value.Length > 0 && Raylib.MeasureText(prefix + value, fontSize) > maxWidth)
            value = value[1..];
        return prefix + value;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private enum ActiveField
    {
        PlayerName,
        ServerUrl,
    }

    private readonly record struct Layout(
        Rectangle Panel,
        Rectangle NameField,
        Rectangle UrlField,
        Rectangle ArchetypeRow,
        Rectangle JoinButton);
}
