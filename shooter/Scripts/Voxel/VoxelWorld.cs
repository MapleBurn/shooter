using System.Collections.Generic;
using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelWorld : Node3D
{
    public VoxelEntity StaticMap;

    public override void _Ready()
    {
        StaticMap = new VoxelEntity();
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