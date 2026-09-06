using Godot;
using VoxelWorld = Shooter.Voxel.VoxelWorld;

namespace Shooter.Editor;

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