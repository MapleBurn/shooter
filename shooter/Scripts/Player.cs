using Godot;
using System;
using System.Collections.Generic;

namespace Shooter.Scripts;

public partial class Player : CharacterBody3D
{
    // ──────────────── Movement ────────────────
    public const float Speed = 5.0f;
    public const float AdsSpeedMultiplier = 0.4f;
    public const float JumpVelocity = 4.5f;

    public float MouseSensitivity = 0.003f;
    public float AdsSensitivityMultiplier = 0.5f;
    public float MinLookAngle = -90.0f;
    public float MaxLookAngle = 90.0f;

    public float Gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();

    // ──────────────── Health & Combat ────────────────
    [Export] public int MaxHealth = 100;
    public int Health;
    public bool IsDead { get; private set; } = false;

    // Respawn
    [Export] public float RespawnDelay = 3.0f;
    private Vector3 _spawnPosition;
    private float _respawnTimer = 0f;

    // ──────────────── Limb Health (for shooting limbs off) ────────────────
    private readonly Dictionary<string, int> _limbHealth = new();
    private const int HeadLimbHp = 40;
    private const int ArmLimbHp = 30;
    private const int LegLimbHp = 35;

    // ──────────────── Pause state (shared) ────────────────
    public static bool IsGamePaused { get; set; } = false;

    // ──────────────── Node references ────────────────
    private Camera3D _camera;
    private float _cameraRotationX = 0.0f;
    private bool IsLocal => GetMultiplayerAuthority() == Multiplayer.GetUniqueId();

    // Body parts (multi-mesh structure)
    private Node3D _bodyPartsRoot;
    private readonly Dictionary<string, MeshInstance3D> _bodyParts = new();
    private readonly Dictionary<string, StandardMaterial3D> _originalMaterials = new();

    // Tracks which body parts have been detached
    private readonly HashSet<string> _detachedParts = new();

    private Node3D _weaponNode;

    // HUD & Overlays
    private PlayerHUD _hud;
    private DamageOverlay _damageOverlay;

    // Aiming
    public bool IsAiming { get; private set; } = false;
    private float _defaultFov = 75.0f;
    private float _adsFov = 45.0f;
    private float _currentFov;

    // Death flop
    private bool _deathFlopActive = false;
    private float _deathFlopTimer = 0f;
    private Vector3 _deathFlopDirection = Vector3.Zero;
    private float _deathTiltAngle = 0f;

    // ──────────────── Wound decal texture (shared) ────────────────
    private static ImageTexture _woundTexture;

    // ──────────────── Signals ────────────────
    [Signal] public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);
    [Signal] public delegate void PlayerDiedEventHandler(string playerName);
    [Signal] public delegate void PlayerRespawnedEventHandler();

    // ═══════════════════════════════════════════
    //  LIFECYCLE
    // ═══════════════════════════════════════════

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _weaponNode = GetNodeOrNull<Node3D>("Weapon");

        // Add to internal group so Weapon can find all players for raycast exclusion
        AddToGroup("_players_internal");

        // Generate wound texture once
        if (_woundTexture == null)
            _woundTexture = GenerateWoundTexture();

        // ── Find BodyParts node and all child meshes ──
        _bodyPartsRoot = GetNodeOrNull<Node3D>("BodyParts");
        if (_bodyPartsRoot != null)
        {
            foreach (var child in _bodyPartsRoot.GetChildren())
            {
                if (child is MeshInstance3D mesh)
                {
                    string zoneName = NodeNameToZone(mesh.Name);
                    _bodyParts[zoneName] = mesh;

                    if (mesh.MaterialOverride is StandardMaterial3D mat)
                        _originalMaterials[zoneName] = mat;
                    else if (mesh.GetSurfaceOverrideMaterialCount() > 0
                             && mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D surfMat)
                        _originalMaterials[zoneName] = surfMat;
                    else
                        _originalMaterials[zoneName] = null;
                }
            }
        }

        // Fallback: old single-mesh
        if (_bodyParts.Count == 0)
        {
            var singleMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
            if (singleMesh != null)
            {
                _bodyParts["torso"] = singleMesh;
                _originalMaterials["torso"] = singleMesh.MaterialOverride as StandardMaterial3D;
            }
        }

        Health = MaxHealth;
        _spawnPosition = GlobalPosition;
        _currentFov = _defaultFov;
        InitLimbHealth();

        if (IsMultiplayerAuthority())
        {
            _camera.Current = true;
            _camera.Fov = _defaultFov;
            Input.MouseMode = Input.MouseModeEnum.Captured;

            // ── FIX: Hide body parts via Camera3D CullMask ──
            // Put body meshes on layer 2, then tell the camera NOT to render layer 2.
            // This way the local player's body is invisible in first-person,
            // but other players' cameras (which render all layers) still see it.
            foreach (var (zone, mesh) in _bodyParts)
            {
                mesh.SetLayerMaskValue(1, false);
                mesh.SetLayerMaskValue(2, true);
            }

            // Camera: render layer 1 (world + weapon) but NOT layer 2 (own body)
            _camera.CullMask = 1; // Only layer 1

            _hud = new PlayerHUD();
            AddChild(_hud);
            _hud.UpdateHealth(Health, MaxHealth);

            _damageOverlay = new DamageOverlay();
            AddChild(_damageOverlay);

            SetupHitZones();
        }
        else
        {
            _camera.Current = false;
            SetPhysicsProcess(false);
            SetProcessUnhandledInput(false);

            SetupHitZones();
        }
    }

    private void InitLimbHealth()
    {
        _limbHealth["head"] = HeadLimbHp;
        _limbHealth["left_arm"] = ArmLimbHp;
        _limbHealth["right_arm"] = ArmLimbHp;
        _limbHealth["left_leg"] = LegLimbHp;
        _limbHealth["right_leg"] = LegLimbHp;
    }

    private static string NodeNameToZone(string nodeName)
    {
        return nodeName switch
        {
            "Head" => "head",
            "Torso" => "torso",
            "LeftArm" => "left_arm",
            "RightArm" => "right_arm",
            "LeftLeg" => "left_leg",
            "RightLeg" => "right_leg",
            _ => nodeName.ToLower()
        };
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsLocal || IsDead) return;
        if (IsGamePaused) return;

        float sensitivity = IsAiming
            ? MouseSensitivity * AdsSensitivityMultiplier
            : MouseSensitivity;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            RotateY(-mouseMotion.Relative.X * sensitivity);

            _cameraRotationX -= mouseMotion.Relative.Y * sensitivity;
            _cameraRotationX = Mathf.Clamp(
                _cameraRotationX,
                Mathf.DegToRad(MinLookAngle),
                Mathf.DegToRad(MaxLookAngle)
            );

            _camera.Rotation = new Vector3(_cameraRotationX, 0, 0);
        }

        if (@event.IsActionPressed("aim"))
            IsAiming = true;
        if (@event.IsActionReleased("aim"))
            IsAiming = false;
    }

    public override void _Process(double delta)
    {
        if (!IsMultiplayerAuthority()) return;
        if (IsDead || IsGamePaused) return;

        float targetFov = IsAiming ? _adsFov : _defaultFov;
        _currentFov = Mathf.Lerp(_currentFov, targetFov, (float)delta * 10.0f);
        _camera.Fov = _currentFov;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority()) return;

        if (IsDead)
        {
            // Death flop animation (tilt body and slide)
            if (_deathFlopActive)
            {
                _deathFlopTimer -= (float)delta;

                // Tilt the body parts toward the ground
                if (_bodyPartsRoot != null)
                {
                    _deathTiltAngle = Mathf.MoveToward(_deathTiltAngle, Mathf.DegToRad(90), (float)delta * 4.0f);
                    _bodyPartsRoot.Rotation = new Vector3(_deathTiltAngle, 0, 0);
                }

                // ── FIX: Keep collision enabled, just apply gravity + push ──
                // Previously collision was disabled which caused falling through floor.
                Vector3 velocity = Velocity;
                if (!IsOnFloor())
                    velocity.Y -= Gravity * (float)delta;
                else
                    velocity.Y = 0;

                velocity.X = Mathf.MoveToward(velocity.X, 0, 2.0f * (float)delta);
                velocity.Z = Mathf.MoveToward(velocity.Z, 0, 2.0f * (float)delta);

                Velocity = velocity;
                MoveAndSlide();

                if (_deathFlopTimer <= 0)
                    _deathFlopActive = false;

                // Check respawn after flop
                _respawnTimer -= (float)delta;
                if (_respawnTimer <= 0)
                {
                    Respawn();
                    Rpc(MethodName.OnRespawn);
                }
            }
            else
            {
                // Keep grounded even after flop ends
                if (!IsOnFloor())
                {
                    Vector3 velocity = Velocity;
                    velocity.Y -= Gravity * (float)delta;
                    Velocity = velocity;
                    MoveAndSlide();
                }

                _respawnTimer -= (float)delta;
                if (_respawnTimer <= 0)
                {
                    Respawn();
                    Rpc(MethodName.OnRespawn);
                }
            }
            return;
        }

        Vector3 vel = Velocity;

        if (!IsOnFloor())
            vel.Y -= Gravity * (float)delta;

        if (!IsGamePaused)
        {
            if (Input.IsActionJustPressed("jump") && IsOnFloor())
                vel.Y = JumpVelocity;

            Vector2 inputDir = Input.GetVector("left", "right", "up", "down");
            Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

            float currentSpeed = IsAiming ? Speed * AdsSpeedMultiplier : Speed;

            if (direction != Vector3.Zero)
            {
                vel.X = direction.X * currentSpeed;
                vel.Z = direction.Z * currentSpeed;
            }
            else
            {
                vel.X = Mathf.MoveToward(Velocity.X, 0, currentSpeed);
                vel.Z = Mathf.MoveToward(Velocity.Z, 0, currentSpeed);
            }
        }
        else
        {
            // Paused: still apply gravity so player doesn't freeze in midair
            vel.X = Mathf.MoveToward(vel.X, 0, Speed);
            vel.Z = Mathf.MoveToward(vel.Z, 0, Speed);
        }

        Velocity = vel;
        MoveAndSlide();
    }

    // ═══════════════════════════════════════════
    //  DAMAGE & DEATH
    // ═══════════════════════════════════════════

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void TakeDamage(int amount, string hitZone, Vector3 hitPosition, Vector3 hitNormal)
    {
        if (IsDead) return;

        float multiplier = GetDamageMultiplier(hitZone);
        int finalDamage = Mathf.RoundToInt(amount * multiplier);

        // Authority handles health changes
        if (IsMultiplayerAuthority())
        {
            Health -= finalDamage;
            Health = Mathf.Max(Health, 0);
            GD.Print($"{Name} hit in {hitZone} for {finalDamage} dmg (x{multiplier}), HP: {Health}");

            EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

            if (_hud != null)
                _hud.UpdateHealth(Health, MaxHealth);

            if (_damageOverlay != null)
                _damageOverlay.ShowDamage(hitZone == "head" ? 0.8f : 0.4f);

            // Track limb damage for shooting limbs off
            if (_limbHealth.ContainsKey(hitZone) && !_detachedParts.Contains(hitZone))
            {
                _limbHealth[hitZone] -= finalDamage;
                if (_limbHealth[hitZone] <= 0)
                {
                    // Limb shot off! Sync to all peers
                    Rpc(MethodName.DetachBodyPartGib, hitZone);
                }
            }

            if (Health <= 0)
            {
                Rpc(MethodName.OnDeath, hitZone, hitPosition);
            }
        }

        // Wound decal on ALL peers (no blood — removed by user request)
        SpawnWoundDecal(hitZone, hitPosition, hitNormal);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void TakeDamageSimple(int amount)
    {
        TakeDamage(amount, "torso", GlobalPosition + Vector3.Up, Vector3.Up);
    }

    private float GetDamageMultiplier(string zone)
    {
        return zone switch
        {
            "head" => 4.0f,
            "heart" => 3.0f,
            "torso" => 1.0f,
            "left_arm" => 0.5f,
            "right_arm" => 0.5f,
            "left_leg" => 0.6f,
            "right_leg" => 0.6f,
            _ => 1.0f
        };
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void OnDeath(string killingZone, Vector3 hitPos)
    {
        IsDead = true;
        GD.Print($"{Name} died! (hit in {killingZone})");

        EmitSignal(SignalName.PlayerDied, Name);

        if (IsMultiplayerAuthority())
        {
            _respawnTimer = RespawnDelay;

            if (_hud != null)
                _hud.ShowDeathScreen(RespawnDelay);

            // ── FIX: Do NOT disable collision. Keep it so MoveAndSlide works. ──
            // Previously: collision.Disabled = true → fell through floor

            // Start death flop
            _deathFlopActive = true;
            _deathFlopTimer = 1.5f;
            _deathTiltAngle = 0f;

            // Push body in direction of the shot
            Vector3 pushDir = (GlobalPosition - hitPos);
            if (pushDir.LengthSquared() > 0.001f)
                pushDir = pushDir.Normalized();
            else
                pushDir = Vector3.Back;
            pushDir.Y = 0.3f;
            Velocity = pushDir * 3.0f;
        }

        // Death material on ALL body parts
        ApplyDeathMaterial();

        // Detach killing zone body part
        if (killingZone == "head" || killingZone.Contains("arm") || killingZone.Contains("leg"))
        {
            if (!_detachedParts.Contains(killingZone))
                Rpc(MethodName.DetachBodyPartGib, killingZone);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void OnRespawn()
    {
        IsDead = false;
        _deathFlopActive = false;
        _deathTiltAngle = 0f;

        // Reset body tilt
        if (_bodyPartsRoot != null)
            _bodyPartsRoot.Rotation = Vector3.Zero;

        RestoreAllBodyParts();
    }

    private void Respawn()
    {
        IsDead = false;
        Health = MaxHealth;
        _deathFlopActive = false;
        _deathTiltAngle = 0f;

        if (_bodyPartsRoot != null)
            _bodyPartsRoot.Rotation = Vector3.Zero;

        var world = GetParent();
        if (world != null)
        {
            var spawnPoints = new List<Vector3>();
            foreach (var child in world.GetChildren())
            {
                if (child is Marker3D marker && child.Name.ToString().StartsWith("SpawnPoint"))
                    spawnPoints.Add(marker.GlobalPosition);
            }

            if (spawnPoints.Count > 0)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                _spawnPosition = spawnPoints[rng.RandiRange(0, spawnPoints.Count - 1)];
            }
        }

        GlobalPosition = _spawnPosition;
        Velocity = Vector3.Zero;

        RestoreAllBodyParts();
        InitLimbHealth();

        if (_hud != null)
        {
            _hud.UpdateHealth(Health, MaxHealth);
            _hud.HideDeathScreen();
        }

        SetupHitZones();

        EmitSignal(SignalName.PlayerRespawned);
        GD.Print($"{Name} respawned!");
    }

    // ═══════════════════════════════════════════
    //  BODY PART MANAGEMENT
    // ═══════════════════════════════════════════

    private void ApplyDeathMaterial()
    {
        var deathMat = new StandardMaterial3D();
        deathMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        deathMat.AlbedoColor = new Color(1, 0.2f, 0.2f, 0.4f);

        foreach (var (zone, mesh) in _bodyParts)
        {
            if (GodotObject.IsInstanceValid(mesh))
                mesh.MaterialOverride = deathMat;
        }
    }

    private void RestoreAllBodyParts()
    {
        foreach (var (zone, mesh) in _bodyParts)
        {
            if (GodotObject.IsInstanceValid(mesh))
            {
                mesh.MaterialOverride = _originalMaterials.GetValueOrDefault(zone);
                mesh.Visible = true;
            }
        }

        foreach (string detached in _detachedParts)
        {
            if (_bodyParts.TryGetValue(detached, out var mesh) && GodotObject.IsInstanceValid(mesh))
                mesh.Visible = true;
        }
        _detachedParts.Clear();

        // Remove wound decals from BodyParts node
        if (_bodyPartsRoot != null)
        {
            foreach (var child in _bodyPartsRoot.GetChildren())
            {
                if (child is MeshInstance3D && child.Name.ToString().StartsWith("Wound_"))
                    child.QueueFree();
            }
        }

        if (IsMultiplayerAuthority())
        {
            foreach (var (zone, mesh) in _bodyParts)
            {
                if (GodotObject.IsInstanceValid(mesh))
                {
                    mesh.SetLayerMaskValue(1, false);
                    mesh.SetLayerMaskValue(2, true);
                }
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void DetachBodyPartGib(string zone)
    {
        if (_detachedParts.Contains(zone)) return;
        if (!_bodyParts.TryGetValue(zone, out var sourceMesh)) return;
        if (!GodotObject.IsInstanceValid(sourceMesh)) return;

        _detachedParts.Add(zone);

        var worldTransform = sourceMesh.GlobalTransform;
        var meshResource = sourceMesh.Mesh;

        sourceMesh.Visible = false;

        if (meshResource != null)
        {
            GibSystem.SpawnMeshGib(GetTree().Root, worldTransform, meshResource, zone);
        }
    }

    // ═══════════════════════════════════════════
    //  HIT ZONES
    // ═══════════════════════════════════════════

    private void SetupHitZones()
    {
        foreach (var child in GetChildren())
        {
            if (child is HitZone) child.QueueFree();
        }

        if (_bodyPartsRoot != null && _bodyParts.Count > 0)
            SetupHitZonesFromBodyParts();
        else
            SetupHitZonesHardcoded();
    }

    private void SetupHitZonesFromBodyParts()
    {
        // ── IMPORTANT: Add limbs FIRST, torso LAST ──
        // Godot's IntersectRay returns whichever Area3D it finds first when
        // shapes overlap. By adding limbs before torso, and keeping the torso
        // narrower than the arm positions, limbs get priority.

        // ── Head ──
        if (_bodyParts.TryGetValue("head", out var headMesh))
        {
            var aabb = headMesh.GetAabb();
            float radius = Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f;
            var headCenter = GetMeshLocalCenter(headMesh);
            AddHitZone("head", headCenter, new SphereShape3D { Radius = Mathf.Max(radius, 0.14f) });
        }
        else
            AddHitZone("head", new Vector3(0, 1.65f, 0), new SphereShape3D { Radius = 0.15f });

        // ── Arms (before torso so they get raycast priority) ──
        SetupLimbHitZone("left_arm",
            new CapsuleShape3D { Radius = 0.09f, Height = 0.55f },
            new Vector3(0.3f, 1.1f, 0));
        SetupLimbHitZone("right_arm",
            new CapsuleShape3D { Radius = 0.09f, Height = 0.55f },
            new Vector3(-0.3f, 1.1f, 0));

        // ── Legs (before torso) ──
        SetupLimbHitZone("left_leg",
            new CapsuleShape3D { Radius = 0.1f, Height = 0.75f },
            new Vector3(0.1f, 0.4f, 0));
        SetupLimbHitZone("right_leg",
            new CapsuleShape3D { Radius = 0.1f, Height = 0.75f },
            new Vector3(-0.1f, 0.4f, 0));

        // ── Heart (small sphere inside torso) ──
        if (_bodyParts.TryGetValue("torso", out var torsoMeshForHeart))
        {
            var heartCenter = GetMeshLocalCenter(torsoMeshForHeart);
            Vector3 heartPos = heartCenter + new Vector3(0.08f, 0.1f, 0);
            AddHitZone("heart", heartPos, new SphereShape3D { Radius = 0.1f });
        }
        else
            AddHitZone("heart", new Vector3(0.08f, 1.2f, 0), new SphereShape3D { Radius = 0.1f });

        // ── Torso (LAST — acts as a catch-all for center-mass hits) ──
        // Keep it narrow (X) so it doesn't overlap the arm hitboxes.
        if (_bodyParts.TryGetValue("torso", out var torsoMesh))
        {
            var aabb = torsoMesh.GetAabb();
            var torsoCenter = GetMeshLocalCenter(torsoMesh);
            // Width: use 80% of mesh width, but cap it so it doesn't cover arms
            float torsoWidth = Mathf.Min(aabb.Size.X * 0.8f, 0.28f);
            AddHitZone("torso", torsoCenter,
                new BoxShape3D { Size = new Vector3(
                    Mathf.Max(torsoWidth, 0.2f),
                    Mathf.Max(aabb.Size.Y, 0.35f),
                    Mathf.Max(aabb.Size.Z, 0.18f))
                });
        }
        else
        {
            AddHitZone("torso", new Vector3(0, 1.1f, 0), new BoxShape3D { Size = new Vector3(0.28f, 0.45f, 0.22f) });
        }

        // Debug: print all hitzone positions and sizes
        foreach (var child in GetChildren())
        {
            if (child is HitZone hz)
            {
                var cs = hz.GetChild<CollisionShape3D>(0);
                GD.Print($"  HitZone '{hz.ZoneName}' pos={hz.Position} shape={cs.Shape}");
            }
        }
    }

    private void SetupLimbHitZone(string zone, Shape3D fallbackShape, Vector3 fallbackPos)
    {
        if (_detachedParts.Contains(zone)) return;

        // Extract fallback sizes
        float fallbackRadius = 0.08f;
        float fallbackHeight = 0.4f;
        if (fallbackShape is CapsuleShape3D fallbackCapsule)
        {
            fallbackRadius = fallbackCapsule.Radius;
            fallbackHeight = fallbackCapsule.Height;
        }

        if (_bodyParts.TryGetValue(zone, out var mesh))
        {
            var aabb = mesh.GetAabb();
            var center = GetMeshLocalCenter(mesh);

            // Use generous sizes — limbs should be easy to hit
            float radius = Mathf.Max(aabb.Size.X, aabb.Size.Z) * 0.5f;
            float height = aabb.Size.Y;

            var shape = new CapsuleShape3D
            {
                Radius = Mathf.Max(radius, fallbackRadius),
                Height = Mathf.Max(height, fallbackHeight)
            };
            AddHitZone(zone, center, shape);
        }
        else
            AddHitZone(zone, fallbackPos, fallbackShape);
    }

    /// <summary>
    /// Get the center of a body part mesh in Player-local space.
    /// This accounts for the AABB offset (mesh geometry may not be centered on the node origin)
    /// and the BodyParts parent transform.
    /// </summary>
    private Vector3 GetMeshLocalCenter(MeshInstance3D mesh)
    {
        Aabb aabb = mesh.GetAabb();
        Vector3 localCenter = aabb.Position + aabb.Size * 0.5f;
        // Transform from mesh-local space to Player-local space
        // mesh is child of BodyParts, which is child of Player
        Vector3 worldCenter = mesh.GlobalTransform * localCenter;
        return ToLocal(worldCenter);
    }

    private void SetupHitZonesHardcoded()
    {
        // Limbs first for raycast priority
        AddHitZone("head", new Vector3(0, 1.65f, 0), new SphereShape3D { Radius = 0.15f });
        AddHitZone("left_arm", new Vector3(0.3f, 1.1f, 0), new CapsuleShape3D { Radius = 0.09f, Height = 0.55f });
        AddHitZone("right_arm", new Vector3(-0.3f, 1.1f, 0), new CapsuleShape3D { Radius = 0.09f, Height = 0.55f });
        AddHitZone("left_leg", new Vector3(0.1f, 0.4f, 0), new CapsuleShape3D { Radius = 0.1f, Height = 0.75f });
        AddHitZone("right_leg", new Vector3(-0.1f, 0.4f, 0), new CapsuleShape3D { Radius = 0.1f, Height = 0.75f });
        AddHitZone("heart", new Vector3(0.08f, 1.2f, 0), new SphereShape3D { Radius = 0.1f });
        AddHitZone("torso", new Vector3(0, 1.1f, 0), new BoxShape3D { Size = new Vector3(0.28f, 0.45f, 0.22f) });
    }

    private void AddHitZone(string zoneName, Vector3 position, Shape3D shape)
    {
        var hitZone = new HitZone();
        hitZone.ZoneName = zoneName;
        hitZone.OwnerPlayer = this;
        hitZone.Name = $"HitZone_{zoneName}";
        hitZone.Position = position;

        hitZone.CollisionLayer = 2;
        hitZone.CollisionMask = 0;
        hitZone.Monitorable = true;
        hitZone.Monitoring = false;

        var collisionShape = new CollisionShape3D();
        collisionShape.Shape = shape;
        hitZone.AddChild(collisionShape);

        AddChild(hitZone);
    }

    // ═══════════════════════════════════════════
    //  VISUAL EFFECTS — Wound Decals
    // ═══════════════════════════════════════════

    /// <summary>
    /// Places a red wound mark directly on the body part mesh that was hit.
    ///
    /// The wound is a child of the BodyParts Node3D (not the mesh itself) so it
    /// moves with the player but its layer mask is independent.
    ///
    /// Positioning uses the mesh's AABB transformed to world space to find the
    /// actual center of the mesh geometry, then projects outward from that center
    /// toward the hit point to place the wound on the mesh surface.
    /// </summary>
    private void SpawnWoundDecal(string hitZone, Vector3 hitPosition, Vector3 hitNormal)
    {
        if (_bodyPartsRoot == null) return;

        // ── Find the target body part mesh ──
        string meshZone = hitZone;
        if (meshZone == "heart") meshZone = "torso";

        MeshInstance3D targetMesh = null;
        if (_bodyParts.TryGetValue(meshZone, out var zoneMesh) && GodotObject.IsInstanceValid(zoneMesh) && zoneMesh.Visible)
        {
            targetMesh = zoneMesh;
        }
        else
        {
            // Fallback: closest visible body part
            float closestDist = float.MaxValue;
            foreach (var (zone, mesh) in _bodyParts)
            {
                if (!GodotObject.IsInstanceValid(mesh) || !mesh.Visible) continue;
                float dist = GetMeshWorldCenter(mesh).DistanceTo(hitPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    targetMesh = mesh;
                }
            }
        }

        if (targetMesh == null) return;

        // ── Get the actual geometric center of the mesh in world space ──
        // GlobalPosition is the node's transform origin, which may be at the
        // bottom/pivot of the mesh. The AABB center gives the real geometry center.
        Vector3 meshCenterWorld = GetMeshWorldCenter(targetMesh);

        // Direction from the mesh center to where the shot hit
        Vector3 dirToHit = (hitPosition - meshCenterWorld);
        if (dirToHit.LengthSquared() < 0.0001f)
            dirToHit = -targetMesh.GlobalTransform.Basis.Z;

        Vector3 outwardDir = dirToHit.Normalized();

        // ── Place wound on the mesh surface by clamping to AABB ──
        // Transform hit position into the mesh's local space, clamp it to the
        // AABB boundary, then transform back. This pulls any exterior hit point
        // (e.g. on the CollisionShape3D) inward onto the actual mesh surface.
        Aabb aabb = targetMesh.GetAabb();
        Vector3 aabbCenter = aabb.Position + aabb.Size * 0.5f;
        Vector3 aabbHalfExt = aabb.Size * 0.5f;

        // Hit position in mesh-local space
        Vector3 localHit = targetMesh.GlobalTransform.AffineInverse() * hitPosition;

        // Direction from AABB center to local hit (for projection)
        Vector3 localDirToHit = localHit - aabbCenter;
        if (localDirToHit.LengthSquared() < 0.0001f)
            localDirToHit = new Vector3(0, 0, -1);

        // Project from AABB center outward to find surface point in that direction
        // Uses ray-AABB intersection: find the smallest t such that center + t*dir
        // reaches a face of the AABB
        Vector3 d = localDirToHit.Normalized();
        float tx = Mathf.Abs(d.X) > 0.001f ? aabbHalfExt.X / Mathf.Abs(d.X) : float.MaxValue;
        float ty = Mathf.Abs(d.Y) > 0.001f ? aabbHalfExt.Y / Mathf.Abs(d.Y) : float.MaxValue;
        float tz = Mathf.Abs(d.Z) > 0.001f ? aabbHalfExt.Z / Mathf.Abs(d.Z) : float.MaxValue;
        float tSurface = Mathf.Min(tx, Mathf.Min(ty, tz));

        // The wound sits on the AABB surface in the direction of the hit
        Vector3 localWound = aabbCenter + d * tSurface;

        // Transform back to world space and nudge slightly outward
        Vector3 woundWorldPos = targetMesh.GlobalTransform * localWound + outwardDir * 0.002f;

        // ── Create wound quad ──
        var wound = new MeshInstance3D();
        wound.Name = $"Wound_{hitZone}_{GD.Randi()}";

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.05f, 0.05f);
        wound.Mesh = quadMesh;

        // Bright red wound material
        var mat = new StandardMaterial3D();
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.AlbedoColor = new Color(0.8f, 0.08f, 0.08f, 0.9f);
        mat.AlbedoTexture = _woundTexture;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        mat.NoDepthTest = false;
        wound.MaterialOverride = mat;
        wound.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

        // Visible on BOTH visual layers
        wound.SetLayerMaskValue(1, true);
        wound.SetLayerMaskValue(2, true);

        // ── Orient quad to face outward ──
        Vector3 fwd = -outwardDir;
        Vector3 right;
        if (Mathf.Abs(fwd.Dot(Vector3.Up)) > 0.99f)
            right = fwd.Cross(Vector3.Right).Normalized();
        else
            right = fwd.Cross(Vector3.Up).Normalized();
        Vector3 up = right.Cross(fwd).Normalized();

        var woundWorldTransform = new Transform3D(new Basis(right, up, fwd), woundWorldPos);

        // ── Attach to BodyParts node (not the mesh) ──
        // Convert to local space of BodyParts
        var parentInverse = _bodyPartsRoot.GlobalTransform.AffineInverse();
        var localTransform = parentInverse * woundWorldTransform;

        _bodyPartsRoot.AddChild(wound);
        wound.Transform = localTransform;
    }

    /// <summary>
    /// Returns the world-space center of a mesh's AABB, accounting for
    /// the fact that GlobalPosition is the node origin, not the geometry center.
    /// </summary>
    private static Vector3 GetMeshWorldCenter(MeshInstance3D mesh)
    {
        Aabb aabb = mesh.GetAabb();
        Vector3 localCenter = aabb.Position + aabb.Size * 0.5f;
        return mesh.GlobalTransform * localCenter;
    }

    /// <summary>
    /// Generates a circular wound texture with bright red tones.
    /// White in the texture gets multiplied by AlbedoColor in the material,
    /// so we use white-to-transparent for the shape and let the material color it red.
    /// </summary>
    private static ImageTexture GenerateWoundTexture()
    {
        int size = 32;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float center = size / 2.0f;
        float outerRadius = size / 2.0f;
        float innerRadius = size / 5.0f;

        var rng = new RandomNumberGenerator();
        rng.Randomize();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Add slight noise for organic look
                float noise = rng.RandfRange(-0.8f, 0.8f);
                float noisyDist = dist + noise;

                if (noisyDist > outerRadius)
                {
                    image.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
                else if (noisyDist < innerRadius)
                {
                    // Bright center (white — will be tinted red by AlbedoColor)
                    float v = rng.RandfRange(0.85f, 1.0f);
                    image.SetPixel(x, y, new Color(v, v, v, 1.0f));
                }
                else
                {
                    float t = (noisyDist - innerRadius) / (outerRadius - innerRadius);
                    float alpha = (1.0f - t) * 0.9f;
                    float v = 1.0f - t * 0.3f; // Bright white fading outward
                    image.SetPixel(x, y, new Color(v, v, v, alpha));
                }
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    // ═══════════════════════════════════════════
    //  PUBLIC HELPERS
    // ═══════════════════════════════════════════

    public Camera3D GetCamera() => _camera;

    public void SetSpawnPosition(Vector3 pos)
    {
        _spawnPosition = pos;
    }

    public MeshInstance3D GetBodyPart(string zone)
    {
        return _bodyParts.GetValueOrDefault(zone);
    }
}
