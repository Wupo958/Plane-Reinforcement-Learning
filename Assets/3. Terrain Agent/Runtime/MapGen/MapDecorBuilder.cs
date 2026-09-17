using System.Collections.Generic;
using UnityEngine;

public class MapDecorBuilder
{
    private MapAssetLibrary library;
    private MapHeightField field;
    private List<MapShape> exclusions;
    private MapMeshBatch batch;
    private float worldLimit = 3450f;
    private float sizeBoost = 1.4f;
    private List<Vector3> claimed = new List<Vector3>();
    // Only clusters with built structures keep trees out. A meadow or a mushroom patch should
    // still have forest growing through it.
    private List<Vector3> structures = new List<Vector3>();

    public int clusterCount;
    public int pieceCount;

    private string[] tents = new string[]
    {
        "tent_detailedClosed", "tent_detailedOpen", "tent_smallClosed", "tent_smallOpen"
    };

    private string[] campfires = new string[]
    {
        "campfire_bricks", "campfire_logs", "campfire_planks", "campfire_stones"
    };

    private string[] seats = new string[]
    {
        "log", "log_large", "stump_round", "stump_square", "stump_old"
    };

    private string[] logPiles = new string[]
    {
        "log_stack", "log_stackLarge", "log_large", "log"
    };

    private string[] stumps = new string[]
    {
        "stump_old", "stump_oldTall", "stump_round", "stump_roundDetailed",
        "stump_square", "stump_squareDetailed", "stump_squareDetailedWide"
    };

    private string[] cropRows = new string[]
    {
        "crops_wheatStageA", "crops_wheatStageB", "crops_cornStageB", "crops_cornStageC",
        "crops_cornStageD", "crops_leafsStageA", "crops_leafsStageB"
    };

    private string[] cropDirt = new string[]
    {
        "crops_dirtRow", "crops_dirtDoubleRow", "crops_dirtSingle"
    };

    private string[] rootCrops = new string[]
    {
        "crop_carrot", "crop_melon", "crop_pumpkin", "crop_turnip"
    };

    private string[] fences = new string[]
    {
        "fence_simple", "fence_simpleHigh", "fence_simpleLow", "fence_planks", "fence_planksDouble"
    };

    private string[] statues = new string[]
    {
        "statue_column", "statue_columnDamaged", "statue_block", "statue_head", "statue_ring"
    };

    private string[] mushrooms = new string[]
    {
        "mushroom_red", "mushroom_redGroup", "mushroom_redTall",
        "mushroom_tan", "mushroom_tanGroup", "mushroom_tanTall"
    };

    private string[] flowers = new string[]
    {
        "flower_purpleA", "flower_purpleB", "flower_purpleC",
        "flower_redA", "flower_redB", "flower_redC",
        "flower_yellowA", "flower_yellowB", "flower_yellowC"
    };

    private string[] tufts = new string[]
    {
        "grass", "grass_large", "grass_leafs", "grass_leafsLarge"
    };

    private string[] bushes = new string[]
    {
        "plant_bush", "plant_bushDetailed", "plant_bushLarge",
        "plant_bushLargeTriangle", "plant_bushSmall", "plant_bushTriangle"
    };

    private string[] boulders = new string[]
    {
        "rock_largeA", "rock_largeB", "rock_largeC", "rock_largeD", "rock_largeE", "rock_largeF",
        "stone_largeA", "stone_largeB", "stone_largeC", "stone_largeE"
    };

    private string[] smallRocks = new string[]
    {
        "rock_smallA", "rock_smallC", "rock_smallE", "rock_smallH",
        "stone_smallB", "stone_smallD", "stone_smallF"
    };

    private string[] pastureTrees = new string[]
    {
        "tree_oak", "tree_fat", "tree_plateau", "tree_default", "tree_detailed"
    };

    private string[] palms = new string[]
    {
        "tree_palmTall", "tree_palmShort", "tree_palmBend", "tree_palmDetailedTall"
    };

    public MapDecorBuilder(MapAssetLibrary library, MapHeightField field, List<MapShape> exclusions, MapMeshBatch batch)
    {
        this.library = library;
        this.field = field;
        this.exclusions = exclusions;
        this.batch = batch;
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

    // Clusters used to be sampled independently, so a farm and a pasture could land on the same
    // patch and draw two fences through each other. Every accepted spot now reserves a radius.
    private bool Crowded(float x, float z, float radius)
    {
        for (int i = 0; i < claimed.Count; i++)
        {
            Vector3 other = claimed[i];
            float minimum = other.z + radius;
            float dx = other.x - x;
            float dz = other.y - z;
            if (dx * dx + dz * dz < minimum * minimum)
            {
                return true;
            }
        }
        return false;
    }

    public List<Vector3> StructureClusters()
    {
        return structures;
    }

    private void ClaimStructure(Vector2 spot, float radius)
    {
        structures.Add(new Vector3(spot.x, spot.y, radius));
    }

    private void Claim(Vector2 spot, float radius)
    {
        claimed.Add(new Vector3(spot.x, spot.y, radius));
    }

    private bool FindSpot(float minHeight, float maxHeight, float maxSlope, float margin, float radius, out Vector2 spot)
    {
        for (int attempt = 0; attempt < 700; attempt++)
        {
            float x = Random.Range(-worldLimit, worldLimit);
            float z = Random.Range(-worldLimit, worldLimit);
            float height = field.SampleHeight(x, z);
            if (height < minHeight || height > maxHeight)
            {
                continue;
            }
            if (field.SlopeAt(x, z) > maxSlope)
            {
                continue;
            }
            if (Excluded(x, z, margin))
            {
                continue;
            }
            if (Crowded(x, z, radius))
            {
                continue;
            }
            spot = new Vector2(x, z);
            Claim(spot, radius);
            return true;
        }
        spot = Vector2.zero;
        return false;
    }

    private bool FindForestSpot(float minDensity, float minHeight, float maxHeight, float radius, out Vector2 spot)
    {
        for (int attempt = 0; attempt < 800; attempt++)
        {
            float x = Random.Range(-worldLimit, worldLimit);
            float z = Random.Range(-worldLimit, worldLimit);
            float height = field.SampleHeight(x, z);
            if (height < minHeight || height > maxHeight)
            {
                continue;
            }
            if (MapHeightField.ForestDensity(x, z) < minDensity)
            {
                continue;
            }
            if (field.SlopeAt(x, z) > 0.32f)
            {
                continue;
            }
            if (Excluded(x, z, 70f))
            {
                continue;
            }
            if (Crowded(x, z, radius))
            {
                continue;
            }
            spot = new Vector2(x, z);
            Claim(spot, radius);
            return true;
        }
        spot = Vector2.zero;
        return false;
    }

    private void PlaceByHeight(string modelName, float x, float z, float targetHeight, float sink)
    {
        PlaceCentered(modelName, x, z, targetHeight, Random.Range(0f, 360f), sink, true);
    }

    private void PlaceByFootprint(string modelName, float x, float z, float targetFootprint, float yaw, float sink)
    {
        PlaceCentered(modelName, x, z, targetFootprint, yaw, sink, false);
    }

    // fence_simple and friends carry their geometry well off the pivot, so the placement point has
    // to be corrected by the scaled bounds centre or a run of them comes out staggered.
    private void PlaceCentered(string modelName, float x, float z, float target, float yaw, float sink, bool byHeight)
    {
        GameObject asset = library.Load(MapAssetLibrary.Nature, modelName);
        if (asset == null)
        {
            return;
        }

        Bounds bounds = library.LocalBounds(asset);
        float reference = Mathf.Max(bounds.size.x, bounds.size.z);
        if (byHeight)
        {
            reference = bounds.size.y;
        }
        if (reference < 0.005f)
        {
            return;
        }

        float scale = target * sizeBoost / reference;
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 pivotShift = rotation * new Vector3(bounds.center.x * scale, 0f, bounds.center.z * scale);

        float placeX = x - pivotShift.x;
        float placeZ = z - pivotShift.z;
        float y = field.SampleHeight(x, z) - sink * sizeBoost;

        library.AddParts(asset, batch, Matrix4x4.TRS(new Vector3(placeX, y, placeZ), rotation, Vector3.one * scale));
        pieceCount++;
    }

    private float ScaledSpanX(string modelName, float target, bool byHeight)
    {
        GameObject asset = library.Load(MapAssetLibrary.Nature, modelName);
        if (asset == null)
        {
            return target;
        }

        Bounds bounds = library.LocalBounds(asset);
        float reference = Mathf.Max(bounds.size.x, bounds.size.z);
        if (byHeight)
        {
            reference = bounds.size.y;
        }
        if (reference < 0.005f)
        {
            return target;
        }
        return bounds.size.x * (target * sizeBoost / reference);
    }

    private string Pick(string[] set)
    {
        return set[Random.Range(0, set.Length)];
    }

    public void PlaceCharacters(float x, float z, float spread, int count)
    {
        for (int i = 0; i < count; i++)
        {
            string modelName = "character-" + (char)('a' + Random.Range(0, 18));
            GameObject asset = library.Load(MapAssetLibrary.Characters, modelName);
            if (asset == null)
            {
                continue;
            }

            Bounds bounds = library.LocalBounds(asset);
            float scale = 2.3f / bounds.size.y;
            float placeX = x + Random.Range(-spread, spread);
            float placeZ = z + Random.Range(-spread, spread);
            float y = field.SampleHeight(placeX, placeZ) - 0.1f;
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            library.AddParts(asset, batch, Matrix4x4.TRS(new Vector3(placeX, y, placeZ), rotation, Vector3.one * scale));
            pieceCount++;
        }
    }

    public void Build()
    {
        BuildCampsites(62);
        BuildFarms(34);
        BuildRuins(26);
        BuildLumberCamps(34);
        BuildShorelines(46);
        BuildMeadows(280);
        BuildMushroomPatches(84);
        BuildBoulderFields(54);
        BuildPastures(28);
    }

    private void BuildCampsites(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindSpot(9f, 275f, 0.24f, 90f, 34f, out spot))
            {
                continue;
            }
            clusterCount++;
            ClaimStructure(spot, 30f);

            PlaceByFootprint(Pick(campfires), spot.x, spot.y, 3.4f, Random.Range(0f, 360f), 0.1f);

            int tentCount = Random.Range(1, 4);
            for (int t = 0; t < tentCount; t++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(7f, 13f);
                float x = spot.x + Mathf.Cos(angle) * radius;
                float z = spot.y + Mathf.Sin(angle) * radius;
                PlaceByHeight(Pick(tents), x, z, Random.Range(5f, 6.5f), 0.2f);
            }

            int seatCount = Random.Range(2, 5);
            for (int s = 0; s < seatCount; s++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(3.2f, 5f);
                float x = spot.x + Mathf.Cos(angle) * radius;
                float z = spot.y + Mathf.Sin(angle) * radius;
                PlaceByFootprint(Pick(seats), x, z, Random.Range(3f, 4.5f), Random.Range(0f, 360f), 0.15f);
            }

            for (int r = 0; r < 3; r++)
            {
                float x = spot.x + Random.Range(-16f, 16f);
                float z = spot.y + Random.Range(-16f, 16f);
                PlaceByFootprint(Pick(smallRocks), x, z, Random.Range(1.6f, 3.2f), Random.Range(0f, 360f), 0.2f);
            }

            for (int b = 0; b < 4; b++)
            {
                float x = spot.x + Random.Range(-18f, 18f);
                float z = spot.y + Random.Range(-18f, 18f);
                PlaceByHeight(Pick(bushes), x, z, Random.Range(2f, 3.2f), 0.15f);
            }

            PlaceCharacters(spot.x, spot.y, 7f, Random.Range(2, 5));
        }
    }

    private void BuildFarms(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindSpot(12f, 130f, 0.14f, 140f, 78f, out spot))
            {
                continue;
            }
            clusterCount++;
            ClaimStructure(spot, 72f);

            float yaw = Random.Range(0, 4) * 90f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            int rows = Random.Range(8, 14);
            int columns = Random.Range(9, 16);

            bool isPloughed = Random.value < 0.28f;
            string cropModel = Pick(cropRows);
            string dirtModel = Pick(cropDirt);
            float cropHeight = Random.Range(3.4f, 4.4f);
            float spacing = 5.4f;
            if (isPloughed)
            {
                spacing = ScaledSpanX(dirtModel, 5.6f, false) * 0.99f;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    Vector3 local = new Vector3((c - columns * 0.5f) * spacing, 0f, (r - rows * 0.5f) * spacing);
                    Vector3 offset = rotation * local;
                    float x = spot.x + offset.x;
                    float z = spot.y + offset.z;

                    if (isPloughed)
                    {
                        PlaceByFootprint(dirtModel, x, z, 5.6f, yaw, 0.05f);
                        if (Random.value < 0.3f)
                        {
                            PlaceByHeight(Pick(rootCrops), x, z, 1.9f, 0.05f);
                        }
                        continue;
                    }

                    if (Random.value < 0.07f)
                    {
                        continue;
                    }

                    float jitter = spacing * 0.12f;
                    PlaceByHeight(cropModel,
                        x + Random.Range(-jitter, jitter),
                        z + Random.Range(-jitter, jitter),
                        cropHeight * Random.Range(0.9f, 1.1f), 0.15f);
                }
            }

            float halfX = columns * spacing * 0.5f + spacing * 0.6f;
            float halfZ = rows * spacing * 0.5f + spacing * 0.6f;
            FenceRectangle(spot, rotation, yaw, halfX, halfZ);
        }
    }

    private void FenceRectangle(Vector2 spot, Quaternion rotation, float yaw, float halfX, float halfZ)
    {
        string fenceModel = Pick(fences);
        float pieceTarget = 5.5f;
        float step = ScaledSpanX(fenceModel, pieceTarget, false);
        if (step < 0.5f)
        {
            return;
        }

        int alongX = Mathf.Max(1, Mathf.RoundToInt(halfX * 2f / step));
        int alongZ = Mathf.Max(1, Mathf.RoundToInt(halfZ * 2f / step));
        float stepX = halfX * 2f / alongX;
        float stepZ = halfZ * 2f / alongZ;
        float fit = pieceTarget * stepX / step;
        float fitZ = pieceTarget * stepZ / step;

        for (int i = 0; i < alongX; i++)
        {
            float t = -halfX + (i + 0.5f) * stepX;
            PlaceFencePiece(fenceModel, spot, rotation, new Vector3(t, 0f, -halfZ), yaw, fit);
            PlaceFencePiece(fenceModel, spot, rotation, new Vector3(t, 0f, halfZ), yaw + 180f, fit);
        }
        for (int i = 0; i < alongZ; i++)
        {
            float t = -halfZ + (i + 0.5f) * stepZ;
            PlaceFencePiece(fenceModel, spot, rotation, new Vector3(-halfX, 0f, t), yaw + 270f, fitZ);
            PlaceFencePiece(fenceModel, spot, rotation, new Vector3(halfX, 0f, t), yaw + 90f, fitZ);
        }
    }

    private void PlaceFencePiece(string modelName, Vector2 spot, Quaternion rotation, Vector3 local, float yaw, float target)
    {
        Vector3 offset = rotation * local;
        PlaceByFootprint(modelName, spot.x + offset.x, spot.y + offset.z, target, yaw, 0.2f);
    }

    private void BuildRuins(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindSpot(45f, 300f, 0.28f, 120f, 40f, out spot))
            {
                continue;
            }
            clusterCount++;
            ClaimStructure(spot, 34f);

            PlaceByHeight("statue_obelisk", spot.x, spot.y, Random.Range(9f, 13f), 0.3f);

            int columnCount = Random.Range(4, 8);
            float ringRadius = Random.Range(9f, 15f);
            for (int c = 0; c < columnCount; c++)
            {
                float angle = (float)c / columnCount * Mathf.PI * 2f;
                float x = spot.x + Mathf.Cos(angle) * ringRadius;
                float z = spot.y + Mathf.Sin(angle) * ringRadius;
                PlaceByHeight(Pick(statues), x, z, Random.Range(5f, 8f), 0.25f);
            }

            for (int s = 0; s < 5; s++)
            {
                float x = spot.x + Random.Range(-20f, 20f);
                float z = spot.y + Random.Range(-20f, 20f);
                PlaceByFootprint(Pick(smallRocks), x, z, Random.Range(1.8f, 3.6f), Random.Range(0f, 360f), 0.2f);
            }
        }
    }

    private void BuildLumberCamps(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindForestSpot(0.56f, 14f, 250f, 38f, out spot))
            {
                continue;
            }
            clusterCount++;
            ClaimStructure(spot, 32f);

            int pileCount = Random.Range(2, 5);
            for (int p = 0; p < pileCount; p++)
            {
                float x = spot.x + Random.Range(-12f, 12f);
                float z = spot.y + Random.Range(-12f, 12f);
                PlaceByFootprint(Pick(logPiles), x, z, Random.Range(4f, 7f), Random.Range(0f, 360f), 0.2f);
            }

            int stumpCount = Random.Range(5, 11);
            for (int s = 0; s < stumpCount; s++)
            {
                float x = spot.x + Random.Range(-22f, 22f);
                float z = spot.y + Random.Range(-22f, 22f);
                PlaceByFootprint(Pick(stumps), x, z, Random.Range(1.8f, 3f), Random.Range(0f, 360f), 0.15f);
            }

            if (Random.value < 0.6f)
            {
                PlaceByHeight(Pick(tents), spot.x + Random.Range(-14f, 14f), spot.y + Random.Range(-14f, 14f), 5.5f, 0.2f);
                PlaceByFootprint(Pick(campfires), spot.x, spot.y, 3.2f, Random.Range(0f, 360f), 0.1f);
            }
        }
    }

    private void BuildShorelines(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindSpot(0.8f, 4.5f, 0.3f, 90f, 32f, out spot))
            {
                continue;
            }
            clusterCount++;

            if (Random.value < 0.55f)
            {
                PlaceByFootprint("canoe", spot.x, spot.y, Random.Range(5f, 6.5f), Random.Range(0f, 360f), 0.1f);
                PlaceByFootprint("canoe_paddle", spot.x + Random.Range(-3f, 3f), spot.y + Random.Range(-3f, 3f), 2.6f, Random.Range(0f, 360f), 0.05f);
            }

            int palmCount = Random.Range(1, 4);
            for (int p = 0; p < palmCount; p++)
            {
                float x = spot.x + Random.Range(-16f, 16f);
                float z = spot.y + Random.Range(-16f, 16f);
                PlaceByHeight(Pick(palms), x, z, Random.Range(11f, 16f), 0.3f);
            }

            for (int r = 0; r < 4; r++)
            {
                float x = spot.x + Random.Range(-20f, 20f);
                float z = spot.y + Random.Range(-20f, 20f);
                PlaceByFootprint(Pick(smallRocks), x, z, Random.Range(1.6f, 3.4f), Random.Range(0f, 360f), 0.25f);
            }

            if (Random.value < 0.4f)
            {
                PlaceByFootprint(Pick(campfires), spot.x + Random.Range(-8f, 8f), spot.y + Random.Range(-8f, 8f), 3.2f, Random.Range(0f, 360f), 0.1f);
            }
        }
    }

    private void BuildMeadows(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindSpot(8f, 230f, 0.22f, 70f, 26f, out spot))
            {
                continue;
            }
            clusterCount++;

            int flowerCount = Random.Range(8, 20);
            for (int f = 0; f < flowerCount; f++)
            {
                float x = spot.x + Random.Range(-18f, 18f);
                float z = spot.y + Random.Range(-18f, 18f);
                PlaceByHeight(Pick(flowers), x, z, Random.Range(0.9f, 1.5f), 0.05f);
            }

            int tuftCount = Random.Range(5, 12);
            for (int t = 0; t < tuftCount; t++)
            {
                float x = spot.x + Random.Range(-20f, 20f);
                float z = spot.y + Random.Range(-20f, 20f);
                PlaceByHeight(Pick(tufts), x, z, Random.Range(1f, 1.9f), 0.05f);
            }
        }
    }

    private void BuildMushroomPatches(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindForestSpot(0.62f, 12f, 220f, 18f, out spot))
            {
                continue;
            }
            clusterCount++;

            int shroomCount = Random.Range(4, 10);
            for (int s = 0; s < shroomCount; s++)
            {
                float x = spot.x + Random.Range(-9f, 9f);
                float z = spot.y + Random.Range(-9f, 9f);
                PlaceByHeight(Pick(mushrooms), x, z, Random.Range(1f, 1.8f), 0.05f);
            }

            PlaceByFootprint(Pick(logPiles), spot.x + Random.Range(-7f, 7f), spot.y + Random.Range(-7f, 7f), 4.2f, Random.Range(0f, 360f), 0.2f);
        }
    }

    private void BuildBoulderFields(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindSpot(30f, 340f, 0.55f, 80f, 30f, out spot))
            {
                continue;
            }
            clusterCount++;

            int rockCount = Random.Range(3, 8);
            for (int r = 0; r < rockCount; r++)
            {
                float x = spot.x + Random.Range(-22f, 22f);
                float z = spot.y + Random.Range(-22f, 22f);
                PlaceByFootprint(Pick(boulders), x, z, Random.Range(6f, 16f), Random.Range(0f, 360f), 0.6f);
            }
        }
    }

    private void BuildPastures(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 spot;
            if (!FindSpot(12f, 140f, 0.16f, 130f, 74f, out spot))
            {
                continue;
            }
            clusterCount++;
            ClaimStructure(spot, 68f);

            float yaw = Random.Range(0, 4) * 90f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            float halfX = Random.Range(32f, 56f);
            float halfZ = Random.Range(28f, 50f);
            FenceRectangle(spot, rotation, yaw, halfX, halfZ);

            int bushCount = Random.Range(7, 15);
            for (int b = 0; b < bushCount; b++)
            {
                float x = spot.x + Random.Range(-halfX * 0.88f, halfX * 0.88f);
                float z = spot.y + Random.Range(-halfZ * 0.88f, halfZ * 0.88f);
                PlaceByHeight(Pick(bushes), x, z, Random.Range(2.2f, 3.6f), 0.15f);
            }

            int treeCount = Random.Range(2, 5);
            for (int t = 0; t < treeCount; t++)
            {
                float x = spot.x + Random.Range(-halfX * 0.8f, halfX * 0.8f);
                float z = spot.y + Random.Range(-halfZ * 0.8f, halfZ * 0.8f);
                PlaceByHeight(Pick(pastureTrees), x, z, Random.Range(9f, 14f), 0.3f);
            }

            float hayX = spot.x + (rotation * new Vector3(halfX * 0.55f, 0f, halfZ * 0.55f)).x;
            float hayZ = spot.y + (rotation * new Vector3(halfX * 0.55f, 0f, halfZ * 0.55f)).z;
            for (int h = 0; h < 6; h++)
            {
                PlaceByFootprint(Pick(logPiles), hayX + Random.Range(-9f, 9f), hayZ + Random.Range(-9f, 9f),
                    Random.Range(3.5f, 5.5f), Random.Range(0f, 360f), 0.2f);
            }

            int tuftCount = Random.Range(14, 26);
            for (int t = 0; t < tuftCount; t++)
            {
                float x = spot.x + Random.Range(-halfX, halfX);
                float z = spot.y + Random.Range(-halfZ, halfZ);
                PlaceByHeight(Pick(tufts), x, z, Random.Range(1.4f, 2.4f), 0.05f);
            }
        }
    }
}
