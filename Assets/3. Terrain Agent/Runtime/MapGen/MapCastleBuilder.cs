using UnityEngine;

public class MapCastleBuilder
{
    public static bool wallFacesZ = true;

    private MapAssetLibrary library;
    private MapHeightField field;
    private MapMeshBatch batch;
    private float unit = 12f;

    public int pieceCount;

    private string[] flags = new string[]
    {
        "flag", "flag-wide", "flag-pennant"
    };

    private string[] siege = new string[]
    {
        "siege-catapult", "siege-trebuchet", "siege-ram", "siege-tower", "siege-ballista"
    };

    public MapCastleBuilder(MapAssetLibrary library, MapHeightField field, MapMeshBatch batch)
    {
        this.library = library;
        this.field = field;
        this.batch = batch;
    }

    private void Place(string modelName, Vector3 position, float yaw)
    {
        PlaceScaled(modelName, position, yaw, unit);
    }

    private void PlaceScaled(string modelName, Vector3 position, float yaw, float scale)
    {
        GameObject asset = library.Load(MapAssetLibrary.Castle, modelName);
        if (asset == null)
        {
            return;
        }
        library.AddParts(asset, batch, Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale));
        pieceCount++;
    }

    private void PlaceFlag(Vector3 apex)
    {
        string modelName = Pick(flags);
        GameObject asset = library.Load(MapAssetLibrary.Castle, modelName);
        if (asset == null)
        {
            return;
        }
        Bounds bounds = library.LocalBounds(asset);
        float scale = 7.5f / bounds.size.y;
        PlaceScaled(modelName, new Vector3(apex.x, apex.y - 1.2f, apex.z), Random.Range(0f, 360f), scale);
    }

    private float PieceHeight(string modelName)
    {
        GameObject asset = library.Load(MapAssetLibrary.Castle, modelName);
        if (asset == null)
        {
            return 0f;
        }
        return library.LocalBounds(asset).size.y * unit;
    }

    // Returns the top of the stack so the caller can hang a flag off it.
    private float StackTower(Vector3 basePosition, int midSections, string roofModel, float yaw)
    {
        float cursor = basePosition.y;
        Place("tower-square-base", new Vector3(basePosition.x, cursor, basePosition.z), yaw);
        cursor += PieceHeight("tower-square-base");

        string[] midModels = new string[] { "tower-square-mid", "tower-square-mid-windows", "tower-square-mid-open-simple" };
        for (int i = 0; i < midSections; i++)
        {
            string midModel = midModels[Random.Range(0, midModels.Length)];
            Place(midModel, new Vector3(basePosition.x, cursor, basePosition.z), yaw);
            cursor += PieceHeight(midModel);
        }

        Place("tower-square-top", new Vector3(basePosition.x, cursor, basePosition.z), yaw);
        cursor += PieceHeight("tower-square-top");

        if (roofModel != null)
        {
            Place(roofModel, new Vector3(basePosition.x, cursor, basePosition.z), yaw);
            cursor += PieceHeight(roofModel);
        }
        return cursor;
    }

    public void Build(Vector2 center, int cellsX, int cellsZ, float groundHeight)
    {
        float originX = center.x - (cellsX - 1) * unit * 0.5f;
        float originZ = center.y - (cellsZ - 1) * unit * 0.5f;
        float wallYawOffset = 0f;
        if (!wallFacesZ)
        {
            wallYawOffset = 90f;
        }

        int gateCell = cellsX / 2;

        for (int i = 0; i < cellsX; i++)
        {
            for (int j = 0; j < cellsZ; j++)
            {
                bool edgeX = i == 0 || i == cellsX - 1;
                bool edgeZ = j == 0 || j == cellsZ - 1;
                if (!edgeX && !edgeZ)
                {
                    continue;
                }

                float x = originX + i * unit;
                float z = originZ + j * unit;
                Vector3 position = new Vector3(x, groundHeight, z);

                if (edgeX && edgeZ)
                {
                    continue;
                }

                float yaw = 0f;
                if (edgeZ)
                {
                    yaw = 90f;
                }
                if (j == 0 && i == gateCell)
                {
                    // wall-doorway only covers half a cell, with the arch on its near edge, so a
                    // mirrored pair fills the cell and centres the opening.
                    Place("wall-doorway", position, yaw + wallYawOffset);
                    Place("wall-doorway", position, yaw + wallYawOffset + 180f);
                    continue;
                }

                Place("wall", position, yaw + wallYawOffset);
            }
        }

        float cornerRoofTop = 0f;
        for (int c = 0; c < 4; c++)
        {
            int i = 0;
            int j = 0;
            if (c == 1 || c == 2)
            {
                i = cellsX - 1;
            }
            if (c == 2 || c == 3)
            {
                j = cellsZ - 1;
            }

            Vector3 basePosition = new Vector3(originX + i * unit, groundHeight, originZ + j * unit);
            cornerRoofTop = StackTower(basePosition, 2, "tower-square-top-roof-high", 90f * c);
            PlaceFlag(new Vector3(basePosition.x, cornerRoofTop, basePosition.z));
        }

        Vector3 keepPosition = new Vector3(center.x, groundHeight, center.y);
        float keepTop = StackTower(keepPosition, 4, "tower-square-top-roof-high-windows", 0f);
        PlaceFlag(new Vector3(keepPosition.x, keepTop, keepPosition.z));

        Vector3[] innerOffsets = new Vector3[]
        {
            new Vector3(-unit * 2f, 0f, unit * 1.6f),
            new Vector3(unit * 2f, 0f, unit * 1.6f),
            new Vector3(-unit * 2f, 0f, -unit * 1.6f),
            new Vector3(unit * 2f, 0f, -unit * 1.6f)
        };
        for (int i = 0; i < innerOffsets.Length; i++)
        {
            Vector3 position = new Vector3(center.x + innerOffsets[i].x, groundHeight, center.y + innerOffsets[i].z);
            StackTower(position, 1, "tower-square-roof", 90f * Random.Range(0, 4));
        }

        for (int i = 0; i < 4; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(cellsX * unit * 0.75f, cellsX * unit * 1.1f);
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.y + Mathf.Sin(angle) * radius;
            Place(Pick(siege), new Vector3(x, field.SampleHeight(x, z) - 0.3f, z), Random.Range(0f, 360f));
        }
    }

    private string Pick(string[] set)
    {
        return set[Random.Range(0, set.Length)];
    }
}
