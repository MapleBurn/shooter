using Godot;
using System.Collections.Generic;

namespace Shooter.Scripts.Voxel;

public static class VoxelMesher
{
    // Directions: Up, Down, Right, Left, Forward, Back
    private static readonly Vector3I[] Directions = {
        new(0, 1, 0), new(0, -1, 0), new(1, 0, 0), 
        new(-1, 0, 0), new(0, 0, 1), new(0, 0, -1)
    };

    // The 4 vertex offsets for a quad on each face direction
    private static readonly Vector3[] FaceOffsets = {
        new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0), // Up (Y+)
        new(0, 0, 1), new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), // Down (Y-)
        new(1, 0, 1), new(1, 1, 1), new(1, 1, 0), new(1, 0, 0), // Right (X+)
        new(0, 0, 0), new(0, 1, 0), new(0, 1, 1), new(0, 0, 1), // Left (X-)
        new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), // Forward (Z+)
        new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0)  // Back (Z-)
    };

    // The indices to form two triangles from the 4 vertices of a quad
    private static readonly int[] FaceIndices = { 0, 1, 2, 0, 2, 3 };

    public static ArrayMesh GenerateMesh(ChunkData chunk, VoxelRegistry registry)
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var indices = new List<int>();

        for (int z = 0; z < ChunkData.Size; z++)
        {
            for (int y = 0; y < ChunkData.Size; y++)
            {
                for (int x = 0; x < ChunkData.Size; x++)
                {
                    byte voxelId = chunk.GetVoxel(x, y, z);
                    if (voxelId == 0) continue; // Skip air

                    VoxelMaterial mat = registry.GetMaterial(voxelId);
                    if (mat == null) continue;

                    // Check all 6 neighbors for face culling
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3I neighborPos = new Vector3I(x, y, z) + Directions[i];
                        
                        // If neighbor is air or out of bounds, we draw this face
                        if (chunk.GetVoxel(neighborPos.X, neighborPos.Y, neighborPos.Z) == 0)
                        {
                            int vertexStartIndex = vertices.Count;

                            // Add the 4 vertices for this quad
                            for (int v = 0; v < 4; v++)
                            {
                                // Calculate position: current voxel pos + offset from face template
                                Vector3 vertPos = new Vector3(x, y, z) + FaceOffsets[i * 4 + v];
                                vertices.Add(vertPos);
                                colors.Add(mat.Color);
                                
                                // Normal is simply the direction of the face
                                normals.Add(Directions[i]);
                            }

                            // Add indices for the two triangles forming the quad
                            for (int j = 0; j < 6; j++)
                            {
                                indices.Add(vertexStartIndex + FaceIndices[j]);
                            }
                        }
                    }
                }
            }
        }

        return BuildArrayMesh(vertices, colors, normals, indices);
    }

    private static ArrayMesh BuildArrayMesh(List<Vector3> verts, List<Color> cols, List<Vector3> norms, List<int> idxs)
    {
        var arrMesh = new ArrayMesh();
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
