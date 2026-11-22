using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(EdgarSequence))]
[CanEditMultipleObjects]
public class EdgarSequenceEditor : Editor
{
    SerializedProperty sequenceIdProp;
    SerializedProperty stepsProp;

    void OnEnable()
    {
        sequenceIdProp = serializedObject.FindProperty("sequenceId");
        stepsProp = serializedObject.FindProperty("steps");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sequence ID Sync", EditorStyles.boldLabel);

        // Show current asset name and sequenceId
        string assetPath = AssetDatabase.GetAssetPath(target);
        string assetName = Path.GetFileNameWithoutExtension(assetPath);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Asset Name:", GUILayout.Width(100));
        EditorGUILayout.LabelField(assetName, EditorStyles.helpBox);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Sequence ID:", GUILayout.Width(100));
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(sequenceIdProp, GUIContent.none);
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUILayout.EndHorizontal();

        // Check if they match
        bool isMatching = assetName.Equals(sequenceIdProp.stringValue, System.StringComparison.OrdinalIgnoreCase);
        if (!isMatching && !string.IsNullOrEmpty(sequenceIdProp.stringValue))
        {
            EditorGUILayout.HelpBox("Asset name and Sequence ID don't match!", MessageType.Warning);
        }
        else if (isMatching)
        {
            EditorGUILayout.HelpBox("✓ Asset name and Sequence ID match", MessageType.Info);
        }

        EditorGUILayout.Space();

        // Sync buttons
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = !string.IsNullOrEmpty(sequenceIdProp.stringValue) && !isMatching;
        if (GUILayout.Button("Set Asset Name → Sequence ID", GUILayout.Height(25)))
        {
            RenameAssetToMatchSequenceId();
        }

        GUI.enabled = !string.IsNullOrEmpty(assetName) && !isMatching;
        if (GUILayout.Button("Set Sequence ID → Asset Name", GUILayout.Height(25)))
        {
            SetSequenceIdFromAssetName();
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sequence Data", EditorStyles.boldLabel);

        // Draw remaining properties
        DrawPropertiesExcluding(serializedObject, "m_Script", "sequenceId");

        serializedObject.ApplyModifiedProperties();
    }

    void RenameAssetToMatchSequenceId()
    {
        string newName = sequenceIdProp.stringValue;
        if (string.IsNullOrEmpty(newName))
        {
            EditorUtility.DisplayDialog("Error", "Sequence ID is empty. Cannot rename asset.", "OK");
            return;
        }

        foreach (Object targetObj in targets)
        {
            string assetPath = AssetDatabase.GetAssetPath(targetObj);
            string directory = Path.GetDirectoryName(assetPath);
            string extension = Path.GetExtension(assetPath);
            string newPath = Path.Combine(directory, newName + extension);

            // Check if target name already exists
            if (File.Exists(newPath) && assetPath != newPath)
            {
                EditorUtility.DisplayDialog("Error", 
                    $"An asset with the name '{newName}' already exists at:\n{newPath}", "OK");
                continue;
            }

            string error = AssetDatabase.RenameAsset(assetPath, newName);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Error", $"Failed to rename asset:\n{error}", "OK");
            }
            else
            {
                Debug.Log($"EdgarSequenceEditor: Renamed asset to '{newName}'");
                AssetDatabase.SaveAssets();
            }
        }
    }

    void SetSequenceIdFromAssetName()
    {
        foreach (Object targetObj in targets)
        {
            string assetPath = AssetDatabase.GetAssetPath(targetObj);
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            
            if (!string.IsNullOrEmpty(assetName))
            {
                sequenceIdProp.stringValue = assetName;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(targetObj);
                Debug.Log($"EdgarSequenceEditor: Set sequenceId to '{assetName}'");
            }
        }
        
        AssetDatabase.SaveAssets();
    }
}

