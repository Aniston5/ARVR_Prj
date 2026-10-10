using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One planter-wide moisture field estimated from twelve depth sensors.</summary>
[DisallowMultipleComponent]
public class PlanterMoistureOverlay : MonoBehaviour
{
    public enum ReadingSource { Demo, Live }
    [Header("Data and view")]
    public ReadingSource source = ReadingSource.Demo;
    public bool undergroundView = true;
    public bool showRoots = true;
    [Range(0, 3)] public int selectedPlant;
    [Min(1)] public float staleAfterSeconds = 10;
    [Range(0.1f, 2)] public float volumeOpacity = 0.85f;
    [Tooltip("Draw through the planter wall, like an underground X-ray view.")]
    public bool xrayOverlay = true;
    public Camera viewingCamera;

    [Header("Root zone dimensions, in CropBedRoot local metres")]
    public float soilSurfaceY = 0.43f;
    [Min(0.1f)] public float rootDepth = 0.32f;
    public Vector2 rootZoneSize = new Vector2(0.25f, 0.28f);
    [Tooltip("Full soil footprint, covering the entire planter.")]
    public Vector2 planterSize = new Vector2(1.1f, 0.3f);
    public Vector2 planterCenter;
    [Tooltip("Three sensor depths in metres below the soil surface; shallow to deep.")]
    public Vector3 sensorDepths = new Vector3(0.04f, 0.14f, 0.26f);
    [Tooltip("Display offset for an exploded view; zero keeps it beneath the plants.")]
    public Vector3 volumeOffset;

    private readonly SoilMoistureProfile readings = new SoilMoistureProfile();
    private readonly List<Material> materials = new List<Material>();
    private readonly List<GameObject> rootLines = new List<GameObject>();
    private readonly Vector3[] plantPositions = new Vector3[4];
    private Material volumeMaterial;
    private Transform volume;
    private Texture3D moistureTexture;
    private Mesh boxMesh, markerMesh;
    private const int GridX = 32, GridY = 16, GridZ = 12;
    private readonly Color32[] fieldPixels = new Color32[GridX * GridY * GridZ];
    private readonly MoistureField.Sample[] fieldSamples = new MoistureField.Sample[12];
    private float nextFieldUpdate;
    private int freshSensorCount;
    private readonly Material[,] sensorMaterials = new Material[4, 3];
    private readonly double[] waterStarted = { -10000, -10000, -10000, -10000 };
    private readonly float[,] displayValues = new float[4, 3];
    private readonly bool[,] fresh = new bool[4, 3];
    private readonly Transform[,] markers = new Transform[4, 3];
    private readonly Transform[] roots = new Transform[4];
    private GameObject visuals;
    private Material lineMaterial, rootMaterial;
    private ReadingSource previousSource;
    private double simulationTime;
    private bool running = true;
    private float timeScale = 1;
    private GUIStyle heading, body, button, small;

    void Awake()
    {
        previousSource = source;
        BuildVisuals();
    }
    void OnEnable() { if (visuals != null) visuals.SetActive(undergroundView); }
    void OnDisable() { if (visuals != null) visuals.SetActive(false); }

    /// <summary>Zero-based plant (0..3), depth slot (0..2), calibrated 0..100 level. Main thread only.</summary>
    public bool SetSensorMoisture(int plant, int depth, float percent)
    {
        if (!readings.Set(plant, depth, percent, Time.unscaledTimeAsDouble)) return false;
        source = previousSource = ReadingSource.Live;
        return true;
    }
    public void ClearReadings() { readings.Clear(); }

    [ContextMenu("Demo/Water selected plant")]
    public void WaterSelectedPlant()
    {
        if (source != ReadingSource.Demo) return;
        waterStarted[Mathf.Clamp(selectedPlant, 0, 3)] = simulationTime;
        running = true;
    }

    [ContextMenu("Demo/Reset all plants")]
    public void ResetDemo()
    {
        source = ReadingSource.Demo;
        readings.Clear();
        simulationTime = 0;
        for (int p = 0; p < 4; p++) waterStarted[p] = -10000;
        running = true;
    }

    void Update()
    {
        if (visuals == null) return;
        if (source != previousSource) { readings.Clear(); previousSource = source; }
        if (source == ReadingSource.Demo && running) simulationTime += Time.unscaledDeltaTime * timeScale;
        if (viewingCamera == null) viewingCamera = Camera.main;
        visuals.SetActive(undergroundView);
        float depth = Mathf.Max(0.1f, rootDepth);
        Vector3 depths = SafeDepths(depth);
        volume.localPosition = new Vector3(planterCenter.x, soilSurfaceY - depth * 0.5f, planterCenter.y) + volumeOffset;
        volume.localScale = new Vector3(planterSize.x, depth, planterSize.y);
        freshSensorCount = 0;
        for (int p = 0; p < 4; p++)
        {
            Vector3 surface = new Vector3(plantPositions[p].x, soilSurfaceY, plantPositions[p].z) + volumeOffset;
            roots[p].localPosition = surface;
            roots[p].localScale = new Vector3(rootZoneSize.x, depth, rootZoneSize.y);
            for (int d = 0; d < 3; d++)
            {
                float value;
                if (source == ReadingSource.Demo)
                {
                    value = SoilMoistureProfile.DemoValue(p, d, simulationTime - waterStarted[p]);
                    fresh[p, d] = true;
                }
                else fresh[p, d] = readings.TryGet(p, d, Time.unscaledTimeAsDouble, staleAfterSeconds, out value);
                displayValues[p, d] = value;
                if (fresh[p, d]) freshSensorCount++;
                markers[p, d].localPosition = surface + new Vector3(rootZoneSize.x * 0.28f, -depths[d], -rootZoneSize.y * 0.22f);
                sensorMaterials[p, d].color = fresh[p, d] ? MoistureColor(value / 100f) : Color.gray;
                Vector3 samplePosition = markers[p, d].localPosition - volumeOffset;
                fieldSamples[p * 3 + d] = new MoistureField.Sample
                {
                    x = samplePosition.x, y = samplePosition.y, z = samplePosition.z,
                    percent = value, valid = fresh[p, d]
                };
            }
        }
        if (Time.unscaledTime >= nextFieldUpdate)
        {
            UpdateField(depth);
            nextFieldUpdate = Time.unscaledTime + 0.2f;
        }
        volumeMaterial.SetFloat("_HasData", freshSensorCount > 0 ? 1 : 0);
        volumeMaterial.SetFloat("_Opacity", volumeOpacity);
        foreach (var item in rootLines) item.SetActive(showRoots);
        foreach (var material in materials)
            material.SetFloat("_ZTest", (float)(xrayOverlay ? CompareFunction.Always : CompareFunction.LessEqual));
    }

    Vector3 SafeDepths(float depth)
    {
        float a = Mathf.Clamp(sensorDepths.x, 0.001f, depth - 0.003f);
        float b = Mathf.Clamp(sensorDepths.y, a + 0.001f, depth - 0.002f);
        return new Vector3(a, b, Mathf.Clamp(sensorDepths.z, b + 0.001f, depth - 0.001f));
    }

    void UpdateField(float depth)
    {
        int index = 0;
        for (int z = 0; z < GridZ; z++)
            for (int y = 0; y < GridY; y++)
                for (int x = 0; x < GridX; x++)
                {
                    float px = planterCenter.x + ((x + 0.5f) / GridX - 0.5f) * planterSize.x;
                    float py = soilSurfaceY - depth + (y + 0.5f) / GridY * depth;
                    float pz = planterCenter.y + ((z + 0.5f) / GridZ - 0.5f) * planterSize.y;
                    bool valid = MoistureField.Estimate(fieldSamples, px, py, pz, out float percent);
                    fieldPixels[index++] = new Color32((byte)Mathf.RoundToInt(percent * 2.55f), 0, 0, (byte)(valid ? 255 : 0));
                }
        moistureTexture.SetPixels32(fieldPixels);
        moistureTexture.Apply(false, false);
    }

    void BuildVisuals()
    {
        var volumeShader = Resources.Load<Shader>("PlanterMoistureVolume");
        var overlayShader = Resources.Load<Shader>("PlanterOverlay");
        if (volumeShader == null || overlayShader == null)
        {
            Debug.LogError("Missing planter visualization shaders in Assets/Resources.", this);
            enabled = false;
            return;
        }
        visuals = new GameObject("UndergroundMoisture");
        visuals.transform.SetParent(transform, false);
        // Build render-only meshes. CreatePrimitive adds colliders which IL2CPP may strip.
        boxMesh = new Mesh { name = "Planter volume box" };
        boxMesh.vertices = new[] {
            new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f),
            new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f),
            new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f)
        };
        boxMesh.triangles = new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,4,7, 0,7,3, 1,2,6, 1,6,5, 0,1,5, 0,5,4, 3,7,6, 3,6,2 };
        boxMesh.RecalculateBounds();
        markerMesh = new Mesh { name = "Sensor marker" };
        markerMesh.vertices = new[] { Vector3.up * .5f, Vector3.right * .5f, Vector3.forward * .5f,
            Vector3.left * .5f, Vector3.back * .5f, Vector3.down * .5f };
        markerMesh.triangles = new[] { 0,2,1, 0,3,2, 0,4,3, 0,1,4, 5,1,2, 5,2,3, 5,3,4, 5,4,1 };
        markerMesh.RecalculateBounds();
        lineMaterial = NewMaterial(overlayShader, new Color(0.7f, 0.9f, 1, 0.45f), 3100);
        rootMaterial = NewMaterial(overlayShader, new Color(1, 0.88f, 0.62f, 0.85f), 3110);
        volumeMaterial = NewMaterial(volumeShader, Color.white, 3000);
        moistureTexture = new Texture3D(GridX, GridY, GridZ, TextureFormat.RGBA32, false)
        {
            name = "Estimated planter moisture", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        volumeMaterial.SetTexture("_MoistureTex", moistureTexture);
        volume = Shape("WholePlanterMoisture", PrimitiveType.Cube, volumeMaterial, Vector3.one);
        for (int axis = 0; axis < 3; axis++)
            for (int a = -1; a <= 1; a += 2)
                for (int b = -1; b <= 1; b += 2)
                {
                    Vector3 start = Vector3.zero, end = Vector3.zero;
                    start[axis] = -0.5f; end[axis] = 0.5f;
                    start[(axis + 1) % 3] = end[(axis + 1) % 3] = a * 0.5f;
                    start[(axis + 2) % 3] = end[(axis + 2) % 3] = b * 0.5f;
                    Line("PlanterEdge", volume, new[] { start, end }, lineMaterial, 0.001f);
                }
        float[] fallbackX = { -0.393f, -0.12f, 0.175f, 0.411f };
        for (int p = 0; p < 4; p++)
        {
            var plant = transform.Find("Plants/Plant_" + (p + 1));
            plantPositions[p] = plant != null ? transform.InverseTransformPoint(plant.position) : new Vector3(fallbackX[p], soilSurfaceY, 0);
            roots[p] = new GameObject("Plant_" + (p + 1) + "_IllustrativeRoots").transform;
            roots[p].SetParent(visuals.transform, false);
            for (int branch = 0; branch < 7; branch++)
            {
                float angle = branch * Mathf.PI * 2 / 7;
                Vector3 side = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var points = new[] { Vector3.zero, side * 0.08f + Vector3.down * 0.20f,
                    side * 0.24f + Vector3.down * 0.46f, side * 0.31f + Vector3.down * (0.70f + branch % 3 * 0.08f) };
                rootLines.Add(Line("RootBranch", roots[p], points, rootMaterial, 0.0025f).gameObject);
                rootLines.Add(Line("RootTip", roots[p], new[] { points[2], points[2] + side * 0.16f + Vector3.down * 0.14f }, rootMaterial, 0.0015f).gameObject);
            }
            for (int d = 0; d < 3; d++)
            {
                var sensorMaterial = NewMaterial(overlayShader, Color.white, 3120);
                sensorMaterials[p, d] = sensorMaterial;
                markers[p, d] = Shape($"Plant_{p + 1}_DepthSensor_{d + 1}", PrimitiveType.Sphere, sensorMaterial, Vector3.one * 0.017f);
            }
        }
    }

    Material NewMaterial(Shader shader, Color color, int queue)
    {
        var material = new Material(shader) { renderQueue = queue };
        if (material.HasProperty("_Color")) material.color = color;
        materials.Add(material);
        return material;
    }
    Transform Shape(string label, PrimitiveType type, Material material, Vector3 scale)
    {
        var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        obj.GetComponent<MeshFilter>().sharedMesh = type == PrimitiveType.Cube ? boxMesh : markerMesh;
        obj.transform.SetParent(visuals.transform, false);
        obj.transform.localScale = scale;
        var renderer = obj.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return obj.transform;
    }
    static LineRenderer Line(string label, Transform parent, Vector3[] points, Material material, float width)
    {
        var obj = new GameObject(label);
        obj.transform.SetParent(parent, false);
        var line = obj.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.sharedMaterial = material;
        line.startWidth = line.endWidth = width;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    public static Color MoistureColor(float t)
    {
        if (t < 0.33f) return Color.Lerp(new Color(0.08f, 0.16f, 0.95f), new Color(0, 0.85f, 0.83f), t / 0.33f);
        if (t < 0.66f) return Color.Lerp(new Color(0, 0.85f, 0.83f), new Color(0.8f, 0.96f, 0.12f), (t - 0.33f) / 0.33f);
        return Color.Lerp(new Color(0.8f, 0.96f, 0.12f), new Color(1, 0.20f, 0.07f), (t - 0.66f) / 0.34f);
    }

    void OnGUI()
    {
        if (visuals == null) return;
        if (heading == null)
        {
            heading = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
        }
        bool portrait = Screen.height > Screen.width;
        float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / (portrait ? 480f : 760f), Screen.height / 610f));
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float w = Screen.width / scale, h = Screen.height / scale;
        selectedPlant = Mathf.Clamp(selectedPlant, 0, 3);
        GUI.Box(new Rect(16, 16, 330, 224), GUIContent.none);
        GUI.Label(new Rect(28, 24, 310, 28), $"Plant {selectedPlant + 1} / Soil profile", heading);
        GUI.Label(new Rect(28, 55, 310, 26), source == ReadingSource.Demo ? "SIMULATED SENSORS + WATER MOVEMENT" : "LIVE SENSORS / ESTIMATED BETWEEN POINTS", small);
        Vector3 depths = SafeDepths(Mathf.Max(0.1f, rootDepth));
        for (int d = 0; d < 3; d++)
            GUI.Label(new Rect(28, 86 + d * 27, 310, 27), $"Sensor {d + 1}   {depths[d] * 100:0.#} cm deep       " +
                (fresh[selectedPlant, d] ? $"{displayValues[selectedPlant, d]:0}%" : "NO DATA / STALE"), body);
        GUI.Label(new Rect(28, 174, 310, 26), $"Whole-bed estimate / {freshSensorCount} of 12 sensors", small);
        GUI.Label(new Rect(28, 198, 310, 26), "Roots are illustrative; not scanned geometry", small);
        float legendWidth = portrait ? 110 : 166;
        float legendX = w - legendWidth - 16;
        GUI.Box(new Rect(legendX, 16, legendWidth, 111), GUIContent.none);
        GUI.Label(new Rect(legendX + 8, 23, legendWidth - 16, 26), "MOISTURE", small);
        Color oldColor = GUI.color;
        for (int i = 0; i < 64; i++)
        {
            GUI.color = MoistureColor(i / 63f);
            float segment = (legendWidth - 16) / 64f;
            GUI.DrawTexture(new Rect(legendX + 8 + i * segment, 57, segment + 0.1f, 18), Texture2D.whiteTexture);
        }
        GUI.color = oldColor;
        GUI.Label(new Rect(legendX + 8, 83, legendWidth - 16, 26), portrait ? "Low       High" : "0% Low      High 100%", small);

        if (undergroundView && viewingCamera != null)
            for (int p = 0; p < 4; p++)
            {
                Vector3 position = viewingCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(plantPositions[p].x, soilSurfaceY + 0.07f, plantPositions[p].z) + volumeOffset));
                if (position.z > 0)
                    GUI.Label(new Rect(position.x / scale - 40, (Screen.height - position.y) / scale, 90, 26), $"Plant {p + 1}", body);
            }
        float rowWidth = w - 56;
        float viewWidth = (rowWidth - 8) / 2;
        float plantWidth = (rowWidth - 24) / 4;
        GUI.Box(new Rect(16, h - 205, w - 32, 189), GUIContent.none);
        if (GUI.Button(new Rect(28, h - 194, viewWidth, 36), "Normal", button)) undergroundView = false;
        if (GUI.Button(new Rect(36 + viewWidth, h - 194, viewWidth, 36), "Underground", button)) undergroundView = true;
        for (int p = 0; p < 4; p++)
        {
            GUI.backgroundColor = selectedPlant == p ? new Color(0.3f, 0.9f, 0.65f) : Color.white;
            if (GUI.Button(new Rect(28 + p * (plantWidth + 8), h - 151, plantWidth, 36), $"Plant {p + 1}", button)) selectedPlant = p;
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = source == ReadingSource.Demo;
        float actionUnit = (rowWidth - 24) / 8;
        if (GUI.Button(new Rect(28, h - 106, actionUnit * 4, 36), portrait ? "Water plant" : "Water selected plant", button)) WaterSelectedPlant();
        if (GUI.Button(new Rect(36 + actionUnit * 4, h - 106, actionUnit * 1.5f, 36), running ? "Pause" : "Resume", button)) running = !running;
        if (GUI.Button(new Rect(44 + actionUnit * 5.5f, h - 106, actionUnit, 36), $"{timeScale:0}x", button)) timeScale = timeScale == 1 ? 5 : timeScale == 5 ? 10 : 1;
        if (GUI.Button(new Rect(52 + actionUnit * 6.5f, h - 106, actionUnit * 1.5f, 36), "Reset", button)) ResetDemo();
        GUI.enabled = true;
        GUI.Label(new Rect(28, h - 61, w - 56, 32), source == ReadingSource.Demo
            ? $"Demo {simulationTime:0.0}s  |  Water moves shallow > middle > deep."
            : "Live: estimated from available sensors; grey = stale.", small);
        GUI.matrix = oldMatrix;
    }

    void OnValidate()
    {
        rootDepth = Mathf.Max(0.1f, rootDepth);
        rootZoneSize = new Vector2(Mathf.Max(0.02f, rootZoneSize.x), Mathf.Max(0.02f, rootZoneSize.y));
        planterSize = new Vector2(Mathf.Max(0.1f, planterSize.x), Mathf.Max(0.1f, planterSize.y));
        sensorDepths = SafeDepths(rootDepth);
        staleAfterSeconds = Mathf.Max(1, staleAfterSeconds);
    }

    void OnDestroy()
    {
        foreach (var material in materials) if (material != null) Destroy(material);
        if (moistureTexture != null) Destroy(moistureTexture);
        if (boxMesh != null) Destroy(boxMesh);
        if (markerMesh != null) Destroy(markerMesh);
        if (visuals != null) Destroy(visuals);
    }
}
