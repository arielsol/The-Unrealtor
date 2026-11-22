using UnityEngine;

/// <summary>
/// Component for controlling Pencil Edge Outline per-object.
/// Uses edge detection for clean lines at depth/normal discontinuities.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class PencilOutlineController : MonoBehaviour
{
    [Header("LINE APPEARANCE")]
    [Tooltip("Line color")]
    public Color lineColor = new Color(0.12f, 0.1f, 0.08f, 1f);

    [Tooltip("Line width in pixels")]
    [Range(0.5f, 5f)]
    public float lineWidth = 1.5f;

    [Tooltip("Line edge softness")]
    [Range(0f, 1f)]
    public float softness = 0.2f;

    [Header("EDGE DETECTION")]
    [Tooltip("Sensitivity to depth differences")]
    [Range(0f, 50f)]
    public float depthSensitivity = 15f;

    [Tooltip("Sensitivity to normal differences")]
    [Range(0f, 10f)]
    public float normalSensitivity = 3f;

    [Tooltip("Minimum depth difference threshold")]
    [Range(0f, 1f)]
    public float depthThreshold = 0.1f;

    [Tooltip("Minimum normal difference threshold")]
    [Range(0f, 1f)]
    public float normalThreshold = 0.3f;

    [Header("SKETCH EFFECT")]
    [Tooltip("Enable hand-drawn wobble")]
    public bool enableSketch = true;

    [Tooltip("Wobble amount")]
    [Range(0f, 1f)]
    public float sketchStrength = 0.3f;

    [Tooltip("Wobble detail scale")]
    [Range(1f, 30f)]
    public float sketchScale = 10f;

    [Tooltip("Wobble animation speed")]
    [Range(0f, 2f)]
    public float sketchSpeed = 0.4f;

    [Header("WAVE EFFECT")]
    [Tooltip("Enable calm wave motion")]
    public bool enableWave = true;

    [Tooltip("Wave strength")]
    [Range(0f, 1f)]
    public float waveStrength = 0.3f;

    [Tooltip("Wave frequency")]
    [Range(0.5f, 8f)]
    public float waveFrequency = 2f;

    [Tooltip("Wave animation speed")]
    [Range(0f, 2f)]
    public float waveSpeed = 0.6f;

    [Header("LINE VARIATION")]
    [Tooltip("Random thickness variation")]
    [Range(0f, 1f)]
    public float thicknessVariation = 0.3f;

    [Tooltip("Random opacity variation")]
    [Range(0f, 1f)]
    public float opacityVariation = 0.2f;

    [Header("DISTANCE SCALING")]
    [Tooltip("Scale line width with camera distance")]
    public bool enableDistanceScale = true;

    [Tooltip("Distance where line width = 100%")]
    public float referenceDistance = 5f;

    [Tooltip("Minimum width multiplier (far away)")]
    [Range(0.1f, 1f)]
    public float minWidthScale = 0.3f;

    [Tooltip("Maximum width multiplier (close up)")]
    [Range(1f, 3f)]
    public float maxWidthScale = 1.5f;

    [Header("DISTANCE FADE")]
    [Tooltip("Fade out at distance")]
    public bool enableDistanceFade = true;

    [Tooltip("Distance where fade starts")]
    public float fadeStart = 30f;

    [Tooltip("Distance where fully faded")]
    public float fadeEnd = 100f;

    [Header("SETUP")]
    [Tooltip("Material slot index (-1 = auto)")]
    public int outlineMaterialIndex = -1;

    [Tooltip("Source material template")]
    public Material sourceMaterial;

    private Renderer targetRenderer;
    private Material materialInstance;

    // Property IDs
    private static readonly int PropOutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");
    private static readonly int PropSoftness = Shader.PropertyToID("_Softness");
    private static readonly int PropDepthSensitivity = Shader.PropertyToID("_DepthSensitivity");
    private static readonly int PropNormalSensitivity = Shader.PropertyToID("_NormalSensitivity");
    private static readonly int PropDepthThreshold = Shader.PropertyToID("_DepthThreshold");
    private static readonly int PropNormalThreshold = Shader.PropertyToID("_NormalThreshold");
    private static readonly int PropEnableSketch = Shader.PropertyToID("_EnableSketch");
    private static readonly int PropSketchStrength = Shader.PropertyToID("_SketchStrength");
    private static readonly int PropSketchScale = Shader.PropertyToID("_SketchScale");
    private static readonly int PropSketchSpeed = Shader.PropertyToID("_SketchSpeed");
    private static readonly int PropEnableWave = Shader.PropertyToID("_EnableWave");
    private static readonly int PropWaveStrength = Shader.PropertyToID("_WaveStrength");
    private static readonly int PropWaveFrequency = Shader.PropertyToID("_WaveFrequency");
    private static readonly int PropWaveSpeed = Shader.PropertyToID("_WaveSpeed");
    private static readonly int PropThicknessVariation = Shader.PropertyToID("_ThicknessVariation");
    private static readonly int PropOpacityVariation = Shader.PropertyToID("_OpacityVariation");
    private static readonly int PropEnableDistanceScale = Shader.PropertyToID("_EnableDistanceScale");
    private static readonly int PropReferenceDistance = Shader.PropertyToID("_ReferenceDistance");
    private static readonly int PropMinWidthScale = Shader.PropertyToID("_MinWidthScale");
    private static readonly int PropMaxWidthScale = Shader.PropertyToID("_MaxWidthScale");
    private static readonly int PropEnableDistanceFade = Shader.PropertyToID("_EnableDistanceFade");
    private static readonly int PropFadeStart = Shader.PropertyToID("_FadeStart");
    private static readonly int PropFadeEnd = Shader.PropertyToID("_FadeEnd");

    private void OnEnable()
    {
        targetRenderer = GetComponent<Renderer>();
        FindOrCreateMaterial();
        ApplyProperties();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && materialInstance != null)
        {
            Destroy(materialInstance);
            materialInstance = null;
        }
    }

    private void OnValidate()
    {
        fadeEnd = Mathf.Max(fadeStart + 1f, fadeEnd);
        if (targetRenderer != null)
            ApplyProperties();
    }

    private void FindOrCreateMaterial()
    {
        if (targetRenderer == null) return;

        Material[] materials = targetRenderer.sharedMaterials;

        if (outlineMaterialIndex < 0)
        {
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && materials[i].shader.name == "Custom/PencilOutline")
                {
                    outlineMaterialIndex = i;
                    break;
                }
            }
        }

        if (outlineMaterialIndex >= 0 && outlineMaterialIndex < materials.Length)
        {
            Material source = materials[outlineMaterialIndex];
            if (source != null && source.shader.name == "Custom/PencilOutline")
            {
                materialInstance = new Material(source);
                materialInstance.name = source.name + " (Instance)";
                materials[outlineMaterialIndex] = materialInstance;
                targetRenderer.sharedMaterials = materials;
            }
        }
    }

    public void ApplyProperties()
    {
        if (materialInstance == null) return;

        materialInstance.SetColor(PropOutlineColor, lineColor);
        materialInstance.SetFloat(PropOutlineWidth, lineWidth);
        materialInstance.SetFloat(PropSoftness, softness);

        materialInstance.SetFloat(PropDepthSensitivity, depthSensitivity);
        materialInstance.SetFloat(PropNormalSensitivity, normalSensitivity);
        materialInstance.SetFloat(PropDepthThreshold, depthThreshold);
        materialInstance.SetFloat(PropNormalThreshold, normalThreshold);

        materialInstance.SetFloat(PropEnableSketch, enableSketch ? 1f : 0f);
        materialInstance.SetFloat(PropSketchStrength, sketchStrength);
        materialInstance.SetFloat(PropSketchScale, sketchScale);
        materialInstance.SetFloat(PropSketchSpeed, sketchSpeed);
        SetKeyword("_ENABLESKETCH_ON", enableSketch);

        materialInstance.SetFloat(PropEnableWave, enableWave ? 1f : 0f);
        materialInstance.SetFloat(PropWaveStrength, waveStrength);
        materialInstance.SetFloat(PropWaveFrequency, waveFrequency);
        materialInstance.SetFloat(PropWaveSpeed, waveSpeed);
        SetKeyword("_ENABLEWAVE_ON", enableWave);

        materialInstance.SetFloat(PropThicknessVariation, thicknessVariation);
        materialInstance.SetFloat(PropOpacityVariation, opacityVariation);

        materialInstance.SetFloat(PropEnableDistanceScale, enableDistanceScale ? 1f : 0f);
        materialInstance.SetFloat(PropReferenceDistance, referenceDistance);
        materialInstance.SetFloat(PropMinWidthScale, minWidthScale);
        materialInstance.SetFloat(PropMaxWidthScale, maxWidthScale);
        SetKeyword("_ENABLEDISTANCESCALE_ON", enableDistanceScale);

        materialInstance.SetFloat(PropEnableDistanceFade, enableDistanceFade ? 1f : 0f);
        materialInstance.SetFloat(PropFadeStart, fadeStart);
        materialInstance.SetFloat(PropFadeEnd, fadeEnd);
        SetKeyword("_ENABLEDISTANCEFADE_ON", enableDistanceFade);
    }

    private void SetKeyword(string keyword, bool state)
    {
        if (materialInstance == null) return;
        if (state)
            materialInstance.EnableKeyword(keyword);
        else
            materialInstance.DisableKeyword(keyword);
    }

    public void SetColor(Color color)
    {
        lineColor = color;
        materialInstance?.SetColor(PropOutlineColor, color);
    }

    public void SetWidth(float width)
    {
        lineWidth = Mathf.Clamp(width, 0.5f, 5f);
        materialInstance?.SetFloat(PropOutlineWidth, lineWidth);
    }

    public void AddOutlineMaterial()
    {
        if (targetRenderer == null || sourceMaterial == null) return;

        Material[] current = targetRenderer.sharedMaterials;
        Material[] newMats = new Material[current.Length + 1];
        for (int i = 0; i < current.Length; i++)
            newMats[i] = current[i];

        materialInstance = new Material(sourceMaterial);
        materialInstance.name = sourceMaterial.name + " (Instance)";
        newMats[newMats.Length - 1] = materialInstance;

        targetRenderer.sharedMaterials = newMats;
        outlineMaterialIndex = newMats.Length - 1;
        ApplyProperties();
    }

    public void RemoveOutlineMaterial()
    {
        if (targetRenderer == null || outlineMaterialIndex < 0) return;

        Material[] current = targetRenderer.sharedMaterials;
        if (outlineMaterialIndex >= current.Length) return;

        Material[] newMats = new Material[current.Length - 1];
        int j = 0;
        for (int i = 0; i < current.Length; i++)
        {
            if (i != outlineMaterialIndex)
                newMats[j++] = current[i];
        }

        targetRenderer.sharedMaterials = newMats;

        if (materialInstance != null)
        {
            if (Application.isPlaying)
                Destroy(materialInstance);
            else
                DestroyImmediate(materialInstance);
            materialInstance = null;
        }

        outlineMaterialIndex = -1;
    }
}
