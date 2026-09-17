using UnityEngine;

public class MapWaterBuilder
{
    private MapAssetLibrary library;
    private MapHeightField field;
    private MapMeshBatch batch;
    private float worldLimit = 3700f;

    public int boatCount;

    private string[] smallBoats = new string[]
    {
        "boat-row-small", "boat-row-large", "boat-speed-a", "boat-speed-c", "boat-speed-e",
        "boat-speed-g", "boat-speed-h", "boat-fishing-small", "boat-fan"
    };

    private string[] sailBoats = new string[]
    {
        "boat-sail-a", "boat-sail-b"
    };

    private string[] workBoats = new string[]
    {
        "boat-tug-a", "boat-tug-b", "boat-tug-c", "boat-tow-a", "boat-tow-b",
        "boat-house-a", "boat-house-b", "boat-house-c", "boat-house-d"
    };

    private string[] bigShips = new string[]
    {
        "ship-cargo-a", "ship-cargo-b", "ship-cargo-c", "ship-large",
        "ship-ocean-liner-small", "ship-small"
    };

    private string[] dockProps = new string[]
    {
        "cargo-container-a", "cargo-container-b", "cargo-container-c", "cargo-pile-a", "cargo-pile-b"
    };

    public MapWaterBuilder(MapAssetLibrary library, MapHeightField field, MapMeshBatch batch)
    {
        this.library = library;
        this.field = field;
        this.batch = batch;
    }

    // Hulls are modelled with the pivot at the keel, so a boat dropped at y = 0 floats entirely
    // above the water. Sinking by a fraction of the scaled height gives it a plausible draught.
    private void PlaceCraft(string folder, string modelName, float x, float z, float scale, float draughtFraction)
    {
        GameObject asset = library.Load(folder, modelName);
        if (asset == null)
        {
            return;
        }

        Bounds bounds = library.LocalBounds(asset);
        float y = -bounds.size.y * scale * draughtFraction;
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        library.AddParts(asset, batch, Matrix4x4.TRS(new Vector3(x, y, z), rotation, Vector3.one * scale));
        boatCount++;
    }

    private bool FindWater(float minDepth, float maxDepth, Vector2 around, float spread, out Vector2 spot)
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            float x = around.x + Random.Range(-spread, spread);
            float z = around.y + Random.Range(-spread, spread);
            if (Mathf.Abs(x) > worldLimit || Mathf.Abs(z) > worldLimit)
            {
                continue;
            }

            float depth = -field.SampleHeight(x, z);
            if (depth < minDepth || depth > maxDepth)
            {
                continue;
            }
            spot = new Vector2(x, z);
            return true;
        }
        spot = Vector2.zero;
        return false;
    }

    public void Build(Vector2 lakeCenter, Vector2 bayCenter, Vector2 harbourCenter)
    {
        ScatterAround(new Vector2(0f, 0f), 3600f, 3f, 70f, 54, 0.55f, 0.25f);
        ScatterAround(bayCenter, 900f, 3f, 45f, 26, 0.45f, 0.2f);
        ScatterAround(lakeCenter, 700f, 2.5f, 32f, 20, 0.15f, 0f);
        BuildHarbour(harbourCenter);
    }

    private void ScatterAround(Vector2 center, float spread, float minDepth, float maxDepth, int count, float sailChance, float workChance)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindWater(minDepth, maxDepth, center, spread, out spot))
            {
                continue;
            }

            float roll = Random.value;
            if (roll < workChance)
            {
                PlaceCraft(MapAssetLibrary.Watercraft, workBoats[Random.Range(0, workBoats.Length)], spot.x, spot.y, 2.2f, 0.22f);
                continue;
            }
            if (roll < workChance + sailChance)
            {
                PlaceCraft(MapAssetLibrary.Watercraft, sailBoats[Random.Range(0, sailBoats.Length)], spot.x, spot.y, 2.0f, 0.1f);
                continue;
            }
            PlaceCraft(MapAssetLibrary.Watercraft, smallBoats[Random.Range(0, smallBoats.Length)], spot.x, spot.y, 2.0f, 0.2f);
        }
    }

    private void BuildHarbour(Vector2 center)
    {
        for (int i = 0; i < 7; i++)
        {
            Vector2 spot;
            if (!FindWater(8f, 60f, center, 620f, out spot))
            {
                continue;
            }
            PlaceCraft(MapAssetLibrary.Watercraft, bigShips[Random.Range(0, bigShips.Length)], spot.x, spot.y, 4.4f, 0.28f);
        }

        for (int i = 0; i < 16; i++)
        {
            Vector2 spot;
            if (!FindWater(3f, 30f, center, 520f, out spot))
            {
                continue;
            }
            PlaceCraft(MapAssetLibrary.Watercraft, workBoats[Random.Range(0, workBoats.Length)], spot.x, spot.y, 2.3f, 0.22f);
        }

        for (int i = 0; i < 12; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(120f, 520f);
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.y + Mathf.Sin(angle) * radius;
            float ground = field.SampleHeight(x, z);
            if (ground < 1.5f || ground > 22f)
            {
                continue;
            }

            GameObject asset = library.Load(MapAssetLibrary.Watercraft, dockProps[Random.Range(0, dockProps.Length)]);
            if (asset == null)
            {
                continue;
            }
            Quaternion rotation = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
            library.AddParts(asset, batch, Matrix4x4.TRS(new Vector3(x, ground - 0.2f, z), rotation, Vector3.one * 2.6f));
            boatCount++;
        }

        for (int i = 0; i < 10; i++)
        {
            Vector2 spot;
            if (!FindWater(4f, 50f, center, 700f, out spot))
            {
                continue;
            }
            string buoyModel = "buoy";
            if (Random.value < 0.4f)
            {
                buoyModel = "buoy-flag";
            }
            PlaceCraft(MapAssetLibrary.Watercraft, buoyModel, spot.x, spot.y, 2.0f, 0.3f);
        }
    }
}
