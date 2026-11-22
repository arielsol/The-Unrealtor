using Cinemachine;
using System.Collections;
using Unity.AppUI.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;


public class BasePlayerController : MonoBehaviour, IDataPersistence
{
    public enum PlayerID
    {
        Player1,
        Player2
    }

    public struct PlayerInputData
    {
        public bool IsMovePaused;
        public bool IsLookPaused;
    }

    public PlayerInputData InputData;
    public PlayerInputData OldInputData;

    [Header("Player Settings")]
    [SerializeField] public PlayerID playerID = PlayerID.Player1;
    [SerializeField] private CinemachineVirtualCamera playerMoveCam;
    [SerializeField] private CinemachineVirtualCamera playerStaticCam;
    [SerializeField] private CinemachineBrain playerCinemachineBrain;
    [SerializeField] private Transform modelTransform;
    [SerializeField] private Animator animator;
    public Animator Animator => animator;

    [Header("Turn-In-Place Integration")]
    [Tooltip("Reference to FootIKSystem on the model for TIP coordination. If null, will search in children.")]
    [SerializeField] private FootIKSystem footIKSystem;
    [SerializeField] public float moveSpeed = 7f;

    [SerializeField] [Tooltip("Sensitivity multiplier when switching from gamepad to mouse control")]
    private float mouseCamFactor = .05f;
    [SerializeField] private float gravityValue = 1.0f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] public float sensitivityMultiplier = 1.0f; // 0 - 2.0
    

    [Header("Gravity Settings")]
    [SerializeField] private float gravityTransitionDuration = 0.5f;
    [SerializeField] private float blendedGravityRotationSpeed = 12f;
    [SerializeField] private bool enableGravityRotation = true;
    [SerializeField] private bool debugGravityRotation;
    [SerializeField] private Vector3 debugGravityDirection = Vector3.down;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float groundCheckDistance = 0.6f;
    [Tooltip("How long grounded remains true after a missed frame on uneven surfaces.")]
    [SerializeField, Min(0f)] private float groundedGraceTime = 0.08f;
    [Tooltip("How long to keep the last valid ground hit for fall probing when ground checks flicker.")]
    [SerializeField, Min(0f)] private float groundHitCacheTime = 0.2f;

    [Header("Step Settings")]
    [SerializeField] private bool enableAutoStep = true;
    [SerializeField] private float maxStepHeight = 0.4f;
    [SerializeField, Range(0f, 1f)] private float stepMaxUpDot = 0.2f;
    [SerializeField] private float stepForwardExtra = 0.05f;
    [Tooltip("How long auto-step remains allowed after losing ground for a frame.")]
    [SerializeField, Min(0f)] private float autoStepGroundedGraceTime = 0.12f;
    [Tooltip("Minimum forward distance used when probing for a climbable step.")]
    [SerializeField, Min(0.05f)] private float autoStepMinProbeDistance = 0.22f;
    [Tooltip("Duration of smooth step interpolation. Set to 0 for instant stepping.")]
    [SerializeField] private float stepDuration = 0.15f;
    [Tooltip("Easing curve for step movement (0=linear, higher=more ease-out)")]
    [SerializeField, Range(0f, 3f)] private float stepEasing = 2f;

    [Header("Fall Prevention")]
    [SerializeField] private bool enableFallPrevention = true;
    [Tooltip("Layers that are fallable: when standing on these, fall prevention is skipped and the player can walk off.")]
    [SerializeField] private LayerMask fallableLayers = 0;
    [Tooltip("If the drop ahead (relative to gravity) exceeds this height, forward movement is blocked while grounded.")]
    [SerializeField] private float maxAllowedDropHeight = 2.0f;
    [Tooltip("How far ahead (on the movement plane) to probe for ground.")]
    [SerializeField] private float fallProbeForward = 0.75f;
    [Tooltip("Extra offset along 'up' (opposite gravity) for the probe start.")]
    [SerializeField] private float fallProbeUpOffset = 0.1f;
    [Tooltip("How far down (along gravity) we search for ground ahead.")]
    [SerializeField] private float fallProbeDownDistance = 6.0f;
    [Tooltip("Minimum dot(groundNormal, up) to treat a hit as ground (filters walls).")]
    [SerializeField, Range(0f, 1f)] private float fallProbeMinUpDot = 0.2f;

    [Header("Debug Settings")]
    [SerializeField] public bool EnableStateMachineDebug = false;
    [SerializeField] private bool enableDebugAutoStep = false;
    [SerializeField] private bool enableDebugGroundCheckGizmos = false;
    [SerializeField] private Color groundCheckGizmoColor = Color.yellow;
    [SerializeField] private Color groundHitGizmoColor = Color.green;

    public PlayerInputManager PlayerManager { get; private set; }
    public Rigidbody Rigidbody { get; private set; }

    private TwoPlayerInput controls;
    public CinemachineBrain PlayerCinemachineBrain { get; private set; }

    private PlayerStateMachine stateMachine;

    private Vector3 currentGravityDirection = Vector3.down;
    private bool isTransitioningGravity = false;
    private Coroutine gravityTransitionCoroutine;
    private int suppressFallPreventionCount = 0;

    // Smooth step interpolation state
    private bool isSteppingUp = false;
    private Vector3 stepStartPosition;
    private Vector3 stepTargetPosition;
    private float stepProgress = 0f;
    private Collider currentStepCollider;

    private float originalHorizontalSpeed;
    private float originalVerticalSpeed;
    private CinemachinePOV povComponent;
    private CinemachineInputProvider camInputProvider;
    [SerializeField] private bool isGrounded;
    private float lastGroundedTime = -999f;
    private RaycastHit cachedGroundHit;
    private float lastCachedGroundHitTime = -999f;
    private bool hasCachedGroundHit = false;
    private Collider[] selfColliders;

    //private bool isInputPaused = false;
    private bool wasCameraEnabledBeforePause = false;

    // Turn-In-Place rotation tracking
    private float targetYawRotation;
    private float currentYawRotation;

    public bool IsPlayerGrounded => IsGroundedWithGrace();
    public Vector3 PlayerVelocity { get; set; }
    public float MoveSpeed => moveSpeed;
    public float GravityValue => gravityValue;
    public float JumpHeight => jumpHeight;
    public float SensitivityMultiplier => sensitivityMultiplier;
    public Vector3 GravityDirection => currentGravityDirection;
    public bool IsTransitioningGravity => isTransitioningGravity;
    public bool EnableAutoStep => enableAutoStep;
    public float MaxStepHeight => maxStepHeight;
    public float StepMaxUpDot => stepMaxUpDot;
    public float StepForwardExtra => stepForwardExtra;
    public float AutoStepMinProbeDistance => autoStepMinProbeDistance;
    public bool IsAutoStepGrounded => isGrounded || (Application.isPlaying && (Time.time - lastGroundedTime) <= Mathf.Max(autoStepGroundedGraceTime, groundedGraceTime));
    public float StepDuration => stepDuration;
    public float StepEasing => stepEasing;
    public bool IsSteppingUp => isSteppingUp;
    public bool EnableAutoStepDebug => enableDebugAutoStep || EnableStateMachineDebug;
    public float GroundCheckRadius => groundCheckRadius;
    public LayerMask GroundLayerMask => groundLayer;
    //public bool IsInputPaused => isInputPaused;
    public CinemachineVirtualCamera PlayerMoveCam => playerMoveCam;
    public bool EnableFallPrevention => enableFallPrevention && suppressFallPreventionCount <= 0;

    /// <summary>
    /// The yaw angle where the player wants to face (from camera/input).
    /// Used by FootIKSystem's TIP system to calculate twist.
    /// </summary>
    public float TargetYawRotation => targetYawRotation;

    /// <summary>
    /// Horizontal forward from the gameplay camera, projected onto the plane perpendicular to gravity.
    /// Use for interaction / UI facing checks so they match what the player is looking at.
    /// </summary>
    public Vector3 GetCameraPlanarForward()
    {
        Vector3 up = (-GravityDirection).normalized;
        Transform viewTf = PlayerCinemachineBrain != null ? PlayerCinemachineBrain.transform : transform;
        Vector3 planar = Vector3.ProjectOnPlane(viewTf.forward, up);
        if (planar.sqrMagnitude < 1e-6f)
        {
            planar = Vector3.ProjectOnPlane(viewTf.right, up);
            if (planar.sqrMagnitude < 1e-6f)
                planar = Vector3.ProjectOnPlane(transform.forward, up);
        }
        return planar.sqrMagnitude > 1e-8f ? planar.normalized : transform.forward;
    }

    public float MaxAllowedDropHeight => maxAllowedDropHeight;
    public float FallProbeForward => fallProbeForward;
    public float FallProbeUpOffset => fallProbeUpOffset;
    public float FallProbeDownDistance => fallProbeDownDistance;
    public float FallProbeMinUpDot => fallProbeMinUpDot;

    public InputAction moveAction { get; private set; }
    private InputAction lookAction;

    public bool IsUsingGamepad()
    {
        return IsPlayerUsingGamepad(playerID);
    }

    public static bool IsPlayerUsingGamepad(PlayerID playerID)
    {
        if (PlayerInputManager.Instance == null) return false;
        string inputType = playerID == PlayerID.Player1
            ? PlayerInputManager.Instance.GetPlayer1InputType()
            : PlayerInputManager.Instance.GetPlayer2InputType();
        return inputType == "Gamepad";
    }


    void Start()
    {
        PlayerManager = PlayerInputManager.Instance;
        if (PlayerManager == null)
        {
            Debug.LogError($"BasePlayerController ({playerID}): PlayerInputManager.Instance is null. Check script execution order or scene setup.");
            enabled = false;
            return;
        }
        controls = playerID == PlayerID.Player1 ? PlayerManager.Player1Controls : PlayerManager.Player2Controls;
        Rigidbody = GetComponent<Rigidbody>();
        if (Rigidbody == null)
        {
            Rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        Rigidbody.useGravity = false; // custom gravity handled in FixedUpdate
        Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        Rigidbody.isKinematic = true; // kinematic pairs best with MovePosition-based character control
        Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        Rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        PlayerCinemachineBrain = playerCinemachineBrain;

        // Cache own colliders for self-hit filtering in ground checks
        selfColliders = GetComponentsInChildren<Collider>();

        moveAction = controls.Gameplay.Move;
        lookAction = controls.Gameplay.Look;
        /*jumpAction = controls.Gameplay.Jump;
        tiltLeftAction = controls.Gameplay.TiltLeft;
        tiltRightAction = controls.Gameplay.TiltRight;
        resetRollAction = controls.Gameplay.ResetRoll;

        if (jumpAction != null)
        {
            jumpAction.Disable(); // Disable jump action
        }
        tiltLeftAction.canceled += OnTiltLeftEnd;
        tiltLeftAction.performed += OnTiltLeftStart;
        tiltRightAction.canceled += OnTiltRightEnd;
        tiltRightAction.performed += OnTiltRightStart;
        resetRollAction.performed += OnResetRollStart;*/

        //Debug.Log("Input: " + InputUser.all.Count);

        stateMachine = new PlayerStateMachine(this);
        stateMachine.Initialize();

        povComponent = playerMoveCam.GetCinemachineComponent<CinemachinePOV>();
        if (povComponent != null)
        {
            originalHorizontalSpeed = povComponent.m_HorizontalAxis.m_MaxSpeed;
            originalVerticalSpeed = povComponent.m_VerticalAxis.m_MaxSpeed;
            UpdateCameraSensitivity();
        }

        camInputProvider = playerMoveCam.GetComponent<CinemachineInputProvider>();
        if (camInputProvider != null)
        {
            camInputProvider.XYAxis = InputActionReference.Create(lookAction);
        }
        else
        {
            Debug.LogError("CinemachineInputProvider not found on playerMoveCam");
        }

        if (debugGravityRotation)
        {
            SwitchGravity(debugGravityDirection);
        }

        // Auto-find FootIKSystem on model if not assigned
        if (footIKSystem == null && modelTransform != null)
        {
            footIKSystem = modelTransform.GetComponent<FootIKSystem>();
            if (footIKSystem == null)
            {
                // Also try GetComponentInChildren in case it's on a child
                footIKSystem = modelTransform.GetComponentInChildren<FootIKSystem>();
            }
        }

        if (footIKSystem != null)
        {
            Debug.Log($"BasePlayerController ({playerID}): FootIKSystem found on {footIKSystem.gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"BasePlayerController ({playerID}): FootIKSystem NOT found! TIP will not work.");
        }

        // Initialize rotation tracking from current model rotation
        if (modelTransform != null)
        {
            currentYawRotation = modelTransform.localEulerAngles.y;
            targetYawRotation = currentYawRotation;
        }
        else if (povComponent != null)
        {
            currentYawRotation = povComponent.m_HorizontalAxis.Value;
            targetYawRotation = currentYawRotation;
        }

        InputData.IsLookPaused = false;
        InputData.IsMovePaused = false;
        SetLookPaused(true);
        SetMovePaused(true);
    }

    public void LoadData(GameData data)
    {
        if (data == null) return;

        if (playerID == PlayerID.Player1)
        {
            bool setPos = data.hasPlayer1Position;
            bool setRot = data.hasPlayer1Rotation;
            if (setPos && setRot)
            {
                transform.SetPositionAndRotation(data.player1Position, data.player1Rotation);
            }
            else
            {
                if (setPos) transform.position = data.player1Position;
                if (setRot) transform.rotation = data.player1Rotation;
            }

            if (data.hasPlayer1GravityDirection)
            {
                currentGravityDirection = data.player1GravityDirection;
                SwitchGravity(currentGravityDirection);
            }
        }
        else if (playerID == PlayerID.Player2)
        {
            bool setPos = data.hasPlayer2Position;
            bool setRot = data.hasPlayer2Rotation;
            if (setPos && setRot)
            {
                transform.SetPositionAndRotation(data.player2Position, data.player2Rotation);
            }
            else
            {
                if (setPos) transform.position = data.player2Position;
                if (setRot) transform.rotation = data.player2Rotation;
            }

            if (data.hasPlayer2GravityDirection)
            {
                currentGravityDirection = data.player2GravityDirection;
                SwitchGravity(currentGravityDirection);
            }
        }

        // Reset FootIK TIP system to prevent stale locked positions after teleport
        if (footIKSystem != null)
        {
            footIKSystem.ResetTIP();
        }
    }

    // Called by IdleState.OnEnter to reset foot IK locks so idle animations play cleanly
    public void NotifyPlayerStopped()
    {
        if (footIKSystem != null)
            footIKSystem.ResetTIP();
    }

    public void SaveData(GameData data)
    {
        if (data == null) return;

        if (playerID == PlayerID.Player1)
        {
            data.player1Position = transform.position;
            data.hasPlayer1Position = true;
            data.player1Rotation = transform.rotation;
            data.hasPlayer1Rotation = true;
            data.player1GravityDirection = currentGravityDirection;
            data.hasPlayer1GravityDirection = true;
        }
        else if (playerID == PlayerID.Player2)
        {
            data.player2Position = transform.position;
            data.hasPlayer2Position = true;
            data.player2Rotation = transform.rotation;
            data.hasPlayer2Rotation = true;
            data.player2GravityDirection = currentGravityDirection;
            data.hasPlayer2GravityDirection = true;
        }
    }

    void OnDisable()
    {
        /*if (jumpAction != null)
        {
            jumpAction.performed -= OnJumpPerformed;
        }
        if (tiltLeftAction != null)
        {
            tiltLeftAction.canceled -= OnTiltLeftEnd;
            tiltLeftAction.performed -= OnTiltLeftStart;
        }
        if (tiltRightAction != null)
        {
            tiltRightAction.canceled -= OnTiltRightEnd;
            tiltRightAction.performed -= OnTiltRightStart;
        }
        if (resetRollAction != null)
        {
            resetRollAction.performed -= OnResetRollStart;
        }*/
    }

    void Update()
    {
        UpdateModelRotation();

        if (stateMachine == null) return;
        stateMachine.Update();
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        if (moveInput != Vector2.zero)
        {
            PlayerEvents.Trigger(PlayerAction.Move);
        }

        Vector2 lookInput = lookAction.ReadValue<Vector2>();
        if (lookInput != Vector2.zero)
        {
            PlayerEvents.Trigger(PlayerAction.Look);
        }

        // Auto-step debug visualization
        if (EnableAutoStepDebug)
        {
            BasePlayerState.DrawAutoStepDebug();
        }
    }

    void FixedUpdate()
    {
        // Process smooth step interpolation
        if (isSteppingUp)
        {
            UpdateSmoothStep(Time.fixedDeltaTime);
        }

        // Refresh ground state in physics step
        PerformGroundCheck();
        // Drive physics-related state logic here for consistent Rigidbody interaction
        if (stateMachine != null)
        {
            stateMachine.PhysicsUpdate();
        }
    }

    /// <summary>
    /// Starts a smooth step-up movement from current position to target position.
    /// Called by BasePlayerState when a step is detected.
    /// </summary>
    public void StartSmoothStep(Vector3 targetPosition, Collider stepCollider)
    {
        StartSmoothStep(Rigidbody.position, targetPosition, stepCollider, false);
    }

    /// <summary>
    /// Starts a smooth step from a pre-raised position while keeping collision active.
    /// </summary>
    public void StartSmoothStepFromRaised(Vector3 raisedPosition, Vector3 targetPosition, Collider stepCollider)
    {
        StartSmoothStep(raisedPosition, targetPosition, stepCollider, false);
    }

    private void StartSmoothStep(Vector3 startPosition, Vector3 targetPosition, Collider stepCollider, bool ignoreStepCollider)
    {
        // If step duration is 0 or very small, use instant movement
        if (stepDuration < 0.01f)
        {
            Rigidbody.MovePosition(targetPosition);
            return;
        }

        Rigidbody.MovePosition(startPosition);
        stepStartPosition = startPosition;
        stepTargetPosition = targetPosition;
        stepProgress = 0f;
        isSteppingUp = true;
        currentStepCollider = ignoreStepCollider ? stepCollider : null;

        if (EnableAutoStepDebug)
        {
            Debug.Log($"[SmoothStep] START from {stepStartPosition} to {stepTargetPosition}, duration={stepDuration}s, collider={stepCollider?.name}, ignoreCollision={ignoreStepCollider}");
        }
    }

    /// <summary>
    /// Updates the smooth step interpolation each physics frame.
    /// </summary>
    private void UpdateSmoothStep(float deltaTime)
    {
        if (!isSteppingUp) return;

        // Advance progress
        stepProgress += deltaTime / stepDuration;

        if (stepProgress >= 1f)
        {
            // Step complete
            stepProgress = 1f;
            Rigidbody.MovePosition(stepTargetPosition);
            if (EnableAutoStepDebug)
            {
                Debug.Log($"[SmoothStep] COMPLETE at {stepTargetPosition}");
            }
            CompleteSmoothStep();
        }
        else
        {
            // Apply easing curve: ease-out effect using power function
            // t^(1/easing) for easing > 1 gives ease-out, linear for easing = 1
            float easedProgress = stepEasing > 0.01f
                ? 1f - Mathf.Pow(1f - stepProgress, stepEasing)
                : stepProgress;

            Vector3 newPosition = Vector3.Lerp(stepStartPosition, stepTargetPosition, easedProgress);
            Rigidbody.MovePosition(newPosition);

            if (EnableAutoStepDebug)
            {
                Debug.Log($"[SmoothStep] progress={stepProgress:F2} eased={easedProgress:F2} pos={newPosition}");
            }
        }
    }

    /// <summary>
    /// Completes the smooth step and re-enables collision.
    /// </summary>
    private void CompleteSmoothStep()
    {
        isSteppingUp = false;

        // Re-enable collision with the step collider
        Collider playerCollider = GetComponent<Collider>();
        if (playerCollider != null && currentStepCollider != null)
        {
            Physics.IgnoreCollision(playerCollider, currentStepCollider, false);
        }
        currentStepCollider = null;
    }

    /// <summary>
    /// Cancels an in-progress smooth step (e.g., if player jumps during step).
    /// </summary>
    public void CancelSmoothStep()
    {
        if (!isSteppingUp) return;
        CompleteSmoothStep();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        stateMachine.TriggerJump();
    }

    private void OnTiltLeftStart(InputAction.CallbackContext context)
    {
        var roll = playerMoveCam.GetComponent<Zoom>();
        if (roll != null) roll.IsMovingLeft = true;
    }

    private void OnTiltLeftEnd(InputAction.CallbackContext context)
    {
        var roll = playerMoveCam.GetComponent<Zoom>();
        if (roll != null) roll.IsMovingLeft = false;
    }

    private void OnTiltRightStart(InputAction.CallbackContext context)
    {
        var roll = playerMoveCam.GetComponent<Zoom>();
        if (roll != null) roll.IsMovingRight = true;
    }

    private void OnTiltRightEnd(InputAction.CallbackContext context)
    {
        var roll = playerMoveCam.GetComponent<Zoom>();
        if (roll != null) roll.IsMovingRight = false;
    }

    private void OnResetRollStart(InputAction.CallbackContext context)
    {
        var roll = playerMoveCam.GetComponent<Zoom>();
        if (roll != null) roll.IsMovingCenter = true;
    }

    /// <summary>
    /// Sets gravity direction immediately (e.g. each physics step from a blend volume).
    /// Cancels any in-progress SwitchGravity transition and smoothly rotates toward the new "up".
    /// </summary>
    public void SetBlendedGravityDirection(Vector3 newGravityDirection, float deltaTime)
    {
        if (newGravityDirection.sqrMagnitude < 1e-8f) return;
        newGravityDirection.Normalize();

        if (gravityTransitionCoroutine != null)
        {
            StopCoroutine(gravityTransitionCoroutine);
            gravityTransitionCoroutine = null;
        }
        isTransitioningGravity = false;

        currentGravityDirection = newGravityDirection;

        if (!enableGravityRotation || Rigidbody == null) return;

        Vector3 newUp = -newGravityDirection;
        Vector3 currentForward = transform.forward;
        Vector3 projectedForward = Vector3.ProjectOnPlane(currentForward, newUp);
        if (projectedForward.sqrMagnitude < 1e-6f)
        {
            Vector3 arbitrary = Mathf.Abs(Vector3.Dot(newUp, Vector3.forward)) < 0.99f ? Vector3.forward : Vector3.right;
            projectedForward = Vector3.Cross(newUp, arbitrary).normalized;
        }

        Quaternion targetRotation = Quaternion.LookRotation(projectedForward.normalized, newUp);
        float t = 1f - Mathf.Exp(-blendedGravityRotationSpeed * deltaTime);
        Rigidbody.MoveRotation(Quaternion.Slerp(Rigidbody.rotation, targetRotation, t));
    }

    public void SwitchGravity(Vector3 newGravityDirection)
    {
        if (isTransitioningGravity) return;

        newGravityDirection = newGravityDirection.normalized;
        if (newGravityDirection == currentGravityDirection) return;

        if (gravityTransitionCoroutine != null)
        {
            StopCoroutine(gravityTransitionCoroutine);
        }

        gravityTransitionCoroutine = StartCoroutine(TransitionGravity(newGravityDirection));
    }

    public void SwitchGravityToDown() => SwitchGravity(Vector3.down);
    public void SwitchGravityToUp() => SwitchGravity(Vector3.up);
    public void SwitchGravityToForward() => SwitchGravity(Vector3.forward);
    public void SwitchGravityToBack() => SwitchGravity(Vector3.back);
    public void SwitchGravityToLeft() => SwitchGravity(Vector3.left);
    public void SwitchGravityToRight() => SwitchGravity(Vector3.right);

    public void SetFallPreventionSuppressed(bool suppressed)
    {
        if (suppressed)
        {
            suppressFallPreventionCount++;
        }
        else
        {
            suppressFallPreventionCount = Mathf.Max(0, suppressFallPreventionCount - 1);
        }
    }

    private IEnumerator TransitionGravity(Vector3 newGravityDirection)
    {
        isTransitioningGravity = true;
        Vector3 oldGravityDirection = currentGravityDirection;
        currentGravityDirection = newGravityDirection;

        if (enableGravityRotation)
        {
            Quaternion startRotation = transform.rotation;

            // Determine new up (opposite gravity) and a stable forward aligned to the new up
            Vector3 newUp = -newGravityDirection;
            Vector3 currentForward = transform.forward;
            Vector3 projectedForward = Vector3.ProjectOnPlane(currentForward, newUp);
            if (projectedForward.sqrMagnitude < 1e-6f)
            {
                // Fallback: pick any vector perpendicular to newUp
                Vector3 arbitrary = Mathf.Abs(Vector3.Dot(newUp, Vector3.forward)) < 0.99f ? Vector3.forward : Vector3.right;
                projectedForward = Vector3.Cross(newUp, arbitrary).normalized;
            }

            Quaternion targetRotation = Quaternion.LookRotation(projectedForward.normalized, newUp);

            float elapsed = 0f;
            while (elapsed < gravityTransitionDuration)
            {
                float t = elapsed / gravityTransitionDuration;
                Quaternion slerped = Quaternion.Slerp(startRotation, targetRotation, t);
                Rigidbody.MoveRotation(slerped);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Rigidbody.MoveRotation(targetRotation);
        }

        isTransitioningGravity = false;
        gravityTransitionCoroutine = null;
    }

    void EnablePlayerCameraMove(bool isEnabled)
    {
        //Debug.Log("EnablePlayerCameraMove: " + isEnabled);

        if (camInputProvider != null)
        {
            camInputProvider.enabled = isEnabled;
        }
        else
        {
            camInputProvider = playerMoveCam.GetComponent<CinemachineInputProvider>();
            if (camInputProvider != null)
            {
                camInputProvider.XYAxis = InputActionReference.Create(lookAction);
                camInputProvider.enabled = isEnabled;
            }
            else
            {
                Debug.LogError("CinemachineInputProvider not found on playerMoveCam");
            }
        }

        /*var roll = playerMoveCam.GetComponent<Zoom>();
        if (roll != null)
        {
            roll.enabled = isEnabled;
        }
        else
        {
            var zoom = playerMoveCam.GetComponent<Zoom>();
            if (zoom != null)
            {
                zoom.enabled = isEnabled;
            }
            else
            {
                Debug.LogError("Zoom not found on playerMoveCam");
            }
        }*/
    }

    public void UpdateCameraSensitivity()
    {
        if (povComponent == null) return;

        float mouseMultiplier = (povComponent.m_HorizontalAxis.m_SpeedMode == AxisState.SpeedMode.MaxSpeed) ? 1 : mouseCamFactor;
        povComponent.m_HorizontalAxis.m_MaxSpeed = originalHorizontalSpeed * sensitivityMultiplier * mouseMultiplier;
        povComponent.m_VerticalAxis.m_MaxSpeed = originalVerticalSpeed * sensitivityMultiplier * mouseMultiplier;
    }

    [Header("TIP Debug (BasePlayerController)")]
    [SerializeField] private bool debugTIPRotation = false;

    private void UpdateModelRotation()
    {
        if (modelTransform == null || povComponent == null) return;

        // Always track where the player WANTS to face (camera heading)
        targetYawRotation = povComponent.m_HorizontalAxis.Value;

        // Check if TIP system wants control of rotation
        // When TIP is active and character is stationary, don't rotate the model instantly.
        // Let the TIP system handle foot stepping first, then the model will follow.
        bool tipWantsControl = footIKSystem != null && footIKSystem.IsTIPActive;

        if (debugTIPRotation && footIKSystem != null)
        {
            Debug.Log($"[TIP-Rotation] IsTIPActive={footIKSystem.IsTIPActive} tipWantsControl={tipWantsControl} target={targetYawRotation:F1} current={currentYawRotation:F1}");
        }

        if (tipWantsControl)
        {
            // TIP is active - don't rotate the model
            // The feet are locked/stepping, model rotation is deferred
            // currentYawRotation stays where it was (feet are planted there)
            return;
        }

        // Normal rotation - model follows camera instantly
        // This happens when moving OR when TIP has completed stepping
        currentYawRotation = targetYawRotation;

        Vector3 currentRotation = modelTransform.localEulerAngles;
        currentRotation.y = currentYawRotation;
        modelTransform.localEulerAngles = currentRotation;
    }

    /// <summary>
    /// Called by FootIKSystem when TIP stepping completes to sync model rotation.
    /// </summary>
    public void SyncRotationFromTIP(float newYawRotation)
    {
        currentYawRotation = newYawRotation;
        targetYawRotation = newYawRotation;

        if (modelTransform != null)
        {
            Vector3 currentRotation = modelTransform.localEulerAngles;
            currentRotation.y = currentYawRotation;
            modelTransform.localEulerAngles = currentRotation;
        }
    }

    RaycastHit[] hit = new RaycastHit[10];
    private RaycastHit[] fallProbeHits = new RaycastHit[10];
    private bool IsGroundedWithGrace()
    {
        if (isGrounded) return true;
        if (!Application.isPlaying) return false;
        return (Time.time - lastGroundedTime) <= groundedGraceTime;
    }

    private void CacheGroundHit(RaycastHit groundHit)
    {
        if (groundHit.collider == null) return;
        cachedGroundHit = groundHit;
        hasCachedGroundHit = true;
        if (Application.isPlaying)
        {
            lastCachedGroundHitTime = Time.time;
        }
    }

    private bool TryGetCachedGroundHit(out RaycastHit groundHit)
    {
        groundHit = default;
        if (!hasCachedGroundHit || cachedGroundHit.collider == null) return false;
        if (Application.isPlaying && (Time.time - lastCachedGroundHitTime) > groundHitCacheTime) return false;

        groundHit = cachedGroundHit;
        return true;
    }

    private bool TryGetStableGroundHit(out RaycastHit groundHit, float extraDistance = 0.5f)
    {
        if (TryGetGroundHit(out groundHit, extraDistance))
        {
            CacheGroundHit(groundHit);
            return true;
        }

        return TryGetCachedGroundHit(out groundHit);
    }

    private void PerformGroundCheck()
    {
        if (Rigidbody == null) return;
        Vector3 origin = Rigidbody.transform.position - (currentGravityDirection * 0.5f);

        int hits = Physics.SphereCastNonAlloc(origin, groundCheckRadius, currentGravityDirection, hit, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);

        bool grounded = false;
        bool foundBest = false;
        float bestDist = float.PositiveInfinity;
        RaycastHit bestHit = default;
        for (int i = 0; i < hits; i++)
        {
            Collider c = hit[i].collider;
            if (c != null && !IsSelfCollider(c))
            {
                grounded = true;
                float d = hit[i].distance;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestHit = hit[i];
                    foundBest = true;
                }
            }
        }

        isGrounded = grounded;
        if (grounded && Application.isPlaying)
        {
            lastGroundedTime = Time.time;
            if (foundBest)
            {
                CacheGroundHit(bestHit);
            }
        }
        else if (Application.isPlaying && hasCachedGroundHit && (Time.time - lastCachedGroundHitTime) > groundHitCacheTime)
        {
            hasCachedGroundHit = false;
        }
    }

    public bool TryGetGroundHit(out RaycastHit groundHit, float extraDistance = 0.5f)
    {
        groundHit = default;

        if (Rigidbody == null) return false;

        Vector3 origin = Rigidbody.transform.position - (currentGravityDirection * 0.5f);
        float maxDistance = Mathf.Max(groundCheckDistance, 0f) + Mathf.Max(extraDistance, 0f);

        int hits = Physics.SphereCastNonAlloc(origin, groundCheckRadius, currentGravityDirection, hit, maxDistance, groundLayer, QueryTriggerInteraction.Ignore);
        if (hits <= 0) return false;

        bool found = false;
        float bestDist = float.PositiveInfinity;
        for (int i = 0; i < hits; i++)
        {
            Collider c = hit[i].collider;
            if (c == null || IsSelfCollider(c)) continue;

            float d = hit[i].distance;
            if (d < bestDist)
            {
                bestDist = d;
                groundHit = hit[i];
                found = true;
            }
        }

        return found;
    }

    public bool ShouldBlockMovementForFall(Vector3 worldMoveDirection, float intendedMoveDistance, out float dropHeight)
    {
        dropHeight = 0f;

        if (!enableFallPrevention) return false;
        if (!IsGroundedWithGrace()) return false;
        if (Rigidbody == null) return false;

        Vector3 up = (-currentGravityDirection).normalized;
        Vector3 horizontalDir = Vector3.ProjectOnPlane(worldMoveDirection, up);
        if (horizontalDir.sqrMagnitude < 1e-6f) return false;
        horizontalDir.Normalize();

        RaycastHit currentGroundHit;
        if (!TryGetStableGroundHit(out currentGroundHit, extraDistance: 1.0f))
        {
            // If we can't find our current ground point, don't block movement.
            return false;
        }

        // If standing on a fallable layer, allow walking off (don't block).
        if (currentGroundHit.collider != null && ((1 << currentGroundHit.collider.gameObject.layer) & fallableLayers) != 0)
            return false;

        float probeForward = Mathf.Max(groundCheckRadius, Mathf.Min(fallProbeForward, intendedMoveDistance + stepForwardExtra));
        Vector3 probeOrigin = Rigidbody.position + (horizontalDir * probeForward) + (up * (groundCheckRadius + fallProbeUpOffset));

        float probeRadius = Mathf.Max(0.05f, groundCheckRadius * 0.75f);
        float downDistance = Mathf.Max(0.1f, fallProbeDownDistance);

        int hits = Physics.SphereCastNonAlloc(probeOrigin, probeRadius, currentGravityDirection, fallProbeHits, downDistance, groundLayer, QueryTriggerInteraction.Ignore);

        bool foundAheadGround = false;
        RaycastHit bestAhead = default;
        float bestDist = float.PositiveInfinity;

        // Fallback: if slope is too steep to be "walkable", still treat it as "not a cliff"
        // so ramps don't get mistaken as infinite drops.
        bool foundAnyAhead = false;
        RaycastHit bestAnyAhead = default;
        float bestAnyDist = float.PositiveInfinity;

        for (int i = 0; i < hits; i++)
        {
            RaycastHit h = fallProbeHits[i];
            if (h.collider == null || IsSelfCollider(h.collider)) continue;

            float upDot = Vector3.Dot(h.normal, up);
            bool sameAsCurrentSupport = currentGroundHit.collider != null && h.collider == currentGroundHit.collider;

            if (upDot < fallProbeMinUpDot)
            {
                // Ignore steep faces from the collider we're already standing on.
                // This avoids stair mesh risers being treated as "safe ahead ground".
                if (sameAsCurrentSupport) continue;

                if (h.distance < bestAnyDist)
                {
                    bestAnyDist = h.distance;
                    bestAnyAhead = h;
                    foundAnyAhead = true;
                }
                continue;
            }

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestAhead = h;
                foundAheadGround = true;
            }
        }

        if (!foundAheadGround)
        {
            if (foundAnyAhead)
            {
                // We hit something ahead but it was too steep to be walkable (likely a steep ramp or wall).
                // Treat it as "not a cliff" so we don't block forward motion incorrectly.
                bestAhead = bestAnyAhead;
                foundAheadGround = true;
            }
            else
            {
                // No ground detected within search distance -> treat as too large a drop.
                dropHeight = float.PositiveInfinity;
                return true;
            }
        }

        // If collider starts inside the spherecast, hit point will always be zero.
        // This circumvents that by assuming those cases are not a large drop.
        if (bestAhead.point == Vector3.zero)
        {
            return false;
        }

        float currentHeight = Vector3.Dot(currentGroundHit.point, up);
        float aheadHeight = Vector3.Dot(bestAhead.point, up);
        dropHeight = currentHeight - aheadHeight; // positive means dropping "down" along gravity

        // If ahead is same height or higher, it's not a drop.
        if (dropHeight <= 0f) return false;
        
        return dropHeight > maxAllowedDropHeight;
    }

    private bool IsSelfCollider(Collider c)
    {
        if (c == null) return false;
        if (Rigidbody != null && c.attachedRigidbody == Rigidbody) return true;
        return c.transform.IsChildOf(transform);
    }

    /*/// <summary>
    /// Pauses player input (movement and camera). Can be called multiple times safely.
    /// </summary>
    private void PauseInput()
    {
        //if (isInputPaused) return;

        //isInputPaused = true;

        // Store current camera state before disabling
        wasCameraEnabledBeforePause = camInputProvider != null && camInputProvider.enabled;

        // Disable movement input
        if (moveAction != null)
        {
            moveAction.Disable();
        }

        // Disable camera movement
        EnablePlayerCameraMove(false);
    }

    /// <summary>
    /// Resumes player input (movement and camera). Restores previous camera state.
    /// </summary>
    private void ResumeInput()
    {
        //if (!isInputPaused) return;

        //isInputPaused = false;

        // Re-enable movement input
        if (moveAction != null)
        {
            moveAction.Enable();
        }

        // Restore camera movement to previous state
        EnablePlayerCameraMove(wasCameraEnabledBeforePause);
    }*/

    public void SetLookPaused(bool paused)
    {
        
        if (paused == InputData.IsLookPaused) return;
        //Debug.Log(playerID + " SetLookPaused called. paused: " + paused);
        InputData.IsLookPaused = paused;

        if (paused)
        {
            // Store current camera state before disabling
            wasCameraEnabledBeforePause = camInputProvider != null && camInputProvider.enabled;
            // Disable camera movement
            EnablePlayerCameraMove(false);
            lookAction.Disable();
        }
        else
        {
            EnablePlayerCameraMove(wasCameraEnabledBeforePause);
            lookAction.Enable();
        }
    }
    
    public void SetMovePaused(bool paused)
    {
        if (paused == InputData.IsMovePaused) return;
        //Debug.Log( playerID + " SetMovePaused called. paused: " + paused);
        InputData.IsMovePaused = paused;

        if (paused)
        {
            // Disable movement input
            if (moveAction != null)
            {
                moveAction.Disable();
            }
        }
        else
        {
            // Disable movement input
            if (moveAction != null)
            {
                moveAction.Enable();
            }
        }
    }

    /// <summary>
    /// Sets input pause state. True to pause, false to resume.
    /// </summary>
    public void SetAllInputPaused(bool paused)
    {
        if (TutorialManager.Instance.IsInputTutorialActive)
        {
            SetLookPaused(paused);
            SetMovePaused(true);
        }
        else
        {
            SetLookPaused(paused);
            SetMovePaused(paused);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (EnableAutoStepDebug)
        {
            BasePlayerState.DrawAutoStepGizmos();
            DrawAutoStepParameterPreviewGizmos();
        }

        if (!enableDebugGroundCheckGizmos) return;

        // Prefer cached Rigidbody at runtime; fall back to component lookup in edit mode
        Rigidbody rb = Application.isPlaying ? Rigidbody : GetComponent<Rigidbody>();

        Vector3 origin = (rb != null ? rb.transform.position : transform.position) - (currentGravityDirection * 0.5f);
        Vector3 direction = currentGravityDirection; // match PerformGroundCheck()

        // Draw the sweep start and end positions and the connecting line
        Gizmos.color = groundCheckGizmoColor;
        Gizmos.DrawWireSphere(origin, groundCheckRadius);
        Vector3 end = origin + direction.normalized * groundCheckDistance;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawWireSphere(end, groundCheckRadius);

        // In play mode, also visualize hit points (if any)
        if (Application.isPlaying)
        {
            RaycastHit[] localHits = new RaycastHit[10];
            int hitCount = Physics.SphereCastNonAlloc(origin, groundCheckRadius, direction, localHits, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore);
            if (hitCount > 0)
            {
                Gizmos.color = groundHitGizmoColor;
                for (int i = 0; i < hitCount; i++)
                {
                    if (localHits[i].collider != null && !IsSelfCollider(localHits[i].collider))
                    {
                        Gizmos.DrawWireSphere(localHits[i].point, 0.05f);
                    }
                }
            }
        }
    }

    private void DrawAutoStepParameterPreviewGizmos()
    {
        // Parameter-driven preview so gizmos update immediately when tuning
        // Max Step Height / Step Max Up Dot / Step Forward Extra in the inspector.
        Rigidbody rb = Application.isPlaying ? Rigidbody : GetComponent<Rigidbody>();
        Vector3 origin = rb != null ? rb.position : transform.position;

        Vector3 up = (-currentGravityDirection).normalized;
        if (up.sqrMagnitude < 1e-6f) up = Vector3.up;

        // For tuning, show preview on player local Z exactly (requested behavior).
        Vector3 forwardDir = transform.forward;
        if (forwardDir.sqrMagnitude < 1e-6f)
            forwardDir = Vector3.forward;
        forwardDir.Normalize();

        float previewForwardDistance = Mathf.Max(autoStepMinProbeDistance, stepForwardExtra + groundCheckRadius);
        float lowProbeHeight = Mathf.Max(0.03f, maxStepHeight * 0.12f);
        float highProbeHeight = Mathf.Max(lowProbeHeight + 0.04f, maxStepHeight * 0.55f);
        float probeRadius = Mathf.Max(0.04f, groundCheckRadius * 0.45f);

        // Show max climb height directly.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + up * maxStepHeight);
        Gizmos.DrawWireSphere(origin + up * maxStepHeight, 0.02f);

        // Show forward preview distance from current tuning.
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + forwardDir * previewForwardDistance);

        // Match low/high forward probes used by runtime detection.
        Vector3 lowStart = origin + up * lowProbeHeight;
        Vector3 lowEnd = lowStart + forwardDir * previewForwardDistance;
        Vector3 highStart = origin + up * highProbeHeight;
        Vector3 highEnd = highStart + forwardDir * previewForwardDistance;

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawLine(lowStart, lowEnd);
        Gizmos.DrawWireSphere(lowStart, probeRadius * 0.35f);
        Gizmos.DrawWireSphere(lowEnd, probeRadius * 0.35f);

        Gizmos.color = new Color(1f, 0f, 1f);
        Gizmos.DrawLine(highStart, highEnd);
        Gizmos.DrawWireSphere(highStart, probeRadius * 0.35f);
        Gizmos.DrawWireSphere(highEnd, probeRadius * 0.35f);

        // Visualize the Step Max Up Dot threshold as an allowed-normal boundary.
        float clampedDot = Mathf.Clamp(stepMaxUpDot, -1f, 1f);
        float slopeAngle = Mathf.Acos(clampedDot) * Mathf.Rad2Deg;
        Vector3 axis = Vector3.Cross(up, forwardDir);
        if (axis.sqrMagnitude < 1e-6f)
            axis = Vector3.Cross(up, Vector3.right);
        axis.Normalize();

        Vector3 thresholdNormal = Quaternion.AngleAxis(slopeAngle, axis) * up;
        Vector3 normalAnchor = origin + forwardDir * (previewForwardDistance * 0.45f) + up * 0.02f;

        Gizmos.color = Color.green; // Perfect up normal reference
        Gizmos.DrawLine(normalAnchor, normalAnchor + up * 0.22f);
        Gizmos.color = Color.blue;  // Threshold normal based on Step Max Up Dot
        Gizmos.DrawLine(normalAnchor, normalAnchor + thresholdNormal * 0.22f);
    }
}
