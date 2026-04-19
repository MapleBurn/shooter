using System.Collections.Generic;
using Godot;
using Shooter.Scripts.Voxel;

namespace Shooter.Scripts.Gun;

public partial class BulletManager : Node3D
{
    public static BulletManager Instance { get; private set; }
    
    private List<BulletData> _bullets = new();
    private const float Gravity = -9.8f;
    
    // temporary bullet hole stuff
    private static ImageTexture _bulletHoleTexture;
    private const int MaxBulletHoles = 15;
    private const float BulletHoleLifetime = 6.0f;
    private static readonly Queue<Decal> ActiveBulletHoles = new();

    public override void _Ready()
    {
        Instance = this;
        
        if (_bulletHoleTexture == null)
            _bulletHoleTexture = GenerateBulletHoleTexture();
    }

    public void SpawnBullet(Vector3 origin, Vector3 direction, float speed, float penetration)
    {
        _bullets.Add(new BulletData
        {
            Position = origin,
            Velocity = direction.Normalized() * speed,
            Lifetime = 5f,
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
                var hitNormal = result["normal"].AsVector3();
                
                var collider = (Node)result["collider"];
                if (collider.GetParent() is Player)
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
                
                // Just a bullet hole texture to see where we hit -temporary
                CreateBulletHole(hitPoint, hitNormal);
                
                _bullets.RemoveAt(i);
            }
        }
    }
    
    private void CreateBulletHole(Vector3 position, Vector3 normal)
    {
        while (ActiveBulletHoles.Count >= MaxBulletHoles)
        {
            var oldest = ActiveBulletHoles.Dequeue();
            if (GodotObject.IsInstanceValid(oldest))
                oldest.QueueFree();
        }

        var decal = new Decal();
        decal.Size = new Vector3(0.12f, 0.05f, 0.12f);
        decal.TextureAlbedo = _bulletHoleTexture;
        decal.Modulate = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        decal.UpperFade = 0.2f;
        decal.LowerFade = 0.5f;
        decal.NormalFade = 0.3f;

        GetTree().Root.AddChild(decal);
        decal.GlobalPosition = position;
        OrientDecalToNormal(decal, normal);

        ActiveBulletHoles.Enqueue(decal);
        FadeAndRemoveDecal(decal);
    }

    private async void FadeAndRemoveDecal(Decal decal)
    {
        await ToSignal(GetTree().CreateTimer(BulletHoleLifetime), "timeout");
        if (!GodotObject.IsInstanceValid(decal)) return;

        var tween = GetTree().CreateTween();
        tween.TweenProperty(decal, "modulate:a", 0.0f, 1.5f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(decal))
                decal.QueueFree();
        }));
    }

    private void OrientDecalToNormal(Decal decal, Vector3 normal)
    {
        normal = normal.Normalized();
        Vector3 up = normal;
        Vector3 right;

        if (Mathf.Abs(normal.Dot(Vector3.Right)) < 0.99f)
            right = normal.Cross(Vector3.Right).Normalized();
        else
            right = normal.Cross(Vector3.Forward).Normalized();

        Vector3 forward = right.Cross(up).Normalized();
        decal.GlobalTransform = new Transform3D(new Basis(right, up, forward), decal.GlobalPosition);
    }
    
    private static ImageTexture GenerateBulletHoleTexture()
    {
        int size = 32;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float center = size / 2.0f;
        float outerRadius = size / 2.0f;
        float innerRadius = size / 6.0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist > outerRadius)
                    image.SetPixel(x, y, new Color(0, 0, 0, 0));
                else if (dist < innerRadius)
                    image.SetPixel(x, y, new Color(0.02f, 0.02f, 0.02f, 1.0f));
                else
                {
                    float t = (dist - innerRadius) / (outerRadius - innerRadius);
                    float alpha = 1.0f - t;
                    float brightness = 0.05f + t * 0.15f;
                    image.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha * 0.8f));
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}