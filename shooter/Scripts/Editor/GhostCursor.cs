using Godot;

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

            // Offset slightly to ensure we are "inside" the target voxel
            Vector3 targetWorldPos = (CurrentMode == EditMode.Place) 
                ? hitPoint + (hitNormal * 0.1f) 
                : hitPoint - (hitNormal * 0.1f);

            // SNAP TO GRID: This creates the global coordinate
            CurrentGlobalVoxelPos = new Vector3I(
                Mathf.FloorToInt(targetWorldPos.X),
                Mathf.FloorToInt(targetWorldPos.Y),
                Mathf.FloorToInt(targetWorldPos.Z)
            );

            // Position the visual mesh (Global Pos + 0.5 to center it in the voxel)
            GlobalPosition = new Vector3(CurrentGlobalVoxelPos.X, CurrentGlobalVoxelPos.Y, CurrentGlobalVoxelPos.Z) + new Vector3(0.5f, 0.5f, 0.5f);
            _ghostMesh.Visible = true;
        }
        else
        {
            _ghostMesh.Visible = false;
        }
    }
}