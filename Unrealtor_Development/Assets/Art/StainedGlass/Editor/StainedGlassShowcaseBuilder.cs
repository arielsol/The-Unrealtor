using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

/// <summary>
/// Builds dark showcase scenes for stained glass materials.
/// Access via: Tools > Stained Glass > Build Demo Scene
/// </summary>
public class StainedGlassShowcaseBuilder : Editor
{
    private static readonly string MaterialFolder = "Assets/StainedGlass/Materials";
    private static readonly string ShaderName = "StainedGlass/Emissive";

    private static readonly string[] MaterialNames = new string[]
    {
        "SG_CathedralRuby",
        "SG_OceanSapphire",
        "SG_ForestEmerald",
        "SG_SunsetAmber",
        "SG_RoyalAmethyst",
        "SG_AuroraBorealis",
        "SG_GoldenCathedral",
        "SG_MidnightSapphire",
        "SG_RoseWindow",
        "SG_IridescentPrism",
        "SG_MosaicVoronoi",
        "SG_FireAndIce"
    };

    private static readonly Color[] BacklightColors = new Color[]
    {
        new Color(1f, 0.3f, 0.1f),
        new Color(0.2f, 0.5f, 1f),
        new Color(0.2f, 1f, 0.3f),
        new Color(1f, 0.6f, 0.1f),
        new Color(0.6f, 0.2f, 1f),
        new Color(0.2f, 1f, 0.8f),
        new Color(1f, 0.85f, 0.3f),
        new Color(0.15f, 0.2f, 1f),
        new Color(1f, 0.4f, 0.5f),
        new Color(0.8f, 0.9f, 1f),
        new Color(1f, 0.7f, 0.3f),
        new Color(0.8f, 0.3f, 1f),
    };

    // =========================================================================
    // DEMO SCENE - Cubes and Spheres showcase
    // =========================================================================
    [MenuItem("Tools/Stained Glass/Build Demo Scene (Cubes and Spheres)")]
    public static void BuildDemoScene()
    {
        string scenePath = "Assets/StainedGlass/Scenes/StainedGlass_Demo.unity";
        EnsureDirectories(scenePath);
        EnsureMaterials();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // === Dark environment ===
        SetupDarkEnvironment();

        // === Room ===
        GameObject room = new GameObject("--- Room ---");
        Material darkFloor = CreateEnvironmentMaterial("DemoFloor", new Color(0.015f, 0.015f, 0.02f), 0.25f, 0.88f);
        Material darkWall = CreateEnvironmentMaterial("DemoWall", new Color(0.025f, 0.025f, 0.03f), 0f, 0.25f);

        // Reflective floor
        CreateCube(room.transform, "Floor", new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 40), darkFloor);
        // Back wall
        CreateCube(room.transform, "BackWall", new Vector3(0, 5, 16), new Vector3(40, 12, 0.5f), darkWall);
        // Side walls
        CreateCube(room.transform, "LeftWall", new Vector3(-20, 5, 0), new Vector3(0.5f, 12, 40), darkWall);
        CreateCube(room.transform, "RightWall", new Vector3(20, 5, 0), new Vector3(0.5f, 12, 40), darkWall);

        // === SECTION 1: Spheres on pedestals (front row) ===
        GameObject sphereSection = new GameObject("--- Glass Spheres ---");
        float sphereSpacing = 3.2f;
        float sphereStartX = -(MaterialNames.Length / 2f - 0.5f) * sphereSpacing;

        for (int i = 0; i < MaterialNames.Length; i++)
        {
            float x = sphereStartX + i * sphereSpacing;
            Material glassMat = LoadGlassMaterial(i);

            // Pedestal
            GameObject pedestal = CreateCube(sphereSection.transform, $"Pedestal_{i}",
                new Vector3(x, 0.4f, 2), new Vector3(0.8f, 0.8f, 0.8f), darkWall);

            // Glass sphere
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = $"Sphere_{MaterialNames[i]}";
            sphere.transform.parent = sphereSection.transform;
            sphere.transform.position = new Vector3(x, 1.5f, 2);
            sphere.transform.localScale = Vector3.one * 1.2f;
            if (glassMat != null) sphere.GetComponent<Renderer>().sharedMaterial = glassMat;

            // Point light inside/behind sphere for glow
            CreatePointLight(sphereSection.transform, $"SphereLight_{i}",
                new Vector3(x, 1.5f, 2.8f), BacklightColors[i], 6f, 4f, true);

            // Subtle floor uplighting
            CreatePointLight(sphereSection.transform, $"FloorGlow_{i}",
                new Vector3(x, 0.05f, 2), BacklightColors[i] * 0.5f, 2f, 3f, false);
        }

        // === SECTION 2: Cubes floating (middle row) ===
        GameObject cubeSection = new GameObject("--- Glass Cubes ---");
        float cubeSpacing = 3.2f;
        float cubeStartX = -(MaterialNames.Length / 2f - 0.5f) * cubeSpacing;

        for (int i = 0; i < MaterialNames.Length; i++)
        {
            float x = cubeStartX + i * cubeSpacing;
            Material glassMat = LoadGlassMaterial(i);

            // Floating glass cube (rotated 45 degrees for diamond look)
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Cube_{MaterialNames[i]}";
            cube.transform.parent = cubeSection.transform;
            cube.transform.position = new Vector3(x, 2.5f, 7);
            cube.transform.localScale = Vector3.one * 1.3f;
            cube.transform.rotation = Quaternion.Euler(15, 45, 15);
            if (glassMat != null) cube.GetComponent<Renderer>().sharedMaterial = glassMat;

            // Light behind cube
            CreatePointLight(cubeSection.transform, $"CubeBackLight_{i}",
                new Vector3(x, 2.5f, 8.2f), BacklightColors[i], 8f, 4.5f, true);

            // Add slow rotation component
            cube.AddComponent<DemoRotator>();
        }

        // === SECTION 3: Large flat panels on back wall ===
        GameObject panelSection = new GameObject("--- Glass Panels (Wall) ---");

        // 6 large panels on the back wall
        int panelCount = 6;
        float panelSpacing = 5f;
        float panelStartX = -(panelCount / 2f - 0.5f) * panelSpacing;
        int[] panelMaterialIndices = { 0, 5, 3, 10, 4, 11 }; // Ruby, Aurora, Amber, Mosaic, Amethyst, Fire&Ice

        for (int i = 0; i < panelCount; i++)
        {
            float x = panelStartX + i * panelSpacing;
            int matIdx = panelMaterialIndices[i];
            Material glassMat = LoadGlassMaterial(matIdx);

            // Flat panel (Quad)
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = $"WallPanel_{MaterialNames[matIdx]}";
            panel.transform.parent = panelSection.transform;
            panel.transform.position = new Vector3(x, 5f, 15.5f);
            panel.transform.localScale = new Vector3(3.5f, 5f, 1f);
            if (glassMat != null) panel.GetComponent<Renderer>().sharedMaterial = glassMat;
            Object.DestroyImmediate(panel.GetComponent<Collider>());

            // Create frame
            CreatePanelFrame(panelSection.transform, panel.transform.position, 3.5f, 5f, darkWall);

            // Strong backlight behind each wall panel
            CreatePointLight(panelSection.transform, $"WallPanelLight_{i}",
                new Vector3(x, 5f, 16.8f), BacklightColors[matIdx], 12f, 6f, true);
        }

        // === SECTION 4: Large centerpiece sphere ===
        GameObject centerpiece = new GameObject("--- Centerpiece ---");

        // Big iridescent prism sphere
        Material prismMat = LoadGlassMaterial(9); // IridescentPrism
        GameObject bigSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bigSphere.name = "Centerpiece_IridescentSphere";
        bigSphere.transform.parent = centerpiece.transform;
        bigSphere.transform.position = new Vector3(0, 3.5f, 12);
        bigSphere.transform.localScale = Vector3.one * 3f;
        if (prismMat != null) bigSphere.GetComponent<Renderer>().sharedMaterial = prismMat;
        bigSphere.AddComponent<DemoRotator>();

        // Orbiting colored lights around centerpiece
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            float rad = angle * Mathf.Deg2Rad;
            Color[] orbitColors = { Color.red, Color.blue, Color.green, new Color(1f, 0.5f, 0f) };

            CreatePointLight(centerpiece.transform, $"OrbitLight_{i}",
                new Vector3(Mathf.Sin(rad) * 3f, 3.5f, 12 + Mathf.Cos(rad) * 3f),
                orbitColors[i], 5f, 4f, false);
        }

        // === SECTION 5: Mixed shapes cluster ===
        GameObject clusterSection = new GameObject("--- Mixed Shapes ---");

        // Small cubes + spheres in a cluster showing different materials side by side
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                int matIdx = row * 6 + col;
                if (matIdx >= MaterialNames.Length) break;

                float x = -8 + col * 3.2f;
                float z = -4 + row * 3f;
                Material glassMat = LoadGlassMaterial(matIdx);

                // Alternating cube and sphere
                GameObject obj;
                if ((row + col) % 2 == 0)
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.localScale = Vector3.one * 0.8f;
                    obj.transform.rotation = Quaternion.Euler(0, 45, 0);
                }
                else
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    obj.transform.localScale = Vector3.one * 0.9f;
                }

                obj.name = $"Small_{MaterialNames[matIdx]}";
                obj.transform.parent = clusterSection.transform;
                obj.transform.position = new Vector3(x, 0.6f, z);
                if (glassMat != null) obj.GetComponent<Renderer>().sharedMaterial = glassMat;
                obj.AddComponent<DemoRotator>();

                // Small colored light
                CreatePointLight(clusterSection.transform, $"SmallLight_{matIdx}",
                    new Vector3(x, 1.5f, z - 0.5f), BacklightColors[matIdx], 3f, 2.5f, false);
            }
        }

        // === LIGHTING ===
        GameObject lightingParent = new GameObject("--- Lighting ---");

        // Very dim directional (moonlight feel)
        GameObject dirObj = new GameObject("DirectionalLight");
        dirObj.transform.parent = lightingParent.transform;
        dirObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        Light dirLight = dirObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.12f, 0.12f, 0.18f);
        dirLight.intensity = 0.25f;
        dirLight.shadows = LightShadows.Soft;

        // Warm overhead
        CreatePointLight(lightingParent.transform, "OverheadWarm",
            new Vector3(0, 8, 5), new Color(1f, 0.85f, 0.6f), 1f, 20f, false);

        // === CAMERA ===
        SetupCamera(new Vector3(0, 3f, -8f), new Vector3(8, 0, 0), scenePath);

        // === POST-PROCESSING ===
        SetupPostProcessing(scenePath, 1.2f, 0.35f);

        // === REFLECTION PROBE ===
        GameObject reflObj = new GameObject("ReflectionProbe");
        reflObj.transform.position = new Vector3(0, 3, 5);
        ReflectionProbe probe = reflObj.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.size = new Vector3(40, 12, 40);
        probe.resolution = 256;
        probe.hdr = true;

        // === SAVE ===
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StainedGlass] Demo scene saved to: {scenePath}");
        Debug.Log("[StainedGlass] Front row: 12 glass spheres on pedestals");
        Debug.Log("[StainedGlass] Middle row: 12 floating glass cubes (rotating)");
        Debug.Log("[StainedGlass] Back wall: 6 large stained glass panels");
        Debug.Log("[StainedGlass] Center: Large iridescent sphere");
        Debug.Log("[StainedGlass] Floor: Mixed small cubes + spheres");
        Debug.Log("[StainedGlass] Press Play to see rotation and pulse animations!");
    }

    // =========================================================================
    // Original curved gallery scene
    // =========================================================================
    [MenuItem("Tools/Stained Glass/Build Showcase Scene (Dark Room)")]
    public static void BuildShowcaseScene()
    {
        string scenePath = "Assets/StainedGlass/Scenes/StainedGlass_Showcase.unity";
        EnsureDirectories(scenePath);
        EnsureMaterials();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupDarkEnvironment();

        Material darkWall = CreateEnvironmentMaterial("DarkWall", new Color(0.03f, 0.03f, 0.04f), 0f, 0.3f);
        Material darkFloor = CreateEnvironmentMaterial("DarkFloor_Reflective", new Color(0.02f, 0.02f, 0.025f), 0.2f, 0.85f);

        // Room
        GameObject room = new GameObject("--- Room ---");
        CreateCube(room.transform, "Floor", new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30), darkFloor);
        CreateCube(room.transform, "BackWall", new Vector3(0, 4, 12), new Vector3(30, 10, 0.5f), darkWall);
        CreateCube(room.transform, "LeftWall", new Vector3(-15, 4, 0), new Vector3(0.5f, 10, 30), darkWall);
        CreateCube(room.transform, "RightWall", new Vector3(15, 4, 0), new Vector3(0.5f, 10, 30), darkWall);
        CreateCube(room.transform, "Ceiling", new Vector3(0, 9, 0), new Vector3(30, 0.5f, 30), darkWall);

        // Curved gallery of panels
        GameObject gallery = new GameObject("--- Gallery ---");
        float galleryRadius = 10f;
        float totalArc = 180f;
        float startAngle = -90f;
        float angleStep = totalArc / (MaterialNames.Length - 1);

        for (int i = 0; i < MaterialNames.Length; i++)
        {
            float angle = startAngle + (i * angleStep);
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * galleryRadius;
            float z = Mathf.Cos(rad) * galleryRadius;

            Material glassMat = LoadGlassMaterial(i);

            GameObject group = new GameObject($"Panel_{i:D2}_{MaterialNames[i]}");
            group.transform.parent = gallery.transform;
            group.transform.position = new Vector3(x, 2.5f, z);
            group.transform.rotation = Quaternion.Euler(0, angle + 180f, 0);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Glass";
            panel.transform.parent = group.transform;
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localScale = new Vector3(2f, 3.5f, 1);
            if (glassMat != null) panel.GetComponent<Renderer>().sharedMaterial = glassMat;
            Object.DestroyImmediate(panel.GetComponent<Collider>());

            CreatePointLight(group.transform, "BackLight",
                new Vector3(0, 0, 0.5f), BacklightColors[i], 8f, 5f, true);
            CreatePointLight(group.transform, "FrontAccent",
                new Vector3(0, 0, -1.5f), Color.Lerp(BacklightColors[i], Color.white, 0.5f), 2f, 3f, false);
        }

        // Dim directional
        GameObject dirObj = new GameObject("DirectionalLight");
        dirObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        Light dir = dirObj.AddComponent<Light>();
        dir.type = LightType.Directional;
        dir.color = new Color(0.15f, 0.15f, 0.2f);
        dir.intensity = 0.3f;
        dir.shadows = LightShadows.Soft;

        SetupCamera(new Vector3(0, 2.5f, -4f), new Vector3(5, 0, 0), scenePath);
        SetupPostProcessing(scenePath, 1.5f, 0.35f);

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StainedGlass] Showcase scene saved to: {scenePath}");
    }

    // =========================================================================
    // Circular gallery with camera orbit
    // =========================================================================
    [MenuItem("Tools/Stained Glass/Build Showcase Scene (Circular Gallery)")]
    public static void BuildCircularGallery()
    {
        string scenePath = "Assets/StainedGlass/Scenes/StainedGlass_CircularGallery.unity";
        EnsureDirectories(scenePath);
        EnsureMaterials();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupDarkEnvironment();

        Material darkFloor = CreateEnvironmentMaterial("DarkFloor_Circular", new Color(0.015f, 0.015f, 0.02f), 0.3f, 0.9f);

        // Circular floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "CircularFloor";
        floor.transform.position = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(20, 0.5f, 20);
        floor.GetComponent<Renderer>().sharedMaterial = darkFloor;

        // Full 360 degree arrangement
        float radius = 7f;
        float angleStep = 360f / MaterialNames.Length;
        GameObject gallery = new GameObject("--- Circular Gallery ---");

        for (int i = 0; i < MaterialNames.Length; i++)
        {
            float angle = i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * radius;
            float z = Mathf.Cos(rad) * radius;

            Material glassMat = LoadGlassMaterial(i);

            GameObject group = new GameObject($"Panel_{i:D2}_{MaterialNames[i]}");
            group.transform.parent = gallery.transform;
            group.transform.position = new Vector3(x, 2.5f, z);
            group.transform.rotation = Quaternion.Euler(0, angle + 180f, 0);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            panel.name = "Glass";
            panel.transform.parent = group.transform;
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localScale = new Vector3(2.5f, 4f, 1);
            if (glassMat != null) panel.GetComponent<Renderer>().sharedMaterial = glassMat;
            Object.DestroyImmediate(panel.GetComponent<Collider>());

            CreatePointLight(group.transform, "BackLight",
                new Vector3(0, 0, 0.6f), BacklightColors[i], 10f, 5f, true);
        }

        // Center uplight
        CreatePointLight(null, "CenterUplight", new Vector3(0, 0.1f, 0),
            new Color(0.8f, 0.7f, 0.5f), 2f, 12f, false);

        // Camera at center with orbit
        GameObject camObj = SetupCamera(new Vector3(0, 2.5f, 0), new Vector3(0, 0, 0), scenePath);
        camObj.AddComponent<CameraOrbit>();
        camObj.GetComponent<Camera>().fieldOfView = 70f;

        SetupPostProcessing(scenePath, 2f, 0.4f);

        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StainedGlass] Circular gallery saved to: {scenePath}");
        Debug.Log("[StainedGlass] Press Play - camera orbits automatically!");
    }

    // =========================================================================
    // Shared Helpers
    // =========================================================================

    private static void EnsureDirectories(string scenePath)
    {
        string dir = Path.GetDirectoryName(scenePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private static void EnsureMaterials()
    {
        bool allExist = true;
        foreach (string name in MaterialNames)
        {
            if (!File.Exists($"{MaterialFolder}/{name}.mat"))
            { allExist = false; break; }
        }
        if (!allExist)
        {
            Debug.Log("[StainedGlass] Generating material presets...");
            StainedGlassMaterialGenerator.GenerateAllPresets();
        }
    }

    private static Material LoadGlassMaterial(int index)
    {
        if (index < 0 || index >= MaterialNames.Length) return null;
        return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{MaterialNames[index]}.mat");
    }

    private static void SetupDarkEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);
        RenderSettings.ambientIntensity = 0.1f;
        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.015f;
        RenderSettings.fogColor = new Color(0.008f, 0.008f, 0.015f);
    }

    private static Material CreateEnvironmentMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        Material mat = new Material(litShader);
        mat.name = name;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        if (!Directory.Exists(MaterialFolder))
            Directory.CreateDirectory(MaterialFolder);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static GameObject CreateCube(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.parent = parent;
        cube.transform.position = pos;
        cube.transform.localScale = scale;
        if (mat != null) cube.GetComponent<Renderer>().sharedMaterial = mat;
        return cube;
    }

    private static GameObject CreatePointLight(Transform parent, string name, Vector3 localPos,
        Color color, float intensity, float range, bool shadows)
    {
        GameObject obj = new GameObject(name);
        if (parent != null)
        {
            obj.transform.parent = parent;
            obj.transform.localPosition = localPos;
        }
        else
        {
            obj.transform.position = localPos;
        }

        Light light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return obj;
    }

    private static void CreatePanelFrame(Transform parent, Vector3 center, float w, float h, Material mat)
    {
        float t = 0.08f;
        float d = 0.05f;

        CreateCube(parent, "Frame_Top", center + new Vector3(0, h / 2f + t / 2f, 0), new Vector3(w + t * 2, t, d), mat);
        CreateCube(parent, "Frame_Bot", center + new Vector3(0, -h / 2f - t / 2f, 0), new Vector3(w + t * 2, t, d), mat);
        CreateCube(parent, "Frame_L", center + new Vector3(-w / 2f - t / 2f, 0, 0), new Vector3(t, h, d), mat);
        CreateCube(parent, "Frame_R", center + new Vector3(w / 2f + t / 2f, 0, 0), new Vector3(t, h, d), mat);
    }

    private static GameObject SetupCamera(Vector3 pos, Vector3 rot, string scenePath)
    {
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.position = pos;
        camObj.transform.rotation = Quaternion.Euler(rot);

        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.005f, 0.005f, 0.01f);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;
        cam.allowHDR = true;

        var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;
        camData.requiresDepthTexture = true;
        camData.requiresColorTexture = true;

        camObj.AddComponent<AudioListener>();
        return camObj;
    }

    private static void SetupPostProcessing(string scenePath, float bloomIntensity, float vignetteIntensity)
    {
        GameObject volumeObj = new GameObject("PostProcess Volume");
        Volume volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.7f);
        bloom.intensity.Override(bloomIntensity);
        bloom.scatter.Override(0.75f);
        bloom.highQualityFiltering.Override(true);
        bloom.tint.Override(new Color(1f, 0.95f, 0.9f));

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.4f);

        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(0.5f);
        colorAdj.contrast.Override(15f);
        colorAdj.saturation.Override(10f);

        string profileName = Path.GetFileNameWithoutExtension(scenePath) + "_VolumeProfile.asset";
        string profilePath = Path.GetDirectoryName(scenePath) + "/" + profileName;
        AssetDatabase.CreateAsset(profile, profilePath);
        volume.sharedProfile = profile;
    }
}

// DemoRotator runtime script is in Assets/StainedGlass/Scripts/DemoRotator.cs
// CameraOrbit runtime script is in Assets/StainedGlass/Scripts/CameraOrbit.cs
