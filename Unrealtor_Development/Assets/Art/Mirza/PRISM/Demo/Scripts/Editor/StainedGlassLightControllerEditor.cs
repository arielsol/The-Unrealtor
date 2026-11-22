using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(StainedGlassLightController))]
[CanEditMultipleObjects]
public class StainedGlassLightControllerEditor : Editor
{
    private SerializedProperty selectionMode;
    private SerializedProperty maxLightCount;
    private SerializedProperty renderingLayerMask;
    private SerializedProperty distanceWeight;
    private SerializedProperty intensityWeight;
    private SerializedProperty maxLightDistance;
    private SerializedProperty updateInterval;
    private SerializedProperty assignedLights;
    private SerializedProperty showDebugInfo;
    private SerializedProperty drawGizmos;

    private bool showURPHelp = false;
    private static GUIStyle helpBoxStyle;

    void OnEnable()
    {
        selectionMode = serializedObject.FindProperty("selectionMode");
        maxLightCount = serializedObject.FindProperty("maxLightCount");
        renderingLayerMask = serializedObject.FindProperty("renderingLayerMask");
        distanceWeight = serializedObject.FindProperty("distanceWeight");
        intensityWeight = serializedObject.FindProperty("intensityWeight");
        maxLightDistance = serializedObject.FindProperty("maxLightDistance");
        updateInterval = serializedObject.FindProperty("updateInterval");
        assignedLights = serializedObject.FindProperty("assignedLights");
        showDebugInfo = serializedObject.FindProperty("showDebugInfo");
        drawGizmos = serializedObject.FindProperty("drawGizmos");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var controller = (StainedGlassLightController)target;

        // Header
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stained Glass Light Controller", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Controls which lights affect this stained glass object. " +
            "Use this to override URP's per-object light limit.",
            MessageType.Info);
        EditorGUILayout.Space();

        // Light Selection Mode
        EditorGUILayout.PropertyField(selectionMode);

        // Max Light Count with visual slider
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Light Count", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Max Lights");

        // Quick preset buttons
        if (GUILayout.Button("1", GUILayout.Width(30))) maxLightCount.intValue = 1;
        if (GUILayout.Button("2", GUILayout.Width(30))) maxLightCount.intValue = 2;
        if (GUILayout.Button("4", GUILayout.Width(30))) maxLightCount.intValue = 4;
        if (GUILayout.Button("8", GUILayout.Width(30))) maxLightCount.intValue = 8;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(maxLightCount, new GUIContent("Custom Value"));

        // Mode-specific options
        EditorGUILayout.Space();

        var mode = (StainedGlassLightController.LightSelectionMode)selectionMode.enumValueIndex;

        switch (mode)
        {
            case StainedGlassLightController.LightSelectionMode.LightLayers:
                DrawLightLayersMode();
                break;

            case StainedGlassLightController.LightSelectionMode.DynamicPriority:
                DrawDynamicPriorityMode();
                break;

            case StainedGlassLightController.LightSelectionMode.ManualAssignment:
                DrawManualAssignmentMode();
                break;
        }

        // Debug options
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showDebugInfo);
        EditorGUILayout.PropertyField(drawGizmos);

        // Active lights display
        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active Lights", EditorStyles.boldLabel);
            var activeLights = controller.GetActiveLights();
            if (activeLights.Count == 0)
            {
                EditorGUILayout.HelpBox("No lights currently active.", MessageType.Warning);
            }
            else
            {
                foreach (var light in activeLights)
                {
                    EditorGUILayout.ObjectField(light, typeof(Light), true);
                }
            }
        }

        // URP Help Section
        EditorGUILayout.Space();
        showURPHelp = EditorGUILayout.Foldout(showURPHelp, "URP Configuration Help");
        if (showURPHelp)
        {
            DrawURPHelp();
        }

        // Utility buttons
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Light Cache"))
        {
            StainedGlassLightController.RefreshLightsCache();
            controller.UpdateLightConfiguration();
        }
        if (GUILayout.Button("Force Update"))
        {
            controller.UpdateLightConfiguration();
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLightLayersMode()
    {
        EditorGUILayout.LabelField("Light Layers Configuration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Light Layers mode uses URP's built-in light layer system.\n\n" +
            "1. Enable 'Light Layers' in your URP Asset\n" +
            "2. Assign this object to a rendering layer\n" +
            "3. Set lights to the matching layer",
            MessageType.Info);

        // Rendering layer mask as a proper mask field
        EditorGUILayout.PropertyField(renderingLayerMask, new GUIContent("Rendering Layer Mask"));

        // Helper buttons for common layers
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Quick Set Layer");
        if (GUILayout.Button("Default (1)")) renderingLayerMask.longValue = 1;
        if (GUILayout.Button("Layer 2")) renderingLayerMask.longValue = 2;
        if (GUILayout.Button("Layer 3")) renderingLayerMask.longValue = 4;
        if (GUILayout.Button("All")) renderingLayerMask.longValue = uint.MaxValue;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDynamicPriorityMode()
    {
        EditorGUILayout.LabelField("Dynamic Priority Configuration", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Dynamic mode automatically selects the most important lights " +
            "based on distance and intensity each frame.",
            MessageType.Info);

        EditorGUILayout.PropertyField(distanceWeight, new GUIContent("Distance Importance"));
        EditorGUILayout.PropertyField(intensityWeight, new GUIContent("Intensity Importance"));
        EditorGUILayout.PropertyField(maxLightDistance, new GUIContent("Max Light Distance"));
        EditorGUILayout.PropertyField(updateInterval, new GUIContent("Update Interval (seconds)"));

        if (updateInterval.floatValue == 0)
        {
            EditorGUILayout.HelpBox("Updates every frame. Consider using an interval for better performance.", MessageType.Warning);
        }
    }

    private void DrawManualAssignmentMode()
    {
        EditorGUILayout.LabelField("Manual Light Assignment", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Manually specify which lights affect this object. " +
            "Good for precise artistic control.",
            MessageType.Info);

        EditorGUILayout.PropertyField(assignedLights, true);
        EditorGUILayout.PropertyField(renderingLayerMask, new GUIContent("Rendering Layer Mask"));

        // Quick add from scene
        if (GUILayout.Button("Add All Scene Lights"))
        {
            var lights = FindObjectsOfType<Light>();
            assignedLights.ClearArray();
            foreach (var light in lights)
            {
                assignedLights.InsertArrayElementAtIndex(assignedLights.arraySize);
                assignedLights.GetArrayElementAtIndex(assignedLights.arraySize - 1).objectReferenceValue = light;
            }
        }

        if (GUILayout.Button("Add Nearby Lights (10m)"))
        {
            var controller = (StainedGlassLightController)target;
            var lights = FindObjectsOfType<Light>()
                .Where(l => Vector3.Distance(l.transform.position, controller.transform.position) < 10f)
                .ToArray();

            assignedLights.ClearArray();
            foreach (var light in lights)
            {
                assignedLights.InsertArrayElementAtIndex(assignedLights.arraySize);
                assignedLights.GetArrayElementAtIndex(assignedLights.arraySize - 1).objectReferenceValue = light;
            }
        }
    }

    private void DrawURPHelp()
    {
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Why 2 Light Limit?", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "URP's 'Additional Lights Per Object Limit' setting controls how many " +
            "point/spot lights affect each object. Mobile presets default to 2.",
            EditorStyles.wordWrappedLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Fix Options:", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("1. Increase URP Per-Object Limit:", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("   Edit > Project Settings > Quality > Rendering", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("   Select your URP Asset > Additional Lights > Per Object Limit", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("2. Enable Forward+ (Recommended):", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("   In URP Renderer Asset, set Rendering Path to 'Forward+'", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("   This removes the per-object light limit entirely.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("3. Enable Light Layers:", EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("   In URP Asset, check 'Light Layers'", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField("   Then use this controller's Light Layers mode.", EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space();
        if (GUILayout.Button("Open URP Asset Settings"))
        {
            var urpAssets = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            if (urpAssets.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(urpAssets[0]);
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        EditorGUILayout.EndVertical();
    }
}

/// <summary>
/// Menu items for bulk operations on stained glass objects
/// </summary>
public static class StainedGlassMenuItems
{
    [MenuItem("Tools/Stained Glass/Add Controller to Selected")]
    static void AddControllerToSelected()
    {
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponent<StainedGlassLightController>() == null)
            {
                Undo.AddComponent<StainedGlassLightController>(go);
            }
        }
    }

    [MenuItem("Tools/Stained Glass/Set All to 4 Lights")]
    static void SetAllTo4Lights()
    {
        var controllers = Object.FindObjectsOfType<StainedGlassLightController>();
        foreach (var controller in controllers)
        {
            Undo.RecordObject(controller, "Set Max Lights");
            controller.maxLightCount = 4;
        }
        Debug.Log($"Set {controllers.Length} stained glass objects to 4 lights.");
    }

    [MenuItem("Tools/Stained Glass/Set All to 8 Lights")]
    static void SetAllTo8Lights()
    {
        var controllers = Object.FindObjectsOfType<StainedGlassLightController>();
        foreach (var controller in controllers)
        {
            Undo.RecordObject(controller, "Set Max Lights");
            controller.maxLightCount = 8;
        }
        Debug.Log($"Set {controllers.Length} stained glass objects to 8 lights.");
    }

    [MenuItem("Tools/Stained Glass/Open URP Quality Settings")]
    static void OpenURPSettings()
    {
        SettingsService.OpenProjectSettings("Project/Quality");
    }
}
