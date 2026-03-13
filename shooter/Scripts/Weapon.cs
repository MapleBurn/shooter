using Godot;
using System;

namespace Shooter.Scripts;

/// <summary>
/// Weapon with ADS, raycasting, hit zones, muzzle flash, bullet trails, synced bullet holes.
///
/// v6.1 FIX: The CharacterBody3D collision shape (big capsule on layer 1) was blocking
/// the raycast from ever reaching the HitZone Area3Ds (layer 2) inside it.
///
/// Solution: Exclude ALL player CharacterBody3D RIDs from the raycast, so the ray
/// passes straight through player bodies and can only hit:
///   - HitZone Area3Ds (layer 2) → zone-specific damage
///   - World geometry (layer 1) → bullet holes
///
/// If the ray hits nothing (missed), no damage is applied.
/// </summary>
public partial class Weapon : Node3D
{
    [Export] public float Damage = 25.0f;
    [Export] public float FireRate = 0.1f;
    [Export] public float Range = 100.0f;

    [Export] public float HipSpread = 0.035f;
    [Export] public float AdsSpread = 0.005f;
    [Export] public float WeaponTiltAmount = 0.8f;

    [Export] public Vector3 AdsPositionOffset = new Vector3(-0.15f, 0.05f, -0.1f);

    [Export] public float TrailDuration = 0.12f;
    [Export] public Color TrailColor = new Color(1.0f, 0.95f, 0.7f, 0.5f);

    private Timer _fireRateTimer;
    private bool _canFire = true;
    private MeshInstance3D _gunMesh;
    private Vector3 _originalGunPos;
    private Camera3D _camera;
    private RandomNumberGenerator _rng;
    private Player _ownerPlayer;

    private OmniLight3D _muzzleFlash;
    private GpuParticles3D _muzzleParticles;
    private Node3D _muzzleTip;

    // Shared bullet hole texture
    private static ImageTexture _bulletHoleTexture;

    // Bullet hole pool
    private const int MaxBulletHoles = 15;
    private const float BulletHoleLifetime = 6.0f;
    private static readonly System.Collections.Generic.Queue<Decal> _activeBulletHoles = new();

    public override void _Ready()
    {
        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        _ownerPlayer = GetOwnerPlayer();
        _camera = _ownerPlayer?.GetCamera() ?? GetNode<Camera3D>("../Camera3D");

        _fireRateTimer = new Timer();
        _fireRateTimer.WaitTime = FireRate;
        _fireRateTimer.OneShot = true;
        _fireRateTimer.Timeout += () => _canFire = true;
        AddChild(_fireRateTimer);

        _gunMesh = GetNode<MeshInstance3D>("MeshInstance3D");
        _originalGunPos = _gunMesh.Position;

        _muzzleTip = new Node3D();
        _muzzleTip.Position = new Vector3(0, 0, -0.5f);
        _gunMesh.AddChild(_muzzleTip);

        _muzzleFlash = new OmniLight3D();
        _muzzleFlash.LightColor = new Color(1.0f, 0.8f, 0.3f);
        _muzzleFlash.LightEnergy = 3.0f;
        _muzzleFlash.OmniRange = 3.0f;
        _muzzleFlash.Visible = false;
        _muzzleTip.AddChild(_muzzleFlash);

        CreateMuzzleParticles();

        if (_bulletHoleTexture == null)
            _bulletHoleTexture = GenerateBulletHoleTexture();
    }

    public override void _Process(double delta)
    {
        if (_camera == null || _ownerPlayer == null) return;
        if (!_ownerPlayer.IsMultiplayerAuthority()) return;
        if (_ownerPlayer.IsDead) return;
        if (Player.IsGamePaused) return;

        bool isAiming = _ownerPlayer.IsAiming;

        float cameraXRotation = _camera.Rotation.X;
        Rotation = new Vector3(cameraXRotation * WeaponTiltAmount, 0, 0);

        Vector3 targetPos = isAiming
            ? _originalGunPos + AdsPositionOffset
            : _originalGunPos;
        _gunMesh.Position = _gunMesh.Position.Lerp(targetPos, (float)delta * 12.0f);

        if (Input.IsActionPressed("shoot") && _canFire)
        {
            Fire();
        }
    }

    private void Fire()
    {
        _canFire = false;
        _fireRateTimer.Start();

        bool isAiming = _ownerPlayer?.IsAiming ?? false;
        float spread = isAiming ? AdsSpread : HipSpread;

        Vector3 rayOrigin = _camera.GlobalPosition;
        Vector3 rayDirection = -_camera.GlobalTransform.Basis.Z;

        Vector3 spreadOffset = new Vector3(
            _rng.RandfRange(-spread, spread),
            _rng.RandfRange(-spread, spread),
            0
        );
        rayDirection = (rayDirection + _camera.GlobalTransform.Basis * spreadOffset).Normalized();

        Vector3 rayEnd = rayOrigin + rayDirection * Range;

        var spaceState = GetWorld3D().DirectSpaceState;

        // ── Build exclude list ──
        // Exclude ALL player CharacterBody3D RIDs so the ray passes through
        // every player's outer collision capsule and can reach the HitZone Area3Ds inside.
        // Also exclude own HitZones so we can't shoot ourselves.
        var excludeList = new Godot.Collections.Array<Rid>();

        // Find all Player nodes in the scene and exclude their body RIDs
        foreach (var node in GetTree().GetNodesInGroup("_players_internal"))
        {
            if (node is Player p)
                excludeList.Add(p.GetRid());
        }

        // Fallback: if the group isn't populated yet, at least exclude own + find players manually
        if (excludeList.Count == 0)
        {
            excludeList.Add(_ownerPlayer.GetRid());

            // Walk the world node to find other players
            var worldNode = _ownerPlayer.GetParent();
            if (worldNode != null)
            {
                foreach (var child in worldNode.GetChildren())
                {
                    if (child is Player otherPlayer)
                        excludeList.Add(otherPlayer.GetRid());
                }
            }
        }

        // Also exclude own HitZones
        foreach (var child in _ownerPlayer.GetChildren())
        {
            if (child is HitZone ownZone)
                excludeList.Add(ownZone.GetRid());
        }

        // ── SINGLE-PASS raycast: areas + bodies, layers 1+2 ──
        // With all player bodies excluded, the ray can only hit:
        //   - HitZone Area3Ds (layer 2) → zone damage
        //   - World geometry bodies (layer 1) → bullet holes
        var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
        query.CollideWithBodies = true;
        query.CollideWithAreas = true;
        query.CollisionMask = 0b11; // Layers 1 and 2
        query.Exclude = excludeList;

        var result = spaceState.IntersectRay(query);

        Vector3 shotEndPoint = rayEnd;
        bool createHole = false;
        Vector3 bulletHolePos = Vector3.Zero;
        Vector3 bulletHoleNormal = Vector3.Zero;

        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();
            var hitPoint = result["position"].AsVector3();
            var hitNormal = result["normal"].AsVector3();
            shotEndPoint = hitPoint;

            if (collider is HitZone hitZone)
            {
                // ── HitZone hit → zone-specific damage ──
                var targetPlayer = hitZone.OwnerPlayer;
                GD.Print($"[Weapon] Ray hit HitZone '{hitZone.ZoneName}' on {targetPlayer?.Name}");
                if (targetPlayer != null && targetPlayer != _ownerPlayer)
                {
                    int dmg = Mathf.RoundToInt(Damage);
                    targetPlayer.Rpc(
                        Player.MethodName.TakeDamage,
                        dmg, hitZone.ZoneName, hitPoint, hitNormal
                    );
                    ShowHitmarker(hitZone.ZoneName);
                }
            }
            else
            {
                // Not a HitZone → must be world geometry → bullet hole
                GD.Print($"[Weapon] Ray hit world geometry: {collider}");
                createHole = true;
                bulletHolePos = hitPoint;
                bulletHoleNormal = hitNormal;
            }
        }

        ShowMuzzleFlash();
        RecoilAnimation();

        Vector3 trailStart = _muzzleTip.GlobalPosition;

        Rpc(MethodName.OnShotFired, trailStart, shotEndPoint,
            createHole, bulletHolePos, bulletHoleNormal);
    }

    // ═══════════════════════════════════════════
    //  VISUAL EFFECTS
    // ═══════════════════════════════════════════

    private void ShowMuzzleFlash()
    {
        _muzzleFlash.Visible = true;
        _muzzleFlash.LightEnergy = 3.0f;

        if (_muzzleParticles != null)
        {
            _muzzleParticles.Restart();
            _muzzleParticles.Emitting = true;
        }

        var tween = GetTree().CreateTween();
        tween.TweenProperty(_muzzleFlash, "light_energy", 0.0f, 0.05f);
        tween.TweenCallback(Callable.From(() => _muzzleFlash.Visible = false));
    }

    private void RecoilAnimation()
    {
        Vector3 recoilPos = _gunMesh.Position + new Vector3(0, 0.02f, 0.08f);
        var tween = GetTree().CreateTween();
        tween.TweenProperty(_gunMesh, "position", recoilPos, 0.03f);

        Vector3 returnPos = (_ownerPlayer?.IsAiming ?? false)
            ? _originalGunPos + AdsPositionOffset
            : _originalGunPos;
        tween.TweenProperty(_gunMesh, "position", returnPos, 0.12f)
            .SetTrans(Tween.TransitionType.Elastic);
    }

    private void ShowHitmarker(string zone)
    {
        if (_ownerPlayer == null) return;
        foreach (var child in _ownerPlayer.GetChildren())
        {
            if (child is PlayerHUD playerHud)
            {
                playerHud.ShowHitConfirmation(zone == "head");
                return;
            }
        }
    }

    // ─────────────────────────────────────────
    //  BULLET HOLES
    // ─────────────────────────────────────────

    private void CreateBulletHole(Vector3 position, Vector3 normal)
    {
        while (_activeBulletHoles.Count >= MaxBulletHoles)
        {
            var oldest = _activeBulletHoles.Dequeue();
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

        _activeBulletHoles.Enqueue(decal);
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

    // ─────────────────────────────────────────
    //  BULLET TRAIL
    // ─────────────────────────────────────────

    private void SpawnBulletTrail(Vector3 from, Vector3 to)
    {
        var trail = new BulletTrailNode();
        GetTree().Root.AddChild(trail);
        trail.Setup(from, to, TrailColor, TrailDuration);
    }

    // ─────────────────────────────────────────
    //  MUZZLE PARTICLES
    // ─────────────────────────────────────────

    private void CreateMuzzleParticles()
    {
        _muzzleParticles = new GpuParticles3D();
        _muzzleParticles.Emitting = false;
        _muzzleParticles.OneShot = true;
        _muzzleParticles.Amount = 4;
        _muzzleParticles.Lifetime = 0.1f;
        _muzzleParticles.Explosiveness = 1.0f;

        var material = new ParticleProcessMaterial();
        material.Direction = new Vector3(0, 0, -1);
        material.Spread = 25.0f;
        material.InitialVelocityMin = 2.0f;
        material.InitialVelocityMax = 5.0f;
        material.Gravity = new Vector3(0, -2, 0);
        material.ScaleMin = 0.02f;
        material.ScaleMax = 0.04f;
        material.Color = new Color(1.0f, 0.9f, 0.5f);
        _muzzleParticles.ProcessMaterial = material;

        var drawMesh = new SphereMesh();
        drawMesh.Radius = 0.01f;
        drawMesh.Height = 0.02f;
        _muzzleParticles.DrawPass1 = drawMesh;

        _muzzleTip.AddChild(_muzzleParticles);
    }

    // ─────────────────────────────────────────
    //  NETWORK SYNC
    // ─────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void OnShotFired(Vector3 trailStart, Vector3 trailEnd,
        bool createHole, Vector3 holePos, Vector3 holeNormal)
    {
        ShowMuzzleFlash();
        SpawnBulletTrail(trailStart, trailEnd);

        if (createHole)
            CreateBulletHole(holePos, holeNormal);
    }

    private Player GetOwnerPlayer()
    {
        Node current = GetParent();
        while (current != null)
        {
            if (current is Player p) return p;
            current = current.GetParent();
        }
        return null;
    }
}

// ═══════════════════════════════════════════════════════
//  BULLET TRAIL NODE
// ═══════════════════════════════════════════════════════

public partial class BulletTrailNode : MeshInstance3D
{
    private float _duration = 0.12f;
    private float _elapsed = 0f;
    private StandardMaterial3D _material;

    public void Setup(Vector3 from, Vector3 to, Color color, float duration)
    {
        _duration = duration;

        var immMesh = new ImmediateMesh();
        _material = new StandardMaterial3D();
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.AlbedoColor = color;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.NoDepthTest = false;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        Vector3 direction = (to - from);
        if (direction.LengthSquared() < 0.001f)
        {
            QueueFree();
            return;
        }
        direction = direction.Normalized();
        float thickness = 0.008f;

        Vector3 perp;
        if (Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f)
            perp = direction.Cross(Vector3.Right).Normalized() * thickness;
        else
            perp = direction.Cross(Vector3.Up).Normalized() * thickness;

        immMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, _material);
        immMesh.SurfaceAddVertex(from + perp);
        immMesh.SurfaceAddVertex(from - perp);
        immMesh.SurfaceAddVertex(to + perp);
        immMesh.SurfaceAddVertex(to - perp);
        immMesh.SurfaceEnd();

        Mesh = immMesh;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;

        float alpha = 1.0f - (_elapsed / _duration);
        if (alpha <= 0f)
        {
            QueueFree();
            return;
        }

        if (_material != null)
        {
            var c = _material.AlbedoColor;
            _material.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
        }
    }
}
