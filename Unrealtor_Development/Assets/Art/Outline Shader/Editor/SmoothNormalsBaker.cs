using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Utility to bake smooth (averaged) normals into UV1 for outline shaders.
/// This fixes the "split corner" look on hard-edged meshes like cubes.
///
/// Usage: Select mesh(es) in scene, then Tools > Outline Shader > Bake Smooth Normals
/// </summary>
public static class SmoothNormalsBaker
{
    [MenuItem("Tools/Outline Shader/Bake Smooth Normals (Selected Objects)", false, 100)]
    public static void BakeSmoothNormalsSelected()
    {
        GameObject[] selected = Selection.gameObjects;

        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection",
                "Please select one or more GameObjects with MeshFilter or SkinnedMeshRenderer components.",
                "OK");
            return;
        }

        int processed = 0;
        int skipped = 0;

        foreach (GameObject go in selected)
        {
            MeshFilter mf = go.GetComponent<MeshFilter>();
            SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();

            Mesh sourceMesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);

            if (sourceMesh == null)
            {
                skipped++;
                continue;
            }

            // Create a copy of the mesh
            Mesh newMesh = BakeSmoothNormals(sourceMesh);

            if (newMesh != null)
            {
                // Save the new mesh as an asset
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save Smooth Normals Mesh",
                    sourceMesh.name + "_SmoothNormals",
                    "asset",
                    "Save the mesh with baked smooth normals"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(newMesh, path);
                    AssetDatabase.SaveAssets();

                    // Assign the new mesh
                    if (mf != null)
                        mf.sharedMesh = newMesh;
                    else if (smr != null)
                        smr.sharedMesh = newMesh;

                    processed++;
                    Debug.Log($"Baked smooth normals for: {go.name} -> {path}");
                }
            }
        }

        EditorUtility.DisplayDialog("Bake Complete",
            $"Processed: {processed} meshes\nSkipped: {skipped} objects (no mesh found)",
            "OK");
    }

    [MenuItem("Tools/Outline Shader/Bake Smooth Normals (Create Variant)", false, 101)]
    public static void BakeSmoothNormalsCreateVariant()
    {
        // Select mesh assets directly
        Object[] selected = Selection.GetFiltered(typeof(Mesh), SelectionMode.Assets);

        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Mesh Selected",
                "Please select one or more Mesh assets in the Project window.",
                "OK");
            return;
        }

        int processed = 0;

        foreach (Object obj in selected)
        {
            Mesh sourceMesh = obj as Mesh;
            if (sourceMesh == null) continue;

            Mesh newMesh = BakeSmoothNormals(sourceMesh);

            if (newMesh != null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(sourceMesh);
                string directory = System.IO.Path.GetDirectoryName(sourcePath);
                string filename = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
                string newPath = $"{directory}/{filename}_SmoothNormals.asset";

                // Ensure unique path
                newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);

                AssetDatabase.CreateAsset(newMesh, newPath);
                processed++;

                Debug.Log($"Created smooth normals variant: {newPath}");
            }
        }

        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Bake Complete",
            $"Created {processed} mesh variants with smooth normals.",
            "OK");
    }

    /// <summary>
    /// Bakes averaged smooth normals into UV1 of a mesh.
    /// </summary>
    public static Mesh BakeSmoothNormals(Mesh sourceMesh)
    {
        if (sourceMesh == null) return null;

        // Create a copy
        Mesh newMesh = Object.Instantiate(sourceMesh);
        newMesh.name = sourceMesh.name + "_SmoothNormals";

        Vector3[] vertices = newMesh.vertices;
        Vector3[] normals = newMesh.normals;
        int vertexCount = vertices.Length;

        // Dictionary to accumulate normals per unique position
        Dictionary<Vector3, Vector3> positionToNormal = new Dictionary<Vector3, Vector3>();

        // Accumulate normals for each unique vertex position
        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 pos = vertices[i];
            Vector3 normal = normals[i];

            // Round position to avoid floating point issues
            Vector3 roundedPos = new Vector3(
                Mathf.Round(pos.x * 10000f) / 10000f,
                Mathf.Round(pos.y * 10000f) / 10000f,
                Mathf.Round(pos.z * 10000f) / 10000f
            );

            if (positionToNormal.ContainsKey(roundedPos))
            {
                positionToNormal[roundedPos] += normal;
            }
            else
            {
                positionToNormal[roundedPos] = normal;
            }
        }

        // Normalize accumulated normals
        List<Vector3> keys = new List<Vector3>(positionToNormal.Keys);
        foreach (Vector3 key in keys)
        {
            positionToNormal[key] = positionToNormal[key].normalized;
        }

        // Create smooth normals array
        Vector3[] smoothNormals = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            Vector3 pos = vertices[i];
            Vector3 roundedPos = new Vector3(
                Mathf.Round(pos.x * 10000f) / 10000f,
                Mathf.Round(pos.y * 10000f) / 10000f,
                Mathf.Round(pos.z * 10000f) / 10000f
            );

            if (positionToNormal.TryGetValue(roundedPos, out Vector3 smoothNormal))
            {
                smoothNormals[i] = smoothNormal;
            }
            else
            {
                smoothNormals[i] = normals[i];
            }
        }

        // Store smooth normals in UV1 (TEXCOORD1)
        // Convert Vector3 to Vector2 array for SetUVs (we use 3D UVs)
        newMesh.SetUVs(1, new List<Vector3>(smoothNormals));

        // Also store in tangent channel as backup
        Vector4[] tangents = new Vector4[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            tangents[i] = new Vector4(smoothNormals[i].x, smoothNormals[i].y, smoothNormals[i].z, 1f);
        }

        // Keep original tangents if they exist, otherwise use our computed ones
        if (newMesh.tangents == null || newMesh.tangents.Length == 0)
        {
            newMesh.tangents = tangents;
        }

        newMesh.UploadMeshData(false);

        return newMesh;
    }

    [MenuItem("Tools/Outline Shader/Verify Smooth Normals", false, 110)]
    public static void VerifySmoothNormals()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select a GameObject.", "OK");
            return;
        }

        MeshFilter mf = selected.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = selected.GetComponent<SkinnedMeshRenderer>();

        Mesh mesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);

        if (mesh == null)
        {
            EditorUtility.DisplayDialog("No Mesh", "Selected object has no mesh.", "OK");
            return;
        }

        List<Vector3> uv1 = new List<Vector3>();
        mesh.GetUVs(1, uv1);

        if (uv1.Count == 0)
        {
            EditorUtility.DisplayDialog("No Smooth Normals",
                $"Mesh '{mesh.name}' does not have smooth normals in UV1.\n\n" +
                "Use 'Bake Smooth Normals' to add them.",
                "OK");
        }
        else
        {
            // Check if they look like valid normals
            bool valid = true;
            foreach (var n in uv1)
            {
                if (n.magnitude < 0.9f || n.magnitude > 1.1f)
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                EditorUtility.DisplayDialog("Smooth Normals Found",
                    $"Mesh '{mesh.name}' has {uv1.Count} smooth normals baked in UV1.\n\n" +
                    "Enable 'Use Smooth Normals (UV1)' in the outline material.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("UV1 Data Invalid",
                    $"Mesh '{mesh.name}' has UV1 data but it doesn't appear to be normals.\n\n" +
                    "Use 'Bake Smooth Normals' to regenerate.",
                    "OK");
            }
        }
    }

    [MenuItem("Tools/Outline Shader/Debug Draw Smooth Normals", false, 111)]
    public static void ToggleDebugDraw()
    {
        SmoothNormalsDebugDrawer drawer = Object.FindFirstObjectByType<SmoothNormalsDebugDrawer>();

        if (drawer != null)
        {
            Object.DestroyImmediate(drawer.gameObject);
            Debug.Log("Smooth normals debug drawing disabled.");
        }
        else
        {
            GameObject debugGO = new GameObject("SmoothNormalsDebugDrawer");
            debugGO.AddComponent<SmoothNormalsDebugDrawer>();
            Debug.Log("Smooth normals debug drawing enabled. Select an object to visualize.");
        }
    }
}

/// <summary>
/// Debug drawer for visualizing smooth normals in scene view.
/// </summary>
[ExecuteAlways]
public class SmoothNormalsDebugDrawer : MonoBehaviour
{
    public float normalLength = 0.1f;
    public Color normalColor = Color.cyan;
    public Color smoothNormalColor = Color.yellow;

    private void OnDrawGizmos()
    {
        GameObject selected = UnityEditor.Selection.activeGameObject;
        if (selected == null) return;

        MeshFilter mf = selected.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Mesh mesh = mf.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        List<Vector3> smoothNormals = new List<Vector3>();
        mesh.GetUVs(1, smoothNormals);

        Transform t = selected.transform;

        // Draw regular normals
        Gizmos.color = normalColor;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = t.TransformPoint(vertices[i]);
            Vector3 worldNormal = t.TransformDirection(normals[i]);
            Gizmos.DrawLine(worldPos, worldPos + worldNormal * normalLength);
        }

        // Draw smooth normals if available
        if (smoothNormals.Count > 0)
        {
            Gizmos.color = smoothNormalColor;
            for (int i = 0; i < vertices.Length && i < smoothNormals.Count; i++)
            {
                Vector3 worldPos = t.TransformPoint(vertices[i]);
                Vector3 worldSmooth = t.TransformDirection(smoothNormals[i]);
                Gizmos.DrawLine(worldPos, worldPos + worldSmooth * normalLength * 1.2f);
            }
        }
    }
}
