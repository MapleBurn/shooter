using System;
using System.Collections.Generic;
using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelEntity : Node3D
{
    public const int GridSize = 8; // Number of chunks along one axis
    
    private Dictionary<Vector3I, VoxelChunk> _chunks = new();
    private StandardMaterial3D _voxelMaterial;

    public override void _Ready()
    {
        _voxelMaterial = new StandardMaterial3D();
        _voxelMaterial.VertexColorUseAsAlbedo = true;
        if (!VoxelRegistry.IsInitialized)
            VoxelRegistry.Initialize();
        //GenerateDummyTerrain();
        //GenerateBrick();
        SpawnCube();
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
                        chunk.SetVoxel(new Vector3I(x, 0, z), 1);

                        if (GD.Randf() > 0.9f)
                        {
                            for (int y = 1; y < 4; y++)
                            {
                                chunk.SetVoxel(new Vector3I(x, y, z), 1);
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
                    chunk.SetVoxel(new Vector3I(x, y, z), 1);
                }
            }
        }
        chunk.UpdateMesh();
    }

    private void SpawnCube()
    {
        var chunkCoord = new Vector3I(0, 0, 0);
        VoxelChunk chunk = CreateChunk(chunkCoord);
        chunk.Initialize(_voxelMaterial);
        chunk.SetVoxel(chunkCoord, 1, Colors.Black);
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
        chunk.ChunkCoord = coord;
        var size = ChunkData.Size * ChunkData.VoxelSize;
        chunk.Position = (Vector3)coord * size;
        
        chunk.OnBecameEmpty = HandleEmptyChunk;
        AddChild(chunk);
        _chunks[coord] = chunk;
        return chunk;
    }

    /// <summary>
    ///  Removes the chunks that Invoke being empty.
    /// </summary>
    private void HandleEmptyChunk(VoxelChunk chunk)
    {
        _chunks.Remove(chunk.ChunkCoord);
        chunk.QueueFree();
    }

    public void SetVoxel(Vector3I globalPos, byte voxelType, Color color)
    {
        Vector3I chunkCoord = new Vector3I(
            Mathf.FloorToInt((float)globalPos.X / ChunkData.Size),
            Mathf.FloorToInt((float)globalPos.Y / ChunkData.Size),
            Mathf.FloorToInt((float)globalPos.Z / ChunkData.Size)
        );
        
        _chunks.TryGetValue(chunkCoord, out VoxelChunk chunk);
        if (chunk == null)
        {
            chunk = CreateChunk(chunkCoord);
            chunk.Initialize(_voxelMaterial);
            _chunks[chunkCoord] = chunk;
        }

        Vector3I localPos = new Vector3I(
            globalPos.X - (chunkCoord.X * ChunkData.Size),
            globalPos.Y - (chunkCoord.Y * ChunkData.Size),
            globalPos.Z - (chunkCoord.Z * ChunkData.Size)
        );
        chunk.SetVoxel(localPos, voxelType, color);
        chunk.UpdateMesh();
    }
}