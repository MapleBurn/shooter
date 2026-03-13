using Godot;

namespace Shooter.Scripts;

/// <summary>
/// In-game HUD showing health bar, crosshair (with ADS feedback), 
/// hit confirmation markers, and death/respawn screen.
/// 
/// This node is created programmatically by Player._Ready() for the local player only.
/// </summary>
public partial class PlayerHUD : CanvasLayer
{
    // Health bar
    private ProgressBar _healthBar;
    private Label _healthLabel;

    // Crosshair
    private Control _crosshairContainer;
    private ColorRect _crosshairTop;
    private ColorRect _crosshairBottom;
    private ColorRect _crosshairLeft;
    private ColorRect _crosshairRight;
    private ColorRect _crosshairDot;

    // Hit confirmation
    private float _hitConfirmTimer = 0f;
    private bool _isHeadshot = false;

    // Death screen
    private ColorRect _deathOverlay;
    private Label _deathLabel;
    private Label _respawnTimerLabel;
    private float _deathTimer = 0f;
    private bool _showingDeath = false;

    public override void _Ready()
    {
        Layer = 50;

        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        CreateHealthBar(root);
        CreateCrosshair(root);
        CreateDeathScreen(root);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Hit confirmation fade
        if (_hitConfirmTimer > 0)
        {
            _hitConfirmTimer -= dt;
            UpdateCrosshairHitColor();
        }

        // Death screen timer
        if (_showingDeath)
        {
            _deathTimer -= dt;
            if (_deathTimer > 0)
            {
                _respawnTimerLabel.Text = $"Respawn in {_deathTimer:F1}s";
            }
            else
            {
                _respawnTimerLabel.Text = "Respawning...";
            }
        }
    }

    // ═══════════════════════════════════════════
    //  HEALTH BAR
    // ═══════════════════════════════════════════

    private void CreateHealthBar(Control root)
    {
        var container = new MarginContainer();
        container.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        container.AddThemeConstantOverride("margin_left", 20);
        container.AddThemeConstantOverride("margin_bottom", 20);
        container.AddThemeConstantOverride("margin_right", 20);
        container.AddThemeConstantOverride("margin_top", 20);
        container.OffsetTop = -80;
        container.OffsetBottom = -20;
        container.OffsetLeft = 20;
        container.OffsetRight = 250;
        root.AddChild(container);

        var vbox = new VBoxContainer();
        container.AddChild(vbox);

        _healthLabel = new Label();
        _healthLabel.Text = "HP: 100";
        _healthLabel.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        _healthLabel.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(_healthLabel);

        _healthBar = new ProgressBar();
        _healthBar.MinValue = 0;
        _healthBar.MaxValue = 100;
        _healthBar.Value = 100;
        _healthBar.CustomMinimumSize = new Vector2(200, 20);
        _healthBar.ShowPercentage = false;

        // Style the health bar
        var bgStyle = new StyleBoxFlat();
        bgStyle.BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        bgStyle.CornerRadiusBottomLeft = 3;
        bgStyle.CornerRadiusBottomRight = 3;
        bgStyle.CornerRadiusTopLeft = 3;
        bgStyle.CornerRadiusTopRight = 3;
        _healthBar.AddThemeStyleboxOverride("background", bgStyle);

        var fillStyle = new StyleBoxFlat();
        fillStyle.BgColor = new Color(0.8f, 0.1f, 0.1f);
        fillStyle.CornerRadiusBottomLeft = 3;
        fillStyle.CornerRadiusBottomRight = 3;
        fillStyle.CornerRadiusTopLeft = 3;
        fillStyle.CornerRadiusTopRight = 3;
        _healthBar.AddThemeStyleboxOverride("fill", fillStyle);

        vbox.AddChild(_healthBar);
    }

    public void UpdateHealth(int current, int max)
    {
        if (_healthBar == null) return;

        _healthBar.MaxValue = max;
        _healthBar.Value = current;
        _healthLabel.Text = $"HP: {current}";

        // Change color based on health
        float ratio = (float)current / max;
        var fillStyle = new StyleBoxFlat();
        fillStyle.CornerRadiusBottomLeft = 3;
        fillStyle.CornerRadiusBottomRight = 3;
        fillStyle.CornerRadiusTopLeft = 3;
        fillStyle.CornerRadiusTopRight = 3;

        if (ratio > 0.6f)
            fillStyle.BgColor = new Color(0.1f, 0.8f, 0.1f); // Green
        else if (ratio > 0.3f)
            fillStyle.BgColor = new Color(0.9f, 0.7f, 0.1f); // Yellow
        else
            fillStyle.BgColor = new Color(0.8f, 0.1f, 0.1f); // Red

        _healthBar.AddThemeStyleboxOverride("fill", fillStyle);

        // Notify damage overlay about low health
        var player = GetParent();
        if (player != null)
        {
            foreach (var child in player.GetChildren())
            {
                if (child is DamageOverlay overlay)
                {
                    overlay.SetLowHealth(ratio <= 0.25f);
                    break;
                }
            }
        }
    }

    // ═══════════════════════════════════════════
    //  CROSSHAIR
    // ═══════════════════════════════════════════

    private void CreateCrosshair(Control root)
    {
        _crosshairContainer = new Control();
        _crosshairContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
        _crosshairContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(_crosshairContainer);

        float lineLength = 8;
        float lineWidth = 2;
        float gap = 4;
        Color crossColor = new Color(1, 1, 1, 0.8f);

        // Center dot
        _crosshairDot = CreateCrosshairLine(0, 0, 2, 2, crossColor);

        // Top line
        _crosshairTop = CreateCrosshairLine(-lineWidth / 2, -(gap + lineLength), lineWidth, lineLength, crossColor);

        // Bottom line
        _crosshairBottom = CreateCrosshairLine(-lineWidth / 2, gap, lineWidth, lineLength, crossColor);

        // Left line
        _crosshairLeft = CreateCrosshairLine(-(gap + lineLength), -lineWidth / 2, lineLength, lineWidth, crossColor);

        // Right line
        _crosshairRight = CreateCrosshairLine(gap, -lineWidth / 2, lineLength, lineWidth, crossColor);
    }

    private ColorRect CreateCrosshairLine(float x, float y, float w, float h, Color color)
    {
        var rect = new ColorRect();
        rect.Position = new Vector2(x, y);
        rect.Size = new Vector2(w, h);
        rect.Color = color;
        rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        _crosshairContainer.AddChild(rect);
        return rect;
    }

    public void ShowHitConfirmation(bool headshot)
    {
        _hitConfirmTimer = 0.3f;
        _isHeadshot = headshot;
        UpdateCrosshairHitColor();
    }

    private void UpdateCrosshairHitColor()
    {
        Color hitColor;
        if (_hitConfirmTimer > 0)
        {
            hitColor = _isHeadshot
                ? new Color(1.0f, 0.2f, 0.2f, 1.0f)  // Red for headshot
                : new Color(1.0f, 1.0f, 1.0f, 1.0f);  // Bright white for body hit
        }
        else
        {
            hitColor = new Color(1, 1, 1, 0.8f); // Normal
        }

        _crosshairDot.Color = hitColor;
        _crosshairTop.Color = hitColor;
        _crosshairBottom.Color = hitColor;
        _crosshairLeft.Color = hitColor;
        _crosshairRight.Color = hitColor;
    }

    // ═══════════════════════════════════════════
    //  DEATH SCREEN
    // ═══════════════════════════════════════════

    private void CreateDeathScreen(Control root)
    {
        _deathOverlay = new ColorRect();
        _deathOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _deathOverlay.Color = new Color(0.1f, 0, 0, 0.7f);
        _deathOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        _deathOverlay.Visible = false;
        root.AddChild(_deathOverlay);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
        vbox.GrowHorizontal = Control.GrowDirection.Both;
        vbox.GrowVertical = Control.GrowDirection.Both;
        vbox.Alignment = BoxContainer.AlignmentMode.Center;
        _deathOverlay.AddChild(vbox);

        _deathLabel = new Label();
        _deathLabel.Text = "YOU DIED";
        _deathLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _deathLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.1f, 0.1f));
        _deathLabel.AddThemeFontSizeOverride("font_size", 48);
        vbox.AddChild(_deathLabel);

        _respawnTimerLabel = new Label();
        _respawnTimerLabel.Text = "";
        _respawnTimerLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _respawnTimerLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        _respawnTimerLabel.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(_respawnTimerLabel);
    }

    public void ShowDeathScreen(float respawnTime)
    {
        _deathOverlay.Visible = true;
        _crosshairContainer.Visible = false;
        _showingDeath = true;
        _deathTimer = respawnTime;
    }

    public void HideDeathScreen()
    {
        _deathOverlay.Visible = false;
        _crosshairContainer.Visible = true;
        _showingDeath = false;
    }
}
