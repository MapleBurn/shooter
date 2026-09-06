using System;
using Godot;

namespace Shooter.Voxel;

public partial class VoxelChunk : Node3D
{
    // Chunk data
    public const int Size = 32;
    public const float VoxelSize = 0.2f; // Size of each voxels in meters
    public const int TotalVoxels = Size * Size * Size;

    public byte[] Voxels = new byte[TotalVoxels];
    public Color[] VoxelColors = new Color[TotalVoxels];
    
    // global chunk position in the grid
    public Vector3I ChunkCoord { get; set; }
    
    private MeshInstance3D _meshInstance;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;

    private bool _isDirty;
    public Action<VoxelChunk> OnBecameEmpty;

    public void Initialize(StandardMaterial3D mat)
    {
        _meshInstance = new MeshInstance3D();
        _meshInstance.MaterialOverride = mat;
        AddChild(_meshInstance);
        
        _staticBody = new StaticBody3D();
        AddChild(_staticBody);
        _collisionShape = new CollisionShape3D();
        _staticBody.AddChild(_collisionShape);

        _isDirty = true;
    }

    #region Internal Chunk logic
    private int GetIndex(Vector3I pos)
    {
        return pos.X + (pos.Y * Size) + (pos.Z * Size * Size);
    }

    public void SetVoxel(Vector3I pos, byte id)
    {
        _isDirty = true;
        
        if (IsInBounds(pos))
        {
            Voxels[GetIndex(pos)] = id;
            var mat = VoxelRegistry.GetMaterial(id);
            if  (mat == null)
            {
                Voxels[GetIndex(pos)] = 0;
                VoxelColors[GetIndex(pos)] = Colors.Transparent;
                return;
            }
            GD.Print("[ChunkData] Set voxel with material: " + mat.Name + ", ID: " + id);
            VoxelColors[GetIndex(pos)] = mat.Color;
        }
        
        if (IsEmpty())
        {
            OnBecameEmpty?.Invoke(this);
        }
    }

    public byte GetVoxel(Vector3I pos)
    {
        if (!IsInBounds(pos)) return 0; // Return air if out of bounds
        return Voxels[GetIndex(pos)];
    }
    
    public void SetVoxelColor(Vector3I pos, Color color)
    {
        if (IsInBounds(pos))
        {
            VoxelColors[GetIndex(pos)] = color;
        }
    }

    public Color GetVoxelColor(Vector3I pos)
    {
        if (!IsInBounds(pos)) return Colors.Transparent;
        return VoxelColors[GetIndex(pos)];
    }

    private bool IsEmpty()
    {
        foreach (var voxel in Voxels)
        {
            if (voxel != 0) return false;
        }
        return true;
    }

    private bool IsInBounds(Vector3I pos)
    {
        return pos.X >= 0 && pos.X < Size && pos.Y >= 0 && pos.Y < Size && pos.Z >= 0 && pos.Z < Size;
    }
    #endregion
    
    
    public void UpdateMesh()
    {
        if (!_isDirty) return;

        // Generate the new mesh using the Mesher
        ArrayMesh newMesh = VoxelMesher.GenerateMesh(this);
        
        if (newMesh == null)
        {
            GD.PrintErr("[VoxelChunk] Failed to generate mesh. I'd prefer this would not happen.");
            return;
        }

        _meshInstance.Mesh = newMesh;
        
        _collisionShape.Shape = new ConcavePolygonShape3D();
        var shape = (ConcavePolygonShape3D)_collisionShape.Shape;
        shape.SetFaces(newMesh.GetFaces());
        _isDirty = false;
    }
    
    public void DamageVoxel(Vector3 worldHitPos, Vector3 worldRayDir, float penetration)
    {
        const float epsilon = 0.01f;

        Vector3 localHit = ToLocal(worldHitPos);
        Vector3 localRayDir = (GlobalTransform.Basis.Inverse() * worldRayDir).Normalized();
        Vector3 samplePos = localHit + localRayDir * epsilon;

        Vector3I pos = new Vector3I(
            Mathf.RoundToInt(samplePos.X / VoxelSize),
            Mathf.RoundToInt(samplePos.Y / VoxelSize),
            Mathf.RoundToInt(samplePos.Z / VoxelSize)
        );

        if (!IsInBounds(pos))
            return;

        byte currentId = GetVoxel(pos);
        if (currentId == 0)
        {
            GD.PrintErr($"[VoxelChunk] Hit an empty voxel at {pos}, samplePos={samplePos}");
            return;
        }

        var mat = VoxelRegistry.GetMaterial(currentId);
        if (mat == null)
        {
            GD.PrintErr($"[VoxelChunk] No material found for voxel ID {currentId} at {pos}");
            return;
        }

        if (penetration > mat.Toughness)
        {
            SetVoxel(pos, 0);
        }
        else
        {
            Color currentColor = GetVoxelColor(pos);
            if (currentColor == Colors.Transparent)
            {
                currentColor = mat.Color;
            }
            
            Color darkenedColor = new Color(
                Mathf.Max(0, currentColor.R * 0.8f),
                Mathf.Max(0, currentColor.G * 0.8f),
                Mathf.Max(0, currentColor.B * 0.8f),
                currentColor.A
            );
            
            SetVoxelColor(pos, darkenedColor);
        }

        UpdateMesh();
    }
}
