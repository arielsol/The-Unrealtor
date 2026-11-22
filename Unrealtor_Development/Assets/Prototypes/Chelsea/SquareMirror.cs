using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Unrealtor/Visual Tools/Square Mirror")]
public class SquareMirror : MonoBehaviour
{
	[Header("References")]
	public Transform playerCamera;
	public Camera mirrorCamera;
	public RawImage mirrorOverlay;

	[Header("Settings")]
	public bool startsActive = false;
	public bool matchMainCameraFov = true;
	public LayerMask mirrorCullingMask = ~0;
	[Range(0.1f, 1.0f)] public float widthNormalized = 0.35f;
	[Range(0.1f, 1.0f)] public float heightNormalized = 0.35f;
	public Vector2 anchorNormalized = new Vector2(0.5f, 0.5f);

	public bool showResolutionInName = false;

	private RenderTexture mirrorRT;
	private bool isActive;
	private int cachedW;
	private int cachedH;

	void Awake()
	{
		if (playerCamera == null)
		{
			Camera cam = Camera.main;
			if (cam != null) playerCamera = cam.transform;
		}
		EnsureCamera();
		EnsureOverlay();
		RecreateRT(true);
	}

	void Start()
	{
		SetActive(startsActive);
	}

	void OnDestroy()
	{
		CleanupRT();
	}

	void Update()
	{
		if (!isActive) return;

		if (matchMainCameraFov && playerCamera != null && mirrorCamera != null)
		{
			Camera mainCam = playerCamera.GetComponent<Camera>();
			if (mainCam != null) mirrorCamera.fieldOfView = mainCam.fieldOfView;
		}

		if (playerCamera != null && mirrorCamera != null)
		{
			mirrorCamera.transform.position = playerCamera.position;
			mirrorCamera.transform.rotation = playerCamera.rotation;
		}

		RecreateRT();
	}

	void OnValidate()
	{
		EnsureCamera();
		EnsureOverlay();
		RecreateRT(true);
		if (mirrorCamera != null) mirrorCamera.enabled = isActive;
		if (mirrorOverlay != null) mirrorOverlay.enabled = isActive;
	}

	public void SetActive(bool active)
	{
		isActive = active;
		if (active)
		{
			// Ensure resources exist and bind a fresh RT immediately to avoid blank UI
			EnsureCamera();
			EnsureOverlay();
			RecreateRT(true);
		}
		else
		{
			CleanupRT();
		}
		if (mirrorCamera != null) mirrorCamera.enabled = active;
		if (mirrorOverlay != null) mirrorOverlay.enabled = active;
	}

	private void EnsureCamera()
	{
		if (mirrorCamera == null)
		{
			GameObject camObj = new GameObject("SquareMirrorCamera");
			mirrorCamera = camObj.AddComponent<Camera>();
			mirrorCamera.enabled = false;
		}
		mirrorCamera.clearFlags = CameraClearFlags.SolidColor;
		mirrorCamera.backgroundColor = Color.black;
		mirrorCamera.cullingMask = mirrorCullingMask;
	}

	private void EnsureOverlay()
	{
		if (mirrorOverlay == null)
		{
			GameObject canvasObj = new GameObject("SquareMirrorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			Canvas canvas = canvasObj.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			GameObject imgObj = new GameObject("SquareMirror");
			imgObj.transform.SetParent(canvasObj.transform, false);
			mirrorOverlay = imgObj.AddComponent<RawImage>();
			mirrorOverlay.raycastTarget = false;
		}

		RectTransform rt = mirrorOverlay.rectTransform;
		rt.anchorMin = rt.anchorMax = anchorNormalized;
		float w = Mathf.Round(Screen.width * widthNormalized);
		float h = Mathf.Round(Screen.height * heightNormalized);
		rt.sizeDelta = new Vector2(w, h);
		rt.anchoredPosition = Vector2.zero;
	}

	private void RecreateRT(bool force = false)
	{
		int w = Mathf.Max(1, Screen.width);
		int h = Mathf.Max(1, Screen.height);
		if (!force && mirrorRT != null && w == cachedW && h == cachedH) return;

		cachedW = w;
		cachedH = h;

		CleanupRT();
		mirrorRT = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
		mirrorRT.name = showResolutionInName ? $"SquareMirror_{w}x{h}" : "SquareMirror";
		mirrorCamera.targetTexture = mirrorRT;
		if (mirrorOverlay != null) mirrorOverlay.texture = mirrorRT;
	}

	private void CleanupRT()
	{
		if (mirrorCamera != null) mirrorCamera.targetTexture = null;
		if (mirrorOverlay != null) mirrorOverlay.texture = null;
		if (mirrorRT != null)
		{
			mirrorRT.Release();
			Destroy(mirrorRT);
			mirrorRT = null;
		}
	}
}


