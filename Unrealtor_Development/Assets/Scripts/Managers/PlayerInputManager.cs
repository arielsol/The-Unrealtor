using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using System.Collections;
using System.Collections.Generic;
using UnityCommunity.UnitySingleton;
using System;
using System.Linq;
using Cinemachine;

[DefaultExecutionOrder(-1000)]
public class PlayerInputManager : MonoSingleton<PlayerInputManager>
{
    private static PlayerInputManager _cachedInstance;

    public new static PlayerInputManager Instance
    {
        get
        {
            if (_cachedInstance != null)
                return _cachedInstance;

            _cachedInstance = FindBestExistingInstance();
            if (_cachedInstance != null)
                return _cachedInstance;
            return null;
        }
    }

    public enum ControllerType
    {
        SinglePlayer,
        TwoPlayer
    }

    [Header("Player Controller")]
    [SerializeField] private ControllerType CurrControllerType;
    public ControllerType CurrentControllerType => CurrControllerType;
    public BasePlayerController Player1;
    public BasePlayerController Player2;

    [Header("Debug Info")]
    [SerializeField] private string player1DeviceDebug;
    [SerializeField] private string player2DeviceDebug;
    [SerializeField] private string player1ControlScheme;
    [SerializeField] private string player2ControlScheme;
    [SerializeField] private string currentActivePlayer;
    [SerializeField] private bool deviceAssignmentCompleted;
    [SerializeField] private bool player1ControlsEnabled;
    [SerializeField] private bool player2ControlsEnabled;
    [SerializeField] private string jumpInputDebug;
    [SerializeField] private string moveInputDebug;
    [SerializeField] private string inputUserDebug;

    public BasePlayerController.PlayerID CurrPlayerEnum { get; private set; }
    public BasePlayerController CurrPlayerObject;

    public TwoPlayerInput Controls { get; private set; }
    public TwoPlayerInput Player1Controls { get; private set; }
    public TwoPlayerInput Player2Controls { get; private set; }
    private InputAction SwitchPlayer;
    private InputAction Escape;
    private InputAction SelectWindow;

    [NonSerialized] public bool IsCursorUsed = true;

    private InputDevice player1Device = null;
    private InputDevice player2Device = null;
    private bool isDeviceAssignmentComplete = false;

    public InputDevice Player1Device => player1Device;
    public InputDevice Player2Device => player2Device;

    private InputUser player1User;
    private InputUser player2User;

    private static PlayerInputManager FindBestExistingInstance()
    {
        PlayerInputManager firstLoaded = null;
        foreach (var candidate in Resources.FindObjectsOfTypeAll<PlayerInputManager>())
        {
            if (candidate == null)
                continue;
            if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                continue;

            if (firstLoaded == null)
                firstLoaded = candidate;

            if (candidate.Player1 != null || candidate.Player2 != null)
                return candidate;
        }

        return firstLoaded;
    }

    /// <summary>
    /// Active-player border HUD — only when the menu session is 1-player (single-character Tab mode).
    /// Every two-player configuration leaves this off.
    /// </summary>
    public bool ShouldShowSinglePlayerActiveBorder()
    {
        return CurrControllerType == ControllerType.SinglePlayer;
    }

    /// <summary>
    /// Tab to switch characters — same rule as the border: only 1-player mode. Never in two-player.
    /// </summary>
    public bool UseTabToSwitchActiveCharacter()
    {
        return CurrControllerType == ControllerType.SinglePlayer;
    }

    public void UpdatePlayerActiveUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ApplySinglePlayerActiveIndicators(
                ShouldShowSinglePlayerActiveBorder(),
                CurrPlayerEnum == BasePlayerController.PlayerID.Player1);
        }

        UpdateSwitchPlayerActionEnabled();
    }

    private void UpdateSwitchPlayerActionEnabled()
    {
        if (SwitchPlayer == null) return;
        if (UseTabToSwitchActiveCharacter())
            SwitchPlayer.Enable();
        else
            SwitchPlayer.Disable();
    }

    /// <summary>
    /// Prototype / BPS scenes without the start menu: treat the session as co-op so the single-player active HUD
    /// and Tab switch stay off. Re-runs two-player device setup when <see cref="Player1"/> and <see cref="Player2"/> exist.
    /// </summary>
    public void ApplyTwoPlayerModeForBpsOrLegacyScene()
    {
        CurrControllerType = ControllerType.TwoPlayer;
        if (Player1 != null && Player2 != null)
            SetupInitialTwoPlayerMode();
        UpdatePlayerActiveUI();
    }

    protected override void Awake()
    {
        if (_cachedInstance == null || _cachedInstance == this)
            _cachedInstance = this;

        base.Awake();

        _cachedInstance = this;

        Controls = new TwoPlayerInput();
        Player1Controls = new TwoPlayerInput();
        Player2Controls = new TwoPlayerInput();

        Controls.Enable();

        SwitchPlayer = Controls.Gameplay.SwitchPlayerSinglePlayer;
        SwitchPlayer.performed += SwitchPlayerPressed;

        Escape = Controls.Gameplay.Escape;
        Escape.Enable();
        Escape.performed += OnEscape;

        SelectWindow = Controls.Gameplay.SelectWindow;
        SelectWindow.Enable();
        SelectWindow.performed += OnLeftMouseClick;

        Player1 = GameObject.Find("Player1_Left").GetComponent<BasePlayerController>();
        Player2 = GameObject.Find("Player2_Right").GetComponent<BasePlayerController>();

        CurrPlayerObject = Player1;
        Cursor.lockState = CursorLockMode.Locked;

        InitializeDeviceAssignment();

        //UpdatePlayerActiveUI();

        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnEnable()
    {
        GameEvents.OnCutsceneStateChanged += OnCutsceneStateChanged;
    }

    private void OnCutsceneStateChanged(GameManager.CutsceneState newState)
    {
        if (newState == GameManager.CutsceneState.GAMEPLAY)
            UpdatePlayerActiveUI();
    }

    private void Start()
    {
        UpdatePlayerActiveUI();
    }

    private void InitializeDeviceAssignment()
    {
        //getting controller type from playerprefs
        int pref = PlayerPrefs.GetInt("PlayerCount", 0);
        if (pref == 1)
        {
            Debug.Log("setting pref 1");
            CurrControllerType = ControllerType.SinglePlayer;
        }
        else if (pref == 2)
        {
            Debug.Log("setting pref 2");
            CurrControllerType = ControllerType.TwoPlayer;
        }

        Player1Controls.Enable();
        Player2Controls.Enable();
        if (CurrControllerType == ControllerType.SinglePlayer)
        {
            /*Player1Controls.Enable();
            Player2Controls.Disable();*/

            string prefscheme = PlayerPrefs.GetString("Player1Control", "");
            Debug.Log("pref: " + prefscheme);
            if (prefscheme == "Controller")
            {
                Debug.Log("gamepad here");
                SetupControlScheme(Player1Controls, "SinglePlayerGamepad", Keyboard.current, Mouse.current);
            }
            else
            {
                SetupControlScheme(Player1Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
            }
            /*Player1.SetLookPaused(false);
            Player2.SetLookPaused(true);*/

            CurrPlayerEnum = BasePlayerController.PlayerID.Player1;
            isDeviceAssignmentComplete = true;
        }
        else
        {
            /*Player1Controls.Enable();
            Player2Controls.Enable();*/
            SetupInitialTwoPlayerMode();
            CurrPlayerEnum = BasePlayerController.PlayerID.Player1;
        }

        UpdatePlayerActiveUI();
    }

    private void SetupInitialTwoPlayerMode()
    {
        // Mixed local co-op should prefer keyboard + one gamepad when both are present.
        if (Gamepad.all.Count == 1 && Keyboard.current != null)
        {
            string pref = PlayerPrefs.GetString("Player1Control", "");
            if (pref == "Controller")
            {
                SetupControlScheme(Player1Controls, "Gamepad", Gamepad.all[0]);
                SetupControlScheme(Player2Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
            }
            else
            {
                SetupControlScheme(Player1Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
                SetupControlScheme(Player2Controls, "Gamepad", Gamepad.all[0]);
            }
        }
        // Split keyboard co-op: P1 WASD, P2 Arrows (+ mouse look when present). No Tab — two-player never uses Tab.
        else if (Gamepad.all.Count >= 2)
        {
            SetupControlScheme(Player1Controls, "Gamepad", Gamepad.all[0]);
            SetupControlScheme(Player2Controls, "Gamepad", Gamepad.all[1]);
            Debug.Log("Two gamepad mode (no keyboard)");
        }
        else if (Keyboard.current != null)
        {
            SetupControlScheme(Player1Controls, "WASD", Keyboard.current);
            if (Mouse.current != null)
                SetupControlScheme(Player2Controls, "Arrows", Keyboard.current, Mouse.current);
            else
                SetupControlScheme(Player2Controls, "Arrows", Keyboard.current);
        }
        else if (Gamepad.all.Count == 1)
        {
            string pref = PlayerPrefs.GetString("Player1Control", "");
            if (pref == "Controller")
            {
                SetupControlScheme(Player1Controls, "Gamepad", Gamepad.all[0]);
                if (Keyboard.current != null)
                    SetupControlScheme(Player2Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
            }
            else
            {
                if (Keyboard.current != null)
                    SetupControlScheme(Player1Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
                SetupControlScheme(Player2Controls, "Gamepad", Gamepad.all[0]);
            }
        }

        isDeviceAssignmentComplete = true;

        if (Player1 != null && Player2 != null)
            StartCoroutine(RefreshCameraControlsDelayed());
    }

    private IEnumerator RefreshCameraControlsDelayed()
    {
        yield return new WaitForEndOfFrame();

        bool player1CamEnabled = Player1.enabled;
        bool player2CamEnabled = Player2.enabled;

        Player1.SetLookPaused(player1CamEnabled);
        Player2.SetLookPaused(player2CamEnabled);

        Debug.Log("Camera controls refreshed after device assignment");
    }

    private void SetupControlScheme(TwoPlayerInput playerControls, string schemeName, params InputDevice[] devices)
    {
        if (devices != null && devices.Length > 0 && devices[0] != null)
        {
            playerControls.bindingMask = InputBinding.MaskByGroup(schemeName);
            playerControls.devices = devices;

            if (playerControls == Player1Controls)
            {
                player1Device = devices[0];
                SetupInputUser(ref player1User, playerControls, devices, 0);

                //set camera input mode
                if (devices.Contains(Mouse.current))
                {
                    CinemachinePOV pov = Player1.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
                    pov.m_HorizontalAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
                    pov.m_VerticalAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
                }
                else
                {
                    CinemachinePOV pov = Player1.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
                    pov.m_HorizontalAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
                    pov.m_VerticalAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
                }
                Player1.UpdateCameraSensitivity();
            }
            else if (playerControls == Player2Controls)
            {
                player2Device = devices[0];
                SetupInputUser(ref player2User, playerControls, devices, 1);

                //set camera input mode
                if (devices.Contains(Mouse.current))
                {
                    CinemachinePOV pov = Player2.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
                    pov.m_HorizontalAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
                    pov.m_VerticalAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
                }
                else
                {
                    CinemachinePOV pov = Player2.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
                    pov.m_HorizontalAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
                    pov.m_VerticalAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
                }
                Player2.UpdateCameraSensitivity();
            }

            Debug.Log($"SetupControlScheme: {schemeName}");
        }
    }

    private void SetupInputUser(ref InputUser user, TwoPlayerInput controls, InputDevice[] devices, int playerIndex)
    {
        if (user.valid)
        {
            foreach (var device in user.pairedDevices)
                InputUser.PerformPairingWithDevice(device, user: default);
        }
        else
        {
            user = InputUser.CreateUserWithoutPairedDevices();
        }

        foreach (var device in devices)
            InputUser.PerformPairingWithDevice(device, user);

        user.AssociateActionsWithUser(controls);
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added)
            HandleDeviceAdded(device);
        else if (change == InputDeviceChange.Removed)
            HandleDeviceRemoved(device);
    }

    private void HandleDeviceAdded(InputDevice device)
    {
        if (device is Gamepad gamepad && CurrControllerType == ControllerType.TwoPlayer)
        {
            // Match SetupInitialTwoPlayerMode: always bind P1 -> pad 0, P2 -> pad 1 when two are present.
            // Never skip P2 when they were already on pad 0 — otherwise both users share one gamepad.
            if (Gamepad.all.Count >= 2)
            {
                if (Keyboard.current != null)
                    return;

                SetupControlScheme(Player1Controls, "Gamepad", Gamepad.all[0]);
                SetupControlScheme(Player2Controls, "Gamepad", Gamepad.all[1]);
                if (Player1 != null && Player2 != null)
                    StartCoroutine(RefreshCameraControlsDelayed());
                return;
            }

            bool p1GetsController = PlayerPrefs.GetString("Player1Control", "") == "Controller";
            if (p1GetsController && !(player1Device is Gamepad))
            {
                SetupControlScheme(Player1Controls, "Gamepad", gamepad);
                if (Keyboard.current != null)
                    SetupControlScheme(Player2Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
            }
            else if (!p1GetsController && !(player2Device is Gamepad))
            {
                if (Keyboard.current != null)
                    SetupControlScheme(Player1Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
                SetupControlScheme(Player2Controls, "Gamepad", gamepad);
            }
        }
    }

    private void HandleDeviceRemoved(InputDevice device)
    {
        if (device == player1Device)
        {
            player1Device = null;
            if (Keyboard.current != null)
                SetupControlScheme(Player1Controls, "WASD", Keyboard.current);
        }
        else if (device == player2Device)
        {
            player2Device = null;
            if (Keyboard.current != null)
            {
                if (Mouse.current != null)
                    SetupControlScheme(Player2Controls, "Arrows", Keyboard.current, Mouse.current);
                else
                    SetupControlScheme(Player2Controls, "Arrows", Keyboard.current);
            }
        }
    }

    private void Update()
    {
        UpdateDebugInfo();
    }

    private void UpdateDebugInfo()
    {
        player1DeviceDebug = player1Device?.displayName ?? "None";
        player2DeviceDebug = player2Device?.displayName ?? "None";

        player1ControlScheme = GetControlSchemeDebugInfo(Player1Controls);
        player2ControlScheme = GetControlSchemeDebugInfo(Player2Controls);

        currentActivePlayer = CurrPlayerEnum.ToString();

        deviceAssignmentCompleted = isDeviceAssignmentComplete;

        player1ControlsEnabled = Player1Controls?.Gameplay.enabled ?? false;
        player2ControlsEnabled = Player2Controls?.Gameplay.enabled ?? false;

        var p1Jump = Player1Controls?.Gameplay.Jump.ReadValue<float>() ?? 0f;
        var p2Jump = Player2Controls?.Gameplay.Jump.ReadValue<float>() ?? 0f;

        jumpInputDebug = $"P1: {p1Jump:F2}, P2: {p2Jump:F2}";

        var p1Move = Player1Controls?.Gameplay.Move.ReadValue<Vector2>() ?? Vector2.zero;
        var p2Move = Player2Controls?.Gameplay.Move.ReadValue<Vector2>() ?? Vector2.zero;

        moveInputDebug = $"P1: ({p1Move.x:F1},{p1Move.y:F1}), P2: ({p2Move.x:F1})";

        var p1Valid = player1User.valid ? "Valid" : "Invalid";
        var p2Valid = player2User.valid ? "Valid" : "Invalid";

        inputUserDebug = $"P1: {p1Valid}, P2: {p2Valid}";
    }

    private string GetControlSchemeDebugInfo(TwoPlayerInput playerControls)
    {
        if (playerControls?.bindingMask != null)
        {
            var mask = playerControls.bindingMask.Value;
            if (!string.IsNullOrEmpty(mask.groups))
                return mask.groups;
        }
        return "No Scheme";
    }

    private void SwitchPlayerPressed(InputAction.CallbackContext context)
    {
        if (!UseTabToSwitchActiveCharacter())
            return;

        PlayerEvents.Trigger(PlayerAction.SwitchPlayer);

        if (CurrPlayerEnum == BasePlayerController.PlayerID.Player1)
        {
            //set curr player to player2
            CurrPlayerEnum = BasePlayerController.PlayerID.Player2;
            CurrPlayerObject = Player2;

            /*Player1Controls.Gameplay.Disable();
            Player2Controls.Gameplay.Enable();*/
            SetupControlScheme(Player2Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);

            Player1.SetAllInputPaused(true);
            Player2.SetAllInputPaused(false);
            //Player1.EnablePlayerCameraMove(false);
            //Player2.EnablePlayerCameraMove(true);
        }
        else
        {
            CurrPlayerEnum = BasePlayerController.PlayerID.Player1;
            CurrPlayerObject = Player1;

            /*Player2Controls.Gameplay.Disable();
            Player1Controls.Gameplay.Enable();*/
            SetupControlScheme(Player1Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);

            Player1.SetAllInputPaused(false);
            Player2.SetAllInputPaused(true);
            /*Player1.EnablePlayerCameraMove(true);
            Player2.EnablePlayerCameraMove(false);*/
        }
        SaveInputData();
        UpdatePlayerActiveUI();

        PlayerEvents.Trigger(PlayerAction.SwitchPlayer);
    }

    public void RestorePlayers()
    {
        RestorePlayerInputFromOldState(Player1);
        RestorePlayerInputFromOldState(Player2);

        if (!UseTabToSwitchActiveCharacter())
        {
            UpdatePlayerActiveUI();
            return;
        }

        if (CurrPlayerEnum == BasePlayerController.PlayerID.Player2)
        {
            //set curr player to player2
            CurrPlayerEnum = BasePlayerController.PlayerID.Player2;
            CurrPlayerObject = Player2;

            //SetupControlScheme(Player2Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);

        }
        else
        {
            CurrPlayerEnum = BasePlayerController.PlayerID.Player1;
            CurrPlayerObject = Player1;

            //SetupControlScheme(Player1Controls, "SinglePlayerWASD", Keyboard.current, Mouse.current);
        }

        UpdatePlayerActiveUI();

        /*Debug.Log("Restoring input data. Player 1: look = " + Player1.OldInputData.IsLookPaused + " move = " + Player1.OldInputData.IsMovePaused
                  + " Player 2: look = " + Player2.OldInputData.IsLookPaused + " move = " + Player2.OldInputData.IsMovePaused);
                  */

    }

    /// <summary>
    /// SelectWindow (mouse 1 / gamepad B) calls <see cref="RestorePlayers"/> to exit cursor/UI focus.
    /// That must not override the frozen move/look state while a pickup clue has the player locked in <see cref="UIManager"/>.
    /// </summary>
    private static void RestorePlayerInputFromOldState(BasePlayerController player)
    {
        if (player == null)
            return;
        if (UIManager.Instance != null &&
            UIManager.Instance.IsPlayerPickupPopupLocked(player.playerID))
            return;

        player.SetLookPaused(player.OldInputData.IsLookPaused);
        player.SetMovePaused(player.OldInputData.IsMovePaused);
    }

    private void OnEscape(InputAction.CallbackContext context)
    {
        IsCursorUsed = true;
        /*SaveInputData();
        if (CurrControllerType == ControllerType.SinglePlayer)
        {
            CurrPlayerObject.SetLookPaused(true);
        }
        else
        {
            Player1.SetLookPaused(true);
            Player2.SetLookPaused(true);
        }*/
    }

    private void OnLeftMouseClick(InputAction.CallbackContext context)
    {
        if (GameManager.Instance != null &&
            GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE)
        {
            return;
        }

        IsCursorUsed = false;

        RestorePlayers();
        if (CurrControllerType == ControllerType.SinglePlayer)
        {
            //CurrPlayerObject.SetLookPaused(false);

        }
        else
        {
            /*Player1.EnablePlayerCameraMove(true);
            Player2.EnablePlayerCameraMove(true);*/
            //PlayerInputManager.Instance.SetAllLookPaused(false);
        }
    }

    public string GetPlayer1InputType()
    {
        return GetInputTypeForUIHints(Player1Controls, player1User, player1Device);
    }

    public string GetPlayer2InputType()
    {
        return GetInputTypeForUIHints(Player2Controls, player2User, player2Device);
    }

    /// <summary>
    /// bindingMask.groups is often "Gamepad", but can be ";Gamepad" or multiple groups — parse tokens.
    /// </summary>
    private static bool BindingMaskGroupsIndicateGamepad(string scheme)
    {
        if (string.IsNullOrWhiteSpace(scheme) || string.Equals(scheme, "No Scheme", StringComparison.OrdinalIgnoreCase))
            return false;
        foreach (var segment in scheme.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var g = segment.Trim();
            if (string.Equals(g, "Gamepad", StringComparison.OrdinalIgnoreCase)
                || string.Equals(g, "SinglePlayerGamepad", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Classifies input for per-player HUD (e.g. interact_KBM vs interact_Gamepad). In two-player,
    /// the active control scheme is authoritative so UI matches bindings even if InputUser pairing
    /// is briefly invalid or gamepads hot-plug.
    /// </summary>
    private string GetInputTypeForUIHints(TwoPlayerInput controls, InputUser user, InputDevice primaryDevice)
    {
        string scheme = GetControlSchemeDebugInfo(controls);
        bool schemeIsGamepad = BindingMaskGroupsIndicateGamepad(scheme);
        bool hasNamedScheme = !string.IsNullOrWhiteSpace(scheme)
            && !string.Equals(scheme, "No Scheme", StringComparison.OrdinalIgnoreCase);

        if (CurrControllerType == ControllerType.TwoPlayer && hasNamedScheme)
            return schemeIsGamepad ? "Gamepad" : "Keyboard/Mouse";

        if (CurrControllerType == ControllerType.TwoPlayer)
        {
            string fromUser = GetInputType(user);
            if (fromUser != "None")
                return fromUser;
            if (primaryDevice is Gamepad)
                return "Gamepad";
            return "Keyboard/Mouse";
        }

        string fromUserSp = GetInputType(user);
        if (fromUserSp != "None")
            return fromUserSp;
        if (primaryDevice is Gamepad)
            return "Gamepad";
        return schemeIsGamepad ? "Gamepad" : "Keyboard/Mouse";
    }

    /// <summary>
    /// Assigns devices to a player and enables the player's controls.
    /// </summary>
    /// <param name="playerID">The player ID to assign devices to.</param>
    /// <param name="schemeName">The control scheme name to use.</param>
    /// <param name="devices">The devices to assign to the player.</param>
    public void AssignPlayerDevices(BasePlayerController.PlayerID playerID, string schemeName, params InputDevice[] devices)
    {
        if (playerID == BasePlayerController.PlayerID.Player1)
        {
            Player1Controls.Enable();
            SetupControlScheme(Player1Controls, schemeName, devices);
        }
        else
        {
            Player2Controls.Enable();
            SetupControlScheme(Player2Controls, schemeName, devices);
        }

        isDeviceAssignmentComplete = true;

        if (Player1 != null && Player2 != null)
            StartCoroutine(RefreshCameraControlsDelayed());
    }

    public List<InputDevice> GetAvailableDevices()
    {
        var devices = new List<InputDevice>();

        if (Keyboard.current != null)
            devices.Add(Keyboard.current);
        if (Mouse.current != null)
            devices.Add(Mouse.current);

        devices.AddRange(Gamepad.all);
        return devices;
    }

    public List<string> GetAvailableControlSchemes(BasePlayerController.PlayerID playerID)
    {
        var schemes = new List<string>();

        if (Gamepad.all.Count > 0)
            schemes.Add("Gamepad");

        if (Keyboard.current != null)
        {
            if (CurrControllerType == ControllerType.SinglePlayer)
            {
                schemes.Add("SinglePlayerWASD");
            }
            else
            {
                schemes.Add(playerID == BasePlayerController.PlayerID.Player1 ? "WASD" : "Arrows");
            }
        }

        return schemes;
    }

    /// <summary>
    /// Gets the current control scheme name for Player 1
    /// </summary>
    public string Player1ControlScheme => GetControlSchemeDebugInfo(Player1Controls);

    /// <summary>
    /// Gets the current control scheme name for Player 2
    /// </summary>
    public string Player2ControlScheme => GetControlSchemeDebugInfo(Player2Controls);

    /// <summary>
    /// Gets the current control scheme name for the active player
    /// </summary>
    public string CurrentPlayerControlScheme => CurrPlayerEnum == BasePlayerController.PlayerID.Player1
        ? Player1ControlScheme
        : Player2ControlScheme;

    private string GetInputType(InputUser user)
    {
        if (!user.valid || user.pairedDevices.Count == 0)
            return "None";

        bool hasGamepad = user.pairedDevices.Any(d => d is Gamepad);
        bool hasKeyboard = user.pairedDevices.Any(d => d is Keyboard);
        bool hasMouse = user.pairedDevices.Any(d => d is Mouse);

        if (hasGamepad) return "Gamepad";
        if (hasKeyboard || hasMouse) return "Keyboard/Mouse";
        return "Unknown";
    }

    private void OnDisable()
    {
        if (_cachedInstance == this)
            _cachedInstance = null;

        GameEvents.OnCutsceneStateChanged -= OnCutsceneStateChanged;
        InputSystem.onDeviceChange -= OnDeviceChange;

        if (player1User.valid)
            player1User.UnpairDevicesAndRemoveUser();
        if (player2User.valid)
            player2User.UnpairDevicesAndRemoveUser();

        SwitchPlayer?.Disable();
        Escape?.Disable();
        SelectWindow?.Disable();

        Controls?.Disable();
        Player1Controls?.Disable();
        Player2Controls?.Disable();
    }

    /*public void EnableAllLookMove(bool isEnabled)
    {
        SetAllMovePaused(isEnabled);
        SetAllLookPaused(isEnabled);
        if (isEnabled)
        {
            Controls.Gameplay.SwitchPlayerSinglePlayer.Enable();

        }
        else
        {
            Controls.Gameplay.SwitchPlayerSinglePlayer.Disable();
        }
    }*/

    public void SetAllLookPaused(bool isPaused)
    {
        Player1.SetLookPaused(isPaused);
        Player2.SetLookPaused(isPaused);
        /*if (isEnabled)
        {
            Controls.Gameplay.Look.Enable();
            Player1Controls.Gameplay.Look.Enable();
            Player2Controls.Gameplay.Look.Enable();

            switch (CurrPlayerEnum)
            {
                case BasePlayerController.PlayerID.Player1:
                    Player1.EnablePlayerCameraMove(true);
                    break;
                case BasePlayerController.PlayerID.Player2:
                    Player2.EnablePlayerCameraMove(true);
                    break;
            }
        }
        else
        {
            Controls.Gameplay.Look.Disable();
            Player1Controls.Gameplay.Look.Disable();
            Player2Controls.Gameplay.Look.Disable();
            Player2.EnablePlayerCameraMove(false);
            Player2.EnablePlayerCameraMove(false);
        }*/
    }
    public void SetAllMovePaused(bool isPaused)
    {
        Player1.SetMovePaused(isPaused);
        Player2.SetMovePaused(isPaused);

        /*if (isPaused)
        {
            Controls.Gameplay.Move.Enable(); //movement &inputs
            Player1Controls.Gameplay.Move.Enable();
            Player2Controls.Gameplay.Move.Enable();
        }
        else
        {
            Controls.Gameplay.Move.Disable(); 
            Player1Controls.Gameplay.Move.Disable();
            Player2Controls.Gameplay.Move.Disable();
        }*/
    }

    public void EnableTutorialLook(bool isEnabled, float minAngle1, float maxAngle1, float minAngle2, float maxAngle2)
    {
        if (isEnabled)
        {
            //limit look
            CinemachinePOV povL = Player1.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
            povL.m_HorizontalAxis.m_MaxValue = maxAngle1;
            povL.m_HorizontalAxis.m_MinValue = minAngle1;
            povL.m_VerticalAxis.m_MaxValue = 0;
            povL.m_VerticalAxis.m_MinValue = 0;
            povL.m_HorizontalAxis.m_Wrap = false;

            CinemachinePOV povR = Player2.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
            povR.m_HorizontalAxis.m_MaxValue = maxAngle2;
            povR.m_HorizontalAxis.m_MinValue = minAngle2;
            povR.m_VerticalAxis.m_MaxValue = 0;
            povR.m_VerticalAxis.m_MinValue = 0;
            povR.m_HorizontalAxis.m_Wrap = false;
        }

        //Possible issue here; these values are hard-coded and won't reflect any editor changes made to the camera
        else
        {
            CinemachinePOV povL = Player1.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
            povL.m_HorizontalAxis.m_MaxValue = 180;
            povL.m_HorizontalAxis.m_MinValue = -180;
            povL.m_VerticalAxis.m_MaxValue = 70;
            povL.m_VerticalAxis.m_MinValue = -70;
            povL.m_HorizontalAxis.m_Wrap = true;

            CinemachinePOV povR = Player2.PlayerMoveCam.GetCinemachineComponent<CinemachinePOV>();
            povR.m_HorizontalAxis.m_MaxValue = 180;
            povR.m_HorizontalAxis.m_MinValue = -180;
            povR.m_VerticalAxis.m_MaxValue = 70;
            povR.m_VerticalAxis.m_MinValue = -70;
            povR.m_HorizontalAxis.m_Wrap = true;

            //flip player cameras around
            povL.m_HorizontalAxis.Value += -90;
            povR.m_HorizontalAxis.Value += 90;
        }
    }

    /*/// <summary>
    /// Pauses input for both players (movement and camera).
    /// </summary>
    public void PauseAllPlayersInput()
    {
        //DONT USE
        Controls.Gameplay.Look.Disable();
        Controls.Gameplay.SwitchPlayerSinglePlayer.Disable();
        if (Player1 != null)
        {
            Player1.PauseInput();
            Player1Controls.Gameplay.Look.Disable();

        }
        if (Player2 != null)
        {
            Player2.PauseInput();
            Player2Controls.Gameplay.Look.Disable();
        }
    }

    /// <summary>
    /// Resumes input for both players (movement and camera).
    /// </summary>
    public void ResumeAllPlayersInput()
    {
        //DONT USE
        Controls.Gameplay.Look.Enable();
        Controls.Gameplay.SwitchPlayerSinglePlayer.Enable();
        if (Player1 != null)
        {
            //Player1.ResumeInput();
            Player1Controls.Gameplay.Look.Enable();
        }
        if (Player2 != null)
        {
            //Player2.ResumeInput();
            Player2Controls.Gameplay.Look.Enable();
        }
    }*/

    /// <summary>
    /// Sets input pause state for both players. True to pause, false to resume.
    /// </summary>
    public void SetAllPlayersInputPaused(bool paused)
    {
        //Debug.Log("SetAllPlayersInputPaused called. paused: " + paused);

        Player1.SetAllInputPaused(paused);
        Player2.SetAllInputPaused(paused);

        if (UseTabToSwitchActiveCharacter())
        {
            if (paused)
                SwitchPlayer?.Disable();
            else
                SwitchPlayer?.Enable();
        }
    }

    /// <summary>
    /// Pauses input for a specific player by ID.
    /// </summary>
    public void PausePlayerInput(BasePlayerController.PlayerID playerID)
    {
        if (playerID == BasePlayerController.PlayerID.Player1 && Player1 != null)
        {
            Player1.SetAllInputPaused(true);
        }
        else if (playerID == BasePlayerController.PlayerID.Player2 && Player2 != null)
        {
            Player2.SetAllInputPaused(true);
        }
    }

    /// <summary>
    /// Resumes input for a specific player by ID.
    /// </summary>
    public void ResumePlayerInput(BasePlayerController.PlayerID playerID)
    {
        if (playerID == BasePlayerController.PlayerID.Player1 && Player1 != null)
        {
            Player1.SetAllInputPaused(false);
        }
        else if (playerID == BasePlayerController.PlayerID.Player2 && Player2 != null)
        {
            Player2.SetAllInputPaused(false);
        }
    }

    public void SaveInputData()
    {
        Player1.OldInputData = Player1.InputData;
        Player2.OldInputData = Player2.InputData;
        //Debug.Log("Saving input data. Player 1: look = " + Player1.OldInputData.IsLookPaused+" move = " + Player1.OldInputData.IsMovePaused 
        //+ " Player 2: look = " + Player2.OldInputData.IsLookPaused+" move = " + Player2.OldInputData.IsMovePaused);
    }

}
