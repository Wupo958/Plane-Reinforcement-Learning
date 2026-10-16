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

    // The gates have to outlive the world root they were built under, because the agents hold
    // direct references to them. Moving the group out to the scene root is what keeps them alive
    // through the DestroyImmediate below.
    private void DetachSurvivors()
    {
        GameObject checkpointGroup = GameObject.Find(worldRootName + "/Checkpoints");
        if (checkpointGroup == null)
        {
            return;
        }
        checkpointGroup.transform.SetParent(null, true);
    }

    public void Rebuild(int seed)
    {
        if (modelManifest == null)
        {
            Debug.LogError("MapRuntimeBuilder has no model manifest, run Terrain Map/Bake Model Manifest");
            return;
        }

        MapAssetLibrary.Manifest = modelManifest;

        // The showcase toggle lives on a component that is about to be destroyed with the world,
        // so read it before the teardown and hand it to the replacement afterwards.
        bool showcaseEnabled = true;
        ShowcaseVisuals previous = FindFirstObjectByType<ShowcaseVisuals>();
        if (previous != null)
        {
            showcaseEnabled = previous.ShowcaseEnabled;
        }

        // Run again on every rebuild rather than trusting the one call in Awake. If anything has
        // re-parented the gates since, or Awake ran before the scene was fully loaded, this is the
        // difference between the checkpoints surviving and being destroyed with the world.
        DetachSurvivors();

        if (worldRoot != null)
        {
            DestroyImmediate(worldRoot);
        }

        TerrainMapBuilder builder = new TerrainMapBuilder();
        worldRoot = builder.BuildWorld(null, seed);
        worldRoot.name = worldRootName;

        RewireShowcase(builder, showcaseEnabled);

        // Colliders in the freshly built world are only queryable once their transforms have been
        // pushed to the physics scene, and auto sync is off in the project settings. The gate
        // placement that runs straight after this would otherwise probe an empty world.
        Physics.SyncTransforms();

        Resources.UnloadUnusedAssets();
    }

    // BuildWorld recreates the showcase objects but not the components that drive them, because
    // those are attached by the editor-only wiring pass that does not run at runtime. Without
    // this, gate highlighting and the showcase toggle are lost on the first map rebuild.
    private void RewireShowcase(TerrainMapBuilder builder, bool showcaseEnabled)
    {
        GameObject showcase = builder.ShowcaseRoot;
        if (showcase == null)
        {
            return;
        }

        ShowcaseVisuals visuals = showcase.AddComponent<ShowcaseVisuals>();
        visuals.Configure(builder.TrafficRoot, builder.SmokeRoot, showcaseEnabled);

        CheckpointHighlighter highlighter = showcase.AddComponent<CheckpointHighlighter>();
        highlighter.Rebind();
    }
}
