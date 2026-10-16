using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class AssetBoundsDumper
{
    [MenuItem("Terrain Map/Dump Asset Bounds")]
    public static void Dump()
    {
        string[] folders = new string[]
        {
            "Assets/1. Art/kenney_city-kit-commercial_2.1/Models/FBX format",
            "Assets/1. Art/kenney_city-kit-roads/Models/FBX format",
            "Assets/1. Art/kenney_city-kit-suburban_20/Models/FBX format",
            "Assets/1. Art/kenney_nature-kit/Models/FBX format",
            "Assets/1. Art/kenney_car-kit/Models/FBX format",
            "Assets/1. Art/kenney_castle-kit/Models/FBX format",
            "Assets/1. Art/kenney_city-kit-industrial_2.0/Models/FBX format",
            "Assets/1. Art/kenney_watercraft-pack/Models/FBX format",
            "Assets/1. Art/kenney_blocky-characters_20/Models/FBX format"
        };

        StringBuilder output = new StringBuilder();
        for (int f = 0; f < folders.Length; f++)
        {
            string[] files = Directory.GetFiles(folders[f], "*.fbx", SearchOption.TopDirectoryOnly);
            output.AppendLine("### " + folders[f] + "  (" + files.Length + " models)");
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace(Path.DirectorySeparatorChar, '/');
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    output.AppendLine(Path.GetFileNameWithoutExtension(path) + "\tNOT_IMPORTED");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                {
                    output.AppendLine(Path.GetFileNameWithoutExtension(path) + "\tNO_RENDERER");
                    Object.DestroyImmediate(instance);
                    continue;
                }

                Bounds bounds = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                {
                    bounds.Encapsulate(renderers[r].bounds);
                }

                int triangles = 0;
                List<Material> materials = new List<Material>();
                MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>();
                for (int m = 0; m < filters.Length; m++)
                {
                    if (filters[m].sharedMesh != null)
                    {
                        triangles += filters[m].sharedMesh.triangles.Length / 3;
                    }
                }
                for (int r = 0; r < renderers.Length; r++)
                {
                    Material[] shared = renderers[r].sharedMaterials;
                    for (int m = 0; m < shared.Length; m++)
                    {
                        if (shared[m] != null && !materials.Contains(shared[m]))
                        {
                            materials.Add(shared[m]);
                        }
                    }
                }

                string materialNames = "";
                for (int m = 0; m < materials.Count; m++)
                {
                    materialNames += materials[m].name + "|" + materials[m].shader.name + " ";
                }

                output.AppendLine(Path.GetFileNameWithoutExtension(path)
                    + "\tsize=" + bounds.size.x.ToString("F3") + "," + bounds.size.y.ToString("F3") + "," + bounds.size.z.ToString("F3")
                    + "\tcenter=" + bounds.center.x.ToString("F3") + "," + bounds.center.y.ToString("F3") + "," + bounds.center.z.ToString("F3")
                    + "\ttris=" + triangles
                    + "\tmats=" + materialNames);

                Object.DestroyImmediate(instance);
            }
            output.AppendLine();
        }

        string outPath = "Assets/../AssetBounds.txt";
        File.WriteAllText(outPath, output.ToString());
        Debug.Log("AssetBoundsDumper wrote " + Path.GetFullPath(outPath));
    }
}
