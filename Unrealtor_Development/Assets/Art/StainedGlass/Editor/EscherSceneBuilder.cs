using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Builds an M.C. Escher impossible space scene using stained glass
/// and Mirza PRISM materials. Colorful, bright, architectural paradox.
/// Access via: Tools > Stained Glass > Build Escher Impossible Space
/// </summary>
public class EscherSceneBuilder : Editor
{
    private static readonly string ScenePath = "Assets/StainedGlass/Scenes/StainedGlass_Escher.unity";
    private static readonly string SGMatFolder = "Assets/StainedGlass/Materials";
    private static readonly string MirzaMatFolder = "Assets/Mirza/PRISM/Demo/Materials";

    // Our stained glass materials
    private static readonly string[] SGNames = {
        "SG_CathedralRuby", "SG_OceanSapphire", "SG_ForestEmerald",
        "SG_SunsetAmber", "SG_RoyalAmethyst", "SG_AuroraBorealis",
        "SG_GoldenCathedral", "SG_MidnightSapphire", "SG_RoseWindow",
        "SG_IridescentPrism", "SG_MosaicVoronoi", "SG_FireAndIce"
    };

    // Bright saturated colors matching the reference image
    private static readonly Color[] PaletteColors = {
        new Color(1f, 0.15f, 0.2f),     // Hot pink/red
        new Color(0f, 0.9f, 0.85f),      // Cyan/teal
        new Color(0.1f, 0.85f, 0.3f),    // Green
        new Color(1f, 0.85f, 0.1f),      // Yellow
        new Color(1f, 0.5f, 0.05f),      // Orange
        new Color(0.2f, 0.3f, 1f),       // Blue
        new Color(0.7f, 0.15f, 0.9f),    // Purple
        new Color(1f, 0.3f, 0.6f),       // Pink
        new Color(0.4f, 1f, 0.2f),       // Lime
        new Color(0f, 0.6f, 1f),         // Sky blue
        new Color(1f, 0.4f, 0.1f),       // Deep orange
        new Color(0.5f, 0f, 0.8f),       // Violet
    };

    private static int colorIndex = 0;
    private static int matIndex = 0;

    [MenuItem("Tools/Stained Glass/Build Escher Impossible Space")]
    public static void BuildEscherScene()
    {
        colorIndex = 0;
        matIndex = 0;

        string dir = Path.GetDirectoryName(ScenePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Ensure stained glass materials exist
        bool materialsExist = true;
        foreach (string n in SGNames)
            if (!File.Exists($"{SGMatFolder}/{n}.mat")) { materialsExist = false; break; }
        if (!materialsExist) StainedGlassMaterialGenerator.GenerateAllPresets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // =====================================================================
        // ENVIRONMENT - Bright colored ambient (not dark - Escher is vivid)
        // =====================================================================
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.3f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.fogColor = new Color(0.15f, 0.18f, 0.22f);

        // Load Mirza PRISM materials
        Material prismV1 = AssetDatabase.LoadAssetAtPath<Material>($"{MirzaMatFolder}/PRISM v1.mat");
        Material prismV2 = AssetDatabase.LoadAssetAtPath<Material>($"{MirzaMatFolder}/PRISM v2 - Monkey 1.mat");

        // =====================================================================
        // BUILD THE IMPOSSIBLE SPACE
        // =====================================================================

        // -- GRAVITY NORMAL (Floor level) --
        GameObject normalGravity = new GameObject("=== Normal Gravity ===");

        // Central platform
        CreateGlassPlatform(normalGravity.transform, Vector3.zero, new Vector3(8, 0.3f, 8), Quaternion.identity);

        // Staircase A - going UP from center (north)
        CreateStaircase(normalGravity.transform, new Vector3(0, 0, 4), Quaternion.identity, 12, 0.6f, 2.5f);

        // Staircase B - going UP from center (east)
        CreateStaircase(normalGravity.transform, new Vector3(4, 0, 0), Quaternion.Euler(0, 90, 0), 10, 0.6f, 2.5f);

        // Staircase C - going UP from center (west)
        CreateStaircase(normalGravity.transform, new Vector3(-4, 0, 0), Quaternion.Euler(0, -90, 0), 10, 0.6f, 2.5f);

        // Upper platform (north, reached by staircase A)
        CreateGlassPlatform(normalGravity.transform, new Vector3(0, 7.2f, 16), new Vector3(6, 0.3f, 6), Quaternion.identity);

        // Columns under upper platform
        CreateColumn(normalGravity.transform, new Vector3(-2.5f, 3.6f, 14), 7.2f);
        CreateColumn(normalGravity.transform, new Vector3(2.5f, 3.6f, 14), 7.2f);
        CreateColumn(normalGravity.transform, new Vector3(-2.5f, 3.6f, 18), 7.2f);
        CreateColumn(normalGravity.transform, new Vector3(2.5f, 3.6f, 18), 7.2f);

        // Upper platform (east)
        CreateGlassPlatform(normalGravity.transform, new Vector3(14, 6f, 0), new Vector3(6, 0.3f, 6), Quaternion.identity);
        CreateColumn(normalGravity.transform, new Vector3(12, 3f, -2.5f), 6f);
        CreateColumn(normalGravity.transform, new Vector3(16, 3f, 2.5f), 6f);

        // Upper platform (west)
        CreateGlassPlatform(normalGravity.transform, new Vector3(-14, 6f, 0), new Vector3(6, 0.3f, 6), Quaternion.identity);
        CreateColumn(normalGravity.transform, new Vector3(-12, 3f, -2.5f), 6f);
        CreateColumn(normalGravity.transform, new Vector3(-16, 3f, 2.5f), 6f);

        // Archways on upper platform
        CreateArchway(normalGravity.transform, new Vector3(0, 7.2f, 13), Quaternion.identity, 3f, 4f);
        CreateArchway(normalGravity.transform, new Vector3(11, 6f, 0), Quaternion.Euler(0, 90, 0), 3f, 3.5f);
        CreateArchway(normalGravity.transform, new Vector3(-11, 6f, 0), Quaternion.Euler(0, 90, 0), 3f, 3.5f);

        // -- WALL GRAVITY (structures on the wall - 90 degree rotation) --
        GameObject wallGravity = new GameObject("=== Wall Gravity (East) ===");

        // This is the IMPOSSIBLE part: a staircase going UP the east wall
        // We rotate the entire group 90 degrees around Z
        CreateStaircase(wallGravity.transform, new Vector3(14, 6.3f, -2),
            Quaternion.Euler(0, 0, -90) * Quaternion.Euler(0, 0, 0), 8, 0.6f, 2f);

        // Wall platform (sideways - on the east wall)
        CreateGlassPlatform(wallGravity.transform, new Vector3(19, 6f, -2),
            new Vector3(0.3f, 5, 5), Quaternion.identity);

        // Stairs going down the wall (west side, mirrored impossible)
        GameObject wallGravityWest = new GameObject("=== Wall Gravity (West) ===");
        CreateStaircase(wallGravityWest.transform, new Vector3(-14, 6.3f, 2),
            Quaternion.Euler(0, 180, 0) * Quaternion.Euler(0, 0, 90), 8, 0.6f, 2f);
        CreateGlassPlatform(wallGravityWest.transform, new Vector3(-19, 6f, 2),
            new Vector3(0.3f, 5, 5), Quaternion.identity);

        // -- CEILING GRAVITY (upside down structures) --
        GameObject ceilingGravity = new GameObject("=== Ceiling Gravity (Inverted) ===");

        // Inverted platform hanging from above
        CreateGlassPlatform(ceilingGravity.transform, new Vector3(0, 16, 10),
            new Vector3(7, 0.3f, 7), Quaternion.identity);

        // Inverted staircase going DOWN from ceiling (appears to go up when viewed from below)
        CreateStaircase(ceilingGravity.transform, new Vector3(0, 16, 7),
            Quaternion.Euler(180, 0, 0), 8, 0.6f, 2.5f);

        // Inverted columns (hanging down)
        CreateColumn(ceilingGravity.transform, new Vector3(-3, 13, 8), 6f);
        CreateColumn(ceilingGravity.transform, new Vector3(3, 13, 8), 6f);
        CreateColumn(ceilingGravity.transform, new Vector3(-3, 13, 12), 6f);
        CreateColumn(ceilingGravity.transform, new Vector3(3, 13, 12), 6f);

        // Inverted archway
        CreateArchway(ceilingGravity.transform, new Vector3(0, 16, 7), Quaternion.Euler(180, 0, 0), 3f, 3.5f);

        // -- DIAGONAL GRAVITY (45-degree tilted structures) --
        GameObject diagGravity = new GameObject("=== Diagonal Gravity ===");

        // Tilted staircase connecting normal to ceiling
        CreateStaircase(diagGravity.transform, new Vector3(6, 7.2f, 16),
            Quaternion.Euler(0, 45, -45), 10, 0.5f, 2f);

        // Tilted platform
        CreateGlassPlatform(diagGravity.transform, new Vector3(-6, 10, 14),
            new Vector3(4, 0.25f, 4), Quaternion.Euler(0, 0, 35));

        CreateStaircase(diagGravity.transform, new Vector3(-8, 10, 12),
            Quaternion.Euler(0, -45, 45), 8, 0.5f, 2f);

        // -- GLASS WALLS & PANELS (large colored transparent surfaces) --
        GameObject glassWalls = new GameObject("=== Glass Walls ===");

        // Large glass walls creating impossible room boundaries
        CreateGlassWall(glassWalls.transform, new Vector3(4, 5, 8), new Vector3(0.05f, 10, 12), Quaternion.identity);
        CreateGlassWall(glassWalls.transform, new Vector3(-4, 5, 8), new Vector3(0.05f, 10, 12), Quaternion.identity);
        CreateGlassWall(glassWalls.transform, new Vector3(0, 8, 5), new Vector3(8, 6, 0.05f), Quaternion.identity);

        // Angled glass panels intersecting impossibly
        CreateGlassWall(glassWalls.transform, new Vector3(8, 8, 6), new Vector3(6, 8, 0.05f), Quaternion.Euler(0, 30, 15));
        CreateGlassWall(glassWalls.transform, new Vector3(-8, 8, 6), new Vector3(6, 8, 0.05f), Quaternion.Euler(0, -30, -15));
        CreateGlassWall(glassWalls.transform, new Vector3(0, 12, 16), new Vector3(10, 6, 0.05f), Quaternion.Euler(20, 0, 0));

        // Smaller decorative glass panels scattered
        CreateGlassWall(glassWalls.transform, new Vector3(10, 3, 8), new Vector3(3, 5, 0.05f), Quaternion.Euler(0, 60, 0));
        CreateGlassWall(glassWalls.transform, new Vector3(-10, 4, 12), new Vector3(3, 4, 0.05f), Quaternion.Euler(0, -45, 10));
        CreateGlassWall(glassWalls.transform, new Vector3(0, 4, -3), new Vector3(4, 3, 0.05f), Quaternion.Euler(0, 0, 0));

        // -- ADDITIONAL IMPOSSIBLE CONNECTIONS --
        GameObject connections = new GameObject("=== Impossible Connections ===");

        // Staircase that connects the upper north platform back to the wall structures
        // This is the "Penrose stairs" illusion
        CreateStaircase(connections.transform, new Vector3(-3, 7.2f, 19),
            Quaternion.Euler(0, 90, 0), 8, 0.5f, 2f);

        // Bridge connecting east wall to ceiling
        CreateGlassPlatform(connections.transform, new Vector3(10, 12, 5),
            new Vector3(8, 0.25f, 2.5f), Quaternion.Euler(0, 0, 25));

        // Bridge connecting west area to diagonal
        CreateGlassPlatform(connections.transform, new Vector3(-10, 8, 8),
            new Vector3(6, 0.25f, 2f), Quaternion.Euler(0, 30, -20));

        // Floating staircase connecting nothing to nothing (pure impossible)
        CreateStaircase(connections.transform, new Vector3(12, 10, 12),
            Quaternion.Euler(-30, 60, 0), 6, 0.5f, 2f);

        // -- DECORATIVE ELEMENTS --
        GameObject decor = new GameObject("=== Decorative Elements ===");

        // Floating glass cubes at various orientations
        float[] cubePositionsX = { -6, 2, 8, -3, 5, -8, 10, -1 };
        float[] cubePositionsY = { 3, 9, 5, 12, 7, 14, 2, 11 };
        float[] cubePositionsZ = { 5, 12, -1, 8, 18, 3, 10, 15 };

        for (int i = 0; i < cubePositionsX.Length; i++)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"FloatingCube_{i}";
            cube.transform.parent = decor.transform;
            cube.transform.position = new Vector3(cubePositionsX[i], cubePositionsY[i], cubePositionsZ[i]);
            cube.transform.localScale = Vector3.one * Random.Range(0.6f, 1.2f);
            cube.transform.rotation = Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
            cube.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();
            cube.AddComponent<DemoRotator>();
        }

        // Floating glass spheres
        for (int i = 0; i < 6; i++)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"FloatingSphere_{i}";
            sphere.transform.parent = decor.transform;
            sphere.transform.position = new Vector3(
                Random.Range(-12f, 12f), Random.Range(4f, 14f), Random.Range(2f, 18f));
            sphere.transform.localScale = Vector3.one * Random.Range(0.5f, 1.5f);

            // Use Mirza PRISM material for some spheres
            if (i % 3 == 0 && prismV1 != null)
                sphere.GetComponent<Renderer>().sharedMaterial = prismV1;
            else if (i % 3 == 1 && prismV2 != null)
                sphere.GetComponent<Renderer>().sharedMaterial = prismV2;
            else
                sphere.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();

            sphere.AddComponent<DemoRotator>();
        }

        // -- ADDITIONAL ARCHWAYS at impossible angles --
        GameObject archSection = new GameObject("=== Impossible Archways ===");
        CreateArchway(archSection.transform, new Vector3(7, 3, 0), Quaternion.Euler(0, 0, 0), 2.5f, 3.5f);
        CreateArchway(archSection.transform, new Vector3(-7, 3, 0), Quaternion.Euler(0, 0, 0), 2.5f, 3.5f);
        CreateArchway(archSection.transform, new Vector3(0, 10, 16), Quaternion.Euler(0, 90, 0), 2.5f, 3f);

        // Sideways archway (impossible - on the wall)
        CreateArchway(archSection.transform, new Vector3(18, 8, 0), Quaternion.Euler(0, 0, 90), 2f, 3f);
        CreateArchway(archSection.transform, new Vector3(-18, 8, 0), Quaternion.Euler(0, 0, -90), 2f, 3f);

        // Upside-down archway
        CreateArchway(archSection.transform, new Vector3(0, 15.5f, 10), Quaternion.Euler(180, 0, 0), 3f, 3f);

        // =====================================================================
        // LIGHTING - Bright and colorful, many colored lights
        // =====================================================================
        GameObject lighting = new GameObject("=== Lighting ===");

        // Main directional - warm bright
        GameObject dirObj = new GameObject("MainLight");
        dirObj.transform.parent = lighting.transform;
        dirObj.transform.rotation = Quaternion.Euler(35, 45, 0);
        Light dirLight = dirObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1f, 0.95f, 0.85f);
        dirLight.intensity = 1.2f;
        dirLight.shadows = LightShadows.Soft;

        // Secondary fill light from opposite side
        GameObject fillObj = new GameObject("FillLight");
        fillObj.transform.parent = lighting.transform;
        fillObj.transform.rotation = Quaternion.Euler(25, -135, 0);
        Light fillLight = fillObj.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.6f, 0.7f, 1f);
        fillLight.intensity = 0.5f;
        fillLight.shadows = LightShadows.None;

        // Colored point lights throughout the space
        Vector3[] lightPositions = {
            new Vector3(0, 2, 0),
            new Vector3(0, 9, 16),
            new Vector3(14, 8, 0),
            new Vector3(-14, 8, 0),
            new Vector3(0, 14, 10),
            new Vector3(8, 5, 8),
            new Vector3(-8, 5, 8),
            new Vector3(0, 4, -2),
            new Vector3(10, 12, 12),
            new Vector3(-6, 11, 14),
            new Vector3(4, 7, 20),
            new Vector3(-4, 7, 20),
        };

        for (int i = 0; i < lightPositions.Length; i++)
        {
            Color c = PaletteColors[i % PaletteColors.Length];
            CreateLight(lighting.transform, $"ColorLight_{i}", lightPositions[i],
                c, 4f, 12f, false);
        }

        // Bright white lights near key architectural features
        CreateLight(lighting.transform, "StairTopLight", new Vector3(0, 10, 16),
            new Color(1f, 0.95f, 0.9f), 3f, 15f, false);
        CreateLight(lighting.transform, "CenterBrightLight", new Vector3(0, 6, 8),
            Color.white, 2f, 18f, false);

        // =====================================================================
        // CAMERA
        // =====================================================================
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.position = new Vector3(-8, 8, -6);
        camObj.transform.rotation = Quaternion.Euler(15, 35, 0);

        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        cam.allowHDR = true;

        var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
        camData.requiresDepthTexture = true;
        camData.requiresColorTexture = true;

        camObj.AddComponent<AudioListener>();

        // =====================================================================
        // POST-PROCESSING - vivid colors, bloom for glass glow
        // =====================================================================
        GameObject volumeObj = new GameObject("PostProcess Volume");
        Volume volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.8f);
        bloom.scatter.Override(0.7f);
        bloom.highQualityFiltering.Override(true);

        Tonemapping tm = profile.Add<Tonemapping>(true);
        tm.mode.Override(TonemappingMode.ACES);

        ColorAdjustments ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.3f);
        ca.contrast.Override(10f);
        ca.saturation.Override(20f);
        ca.colorFilter.Override(new Color(1f, 0.98f, 0.95f));

        Vignette vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.2f);

        string profilePath = "Assets/StainedGlass/Scenes/Escher_VolumeProfile.asset";
        AssetDatabase.CreateAsset(profile, profilePath);
        volume.sharedProfile = profile;

        // =====================================================================
        // REFLECTION PROBE
        // =====================================================================
        GameObject reflObj = new GameObject("ReflectionProbe");
        reflObj.transform.position = new Vector3(0, 8, 8);
        ReflectionProbe probe = reflObj.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(50, 25, 50);
        probe.resolution = 256;
        probe.hdr = true;

        // =====================================================================
        // SAVE
        // =====================================================================
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StainedGlass] Escher Impossible Space scene saved to: {ScenePath}");
        Debug.Log("[StainedGlass] Features:");
        Debug.Log("  - Normal gravity: central platform + 3 staircases + upper platforms");
        Debug.Log("  - Wall gravity: sideways staircases on east/west walls");
        Debug.Log("  - Ceiling gravity: inverted platform + upside-down stairs");
        Debug.Log("  - Diagonal: tilted stairs connecting levels impossibly");
        Debug.Log("  - Large colored glass walls intersecting at angles");
        Debug.Log("  - Archways at normal, sideways, and inverted orientations");
        Debug.Log("  - Floating glass cubes and spheres (Mirza PRISM + StainedGlass)");
        Debug.Log("  - Press Play for rotating elements!");
    }

    // =========================================================================
    // GEOMETRY BUILDERS
    // =========================================================================

    /// <summary>
    /// Creates a staircase: a series of steps going up.
    /// </summary>
    private static void CreateStaircase(Transform parent, Vector3 startPos, Quaternion rotation,
        int stepCount, float stepHeight, float stepWidth)
    {
        GameObject stairGroup = new GameObject($"Staircase_{matIndex}");
        stairGroup.transform.parent = parent;
        stairGroup.transform.position = startPos;
        stairGroup.transform.rotation = rotation;

        float stepDepth = 0.6f;

        for (int i = 0; i < stepCount; i++)
        {
            // Step surface (glass)
            GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = $"Step_{i}";
            step.transform.parent = stairGroup.transform;
            step.transform.localPosition = new Vector3(0, i * stepHeight, i * stepDepth);
            step.transform.localScale = new Vector3(stepWidth, 0.12f, stepDepth);
            step.transform.localRotation = Quaternion.identity;
            step.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();

            // Step riser (vertical face, also glass)
            if (i > 0)
            {
                GameObject riser = GameObject.CreatePrimitive(PrimitiveType.Cube);
                riser.name = $"Riser_{i}";
                riser.transform.parent = stairGroup.transform;
                riser.transform.localPosition = new Vector3(0, i * stepHeight - stepHeight / 2f, i * stepDepth - stepDepth / 2f);
                riser.transform.localScale = new Vector3(stepWidth, stepHeight, 0.06f);
                riser.transform.localRotation = Quaternion.identity;
                riser.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();
            }
        }

        // Side rails (thin glass walls along the staircase)
        float totalHeight = stepCount * stepHeight;
        float totalDepth = stepCount * stepDepth;
        float railHeight = 1.5f;

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = side < 0 ? "RailLeft" : "RailRight";
            rail.transform.parent = stairGroup.transform;

            float midStep = stepCount / 2f;
            rail.transform.localPosition = new Vector3(
                side * (stepWidth / 2f + 0.03f),
                midStep * stepHeight + railHeight / 2f,
                midStep * stepDepth);

            float length = Mathf.Sqrt(totalHeight * totalHeight + totalDepth * totalDepth);
            float angle = Mathf.Atan2(totalHeight, totalDepth) * Mathf.Rad2Deg;

            rail.transform.localScale = new Vector3(0.06f, railHeight, length);
            rail.transform.localRotation = Quaternion.Euler(angle, 0, 0);
            rail.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();
        }
    }

    /// <summary>
    /// Creates a glass platform (floor/ceiling section).
    /// </summary>
    private static void CreateGlassPlatform(Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = $"Platform_{matIndex}";
        platform.transform.parent = parent;
        platform.transform.position = pos;
        platform.transform.localScale = scale;
        platform.transform.rotation = rot;
        platform.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();
    }

    /// <summary>
    /// Creates a glass column (vertical cylinder).
    /// </summary>
    private static void CreateColumn(Transform parent, Vector3 center, float height)
    {
        GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        col.name = $"Column_{matIndex}";
        col.transform.parent = parent;
        col.transform.position = center;
        col.transform.localScale = new Vector3(0.35f, height / 2f, 0.35f);
        col.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();
    }

    /// <summary>
    /// Creates an archway using a series of cubes arranged in a semicircle.
    /// </summary>
    private static void CreateArchway(Transform parent, Vector3 pos, Quaternion baseRot,
        float width, float height)
    {
        GameObject archGroup = new GameObject($"Archway_{matIndex}");
        archGroup.transform.parent = parent;
        archGroup.transform.position = pos;
        archGroup.transform.rotation = baseRot;

        Material archMat = GetNextGlassMaterial();
        float pillarThick = 0.25f;
        float pillarHeight = height * 0.6f;

        // Left pillar
        GameObject leftPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftPillar.name = "PillarL";
        leftPillar.transform.parent = archGroup.transform;
        leftPillar.transform.localPosition = new Vector3(-width / 2f, pillarHeight / 2f, 0);
        leftPillar.transform.localScale = new Vector3(pillarThick, pillarHeight, pillarThick);
        leftPillar.transform.localRotation = Quaternion.identity;
        leftPillar.GetComponent<Renderer>().sharedMaterial = archMat;

        // Right pillar
        GameObject rightPillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightPillar.name = "PillarR";
        rightPillar.transform.parent = archGroup.transform;
        rightPillar.transform.localPosition = new Vector3(width / 2f, pillarHeight / 2f, 0);
        rightPillar.transform.localScale = new Vector3(pillarThick, pillarHeight, pillarThick);
        rightPillar.transform.localRotation = Quaternion.identity;
        rightPillar.GetComponent<Renderer>().sharedMaterial = archMat;

        // Arch curve (semicircle of cubes)
        Material archCurveMat = GetNextGlassMaterial();
        int segments = 12;
        float archRadius = width / 2f;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = t * Mathf.PI;
            float x = -Mathf.Cos(angle) * archRadius;
            float y = Mathf.Sin(angle) * archRadius + pillarHeight;

            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = $"ArchSeg_{i}";
            seg.transform.parent = archGroup.transform;
            seg.transform.localPosition = new Vector3(x, y, 0);
            seg.transform.localScale = new Vector3(pillarThick * 1.2f, pillarThick, pillarThick);
            seg.transform.localRotation = Quaternion.Euler(0, 0, -angle * Mathf.Rad2Deg + 90);
            seg.GetComponent<Renderer>().sharedMaterial = archCurveMat;
        }
    }

    /// <summary>
    /// Creates a large glass wall panel.
    /// </summary>
    private static void CreateGlassWall(Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        // Use a Quad for thin walls to get proper transparency
        GameObject wall;
        if (scale.x < 0.1f || scale.z < 0.1f)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(wall.GetComponent<Collider>());

            if (scale.x < 0.1f)
                wall.transform.localScale = new Vector3(scale.z, scale.y, 1);
            else
                wall.transform.localScale = new Vector3(scale.x, scale.y, 1);
        }
        else
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = scale;
        }

        wall.name = $"GlassWall_{matIndex}";
        wall.transform.parent = parent;
        wall.transform.position = pos;
        wall.transform.rotation = rot;
        wall.GetComponent<Renderer>().sharedMaterial = GetNextGlassMaterial();
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private static Material GetNextGlassMaterial()
    {
        string name = SGNames[matIndex % SGNames.Length];
        matIndex++;
        Material mat = AssetDatabase.LoadAssetAtPath<Material>($"{SGMatFolder}/{name}.mat");
        return mat;
    }

    private static Color GetNextColor()
    {
        Color c = PaletteColors[colorIndex % PaletteColors.Length];
        colorIndex++;
        return c;
    }

    private static void CreateLight(Transform parent, string name, Vector3 pos,
        Color color, float intensity, float range, bool shadows)
    {
        GameObject obj = new GameObject(name);
        obj.transform.parent = parent;
        obj.transform.position = pos;
        Light light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }
}
