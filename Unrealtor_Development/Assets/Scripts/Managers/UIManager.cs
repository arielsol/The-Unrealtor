using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    [System.Serializable]
    public class UIPanel
    {
        public string id;
        public GameObject leftPanel;
        public GameObject rightPanel;
    }
    [SerializeField] private List<UIPanel> uiPanels = new List<UIPanel>();
    public  List<UIPanel> UIPanels { get { return uiPanels; } }
    private HashSet<string> bufferedPanels = new HashSet<string>();
    private const float DefaultFadeDuration = 1f;
    private static readonly string[] SinglePlayerActiveChromeChildNames = { "Left", "Right", "Top", "Bottom" };
    private readonly Dictionary<GameObject, Coroutine> _activePanelFades = new Dictionary<GameObject, Coroutine>();
    private readonly Dictionary<string, int> _panelSideRequestCounts = new Dictionary<string, int>();
    private static readonly HashSet<int> _loggedMiswiredInteractGamepadSlots = new HashSet<int>();
    private readonly Dictionary<BasePlayerController.PlayerID, BasePlayerController.PlayerInputData> _pickupPopupSavedInputStates = new Dictionary<BasePlayerController.PlayerID, BasePlayerController.PlayerInputData>();
    private readonly HashSet<BasePlayerController.PlayerID> _pickupPopupLockedPlayers = new HashSet<BasePlayerController.PlayerID>();
    private readonly Dictionary<BasePlayerController.PlayerID, MatchUI.PopUpType> _pickupPopupTypeByPlayer = new Dictionary<BasePlayerController.PlayerID, MatchUI.PopUpType>();
    private readonly HashSet<BasePlayerController.PlayerID> _pickupCloseBlockedUntilInteractReleased = new HashSet<BasePlayerController.PlayerID>();
    private readonly HashSet<BasePlayerController.PlayerID> _pickupSuppressWorldOpenUntilInteractReleased = new HashSet<BasePlayerController.PlayerID>();
    public MatchUI matchUI;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        HideSinglePlayerOnlyUiInstant();
    }
    private void OnEnable()
    {
        GameEvents.OnCutsceneStateChanged += HandleCutsceneStateChange;
    }
    private void Start()
    {

    }
 
    private void OnDisable()
    {
        GameEvents.OnCutsceneStateChanged -= HandleCutsceneStateChange;
        ReleaseAllPickupPopupLocks();
    }
    private void HandleCutsceneStateChange(GameManager.CutsceneState newState)
    {
        if (newState == GameManager.CutsceneState.CUTSCENE)
        {
            // Match/puzzle: clue popups live on MatchUI, not in uiPanels — hide them here too.
            matchUI?.HideAllPickupPopupsImmediate();

            HideAllUIImmediate();
        }
        else if (newState == GameManager.CutsceneState.GAMEPLAY)
        {
            foreach (string id in bufferedPanels)
            {
                ShowUI(id);
            }
            bufferedPanels.Clear();
            ApplyPanelSideRequestCountsToVisibility();
        }
    }


    private void ApplyPanelSideRequestCountsToVisibility()
    {
        foreach (KeyValuePair<string, int> kvp in _panelSideRequestCounts)
        {
            if (kvp.Value <= 0) continue;
            string key = kvp.Key;
            int sepIndex = key.LastIndexOf("::", StringComparison.Ordinal);
            if (sepIndex < 0) continue;
            string id = key.Substring(0, sepIndex);
            string sideTag = key.Substring(sepIndex + 2);
            bool leftSide = string.Equals(sideTag, "L", StringComparison.OrdinalIgnoreCase);
            SetPanelSideVisibleInstant(id, leftSide, true);
        }
    }
    public void ShowUI(string id)
    {
        Debug.Log("ShowUI: " + id);
        if (GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE)
        {
            bufferedPanels.Add(id);
            return;
        }
        ShowUIWithFade(id, DefaultFadeDuration);
    }
    public void HideUI(string id)
    {
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel != null)
            HideUIWithFade(id, DefaultFadeDuration);
        else
            Debug.LogWarning($"UI Panel with ID '{id}' not found!");
    }
    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float from, float to, float duration, System.Action onComplete = null)
    {
        if (canvasGroup == null)
        {
            onComplete?.Invoke();
            yield break;
        }
        canvasGroup.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);
            canvasGroup.alpha = Mathf.Lerp(from, to, smoothT);
            yield return null;
        }
        canvasGroup.alpha = to;
        onComplete?.Invoke();
    }
    private void EnsureActive(GameObject go, bool active)
    {
        // These are core HUD containers; never disable them (single or two player).
        if (!active && IsControlsPanelRoot(go))
            return;
        if (go != null && go.activeSelf != active)
        {
            go.SetActive(active);
        }
    }
    private bool IsControlsPanelRoot(GameObject go)
    {
        if (go == null) return false;
        // Hard-coded by name to avoid requiring inspector wiring.
        return string.Equals(go.name, "LeftControlsPanel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(go.name, "RightControlsPanel", StringComparison.OrdinalIgnoreCase);
    }
    private CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        if (IsControlsPanelRoot(go))
        {
            CanvasGroup rootCg = go.GetComponent<CanvasGroup>();
            if (rootCg != null) rootCg.alpha = 1f;
            return rootCg;
        }
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
    private void StartTrackedPanelFade(GameObject panelRoot, CanvasGroup cg, float from, float to, float duration, Action onComplete = null)
    {
        if (panelRoot == null || cg == null)
        {
            onComplete?.Invoke();
            return;
        }
        if (IsControlsPanelRoot(panelRoot))
        {
            cg.alpha = 1f;
            EnsureActive(panelRoot, true);
            onComplete?.Invoke();
            return;
        }
        if (Mathf.Approximately(from, to))
        {
            cg.alpha = to;
            onComplete?.Invoke();
            return;
        }
        if (_activePanelFades.TryGetValue(panelRoot, out Coroutine existing) && existing != null)
            StopCoroutine(existing);
        Coroutine running = null;
        running = StartCoroutine(TrackedPanelFadeRoutine(panelRoot, cg, from, to, duration, onComplete, () => running));
        _activePanelFades[panelRoot] = running;
    }
    private IEnumerator TrackedPanelFadeRoutine(GameObject panelRoot, CanvasGroup cg, float from, float to, float duration, Action onComplete, Func<Coroutine> getRunning)
    {
        yield return FadeCanvasGroup(cg, from, to, duration, null);
        onComplete?.Invoke();
        if (_activePanelFades.TryGetValue(panelRoot, out Coroutine tracked) && tracked == getRunning())
            _activePanelFades.Remove(panelRoot);
    }


    public GameObject ResolveInteractGamepadMiswiredSide(GameObject assignedSlotRoot, bool isLeftPanelSlot)
    {
        if (assignedSlotRoot == null) return null;
        bool isLikelyMiswired = IsLikelyMiswiredInteractGamepadSlot(assignedSlotRoot);
        bool isControlsRoot = IsControlsPanelRoot(assignedSlotRoot);
        if (!isLikelyMiswired && !isControlsRoot)
            return assignedSlotRoot;
        Transform root = assignedSlotRoot.transform.root;
        string controlsName = isLeftPanelSlot ? "ControlsLeftGamepad" : "ControlsRightGamepad";
        Transform controls = string.Equals(assignedSlotRoot.name, controlsName, StringComparison.OrdinalIgnoreCase)
            ? assignedSlotRoot.transform
            : FindDescendantTransformByName(root, controlsName);
        if (controls == null) return assignedSlotRoot;
        Transform hint = FindDescendantTransformByName(controls, "X-Interact");
        if (hint == null) return assignedSlotRoot;
        if (hint.gameObject != assignedSlotRoot)
        {
            int logKey = (assignedSlotRoot.GetInstanceID() << 1) ^ (isLeftPanelSlot ? 1 : 0);
            if (_loggedMiswiredInteractGamepadSlots.Add(logKey))
            {
                Debug.LogWarning(
                    $"[UIManager] interact_Gamepad {(isLeftPanelSlot ? "left" : "right")} slot was wired to '{assignedSlotRoot.name}'; using '{controlsName}/X-Interact' for visibility. Fix UIPanel references on UIManager when convenient.",
                    assignedSlotRoot);
            }
        }
        return hint.gameObject;
    }

    private static bool IsLikelyMiswiredInteractGamepadSlot(GameObject go)
    {
        return string.Equals(go.name, "SinglePlayerLeftActive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(go.name, "SinglePlayerRightActive", StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindDescendantTransformByName(Transform t, string exactName)
    {
        if (t == null) return null;
        if (string.Equals(t.name, exactName, StringComparison.Ordinal)) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform found = FindDescendantTransformByName(t.GetChild(i), exactName);
            if (found != null) return found;
        }
        return null;
    }

    private GameObject GetResolvedPanelSideGameObject(string id, bool leftSide, UIPanel panel)
    {
        GameObject side = leftSide ? panel.leftPanel : panel.rightPanel;
        if (side == null) return null;
        if (string.Equals(id, "interact_Gamepad", StringComparison.OrdinalIgnoreCase))
            return ResolveInteractGamepadMiswiredSide(side, leftSide);
        return side;
    }

    public void SetPanelSideVisibleInstant(string id, bool leftSide, bool visible)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE)
            return;
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel == null) return;
        GameObject side = GetResolvedPanelSideGameObject(id, leftSide, panel);
        if (side == null) return;
        // Never allow the core control panel roots to be hidden/faded.
        if (IsControlsPanelRoot(side))
        {
            // Don't add a CanvasGroup here (we intentionally keep these roots free of alpha state).
            CanvasGroup rootCg = side.GetComponent<CanvasGroup>();
            if (rootCg != null) rootCg.alpha = 1f;
            EnsureActive(side, true);
            return;
        }
        if (_activePanelFades.TryGetValue(side, out Coroutine existing) && existing != null)
            StopCoroutine(existing);
        _activePanelFades.Remove(side);
        CanvasGroup cg = GetOrAddCanvasGroup(side);
        if (cg != null)
            cg.alpha = visible ? 1f : 0f;
        EnsureActive(side, visible);
    }

    public void ApplySinglePlayerActiveIndicators(bool singlePlayerMode, bool leftPlayerSelected)
    {
        const string id = "Playeractive";
        if (!singlePlayerMode)
        {
            HideSinglePlayerOnlyUiInstant();
            return;
        }
        SetSinglePlayerActiveChromeSuppressed(false);
        ClearPanelSideRequests(id);
        SetPanelSideVisibleInstant(id, true, leftPlayerSelected);
        SetPanelSideVisibleInstant(id, false, !leftPlayerSelected);
    }
    private void HideSinglePlayerOnlyUiInstant()
    {
        const string id = "Playeractive";
        SetSinglePlayerActiveChromeSuppressed(true);
        ForceSetPanelSideVisibleInstant(id, true, false);
        ForceSetPanelSideVisibleInstant(id, false, false);
        _panelSideRequestCounts.Remove(id + "::L");
        _panelSideRequestCounts.Remove(id + "::R");
        HideSinglePlayerOnlyControlHints();
    }
    /// <summary>
    /// SinglePlayerLeft/RightActive share roots with global control prompts; turn off the border chrome in co-op
    /// so it does not reappear when prompts fade those roots back in.
    /// </summary>
    private void SetSinglePlayerActiveChromeSuppressed(bool suppress)
    {
        UIPanel panel = uiPanels.Find(p => p.id == "Playeractive");
        if (panel == null) return;
        SetChromeSuppressedOnSinglePlayerActiveRoot(panel.leftPanel, suppress);
        SetChromeSuppressedOnSinglePlayerActiveRoot(panel.rightPanel, suppress);
    }
    private static void SetChromeSuppressedOnSinglePlayerActiveRoot(GameObject root, bool suppress)
    {
        if (root == null) return;
        Transform t = root.transform;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            for (int n = 0; n < SinglePlayerActiveChromeChildNames.Length; n++)
            {
                if (string.Equals(c.name, SinglePlayerActiveChromeChildNames[n], StringComparison.Ordinal))
                {
                    c.gameObject.SetActive(!suppress);
                    break;
                }
            }
        }
    }
    private void ForceSetPanelSideVisibleInstant(string id, bool leftSide, bool visible)
    {
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel == null) return;
        GameObject side = GetResolvedPanelSideGameObject(id, leftSide, panel);
        if (side == null) return;
        if (IsControlsPanelRoot(side))
        {
            CanvasGroup rootCg = side.GetComponent<CanvasGroup>();
            if (rootCg != null) rootCg.alpha = 1f;
            EnsureActive(side, true);
            return;
        }
        if (_activePanelFades.TryGetValue(side, out Coroutine existing) && existing != null)
            StopCoroutine(existing);
        _activePanelFades.Remove(side);
        CanvasGroup cg = GetOrAddCanvasGroup(side);
        if (cg != null)
            cg.alpha = visible ? 1f : 0f;
        EnsureActive(side, visible);
    }
    private void HideSinglePlayerOnlyControlHints()
    {
        const float instant = 0f;
        HideUIWithFade("switchplayer_KBM", instant);
        HideUIWithFade("switchplayer_Gamepad", instant);
    }

    public void RequestPanelSideVisible(string id, bool leftSide, bool visible)
    {
        if (string.IsNullOrEmpty(id)) return;
        string key = id + "::" + (leftSide ? "L" : "R");
        int count = 0;
        _panelSideRequestCounts.TryGetValue(key, out count);
        if (visible)
        {
            count++;
        }
        else
        {
            count = Mathf.Max(0, count - 1);
        }
        if (count == 0)
            _panelSideRequestCounts.Remove(key);
        else
            _panelSideRequestCounts[key] = count;
        SetPanelSideVisibleInstant(id, leftSide, count > 0);
    }

    public void ClearPanelSideRequests(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        _panelSideRequestCounts.Remove(id + "::L");
        _panelSideRequestCounts.Remove(id + "::R");
        SetPanelSideVisibleInstant(id, true, false);
        SetPanelSideVisibleInstant(id, false, false);
    }
    public void ShowUIWithFade(string id, float duration = 1f)
    {
        if (GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE)
        {
            bufferedPanels.Add(id);
            return;
        }
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel == null) return;
        if (panel.leftPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.leftPanel);
            EnsureActive(panel.leftPanel, true);
            StartTrackedPanelFade(panel.leftPanel, cg, cg.alpha, 1f, duration);
        }
        if (panel.rightPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.rightPanel);
            EnsureActive(panel.rightPanel, true);
            StartTrackedPanelFade(panel.rightPanel, cg, cg.alpha, 1f, duration);
        }
    }
    public void HideUIWithFade(string id, float duration = 1f)
    {
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel == null) return;
        if (panel.leftPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.leftPanel);
            EnsureActive(panel.leftPanel, true); // keep active during fade
            StartTrackedPanelFade(panel.leftPanel, cg, cg != null ? cg.alpha : 1f, 0f, duration, () =>
            {
                EnsureActive(panel.leftPanel, false);
            });
        }
        if (panel.rightPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.rightPanel);
            EnsureActive(panel.rightPanel, true); // keep active during fade
            StartTrackedPanelFade(panel.rightPanel, cg, cg != null ? cg.alpha : 1f, 0f, duration, () =>
            {
                EnsureActive(panel.rightPanel, false);
            });
        }
    }
    public void ToggleUI(string id)
    {
        if (GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE) return;
        if (IsUIVisible(id))
            HideUIWithFade(id, DefaultFadeDuration);
        else
            ShowUIWithFade(id, DefaultFadeDuration);
    }
    public void ShowLeftUI(string id)
    {
        if (GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE) return;
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel != null && panel.leftPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.leftPanel);
            EnsureActive(panel.leftPanel, true);
            StartTrackedPanelFade(panel.leftPanel, cg, cg.alpha, 1f, DefaultFadeDuration);
        }
    }
    public void ShowRightUI(string id)
    {
        if (GameManager.Instance.CurrentCutsceneState == GameManager.CutsceneState.CUTSCENE) return;
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel != null && panel.rightPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.rightPanel);
            EnsureActive(panel.rightPanel, true);
            StartTrackedPanelFade(panel.rightPanel, cg, cg.alpha, 1f, DefaultFadeDuration);
        }
    }
    public void HideLeftUI(string id)
    {
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel != null && panel.leftPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.leftPanel);
            EnsureActive(panel.leftPanel, true);
            StartTrackedPanelFade(panel.leftPanel, cg, cg != null ? cg.alpha : 1f, 0f, DefaultFadeDuration, () => EnsureActive(panel.leftPanel, false));
        }
    }
    public void HideRightUI(string id)
    {
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel != null && panel.rightPanel != null)
        {
            var cg = GetOrAddCanvasGroup(panel.rightPanel);
            EnsureActive(panel.rightPanel, true);
            StartTrackedPanelFade(panel.rightPanel, cg, cg != null ? cg.alpha : 1f, 0f, DefaultFadeDuration, () => EnsureActive(panel.rightPanel, false));
        }
    }
    public void HideAllUI()
    {
        foreach (var panel in uiPanels)
        {
            if (panel.leftPanel != null)
            {
                var cg = GetOrAddCanvasGroup(panel.leftPanel);
                EnsureActive(panel.leftPanel, true);
                StartTrackedPanelFade(panel.leftPanel, cg, cg != null ? cg.alpha : 1f, 0f, DefaultFadeDuration, () => EnsureActive(panel.leftPanel, false));
            }
            if (panel.rightPanel != null)
            {
                var cg = GetOrAddCanvasGroup(panel.rightPanel);
                EnsureActive(panel.rightPanel, true);
                StartTrackedPanelFade(panel.rightPanel, cg, cg != null ? cg.alpha : 1f, 0f, DefaultFadeDuration, () => EnsureActive(panel.rightPanel, false));
            }
        }
    }


    private void HideAllUIImmediate()
    {
        foreach (var panel in uiPanels)
        {
            if (panel.leftPanel != null)
            {
                if (_activePanelFades.TryGetValue(panel.leftPanel, out Coroutine c0) && c0 != null)
                    StopCoroutine(c0);
                _activePanelFades.Remove(panel.leftPanel);
                if (IsControlsPanelRoot(panel.leftPanel))
                {
                    CanvasGroup rootCgL = GetOrAddCanvasGroup(panel.leftPanel);
                    if (rootCgL != null) rootCgL.alpha = 1f;
                    EnsureActive(panel.leftPanel, true);
                }
                else
                {
                    var cgL = GetOrAddCanvasGroup(panel.leftPanel);
                    if (cgL != null) cgL.alpha = 0f;
                    EnsureActive(panel.leftPanel, false);
                }
            }
            if (panel.rightPanel != null)
            {
                if (_activePanelFades.TryGetValue(panel.rightPanel, out Coroutine c1) && c1 != null)
                    StopCoroutine(c1);
                _activePanelFades.Remove(panel.rightPanel);
                if (IsControlsPanelRoot(panel.rightPanel))
                {
                    CanvasGroup rootCgR = GetOrAddCanvasGroup(panel.rightPanel);
                    if (rootCgR != null) rootCgR.alpha = 1f;
                    EnsureActive(panel.rightPanel, true);
                }
                else
                {
                    var cgR = GetOrAddCanvasGroup(panel.rightPanel);
                    if (cgR != null) cgR.alpha = 0f;
                    EnsureActive(panel.rightPanel, false);
                }
            }
        }
    }
    public void ShowUIForInputType(string baseId, string inputType)
    {
        string panelId = baseId;
        switch (inputType)
        {
            case "Gamepad":
                panelId = baseId + "_Gamepad";
                break;
            case "Keyboard/Mouse":
                panelId = baseId + "_KBM";
                break;
            default:
                panelId = baseId + "_KBM";
                break;
        }
        Debug.Log("ShowUIForInputType: " + panelId);
        ShowUI(panelId);
    }
    public void ShowUIForInputTypeWithFade(string baseId, string inputType, float duration = 1f)
    {
        string panelId = baseId;
        switch (inputType)
        {
            case "Gamepad":
                panelId = baseId + "_Gamepad";
                break;
            case "Keyboard/Mouse":
                panelId = baseId + "_KBM";
                break;
            default:
                panelId = baseId + "_KBM";
                break;
        }
        ShowUIWithFade(panelId, duration);
    }
    public bool IsUIVisible(string id)
    {
        UIPanel panel = uiPanels.Find(p => p.id == id);
        if (panel == null) return false;
        bool leftVisible = panel.leftPanel != null && panel.leftPanel.activeSelf;
        bool rightVisible = panel.rightPanel != null && panel.rightPanel.activeSelf;
        return leftVisible || rightVisible;
    }

    public void UpdatePickupPopup(bool isActive, MatchUI.PopUpType type, BasePlayerController.PlayerID playerID)
    {
        matchUI?.UpdatePopUp(isActive, type, playerID);

        if (isActive)
            LockPlayerForPickupPopup(playerID, type);
        //else
            //UnlockPlayerForPickupPopup(playerID);
    }

    /// <summary>
    /// Closes the pickup hint for this player only when MatchUI shows a pickup row for their side, after interact was released since open (so the same press cannot open and dismiss).
    /// </summary>
    public bool TryClosePickupPopupWithInteract(BasePlayerController.PlayerID playerId)
    {
        bool locked = _pickupPopupLockedPlayers.Contains(playerId);
        bool hasType = _pickupPopupTypeByPlayer.TryGetValue(playerId, out MatchUI.PopUpType lockedType);
        bool uiOpen = matchUI != null && matchUI.IsPickupPopupOpenForPlayer(playerId);

        if (!uiOpen)
            return false;

        if (_pickupCloseBlockedUntilInteractReleased.Contains(playerId))
            return false;

        // Multiple PopUpTypes can share the same PopUpFormat (e.g. NOTEBOOK). MatchUI's scan used PopUpList.Find(format)
        // which returns the first match (wrong clue). Prefer the type we stored when locking — always KEYCLUE_1 vs PLUNGERCLUE_1.
        if (locked && hasType)
        {
            matchUI?.UpdatePopUp(false, lockedType, playerId);
            PickUpObject.NotifyPickupHintClosedForPlayer(playerId, lockedType);
            //UnlockPlayerForPickupPopup(playerId);
            RegisterSuppressPickupWorldOpenUntilInteractReleased(playerId);
            return true;
        }

        if (matchUI != null && matchUI.TryClosePickupPopupForInteract(playerId, out MatchUI.PopUpType closedType))
        {
            PickUpObject.NotifyPickupHintClosedForPlayer(playerId, closedType);
            if (_pickupPopupLockedPlayers.Contains(playerId))
                UnlockPlayerForPickupPopup(playerId);
            else
                PickUpObject.ClearGlobalInteractRegistrationForPlayer(playerId);
            RegisterSuppressPickupWorldOpenUntilInteractReleased(playerId);
            return true;
        }

        if (!locked)
            return false;

        if (!hasType)
        {
            UnlockPlayerForPickupPopup(playerId);
            RegisterSuppressPickupWorldOpenUntilInteractReleased(playerId);
            return true;
        }

        return false;
    }

    private void RegisterSuppressPickupWorldOpenUntilInteractReleased(BasePlayerController.PlayerID playerId)
    {
        _pickupSuppressWorldOpenUntilInteractReleased.Add(playerId);
    }

    public void NotifyPickupPopupInteractReleased(BasePlayerController.PlayerID playerId)
    {
        _pickupCloseBlockedUntilInteractReleased.Remove(playerId);
        _pickupSuppressWorldOpenUntilInteractReleased.Remove(playerId);
    }

    public bool IsPlayerPickupPopupLocked(BasePlayerController.PlayerID playerId) =>
        _pickupPopupLockedPlayers.Contains(playerId);

    public bool IsPickupWorldOpenSuppressedUntilInteractReleased(BasePlayerController.PlayerID playerId) =>
        _pickupSuppressWorldOpenUntilInteractReleased.Contains(playerId);

    private void LockPlayerForPickupPopup(BasePlayerController.PlayerID playerID, MatchUI.PopUpType popupType)
    {
        BasePlayerController player = GetPlayerController(playerID);
        if (player == null)
            return;

        if (_pickupPopupLockedPlayers.Contains(playerID))
            return;

        _pickupPopupSavedInputStates[playerID] = player.InputData;
        _pickupPopupLockedPlayers.Add(playerID);
        _pickupPopupTypeByPlayer[playerID] = popupType;
        _pickupCloseBlockedUntilInteractReleased.Add(playerID);

        player.PlayerVelocity = Vector3.zero;
        if (player.Rigidbody != null)
            player.Rigidbody.linearVelocity = Vector3.zero;

        player.SetAllInputPaused(true);
    }

    private void UnlockPlayerForPickupPopup(BasePlayerController.PlayerID playerID)
    {
        if (!_pickupPopupLockedPlayers.Remove(playerID))
            return;

        _pickupPopupTypeByPlayer.Remove(playerID);
        _pickupCloseBlockedUntilInteractReleased.Remove(playerID);
        PickUpObject.ClearGlobalInteractRegistrationForPlayer(playerID);

        BasePlayerController player = GetPlayerController(playerID);
        if (player == null)
        {
            _pickupPopupSavedInputStates.Remove(playerID);
            return;
        }

        if (_pickupPopupSavedInputStates.TryGetValue(playerID, out BasePlayerController.PlayerInputData savedState))
        {
            player.SetLookPaused(savedState.IsLookPaused);
            player.SetMovePaused(savedState.IsMovePaused);
            _pickupPopupSavedInputStates.Remove(playerID);
        }
        else
        {
            player.SetAllInputPaused(false);
        }
    }

    public void CallUnlockPlayerForPickupPopup(BasePlayerController.PlayerID playerID)
    {
        UnlockPlayerForPickupPopup( playerID);
    }
    private void ReleaseAllPickupPopupLocks()
    {
        foreach (BasePlayerController.PlayerID playerID in new List<BasePlayerController.PlayerID>(_pickupPopupLockedPlayers))
        {
            UnlockPlayerForPickupPopup(playerID);
        }

        _pickupCloseBlockedUntilInteractReleased.Clear();
        _pickupSuppressWorldOpenUntilInteractReleased.Clear();
    }

    private static BasePlayerController GetPlayerController(BasePlayerController.PlayerID playerID)
    {
        if (PlayerInputManager.Instance == null)
            return null;

        return playerID == BasePlayerController.PlayerID.Player1
            ? PlayerInputManager.Instance.Player1
            : PlayerInputManager.Instance.Player2;
    }
}
