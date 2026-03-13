using Godot;
using System.Collections.Generic;

namespace Shooter.Scripts;

public partial class World : Node3D
{
    [Export] private PackedScene _playerScene;
    [Export] private MultiplayerSpawner _spawner;

    private const string MainMenuPath = "res://Scenes/UI/main_menu.tscn";

    private List<Vector3> _spawnPoints = new();
    private int _spawnIndex = 0;

    // Cache whether we're the server, because Multiplayer.IsServer() crashes
    // after the peer has been set to null during disconnect cleanup.
    private bool _wasServer = false;

    public override void _Ready()
    {
        CollectSpawnPoints();

        _spawner.SpawnFunction = Callable.From<Variant, Node>(SpawnPlayerCallback);

        if (Multiplayer.IsServer())
        {
            _wasServer = true;
            SpawnPlayer(1);
            Multiplayer.PeerConnected += SpawnPlayer;
            Multiplayer.PeerDisconnected += DespawnPlayer;
        }

        // ── Handle host disconnection ──
        // When the host drops, all clients get this signal.
        // We return them to the main menu so they can join another session.
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    public override void _ExitTree()
    {
        Multiplayer.ServerDisconnected -= OnServerDisconnected;

        // Use cached flag — Multiplayer.IsServer() throws if peer is already null
        if (_wasServer)
        {
            Multiplayer.PeerConnected -= SpawnPlayer;
            Multiplayer.PeerDisconnected -= DespawnPlayer;
        }
    }

    private void OnServerDisconnected()
    {
        GD.Print("Host disconnected — returning to main menu.");

        // Clean up multiplayer peer
        if (Multiplayer.MultiplayerPeer != null)
        {
            Multiplayer.MultiplayerPeer.Close();
            Multiplayer.MultiplayerPeer = null;
        }

        // Reset pause state
        Player.IsGamePaused = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Return to main menu
        var err = GetTree().ChangeSceneToFile(MainMenuPath);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Failed to load main menu at '{MainMenuPath}': {err}");
            string[] alternatives = {
                "res://Scenes/main_menu.tscn",
                "res://main_menu.tscn",
                "res://Scenes/MainMenu.tscn",
            };
            foreach (var alt in alternatives)
            {
                if (ResourceLoader.Exists(alt))
                {
                    GetTree().ChangeSceneToFile(alt);
                    return;
                }
            }
        }
    }

    private void CollectSpawnPoints()
    {
        foreach (var child in GetChildren())
        {
            if (child is Marker3D marker && child.Name.ToString().StartsWith("SpawnPoint"))
                _spawnPoints.Add(marker.GlobalPosition);
        }

        if (_spawnPoints.Count == 0)
        {
            _spawnPoints.Add(new Vector3(0, 1, 0));
            _spawnPoints.Add(new Vector3(5, 1, 0));
            _spawnPoints.Add(new Vector3(-5, 1, 0));
            _spawnPoints.Add(new Vector3(0, 1, 5));
        }
    }

    private Vector3 GetNextSpawnPoint()
    {
        var point = _spawnPoints[_spawnIndex % _spawnPoints.Count];
        _spawnIndex++;
        return point;
    }

    private Node SpawnPlayerCallback(Variant data)
    {
        int id = int.Parse(data.AsString());
        var player = _playerScene.Instantiate<CharacterBody3D>();

        player.Name = id.ToString();
        player.SetMultiplayerAuthority(id);

        Vector3 spawnPos = GetNextSpawnPoint();
        player.Position = spawnPos;

        if (player is Player p)
            p.SetSpawnPosition(spawnPos);

        return player;
    }

    private void SpawnPlayer(long id)
    {
        _spawner.Spawn(id.ToString());
    }

    private void DespawnPlayer(long id)
    {
        var node = GetNodeOrNull(id.ToString());
        node?.QueueFree();
    }
}
