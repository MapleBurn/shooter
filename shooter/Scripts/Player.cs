using Godot;
using System;
using System.Collections.Generic;
using Shooter.Scripts.Voxel;

namespace Shooter.Scripts;

public partial class Player : VoxelCharacter
{
    // ──────────────── Node References ────────────────
    //public SessionManager World;
    
    // ──────────────── Movement ───────────────
    protected float Speed = 4.0f;
    protected float JumpVelocity = 4.5f;
    protected bool IsCreativeMode = false;
    
    // ──────────────── Stats ────────────────
    public int MaxHealth = 100;
    public int Health;
    
    // ──────────────── Camera ────────────────
    [Export] public Camera3D Camera;
    private float _cameraRotationX = 0f;
    private const float MinLookAngle = -90.0f;
    private const float MaxLookAngle = 90.0f;
    protected float MouseSensitivity = 0.003f;
    
    // ──────────────── Misc ────────────────
    public static bool IsGamePaused { get; set; } = false;
    
    // ──────────────── Respawn ──────────────
    [Export] public float RespawnDelay = 3.0f;
    private Vector3 _spawnPosition;
    private float _respawnTimer = 0f;
    public bool IsDead;

    public override void _Ready()
    {
        if (IsMultiplayerAuthority())
        {
            Camera.Current = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _Input(InputEvent @event)
    {
        Look(@event, MouseSensitivity);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.IsPressed() && keyEvent.Keycode == Key.C)
        {
            IsCreativeMode = !IsCreativeMode;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority())
            return;

        if (IsDead)
        {
            _respawnTimer -= (float)delta;
            if (_respawnTimer <= 0)
            {
                Respawn();
                Rpc(MethodName.OnRespawn);
            }

            _respawnTimer -= (float)delta;
            if (_respawnTimer <= 0)
            {
                Respawn();
                Rpc(MethodName.OnRespawn);
            }
        }
        else
        {
            Move((float)delta);
        }
    }
    
    
    private void Move(float delta)
    {
        Vector3 velocity = Velocity;
        var speed = Input.IsActionPressed("sprint") ? Speed * 8f : Speed;
        if (IsCreativeMode)
        {
            speed *= 2f;
        }
		
        // Add the gravity.
        if (!IsOnFloor() && !IsCreativeMode)
        {
            velocity += GetGravity() * delta;
        }

        // Handle Jump.
        if (Input.IsActionJustPressed("jump") && IsOnFloor() && !IsCreativeMode)
        {
            velocity.Y = JumpVelocity;
        }
		
        Vector2 inputDir = Input.GetVector("left", "right", "up", "down");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, speed);
        }

        if (Input.IsActionPressed("crouch"))
        {
            velocity.Y = -speed;
        }
        else if (Input.IsActionPressed("jump"))
        {
            velocity.Y = speed;
        }
        else if (IsCreativeMode)
        {
            velocity.Y = Mathf.MoveToward(Velocity.Y, 0, speed);;
        }
		
        Velocity = velocity;
        MoveAndSlide();
    }

    private void Look(InputEvent @event, float sensitivity)
    {
        if (@event is InputEventMouseMotion mouseMotion)
        {
            RotateY(-mouseMotion.Relative.X * sensitivity);

            _cameraRotationX -= mouseMotion.Relative.Y * sensitivity;
            _cameraRotationX = Mathf.Clamp(
                _cameraRotationX,
                Mathf.DegToRad(MinLookAngle),
                Mathf.DegToRad(MaxLookAngle)
            );

            Camera.Rotation = new Vector3(_cameraRotationX, 0, 0);
        }
    }

    #region Player damage, death and Respawn
    [Signal] public delegate void PlayerDiedEventHandler(string playerName);
    [Signal] public delegate void PlayerRespawnedEventHandler();
    
    public void SetSpawnPosition(Vector3 pos)
    {
        _spawnPosition = pos;
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        if (IsMultiplayerAuthority())
        {
            Health -= amount;
            if (Health <= 0)
                Rpc(MethodName.OnDeath);
        }
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void OnDeath()
    {
        IsDead = true;
        EmitSignal(SignalName.PlayerDied, Name);

        if (IsMultiplayerAuthority())
        {
            _respawnTimer = RespawnDelay;
        }
    }
    
    private void Respawn()
    {
        IsDead = false;
        Health = MaxHealth;
        
        var world = GetParent<SessionManager>();
        var spawnPoints = new List<Vector3>();
        foreach (var child in world.GetChildren())
        {
            if (child is Marker3D marker && child.Name.ToString().StartsWith("SpawnPoint"))
                spawnPoints.Add(marker.GlobalPosition);
        }

        if (spawnPoints.Count > 0)
        {
            _spawnPosition = spawnPoints[Random.Shared.Next(0, spawnPoints.Count - 1)];
        }

        GlobalPosition = _spawnPosition;
        Velocity = Vector3.Zero;
    }
    
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void OnRespawn()
    {
        IsDead = false;
    }
    #endregion
    
    // ──────────────── Weapon logic ────────────────
    public void OnUpdateAmmo(int currentAmmo, int maxAmmo)
    {
        GameEvents.Instance.EmitSignal(GameEvents.SignalName.PlayerAmmoChanged, currentAmmo, maxAmmo);
    }
}