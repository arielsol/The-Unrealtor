using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Collects gameplay metrics for difficulty tuning:
/// 1) Match completion time (solvable → complete)
/// 2) Level / Act clear time
/// Output: Debug.Log by default; extend or subscribe to events to send to analytics.
/// </summary>
public static class GameplayMetricsCollector
{
    // ---- Match completion time ----
    private static readonly Dictionary<int, float> MatchSolvableStartTime = new Dictionary<int, float>();

    // ---- Level clear time ----
    private static float _levelStartTime;
    private static int _levelId = -1;
    private static bool _levelStarted;

    // ---- Act clear time ----
    private static float _actStartTime;
    private static GameManager.GameState _currentAct;
    private static bool _actStarted;

    /// <summary>
    /// Call when a Match first enters solvable state (MATCHHOLD).
    /// </summary>
    public static void RecordMatchSolvableStart(MatchBase match)
    {
        if (match == null) return;
        int id = match.GetInstanceID();
        if (MatchSolvableStartTime.ContainsKey(id)) return;
        MatchSolvableStartTime[id] = Time.time;
    }

    /// <summary>
    /// Call when a Match is completed. Computes duration and reports; removes start time.
    /// </summary>
    public static void RecordMatchComplete(MatchBase match)
    {
        if (match == null) return;
        int id = match.GetInstanceID();
        if (!MatchSolvableStartTime.TryGetValue(id, out float startTime))
        {
            OnMatchComplete?.Invoke(match.MatchTypeEnum, GetLevelOrSceneId(), GetCurrentAct(), -1f);
            return;
        }
        MatchSolvableStartTime.Remove(id);
        float duration = Time.time - startTime;
        OnMatchComplete?.Invoke(match.MatchTypeEnum, GetLevelOrSceneId(), GetCurrentAct(), duration);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Metrics] MatchComplete | MatchType={match.MatchTypeEnum} | Level={GetLevelOrSceneId()} | Act={GetCurrentAct()} | DurationSec={duration:F1}");
#endif
    }

    /// <summary>
    /// Call when the player enters a level (e.g. LevelManager.Start).
    /// </summary>
    public static void RecordLevelStart()
    {
        _levelId = SceneManager.GetActiveScene().buildIndex;
        _levelStartTime = Time.time;
        _levelStarted = true;
        OnLevelStart?.Invoke(_levelId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Metrics] LevelStart | LevelId={_levelId}");
#endif
    }

    /// <summary>
    /// Call when the level is marked complete (e.g. LevelManager.SetComplete(true)).
    /// </summary>
    public static void RecordLevelComplete()
    {
        if (!_levelStarted)
        {
            OnLevelComplete?.Invoke(_levelId, -1f);
            return;
        }
        float duration = Time.time - _levelStartTime;
        OnLevelComplete?.Invoke(_levelId, duration);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Metrics] LevelComplete | LevelId={_levelId} | DurationSec={duration:F1}");
#endif
        _levelStarted = false;
    }

    /// <summary>
    /// Call when the player enters an Act (e.g. after GameManager advances state, or at game start for TUTORIAL).
    /// </summary>
    public static void RecordActStart(GameManager.GameState act)
    {
        _currentAct = act;
        _actStartTime = Time.time;
        _actStarted = true;
        OnActStart?.Invoke(act);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Metrics] ActStart | Act={act}");
#endif
    }

    /// <summary>
    /// Call when an Act is completed (right before advancing to the next act). Duration is computed from last RecordActStart.
    /// </summary>
    public static void RecordActComplete(GameManager.GameState completedAct)
    {
        float duration = _actStarted ? Time.time - _actStartTime : -1f;
        OnActComplete?.Invoke(completedAct, duration);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Metrics] ActComplete | Act={completedAct} | DurationSec={duration:F1}");
#endif
        _actStarted = false;
    }

    private static int GetLevelOrSceneId()
    {
        return SceneManager.GetActiveScene().buildIndex;
    }

    private static GameManager.GameState GetCurrentAct()
    {
        return _currentAct;
    }

    // ---- Optional: subscribe to send to your analytics ----
    public static event Action<MatchBase.MatchType, int, GameManager.GameState, float> OnMatchComplete;
    public static event Action<int> OnLevelStart;
    public static event Action<int, float> OnLevelComplete;
    public static event Action<GameManager.GameState> OnActStart;
    public static event Action<GameManager.GameState, float> OnActComplete;
}
