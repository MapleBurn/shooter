using System.Collections.Generic;
using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelWorld : Node3D
{
    public List<VoxelEntity> Entities = new List<VoxelEntity>();
    public VoxelEntity StaticMap;

    public override void _Ready()
    {
        StaticMap = new VoxelEntity();
        AddChild(StaticMap);
    }
    
    public void SaveWorld()
    {
        StaticMap.SaveAllChunks();
        foreach (VoxelEntity entity in Entities)
        {
            entity.SaveAllChunks();
        }
    }

    public void LoadWorld()
    {
        StaticMap.LoadAllChunks();
        foreach (VoxelEntity entity in Entities)
        {
            entity.LoadAllChunks();
        }
    }
}