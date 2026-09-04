using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Shooter.Scripts.Voxel.Resources;

namespace Shooter.Scripts.Voxel;

public static class ChunkSaver
{
    private const string SaveDir = "user://Shooter/saves/";
    //private const string SaveFile = "world.bin";
    private const uint   Magic = 0x564F584C; // "VOXL" magic number
    private const ushort Version = 1;
    
    public static void Save(Dictionary<Vector3I, VoxelChunk> chunks, string saveFile)
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        string path = ProjectSettings.GlobalizePath(SaveDir + saveFile);

        using var fs = File.Open(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // Header
        bw.Write(Magic);
        bw.Write(Version);
        bw.Write(chunks.Count);

        foreach (var (pos, chunk) in chunks)
        {
            WriteChunk(bw, pos, chunk);
        }
    }

    private static void WriteChunk(BinaryWriter bw, Vector3I pos, VoxelChunk chunk)
    {
        bw.Write(pos.X);
        bw.Write(pos.Y);
        bw.Write(pos.Z);
        
        bw.Write(chunk.Voxels.Length);
        bw.Write(chunk.Voxels);
        
        bw.Write(chunk.VoxelColors.Length);
        foreach (var c in chunk.VoxelColors)
        {
            bw.Write(PackColor(c));
        }
    }

    public static Dictionary<Vector3I, VoxelChunk> Load(string saveFile)
    {
        string path = ProjectSettings.GlobalizePath(SaveDir + saveFile);
        var result  = new Dictionary<Vector3I, VoxelChunk>();

        if (!File.Exists(path))
        {
            GD.PrintErr("Save file not found: ", path);
            return result;
        }

        using var fs = File.Open(path, FileMode.Open);
        using var br = new BinaryReader(fs);
        
        uint  magic = br.ReadUInt32();
        ushort version = br.ReadUInt16();

        if (magic != Magic)
            throw new InvalidDataException("Not a valid voxel save file.");
        if (version != Version)
            throw new InvalidDataException($"Unsupported save version: {version}");

        int chunkCount = br.ReadInt32();

        for (int i = 0; i < chunkCount; i++)
        {
            var (pos, chunk) = ReadChunk(br);
            result[pos] = chunk;
        }

        return result;
    }

    private static (Vector3I pos, VoxelChunk chunk) ReadChunk(BinaryReader br)
    {
        var pos = new Vector3I(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());

        int voxelLen = br.ReadInt32();
        byte[] voxels = br.ReadBytes(voxelLen);

        int colorLen = br.ReadInt32();
        var colors = new Color[colorLen];
        for (int i = 0; i < colorLen; i++)
            colors[i] = UnpackColor(br.ReadUInt32());

        var chunk = new VoxelChunk();
        chunk.Voxels = voxels;
        chunk.VoxelColors = colors;
        return (pos, chunk);
    }
    
    private static uint PackColor(Color c)
    {
        return ((uint)(c.R8) << 24) |
               ((uint)(c.G8) << 16) |
               ((uint)(c.B8) <<  8) |
               ((uint)(c.A8));
    }
    
    private static Color UnpackColor(uint packed)
    {
        byte r = (byte)((packed >> 24) & 0xFF);
        byte g = (byte)((packed >> 16) & 0xFF);
        byte b = (byte)((packed >>  8) & 0xFF);
        byte a = (byte)( packed        & 0xFF);
        return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
    
    public static void SaveToResource(Dictionary<Vector3I, VoxelChunk> chunks, string saveFileName)
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        // .tres = text format (human-readable, editor-friendly)
        // .res  = binary format (smaller size, faster I/O)
        string path = ProjectSettings.GlobalizePath(SaveDir + saveFileName + ".tres");

        var resource = new VoxelWorldResource();
        resource.Chunks = new Godot.Collections.Dictionary<string, ChunkDataResource>();

        foreach (var (pos, chunk) in chunks)
        {
            var chunkData = new ChunkDataResource
            {
                Position = pos,
                Voxels = chunk.Voxels,
                VoxelColors = chunk.VoxelColors
            };
            
            // Godot's resource serializer requires string keys for dictionaries
            string key = $"{pos.X},{pos.Y},{pos.Z}";
            resource.Chunks[key] = chunkData;
        }

        ResourceSaver.Save(resource, path);
        GD.Print($"[ChunkSaver] Saved resource to: {path}");
    }

    public static Dictionary<Vector3I, VoxelChunk> LoadFromResource(string saveFileName)
    {
        string path = ProjectSettings.GlobalizePath(SaveDir + saveFileName + ".tres");
        var result = new Dictionary<Vector3I, VoxelChunk>();

        if (!File.Exists(path))
        {
            GD.PrintErr("[ChunkSaver] Resource file not found: ", path);
            return result;
        }
        
        var loadedResource = GD.Load<VoxelWorldResource>(path);
        if (loadedResource == null || loadedResource.Chunks == null)
        {
            GD.PrintErr("[ChunkSaver] Failed to load resource or chunks are null.");
            return result;
        }

        foreach (var kvp in loadedResource.Chunks)
        {
            var data = kvp.Value;
            var pos = data.Position;
            
            var chunk = new VoxelChunk
            {
                Voxels = data.Voxels ?? Array.Empty<byte>(),
                VoxelColors = data.VoxelColors ?? Array.Empty<Color>()
            };
            result[pos] = chunk;
        }

        return result;
    }
}