using Godot;  
using System;  
  
namespace Shooter.Scripts;  
  
public partial class Weapon : Node3D  
{  
    [Export] public float Damage = 25.0f;  
    [Export] public float FireRate = 0.1f;  
    [Export] public float Range = 100.0f;  
    [Export] public float Spread = 0.02f; // Rozptyl v radiánech (cca 1 stupeň)  
    [Export] public float WeaponTiltAmount = 0.5f; // Jak moc se zbraň nakloní s kamerou (0-1)  
      
    private RayCast3D _rayCast;  
    private Timer _fireRateTimer;  
    private bool _canFire = true;  
    private MeshInstance3D _gunMesh;  
    private Vector3 _originalPos;  
    private Camera3D _camera;  
    private RandomNumberGenerator _rng;  
  
    public override void _Ready()  
    {  
        // Inicializace random generátoru  
        _rng = new RandomNumberGenerator();  
        _rng.Randomize();  
          
        // Získání kamery (rodič tohoto uzlu)  
        _camera = GetNode<Camera3D>("../Camera3D");  
          
        // Vytvoření RayCast3D  
        _rayCast = new RayCast3D();  
        _rayCast.TargetPosition = new Vector3(0, 0, -Range);  
        _rayCast.CollideWithAreas = false;  
        _rayCast.CollideWithBodies = true;  
        AddChild(_rayCast);  
          
        // Timer pro fire rate  
        _fireRateTimer = new Timer();  
        _fireRateTimer.WaitTime = FireRate;  
        _fireRateTimer.OneShot = true;  
        _fireRateTimer.Timeout += OnFireRateTimeout;  
        AddChild(_fireRateTimer);  
          
        // Reference na model zbraně  
        _gunMesh = GetNode<MeshInstance3D>("MeshInstance3D");  
        _originalPos = _gunMesh.Position;  
    }  
  
    public override void _Process(double delta)  
    {  
        // Vertikální míření - otáčení zbraně s kamerou  
        if (_camera != null)  
        {  
            // Získáme rotaci kamery na ose X (nahoru/dolů)  
            float cameraXRotation = _camera.Rotation.X;  
              
            // Aplikujeme jen část rotace na zbraň pro přirozenější vzhled  
            Rotation = new Vector3(cameraXRotation * WeaponTiltAmount, 0, 0);  
        }  
          
        // Střelba na levé tlačítko myši  
        if (Input.IsActionPressed("shoot") && _canFire)  
        {  
            Fire();  
        }  
    }  
  
    private void Fire()  
    {  
        _canFire = false;  
        _fireRateTimer.Start();  
          
        // Přidání rozptylu  
        Vector3 spreadOffset = new Vector3(  
            _rng.RandfRange(-Spread, Spread),  
            _rng.RandfRange(-Spread, Spread),  
            0  
        );  
          
        // Aplikace rozptylu na směr raycastu  
        Vector3 originalTarget = new Vector3(0, 0, -Range);  
        _rayCast.TargetPosition = originalTarget + (spreadOffset * Range);  
          
        // Vynutit aktualizaci raycastu  
        _rayCast.ForceRaycastUpdate();  
          
        if (_rayCast.IsColliding())  
        {  
            var collider = _rayCast.GetCollider();  
            var hitPoint = _rayCast.GetCollisionPoint();  
              
            GD.Print($"Zásah na pozici: {hitPoint}");  
              
            if (collider is Player hitPlayer)  
            {  
                // Pošleme damage přes RPC na vlastníka hráče (autoritativní peer)  
                hitPlayer.Rpc(Player.MethodName.TakeDamage, 10);  
            }  
            else if (collider is Node node && node.HasMethod("TakeDamage"))  
            {  
                node.Call("TakeDamage", Damage);  
            }  
              
            CreateHitMarker(hitPoint);  
        }  
        else  
        {  
            GD.Print("Netrefil jsi nic");  
        }  
          
        // Reset rozptylu po výstřelu  
        _rayCast.TargetPosition = new Vector3(0, 0, -Range);  
          
        // Vizuální efekt - cuknutí zbraně  
        _gunMesh.Position = _originalPos + new Vector3(0, 0, 0.1f);  
        var tween = GetTree().CreateTween();  
        tween.TweenProperty(_gunMesh, "position", _originalPos, 0.1f);  
    }  
  
    private void CreateHitMarker(Vector3 position)  
    {  
        var marker = new MeshInstance3D();  
        var sphere = new SphereMesh();  
        sphere.Radius = 0.05f;  
        sphere.Height = 0.1f;  
        marker.Mesh = sphere;  
          
        var material = new StandardMaterial3D();  
        material.AlbedoColor = new Color(1, 0, 0);  
        marker.MaterialOverride = material;  
          
        GetTree().Root.AddChild(marker);  
        marker.GlobalPosition = position;  
          
        var timer = GetTree().CreateTimer(2.0);  
        timer.Timeout += () => marker.QueueFree();  
    }  
  
    private void OnFireRateTimeout()  
    {  
        _canFire = true;  
    }  
}