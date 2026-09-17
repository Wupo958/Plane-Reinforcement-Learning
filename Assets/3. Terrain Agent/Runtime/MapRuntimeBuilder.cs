using UnityEngine;

public class MapRuntimeBuilder : MonoBehaviour
{
    [SerializeField] private MapModelManifest modelManifest;
    [SerializeField] private string worldRootName = "World";

    private GameObject worldRoot;

    private void Awake()
    {
        MapAssetLibrary.Manifest = modelManifest;
        worldRoot = GameObject.Find(worldRootName);
        DetachSurvivors();
    }

    private void DetachSurvivors()
    {
        GameObject checkpointGroup = GameObject.Find(worldRootName + "/Checkpoints");
        if (checkpointGroup != null)
        {
            checkpointGroup.transform.SetParent(null, true);
        }
    }

    public void Rebuild(int seed)
    {
        if (modelManifest == null)
        {
            Debug.LogError("MapRuntimeBuilder has no model manifest, run Terrain Map/Bake Model Manifest");
            return;
        }

        MapAssetLibrary.Manifest = modelManifest;

        if (worldRoot != null)
        {
            DestroyImmediate(worldRoot);
        }

        TerrainMapBuilder builder = new TerrainMapBuilder();
        worldRoot = builder.BuildWorld(null, seed);
        worldRoot.name = worldRootName;

        Resources.UnloadUnusedAssets();
    }
}
