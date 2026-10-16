using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MapPreviewShot
{
    public string name;
    public Vector3 position;
    public Vector3 lookAt;
    public float fieldOfView = 55f;
    public bool orthographic;
    public float orthographicSize = 100f;
    public int width = 1280;
    public int height = 720;
}

public class MapPreviewCapture
{
    public static string OutputFolder = "MapPreviews";

    [MenuItem("Terrain Map/Capture Previews")]
    public static void CaptureMenu()
    {
        List<MapPreviewShot> shots = new List<MapPreviewShot>();

        MapPreviewShot overview = new MapPreviewShot();
        overview.name = "01_overview_top";
        overview.position = new Vector3(0f, 5000f, 0f);
        overview.lookAt = Vector3.zero;
        overview.orthographic = true;
        overview.orthographicSize = 3900f;
        overview.width = 1500;
        overview.height = 1500;
        shots.Add(overview);

        MapPreviewShot wide = new MapPreviewShot();
        wide.name = "02_wide_southwest";
        wide.position = new Vector3(-3400f, 2100f, -4200f);
        wide.lookAt = new Vector3(200f, 120f, 600f);
        wide.fieldOfView = 55f;
        wide.width = 1600;
        wide.height = 900;
        shots.Add(wide);

        MapPreviewShot city = new MapPreviewShot();
        city.name = "03_kestrel_city";
        city.position = new Vector3(900f, 420f, -400f);
        city.lookAt = new Vector3(1900f, 80f, 800f);
        city.fieldOfView = 50f;
        city.width = 1600;
        city.height = 900;
        shots.Add(city);

        MapPreviewShot cityClose = new MapPreviewShot();
        cityClose.name = "04_city_streets";
        cityClose.position = new Vector3(1700f, 150f, 420f);
        cityClose.lookAt = new Vector3(1900f, 50f, 800f);
        cityClose.fieldOfView = 45f;
        cityClose.width = 1600;
        cityClose.height = 900;
        shots.Add(cityClose);

        MapPreviewShot mountains = new MapPreviewShot();
        mountains.name = "05_mountains_lake";
        mountains.position = new Vector3(600f, 900f, 300f);
        mountains.lookAt = new Vector3(-2300f, 250f, 1900f);
        mountains.fieldOfView = 55f;
        mountains.width = 1600;
        mountains.height = 900;
        shots.Add(mountains);

        MapPreviewShot airport = new MapPreviewShot();
        airport.name = "06_airport";
        airport.position = new Vector3(250f, 280f, -3500f);
        airport.lookAt = new Vector3(250f, 20f, -2400f);
        airport.fieldOfView = 50f;
        airport.width = 1600;
        airport.height = 900;
        shots.Add(airport);

        MapPreviewShot suburb = new MapPreviewShot();
        suburb.name = "07_larkfield";
        suburb.position = new Vector3(-400f, 320f, -2700f);
        suburb.lookAt = new Vector3(-1350f, 40f, -1800f);
        suburb.fieldOfView = 50f;
        suburb.width = 1600;
        suburb.height = 900;
        shots.Add(suburb);

        MapPreviewShot valley = new MapPreviewShot();
        valley.name = "08_hollow_pine";
        valley.position = new Vector3(-1100f, 520f, 700f);
        valley.lookAt = new Vector3(-2150f, 210f, 1300f);
        valley.fieldOfView = 50f;
        valley.width = 1600;
        valley.height = 900;
        shots.Add(valley);

        MapPreviewShot lake = new MapPreviewShot();
        lake.name = "09_mirror_lake";
        lake.position = new Vector3(400f, 480f, 300f);
        lake.lookAt = new Vector3(-1000f, 40f, 1050f);
        lake.fieldOfView = 52f;
        lake.width = 1600;
        lake.height = 900;
        shots.Add(lake);

        MapPreviewShot river = new MapPreviewShot();
        river.name = "10_river_north";
        river.position = new Vector3(-560f, 300f, -1900f);
        river.lookAt = new Vector3(-800f, 30f, 400f);
        river.fieldOfView = 52f;
        river.width = 1600;
        river.height = 900;
        shots.Add(river);

        MapPreviewShot roadCheck = new MapPreviewShot();
        roadCheck.name = "11_road_orientation";
        roadCheck.position = new Vector3(1900f, 400f, 800f);
        roadCheck.lookAt = new Vector3(1900f, 0f, 800f);
        roadCheck.orthographic = true;
        roadCheck.orthographicSize = 120f;
        roadCheck.width = 1100;
        roadCheck.height = 1100;
        shots.Add(roadCheck);

        MapPreviewShot decorCheck = new MapPreviewShot();
        decorCheck.name = "12_decor_ground";
        decorCheck.position = new Vector3(-1450f, 150f, 2300f);
        decorCheck.lookAt = new Vector3(-1250f, 60f, 2000f);
        decorCheck.fieldOfView = 55f;
        decorCheck.width = 1600;
        decorCheck.height = 900;
        shots.Add(decorCheck);

        MapPreviewShot castle = new MapPreviewShot();
        castle.name = "14_castle";
        castle.position = new Vector3(-2400f, 430f, 1450f);
        castle.lookAt = new Vector3(-2750f, 330f, 1800f);
        castle.fieldOfView = 48f;
        castle.width = 1600;
        castle.height = 900;
        shots.Add(castle);

        MapPreviewShot harbour = new MapPreviewShot();
        harbour.name = "15_harbour";
        harbour.position = new Vector3(2100f, 260f, -2750f);
        harbour.lookAt = new Vector3(2150f, 10f, -1900f);
        harbour.fieldOfView = 52f;
        harbour.width = 1600;
        harbour.height = 900;
        shots.Add(harbour);

        MapPreviewShot industry = new MapPreviewShot();
        industry.name = "16_ironworks";
        industry.position = new Vector3(500f, 330f, -1750f);
        industry.lookAt = new Vector3(1150f, 50f, -1000f);
        industry.fieldOfView = 52f;
        industry.width = 1600;
        industry.height = 900;
        shots.Add(industry);

        MapPreviewShot windFarm = new MapPreviewShot();
        windFarm.name = "17_wind_farm";
        windFarm.position = new Vector3(1100f, 420f, 2100f);
        windFarm.lookAt = new Vector3(1800f, 260f, 2450f);
        windFarm.fieldOfView = 52f;
        windFarm.width = 1600;
        windFarm.height = 900;
        shots.Add(windFarm);

        Capture(shots);
        CaptureDecorCloseups();
    }

    // The decor clusters land wherever the generator's rejection sampling puts them, so the only
    // way to frame one is to ask the scene where the decor meshes actually are.
    public static void CaptureDecorCloseups()
    {
        GameObject props = GameObject.Find("World/Props");
        if (props == null)
        {
            return;
        }

        List<Vector3> anchors = new List<Vector3>();
        MeshFilter[] filters = props.GetComponentsInChildren<MeshFilter>();
        for (int i = 0; i < filters.Length; i++)
        {
            if (!filters[i].gameObject.name.StartsWith("Decor"))
            {
                continue;
            }
            if (filters[i].sharedMesh == null)
            {
                continue;
            }
            anchors.Add(filters[i].sharedMesh.bounds.center);
        }

        List<MapPreviewShot> shots = new List<MapPreviewShot>();
        for (int i = 0; i < anchors.Count && i < 4; i++)
        {
            Vector3 target = anchors[i];
            MapPreviewShot shot = new MapPreviewShot();
            shot.name = "13_decor_closeup_" + i;
            shot.position = target + new Vector3(70f, 55f, -70f);
            shot.lookAt = target;
            shot.fieldOfView = 55f;
            shot.width = 1400;
            shot.height = 800;
            shots.Add(shot);
        }

        if (shots.Count > 0)
        {
            Capture(shots);
        }
    }

    public static void Capture(List<MapPreviewShot> shots)
    {
        string root = Path.GetDirectoryName(Application.dataPath);
        string folder = Path.Combine(root, OutputFolder);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        GameObject holder = new GameObject("PreviewCamera");
        holder.hideFlags = HideFlags.HideAndDontSave;
        Camera camera = holder.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 1f;
        camera.farClipPlane = 22000f;
        camera.allowHDR = true;
        UniversalAdditionalCameraData cameraData = holder.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        for (int i = 0; i < shots.Count; i++)
        {
            MapPreviewShot shot = shots[i];
            Vector3 forward = (shot.lookAt - shot.position).normalized;
            Vector3 up = Vector3.up;
            if (Mathf.Abs(forward.y) > 0.999f)
            {
                up = Vector3.forward;
            }

            camera.transform.position = shot.position;
            camera.transform.rotation = Quaternion.LookRotation(forward, up);
            camera.orthographic = shot.orthographic;
            camera.orthographicSize = shot.orthographicSize;
            camera.fieldOfView = shot.fieldOfView;

            RenderTexture target = new RenderTexture(shot.width, shot.height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(shot.width, shot.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, shot.width, shot.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;
            byte[] png = image.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(folder, shot.name + ".png"), png);

            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }

        Object.DestroyImmediate(holder);
        Debug.Log("Captured " + shots.Count + " previews to " + folder);
    }
}
