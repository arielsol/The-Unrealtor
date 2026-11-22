using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PuzzleHint : MonoBehaviour
{
    [Header("Hint Appearance")]
    public Color hintColor = Color.green;
    [Tooltip("Minimum visible intensity")]
    [Range(0f, 1f)] public float minIntensity = 0.05f;
    [Tooltip("Tint the color")]
    public bool useEmission = true;
    [Tooltip("Multiplier for emission intensity curve")] 
    [Range(0f, 5f)] public float emissionBoost = 1.5f;

    [SerializeField] [Tooltip("Material used only during Slow Mode to show the hint shader")] 
    public Material hintMaterial;
    [Tooltip("Swap to hintMaterial only while Slow Mode is active")] 
    public bool swapToHintMaterial = true;

    private Material mat;
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;
    [Header("Add Shader :)")]
    [Tooltip("Drive shader properties _PuzzleColor and _HintIntensity on the renderer's material")] 
    public bool driveHintShader = true;

    private Color baseColor;
    private bool hasBaseColorProperty;
    private bool hasEmissionProperty;
    private Color baseEmissionColor;
    private float currentIntensity;
    private Color currentOverlayColor;

    public float CurrentIntensity => currentIntensity;
    public Color CurrentOverlayColor => currentOverlayColor;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        mat = cachedRenderer.material;
        propertyBlock = new MaterialPropertyBlock();
        hasBaseColorProperty = mat.HasProperty("_BaseColor");
        baseColor = hasBaseColorProperty ? mat.GetColor("_BaseColor") : mat.color;

        hasEmissionProperty = mat.HasProperty("_EmissionColor");
        if (hasEmissionProperty)
        {
            baseEmissionColor = mat.GetColor("_EmissionColor");
            if (useEmission)
            {
                mat.EnableKeyword("_EMISSION");
            }
        }
    }

    public void SetHintIntensity(float t)
    {
        t = Mathf.Clamp01(t);
        if (t > 0f)
        {
            float adjusted = Mathf.Max(t, minIntensity);
            Color target = Color.Lerp(hintColor * 0.5f + baseColor * 0.0f, hintColor, adjusted);

            if (useEmission && hasEmissionProperty)
            {
                float e = Mathf.Clamp01(adjusted * emissionBoost);
                mat.SetColor("_EmissionColor", Color.Lerp(baseEmissionColor, hintColor, e));
            }

            currentIntensity = adjusted;
            currentOverlayColor = target;

            RestoreOriginalSharedMaterialIfNeeded();

            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            cachedRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", target);
            if (driveHintShader)
            {
                propertyBlock.SetColor("_PuzzleColor", hintColor);
                propertyBlock.SetFloat("_HintIntensity", adjusted);
            }
            cachedRenderer.SetPropertyBlock(propertyBlock);
        }
        else
        {
            if (hasEmissionProperty) mat.SetColor("_EmissionColor", baseEmissionColor);

            currentIntensity = 0f;
            currentOverlayColor = baseColor;

            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            cachedRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
            if (driveHintShader)
            {
                propertyBlock.SetFloat("_HintIntensity", 0f);
            }
            cachedRenderer.SetPropertyBlock(propertyBlock);

            RestoreOriginalSharedMaterialIfNeeded();
        }
    }

    private Material _originalSharedMaterial;
    private bool _usingHintSharedMaterial;

    private void ApplyHintSharedMaterialIfNeeded()
    {
        if (cachedRenderer == null || hintMaterial == null) return;
        if (_usingHintSharedMaterial) return;
        _originalSharedMaterial = _originalSharedMaterial ?? cachedRenderer.sharedMaterial;
        cachedRenderer.sharedMaterial = hintMaterial;
        _usingHintSharedMaterial = true;
        mat = cachedRenderer.material;
    }

    private void RestoreOriginalSharedMaterialIfNeeded()
    {
        if (cachedRenderer == null || !_usingHintSharedMaterial) return;
        if (_originalSharedMaterial != null)
        {
            cachedRenderer.sharedMaterial = _originalSharedMaterial;
            mat = cachedRenderer.material;
        }
        _usingHintSharedMaterial = false;
    }
}
