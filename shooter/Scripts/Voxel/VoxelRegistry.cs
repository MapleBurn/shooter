using Godot;

namespace Shooter.Scripts.Voxel;

[GlobalClass]
public partial class VoxelRegistry : Resource
{
    // Maps an ID (byte) to a Material
    [Export] public Godot.Collections.Array<VoxelMaterial> Materials { get; set; } = new();
    
    public VoxelMaterial? GetMaterial(byte id)
    {
        if (id == 0)
            return null; // 0 is Air
        
        if (id <= Materials.Count)
            return Materials[id - 1];
        
        return null;
    }

    public float GetToughness(byte id) => GetMaterial(id)?.Toughness ?? 0f;
}