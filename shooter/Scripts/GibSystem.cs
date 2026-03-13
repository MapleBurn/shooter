using Godot;
using System.Collections.Generic;

namespace Shooter.Scripts;

/// <summary>
/// Spawns breakable body part "gibs" when a player dies.
/// 
/// Now supports two modes:
/// 1. SpawnMeshGib() — uses the actual body part mesh from the player scene
///    (called by Player.DetachBodyPartGib via RPC)
/// 2. SpawnGibs()/SpawnSingleGib() — fallback procedural shapes
/// </summary>
public static class GibSystem
{
    private static readonly RandomNumberGenerator Rng = new();

    static GibSystem()
    {
        Rng.Randomize();
    }

    /// <summary>
    /// Spawns a gib using the actual body part mesh resource.
    /// The mesh is duplicated and launched as a physics body.
    /// </summary>
    public static void SpawnMeshGib(Node root, Transform3D meshWorldTransform, Mesh meshResource, string bodyPart)
    {
        var gib = new RigidBody3D();
        gib.Mass = GetGibMass(bodyPart);
        gib.GravityScale = 1.5f;
        gib.LinearDamp = 0.5f;

        // Clone the actual mesh
        var meshInstance = new MeshInstance3D();
        meshInstance.Mesh = meshResource;

        // Bloody material overlay
        var material = new StandardMaterial3D();
        material.AlbedoColor = GetGibColor(bodyPart);
        material.Roughness = 0.9f;
        meshInstance.MaterialOverride = material;

        gib.AddChild(meshInstance);

        // Simple collision shape based on body part
        var collision = new CollisionShape3D();
        var shape = new SphereShape3D();
        shape.Radius = GetGibSize(bodyPart);
        collision.Shape = shape;
        gib.AddChild(collision);

        // Put gibs on their own layer so they don't block bullets
        gib.CollisionLayer = 4; // Layer 3
        gib.CollisionMask = 1;  // Only collide with world geometry

        root.AddChild(gib);
        gib.GlobalTransform = meshWorldTransform;

        // Slight random offset so it doesn't spawn exactly on the body
        gib.GlobalPosition += new Vector3(
            Rng.RandfRange(-0.05f, 0.05f),
            Rng.RandfRange(0.0f, 0.1f),
            Rng.RandfRange(-0.05f, 0.05f)
        );

        // Launch the gib in a random upward direction
        Vector3 launchDir = new Vector3(
            Rng.RandfRange(-1f, 1f),
            Rng.RandfRange(0.5f, 2f),
            Rng.RandfRange(-1f, 1f)
        ).Normalized();

        float launchForce = Rng.RandfRange(2f, 5f);
        gib.ApplyImpulse(launchDir * launchForce);

        // Random spin
        gib.AngularVelocity = new Vector3(
            Rng.RandfRange(-8f, 8f),
            Rng.RandfRange(-8f, 8f),
            Rng.RandfRange(-8f, 8f)
        );

        // Fade out and remove
        FadeAndRemoveGib(gib, meshInstance, material);
    }

    /// <summary>
    /// Fallback: spawns multiple procedural gibs in a burst (used when no mesh available).
    /// </summary>
    public static void SpawnGibs(Node root, Vector3 origin, string killingZone)
    {
        int gibCount = killingZone switch
        {
            "head" => 5,
            _ => 2
        };

        for (int i = 0; i < gibCount; i++)
        {
            SpawnSingleGib(root, origin, killingZone);
        }
    }

    /// <summary>
    /// Fallback: spawns a single procedural gib piece.
    /// </summary>
    public static void SpawnSingleGib(Node root, Vector3 origin, string bodyPart)
    {
        var gib = new RigidBody3D();
        gib.Mass = 0.2f;
        gib.GravityScale = 1.5f;
        gib.LinearDamp = 0.5f;

        var mesh = new MeshInstance3D();
        mesh.Mesh = GetProceduralGibMesh(bodyPart);

        var material = new StandardMaterial3D();
        material.AlbedoColor = GetGibColor(bodyPart);
        material.Roughness = 0.9f;
        mesh.MaterialOverride = material;

        gib.AddChild(mesh);

        var collision = new CollisionShape3D();
        var shape = new SphereShape3D();
        shape.Radius = GetGibSize(bodyPart);
        collision.Shape = shape;
        gib.AddChild(collision);

        gib.CollisionLayer = 4;
        gib.CollisionMask = 1;

        root.AddChild(gib);
        gib.GlobalPosition = origin + new Vector3(
            Rng.RandfRange(-0.1f, 0.1f),
            Rng.RandfRange(-0.05f, 0.1f),
            Rng.RandfRange(-0.1f, 0.1f)
        );

        Vector3 launchDir = new Vector3(
            Rng.RandfRange(-1f, 1f),
            Rng.RandfRange(0.5f, 2f),
            Rng.RandfRange(-1f, 1f)
        ).Normalized();

        float launchForce = Rng.RandfRange(3f, 6f);
        gib.ApplyImpulse(launchDir * launchForce);

        gib.AngularVelocity = new Vector3(
            Rng.RandfRange(-10f, 10f),
            Rng.RandfRange(-10f, 10f),
            Rng.RandfRange(-10f, 10f)
        );

        FadeAndRemoveGib(gib, mesh, material);
    }

    private static float GetGibMass(string bodyPart)
    {
        return bodyPart switch
        {
            "head" => 0.4f,
            "torso" => 0.8f,
            "left_arm" or "right_arm" => 0.25f,
            "left_leg" or "right_leg" => 0.35f,
            _ => 0.2f
        };
    }

    private static Mesh GetProceduralGibMesh(string bodyPart)
    {
        return bodyPart switch
        {
            "head" => new SphereMesh
            {
                Radius = 0.06f,
                Height = 0.12f
            },
            "left_arm" or "right_arm" => new CapsuleMesh
            {
                Radius = 0.03f,
                Height = 0.15f
            },
            "left_leg" or "right_leg" => new CapsuleMesh
            {
                Radius = 0.035f,
                Height = 0.18f
            },
            _ => new BoxMesh
            {
                Size = new Vector3(0.06f, 0.06f, 0.06f)
            }
        };
    }

    private static Color GetGibColor(string bodyPart)
    {
        return bodyPart switch
        {
            "head" => new Color(0.75f, 0.55f, 0.5f),
            "left_arm" or "right_arm" => new Color(0.7f, 0.4f, 0.4f),
            "left_leg" or "right_leg" => new Color(0.6f, 0.35f, 0.35f),
            _ => new Color(0.5f, 0.15f, 0.15f)
        };
    }

    private static float GetGibSize(string bodyPart)
    {
        return bodyPart switch
        {
            "head" => 0.08f,
            "left_arm" or "right_arm" => 0.05f,
            "left_leg" or "right_leg" => 0.06f,
            _ => 0.05f
        };
    }

    private static async void FadeAndRemoveGib(RigidBody3D gib, MeshInstance3D mesh, StandardMaterial3D material)
    {
        // Wait before starting fade
        await gib.ToSignal(gib.GetTree().CreateTimer(3.0), "timeout");

        if (!GodotObject.IsInstanceValid(gib)) return;

        material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

        var tween = gib.GetTree().CreateTween();
        tween.TweenMethod(
            Callable.From<float>((alpha) =>
            {
                if (GodotObject.IsInstanceValid(gib))
                {
                    var c = material.AlbedoColor;
                    material.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
                }
            }),
            1.0f, 0.0f, 2.0f
        );

        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(gib))
                gib.QueueFree();
        }));
    }
}
