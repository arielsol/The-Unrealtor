using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LightToggleEvent : MonoBehaviour
{
    [SerializeField] private PaintingCondition PaintingConditionRef;
    [SerializeField] private PuzzleBase.PuzzleSide PaintingSide;

    [Header("Flat Color Settings")]
    public Color flatAmbientColor = Color.black;

    [Header("Skybox Settings")]
    public Material skyboxMaterial;
    public float skyboxAmbientIntensity = 1f;

    [Header("Directional Lights (expandable list)")]
    public List<Light> directionalLights = new List<Light>();

    [Header("Spotlights (order: White, Red, Green, Blue)")]
    [Tooltip("Provide exactly 4 spotlights in this order: White, Red, Green, Blue")]
    public List<Light> spotlights = new List<Light>(4);

    [SerializeField] public Light CorrectLight;
    private bool isInPlace = false;
    private int lightIndex = 0;
    [SerializeField] public InteractionBase Interaction;

    void Start()
    {
        ApplySkyboxMode();
        EnableOnlySpotlight(lightIndex);
    }

    void ApplySkyboxMode()
    {
        SetDirectionalLightsEnabled(true);
    }

    void ApplyBlackoutMode()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = flatAmbientColor;

        //RenderSettings.skybox = null;
        /* if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = Color.black;
        }*/

        SetDirectionalLightsEnabled(false);
    }

    void EnableOnlySpotlight(int index)
    {
        for (int i = 0; i < spotlights.Count; i++)
        {
            var l = spotlights[i];
            if (l == null) continue;

            l.enabled = false;
            l.gameObject.SetActive(false);
        }

        if (index < 0 || index >= spotlights.Count)
        {
            return;
        }

        Light target = spotlights[index];
        if (target == null)
        {
            return;
        }

        target.gameObject.SetActive(true);
        target.enabled = true;

        if (CorrectLight && Interaction)
        {
            if (CorrectLight == target)
            {
                if (PaintingSide == PuzzleBase.PuzzleSide.LeftPuzzle) PaintingConditionRef.SetPaintingSides(true, true);
                else PaintingConditionRef.SetPaintingSides(false, true);

                Debug.Log(gameObject.name + " is ready to match");
            }
            else
            {
                if (PaintingSide == PuzzleBase.PuzzleSide.LeftPuzzle) PaintingConditionRef.SetPaintingSides(true, false);
                else PaintingConditionRef.SetPaintingSides(false, false);
            }
        }
    }

    void SetDirectionalLightsEnabled(bool enabled)
    {
        foreach (var dir in directionalLights)
        {
            if (dir != null) dir.enabled = enabled;
        }
    }

    public void ToggleLightByEvent()
    {
        lightIndex++;
        if (lightIndex >= spotlights.Count) lightIndex = 0;

        /*if (lightIndex == 0)
        {
            ApplySkyboxMode();
        }
        else
        {
            ApplyBlackoutMode();
        }*/

        Debug.Log(lightIndex);
        EnableOnlySpotlight(lightIndex);
    }
}
