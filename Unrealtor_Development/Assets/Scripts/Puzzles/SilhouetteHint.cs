using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class SilhouetteHint : MonoBehaviour
{
    public struct SilhouettePair
    {
        public MatchBase.MatchType matchType;
        public Image matchSilhouette;
    }
    
    [System.Serializable]
    public struct SilhouetteSpritePair
    {
        public MatchBase.MatchType matchType;
        public Sprite sprite;
    }
    public enum SilhouetteType
    {
        LeftSilhouette,
        RightSilhouette
    }

    [Header("Mapping (choose one mode)")]
    [Tooltip("Map each puzzle type to a dedicated Image object (multiple child Images in the hierarchy).")]
    [SerializeField] private List<SilhouettePair> SilhouettePairList = new List<SilhouettePair>();
    [Tooltip("Map each puzzle type to a Sprite. A single Image (SilhouetteImage) will swap sprites.")]
    [SerializeField] private List<SilhouetteSpritePair> SpritePairList = new List<SilhouetteSpritePair>();
    [Tooltip("If true, uses sprite swapping on a single Image. If false, enables a specific Image from SilhouettePairList.")]
    [SerializeField] private bool useSpriteSwap = true;
    
    private Dictionary<MatchBase.MatchType, Image> SilhouetteDict;
    private Dictionary<MatchBase.MatchType, Sprite> SpriteDict;
    [SerializeField] private SilhouetteType ThisSilhouetteType;
    [SerializeField] private Image SilhouetteImage;
    [SerializeField] private float FadeDuration = 2.0f;

    [Header("Proximity Settings")]
    [SerializeField] private float showDistance = 5f;
    [SerializeField] private float hideDistance = 7f;
    [SerializeField] private bool requireSlowHold = true;
    [Tooltip("Require ShowHint action (Right Trigger / Z) to display the hint.")]
    [SerializeField] private bool requireHintHold = false;
    [SerializeField] private Transform targetPuzzleTransform;

    private float currentAlpha = 0f;
    private bool hasCurrentType = false;
    private MatchBase.MatchType currentType;
    private bool hasAssignmentForCurrentType = false;

    private void EnsureSilhouetteImage()
    {
        if (SilhouetteImage == null)
        {
            SilhouetteImage = GetComponent<Image>();
        }
        if (SilhouetteImage == null)
        {
            SilhouetteImage = GetComponentInChildren<Image>(true);
        }
        if (SilhouetteImage == null)
        {
            Debug.LogWarning($"[SilhouetteHint:{name}] No Image assigned/found. Assign 'Silhouette Image' or put this script on the Image object.");
        }
    }

    void Awake()
    {
        EnsureSilhouetteImage();
        if (!useSpriteSwap)
        {
            SilhouetteDict = new Dictionary<MatchBase.MatchType, Image>();
            foreach (var p in SilhouettePairList)
            {
                SilhouetteDict[p.matchType] = p.matchSilhouette;
                if (p.matchSilhouette != null)
                {
                    var c = p.matchSilhouette.color; c.a = 0f; p.matchSilhouette.color = c;
                    p.matchSilhouette.enabled = true;
                }
            }
        }
        else
        {
            SpriteDict = new Dictionary<MatchBase.MatchType, Sprite>();
            foreach (var p in SpritePairList)
            {
                SpriteDict[p.matchType] = p.sprite;
            }
        }

        if (SilhouetteImage != null)
        {
            var c = SilhouetteImage.color;
            c.a = 0f;
            SilhouetteImage.color = c;
            if (useSpriteSwap)
            {
                SilhouetteImage.enabled = true;
                SilhouetteImage.sprite = null;
            }
        }
    }

    void Update()
    {
        EnsureSilhouetteImage();
        if (SilhouetteImage == null) return;
        // Gather hold requirements
        bool slowHeldOk = true;
        bool hintHeldOk = true;
        if (requireSlowHold)
        {
            slowHeldOk = false;
        }
        if (requireHintHold)
        {
            hintHeldOk = false;
        }
        bool holdOk = slowHeldOk && hintHeldOk;
        float targetAlpha = 0f;
        if (!hasAssignmentForCurrentType)
            targetAlpha = 0f;
        else if (holdOk)
        {
            if (targetPuzzleTransform == null)
            {
                targetAlpha = 1f;
            }
            else
            {
                Transform playerT = null;
                var pm = PlayerInputManager.Instance;
                if (pm != null)
                {
                    playerT = (ThisSilhouetteType == SilhouetteType.LeftSilhouette)
                        ? pm.Player1?.transform
                        : pm.Player2?.transform;
                }
                if (playerT != null)
                {
                    float dist = Vector3.Distance(playerT.position, targetPuzzleTransform.position);
                    float t = Mathf.InverseLerp(hideDistance, showDistance, dist);
                    targetAlpha = Mathf.Clamp01(t);
                }
            }
        }

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.unscaledDeltaTime / Mathf.Max(0.0001f, FadeDuration));
        if (!hasAssignmentForCurrentType && SilhouetteImage != null)
        {
            SilhouetteImage.enabled = false;
            var c = SilhouetteImage.color; c.a = 0f; SilhouetteImage.color = c;
        }
        else if (SilhouetteImage != null)
        {
            SilhouetteImage.enabled = true;
            var col = SilhouetteImage.color;
            col.a = currentAlpha;
            SilhouetteImage.color = col;
        }
    }

    public void SetSilhouette(MatchBase.MatchType matchType)
    {
        EnsureSilhouetteImage();
        if (hasCurrentType && currentType.Equals(matchType))
        {
            return;
        }
        hasCurrentType = true;
        currentType = matchType;
        if (useSpriteSwap)
        {
            if (SilhouetteImage != null && SpriteDict != null && SpriteDict.TryGetValue(matchType, out var sprite) && sprite != null)
            {
                hasAssignmentForCurrentType = true;
                if (SilhouetteImage.sprite != sprite)
                {
                    SilhouetteImage.sprite = sprite;
                    SilhouetteImage.enabled = true;
                    currentAlpha = 0f;
                    var c = SilhouetteImage.color; c.a = 0f; SilhouetteImage.color = c;
                }
            }
            else
            {
                hasAssignmentForCurrentType = false;
                if (SilhouetteImage != null)
                {
                    SilhouetteImage.sprite = null;
                    SilhouetteImage.enabled = false;
                    var c = SilhouetteImage.color; c.a = 0f; SilhouetteImage.color = c;
                }
            }
        }
        else if (SilhouetteDict != null && SilhouetteDict.TryGetValue(matchType, out var newImage) && newImage != null)
        {
            hasAssignmentForCurrentType = true;
            if (SilhouetteImage != newImage)
            {
                foreach (var kv in SilhouetteDict)
                {
                    if (kv.Value == null) continue;
                    var cc = kv.Value.color; cc.a = 0f; kv.Value.color = cc;
                }
                SilhouetteImage = newImage;
                if (SilhouetteImage != null)
                {
                    currentAlpha = 0f;
                    var c = SilhouetteImage.color; c.a = 0f; SilhouetteImage.color = c;
                }
            }
        }
        else
        {
            hasAssignmentForCurrentType = false;
            if (SilhouetteDict != null)
            {
                foreach (var kv in SilhouetteDict)
                {
                    if (kv.Value == null) continue;
                    var cc = kv.Value.color; cc.a = 0f; kv.Value.color = cc;
                    kv.Value.enabled = false;
                }
            }
        }
    }

    public void ResetSilhouette()
    {
        hasAssignmentForCurrentType = false;
        if (useSpriteSwap)
        {
            if (SilhouetteImage != null)
            {
                var c = SilhouetteImage.color; c.a = 0f; SilhouetteImage.color = c;
                SilhouetteImage.sprite = null;
                SilhouetteImage.enabled = false;
            }
        }
        else
        {
            if (SilhouetteDict != null)
            {
                foreach (var kv in SilhouetteDict)
                {
                    if (kv.Value == null) continue;
                    var c = kv.Value.color; c.a = 0f; kv.Value.color = c;
                }
            }
            SilhouetteImage = null;
        }
    }
    public void SetTarget(Transform puzzleRoot)
    {
        targetPuzzleTransform = puzzleRoot;
    }
}