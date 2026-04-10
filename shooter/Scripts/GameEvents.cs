using Godot;
using System;

namespace Shooter.Scripts;

public partial class GameEvents : Node
{
    [Signal]
    public delegate void PlayerAmmoChangedEventHandler(int current, int max);
    
    public static GameEvents Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

}
