using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor utilities for creating Pencil Edge Outline materials.
/// </summary>
public static class PencilOutlineSetup
{
    private const string ShaderName = "Custom/PencilOutline";
    private const string BasePath = "Assets/Art/Outline Shader";

    [MenuItem("Assets/Create/Pencil Outline/Pencil Sketch Material", false, 300)]
    public static void CreatePencilMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) { Debug.LogError("Shader not found: " + ShaderName); return; }

        Material mat = new Material(shader);
        mat.SetColor("_OutlineColor", new Color(0.12f, 0.1f, 0.08f, 1f));
        mat.SetFloat("_OutlineWidth", 1.5f);
        mat.SetFloat("_Softness", 0.15f);
        mat.SetFloat("_DepthSensitivity", 15f);
        mat.SetFloat("_NormalSensitivity", 3f);
        mat.SetFloat("_EnableSketch", 1f);
        mat.SetFloat("_SketchStrength", 0.35f);
        mat.SetFloat("_EnableWave", 0f);
        mat.SetFloat("_ThicknessVariation", 0.3f);
        mat.SetFloat("_OpacityVariation", 0.25f);
        mat.SetFloat("_EnableDistanceScale", 1f);
        mat.SetFloat("_ReferenceDistance", 5f);
        mat.SetFloat("_MinWidthScale", 0.3f);
        mat.SetFloat("_MaxWidthScale", 1.5f);
        mat.SetFloat("_EnableDistanceFade", 1f);
        mat.EnableKeyword("_ENABLESKETCH_ON");
        mat.EnableKeyword("_ENABLEDISTANCESCALE_ON");
        mat.EnableKeyword("_ENABLEDISTANCEFADE_ON");

        SaveMaterial(mat, "PencilOutline_Sketch");
    }

    [MenuItem("Assets/Create/Pencil Outline/Calm Wave Material", false, 301)]
    public static void CreateCalmMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return;

        Material mat = new Material(shader);
        mat.SetColor("_OutlineColor", new Color(0.15f, 0.12f, 0.1f, 1f));
        mat.SetFloat("_OutlineWidth", 1.8f);
        mat.SetFloat("_Softness", 0.2f);
        mat.SetFloat("_DepthSensitivity", 12f);
        mat.SetFloat("_NormalSensitivity", 2.5f);
        mat.SetFloat("_EnableSketch", 1f);
        mat.SetFloat("_SketchStrength", 0.2f);
        mat.SetFloat("_EnableWave", 1f);
        mat.SetFloat("_WaveStrength", 0.3f);
        mat.SetFloat("_WaveSpeed", 0.5f);
        mat.SetFloat("_EnableDistanceScale", 1f);
        mat.SetFloat("_ReferenceDistance", 5f);
        mat.SetFloat("_EnableDistanceFade", 1f);
        mat.EnableKeyword("_ENABLESKETCH_ON");
        mat.EnableKeyword("_ENABLEWAVE_ON");
        mat.EnableKeyword("_ENABLEDISTANCESCALE_ON");
        mat.EnableKeyword("_ENABLEDISTANCEFADE_ON");

        SaveMaterial(mat, "PencilOutline_CalmWave");
    }

    [MenuItem("Assets/Create/Pencil Outline/Bold Material", false, 302)]
    public static void CreateBoldMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return;

        Material mat = new Material(shader);
        mat.SetColor("_OutlineColor", new Color(0.05f, 0.03f, 0.02f, 1f));
        mat.SetFloat("_OutlineWidth", 3f);
        mat.SetFloat("_Softness", 0.1f);
        mat.SetFloat("_DepthSensitivity", 25f);
        mat.SetFloat("_NormalSensitivity", 5f);
        mat.SetFloat("_EnableSketch", 1f);
        mat.SetFloat("_SketchStrength", 0.15f);
        mat.SetFloat("_EnableWave", 0f);
        mat.SetFloat("_EnableDistanceScale", 1f);
        mat.SetFloat("_ReferenceDistance", 5f);
        mat.SetFloat("_EnableDistanceFade", 1f);
        mat.EnableKeyword("_ENABLESKETCH_ON");
        mat.EnableKeyword("_ENABLEDISTANCESCALE_ON");
        mat.EnableKeyword("_ENABLEDISTANCEFADE_ON");

        SaveMaterial(mat, "PencilOutline_Bold");
    }

    [MenuItem("Assets/Create/Pencil Outline/Clean Material", false, 303)]
    public static void CreateCleanMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return;

        Material mat = new Material(shader);
        mat.SetColor("_OutlineColor", new Color(0.1f, 0.1f, 0.1f, 1f));
        mat.SetFloat("_OutlineWidth", 1.2f);
        mat.SetFloat("_Softness", 0.05f);
        mat.SetFloat("_DepthSensitivity", 20f);
        mat.SetFloat("_NormalSensitivity", 4f);
        mat.SetFloat("_EnableSketch", 0f);
        mat.SetFloat("_EnableWave", 0f);
        mat.SetFloat("_ThicknessVariation", 0f);
        mat.SetFloat("_OpacityVariation", 0f);
        mat.SetFloat("_EnableDistanceScale", 1f);
        mat.SetFloat("_ReferenceDistance", 5f);
        mat.SetFloat("_EnableDistanceFade", 1f);
        mat.EnableKeyword("_ENABLEDISTANCESCALE_ON");
        mat.EnableKeyword("_ENABLEDISTANCEFADE_ON");

        SaveMaterial(mat, "PencilOutline_Clean");
    }

    private static void SaveMaterial(Material mat, string name)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath($"{BasePath}/{name}.mat");
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = mat;
        EditorGUIUtility.PingObject(mat);
        Debug.Log($"Created: {path}");
    }
}
