using Godot;

namespace Shooter.Scripts.Gun;

public struct BulletData
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Lifetime;
    public float Penetration;
    //public ulong ShooterID;
}