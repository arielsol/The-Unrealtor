using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Subscribes to GameplayMetricsCollector events and appends each to a local JSONL file.
/// File: Application.persistentDataPath / GameplayMetrics.jsonl
/// Open with Excel (import as CSV with delimiter newline, then parse JSON column) or any JSON viewer.
/// </summary>
public class MetricsLocalLogger : MonoBehaviour
{
    [Tooltip("Enable writing metrics to local file")]
    public bool enableFileLog = true;

    [Tooltip("Filename under persistentDataPath")]
    public string fileName = "GameplayMetrics.jsonl";

    private string _filePath;
    private static readonly object _writeLock = new object();
    private bool _hasWrittenOnce;

    /// <summary>
    /// Full path to the log file (set after Awake). Use this to find GameplayMetrics.jsonl.
    /// </summary>
    public static string LogFilePath { get; private set; }

    private void Awake()
    {
        _filePath = Path.Combine(Application.persistentDataPath, fileName);
        LogFilePath = _filePath;
        if (enableFileLog)
            Debug.Log($"[MetricsLocalLogger] Log file will be: {_filePath}");
    }

    private void OnEnable()
    {
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

    private void OnMatchComplete(MatchBase.MatchType matchType, int levelId, GameManager.GameState act, float durationSec)
    {
        if (!enableFileLog) return;
        string line = $"{{\"time\":{Time.time:F2},\"event\":\"MatchComplete\",\"matchType\":\"{matchType}\",\"levelId\":{levelId},\"act\":\"{act}\",\"durationSec\":{(durationSec >= 0 ? durationSec.ToString("F1") : "null")}}}\n";
        AppendLine(line);
    }

    private void OnLevelStart(int levelId)
    {
        if (!enableFileLog) return;
        string line = $"{{\"time\":{Time.time:F2},\"event\":\"LevelStart\",\"levelId\":{levelId}}}\n";
        AppendLine(line);
    }

    private void OnLevelComplete(int levelId, float durationSec)
    {
        if (!enableFileLog) return;
        string line = $"{{\"time\":{Time.time:F2},\"event\":\"LevelComplete\",\"levelId\":{levelId},\"durationSec\":{(durationSec >= 0 ? durationSec.ToString("F1") : "null")}}}\n";
        AppendLine(line);
    }

    private void OnActStart(GameManager.GameState act)
    {
        if (!enableFileLog) return;
        string line = $"{{\"time\":{Time.time:F2},\"event\":\"ActStart\",\"act\":\"{act}\"}}\n";
        AppendLine(line);
    }

    private void OnActComplete(GameManager.GameState act, float durationSec)
    {
        if (!enableFileLog) return;
        string line = $"{{\"time\":{Time.time:F2},\"event\":\"ActComplete\",\"act\":\"{act}\",\"durationSec\":{(durationSec >= 0 ? durationSec.ToString("F1") : "null")}}}\n";
        AppendLine(line);
    }

    private void AppendLine(string line)
    {
        try
        {
            lock (_writeLock)
            {
                File.AppendAllText(_filePath, line);
                if (!_hasWrittenOnce)
                {
                    _hasWrittenOnce = true;
                    Debug.Log($"[MetricsLocalLogger] First write OK. File at: {_filePath}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MetricsLocalLogger] Write failed: {e.Message}");
        }
    }

    /// <summary>
    /// Call from Editor or a button: open the folder containing the log file.
    /// </summary>
    public static void OpenLogFolder()
    {
        string path = Application.persistentDataPath;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(path);
#else
        Application.OpenURL("file://" + path);
#endif
    }
}
