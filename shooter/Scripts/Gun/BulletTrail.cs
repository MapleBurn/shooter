using Godot;

namespace Shooter.Scripts.Gun;

public partial class BulletTrail : MeshInstance3D
{
    private float _duration = 0.12f;
    private float _elapsed = 0f;
    private StandardMaterial3D _material;

    public void Setup(Vector3 from, Vector3 to, Color color, float duration)
    {
        _duration = duration;

        var immMesh = new ImmediateMesh();
        _material = new StandardMaterial3D();
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.AlbedoColor = color;
        _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        _material.NoDepthTest = false;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;

        Vector3 direction = (to - from);
        if (direction.LengthSquared() < 0.001f)
        {
            QueueFree();
            return;
        }
        direction = direction.Normalized();
        float thickness = 0.008f;

        Vector3 perp;
        if (Mathf.Abs(direction.Dot(Vector3.Up)) > 0.99f)
            perp = direction.Cross(Vector3.Right).Normalized() * thickness;
        else
            perp = direction.Cross(Vector3.Up).Normalized() * thickness;

        immMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip, _material);
        immMesh.SurfaceAddVertex(from + perp);
        immMesh.SurfaceAddVertex(from - perp);
        immMesh.SurfaceAddVertex(to + perp);
        immMesh.SurfaceAddVertex(to - perp);
        immMesh.SurfaceEnd();

        Mesh = immMesh;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }
    
    public override void _Process(double delta)
    {
        _elapsed += (float)delta;

        float alpha = 1.0f - (_elapsed / _duration);
        if (alpha <= 0f)
        {
            QueueFree();
            return;
        }

        if (_material != null)
        {
            var c = _material.AlbedoColor;
            _material.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
        }
    }
}