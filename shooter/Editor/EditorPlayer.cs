using Godot;
using VoxelWorld = Shooter.Voxel.VoxelWorld;

namespace Shooter.Editor;

public partial class EditorPlayer : CharacterBody3D
{
    [Export] public GhostCursor GhostCursor;
    [Export] public VoxelWorld World;
    [Export] public ColorPicker Picker;
    [Export] public SaveMenu MaterialMenu;
    
    // --------------- Camera control parameters ---------------
    [Export] public Camera3D EditorCamera;
    private float _orbitSensitivity = 0.006f;
    private float _panSensitivity = 0.0025f;
    private float _zoomStep = 1.0f;
    private float _minZoom = 2.0f;
    private float _maxZoom = 80.0f;
    private float _defaultZoom = 12.0f;
    private float _minPitchDegrees = -89.0f;
    private float _maxPitchDegrees = 89.0f;

    private bool _isDeleteMode;
    private bool _isMiddleMouseDown;

    private float _yaw;
    private float _pitch;
    private float _zoom;

    public bool IsGamePaused = false;

    public override void _Ready()
    {
        CacheCameraRigFromCurrentTransform();
        ApplyCameraRig();
    }

    public override void _Input(InputEvent @event)
    {
        if (IsGamePaused)
        {
            _isMiddleMouseDown = false;
            return;
        }

        UpdateEditMode();

        if (@event is InputEventMouseButton mouseButton)
        {
            HandleMouseButton(mouseButton);
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleMouseMotion(mouseMotion);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsGamePaused) return;

        UpdateEditMode();
    }

    private void HandleMouseButton(InputEventMouseButton mouseButton)
    {
        if (mouseButton.ButtonIndex == MouseButton.Middle)
        {
            _isMiddleMouseDown = mouseButton.Pressed;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!mouseButton.Pressed)
            return;

        if (mouseButton.ButtonIndex == MouseButton.Left)  
        {
            PlaceOrBreak();  
            return;  
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelUp)
        {
            Zoom(-_zoomStep);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (mouseButton.ButtonIndex == MouseButton.WheelDown)
        {
            Zoom(_zoomStep);
            GetViewport().SetInputAsHandled();
        }
    }

    private void HandleMouseMotion(InputEventMouseMotion mouseMotion)
    {
        if (!_isMiddleMouseDown || EditorCamera == null)
            return;

        if (Input.IsKeyPressed(Key.Shift))
            Pan(mouseMotion.Relative);
        else
            Orbit(mouseMotion.Relative);

        GetViewport().SetInputAsHandled();
    }

    private void UpdateEditMode()
    {
        _isDeleteMode = Input.IsKeyPressed(Key.Z);

        if (GhostCursor == null)
            return;

        GhostCursor.CurrentMode = _isDeleteMode
            ? GhostCursor.EditMode.Delete
            : GhostCursor.EditMode.Place;
    }

    private void PlaceOrBreak()
    {
        if (GhostCursor == null || World == null || Picker == null)
            return;

        if (!GhostCursor.HasValidTarget)
            return;
        
        if (_isDeleteMode)
            World.StaticMap.SetVoxel(GhostCursor.CurrentGlobalVoxelPos, 0, Picker.Color);
        else
            World.StaticMap.SetVoxel(GhostCursor.CurrentGlobalVoxelPos, MaterialMenu.SelectedVoxelId, Picker.Color);
    }

    private void Orbit(Vector2 mouseDelta)
    {
        _yaw -= mouseDelta.X * _orbitSensitivity;
        _pitch -= mouseDelta.Y * _orbitSensitivity;

        float minPitch = Mathf.DegToRad(_minPitchDegrees);
        float maxPitch = Mathf.DegToRad(_maxPitchDegrees);

        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        ApplyCameraRig();
    }

    private void Pan(Vector2 mouseDelta)
    {
        if (EditorCamera == null)
            return;

        Vector3 cameraRight = EditorCamera.GlobalTransform.Basis.X;
        Vector3 cameraUp = EditorCamera.GlobalTransform.Basis.Y;

        float scaledPanSpeed = _panSensitivity * _zoom;

        Vector3 pan =
            (-cameraRight * mouseDelta.X + cameraUp * mouseDelta.Y) *
            scaledPanSpeed;

        GlobalPosition += pan;

        ApplyCameraRig();
    }

    private void Zoom(float amount)
    {
        _zoom = Mathf.Clamp(_zoom + amount, _minZoom, _maxZoom);
        ApplyCameraRig();
    }

    private void CacheCameraRigFromCurrentTransform()
    {
        _zoom = _defaultZoom;

        if (EditorCamera == null)
            return;

        Vector3 offset = EditorCamera.GlobalPosition - GlobalPosition;

        if (offset.LengthSquared() <= 0.001f)
            return;

        _zoom = Mathf.Clamp(offset.Length(), _minZoom, _maxZoom);

        Vector3 direction = offset.Normalized();

        _yaw = Mathf.Atan2(direction.X, direction.Z);
        _pitch = Mathf.Asin(Mathf.Clamp(direction.Y, -0.999f, 0.999f));
    }

    private void ApplyCameraRig()
    {
        if (EditorCamera == null)
            return;

        float cosPitch = Mathf.Cos(_pitch);

        Vector3 cameraOffset = new Vector3(
            Mathf.Sin(_yaw) * cosPitch,
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * cosPitch
        ) * _zoom;

        EditorCamera.GlobalPosition = GlobalPosition + cameraOffset;
        EditorCamera.LookAt(GlobalPosition, Vector3.Up);
    }
}