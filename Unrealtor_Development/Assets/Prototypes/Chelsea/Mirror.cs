using UnityEngine;

public class SimpleMirrorSetupLegacy : MonoBehaviour
{
    public Camera mirrorCamera;
    public Renderer mirrorSurface;
    public int textureWidth = 512;
    public int textureHeight = 512;

    void Start()
    {
        if (!mirrorCamera || !mirrorSurface) return;
        RenderTexture renderTex = new RenderTexture(textureWidth, textureHeight, 16);
        renderTex.name = "SimpleMirrorRT_" + gameObject.name;

        mirrorCamera.targetTexture = renderTex;

        Material mat = new Material(mirrorSurface.sharedMaterial);
        mat.mainTexture = renderTex;
        mirrorSurface.material = mat;
    }

    public void SetActive(bool active)
    {
        if (mirrorCamera != null) mirrorCamera.enabled = active;
        if (mirrorSurface != null) mirrorSurface.enabled = active;
    }
}
