using System.Collections.Generic;
using Godot;
using VoxelChunk = Shooter.Voxel.VoxelChunk;

namespace Shooter.World.Gun;

public partial class BulletManager : Node3D
{
    public static BulletManager Instance { get; private set; }
    
    private List<BulletData> _bullets = new();
    private const float Gravity = -9.8f;

    public override void _Ready()
    {
        Instance = this;
    }

    public void SpawnBullet(Vector3 origin, Vector3 direction, float speed, float penetration)
    {
        _bullets.Add(new BulletData
        {
            Position = origin,
            Velocity = direction.Normalized() * speed,
            Lifetime = 150f,
            Penetration = penetration
        });
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];

            Vector3 prevPos = b.Position;

            // Apply gravity
            b.Velocity.Y += Gravity * dt;
            b.Position += b.Velocity * dt;
            b.Lifetime -= dt;

            _bullets[i] = b;

            if (b.Lifetime <= 0f)
            {
                _bullets.RemoveAt(i);
                continue;
            }

            // Short raycast between last and current position
            var query = PhysicsRayQueryParameters3D.Create(prevPos, b.Position);
            var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

            if (result.Count > 0)
            {
                var hitPoint = result["position"].AsVector3();
                //var hitNormal = result["normal"].AsVector3();
                
                var collider = (Node)result["collider"];
                if (collider.GetParent() is Player.Player)
                {
                    _bullets.RemoveAt(i);
                    continue;
                }
                
                var chunk = collider.GetParent() as VoxelChunk;
                if (chunk == null)
                {
                    GD.PrintErr("[BulletManager] Couldn't get VoxelChunk from hit collider!");
                    _bullets.RemoveAt(i);
                    continue;
                }
                
                Vector3 travelDir = (b.Position - prevPos).Normalized();  
                chunk.DamageVoxel(hitPoint, travelDir, b.Penetration);
                
                _bullets.RemoveAt(i);
            }
        }
    }
}