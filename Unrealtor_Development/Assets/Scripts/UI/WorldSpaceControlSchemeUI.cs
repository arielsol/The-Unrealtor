using UnityEngine;

public class WorldSpaceControlSchemeUI : MonoBehaviour
{
    [Header("Player 1 (Left) Icons — two-player")]
    [Tooltip("Shown when Player 1 is on Keyboard/Mouse.")]
    [SerializeField] private GameObject keyboardLeft;
    [Tooltip("Shown when Player 1 is on Gamepad.")]
    [SerializeField] private GameObject gamepadLeft;

    [Header("Player 2 (Right) Icons — two-player")]
    [Tooltip("Shown when Player 2 is on Keyboard/Mouse.")]
    [SerializeField] private GameObject keyboardRight;
    [Tooltip("Shown when Player 2 is on Gamepad.")]
    [SerializeField] private GameObject gamepadRight;

    [Header("Single-player — optional left / right")]
    [Tooltip("When any of these are assigned and PlayerInputManager is in SinglePlayer mode, the two-player icons above are hidden and this set is used instead. Uses the active character's device (keyboard vs gamepad) to pick the keyboard pair or gamepad pair.")]
    [SerializeField] private GameObject singlePlayerKeyboardLeft;
    [SerializeField] private GameObject singlePlayerKeyboardRight;
    [SerializeField] private GameObject singlePlayerGamepadLeft;
    [SerializeField] private GameObject singlePlayerGamepadRight;

    [Header("Update Settings")]
    [Tooltip("How often (in seconds) to re-check each player's control scheme. 0 = every frame.")]
    [SerializeField, Min(0f)] private float pollInterval = 0.1f;

    [Tooltip("Force refresh on enable so icons start in the correct state.")]
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    /// <summary>
    /// When set from a pickup row: which player's device to show. Single-player: drives SP custom icons.
    /// Two-player: only that player's left or right column is shown so KBM vs gamepad matches who opened.
    /// When null, single-player falls back to <see cref="PlayerInputManager.CurrPlayerObject"/>; two-player shows both columns.
    /// </summary>
    private BasePlayerController.PlayerID? _deviceContextPlayerId;

    private float _nextPollTime;

    /// <summary>
    /// Pin HUD device icons to a specific player (pickup/dismiss owner). Pass null to clear.
    /// </summary>
    public void SetDeviceContextPlayer(BasePlayerController.PlayerID? playerId)
    {
        _deviceContextPlayerId = playerId;
    }

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            ApplyCurrentState();
            _nextPollTime = Time.unscaledTime + pollInterval;
        }
    }

    private void OnDisable()
    {
        _deviceContextPlayerId = null;
        // Popup rows are toggled via SetActive on parents; ensure hint icons never stay up for a closed pickup or leak into the next open row/instance.
        HideAllIconTargets();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextPollTime) return;
        _nextPollTime = Time.unscaledTime + pollInterval;
        ApplyCurrentState();
    }

    private void ApplyCurrentState()
    {
        // Always start from a blank slate so switching popup rows, sessions, or devices cannot leave the other scheme visible.
        HideAllIconTargets();

        bool activeIsGamepad = false;
        bool useSpCustom = HasAnySinglePlayerIconAssigned()
            && TryResolveSinglePlayerCustomGamepad(out activeIsGamepad);

        if (useSpCustom)
        {
            SetActiveSafe(singlePlayerKeyboardLeft, !activeIsGamepad);
            SetActiveSafe(singlePlayerKeyboardRight, !activeIsGamepad);
            SetActiveSafe(singlePlayerGamepadLeft, activeIsGamepad);
            SetActiveSafe(singlePlayerGamepadRight, activeIsGamepad);

            if (debugLog)
            {
                string ctx = _deviceContextPlayerId.HasValue
                    ? $"contextPlayer={_deviceContextPlayerId.Value}"
                    : "context=CurrPlayer";
                Debug.Log(
                    $"[WorldSpaceControlSchemeUI] {name}: single-player custom icons " +
                    $"{ctx} activeDevice={(activeIsGamepad ? "Gamepad" : "KBM")}",
                    this);
            }
            return;
        }

        bool p1IsGamepad = BasePlayerController.IsPlayerUsingGamepad(BasePlayerController.PlayerID.Player1);
        bool p2IsGamepad = BasePlayerController.IsPlayerUsingGamepad(BasePlayerController.PlayerID.Player2);

        // Pickup / owner context: show only the opener's column so mixed KBM + gamepad matches who is reading this popup.
        if (_deviceContextPlayerId.HasValue
            && PlayerInputManager.Instance != null
            && PlayerInputManager.Instance.CurrentControllerType == PlayerInputManager.ControllerType.TwoPlayer)
        {
            if (_deviceContextPlayerId.Value == BasePlayerController.PlayerID.Player1)
            {
                SetActiveSafe(keyboardLeft, !p1IsGamepad);
                SetActiveSafe(gamepadLeft, p1IsGamepad);
            }
            else
            {
                SetActiveSafe(keyboardRight, !p2IsGamepad);
                SetActiveSafe(gamepadRight, p2IsGamepad);
            }

            if (debugLog)
            {
                bool ctxPad = _deviceContextPlayerId.Value == BasePlayerController.PlayerID.Player1
                    ? p1IsGamepad
                    : p2IsGamepad;
                Debug.Log(
                    $"[WorldSpaceControlSchemeUI] {name}: two-player pickup context={_deviceContextPlayerId.Value} " +
                    $"shownSide={(_deviceContextPlayerId.Value == BasePlayerController.PlayerID.Player1 ? "Left" : "Right")} " +
                    $"device={(ctxPad ? "Gamepad" : "KBM")}",
                    this);
            }
            return;
        }

        SetActiveSafe(keyboardLeft, !p1IsGamepad);
        SetActiveSafe(gamepadLeft, p1IsGamepad);

        SetActiveSafe(keyboardRight, !p2IsGamepad);
        SetActiveSafe(gamepadRight, p2IsGamepad);

        if (debugLog)
        {
            Debug.Log(
                $"[WorldSpaceControlSchemeUI] {name}: two-player P1={(p1IsGamepad ? "Gamepad" : "KBM")} " +
                $"P2={(p2IsGamepad ? "Gamepad" : "KBM")}",
                this);
        }
    }
    
    /// <summary>
    /// Single-player custom icons: device can be pinned to the pickup opener instead of always using
    /// <see cref="PlayerInputManager.CurrPlayerObject"/>.
    /// </summary>
    private bool TryResolveSinglePlayerCustomGamepad(out bool activePlayerIsGamepad)
    {
        activePlayerIsGamepad = false;
        if (PlayerInputManager.Instance == null)
            return false;
        if (PlayerInputManager.Instance.CurrentControllerType != PlayerInputManager.ControllerType.SinglePlayer)
            return false;

        if (_deviceContextPlayerId.HasValue)
            activePlayerIsGamepad = BasePlayerController.IsPlayerUsingGamepad(_deviceContextPlayerId.Value);
        else
        {
            BasePlayerController active = PlayerInputManager.Instance.CurrPlayerObject;
            if (active != null)
                activePlayerIsGamepad = active.IsUsingGamepad();
        }

        return true;
    }

    private bool HasAnySinglePlayerIconAssigned()
    {
        return singlePlayerKeyboardLeft != null || singlePlayerKeyboardRight != null
            || singlePlayerGamepadLeft != null || singlePlayerGamepadRight != null;
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf != active) go.SetActive(active);
    }

    private void HideAllIconTargets()
    {
        SetActiveSafe(keyboardLeft, false);
        SetActiveSafe(gamepadLeft, false);
        SetActiveSafe(keyboardRight, false);
        SetActiveSafe(gamepadRight, false);
        SetActiveSafe(singlePlayerKeyboardLeft, false);
        SetActiveSafe(singlePlayerKeyboardRight, false);
        SetActiveSafe(singlePlayerGamepadLeft, false);
        SetActiveSafe(singlePlayerGamepadRight, false);
    }

    public void ForceRefresh()
    {
        ApplyCurrentState();
        _nextPollTime = Time.unscaledTime + pollInterval;
    }
}
