using UnityEngine;

public class SimpleMirrorSetup : MonoBehaviour
{   //hi diego
    public Camera mirrorCamera;
    public Renderer mirrorSurface;
    public int textureWidth = 512;
    public int textureHeight = 512;

    private RenderTexture runtimeRT;
    private Material runtimeMaterial;

    void Start()
    {
        if (!mirrorCamera || !mirrorSurface) return;
        runtimeRT = new RenderTexture(textureWidth, textureHeight, 16);
        runtimeRT.name = "SimpleMirrorRT_" + gameObject.name;

        mirrorCamera.targetTexture = runtimeRT;

        runtimeMaterial = new Material(mirrorSurface.sharedMaterial);
        runtimeMaterial.mainTexture = runtimeRT;
        mirrorSurface.material = runtimeMaterial;
    }

    public void SetActive(bool active)
    {
        if (mirrorCamera != null) mirrorCamera.enabled = active;
        if (mirrorSurface != null) mirrorSurface.enabled = active;
    }

    public void ClearAndDisable()
    {
        SetActive(false);

        // Detach and destroy
        if (mirrorCamera != null)
        {
            mirrorCamera.targetTexture = null;
        }
        if (runtimeRT != null)
        {
            runtimeRT.Release();
            Destroy(runtimeRT);
            runtimeRT = null;
        }

        // Restore original shared material to clear UI/surface
        if (mirrorSurface != null)
        {
            mirrorSurface.material = mirrorSurface.sharedMaterial;
        }
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }
    }

    void OnDisable()
    {
        ClearAndDisable();
    }
}
