using Godot;

namespace Shooter.Scripts;

public partial class World : Node3D
{
    [Export] private PackedScene _playerScene;
    [Export] private MultiplayerSpawner _spawner;

    public override void _Ready()
    {
        // Nastavení spawneru
        _spawner.SpawnFunction = Callable.From<Variant, Node>(SpawnPlayerCallback);

        if (Multiplayer.IsServer())
        {
            // Host (ID 1)
            SpawnPlayer(1);

            Multiplayer.PeerConnected += SpawnPlayer;
            Multiplayer.PeerDisconnected += DespawnPlayer;
        }
    }

    private Node SpawnPlayerCallback(Variant data)
    {
        int id = int.Parse(data.AsString());
        var player = _playerScene.Instantiate<CharacterBody3D>();
        
        // Jméno musí být unikátní (ID hráče)
        player.Name = id.ToString();
        
        // Nastavíme, kdo uzel ovládá, JEŠTĚ PŘED přidáním do scény
        player.SetMultiplayerAuthority(id);
        
        return player;
    }

    private void SpawnPlayer(long id)
    {
        // Spawner.Spawn automaticky zavolá SpawnPlayerCallback na všech klientech
        _spawner.Spawn(id.ToString());
    }

    private void DespawnPlayer(long id)
    {
        var node = GetNodeOrNull(id.ToString());
        node?.QueueFree();
    }
}