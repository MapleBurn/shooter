using Godot;

namespace Shooter.Scripts.Voxel.Resources;

public partial class ChunkDataResource : Resource
{
    [Export] public Vector3I Position { get; set; }
    [Export] public byte[] Voxels { get; set; }
    [Export] public Color[] VoxelColors { get; set; }
}