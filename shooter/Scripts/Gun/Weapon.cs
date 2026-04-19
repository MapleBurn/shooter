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
    
    public int MaxAmmo = 30;
    private int _currentAmmo;
    private bool _isReloading = false;
    private float _reloadDuration = 4.0f;
    private float _bulletSpeed = 500.0f;
    private float _bulletPenetration = 100.0f;

    private Timer _fireRateTimer;
    private bool _canFire = true;
    [Export] private MeshInstance3D _gunMesh;
    [Export] private Marker3D _muzzle;
    private Vector3 _originalGunPos;
    private Camera3D _camera;
    private RandomNumberGenerator _rng;
    private Player _ownerPlayer;

    private OmniLight3D _muzzleFlash;
    private GpuParticles3D _muzzleParticles;

    #region Godot Lifecycle
    public override void _Ready()
    {
        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        _ownerPlayer = GetParent<Player>();
        if (_ownerPlayer == null)
        {
            GD.PrintErr("[Weapon] Couldn't get the player!");
            return;
        }

        _camera = _ownerPlayer.Camera;
        if (_camera == null)
        {
            GD.PrintErr("[Weapon] Could not find player's camera!");
            return;
        }

        // Initialize Ammo
        _currentAmmo = MaxAmmo;

        _fireRateTimer = new Timer();
        _fireRateTimer.WaitTime = FireRate;
        _fireRateTimer.OneShot = true;
        _fireRateTimer.Timeout += () => _canFire = true;
        AddChild(_fireRateTimer);
        
        _originalGunPos = _gunMesh.Position;

        _muzzleFlash = new OmniLight3D();
        _muzzleFlash.LightColor = new Color(1.0f, 0.8f, 0.3f);
        _muzzleFlash.LightEnergy = 3.0f;
        _muzzleFlash.OmniRange = 3.0f;
        _muzzleFlash.Visible = false;
        _muzzle.AddChild(_muzzleFlash);

        CreateMuzzleParticles();
    }
    
    public override void _Process(double delta)
    {
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
            ReloadAsync();
        }

        // Handle Shooting
        if (Input.IsActionPressed("shoot") && _canFire && !_isReloading && _currentAmmo > 0)
            Fire();
    }
    #endregion

    private void Fire()
    {
        _currentAmmo--;
        _canFire = false;
        _fireRateTimer.Start();
        _ownerPlayer.OnUpdateAmmo(_currentAmmo, MaxAmmo);

        // If we ran out of ammo, trigger reload automatically
        /*if (_currentAmmo <= 0 && !_isReloading)
        {
            Reload();
        }*/

        bool isAiming = _ownerPlayer?.IsAiming ?? false;
        float spread = isAiming ? AdsSpread : HipSpread;

        Vector3 spreadOffset = new Vector3(
            _rng.RandfRange(-spread, spread),
            _rng.RandfRange(-spread, spread),
            0
        );
        
        var shootDir = (_camera.GlobalTransform.Basis * Vector3.Forward + spreadOffset).Normalized();
        BulletManager.Instance.SpawnBullet(_muzzle.GlobalPosition, shootDir, _bulletSpeed, _bulletPenetration);

        ShowMuzzleFlash();
        RecoilAnimation();

        //Rpc(MethodName.OnShotFired);
    }

    // could be done without async/await but this way it was cleaner
    private async Task ReloadAsync()
    {
        if (_isReloading || _currentAmmo == MaxAmmo) return;

        _isReloading = true;
        _ownerPlayer.OnUpdateAmmo(_currentAmmo, MaxAmmo);
        GD.Print("[Weapon] Reloading...");
        
        // continues after the timer times out
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

        _muzzle.AddChild(_muzzleParticles);
    }
    #endregion

    // ─────────────────────────────────────────
    //  NETWORK SYNC
    // ─────────────────────────────────────────

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void OnShotFired()
    {
        ShowMuzzleFlash();
        //SpawnBulletTrail(trailStart, trailEnd);

        //if (createHole)
            //CreateBulletHole(holePos, holeNormal);
    }
}
