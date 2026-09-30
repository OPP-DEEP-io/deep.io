using DeepIo.Client.Net;
using DeepIo.Client.Rendering;

namespace DeepIo.Client.Ui;

/// <summary>Join-screen client of the session facade.</summary>
public sealed class JoinScreenController
{
    private readonly JoinScreen _screen;
    private readonly ClientSessionFacade _session;
    private string? _seenSessionError;

    public JoinScreenController(string playerName, string serverUrl,
        IRenderBackend backend, ClientSessionFacade session)
    {
        _screen = new JoinScreen(playerName, serverUrl, backend);
        _session = session;
    }

    public void RunFrame()
    {
        bool connecting = _session.State == ClientSessionState.Connecting;
        if (_session.Error is null)
            _seenSessionError = null;
        else if (_session.State == ClientSessionState.JoinScreen && _seenSessionError != _session.Error)
        {
            _screen.SetError($"Could not join: {_session.Error}");
            _seenSessionError = _session.Error;
        }

        if (_screen.Update(connecting) && !connecting &&
            _screen.TryGetConnectionSettings(out string playerName, out string serverUrl))
        {
            _screen.ClearError();
            _session.BeginJoin(playerName, serverUrl, _screen.SelectedArchetype);
        }

        _screen.Draw(_session.State == ClientSessionState.Connecting);
    }
}
