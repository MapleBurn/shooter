using Godot;
using Shooter.Scripts.PlayerLogic;
using Shooter.Scripts.Voxel;

namespace Shooter.Scripts.Editor;

public partial class EditorPlayer : Player
{
    [Export] public GhostCursor GhostCursor;
    [Export] public VoxelEntity VoxelWorld;
    private bool _isDeleteMode;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;
        IsCreativeMode = true;
    }
    
    public override void _Input(InputEvent @event)
    {
        if (IsGamePaused) return;
        Look(@event, MouseSensitivity);

        _isDeleteMode = Input.IsKeyPressed(Key.Shift);
        if (_isDeleteMode)
            GhostCursor.CurrentMode = GhostCursor.EditMode.Delete;
        else
            GhostCursor.CurrentMode = GhostCursor.EditMode.Place;
        
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
                PlaceOrBreak();
        }
    }

    private void PlaceOrBreak()
    {
        if (_isDeleteMode)
            VoxelWorld.SetVoxel(GhostCursor.CurrentGlobalVoxelPos, 0);
        else
            VoxelWorld.SetVoxel(GhostCursor.CurrentGlobalVoxelPos, 1);
    }
    
    public override void _PhysicsProcess(double delta)
    {
        Move((float)delta);
    }
}