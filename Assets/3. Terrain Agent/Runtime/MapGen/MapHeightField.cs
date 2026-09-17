using System.Collections.Generic;
using UnityEngine;

public class MapShape
{
    public Vector2 center;
    public bool isRect;
    public Vector2 halfExtent;
    public float radius;
    public float falloff;
    public Vector2[] polyline;

    public static MapShape Path(Vector2[] points, float halfWidth, float falloff)
    {
        MapShape shape = new MapShape();
        shape.polyline = points;
        shape.radius = halfWidth;
        shape.falloff = falloff;
        return shape;
    }

    public static MapShape Disc(float x, float z, float radius, float falloff)
    {
        MapShape shape = new MapShape();
        shape.center = new Vector2(x, z);
        shape.isRect = false;
        shape.radius = radius;
        shape.falloff = falloff;
        return shape;
    }

    public static MapShape Rect(float x, float z, float halfX, float halfZ, float falloff)
    {
        MapShape shape = new MapShape();
        shape.center = new Vector2(x, z);
        shape.isRect = true;
        shape.halfExtent = new Vector2(halfX, halfZ);
        shape.falloff = falloff;
        return shape;
    }

    public float SignedDistance(float x, float z)
    {
        if (polyline != null)
        {
            return MapNoise.DistanceToPolyline(new Vector2(x, z), polyline) - radius;
        }

        if (!isRect)
        {
            float length = Vector2.Distance(new Vector2(x, z), center);
            return length - radius;
        }

        float dx = Mathf.Abs(x - center.x) - halfExtent.x;
        float dz = Mathf.Abs(z - center.y) - halfExtent.y;
        if (dx > 0f || dz > 0f)
        {
            float outX = Mathf.Max(dx, 0f);
            float outZ = Mathf.Max(dz, 0f);
            return Mathf.Sqrt(outX * outX + outZ * outZ);
        }
        return Mathf.Max(dx, dz);
    }

    public float Weight(float x, float z)
    {
        float distance = SignedDistance(x, z);
        return 1f - MapNoise.SmoothStep(0f, falloff, distance);
    }

    public bool Contains(float x, float z, float margin)
    {
        return SignedDistance(x, z) < margin;
    }
}

public class MapFlatten
{
    public MapShape shape;
    public float height;
}

public class MapPaint
{
    public MapShape shape;
    public int paletteIndex;
    public int secondaryIndex = -1;
    public float threshold;
    public float noiseAmount;
    public float noiseScale = 260f;
}

public class MapHeightField
{
    public int cells = 256;
    public float cellSize = 32f;
    public float minCorner = -4096f;
    public float[] heights;

    public List<MapFlatten> flattens = new List<MapFlatten>();
    public List<MapPaint> paints = new List<MapPaint>();

    private Vector2[] mainRidge = new Vector2[]
    {
        new Vector2(-3100f, 2750f),
        new Vector2(-2450f, 900f),
        new Vector2(-1650f, -600f),
        new Vector2(-950f, -1950f)
    };

    private Vector2[] northRidge = new Vector2[]
    {
        new Vector2(-200f, 3150f),
        new Vector2(900f, 2900f),
        new Vector2(1900f, 2450f),
        new Vector2(2700f, 1650f)
    };

    private Vector2[] river = new Vector2[]
    {
        new Vector2(-1000f, 1050f),
        new Vector2(-760f, 640f),
        new Vector2(-830f, 180f),
        new Vector2(-560f, -260f),
        new Vector2(-640f, -760f),
        new Vector2(-430f, -1180f),
        new Vector2(-570f, -1700f)
    };

    private Vector2[] bay = new Vector2[]
    {
        new Vector2(-480f, -3700f),
        new Vector2(-560f, -2700f),
        new Vector2(-620f, -2050f),
        new Vector2(-590f, -1650f)
    };

    private Vector2 lakeCenter = new Vector2(-1000f, 1050f);

    public float WorldSize()
    {
        return cells * cellSize;
    }

    public float VertexX(int i)
    {
        return minCorner + i * cellSize;
    }

    public float NaturalHeight(float x, float z)
    {
        float radial = Mathf.Sqrt(x * x + z * z) / 3500f;
        float coast = MapNoise.Fbm(x, z, 1f / 1500f, 3, 11.3f, 47.7f) - 0.5f;
        float shaped = radial + coast * 0.42f;
        float continent = 1f - MapNoise.SmoothStep(0.62f, 1.02f, shaped);

        float height = Mathf.Lerp(-88f, 40f, continent);

        float hills = (MapNoise.Fbm(x, z, 1f / 900f, 4, 131.7f, 57.2f) - 0.35f) * 82f;
        height += hills * continent;
        height += MountainHeight(x, z) * continent;

        float detail = (MapNoise.Fbm(x, z, 1f / 230f, 3, 311.5f, 88.9f) - 0.5f) * 11f;
        height += detail * continent;

        height = CarveLake(x, z, height);
        height = CarveBay(x, z, height);
        height = CarveRiver(x, z, height);
        return height;
    }

    public float MountainHeight(float x, float z)
    {
        Vector2 point = new Vector2(x, z);

        float mainDistance = MapNoise.DistanceToPolyline(point, mainRidge);
        float mainBand = 1f - MapNoise.SmoothStep(320f, 2150f, mainDistance);
        float mainRidgeNoise = MapNoise.Ridge(x, z, 1f / 1250f, 4, 9.1f, 73.4f);
        float mainHeight = mainBand * mainBand * mainRidgeNoise * 455f;

        float northDistance = MapNoise.DistanceToPolyline(point, northRidge);
        float northBand = 1f - MapNoise.SmoothStep(380f, 1650f, northDistance);
        float northRidgeNoise = MapNoise.Ridge(x, z, 1f / 820f, 3, 143.6f, 201.9f);
        float northHeight = northBand * northBand * (0.32f + northRidgeNoise * 0.68f) * 425f;

        return Mathf.Max(mainHeight, northHeight);
    }

    private float CarveLake(float x, float z, float height)
    {
        float distance = Vector2.Distance(new Vector2(x, z), lakeCenter);
        distance += (MapNoise.Fbm(x, z, 1f / 340f, 2, 77.2f, 133.8f) - 0.5f) * 170f;
        float t = 1f - MapNoise.SmoothStep(430f, 760f, distance);
        return Mathf.Lerp(height, -30f, t);
    }

    private float CarveBay(float x, float z, float height)
    {
        float distance = MapNoise.DistanceToPolyline(new Vector2(x, z), bay);
        distance += (MapNoise.Fbm(x, z, 1f / 420f, 2, 301.4f, 12.6f) - 0.5f) * 280f;
        float t = 1f - MapNoise.SmoothStep(330f, 1150f, distance);
        return Mathf.Lerp(height, -42f, t);
    }

    private float CarveRiver(float x, float z, float height)
    {
        float distance = MapNoise.DistanceToPolyline(new Vector2(x, z), river);
        distance += (MapNoise.Fbm(x, z, 1f / 260f, 2, 401.7f, 55.1f) - 0.5f) * 30f;
        float t = 1f - MapNoise.SmoothStep(40f, 210f, distance);
        return Mathf.Lerp(height, -7f, t);
    }

    public static float ForestDensity(float x, float z)
    {
        return MapNoise.Fbm(x, z, 1f / 760f, 3, 211.3f, 19.8f);
    }

    public void Generate()
    {
        int side = cells + 1;
        heights = new float[side * side];
        for (int j = 0; j < side; j++)
        {
            float z = VertexX(j);
            for (int i = 0; i < side; i++)
            {
                float x = VertexX(i);
                heights[j * side + i] = NaturalHeight(x, z);
            }
        }

        for (int f = 0; f < flattens.Count; f++)
        {
            MapFlatten flatten = flattens[f];
            for (int j = 0; j < side; j++)
            {
                float z = VertexX(j);
                for (int i = 0; i < side; i++)
                {
                    float x = VertexX(i);
                    float weight = flatten.shape.Weight(x, z);
                    if (weight <= 0f)
                    {
                        continue;
                    }
                    int index = j * side + i;
                    heights[index] = Mathf.Lerp(heights[index], flatten.height, weight);
                }
            }
        }
    }

    public float VertexHeight(int i, int j)
    {
        int side = cells + 1;
        int clampedI = Mathf.Clamp(i, 0, cells);
        int clampedJ = Mathf.Clamp(j, 0, cells);
        return heights[clampedJ * side + clampedI];
    }

    public float SampleHeight(float x, float z)
    {
        float localX = (x - minCorner) / cellSize;
        float localZ = (z - minCorner) / cellSize;
        int i = Mathf.Clamp(Mathf.FloorToInt(localX), 0, cells - 1);
        int j = Mathf.Clamp(Mathf.FloorToInt(localZ), 0, cells - 1);
        float u = Mathf.Clamp01(localX - i);
        float v = Mathf.Clamp01(localZ - j);

        float h00 = VertexHeight(i, j);
        float h10 = VertexHeight(i + 1, j);
        float h01 = VertexHeight(i, j + 1);
        float h11 = VertexHeight(i + 1, j + 1);

        if (v >= u)
        {
            return h00 + (h01 - h00) * v + (h11 - h01) * u;
        }
        return h00 + (h10 - h00) * u + (h11 - h10) * v;
    }

    public Vector3 SampleNormal(float x, float z)
    {
        float step = cellSize * 0.5f;
        float left = SampleHeight(x - step, z);
        float right = SampleHeight(x + step, z);
        float back = SampleHeight(x, z - step);
        float front = SampleHeight(x, z + step);
        Vector3 normal = new Vector3(left - right, 2f * step, back - front);
        return normal.normalized;
    }

    public float SlopeAt(float x, float z)
    {
        return 1f - SampleNormal(x, z).y;
    }

    public int PaintOverride(float x, float z)
    {
        for (int i = 0; i < paints.Count; i++)
        {
            MapPaint paint = paints[i];
            float weight = paint.shape.Weight(x, z);
            if (paint.noiseAmount > 0f)
            {
                weight += (MapNoise.Fbm(x, z, 1f / paint.noiseScale, 3, 233.1f, 71.9f) - 0.5f) * paint.noiseAmount;
            }
            if (weight <= paint.threshold)
            {
                continue;
            }

            if (paint.secondaryIndex >= 0 && MapNoise.Fbm(x, z, 1f / 95f, 2, 19.7f, 143.2f) > 0.56f)
            {
                return paint.secondaryIndex;
            }
            return paint.paletteIndex;
        }
        return -1;
    }

    public int BiomeIndex(float x, float z, float height, float slope)
    {
        int painted = PaintOverride(x, z);
        if (painted >= 0)
        {
            return painted;
        }

        float variation = MapNoise.Fbm(x, z, 1f / 430f, 2, 5.5f, 19.2f);
        float fine = MapNoise.Fbm(x, z, 1f / 150f, 2, 61.9f, 83.4f);

        if (height < -20f)
        {
            return MapPalette.DeepBed;
        }
        if (height < 0.4f)
        {
            return MapPalette.ShallowBed;
        }

        float snowLine = 296f + variation * 90f;
        if (height > snowLine && slope < 0.72f)
        {
            if (fine > 0.46f)
            {
                return MapPalette.Snow;
            }
            return MapPalette.SnowDim;
        }

        if (slope > 0.62f)
        {
            return MapPalette.Cliff;
        }
        if (slope > 0.38f)
        {
            if (fine > 0.55f)
            {
                return MapPalette.RockLight;
            }
            if (fine > 0.3f)
            {
                return MapPalette.Rock;
            }
            return MapPalette.RockDark;
        }

        if (height < 6.5f)
        {
            if (fine > 0.48f)
            {
                return MapPalette.Sand;
            }
            return MapPalette.SandDark;
        }

        if (height > 235f)
        {
            if (fine > 0.5f)
            {
                return MapPalette.Rock;
            }
            return MapPalette.DirtDark;
        }

        if (height > 190f)
        {
            if (fine > 0.58f)
            {
                return MapPalette.RockLight;
            }
            return MapPalette.GrassDark;
        }

        float forest = ForestDensity(x, z);
        if (forest > 0.575f && height > 9f)
        {
            return MapPalette.ForestFloor;
        }

        if (slope > 0.2f)
        {
            if (fine > 0.6f)
            {
                return MapPalette.Dirt;
            }
            return MapPalette.GrassDark;
        }

        float blended = variation * 0.62f + fine * 0.38f;
        if (blended > 0.6f)
        {
            return MapPalette.Meadow;
        }
        if (blended > 0.53f)
        {
            return MapPalette.GrassLight;
        }
        if (blended > 0.45f)
        {
            return MapPalette.Grass;
        }
        if (blended > 0.37f)
        {
            return MapPalette.GrassMid;
        }
        return MapPalette.GrassDark;
    }
}
