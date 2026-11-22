using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Volume component for the volumetric fog system.
/// Add this to a Volume to control fog density, scattering, and appearance.
/// Works with all Unity light types: directional, point, spot.
/// Stained glass colored lights will tint the fog with their color.
/// </summary>
[System.Serializable, VolumeComponentMenu("Post-processing/Volumetric Fog")]
public class VolumetricFogVolume : VolumeComponent
{
    [Header("Fog Density")]
    [Tooltip("Overall fog density. Higher = thicker fog.")]
    public ClampedFloatParameter density = new ClampedFloatParameter(0f, 0f, 0.5f);

    [Tooltip("Base fog color / ambient fog tint.")]
    public ColorParameter fogColor = new ColorParameter(new Color(0.5f, 0.6f, 0.7f, 1f));

    [Header("Ray Marching Quality")]
    [Tooltip("Number of ray march steps. More = better quality, lower performance.")]
    public ClampedIntParameter steps = new ClampedIntParameter(48, 8, 128);

    [Tooltip("Maximum fog distance from camera.")]
    public ClampedFloatParameter maxDistance = new ClampedFloatParameter(80f, 10f, 500f);

    [Tooltip("Temporal jitter strength to reduce banding artifacts.")]
    public ClampedFloatParameter temporalJitter = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Header("Light Scattering")]
    [Tooltip("Scattering anisotropy (Henyey-Greenstein g). Positive = forward scattering (god rays toward camera). 0 = isotropic.")]
    public ClampedFloatParameter scatteringAnisotropy = new ClampedFloatParameter(0.65f, -0.9f, 0.95f);

    [Tooltip("Main directional light intensity multiplier in fog.")]
    public ClampedFloatParameter lightIntensity = new ClampedFloatParameter(1.5f, 0f, 10f);

    [Tooltip("Additional lights (point/spot) intensity in fog. Stained glass colored lights use this.")]
    public ClampedFloatParameter additionalLightsIntensity = new ClampedFloatParameter(1.2f, 0f, 10f);

    [Tooltip("Maximum additional lights evaluated per ray step.")]
    public ClampedIntParameter maxAdditionalLights = new ClampedIntParameter(8, 0, 16);

    [Tooltip("Ambient fog scattering (base light in fog even without direct light).")]
    public ClampedFloatParameter ambientIntensity = new ClampedFloatParameter(0.03f, 0f, 1f);

    [Header("Height Fog")]
    [Tooltip("Height at which fog density starts to decrease.")]
    public FloatParameter baseHeight = new FloatParameter(0f);

    [Tooltip("How quickly fog density decreases above base height. 0 = no height falloff.")]
    public ClampedFloatParameter heightFalloff = new ClampedFloatParameter(0.05f, 0f, 5f);

    [Header("Noise")]
    [Tooltip("Scale of the 3D noise pattern. Smaller = larger noise features.")]
    public ClampedFloatParameter noiseScale = new ClampedFloatParameter(0.3f, 0.01f, 2f);

    [Tooltip("How much noise affects fog density. 0 = uniform fog.")]
    public ClampedFloatParameter noiseIntensity = new ClampedFloatParameter(0.2f, 0f, 1f);

    public bool IsActive() => density.value > 0.0001f;
}
