using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public class LetterboxBarsOverlayUI : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Width/Height aspect, e.g., 2.39 means 2.39:1 (cinema scope)")]
    public float targetAspect = 2.39f;
    public Color barColor = Color.black;

    [Range(0f, 1f)]
    [Tooltip("Extra crop on top of the calculated bar height (0 = exact fit).")]
    public float extraCrop = 0f;

    [Header("Animation")]
    public bool animateOnEnable = false;
    [Tooltip("Seconds to animate from no bars to full bars.")]
    public float animateDuration = 0.25f;

    [Header("Sorting")]
    [Tooltip("Canvas Sort Order for the overlay bars (higher draws on top).")]
    public int sortingOrder = -100;

    // internals
    private Canvas _canvas;
    private RectTransform _top;
    private RectTransform _bottom;
    private Image _topImg;
    private Image _botImg;

    private int _lastW, _lastH;
    private float _animT = 1f; // 1 = full bars

    void Reset()
    {
        targetAspect = 2.39f;
        barColor = Color.black;
        sortingOrder = -100;
    }

    void OnEnable()
    {
        if (_canvas == null) Build();

        _animT = animateOnEnable ? 0f : 1f;
        ForceRefresh();
    }

    void Update()
    {
        // React to resolution/aspect changes
        if (Screen.width != _lastW || Screen.height != _lastH)
            ForceRefresh();

        // Simple open animation (unscaled so Timescale=0 cutscenes still animate)
        if (_animT < 1f)
        {
            _animT = Mathf.Min(1f, _animT + Time.unscaledDeltaTime / Mathf.Max(0.0001f, animateDuration));
            ApplyBars();
        }
    }

    private void Build()
    {
        // Create overlay canvas to cover BOTH split-screen cameras
        GameObject cgo = new GameObject("LetterboxCanvas_Overlay",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);

        _canvas = cgo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = sortingOrder; // <— Inspector-driven order

        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Create top bar
        _top = CreateBar("TopBar", out _topImg);
        _top.anchorMin = new Vector2(0f, 1f);
        _top.anchorMax = new Vector2(1f, 1f);
        _top.pivot = new Vector2(0.5f, 1f);

        // Create bottom bar
        _bottom = CreateBar("BottomBar", out _botImg);
        _bottom.anchorMin = new Vector2(0f, 0f);
        _bottom.anchorMax = new Vector2(1f, 0f);
        _bottom.pivot = new Vector2(0.5f, 0f);

        _topImg.color = barColor;
        _botImg.color = barColor;
    }

    private RectTransform CreateBar(string name, out Image img)
    {
        GameObject go = new GameObject(name, typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        img = go.GetComponent<Image>();
        img.raycastTarget = false; // never block clicks
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    private void ForceRefresh()
    {
        _lastW = Screen.width;
        _lastH = Screen.height;
        ApplyBars(true);
    }

    private void ApplyBars(bool instant = false)
    {
        if (_canvas == null) return;

        float screenW = _canvas.pixelRect.width;
        float screenH = _canvas.pixelRect.height;
        if (screenW <= 0f || screenH <= 0f) return;

        // Desired visible height if we keep full width at target aspect
        float desiredHeight = screenW / Mathf.Max(0.0001f, targetAspect);
        float totalLetterbox = Mathf.Max(0f, screenH - desiredHeight);
        float halfBar = 0.5f * totalLetterbox;

        // Optional extra crop as a fraction of full screen height
        halfBar += extraCrop * 0.5f * screenH;

        // Apply animation factor
        float h = halfBar * Mathf.Clamp01(_animT);

        _top.sizeDelta = new Vector2(0f, h);
        _bottom.sizeDelta = new Vector2(0f, h);

        // Keep color in sync at runtime
        if (_topImg.color != barColor) _topImg.color = barColor;
        if (_botImg.color != barColor) _botImg.color = barColor;
    }

    // Public controls for cutscenes/state changes
    public void ShowBars(float duration = 0.25f)
    {
        animateDuration = duration;
        _animT = 0f;
    }

    public void HideBars(float duration = 0.25f)
    {
        animateDuration = duration;
        StartCoroutine(HideRoutine());
    }

    private System.Collections.IEnumerator HideRoutine()
    {
        float startT = _animT;
        float t = 0f;
        while (t < animateDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / Mathf.Max(0.0001f, animateDuration));
            _animT = a * startT;
            ApplyBars();
            yield return null;
        }
        _animT = 0f;
        ApplyBars();
    }
}
