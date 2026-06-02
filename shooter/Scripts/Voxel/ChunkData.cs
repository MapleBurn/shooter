using Godot;
using System;

namespace Shooter.Scripts.Voxel;

public class ChunkData
{
    public const int Size = 32;
    public const float VoxelSize = 0.2f; // Size of each voxels in meters
    public const int TotalVoxels = Size * Size * Size;

    private readonly byte[] _voxels = new byte[TotalVoxels];
    private readonly Color[] _voxelColors = new Color[TotalVoxels];
    
    private int GetIndex(Vector3I pos)
    {
        return pos.X + (pos.Y * Size) + (pos.Z * Size * Size);
    }

    public void SetVoxel(Vector3I pos, byte id)
    {
        if (IsInBounds(pos))
        {
            _voxels[GetIndex(pos)] = id;
            var mat = VoxelRegistry.GetMaterial(id);
            if  (mat == null)
            {
                GD.Print("[ChunkData] Voxel ID is: " + id + ", it does not exist or is Air.");
                _voxels[GetIndex(pos)] = 0;
                _voxelColors[GetIndex(pos)] = Colors.Transparent;
                return;
            }
            GD.Print("[ChunkData] Set voxel with material: " + mat.Name + ", ID: " + id);
            _voxelColors[GetIndex(pos)] = mat.Color;
        }
    }

    public byte GetVoxel(Vector3I pos)
    {
        if (!IsInBounds(pos)) return 0; // Return air if out of bounds
        return _voxels[GetIndex(pos)];
    }
    
    public void SetVoxelColor(Vector3I pos, Color color)
    {
        if (IsInBounds(pos))
        {
            _voxelColors[GetIndex(pos)] = color;
        }
    }

    public Color GetVoxelColor(Vector3I pos)
    {
        if (!IsInBounds(pos)) return Colors.Transparent;
        return _voxelColors[GetIndex(pos)];
    }

    public bool IsEmpty()
    {
        foreach (var voxel in _voxels)
        {
            if (voxel != 0) return false;
        }
        return true;
    }

    public bool IsInBounds(Vector3I pos)
    {
        return pos.X >= 0 && pos.X < Size && pos.Y >= 0 && pos.Y < Size && pos.Z >= 0 && pos.Z < Size;
    }
}