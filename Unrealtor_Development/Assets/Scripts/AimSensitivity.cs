using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

[DisallowMultipleComponent]
public class VCamLookDriver : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Input System action that delivers Vector2 look (x = yaw, y = pitch).")]
    public InputActionReference lookAction;

    [Header("Sensitivity (per camera/player)")]
    [Range(0.05f, 10f)] public float horizontalSensitivity = 1.0f;
    [Range(0.05f, 10f)] public float verticalSensitivity = 1.0f;
    public bool linkXY = true;        // if true, scroll changes both equally
    public bool invertY = false;

    [Header("Scroll Tuning")]
    [Tooltip("How much sensitivity changes per wheel tick (scaled by scroll magnitude).")]
    public float scrollStep = 0.05f;
    public float minSensitivity = 0.05f;
    public float maxSensitivity = 10f;

    [Header("Feel")]
    [Tooltip("Extra scale for gamepads (sticks).")]
    public float gamepadBoost = 2.0f;
    [Tooltip("Multiply by deltaTime (nice for sticks).")]
    public bool scaleByDeltaTime = true;

    [Header("HUD")]
    public bool showHud = true;
    public Vector2 hudOffset = new Vector2(8, 8); // top-left padding
    public int hudFontSize = 14;

    CinemachinePOV pov;
    InputAction look;
    float lastHudValue; // for a tiny bit of stability in the label

    void Awake()
    {
        var vcam = GetComponent<CinemachineVirtualCamera>();
        pov = vcam.GetCinemachineComponent<CinemachinePOV>();
        if (pov == null)
        {
            Debug.LogError("[VCamLookDriver] Add a CinemachinePOV to this vcam.");
            enabled = false;
            return;
        }

        // We’ll drive POV manually.
        pov.m_HorizontalAxis.m_InputAxisName = string.Empty;
        pov.m_VerticalAxis.m_InputAxisName = string.Empty;

        if (lookAction != null) look = lookAction.action;

        // keep XY in sync if linked
        if (linkXY) verticalSensitivity = horizontalSensitivity;

        ClampSens();
    }

    void OnEnable()
    {
        if (look != null) look.Enable();
    }

    void OnDisable()
    {
        if (look != null) look.Disable();
    }

    void Update()
    {
        if (look == null || pov == null) return;

        // --- 1) Adjust sensitivity via scroll wheel ---
        float scrollY = 0f;
        if (Mouse.current != null)
        {
            // Positive when scrolling up on most systems
            scrollY = Mouse.current.scroll.ReadValue().y;
        }

        if (Mathf.Abs(scrollY) > 0.01f)
        {
            float delta = scrollStep * Mathf.Sign(scrollY) * Mathf.Clamp(Mathf.Abs(scrollY) / 120f, 0.5f, 2f);
            // 120 is a common 'tick' on some platforms; this keeps behavior reasonable across mice
            horizontalSensitivity += delta;
            if (linkXY) verticalSensitivity = horizontalSensitivity;
            else verticalSensitivity += delta;

            ClampSens();
            lastHudValue = GetDisplayedSensitivity();
        }

        // --- 2) Apply look input with current sensitivity ---
        Vector2 li = look.ReadValue<Vector2>();

        bool usingGamepad = Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame;
        float dt = scaleByDeltaTime ? Time.deltaTime : 1f;
        float pad = usingGamepad ? gamepadBoost : 1f;

        float dx = li.x * horizontalSensitivity * pad * dt * 100f;
        float dy = li.y * verticalSensitivity * pad * dt * 100f;
        if (invertY) dy = -dy;

        pov.m_HorizontalAxis.Value += dx;
        pov.m_VerticalAxis.Value -= dy;
    }

    void ClampSens()
    {
        horizontalSensitivity = Mathf.Clamp(horizontalSensitivity, minSensitivity, maxSensitivity);
        verticalSensitivity = Mathf.Clamp(verticalSensitivity, minSensitivity, maxSensitivity);
    }

    float GetDisplayedSensitivity()
    {
        // If linked, show one number; otherwise show average
        if (linkXY) return horizontalSensitivity;
        return (horizontalSensitivity + verticalSensitivity) * 0.5f;
    }

    // --- 3) Simple top-left HUD without needing a Canvas ---
    void OnGUI()
    {
        if (!showHud) return;

        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = hudFontSize;
        style.alignment = TextAnchor.UpperLeft;
        style.richText = false;

        float display = GetDisplayedSensitivity();
        // lightly smooth the readout to reduce flicker on rapid scrolls
        lastHudValue = Mathf.Lerp(lastHudValue, display, 0.3f);

        Rect rect = new Rect(hudOffset.x, hudOffset.y, 400, 30);
        GUI.Label(rect, $"Aim Speed: {lastHudValue:0.00}", style);
    }
}

