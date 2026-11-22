using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

[CustomEditor(typeof(EdgarSequenceDatabase))]
public class EdgarSequenceDatabaseEditor : Editor
{
    SerializedProperty sequencesProp;

    void OnEnable()
    {
        sequencesProp = serializedObject.FindProperty("sequences");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sequence ID Sync Tools", EditorStyles.boldLabel);

        EdgarSequenceDatabase database = target as EdgarSequenceDatabase;
        if (database == null || database.sequences == null)
        {
            EditorGUILayout.HelpBox("No sequences in database.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Analyze mismatches
        List<EdgarSequence> mismatches = new List<EdgarSequence>();
        foreach (EdgarSequence seq in database.sequences)
        {
            if (seq == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(seq);
            if (string.IsNullOrEmpty(assetPath)) continue;

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            if (!assetName.Equals(seq.sequenceId, System.StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add(seq);
            }
        }

        if (mismatches.Count > 0)
        {
            EditorGUILayout.HelpBox($"Found {mismatches.Count} sequence(s) with mismatched names.", MessageType.Warning);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Mismatched Sequences:", EditorStyles.boldLabel);
            foreach (EdgarSequence seq in mismatches)
            {
                string assetPath = AssetDatabase.GetAssetPath(seq);
                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  • {assetName}", GUILayout.Width(150));
                EditorGUILayout.LabelField("→", GUILayout.Width(20));
                EditorGUILayout.LabelField(seq.sequenceId ?? "(empty)");
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox($"✓ All {database.sequences.Count} sequence(s) have matching names.", MessageType.Info);
        }

        EditorGUILayout.Space(5);

        // Bulk sync buttons
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Sync All: Asset Name → Sequence ID", GUILayout.Height(30)))
        {
            SyncAllAssetNameToSequenceId(database);
        }

        if (GUILayout.Button("Sync All: Sequence ID → Asset Name", GUILayout.Height(30)))
        {
            SyncAllSequenceIdToAssetName(database);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Validate All Sequences", GUILayout.Height(25)))
        {
            ValidateAllSequences(database);
        }

        serializedObject.ApplyModifiedProperties();
    }

    void SyncAllAssetNameToSequenceId(EdgarSequenceDatabase database)
    {
        int synced = 0;
        int skipped = 0;

        foreach (EdgarSequence seq in database.sequences)
        {
            if (seq == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(seq);
            if (string.IsNullOrEmpty(assetPath)) continue;

            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrEmpty(assetName))
            {
                skipped++;
                continue;
            }

            if (!assetName.Equals(seq.sequenceId, System.StringComparison.OrdinalIgnoreCase))
            {
                seq.sequenceId = assetName;
                EditorUtility.SetDirty(seq);
                synced++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"EdgarSequenceDatabaseEditor: Synced {synced} sequence(s). Skipped {skipped}.");
        EditorUtility.DisplayDialog("Sync Complete", 
            $"Synced {synced} sequence(s) from asset names.\nSkipped {skipped} sequence(s).", "OK");
    }

    void SyncAllSequenceIdToAssetName(EdgarSequenceDatabase database)
    {
        int synced = 0;
        int skipped = 0;
        List<string> errors = new List<string>();

        foreach (EdgarSequence seq in database.sequences)
        {
            if (seq == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(seq);
            if (string.IsNullOrEmpty(assetPath))
            {
                skipped++;
                continue;
            }

            if (string.IsNullOrEmpty(seq.sequenceId))
            {
                skipped++;
                continue;
            }

            string directory = Path.GetDirectoryName(assetPath);
            string extension = Path.GetExtension(assetPath);
            string newPath = Path.Combine(directory, seq.sequenceId + extension);

            // Check if already matches
            string currentName = Path.GetFileNameWithoutExtension(assetPath);
            if (currentName.Equals(seq.sequenceId, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if target name already exists
            if (File.Exists(newPath) && assetPath != newPath)
            {
                errors.Add($"'{currentName}' → '{seq.sequenceId}' (target exists)");
                skipped++;
                continue;
            }

            string error = AssetDatabase.RenameAsset(assetPath, seq.sequenceId);
            if (!string.IsNullOrEmpty(error))
            {
                errors.Add($"'{currentName}' → '{seq.sequenceId}' ({error})");
                skipped++;
            }
            else
            {
                synced++;
            }
        }

        AssetDatabase.SaveAssets();

        string message = $"Renamed {synced} asset(s).";
        if (skipped > 0)
        {
            message += $"\nSkipped {skipped} asset(s).";
        }
        if (errors.Count > 0)
        {
            message += "\n\nErrors:\n" + string.Join("\n", errors);
        }

        Debug.Log($"EdgarSequenceDatabaseEditor: {message}");
        EditorUtility.DisplayDialog("Sync Complete", message, "OK");
    }

    void ValidateAllSequences(EdgarSequenceDatabase database)
    {
        List<string> issues = new List<string>();
        int validCount = 0;

        foreach (EdgarSequence seq in database.sequences)
        {
            if (seq == null)
            {
                issues.Add("Null sequence found in database");
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(seq);
            if (string.IsNullOrEmpty(assetPath))
            {
                issues.Add($"Sequence '{seq.sequenceId}' has no asset path");
                continue;
            }

            string assetName = Path.GetFileNameWithoutExtension(assetPath);

            if (string.IsNullOrEmpty(seq.sequenceId))
            {
                issues.Add($"Asset '{assetName}' has empty sequenceId");
                continue;
            }

            if (!assetName.Equals(seq.sequenceId, System.StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"'{assetName}' → sequenceId: '{seq.sequenceId}' (mismatch)");
                continue;
            }

            if (seq.steps == null || seq.steps.Count == 0)
            {
                issues.Add($"'{assetName}' has no steps");
                continue;
            }

            validCount++;
        }

        string message = $"Validation Complete:\n{validCount} valid sequence(s)";
        if (issues.Count > 0)
        {
            message += $"\n{issues.Count} issue(s) found:\n\n" + string.Join("\n", issues);
        }

        Debug.Log($"EdgarSequenceDatabaseEditor: {message}");
        EditorUtility.DisplayDialog("Validation Results", message, "OK");
    }
}

