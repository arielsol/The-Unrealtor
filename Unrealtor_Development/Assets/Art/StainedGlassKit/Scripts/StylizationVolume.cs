using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Volume component for the fullscreen stylization post-processing effect.
/// Add this override to a Volume to control anime outlines, posterization,
/// film grain, halftone shadows, and saturation.
/// </summary>
[System.Serializable, VolumeComponentMenu("Post-processing/Stylization")]
public class StylizationVolume : VolumeComponent
{
    [Header("Global")]
    [Tooltip("Master intensity. 0 = original image, 1 = full effect.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Header("Edge Detection / Outlines")]
    [Tooltip("Outline color. Dark brown-black gives a natural anime look.")]
    public ColorParameter outlineColor = new ColorParameter(new Color(0.05f, 0.02f, 0.02f, 1f));

    [Tooltip("Outline thickness in texels.")]
    public ClampedFloatParameter outlineWidth = new ClampedFloatParameter(1.5f, 0.5f, 5f);

    [Tooltip("Sensitivity to depth discontinuities. Higher = more outlines from depth edges.")]
    public ClampedFloatParameter depthSensitivity = new ClampedFloatParameter(3f, 0f, 20f);

    [Tooltip("Sensitivity to normal discontinuities. Higher = more outlines from surface angle changes.")]
    public ClampedFloatParameter normalSensitivity = new ClampedFloatParameter(2f, 0f, 10f);

    [Tooltip("Depth edge threshold. Edges below this are discarded.")]
    public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.05f, 0f, 1f);

    [Tooltip("Normal edge threshold. Edges below this are discarded.")]
    public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.2f, 0f, 1f);

    [Tooltip("Distance (world units) where outlines begin to fade out. Full outline before this distance.")]
    public MinFloatParameter outlineFadeStart = new MinFloatParameter(20f, 0f);

    [Tooltip("Distance (world units) where outlines fully disappear.")]
    public MinFloatParameter outlineFadeEnd = new MinFloatParameter(60f, 0f);

    [Header("Posterization")]
    [Tooltip("Number of discrete color levels per channel.")]
    public ClampedIntParameter posterizeLevels = new ClampedIntParameter(6, 2, 32);

    [Tooltip("Blend between smooth (0) and posterized (1).")]
    public ClampedFloatParameter posterizeStrength = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Header("Saturation")]
    [Tooltip("Color saturation multiplier. 1 = unchanged, >1 = more vivid.")]
    public ClampedFloatParameter saturationBoost = new ClampedFloatParameter(1.4f, 0f, 3f);

    [Header("Film Grain / Noise")]
    [Tooltip("Grain overlay strength.")]
    public ClampedFloatParameter noiseStrength = new ClampedFloatParameter(0.03f, 0f, 0.5f);

    [Tooltip("Noise pattern scale.")]
    public ClampedFloatParameter noiseScale = new ClampedFloatParameter(300f, 10f, 1000f);

    [Tooltip("Noise animation speed.")]
    public ClampedFloatParameter noiseSpeed = new ClampedFloatParameter(15f, 0f, 50f);

    [Header("Halftone (Shadow Areas)")]
    [Tooltip("Halftone/crosshatch pattern strength in shadow areas.")]
    public ClampedFloatParameter halftoneStrength = new ClampedFloatParameter(0.3f, 0f, 1f);

    [Tooltip("Halftone pattern scale.")]
    public ClampedFloatParameter halftoneScale = new ClampedFloatParameter(150f, 20f, 500f);

    [Tooltip("Luminance below this gets halftone overlay.")]
    public ClampedFloatParameter halftoneShadowThreshold = new ClampedFloatParameter(0.35f, 0f, 1f);

    [Header("Rim Light")]
    [Tooltip("Rim light color. Slightly cool tones (blue/purple) work well for anime style.")]
    public ColorParameter rimColor = new ColorParameter(new Color(0.6f, 0.7f, 1f, 1f));

    [Tooltip("Rim light intensity. 0 = off.")]
    public ClampedFloatParameter rimIntensity = new ClampedFloatParameter(0f, 0f, 3f);

    [Tooltip("Rim sharpness / falloff power. Higher = thinner rim.")]
    public ClampedFloatParameter rimPower = new ClampedFloatParameter(3f, 0.5f, 10f);

    public bool IsActive() => intensity.value > 0.001f;
}
