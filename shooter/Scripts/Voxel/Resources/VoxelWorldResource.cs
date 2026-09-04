using Godot;
using Godot.Collections;

namespace Shooter.Scripts.Voxel.Resources;

public partial class VoxelWorldResource : Resource
{
    [Export] public Dictionary<string, ChunkDataResource> Chunks { get; set; }
}