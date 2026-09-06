using Godot;
using Godot.Collections;

namespace Shooter.Voxel.Resources;

public partial class VoxelWorldResource : Resource
{
    [Export] public Dictionary<string, Shooter.Voxel.Resources.ChunkDataResource> Chunks { get; set; }
}