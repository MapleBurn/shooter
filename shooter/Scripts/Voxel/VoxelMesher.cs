using System;
using System.Collections.Generic;
using Godot;

namespace Shooter.Scripts.Voxel;

public static class VoxelMesher
{
    // The 8 corners of a unit cube (centered at 0,0,0)
    private static readonly Vector3[] _cubeVertices = new Vector3[]
    {
        new(-0.5f, -0.5f,  0.5f), // 0: Front-Bottom-Left
        new( 0.5f, -0.5f,  0.5f), // 1: Front-Bottom-Right
        new( 0.5f, -0.5f, -0.5f), // 2: Back-Bottom-Right
        new(-0.5f, -0.5f, -0.5f), // 3: Back-Bottom-Left
        new(-0.5f,  0.5f,  0.5f), // 4: Front-Top-Left
        new( 0.5f,  0.5f,  0.5f), // 5: Front-Top-Right
        new( 0.5f,  0.5f, -0.5f), // 6: Back-Top-Right
        new(-0.5f,  0.5f, -0.5f)  // 7: Back-Top-Left
    };

    private enum Faces { Front, Back, Left, Right, Top, Bottom }

    // Your working triangle definitions (indices of _cubeVertices)
    private static readonly Dictionary<Faces, Vector3[]> _faceTriangles = new()
    {
        { Faces.Front,  new[] { new Vector3(0, 4, 5), new Vector3(0, 5, 1) } },
        { Faces.Back,   new[] { new Vector3(2, 6, 7), new Vector3(2, 7, 3) } },
        { Faces.Left,   new[] { new Vector3(3, 7, 4), new Vector3(3, 4, 0) } },
        { Faces.Right,  new[] { new Vector3(1, 5, 6), new Vector3(1, 6, 2) } },
        { Faces.Top,    new[] { new Vector3(4, 7, 6), new Vector3(4, 6, 5) } },
        { Faces.Bottom, new[] { new Vector3(3, 0, 1), new Vector3(3, 1, 2) } }
    };

    private static readonly Dictionary<Faces, Vector3> _faceNormals = new()
    {
        { Faces.Front,  new Vector3(0, 0, 1) },
        { Faces.Back,   new Vector3(0, 0, -1) },
        { Faces.Left,   new Vector3(-1, 0, 0) },
        { Faces.Right,  new Vector3(1, 0, 0) },
        { Faces.Top,    new Vector3(0, 1, 0) },
        { Faces.Bottom, new Vector3(0, -1, 0) }
    };

    public static ArrayMesh GenerateMesh(ChunkData data, VoxelRegistry registry)
    {
        List<Vector3> vertices = new();
        List<Color> colors = new();
        List<Vector3> normals = new();
        List<int> indices = new();

        for (int x = 0; x < ChunkData.Size; x++)
        {
            for (int y = 0; y < ChunkData.Size; y++)
            {
                for (int z = 0; z < ChunkData.Size; z++)
                {
                    byte voxelId = data.GetVoxel(x, y, z);
                    if (voxelId == 0) continue; // Skip air

                    var pos = new Vector3(x, y, z);

                    // Get the base material color or use the custom voxel color
                    Color voxelColor = data.GetVoxelColor(x, y, z);
                    Color finalColor = voxelColor != Colors.Transparent ? voxelColor : registry.GetMaterial(voxelId)?.Color ?? Colors.White;

                    // Check each face to see if it's exposed
                    foreach (Faces face in Enum.GetValues(typeof(Faces)))
                    {
                        if (IsFaceExposed(face, x, y, z, data))
                        {
                            AddFace(face, pos, finalColor, vertices, colors, normals, indices);
                        }
                    }
                }
            }
        }

        return BuildArrayMesh(vertices, colors, normals, indices);
    }

    private static bool IsFaceExposed(Faces face, int x, int y, int z, ChunkData data)
    {
        Vector3 normal = _faceNormals[face];
        // Calculate neighbor position using integer math to avoid float errors
        int nx = x + (int)normal.X;
        int ny = y + (int)normal.Y;
        int nz = z + (int)normal.Z;

        // If neighbor is outside chunk bounds, it's exposed (air)
        if (nx < 0 || nx >= ChunkData.Size || 
            ny < 0 || ny >= ChunkData.Size || 
            nz < 0 || nz >= ChunkData.Size)
        {
            return true;
        }

        // If neighbor is air, it's exposed
        return data.GetVoxel(nx, ny, nz) == 0;
    }

    private static void AddFace(Faces face, Vector3 position, Color color, 
        List<Vector3> vertices, List<Color> colors, List<Vector3> normals, List<int> indices)
    {
        var triangles = _faceTriangles[face];
        Vector3 normal = _faceNormals[face];

        foreach (var triangle in triangles)
        {
            // Each 'triangle' is a Vector3 containing 3 vertex indices from _cubeVertices
            for (int i = 0; i < 3; i++)
            {
                int vertexIndex = (int)triangle[i];
                
                // Add the actual world position: Cube Corner + Block Position
                vertices.Add(_cubeVertices[vertexIndex] + position);
                colors.Add(color);
                normals.Add(normal);
                
                // We add indices to the index list for the ArrayMesh
                indices.Add(vertices.Count - 1);
            }
        }
    }

    private static ArrayMesh BuildArrayMesh(List<Vector3> verts, List<Color> cols, List<Vector3> norms, List<int> idxs)
    {
        ArrayMesh arrMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = cols.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = norms.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = idxs.ToArray();

        arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return arrMesh;
    }
}