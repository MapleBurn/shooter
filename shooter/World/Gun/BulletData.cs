using Godot;

namespace Shooter.World.Gun;

public struct BulletData
{
    public Vector3 Position;
    public Vector3 Velocity;
    public float Lifetime;
    public float Penetration;
    //public ulong ShooterID;
}