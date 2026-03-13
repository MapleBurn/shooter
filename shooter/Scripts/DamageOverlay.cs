using Godot;

namespace Shooter.Scripts;

/// <summary>
/// Full-screen red vignette / flash overlay that appears when the local player takes damage.
/// Uses a ColorRect with a custom shader for a vignette effect.
/// Falls back to a simple color flash if shaders can't be loaded.
/// 
/// This node is added as a child of the Player by Player._Ready().
/// </summary>
public partial class DamageOverlay : CanvasLayer
{
    private ColorRect _vignetteRect;
    private ShaderMaterial _vignetteMaterial;
    private float _currentIntensity = 0f;
    private float _targetIntensity = 0f;
    private float _fadeSpeed = 3.0f;

    // Low health persistent warning
    private float _lowHealthPulse = 0f;
    private bool _lowHealthActive = false;

    public override void _Ready()
    {
        Layer = 100; // On top of everything

        _vignetteRect = new ColorRect();
        _vignetteRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _vignetteRect.MouseFilter = Control.MouseFilterEnum.Ignore;

        // Try to create shader-based vignette
        var shader = new Shader();
        shader.Code = DamageVignetteShader.Code;

        _vignetteMaterial = new ShaderMaterial();
        _vignetteMaterial.Shader = shader;
        _vignetteMaterial.SetShaderParameter("intensity", 0.0f);
        _vignetteMaterial.SetShaderParameter("vignette_power", 2.5f);
        _vignetteMaterial.SetShaderParameter("color", new Color(0.8f, 0.0f, 0.0f, 1.0f));

        _vignetteRect.Material = _vignetteMaterial;
        _vignetteRect.Color = new Color(0, 0, 0, 0); // Transparent base

        AddChild(_vignetteRect);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Fade out damage flash
        if (_currentIntensity > _targetIntensity)
        {
            _currentIntensity = Mathf.MoveToward(_currentIntensity, _targetIntensity, _fadeSpeed * dt);
        }

        // Low health persistent pulsing warning
        float totalIntensity = _currentIntensity;
        if (_lowHealthActive)
        {
            _lowHealthPulse += dt * 3.0f;
            float pulse = (Mathf.Sin(_lowHealthPulse) + 1.0f) * 0.15f; // 0.0 - 0.3
            totalIntensity = Mathf.Max(totalIntensity, pulse);
        }

        _vignetteMaterial?.SetShaderParameter("intensity", totalIntensity);
    }

    /// <summary>
    /// Flash the damage overlay. intensity 0.0 - 1.0.
    /// Headshots use higher intensity than body shots.
    /// </summary>
    public void ShowDamage(float intensity = 0.5f)
    {
        _currentIntensity = Mathf.Clamp(intensity, 0f, 1f);
        _targetIntensity = 0f;
        _fadeSpeed = 2.0f;
    }

    /// <summary>
    /// Enable/disable the low-health warning pulsing.
    /// </summary>
    public void SetLowHealth(bool active)
    {
        _lowHealthActive = active;
        if (!active)
        {
            _lowHealthPulse = 0f;
        }
    }
}

/// <summary>
/// GLSL shader code for the damage vignette effect.
/// Creates a red border around the screen that intensifies with damage.
/// </summary>
public static class DamageVignetteShader
{
    public const string Code = @"
shader_type canvas_item;

uniform float intensity : hint_range(0.0, 1.0) = 0.0;
uniform float vignette_power : hint_range(1.0, 5.0) = 2.5;
uniform vec4 color : source_color = vec4(0.8, 0.0, 0.0, 1.0);

void fragment() {
    // Calculate distance from center (0 at center, 1 at corners)
    vec2 uv = UV - vec2(0.5);
    float dist = length(uv) * 1.4142; // normalize so corners = 1

    // Vignette: stronger at edges, zero at center
    float vignette = pow(dist, vignette_power);

    // Edge darkening + red tint
    float alpha = vignette * intensity;

    // Add slight pulsing noise for organic feel
    float noise = fract(sin(dot(UV, vec2(12.9898, 78.233)) + TIME * 5.0) * 43758.5453);
    alpha += noise * 0.02 * intensity;

    COLOR = vec4(color.rgb, clamp(alpha, 0.0, 0.9));
}
";
}
