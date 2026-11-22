using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static BasePlayerController;

public class FocusVisuals : MonoBehaviour
{
    [SerializeField] private Volume player1Volume;
    [SerializeField] private Volume player2Volume;
    [SerializeField] private Camera player1Camera;
    [SerializeField] private Camera player2Camera;
    [SerializeField] private TextMeshProUGUI[] textLeft;
    [SerializeField] private TextMeshProUGUI[] textRight;

    private Vignette p1Vignette;
    private Vignette p2Vignette;
    private ColorAdjustments p1ColorAdjustments;
    private ColorAdjustments p2ColorAdjustments;

    private TwoPlayerInput p1Controls;
    private TwoPlayerInput p2Controls;

    private float currentTime1 = 0f;
    private float currentTime2 = 0f;

    public float vignetteIntensity = 0.31f;
    public float fadeInTime = 1f;
    public float fadeOutTime = 1f;

    private bool isPlayer1Holding = false;
    private bool isPlayer2Holding = false;

    void Start()
    {
        // === Camera VolumeLayerMask setup: keep original mask and add GlobalPostFX + each side's vignette ===
        var p1CamData = player1Camera.GetUniversalAdditionalCameraData();
        var p2CamData = player2Camera.GetUniversalAdditionalCameraData();

        // Cache the original mask from the Inspector to avoid overwriting other volumes (PostProcess, VO, etc.)
        int p1OriginalMask = p1CamData.volumeLayerMask;
        int p2OriginalMask = p2CamData.volumeLayerMask;

        // Masks for the layers we need
        int globalPostFXMask = LayerMask.GetMask("GlobalPostFX");
        int vignetteLeftMask = LayerMask.GetMask("VignetteLeft");
        int vignetteRightMask = LayerMask.GetMask("VignetteRight");

        // Player 1: original mask + global style + left-side vignette
        p1CamData.volumeLayerMask = p1OriginalMask | globalPostFXMask | vignetteLeftMask;
        // Player 2: original mask + global style + right-side vignette
        p2CamData.volumeLayerMask = p2OriginalMask | globalPostFXMask | vignetteRightMask;

        // === Input bindings ===
        p1Controls = PlayerInputManager.Instance.Player1Controls;
        p2Controls = PlayerInputManager.Instance.Player2Controls;

        p1Controls.Gameplay.Micro.performed += _ => OnMicro(PlayerID.Player1);
        p1Controls.Gameplay.Micro.canceled += _ => OnMicroOff(PlayerID.Player1);

        p2Controls.Gameplay.Micro.performed += _ => OnMicro(PlayerID.Player2);
        p2Controls.Gameplay.Micro.canceled += _ => OnMicroOff(PlayerID.Player2);

        /*
        controls = PlayerInputManager.Instance.Controls;
        controls.Gameplay.Micro1.performed += ctx => OnPlayer1Micro();
        controls.Gameplay.Micro1.canceled += ctx => OnPlayer1MicroOff();
        controls.Gameplay.Micro2.performed += ctx => OnPlayer2Micro();
        controls.Gameplay.Micro2.canceled += ctx => OnPlayer2MicroOff();
        */

        // === Put the two vignette volumes on their own layers ===
        player1Volume.gameObject.layer = LayerMask.NameToLayer("VignetteLeft");
        player2Volume.gameObject.layer = LayerMask.NameToLayer("VignetteRight");

        // === Read the components from the Volume profiles ===
        player1Volume.profile.TryGet(out p1Vignette);
        player2Volume.profile.TryGet(out p2Vignette);
        player1Volume.profile.TryGet(out p1ColorAdjustments);
        player2Volume.profile.TryGet(out p2ColorAdjustments);

        if (!player1Volume.profile.TryGet(out p1Vignette))
        {
            Debug.LogError("Vignette component not found in player1Volume profile!");
        }
        if (!player2Volume.profile.TryGet(out p2Vignette))
        {
            Debug.LogError("Vignette component not found in player2Volume profile!");
        }

        // Set a high priority so these volumes override others (but not GlobalPostFX, which is on another volume)
        player1Volume.priority = 1000;
        player2Volume.priority = 1000;

        // Initialize vignette: start with intensity 0 and split centers left/right
        if (p1Vignette != null)
        {
            p1Vignette.intensity.overrideState = true;
            p1Vignette.intensity.value = 0f;
            p1Vignette.center.overrideState = true;
            p1Vignette.center.value = new Vector2(0.75f, 0.5f);
        }
        if (p2Vignette != null)
        {
            p2Vignette.intensity.overrideState = true;
            p2Vignette.intensity.value = 0f;
            p2Vignette.center.overrideState = true;
            p2Vignette.center.value = new Vector2(0.25f, 0.5f);
        }

        // Reset any exposure values in these volumes to 0 to avoid conflicts with other volumes
        if (p1ColorAdjustments != null)
        {
            p1ColorAdjustments.postExposure.overrideState = true;
            p1ColorAdjustments.postExposure.value = 0f;
        }
        if (p2ColorAdjustments != null)
        {
            p2ColorAdjustments.postExposure.overrideState = true;
            p2ColorAdjustments.postExposure.value = 0f;
        }

        // Debug.Log($"Vignette initialized - P1: {p1Vignette?.intensity.value}, P2: {p2Vignette?.intensity.value}");
    }

    void OnMicro(PlayerID id)
    {
        if (id == PlayerID.Player1)
        {
            isPlayer1Holding = true;
            currentTime1 = 0f;
        }
        else
        {
            isPlayer2Holding = true;
            currentTime2 = 0f;
        }
    }

    void OnMicroOff(PlayerID id)
    {
        if (id == PlayerID.Player1)
        {
            isPlayer1Holding = false;
            currentTime1 = 0f;
        }
        else
        {
            isPlayer2Holding = false;
            currentTime2 = 0f;
        }
    }

    void Update()
    {
        // ===== Player 1 =====
        if (p1Vignette != null)
        {
            if (isPlayer1Holding)
            {
                // Fade in while the button is held
                if (currentTime1 < fadeInTime)
                {
                    float t = currentTime1 / fadeInTime;
                    t = 1f - (1f - t) * (1f - t); // quadratic ease out
                    p1Vignette.intensity.value = Mathf.Lerp(0f, vignetteIntensity, t);

                    foreach (TextMeshProUGUI i in textLeft)
                    {
                        float alpha = Mathf.Lerp(1f, 0f, t);
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, alpha);
                    }
                    currentTime1 += Time.deltaTime;
                }
                else
                {
                    // Make sure we reach full intensity
                    p1Vignette.intensity.value = vignetteIntensity;
                    foreach (TextMeshProUGUI i in textLeft)
                    {
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, 0f);
                    }
                }
            }
            else
            {
                // Fade out after the button is released
                if (currentTime1 < fadeOutTime)
                {
                    float t = currentTime1 / fadeOutTime;
                    t = 1f - (1f - t) * (1f - t); // quadratic ease out
                    p1Vignette.intensity.value = Mathf.Lerp(vignetteIntensity, 0f, t);

                    foreach (TextMeshProUGUI i in textLeft)
                    {
                        float alpha = Mathf.Lerp(0f, 1f, t);
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, alpha);
                    }
                    currentTime1 += Time.deltaTime;
                }
                else
                {
                    // Fully disabled
                    p1Vignette.intensity.value = 0f;
                    foreach (TextMeshProUGUI i in textLeft)
                    {
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, 1f);
                    }
                }
            }
        }

        // ===== Player 2 =====
        if (p2Vignette != null)
        {
            if (isPlayer2Holding)
            {
                // Fade in while the button is held
                if (currentTime2 < fadeInTime)
                {
                    float t = currentTime2 / fadeInTime;
                    t = 1f - (1f - t) * (1f - t); // quadratic ease out
                    p2Vignette.intensity.value = Mathf.Lerp(0f, vignetteIntensity, t);

                    foreach (TextMeshProUGUI i in textRight)
                    {
                        float alpha = Mathf.Lerp(1f, 0f, t);
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, alpha);
                    }
                    currentTime2 += Time.deltaTime;
                }
                else
                {
                    p2Vignette.intensity.value = vignetteIntensity;
                    foreach (TextMeshProUGUI i in textRight)
                    {
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, 0f);
                    }
                }
            }
            else
            {
                // Fade out after the button is released
                if (currentTime2 < fadeOutTime)
                {
                    float t = currentTime2 / fadeOutTime;
                    t = 1f - (1f - t) * (1f - t); // quadratic ease out
                    p2Vignette.intensity.value = Mathf.Lerp(vignetteIntensity, 0f, t);

                    foreach (TextMeshProUGUI i in textRight)
                    {
                        float alpha = Mathf.Lerp(0f, 1f, t);
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, alpha);
                    }
                    currentTime2 += Time.deltaTime;
                }
                else
                {
                    // Fully disabled
                    p2Vignette.intensity.value = 0f;
                    foreach (TextMeshProUGUI i in textRight)
                    {
                        Color color = i.color;
                        i.color = new Color(color.r, color.g, color.b, 1f);
                    }
                }
            }
        }
    }
}
