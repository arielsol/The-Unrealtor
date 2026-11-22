using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityCommunity.UnitySingleton;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveSystem : MonoSingleton<ObjectiveSystem>
{
    private const float FadeDuration = 1f;

    [System.Serializable]
    public class Objective
    {
        public string id;
        [TextArea(1, 3)] public string title;
        public Sprite icon;
        [System.NonSerialized] public bool isCompleted;
    }

    [SerializeField] private List<Objective> objectives = new List<Objective>();

    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Image iconImage;
    [Tooltip("Optional root with CanvasGroup to fade the whole objective block. If null, title and icon are faded individually.")]
    [SerializeField] private GameObject objectiveUIRoot;
    [Tooltip("Time the UI stays hidden before the next objective appears.")]
    [SerializeField] private float nextObjectiveDelay = 10f;
    public int CurrentIndex { get; private set; } = -1;
    private Coroutine _delayedShowCoroutine;
    private Coroutine _fadeCoroutine;
    public Objective Current => (CurrentIndex >= 0 && CurrentIndex < objectives.Count) ? objectives[CurrentIndex] : null;

    private void Start()
    {
        if (objectives.Count > 0) SetObjective(0, immediate: true);
        else RefreshUI();
    }

    public void NextObjective()
    {
        CompleteCurrentObjectiveInternal();
        int next = Mathf.Clamp(CurrentIndex + 1, 0, objectives.Count);
        if (next < objectives.Count) SetObjective(next);
        else SetNoActiveObjective();
    }

    public void SetObjective(int index, bool immediate = false)
    {
        if (index < 0 || index >= objectives.Count) return;
        if (_delayedShowCoroutine != null)
        {
            StopCoroutine(_delayedShowCoroutine);
            _delayedShowCoroutine = null;
        }
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        if (immediate || nextObjectiveDelay <= 0f)
        {
            CurrentIndex = index;
            SetObjectiveAlpha(1f);
            RefreshUI();
            return;
        }
        _delayedShowCoroutine = StartCoroutine(DelayedSetObjective(index));
    }

    private IEnumerator DelayedSetObjective(int index)
    {
        yield return FadeOutObjectiveUI();
        HideObjectiveUI();
        yield return new WaitForSeconds(nextObjectiveDelay);
        _delayedShowCoroutine = null;
        CurrentIndex = index;
        RefreshUI();
        yield return FadeInObjectiveUI();
    }

    private IEnumerator FadeOutObjectiveUI()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeObjectiveUI(1f, 0f, FadeDuration));
        yield return _fadeCoroutine;
        _fadeCoroutine = null;
    }

    private IEnumerator FadeInObjectiveUI()
    {
        SetObjectiveAlpha(0f);
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeObjectiveUI(0f, 1f, FadeDuration));
        yield return _fadeCoroutine;
        _fadeCoroutine = null;
    }

    private IEnumerator FadeObjectiveUI(float from, float to, float duration)
    {
        CanvasGroup cg = objectiveUIRoot != null ? objectiveUIRoot.GetComponent<CanvasGroup>() : null;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, t);
            if (cg != null)
                cg.alpha = alpha;
            else
                SetObjectiveAlpha(alpha);
            yield return null;
        }
        if (cg != null)
            cg.alpha = to;
        else
            SetObjectiveAlpha(to);
    }

    private void SetObjectiveAlpha(float alpha)
    {
        var cg = objectiveUIRoot != null ? objectiveUIRoot.GetComponent<CanvasGroup>() : null;
        if (cg != null)
        {
            cg.alpha = alpha;
            return;
        }
        if (titleText != null)
        {
            var c = titleText.color;
            titleText.color = new Color(c.r, c.g, c.b, alpha);
        }
        if (iconImage != null)
        {
            var c = iconImage.color;
            iconImage.color = new Color(c.r, c.g, c.b, alpha);
        }
    }

    private void HideObjectiveUI()
    {
        if (titleText != null) titleText.text = "";
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    public void SetObjectiveById(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        int idx = objectives.FindIndex(o => o != null && o.id == id);
        if (idx >= 0) SetObjective(idx);
    }

    public void SetNoActiveObjective()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        _fadeCoroutine = StartCoroutine(FadeOutThenClearObjective());
    }

    private IEnumerator FadeOutThenClearObjective()
    {
        yield return FadeOutObjectiveUI();
        HideObjectiveUI();
        CurrentIndex = -1;
        RefreshUI();
        SetObjectiveAlpha(1f);
        _fadeCoroutine = null;
    }

    private void CompleteCurrentObjectiveInternal()
    {
        if (CurrentIndex >= 0 && CurrentIndex < objectives.Count)
        {
            var obj = objectives[CurrentIndex];
            if (obj != null && !obj.isCompleted) obj.isCompleted = true;
        }
    }

    private void RefreshUI()
    {
        if (titleText != null) titleText.text = Current != null ? (Current.title ?? "") : "";
        if (iconImage != null)
        {
            iconImage.sprite = Current != null ? Current.icon : null;
            iconImage.enabled = (iconImage.sprite != null);
        }
    }
}

