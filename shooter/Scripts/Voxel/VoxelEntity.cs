using System;
using System.Collections.Generic;
using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelEntity : Node3D
{
    public const int GridSize = 8; // Number of chunks along one axis

    public VoxelRegistry Registry { get; set; }
    private Dictionary<Vector3I, VoxelChunk> _chunks = new();
    private StandardMaterial3D _voxelMaterial;

    public override void _Ready()
    {
        _voxelMaterial = new StandardMaterial3D();
        _voxelMaterial.VertexColorUseAsAlbedo = true;
        Registry = GD.Load<VoxelRegistry>("res://Resources/VoxelMaterials.tres");
        //GenerateDummyTerrain();
        GenerateBrick();
    }
    
    private void GenerateDummyTerrain()
    {
        for (int cx = 0; cx < GridSize; cx++)
        {
            for (int cz = 0; cz < GridSize; cz++)
            {
                var chunkCoord = new Vector3I(cx, 0, cz);
                VoxelChunk chunk = CreateChunk(chunkCoord);

                // Fill terrain
                for (int x = 0; x < ChunkData.Size; x++)
                {
                    for (int z = 0; z < ChunkData.Size; z++)
                    {
                        chunk.SetVoxel(x, 0, z, 1);

                        if (GD.Randf() > 0.9f)
                        {
                            for (int y = 1; y < 4; y++)
                            {
                                chunk.SetVoxel(x, y, z, 1);
                            }
                        }
                    }
                    chunk.UpdateMesh();
                }
            }
        }
    }

    private void GenerateBrick()
    {
        var chunkCoord = new Vector3I(0, 0, 0);
        VoxelChunk chunk = CreateChunk(chunkCoord);
        
        for (int x = 0; x < ChunkData.Size; x++)
        {
            for (int z = 0; z < ChunkData.Size; z++)
            {
                for (int y = 1; y < ChunkData.Size; y++)
                {
                    chunk.SetVoxel(x, y, z, 1);
                }
            }
        }
        chunk.UpdateMesh();
    }

    /// <summary>
    /// Tries to retrieve a chunk at the given chunk coordinate.
    /// If the chunk doesn't exist, new one is created.
    /// </summary>
    private VoxelChunk CreateChunk(Vector3I coord)
    {
        if (_chunks.TryGetValue(coord, out VoxelChunk existingChunk))
            return existingChunk;

        var chunk = new VoxelChunk();
        chunk.Initialize(Registry, _voxelMaterial);
        var size = ChunkData.Size * ChunkData.VoxelSize;
        chunk.Position = coord * new Vector3(size, size, size);
        
        AddChild(chunk);
        _chunks[coord] = chunk;
        return chunk;
    }

    public void SetVoxel(Vector3I globalPos, byte id)
    {
        Vector3I chunkCoord = new Vector3I(
            Mathf.FloorToInt((float)globalPos.X / ChunkData.Size),
            Mathf.FloorToInt((float)globalPos.Y / ChunkData.Size),
            Mathf.FloorToInt((float)globalPos.Z / ChunkData.Size)
        );

        VoxelChunk chunk = CreateChunk(chunkCoord);

        Vector3I localPos = new Vector3I(
            globalPos.X - (chunkCoord.X * ChunkData.Size),
            globalPos.Y - (chunkCoord.Y * ChunkData.Size),
            globalPos.Z - (chunkCoord.Z * ChunkData.Size)
        );
        chunk.SetVoxel(localPos.X, localPos.Y, localPos.Z, id);
        chunk.UpdateMesh();
    }
}