using Godot;

namespace Shooter.Scripts.UI;

public partial class EditorMenuScreen : Control
{
	private const string MainMenuScenePath = "res://Scenes/UI/main_menu.tscn";
	
	public override void _Ready()
	{
		Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("escape"))
		{
			Visible = !Visible;
			GetViewport().SetInputAsHandled();
		}
	}
	
	public void BtnResumePressed()
	{
		Visible = false;
	}

	public void BtnExitPressed()
	{
		GetTree().ChangeSceneToFile(MainMenuScenePath);
	}
	
	public void BtnSavePressed()
	{
		// Implement save functionality here
	}
	
	public override void _Process(double delta)
	{
	}
}