using Godot;
using System;
using System.Collections.Generic;
using Shooter.Scripts.PlayerLogic.slop;

namespace Shooter.Scripts.PlayerLogic;

public partial class Player : CharacterBody3D
{
    // ──────────────── Movement ────────────────
    protected float Speed = 4.0f;
    protected float JumpVelocity = 4.5f;
    protected bool IsCreativeMode = false;
    
    // ──────────────── Camera ────────────────
    [Export] public Camera3D Camera;
    private float _cameraRotationX = 0f;
    private const float MinLookAngle = -90.0f;
    private const float MaxLookAngle = 90.0f;
    protected float MouseSensitivity = 0.003f;
    
    // ──────────────── Misc ────────────────
    public static bool IsGamePaused { get; set; } = false;

    protected virtual void Move(float delta)
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

    protected virtual void Look(InputEvent @event, float sensitivity)
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
}