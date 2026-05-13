using System;
using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelChunk : Node3D
{
    public VoxelRegistry Registry { get; set; }
    public Vector3I ChunkCoord { get; set; }
    
    public ChunkData ChunkData;
    private MeshInstance3D _meshInstance;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;

    private bool _isDirty;
    public Action<VoxelChunk> OnBecameEmpty;

    public void Initialize(VoxelRegistry registry, Material mat)
    {
        Registry = registry;
        ChunkData = new ChunkData();
        ChunkData.Registry = Registry;

        // 2. Set up the visual component
        _meshInstance = new MeshInstance3D();
        _meshInstance.MaterialOverride = mat;
        AddChild(_meshInstance);

        // 3. Set up the physics component
        _staticBody = new StaticBody3D();
        AddChild(_staticBody);
        _collisionShape = new CollisionShape3D();
        _staticBody.AddChild(_collisionShape);
    }

    public override void _Ready()
    {
        // If it was already initialized manually, do nothing.
        // Otherwise, we might need to initialize it if it's placed in the editor.
        /*if (ChunkData == null)
        {
            Initialize(Registry);
        }*/
    }

    /// <summary>
    /// Rebuilds the mesh and collision based on current ChunkData.
    /// </summary>
    public void UpdateMesh()
    {
        if (ChunkData == null || Registry == null || !_isDirty) return;

        // Generate the new mesh using our Mesher
        ArrayMesh newMesh = VoxelMesher.GenerateMesh(ChunkData, Registry);
        
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
    public void SetVoxel(int x, int y, int z, byte id)
    {
        ChunkData.SetVoxel(x, y, z, id);
        
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

        if (!ChunkData.IsInBounds(pos.X, pos.Y, pos.Z))
            return;

        byte currentId = ChunkData.GetVoxel(pos.X, pos.Y, pos.Z);
        if (currentId == 0)
        {
            GD.PrintErr($"[VoxelChunk] Hit an empty voxel at {pos}, samplePos={samplePos}");
            return;
        }

        var mat = Registry.GetMaterial(currentId);
        if (mat == null)
        {
            GD.PrintErr($"[VoxelChunk] No material found for voxel ID {currentId} at {pos}");
            return;
        }

        if (penetration > mat.Toughness)
        {
            SetVoxel(pos.X, pos.Y, pos.Z, 0);
        }
        else
        {
            Color currentColor = ChunkData.GetVoxelColor(pos.X, pos.Y, pos.Z);
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
            
            ChunkData.SetVoxelColor(pos.X, pos.Y, pos.Z, darkenedColor);
        }

        UpdateMesh();
    }
}
