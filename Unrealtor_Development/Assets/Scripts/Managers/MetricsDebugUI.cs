using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// In-game overlay to view the last N metrics events. Toggle visibility with F10.
/// Add this component to any GameObject in a scene that has GameplayMetricsCollector events firing.
/// </summary>
public class MetricsDebugUI : MonoBehaviour
{
    [Tooltip("Max lines to keep in the log")]
    public int maxLines = 24;

    [Tooltip("Key to toggle overlay visibility")]
    public KeyCode toggleKey = KeyCode.F10;

    [Tooltip("Show overlay on start (otherwise press toggle key)")]
    public bool visibleOnStart = false;

    private readonly List<string> _lines = new List<string>();
    private bool _visible;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;

    private void OnEnable()
    {
        _visible = visibleOnStart;
        GameplayMetricsCollector.OnMatchComplete += OnMatchComplete;
        GameplayMetricsCollector.OnLevelStart += OnLevelStart;
        GameplayMetricsCollector.OnLevelComplete += OnLevelComplete;
        GameplayMetricsCollector.OnActStart += OnActStart;
        GameplayMetricsCollector.OnActComplete += OnActComplete;
    }

    private void OnDisable()
    {
        GameplayMetricsCollector.OnMatchComplete -= OnMatchComplete;
        GameplayMetricsCollector.OnLevelStart -= OnLevelStart;
        GameplayMetricsCollector.OnLevelComplete -= OnLevelComplete;
        GameplayMetricsCollector.OnActStart -= OnActStart;
        GameplayMetricsCollector.OnActComplete -= OnActComplete;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            _visible = !_visible;
    }

    private void AddLine(string text)
    {
        _lines.Add($"[{Time.time:F0}s] {text}");
        while (_lines.Count > maxLines)
            _lines.RemoveAt(0);
    }

    private void OnMatchComplete(MatchBase.MatchType matchType, int levelId, GameManager.GameState act, float durationSec)
    {
        AddLine($"MatchComplete {matchType} | Lv{levelId} | {act} | {(durationSec >= 0 ? durationSec.ToString("F1") + "s" : "?")}");
    }

    private void OnLevelStart(int levelId)
    {
        AddLine($"LevelStart Lv{levelId}");
    }

    private void OnLevelComplete(int levelId, float durationSec)
    {
        AddLine($"LevelComplete Lv{levelId} | {(durationSec >= 0 ? durationSec.ToString("F1") + "s" : "?")}");
    }

    private void OnActStart(GameManager.GameState act)
    {
        AddLine($"ActStart {act}");
    }

    private void OnActComplete(GameManager.GameState act, float durationSec)
    {
        AddLine($"ActComplete {act} | {(durationSec >= 0 ? durationSec.ToString("F1") + "s" : "?")}");
    }

    private void OnGUI()
    {
        if (!_visible) return;

        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 8, 8),
                fontSize = 12
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = false
            };
        }

        float w = 420f;
        float h = Mathf.Min((_lines.Count + 1) * 18f + 16f, 400f);
        float x = 10f;
        float y = 10f;

        GUI.Box(new Rect(x, y, w, h), "Gameplay Metrics (F10 toggle)", _boxStyle);
        float lineY = y + 22f;
        for (int i = _lines.Count - 1; i >= 0; i--)
        {
            GUI.Label(new Rect(x + 8, lineY, w - 16, 18), _lines[i], _labelStyle);
            lineY += 18f;
        }

        if (!string.IsNullOrEmpty(MetricsLocalLogger.LogFilePath))
        {
            float pathY = y + h + 4f;
            GUI.Box(new Rect(x, pathY, w, 36), "", _boxStyle);
            GUI.Label(new Rect(x + 8, pathY + 4, w - 16, 14), "Log file:", _labelStyle);
            GUI.Label(new Rect(x + 8, pathY + 18, w - 16, 14), MetricsLocalLogger.LogFilePath, _labelStyle);
        }
    }
}
