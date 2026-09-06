using Godot;
using VoxelMaterial = Shooter.Voxel.Resources.VoxelMaterial;

namespace Shooter.Voxel;

public static class VoxelRegistry
{
    public static bool IsInitialized;
    [Export] public static Godot.Collections.Array<VoxelMaterial> Materials { get; set; } = new();
    
    public static void Initialize()
    {
        using var dir = DirAccess.Open("res://Editor/Materials/");
		
        foreach (var fileName in dir.GetFiles())
        {
            if (!fileName.EndsWith(".tres") && !fileName.EndsWith(".res"))
                continue;

            string fullPath = $"res://Editor/Materials/{fileName}";
            var material = ResourceLoader.Load<VoxelMaterial>(fullPath);

            if (material == null)
                continue;

            Materials.Add(material);
        }
        
        IsInitialized = true;
    }
    
    public static VoxelMaterial GetMaterial(byte id)
    {
        if (id == 0)
            return null; // 0 is Air
        
        if (id <= Materials.Count)
            return Materials[id - 1];
        
        return null;
    }
}