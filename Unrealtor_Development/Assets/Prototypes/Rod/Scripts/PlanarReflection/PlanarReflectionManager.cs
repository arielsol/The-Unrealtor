using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
public class PlanarReflectionManager : MonoBehaviour
{
    public static PlanarReflectionManager Instance { get; private set; }
    [SerializeField] Shader mirrorShader;
    private List<PlanarReflectionComponent> mirrors;
    [HideInInspector] public Texture2DArray rtArray;
    [HideInInspector] public List<RenderTexture> rtTextures = new List<RenderTexture>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        mirrors = FindObjectsByType<PlanarReflectionComponent>(FindObjectsSortMode.InstanceID).ToList();
        for (int i = 0; i < mirrors.Count; i++)
        {
            Material curMat = CoreUtils.CreateEngineMaterial(mirrorShader);
            curMat.SetInt("_Index", i);
            mirrors[i].gameObject.GetComponent<MeshRenderer>().material = curMat;
            mirrors[i].mirrorIndex = i;
        }

        foreach (var mirror in mirrors)
        {
            mirror.EnablePlanarReflection();
        }

        
    }

    private void OnEnable()
    {
        
    }

    private void Update()
    {
        rtTextures.Clear();
        foreach (var mirror in mirrors)
        {
            mirror.AddRT();
        }
        
        CreateTexture2DArray();
        
        Shader.SetGlobalTexture("_ReflectionTextureArray", rtArray);
    }

    public void CreateTexture2DArray()
    {
        if (rtTextures == null || rtTextures.Count == 0)
        {
            Debug.LogWarning("No textures to create array from.");
            return;
        }

        int width = rtTextures[0].width;
        int height = rtTextures[0].height;

        rtArray = new Texture2DArray(width, height, rtTextures.Count, TextureFormat.RGB24, false);

        // Create a single temporary Texture2D for reading
        Texture2D tempTex = new Texture2D(width, height, TextureFormat.RGB24, false);

        RenderTexture previousActive = RenderTexture.active;

        for (int i = 0; i < rtTextures.Count; i++)
        {
            RenderTexture currentRT = rtTextures[i];

            // Avoid setting active if already active
            if (RenderTexture.active != currentRT)
                RenderTexture.active = currentRT;

            tempTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tempTex.Apply(false); // false = don't recalculate mipmaps

            rtArray.SetPixels(tempTex.GetPixels(), i);
        }

        rtArray.Apply(false); // false = don't recalculate mipmaps
        RenderTexture.active = previousActive;

        // Clean up
        UnityEngine.Object.Destroy(tempTex);
    }
}
