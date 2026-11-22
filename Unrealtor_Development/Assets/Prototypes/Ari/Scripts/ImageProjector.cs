using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class ImageProjector : MonoBehaviour
{
    public Camera playerCamera;  // Camera doing the projection
    private CinemachineBrain brain;
    public GameObject projectionQuad;         // Quad in front of the camera   
    public List<Material> drawPixelMaterials = new List<Material>();
    public int pixelsPerStep = 1;             // Skip pixels to reduce workload (1 = full resolution)
    public int batchSize = 1000;              // Pixels to process per frame
    public TextureSaver saver;

    private Vector3 quadPos;
    private Vector3 quadRight;
    private Vector3 quadUp;
    private Vector3 origin;

    private bool isProjecting = false;
    private List<PaintableObject> hitObjects = new List<PaintableObject>();

    void Awake()
    {
        brain = playerCamera.GetComponent<CinemachineBrain>();
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isProjecting)
        {
            StartCoroutine(ProjectImageCoroutine());
        }
    }

    private IEnumerator ProjectImageCoroutine()
    {
        Debug.Log("Starting Projection");

        if (!playerCamera || !projectionQuad)
        {
            Debug.LogWarning("Missing reference(s)!");
            yield break;
        }

        Renderer quadRenderer = projectionQuad.GetComponent<Renderer>();
        if (!quadRenderer)
        {
            Debug.LogWarning("Projection object must have a Renderer.");
            yield break;
        }

        Texture2D sourceImage = quadRenderer.material.mainTexture as Texture2D;

        if (!sourceImage)
        {
            Debug.LogWarning("Projection quad material must have a readable Texture2D.");
            yield break;
        }

        int texWidth = sourceImage.width;
        int texHeight = sourceImage.height;

        quadPos = projectionQuad.transform.position;
        quadRight = projectionQuad.transform.right * projectionQuad.transform.localScale.x;
        quadUp = projectionQuad.transform.up * projectionQuad.transform.localScale.y;
        origin = quadPos - quadRight * 0.5f - quadUp * 0.5f;

        projectionQuad.SetActive(false);  // Hide quad to prevent blocking raycasts
        isProjecting = true;

        int pixelCount = 0;

        for (int y = 0; y < texHeight; y += pixelsPerStep)
        {
            for (int x = 0; x < texWidth; x += pixelsPerStep)
            {
                //Debug.Log("quadPos: " + quadPos + ", quadRight: " + quadRight + ", quadUp: " + quadUp + ", origin: " + origin);

                Color pixelColor = sourceImage.GetPixel(x, y);
                if (pixelColor.a != 1) continue;

                float u = (float)x / texWidth;
                float v = (float)y / texHeight;

                Vector3 pixelWorldPos = origin + quadRight * u + quadUp * v;
                Vector3 screenPoint = playerCamera.WorldToScreenPoint(pixelWorldPos);
                Ray ray = playerCamera.ScreenPointToRay(screenPoint);

                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    Renderer renderer = hit.collider.GetComponent<Renderer>();
                    PaintableObject paintTarget = hit.collider.GetComponent<PaintableObject>();

                    if (paintTarget != null && renderer != null && paintTarget.paintable)
                    {
                        if (!hitObjects.Contains(paintTarget))
                            { hitObjects.Add(paintTarget); }
                        
                        PaintAtUV(paintTarget.paintTexture, renderer.material, hit.textureCoord, pixelColor, 2f);
                    }
                }

                pixelCount++;
                if (pixelCount >= batchSize)

                {
                    pixelCount = 0;
                    yield return null;  // Wait one frame
                }
            }
        }

        isProjecting = false;
        Debug.Log("Projection complete");

        saver.SaveRenderTextures();
        Render();
    }

    private void Render()
    {
        foreach (PaintableObject obj in hitObjects)
        {
            obj.paintable = false;
            obj.LoadRenderedMaterial(obj.name + "Tex");
            Debug.Log(obj.name + "Tex");

            // obj.ApplyShader();
        }
    }
    public void PaintAtUV(RenderTexture renderTex, Material drawMaterial, Vector2 uv, Color color, float size = 1f)
    {
        RenderTexture tempTex = RenderTexture.GetTemporary(renderTex.width, renderTex.height, 0, renderTex.format);
        Graphics.Blit(renderTex, tempTex); // Copy current state

        // Save current render target so we can restore it later
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;

        GL.PushMatrix();
        GL.LoadOrtho(); // Set matrix to 0–1 range

        // Set color on the draw material
        //drawPixelMaterial.SetTexture("_MainTex", renderTex);
        drawMaterial.SetTexture("_MainTex", tempTex);
        drawMaterial.SetColor("_Color", color);
        drawMaterial.SetVector("_TargetUV", uv);
        drawMaterial.SetFloat("_Radius", 0.005f); // You can tweak this
        drawMaterial.SetPass(0);

        float pixelSizeU = size / renderTex.width;
        float pixelSizeV = size / renderTex.height;

        float u = Mathf.Clamp01(uv.x);
        float v = Mathf.Clamp01(uv.y);

        float left = u - pixelSizeU * 0.5f;
        float right = u + pixelSizeU * 0.5f;
        float bottom = v - pixelSizeV * 0.5f;
        float top = v + pixelSizeV * 0.5f;

        GL.Begin(GL.QUADS);

        GL.TexCoord2(0, 0); GL.Vertex3(left, bottom, 0);
        GL.TexCoord2(1, 0); GL.Vertex3(right, bottom, 0);
        GL.TexCoord2(1, 1); GL.Vertex3(right, top, 0);
        GL.TexCoord2(0, 1); GL.Vertex3(left, top, 0);

        // Debug.Log($"PaintAtUV @ UV({u:F3}, {v:F3}) => Quad: ({left:F3}, {bottom:F3}) to ({right:F3}, {top:F3})");

        GL.End();
        GL.PopMatrix();

        // Restore previous render target
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tempTex);
    }
}