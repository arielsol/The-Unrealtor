using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class Zoom : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera VCam;
    [SerializeField] private Camera PlayerCamera;

    [Header("Player")]
    [Tooltip("Which player this zoom belongs to; only this player's input will drive zoom.")]
    [SerializeField] private BasePlayerController.PlayerID whichPlayer = BasePlayerController.PlayerID.Player1;

    [Header("Zoom Settings")]
    [SerializeField] private float ZoomLerpSpeed = 10f;
    [SerializeField] private float ZoomRatePerSecond = 60f;
    [SerializeField] private float ScrollRate = 250f;
    [SerializeField] private float MinFOV = 10f;
    [SerializeField] private float MaxFOV = 74f;
    [SerializeField] private float DefaultFOV = 40f;
    private float _targetFov;

    [Header("Controls (Optional)")]
    [Tooltip("Enable mouse wheel/number hotkeys for zoom steps (1,2,5).")]
    [SerializeField] private bool EnableLocalInput = true;

    [Header("Step Triggers")]
    public bool IsMovingLeft;
    public bool IsMovingRight;
    public bool IsMovingCenter;

    private bool _lastLeft;
    private bool _lastRight;
    private bool _lastCenter;

    private void Awake()
    {

        float startFov = 60f;
        if (VCam != null) startFov = VCam.m_Lens.FieldOfView;
        else if (PlayerCamera != null) startFov = PlayerCamera.fieldOfView;
        _targetFov = Mathf.Clamp(startFov, MinFOV, MaxFOV);
        ApplyFovImmediate(_targetFov);
    }

    private void Update()
    {
        return;
        HandleZoomInputContinuous();

        // Only allow local input path in single-player to avoid cross-effects in split-screen
        var manager = PlayerInputManager.Instance;
        bool allowLocalInput = EnableLocalInput
            && manager != null
            && manager.CurrentControllerType == PlayerInputManager.ControllerType.SinglePlayer;
        if (allowLocalInput)
        {
            HandleLocalInput();
        }

        LerpToFov(_targetFov);
    }

    private void HandleZoomInputContinuous()
    {
        if (IsMovingCenter && !_lastCenter)
        {
            ResetZoom();
            PlayerEvents.Trigger(PlayerAction.ResetCam);
            PlayerEvents.TriggerByPlayer(PlayerAction.ResetCam, whichPlayer);
        }
        else
        {
            if (IsMovingLeft)
                _targetFov = Mathf.Clamp(_targetFov + ZoomRatePerSecond * Time.unscaledDeltaTime, MinFOV, MaxFOV);
            if (IsMovingRight)
                _targetFov = Mathf.Clamp(_targetFov - ZoomRatePerSecond * Time.unscaledDeltaTime, MinFOV, MaxFOV);
            if (IsMovingLeft || IsMovingRight)
            {
                PlayerEvents.Trigger(PlayerAction.Rotate);
                PlayerEvents.TriggerByPlayer(PlayerAction.Rotate, whichPlayer);
            }
        }

        _lastLeft = IsMovingLeft;
        _lastRight = IsMovingRight;
        _lastCenter = IsMovingCenter;
    }

    private void HandleLocalInput()
    {
        // This method is only called when single-player (guarded in Update)
        var manager = PlayerInputManager.Instance;
        if (manager == null) return;

        TwoPlayerInput myControls = whichPlayer == BasePlayerController.PlayerID.Player1
            ? manager.Player1Controls
            : manager.Player2Controls;
        if (myControls == null || !myControls.Gameplay.enabled) return;

        // Scroll only for the "current" player
        if (manager.CurrPlayerEnum == whichPlayer)
        {
            float scrollInput = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scrollInput) > 0.0001f)
                _targetFov = Mathf.Clamp(_targetFov - scrollInput * ScrollRate, MinFOV, MaxFOV);
        }

        // Use this player's tilt actions
        if (myControls.Gameplay.TiltLeft.IsPressed())
            _targetFov = Mathf.Clamp(_targetFov + ZoomRatePerSecond * Time.unscaledDeltaTime, MinFOV, MaxFOV);
        if (myControls.Gameplay.TiltRight.IsPressed())
            _targetFov = Mathf.Clamp(_targetFov - ZoomRatePerSecond * Time.unscaledDeltaTime, MinFOV, MaxFOV);
    }

    public void ResetZoom()
    {
        _targetFov = Mathf.Clamp(DefaultFOV, MinFOV, MaxFOV);
    }

    private void LerpToFov(float targetFov)
    {
        if (VCam != null)
        {
            VCam.m_Lens.FieldOfView = Mathf.Lerp(VCam.m_Lens.FieldOfView, targetFov, Time.unscaledDeltaTime * ZoomLerpSpeed);
        }
        else if (PlayerCamera != null)
        {
            PlayerCamera.fieldOfView = Mathf.Lerp(PlayerCamera.fieldOfView, targetFov, Time.unscaledDeltaTime * ZoomLerpSpeed);
        }
    }

    private void ApplyFovImmediate(float fov)
    {
        if (VCam != null) VCam.m_Lens.FieldOfView = fov;
        else if (PlayerCamera != null) PlayerCamera.fieldOfView = fov;
    }
}