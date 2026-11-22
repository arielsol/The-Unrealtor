
using Cinemachine;
using System;
using System.Collections.Generic;
//using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public class MatchBase : PuzzleBase
{
    public enum MatchType
    {        
        EXTINGUISHER = 1,
        KEY= 4,
        LAMP= 5,
        JIGSAW= 7, 
        PLUNGER= 8,
        DRAIN= 11,
        EXITSIGN= 13,
        BROCHURE = 14,
        NONE = 100
    }

    [Header("Match Checking Info")]
    [SerializeField] public MatchType MatchTypeEnum;
    [SerializeField] public GameManager.GameState GameStateEnum;
    [SerializeField] public QuadBase leftQuad;
    [SerializeField] public QuadBase rightQuad;
    [SerializeField] public bool HasExternalCondition = false;
    [SerializeField] public PuzzleBase ExternalCondition;
    [SerializeField] public List<MatchBase> MatchPrereqs;
    private BehaviorGraphData BehaviorGraphSO; 
    [SerializeField] public float yThresholdPercent = 1.2f;
    [Tooltip("If players get too close to the puzzle piece, instead of using the angle threshold calulation, " +
             "calculation instead considers if player is within a certain radius.")]
        [SerializeField] public float smallRadiusThreshold = 2.0f;

    [Header("Left Parameters")]
    [SerializeField] public float leftXThresholdPercent = 0.75f;
    [SerializeField] public float leftFacingAngleThreshold = 20f;
    
    [Header("Right Parameters")]
    [SerializeField] public float rightXThresholdPercent = 0.75f;
    [SerializeField] public float rightFacingAngleThreshold = 20f;

    [Header("Post-Match Info: Physical Transitions")]
    [Tooltip("Leave blank if you don't want complete item zoom out cutscene to play")]
    [SerializeField] public DoorBase DoorToOpen ;
    [SerializeField] public GameObject completeItem;
    [SerializeField] public List<GameObject> preCamHideObjects = new List<GameObject>();
    [SerializeField] public List<GameObject> preCamShowObjects = new List<GameObject>();
    [SerializeField] public List<GameObject> postCamHideObj = new List<GameObject>();
    [SerializeField] public List<GameObject> postCamShowObjects = new List<GameObject>();
    [SerializeField] public List<GameEvents.GameActionEnum> TriggeredHouseEvents;
    [SerializeField] public Animator NewObjectAnim;
    [SerializeField] public Animator EnvironmentAnim;
    [SerializeField] public TransformRoom.TransformRoomType TransformRoomEnum = TransformRoom.TransformRoomType.NONE;
    //[SerializeField] public bool DeactivateAfterSolve = false; 

    [Header("Post-Match Info: Camera")]
    [Tooltip("For puzzle cam: Percentage (vertically) of the screen the object will taking up ")]
        [SerializeField] public float heightPercent = 0.3f;
    [Tooltip("For zoom out cam: Manually set the height of the completed object instead of looking at the meshRenderer." +
             " The height of the completed object is used to calculate initial camera position. " +
             "Leave as 0.0 or blank if you want automatic height calculation. " +
             "This is good if the automatic zoom out thinks the object is too big or small")]
        [SerializeField] public float completeItemHeight = 0.0f;
    [Tooltip("For zoom out cam: How far the camera will zoom out")]
        [SerializeField] public float completeItemZoomDist = 2.0f;
    [SerializeField] public PlayableAsset PlayableTimeline;

    

    
    
    //Diego: I commented Hint settings out because we are not using them anymore
    /*
    [Header("Puzzle Hint Settings")]
    [Tooltip("Objects that will have hints for Player 1")]
    public List<GameObject> leftHintObjects = new List<GameObject>();
    [Tooltip("Objects that will have hints for Player 2")]
    public List<GameObject> rightHintObjects = new List<GameObject>();
    public Color hintColor = Color.green;
    [Range(0f, 1f)] public float minIntensity = 0.05f;
    public bool useEmission = true;
    [Range(0f, 5f)] public float emissionBoost = 1.5f;
    public bool driveHintShader = true;
    [SerializeField] public Material hintMaterial;
    public bool swapToHintMaterial = true;
    [Header("Hint Rendering Mode")]
    [Tooltip("Use distance-based saturation shader for hints (Custom/Test). Overrides paintable.")]
    public bool useDistanceSaturationHints = true;
    [SerializeField] public float saturationMinDist = 0f;
    [SerializeField] public float saturationMaxDist = 10f;
    [Tooltip("Tint saturation shader color with hintColor")] public bool saturationTintWithHintColor = true;
    [SerializeField] public string saturationLayerName = "Saturation_Layer";
    */

    [Header("Rendering")]
    [Tooltip("URP renderer index for the overlay camera (should be a stacking-capable color renderer)")]
    [SerializeField] private int overlayRendererIndex = 1;

    [Header("Dev Tools")]
    [SerializeField] public MatchType PrevMatchEnum;
    
    [Header("Post-Match Info: UI")]
    [Tooltip("If true, completing this puzzle does not advance the objective popup index.")]
    [SerializeField] public bool isOptionalPuzzle = false;
    [SerializeField] public string matchText;
    [SerializeField]public string effectText;
    [SerializeField] private Image doorImage;
    [SerializeField] public string completeMatchDialogue = "";
  
    [NonSerialized] public bool isLeftMatched = false;
    [NonSerialized] public bool isRightMatched = false;
    [NonSerialized] public bool isLeftOnScreen = false;
    [NonSerialized] public bool isRightOnScreen = false;

    private List<Renderer> leftRenderers = new List<Renderer>();
    private List<Renderer> rightRenderers = new List<Renderer>();
    private Dictionary<Renderer, MaterialPropertyBlock> propertyBlocks = new Dictionary<Renderer, MaterialPropertyBlock>();
    private Dictionary<Renderer, Color> baseColors = new Dictionary<Renderer, Color>();
    private Dictionary<Renderer, Color> baseEmissions = new Dictionary<Renderer, Color>();
    private Dictionary<Renderer, Material> originalSharedMaterials = new Dictionary<Renderer, Material>();
    private HashSet<Renderer> usingHintSharedMaterial = new HashSet<Renderer>();
    private Dictionary<Renderer, PaintableObject> paintableByRenderer = new Dictionary<Renderer, PaintableObject>();
    private Dictionary<Renderer, Material> saturationMaterialByRenderer = new Dictionary<Renderer, Material>();
    private Dictionary<Renderer, Texture2D> solidBaseMapByRenderer = new Dictionary<Renderer, Texture2D>();
    private Dictionary<GameObject, int> originalLayerByObject = new Dictionary<GameObject, int>();
    private Dictionary<Camera, Camera> overlayByBaseCamera = new Dictionary<Camera, Camera>();
    private int saturationLayer = -1;
    private Dictionary<Camera, int> originalCullingMaskByCamera = new Dictionary<Camera, int>();
    [Header("Debug")]
    [SerializeField] private bool debugHints = false;

    private void LogHint(string msg)
    {
        if (debugHints)
        {
            Debug.Log("[MatchBase] " + gameObject.name + ": " + msg);
        }
    }

    private void Awake()
    {
        if (leftQuad) leftQuad.SetMatchData(this);
        if (rightQuad) rightQuad.SetMatchData(this);

        /*
        yThreshold = Screen.height * yThresholdPercent / 100;
        leftXThreshold = Screen.width * leftXThresholdPercent / 100;
        rightXThreshold = Screen.width * rightXThresholdPercent / 100;*/

        //InitializeRenderers(leftHintObjects, leftRenderers);    //Diego: I removed this becuase we don't use it anymore
        //InitializeRenderers(rightHintObjects, rightRenderers);    //Diego: I removed this becuase we don't use it anymore

        if (completeItem)
        {
            MeshRenderer childMesh =  completeItem.transform.GetComponentInChildren<MeshRenderer>();
            if (childMesh && completeItemHeight == 0.0)
            {
                Bounds bounds = completeItem.transform.GetComponentInChildren<MeshRenderer>().bounds;
                completeItemHeight =Mathf.Max(Mathf.Max( bounds.size.y, bounds.size.x ), bounds.size.z);
                /*completeItemHeight = Mathf.Min(Mathf.Max(bounds.size.y, bounds.size.z),
                    Mathf.Max(bounds.size.x, bounds.size.z), Mathf.Max(bounds.size.y, bounds.size.x));*/
            }
            completeItem.SetActive(false);
        }
        
    }
    

    private void OnEnable() 
    {
        BehaviorGraphSO = MatchManager.Instance.BehaviorGraphSO;
        if (BehaviorGraphSO)
        {
            BehaviorGraphSO.ReceivePuzzleCompletedGraphEvent.Event += OnRecievedMatchCompletedEvent;
        }
        else
        {
            Debug.Log("Missing matchCompletedGraphEvent on" + gameObject.name);
        }
    }


    private void OnValidate()
    {
        #if UNITY_EDITOR
        //useDistanceSaturationHints = true;     //Diego: I removed this becuase we don't use it anymore
        overlayRendererIndex = 1;
        #endif
    }

    private void InitializeRenderers(List<GameObject> objects, List<Renderer> renderersList)
    {
        foreach (var obj in objects)
        {
            if (obj == null) continue;
            var rend = obj.GetComponent<Renderer>();
            if (rend == null) continue;

            var standaloneHint = obj.GetComponent<PuzzleHint>();
            if (standaloneHint != null) standaloneHint.enabled = false;

            renderersList.Add(rend);

            var block = new MaterialPropertyBlock();
            propertyBlocks[rend] = block;

            Color baseColor = rend.material.HasProperty("_BaseColor") ? rend.material.GetColor("_BaseColor") : rend.material.color;
            Color baseEmission = rend.material.HasProperty("_EmissionColor") ? rend.material.GetColor("_EmissionColor") : Color.black;

            baseColors[rend] = baseColor;
            baseEmissions[rend] = baseEmission;
        }
    }


    private void OnDisable()
    {
        //ClearAllHints();     //Diego: I removed this becuase we don't use it anymore

        if (BehaviorGraphSO)
        {
            BehaviorGraphSO.ReceivePuzzleCompletedGraphEvent.Event -= OnRecievedMatchCompletedEvent;
        }
        else
        {
            Debug.Log("Missing matchCompletedGraphEvent on" + gameObject.name);
        }
    }

    
    public bool CheckMatchPrereqs()
    {
        bool isReady = true;
        foreach (MatchBase var in MatchPrereqs)
        {
            if (!var.isSolved)
            {
                isReady = false;
            }
        }
        return isReady;
    }
    
    private void OnRecievedMatchCompletedEvent(PuzzleBase match)
    {
        // Behavior graph should send the same MatchBase instance as CurrMatch; require reference
        // equality so duplicate MatchTypeEnum values in the scene do not all fire MatchComplete.
        if (match is not MatchBase completed || completed != this)
            return;
        if (MatchManager.Instance == null)
            return;

        MatchManager.Instance.MatchComplete(this);
        if (MatchManager.Instance.GeneralUI != null)
            MatchManager.Instance.GeneralUI.TestText2.text = "Match Event on:" + MatchTypeEnum;
    }
    
    private void Update()
    {
        // Silhouette behavior removed from MatchBase
       // ClearAllHints(); //Doiego: Don't use this anmyre
    }


    //Diego: I removed this becuase we don't use it anymore
    /*
   private void UpdatePuzzleHintsForIndex(List<Renderer> renderers, int playerIndex)
{
    if (!TryGetPlayerTransform(playerIndex, out var playerTransform))
    {
        ClearRenderers(renderers);
        return;
    }

    float maxDistance = saturationMaxDist;
    if (maxDistance <= 0.0001f) maxDistance = 10f;
    bool useCutoff = maxDistance > 0.05f;

    bool isShiftPressed = (playerIndex == 0)
        ? Input.GetKey(KeyCode.LeftShift)
        : Input.GetKey(KeyCode.RightShift);

    foreach (var rend in renderers)
    {
        if (rend == null) continue;

        float intensity = 0f;

        if (useCutoff)
        {
            float tRaw = 1f - Vector3.Distance(rend.transform.position, playerTransform.position) / maxDistance;
            if (tRaw > 0f)
            {
                float t = Mathf.Clamp01(tRaw);
                float eased = Mathf.Pow(t, Mathf.Max(0.0001f, 2.0f));
                intensity = Mathf.Lerp(minIntensity, 1f, eased);
            }
        }
        else
        {
            intensity = minIntensity;
        }

        if (isShiftPressed && intensity > 0.75f)
        {
            intensity = 1f;
        }

        ApplyHintIntensity(rend, intensity);
    }
}
*/

    private bool TryGetPlayerTransform(int index, out Transform player)
    {
        player = null;
        var controllers = UnityEngine.Object.FindObjectsOfType<BasePlayerController>();
        if (controllers != null && controllers.Length > index && controllers[index] != null)
        {
            player = controllers[index].transform;
            if (player != null) return true;
        }

        if (Camera.main != null)
        {
            player = Camera.main.transform;
            if (player != null) return true;
        }

        return false;
    }

    //Diego: I removed this becuase we don't use it anymore
    /*
    private void ClearAllHints()
    {
        ClearRenderers(leftRenderers);
        ClearRenderers(rightRenderers);
        if (useDistanceSaturationHints)
        {
            RestoreAllOriginalLayers(leftRenderers);
            RestoreAllOriginalLayers(rightRenderers);
            RemoveAllCinemachineOverlays();
        }
    }

    private void ClearRenderers(List<Renderer> renderers)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            var rend = renderers[i];
            if (rend == null) continue;
            ApplyHintIntensity(rend, 0f);
        }
    }

    private void ApplyHintIntensity(Renderer rend, float intensity)
    {
        if (useDistanceSaturationHints)
        {
            RestoreOriginalLayerIfNeeded(rend);
            RestoreOriginalSharedMaterialIfNeeded(rend);
            return;
        }

        // Non-saturation path: ensure materials are restored; do not tint or change emission.
        RestoreOriginalSharedMaterialIfNeeded(rend);
    }

    private void ApplyHintSharedMaterialIfNeeded(Renderer rend) { }

    private void ApplySaturationSharedMaterialIfNeeded(Renderer rend)
{
    if (rend == null) return;

    if (!originalSharedMaterials.ContainsKey(rend))
    {
        originalSharedMaterials[rend] = rend.sharedMaterial;
    }

    if (saturationMaterialByRenderer.TryGetValue(rend, out var existingMat) && existingMat != null)
    {
        rend.sharedMaterial = existingMat;
        usingHintSharedMaterial.Add(rend);
        TryApplySaturationLayer(rend);
        return;
    }

    // Find the shader
    var shader = Shader.Find("Custom/Test");
    if (shader == null) return;

    Material baseMat = null;
    if (hintMaterial != null && hintMaterial.shader == shader)
    {
        baseMat = hintMaterial;
    }

    var src = originalSharedMaterials[rend];
    if (baseMat == null && src != null && src.shader == shader)
    {
        baseMat = src;
    }

    Material instance = baseMat != null ? new Material(baseMat) : new Material(shader);
    instance.name = (src != null ? src.name + "_satInstance" : "SaturationInstance");

    if (src != null)
    {
        if (src.HasProperty("_BaseMap") && instance.HasProperty("_BaseMap"))
        {
            var t = src.GetTexture("_BaseMap");
            if (t != null) instance.SetTexture("_BaseMap", t);
        }
        if (src.mainTexture != null && instance.HasProperty("_MainTex"))
        {
            instance.SetTexture("_MainTex", src.mainTexture);
        }
    }

    saturationMaterialByRenderer[rend] = instance;
    rend.sharedMaterial = instance;
    usingHintSharedMaterial.Add(rend);
    TryApplySaturationLayer(rend);
}

    
    private void SetSaturationShaderParams(Renderer rend)
    {
       
        if (rend == null) return;
        TryApplySaturationLayer(rend);
    }

    private void RestoreOriginalSharedMaterialIfNeeded(Renderer rend)
    {
        if (rend == null) return;
        if (!usingHintSharedMaterial.Contains(rend)) return;
        if (originalSharedMaterials.TryGetValue(rend, out var original) && original != null)
        {
            rend.sharedMaterial = original;
        }
        usingHintSharedMaterial.Remove(rend);
        RestoreOriginalLayerIfNeeded(rend);
    }

    private void SetPaintableHintColor(Renderer rend, float intensity) { }

    private void EnsureSaturationSetup()
    {
        if (saturationLayer == -1)
        {
            saturationLayer = LayerMask.NameToLayer(saturationLayerName);
            if (saturationLayer == -1)
            {
                int fallback = LayerMask.NameToLayer("Saturation");
                if (fallback != -1)
                {
                    saturationLayer = fallback;
                }
                else
                {
                    Debug.LogWarning("[MatchBase] Saturation layer not found: '" + saturationLayerName + "' (and fallback 'Saturation'). Overlay will render nothing until a layer exists.");
                }
            }
        }

        EnsureCinemachineOverlays();
    }*/

    private void EnsureCinemachineOverlays()
    {
        RemoveAllCinemachineOverlays();
    }

    private System.Collections.Generic.List<Camera> FindSplitScreenCameras(bool leftSide)
    {
        var result = new System.Collections.Generic.List<Camera>();
        var brains = UnityEngine.Object.FindObjectsOfType<Cinemachine.CinemachineBrain>(true);
        for (int i = 0; i < brains.Length; i++)
        {
            var cam = brains[i] != null ? brains[i].GetComponent<Camera>() : null;
            if (cam == null) continue;
            var r = cam.rect;
            bool isLeft = (r.xMax <= 0.51f) || (r.x <= 0.01f && r.width <= 0.51f);
            bool isRight = (r.xMin >= 0.49f) || (r.x >= 0.49f);
            if (leftSide && isLeft) result.Add(cam);
            if (!leftSide && isRight) result.Add(cam);
        }
        return result;
    }

    private void RemoveAllCinemachineOverlays()
    {
        var keys = new List<Camera>(overlayByBaseCamera.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            RemoveOverlayForCamera(keys[i]);
        }
    }

    /*
    private void TryApplySaturationLayer(Renderer rend)
    {
        if (rend == null) return;
        if (saturationLayer == -1) return;
        if (!AnyOverlayExists()) return;
        var go = rend.gameObject;
        if (!originalLayerByObject.ContainsKey(go))
        {
            originalLayerByObject[go] = go.layer;
        }
        go.layer = saturationLayer;
    }*/

    private void RestoreOriginalLayerIfNeeded(Renderer rend)
    {
        if (rend == null) return;
        var go = rend.gameObject;
        if (originalLayerByObject.TryGetValue(go, out var layer))
        {
            go.layer = layer;
            originalLayerByObject.Remove(go);
        }
    }

    private void RestoreAllOriginalLayers(List<Renderer> renderers)
    {
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            RestoreOriginalLayerIfNeeded(rend);
        }
    }

    private bool AnyOverlayExists()
    {
        foreach (var kv in overlayByBaseCamera)
        {
            if (kv.Value != null) return true;
        }
        return false;
    }

    private void EnsureOverlayForCamera(Camera baseCam)
    {
        if (baseCam == null) return;
        if (overlayByBaseCamera.ContainsKey(baseCam)) return;

        var overlayGo = new GameObject(baseCam.gameObject.name + "_SaturationOverlay");
        var overlayCam = overlayGo.AddComponent<Camera>();
        overlayCam.CopyFrom(baseCam);
        overlayCam.cullingMask = (saturationLayer == -1) ? 0 : (1 << saturationLayer);
        overlayCam.clearFlags = CameraClearFlags.Nothing;
        overlayCam.depth = baseCam.depth + 1f;
        overlayGo.transform.SetParent(baseCam.transform, false);
        overlayGo.transform.localPosition = Vector3.zero;
        overlayGo.transform.localRotation = Quaternion.identity;
        overlayGo.transform.localScale = Vector3.one;

        var baseData = baseCam.GetComponent<UniversalAdditionalCameraData>();
        if (baseData == null)
        {
            baseData = baseCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        }
        var overlayData = overlayCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        overlayData.renderType = CameraRenderType.Overlay;
        overlayData.renderPostProcessing = false;
        overlayData.volumeLayerMask = 0;
        overlayData.renderShadows = false;
        if (overlayRendererIndex < 0) overlayRendererIndex = 0;
        overlayData.SetRenderer(overlayRendererIndex);
        baseData.cameraStack.Add(overlayCam);

        overlayByBaseCamera[baseCam] = overlayCam;

        if (!originalCullingMaskByCamera.ContainsKey(baseCam))
        {
            originalCullingMaskByCamera[baseCam] = baseCam.cullingMask;
        }
        if (saturationLayer != -1)
        {
            baseCam.cullingMask &= ~(1 << saturationLayer);
        }
    }

    private void RemoveOverlayForCamera(Camera baseCam)
    {
        if (baseCam == null) return;
        if (!overlayByBaseCamera.TryGetValue(baseCam, out var overlayCam) || overlayCam == null) return;
        var baseData = baseCam.GetComponent<UniversalAdditionalCameraData>();
        if (baseData != null)
        {
            baseData.cameraStack.Remove(overlayCam);
        }
        DestroySafe(overlayCam.gameObject);
        overlayByBaseCamera.Remove(baseCam);

        if (originalCullingMaskByCamera.TryGetValue(baseCam, out var mask))
        {
            baseCam.cullingMask = mask;
            originalCullingMaskByCamera.Remove(baseCam);
        }
    }

    private void DestroySafe(GameObject go)
    {
        if (go == null) return;
        #if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () => { if (go != null) GameObject.DestroyImmediate(go); };
            return;
        }
        #endif
        GameObject.Destroy(go);
    }
    
    public void ShowLeftSilhouette()
    {
    }
    public void ShowRightSilhouette()
    {
    }
    public void ShowBothSilhouettes()
    {
    }
    public void HideSilhouettes()
    {
    }

}

