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
        
        var noray = GetNode<Node>("/root/Noray");
        // Přidali jsme parametr string, aby odpovídal signálu on_pid
        noray.Connect("on_pid", Callable.From<string>(OnPidReceived));

        // Pro klienta: Počkáme na úspěšné připojení k multiplayer peeru, než změníme scénu
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
        
        // Hostitel může změnit scénu okamžitě
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
        // Tato metoda se zavolá na klientovi, jakmile ENet naváže spojení se serverem
        GetTree().ChangeSceneToFile(WorldScenePath);
    }

    // Nezapomeň odpojit signály při zániku nodu
    public override void _ExitTree()
    {
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
    }
}