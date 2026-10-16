using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

public class MapPalette
{
    public static int DeepBed = 0;
    public static int ShallowBed = 1;
    public static int Sand = 2;
    public static int SandDark = 3;
    public static int GrassLight = 4;
    public static int Grass = 5;
    public static int GrassMid = 6;
    public static int GrassDark = 7;
    public static int ForestFloor = 8;
    public static int Meadow = 9;
    public static int Dirt = 10;
    public static int DirtDark = 11;
    public static int RockLight = 12;
    public static int Rock = 13;
    public static int RockDark = 14;
    public static int Cliff = 15;
    public static int Snow = 16;
    public static int SnowDim = 17;
    public static int Concrete = 18;
    public static int ConcreteDark = 19;
    public static int Tarmac = 20;
    public static int MarkingWhite = 21;
    public static int Apron = 22;
    public static int RunwayGrass = 23;
    public static int Count = 24;

    public static Color[] Entries()
    {
        Color[] entries = new Color[Count];
        entries[DeepBed] = Hex("24414B");
        entries[ShallowBed] = Hex("3A6A62");
        entries[Sand] = Hex("E4D7A9");
        entries[SandDark] = Hex("D2C08A");
        entries[GrassLight] = Hex("9FC85E");
        entries[Grass] = Hex("89B84F");
        entries[GrassMid] = Hex("76A646");
        entries[GrassDark] = Hex("5F8F3C");
        entries[ForestFloor] = Hex("4D7B34");
        entries[Meadow] = Hex("ADD069");
        entries[Dirt] = Hex("9C7C50");
        entries[DirtDark] = Hex("866940");
        entries[RockLight] = Hex("918C82");
        entries[Rock] = Hex("7C776D");
        entries[RockDark] = Hex("636058");
        entries[Cliff] = Hex("56524B");
        entries[Snow] = Hex("F5F8F9");
        entries[SnowDim] = Hex("DEE7EB");
        entries[Concrete] = Hex("A2A7A6");
        entries[ConcreteDark] = Hex("8C918F");
        entries[Tarmac] = Hex("43474B");
        entries[MarkingWhite] = Hex("EDEFEF");
        entries[Apron] = Hex("6F7377");
        entries[RunwayGrass] = Hex("7FAE4B");
        return entries;
    }

    public static Color Hex(string hex)
    {
        Color parsed;
        ColorUtility.TryParseHtmlString("#" + hex, out parsed);
        return parsed;
    }

    public static Vector2 Uv(int index)
    {
        return new Vector2((index + 0.5f) / Count, 0.5f);
    }

    public static Texture2D CreateTexture()
    {
        Color[] entries = Entries();
        Texture2D texture = new Texture2D(Count, 1, TextureFormat.RGBA32, false, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(entries);
        texture.Apply(false, false);
        return texture;
    }
}

public class MapNoise
{
    public static float Value(float x, float z, float frequency, float offsetX, float offsetZ)
    {
        return Mathf.PerlinNoise(x * frequency + offsetX, z * frequency + offsetZ);
    }

    public static float Fbm(float x, float z, float frequency, int octaves, float offsetX, float offsetZ)
    {
        float sum = 0f;
        float amplitude = 1f;
        float total = 0f;
        float currentFrequency = frequency;
        for (int i = 0; i < octaves; i++)
        {
            sum += Value(x, z, currentFrequency, offsetX + i * 37.17f, offsetZ + i * 91.31f) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            currentFrequency *= 2.03f;
        }
        return sum / total;
    }

    public static float Ridge(float x, float z, float frequency, int octaves, float offsetX, float offsetZ)
    {
        float sum = 0f;
        float amplitude = 1f;
        float total = 0f;
        float currentFrequency = frequency;
        for (int i = 0; i < octaves; i++)
        {
            float raw = Value(x, z, currentFrequency, offsetX + i * 53.9f, offsetZ + i * 17.3f);
            float ridged = 1f - Mathf.Abs(raw * 2f - 1f);
            ridged *= ridged;
            sum += ridged * amplitude;
            total += amplitude;
            amplitude *= 0.45f;
            currentFrequency *= 2.11f;
        }
        return sum / total;
    }

    public static float SmoothStep(float edge0, float edge1, float value)
    {
        if (Mathf.Approximately(edge0, edge1))
        {
            if (value < edge0)
            {
                return 0f;
            }
            return 1f;
        }
        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    public static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSquared = ab.sqrMagnitude;
        if (lengthSquared < 0.0001f)
        {
            return Vector2.Distance(point, a);
        }
        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(point, closest);
    }

    public static float DistanceToPolyline(Vector2 point, Vector2[] points)
    {
        float best = float.MaxValue;
        for (int i = 0; i < points.Length - 1; i++)
        {
            float distance = DistanceToSegment(point, points[i], points[i + 1]);
            if (distance < best)
            {
                best = distance;
            }
        }
        return best;
    }
}

public class MapRenderPart
{
    public Mesh mesh;
    public int subMeshIndex;
    public Material material;
    public Matrix4x4 localMatrix;
}

public class MapAssetLibrary
{
    private Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
    private Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private List<Material> createdMaterials = new List<Material>();
    private Dictionary<GameObject, List<MapRenderPart>> partCache = new Dictionary<GameObject, List<MapRenderPart>>();
    private Dictionary<GameObject, Bounds> boundsCache = new Dictionary<GameObject, Bounds>();

    public static MapModelManifest Manifest;

    public static string Commercial = "Assets/1. Art/kenney_city-kit-commercial_2.1/Models/FBX format/";
    public static string Roads = "Assets/1. Art/kenney_city-kit-roads/Models/FBX format/";
    public static string Suburban = "Assets/1. Art/kenney_city-kit-suburban_20/Models/FBX format/";
    public static string Nature = "Assets/1. Art/kenney_nature-kit/Models/FBX format/";
    public static string Cars = "Assets/1. Art/kenney_car-kit/Models/FBX format/";
    public static string Castle = "Assets/1. Art/kenney_castle-kit/Models/FBX format/";
    public static string Industrial = "Assets/1. Art/kenney_city-kit-industrial_2.0/Models/FBX format/";
    public static string Watercraft = "Assets/1. Art/kenney_watercraft-pack/Models/FBX format/";
    public static string Characters = "Assets/1. Art/kenney_blocky-characters_20/Models/FBX format/";

    public GameObject Load(string folder, string modelName)
    {
        string path = folder + modelName + ".fbx";
        GameObject cached;
        if (cache.TryGetValue(path, out cached))
        {
            return cached;
        }

        GameObject asset = null;
        if (Manifest != null)
        {
            asset = Manifest.Find(path);
        }
#if UNITY_EDITOR
        if (asset == null)
        {
            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
#endif
        if (asset == null)
        {
            Debug.LogError("MapAssetLibrary could not load " + path);
        }
        cache[path] = asset;
        return asset;
    }

    public List<MapRenderPart> Parts(GameObject asset)
    {
        List<MapRenderPart> cached;
        if (partCache.TryGetValue(asset, out cached))
        {
            return cached;
        }

        cached = new List<MapRenderPart>();
        MeshRenderer[] renderers = asset.GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshFilter filter = renderers[i].GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 local = asset.transform.worldToLocalMatrix * renderers[i].transform.localToWorldMatrix;
            Material[] materials = renderers[i].sharedMaterials;
            int subMeshCount = filter.sharedMesh.subMeshCount;
            for (int m = 0; m < materials.Length && m < subMeshCount; m++)
            {
                MapRenderPart part = new MapRenderPart();
                part.mesh = filter.sharedMesh;
                part.subMeshIndex = m;
                part.material = Share(materials[m]);
                part.localMatrix = local;
                cached.Add(part);
            }
        }

        partCache[asset] = cached;
        return cached;
    }

    public Bounds LocalBounds(GameObject asset)
    {
        Bounds cachedBounds;
        if (boundsCache.TryGetValue(asset, out cachedBounds))
        {
            return cachedBounds;
        }

        MeshFilter[] filters = asset.GetComponentsInChildren<MeshFilter>();
        bool started = false;
        Bounds bounds = new Bounds();
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
            {
                continue;
            }
            Bounds local = filters[i].sharedMesh.bounds;
            Matrix4x4 matrix = asset.transform.worldToLocalMatrix * filters[i].transform.localToWorldMatrix;
            Bounds transformed = TransformBounds(matrix, local);
            if (!started)
            {
                bounds = transformed;
                started = true;
            }
            else
            {
                bounds.Encapsulate(transformed);
            }
        }
        boundsCache[asset] = bounds;
        return bounds;
    }

    public void AddParts(GameObject asset, MapMeshBatch batch, Matrix4x4 matrix)
    {
        List<MapRenderPart> parts = Parts(asset);
        for (int i = 0; i < parts.Count; i++)
        {
            batch.Add(parts[i].mesh, parts[i].subMeshIndex, parts[i].material, matrix * parts[i].localMatrix);
        }
    }

    public static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
    {
        Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
        Vector3 extents = bounds.extents;
        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
        Vector3 newExtents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, newExtents * 2f);
    }

    private static Dictionary<string, Color> paletteOverrides;

    public static Dictionary<string, Color> PaletteOverrides()
    {
        if (paletteOverrides != null)
        {
            return paletteOverrides;
        }

        paletteOverrides = new Dictionary<string, Color>();
        paletteOverrides["grass"] = MapPalette.Hex("7FAE4B");
        paletteOverrides["dirt"] = MapPalette.Hex("9C7C50");
        paletteOverrides["dirtDark"] = MapPalette.Hex("866940");
        paletteOverrides["stone"] = MapPalette.Hex("9A968C");
        paletteOverrides["stoneDark"] = MapPalette.Hex("6E6A62");
        paletteOverrides["wood"] = MapPalette.Hex("A67C4E");
        paletteOverrides["woodDark"] = MapPalette.Hex("7A5836");
        paletteOverrides["woodBark"] = MapPalette.Hex("6E5236");
        paletteOverrides["woodBarkDark"] = MapPalette.Hex("54402A");
        paletteOverrides["woodInner"] = MapPalette.Hex("C7A063");
        paletteOverrides["woodBirch"] = MapPalette.Hex("DCD6C8");
        paletteOverrides["leafsGreen"] = MapPalette.Hex("72A83A");
        paletteOverrides["leafsDark"] = MapPalette.Hex("3E7A40");
        paletteOverrides["leafsFall"] = MapPalette.Hex("D2822A");
        paletteOverrides["colorRed"] = MapPalette.Hex("C3453B");
        paletteOverrides["colorRedDark"] = MapPalette.Hex("8E322B");
        paletteOverrides["colorPurple"] = MapPalette.Hex("8B5FB0");
        paletteOverrides["colorTan"] = MapPalette.Hex("D7BE8A");
        paletteOverrides["colorWhite"] = MapPalette.Hex("EFEFE8");
        paletteOverrides["colorYellow"] = MapPalette.Hex("E0B64A");
        paletteOverrides["corn"] = MapPalette.Hex("E2C24E");
        paletteOverrides["water"] = MapPalette.Hex("3E7FA8");
        paletteOverrides["_defaultMat"] = MapPalette.Hex("B0AA9C");
        return paletteOverrides;
    }

    public Material Share(Material source)
    {
        if (source == null)
        {
            return null;
        }

        Color replacement = Color.clear;
        bool hasReplacement = PaletteOverrides().TryGetValue(source.name, out replacement);

        string key = source.shader.name;
        if (hasReplacement)
        {
            key += "|override|" + source.name;
        }
        Texture baseMap = null;
        if (source.HasProperty("_BaseMap"))
        {
            baseMap = source.GetTexture("_BaseMap");
        }
        if (baseMap == null && source.HasProperty("_MainTex"))
        {
            baseMap = source.GetTexture("_MainTex");
        }
        if (baseMap != null)
        {
            key += "|" + baseMap.GetInstanceID();
        }
        if (source.HasProperty("_BaseColor"))
        {
            key += "|" + source.GetColor("_BaseColor").ToString("F3");
        }
        if (source.HasProperty("_Smoothness"))
        {
            key += "|" + source.GetFloat("_Smoothness").ToString("F2");
        }

        Material shared;
        if (materialCache.TryGetValue(key, out shared))
        {
            return shared;
        }

        shared = new Material(source);
        shared.name = "Shared_" + source.name + "_" + materialCache.Count;
        if (hasReplacement)
        {
            shared.SetColor("_BaseColor", replacement);
            if (shared.HasProperty("_Color"))
            {
                shared.SetColor("_Color", replacement);
            }
            shared.SetFloat("_Smoothness", 0f);
        }
        materialCache[key] = shared;
        createdMaterials.Add(shared);
        return shared;
    }

    public List<Material> CreatedMaterials()
    {
        return createdMaterials;
    }
}

public class MapMeshBatch
{
    private Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
    private Dictionary<Material, int> vertexCounts = new Dictionary<Material, int>();
    private List<Mesh> finished = new List<Mesh>();
    private List<Material> finishedMaterials = new List<Material>();
    private int vertexLimit = 200000;

    public void Add(Mesh mesh, int subMeshIndex, Material material, Matrix4x4 matrix)
    {
        if (mesh == null || material == null)
        {
            return;
        }
        if (subMeshIndex >= mesh.subMeshCount)
        {
            return;
        }

        List<CombineInstance> list;
        if (!groups.TryGetValue(material, out list))
        {
            list = new List<CombineInstance>();
            groups[material] = list;
            vertexCounts[material] = 0;
        }

        if (vertexCounts[material] + mesh.vertexCount > vertexLimit)
        {
            Flush(material);
            list = groups[material];
        }

        CombineInstance instance = new CombineInstance();
        instance.mesh = mesh;
        instance.subMeshIndex = subMeshIndex;
        instance.transform = matrix;
        list.Add(instance);
        vertexCounts[material] = vertexCounts[material] + mesh.vertexCount;
    }

    public void AddRenderer(Renderer renderer, Matrix4x4 rootToLocal, MapAssetLibrary library)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            return;
        }

        Matrix4x4 matrix = rootToLocal * renderer.transform.localToWorldMatrix;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Add(filter.sharedMesh, i, library.Share(materials[i]), matrix);
        }
    }

    private void Flush(Material material)
    {
        List<CombineInstance> list;
        if (!groups.TryGetValue(material, out list))
        {
            return;
        }
        if (list.Count == 0)
        {
            return;
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.CombineMeshes(list.ToArray(), true, true, false);
        mesh.RecalculateBounds();
        finished.Add(mesh);
        finishedMaterials.Add(material);

        groups[material] = new List<CombineInstance>();
        vertexCounts[material] = 0;
    }

    public void Build(Transform parent, string prefix, List<Mesh> meshSink)
    {
        List<Material> materials = new List<Material>(groups.Keys);
        for (int i = 0; i < materials.Count; i++)
        {
            Flush(materials[i]);
        }

        for (int i = 0; i < finished.Count; i++)
        {
            Mesh mesh = finished[i];
            mesh.name = prefix + "_" + i;
            GameObject piece = new GameObject(prefix + "_" + i);
            piece.transform.SetParent(parent, false);
            MeshFilter filter = piece.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = finishedMaterials[i];
#if UNITY_EDITOR
            GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
#endif
            meshSink.Add(mesh);
        }

        finished.Clear();
        finishedMaterials.Clear();
        groups.Clear();
        vertexCounts.Clear();
    }
}
