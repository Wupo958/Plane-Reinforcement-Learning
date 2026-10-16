using System.Collections.Generic;
using UnityEngine;

public class MapNatureBuilder
{
    private MapAssetLibrary library;
    private MapHeightField field;
    private List<MapShape> exclusions;

    public int treeCount;
    public int rockCount;

    private string[] deciduous = new string[]
    {
        "tree_default", "tree_oak", "tree_tall", "tree_thin", "tree_fat",
        "tree_detailed", "tree_plateau", "tree_simple", "tree_blocks", "tree_cone"
    };

    private string[] deciduousFall = new string[]
    {
        "tree_default_fall", "tree_oak_fall", "tree_tall_fall", "tree_simple_fall", "tree_plateau_fall"
    };

    private string[] pines = new string[]
    {
        "tree_pineTallA", "tree_pineTallB", "tree_pineTallC", "tree_pineTallD",
        "tree_pineDefaultA", "tree_pineDefaultB", "tree_pineRoundC", "tree_pineRoundE",
        "tree_pineSmallB", "tree_pineSmallD"
    };

    private string[] palms = new string[]
    {
        "tree_palmTall", "tree_palmShort", "tree_palmDetailedTall", "tree_palmDetailedShort", "tree_palmBend"
    };

    private string[] bigRocks = new string[]
    {
        "rock_largeA", "rock_largeB", "rock_largeC", "rock_largeD", "rock_largeE", "rock_largeF",
        "stone_largeA", "stone_largeB", "stone_largeC", "stone_largeD"
    };

    private string[] tallRocks = new string[]
    {
        "rock_tallA", "rock_tallC", "rock_tallD", "rock_tallF", "rock_tallH",
        "stone_tallB", "stone_tallC", "stone_tallE", "stone_tallH"
    };

    private string[] shrubs = new string[]
    {
        "plant_bush", "plant_bushDetailed", "plant_bushLarge", "plant_bushLargeTriangle", "grass_large"
    };

    public MapNatureBuilder(MapAssetLibrary library, MapHeightField field, List<MapShape> exclusions)
    {
        this.library = library;
        this.field = field;
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

    public void Scatter(MapMeshBatch treeBatch, MapMeshBatch rockBatch)
    {
        float spacing = 24f;
        float half = field.WorldSize() * 0.5f - spacing;

        for (float z = -half; z <= half; z += spacing)
        {
            for (float x = -half; x <= half; x += spacing)
            {
                float sampleX = x + Random.Range(-spacing * 0.48f, spacing * 0.48f);
                float sampleZ = z + Random.Range(-spacing * 0.48f, spacing * 0.48f);

                float height = field.SampleHeight(sampleX, sampleZ);
                if (height < 1.2f || height > 355f)
                {
                    continue;
                }
                if (Excluded(sampleX, sampleZ, 55f))
                {
                    continue;
                }

                float slope = field.SlopeAt(sampleX, sampleZ);
                if (slope > 0.55f)
                {
                    TryRock(rockBatch, sampleX, sampleZ, height, slope);
                    continue;
                }

                float density = MapHeightField.ForestDensity(sampleX, sampleZ);

                if (height < 7.5f)
                {
                    if (Random.value < 0.09f)
                    {
                        PlaceTree(treeBatch, palms, sampleX, sampleZ, height, Random.Range(11f, 17f));
                    }
                    continue;
                }

                if (height > 310f)
                {
                    TryRock(rockBatch, sampleX, sampleZ, height, slope);
                    continue;
                }

                string[] species = deciduous;
                float targetHeight = Random.Range(13f, 22f);
                float threshold = 0.545f;

                if (height > 150f)
                {
                    species = pines;
                    targetHeight = Random.Range(16f, 28f);
                    threshold = 0.5f;
                }
                else if (density > 0.66f && Random.value < 0.22f)
                {
                    species = deciduousFall;
                }

                if (density < threshold)
                {
                    if (Random.value < 0.035f)
                    {
                        PlaceTree(treeBatch, species, sampleX, sampleZ, height, targetHeight * 0.85f);
                    }
                    else if (Random.value < 0.14f)
                    {
                        PlaceTree(treeBatch, shrubs, sampleX, sampleZ, height, Random.Range(2.5f, 4.5f));
                    }
                    continue;
                }

                float chance = Mathf.InverseLerp(threshold, 0.74f, density) * 0.72f + 0.18f;
                if (Random.value > chance)
                {
                    continue;
                }

                PlaceTree(treeBatch, species, sampleX, sampleZ, height, targetHeight);

                if (Random.value < 0.08f)
                {
                    TryRock(rockBatch, sampleX + Random.Range(-12f, 12f), sampleZ + Random.Range(-12f, 12f), height, slope);
                }
            }
        }
    }

    private void TryRock(MapMeshBatch rockBatch, float x, float z, float height, float slope)
    {
        if (Random.value > 0.16f)
        {
            return;
        }

        string[] set = bigRocks;
        if (Random.value < 0.4f)
        {
            set = tallRocks;
        }

        string modelName = set[Random.Range(0, set.Length)];
        GameObject asset = library.Load(MapAssetLibrary.Nature, modelName);
        Bounds bounds = library.LocalBounds(asset);
        float scale = Random.Range(7f, 22f) / Mathf.Max(bounds.size.x, bounds.size.z);

        float y = field.SampleHeight(x, z) - bounds.size.y * scale * 0.18f;
        Quaternion rotation = Quaternion.Euler(Random.Range(-7f, 7f), Random.Range(0f, 360f), Random.Range(-7f, 7f));
        library.AddParts(asset, rockBatch, Matrix4x4.TRS(new Vector3(x, y, z), rotation, Vector3.one * scale));
        rockCount++;
    }

    private void PlaceTree(MapMeshBatch treeBatch, string[] species, float x, float z, float height, float targetHeight)
    {
        string modelName = species[Random.Range(0, species.Length)];
        GameObject asset = library.Load(MapAssetLibrary.Nature, modelName);
        if (asset == null)
        {
            return;
        }

        Bounds bounds = library.LocalBounds(asset);
        if (bounds.size.y < 0.01f)
        {
            return;
        }

        float scale = targetHeight / bounds.size.y;
        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Vector3 position = new Vector3(x, height - targetHeight * 0.03f, z);
        library.AddParts(asset, treeBatch, Matrix4x4.TRS(position, rotation, Vector3.one * scale));
        treeCount++;
    }
}
