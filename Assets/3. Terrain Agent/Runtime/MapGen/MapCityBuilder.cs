using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class MapCityPlan
{
    public string name;
    public Vector2 center;
    public float halfX;
    public float halfZ;
    public float tileSize = 18f;
    public int roadPeriod = 6;
    public string buildingFolder;
    public string[] coreModels;
    public string[] midModels;
    public string[] outerModels;
    public int coreGrid = 2;
    public int midGrid = 3;
    public int outerGrid = 3;
    public float coreRadius = 0.36f;
    public float midRadius = 0.72f;
    public float coreFootprint = 0.82f;
    public float midFootprint = 0.78f;
    public float outerFootprint = 0.74f;
    public float fillChance = 0.88f;
    public float parkChance = 0.09f;
    public float carChance = 0.2f;
    public float streetTreeChance = 0.12f;
    public string[] treeModels;
    public float treeHeight = 14f;
    public float outlineNoiseScale = 0.22f;
}

public class MapCityBuilder
{
    public static bool straightRunsAlongZ = false;

    private MapAssetLibrary library;
    private MapHeightField field;
    private Transform buildingsRoot;
    private MapMeshBatch roadBatch;
    private MapMeshBatch carBatch;
    private MapMeshBatch propBatch;

    private string[] carModels = new string[]
    {
        "sedan", "sedan-sports", "suv", "suv-luxury", "taxi", "van", "truck",
        "hatchback-sports", "police", "delivery", "garbage-truck", "truck-flat"
    };

    private string[] parkDetails = new string[]
    {
        "plant_bush", "plant_bushDetailed", "plant_bushLarge", "plant_bushSmall",
        "grass_large", "grass_leafsLarge", "flower_redA", "flower_yellowB", "flower_purpleC",
        "stump_roundDetailed", "rock_smallB", "mushroom_tanGroup"
    };

    public int buildingCount;
    public int roadTileCount;
    public int carCount;

    public MapCityBuilder(MapAssetLibrary library, MapHeightField field, Transform buildingsRoot,
                          MapMeshBatch roadBatch, MapMeshBatch carBatch, MapMeshBatch propBatch)
    {
        this.library = library;
        this.field = field;
        this.buildingsRoot = buildingsRoot;
        this.roadBatch = roadBatch;
        this.carBatch = carBatch;
        this.propBatch = propBatch;
    }

    // A spur that stops at an arbitrary point on a district edge reads as a road that gives up.
    // This returns the world coordinate of a real grid street so a spur can be aimed at one.
    public static float RoadLineCoordinate(MapCityPlan plan, bool alongX, float nearTo)
    {
        float half = plan.halfZ;
        float center = plan.center.y;
        if (alongX)
        {
            half = plan.halfX;
            center = plan.center.x;
        }

        int tiles = Mathf.FloorToInt(half * 2f / plan.tileSize);
        float origin = center - tiles * plan.tileSize * 0.5f;

        float best = center;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < tiles; i += plan.roadPeriod)
        {
            float coordinate = origin + (i + 0.5f) * plan.tileSize;
            float distance = Mathf.Abs(coordinate - nearTo);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = coordinate;
            }
        }
        return best;
    }

    public static bool IsOnRoad(MapCityPlan plan, float x, float z, float radius)
    {
        int tilesX = Mathf.FloorToInt(plan.halfX * 2f / plan.tileSize);
        int tilesZ = Mathf.FloorToInt(plan.halfZ * 2f / plan.tileSize);
        float originX = plan.center.x - tilesX * plan.tileSize * 0.5f;
        float originZ = plan.center.y - tilesZ * plan.tileSize * 0.5f;

        for (int corner = 0; corner < 5; corner++)
        {
            float sampleX = x;
            float sampleZ = z;
            if (corner < 4)
            {
                float signX = 1f;
                float signZ = 1f;
                if (corner == 1 || corner == 2)
                {
                    signX = -1f;
                }
                if (corner == 2 || corner == 3)
                {
                    signZ = -1f;
                }
                sampleX = x + signX * radius;
                sampleZ = z + signZ * radius;
            }

            int i = Mathf.FloorToInt((sampleX - originX) / plan.tileSize);
            int j = Mathf.FloorToInt((sampleZ - originZ) / plan.tileSize);
            if (i < 0 || j < 0 || i >= tilesX || j >= tilesZ)
            {
                continue;
            }
            if (i % plan.roadPeriod == 0 || j % plan.roadPeriod == 0)
            {
                return true;
            }
        }
        return false;
    }

    public void Build(MapCityPlan plan)
    {
        int tilesX = Mathf.FloorToInt(plan.halfX * 2f / plan.tileSize);
        int tilesZ = Mathf.FloorToInt(plan.halfZ * 2f / plan.tileSize);
        float originX = plan.center.x - tilesX * plan.tileSize * 0.5f;
        float originZ = plan.center.y - tilesZ * plan.tileSize * 0.5f;

        GameObject cityGroup = new GameObject(plan.name);
        cityGroup.transform.SetParent(buildingsRoot, false);

        for (int j = 0; j < tilesZ; j++)
        {
            for (int i = 0; i < tilesX; i++)
            {
                bool roadColumn = i % plan.roadPeriod == 0;
                bool roadRow = j % plan.roadPeriod == 0;
                if (!roadColumn && !roadRow)
                {
                    continue;
                }

                float x = originX + (i + 0.5f) * plan.tileSize;
                float z = originZ + (j + 0.5f) * plan.tileSize;
                if (Outline(plan, x, z) > 1.04f)
                {
                    continue;
                }

                PlaceRoadTile(plan, x, z, roadColumn, roadRow);
            }
        }

        int blocksX = tilesX / plan.roadPeriod;
        int blocksZ = tilesZ / plan.roadPeriod;
        float blockSize = (plan.roadPeriod - 1) * plan.tileSize;

        for (int bj = 0; bj < blocksZ; bj++)
        {
            for (int bi = 0; bi < blocksX; bi++)
            {
                float blockMinX = originX + (bi * plan.roadPeriod + 1) * plan.tileSize;
                float blockMinZ = originZ + (bj * plan.roadPeriod + 1) * plan.tileSize;
                float blockCenterX = blockMinX + blockSize * 0.5f;
                float blockCenterZ = blockMinZ + blockSize * 0.5f;

                float outline = Outline(plan, blockCenterX, blockCenterZ);
                if (outline > 1f)
                {
                    continue;
                }

                if (Random.value < plan.parkChance)
                {
                    FillPark(plan, blockMinX, blockMinZ, blockSize, cityGroup.transform);
                    continue;
                }

                FillBlock(plan, blockMinX, blockMinZ, blockSize, outline, cityGroup.transform);
            }
        }
    }

    private float Outline(MapCityPlan plan, float x, float z)
    {
        float dx = (x - plan.center.x) / plan.halfX;
        float dz = (z - plan.center.y) / plan.halfZ;
        float radius = Mathf.Sqrt(dx * dx + dz * dz);
        float noise = MapNoise.Fbm(x, z, 1f / 520f, 2, 17.4f, 91.2f) - 0.5f;
        return radius + noise * plan.outlineNoiseScale;
    }

    private void PlaceRoadTile(MapCityPlan plan, float x, float z, bool roadColumn, bool roadRow)
    {
        bool isCrossing = roadColumn && roadRow;
        string modelName = "road-straight";
        if (isCrossing)
        {
            modelName = "road-crossroad-line";
        }

        float roadYaw = 90f;
        if (roadColumn)
        {
            roadYaw = 0f;
        }

        float tileYaw = roadYaw;
        if (!straightRunsAlongZ && !isCrossing)
        {
            tileYaw += 90f;
        }

        GameObject asset = library.Load(MapAssetLibrary.Roads, modelName);
        Bounds bounds = library.LocalBounds(asset);
        float scale = plan.tileSize / Mathf.Max(bounds.size.x, bounds.size.z);
        float y = field.SampleHeight(x, z);

        Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(x, y + 0.12f, z), Quaternion.Euler(0f, tileYaw, 0f), Vector3.one * scale);
        AddCombined(asset, roadBatch, matrix);
        roadTileCount++;

        if (!isCrossing && Random.value < plan.carChance)
        {
            PlaceCar(plan, x, z, roadYaw, y);
        }
    }

    private void PlaceCar(MapCityPlan plan, float x, float z, float roadYaw, float y)
    {
        int count = 1;
        if (Random.value < 0.45f)
        {
            count = 2;
        }

        Quaternion laneRotation = Quaternion.Euler(0f, roadYaw, 0f);

        for (int i = 0; i < count; i++)
        {
            string modelName = carModels[Random.Range(0, carModels.Length)];
            GameObject asset = library.Load(MapAssetLibrary.Cars, modelName);
            Bounds bounds = library.LocalBounds(asset);
            float scale = 4.7f / bounds.size.z;

            float lateral = plan.tileSize * 0.22f;
            Quaternion facing = laneRotation;
            if (i == 1)
            {
                lateral = -lateral;
                facing = laneRotation * Quaternion.Euler(0f, 180f, 0f);
            }
            float along = Random.Range(-0.24f, 0.24f) * plan.tileSize;

            Vector3 offset = laneRotation * new Vector3(lateral, 0f, along);
            Vector3 position = new Vector3(x + offset.x, y + 0.2f, z + offset.z);
            Matrix4x4 matrix = Matrix4x4.TRS(position, facing, Vector3.one * scale);
            AddCombined(asset, carBatch, matrix);
            carCount++;
        }
    }

    private void FillPark(MapCityPlan plan, float minX, float minZ, float size, Transform group)
    {
        if (plan.treeModels == null)
        {
            return;
        }

        int count = Mathf.RoundToInt(size * size / 420f);
        for (int i = 0; i < count; i++)
        {
            float x = minX + Random.Range(0.08f, 0.92f) * size;
            float z = minZ + Random.Range(0.08f, 0.92f) * size;
            string modelName = plan.treeModels[Random.Range(0, plan.treeModels.Length)];
            GameObject asset = library.Load(MapAssetLibrary.Nature, modelName);
            Bounds bounds = library.LocalBounds(asset);
            float scale = plan.treeHeight * Random.Range(0.75f, 1.3f) / bounds.size.y;
            float y = field.SampleHeight(x, z) - 0.3f;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * scale);
            AddCombined(asset, propBatch, matrix);
        }

        int detailCount = Mathf.RoundToInt(size * size / 150f);
        for (int i = 0; i < detailCount; i++)
        {
            float x = minX + Random.Range(0.05f, 0.95f) * size;
            float z = minZ + Random.Range(0.05f, 0.95f) * size;
            string modelName = parkDetails[Random.Range(0, parkDetails.Length)];
            GameObject asset = library.Load(MapAssetLibrary.Nature, modelName);
            Bounds bounds = library.LocalBounds(asset);
            float scale = Random.Range(1.6f, 3.4f) / Mathf.Max(bounds.size.y, 0.05f);
            float y = field.SampleHeight(x, z) - 0.15f;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * scale);
            AddCombined(asset, propBatch, matrix);
        }

        int peopleCount = Random.Range(3, 8);
        for (int i = 0; i < peopleCount; i++)
        {
            string modelName = "character-" + (char)('a' + Random.Range(0, 18));
            GameObject asset = library.Load(MapAssetLibrary.Characters, modelName);
            if (asset == null)
            {
                continue;
            }

            Bounds bounds = library.LocalBounds(asset);
            float scale = 2.3f / bounds.size.y;
            float x = minX + Random.Range(0.1f, 0.9f) * size;
            float z = minZ + Random.Range(0.1f, 0.9f) * size;
            float y = field.SampleHeight(x, z) - 0.1f;
            Matrix4x4 matrix = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * scale);
            AddCombined(asset, propBatch, matrix);
        }
    }

    private void FillBlock(MapCityPlan plan, float minX, float minZ, float size, float outline, Transform group)
    {
        string[] models = plan.outerModels;
        int grid = plan.outerGrid;
        float footprintRatio = plan.outerFootprint;

        if (outline < plan.coreRadius)
        {
            models = plan.coreModels;
            grid = plan.coreGrid;
            footprintRatio = plan.coreFootprint;
        }
        else if (outline < plan.midRadius)
        {
            models = plan.midModels;
            grid = plan.midGrid;
            footprintRatio = plan.midFootprint;
        }

        float cell = size / grid;
        for (int j = 0; j < grid; j++)
        {
            for (int i = 0; i < grid; i++)
            {
                if (Random.value > plan.fillChance)
                {
                    continue;
                }

                float centerX = minX + (i + 0.5f) * cell;
                float centerZ = minZ + (j + 0.5f) * cell;
                float footprint = cell * footprintRatio * Random.Range(0.86f, 1f);

                string modelName = models[Random.Range(0, models.Length)];
                GameObject asset = library.Load(plan.buildingFolder, modelName);
                if (asset == null)
                {
                    continue;
                }

                Bounds bounds = library.LocalBounds(asset);
                float scale = footprint / Mathf.Max(bounds.size.x, bounds.size.z);

                float jitter = cell * 0.06f;
                float x = centerX + Random.Range(-jitter, jitter);
                float z = centerZ + Random.Range(-jitter, jitter);
                float y = field.SampleHeight(x, z) - 0.35f;

#if UNITY_EDITOR
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, group);
#else
                GameObject instance = UnityEngine.Object.Instantiate(asset, group);
#endif
                instance.transform.position = new Vector3(x, y, z);
                instance.transform.rotation = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
                instance.transform.localScale = Vector3.one * scale;
                instance.tag = "Building";

                BoxCollider collider = instance.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;

#if UNITY_EDITOR
                GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
#endif
                buildingCount++;
            }
        }
    }

    private void AddCombined(GameObject asset, MapMeshBatch batch, Matrix4x4 matrix)
    {
        library.AddParts(asset, batch, matrix);
    }
}
