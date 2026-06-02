using System;
using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelChunk : Node3D
{
    public Vector3I ChunkCoord { get; set; }
    
    public ChunkData ChunkData;
    private MeshInstance3D _meshInstance;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;

    private bool _isDirty;
    public Action<VoxelChunk> OnBecameEmpty;

    public void Initialize(StandardMaterial3D mat)
    {
        ChunkData = new ChunkData();
        
        _meshInstance = new MeshInstance3D();
        _meshInstance.MaterialOverride = mat;
        AddChild(_meshInstance);
        
        _staticBody = new StaticBody3D();
        AddChild(_staticBody);
        _collisionShape = new CollisionShape3D();
        _staticBody.AddChild(_collisionShape);
    }

    /// <summary>
    /// Rebuilds the mesh and collision based on current ChunkData.
    /// </summary>
    public void UpdateMesh()
    {
        if (ChunkData == null || !_isDirty) return;

        // Generate the new mesh using our Mesher
        ArrayMesh newMesh = VoxelMesher.GenerateMesh(ChunkData);
        
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

    /// <summary>
    /// Public method to modify voxels from outside (e.g., a player tool).
    /// </summary>
    public void SetVoxel(Vector3I pos, byte voxelType, Color color = default)
    {
        ChunkData.SetVoxel(pos, voxelType);
        if (color != default)
            ChunkData.SetVoxelColor(pos, color);
        
        if (ChunkData.IsEmpty())
        {
            OnBecameEmpty?.Invoke(this);
            return;
        }
        _isDirty = true;
    }
    
    public void DamageVoxel(Vector3 worldHitPos, Vector3 worldRayDir, float penetration)
    {
        const float epsilon = 0.01f;

        Vector3 localHit = ToLocal(worldHitPos);
        Vector3 localRayDir = (GlobalTransform.Basis.Inverse() * worldRayDir).Normalized();
        Vector3 samplePos = localHit + localRayDir * epsilon;

        Vector3I pos = new Vector3I(
            Mathf.RoundToInt(samplePos.X / ChunkData.VoxelSize),
            Mathf.RoundToInt(samplePos.Y / ChunkData.VoxelSize),
            Mathf.RoundToInt(samplePos.Z / ChunkData.VoxelSize)
        );

        if (!ChunkData.IsInBounds(pos))
            return;

        byte currentId = ChunkData.GetVoxel(pos);
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
            Color currentColor = ChunkData.GetVoxelColor(pos);
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
            
            ChunkData.SetVoxelColor(pos, darkenedColor);
        }

        UpdateMesh();
    }
}
