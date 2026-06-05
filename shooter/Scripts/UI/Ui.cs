using Godot;

namespace Shooter.Scripts.UI;

public partial class Ui : Control
{
	[Export] private Label _ammoLabel;
	private Player _player;
	
	public override void _Ready()
	{
		GameEvents.Instance.PlayerAmmoChanged += PlayerAmmoChanged;
	}
	
	private void PlayerAmmoChanged(int currentAmmo, int maxAmmo)
	{
		_ammoLabel.Text = $"Ammo: {currentAmmo}/{maxAmmo}";
	}
	
	// Prevent memory leaks by unsubscribing from events when the UI is removed from the scene
	public override void _ExitTree()
	{
		GameEvents.Instance.PlayerAmmoChanged -= PlayerAmmoChanged;
	}
}
