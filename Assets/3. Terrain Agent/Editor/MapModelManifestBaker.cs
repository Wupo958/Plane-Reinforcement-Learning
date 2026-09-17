using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class MapModelManifestBaker
{
    public static string ManifestPath = "Assets/3. Terrain Agent/MapModelManifest.asset";

    [MenuItem("Terrain Map/Bake Model Manifest")]
    public static void Bake()
    {
        string[] folders = new string[]
        {
            MapAssetLibrary.Commercial,
            MapAssetLibrary.Roads,
            MapAssetLibrary.Suburban,
            MapAssetLibrary.Nature,
            MapAssetLibrary.Cars,
            MapAssetLibrary.Castle,
            MapAssetLibrary.Industrial,
            MapAssetLibrary.Watercraft,
            MapAssetLibrary.Characters
        };

        List<string> paths = new List<string>();
        List<GameObject> models = new List<GameObject>();

        for (int i = 0; i < folders.Length; i++)
        {
            string folder = folders[i].TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("Model folder missing: " + folder);
                continue;
            }

            string[] files = Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly);
            for (int f = 0; f < files.Length; f++)
            {
                string path = files[f].Replace('\\', '/');
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null)
                {
                    continue;
                }
                paths.Add(path);
                models.Add(model);
            }
        }

        MapModelManifest manifest = AssetDatabase.LoadAssetAtPath<MapModelManifest>(ManifestPath);
        if (manifest == null)
        {
            manifest = ScriptableObject.CreateInstance<MapModelManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
        }

        manifest.Replace(paths, models);
        EditorUtility.SetDirty(manifest);
        AssetDatabase.SaveAssets();

        Debug.Log("Baked model manifest with " + paths.Count + " models to " + ManifestPath);
    }
}
