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
        ChunkData = new ChunkData(Registry);

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
}
