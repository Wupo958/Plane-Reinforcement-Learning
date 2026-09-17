using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class MapCorridor
{
    public Vector2[] nodes;
    public float halfWidth = 26f;
    public float falloff = 150f;
    public float tileSize = 22f;
    public List<Vector2> samples = new List<Vector2>();
    public List<float> sampleHeights = new List<float>();
}

public class TerrainMapBuilder
{
    public static string GeneratedFolder = "Assets/3. Terrain Agent/Generated";
    public static string ScenePath = "Assets/3. Terrain Agent/TerrainWorldScene.unity";
    public static string MeshAssetPath = "Assets/3. Terrain Agent/Generated/MapMeshes.asset";
    public static string CheckpointLayoutPath = "Assets/3. Terrain Agent/CheckpointLayout.asset";
    public static int Seed = 20260916;
    public static int AgentCount = 25;

    private MapHeightField field;
    private MapAssetLibrary library;
    private List<Mesh> meshSink = new List<Mesh>();
    private List<MapShape> exclusions = new List<MapShape>();
    private List<MapCorridor> corridors = new List<MapCorridor>();
    private MapMeshBatch decorBatch;
    private List<GameObject> checkpointObjects = new List<GameObject>();

    private Material terrainMaterial;
    private Material waterMaterial;
    private Material gateMaterial;
    private Material cloudMaterial;
    private Material smokeMaterial;
    private List<Vector3> chimneyTops = new List<Vector3>();
    private List<CheckpointGate> gateComponents = new List<CheckpointGate>();
    private Texture2D paletteTexture;

    private Vector2 kestrelCenter = new Vector2(1900f, 800f);
    private Vector2 portVaneCenter = new Vector2(2150f, -1350f);
    private Vector2 larkfieldCenter = new Vector2(-1350f, -1800f);
    private Vector2 hollowPineCenter = new Vector2(-2150f, 1300f);
    private Vector2 airportCenter = new Vector2(250f, -2400f);
    private Vector2 ironworksCenter = new Vector2(1150f, -1000f);
    private Vector2 northgateCenter = new Vector2(600f, 1750f);
    private Vector2 castleCenter = new Vector2(-2750f, 1800f);
    private Vector2 lakeCenter = new Vector2(-1000f, 1050f);
    private Vector2 bayCenter = new Vector2(-560f, -2700f);
    private Vector2 solarFarmCenter = new Vector2(60f, -1150f);

    private float kestrelHeight = 44f;
    private float portVaneHeight = 20f;
    private float larkfieldHeight = 26f;
    private float hollowPineHeight = 196f;
    private float airportHeight = 22f;
    private float ironworksHeight = 34f;
    private float northgateHeight = 46f;
    private float castleHeight = 302f;

    private GameObject showcaseRoot;
    private GameObject smokeRoot;
    private GameObject trafficRoot;

    private float runwayHalfWidth = 30f;
    private float runwayHalfLength = 700f;

#if UNITY_EDITOR
    [MenuItem("Terrain Map/Build World Scene")]
    public static void BuildMenu()
    {
        TerrainMapBuilder builder = new TerrainMapBuilder();
        builder.Run();
    }

    public void Run()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject root = BuildWorld(null, Seed);
        BuildCheckpoints(root.transform);

        GameObject agent = BuildAgents();
        WireShowcase(showcaseRoot, smokeRoot, trafficRoot, agent);
        SaveGeneratedAssets();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log("World built with " + meshSink.Count + " generated meshes. Scene: " + ScenePath);
    }
#endif

    public GameObject BuildWorld(Transform parent, int seed)
    {
        Random.InitState(seed);
        library = new MapAssetLibrary();
        meshSink.Clear();
        exclusions.Clear();

        BuildHeightField();
        PrepareCorridors();
        PrepareAssets();

        GameObject root = new GameObject("World");
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
        }

        GameObject terrainRoot = NewChild(root.transform, "Terrain");
        MapTerrainMesh terrainMesh = new MapTerrainMesh();
        terrainMesh.Build(field, terrainRoot.transform, terrainMaterial, meshSink);

        BuildWater(root.transform);

        GameObject buildingsRoot = NewChild(root.transform, "Buildings");
        GameObject roadsRoot = NewChild(root.transform, "Roads");
        GameObject propsRoot = NewChild(root.transform, "Props");

        MapMeshBatch roadBatch = new MapMeshBatch();
        MapMeshBatch carBatch = new MapMeshBatch();
        MapMeshBatch propBatch = new MapMeshBatch();
        MapMeshBatch treeBatch = new MapMeshBatch();
        MapMeshBatch rockBatch = new MapMeshBatch();
        decorBatch = new MapMeshBatch();

        MapCityBuilder cityBuilder = new MapCityBuilder(library, field, buildingsRoot.transform, roadBatch, carBatch, propBatch);
        cityBuilder.Build(KestrelCityPlan());
        cityBuilder.Build(PortVanePlan());
        cityBuilder.Build(LarkfieldPlan());
        cityBuilder.Build(HollowPinePlan());
        cityBuilder.Build(IndustrialPlan("Ironworks", ironworksCenter, 400f, 360f));
        cityBuilder.Build(IndustrialPlan("NorthgateWorks", northgateCenter, 340f, 310f));

        PlaceCorridorRoads(roadBatch, carBatch);
        BuildAirport(root.transform, buildingsRoot.transform, carBatch);

        MapDecorBuilder decorBuilder = new MapDecorBuilder(library, field, exclusions, decorBatch);
        decorBuilder.Build();

        MapCastleBuilder castleBuilder = new MapCastleBuilder(library, field, decorBatch);
        castleBuilder.Build(castleCenter, 11, 10, castleHeight);

        MapIndustryBuilder industryBuilder = new MapIndustryBuilder(library, field, decorBatch, exclusions);
        industryBuilder.ScatterYard(IndustrialPlan("Ironworks", ironworksCenter, 400f, 360f), ironworksCenter, 330f, 9, 7, 26, 2);
        industryBuilder.ScatterYard(IndustrialPlan("NorthgateWorks", northgateCenter, 340f, 310f), northgateCenter, 270f, 7, 6, 20, 2);
        industryBuilder.BuildSolarFarm(solarFarmCenter, 9, 7, 46f, 20f);
        industryBuilder.BuildWindFarm(new Vector2(1250f, 2720f), new Vector2(2250f, 2180f), 13, 55f);
        industryBuilder.BuildWindFarm(new Vector2(-2950f, -520f), new Vector2(-2350f, -1350f), 9, 50f);

        MapWaterBuilder waterBuilder = new MapWaterBuilder(library, field, decorBatch);
        waterBuilder.Build(lakeCenter, bayCenter, new Vector2(2150f, -1950f));

        decorBuilder.PlaceCharacters(airportCenter.x + 175f, airportCenter.y - 260f, 90f, 14);

        exclusions.Add(MapShape.Disc(solarFarmCenter.x, solarFarmCenter.y, 290f, 60f));
        List<Vector3> decorClusters = decorBuilder.StructureClusters();
        for (int i = 0; i < decorClusters.Count; i++)
        {
            float radius = Mathf.Max(decorClusters[i].z - 55f, 8f);
            exclusions.Add(MapShape.Disc(decorClusters[i].x, decorClusters[i].y, radius, 10f));
        }

        MapNatureBuilder natureBuilder = new MapNatureBuilder(library, field, exclusions);
        natureBuilder.Scatter(treeBatch, rockBatch);

        roadBatch.Build(roadsRoot.transform, "Roads", meshSink);
        carBatch.Build(propsRoot.transform, "Cars", meshSink);
        propBatch.Build(propsRoot.transform, "CityProps", meshSink);
        treeBatch.Build(propsRoot.transform, "Trees", meshSink);
        rockBatch.Build(propsRoot.transform, "Rocks", meshSink);
        decorBatch.Build(propsRoot.transform, "Decor", meshSink);

        BuildLighting(root.transform);
        BuildPostProcessing(root.transform);
        BuildClouds(root.transform);

        showcaseRoot = NewChild(root.transform, "Showcase");
        smokeRoot = BuildSmoke(showcaseRoot.transform, industryBuilder.chimneyTops);
        trafficRoot = BuildTraffic(showcaseRoot.transform);

        return root;
    }

    private GameObject NewChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private void BuildHeightField()
    {
        field = new MapHeightField();

        AddCity(kestrelCenter, 800f, 720f, 850f, kestrelHeight);
        AddCity(portVaneCenter, 520f, 480f, 780f, portVaneHeight);
        AddCity(larkfieldCenter, 600f, 540f, 700f, larkfieldHeight);
        AddCity(hollowPineCenter, 330f, 290f, 620f, hollowPineHeight);
        AddCity(airportCenter, 430f, 800f, 620f, airportHeight);
        AddCity(ironworksCenter, 420f, 380f, 560f, ironworksHeight);
        AddCity(northgateCenter, 360f, 330f, 520f, northgateHeight);
        AddCity(castleCenter, 170f, 155f, 250f, castleHeight);

        AddDiscPaint(kestrelCenter, 560f, 300f, MapPalette.GrassMid, MapPalette.GrassDark, 0.55f, 0.75f);
        AddDiscPaint(portVaneCenter, 400f, 250f, MapPalette.Grass, MapPalette.GrassMid, 0.55f, 0.75f);
        AddDiscPaint(larkfieldCenter, 470f, 260f, MapPalette.GrassMid, MapPalette.Grass, 0.55f, 0.8f);
        AddDiscPaint(hollowPineCenter, 250f, 190f, MapPalette.GrassDark, MapPalette.Dirt, 0.55f, 0.7f);
        AddPaint(airportCenter, 380f, 740f, 200f, MapPalette.RunwayGrass, MapPalette.GrassLight, 0.6f, 0.35f);
        AddDiscPaint(ironworksCenter, 380f, 240f, MapPalette.Apron, MapPalette.ConcreteDark, 0.55f, 0.7f);
        AddDiscPaint(northgateCenter, 320f, 220f, MapPalette.ConcreteDark, MapPalette.Apron, 0.55f, 0.7f);
        AddDiscPaint(castleCenter, 165f, 120f, MapPalette.Dirt, MapPalette.RockLight, 0.5f, 0.45f);

        field.Generate();
    }

    private void AddPaint(Vector2 center, float halfX, float halfZ, float falloff, int primary, int secondary, float threshold, float noiseAmount)
    {
        MapPaint paint = new MapPaint();
        paint.shape = MapShape.Rect(center.x, center.y, halfX, halfZ, falloff);
        paint.paletteIndex = primary;
        paint.secondaryIndex = secondary;
        paint.threshold = threshold;
        paint.noiseAmount = noiseAmount;
        field.paints.Add(paint);
    }

    private void AddDiscPaint(Vector2 center, float radius, float falloff, int primary, int secondary, float threshold, float noiseAmount)
    {
        MapPaint paint = new MapPaint();
        paint.shape = MapShape.Disc(center.x, center.y, radius, falloff);
        paint.paletteIndex = primary;
        paint.secondaryIndex = secondary;
        paint.threshold = threshold;
        paint.noiseAmount = noiseAmount;
        paint.noiseScale = 190f;
        field.paints.Add(paint);
    }

    private void AddCity(Vector2 center, float halfX, float halfZ, float falloff, float height)
    {
        MapFlatten flatten = new MapFlatten();
        flatten.shape = MapShape.Rect(center.x, center.y, halfX, halfZ, falloff);
        flatten.height = height;
        field.flattens.Add(flatten);
        exclusions.Add(MapShape.Rect(center.x, center.y, halfX, halfZ, falloff));
    }

    private void PrepareAssets()
    {
#if UNITY_EDITOR
        if (AssetDatabase.IsValidFolder(GeneratedFolder))
        {
            AssetDatabase.DeleteAsset(GeneratedFolder);
        }
        AssetDatabase.CreateFolder("Assets/3. Terrain Agent", "Generated");
#endif

        paletteTexture = MapPalette.CreateTexture();
        paletteTexture.name = "MapPalette";

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        terrainMaterial = new Material(lit);
        terrainMaterial.name = "MapTerrain";
        terrainMaterial.SetTexture("_BaseMap", paletteTexture);
        terrainMaterial.SetFloat("_Smoothness", 0f);
        terrainMaterial.SetFloat("_Metallic", 0f);

        Shader waterShader = Shader.Find("Terrain/MapWater");
        if (waterShader == null)
        {
            Debug.LogError("Terrain/MapWater shader not found, falling back to Lit.");
            waterShader = lit;
        }
        waterMaterial = new Material(waterShader);
        waterMaterial.name = "MapWater";
        waterMaterial.renderQueue = 3000;

        // Lit alone leaves the underside of a cloud sitting in the dark ambient ground colour, which
        // reads as grey rock rather than cloud. Emission lifts the shadowed side back to white.
        cloudMaterial = new Material(lit);
        cloudMaterial.name = "MapCloud";
        cloudMaterial.SetColor("_BaseColor", Color.white);
        cloudMaterial.SetFloat("_Smoothness", 0f);
        cloudMaterial.SetFloat("_Metallic", 0f);
        cloudMaterial.EnableKeyword("_EMISSION");
        cloudMaterial.SetColor("_EmissionColor", new Color(0.62f, 0.65f, 0.7f, 1f));
        cloudMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        smokeMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        smokeMaterial.name = "MapSmoke";
        smokeMaterial.SetColor("_BaseColor", new Color(0.72f, 0.73f, 0.75f, 0.42f));
        smokeMaterial.SetFloat("_Surface", 1f);
        smokeMaterial.SetFloat("_Blend", 0f);
        smokeMaterial.SetFloat("_ZWrite", 0f);
        smokeMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        smokeMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        smokeMaterial.renderQueue = 3000;

        gateMaterial = new Material(lit);
        gateMaterial.name = "MapGate";
        gateMaterial.SetColor("_BaseColor", new Color(0.05f, 0.85f, 0.95f, 1f));
        gateMaterial.SetFloat("_Smoothness", 0.4f);
        gateMaterial.EnableKeyword("_EMISSION");
        gateMaterial.SetColor("_EmissionColor", new Color(0.1f, 1.6f, 1.9f, 1f));
        gateMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private void BuildWater(Transform parent)
    {
        MapSkyBuilder skyBuilder = new MapSkyBuilder(field, null);
        Mesh mesh = skyBuilder.BuildWaterMesh(11000f, 80f, 60000f);
        meshSink.Add(mesh);

        GameObject water = new GameObject("Water");
        water.transform.SetParent(parent, false);
        water.transform.position = new Vector3(0f, -0.1f, 0f);
        MeshFilter filter = water.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = water.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = waterMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private MapCityPlan KestrelCityPlan()
    {
        MapCityPlan plan = new MapCityPlan();
        plan.name = "KestrelCity";
        plan.center = kestrelCenter;
        plan.halfX = 790f;
        plan.halfZ = 710f;
        plan.tileSize = 20f;
        plan.roadPeriod = 6;
        plan.buildingFolder = MapAssetLibrary.Commercial;
        plan.coreModels = new string[]
        {
            "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
            "building-skyscraper-d", "building-skyscraper-e",
            "low-detail-building-a", "low-detail-building-b", "low-detail-building-c",
            "low-detail-building-f", "low-detail-building-m", "building-m"
        };
        plan.midModels = new string[]
        {
            "building-i", "building-j", "building-k", "building-l", "building-n", "building-m",
            "low-detail-building-e", "low-detail-building-g", "low-detail-building-h",
            "low-detail-building-i", "low-detail-building-l", "building-skyscraper-a"
        };
        plan.outerModels = new string[]
        {
            "building-a", "building-b", "building-c", "building-d", "building-e",
            "building-f", "building-g", "building-h",
            "low-detail-building-n", "low-detail-building-wide-a", "low-detail-building-wide-b"
        };
        plan.coreGrid = 2;
        plan.midGrid = 2;
        plan.outerGrid = 3;
        plan.coreRadius = 0.34f;
        plan.midRadius = 0.7f;
        plan.parkChance = 0.08f;
        plan.carChance = 0.24f;
        plan.streetTreeChance = 0.14f;
        plan.treeModels = new string[] { "tree_default", "tree_oak", "tree_tall", "tree_simple", "tree_detailed" };
        plan.treeHeight = 13f;
        return plan;
    }

    private MapCityPlan PortVanePlan()
    {
        MapCityPlan plan = new MapCityPlan();
        plan.name = "PortVane";
        plan.center = portVaneCenter;
        plan.halfX = 510f;
        plan.halfZ = 470f;
        plan.tileSize = 18f;
        plan.roadPeriod = 5;
        plan.buildingFolder = MapAssetLibrary.Commercial;
        plan.coreModels = new string[]
        {
            "building-l", "building-m", "building-n", "building-skyscraper-a",
            "low-detail-building-d", "low-detail-building-e", "low-detail-building-k"
        };
        plan.midModels = new string[]
        {
            "building-f", "building-g", "building-i", "building-j", "building-k",
            "low-detail-building-n", "low-detail-building-wide-a"
        };
        plan.outerModels = new string[]
        {
            "building-a", "building-b", "building-c", "building-d", "building-e", "building-h",
            "low-detail-building-wide-a", "low-detail-building-wide-b"
        };
        plan.coreGrid = 2;
        plan.midGrid = 2;
        plan.outerGrid = 2;
        plan.coreRadius = 0.4f;
        plan.midRadius = 0.75f;
        plan.parkChance = 0.1f;
        plan.carChance = 0.26f;
        plan.streetTreeChance = 0.16f;
        plan.treeModels = new string[] { "tree_palmTall", "tree_palmShort", "tree_default", "tree_oak" };
        plan.treeHeight = 12f;
        return plan;
    }

    private MapCityPlan LarkfieldPlan()
    {
        MapCityPlan plan = new MapCityPlan();
        plan.name = "Larkfield";
        plan.center = larkfieldCenter;
        plan.halfX = 590f;
        plan.halfZ = 530f;
        plan.tileSize = 15f;
        plan.roadPeriod = 5;
        plan.buildingFolder = MapAssetLibrary.Suburban;
        plan.coreModels = new string[]
        {
            "building-type-b", "building-type-d", "building-type-n", "building-type-t", "building-type-f"
        };
        plan.midModels = new string[]
        {
            "building-type-a", "building-type-c", "building-type-e", "building-type-j",
            "building-type-o", "building-type-s", "building-type-u"
        };
        plan.outerModels = new string[]
        {
            "building-type-g", "building-type-h", "building-type-i", "building-type-k",
            "building-type-l", "building-type-m", "building-type-p", "building-type-q", "building-type-r"
        };
        plan.coreGrid = 2;
        plan.midGrid = 2;
        plan.outerGrid = 2;
        plan.coreRadius = 0.45f;
        plan.midRadius = 0.8f;
        plan.coreFootprint = 0.8f;
        plan.midFootprint = 0.76f;
        plan.outerFootprint = 0.72f;
        plan.fillChance = 0.82f;
        plan.parkChance = 0.14f;
        plan.carChance = 0.18f;
        plan.streetTreeChance = 0.3f;
        plan.treeModels = new string[] { "tree_default", "tree_oak", "tree_fat", "tree_plateau", "tree_simple", "tree_blocks" };
        plan.treeHeight = 11f;
        return plan;
    }

    private MapCityPlan HollowPinePlan()
    {
        MapCityPlan plan = new MapCityPlan();
        plan.name = "HollowPine";
        plan.center = hollowPineCenter;
        plan.halfX = 320f;
        plan.halfZ = 280f;
        plan.tileSize = 15f;
        plan.roadPeriod = 5;
        plan.buildingFolder = MapAssetLibrary.Suburban;
        plan.coreModels = new string[] { "building-type-b", "building-type-d", "building-type-n", "building-type-t" };
        plan.midModels = new string[] { "building-type-c", "building-type-e", "building-type-j", "building-type-u" };
        plan.outerModels = new string[] { "building-type-g", "building-type-k", "building-type-l", "building-type-q", "building-type-r" };
        plan.coreGrid = 2;
        plan.midGrid = 2;
        plan.outerGrid = 2;
        plan.coreRadius = 0.45f;
        plan.midRadius = 0.82f;
        plan.fillChance = 0.72f;
        plan.parkChance = 0.18f;
        plan.carChance = 0.12f;
        plan.streetTreeChance = 0.38f;
        plan.treeModels = new string[] { "tree_pineTallA", "tree_pineTallD", "tree_pineRoundC", "tree_pineDefaultA" };
        plan.treeHeight = 16f;
        return plan;
    }

    private MapCityPlan IndustrialPlan(string name, Vector2 center, float halfX, float halfZ)
    {
        MapCityPlan plan = new MapCityPlan();
        plan.name = name;
        plan.center = center;
        plan.halfX = halfX;
        plan.halfZ = halfZ;
        plan.tileSize = 22f;
        plan.roadPeriod = 5;
        plan.buildingFolder = MapAssetLibrary.Industrial;
        plan.coreModels = new string[] { "building-a", "building-b", "building-l", "building-r", "building-q", "building-m" };
        plan.midModels = new string[] { "building-c", "building-e", "building-f", "building-g", "building-n", "building-t" };
        plan.outerModels = new string[] { "building-h", "building-i", "building-j", "building-k", "building-o", "building-p", "building-s" };
        plan.coreGrid = 1;
        plan.midGrid = 1;
        plan.outerGrid = 2;
        plan.coreRadius = 0.4f;
        plan.midRadius = 0.78f;
        plan.coreFootprint = 0.9f;
        plan.midFootprint = 0.86f;
        plan.outerFootprint = 0.8f;
        plan.fillChance = 0.8f;
        plan.parkChance = 0f;
        plan.carChance = 0.16f;
        plan.treeModels = new string[] { "tree_simple", "tree_thin", "plant_bushLarge" };
        plan.treeHeight = 9f;
        return plan;
    }

    private void PrepareCorridors()
    {
        MapCityPlan kestrelPlan = KestrelCityPlan();
        MapCityPlan portVanePlan = PortVanePlan();
        MapCityPlan larkfieldPlan = LarkfieldPlan();
        MapCityPlan ironworksPlan = IndustrialPlan("Ironworks", ironworksCenter, 400f, 360f);
        MapCityPlan northgatePlan = IndustrialPlan("NorthgateWorks", northgateCenter, 340f, 310f);

        MapCorridor airportToKestrel = new MapCorridor();
        airportToKestrel.nodes = new Vector2[]
        {
            new Vector2(450f, -1700f),
            new Vector2(620f, -1150f),
            new Vector2(700f, -560f),
            new Vector2(1150f, -180f),
            new Vector2(1600f, 60f),
            new Vector2(MapCityBuilder.RoadLineCoordinate(kestrelPlan, true, 1840f), 200f)
        };
        corridors.Add(airportToKestrel);

        MapCorridor kestrelToPortVane = new MapCorridor();
        kestrelToPortVane.nodes = new Vector2[]
        {
            new Vector2(MapCityBuilder.RoadLineCoordinate(kestrelPlan, true, 2200f), 165f),
            new Vector2(2300f, -250f),
            new Vector2(2240f, -700f),
            new Vector2(MapCityBuilder.RoadLineCoordinate(portVanePlan, true, 2200f), -1010f)
        };
        corridors.Add(kestrelToPortVane);

        MapCorridor airportToLarkfield = new MapCorridor();
        airportToLarkfield.nodes = new Vector2[]
        {
            new Vector2(-180f, -2350f),
            new Vector2(-620f, -2440f),
            new Vector2(-1060f, -2490f),
            new Vector2(MapCityBuilder.RoadLineCoordinate(larkfieldPlan, true, -1330f), -2250f)
        };
        corridors.Add(airportToLarkfield);

        MapCorridor ironworksSpur = new MapCorridor();
        ironworksSpur.halfWidth = 20f;
        ironworksSpur.falloff = 120f;
        ironworksSpur.tileSize = 20f;
        ironworksSpur.nodes = new Vector2[]
        {
            new Vector2(880f, -470f),
            new Vector2(1010f, -600f),
            new Vector2(MapCityBuilder.RoadLineCoordinate(ironworksPlan, true, 1100f), -780f)
        };
        corridors.Add(ironworksSpur);

        MapCorridor northgateSpur = new MapCorridor();
        northgateSpur.halfWidth = 20f;
        northgateSpur.falloff = 120f;
        northgateSpur.tileSize = 20f;
        northgateSpur.nodes = new Vector2[]
        {
            new Vector2(680f, 2300f),
            new Vector2(630f, 2160f),
            new Vector2(MapCityBuilder.RoadLineCoordinate(northgatePlan, true, 610f), 1960f)
        };
        corridors.Add(northgateSpur);

        MapCorridor lakeRoad = new MapCorridor();
        lakeRoad.nodes = new Vector2[]
        {
            new Vector2(1450f, 1500f),
            new Vector2(1050f, 2200f),
            new Vector2(300f, 2330f),
            new Vector2(-700f, 2050f),
            new Vector2(-1500f, 1800f),
            new Vector2(-1900f, 1430f)
        };
        corridors.Add(lakeRoad);

        MapCorridor castleRoad = new MapCorridor();
        castleRoad.halfWidth = 16f;
        castleRoad.falloff = 110f;
        castleRoad.tileSize = 16f;
        castleRoad.nodes = new Vector2[]
        {
            new Vector2(-2210f, 1430f),
            new Vector2(-2430f, 1540f),
            new Vector2(-2600f, 1660f),
            new Vector2(-2740f, 1770f)
        };
        corridors.Add(castleRoad);

        for (int i = 0; i < corridors.Count; i++)
        {
            SampleCorridor(corridors[i]);
            FlattenCorridor(corridors[i]);
            exclusions.Add(MapShape.Path(corridors[i].nodes, corridors[i].halfWidth + 16f, 40f));
        }
    }

    private void PlaceCorridorRoads(MapMeshBatch roadBatch, MapMeshBatch carBatch)
    {
        for (int i = 0; i < corridors.Count; i++)
        {
            PlaceCorridorRoad(corridors[i], roadBatch, carBatch);
        }
    }

    private void SampleCorridor(MapCorridor corridor)
    {
        float step = corridor.tileSize / 3f;
        corridor.samples.Clear();
        for (int i = 0; i < corridor.nodes.Length - 1; i++)
        {
            Vector2 a = corridor.nodes[i];
            Vector2 b = corridor.nodes[i + 1];
            float length = Vector2.Distance(a, b);
            int count = Mathf.Max(1, Mathf.RoundToInt(length / step));
            for (int s = 0; s < count; s++)
            {
                corridor.samples.Add(Vector2.Lerp(a, b, (float)s / count));
            }
        }
        corridor.samples.Add(corridor.nodes[corridor.nodes.Length - 1]);
        ResampleCorridorHeights(corridor);
    }

    private void ResampleCorridorHeights(MapCorridor corridor)
    {
        List<float> raw = new List<float>();
        for (int i = 0; i < corridor.samples.Count; i++)
        {
            raw.Add(field.SampleHeight(corridor.samples[i].x, corridor.samples[i].y));
        }

        corridor.sampleHeights.Clear();
        int window = 14;
        for (int i = 0; i < raw.Count; i++)
        {
            float sum = 0f;
            int used = 0;
            for (int k = -window; k <= window; k++)
            {
                int index = Mathf.Clamp(i + k, 0, raw.Count - 1);
                sum += raw[index];
                used++;
            }
            corridor.sampleHeights.Add(Mathf.Max(sum / used, 4f));
        }
    }

    private void FlattenCorridor(MapCorridor corridor)
    {
        float reach = corridor.halfWidth + corridor.falloff;
        Vector2 min = corridor.samples[0];
        Vector2 max = corridor.samples[0];
        for (int i = 1; i < corridor.samples.Count; i++)
        {
            min = Vector2.Min(min, corridor.samples[i]);
            max = Vector2.Max(max, corridor.samples[i]);
        }

        int side = field.cells + 1;
        int minI = Mathf.Max(0, Mathf.FloorToInt((min.x - reach - field.minCorner) / field.cellSize));
        int maxI = Mathf.Min(field.cells, Mathf.CeilToInt((max.x + reach - field.minCorner) / field.cellSize));
        int minJ = Mathf.Max(0, Mathf.FloorToInt((min.y - reach - field.minCorner) / field.cellSize));
        int maxJ = Mathf.Min(field.cells, Mathf.CeilToInt((max.y + reach - field.minCorner) / field.cellSize));

        for (int j = minJ; j <= maxJ; j++)
        {
            float z = field.VertexX(j);
            for (int i = minI; i <= maxI; i++)
            {
                float x = field.VertexX(i);
                Vector2 point = new Vector2(x, z);

                float bestDistance = float.MaxValue;
                int bestIndex = 0;
                for (int s = 0; s < corridor.samples.Count; s++)
                {
                    float distance = (corridor.samples[s] - point).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = s;
                    }
                }

                float actual = Mathf.Sqrt(bestDistance);
                float weight = 1f - MapNoise.SmoothStep(corridor.halfWidth, reach, actual);
                if (weight <= 0f)
                {
                    continue;
                }

                int index = j * side + i;
                field.heights[index] = Mathf.Lerp(field.heights[index], corridor.sampleHeights[bestIndex], weight);
            }
        }
    }

    private void PlaceCorridorRoad(MapCorridor corridor, MapMeshBatch roadBatch, MapMeshBatch carBatch)
    {
        GameObject asset = library.Load(MapAssetLibrary.Roads, "road-straight");
        Bounds bounds = library.LocalBounds(asset);
        float scale = corridor.tileSize * 1.06f / Mathf.Max(bounds.size.x, bounds.size.z);

        for (int i = 3; i < corridor.samples.Count; i += 3)
        {
            Vector2 point = corridor.samples[i - 1];
            Vector2 direction = corridor.samples[i] - corridor.samples[i - 3];
            float roadYaw = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            float tileYaw = roadYaw;
            if (!MapCityBuilder.straightRunsAlongZ)
            {
                tileYaw += 90f;
            }

            float y = field.SampleHeight(point.x, point.y);
            Vector3 normal = field.SampleNormal(point.x, point.y);
            Quaternion grade = Quaternion.FromToRotation(Vector3.up, normal);
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(point.x, y + 0.14f, point.y), grade * Quaternion.Euler(0f, tileYaw, 0f), Vector3.one * scale);
            library.AddParts(asset, roadBatch, matrix);

            if (Random.value < 0.07f)
            {
                PlaceHighwayCar(carBatch, point, grade * Quaternion.Euler(0f, roadYaw, 0f), y);
            }

        }
    }

    private void PlaceHighwayCar(MapMeshBatch carBatch, Vector2 point, Quaternion rotation, float y)
    {
        string[] models = new string[] { "sedan", "suv", "truck", "van", "delivery", "taxi", "garbage-truck" };
        GameObject asset = library.Load(MapAssetLibrary.Cars, models[Random.Range(0, models.Length)]);
        Bounds bounds = library.LocalBounds(asset);
        float scale = 4.7f / bounds.size.z;

        float lateral = 5.5f;
        Quaternion carRotation = rotation;
        if (Random.value < 0.5f)
        {
            lateral = -lateral;
            carRotation = rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        Vector3 offset = rotation * new Vector3(lateral, 0f, 0f);
        Vector3 position = new Vector3(point.x + offset.x, y + 0.25f, point.y + offset.z);
        library.AddParts(asset, carBatch, Matrix4x4.TRS(position, carRotation, Vector3.one * scale));
    }

    private void BuildAirport(Transform parent, Transform buildingsRoot, MapMeshBatch carBatch)
    {
        GameObject airport = NewChild(parent, "AerisField");

        float centerX = airportCenter.x;
        float centerZ = airportCenter.y;
        float top = field.SampleHeight(centerX, centerZ) + 0.3f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        AddSlab(vertices, normals, uvs, triangles, centerX, centerZ, runwayHalfWidth, runwayHalfLength, top, 2.2f, MapPalette.Tarmac);
        AddSlab(vertices, normals, uvs, triangles, centerX + 175f, centerZ - 260f, 105f, 190f, top, 2.1f, MapPalette.Apron);
        AddSlab(vertices, normals, uvs, triangles, centerX + 50f, centerZ - 260f, 55f, 22f, top, 2.1f, MapPalette.Apron);

        float markingY = top + 0.06f;
        for (int stripe = -3; stripe <= 3; stripe++)
        {
            float offset = stripe * 7f;
            AddQuad(vertices, normals, uvs, triangles, centerX + offset, markingY, centerZ + runwayHalfLength - 46f, 2.2f, 26f, MapPalette.MarkingWhite);
            AddQuad(vertices, normals, uvs, triangles, centerX + offset, markingY, centerZ - runwayHalfLength + 46f, 2.2f, 26f, MapPalette.MarkingWhite);
        }
        AddQuad(vertices, normals, uvs, triangles, centerX, markingY, centerZ + runwayHalfLength - 8f, runwayHalfWidth * 0.86f, 2f, MapPalette.MarkingWhite);
        AddQuad(vertices, normals, uvs, triangles, centerX, markingY, centerZ - runwayHalfLength + 8f, runwayHalfWidth * 0.86f, 2f, MapPalette.MarkingWhite);

        float dashSpacing = 60f;
        for (float z = -runwayHalfLength + 90f; z <= runwayHalfLength - 90f; z += dashSpacing)
        {
            AddQuad(vertices, normals, uvs, triangles, centerX, markingY, centerZ + z, 1.4f, 15f, MapPalette.MarkingWhite);
        }

        for (float z = -runwayHalfLength; z <= runwayHalfLength; z += 40f)
        {
            AddQuad(vertices, normals, uvs, triangles, centerX - runwayHalfWidth + 1.6f, markingY, centerZ + z, 1.2f, 16f, MapPalette.MarkingWhite);
            AddQuad(vertices, normals, uvs, triangles, centerX + runwayHalfWidth - 1.6f, markingY, centerZ + z, 1.2f, 16f, MapPalette.MarkingWhite);
        }

        Mesh mesh = new Mesh();
        mesh.name = "RunwayMesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        meshSink.Add(mesh);

        GameObject runway = new GameObject("Runway");
        runway.transform.SetParent(airport.transform, false);
        runway.tag = "Runway";
        MeshFilter filter = runway.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = runway.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = terrainMaterial;
        BoxCollider collider = runway.AddComponent<BoxCollider>();
        collider.center = new Vector3(centerX, top - 1.1f, centerZ);
        collider.size = new Vector3(runwayHalfWidth * 2f, 2.2f, runwayHalfLength * 2f);
        GameObjectUtility.SetStaticEditorFlags(runway, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

        BoxCollider apron = runway.AddComponent<BoxCollider>();
        apron.center = new Vector3(centerX + 175f, top - 1.05f, centerZ - 260f);
        apron.size = new Vector3(210f, 2.1f, 380f);

        string[] terminalModels = new string[] { "building-e", "building-c", "building-k", "low-detail-building-wide-a", "low-detail-building-wide-b" };
        for (int i = 0; i < 6; i++)
        {
            string modelName = terminalModels[Random.Range(0, terminalModels.Length)];
            GameObject asset = library.Load(MapAssetLibrary.Commercial, modelName);
            Bounds bounds = library.LocalBounds(asset);
            float footprint = Random.Range(34f, 52f);
            float scale = footprint / Mathf.Max(bounds.size.x, bounds.size.z);

            float x = centerX + 240f + Random.Range(-15f, 55f);
            float z = centerZ - 470f + i * 96f;
            float y = field.SampleHeight(x, z) - 0.3f;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, buildingsRoot);
            instance.name = "AirportBuilding_" + i;
            instance.transform.position = new Vector3(x, y, z);
            instance.transform.rotation = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
            instance.transform.localScale = Vector3.one * scale;
            instance.tag = "Building";
            BoxCollider box = instance.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        string[] apronCars = new string[] { "delivery", "truck-flat", "van", "firetruck", "ambulance", "suv" };
        for (int i = 0; i < 22; i++)
        {
            GameObject asset = library.Load(MapAssetLibrary.Cars, apronCars[Random.Range(0, apronCars.Length)]);
            Bounds bounds = library.LocalBounds(asset);
            float scale = 4.9f / bounds.size.z;
            float x = centerX + 85f + Random.Range(0f, 175f);
            float z = centerZ - 260f + Random.Range(-170f, 170f);
            float y = field.SampleHeight(x, z) + 0.3f;
            Quaternion rotation = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
            library.AddParts(asset, carBatch, Matrix4x4.TRS(new Vector3(x, y, z), rotation, Vector3.one * scale));
        }
    }

    private void AddSlab(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
                         float centerX, float centerZ, float halfX, float halfZ, float top, float thickness, int paletteIndex)
    {
        AddQuad(vertices, normals, uvs, triangles, centerX, top, centerZ, halfX, halfZ, paletteIndex);

        float bottom = top - thickness;
        AddSide(vertices, normals, uvs, triangles,
            new Vector3(centerX - halfX, top, centerZ - halfZ), new Vector3(centerX + halfX, top, centerZ - halfZ), bottom, paletteIndex);
        AddSide(vertices, normals, uvs, triangles,
            new Vector3(centerX + halfX, top, centerZ + halfZ), new Vector3(centerX - halfX, top, centerZ + halfZ), bottom, paletteIndex);
        AddSide(vertices, normals, uvs, triangles,
            new Vector3(centerX + halfX, top, centerZ - halfZ), new Vector3(centerX + halfX, top, centerZ + halfZ), bottom, paletteIndex);
        AddSide(vertices, normals, uvs, triangles,
            new Vector3(centerX - halfX, top, centerZ + halfZ), new Vector3(centerX - halfX, top, centerZ - halfZ), bottom, paletteIndex);
    }

    private void AddSide(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
                         Vector3 a, Vector3 b, float bottom, int paletteIndex)
    {
        Vector3 c = new Vector3(b.x, bottom, b.z);
        Vector3 d = new Vector3(a.x, bottom, a.z);
        Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
        Vector2 uv = MapPalette.Uv(paletteIndex);

        int baseIndex = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        for (int i = 0; i < 4; i++)
        {
            normals.Add(normal);
            uvs.Add(uv);
        }
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

    private void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
                         float centerX, float y, float centerZ, float halfX, float halfZ, int paletteIndex)
    {
        Vector2 uv = MapPalette.Uv(paletteIndex);
        int baseIndex = vertices.Count;
        vertices.Add(new Vector3(centerX - halfX, y, centerZ - halfZ));
        vertices.Add(new Vector3(centerX - halfX, y, centerZ + halfZ));
        vertices.Add(new Vector3(centerX + halfX, y, centerZ + halfZ));
        vertices.Add(new Vector3(centerX + halfX, y, centerZ - halfZ));
        for (int i = 0; i < 4; i++)
        {
            normals.Add(Vector3.up);
            uvs.Add(uv);
        }
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);
    }

#if UNITY_EDITOR
    private void BuildCheckpoints(Transform parent)
    {
        GameObject group = NewChild(parent, "Checkpoints");

        Vector3[] gates = new Vector3[]
        {
            new Vector3(250f, -1450f, 120f),
            new Vector3(1900f, 800f, 235f),
            new Vector3(1450f, 1780f, 100f),
            new Vector3(2560f, -400f, 145f),
            new Vector3(2150f, -1350f, 130f),
            new Vector3(700f, -2950f, 95f),
            new Vector3(-820f, -2550f, 110f),
            new Vector3(-1350f, -1800f, 115f),
            new Vector3(-2300f, -700f, 170f),
            new Vector3(-2900f, 600f, 190f),
            new Vector3(-2150f, 1300f, 175f),
            new Vector3(-2600f, 2400f, 160f),
            new Vector3(-1000f, 1050f, 115f),
            new Vector3(950f, 2600f, 170f),

            new Vector3(-700f, -30f, 48f),
            new Vector3(-820f, 500f, 52f),
            new Vector3(-2500f, 1900f, 110f),
            new Vector3(-1950f, 300f, 85f),
            new Vector3(-1320f, 760f, 45f),
            new Vector3(2150f, 1050f, 100f),
            new Vector3(1250f, 2780f, 120f),
            new Vector3(-3250f, -200f, 75f),
            new Vector3(-2750f, 1800f, 130f),
            new Vector3(2150f, -1950f, 80f),
            new Vector3(1150f, -1000f, 105f)
        };

        Mesh ring = BuildTorus(38f, 2.6f, 40, 8);
        ring.name = "GateRing";
        meshSink.Add(ring);

        // A baked layout wins over the table above, so gates dragged around in the editor and then
        // baked survive a rebuild instead of snapping back to the generator defaults.
        CheckpointLayout layout = AssetDatabase.LoadAssetAtPath<CheckpointLayout>(CheckpointLayoutPath);
        bool useBaked = layout != null && layout.Count > 0;
        int gateCount = gates.Length;
        if (useBaked)
        {
            gateCount = layout.Count;
            Debug.Log("Using baked checkpoint layout (" + gateCount + " gates) from " + CheckpointLayoutPath);
        }

        for (int i = 0; i < gateCount; i++)
        {
            Vector3 gatePosition;
            if (useBaked)
            {
                gatePosition = layout.Get(i);
            }
            else
            {
                float ground = field.SampleHeight(gates[i].x, gates[i].y);
                gatePosition = new Vector3(gates[i].x, Mathf.Max(ground + gates[i].z, 55f), gates[i].y);
            }

            GameObject checkpoint = new GameObject("Checkpoint " + (i + 1));
            checkpoint.transform.SetParent(group.transform, false);
            checkpoint.transform.position = gatePosition;
            checkpoint.tag = "Checkpoint";
            checkpoint.layer = LayerMask.NameToLayer("Checkpoint");

            SphereCollider trigger = checkpoint.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 38f;

            AddRing(checkpoint.transform, ring, Quaternion.identity);
            AddRing(checkpoint.transform, ring, Quaternion.Euler(90f, 0f, 0f));
            AddRing(checkpoint.transform, ring, Quaternion.Euler(0f, 0f, 90f));

            gateComponents.Add(checkpoint.AddComponent<CheckpointGate>());
            checkpointObjects.Add(checkpoint);
        }
    }

#endif

    private void AddRing(Transform parent, Mesh ring, Quaternion rotation)
    {
        GameObject piece = new GameObject("Ring");
        piece.transform.SetParent(parent, false);
        piece.transform.localRotation = rotation;
        piece.layer = parent.gameObject.layer;
        MeshFilter filter = piece.AddComponent<MeshFilter>();
        filter.sharedMesh = ring;
        MeshRenderer renderer = piece.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = gateMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private Mesh BuildTorus(float radius, float tube, int segments, int sides)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int s = 0; s <= segments; s++)
        {
            float u = (float)s / segments * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(u) * radius, 0f, Mathf.Sin(u) * radius);
            Vector3 outward = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));

            for (int t = 0; t <= sides; t++)
            {
                float v = (float)t / sides * Mathf.PI * 2f;
                Vector3 normal = outward * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                vertices.Add(center + normal * tube);
                normals.Add(normal);
                uvs.Add(new Vector2((float)s / segments, (float)t / sides));
            }
        }

        int stride = sides + 1;
        for (int s = 0; s < segments; s++)
        {
            for (int t = 0; t < sides; t++)
            {
                int a = s * stride + t;
                int b = (s + 1) * stride + t;
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

    private void BuildLighting(Transform parent)
    {
        GameObject lightObject = new GameObject("Sun");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(46f, -38f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.89f);
        light.intensity = 1.35f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.72f;

        Material skybox = new Material(Shader.Find("Skybox/Procedural"));
        skybox.name = "MapSky";
        skybox.SetColor("_SkyTint", new Color(0.46f, 0.62f, 0.85f));
        skybox.SetColor("_GroundColor", new Color(0.72f, 0.81f, 0.88f));
        skybox.SetFloat("_AtmosphereThickness", 0.88f);
        skybox.SetFloat("_Exposure", 1.15f);
        skybox.SetFloat("_SunSize", 0.035f);

        RenderSettings.skybox = skybox;
        RenderSettings.sun = light;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.66f, 0.79f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.5f, 0.5f);
        RenderSettings.ambientGroundColor = new Color(0.27f, 0.28f, 0.25f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.72f, 0.81f, 0.88f);
        RenderSettings.fogStartDistance = 2600f;
        RenderSettings.fogEndDistance = 11000f;

#if UNITY_EDITOR
        AssetDatabase.CreateAsset(skybox, GeneratedFolder + "/MapSky.mat");
#endif
    }

#if UNITY_EDITOR
    private void WireShowcase(GameObject showcaseRoot, GameObject smokeRoot, GameObject trafficRoot, GameObject agent)
    {
        ShowcaseVisuals showcase = showcaseRoot.AddComponent<ShowcaseVisuals>();
        SerializedObject serialized = new SerializedObject(showcase);
        serialized.FindProperty("trafficRoot").objectReferenceValue = trafficRoot;
        serialized.FindProperty("smokeRoot").objectReferenceValue = smokeRoot;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        CheckpointHighlighter highlighter = showcaseRoot.AddComponent<CheckpointHighlighter>();
        SerializedObject highlighterObject = new SerializedObject(highlighter);
        SerializedProperty gatesProperty = highlighterObject.FindProperty("gates");
        gatesProperty.arraySize = gateComponents.Count;
        for (int i = 0; i < gateComponents.Count; i++)
        {
            gatesProperty.GetArrayElementAtIndex(i).objectReferenceValue = gateComponents[i];
        }

        if (agent != null)
        {
            TerrainAgentScript script = agent.GetComponent<TerrainAgentScript>();
            highlighterObject.FindProperty("watchedAgent").objectReferenceValue = script;
            agent.AddComponent<WingtipTrails>();

            Camera agentCamera = agent.GetComponentInChildren<Camera>();
            if (agentCamera != null)
            {
                agentCamera.allowHDR = true;
                UniversalAdditionalCameraData cameraData = agentCamera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                CinematicCamera cinematic = agentCamera.gameObject.AddComponent<CinematicCamera>();
                SerializedObject cameraObject = new SerializedObject(cinematic);
                cameraObject.FindProperty("target").objectReferenceValue = agent.transform;
                cameraObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        highlighterObject.ApplyModifiedPropertiesWithoutUndo();
    }
#endif

    private void BuildPostProcessing(Transform parent)
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "MapPostFX";

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.active = true;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.05f;
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.85f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.62f;

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.active = true;
        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.Neutral;

        ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.active = true;
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.postExposure.value = 0.12f;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.contrast.value = 8f;
        colorAdjustments.saturation.overrideState = true;
        colorAdjustments.saturation.value = 6f;

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.18f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.55f;

#if UNITY_EDITOR
        AssetDatabase.CreateAsset(profile, GeneratedFolder + "/MapPostFX.asset");
#endif

        GameObject volumeObject = NewChild(parent, "PostProcessing");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;
    }

    private void BuildClouds(Transform parent)
    {
        MapMeshBatch cloudBatch = new MapMeshBatch();
        MapSkyBuilder skyBuilder = new MapSkyBuilder(field, cloudBatch);
        skyBuilder.BuildClouds(cloudMaterial, 70, 3900f, meshSink);

        GameObject cloudRoot = NewChild(parent, "Clouds");
        cloudBatch.Build(cloudRoot.transform, "Cloud", meshSink);

        MeshRenderer[] renderers = cloudRoot.GetComponentsInChildren<MeshRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private GameObject BuildSmoke(Transform parent, List<Vector3> tops)
    {
        GameObject smokeRoot = NewChild(parent, "Smoke");
        for (int i = 0; i < tops.Count; i++)
        {
            GameObject stack = new GameObject("ChimneySmoke_" + i);
            stack.transform.SetParent(smokeRoot.transform, false);
            stack.transform.position = tops[i];

            ParticleSystem system = stack.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = 9f;
            main.startSpeed = 7f;
            main.startSize = 18f;
            main.startColor = new Color(0.85f, 0.86f, 0.88f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 3.5f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 3f;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.45f, 1f, 1f));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.18f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(4f);

            ParticleSystemRenderer renderer = stack.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = smokeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return smokeRoot;
    }

    private GameObject BuildTraffic(Transform parent)
    {
        GameObject trafficRoot = NewChild(parent, "Traffic");
        string[] models = new string[] { "sedan", "suv", "taxi", "van", "truck", "delivery", "hatchback-sports", "police" };

        for (int c = 0; c < corridors.Count; c++)
        {
            MapCorridor corridor = corridors[c];
            if (corridor.samples.Count < 8)
            {
                continue;
            }

            Vector3[] points = new Vector3[corridor.samples.Count];
            for (int i = 0; i < corridor.samples.Count; i++)
            {
                Vector2 sample = corridor.samples[i];
                points[i] = new Vector3(sample.x, field.SampleHeight(sample.x, sample.y) + 0.2f, sample.y);
            }

            GameObject routeObject = NewChild(trafficRoot.transform, "Route_" + c);
            TrafficRoute route = routeObject.AddComponent<TrafficRoute>();
            route.SetPoints(points);

            float length = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
            }

            int carCount = Mathf.Clamp(Mathf.RoundToInt(length / 320f), 3, 16);
            for (int i = 0; i < carCount; i++)
            {
                GameObject asset = library.Load(MapAssetLibrary.Cars, models[Random.Range(0, models.Length)]);
                if (asset == null)
                {
                    continue;
                }

                GameObject car = (GameObject)PrefabUtility.InstantiatePrefab(asset, routeObject.transform);
                car.name = "TrafficCar_" + i;
                Bounds bounds = library.LocalBounds(asset);
                car.transform.localScale = Vector3.one * (4.7f / bounds.size.z);

                bool reversed = Random.value < 0.5f;
                float lateral = 5.5f;
                if (reversed)
                {
                    lateral = -lateral;
                }

                TrafficCar mover = car.AddComponent<TrafficCar>();
                mover.Configure(route, length * i / carCount, Random.Range(13f, 24f), lateral, reversed);
            }
        }
        return trafficRoot;
    }

    // Parallel agents are generated rather than duplicated by hand, because a rebuild wipes the
    // scene and any copies made in the editor go with it.
#if UNITY_EDITOR
    private GameObject BuildAgents()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3. Terrain Agent/TerrainAgent.prefab");
        if (prefab == null)
        {
            return null;
        }

        float runwayTop = field.SampleHeight(airportCenter.x, airportCenter.y);
        Vector3 spawn = new Vector3(airportCenter.x, runwayTop + 1.5f, airportCenter.y - runwayHalfLength + 90f);
        GameObject first = null;

        for (int index = 0; index < AgentCount; index++)
        {
            GameObject agent = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            agent.name = "TerrainAgent";
            if (index > 0)
            {
                agent.name = "TerrainAgent (" + index + ")";
            }
            agent.transform.position = spawn;
            agent.transform.rotation = Quaternion.identity;

            // Only the first aircraft is watched, and 25 cameras rendering the same world would
            // cost far more than the training itself.
            if (index > 0)
            {
                Camera extraCamera = agent.GetComponentInChildren<Camera>();
                if (extraCamera != null)
                {
                    Object.DestroyImmediate(extraCamera.gameObject);
                }
            }

            TerrainAgentScript script = agent.GetComponent<TerrainAgentScript>();
            if (script == null)
            {
                Debug.LogWarning("TerrainAgent prefab has no TerrainAgentScript.");
                continue;
            }

            SerializedObject serialized = new SerializedObject(script);
            SerializedProperty checkpointsProperty = serialized.FindProperty("checkpoints");
            checkpointsProperty.arraySize = checkpointObjects.Count;
            for (int i = 0; i < checkpointObjects.Count; i++)
            {
                checkpointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = checkpointObjects[i];
            }

            SerializedProperty spawnProperty = serialized.FindProperty("runwaySpawnPosition");
            if (spawnProperty != null)
            {
                spawnProperty.vector3Value = spawn;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (index == 0)
            {
                first = agent;
            }
        }
        return first;
    }
#endif

#if UNITY_EDITOR
    private void SaveGeneratedAssets()
    {
        string texturePath = GeneratedFolder + "/MapPalette.asset";
        AssetDatabase.CreateAsset(paletteTexture, texturePath);
        AssetDatabase.CreateAsset(terrainMaterial, GeneratedFolder + "/MapTerrain.mat");
        AssetDatabase.CreateAsset(waterMaterial, GeneratedFolder + "/MapWater.mat");
        AssetDatabase.CreateAsset(gateMaterial, GeneratedFolder + "/MapGate.mat");
        AssetDatabase.CreateAsset(cloudMaterial, GeneratedFolder + "/MapCloud.mat");
        AssetDatabase.CreateAsset(smokeMaterial, GeneratedFolder + "/MapSmoke.mat");

        List<Material> shared = library.CreatedMaterials();
        for (int i = 0; i < shared.Count; i++)
        {
            AssetDatabase.CreateAsset(shared[i], GeneratedFolder + "/" + shared[i].name + ".mat");
        }

        Mesh container = new Mesh();
        container.name = "MapMeshes";
        AssetDatabase.CreateAsset(container, MeshAssetPath);
        for (int i = 0; i < meshSink.Count; i++)
        {
            AssetDatabase.AddObjectToAsset(meshSink[i], MeshAssetPath);
        }
        AssetDatabase.SaveAssets();
    }
#endif
}
