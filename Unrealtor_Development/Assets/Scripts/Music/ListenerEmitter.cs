using FMOD;using FMOD.Studio;
using FMODUnity;
using System.Collections.Generic;
using UnityEngine;using Cinemachine;


public class ListenerEmitter : MonoBehaviour
{
    [Header("Music Manager / FMOD")]
    [SerializeField] private SoundID loopingSoundId = SoundID.PuzzlePieceApproached;    [SerializeField] private SoundID bgmEvent = SoundID.FinalBGM;
    [SerializeField] private string firePanParameterName = "firePan";
    [SerializeField] private string fireDistParameterName = "fireDist";    [SerializeField] private string fireOcclusionParameterName = "fireOcclude";
    [SerializeField] private string clogPanParameterName = "clogPan";
    [SerializeField] private string clogDistParameterName = "clogDist";    [SerializeField] private string clogOcclusionParameterName = "clogOcclude";
    [SerializeField] private string speakerPanParameterName = "speakerPan";
    [SerializeField] private string speakerDistParameterName = "speakerDist";    [SerializeField] private string speakerOcclusionParameterName = "speakerOcclude";
    [SerializeField] private string light1PanParameterName = "light1Pan";
    [SerializeField] private string light1DistParameterName = "light1Dist";    [SerializeField] private string light1OcclusionParameterName = "light1Occlude";    [SerializeField] private string showerPanParameterName = "showerPan";
    [SerializeField] private string showerDistParameterName = "showerDist";    [SerializeField] private string showerOcclusionParameterName = "showerOcclude";

    [SerializeField] private bool isSpeaker = false;

    [Header("References")]
    [SerializeField] private SphereCollider triggerCollider;
    [SerializeField] private BasePlayerController leftPlayer;
    [SerializeField] private BasePlayerController rightPlayer;    private CinemachineVirtualCamera leftCam;

    private CinemachineVirtualCamera rightCam;

    private MusicManager musicManager;
    private EventInstance _loopInstance;    private EventInstance bgmInstance;
    private bool _isLoopPlaying;

    private int _leftOverlapCount;
    private int _rightOverlapCount;
    private readonly List<Collider> _leftPlayerCollidersInRange = new List<Collider>();
    private readonly List<Collider> _rightPlayerCollidersInRange = new List<Collider>();    private enum OcclusionType
    {
        None,

        Partial,

        Full
    }    private Vector3 debugEmitterPos;

    private Vector3 debugPlayerPos;

    private OcclusionType debugLastOcclusionResult;

    private void Awake()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<SphereCollider>();        musicManager = MusicManager.Instance;
        CachePlayersFromManager();        leftCam = leftPlayer.gameObject.GetComponentInChildren<CinemachineVirtualCamera>();        rightCam = rightPlayer.gameObject.GetComponentInChildren<CinemachineVirtualCamera>();
    }

    private void OnEnable()
    {        musicManager = MusicManager.Instance;

        CachePlayersFromManager();
    }

    private void OnDisable()
    {
        StopLoop();
        ClearRuntimeValues();
    }

    private void Update()
    {
        if (!_isLoopPlaying)
            return;

        UpdateSoundParameters();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetPlayerId(other, out int playerId))
            return;

        bool wasAnyInRange = AnyPlayersInRange();
        IncrementOverlap(playerId, other);        if (musicManager._activeInstances.TryGetValue(bgmEvent, out var instance))

        {

            bgmInstance = instance;

        }

        if (!wasAnyInRange && AnyPlayersInRange())
            StartLoop();

        if (_isLoopPlaying)
            UpdateSoundParameters();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryGetPlayerId(other, out int playerId))
            return;

        DecrementOverlap(playerId, other);

        if (AnyPlayersInRange())
        {
            if (_isLoopPlaying)
                UpdateSoundParameters();
            return;
        }

        StopLoop();
        ClearRuntimeValues();
    }

    private void StartLoop()
    {
        if (MusicManager.Instance == null)
            return;

        _loopInstance = MusicManager.Instance.CreateSoundInstance(loopingSoundId, null);
        if (!_loopInstance.isValid())
            return;
        UpdateSoundParameters();
        _loopInstance.start();
        _isLoopPlaying = true;
    }

    private void StopLoop()
    {
        if (_loopInstance.isValid())
        {
            _loopInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _loopInstance.release();
            _loopInstance.clearHandle();
        }

        _isLoopPlaying = false;
    }

    private void UpdateSoundParameters()
    {
        if (!_loopInstance.isValid())
            return;

        CachePlayersFromManager();
        if (leftPlayer == null || rightPlayer == null)
            return;

        float radius = GetWorldRadius();
        if (radius <= 0.0001f)
            radius = 0.0001f;
        float leftDist = GetPlayerDistanceFromActiveColliders(
            _leftPlayerCollidersInRange, leftPlayer.transform.position);
        float rightDist = GetPlayerDistanceFromActiveColliders(
            _rightPlayerCollidersInRange, rightPlayer.transform.position);



        bool isPartiallyOccluded = false;

        if (isAudioOccluded(GetEmitterPosition(), leftCam.transform.position) == OcclusionType.Partial)

        {

            if (leftDist < rightDist) // apply occlusion if left player's perspective is closer and occluded

                isPartiallyOccluded = true;

        }        else if (isAudioOccluded(GetEmitterPosition(), leftCam.transform.position) == OcclusionType.Full)

        {

            leftDist = radius;

        }



        if (isAudioOccluded(GetEmitterPosition(), rightCam.transform.position) == OcclusionType.Partial)

        {

            if (rightDist < leftDist) // apply occlusion if right player's perspective is closer and occluded

                isPartiallyOccluded = true;

        }        else if (isAudioOccluded(GetEmitterPosition(), rightCam.transform.position) == OcclusionType.Full)

        {

            rightDist = radius;

        }



        float distToCloserPlayer = Mathf.Min(leftDist, rightDist);

        float pan = Mathf.Clamp((leftDist - rightDist) / radius, -1f, 1f);
        float dist = Mathf.Clamp(distToCloserPlayer / radius, 0f, 1f);

        SetParameter(_loopInstance, firePanParameterName, pan);
        SetParameter(_loopInstance, clogPanParameterName, pan);
        SetParameter(_loopInstance, light1PanParameterName, pan);
        SetParameter(_loopInstance, showerPanParameterName, pan);

        SetParameter(_loopInstance, fireDistParameterName, dist);
        SetParameter(_loopInstance, clogDistParameterName, dist);
        SetParameter(_loopInstance, light1DistParameterName, dist);        SetParameter(_loopInstance, showerDistParameterName, dist);


        if (isPartiallyOccluded)

        {

            SetParameter(_loopInstance, fireOcclusionParameterName, 1f);

            SetParameter(_loopInstance, clogOcclusionParameterName, 1f);

            SetParameter(_loopInstance, light1OcclusionParameterName, 1f);

            SetParameter(_loopInstance, showerOcclusionParameterName, 1f);

            if (isSpeaker)

                SetParameter(bgmInstance, speakerOcclusionParameterName, 1f);

        }

        else

        {

            SetParameter(_loopInstance, fireOcclusionParameterName, 0f);

            SetParameter(_loopInstance, clogOcclusionParameterName, 0f);

            SetParameter(_loopInstance, light1OcclusionParameterName, 0f);

            SetParameter(_loopInstance, showerOcclusionParameterName, 0f);

            if (isSpeaker)

                SetParameter(bgmInstance, speakerOcclusionParameterName, 0f);

        }


        if (isSpeaker)

        {

             SetParameter(bgmInstance, speakerPanParameterName, pan);

             SetParameter(bgmInstance, speakerDistParameterName, dist);

        }
    }

    private static void SetParameter(EventInstance instance, string parameterName, float value)
    {
        if (string.IsNullOrEmpty(parameterName))
            return;

        FMOD.RESULT localResult = instance.setParameterByName(parameterName, value);
        if (localResult == FMOD.RESULT.OK)
            return;

        if (RuntimeManager.IsInitialized)
            RuntimeManager.StudioSystem.setParameterByName(parameterName, value);
    }

    private float GetWorldRadius()
    {
        if (triggerCollider == null)
            return 1f;

        Vector3 scale = triggerCollider.transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return triggerCollider.radius * maxScale;
    }


    private Vector3 GetEmitterPosition()
    {
        if (triggerCollider != null)
            return triggerCollider.bounds.center;
        return transform.position;
    }

    private void CachePlayersFromManager()
    {
        if (PlayerInputManager.Instance == null)
            return;

        if (leftPlayer == null)
            leftPlayer = PlayerInputManager.Instance.Player1;

        if (rightPlayer == null)
            rightPlayer = PlayerInputManager.Instance.Player2;
    }

    private static bool TryGetPlayerId(Collider other, out int playerId)
    {
        playerId = -1;
        BasePlayerController controller = other.GetComponentInParent<BasePlayerController>();
        if (controller == null)
            return false;

        playerId = controller.playerID == BasePlayerController.PlayerID.Player1 ? 0 : 1;
        return true;
    }

    private float GetPlayerDistanceFromActiveColliders(List<Collider> colliders, Vector3 fallbackPosition)
    {
        Vector3 emitterPos = GetEmitterPosition();
        float minDist = float.PositiveInfinity;
        bool anyValid = false;

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled)
                continue;

            anyValid = true;
            Vector3 closestPoint = col.ClosestPoint(emitterPos);
            float d = Vector3.Distance(closestPoint, emitterPos);
            if (d < minDist)
                minDist = d;
        }

        if (anyValid)
        {

                float rootDist = Vector3.Distance(fallbackPosition, emitterPos);

                return Mathf.Min(minDist, rootDist);
        }
        return Vector3.Distance(fallbackPosition, emitterPos);
    }
    private OcclusionType isAudioOccluded(Vector3 emitterPos, Vector3 playerPos)
    {

        OcclusionType isOccluded = OcclusionType.None;



        // 1. Flip the math: Vector now points from Player -> Emitter
        Vector3 heading = emitterPos - playerPos;

        float raycastDist = heading.magnitude;

        Vector3 playerToEmitter = heading.normalized;



        // 2. Nudge the start point slightly forward from the player towards the emitter
        float startOffset = .75f;

        float endOffset = 2f;

        Vector3 adjustedStartPos = playerPos + (playerToEmitter * startOffset);

        float adjustedDist = raycastDist - startOffset - endOffset;



        // 3. Fire the reversed raycast
        RaycastHit[] hits = Physics.RaycastAll(adjustedStartPos, playerToEmitter, adjustedDist, ~0, QueryTriggerInteraction.Ignore);

        List<string> hitNames = new List<string>();

        foreach (RaycastHit hit in hits)
        {

            hitNames.Add(hit.collider.name);

            // If it's a permeable object (bush, glass, etc.)
            if (hit.collider.CompareTag("AudioPermeable"))
            {

                if (isOccluded != OcclusionType.Full)
                {

                    isOccluded = OcclusionType.Partial;

                }

            }
            else if (!hit.collider.CompareTag("IgnoreAudioOcclusion")) // NOTE: Emitters must be tagged with IgnoreAudioOcclusion
            {

                isOccluded = OcclusionType.Full;

                break;
            }
        }

        string hitListString = hitNames.Count > 0 ? string.Join(", ", hitNames) : "Nothing";

        UnityEngine.Debug.Log($"Audio Check Result: {isOccluded} | Objects Intersected: [{hitListString}]");



        // === Raycast Draw ===

        debugEmitterPos = emitterPos - (playerToEmitter * endOffset);

        debugPlayerPos = adjustedStartPos;

        debugLastOcclusionResult = isOccluded;

        // =====================

        return isOccluded;

    }

    private void IncrementOverlap(int playerId, Collider collider)
    {
        if (playerId == 0)
        {
            _leftOverlapCount++;
            if (collider != null && !_leftPlayerCollidersInRange.Contains(collider))
                _leftPlayerCollidersInRange.Add(collider);
        }
        else if (playerId == 1)
        {
            _rightOverlapCount++;
            if (collider != null && !_rightPlayerCollidersInRange.Contains(collider))
                _rightPlayerCollidersInRange.Add(collider);
        }
    }

    private void DecrementOverlap(int playerId, Collider collider)
    {
        if (playerId == 0)
        {
            _leftOverlapCount = Mathf.Max(0, _leftOverlapCount - 1);
            if (collider != null)
                _leftPlayerCollidersInRange.Remove(collider);
            _leftPlayerCollidersInRange.RemoveAll(c => c == null);
        }
        else if (playerId == 1)
        {
            _rightOverlapCount = Mathf.Max(0, _rightOverlapCount - 1);
            if (collider != null)
                _rightPlayerCollidersInRange.Remove(collider);
            _rightPlayerCollidersInRange.RemoveAll(c => c == null);
        }
    }

    private bool AnyPlayersInRange()
    {
        return _leftOverlapCount > 0 || _rightOverlapCount > 0;
    }

    private void ClearRuntimeValues()
    {
        _leftOverlapCount = 0;
        _rightOverlapCount = 0;
        _leftPlayerCollidersInRange.Clear();
        _rightPlayerCollidersInRange.Clear();        SetParameter(_loopInstance, firePanParameterName, 0f);
        SetParameter(_loopInstance, clogPanParameterName, 0f);
        SetParameter(_loopInstance, light1PanParameterName, 0f);
        SetParameter(_loopInstance, showerPanParameterName, 0f);


        SetParameter(_loopInstance, fireDistParameterName, 1f);

        SetParameter(_loopInstance, clogDistParameterName, 1f);

        SetParameter(_loopInstance, light1DistParameterName, 1f);

        SetParameter(_loopInstance, showerDistParameterName, 1f);        SetParameter(_loopInstance, fireOcclusionParameterName, 0f);

        SetParameter(_loopInstance, clogOcclusionParameterName, 0f);

        SetParameter(_loopInstance, light1OcclusionParameterName, 0f);

        SetParameter(_loopInstance, showerOcclusionParameterName, 0f);        if (isSpeaker)

        {

            SetParameter(bgmInstance, speakerPanParameterName, 0f);

            SetParameter(bgmInstance, speakerDistParameterName, 1f);

            SetParameter(bgmInstance, speakerOcclusionParameterName, 0f);

        }
    }    private void OnDrawGizmos()
    {
        // Only draw if the game is running and has calculated positions
        if (debugEmitterPos == Vector3.zero && debugPlayerPos == Vector3.zero) return;

        // Change the color of the ray depending on the occlusion state
        switch (debugLastOcclusionResult)
        {
            case OcclusionType.None:
                Gizmos.color = Color.green; // Clear path
                break;
            case OcclusionType.Partial:
                Gizmos.color = Color.yellow; // Partially blocked
                break;
            case OcclusionType.Full:
                Gizmos.color = Color.red; // Fully blocked
                break;
        }

        // Draw the actual line representing the raycast path
        Gizmos.DrawLine(debugEmitterPos, debugPlayerPos);

        // Optional: Draw little spheres at the start and end points
        Gizmos.DrawSphere(debugEmitterPos, 0.15f);
        Gizmos.DrawSphere(debugPlayerPos, 0.15f);
    }
}
