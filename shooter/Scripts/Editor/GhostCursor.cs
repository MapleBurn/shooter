using Godot;
using Shooter.Scripts.Voxel;

namespace Shooter.Scripts.Editor;

public partial class GhostCursor : Node3D
{
    [Export] public Camera3D PlayerCamera;
    
    public Color PlaceColor = new Color(0.2f, 1.0f, 0.25f, 0.45f); // Semi-transparent green
    public Color DeleteColor = new Color(1.0f, 0.1f, 0.1f, 0.45f); // Semi-transparent red

    public float RayLength = 100f;
    [Export] private MeshInstance3D _ghostMesh;
    private StandardMaterial3D _ghostMaterial;

    public enum EditMode
    {
        Place,
        Delete
    }

    public EditMode CurrentMode = EditMode.Place;

    public Vector3I CurrentGlobalVoxelPos { get; private set; }

    public bool HasValidTarget { get; private set; }

    public override void _Ready()
    {
        _ghostMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = PlaceColor
        };

        _ghostMesh.MaterialOverride = _ghostMaterial;
        _ghostMesh.Visible = false;
    }

    public override void _Process(double delta)
    {
        UpdateGhostPosition();
        UpdateGhostMaterial();
    }

    private void UpdateGhostPosition()
    {
        if (PlayerCamera == null || _ghostMesh == null)
        {
            HasValidTarget = false;

            if (_ghostMesh != null)
                _ghostMesh.Visible = false;

            return;
        }

        Vector2 mousePosition = GetViewport().GetMousePosition();

        Vector3 rayOrigin = PlayerCamera.ProjectRayOrigin(mousePosition);
        Vector3 rayDirection = PlayerCamera.ProjectRayNormal(mousePosition);

        var query = PhysicsRayQueryParameters3D.Create(
            rayOrigin,
            rayOrigin + rayDirection * RayLength
        );

        //query.CollisionMask = X;  // all layers by default, but can be set to ignore certain layers if needed
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (result.Count <= 0)
        {
            HasValidTarget = false;
            _ghostMesh.Visible = false;
            return;
        }

        Vector3 hitPoint = (Vector3)result["position"];
        Vector3 hitNormal = (Vector3)result["normal"];

        float offsetAmount = ChunkData.VoxelSize * 0.1f;

        if (CurrentMode == EditMode.Place)
            hitPoint += hitNormal * offsetAmount;
        else
            hitPoint -= hitNormal * offsetAmount;

        CurrentGlobalVoxelPos = new Vector3I(
            Mathf.RoundToInt(hitPoint.X / ChunkData.VoxelSize),
            Mathf.RoundToInt(hitPoint.Y / ChunkData.VoxelSize),
            Mathf.RoundToInt(hitPoint.Z / ChunkData.VoxelSize)
        );

        GlobalPosition =
            new Vector3(
                CurrentGlobalVoxelPos.X,
                CurrentGlobalVoxelPos.Y,
                CurrentGlobalVoxelPos.Z
            ) * ChunkData.VoxelSize;

        GlobalRotation = Vector3.Zero;

        HasValidTarget = true;
        _ghostMesh.Visible = true;
    }

    private void UpdateGhostMaterial()
    {
        if (_ghostMaterial == null)
            return;

        _ghostMaterial.AlbedoColor = CurrentMode == EditMode.Place
            ? PlaceColor
            : DeleteColor;
    }
}