using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelChunk : Node3D
{
    public VoxelRegistry Registry { get; set; }
    
    public ChunkData ChunkData;
    private MeshInstance3D _meshInstance;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;

    public override void _Ready()
    {
        // 1. Initialize the Data (In a real game, this might be loaded from disk)
        ChunkData = new ChunkData();
        ChunkData.Registry = Registry;

        // 2. Set up the visual component
        _meshInstance = new MeshInstance3D();
        var mat = new StandardMaterial3D();
        mat.VertexColorUseAsAlbedo = true;
        _meshInstance.MaterialOverride = mat;
        AddChild(_meshInstance);

        // 3. Set up the physics component (even if empty for now, we need the structure)
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
        if (ChunkData == null || Registry == null) return;

        // Generate the new mesh using our Mesher
        ArrayMesh newMesh = VoxelMesher.GenerateMesh(ChunkData, Registry);
        _meshInstance.Mesh = newMesh;

        // Update Collision (Note: Creating collision from mesh is expensive, 
        // but for a prototype/small chunks it works fine).
        // In a production engine, we would use a more optimized way to update collisions.
        _collisionShape.Shape = new ConcavePolygonShape3D();
        var shape = (ConcavePolygonShape3D)_collisionShape.Shape;
        shape.SetFaces(newMesh.GetFaces());
    }

    /// <summary>
    /// Public method to modify voxels from outside (e.g., a player tool).
    /// </summary>
    public void SetVoxel(int x, int y, int z, byte id)
    {
        ChunkData.SetVoxel(x, y, z, id);
        UpdateMesh();
    }
    
    public void DamageVoxel(Vector3 worldHitPos, Vector3 worldRayDir, float penetration)
    {
        const float epsilon = 0.01f;

        Vector3 localHit = ToLocal(worldHitPos);
        Vector3 localRayDir = (GlobalTransform.Basis.Inverse() * worldRayDir).Normalized();
        Vector3 samplePos = localHit + localRayDir * epsilon;

        Vector3I pos = new Vector3I(
            Mathf.RoundToInt(samplePos.X),
            Mathf.RoundToInt(samplePos.Y),
            Mathf.RoundToInt(samplePos.Z)
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
