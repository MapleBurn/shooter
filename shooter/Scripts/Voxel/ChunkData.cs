using Godot;
using System;

namespace Shooter.Scripts.Voxel;

public class ChunkData
{
    public const int Size = 16;
    public const float VoxelSize = 0.2f; // Size of each voxels in meters
    public const int TotalVoxels = Size * Size * Size;

    private readonly byte[] _voxels = new byte[TotalVoxels];
    private readonly Color[] _voxelColors = new Color[TotalVoxels];

    public VoxelRegistry Registry { get; set; }
    
    private int GetIndex(int x, int y, int z)
    {
        return x + (y * Size) + (z * Size * Size);
    }

    public void SetVoxel(int x, int y, int z, byte id)
    {
        if (IsInBounds(x, y, z))
        {
            _voxels[GetIndex(x, y, z)] = id;
            var mat = Registry.GetMaterial(id);
            _voxelColors[GetIndex(x, y, z)] = mat?.Color ?? Colors.DeepPink;
        }
    }

    public byte GetVoxel(int x, int y, int z)
    {
        if (!IsInBounds(x, y, z)) return 0; // Return air if out of bounds
        return _voxels[GetIndex(x, y, z)];
    }
    
    public void SetVoxelColor(int x, int y, int z, Color color)
    {
        if (IsInBounds(x, y, z))
        {
            _voxelColors[GetIndex(x, y, z)] = color;
        }
    }

    public Color GetVoxelColor(int x, int y, int z)
    {
        if (!IsInBounds(x, y, z)) return Colors.Transparent;
        return _voxelColors[GetIndex(x, y, z)];
    }

    public bool IsEmpty()
    {
        foreach (var voxel in _voxels)
        {
            if (voxel != 0) return false;
        }
        return true;
    }

    public bool IsInBounds(int x, int y, int z)
    {
        return x >= 0 && x < Size && y >= 0 && y < Size && z >= 0 && z < Size;
    }
}