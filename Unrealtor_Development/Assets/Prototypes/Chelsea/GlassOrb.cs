using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

public class GlassOrb : MonoBehaviour
{
    [Header("References")]
    public Transform playerCamera;          // optional: inspector override
    public Camera orbCamera;
    public RawImage orbOverlay;
    public Shader orbLensShader;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.V;
    public bool startsActive = false;
    [Range(0.1f, 0.9f)] public float radiusNormalized = 0.33f;
    [Range(0.0f, 1.0f)] public float edgeFeather = 0.01f;
    [Range(-1.0f, 1.0f)] public float distortion = 0.35f;
    public bool invertInside = true;
    public Color outlineColor = new Color(0.8f, 0.9f, 1.0f, 0.85f);
    [Range(0.0f, 0.05f)] public float outlineThickness = 0.01f;
    public Vector2 centerNormalized = new Vector2(0.5f, 0.5f);
    public LayerMask orbCullingMask = ~0;
    public bool matchMainCameraFov = true;

    [Header("Behavior")]
    [Tooltip("If true, orb FOV will lerp smoothly to match main camera.")]
    public bool smoothFov = true;
    [Tooltip("How fast the orb FOV interpolates to match the main camera.")]
    public float fovLerpSpeed = 10f;

    public bool showResolutionInName = false;

    private RenderTexture orbTexture;
    private Material orbMaterial;
    private Canvas overlayCanvas;
    private bool isActive;
    private int cachedWidth;
    private int cachedHeight;

    // Resolved runtime refs
    private Camera mainUnityCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCam; // optional if you want to assign one

    void Awake()
    {
        // If user didn't set a playerCamera, try Camera.main
        if (playerCamera == null)
        {
            Camera cam = Camera.main;
            if (cam != null) playerCamera = cam.transform;
        }

        // Resolve mainUnityCamera: prefer Camera.main (the actual render camera)
        mainUnityCamera = Camera.main;

        // If Camera.main isn't available yet, try to grab camera from playerCamera (if provided)
        if (mainUnityCamera == null && playerCamera != null)
        {
            mainUnityCamera = playerCamera.GetComponent<Camera>();
        }

        // Try to resolve a virtual camera from the playerCamera transform if present
        if (virtualCam == null && playerCamera != null)
        {
            virtualCam = playerCamera.GetComponent<CinemachineVirtualCamera>();
        }

        if (mainUnityCamera == null)
        {
            Debug.LogWarning("[GlassOrb] No main camera found (Camera.main is null). Orb will not follow or match FOV until a main camera exists.");
        }

        EnsureCamera();
        EnsureOverlay();
        RecreateRenderTextureIfNeeded(true);
        ApplyMaterialParams();
    }

    void Start()
    {
        SetActive(startsActive);
    }

    void OnDestroy()
    {
        CleanupRenderTexture();
        if (orbMaterial != null)
        {
            Destroy(orbMaterial);
            orbMaterial = null;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetActive(!isActive);
        }

        if (!isActive) return;

        // If we have a main render camera, use it as the authoritative transform & FOV.
        // This works with Cinemachine because Cinemachine ultimately drives the real camera.
        Camera sourceCam = mainUnityCamera;
        if (sourceCam == null)
        {
            // fallback to virtualCam's lens only if no main Unity camera is present
            if (virtualCam != null)
            {
                // virtualCam exists but there is no Camera.main (rare). Use its lens FOV.
                if (matchMainCameraFov && orbCamera != null)
                {
                    if (smoothFov)
                        orbCamera.fieldOfView = Mathf.Lerp(orbCamera.fieldOfView, virtualCam.m_Lens.FieldOfView, Time.deltaTime * fovLerpSpeed);
                    else
                        orbCamera.fieldOfView = virtualCam.m_Lens.FieldOfView;
                }

                // virtual camera does not have a world transform that is used for rendering,
                // so we can't reliably follow its transform for position/rotation. Skip position sync.
            }
            else if (playerCamera != null)
            {
                // last resort: use the inspector-provided transform (if it has a Camera component)
                Camera pc = playerCamera.GetComponent<Camera>();
                if (pc != null) sourceCam = pc;
            }
        }

        if (sourceCam != null && orbCamera != null)
        {
            // Match FOV (smooth or instant)
            if (matchMainCameraFov)
            {
                if (smoothFov)
                {
                    orbCamera.fieldOfView = Mathf.Lerp(orbCamera.fieldOfView, sourceCam.fieldOfView, Time.deltaTime * fovLerpSpeed);
                }
                else
                {
                    orbCamera.fieldOfView = sourceCam.fieldOfView;
                }
            }

            // Follow the actual rendering camera's world position/rotation
            orbCamera.transform.position = sourceCam.transform.position;
            orbCamera.transform.rotation = sourceCam.transform.rotation;
        }

        RecreateRenderTextureIfNeeded();
        ApplyMaterialParams();
    }

    public void SetActive(bool active)
    {
        isActive = active;
        if (orbCamera != null) orbCamera.enabled = active;
        if (overlayCanvas != null) overlayCanvas.enabled = active;
        if (orbOverlay != null) orbOverlay.enabled = active;
    }

    private void EnsureCamera()
    {
        if (orbCamera == null)
        {
            GameObject camObj = new GameObject("GlassOrbCamera");
            orbCamera = camObj.AddComponent<Camera>();
            orbCamera.clearFlags = CameraClearFlags.SolidColor;
            orbCamera.backgroundColor = Color.black;
            orbCamera.cullingMask = orbCullingMask;
            orbCamera.enabled = false;
        }
        else
        {
            orbCamera.cullingMask = orbCullingMask;
        }
    }

    private void EnsureOverlay()
    {
        if (orbLensShader == null)
        {
            orbLensShader = Shader.Find("Hidden/GlassOrbLens");
            if (orbLensShader == null)
            {
                Debug.LogWarning("[GlassOrb] Shader 'Hidden/GlassOrbLens' not found. Make sure your shader exists and is included in build.");
            }
        }

        if (orbMaterial == null)
        {
            if (orbLensShader != null)
                orbMaterial = new Material(orbLensShader);
            else
                orbMaterial = new Material(Shader.Find("UI/Default")); // fallback so we don't crash
        }

        if (orbOverlay == null)
        {
            GameObject canvasObj = new GameObject("GlassOrbOverlay");
            overlayCanvas = canvasObj.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            GameObject imgObj = new GameObject("Orb");
            imgObj.transform.SetParent(canvasObj.transform, false);
            orbOverlay = imgObj.AddComponent<RawImage>();
            orbOverlay.raycastTarget = false;
            orbOverlay.material = orbMaterial;
            RectTransform rt = orbOverlay.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            overlayCanvas = orbOverlay.canvas;
            orbOverlay.material = orbMaterial;
        }
    }

    private void RecreateRenderTextureIfNeeded(bool force = false)
    {
        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);
        if (!force && w == cachedWidth && h == cachedHeight && orbTexture != null) return;

        cachedWidth = w;
        cachedHeight = h;

        CleanupRenderTexture();
        orbTexture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
        orbTexture.name = showResolutionInName ? $"GlassOrb_{w}x{h}" : "GlassOrb";

        if (orbCamera != null) orbCamera.targetTexture = orbTexture;
        if (orbOverlay != null) orbOverlay.texture = orbTexture;
    }

    private void CleanupRenderTexture()
    {
        if (orbCamera != null) orbCamera.targetTexture = null;
        if (orbOverlay != null) orbOverlay.texture = null;
        if (orbTexture != null)
        {
            orbTexture.Release();
            Destroy(orbTexture);
            orbTexture = null;
        }
    }

    private void ApplyMaterialParams()
    {
        if (orbMaterial == null) return;
        orbMaterial.SetTexture("_SceneTex", orbTexture);
        orbMaterial.SetVector("_Center", centerNormalized);
        orbMaterial.SetFloat("_Radius", Mathf.Clamp01(radiusNormalized));
        orbMaterial.SetFloat("_Edge", Mathf.Max(0.0001f, edgeFeather));
        orbMaterial.SetFloat("_Distortion", distortion);
        orbMaterial.SetFloat("_InvertInside", invertInside ? 1f : 0f);
        orbMaterial.SetColor("_OutlineColor", outlineColor);
        orbMaterial.SetFloat("_OutlineThickness", Mathf.Max(0f, outlineThickness));
    }
}