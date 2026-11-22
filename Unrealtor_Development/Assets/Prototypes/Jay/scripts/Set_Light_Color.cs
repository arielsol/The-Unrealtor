using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Set_Light_Color : MonoBehaviour
{
    [Header("Flat Color Settings")]
    public Color flatAmbientColor = Color.black;

    [Header("Skybox Settings")]
    public Material skyboxMaterial;
    public float skyboxAmbientIntensity = 1f;

    [Header("Directional Lights (expandable list)")]
    public List<Light> directionalLights = new List<Light>(); // All "sun" lights

    [Header("Spotlights (order: White, Red, Green, Blue)")]
    [Tooltip("Provide exactly 4 spotlights in this order: White, Red, Green, Blue")]
    public List<Light> spotlights = new List<Light>(4);

    private bool isInPlace = false;
    private int lightIndex = 0; // 0 = White, 1 = Red, 2 = Green, 3 = Blue

    void Start()
    {
        ValidateLights();
        // Start in Skybox mode with White spotlight (index 0)
        ApplySkyboxMode();
        EnableOnlySpotlight(lightIndex);
    }

    void Update()
    {
        if (!isInPlace) return;

        if (Input.GetKeyDown(KeyCode.T))
        {
            lightIndex++;
            if (lightIndex > 3) lightIndex = 0;

            if (lightIndex == 0)
            {
                Debug.Log("⚪ White spotlight ON (Skybox mode).");
                ApplySkyboxMode();
            }
            else
            {
                string label = lightIndex == 1 ? "🔴 Red" : lightIndex == 2 ? "🟢 Green" : "🔵 Blue";
                Debug.Log($"{label} spotlight ON (Blackout mode).");
                ApplyBlackoutMode();
            }

            EnableOnlySpotlight(lightIndex);
        }
    }

    // --- Lighting Mode Helpers --- //
    void ApplySkyboxMode()
    {
        if (skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
        }
        else
        {
            Debug.LogWarning("⚠️ No skyboxMaterial assigned! Using whatever is set in Lighting settings.");
        }

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = skyboxAmbientIntensity;
        RenderSettings.ambientLight = Color.white;

        if (Camera.main != null)
            Camera.main.clearFlags = CameraClearFlags.Skybox;

        SetDirectionalLightsEnabled(true);

        DynamicGI.UpdateEnvironment();
        Debug.Log("✅ Skybox lighting and directional lights enabled.");
    }

    void ApplyBlackoutMode()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = flatAmbientColor;
        RenderSettings.skybox = null;

        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.black;
        }

        SetDirectionalLightsEnabled(false);

        Debug.Log("🌑 Blackout mode: no skybox, flat ambient, black background, directional lights OFF.");
    }

    // --- Spotlight & Directional Controls --- //
    void EnableOnlySpotlight(int index)
    {
        // Disable all first
        for (int i = 0; i < spotlights.Count; i++)
        {
            if (spotlights[i] != null)
                spotlights[i].enabled = false;
        }

        // Enable the selected one
        if (index >= 0 && index < spotlights.Count && spotlights[index] != null)
        {
            spotlights[index].enabled = true;
        }
        else
        {
            Debug.LogWarning("⚠️ Spotlight index out of range or missing reference.");
        }
    }

    void SetDirectionalLightsEnabled(bool enabled)
    {
        foreach (var dir in directionalLights)
        {
            if (dir != null) dir.enabled = enabled;
        }
    }

    // --- Trigger Handling --- //
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            isInPlace = true;
            Debug.Log("👈 Player entered");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Player1") || other.CompareTag("Player2"))
        {
            isInPlace = false;
            Debug.Log("👈 Player exited");
        }
    }

    // --- Validation --- //
    void ValidateLights()
    {
        // Warn if spotlights list isn't exactly 4 (White, Red, Green, Blue)
        if (spotlights.Count != 4)
        {
            Debug.LogWarning("⚠️ Spotlights list should have EXACTLY 4 entries in this order: White, Red, Green, Blue.");
        }

        // Warn if any spotlight isn't a Spot
        for (int i = 0; i < spotlights.Count; i++)
        {
            var l = spotlights[i];
            if (l != null && l.type != LightType.Spot)
            {
                Debug.LogWarning($"⚠️ Spotlight at index {i} is not of type Spot.");
            }
        }
    }
}
