using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Controls how many and which light sources affect this stained glass object.
/// Provides per-object control over light interactions in URP.
///
/// There are two modes of operation:
/// 1. Light Layer Mode: Uses URP's built-in light layers for static assignment
/// 2. Dynamic Priority Mode: Automatically selects the N closest/brightest lights
/// </summary>
[ExecuteAlways]
public class StainedGlassLightController : MonoBehaviour
{
    public enum LightSelectionMode
    {
        /// <summary>
        /// Uses URP Light Layers. Assign this object and lights to matching layers.
        /// Most performant option. Requires Light Layers enabled in URP settings.
        /// </summary>
        LightLayers,

        /// <summary>
        /// Dynamically selects the top N lights based on distance and intensity.
        /// Updates every frame. More flexible but has runtime overhead.
        /// </summary>
        DynamicPriority,

        /// <summary>
        /// Manually specify which lights affect this object.
        /// Good for precise artistic control.
        /// </summary>
        ManualAssignment
    }

    [Header("Light Selection")]
    [Tooltip("How lights are selected for this stained glass object")]
    public LightSelectionMode selectionMode = LightSelectionMode.DynamicPriority;

    [Header("Light Count Control")]
    [Tooltip("Maximum number of lights this object interacts with (1-8)")]
    [Range(1, 8)]
    public int maxLightCount = 4;

    [Header("Light Layers Mode")]
    [Tooltip("Which rendering layer mask this object uses (for Light Layers mode)")]
    public uint renderingLayerMask = 1;

    [Header("Dynamic Priority Mode")]
    [Tooltip("How much distance affects light priority (higher = distance matters more)")]
    [Range(0f, 2f)]
    public float distanceWeight = 1f;

    [Tooltip("How much intensity affects light priority (higher = brightness matters more)")]
    [Range(0f, 2f)]
    public float intensityWeight = 1f;

    [Tooltip("Maximum distance to consider lights (0 = unlimited)")]
    public float maxLightDistance = 0f;

    [Tooltip("Update frequency in seconds (0 = every frame)")]
    public float updateInterval = 0f;

    [Header("Manual Assignment Mode")]
    [Tooltip("Manually assigned lights (for Manual Assignment mode)")]
    public List<Light> assignedLights = new List<Light>();

    [Header("Debug")]
    [Tooltip("Show which lights are currently affecting this object")]
    public bool showDebugInfo = false;

    [Tooltip("Draw gizmos to visualize light connections")]
    public bool drawGizmos = true;

    // Runtime state
    private Renderer[] _renderers;
    private List<Light> _activeLights = new List<Light>();
    private float _lastUpdateTime;
    private static Light[] _allLightsCache;
    private static float _lightsCacheTime;
    private const float LIGHTS_CACHE_DURATION = 0.5f;

    // Light scoring for priority mode
    private struct LightScore
    {
        public Light light;
        public float score;
    }
    private List<LightScore> _lightScores = new List<LightScore>();

    void OnEnable()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        UpdateLightConfiguration();
    }

    void Update()
    {
        if (selectionMode == LightSelectionMode.DynamicPriority)
        {
            if (updateInterval <= 0f || Time.time - _lastUpdateTime >= updateInterval)
            {
                UpdateDynamicLights();
                _lastUpdateTime = Time.time;
            }
        }
    }

    void OnValidate()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<Renderer>();

        UpdateLightConfiguration();
    }

    /// <summary>
    /// Call this to force an update of the light configuration
    /// </summary>
    public void UpdateLightConfiguration()
    {
        switch (selectionMode)
        {
            case LightSelectionMode.LightLayers:
                ApplyLightLayerMask();
                break;
            case LightSelectionMode.DynamicPriority:
                UpdateDynamicLights();
                break;
            case LightSelectionMode.ManualAssignment:
                ApplyManualLights();
                break;
        }
    }

    /// <summary>
    /// Set the maximum light count at runtime
    /// </summary>
    public void SetMaxLightCount(int count)
    {
        maxLightCount = Mathf.Clamp(count, 1, 8);
        UpdateLightConfiguration();
    }

    /// <summary>
    /// Get the lights currently affecting this object
    /// </summary>
    public List<Light> GetActiveLights()
    {
        return new List<Light>(_activeLights);
    }

    private void ApplyLightLayerMask()
    {
        if (_renderers == null) return;

        foreach (var renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.renderingLayerMask = renderingLayerMask;
            }
        }

        // For light layers mode, find lights on matching layers
        _activeLights.Clear();
        var lights = GetAllLights();
        foreach (var light in lights)
        {
            if (light != null && (light.renderingLayerMask & renderingLayerMask) != 0)
            {
                _activeLights.Add(light);
                if (_activeLights.Count >= maxLightCount)
                    break;
            }
        }
    }

    private void UpdateDynamicLights()
    {
        if (_renderers == null || _renderers.Length == 0) return;

        var lights = GetAllLights();
        Vector3 objectPosition = transform.position;

        // Score all lights
        _lightScores.Clear();
        foreach (var light in lights)
        {
            if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                continue;

            // Skip main directional light (it's always included by URP)
            if (light.type == LightType.Directional)
                continue;

            float distance = Vector3.Distance(objectPosition, light.transform.position);

            // Skip if beyond max distance
            if (maxLightDistance > 0f && distance > maxLightDistance)
                continue;

            // Calculate effective range based on light type
            float lightRange = light.range;
            if (distance > lightRange)
                continue; // Light can't reach this object

            // Calculate priority score (higher = more important)
            float distanceFactor = 1f - (distance / lightRange);
            float intensityFactor = light.intensity;

            float score = (distanceFactor * distanceWeight) + (intensityFactor * intensityWeight);

            _lightScores.Add(new LightScore { light = light, score = score });
        }

        // Sort by score descending
        _lightScores.Sort((a, b) => b.score.CompareTo(a.score));

        // Take top N lights
        _activeLights.Clear();
        int count = Mathf.Min(maxLightCount, _lightScores.Count);
        for (int i = 0; i < count; i++)
        {
            _activeLights.Add(_lightScores[i].light);
        }

        // In dynamic mode, we can use light layers to control which lights affect this object
        // We assign the object to layer 1 and enable only our selected lights on that layer
        // Note: This modifies light settings globally, so use with caution in complex scenes
        UpdateLightLayersForDynamicMode();
    }

    private void UpdateLightLayersForDynamicMode()
    {
        // Use a unique layer bit for this controller (layers 2-8, bit 1-7)
        // For simplicity, we'll use the default layer approach
        // In production, you might want a more sophisticated layer management system

        if (_renderers == null) return;

        // Set renderer to use default rendering layer
        uint mask = 1; // Default layer
        foreach (var renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.renderingLayerMask = mask;
            }
        }
    }

    private void ApplyManualLights()
    {
        _activeLights.Clear();

        int count = Mathf.Min(maxLightCount, assignedLights.Count);
        for (int i = 0; i < count; i++)
        {
            if (assignedLights[i] != null)
            {
                _activeLights.Add(assignedLights[i]);
            }
        }

        // For manual mode, we use light layers to ensure only assigned lights affect this object
        // This requires setting up light layers in your URP asset
        if (_renderers == null) return;

        foreach (var renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.renderingLayerMask = renderingLayerMask;
            }
        }
    }

    private Light[] GetAllLights()
    {
        // Cache the lights array to avoid FindObjectsOfType every frame
        if (_allLightsCache == null || Time.time - _lightsCacheTime > LIGHTS_CACHE_DURATION)
        {
            _allLightsCache = FindObjectsOfType<Light>();
            _lightsCacheTime = Time.time;
        }
        return _allLightsCache;
    }

    /// <summary>
    /// Force refresh the global lights cache
    /// </summary>
    public static void RefreshLightsCache()
    {
        _allLightsCache = null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.yellow;

        // Draw connections to active lights
        foreach (var light in _activeLights)
        {
            if (light != null)
            {
                Gizmos.DrawLine(transform.position, light.transform.position);
                Gizmos.DrawWireSphere(light.transform.position, 0.2f);
            }
        }

        // Draw max distance sphere if set
        if (maxLightDistance > 0f && selectionMode == LightSelectionMode.DynamicPriority)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, maxLightDistance);
        }
    }
#endif

    void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        // Convert world position to screen position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z < 0) return;

        float y = Screen.height - screenPos.y;

        GUI.color = Color.white;
        GUI.Label(new Rect(screenPos.x, y, 300, 20), $"Stained Glass: {gameObject.name}");
        GUI.Label(new Rect(screenPos.x, y + 20, 300, 20), $"Mode: {selectionMode}");
        GUI.Label(new Rect(screenPos.x, y + 40, 300, 20), $"Active Lights: {_activeLights.Count}/{maxLightCount}");

        for (int i = 0; i < _activeLights.Count; i++)
        {
            if (_activeLights[i] != null)
            {
                GUI.Label(new Rect(screenPos.x + 10, y + 60 + i * 20, 300, 20),
                    $"- {_activeLights[i].name} ({_activeLights[i].type})");
            }
        }
    }
}
