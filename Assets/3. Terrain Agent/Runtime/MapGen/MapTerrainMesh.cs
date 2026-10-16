using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

public class MapTerrainMesh
{
    public int chunkCells = 16;

    public void Build(MapHeightField field, Transform parent, Material terrainMaterial, List<Mesh> meshSink)
    {
        int chunkCount = field.cells / chunkCells;
        for (int cz = 0; cz < chunkCount; cz++)
        {
            for (int cx = 0; cx < chunkCount; cx++)
            {
                BuildChunk(field, parent, terrainMaterial, meshSink, cx, cz);
            }
        }
    }

    private void BuildChunk(MapHeightField field, Transform parent, Material terrainMaterial, List<Mesh> meshSink, int chunkX, int chunkZ)
    {
        int startI = chunkX * chunkCells;
        int startJ = chunkZ * chunkCells;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int j = 0; j < chunkCells; j++)
        {
            for (int i = 0; i < chunkCells; i++)
            {
                int gi = startI + i;
                int gj = startJ + j;

                float x0 = field.VertexX(gi);
                float x1 = field.VertexX(gi + 1);
                float z0 = field.VertexX(gj);
                float z1 = field.VertexX(gj + 1);

                Vector3 v00 = new Vector3(x0, field.VertexHeight(gi, gj), z0);
                Vector3 v10 = new Vector3(x1, field.VertexHeight(gi + 1, gj), z0);
                Vector3 v01 = new Vector3(x0, field.VertexHeight(gi, gj + 1), z1);
                Vector3 v11 = new Vector3(x1, field.VertexHeight(gi + 1, gj + 1), z1);

                AddTriangle(field, vertices, normals, uvs, triangles, v00, v01, v11);
                AddTriangle(field, vertices, normals, uvs, triangles, v00, v11, v10);
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "TerrainChunk_" + chunkX + "_" + chunkZ;
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        meshSink.Add(mesh);

        Mesh colliderMesh = BuildColliderMesh(field, startI, startJ);
        colliderMesh.name = "TerrainCollider_" + chunkX + "_" + chunkZ;
        meshSink.Add(colliderMesh);

        GameObject chunk = new GameObject("TerrainChunk_" + chunkX + "_" + chunkZ);
        chunk.transform.SetParent(parent, false);
        chunk.tag = "Ground";
        MeshFilter filter = chunk.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = chunk.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMaterial;
        MeshCollider collider = chunk.AddComponent<MeshCollider>();
        collider.sharedMesh = colliderMesh;
#if UNITY_EDITOR
        GameObjectUtility.SetStaticEditorFlags(chunk, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
#endif
    }

    private void AddTriangle(MapHeightField field, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        Vector3 centroid = (a + b + c) / 3f;
        float slope = 1f - Mathf.Abs(normal.y);
        int biome = field.BiomeIndex(centroid.x, centroid.z, centroid.y, slope);
        Vector2 uv = MapPalette.Uv(biome);

        int baseIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        uvs.Add(uv);
        uvs.Add(uv);
        uvs.Add(uv);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
    }

    private Mesh BuildColliderMesh(MapHeightField field, int startI, int startJ)
    {
        int side = chunkCells + 1;
        Vector3[] vertices = new Vector3[side * side];
        for (int j = 0; j < side; j++)
        {
            for (int i = 0; i < side; i++)
            {
                int gi = startI + i;
                int gj = startJ + j;
                vertices[j * side + i] = new Vector3(field.VertexX(gi), field.VertexHeight(gi, gj), field.VertexX(gj));
            }
        }

        int[] triangles = new int[chunkCells * chunkCells * 6];
        int cursor = 0;
        for (int j = 0; j < chunkCells; j++)
        {
            for (int i = 0; i < chunkCells; i++)
            {
                int i00 = j * side + i;
                int i10 = j * side + i + 1;
                int i01 = (j + 1) * side + i;
                int i11 = (j + 1) * side + i + 1;

                triangles[cursor] = i00;
                triangles[cursor + 1] = i01;
                triangles[cursor + 2] = i11;
                triangles[cursor + 3] = i00;
                triangles[cursor + 4] = i11;
                triangles[cursor + 5] = i10;
                cursor += 6;
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
