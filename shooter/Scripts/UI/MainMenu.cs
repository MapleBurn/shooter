using Godot;

namespace Shooter.Scripts.UI;

public partial class MainMenu : Control
{
    [Export] private Button _hostButton;
    [Export] private Button _joinButton;
    [Export] private LineEdit _codeInput;
    [Export] private LineEdit _codeLabel;

    private Node _mp;
    private const string WorldScenePath = "res://Scenes/world.tscn";

    public override void _Ready()
    {
        _mp = GetNode<Node>("/root/MultiplayerManager");
        _codeLabel.Visible = false;

        // Reset UI state (in case we're returning from a game session)
        _joinButton.Disabled = false;
        _hostButton.Disabled = false;
        _codeInput.Editable = true;
        _codeInput.Text = "";

        // Reset the noray helper's host flag so joining works again
        _mp.Set("is_host", false);

        var noray = GetNode<Node>("/root/Noray");

        // Check if Noray already has an OID (e.g., returning from a game)
        // If so, display it immediately so the user can host a new lobby.
        string existingOid = (string)noray.Get("oid");
        if (!string.IsNullOrEmpty(existingOid))
        {
            _codeLabel.Text = $"Kód: {existingOid}";
            _codeLabel.Visible = true;
        }

        // Listen for new OIDs (first connection or reconnection)
        noray.Connect("on_pid", Callable.From<string>(OnPidReceived),
            (uint)GodotObject.ConnectFlags.OneShot);

        Multiplayer.ConnectedToServer += OnConnectedToServer;
    }

    private void OnPidReceived(string pid)
    {
        var noray = GetNode<Node>("/root/Noray");
        string oid = (string)noray.Get("oid");
        _codeLabel.Text = $"Kód: {oid}";
        _codeLabel.Visible = true;
    }

    public void BtnHostPressed()
    {
        _mp.Call("host");
        GetTree().ChangeSceneToFile(WorldScenePath);
    }

    public void BtnJoinPressed()
    {
        string oid = _codeInput.Text.Trim();
        if (string.IsNullOrEmpty(oid)) return;

        _mp.Call("join", oid);

        _joinButton.Disabled = true;
        _hostButton.Disabled = true;
        _codeInput.Editable = false;

        _codeLabel.Text = "Připojování...";
        _codeLabel.Visible = true;
    }

    private void OnConnectedToServer()
    {
        GetTree().ChangeSceneToFile(WorldScenePath);
    }

    public override void _ExitTree()
    {
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
    }
}
