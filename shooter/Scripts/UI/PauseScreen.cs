using Godot;
using System;

namespace Shooter.Scripts.UI;

public partial class PauseScreen : Control
{
	public override void _Ready()
	{
	}
	
	public override void _Process(double delta)
	{
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("escape"))
		{
			Visible = !Visible;
			Input.MouseMode = Visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
		}
	}


	public void BtnResumePressed()
	{
		Visible = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
	
	public void BtnLeavePressed()
	{
		GetTree().ChangeSceneToFile("res://Scripts/UI/MainMenu.cs");
	}
}
