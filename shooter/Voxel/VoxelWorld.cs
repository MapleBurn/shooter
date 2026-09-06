using Godot;

namespace Shooter.Voxel;

public partial class VoxelWorld : Node3D
{
    public Shooter.Voxel.VoxelEntity StaticMap;

    public override void _Ready()
    {
        StaticMap = new Shooter.Voxel.VoxelEntity();
        AddChild(StaticMap);
    }

    public void SaveWorld()
    {
        StaticMap.SaveAllChunks();
    }

    public void LoadWorld()
    {
        StaticMap.LoadAllChunks();
    }
}