using Godot;  
using System;  
  
namespace Shooter.Scripts;  
  
public partial class Player : CharacterBody3D  
{  
    public const float Speed = 5.0f;  
    public const float JumpVelocity = 4.5f;  
      
    public float MouseSensitivity = 0.003f;  
    public float MinLookAngle = -90.0f;  
    public float MaxLookAngle = 90.0f;  
  
    public float Gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();  
      
    public int Health = 100;  
  
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]  
    public void TakeDamage(int amount)  
    {  
        // Spustí se na všech peerech – ale zdraví odečítáme jen na autoritě (serveru/vlastníkovi)  
        if (!IsMultiplayerAuthority())  
            return;  
  
        Health -= amount;  
        GD.Print($"{Name} dostal {amount} damage, zbývá HP: {Health}");  
  
        if (Health <= 0)  
        {  
            GD.Print($"{Name} zemřel!");  
            // TODO: logika smrti (respawn, atd.)  
        }  
    }  
      
    private Camera3D _camera;  
    private float _cameraRotationX = 0.0f;  
    private bool IsLocal => GetMultiplayerAuthority() == Multiplayer.GetUniqueId();

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        // MultiplayerSynchronizer nepotřebuješ volat v kódu,   
        // pokud jsi nastavil vlastnosti v editoru.  

        if (IsMultiplayerAuthority())
        {
            _camera.Current = true;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
        else
        {
            _camera.Current = false;

            // Vypneme procesy, aby cizí hráč neběhal podle našeho inputu  
            // a nepočítal vlastní gravitaci (bude se hýbat podle dat ze sítě)  
            SetProcess(false);
            SetPhysicsProcess(false);
            SetProcessUnhandledInput(false);
        }
    }

    public override void _Input(InputEvent @event)  
    {  
        // Cizí hráče nikdy neovládáme  
        if (!IsLocal)  
            return;  
  
        if (@event is InputEventMouseMotion mouseMotion)  
        {  
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);  
              
            _cameraRotationX -= mouseMotion.Relative.Y * MouseSensitivity;  
            _cameraRotationX = Mathf.Clamp(  
                _cameraRotationX,  
                Mathf.DegToRad(MinLookAngle),  
                Mathf.DegToRad(MaxLookAngle)  
            );  
              
            _camera.Rotation = new Vector3(_cameraRotationX, 0, 0);  
        }  
          
        if (Input.IsActionJustPressed("escape"))  
        {  
            if (!IsMultiplayerAuthority()) return;
            Input.MouseMode = Input.MouseModeEnum.Visible;  
        }  
    }  
  
    public override void _PhysicsProcess(double delta)  
    {  
        // Pohyb počítá jen vlastník uzlu  
        if (!IsMultiplayerAuthority()) return;
  
        Vector3 velocity = Velocity;  
  
        if (!IsOnFloor())  
            velocity.Y -= Gravity * (float)delta;  
  
        if (Input.IsActionJustPressed("jump") && IsOnFloor())  
            velocity.Y = JumpVelocity;  
  
        Vector2 inputDir = Input.GetVector("left", "right", "up", "down");  
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();  
          
        if (direction != Vector3.Zero)  
        {  
            velocity.X = direction.X * Speed;  
            velocity.Z = direction.Z * Speed;  
        }  
        else  
        {  
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);  
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);  
        }  
  
        Velocity = velocity;  
        MoveAndSlide();  
    }  
}