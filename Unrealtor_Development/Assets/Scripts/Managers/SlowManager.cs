using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;

public class SlowManager : MonoBehaviour
{
    [Header("Slow Mode Settings")]
    public float fadeInTime = 1f;
    public float fadeOutTime = 1f;
    public float slowTimeScale = 0.3f;

    [Header("Player References")]
    public Camera player1Camera;
    public Camera player2Camera;
    public Volume player1Volume;
    public Volume player2Volume;
    public float vignetteIntensity = 0.31f;

    [Header("Puzzle Hints (Distance Saturation)")]
    public Transform[] players;
    public float colorDistance = 5f;

    [Header("Slow Ratios")]
    [Range(0f, 1f)] public float slowMovementRatio = 0.2f;
    [Range(0f, 1f)] public float slowAimRatio = 0.3f;
    [Range(0f, 1f)] public float slowRollRatio = 0.3f;

    [Header("Camera Renderers")]
    [Tooltip("Renderer index for normal gameplay (matches list order in URP Renderer List)")]
    [SerializeField] private int baseRendererIndex = 0;
    [Tooltip("Renderer index that applies grayscale (renderer with B/W pass configured)")]
    [SerializeField] private int grayscaleRendererIndex = 1;
    [Tooltip("Renderer index used by overlay cameras (should be a color renderer)")]
    [SerializeField] private int overlayRendererIndex = 0;
    [Tooltip("Layer name used by overlays to render hints only")] public string overlaySaturationLayerName = "Saturation_Layer";
    private int overlaySaturationLayer = -1;

    [Header("Debug")]
    [Tooltip("If enabled, disables all renderer switching and forces base renderer for both cameras.")]
    [SerializeField] private bool debugBypassRendererSwitch = true;
    [Tooltip("Print detailed diagnostics about renderer/volume switching.")]
    [SerializeField] private bool verboseLogging = true;
    [Tooltip("If true, SlowManager will control vignette intensity. Disable to let another script (e.g., PuzzleProximityVignette) manage it.")]
    [SerializeField] private bool controlVignetteInSlow = false;

    private Vignette p1Vignette;
    private Vignette p2Vignette;
    private ColorAdjustments p1Color;
    private ColorAdjustments p2Color;

    private bool isSlowModeActive = false;
    private float currentTime = 0f;
    private float targetTimeScale = 1f;
    private float p1EffectWeight = 0f;
    private float p2EffectWeight = 0f;
    // Track last-applied renderer for logging (avoid spam)
    private int p1LastAppliedRenderer = int.MinValue;
    private int p2LastAppliedRenderer = int.MinValue;

    private TwoPlayerInput p1Controls;
    private TwoPlayerInput p2Controls;

    private InputAction p1SlowAction;
    private InputAction p1HintAction;
    private InputAction p1TiltLeftAction;
    private InputAction p1TiltRightAction;

    private InputAction p2SlowAction;
    private InputAction p2HintAction;
    private InputAction p2TiltLeftAction;
    private InputAction p2TiltRightAction;

    private bool p1SlowHeld;
    private bool p1HintHeld;
    private bool p1TiltLeftHeld;
    private bool p1TiltRightHeld;

    private bool p2SlowHeld;
    private bool p2HintHeld;
    private bool p2TiltLeftHeld;
    private bool p2TiltRightHeld;

    public bool IsActive => isSlowModeActive;
    public bool P1SlowActive => p1SlowHeld;
    public bool P2SlowActive => p2SlowHeld;
    public bool P1HintActive => p1HintHeld;
    public bool P2HintActive => p2HintHeld;
    private bool wasAnySlowHeld = false;

    void Awake()
    {
    }

    void OnValidate()
    {
        #if UNITY_EDITOR
        // Enforce renderer indices: 1 = color (URP-Balanced-Renderer), 0 = grayscale
        baseRendererIndex = 1;
        grayscaleRendererIndex = 0;
        overlayRendererIndex = 1;
        #endif
    }

    void Start()
    {
        SetupInputBindings();
        player1Volume.profile.TryGet(out p1Vignette);
        player2Volume.profile.TryGet(out p2Vignette);
        player1Volume.profile.TryGet(out p1Color);
        player2Volume.profile.TryGet(out p2Color);

        if (controlVignetteInSlow)
        {
            if (p1Vignette != null) { p1Vignette.intensity.overrideState = true; p1Vignette.intensity.value = 0f; }
            if (p2Vignette != null) { p2Vignette.intensity.overrideState = true; p2Vignette.intensity.value = 0f; }
        }
        if (p1Color != null) { p1Color.saturation.overrideState = true; p1Color.saturation.value = 0f; }
        if (p2Color != null) { p2Color.saturation.overrideState = true; p2Color.saturation.value = 0f; }

        overlaySaturationLayer = LayerMask.NameToLayer(overlaySaturationLayerName);

        if (verboseLogging)
        {
            Debug.Log($"[SlowManager] Start - Cameras: L='{player1Camera?.name}', R='{player2Camera?.name}'");
            Debug.Log($"[SlowManager] Start - Volumes: L='{player1Volume?.name}', R='{player2Volume?.name}'");
            Debug.Log($"[SlowManager] Start - Components found: p1Vignette={(p1Vignette!=null)}, p2Vignette={(p2Vignette!=null)}, p1Color={(p1Color!=null)}, p2Color={(p2Color!=null)}");
            Debug.Log($"[SlowManager] Start - Renderers: base={baseRendererIndex}, grayscale={grayscaleRendererIndex}, overlay={overlayRendererIndex}");
            var lData = player1Camera != null ? player1Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            var rData = player2Camera != null ? player2Camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            Debug.Log($"[SlowManager] Start - PostProcessing(L/R): {(lData!=null && lData.renderPostProcessing)}, {(rData!=null && rData.renderPostProcessing)}");
            Debug.Log($"[SlowManager] Start - Overlay layer '{overlaySaturationLayerName}' => {overlaySaturationLayer}");
        }
    }

    private void SetupInputBindings()
    {
        var playerManager = PlayerInputManager.Instance;
        if (playerManager == null)
        {
            return;
        }

        p1Controls = playerManager.Player1Controls;
        p2Controls = playerManager.Player2Controls;

        p1SlowAction = FindActionSafe(p1Controls, "Gameplay/Micro") ?? FindActionSafe(p1Controls, "Gameplay/Slow");
        p1HintAction = FindActionSafe(p1Controls, "Gameplay/ShowHint");
        p1TiltLeftAction = FindActionSafe(p1Controls, "Gameplay/TiltLeft");
        p1TiltRightAction = FindActionSafe(p1Controls, "Gameplay/TiltRight");

        if (p1SlowAction != null)
        {
            p1SlowAction.started += OnP1SlowStarted;
            p1SlowAction.performed += OnP1SlowStarted;
            p1SlowAction.canceled += OnP1SlowCanceled;
        }
        if (p1HintAction != null)
        {
            p1HintAction.started += OnP1HintStarted;
            p1HintAction.performed += OnP1HintStarted;
            p1HintAction.canceled += OnP1HintCanceled;
        }
        if (p1TiltLeftAction != null)
        {
            p1TiltLeftAction.started += OnP1TiltLeftStarted;
            p1TiltLeftAction.performed += OnP1TiltLeftStarted;
            p1TiltLeftAction.canceled += OnP1TiltLeftCanceled;
        }
        if (p1TiltRightAction != null)
        {
            p1TiltRightAction.started += OnP1TiltRightStarted;
            p1TiltRightAction.performed += OnP1TiltRightStarted;
            p1TiltRightAction.canceled += OnP1TiltRightCanceled;
        }

        p2SlowAction = FindActionSafe(p2Controls, "Gameplay/Micro") ?? FindActionSafe(p2Controls, "Gameplay/Slow");
        p2HintAction = FindActionSafe(p2Controls, "Gameplay/ShowHint");
        p2TiltLeftAction = FindActionSafe(p2Controls, "Gameplay/TiltLeft");
        p2TiltRightAction = FindActionSafe(p2Controls, "Gameplay/TiltRight");

        if (p2SlowAction != null)
        {
            p2SlowAction.started += OnP2SlowStarted;
            p2SlowAction.performed += OnP2SlowStarted;
            p2SlowAction.canceled += OnP2SlowCanceled;
        }
        if (p2HintAction != null)
        {
            p2HintAction.started += OnP2HintStarted;
            p2HintAction.performed += OnP2HintStarted;
            p2HintAction.canceled += OnP2HintCanceled;
        }
        if (p2TiltLeftAction != null)
        {
            p2TiltLeftAction.started += OnP2TiltLeftStarted;
            p2TiltLeftAction.performed += OnP2TiltLeftStarted;
            p2TiltLeftAction.canceled += OnP2TiltLeftCanceled;
        }
        if (p2TiltRightAction != null)
        {
            p2TiltRightAction.started += OnP2TiltRightStarted;
            p2TiltRightAction.performed += OnP2TiltRightStarted;
            p2TiltRightAction.canceled += OnP2TiltRightCanceled;
        }
    }

    private void OnP1SlowStarted(InputAction.CallbackContext context) { p1SlowHeld = true; RecomputeSlowMode(); PlayerEvents.Trigger(PlayerAction.SlowMovement); }
    private void OnP1SlowCanceled(InputAction.CallbackContext context) { p1SlowHeld = false; RecomputeSlowMode(); }
    private void OnP1HintStarted(InputAction.CallbackContext context) { p1HintHeld = true; }
    private void OnP1HintCanceled(InputAction.CallbackContext context) { p1HintHeld = false; }
    private void OnP1TiltLeftStarted(InputAction.CallbackContext context) { p1TiltLeftHeld = true; RecomputeSlowMode(); }
    private void OnP1TiltLeftCanceled(InputAction.CallbackContext context) { p1TiltLeftHeld = false; RecomputeSlowMode(); }
    private void OnP1TiltRightStarted(InputAction.CallbackContext context) { p1TiltRightHeld = true; RecomputeSlowMode(); }
    private void OnP1TiltRightCanceled(InputAction.CallbackContext context) { p1TiltRightHeld = false; RecomputeSlowMode(); }

    private void OnP2SlowStarted(InputAction.CallbackContext context) { p2SlowHeld = true; RecomputeSlowMode(); PlayerEvents.Trigger(PlayerAction.SlowMovement); }
    private void OnP2SlowCanceled(InputAction.CallbackContext context) { p2SlowHeld = false; RecomputeSlowMode(); }
    private void OnP2HintStarted(InputAction.CallbackContext context) { p2HintHeld = true; }
    private void OnP2HintCanceled(InputAction.CallbackContext context) { p2HintHeld = false; }
    private void OnP2TiltLeftStarted(InputAction.CallbackContext context) { p2TiltLeftHeld = true; RecomputeSlowMode(); }
    private void OnP2TiltLeftCanceled(InputAction.CallbackContext context) { p2TiltLeftHeld = false; RecomputeSlowMode(); }
    private void OnP2TiltRightStarted(InputAction.CallbackContext context) { p2TiltRightHeld = true; RecomputeSlowMode(); }
    private void OnP2TiltRightCanceled(InputAction.CallbackContext context) { p2TiltRightHeld = false; RecomputeSlowMode(); }

    private static InputAction FindActionSafe(TwoPlayerInput controls, string actionPath)
    {
        if (controls == null || controls.asset == null) return null;
        return controls.asset.FindAction(actionPath, false);
    }

    private void RecomputeSlowMode()
    {
        bool shouldBeActive = p1SlowHeld || p2SlowHeld;
        if (shouldBeActive && !isSlowModeActive)
        {
            ActivateSlowMode();
        }
        else if (!shouldBeActive && isSlowModeActive)
        {
            DeactivateSlowMode();
        }
    }

	private IEnumerable<Transform> EnumerateHintObservers()
	{
		if (players != null && players.Length > 0)
		{
			for (int i = 0; i < players.Length; i++)
			{
				var t = players[i];
				if (t != null) yield return t;
			}
			yield break;
		}

		var controllers = Object.FindObjectsOfType<BasePlayerController>();
		if (controllers != null && controllers.Length > 0)
		{
			for (int i = 0; i < controllers.Length; i++)
			{
				var c = controllers[i];
				if (c != null && c.enabled && c.gameObject.activeInHierarchy) yield return c.transform;
			}
		}

		if (player1Camera != null) yield return player1Camera.transform;
		if (player2Camera != null) yield return player2Camera.transform;
		if (Camera.main != null) yield return Camera.main.transform;
	}

    public void ActivateSlowMode()
    {
        isSlowModeActive = true;
        currentTime = 0f;
        targetTimeScale = slowTimeScale;
    }

    public void DeactivateSlowMode()
    {
        isSlowModeActive = false;
        currentTime = 0f;
        targetTimeScale = 1f;
    }

    void Update()
    {
        var pm = PlayerInputManager.Instance;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (pm != null && pm.CurrentControllerType == PlayerInputManager.ControllerType.SinglePlayer)
        {
            bool shift = kb != null && kb.shiftKey.isPressed;
            bool zKey = kb != null && kb.zKey.isPressed;
            if (pm.CurrPlayerEnum == BasePlayerController.PlayerID.Player1)
            {
                p1SlowHeld = (p1SlowAction != null && p1SlowAction.IsPressed()) || shift;
                p2SlowHeld = false;
                p1HintHeld = (p1HintAction != null && p1HintAction.IsPressed()) || zKey;
                p2HintHeld = false;
                PlayerEvents.Trigger(PlayerAction.SlowMovement);
                //Debug.Log("SlowMovement triggered for Player 1");
            }
            else
            {
                p1SlowHeld = false;
                p2SlowHeld = (p2SlowAction != null && p2SlowAction.IsPressed()) || shift;
                p1HintHeld = false;
                p2HintHeld = (p2HintAction != null && p2HintAction.IsPressed()) || zKey;
                PlayerEvents.Trigger(PlayerAction.SlowMovement);
                //Debug.Log("SlowMovement triggered for Player 2");
            }
        }
        else
        {
            p1SlowHeld = (p1SlowAction != null && p1SlowAction.IsPressed());
            p2SlowHeld = (p2SlowAction != null && p2SlowAction.IsPressed());
            p1HintHeld = (p1HintAction != null && p1HintAction.IsPressed());
            p2HintHeld = (p2HintAction != null && p2HintAction.IsPressed());
        }

        bool anySlow = p1SlowHeld || p2SlowHeld;
            
        wasAnySlowHeld = anySlow;


        RecomputeSlowMode();

        float p1Duration = p1SlowHeld ? fadeInTime : fadeOutTime;
        float p2Duration = p2SlowHeld ? fadeInTime : fadeOutTime;

        float p1Target = p1SlowHeld ? 1f : 0f;
        float p2Target = p2SlowHeld ? 1f : 0f;
        p1EffectWeight = Mathf.MoveTowards(p1EffectWeight, p1Target, Time.unscaledDeltaTime / Mathf.Max(0.0001f, p1Duration));
        p2EffectWeight = Mathf.MoveTowards(p2EffectWeight, p2Target, Time.unscaledDeltaTime / Mathf.Max(0.0001f, p2Duration));

        float t1 = p1EffectWeight * p1EffectWeight * (3f - 2f * p1EffectWeight);
        float t2 = p2EffectWeight * p2EffectWeight * (3f - 2f * p2EffectWeight);

        // Vignette is now handled elsewhere (e.g., PuzzleProximityVignette). Do not touch it here unless explicitly enabled.
        if (controlVignetteInSlow)
        {
            if (p1Vignette != null) p1Vignette.intensity.value = t1 * vignetteIntensity;
            if (p2Vignette != null) p2Vignette.intensity.value = t2 * vignetteIntensity;
        }
        float p1Sat = Mathf.Lerp(0f, -100f, t1);
        float p2Sat = Mathf.Lerp(0f, -100f, t2);
        if (p1Color != null) p1Color.saturation.value = p1Sat;
        if (p2Color != null) p2Color.saturation.value = p2Sat;
        if (verboseLogging)
        {
            //Debug.Log($"[SlowManager] SlowHeld(L/R): {p1SlowHeld}/{p2SlowHeld} | t1={t1:F2}, t2={t2:F2} | satL={p1Sat:F1}, satR={p2Sat:F1}");
        }

        if (debugBypassRendererSwitch)
        {
            ApplyRendererForCamera(player1Camera, baseRendererIndex);
            ApplyRendererForCamera(player2Camera, baseRendererIndex);
        }
        else
        {
            int p1TargetIdx = p1SlowHeld ? grayscaleRendererIndex : baseRendererIndex;
            int p2TargetIdx = p2SlowHeld ? grayscaleRendererIndex : baseRendererIndex;
            if (p1TargetIdx != p1LastAppliedRenderer)
            {
                ApplyRendererForCamera(player1Camera, p1TargetIdx);
                p1LastAppliedRenderer = p1TargetIdx;
                if (verboseLogging) Debug.Log($"[SlowManager] Applied renderer to P1 '{player1Camera?.name}': {p1TargetIdx}");
                PlayerEvents.Trigger(PlayerAction.SlowMovement);
                Debug.Log("SlowMovement triggered for Player 1");
            }
            if (p2TargetIdx != p2LastAppliedRenderer)
            {
                ApplyRendererForCamera(player2Camera, p2TargetIdx);
                p2LastAppliedRenderer = p2TargetIdx;
                if (verboseLogging) Debug.Log($"[SlowManager] Applied renderer to P2 '{player2Camera?.name}': {p2TargetIdx}");
                PlayerEvents.Trigger(PlayerAction.SlowMovement);
                Debug.Log("SlowMovement triggered for Player 2");
            }
        }

        EnsureOverlaySettings(player1Camera);
        EnsureOverlaySettings(player2Camera);

        float timeScaleDuration = isSlowModeActive ? fadeInTime : fadeOutTime;
        Time.timeScale = Mathf.MoveTowards(Time.timeScale, targetTimeScale, Time.unscaledDeltaTime / Mathf.Max(0.0001f, timeScaleDuration));

    }

    private void OnDestroy()
    {
        if (p1SlowAction != null) { p1SlowAction.started -= OnP1SlowStarted; p1SlowAction.canceled -= OnP1SlowCanceled; }
        if (p1HintAction != null) { p1HintAction.started -= OnP1HintStarted; p1HintAction.canceled -= OnP1HintCanceled; }
        if (p1TiltLeftAction != null) { p1TiltLeftAction.started -= OnP1TiltLeftStarted; p1TiltLeftAction.canceled -= OnP1TiltLeftCanceled; }
        if (p1TiltRightAction != null) { p1TiltRightAction.started -= OnP1TiltRightStarted; p1TiltRightAction.canceled -= OnP1TiltRightCanceled; }
        if (p2SlowAction != null) { p2SlowAction.started -= OnP2SlowStarted; p2SlowAction.canceled -= OnP2SlowCanceled; }
        if (p2HintAction != null) { p2HintAction.started -= OnP2HintStarted; p2HintAction.canceled -= OnP2HintCanceled; }
        if (p2TiltLeftAction != null) { p2TiltLeftAction.started -= OnP2TiltLeftStarted; p2TiltLeftAction.canceled -= OnP2TiltLeftCanceled; }
        if (p2TiltRightAction != null) { p2TiltRightAction.started -= OnP2TiltRightStarted; p2TiltRightAction.canceled -= OnP2TiltRightCanceled; }
    }

    private static void ApplyRendererForCamera(Camera cam, int rendererIndex)
    {
        if (cam == null) return;
        var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (data == null) return;
        if (rendererIndex < 0) rendererIndex = 0;
        data.SetRenderer(rendererIndex);
        // Ensure post-processing is enabled so per-camera volumes (vignette/grayscale) can apply
        data.renderPostProcessing = true;
        // Log details for troubleshooting
        Debug.Log($"[SlowManager] SetRenderer cam='{cam.name}', renderType={data.renderType}, index={rendererIndex}, postFX={data.renderPostProcessing}");
        PlayerEvents.Trigger(PlayerAction.SlowMovement);
        Debug.Log("SlowMovement triggered for Player 1");
    }

    private void EnsureOverlaySettings(Camera baseCam)
    {
        if (baseCam == null) return;
        var baseData = baseCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (baseData == null) return;
        var stack = baseData.cameraStack;
        for (int i = 0; i < stack.Count; i++)
        {
            var overlay = stack[i];
            if (overlay == null) continue;
            var overlayData = overlay.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (overlayData == null) continue;
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;
            overlayData.volumeLayerMask = 0;
            overlayData.SetRenderer(overlayRendererIndex);
            if (overlaySaturationLayer != -1)
            {
                overlay.cullingMask = 1 << overlaySaturationLayer;
            }
            if (verboseLogging)
            {
                Debug.Log($"[SlowManager] Overlay setup parent='{baseCam.name}' overlay='{overlay.name}' renderer={overlayRendererIndex} cullMask={overlay.cullingMask}");
            }
        }
    }
}
