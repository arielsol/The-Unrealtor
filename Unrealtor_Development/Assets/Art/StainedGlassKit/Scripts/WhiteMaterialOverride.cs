using UnityEngine;

/// <summary>
/// Uses MaterialPropertyBlock to override all child Renderers to white.
/// Never replaces material references — zero risk of material loss.
/// Toggle the component on/off to switch. Works in Edit mode.
/// </summary>
[ExecuteAlways]
public class WhiteMaterialOverride : MonoBehaviour
{
    [Tooltip("Override color applied to all materials (default white)")]
    public Color overrideColor = Color.white;

    [Tooltip("If true, also clears the base texture so the surface is pure flat white")]
    public bool clearBaseMap = true;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionMapID = Shader.PropertyToID("_EmissionMap");

    private Renderer[] cachedRenderers;
    private bool isApplied = false;

    void OnEnable()
    {
        Apply();
    }

    void OnDisable()
    {
        Revert();
    }

    void OnDestroy()
    {
        Revert();
    }

    void OnValidate()
    {
        if (enabled)
        {
            Revert();
            Apply();
        }
    }

    void Apply()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        var whiteTexture = Texture2D.whiteTexture;

        foreach (var r in cachedRenderers)
        {
            if (r == null) continue;

            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);

            block.SetColor(BaseColorID, overrideColor);
            block.SetColor(ColorID, overrideColor);
            block.SetColor(EmissionColorID, Color.black);

            if (clearBaseMap)
            {
                block.SetTexture(BaseMapID, whiteTexture);
                block.SetTexture(MainTexID, whiteTexture);
            }

            block.SetTexture(EmissionMapID, whiteTexture);

            r.SetPropertyBlock(block);
        }

        isApplied = true;
    }

    void Revert()
    {
        if (!isApplied || cachedRenderers == null) return;

        foreach (var r in cachedRenderers)
        {
            if (r == null) continue;
            r.SetPropertyBlock(null);
        }

        cachedRenderers = null;
        isApplied = false;
    }
}
