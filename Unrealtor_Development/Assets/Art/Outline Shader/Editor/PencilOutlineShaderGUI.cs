using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom shader GUI for the Pencil Edge Outline shader.
/// </summary>
public class PencilOutlineShaderGUI : ShaderGUI
{
    private static bool showAppearance = true;
    private static bool showEdgeDetection = true;
    private static bool showSketch = true;
    private static bool showWave = true;
    private static bool showVariation = false;
    private static bool showDistanceScale = true;
    private static bool showDistanceFade = false;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material material = materialEditor.target as Material;

        EditorGUILayout.Space(5);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 14;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("Pencil Edge Outline", titleStyle);
        EditorGUILayout.Space(5);

        // Preset bar
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Presets:", GUILayout.Width(55));
        if (GUILayout.Button("Pencil")) ApplyPreset(material, properties, "pencil");
        if (GUILayout.Button("Calm")) ApplyPreset(material, properties, "calm");
        if (GUILayout.Button("Bold")) ApplyPreset(material, properties, "bold");
        if (GUILayout.Button("Clean")) ApplyPreset(material, properties, "clean");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Line Appearance
        showAppearance = DrawSection("Line Appearance", showAppearance, () =>
        {
            materialEditor.ShaderProperty(FindProperty("_OutlineColor", properties), "Line Color");
            materialEditor.ShaderProperty(FindProperty("_OutlineWidth", properties), "Line Width");
            materialEditor.ShaderProperty(FindProperty("_Softness", properties), "Softness");
        });

        // Edge Detection
        showEdgeDetection = DrawSection("Edge Detection", showEdgeDetection, () =>
        {
            materialEditor.ShaderProperty(FindProperty("_DepthSensitivity", properties),
                new GUIContent("Depth Sensitivity", "How strongly depth differences create edges"));
            materialEditor.ShaderProperty(FindProperty("_NormalSensitivity", properties),
                new GUIContent("Normal Sensitivity", "How strongly normal differences create edges"));
            materialEditor.ShaderProperty(FindProperty("_DepthThreshold", properties),
                new GUIContent("Depth Threshold", "Minimum depth difference for edge"));
            materialEditor.ShaderProperty(FindProperty("_NormalThreshold", properties),
                new GUIContent("Normal Threshold", "Minimum normal difference for edge"));

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUIUtility.labelWidth);
            if (GUILayout.Button("Subtle", EditorStyles.miniButtonLeft))
            {
                FindProperty("_DepthSensitivity", properties).floatValue = 8f;
                FindProperty("_NormalSensitivity", properties).floatValue = 2f;
            }
            if (GUILayout.Button("Normal", EditorStyles.miniButtonMid))
            {
                FindProperty("_DepthSensitivity", properties).floatValue = 15f;
                FindProperty("_NormalSensitivity", properties).floatValue = 3f;
            }
            if (GUILayout.Button("Strong", EditorStyles.miniButtonRight))
            {
                FindProperty("_DepthSensitivity", properties).floatValue = 30f;
                FindProperty("_NormalSensitivity", properties).floatValue = 5f;
            }
            EditorGUILayout.EndHorizontal();
        });

        // Sketch Effect
        showSketch = DrawSection("Sketch Effect", showSketch, () =>
        {
            var enableProp = FindProperty("_EnableSketch", properties);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("Enable Sketch", enableProp.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                enableProp.floatValue = enabled ? 1f : 0f;
                SetKeyword(material, "_ENABLESKETCH_ON", enabled);
            }

            if (enabled)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_SketchStrength", properties), "Wobble Amount");
                materialEditor.ShaderProperty(FindProperty("_SketchScale", properties), "Detail Scale");
                materialEditor.ShaderProperty(FindProperty("_SketchSpeed", properties), "Animation Speed");
                EditorGUI.indentLevel--;
            }
        });

        // Wave Effect
        showWave = DrawSection("Wave Effect", showWave, () =>
        {
            var enableProp = FindProperty("_EnableWave", properties);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("Enable Wave", enableProp.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                enableProp.floatValue = enabled ? 1f : 0f;
                SetKeyword(material, "_ENABLEWAVE_ON", enabled);
            }

            if (enabled)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_WaveStrength", properties), "Wave Strength");
                materialEditor.ShaderProperty(FindProperty("_WaveFrequency", properties), "Wave Frequency");
                materialEditor.ShaderProperty(FindProperty("_WaveSpeed", properties), "Wave Speed");
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(3);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(EditorGUIUtility.labelWidth);
                if (GUILayout.Button("Gentle", EditorStyles.miniButtonLeft))
                {
                    FindProperty("_WaveStrength", properties).floatValue = 0.15f;
                    FindProperty("_WaveSpeed", properties).floatValue = 0.3f;
                }
                if (GUILayout.Button("Calm", EditorStyles.miniButtonMid))
                {
                    FindProperty("_WaveStrength", properties).floatValue = 0.3f;
                    FindProperty("_WaveSpeed", properties).floatValue = 0.6f;
                }
                if (GUILayout.Button("Flowing", EditorStyles.miniButtonRight))
                {
                    FindProperty("_WaveStrength", properties).floatValue = 0.5f;
                    FindProperty("_WaveSpeed", properties).floatValue = 1.0f;
                }
                EditorGUILayout.EndHorizontal();
            }
        });

        // Line Variation
        showVariation = DrawSection("Line Variation", showVariation, () =>
        {
            materialEditor.ShaderProperty(FindProperty("_ThicknessVariation", properties), "Thickness Variation");
            materialEditor.ShaderProperty(FindProperty("_OpacityVariation", properties), "Opacity Variation");
        });

        // Distance Scaling
        showDistanceScale = DrawSection("Distance Scaling (Recommended)", showDistanceScale, () =>
        {
            var enableProp = FindProperty("_EnableDistanceScale", properties);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(
                new GUIContent("Scale Width With Distance", "Prevents outline from overwhelming object at distance"),
                enableProp.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                enableProp.floatValue = enabled ? 1f : 0f;
                SetKeyword(material, "_ENABLEDISTANCESCALE_ON", enabled);
            }

            if (enabled)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_ReferenceDistance", properties),
                    new GUIContent("Reference Distance", "Distance where line width = 100%"));
                materialEditor.ShaderProperty(FindProperty("_MinWidthScale", properties),
                    new GUIContent("Min Scale (Far)", "Minimum width multiplier when far away"));
                materialEditor.ShaderProperty(FindProperty("_MaxWidthScale", properties),
                    new GUIContent("Max Scale (Near)", "Maximum width multiplier when close"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox(
                "This scales the line width based on camera distance, keeping the outline proportional to the object's screen size.",
                MessageType.None);
        });

        // Distance Fade
        showDistanceFade = DrawSection("Distance Fade", showDistanceFade, () =>
        {
            var enableProp = FindProperty("_EnableDistanceFade", properties);

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(
                new GUIContent("Fade Out At Distance", "Completely fade outline at far distances"),
                enableProp.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                enableProp.floatValue = enabled ? 1f : 0f;
                SetKeyword(material, "_ENABLEDISTANCEFADE_ON", enabled);
            }

            if (enabled)
            {
                EditorGUI.indentLevel++;
                materialEditor.ShaderProperty(FindProperty("_FadeStart", properties), "Start Distance");
                materialEditor.ShaderProperty(FindProperty("_FadeEnd", properties), "End Distance");
                EditorGUI.indentLevel--;
            }
        });

        EditorGUILayout.Space(10);
        materialEditor.RenderQueueField();
    }

    private void ApplyPreset(Material material, MaterialProperty[] properties, string preset)
    {
        switch (preset)
        {
            case "pencil":
                FindProperty("_OutlineColor", properties).colorValue = new Color(0.12f, 0.1f, 0.08f, 1f);
                FindProperty("_OutlineWidth", properties).floatValue = 1.5f;
                FindProperty("_Softness", properties).floatValue = 0.15f;
                FindProperty("_DepthSensitivity", properties).floatValue = 15f;
                FindProperty("_NormalSensitivity", properties).floatValue = 3f;
                FindProperty("_EnableSketch", properties).floatValue = 1f;
                FindProperty("_SketchStrength", properties).floatValue = 0.35f;
                FindProperty("_EnableWave", properties).floatValue = 0f;
                FindProperty("_ThicknessVariation", properties).floatValue = 0.3f;
                FindProperty("_OpacityVariation", properties).floatValue = 0.25f;
                FindProperty("_EnableDistanceScale", properties).floatValue = 1f;
                FindProperty("_ReferenceDistance", properties).floatValue = 5f;
                SetKeyword(material, "_ENABLESKETCH_ON", true);
                SetKeyword(material, "_ENABLEWAVE_ON", false);
                SetKeyword(material, "_ENABLEDISTANCESCALE_ON", true);
                break;

            case "calm":
                FindProperty("_OutlineColor", properties).colorValue = new Color(0.15f, 0.12f, 0.1f, 1f);
                FindProperty("_OutlineWidth", properties).floatValue = 1.8f;
                FindProperty("_Softness", properties).floatValue = 0.2f;
                FindProperty("_DepthSensitivity", properties).floatValue = 12f;
                FindProperty("_NormalSensitivity", properties).floatValue = 2.5f;
                FindProperty("_EnableSketch", properties).floatValue = 1f;
                FindProperty("_SketchStrength", properties).floatValue = 0.2f;
                FindProperty("_EnableWave", properties).floatValue = 1f;
                FindProperty("_WaveStrength", properties).floatValue = 0.3f;
                FindProperty("_WaveSpeed", properties).floatValue = 0.5f;
                FindProperty("_EnableDistanceScale", properties).floatValue = 1f;
                FindProperty("_ReferenceDistance", properties).floatValue = 5f;
                SetKeyword(material, "_ENABLESKETCH_ON", true);
                SetKeyword(material, "_ENABLEWAVE_ON", true);
                SetKeyword(material, "_ENABLEDISTANCESCALE_ON", true);
                break;

            case "bold":
                FindProperty("_OutlineColor", properties).colorValue = new Color(0.05f, 0.03f, 0.02f, 1f);
                FindProperty("_OutlineWidth", properties).floatValue = 3f;
                FindProperty("_Softness", properties).floatValue = 0.1f;
                FindProperty("_DepthSensitivity", properties).floatValue = 25f;
                FindProperty("_NormalSensitivity", properties).floatValue = 5f;
                FindProperty("_EnableSketch", properties).floatValue = 1f;
                FindProperty("_SketchStrength", properties).floatValue = 0.15f;
                FindProperty("_EnableWave", properties).floatValue = 0f;
                FindProperty("_EnableDistanceScale", properties).floatValue = 1f;
                FindProperty("_ReferenceDistance", properties).floatValue = 5f;
                SetKeyword(material, "_ENABLESKETCH_ON", true);
                SetKeyword(material, "_ENABLEWAVE_ON", false);
                SetKeyword(material, "_ENABLEDISTANCESCALE_ON", true);
                break;

            case "clean":
                FindProperty("_OutlineColor", properties).colorValue = new Color(0.1f, 0.1f, 0.1f, 1f);
                FindProperty("_OutlineWidth", properties).floatValue = 1.2f;
                FindProperty("_Softness", properties).floatValue = 0.05f;
                FindProperty("_DepthSensitivity", properties).floatValue = 20f;
                FindProperty("_NormalSensitivity", properties).floatValue = 4f;
                FindProperty("_EnableSketch", properties).floatValue = 0f;
                FindProperty("_EnableWave", properties).floatValue = 0f;
                FindProperty("_ThicknessVariation", properties).floatValue = 0f;
                FindProperty("_OpacityVariation", properties).floatValue = 0f;
                FindProperty("_EnableDistanceScale", properties).floatValue = 1f;
                FindProperty("_ReferenceDistance", properties).floatValue = 5f;
                SetKeyword(material, "_ENABLESKETCH_ON", false);
                SetKeyword(material, "_ENABLEWAVE_ON", false);
                SetKeyword(material, "_ENABLEDISTANCESCALE_ON", true);
                break;
        }
    }

    private bool DrawSection(string title, bool expanded, System.Action content)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
        if (expanded)
        {
            EditorGUILayout.Space(2);
            EditorGUI.indentLevel++;
            content();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndVertical();
        return expanded;
    }

    private void SetKeyword(Material material, string keyword, bool state)
    {
        if (state)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);
    }
}
