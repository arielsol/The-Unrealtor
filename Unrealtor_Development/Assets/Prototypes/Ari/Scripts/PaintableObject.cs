using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PaintableObject : MonoBehaviour
{
    private Shader paintShader;
    private TextureSaver saver;
    private ImageProjector projector;
    public bool paintable = true;
    public int textureSize = 1024;

    [HideInInspector] public RenderTexture paintTexture;
    [HideInInspector] public Material paintMaterial;

    void Awake()
    {
        saver = GameObject.Find("StampManager")?.GetComponent<TextureSaver>();
        projector = GameObject.Find("StampManager")?.GetComponent<ImageProjector>();

        if (paintable)
        {
            paintShader = Shader.Find("Custom/DrawPixel");

            CreatePaintTexture();
            CreateMaterialWithTexture();
            AssignMaterialToRenderer();
            AssignMaterialToProjector();
            AddTextureToSaver();
        }
        else
        {
            paintShader = Shader.Find("Universal Render Pipeline/Unlit");
            LoadRenderedMaterial(gameObject.name + "Tex");
        }

        if (paintShader == null)
        {
            Debug.LogError("Paint shader not assigned.");
            return;
        }

    }

    void CreatePaintTexture()
    {
        paintTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
        paintTexture.name = gameObject.name + "Tex";
        paintTexture.Create();
    }

    void CreateMaterialWithTexture()
    {
        paintMaterial = new Material(paintShader);
        paintMaterial.name = gameObject.name + "Mat";
        paintMaterial.mainTexture = paintTexture;
    }

    void AssignMaterialToRenderer()
    {
        Renderer rend = GetComponent<Renderer>();
        rend.material = paintMaterial;
    }

    void AssignMaterialToProjector()
    {
        projector.drawPixelMaterials.Add(paintMaterial);
    }
    
    void AddTextureToSaver()
    {
        saver.renderTextures.Add(paintTexture);
    }

    public void LoadRenderedMaterial(string fileName)
    {
        Renderer rend = GetComponent<Renderer>();
        Shader displayShader = Shader.Find("Universal Render Pipeline/Unlit");

        Material displayMat = new Material(displayShader);
        displayMat.mainTexture = Resources.Load<Texture2D>("SavedTextures/" + fileName);
        Debug.Log(displayMat.mainTexture);
        rend.material = displayMat;

        if (displayShader == null)
        {
            Debug.LogError("URP Unlit shader not found. Make sure it's included in the build.");
            return;
        }
    }
}
