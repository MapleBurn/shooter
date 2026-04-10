using Godot;
using System;
using System.Threading.Tasks;
using Shooter.Scripts.Gun;

namespace Shooter.Scripts.Gun;

public partial class Weapon : Node3D
{
    #region Properties
    public float Damage = 25.0f;
    public float FireRate = 0.1f;
    public float Range = 100.0f;

    public float HipSpread = 0.035f;
    public float AdsSpread = 0.005f;
    public float WeaponTiltAmount = 0.8f;

    public Vector3 AdsPositionOffset = new Vector3(-0.15f, 0.05f, -0.1f);

    public float TrailDuration = 0.12f;
    public Color TrailColor = new Color(1.0f, 0.95f, 0.7f, 0.5f);
    #endregion

    // --- NEW AMMO MECHANICS ---
    [Export] public int MaxAmmo = 30;
    private int _currentAmmo;
    private bool _isReloading = false;
    private float _reloadDuration = 4.0f;

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

    private static ImageTexture _bulletHoleTexture;
    private const int MaxBulletHoles = 15;
    private const float BulletHoleLifetime = 6.0f;
    private static readonly System.Collections.Generic.Queue<Decal> ActiveBulletHoles = new();

    #region Godot Lifecycle
    public override void _Ready()
    {
        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        _ownerPlayer = GetOwnerPlayer();
        _camera = _ownerPlayer?.GetCamera() ?? GetNode<Camera3D>("../Camera3D");

        // Initialize Ammo
        _currentAmmo = MaxAmmo;

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

        // Handle Reload Input
        if (Input.IsActionJustPressed("reload") && !_isReloading && _currentAmmo < MaxAmmo)
        {
            Reload();
        }

        // Handle Shooting
        if (Input.IsActionPressed("shoot") && _canFire && !_isReloading && _currentAmmo > 0)
        {
            Fire();
        }
        else if (Input.IsActionPressed("shoot") && _canFire && _currentAmmo <= 0 && !_isReloading)
        {
            // Auto-reload if empty and player is holding shoot? 
            // Optional: Uncomment below to auto-reload when clicking while empty
            // Reload();
        }
    }
    #endregion

    private void Fire()
    {
        _currentAmmo--;
        _canFire = false;
        _fireRateTimer.Start();
        _ownerPlayer.OnUpdateAmmo(_currentAmmo, MaxAmmo);

        // If we ran out of ammo, trigger reload automatically
        if (_currentAmmo <= 0 && !_isReloading)
        {
            Reload();
        }

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
        var excludeList = new Godot.Collections.Array<Rid>();

        foreach (var node in GetTree().GetNodesInGroup("_players_internal"))
        {
            if (node is Player p)
                excludeList.Add(p.GetRid());
        }

        if (excludeList.Count == 0)
        {
            excludeList.Add(_ownerPlayer.GetRid());
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

        foreach (var child in _ownerPlayer.GetChildren())
        {
            if (child is HitZone ownZone)
                excludeList.Add(ownZone.GetRid());
        }

        var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayEnd);
        query.CollideWithBodies = true;
        query.CollideWithAreas = true;
        query.CollisionMask = 0b11; 
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
                var targetPlayer = hitZone.OwnerPlayer;
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

    private async Task Reload()
    {
        if (_isReloading || _currentAmmo == MaxAmmo) return;

        _isReloading = true;
        _ownerPlayer.OnUpdateAmmo(_currentAmmo, MaxAmmo);
        GD.Print("[Weapon] Reloading...");

        // You could trigger a reload animation here via Tween or AnimationPlayer
        await ToSignal(GetTree().CreateTimer(_reloadDuration), "timeout");

        _currentAmmo = MaxAmmo;
        _isReloading = false;
        _ownerPlayer.OnUpdateAmmo(_currentAmmo, MaxAmmo);
        GD.Print("[Weapon] Reload Complete!");
    }

    #region Visual
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
            if (child is PlayerHud playerHud)
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

    // ─────────────────────────────────────────
    //  BULLET TRAIL
    // ─────────────────────────────────────────

    private void SpawnBulletTrail(Vector3 from, Vector3 to)
    {
        var trail = new BulletTrail();
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
    #endregion

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
