using System.Linq;
using Godot;
using Shooter.Scripts.Voxel;

namespace Shooter.Scripts.Editor;

public partial class SaveMenu : Control
{
	[Export] private OptionButton _saveOptionButton;
	[Export] private ItemList _materialList;

	public byte SelectedVoxelId;
	
	public override void _Ready()
	{

	}

	private void OnLoadMaterialsPressed()
	{
		foreach (var mat in VoxelRegistry.Materials)
		{
			_materialList.AddItem(mat.Name);
		}
	}
	
	public override void _Process(double delta)
	{
	}
	
	private void OnBtnSavePressed()
	{
		var selectedOption = _saveOptionButton.GetItemText(_saveOptionButton.Selected);
		
	}

	private void OnItemSelected(int index)
	{
		SelectedVoxelId = (byte)_materialList.GetSelectedItems().First();
		SelectedVoxelId++;
	}
}