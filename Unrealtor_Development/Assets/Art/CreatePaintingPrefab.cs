#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class CreatePaintingPrefab
{
    [MenuItem("Tools/Create Landscape Painting")]
    public static void Create()
    {
        // Load texture
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/landscape-hills.png");
        if (tex == null)
        {
            Debug.LogError("Cannot find Assets/Art/landscape-hills.png");
            return;
        }

        // Create unlit material (no lighting, like a painting)
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        AssetDatabase.CreateAsset(mat, "Assets/Art/landscape-hills_Mat.mat");

        // Create quad (painting surface)
        GameObject painting = GameObject.CreatePrimitive(PrimitiveType.Quad);
        painting.name = "Painting_LandscapeHills";

        // Scale to match image aspect ratio (626:417 ≈ 1.5:1)
        float aspect = (float)tex.width / tex.height;
        float height = 1.5f;
        painting.transform.localScale = new Vector3(height * aspect, height, 1f);

        // Apply material
        painting.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Remove collider (optional, paintings don't need physics)
        Object.DestroyImmediate(painting.GetComponent<Collider>());

        Selection.activeGameObject = painting;
        Debug.Log("Painting created! Move it to your desired wall position.");
    }
}
#endif
