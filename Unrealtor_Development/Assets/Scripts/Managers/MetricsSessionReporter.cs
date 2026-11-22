using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Buffers gameplay metrics for the whole play session and sends ONE report when the app quits (and also on pause).
///
/// Attach to a persistent object (e.g. the GameManager singleton GameObject).
/// This is intentionally independent from MetricsLocalLogger so you can keep local JSONL while also sending a session summary.
/// </summary>
public class MetricsSessionReporter : MonoBehaviour
{
    public enum TransportMode
    {
        BackendApi = 0,
        GoogleSheetDirect = 1
    }

    [Header("Transport")]
    [Tooltip("Choose where the session report is posted.")]
    public TransportMode transportMode = TransportMode.BackendApi;

    [Header("Endpoint")]
    [Tooltip("Example: https://api.your-backend.com")]
    public string baseUrl = "";

    [Tooltip("Example: /metrics/session")]
    public string path = "/metrics/session";

    [Tooltip("Optional. Example: Bearer <token>")]
    public string authorizationHeader = "";

    [Header("Google Sheet Direct (Webhook)")]
    [Tooltip("Direct Google Sheet endpoint (usually an Apps Script Web App URL).")]
    public string googleSheetUrl = "";

    [Tooltip("Optional auth header for Google Sheet endpoint.")]
    public string googleSheetAuthorizationHeader = "";

    [Header("Behavior")]
    [Tooltip("If true, also attempt to send when the app is backgrounded (recommended for mobile).")]
    public bool sendOnPause = true;

    [Tooltip("If true, writes the session report JSON to persistentDataPath as a backup.")]
    public bool writeBackupJson = true;

    [Header("Debug")]
    public bool verboseLogs = true;

    private string _sessionId;
    private string _sessionStartUtc;
    private float _sessionStartTime;
    private bool _hasSent;
    private string _backupPath;

    private readonly List<MetricEvent> _events = new List<MetricEvent>(256);

    private string EndpointUrl => baseUrl.TrimEnd('/') + path;

    [Serializable]
    private class MetricEvent
    {
        public string eventType;
        public float t; // seconds since session start (Time.time)

        public int levelId;         // optional (0 if unused)
        public bool hasLevelId;

        public string act;          // optional
        public bool hasAct;

        public string matchType;    // optional
        public bool hasMatchType;

        public float durationSec;   // optional
        public bool hasDurationSec;
    }

    [Serializable]
    private class SessionReport
    {
        public string schemaVersion = "1.0";
        public string sessionId;
        public string startedAtUtc;
        public string endedAtUtc;
        public string appVersion;
        public string unityVersion;
        public string platform;

        public float totalPlayTimeSec;

        public int totalEvents;
        public int matchCompleteCount;
        public int levelCompleteCount;
        public int actCompleteCount;

        public List<MetricEvent> events;
    }

    private void Awake()
    {
        _sessionId = Guid.NewGuid().ToString("N");
        _sessionStartUtc = DateTime.UtcNow.ToString("o");
        _sessionStartTime = Time.time;

        _backupPath = Path.Combine(Application.persistentDataPath, $"GameplaySessionReport_{_sessionId}.json");
        if (verboseLogs)
        {
            Debug.Log($"[MetricsSessionReporter] Session started. sessionId={_sessionId}");
            if (writeBackupJson) Debug.Log($"[MetricsSessionReporter] Backup path: {_backupPath}");
        }
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

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!sendOnPause) return;
        if (!pauseStatus) return;
        TryFinalizeAndSendOnce(reason: "pause");
    }

    private void OnApplicationQuit()
    {
        TryFinalizeAndSendOnce(reason: "quit");
    }

    private void OnMatchComplete(MatchBase.MatchType matchType, int levelId, GameManager.GameState act, float durationSec)
    {
        var e = NewEvent("MatchComplete");
        e.hasLevelId = true;
        e.levelId = levelId;
        e.hasAct = true;
        e.act = act.ToString();
        e.hasMatchType = true;
        e.matchType = matchType.ToString();
        if (durationSec >= 0f)
        {
            e.hasDurationSec = true;
            e.durationSec = durationSec;
        }
        _events.Add(e);
    }

    private void OnLevelStart(int levelId)
    {
        var e = NewEvent("LevelStart");
        e.hasLevelId = true;
        e.levelId = levelId;
        _events.Add(e);
    }

    private void OnLevelComplete(int levelId, float durationSec)
    {
        var e = NewEvent("LevelComplete");
        e.hasLevelId = true;
        e.levelId = levelId;
        if (durationSec >= 0f)
        {
            e.hasDurationSec = true;
            e.durationSec = durationSec;
        }
        _events.Add(e);
    }

    private void OnActStart(GameManager.GameState act)
    {
        var e = NewEvent("ActStart");
        e.hasAct = true;
        e.act = act.ToString();
        _events.Add(e);
    }

    private void OnActComplete(GameManager.GameState act, float durationSec)
    {
        var e = NewEvent("ActComplete");
        e.hasAct = true;
        e.act = act.ToString();
        if (durationSec >= 0f)
        {
            e.hasDurationSec = true;
            e.durationSec = durationSec;
        }
        _events.Add(e);
    }

    private MetricEvent NewEvent(string eventType)
    {
        return new MetricEvent
        {
            eventType = eventType,
            t = Time.time
        };
    }

    private void TryFinalizeAndSendOnce(string reason)
    {
        if (_hasSent) return;
        _hasSent = true;

        var report = BuildReport();
        string json = SerializeReport(report);

        if (writeBackupJson)
        {
            try
            {
                File.WriteAllText(_backupPath, json);
                if (verboseLogs) Debug.Log($"[MetricsSessionReporter] Wrote backup report: {_backupPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MetricsSessionReporter] Failed writing backup report: {ex.Message}");
            }
        }

        string targetUrl;
        string targetAuth;
        if (!TryResolveTarget(out targetUrl, out targetAuth))
        {
            return;
        }

        if (verboseLogs)
        {
            Debug.Log($"[MetricsSessionReporter] Sending session report ({reason}) via {transportMode} to {targetUrl}. events={report.totalEvents}");
        }
        StartCoroutine(PostJson(targetUrl, json, targetAuth));
    }

    private SessionReport BuildReport()
    {
        int matchComplete = 0;
        int levelComplete = 0;
        int actComplete = 0;
        for (int i = 0; i < _events.Count; i++)
        {
            switch (_events[i].eventType)
            {
                case "MatchComplete": matchComplete++; break;
                case "LevelComplete": levelComplete++; break;
                case "ActComplete": actComplete++; break;
            }
        }

        return new SessionReport
        {
            sessionId = _sessionId,
            startedAtUtc = _sessionStartUtc,
            endedAtUtc = DateTime.UtcNow.ToString("o"),
            appVersion = Application.version,
            unityVersion = Application.unityVersion,
            platform = Application.platform.ToString(),
            totalPlayTimeSec = Mathf.Max(0f, Time.time - _sessionStartTime),
            totalEvents = _events.Count,
            matchCompleteCount = matchComplete,
            levelCompleteCount = levelComplete,
            actCompleteCount = actComplete,
            events = _events
        };
    }

    private static string SerializeReport(SessionReport report)
    {
        // JsonUtility is fast/reliable for [Serializable] types and keeps dependencies minimal.
        return JsonUtility.ToJson(report, prettyPrint: false);
    }

    private bool TryResolveTarget(out string targetUrl, out string targetAuth)
    {
        targetUrl = "";
        targetAuth = "";

        switch (transportMode)
        {
            case TransportMode.BackendApi:
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    Debug.LogWarning("[MetricsSessionReporter] baseUrl is empty. Skipping HTTP send.");
                    return false;
                }
                targetUrl = EndpointUrl;
                targetAuth = authorizationHeader;
                return true;

            case TransportMode.GoogleSheetDirect:
                if (string.IsNullOrWhiteSpace(googleSheetUrl))
                {
                    Debug.LogWarning("[MetricsSessionReporter] googleSheetUrl is empty. Skipping HTTP send.");
                    return false;
                }
                targetUrl = googleSheetUrl.Trim();
                targetAuth = googleSheetAuthorizationHeader;
                return true;

            default:
                Debug.LogWarning($"[MetricsSessionReporter] Unsupported transport mode: {transportMode}. Skipping HTTP send.");
                return false;
        }
    }

    private IEnumerator PostJson(string url, string json, string authHeader)
    {
        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(authHeader))
            {
                request.SetRequestHeader("Authorization", authHeader);
            }

            yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool isError = request.result == UnityWebRequest.Result.ConnectionError ||
                           request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool isError = request.isNetworkError || request.isHttpError;
#endif
            if (isError)
            {
                Debug.LogWarning($"[MetricsSessionReporter] HTTP send failed: {request.responseCode} {request.error}");
            }
            else
            {
                if (verboseLogs) Debug.Log($"[MetricsSessionReporter] HTTP send OK: {request.responseCode}");
            }
        }
    }
}

