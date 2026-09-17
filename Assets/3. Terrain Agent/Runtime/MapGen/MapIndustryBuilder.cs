using System.Collections.Generic;
using UnityEngine;

public class MapIndustryBuilder
{
    private MapAssetLibrary library;
    private MapHeightField field;
    private MapMeshBatch batch;
    private List<MapShape> exclusions;

    public int pieceCount;
    public List<Vector3> chimneyTops = new List<Vector3>();

    private string[] tanks = new string[]
    {
        "detail-tank-large", "detail-tank"
    };

    private string[] chimneys = new string[]
    {
        "chimney-basic", "chimney-large", "chimney-medium", "chimney-small"
    };

    private string[] containers = new string[]
    {
        "shipping-container-a", "shipping-container-b", "shipping-container-c"
    };

    public MapIndustryBuilder(MapAssetLibrary library, MapHeightField field, MapMeshBatch batch, List<MapShape> exclusions)
    {
        this.library = library;
        this.field = field;
        this.batch = batch;
        this.exclusions = exclusions;
    }

    private bool Excluded(float x, float z, float margin)
    {
        for (int i = 0; i < exclusions.Count; i++)
        {
            if (exclusions[i].Contains(x, z, margin))
            {
                return true;
            }
        }
        return false;
    }

    private void Place(string modelName, float x, float z, float scale, float yaw, float sink)
    {
        GameObject asset = library.Load(MapAssetLibrary.Industrial, modelName);
        if (asset == null)
        {
            return;
        }
        float y = field.SampleHeight(x, z) - sink;
        library.AddParts(asset, batch, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale));
        pieceCount++;
    }

    // Yard props sit inside the district, so the only thing they have to dodge is the district's
    // own street grid; a tank in the middle of a road reads as a mistake immediately.
    private bool FindYardSpot(MapCityPlan plan, Vector2 center, float radius, float clearance, out Vector2 spot)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector2 candidate = RandomIn(center, radius);
            if (MapCityBuilder.IsOnRoad(plan, candidate.x, candidate.y, clearance))
            {
                continue;
            }
            spot = candidate;
            return true;
        }
        spot = center;
        return false;
    }

    public void ScatterYard(MapCityPlan plan, Vector2 center, float radius, int tankCount, int chimneyCount, int containerCount, int towerCount)
    {
        for (int i = 0; i < tankCount; i++)
        {
            Vector2 spot;
            if (!FindYardSpot(plan, center, radius, 16f, out spot))
            {
                continue;
            }
            Place(tanks[Random.Range(0, tanks.Length)], spot.x, spot.y, Random.Range(16f, 24f), 90f * Random.Range(0, 4), 0.3f);
        }

        for (int i = 0; i < chimneyCount; i++)
        {
            Vector2 spot;
            if (!FindYardSpot(plan, center, radius, 10f, out spot))
            {
                continue;
            }
            string chimneyModel = chimneys[Random.Range(0, chimneys.Length)];
            float chimneyScale = Random.Range(18f, 27f);
            Place(chimneyModel, spot.x, spot.y, chimneyScale, 90f * Random.Range(0, 4), 0.3f);

            GameObject chimneyAsset = library.Load(MapAssetLibrary.Industrial, chimneyModel);
            if (chimneyAsset != null)
            {
                float top = field.SampleHeight(spot.x, spot.y) + library.LocalBounds(chimneyAsset).size.y * chimneyScale;
                chimneyTops.Add(new Vector3(spot.x, top, spot.y));
            }
        }

        for (int i = 0; i < containerCount; i++)
        {
            Vector2 spot;
            if (!FindYardSpot(plan, center, radius, 9f, out spot))
            {
                continue;
            }
            int stack = Random.Range(1, 4);
            float scale = 15f;
            GameObject asset = library.Load(MapAssetLibrary.Industrial, containers[Random.Range(0, containers.Length)]);
            if (asset == null)
            {
                continue;
            }

            Bounds bounds = library.LocalBounds(asset);
            float step = bounds.size.y * scale;
            float ground = field.SampleHeight(spot.x, spot.y) - 0.2f;
            float yaw = 90f * Random.Range(0, 4);
            for (int s = 0; s < stack; s++)
            {
                library.AddParts(asset, batch,
                    Matrix4x4.TRS(new Vector3(spot.x, ground + s * step, spot.y), Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale));
                pieceCount++;
            }
        }

        for (int i = 0; i < towerCount; i++)
        {
            Vector2 spot;
            if (!FindYardSpot(plan, center, radius, 12f, out spot))
            {
                continue;
            }
            Place("water-tower", spot.x, spot.y, Random.Range(15f, 20f), 90f * Random.Range(0, 4), 0.3f);
        }
    }

    public void BuildSolarFarm(Vector2 center, int columns, int rows, float spacing, float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector3 local = new Vector3((c - columns * 0.5f) * spacing, 0f, (r - rows * 0.5f) * spacing * 0.75f);
                Vector3 offset = rotation * local;
                float x = center.x + offset.x;
                float z = center.y + offset.z;
                if (field.SampleHeight(x, z) < 6f)
                {
                    continue;
                }
                if (field.SlopeAt(x, z) > 0.22f)
                {
                    continue;
                }
                if (Excluded(x, z, 34f))
                {
                    continue;
                }
                Place("solar-panel-landscape-group", x, z, 26f, yaw, 0.2f);
            }
        }
    }

    // Turbines are stepped along the ridge line rather than scattered, which is both how wind farms
    // are actually laid out and the only way they read as a group from the air.
    public void BuildWindFarm(Vector2 start, Vector2 end, int count, float scatter)
    {
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / Mathf.Max(1, count - 1);
            Vector2 point = Vector2.Lerp(start, end, t);
            float x = point.x + Random.Range(-scatter, scatter);
            float z = point.y + Random.Range(-scatter, scatter);
            if (field.SampleHeight(x, z) < 12f)
            {
                continue;
            }
            if (Excluded(x, z, 40f))
            {
                continue;
            }

            string model = "windmill";
            if (Random.value < 0.35f)
            {
                model = "windmill-low";
            }
            Place(model, x, z, Random.Range(24f, 32f), Random.Range(-35f, 35f), 0.4f);
        }
    }

    private Vector2 RandomIn(Vector2 center, float radius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = radius * Mathf.Sqrt(Random.value);
        return new Vector2(center.x + Mathf.Cos(angle) * distance, center.y + Mathf.Sin(angle) * distance);
    }
}
