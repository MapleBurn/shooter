using Godot;

namespace Shooter.Scripts.Voxel;

public partial class VoxelChunk : Node3D
{
    [Export] public VoxelRegistry Registry { get; set; }
    
    private ChunkData _chunkData;
    private MeshInstance3D _meshInstance;
    private StaticBody3D _staticBody;
    private CollisionShape3D _collisionShape;

    public override void _Ready()
    {
        // 1. Initialize the Data (In a real game, this might be loaded from disk)
        _chunkData = new ChunkData(Registry);

        // 2. Set up the visual component
        _meshInstance = new MeshInstance3D();
        AddChild(_meshInstance);

        // 3. Set up the physics component (even if empty for now, we need the structure)
        _staticBody = new StaticBody3D();
        AddChild(_staticBody);
        _staticBody.AddChild(_collisionShape);
        _collisionShape = new CollisionShape3D(); // Placeholder

        // 4. Generate some initial terrain so we can see something!
        GenerateDummyTerrain();

        // 5. Build the mesh
        UpdateMesh();
    }

    /// <summary>
    /// Rebuilds the mesh and collision based on current ChunkData.
    /// </summary>
    public void UpdateMesh()
    {
        if (_chunkData == null || Registry == null) return;

        // Generate the new mesh using our Mesher
        ArrayMesh newMesh = VoxelMesher.GenerateMesh(_chunkData, Registry);
        _meshInstance.Mesh = newMesh;

        // Update Collision (Note: Creating collision from mesh is expensive, 
        // but for a prototype/small chunks it works fine).
        // In a production engine, we would use a more optimized way to update collisions.
        _collisionShape.Shape = new ConcavePolygonShape3D();
        var shape = (ConcavePolygonShape3D)_collisionShape.Shape;
        shape.SetFaces(newMesh.GetFaces());
    }

    /// <summary>
    /// A helper to create a simple floor so the scene isn't empty.
    /// </summary>
    private void GenerateDummyTerrain()
    {
        for (int x = 0; x < ChunkData.Size; x++)
        {
            for (int z = 0; z < ChunkData.Size; z++)
            {
                // Create a flat floor at y = 0
                _chunkData.SetVoxel(x, 0, z, 1); // Assuming ID 1 is Stone/Floor
                
                // Add some random "pillars" for visual interest
                if (GD.Randf() > 0.95f)
                {
                    for (int y = 1; y < 4; y++)
                    {
                        _chunkData.SetVoxel(x, y, z, 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Public method to modify voxels from outside (e.g., a player tool).
    /// </summary>
    public void SetVoxel(int x, int y, int z, byte id)
    {
        _chunkData.SetVoxel(x, y, z, id);
        UpdateMesh();
    }
}
