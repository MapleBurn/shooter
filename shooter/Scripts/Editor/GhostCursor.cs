using System;
using Godot;
using Shooter.Scripts.Voxel;

namespace Shooter.Scripts.Editor;

public partial class GhostCursor : Node3D
{
    [Export] public Camera3D PlayerCamera;
    [Export] public float RayLength = 10f;

    private MeshInstance3D _ghostMesh;
    private StandardMaterial3D _ghostMaterial;

    public enum EditMode { Place, Delete }
    public EditMode CurrentMode = EditMode.Place;

    // This is the most important property: the global voxel coordinate
    public Vector3I CurrentGlobalVoxelPos { get; private set; }

    public override void _Ready()
    {
        _ghostMesh = GetNode<MeshInstance3D>("MeshInstance3D");
    }

    public override void _Input(InputEvent @event)
    {

    }

    public override void _Process(double delta)
    {
        UpdateGhostPosition();
    }

    private void UpdateGhostPosition()
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        Vector3 cameraPos = PlayerCamera.GlobalPosition;
        Vector3 cameraDir = -PlayerCamera.GlobalTransform.Basis.Z;
        
        var query = PhysicsRayQueryParameters3D.Create(cameraPos, cameraPos + cameraDir * RayLength);
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            Vector3 hitPoint = (Vector3)result["position"];
            Vector3 hitNormal = (Vector3)result["normal"];

            // moves the coordinates inside the correct voxel based on the hit face normal.
            if (CurrentMode == EditMode.Place)
                hitPoint += hitNormal * 0.1f * ChunkData.VoxelSize;
            else
                hitPoint -= hitNormal * 0.1f * ChunkData.VoxelSize;

            // SNAP TO GRID: This creates the global coordinate
            CurrentGlobalVoxelPos = new Vector3I(
                Mathf.RoundToInt(hitPoint.X / ChunkData.VoxelSize),
                Mathf.RoundToInt(hitPoint.Y / ChunkData.VoxelSize),
                Mathf.RoundToInt(hitPoint.Z / ChunkData.VoxelSize)
            );

            // Position the visual mesh (Global Pos)
            GlobalPosition = new Vector3(CurrentGlobalVoxelPos.X, CurrentGlobalVoxelPos.Y, CurrentGlobalVoxelPos.Z) * ChunkData.VoxelSize;
            GlobalRotation = Vector3.Zero;
            _ghostMesh.Visible = true;
        }
        else
        {
            _ghostMesh.Visible = false;
        }
    }
}