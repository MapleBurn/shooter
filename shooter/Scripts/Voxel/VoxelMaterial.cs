using Godot;

namespace Shooter.Scripts.Voxel;

[GlobalClass] // This makes it appear in the "Create Resource" menu
public partial class VoxelMaterial : Resource
{
    [Export] public string Name { get; set; } = "New Voxel";
    [Export] public float Toughness { get; set; } = 10.0f;
    
    // We use vertex colors for performance
    [Export] public Color Color { get; set; } = Colors.White;
}