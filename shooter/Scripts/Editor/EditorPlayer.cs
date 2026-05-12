using Godot;
using Shooter.Scripts.PlayerLogic;

namespace Shooter.Scripts.Editor;

public partial class EditorPlayer : Player
{
	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		IsCreativeMode = true;
	}
	
	public override void _Input(InputEvent @event)
	{
		if (IsGamePaused) return;
		Look(@event, MouseSensitivity);
	}
	
	public override void _PhysicsProcess(double delta)
	{
		Move((float)delta);
	}
}