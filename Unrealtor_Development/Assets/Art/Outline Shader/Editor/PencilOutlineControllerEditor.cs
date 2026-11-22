using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PencilOutlineController))]
[CanEditMultipleObjects]
public class PencilOutlineControllerEditor : Editor
{
    private PencilOutlineController controller;

    private static bool showAppearance = true;
    private static bool showEdge = true;
    private static bool showSketch = true;
    private static bool showWave = true;
    private static bool showVariation = false;
    private static bool showDistanceScale = true;
    private static bool showFade = false;
    private static bool showSetup = false;

    private void OnEnable()
    {
        controller = target as PencilOutlineController;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(5);

        // Header
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Pencil Edge Outline", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        var indexProp = serializedObject.FindProperty("outlineMaterialIndex");
        if (indexProp.intValue < 0)
        {
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
            if (GUILayout.Button("+ Add", GUILayout.Width(60)))
                AddOutline();
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                if (EditorUtility.DisplayDialog("Remove", "Remove outline?", "Yes", "No"))
                    controller.RemoveOutlineMaterial();
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        if (indexProp.intValue < 0)
        {
            EditorGUILayout.HelpBox("No outline. Click 'Add' or assign Source Material.", MessageType.Info);
            DrawSetup();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Presets
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Presets:", GUILayout.Width(55));
        if (GUILayout.Button("Pencil")) ApplyPreset("pencil");
        if (GUILayout.Button("Calm")) ApplyPreset("calm");
        if (GUILayout.Button("Bold")) ApplyPreset("bold");
        if (GUILayout.Button("Clean")) ApplyPreset("clean");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        // Sections
        showAppearance = DrawSection("Line Appearance", showAppearance, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lineColor"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lineWidth"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("softness"));
        });

        showEdge = DrawSection("Edge Detection", showEdge, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("depthSensitivity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("normalSensitivity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("depthThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("normalThreshold"));
        });

        showSketch = DrawSection("Sketch Effect", showSketch, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableSketch"), new GUIContent("Enable"));
            if (serializedObject.FindProperty("enableSketch").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sketchStrength"), new GUIContent("Wobble"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sketchScale"), new GUIContent("Scale"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sketchSpeed"), new GUIContent("Speed"));
                EditorGUI.indentLevel--;
            }
        });

        showWave = DrawSection("Wave Effect", showWave, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableWave"), new GUIContent("Enable"));
            if (serializedObject.FindProperty("enableWave").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("waveStrength"), new GUIContent("Strength"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("waveFrequency"), new GUIContent("Frequency"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("waveSpeed"), new GUIContent("Speed"));
                EditorGUI.indentLevel--;
            }
        });

        showVariation = DrawSection("Line Variation", showVariation, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("thicknessVariation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("opacityVariation"));
        });

        showDistanceScale = DrawSection("Distance Scaling", showDistanceScale, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDistanceScale"), new GUIContent("Enable"));
            if (serializedObject.FindProperty("enableDistanceScale").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("referenceDistance"), new GUIContent("Reference Dist"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("minWidthScale"), new GUIContent("Min Scale (Far)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWidthScale"), new GUIContent("Max Scale (Near)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.HelpBox("Keeps outline proportional to object's screen size.", MessageType.None);
        });

        showFade = DrawSection("Distance Fade", showFade, () =>
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDistanceFade"), new GUIContent("Enable"));
            if (serializedObject.FindProperty("enableDistanceFade").boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeStart"), new GUIContent("Start"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeEnd"), new GUIContent("End"));
                EditorGUI.indentLevel--;
            }
        });

        DrawSetup();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(5);
        if (GUILayout.Button("Apply", GUILayout.Height(25)))
        {
            foreach (var t in targets)
                (t as PencilOutlineController)?.ApplyProperties();
        }
    }

    private void DrawSetup()
    {
        showSetup = DrawSection("Setup", showSetup, () =>
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("outlineMaterialIndex"));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceMaterial"));
        });
    }

    private bool DrawSection(string title, bool expanded, System.Action content)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
        if (expanded) { EditorGUILayout.Space(2); content(); EditorGUILayout.Space(2); }
        EditorGUILayout.EndVertical();
        return expanded;
    }

    private void AddOutline()
    {
        var src = serializedObject.FindProperty("sourceMaterial");
        if (src.objectReferenceValue == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Material PencilOutline");
            foreach (string guid in guids)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat != null && mat.shader.name == "Custom/PencilOutline")
                { src.objectReferenceValue = mat; break; }
            }
        }
        serializedObject.ApplyModifiedProperties();
        if (src.objectReferenceValue != null) { controller.AddOutlineMaterial(); serializedObject.Update(); }
        else EditorUtility.DisplayDialog("No Material", "Create via Assets > Create > Pencil Outline", "OK");
    }

    private void ApplyPreset(string preset)
    {
        Undo.RecordObjects(targets, "Preset");
        foreach (var t in targets)
        {
            var c = t as PencilOutlineController;
            if (c == null) continue;

            switch (preset)
            {
                case "pencil":
                    c.lineColor = new Color(0.12f, 0.1f, 0.08f, 1f);
                    c.lineWidth = 1.5f; c.enableSketch = true; c.sketchStrength = 0.35f;
                    c.enableWave = false; c.thicknessVariation = 0.3f; c.opacityVariation = 0.25f;
                    c.enableDistanceScale = true; c.referenceDistance = 5f;
                    break;
                case "calm":
                    c.lineColor = new Color(0.15f, 0.12f, 0.1f, 1f);
                    c.lineWidth = 1.8f; c.enableSketch = true; c.sketchStrength = 0.2f;
                    c.enableWave = true; c.waveStrength = 0.3f; c.waveSpeed = 0.5f;
                    c.enableDistanceScale = true; c.referenceDistance = 5f;
                    break;
                case "bold":
                    c.lineColor = new Color(0.05f, 0.03f, 0.02f, 1f);
                    c.lineWidth = 3f; c.enableSketch = true; c.sketchStrength = 0.15f;
                    c.enableWave = false;
                    c.enableDistanceScale = true; c.referenceDistance = 5f;
                    break;
                case "clean":
                    c.lineColor = new Color(0.1f, 0.1f, 0.1f, 1f);
                    c.lineWidth = 1.2f; c.enableSketch = false; c.enableWave = false;
                    c.thicknessVariation = 0f; c.opacityVariation = 0f;
                    c.enableDistanceScale = true; c.referenceDistance = 5f;
                    break;
            }
            c.ApplyProperties();
            EditorUtility.SetDirty(c);
        }
    }
}
