using Godot;
using System;

namespace Shooter.Scripts.UI;

public partial class PauseScreen : Control
{
    // Path to your main menu scene — adjust if yours is at a different location.
    // Common paths: "res://Scenes/main_menu.tscn" or "res://main_menu.tscn"
    [Export] public string MainMenuScenePath = "res://Scenes/UI/main_menu.tscn";

    public override void _Ready()
    {
        // Start hidden
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
        // Unpause first
        Player.IsGamePaused = false;

        // Disconnect from multiplayer cleanly
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }

        // Return to main menu
        // If this fails, your scene file might be at a different path.
        // Check the Export property in the editor or update MainMenuScenePath.
        var err = GetTree().ChangeSceneToFile(MainMenuScenePath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Failed to change scene to '{MainMenuScenePath}': {err}");
            GD.PrintErr("Check that the scene file exists at that path.");
            // Fallback: try common alternative paths
            var alternatives = new string[]
            {
                "res://Scenes/main_menu.tscn",
                "res://main_menu.tscn",
                "res://Scenes/MainMenu.tscn",
                "res://scenes/main_menu.tscn",
                "res://UI/main_menu.tscn",
            };
            foreach (var alt in alternatives)
            {
                if (ResourceLoader.Exists(alt))
                {
                    GD.Print($"Found scene at: {alt}");
                    GetTree().ChangeSceneToFile(alt);
                    return;
                }
            }
        }
    }

    public override void _ExitTree()
    {
        // Make sure we clean up the pause state when this node is removed
        Player.IsGamePaused = false;
    }
}
