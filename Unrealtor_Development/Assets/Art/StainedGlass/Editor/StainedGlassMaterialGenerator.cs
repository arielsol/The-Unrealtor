using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor tool to generate stained glass material presets.
/// Access via menu: Tools > Stained Glass > Generate Material Presets
/// </summary>
public class StainedGlassMaterialGenerator : EditorWindow
{
    private static readonly string MaterialFolder = "Assets/StainedGlass/Materials";
    private static readonly string ShaderName = "StainedGlass/Emissive";

    [MenuItem("Tools/Stained Glass/Generate All Material Presets")]
    public static void GenerateAllPresets()
    {
        if (!Directory.Exists(MaterialFolder))
            Directory.CreateDirectory(MaterialFolder);

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[StainedGlass] Shader '{ShaderName}' not found! Make sure the shader compiles correctly.");
            return;
        }

        // Delete old materials to regenerate with new properties
        string[] oldMats = Directory.GetFiles(MaterialFolder, "SG_*.mat");
        foreach (string f in oldMats)
            AssetDatabase.DeleteAsset(f.Replace('\\', '/'));

        int count = 0;
        count += CreateCathedralRuby(shader) ? 1 : 0;
        count += CreateOceanSapphire(shader) ? 1 : 0;
        count += CreateForestEmerald(shader) ? 1 : 0;
        count += CreateSunsetAmber(shader) ? 1 : 0;
        count += CreateRoyalAmethyst(shader) ? 1 : 0;
        count += CreateAuroraBorealis(shader) ? 1 : 0;
        count += CreateGoldenCathedral(shader) ? 1 : 0;
        count += CreateMidnightSapphire(shader) ? 1 : 0;
        count += CreateRoseWindow(shader) ? 1 : 0;
        count += CreateIridescentPrism(shader) ? 1 : 0;
        count += CreateMosaicVoronoi(shader) ? 1 : 0;
        count += CreateFireAndIce(shader) ? 1 : 0;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[StainedGlass] Generated {count} material presets in {MaterialFolder}");
    }

    [MenuItem("Tools/Stained Glass/Presets/Cathedral Ruby")]
    public static void MenuCathedralRuby()
    {
        Shader s = Shader.Find(ShaderName);
        if (s != null) { CreateCathedralRuby(s); AssetDatabase.SaveAssets(); }
    }

    [MenuItem("Tools/Stained Glass/Presets/Ocean Sapphire")]
    public static void MenuOceanSapphire()
    {
        Shader s = Shader.Find(ShaderName);
        if (s != null) { CreateOceanSapphire(s); AssetDatabase.SaveAssets(); }
    }

    [MenuItem("Tools/Stained Glass/Presets/Aurora Borealis")]
    public static void MenuAuroraBorealis()
    {
        Shader s = Shader.Find(ShaderName);
        if (s != null) { CreateAuroraBorealis(s); AssetDatabase.SaveAssets(); }
    }

    // =========================================================================
    // Material Creation Helpers
    // =========================================================================

    private static Material CreateBaseMaterial(Shader shader, string name)
    {
        string path = $"{MaterialFolder}/{name}.mat";

        // Delete if exists
        if (File.Exists(path))
            AssetDatabase.DeleteAsset(path);

        Material mat = new Material(shader);
        mat.name = name;

        // Common URP transparent settings
        mat.SetFloat("_QueueOffset", 0);
        mat.SetFloat("_QueueControl", -1);
        mat.SetFloat("_SpecularHighlights", 1);
        mat.SetFloat("_EnvironmentReflections", 1);
        mat.SetFloat("_ReceiveShadows", 1);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        mat.EnableKeyword("_EMISSION");

        // Double-sided by default for 3D objects
        mat.SetFloat("_DoubleSided", 1);
        mat.EnableKeyword("_DOUBLE_SIDED");

        // Glassy defaults - high transparency, strong refraction, shiny
        mat.SetFloat("_GlassOpacity", 0.15f);
        mat.SetFloat("_GlassTintStrength", 0.6f);
        mat.SetFloat("_FresnelOpacityPower", 3f);
        mat.SetFloat("_FresnelOpacityScale", 0.6f);
        mat.SetFloat("_Metallic", 0.08f);
        mat.SetFloat("_Smoothness", 0.98f);
        mat.SetFloat("_Refraction", 0.25f);
        mat.SetFloat("_RefractionIOR", 1.52f);
        mat.SetFloat("_FresnelPower", 4f);
        mat.SetFloat("_FresnelScale", 1.5f);
        mat.SetColor("_FresnelColor", new Color(1f, 1f, 1f, 1f));

        // Moderate emission so glass stays see-through
        mat.SetFloat("_EmissionIntensity", 0.6f);
        mat.SetFloat("_UseGlassColorEmission", 1f);
        mat.EnableKeyword("_USE_GLASS_COLOR_EMISSION");

        // Translucency defaults
        mat.SetFloat("_TransmissionTintStrength", 0.7f);
        mat.SetFloat("_Translucency", 5f);
        mat.SetFloat("_TransDirect", 0.8f);
        mat.SetFloat("_TransAmbient", 0.2f);
        mat.SetFloat("_TransScattering", 3f);
        mat.SetFloat("_TransNormal", 0.3f);
        mat.SetFloat("_TransShadow", 0.5f);

        mat.SetColor("_LeadLineColor", new Color(0.08f, 0.08f, 0.08f, 1f));

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void SetColorMode(Material mat, int mode)
    {
        mat.SetFloat("_ColorMode", mode);
        mat.DisableKeyword("_COLORMODE_SOLID");
        mat.DisableKeyword("_COLORMODE_GRADIENT");
        mat.DisableKeyword("_COLORMODE_VORONOI");
        mat.DisableKeyword("_COLORMODE_MULTIZONE");

        switch (mode)
        {
            case 0: mat.EnableKeyword("_COLORMODE_SOLID"); break;
            case 1: mat.EnableKeyword("_COLORMODE_GRADIENT"); break;
            case 2: mat.EnableKeyword("_COLORMODE_VORONOI"); break;
            case 3: mat.EnableKeyword("_COLORMODE_MULTIZONE"); break;
        }
    }

    // =========================================================================
    // Preset Definitions - all with glassy transparent look
    // =========================================================================

    // 1. Cathedral Ruby - Deep red glass with warm glow
    private static bool CreateCathedralRuby(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_CathedralRuby");
        SetColorMode(mat, 1); // Gradient

        mat.SetColor("_BaseColor", new Color(0.8f, 0.05f, 0.05f, 0.2f));
        mat.SetColor("_ColorB", new Color(0.95f, 0.25f, 0.05f, 0.18f));
        mat.SetFloat("_GlassOpacity", 0.15f);
        mat.SetFloat("_GlassTintStrength", 0.7f);

        mat.SetFloat("_GradientAngle", 30f);
        mat.SetFloat("_GradientOffset", -0.1f);
        mat.SetFloat("_GradientSharpness", 1.2f);

        mat.SetColor("_EmissionColor", new Color(1.5f, 0.4f, 0.1f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.8f);

        mat.SetColor("_FresnelColor", new Color(1f, 0.85f, 0.7f, 1f));
        mat.SetFloat("_FresnelPower", 3.5f);
        mat.SetFloat("_FresnelScale", 1.5f);
        return true;
    }

    // 2. Ocean Sapphire - Blue/teal glass with cyan edge glow
    private static bool CreateOceanSapphire(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_OceanSapphire");
        SetColorMode(mat, 1);

        mat.SetColor("_BaseColor", new Color(0.05f, 0.2f, 0.85f, 0.18f));
        mat.SetColor("_ColorB", new Color(0.05f, 0.65f, 0.75f, 0.15f));
        mat.SetFloat("_GlassOpacity", 0.12f);
        mat.SetFloat("_GlassTintStrength", 0.65f);

        mat.SetFloat("_GradientAngle", 90f);
        mat.SetFloat("_GradientSharpness", 0.8f);

        mat.SetColor("_EmissionColor", new Color(0.3f, 0.8f, 1.5f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.7f);

        mat.SetFloat("_EmissionEdgeEnabled", 1f);
        mat.EnableKeyword("_EMISSION_EDGE_ON");
        mat.SetFloat("_EmissionEdgeWidth", 0.12f);
        mat.SetFloat("_EmissionEdgePower", 2.5f);

        mat.SetColor("_FresnelColor", new Color(0.6f, 0.9f, 1f, 1f));
        mat.SetFloat("_FresnelPower", 3f);
        mat.SetFloat("_FresnelScale", 1.8f);
        mat.SetFloat("_Refraction", 0.3f);
        return true;
    }

    // 3. Forest Emerald - Green glass with golden accents
    private static bool CreateForestEmerald(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_ForestEmerald");
        SetColorMode(mat, 1);

        mat.SetColor("_BaseColor", new Color(0.05f, 0.65f, 0.15f, 0.18f));
        mat.SetColor("_ColorB", new Color(0.35f, 0.8f, 0.05f, 0.16f));
        mat.SetFloat("_GlassOpacity", 0.14f);
        mat.SetFloat("_GlassTintStrength", 0.65f);

        mat.SetFloat("_GradientAngle", 135f);
        mat.SetFloat("_GradientSharpness", 1.0f);

        mat.SetColor("_EmissionColor", new Color(0.5f, 1.2f, 0.3f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.6f);

        mat.SetColor("_FresnelColor", new Color(0.8f, 1f, 0.6f, 1f));
        mat.SetFloat("_Refraction", 0.22f);
        return true;
    }

    // 4. Sunset Amber - Warm orange glass with pulsing glow
    private static bool CreateSunsetAmber(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_SunsetAmber");
        SetColorMode(mat, 1);

        mat.SetColor("_BaseColor", new Color(0.95f, 0.55f, 0.05f, 0.2f));
        mat.SetColor("_ColorB", new Color(0.95f, 0.25f, 0.05f, 0.18f));
        mat.SetFloat("_GlassOpacity", 0.16f);
        mat.SetFloat("_GlassTintStrength", 0.7f);

        mat.SetFloat("_GradientAngle", 270f);
        mat.SetFloat("_GradientSharpness", 0.7f);

        mat.SetColor("_EmissionColor", new Color(2f, 1f, 0.3f, 1f));
        mat.SetFloat("_EmissionIntensity", 1.0f);

        mat.SetFloat("_EmissionPulseEnabled", 1f);
        mat.EnableKeyword("_EMISSION_PULSE_ON");
        mat.SetFloat("_EmissionPulseSpeed", 1.5f);
        mat.SetFloat("_EmissionPulseMin", 0.5f);

        mat.SetColor("_FresnelColor", new Color(1f, 0.9f, 0.5f, 1f));
        return true;
    }

    // 5. Royal Amethyst - Purple/violet glass
    private static bool CreateRoyalAmethyst(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_RoyalAmethyst");
        SetColorMode(mat, 1);

        mat.SetColor("_BaseColor", new Color(0.45f, 0.05f, 0.75f, 0.18f));
        mat.SetColor("_ColorB", new Color(0.75f, 0.1f, 0.55f, 0.16f));
        mat.SetFloat("_GlassOpacity", 0.13f);
        mat.SetFloat("_GlassTintStrength", 0.6f);

        mat.SetFloat("_GradientAngle", 60f);
        mat.SetFloat("_GradientSharpness", 0.9f);

        mat.SetColor("_EmissionColor", new Color(1.2f, 0.3f, 1.5f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.7f);

        mat.SetColor("_FresnelColor", new Color(0.9f, 0.6f, 1f, 1f));
        mat.SetFloat("_Refraction", 0.28f);
        mat.SetFloat("_RefractionIOR", 1.54f);
        return true;
    }

    // 6. Aurora Borealis - Multi-color voronoi mosaic glass
    private static bool CreateAuroraBorealis(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_AuroraBorealis");
        SetColorMode(mat, 2); // Voronoi

        mat.SetColor("_BaseColor", new Color(0.1f, 0.85f, 0.4f, 0.16f));
        mat.SetColor("_ColorB", new Color(0.1f, 0.4f, 0.9f, 0.16f));
        mat.SetColor("_ColorC", new Color(0.55f, 0.1f, 0.85f, 0.16f));
        mat.SetFloat("_GlassOpacity", 0.12f);
        mat.SetFloat("_GlassTintStrength", 0.55f);

        mat.SetFloat("_VoronoiScale", 6f);
        mat.SetFloat("_VoronoiEdgeWidth", 0.04f);

        mat.SetColor("_EmissionColor", new Color(0.5f, 1.2f, 1f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.8f);

        mat.SetFloat("_EmissionPulseEnabled", 1f);
        mat.EnableKeyword("_EMISSION_PULSE_ON");
        mat.SetFloat("_EmissionPulseSpeed", 0.5f);
        mat.SetFloat("_EmissionPulseMin", 0.7f);

        mat.SetFloat("_IridescenceEnabled", 1f);
        mat.EnableKeyword("_IRIDESCENCE_ON");
        mat.SetFloat("_IridescenceThickness", 2f);
        mat.SetFloat("_IridescenceIntensity", 0.25f);

        mat.SetColor("_FresnelColor", new Color(0.7f, 1f, 0.9f, 1f));
        mat.SetFloat("_FresnelScale", 2f);
        mat.SetFloat("_Refraction", 0.2f);
        return true;
    }

    // 7. Golden Cathedral - Warm gold with lead lines
    private static bool CreateGoldenCathedral(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_GoldenCathedral");
        SetColorMode(mat, 3); // MultiZone

        mat.SetColor("_BaseColor", new Color(0.9f, 0.7f, 0.1f, 0.2f));
        mat.SetColor("_ColorB", new Color(0.75f, 0.4f, 0.05f, 0.18f));
        mat.SetColor("_ColorC", new Color(0.98f, 0.88f, 0.35f, 0.16f));
        mat.SetFloat("_GlassOpacity", 0.15f);
        mat.SetFloat("_GlassTintStrength", 0.65f);

        mat.SetFloat("_MultiZoneCount", 4f);
        mat.SetFloat("_MultiZoneBlend", 0.15f);

        mat.SetColor("_EmissionColor", new Color(2f, 1.5f, 0.5f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.7f);

        mat.SetFloat("_LeadLinesEnabled", 1f);
        mat.EnableKeyword("_LEADLINES_ON");
        mat.SetFloat("_LeadLineWidth", 0.025f);
        mat.SetFloat("_LeadLineGridX", 5f);
        mat.SetFloat("_LeadLineGridY", 7f);
        mat.SetColor("_LeadLineColor", new Color(0.12f, 0.1f, 0.06f, 1f));

        mat.SetColor("_FresnelColor", new Color(1f, 0.95f, 0.7f, 1f));
        mat.SetFloat("_Metallic", 0.15f);
        return true;
    }

    // 8. Midnight Sapphire - Deep dark blue glass
    private static bool CreateMidnightSapphire(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_MidnightSapphire");
        SetColorMode(mat, 0); // Solid

        mat.SetColor("_BaseColor", new Color(0.03f, 0.05f, 0.35f, 0.22f));
        mat.SetFloat("_GlassOpacity", 0.18f);
        mat.SetFloat("_GlassTintStrength", 0.75f);
        mat.SetFloat("_Metallic", 0.12f);

        mat.SetColor("_EmissionColor", new Color(0.2f, 0.3f, 2f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.5f);

        mat.SetFloat("_EmissionEdgeEnabled", 1f);
        mat.EnableKeyword("_EMISSION_EDGE_ON");
        mat.SetFloat("_EmissionEdgeWidth", 0.15f);
        mat.SetFloat("_EmissionEdgePower", 3f);

        mat.SetColor("_FresnelColor", new Color(0.3f, 0.5f, 1f, 1f));
        mat.SetFloat("_FresnelPower", 3.5f);
        mat.SetFloat("_FresnelScale", 2f);
        mat.SetFloat("_Refraction", 0.3f);
        mat.SetFloat("_RefractionIOR", 1.55f);
        return true;
    }

    // 9. Rose Window - Pink multizone with lead lines
    private static bool CreateRoseWindow(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_RoseWindow");
        SetColorMode(mat, 3); // MultiZone

        mat.SetColor("_BaseColor", new Color(0.88f, 0.2f, 0.38f, 0.18f));
        mat.SetColor("_ColorB", new Color(0.95f, 0.55f, 0.65f, 0.15f));
        mat.SetColor("_ColorC", new Color(0.65f, 0.08f, 0.22f, 0.2f));
        mat.SetFloat("_GlassOpacity", 0.14f);
        mat.SetFloat("_GlassTintStrength", 0.6f);

        mat.SetFloat("_MultiZoneCount", 5f);
        mat.SetFloat("_MultiZoneBlend", 0.12f);

        mat.SetColor("_EmissionColor", new Color(1.5f, 0.5f, 0.7f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.65f);

        mat.SetFloat("_LeadLinesEnabled", 1f);
        mat.EnableKeyword("_LEADLINES_ON");
        mat.SetFloat("_LeadLineWidth", 0.02f);
        mat.SetFloat("_LeadLineGridX", 6f);
        mat.SetFloat("_LeadLineGridY", 6f);

        mat.SetColor("_FresnelColor", new Color(1f, 0.75f, 0.85f, 1f));
        return true;
    }

    // 10. Iridescent Prism - Clear glass with strong rainbow shimmer
    private static bool CreateIridescentPrism(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_IridescentPrism");
        SetColorMode(mat, 0); // Solid

        mat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.97f, 0.1f));
        mat.SetFloat("_GlassOpacity", 0.08f);
        mat.SetFloat("_GlassTintStrength", 0.2f);
        mat.SetFloat("_Metallic", 0.1f);
        mat.SetFloat("_Smoothness", 0.99f);

        mat.SetColor("_EmissionColor", new Color(1f, 1f, 1f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.3f);
        mat.SetFloat("_UseGlassColorEmission", 0f);

        mat.SetFloat("_IridescenceEnabled", 1f);
        mat.EnableKeyword("_IRIDESCENCE_ON");
        mat.SetFloat("_IridescenceThickness", 2.5f);
        mat.SetFloat("_IridescenceIntensity", 0.5f);

        mat.SetColor("_FresnelColor", new Color(1f, 1f, 1f, 1f));
        mat.SetFloat("_FresnelPower", 2.5f);
        mat.SetFloat("_FresnelScale", 2f);
        mat.SetFloat("_Refraction", 0.35f);
        mat.SetFloat("_RefractionIOR", 1.6f);
        return true;
    }

    // 11. Mosaic Voronoi - Classic 4-color stained glass mosaic
    private static bool CreateMosaicVoronoi(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_MosaicVoronoi");
        SetColorMode(mat, 2); // Voronoi

        mat.SetColor("_BaseColor", new Color(0.85f, 0.1f, 0.1f, 0.18f));
        mat.SetColor("_ColorB", new Color(0.1f, 0.25f, 0.85f, 0.18f));
        mat.SetColor("_ColorC", new Color(0.95f, 0.75f, 0.1f, 0.18f));
        mat.SetFloat("_GlassOpacity", 0.14f);
        mat.SetFloat("_GlassTintStrength", 0.65f);

        mat.SetFloat("_VoronoiScale", 8f);
        mat.SetFloat("_VoronoiEdgeWidth", 0.05f);

        mat.SetColor("_EmissionColor", new Color(1.2f, 1.2f, 1.2f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.6f);

        mat.SetColor("_FresnelColor", new Color(1f, 1f, 1f, 0.8f));
        mat.SetFloat("_Refraction", 0.2f);
        return true;
    }

    // 12. Fire and Ice - Red-to-blue gradient with edge glow
    private static bool CreateFireAndIce(Shader shader)
    {
        Material mat = CreateBaseMaterial(shader, "SG_FireAndIce");
        SetColorMode(mat, 1); // Gradient

        mat.SetColor("_BaseColor", new Color(0.95f, 0.15f, 0.02f, 0.18f));
        mat.SetColor("_ColorB", new Color(0.05f, 0.2f, 0.95f, 0.18f));
        mat.SetFloat("_GlassOpacity", 0.13f);
        mat.SetFloat("_GlassTintStrength", 0.6f);

        mat.SetFloat("_GradientAngle", 0f);
        mat.SetFloat("_GradientSharpness", 1.5f);

        mat.SetColor("_EmissionColor", new Color(1.5f, 0.8f, 1.5f, 1f));
        mat.SetFloat("_EmissionIntensity", 0.7f);

        mat.SetFloat("_EmissionEdgeEnabled", 1f);
        mat.EnableKeyword("_EMISSION_EDGE_ON");
        mat.SetFloat("_EmissionEdgeWidth", 0.1f);
        mat.SetFloat("_EmissionEdgePower", 2f);

        mat.SetColor("_FresnelColor", new Color(1f, 0.7f, 1f, 1f));
        mat.SetFloat("_FresnelScale", 1.8f);
        mat.SetFloat("_Refraction", 0.28f);
        return true;
    }
}
