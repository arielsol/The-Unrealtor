using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class InteractionBase : PuzzleBase, IInteractable
{
    public bool canInteract = true;
    [Header("UI Prompt (Keyboard/Mouse)")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private GameObject promptUIPlayer1;
    [SerializeField] private GameObject promptUIPlayer2;
    [Header("UI Prompt (Controller / Gamepad)")]
    [SerializeField] private GameObject promptUI_Gamepad;
    [SerializeField] private GameObject promptUIPlayer1_Gamepad;
    [SerializeField] private GameObject promptUIPlayer2_Gamepad;
    [SerializeField] private bool useGlobalUIKey = false;
    [SerializeField] private string globalUIKey = "interact";
    [SerializeField]
    [Tooltip("When enabled, logs gamepad-related prompt and OnInteract flow for this object (see Console tag [InteractionBase Gamepad]).")]
    private bool debugGamepadInteractions = false;
    [Header("Events")]
    public UnityEvent onInteractPlayer1;
    public UnityEvent onInteractPlayer2;
    public UnityEvent onInteractAnyPlayer;
    [SerializeField] protected SoundID _interactSound;
    [SerializeField] protected bool playSoundOnInteract = false;
    [SerializeField] public string dialogueName = "";
    protected bool dialogueHasPlayed = false;
    [Header("Level")]
    [SerializeField] private int levelId = 0;
    public List<GameObject> objectsToHide = new List<GameObject>();
    public List<GameObject> objectsToShow = new List<GameObject>();
    [Header("Interaction Behavior")]
    [SerializeField] private bool oneTimeUse = true;
    [SerializeField] public MatchBase AssociatedMatch;
    [Tooltip("Minimum dot product between the camera's horizontal forward and the direction to this object (0 = perpendicular, 1 = straight ahead).")]
    [SerializeField] private float minInteractFacingDot = 0.55f;
    [Header("Outline")]
    [SerializeField] private bool showOutlineWhenPromptVisible = true;
    [SerializeField] private Material outlineMaterialTemplate;
    [SerializeField] private bool includeChildRenderers = true;
    public void SetOneTimeUse(bool value) => oneTimeUse = value;
    private bool _hasBeenUsed = false;
    public bool HasBeenUsed => _hasBeenUsed;
    private bool _promptVisiblePlayer1 = false;
    private bool _promptVisiblePlayer2 = false;
    private bool _lastPromptWasGamepadPlayer1 = false;
    private bool _lastPromptWasGamepadPlayer2 = false;
    private bool _suppressPromptPlayer1WhileInteracting = false;
    private bool _suppressPromptPlayer2WhileInteracting = false;
    private GameObject _lastShownPromptPlayer1;
    private GameObject _lastShownPromptPlayer2;

    private readonly HashSet<Collider> _player1TriggerCollidersInRange = new HashSet<Collider>();
    private readonly HashSet<Collider> _player2TriggerCollidersInRange = new HashSet<Collider>();
    private readonly List<PencilOutlineController> _outlineControllers = new();
    private readonly Dictionary<PencilOutlineController, Material> _outlineInstances = new();
    private bool _outlineInitialized;
    public int LevelId => levelId;
    /// <summary>
    /// Shows or hides the interact hint for this player. For world detection, <see cref="PlayerInteractor"/>
    /// should only call with <paramref name="visible"/> true after <see cref="CanAcceptInteractAtProbe"/>
    /// succeeds (facing, <see cref="CanInteract"/>, optional line of sight) so UIManager is not asked to
    /// show the prompt while the player is aimed at something else or cannot use the object yet.
    /// </summary>
    public void ShowPrompt(bool visible, BasePlayerController player)
    {
        if (player == null) return;
        var controller = player.GetComponentInParent<BasePlayerController>();
        if (controller == null) return;

        // Prompt should only ever render for interactable objects.
        if (visible && !CanInteract())
            visible = false;


        if (visible &&
            UIManager.Instance != null &&
            UIManager.Instance.IsPlayerPickupPopupLocked(controller.playerID))
        {
            visible = false;
        }

        if (visible && useGlobalUIKey &&
            PlayerInputManager.Instance != null &&
            PlayerInputManager.Instance.CurrentControllerType == PlayerInputManager.ControllerType.SinglePlayer &&
            controller.playerID != PlayerInputManager.Instance.CurrPlayerEnum)
        {
            if (UIManager.Instance != null &&
                (GameManager.Instance == null || GameManager.Instance.CurrentCutsceneState != GameManager.CutsceneState.CUTSCENE))
            {
                bool inactiveIsGamepad = IsPlayerUsingGamepad(controller.playerID);
                string effectiveGlobalUIKeyForInactivePlayer = ResolveGlobalUIPanelId(GetGlobalUIKeyForInputType(inactiveIsGamepad));
                bool forP1 = controller.playerID == BasePlayerController.PlayerID.Player1;
                LogGamepad($"SinglePlayer inactive-side hint: player={controller.playerID} inactiveIsGamepad={inactiveIsGamepad} panelId={effectiveGlobalUIKeyForInactivePlayer} leftSide={forP1}");
                if (forP1)
                {
                    _promptVisiblePlayer1 = false;
                    _lastShownPromptPlayer1 = null;
                    UIManager.Instance.RequestPanelSideVisible(effectiveGlobalUIKeyForInactivePlayer, true, false);
                }
                else
                {
                    _promptVisiblePlayer2 = false;
                    _lastShownPromptPlayer2 = null;

                    UIManager.Instance.RequestPanelSideVisible(effectiveGlobalUIKeyForInactivePlayer, false, false);
                }
            }
            return;
        }

        if (visible && useGlobalUIKey &&
            PlayerInputManager.Instance != null &&
            PlayerInputManager.Instance.CurrentControllerType == PlayerInputManager.ControllerType.TwoPlayer &&
            UIManager.Instance != null)
        {
            UIManager.Instance.ApplySinglePlayerActiveIndicators(false, false);
        }

        bool isPlayer1 = controller.playerID == BasePlayerController.PlayerID.Player1;
        bool isGamepad = IsPlayerUsingGamepad(controller.playerID);
        string effectiveGlobalUIKey = ResolveGlobalUIPanelId(GetGlobalUIKeyForInputType(isGamepad));
        GameObject promptForPlayer = GetPromptForPlayer(controller.playerID, isGamepad);

        // Always log on entry when debug is on, for BOTH KBM and gamepad players, so we can see
        // when one side never enters the work block (e.g. early-return short-circuit).
        if (debugGamepadInteractions)
        {
            string prevState = isPlayer1
                ? $"prevVisP1={_promptVisiblePlayer1} prevWasGamepadP1={_lastPromptWasGamepadPlayer1}"
                : $"prevVisP2={_promptVisiblePlayer2} prevWasGamepadP2={_lastPromptWasGamepadPlayer2}";
            Debug.Log(
                $"[InteractionBase Diag] {name}: ShowPrompt ENTER " +
                $"player={controller.playerID} visible={visible} isGamepad={isGamepad} " +
                $"effectiveGlobalUIKey={effectiveGlobalUIKey} " +
                $"promptGO={(promptForPlayer != null ? promptForPlayer.name : "null")} " +
                $"useGlobalUIKey={useGlobalUIKey} " +
                $"controllerType={(PlayerInputManager.Instance != null ? PlayerInputManager.Instance.CurrentControllerType.ToString() : "null")} " +
                $"{prevState}",
                this);
        }

        if (visible && promptForPlayer == null)
        {
            if (isGamepad)
                LogGamepad("ShowPrompt bail: visible but no prompt GameObject / UI panel for resolved key");
            if (debugGamepadInteractions)
                Debug.Log($"[InteractionBase Diag] {name}: ShowPrompt RETURN promptForPlayer==null player={controller.playerID}", this);
            return;
        }
        if (isPlayer1)
        {
            if (_promptVisiblePlayer1 == visible)
            {
                if (!visible)
                {
                    _suppressPromptPlayer1WhileInteracting = false;
                    SetPromptCanvasVisible(promptForPlayer, true);
                    if (debugGamepadInteractions)
                        Debug.Log($"[InteractionBase Diag] {name}: ShowPrompt RETURN P1 already-hidden short-circuit", this);
                    return;
                }
                if (_lastPromptWasGamepadPlayer1 == isGamepad)
                {
                    if (debugGamepadInteractions)
                        Debug.Log($"[InteractionBase Diag] {name}: ShowPrompt RETURN P1 same-input-type short-circuit (isGamepad={isGamepad})", this);
                    return;
                }
            }
            _promptVisiblePlayer1 = visible;
            if (visible) _lastPromptWasGamepadPlayer1 = isGamepad;
        }
        else
        {
            if (_promptVisiblePlayer2 == visible)
            {
                if (!visible)
                {
                    _suppressPromptPlayer2WhileInteracting = false;
                    SetPromptCanvasVisible(promptForPlayer, true);
                    if (debugGamepadInteractions)
                        Debug.Log($"[InteractionBase Diag] {name}: ShowPrompt RETURN P2 already-hidden short-circuit", this);
                    return;
                }
                if (_lastPromptWasGamepadPlayer2 == isGamepad)
                {
                    if (debugGamepadInteractions)
                        Debug.Log($"[InteractionBase Diag] {name}: ShowPrompt RETURN P2 same-input-type short-circuit (isGamepad={isGamepad})", this);
                    return;
                }

            }
            _promptVisiblePlayer2 = visible;
            if (visible)
                _lastPromptWasGamepadPlayer2 = isGamepad;

        }

        if (visible)
        {
            if (isGamepad)
                LogGamepad($"ShowPrompt apply player={controller.playerID} uiPanelId={effectiveGlobalUIKey} promptGO={(promptForPlayer != null ? promptForPlayer.name : "null")}");
            if (!useGlobalUIKey)
            {
                GameObject otherPrompt = GetPromptForPlayer(controller.playerID, !isGamepad);
                if (otherPrompt != null && otherPrompt != promptForPlayer)
                    otherPrompt.SetActive(false);
            }
            if (useGlobalUIKey)
            {
                // Always update per-side request counts even during a cutscene so the
                // counts stay in sync with logical state. SetPanelSideVisibleInstant
                // (called transitively from RequestPanelSideVisible) is itself a no-op
                // during a cutscene, so no actual visual change happens until the
                // cutscene ends and UIManager re-applies the count-based visibility.
                if (isGamepad)
                    LogGamepad($"UIManager.RequestPanelSideVisible id={effectiveGlobalUIKey} leftSide={isPlayer1} visible=true");
                UIManager.Instance.RequestPanelSideVisible(effectiveGlobalUIKey, isPlayer1, true);

                // Ensure only the current input-type prompt is shown for this side.
                string otherEffectiveGlobalUIKey = ResolveGlobalUIPanelId(GetGlobalUIKeyForInputType(!isGamepad));
                if (!string.Equals(otherEffectiveGlobalUIKey, effectiveGlobalUIKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (isGamepad)
                        LogGamepad($"UIManager.RequestPanelSideVisible hide other input id={otherEffectiveGlobalUIKey} leftSide={isPlayer1} visible=false");
                    UIManager.Instance.RequestPanelSideVisible(otherEffectiveGlobalUIKey, isPlayer1, false);
                }
            }
            else
                UIManager.Instance.ShowUI(effectiveGlobalUIKey);
            SetPromptCanvasVisible(promptForPlayer, !IsPromptSuppressed(controller.playerID));
            if (isPlayer1) _lastShownPromptPlayer1 = promptForPlayer; else _lastShownPromptPlayer2 = promptForPlayer;
        }
        else
        {
            if (useGlobalUIKey && UIManager.Instance != null)
            {
                // Always decrement the per-side request count, even during a cutscene.
                // SetPanelSideVisibleInstant is a no-op while a cutscene is active, so
                // this is safe and keeps counts from getting stuck (which would make the
                // prompt UI appear forever after the cutscene ends).
                if (isGamepad)
                    LogGamepad($"UIManager.RequestPanelSideVisible id={effectiveGlobalUIKey} leftSide={isPlayer1} visible=false (hide prompt)");
                UIManager.Instance.RequestPanelSideVisible(effectiveGlobalUIKey, isPlayer1, false);
            }
            else
            {
                GameObject lastShown = isPlayer1 ? _lastShownPromptPlayer1 : _lastShownPromptPlayer2;
                if (lastShown != null)
                {
                    SetPromptCanvasVisible(lastShown, true);
                    lastShown.SetActive(_promptVisiblePlayer1 || _promptVisiblePlayer2);
                }
            }
            if (isPlayer1) _suppressPromptPlayer1WhileInteracting = false; else _suppressPromptPlayer2WhileInteracting = false;
            if (isPlayer1) _lastShownPromptPlayer1 = null; else _lastShownPromptPlayer2 = null;
        }

        UpdateOutlineVisibility();
    }

    private bool IsPlayerUsingGamepad(BasePlayerController.PlayerID playerID)
    {
        if (PlayerInputManager.Instance == null) return false;
        string inputType = playerID == BasePlayerController.PlayerID.Player1
            ? PlayerInputManager.Instance.GetPlayer1InputType()
            : PlayerInputManager.Instance.GetPlayer2InputType();
        return inputType == "Gamepad";

    }

    private string GetGlobalUIKeyForInputType(bool isGamepad)
    {
        if (string.IsNullOrWhiteSpace(globalUIKey))
            return globalUIKey;

        string baseKey = globalUIKey;

        const string kbmSuffix = "_KBM";
        const string gamepadSuffix = "_Gamepad";

        if (baseKey.EndsWith(kbmSuffix, StringComparison.OrdinalIgnoreCase))
            baseKey = baseKey.Substring(0, baseKey.Length - kbmSuffix.Length);
        else if (baseKey.EndsWith(gamepadSuffix, StringComparison.OrdinalIgnoreCase))
            baseKey = baseKey.Substring(0, baseKey.Length - gamepadSuffix.Length);

        return baseKey + (isGamepad ? gamepadSuffix : kbmSuffix);
    }

    private string ResolveGlobalUIPanelId(string desiredId)
    {
        if (UIManager.Instance == null || string.IsNullOrWhiteSpace(desiredId))
            return desiredId;

        UIManager.UIPanel panel = UIManager.Instance.UIPanels.Find(p =>
            string.Equals(p.id, desiredId, StringComparison.OrdinalIgnoreCase));
        return panel != null ? panel.id : desiredId;
    }

    private GameObject GetPromptForPlayer(BasePlayerController.PlayerID playerID, bool forGamepad)
    {
        if (useGlobalUIKey)
        {
            if (UIManager.Instance == null) return null;
            string desiredId = GetGlobalUIKeyForInputType(forGamepad);
            string effectiveGlobalUIKey = ResolveGlobalUIPanelId(desiredId);
            UIManager.UIPanel panel = UIManager.Instance.UIPanels.Find(p =>
                string.Equals(p.id, effectiveGlobalUIKey, StringComparison.OrdinalIgnoreCase));
            if (panel == null) return null;
            bool leftSlot = playerID == BasePlayerController.PlayerID.Player1;
            GameObject side = leftSlot ? panel.leftPanel : panel.rightPanel;
            if (side == null) return null;
            if (forGamepad && string.Equals(effectiveGlobalUIKey, "interact_Gamepad", StringComparison.OrdinalIgnoreCase))
                return UIManager.Instance.ResolveInteractGamepadMiswiredSide(side, leftSlot);
            return side;
        }
        if (forGamepad)
        {
            GameObject gamepadPrompt = playerID == BasePlayerController.PlayerID.Player1
                ? (promptUIPlayer1_Gamepad != null ? promptUIPlayer1_Gamepad : promptUI_Gamepad)
                : (promptUIPlayer2_Gamepad != null ? promptUIPlayer2_Gamepad : promptUI_Gamepad);
            if (gamepadPrompt != null) return gamepadPrompt;
            return GetPromptForPlayer(playerID, false);
        }
        GameObject kbm = playerID == BasePlayerController.PlayerID.Player1
            ? (promptUIPlayer1 != null ? promptUIPlayer1 : promptUI)
            : (promptUIPlayer2 != null ? promptUIPlayer2 : promptUI);
        return kbm;
    }

    public virtual void OnInteract(BasePlayerController player)
    {
        if (player == null || !CanInteract())
        {
            LogGamepadIfPlayerOnPad(player, "OnInteract ignored: CanInteract() false or invalid state");
            return;
        }
        Vector3 probe = player.Rigidbody != null ? player.Rigidbody.position : player.transform.position;
        if (!IsPlayerFacingInteractable(player, probe, GetComponent<Collider>()))
        {
            LogGamepadIfPlayerOnPad(player, "OnInteract ignored: not facing interactable");
            return;
        }
        if (oneTimeUse)
            _hasBeenUsed = true;
        if (playSoundOnInteract)
            MusicManager.Instance.PlaySFX(_interactSound, Vector3.zero);
        foreach (var obj in objectsToHide)
            if (obj != null) obj.SetActive(false);
        foreach (var obj in objectsToShow)
            if (obj != null) obj.SetActive(true);
        if (player.playerID == BasePlayerController.PlayerID.Player1)
            onInteractPlayer1?.Invoke();
        else
            onInteractPlayer2?.Invoke();
        onInteractAnyPlayer?.Invoke();


        bool nowSuppressed = TogglePromptSuppressed(player.playerID);
        bool isGamepad = IsPlayerUsingGamepad(player.playerID);
        GameObject promptForPlayer = GetPromptForPlayer(player.playerID, isGamepad);
        SetPromptCanvasVisible(promptForPlayer, !nowSuppressed);

        LogGamepadIfPlayerOnPad(player, $"OnInteract accepted player={player.playerID}");
        UpdateOutlineVisibility();
    }

    private bool TogglePromptSuppressed(BasePlayerController.PlayerID playerID)
    {
        if (playerID == BasePlayerController.PlayerID.Player1)
        {
            _suppressPromptPlayer1WhileInteracting = !_suppressPromptPlayer1WhileInteracting;
            return _suppressPromptPlayer1WhileInteracting;
        }

        _suppressPromptPlayer2WhileInteracting = !_suppressPromptPlayer2WhileInteracting;
        return _suppressPromptPlayer2WhileInteracting;
    }

    private bool IsPromptSuppressed(BasePlayerController.PlayerID playerID)
    {
        return playerID == BasePlayerController.PlayerID.Player1
            ? _suppressPromptPlayer1WhileInteracting
            : _suppressPromptPlayer2WhileInteracting;
    }

    private static void SetPromptCanvasVisible(GameObject prompt, bool visible)
    {
        if (prompt == null) return;
        var cg = prompt.GetComponent<CanvasGroup>();
        if (cg == null) return;
        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
    }

    private void LogGamepad(string message)
    {
        if (!debugGamepadInteractions) return;
        Debug.Log($"[InteractionBase Gamepad] {name}: {message}", this);
    }

    private void LogGamepadIfPlayerOnPad(BasePlayerController player, string message)
    {
        if (!debugGamepadInteractions || player == null || !IsPlayerUsingGamepad(player.playerID)) return;
        Debug.Log($"[InteractionBase Gamepad] {name}: {message}", this);
    }

    public void OnInteract()
    {
    }

    public bool CanInteract()
    {
        if (!canInteract) return false;
        if (oneTimeUse && _hasBeenUsed) return false;
        return true;
    }

    public bool HasConditionMet() => CanInteract();


    public virtual bool CanAcceptInteractAtProbe(BasePlayerController player, Vector3 worldProbeOrigin, LayerMask lineOfSightMask, LayerMask blockerMask, Collider hitCollider)
    {
        if (player == null || hitCollider == null) return false;
        if (hitCollider.GetComponentInParent<InteractionBase>() != this) return false;
        if (!CanInteract()) return false;
        if (!IsPlayerFacingInteractable(player, worldProbeOrigin, hitCollider)) return false;
        return HasClearInteractLineFromProbe(worldProbeOrigin, lineOfSightMask, blockerMask, hitCollider);
    }


    public bool IsPlayerFacingInteractable(BasePlayerController controller, Vector3 fromWorldPosition, Collider hitCollider)
    {
        if (controller == null) return false;
        Collider col = hitCollider != null ? hitCollider : GetComponent<Collider>();
        if (col == null) return true;

        Vector3 up = (-controller.GravityDirection).normalized;
        Vector3 targetPoint = col.ClosestPoint(fromWorldPosition);
        Vector3 toTarget = targetPoint - fromWorldPosition;
        if (toTarget.sqrMagnitude < 1e-8f) return true;

        Vector3 dirPlanar = Vector3.ProjectOnPlane(toTarget, up);
        if (dirPlanar.sqrMagnitude < 1e-8f) return true;
        dirPlanar.Normalize();

        Vector3 forwardPlanar = controller.GetCameraPlanarForward();
        return Vector3.Dot(forwardPlanar, dirPlanar) >= minInteractFacingDot;
    }


    public bool IsPlayerFacingInteractable(BasePlayerController controller)
    {
        if (controller == null) return false;
        return IsPlayerFacingInteractable(controller, controller.transform.position, GetComponent<Collider>());
    }

    private static bool HasClearInteractLineFromProbe(Vector3 probeWorld, LayerMask interactableMask, LayerMask blockerMask, Collider colliderHit)
    {
        Vector3 targetPoint = colliderHit.ClosestPoint(probeWorld);
        Vector3 direction = (targetPoint - probeWorld).normalized;
        float distance = Vector3.Distance(probeWorld, targetPoint);
        if (distance < 1e-4f) return true;

        Vector3 origin = probeWorld + direction * 0.1f;
        float castDistance = distance + 0.1f;

        if (blockerMask.value != 0 &&
            Physics.Raycast(origin, direction, out RaycastHit blockerHit, castDistance, blockerMask, QueryTriggerInteraction.Ignore))
        {
       
            if (blockerHit.collider != colliderHit && !blockerHit.collider.transform.IsChildOf(colliderHit.transform.root))
                return false;
        }

        if (!Physics.Raycast(origin, direction, out RaycastHit rayHit, castDistance, interactableMask, QueryTriggerInteraction.Collide))
            return false;
        return rayHit.collider == colliderHit || rayHit.collider.transform.IsChildOf(colliderHit.transform);
    }

    public GameObject GetUI()
    {
        return null;
    }

    private void OnTriggerEnter(Collider other)
    {
        var controller = other.GetComponentInParent<BasePlayerController>();
        if (controller == null) return;
        if (controller.playerID == BasePlayerController.PlayerID.Player1)
            _player1TriggerCollidersInRange.Add(other);
        else
            _player2TriggerCollidersInRange.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        var controller = other.GetComponentInParent<BasePlayerController>();
        if (controller == null) return;
        if (controller.playerID == BasePlayerController.PlayerID.Player1)
            _player1TriggerCollidersInRange.Remove(other);
        else
            _player2TriggerCollidersInRange.Remove(other);
        bool stillInTrigger = controller.playerID == BasePlayerController.PlayerID.Player1
            ? _player1TriggerCollidersInRange.Count > 0
            : _player2TriggerCollidersInRange.Count > 0;
        if (!stillInTrigger)
            ShowPrompt(false, controller);
    }

    private void OnTriggerStay(Collider other)
    {
        var controller = other.GetComponentInParent<BasePlayerController>();
        if (controller == null) return;
        if (controller.playerID == BasePlayerController.PlayerID.Player1)
            _player1TriggerCollidersInRange.Add(other);
        else
            _player2TriggerCollidersInRange.Add(other);
    }

    private void OnDisable()
    {
        if (_outlineInitialized)
        {
            foreach (var controller in _outlineControllers)
            {
                RemoveOutlineFromController(controller);
            }
        }
    }

    private void UpdateOutlineVisibility()
    {
        if (!showOutlineWhenPromptVisible)
            return;

        EnsureOutlineControllers();

        bool shouldShowOutline = CanInteract() && (_promptVisiblePlayer1 || _promptVisiblePlayer2);

        foreach (var controller in _outlineControllers)
        {
            if (controller == null)
                continue;

            if (shouldShowOutline)
            {
                AddOutlineToController(controller);
            }
            else
            {
                RemoveOutlineFromController(controller);
            }
        }
    }

    private void EnsureOutlineControllers()
    {
        if (_outlineInitialized)
            return;

        _outlineInitialized = true;
        _outlineControllers.Clear();

        IEnumerable<Renderer> renderers = includeChildRenderers
            ? GetComponentsInChildren<Renderer>(true)
            : GetComponents<Renderer>();

        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            var controller = renderer.GetComponent<PencilOutlineController>();
            if (controller == null)
                controller = renderer.gameObject.AddComponent<PencilOutlineController>();

            controller.sourceMaterial = GetOutlineMaterialTemplate();

            _outlineControllers.Add(controller);
        }
    }

    private Material GetOutlineMaterialTemplate()
    {
        if (outlineMaterialTemplate != null)
            return outlineMaterialTemplate;

        Debug.LogWarning($"{name} needs an outline material template that uses Custom/PencilOutline.");
        return null;
    }

    private void AddOutlineToController(PencilOutlineController controller)
    {
        if (controller == null || _outlineInstances.ContainsKey(controller))
            return;

        Renderer renderer = controller.GetComponent<Renderer>();
        Material template = controller.sourceMaterial != null ? controller.sourceMaterial : GetOutlineMaterialTemplate();
        if (renderer == null || template == null)
            return;

        Material[] current = renderer.sharedMaterials;
        Material[] updated = new Material[current.Length + 1];

        for (int i = 0; i < current.Length; i++)
            updated[i] = current[i];

        Material instance = new Material(template)
        {
            name = template.name + " (Interaction Instance)"
        };

        updated[updated.Length - 1] = instance;
        renderer.sharedMaterials = updated;
        controller.outlineMaterialIndex = updated.Length - 1;
        _outlineInstances[controller] = instance;
    }

    private void RemoveOutlineFromController(PencilOutlineController controller)
    {
        if (controller == null || !_outlineInstances.TryGetValue(controller, out Material outlineInstance))
            return;

        Renderer renderer = controller.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material[] current = renderer.sharedMaterials;
            List<Material> updated = new List<Material>(current.Length);

            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != outlineInstance)
                    updated.Add(current[i]);
            }

            renderer.sharedMaterials = updated.ToArray();
        }

        if (Application.isPlaying)
            Destroy(outlineInstance);
        else
            DestroyImmediate(outlineInstance);

        controller.outlineMaterialIndex = -1;
        _outlineInstances.Remove(controller);
    }
}
