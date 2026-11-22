using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EdgarState
{
    Idle,
    PlayingSequence
}

public class EdgarManager : MonoBehaviour
{
    public static EdgarManager Instance;

    [Header("References")]
    public Transform edgarPointsHolder;                // Drag EdgarPointsHolder here
    public AudioSource voiceSource;                    // AudioSource for voice (can also be on edgar)

    [Header("Spawn Settings")]
    public GameObject edgarFullBodyPrefab;             // Full body Edgar prefab (optional, if null will use edgarCharacter if set)
    public GameObject edgarFaceOnlyPrefab;             // Face-only Edgar prefab (optional, for wall face appearances)
    public Transform edgarSpawnParent;                 // Parent transform for spawned Edgar instances (optional)

    [Header("Hide Animation")]
    public float hideDuration = 1.2f;
    public float hideSinkDistance = 2f;
    public float hideSpinSpeed = 180f;

    [Header("Runtime (Read-only)")]
    public EdgarCharacterController edgarCharacter;    // Current active Edgar instance (spawned at runtime)

    [Header("Configuration")]
    public EdgarSequenceDatabase sequenceDatabase; // Database containing all Edgar sequences (ScriptableObject)

    [Header("State (Read-only)")]
    public EdgarState currentState = EdgarState.Idle;
    public string currentSequenceId;

    Dictionary<string, EdgarPoint> pointMap = new Dictionary<string, EdgarPoint>();
    Dictionary<string, EdgarSequence> sequenceMap = new Dictionary<string, EdgarSequence>();
    EdgarSequence activeSequence;
    Coroutine sequenceCoroutine;

    GameObject currentEdgarInstance;                    // Currently spawned Edgar GameObject
    bool isFullBodyMode = true;                        // Track which mode we're in

    void Awake()
    {
        Instance = this;

        CachePoints();
        CacheSequences();

        // If edgarCharacter is already set (scene reference), subscribe to it
        if (edgarCharacter != null)
        {
            edgarCharacter.OnReachedPoint += HandleEdgarReachedPoint;
            currentEdgarInstance = edgarCharacter.gameObject;
        }
    }

    void CachePoints()
    {
        pointMap.Clear();

        if (edgarPointsHolder == null)
        {
            Debug.LogWarning("EdgarManager: edgarPointsHolder not set");
            return;
        }

        EdgarPoint[] points = edgarPointsHolder.GetComponentsInChildren<EdgarPoint>(true);
        foreach (EdgarPoint p in points)
        {
            if (!string.IsNullOrEmpty(p.pointId) && !pointMap.ContainsKey(p.pointId))
            {
                pointMap.Add(p.pointId, p);
            }
        }
    }

    void CacheSequences()
    {
        sequenceMap.Clear();

        if (sequenceDatabase == null || sequenceDatabase.sequences == null)
        {
            Debug.LogWarning("EdgarManager: sequenceDatabase not set or empty");
            return;
        }

        foreach (EdgarSequence seq in sequenceDatabase.sequences)
        {
            if (seq != null && !string.IsNullOrEmpty(seq.sequenceId))
            {
                if (sequenceMap.ContainsKey(seq.sequenceId))
                {
                    Debug.LogWarning("EdgarManager: Duplicate sequenceId found: " + seq.sequenceId);
                }
                else
                {
                    sequenceMap.Add(seq.sequenceId, seq);
                }
            }
        }
    }

    public EdgarPoint GetPoint(string pointId)
    {
        EdgarPoint p;
        pointMap.TryGetValue(pointId, out p);
        return p;
    }

    EdgarSequence FindSequence(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId))
        {
            return null;
        }

        EdgarSequence seq;
        sequenceMap.TryGetValue(sequenceId, out seq);
        return seq;
    }

    // ============ Public API: Unified Sequence Player ============

    // External systems (Puzzle / Trigger / Timeline, etc.) only need to call this
    public void PlaySequence(string sequenceId)
    {
        EdgarSequence sequence = FindSequence(sequenceId);
        if (sequence == null)
        {
            Debug.LogError("EdgarManager: EdgarSequence not found: " + sequenceId);
            return;
        }

        PlaySequence(sequence);
    }

    public void PlaySequence(EdgarSequence sequence)
    {
        if (sequence == null)
        {
            return;
        }

        // Interrupt current sequence
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }

        activeSequence = sequence;
        currentSequenceId = sequence.sequenceId;
        sequenceCoroutine = StartCoroutine(RunSequenceCoroutine(sequence));
    }

    public bool IsPlayingSequence()
    {
        return currentState == EdgarState.PlayingSequence;
    }

    // ============ Sequence Execution Core ============

    IEnumerator RunSequenceCoroutine(EdgarSequence sequence)
    {
        currentState = EdgarState.PlayingSequence;

        if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
        {
            currentState = EdgarState.Idle;
            yield break;
        }

        for (int i = 0; i < sequence.steps.Count; i++)
        {
            EdgarStep step = sequence.steps[i];

            if (step == null)
            {
                continue;
            }

            yield return ExecuteStep(step);
        }

        activeSequence = null;
        currentSequenceId = null;
        currentState = EdgarState.Idle;
        sequenceCoroutine = null;
    }

    IEnumerator ExecuteStep(EdgarStep step)
    {
        switch (step.actionType)
        {
            // ============ Movement / Position ============
            case EdgarActionType.GoToPoint:
                {
                    if (string.IsNullOrEmpty(step.pointId))
                    {
                        Debug.LogWarning("EdgarManager: GoToPoint requires pointId");
                        yield break;
                    }

                    EdgarPoint point = GetPoint(step.pointId);
                    if (point == null)
                    {
                        Debug.LogWarning("EdgarManager: GoToPoint pointId not found: " + step.pointId);
                        yield break;
                    }

                    yield return MoveAndWait(point);
                    yield break;
                }

            case EdgarActionType.SnapToPoint:
                {
                    if (string.IsNullOrEmpty(step.pointId))
                    {
                        Debug.LogWarning("EdgarManager: SnapToPoint requires pointId");
                        yield break;
                    }

                    EdgarPoint point = GetPoint(step.pointId);
                    if (point == null)
                    {
                        Debug.LogWarning("EdgarManager: SnapToPoint pointId not found: " + step.pointId);
                        yield break;
                    }

                    if (edgarCharacter != null)
                    {
                        edgarCharacter.TeleportTo(point);
                    }

                    yield break;
                }

            // ============ Appearance / Visibility ============
            case EdgarActionType.ShowFullBodyAtPoint:
                {
                    if (string.IsNullOrEmpty(step.pointId))
                    {
                        Debug.LogWarning("EdgarManager: ShowFullBodyAtPoint requires pointId");
                        yield break;
                    }

                    EdgarPoint point = GetPoint(step.pointId);
                    if (point == null)
                    {
                        Debug.LogWarning("EdgarManager: ShowFullBodyAtPoint pointId not found: " + step.pointId);
                        yield break;
                    }

                    SpawnEdgarFullBody(point.transform.position);
                    if (edgarCharacter != null)
                    {
                        edgarCharacter.TeleportTo(point);
                    }

                    yield break;
                }

            case EdgarActionType.ShowFaceAtPoint:
                {
                    if (string.IsNullOrEmpty(step.pointId))
                    {
                        Debug.LogWarning("EdgarManager: ShowFaceAtPoint requires pointId");
                        yield break;
                    }

                    EdgarPoint point = GetPoint(step.pointId);
                    if (point == null)
                    {
                        Debug.LogWarning("EdgarManager: ShowFaceAtPoint pointId not found: " + step.pointId);
                        yield break;
                    }

                    SpawnEdgarFaceOnly(point.transform.position);
                    if (edgarCharacter != null)
                    {
                        edgarCharacter.TeleportTo(point);
                    }

                    yield break;
                }

            case EdgarActionType.HideEdgar:
                {
                    if (edgarCharacter != null)
                    {
                        yield return edgarCharacter.HideAndSink();
                    }
                    DespawnEdgar();
                    yield break;
                }

            // ============ VO / Audio ============
            case EdgarActionType.PlayVO:
                {
                    string voToPlay = step.voId;

                    // If step.voId is empty, try to use point's default voId
                    if (string.IsNullOrEmpty(voToPlay) && !string.IsNullOrEmpty(step.pointId))
                    {
                        EdgarPoint p = GetPoint(step.pointId);
                        if (p != null && !string.IsNullOrEmpty(p.defaultVoId))
                        {
                            voToPlay = p.defaultVoId;
                        }
                    }

                    if (!string.IsNullOrEmpty(voToPlay))
                    {
                        yield return StartVOWhenAvailable(voToPlay);
                    }
                    else
                    {
                        Debug.LogWarning("EdgarManager: PlayVO has no valid voId (neither step.voId nor point.defaultVoId)");
                    }

                    // Doesn't auto-wait, designer should add WaitSeconds step if needed
                    yield break;
                }

            // ============ Animation / Pose ============
            case EdgarActionType.PlayAnimation:
                {
                    if (edgarCharacter != null)
                    {
                        if (!string.IsNullOrEmpty(step.animationTrigger))
                        {
                            edgarCharacter.PlayAnimation(step.animationTrigger);
                        }
                        else
                        {
                            Debug.LogWarning("EdgarManager: PlayAnimation requires animationTrigger");
                        }
                    }

                    yield break;
                }

            case EdgarActionType.PlayPointDefaultAnimation:
                {
                    if (string.IsNullOrEmpty(step.pointId))
                    {
                        Debug.LogWarning("EdgarManager: PlayPointDefaultAnimation requires pointId");
                        yield break;
                    }

                    EdgarPoint point = GetPoint(step.pointId);
                    if (point == null)
                    {
                        Debug.LogWarning("EdgarManager: PlayPointDefaultAnimation pointId not found: " + step.pointId);
                        yield break;
                    }

                    if (edgarCharacter != null)
                    {
                        if (!string.IsNullOrEmpty(point.defaultAnimation))
                        {
                            edgarCharacter.PlayAnimation(point.defaultAnimation);
                        }
                        else
                        {
                            Debug.LogWarning("EdgarManager: PlayPointDefaultAnimation - point has no defaultAnimation: " + step.pointId);
                        }
                    }

                    yield break;
                }

            // ============ Look / Orientation ============
            case EdgarActionType.LookAtPlayer:
                {
                    // TODO: Implement look at player logic
                    // For now, this is a placeholder
                    if (edgarCharacter != null)
                    {
                        Transform playerPos = FindNearestPlayer();
                        edgarCharacter.LookAt(playerPos);
                    }

                    yield break;
                }

            case EdgarActionType.LookAtPoint:
                {
                    if (string.IsNullOrEmpty(step.pointId))
                    {
                        Debug.LogWarning("EdgarManager: LookAtPoint requires pointId");
                        yield break;
                    }

                    EdgarPoint point = GetPoint(step.pointId);
                    if (point == null)
                    {
                        Debug.LogWarning("EdgarManager: LookAtPoint pointId not found: " + step.pointId);
                        yield break;
                    }

                    // TODO: Implement look at point logic
                    // if (edgarCharacter != null)
                    // {
                    //     edgarCharacter.LookAt(point.transform);
                    // }

                    yield break;
                }

            case EdgarActionType.RotateEdgar:
                {
                    if (edgarCharacter != null)
                    {
                        edgarCharacter.RotateToDirection(step.rotationY);
                    }
                    yield break;
                }

            case EdgarActionType.FacePlayer:
                {
                    if (edgarCharacter != null)
                    {
                        Transform nearestPlayer = FindNearestPlayer();
                        if (nearestPlayer != null)
                        {
                            edgarCharacter.LookAt(nearestPlayer);
                        }
                        else
                        {
                            Debug.LogWarning("EdgarManager: FacePlayer - no player found");
                        }
                    }
                    yield break;
                }

            // ============ State / Timing ============
            case EdgarActionType.SetTalking:
                {
                    if (edgarCharacter != null)
                    {
                        edgarCharacter.SetTalking(step.talking);
                    }

                    yield break;
                }

            case EdgarActionType.WaitSeconds:
                {
                    float waitTime = Mathf.Max(0.0f, step.waitSeconds);
                    if (waitTime > 0.0f)
                    {
                        yield return new WaitForSeconds(waitTime);
                    }

                    yield break;
                }

            default:
                {
                    Debug.LogWarning("EdgarManager: Unknown action type: " + step.actionType);
                    yield break;
                }
        }
    }

    IEnumerator MoveAndWait(EdgarPoint target)
    {
        if (edgarCharacter == null || target == null)
        {
            yield break;
        }

        edgarCharacter.MoveTo(target);

        while (edgarCharacter.IsMoving)
        {
            yield return null;
        }
    }

    // ============ Spawn / Despawn Logic ============

    void SpawnEdgarFullBody(Vector3 position)
    {
        // Despawn current instance if exists and different mode
        if (currentEdgarInstance != null && !isFullBodyMode)
        {
            DespawnEdgar();
        }

        // If already spawned in full body mode, just move it
        if (currentEdgarInstance != null && isFullBodyMode)
        {
            if (edgarCharacter != null)
            {
                edgarCharacter.transform.position = position;
            }
            return;
        }

        // Spawn new instance
        GameObject prefabToSpawn = edgarFullBodyPrefab;
        if (prefabToSpawn == null && edgarCharacter != null)
        {
            // Fallback: use existing edgarCharacter if prefab not set
            currentEdgarInstance = edgarCharacter.gameObject;
            edgarCharacter.transform.position = position;
            currentEdgarInstance.SetActive(true);
            isFullBodyMode = true;
            return;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("EdgarManager: Cannot spawn full body - edgarFullBodyPrefab not set");
            return;
        }

        currentEdgarInstance = Instantiate(prefabToSpawn, position, Quaternion.identity, edgarSpawnParent);
        edgarCharacter = currentEdgarInstance.GetComponent<EdgarCharacterController>();
        if (edgarCharacter != null)
        {
            edgarCharacter.OnReachedPoint += HandleEdgarReachedPoint;
        }
        isFullBodyMode = true;
    }

    void SpawnEdgarFaceOnly(Vector3 position)
    {
        // Despawn current instance if exists and different mode
        if (currentEdgarInstance != null && isFullBodyMode)
        {
            DespawnEdgar();
        }

        // If already spawned in face-only mode, just move it
        if (currentEdgarInstance != null && !isFullBodyMode)
        {
            if (edgarCharacter != null)
            {
                edgarCharacter.transform.position = position;
            }
            return;
        }

        // Spawn new instance
        GameObject prefabToSpawn = edgarFaceOnlyPrefab;
        if (prefabToSpawn == null)
        {
            // Fallback: use full body prefab if face-only not set
            prefabToSpawn = edgarFullBodyPrefab;
            if (prefabToSpawn == null && edgarCharacter != null)
            {
                currentEdgarInstance = edgarCharacter.gameObject;
                edgarCharacter.transform.position = position;
                currentEdgarInstance.SetActive(true);
                isFullBodyMode = false;
                return;
            }
        }

        if (prefabToSpawn == null)
        {
            Debug.LogWarning("EdgarManager: Cannot spawn face-only - no prefab set");
            return;
        }

        currentEdgarInstance = Instantiate(prefabToSpawn, position, Quaternion.identity, edgarSpawnParent);
        edgarCharacter = currentEdgarInstance.GetComponent<EdgarCharacterController>();
        if (edgarCharacter != null)
        {
            edgarCharacter.OnReachedPoint += HandleEdgarReachedPoint;
        }
        isFullBodyMode = false;
    }

    void DespawnEdgar()
    {
        if (currentEdgarInstance != null)
        {
            // Unsubscribe before destroying
            if (edgarCharacter != null)
            {
                edgarCharacter.OnReachedPoint -= HandleEdgarReachedPoint;
            }

            // If it was spawned (not scene reference), destroy it
            // Otherwise just deactivate
            if (edgarFullBodyPrefab != null || edgarFaceOnlyPrefab != null)
            {
                Destroy(currentEdgarInstance);
            }
            else
            {
                currentEdgarInstance.SetActive(false);
            }

            currentEdgarInstance = null;
            edgarCharacter = null;
        }
    }

    // ============ VO Playback Placeholder (connect to your VO system later) ============

    void PlayVO(string voId)
    {
        if (string.IsNullOrEmpty(voId))
        {
            Debug.LogWarning("EdgarManager: PlayVO called with empty voId");
            return;
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.StartDialogue(voId);
        }
        else
        {
            Debug.LogWarning("EdgarManager: DialogueManager.Instance not found, cannot play VO: " + voId);
        }
    }

    IEnumerator StartVOWhenAvailable(string voId)
    {
        if (string.IsNullOrEmpty(voId))
        {
            yield break;
        }

        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("EdgarManager: DialogueManager.Instance not found, cannot play VO: " + voId);
            yield break;
        }

        while (true)
        {
            while (DialogueManager.Instance.IsCompletingDialogue)
            {
                yield return null;
            }

            if (DialogueManager.Instance.IsDialoguePlaying)
            {
                yield return null;
                continue;
            }

            if (DialogueManager.Instance.StartDialogue(voId))
            {
                yield break;
            }

            if (!DialogueManager.Instance.IsDialoguePlaying)
            {
                Debug.LogWarning("EdgarManager: Dialogue did not start, skipping VO: " + voId);
                yield break;
            }

            yield return null;
        }
    }

    // ============ Event Callbacks ============

    void HandleEdgarReachedPoint(EdgarPoint point)
    {
        // If you need additional logic at specific points, check point.pointId here
        // e.g., if (point.pointId == "Intro_Mid") { ... }
    }

    // ============ Helper Methods ============

    Transform FindNearestPlayer()
    {
        BasePlayerController[] players = FindObjectsOfType<BasePlayerController>();
        if (players == null || players.Length == 0)
        {
            return null;
        }

        if (edgarCharacter == null)
        {
            return null;
        }

        Transform edgarTransform = edgarCharacter.transform;
        Transform nearestPlayer = null;
        float nearestDistance = float.MaxValue;

        foreach (BasePlayerController player in players)
        {
            if (player != null && player.transform != null)
            {
                float distance = Vector3.Distance(edgarTransform.position, player.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayer = player.transform;
                }
            }
        }

        return nearestPlayer;
    }

    // ============ Debug Methods ============

    [Header("Debug (Runtime Only)")]
    [Tooltip("Enter a sequence ID here and call DebugPlaySequence() to test sequences at runtime")]
    public string debugSequenceId = "";

    [ContextMenu("Debug: Play Sequence")]
    public void DebugPlaySequence()
    {
        if (string.IsNullOrEmpty(debugSequenceId))
        {
            Debug.LogWarning("EdgarManager: debugSequenceId is empty. Please enter a sequence ID first.");
            return;
        }

        Debug.Log("EdgarManager: Debug - Playing sequence: " + debugSequenceId);
        PlaySequence(debugSequenceId);
    }

    public void DebugPlaySequence(string sequenceId)
    {
        if (string.IsNullOrEmpty(sequenceId))
        {
            Debug.LogWarning("EdgarManager: DebugPlaySequence called with empty sequenceId");
            return;
        }

        Debug.Log("EdgarManager: Debug - Playing sequence: " + sequenceId);
        PlaySequence(sequenceId);
    }
}

