using Godot;

namespace Shooter.Scripts;

/// <summary>
/// An Area3D that represents a hittable body part on a player.
/// The Weapon's raycast checks for these to determine which zone was hit
/// and applies the appropriate damage multiplier.
///
/// Hit zones are created programmatically by Player.SetupHitZones().
/// They sit on collision layer 2 so they don't interfere with CharacterBody3D movement
/// (which uses layer 1 by default).
/// </summary>
public partial class HitZone : Area3D
{
    /// <summary>
    /// Name of the body zone: "head", "torso", "heart", "left_arm", "right_arm", "left_leg", "right_leg"
    /// </summary>
    public string ZoneName { get; set; } = "torso";

    /// <summary>
    /// Reference to the Player that owns this hit zone.
    /// </summary>
    public Player OwnerPlayer { get; set; }
}
