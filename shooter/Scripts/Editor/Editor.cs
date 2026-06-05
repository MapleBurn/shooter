using Godot;
using Shooter.Scripts.Voxel;

namespace Shooter.Scripts.Editor;

public partial class Editor : Node3D
{
	[Export] private VoxelWorld _map;
	
	public override void _Ready()
	{
		
	}
	
	public override void _Process(double delta)
	{
	}

	public void OnSaveClicked()
	{
		_map.SaveWorld();
	}
	
	public void OnLoadClicked()
    {
    	_map.LoadWorld();
    }
}