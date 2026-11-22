using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class TextureOptimizer : EditorWindow
{
    private long minFileSizeBytes = 10 * 1024 * 1024; // 10MB default
    private int minFileSizeMB = 10;
    private int maxTextureSize = 2048;
    private bool enableCrunchedCompression = true;
    private int crunchedQuality = 75;
    private bool enableMipmapStreaming = true;
    private bool dryRun = true;
    private Vector2 scrollPos;
    private List<string> results = new List<string>();
    private bool isProcessing = false;

    [MenuItem("Tools/Texture Optimizer")]
    public static void ShowWindow()
    {
        GetWindow<TextureOptimizer>("Texture Optimizer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Texture Optimization Settings", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        minFileSizeMB = EditorGUILayout.IntSlider("Min File Size (MB)", minFileSizeMB, 1, 50);
        minFileSizeBytes = (long)minFileSizeMB * 1024 * 1024;

        maxTextureSize = EditorGUILayout.IntPopup("Max Texture Size",
            maxTextureSize,
            new string[] { "512", "1024", "2048", "4096" },
            new int[] { 512, 1024, 2048, 4096 });

        enableCrunchedCompression = EditorGUILayout.Toggle("Enable Crunched Compression", enableCrunchedCompression);
        if (enableCrunchedCompression)
        {
            crunchedQuality = EditorGUILayout.IntSlider("Crunched Quality", crunchedQuality, 0, 100);
        }
        enableMipmapStreaming = EditorGUILayout.Toggle("Enable Mipmap Streaming", enableMipmapStreaming);

        EditorGUILayout.Space();
        dryRun = EditorGUILayout.Toggle("Dry Run (preview only)", dryRun);

        EditorGUILayout.Space();

        GUI.enabled = !isProcessing;
        if (GUILayout.Button(dryRun ? "Preview Changes" : "Apply Changes", GUILayout.Height(30)))
        {
            ProcessTextures();
        }
        GUI.enabled = true;

        if (results.Count > 0)
        {
            EditorGUILayout.Space();
            GUILayout.Label($"Results: {results.Count} textures", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(400));
            foreach (var r in results)
            {
                EditorGUILayout.LabelField(r, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void ProcessTextures()
    {
        results.Clear();
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int changed = 0;
        int skipped = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (EditorUtility.DisplayCancelableProgressBar("Processing Textures",
                $"{i + 1}/{guids.Length}: {Path.GetFileName(path)}", (float)i / guids.Length))
            {
                break;
            }

            // Check file size
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                continue;

            long fileSize = new FileInfo(fullPath).Length;
            if (fileSize < minFileSizeBytes)
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            // Skip lightmaps, sprites, cookies
            if (importer.textureType == TextureImporterType.Lightmap ||
                importer.textureType == TextureImporterType.Sprite ||
                importer.textureType == TextureImporterType.Cookie)
            {
                skipped++;
                continue;
            }

            bool needsChange = false;
            string changes = $"{path} ({fileSize / (1024 * 1024)}MB): ";
            List<string> changeList = new List<string>();

            // Check max texture size
            if (importer.maxTextureSize > maxTextureSize)
            {
                changeList.Add($"size {importer.maxTextureSize}->{maxTextureSize}");
                needsChange = true;
            }

            // Check crunched compression
            if (enableCrunchedCompression && !importer.crunchedCompression)
            {
                changeList.Add("crunched ON");
                needsChange = true;
            }

            // Check mipmap streaming
            if (enableMipmapStreaming && !importer.streamingMipmaps)
            {
                changeList.Add("mipmap streaming ON");
                needsChange = true;
            }

            if (!needsChange)
            {
                skipped++;
                continue;
            }

            changes += string.Join(", ", changeList);
            results.Add(changes);
            changed++;

            if (!dryRun)
            {
                if (importer.maxTextureSize > maxTextureSize)
                    importer.maxTextureSize = maxTextureSize;

                if (enableCrunchedCompression)
                {
                    importer.crunchedCompression = true;
                    importer.compressionQuality = crunchedQuality;
                }

                if (enableMipmapStreaming)
                    importer.streamingMipmaps = true;

                importer.SaveAndReimport();
            }
        }

        EditorUtility.ClearProgressBar();
        results.Insert(0, $"--- Total: {changed} to change, {skipped} skipped ---");

        if (!dryRun)
            AssetDatabase.Refresh();

        Debug.Log($"[TextureOptimizer] Done. {changed} textures {(dryRun ? "would be" : "were")} modified, {skipped} skipped.");
    }
}
