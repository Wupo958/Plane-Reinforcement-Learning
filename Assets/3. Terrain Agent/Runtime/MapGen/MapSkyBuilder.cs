using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MapSkyBuilder
{
    private MapHeightField field;
    private MapMeshBatch batch;

    public int cloudCount;

    public MapSkyBuilder(MapHeightField field, MapMeshBatch batch)
    {
        this.field = field;
        this.batch = batch;
    }

    // Clouds are blobs of low-poly spheres merged into one mesh. They carry no collider, so they
    // are invisible to the agent's ray sensors and to the clearance raycast.
    public void BuildClouds(Material cloudMaterial, int count, float worldLimit, List<Mesh> meshSink)
    {
        Mesh unitSphere = BuildSphere(10, 7);
        unitSphere.name = "CloudSphere";
        meshSink.Add(unitSphere);

        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-worldLimit, worldLimit);
            float z = Random.Range(-worldLimit, worldLimit);
            float y = Random.Range(340f, 900f);

            int blobs = Random.Range(7, 14);
            float spread = Random.Range(55f, 160f);
            for (int b = 0; b < blobs; b++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-spread, spread),
                    Random.Range(-spread * 0.10f, spread * 0.10f),
                    Random.Range(-spread * 0.7f, spread * 0.7f));

                float radius = Random.Range(spread * 0.38f, spread * 0.70f);
                Vector3 scale = new Vector3(radius * Random.Range(1.1f, 1.7f), radius * Random.Range(0.42f, 0.66f), radius * Random.Range(0.9f, 1.3f));
                Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(x, y, z) + offset, Quaternion.identity, scale);
                batch.Add(unitSphere, 0, cloudMaterial, matrix);
            }
            cloudCount++;
        }
    }

    private Mesh BuildSphere(int segments, int rings)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            float v = (float)r / rings;
            float polar = v * Mathf.PI;
            float sinPolar = Mathf.Sin(polar);
            float cosPolar = Mathf.Cos(polar);

            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float azimuth = u * Mathf.PI * 2f;
                Vector3 point = new Vector3(sinPolar * Mathf.Cos(azimuth), cosPolar, sinPolar * Mathf.Sin(azimuth));
                vertices.Add(point);
                normals.Add(point);
                uvs.Add(new Vector2(u, v));
            }
        }

        int stride = segments + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * stride + s;
                int b = (r + 1) * stride + s;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b + 1);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // A single 4-vertex quad cannot show vertex waves, so the water is a real grid out to the fog
    // distance with one flat skirt beyond it that is never visible unfogged.
    public Mesh BuildWaterMesh(float innerHalf, float cellSize, float outerHalf)
    {
        int cells = Mathf.RoundToInt(innerHalf * 2f / cellSize);
        int side = cells + 1;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int j = 0; j < side; j++)
        {
            for (int i = 0; i < side; i++)
            {
                float x = -innerHalf + i * cellSize;
                float z = -innerHalf + j * cellSize;
                vertices.Add(new Vector3(x, 0f, z));
                normals.Add(Vector3.up);
                uvs.Add(new Vector2((float)i / cells, (float)j / cells));
            }
        }

        for (int j = 0; j < cells; j++)
        {
            for (int i = 0; i < cells; i++)
            {
                int i00 = j * side + i;
                int i10 = j * side + i + 1;
                int i01 = (j + 1) * side + i;
                int i11 = (j + 1) * side + i + 1;

                triangles.Add(i00);
                triangles.Add(i01);
                triangles.Add(i11);
                triangles.Add(i00);
                triangles.Add(i11);
                triangles.Add(i10);
            }
        }

        AddSkirtQuad(vertices, normals, uvs, triangles, -outerHalf, -outerHalf, outerHalf, -innerHalf);
        AddSkirtQuad(vertices, normals, uvs, triangles, -outerHalf, innerHalf, outerHalf, outerHalf);
        AddSkirtQuad(vertices, normals, uvs, triangles, -outerHalf, -innerHalf, -innerHalf, innerHalf);
        AddSkirtQuad(vertices, normals, uvs, triangles, innerHalf, -innerHalf, outerHalf, innerHalf);

        Mesh mesh = new Mesh();
        mesh.name = "WaterSurface";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AddSkirtQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
                              float minX, float minZ, float maxX, float maxZ)
    {
        int baseIndex = vertices.Count;
        vertices.Add(new Vector3(minX, 0f, minZ));
        vertices.Add(new Vector3(minX, 0f, maxZ));
        vertices.Add(new Vector3(maxX, 0f, maxZ));
        vertices.Add(new Vector3(maxX, 0f, minZ));
        for (int i = 0; i < 4; i++)
        {
            normals.Add(Vector3.up);
            uvs.Add(Vector2.zero);
        }
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }
}
