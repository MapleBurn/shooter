using System.Collections.Generic;
using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelEntity : Node3D
{
    public const int GridSize = 8; // Number of chunks along one axis

    public VoxelRegistry Registry { get; set; }
    //private bool _isStatic = true;

    private Dictionary<Vector3I, VoxelChunk> _chunks = new();

    public override void _Ready()
    {
        Registry = GD.Load<VoxelRegistry>("res://Resources/VoxelMaterials.tres");
        GenerateDummyTerrain();
        //GenerateBrick();
    }
    
    /// <summary>
    /// A helper to create a simple floor so the scene isn't empty.
    /// </summary>
    private void GenerateDummyTerrain()
    {
        for (int cx = 0; cx < GridSize; cx++)
        {
            for (int cz = 0; cz < GridSize; cz++)
            {
                var chunkCoord = new Vector3I(cx, cz, 0);

                // Create and register the chunk
                VoxelChunk chunk = new VoxelChunk();
                chunk.Registry = Registry;
                AddChild(chunk);
                chunk.Position = new Vector3(
                    cx * ChunkData.Size,
                    0,
                    cz * ChunkData.Size
                );
                _chunks[chunkCoord] = chunk;

                // Fill terrain
                for (int x = 0; x < ChunkData.Size; x++)
                {
                    for (int z = 0; z < ChunkData.Size; z++)
                    {
                        chunk.ChunkData.SetVoxel(x, 0, z, 1);

                        if (GD.Randf() > 0.9f)
                        {
                            for (int y = 1; y < 4; y++)
                            {
                                chunk.ChunkData.SetVoxel(x, y, z, 1);
                            }
                        }
                    }
                }
            }
        }

        // After modifying the data, we need to update the meshes
        foreach (var chunk in _chunks.Values)
        {
            chunk.UpdateMesh();
        }
    }

    private void GenerateBrick()
    {
        var chunkCoord = new Vector3I(0, 0, 0);
        var chunk = new VoxelChunk();
        chunk.Registry = Registry;
        chunk.Position = chunkCoord;
        _chunks[chunkCoord] = chunk;
        AddChild(chunk);
        
        for (int x = 0; x < ChunkData.Size; x++)
        {
            for (int z = 0; z < ChunkData.Size; z++)
            {
                for (int y = 1; y < ChunkData.Size; y++)
                {
                    chunk.ChunkData.SetVoxel(x, y, z, 1);
                }
            }
        }
        
        chunk.UpdateMesh();
    }
}