using Godot;
using Player = Shooter.World.Player.Player;

namespace Shooter.UI;

public partial class PauseScreen : Control
{
    [Export] public string MainMenuScenePath = "res://UI/main_menu.tscn";

    public override void _Ready()
    {
        Visible = false;
        Player.IsGamePaused = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("escape"))
        {
            TogglePause();
            // Mark the event as handled so it doesn't propagate further
            GetViewport().SetInputAsHandled();
        }
    }

    private void TogglePause()
    {
        Visible = !Visible;
        Player.IsGamePaused = Visible;
        Input.MouseMode = Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    public void BtnResumePressed()
    {
        Visible = false;
        Player.IsGamePaused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public void BtnLeavePressed()
    {
        Player.IsGamePaused = false;

        // Disconnect from multiplayer
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }
        
        GetTree().ChangeSceneToFile(MainMenuScenePath);
    }

    public override void _ExitTree()
    {
        Player.IsGamePaused = false;
    }
}
