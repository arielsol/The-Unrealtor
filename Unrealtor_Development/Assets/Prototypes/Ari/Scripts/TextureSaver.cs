#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Unity.VisualScripting;

public class TextureSaver : MonoBehaviour
{
    public string folderPath = "Assets/Resources/SavedTextures/";
    public List<RenderTexture> renderTextures = new List<RenderTexture>();
  
    [ContextMenu("Save RenderTextures to PNG")]
    public void SaveRenderTextures()
    {
        if (renderTextures == null)
        {
            Debug.LogWarning("No RenderTexture assigned.");
            return;
        }

        foreach (RenderTexture rt in renderTextures)
        {
            // Set active RenderTexture so we can read from it
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = rt;

            // Create a new Texture2D and read pixels from RT
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            RenderTexture.active = currentRT;

            // Choose path to save
            string filePath = folderPath + rt.name + ".png";

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            // Save PNG
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);

#if UNITY_EDITOR
            Debug.Log($"Saved RenderTexture to {filePath}");
#endif
        }

        //AssetDatabase.Refresh();

    }
}

