using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MapCheckpointBaker
{
    public static string LayoutPath = "Assets/3. Terrain Agent/CheckpointLayout.asset";

    public static CheckpointLayout Load()
    {
        return AssetDatabase.LoadAssetAtPath<CheckpointLayout>(LayoutPath);
    }

    [MenuItem("Terrain Map/Bake Checkpoints From Scene")]
    public static void Bake()
    {
        GameObject group = GameObject.Find("World/Checkpoints");
        if (group == null)
        {
            Debug.LogError("No 'World/Checkpoints' object in the open scene, nothing to bake.");
            return;
        }

        List<Vector3> positions = new List<Vector3>();
        for (int i = 0; i < group.transform.childCount; i++)
        {
            positions.Add(group.transform.GetChild(i).position);
        }

        if (positions.Count == 0)
        {
            Debug.LogError("The Checkpoints group has no children, nothing to bake.");
            return;
        }

        CheckpointLayout layout = Load();
        if (layout == null)
        {
            layout = ScriptableObject.CreateInstance<CheckpointLayout>();
            layout.SetPositions(positions.ToArray());
            AssetDatabase.CreateAsset(layout, LayoutPath);
        }
        else
        {
            layout.SetPositions(positions.ToArray());
            EditorUtility.SetDirty(layout);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Baked " + positions.Count + " checkpoint positions into " + LayoutPath
            + ". Rebuilding the world will now use these instead of the generator defaults.");
    }

    [MenuItem("Terrain Map/Clear Baked Checkpoints")]
    public static void Clear()
    {
        if (Load() == null)
        {
            Debug.Log("No baked checkpoint layout to clear.");
            return;
        }
        AssetDatabase.DeleteAsset(LayoutPath);
        Debug.Log("Cleared the baked checkpoint layout; the generator defaults are in use again.");
    }
}
