using System.Collections.Generic;
using UnityEngine;

public class MapModelManifest : ScriptableObject
{
    [SerializeField] private List<string> paths = new List<string>();
    [SerializeField] private List<GameObject> models = new List<GameObject>();

    private Dictionary<string, GameObject> lookup;

    public int Count
    {
        get { return paths.Count; }
    }

    public void Replace(List<string> newPaths, List<GameObject> newModels)
    {
        paths = newPaths;
        models = newModels;
        lookup = null;
    }

    public GameObject Find(string path)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<string, GameObject>();
            for (int i = 0; i < paths.Count && i < models.Count; i++)
            {
                lookup[paths[i]] = models[i];
            }
        }

        GameObject found;
        if (lookup.TryGetValue(path, out found))
        {
            return found;
        }
        return null;
    }
}
