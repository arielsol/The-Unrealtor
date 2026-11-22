using UnityEngine;

/// <summary>
/// Foot IK system with proper pelvis displacement, soft IK, multi-ray ground detection,
/// and slope-aligned foot placement. Now gravity-aware for wall-walking support.
/// Flow: Detect ground → Calculate pelvis drop → Apply IK → Align feet to slope
/// </summary>
public class FootIKSystem : MonoBehaviour
{
    [Header("IK Settings")]
    [SerializeField] private bool enableIK = true;
    [SerializeField] [Range(0f, 1f)] private float ikWeight = 1f;

    [Header("Gravity Reference")]
    [Tooltip("Reference to the player controller for gravity direction. If null, will search in parent.")]
    [SerializeField] private BasePlayerController playerController;

    [Header("Left Leg Bones")]
    [SerializeField] private Transform leftHip;
    [SerializeField] private Transform leftKnee;
    [SerializeField] private Transform leftAnkle;
    [SerializeField] private Transform leftToesEnd;

    [Header("Right Leg Bones")]
    [SerializeField] private Transform rightHip;
    [SerializeField] private Transform rightKnee;
    [SerializeField] private Transform rightAnkle;
    [SerializeField] private Transform rightToesEnd;

    [Header("Body")]
    [SerializeField] private Transform root;
    [SerializeField] private Animator animator;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float raycastDistance = 2.0f;
    [SerializeField] private float raycastOriginHeight = 0.5f;
    [SerializeField] private bool useMultiRay = true;
    [SerializeField] private Vector3 heelOffset = new Vector3(0f, 0f, -0.08f);
    [SerializeField] private Vector3 ballOffset = new Vector3(0f, 0f, 0.05f);
    [SerializeField] private float toeRayWeight = 0.25f;
    [SerializeField] private float ballRayWeight = 0.5f;
    [SerializeField] private float heelRayWeight = 0.25f;

    [Header("Foot Settings")]
    [SerializeField] private float groundOffset = 0.02f;

    [Header("Soft IK")]
    [Tooltip("Ratio of max reach where soft falloff begins (0.95 = starts slowing at 95% extension)")]
    [SerializeField] [Range(0.8f, 0.99f)] private float softIKRatio = 0.95f;

    [Header("Foot Alignment")]
    [SerializeField] private float footAlignmentSmoothTime = 0.1f;
    [SerializeField] [Range(0f, 1f)] private float footAlignmentWeight = 1f;

    [Header("Pelvis Settings")]
    [SerializeField] private float maxPelvisOffset = 1.0f;
    [SerializeField] private float pelvisSmoothTime = 0.1f;
    [SerializeField] private float airborneResetSpeed = 5f;
    [Tooltip("Minimum dropNeeded to apply pelvis offset. Filters micro-variations from the blend tree " +
             "during slow movement that otherwise cause a visible tilt when inching forwards.")]
    [SerializeField] private float minPelvisDropThreshold = 0.02f;

    [Header("Ledge Settings")]
    [Tooltip("Height difference threshold to trigger ledge stance logic")]
    [SerializeField] private float ledgeThreshold = 0.3f;
    [Tooltip("When on ledge, how much the lower leg can bend (0 = fully straight)")]
    [SerializeField] [Range(0f, 0.2f)] private float lowerLegBendAllowance = 0.05f;

    [Header("Step Arc Settings")]
    [SerializeField] private float stepHeight = 0.1f;
    [SerializeField] private bool useStepArc = true;
    [Tooltip("Velocity threshold to detect swing phase (uses actual foot movement)")]
    [SerializeField] private float swingVelocityThreshold = 0.1f;

    [Header("Knee Direction")]
    [SerializeField] private Vector3 kneeForwardAxis = Vector3.forward;

    [Header("Velocity-Aware Pelvis (Elevator Fix)")]
    [Tooltip("Character vertical velocity above which pelvis snaps instantly")]
    [SerializeField] private float pelvisSnapVelocityThreshold = 2.0f;
    [Tooltip("Rate of pelvis target change (m/s) above which pelvis snaps instantly")]
    [SerializeField] private float pelvisSnapDeltaRate = 0.5f;

    [Header("Adaptive Knee Direction (Wide Stance Fix)")]
    [Tooltip("Blend between character forward (0) and hip's outward direction (1) for knee orientation")]
    [SerializeField] [Range(0f, 1f)] private float hipForwardBlend = 0.5f;

    [Header("Step Arc Hysteresis (Idle Hop Fix)")]
    [Tooltip("Time foot must be un-grounded before step arc activates")]
    [SerializeField] private float ungroundedTimeThreshold = 0.05f;
    [Tooltip("Minimum horizontal speed required to trigger step arc")]
    [SerializeField] private float minimumMoveSpeed = 0.3f;
    [Tooltip("Exit threshold multiplier for hysteresis (lower = stickier swing state)")]
    [SerializeField] [Range(0.5f, 0.9f)] private float swingExitMultiplier = 0.7f;

    // ═══════════════════════════════════════════════════════════════════════════════
    // TURN-IN-PLACE (TIP) SYSTEM - Industrial Standard Implementation
    // Features: Spine-lead twist buffer, inside-out staggered stepping, world-space
    // foot locking, weight shift, and proper blend-out on movement.
    // ═══════════════════════════════════════════════════════════════════════════════

    [Header("Turn-In-Place Settings")]
    [Tooltip("Enable Turn-In-Place system")]
    [SerializeField] private bool enableTurnInPlace = true;
    [Tooltip("Velocity threshold below which TIP activates (character considered stationary)")]
    [SerializeField] private float tipVelocityThreshold = 0.1f;
    [Tooltip("Twist angle threshold between chest and root to trigger stepping (degrees)")]
    [SerializeField] private float twistThreshold = 50f;
    [Tooltip("Duration of each foot step (seconds)")]
    [SerializeField] private float tipStepDuration = 0.5f;
    [Tooltip("Peak height of parabolic foot arc")]
    [SerializeField] private float tipFootLiftHeight = 0.08f;
    [Tooltip("How fast to blend out of TIP when movement starts")]
    [SerializeField] private float tipBlendOutSpeed = 3f;

    [Header("TIP Weight Shift")]
    [Tooltip("Pelvis lateral shift toward stationary foot during step")]
    [SerializeField] private float tipWeightShiftAmount = 0.04f;
    [Tooltip("Pelvis vertical dip during step")]
    [SerializeField] private float tipPelvisDip = 0.02f;

    [Header("TIP Spine Chain (for gradual twist)")]
    [Tooltip("Spine bones from bottom to top for gradual twist distribution")]
    [SerializeField] private Transform spine1;
    [SerializeField] private Transform spine2;
    [SerializeField] private Transform spine3;
    [SerializeField] private Transform neck;
    [SerializeField] private Transform head;

    [Header("TIP Head Look-At")]
    [Tooltip("Enable head to look toward camera direction")]
    [SerializeField] private bool enableHeadLookAt = true;
    [Tooltip("Maximum head turn angle (degrees)")]
    [SerializeField] private float maxHeadTurnAngle = 60f;
    [Tooltip("How fast head rotates to target")]
    [SerializeField] private float headLookAtSpeed = 3f;

    [Header("TIP Spine Twist Settings")]
    [Tooltip("How much the spine can twist ahead of feet (multiplier)")]
    [SerializeField] [Range(0f, 1f)] private float spineLeadAmount = 0.7f;
    [Tooltip("Speed of spine twist interpolation")]
    [SerializeField] private float spineTwistSpeed = 4f;
    [Tooltip("Twist distribution weights (spine1, spine2, spine3, neck) - should sum to ~1")]
    [SerializeField] private Vector4 spineTwistDistribution = new Vector4(0.15f, 0.25f, 0.35f, 0.25f);

    [Header("TIP Debug")]
    [SerializeField] private bool debugTIP = false;

    /// <summary>
    /// Public property for movement controller to check if TIP wants control.
    /// Returns true when TIP is actively controlling rotation (feet locked/stepping).
    /// </summary>
    public bool IsTIPActive => enableTurnInPlace && tipBlendWeight > 0.5f && tipState != TIPState.Inactive;

    /// <summary>
    /// Resets the Turn-In-Place system. Call this after teleporting the character
    /// or loading a save to prevent feet from stretching toward old locked positions.
    /// </summary>
    public void ResetTIP()
    {
        tipState = TIPState.Inactive;
        tipBlendWeight = 0f;
        tipLeftFoot.isLocked = false;
        tipLeftFoot.isStepping = false;
        tipRightFoot.isLocked = false;
        tipRightFoot.isStepping = false;

        // Reset locked positions to current ankle positions (if available)
        if (leftAnkle != null)
        {
            tipLeftFoot.lockedWorldPosition = leftAnkle.position;
            tipLeftFoot.lockedWorldRotation = leftAnkle.rotation;
        }
        if (rightAnkle != null)
        {
            tipRightFoot.lockedWorldPosition = rightAnkle.position;
            tipRightFoot.lockedWorldRotation = rightAnkle.rotation;
        }

        // Reset position tracking to prevent false teleport detection next frame
        positionInitialized = false;

        // Block TIP re-entry for long enough that the idle blend tree settles (~0.25s covers
        // the 0.05f dampTime used by IdleState before feet get locked again in mid-stride pose)
        tipReactivationCooldown = 0.25f;
    }

    // Debug: expose TIP state for inspector
    [SerializeField, HideInInspector] private string debugTIPState;
    [SerializeField, HideInInspector] private float debugTIPBlendWeight;
    [SerializeField, HideInInspector] private float debugTwist;
    [SerializeField, HideInInspector] private float debugVelocity;

    // TIP State Machine
    private enum TIPState
    {
        Inactive,           // Not in TIP mode (moving or TIP disabled)
        TwistBuffer,        // Stationary, accumulating twist (chest leads, feet locked)
        InsideFootStepping, // Inside foot is stepping toward target heading
        OutsideFootStepping,// Outside foot is stepping (triggered at 50% of inside)
        Settling            // Both feet done, settling back to idle
    }

    // Per-foot TIP data
    private struct TIPFootData
    {
        public bool isLocked;                 // Is this foot locked in world space?
        public Vector3 lockedWorldPosition;   // World-space locked position
        public Quaternion lockedWorldRotation;// World-space locked rotation
        public bool isStepping;               // Is this foot currently stepping?
        public float stepAlpha;               // 0.0 to 1.0 step interpolation
        public Vector3 stepStartPosition;     // Start of step arc
        public Vector3 stepTargetPosition;    // End of step arc
        public Quaternion stepStartRotation;
        public Quaternion stepTargetRotation;
    }

    // TIP Runtime State
    private TIPState tipState = TIPState.Inactive;
    private TIPFootData tipLeftFoot;
    private TIPFootData tipRightFoot;
    private float tipBlendWeight = 0f;        // 0 = normal IK, 1 = full TIP control
    private float tipReactivationCooldown = 0f; // Blocks TIP re-entry after ResetTIP() until idle pose settles
    private float targetHeadingYaw;           // Target yaw angle (where player wants to face)
    private float rootHeadingYaw;             // Current root yaw angle
    private float accumulatedTwist;           // Current twist between chest and root
    private int turnDirection;                // -1 = left, +1 = right, 0 = none
    private bool isInsideFootLeft;            // Which foot is the "inside" foot
    private Vector3 tipWeightShiftOffset;     // Current pelvis weight shift
    private float tipWeightShiftVelocity;     // For smoothing

    // Spine twist runtime state
    private float currentSpineTwist;          // Smoothed spine twist angle
    private float[] spineCurrentTwists = new float[4]; // Per-bone twist (spine1, spine2, spine3, neck)
    private Quaternion[] spineOriginalRotations = new Quaternion[4]; // Cached original rotations
    private bool spineRotationsCached = false;

    // ── Optimization caches (avoids per-frame heap allocations) ──────────────
    // Pre-allocated spine arrays — prevents 2 GC allocs/frame in ApplySpineTwist
    private readonly Transform[] _spineBones = new Transform[4];
    private readonly float[]     _spineDistribution = new float[4];

    // Cached reciprocal of tipStepDuration — avoids division in step loops
    private float _tipAlphaRate;

    // Debug state cache — prevents Enum.ToString() string alloc every frame
    private TIPState _lastDebugTipState = (TIPState)(-1);

    // Teleport threshold squared — avoids sqrt in Vector3.Distance each frame
    private const float TELEPORT_DISTANCE_SQ =
        TELEPORT_DISTANCE_THRESHOLD * TELEPORT_DISTANCE_THRESHOLD;

    // Performance tracking (A/B testing)
    [Header("Performance Tracking (A/B)")]
    [Tooltip("Enable to expose LateUpdate timing in the inspector for before/after comparison")]
    [SerializeField] private bool trackPerformance = false;
    [SerializeField, HideInInspector] private float lastUpdateMs;
    [SerializeField, HideInInspector] private float avgUpdateMs;
    private System.Diagnostics.Stopwatch _perfTimer;
    private float _perfAccum;
    private int   _perfSamples;

    // Head look-at runtime state
    private float currentHeadYaw;             // Current head horizontal angle
    private float currentHeadPitch;           // Current head vertical angle

    // Root rotation interpolation during stepping (AAA standard: rotate DURING step, not after)
    private float steppingStartYaw;           // Root yaw at step start
    private float currentRotatingYaw;         // Interpolated yaw during stepping

    // Head look-at base rotation caching (prevents cumulative tilt)
    private Quaternion headBaseLocalRotation; // Cached after animator runs
    private bool headBaseCached = false;

    // Pre-calculated final foot positions (calculated at turn START)
    private Vector3 leftFootFinalPosition;
    private Vector3 rightFootFinalPosition;
    private Quaternion leftFootFinalRotation;
    private Quaternion rightFootFinalRotation;

    // Runtime data - leg lengths cached once at Start
    private float leftUpperLegLength;
    private float leftLowerLegLength;
    private float rightUpperLegLength;
    private float rightLowerLegLength;

    private float currentPelvisOffset;
    private float pelvisVelocity;
    private Vector3 originalRootLocalPos;

    // Velocity-aware pelvis tracking (Challenge 1: Elevator Fix)
    private float previousPelvisTarget;
    private Vector3 previousRootWorldPosition;
    private float characterVerticalVelocity;

    // Adaptive pole vector caching (Challenge 2: Wide Stance Fix)
    private Quaternion leftHipInitialLocalRotation;
    private Quaternion rightHipInitialLocalRotation;

    // Per-foot data
    private struct FootIKData
    {
        public bool grounded;
        public float groundHeight;
        public float dropNeeded;
        public Vector3 targetAnklePosition;
        public Vector3 groundNormal;
        public Vector3 smoothedNormal;
        public Vector3 normalVelocity;
        public Vector3 previousPosition;
        public float verticalVelocity;

        // Hysteresis fields (Challenge 3: Idle Hop Fix)
        public float ungroundedTime;      // Time foot has been detected as "lifting"
        public bool isInSwingPhase;       // Latched swing state for hysteresis
    }

    private FootIKData leftFoot;
    private FootIKData rightFoot;

    // Ledge state
    private bool isOnLedge;
    private bool leftFootIsLower;

    // Turn tracking (for detecting rotation direction)
    private float previousModelYRotation;

    // Teleport detection (Challenge 4: TIP Stale Position Fix)
    private Vector3 previousFramePosition;
    private bool positionInitialized = false;
    private const float TELEPORT_DISTANCE_THRESHOLD = 1.0f;

    // Gravity-aware direction properties
    // These replace hardcoded Vector3.up/down to support wall-walking
    private Vector3 GravityUp => playerController != null ? -playerController.GravityDirection : Vector3.up;
    private Vector3 GravityDown => playerController != null ? playerController.GravityDirection : Vector3.down;

    private void Start()
    {
        CacheLegLengths();

        // Auto-find player controller if not assigned
        if (playerController == null)
        {
            playerController = GetComponentInParent<BasePlayerController>();
        }

        if (root != null)
        {
            originalRootLocalPos = root.localPosition;
            // Initialize velocity tracking (Challenge 1: Elevator Fix)
            previousRootWorldPosition = root.position;
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // Initialize smoothed normals to prevent first-frame jitter (gravity-aware)
        leftFoot.smoothedNormal = GravityUp;
        rightFoot.smoothedNormal = GravityUp;
        leftFoot.previousPosition = (leftToesEnd != null) ? leftToesEnd.position : Vector3.zero;
        rightFoot.previousPosition = (rightToesEnd != null) ? rightToesEnd.position : Vector3.zero;

        // Cache initial hip rotations for adaptive pole vector (Challenge 2: Wide Stance Fix)
        // These represent the "natural" knee orientation before any IK is applied
        if (leftHip != null)
        {
            leftHipInitialLocalRotation = leftHip.localRotation;
        }
        if (rightHip != null)
        {
            rightHipInitialLocalRotation = rightHip.localRotation;
        }

        // Initialize turn-in-place tracking
        previousModelYRotation = transform.eulerAngles.y;

        // Initialize Turn-In-Place system
        InitializeTIPSystem();

        // Prime optimization caches
        RefreshSpineBonesCache();
        _tipAlphaRate = tipStepDuration > 0.0001f ? 1f / tipStepDuration : 0f;
    }

    private void InitializeTIPSystem()
    {
        // Initialize TIP state
        tipState = TIPState.Inactive;
        tipBlendWeight = 0f;
        rootHeadingYaw = transform.eulerAngles.y;
        targetHeadingYaw = rootHeadingYaw;
        accumulatedTwist = 0f;
        turnDirection = 0;

        // Initialize foot data - both feet start locked at current positions
        tipLeftFoot = new TIPFootData
        {
            isLocked = true,
            lockedWorldPosition = leftAnkle != null ? leftAnkle.position : Vector3.zero,
            lockedWorldRotation = leftAnkle != null ? leftAnkle.rotation : Quaternion.identity,
            isStepping = false,
            stepAlpha = 0f
        };

        tipRightFoot = new TIPFootData
        {
            isLocked = true,
            lockedWorldPosition = rightAnkle != null ? rightAnkle.position : Vector3.zero,
            lockedWorldRotation = rightAnkle != null ? rightAnkle.rotation : Quaternion.identity,
            isStepping = false,
            stepAlpha = 0f
        };

        tipWeightShiftOffset = Vector3.zero;
    }

    [ContextMenu("Auto Find Bones")]
    public void AutoFindBones()
    {
        Transform[] allBones = GetComponentsInChildren<Transform>();

        foreach (Transform bone in allBones)
        {
            string name = bone.name.ToLower();

            // Left leg
            if (leftHip == null && ContainsAny(name, "lefthip", "leftupperleg", "left_hip", "l_hip", "hip_l", "leftthigh"))
                leftHip = bone;
            else if (leftKnee == null && ContainsAny(name, "leftknee", "leftlowerleg", "left_knee", "l_knee", "knee_l", "leftcalf", "leftshin"))
                leftKnee = bone;
            else if (leftAnkle == null && ContainsAny(name, "leftankle", "leftfoot", "left_ankle", "l_ankle", "ankle_l", "l_foot", "foot_l") && !name.Contains("toe") && !name.Contains("end"))
                leftAnkle = bone;
            else if (leftToesEnd == null && ContainsAny(name, "lefttoe", "l_toe", "toe_l") && ContainsAny(name, "end", "tip", "nub"))
                leftToesEnd = bone;

            // Right leg
            if (rightHip == null && ContainsAny(name, "righthip", "rightupperleg", "right_hip", "r_hip", "hip_r", "rightthigh"))
                rightHip = bone;
            else if (rightKnee == null && ContainsAny(name, "rightknee", "rightlowerleg", "right_knee", "r_knee", "knee_r", "rightcalf", "rightshin"))
                rightKnee = bone;
            else if (rightAnkle == null && ContainsAny(name, "rightankle", "rightfoot", "right_ankle", "r_ankle", "ankle_r", "r_foot", "foot_r") && !name.Contains("toe") && !name.Contains("end"))
                rightAnkle = bone;
            else if (rightToesEnd == null && ContainsAny(name, "righttoe", "r_toe", "toe_r") && ContainsAny(name, "end", "tip", "nub"))
                rightToesEnd = bone;

            // Root
            if (root == null && ContainsAny(name, "root", "pelvis", "hips"))
                root = bone;
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        Debug.Log("FootIKSystem: Auto-find complete. Please verify bone assignments.");
    }

    private bool ContainsAny(string source, params string[] patterns)
    {
        foreach (string pattern in patterns)
            if (source.Contains(pattern))
                return true;
        return false;
    }

    private void CacheLegLengths()
    {
        if (leftHip != null && leftKnee != null && leftAnkle != null)
        {
            leftUpperLegLength = Vector3.Distance(leftHip.position, leftKnee.position);
            leftLowerLegLength = Vector3.Distance(leftKnee.position, leftAnkle.position);
        }

        if (rightHip != null && rightKnee != null && rightAnkle != null)
        {
            rightUpperLegLength = Vector3.Distance(rightHip.position, rightKnee.position);
            rightLowerLegLength = Vector3.Distance(rightKnee.position, rightAnkle.position);
        }
    }

    /// <summary>
    /// Populates the pre-allocated _spineBones array from serialized fields.
    /// Called once in Start() and again in OnValidate() if bones are reassigned.
    /// </summary>
    private void RefreshSpineBonesCache()
    {
        _spineBones[0] = spine1;
        _spineBones[1] = spine2;
        _spineBones[2] = spine3;
        _spineBones[3] = neck;
    }

    private void LateUpdate()
    {
        if (!enableIK)
            return;

        // Performance tracking — enable 'trackPerformance' in the inspector to compare
        // before/after values in avgUpdateMs (averages over 120 frames).
        if (trackPerformance)
        {
            if (_perfTimer == null) _perfTimer = new System.Diagnostics.Stopwatch();
            _perfTimer.Restart();
        }

        LateUpdateInternal();

        if (trackPerformance && _perfTimer != null)
        {
            _perfTimer.Stop();
            lastUpdateMs = (float)_perfTimer.Elapsed.TotalMilliseconds;
            _perfAccum  += lastUpdateMs;
            _perfSamples++;
            if (_perfSamples >= 120)
            {
                avgUpdateMs  = _perfAccum / _perfSamples;
                _perfAccum   = 0f;
                _perfSamples = 0;
            }
        }
    }

    private void LateUpdateInternal()
    {
        // Note: CacheLegLengths() removed from here - bone lengths are constant, cached in Start()

        float deltaTime = Time.deltaTime;

        // Get animation phase for step arc (fallback only)
        float normalizedTime = GetAnimationNormalizedTime();

        // STEP 1: Detect ground under each foot
        if (useMultiRay)
        {
            DetectGroundMultiRay(leftAnkle, leftToesEnd, ref leftFoot, deltaTime);
            DetectGroundMultiRay(rightAnkle, rightToesEnd, ref rightFoot, deltaTime);
        }
        else
        {
            DetectGroundSingleRay(leftAnkle, leftToesEnd, ref leftFoot, deltaTime);
            DetectGroundSingleRay(rightAnkle, rightToesEnd, ref rightFoot, deltaTime);
        }

        // STEP 2: Calculate world-space ankle targets (before pelvis moves!)
        CalculateFootTarget(leftAnkle, leftToesEnd, ref leftFoot, normalizedTime, 0f, deltaTime);
        CalculateFootTarget(rightAnkle, rightToesEnd, ref rightFoot, normalizedTime, 0.5f, deltaTime);

        // STEP 2.4: Teleport detection - reset TIP if character moved too far in one frame
        // This prevents feet from stretching toward old locked positions after teleport/respawn
        if (enableTurnInPlace)
        {
            Vector3 currentPos = transform.position;
            if (!positionInitialized)
            {
                previousFramePosition = currentPos;
                positionInitialized = true;
            }
            else
            {
                // sqrMagnitude avoids a sqrt vs Vector3.Distance — threshold is 1.0m so sq = 1.0
                if ((currentPos - previousFramePosition).sqrMagnitude > TELEPORT_DISTANCE_SQ)
                {
                    // Character teleported - reset TIP to prevent stale locked positions
                    ResetTIP();
                }
                previousFramePosition = currentPos;
            }
        }

        // STEP 2.5: Turn-In-Place system
        if (enableTurnInPlace)
        {
            UpdateTurnInPlace(deltaTime);
        }

        // STEP 2.6: Inject TIP targets BEFORE IK (single IK pass approach)
        if (enableTurnInPlace && tipBlendWeight > 0.01f)
        {
            InjectTIPTargets();
        }

        // STEP 3: Displace pelvis
        DisplacePelvis(deltaTime);

        // STEP 4: Apply IK to legs
        if (isOnLedge)
        {
            // Only IK the upper foot - lower foot is naturally at ground level
            if (leftFootIsLower)
            {
                if (rightFoot.grounded)
                {
                    ApplyLegIK(rightHip, rightKnee, rightAnkle, rightToesEnd,
                              rightUpperLegLength, rightLowerLegLength, rightFoot, isLeftLeg: false);
                }
            }
            else
            {
                if (leftFoot.grounded)
                {
                    ApplyLegIK(leftHip, leftKnee, leftAnkle, leftToesEnd,
                              leftUpperLegLength, leftLowerLegLength, leftFoot, isLeftLeg: true);
                }
            }
        }
        else
        {
            // Normal mode: IK both feet
            if (leftFoot.grounded)
            {
                ApplyLegIK(leftHip, leftKnee, leftAnkle, leftToesEnd,
                          leftUpperLegLength, leftLowerLegLength, leftFoot, isLeftLeg: true);
            }

            if (rightFoot.grounded)
            {
                ApplyLegIK(rightHip, rightKnee, rightAnkle, rightToesEnd,
                          rightUpperLegLength, rightLowerLegLength, rightFoot, isLeftLeg: false);
            }
        }

        // STEP 5: Align feet to ground slope
        // Skip for TIP-locked/stepping feet — ApplyTIPFootRotation owns their rotation.
        bool leftTIPOwnsRotation  = enableTurnInPlace && (tipLeftFoot.isLocked  || tipLeftFoot.isStepping);
        bool rightTIPOwnsRotation = enableTurnInPlace && (tipRightFoot.isLocked || tipRightFoot.isStepping);

        if (!leftTIPOwnsRotation)
            AlignFootToGround(leftAnkle, leftToesEnd, ref leftFoot, deltaTime);
        if (!rightTIPOwnsRotation)
            AlignFootToGround(rightAnkle, rightToesEnd, ref rightFoot, deltaTime);

        // STEP 6: Apply TIP foot rotation overrides (position already handled by InjectTIPTargets)
        if (enableTurnInPlace && tipBlendWeight > 0.01f)
        {
            ApplyTIPFootRotation();
        }
    }

    private float GetAnimationNormalizedTime()
    {
        // Use GetCurrentAnimatorStateInfo (struct, no alloc) directly.
        // GetCurrentAnimatorClipInfo allocates a managed array every call — avoided here.
        if (animator != null)
        {
            return animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
        }
        return (Time.time * 2f) % 1f;
    }

    /// <summary>
    /// Multi-ray ground detection using heel, ball, and toe positions.
    /// Provides better slope detection and handles uneven geometry.
    /// Now gravity-aware for wall-walking support.
    /// </summary>
    private void DetectGroundMultiRay(Transform ankle, Transform toesEnd, ref FootIKData footData, float deltaTime)
    {
        footData.grounded = false;
        footData.groundHeight = 0f;
        footData.groundNormal = GravityUp;

        if (ankle == null)
            return;

        // Cache gravity directions for this frame
        Vector3 upDir = GravityUp;
        Vector3 downDir = GravityDown;

        int hitCount = 0;
        float weightedDistance = 0f;
        Vector3 accumulatedNormal = Vector3.zero;
        float totalWeight = 0f;
        Vector3 referencePoint = Vector3.zero;

        // Heel ray (behind ankle)
        Vector3 heelWorld = ankle.TransformPoint(heelOffset);
        Vector3 heelRayOrigin = heelWorld + upDir * raycastOriginHeight;
        if (Physics.Raycast(heelRayOrigin, downDir, out RaycastHit heelHit, raycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Use distance along gravity axis instead of world Y
            float distanceAlongGravity = Vector3.Dot(heelHit.point - heelRayOrigin, downDir);
            weightedDistance += distanceAlongGravity * heelRayWeight;
            accumulatedNormal += heelHit.normal * heelRayWeight;
            totalWeight += heelRayWeight;
            referencePoint += heelHit.point * heelRayWeight;
            hitCount++;
        }

        // Ball ray (front of foot, under metatarsals)
        Vector3 ballWorld = ankle.TransformPoint(ballOffset);
        Vector3 ballRayOrigin = ballWorld + upDir * raycastOriginHeight;
        if (Physics.Raycast(ballRayOrigin, downDir, out RaycastHit ballHit, raycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            float distanceAlongGravity = Vector3.Dot(ballHit.point - ballRayOrigin, downDir);
            weightedDistance += distanceAlongGravity * ballRayWeight;
            accumulatedNormal += ballHit.normal * ballRayWeight;
            totalWeight += ballRayWeight;
            referencePoint += ballHit.point * ballRayWeight;
            hitCount++;
        }

        // Toe ray (at toe end position)
        Vector3 toePos = (toesEnd != null) ? toesEnd.position : ankle.position + ankle.forward * 0.1f;
        Vector3 toeRayOrigin = toePos + upDir * raycastOriginHeight;
        if (Physics.Raycast(toeRayOrigin, downDir, out RaycastHit toeHit, raycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            float distanceAlongGravity = Vector3.Dot(toeHit.point - toeRayOrigin, downDir);
            weightedDistance += distanceAlongGravity * toeRayWeight;
            accumulatedNormal += toeHit.normal * toeRayWeight;
            totalWeight += toeRayWeight;
            referencePoint += toeHit.point * toeRayWeight;
            hitCount++;
        }

        if (hitCount > 0 && totalWeight > 0.001f)
        {
            footData.grounded = true;
            // Store the weighted average ground point for gravity-relative calculations
            footData.groundHeight = Vector3.Dot(referencePoint / totalWeight, upDir);
            footData.groundNormal = accumulatedNormal.normalized;
        }

        // Smooth the ground normal to prevent jitter
        footData.smoothedNormal = Vector3.SmoothDamp(
            footData.smoothedNormal,
            footData.groundNormal,
            ref footData.normalVelocity,
            footAlignmentSmoothTime,
            Mathf.Infinity,
            deltaTime
        );
    }

    /// <summary>
    /// Single ray ground detection (legacy mode).
    /// Now gravity-aware for wall-walking support.
    /// </summary>
    private void DetectGroundSingleRay(Transform ankle, Transform toesEnd, ref FootIKData footData, float deltaTime)
    {
        footData.grounded = false;
        footData.groundHeight = 0f;
        footData.groundNormal = GravityUp;

        if (ankle == null)
            return;

        // Cache gravity directions for this frame
        Vector3 upDir = GravityUp;
        Vector3 downDir = GravityDown;

        Vector3 rayOrigin;
        if (toesEnd != null)
        {
            rayOrigin = toesEnd.position + upDir * raycastOriginHeight;
        }
        else
        {
            rayOrigin = ankle.position + upDir * raycastOriginHeight;
        }

        if (Physics.Raycast(rayOrigin, downDir, out RaycastHit hit, raycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            footData.grounded = true;
            // Use gravity-relative height instead of world Y
            footData.groundHeight = Vector3.Dot(hit.point, upDir);
            footData.groundNormal = hit.normal;
        }

        // Smooth the ground normal
        footData.smoothedNormal = Vector3.SmoothDamp(
            footData.smoothedNormal,
            footData.groundNormal,
            ref footData.normalVelocity,
            footAlignmentSmoothTime,
            Mathf.Infinity,
            deltaTime
        );
    }

    /// <summary>
    /// Calculate target ankle position with hysteresis-based step arc detection.
    ///
    /// CHALLENGE 3 FIX (Idle Hop Fix):
    /// Minor animation jitter at standstill can cause verticalVelocity to exceed
    /// swingVelocityThreshold, making feet "hop" while idling. We fix this with:
    ///
    /// 1. GROUNDEDNESS TIMER: Foot must be detected as "lifting" for > 0.05s
    ///    before step arc activates. Brief jitter spikes are filtered out.
    ///
    /// 2. MINIMUM MOVE SPEED: Horizontal velocity must exceed minimumMoveSpeed
    ///    threshold. Idle animation drift won't trigger step arc.
    ///
    /// 3. HYSTERESIS: Once in swing phase, exit thresholds are lower (multiplied
    ///    by swingExitMultiplier) to prevent oscillation at boundary conditions.
    ///
    /// This creates a "sticky" swing state that requires intentional movement
    /// to enter and clear velocity drop to exit.
    /// </summary>
    private void CalculateFootTarget(Transform ankle, Transform toesEnd, ref FootIKData footData,
                                      float normalizedTime, float phaseOffset, float deltaTime)
    {
        if (!footData.grounded || ankle == null)
        {
            footData.dropNeeded = 0f;
            // Reset swing state when foot loses ground contact
            footData.ungroundedTime = 0f;
            footData.isInSwingPhase = false;
            return;
        }

        // Cache gravity direction for this frame
        Vector3 upDir = GravityUp;

        Vector3 toesPosition = (toesEnd != null) ? toesEnd.position : ankle.position;
        // Use gravity-relative height instead of world Y
        float currentToesHeight = Vector3.Dot(toesPosition, upDir);
        float targetToesHeight = footData.groundHeight + groundOffset;

        // Calculate foot velocity for improved step arc detection (gravity-aware)
        float arcOffset = 0f;
        if (useStepArc && deltaTime > 0.0001f)
        {
            Vector3 currentPos = toesPosition;
            Vector3 velocity = (currentPos - footData.previousPosition) / deltaTime;
            // Use gravity-relative vertical velocity instead of world Y
            footData.verticalVelocity = Vector3.Dot(velocity, upDir);
            footData.previousPosition = currentPos;

            // Calculate horizontal speed perpendicular to gravity
            Vector3 horizontalVelocity = velocity - upDir * footData.verticalVelocity;
            float horizontalSpeed = horizontalVelocity.magnitude;

            // ═══════════════════════════════════════════════════════════════════════
            // HYSTERESIS STEP-ARC DETECTION (Challenge 3: Idle Hop Fix)
            //
            // Entry conditions (must ALL be met to START swinging):
            // 1. Vertical velocity > threshold (foot lifting)
            // 2. Ungrounded time > timer threshold (sustained lift, not jitter)
            // 3. Horizontal speed > minimum move speed (character actually moving)
            //
            // Exit conditions (must ALL be met to STOP swinging):
            // 1. Vertical velocity < threshold * exitMultiplier (lower threshold)
            // 2. Horizontal speed < minimum move speed * exitMultiplier
            //
            // This hysteresis prevents rapid toggling at threshold boundaries.
            // ═══════════════════════════════════════════════════════════════════════

            // Track how long foot has been "lifting" (velocity indicates upward movement)
            bool isLiftingNow = footData.verticalVelocity > swingVelocityThreshold;

            if (isLiftingNow)
            {
                footData.ungroundedTime += deltaTime;
            }
            else
            {
                // Decay ungrounded time when foot is not lifting
                // This provides some persistence but eventually resets
                footData.ungroundedTime = Mathf.Max(0f, footData.ungroundedTime - deltaTime * 2f);
            }

            // Determine swing state with hysteresis
            if (!footData.isInSwingPhase)
            {
                // ═══════════════════════════════════════════════════════════════
                // ENTRY CONDITIONS (stricter - prevents false positives)
                // All conditions must be met to enter swing phase
                // ═══════════════════════════════════════════════════════════════
                bool hasEnoughLiftTime = footData.ungroundedTime > ungroundedTimeThreshold;
                bool hasEnoughVerticalVelocity = footData.verticalVelocity > swingVelocityThreshold;
                bool hasEnoughHorizontalSpeed = horizontalSpeed > minimumMoveSpeed;

                if (hasEnoughLiftTime && hasEnoughVerticalVelocity && hasEnoughHorizontalSpeed)
                {
                    footData.isInSwingPhase = true;
                }
            }
            else
            {
                // ═══════════════════════════════════════════════════════════════
                // EXIT CONDITIONS (looser - prevents oscillation)
                // Lower thresholds for exiting swing to create hysteresis band
                // ═══════════════════════════════════════════════════════════════
                float exitVerticalThreshold = swingVelocityThreshold * swingExitMultiplier;
                float exitHorizontalThreshold = minimumMoveSpeed * swingExitMultiplier;

                bool belowVerticalExit = footData.verticalVelocity < exitVerticalThreshold;
                bool belowHorizontalExit = horizontalSpeed < exitHorizontalThreshold;

                if (belowVerticalExit && belowHorizontalExit)
                {
                    footData.isInSwingPhase = false;
                    footData.ungroundedTime = 0f; // Reset timer on exit
                }
            }

            // Apply arc only when in confirmed swing phase
            if (footData.isInSwingPhase)
            {
                // Use normalized time for arc shape when velocity indicates swing
                float stepProgress = (normalizedTime + phaseOffset) % 1f;
                if (stepProgress < 0.5f)
                {
                    float arcProgress = stepProgress * 2f;
                    arcOffset = Mathf.Sin(arcProgress * Mathf.PI) * stepHeight;
                }
            }
        }

        // How much the foot needs to drop (for pelvis calculation) - gravity-relative
        footData.dropNeeded = currentToesHeight - targetToesHeight - arcOffset;

        // Calculate ankle-to-toes offset along gravity axis
        float ankleToToesOffset = Vector3.Dot(ankle.position - toesPosition, upDir);

        // Calculate world-space ankle target position using gravity-relative offset
        // Project current ankle position onto the plane perpendicular to gravity,
        // then add the correct height along gravity axis
        Vector3 ankleOnPlane = ankle.position - upDir * Vector3.Dot(ankle.position, upDir);
        float targetHeight = targetToesHeight + arcOffset + ankleToToesOffset;
        footData.targetAnklePosition = ankleOnPlane + upDir * targetHeight;
    }

    /// <summary>
    /// Displace pelvis based on ground detection with velocity-aware smoothing.
    /// Now gravity-aware for wall-walking support.
    ///
    /// CHALLENGE 1 FIX (Elevator Problem):
    /// When the character is on a rapidly moving platform, the pelvis must snap instantly
    /// to prevent feet from sinking through the floor. We detect this by monitoring:
    /// 1. Character's velocity along gravity axis (computed from root world position changes)
    /// 2. Rate of change of the pelvis target offset
    ///
    /// If either exceeds threshold, we bypass SmoothDamp and snap directly.
    /// All calculations use root-space to avoid floating-point precision issues.
    /// </summary>
    private void DisplacePelvis(float deltaTime)
    {
        if (root == null)
            return;

        // Cache gravity direction for this frame
        Vector3 upDir = GravityUp;

        // ═══════════════════════════════════════════════════════════════════════
        // VELOCITY TRACKING (Challenge 1: Elevator Fix)
        // Track character's velocity along gravity axis using root world position.
        // This detects platform movement independent of animation.
        // ═══════════════════════════════════════════════════════════════════════
        if (deltaTime > 0.0001f)
        {
            Vector3 currentRootWorldPos = root.position;
            // Use gravity-relative velocity instead of world Y
            Vector3 posDelta = currentRootWorldPos - previousRootWorldPosition;
            characterVerticalVelocity = Vector3.Dot(posDelta, upDir) / deltaTime;
            previousRootWorldPosition = currentRootWorldPos;
        }

        // AIRBORNE DETECTION: If neither foot is grounded, rapidly reset pelvis
        bool isAirborne = !leftFoot.grounded && !rightFoot.grounded;

        if (isAirborne)
        {
            currentPelvisOffset = Mathf.MoveTowards(
                currentPelvisOffset,
                0f,
                airborneResetSpeed * deltaTime
            );
            // Use local up direction for pelvis offset (root's local space)
            root.localPosition = originalRootLocalPos + Vector3.up * currentPelvisOffset;
            isOnLedge = false;
            previousPelvisTarget = 0f;
            return;
        }

        // Check for ledge scenario
        isOnLedge = false;
        float targetOffset = 0f;

        if (leftFoot.grounded && rightFoot.grounded)
        {
            float heightDifference = Mathf.Abs(leftFoot.groundHeight - rightFoot.groundHeight);

            if (heightDifference >= ledgeThreshold)
            {
                // LEDGE MODE: Position based on LOWER ground (gravity-relative)
                isOnLedge = true;
                leftFootIsLower = leftFoot.groundHeight < rightFoot.groundHeight;

                float lowerGroundHeight = leftFootIsLower ? leftFoot.groundHeight : rightFoot.groundHeight;
                Transform lowerAnkle = leftFootIsLower ? leftAnkle : rightAnkle;
                Transform lowerToesEnd = leftFootIsLower ? leftToesEnd : rightToesEnd;

                Vector3 toesPos = (lowerToesEnd != null) ? lowerToesEnd.position : lowerAnkle.position;
                // Use gravity-relative offset
                float rootToToesOffset = Vector3.Dot(root.position - toesPos, upDir);

                float targetRootHeight = (lowerGroundHeight + groundOffset) + rootToToesOffset;
                float currentRootHeight = Vector3.Dot(originalRootLocalPos, Vector3.up) + Vector3.Dot(root.parent.position, upDir);
                targetOffset = targetRootHeight - currentRootHeight;
                targetOffset -= lowerLegBendAllowance;
            }
        }

        // Normal mode (not on ledge)
        if (!isOnLedge)
        {
            float maxDrop = 0f;

            // Only count a foot if its drop exceeds the minimum threshold.
            // This filters blend-tree micro-variation during slow movement that
            // would otherwise asymmetrically tilt the pelvis when inching forwards.
            if (leftFoot.grounded && leftFoot.dropNeeded > minPelvisDropThreshold)
            {
                maxDrop = leftFoot.dropNeeded;
            }

            if (rightFoot.grounded && rightFoot.dropNeeded > minPelvisDropThreshold)
            {
                if (rightFoot.dropNeeded > maxDrop)
                    maxDrop = rightFoot.dropNeeded;
            }

            targetOffset = -maxDrop;
        }

        // Clamp target
        targetOffset = Mathf.Clamp(targetOffset, -maxPelvisOffset, maxPelvisOffset);

        // ═══════════════════════════════════════════════════════════════════════
        // VELOCITY-AWARE SMOOTHING (Challenge 1: Elevator Fix)
        // Determine if we should snap or smooth based on velocity conditions.
        //
        // Snap conditions (bypass smoothing):
        // 1. Character vertical velocity exceeds threshold (platform moving fast)
        // 2. Pelvis target delta rate exceeds threshold (ground changed rapidly)
        //
        // This ensures feet maintain ground contact on elevators/lifts.
        // ═══════════════════════════════════════════════════════════════════════
        float targetDeltaRate = 0f;
        if (deltaTime > 0.0001f)
        {
            targetDeltaRate = Mathf.Abs(targetOffset - previousPelvisTarget) / deltaTime;
        }
        previousPelvisTarget = targetOffset;

        // Check if we need to snap (high velocity or rapid target change)
        bool shouldSnap = Mathf.Abs(characterVerticalVelocity) > pelvisSnapVelocityThreshold ||
                          targetDeltaRate > pelvisSnapDeltaRate;

        if (shouldSnap)
        {
            // SNAP: Bypass smoothing entirely for instant ground contact
            currentPelvisOffset = targetOffset;
            pelvisVelocity = 0f; // Reset velocity to prevent oscillation on resume
        }
        else
        {
            // SMOOTH: Use SmoothDamp for temporal stability during normal movement
            currentPelvisOffset = Mathf.SmoothDamp(
                currentPelvisOffset,
                targetOffset,
                ref pelvisVelocity,
                pelvisSmoothTime
            );
        }

        root.localPosition = originalRootLocalPos + Vector3.up * currentPelvisOffset;
    }

    /// <summary>
    /// Apply two-bone IK with soft distance falloff.
    /// </summary>
    private void ApplyLegIK(Transform hip, Transform knee, Transform ankle, Transform toesEnd,
                            float upperLen, float lowerLen, FootIKData footData, bool isLeftLeg = true)
    {
        if (hip == null || knee == null || ankle == null)
            return;

        Vector3 targetAnklePos = footData.targetAnklePosition;
        SolveTwoBoneIK(hip, knee, ankle, targetAnklePos, upperLen, lowerLen, ikWeight, isLeftLeg);
    }

    /// <summary>
    /// Two-bone IK solver with soft IK and adaptive pole vector.
    ///
    /// CHALLENGE 2 FIX (Sumo/Monster Knee Fix):
    /// Static forward pole vectors cause "pigeon-toed" knees on characters with wide
    /// leg stances or during deep crouches. We fix this by:
    /// 1. Computing the leg's natural plane using cross product of hip-to-target × up
    /// 2. Blending between character forward and hip's local outward direction
    /// 3. This ensures knees point naturally outward as legs spread apart
    ///
    /// The pole direction adapts dynamically based on leg geometry rather than
    /// relying on a static transform.forward reference.
    /// </summary>
    private void SolveTwoBoneIK(Transform hip, Transform knee, Transform ankle,
                                 Vector3 targetPos, float upperLen, float lowerLen, float weight,
                                 bool isLeftLeg = true)
    {
        if (weight < 0.01f)
            return;

        Vector3 hipPos = hip.position;
        Vector3 toTarget = targetPos - hipPos;
        float targetDistance = toTarget.magnitude;

        // Calculate reach limits
        float maxReach = upperLen + lowerLen - 0.001f;
        float minReach = Mathf.Abs(upperLen - lowerLen) + 0.001f;

        // Apply SOFT IK instead of hard clamp
        float softDistance = ApplySoftIK(targetDistance, maxReach, softIKRatio);
        softDistance = Mathf.Max(softDistance, minReach);

        Vector3 targetDirection = toTarget.normalized;
        Vector3 clampedTarget = hipPos + targetDirection * softDistance;

        // Law of cosines for hip angle
        float cosHipAngle = (upperLen * upperLen + softDistance * softDistance - lowerLen * lowerLen)
                            / (2f * upperLen * softDistance);
        cosHipAngle = Mathf.Clamp(cosHipAngle, -1f, 1f);
        float hipAngle = Mathf.Acos(cosHipAngle);

        // ═══════════════════════════════════════════════════════════════════════
        // ADAPTIVE POLE VECTOR (Challenge 2: Wide Stance Fix)
        //
        // Instead of using a static transform.forward, we compute the pole direction
        // dynamically based on the leg's geometry:
        //
        // 1. Compute leg plane normal: Cross(hipToTarget, worldUp)
        //    This gives us the natural outward direction for the knee
        //
        // 2. Get hip's local outward: Transform the hip's local X-axis to world space
        //    Left leg: local +X points outward (left)
        //    Right leg: local -X points outward (right)
        //
        // 3. Blend between character forward and hip outward using hipForwardBlend
        //    0 = pure character forward (original behavior)
        //    1 = pure hip outward (maximum knee spread)
        // ═══════════════════════════════════════════════════════════════════════
        Vector3 poleDirection = CalculateAdaptivePoleVector(hip, targetDirection, isLeftLeg);

        // Determine bend axis perpendicular to both target direction and pole
        Vector3 bendAxis = Vector3.Cross(targetDirection, poleDirection);

        if (bendAxis.sqrMagnitude < 0.001f)
        {
            // Target is parallel to pole direction, use character's right as fallback
            // Flip for left/right leg to maintain outward knee direction
            bendAxis = isLeftLeg ? -transform.right : transform.right;
        }

        bendAxis.Normalize();

        // Calculate knee position
        Vector3 hipToKneeDir = Quaternion.AngleAxis(hipAngle * Mathf.Rad2Deg, bendAxis) * targetDirection;
        Vector3 newKneePos = hipPos + hipToKneeDir * upperLen;

        // Apply hip rotation
        Vector3 currentHipToKnee = knee.position - hipPos;
        Vector3 desiredHipToKnee = newKneePos - hipPos;

        if (currentHipToKnee.sqrMagnitude > 0.001f && desiredHipToKnee.sqrMagnitude > 0.001f)
        {
            Quaternion hipRotDelta = Quaternion.FromToRotation(currentHipToKnee.normalized, desiredHipToKnee.normalized);
            hip.rotation = Quaternion.Slerp(hip.rotation, hipRotDelta * hip.rotation, weight);
        }

        // Apply knee rotation
        Vector3 currentKneeToAnkle = ankle.position - knee.position;
        Vector3 desiredKneeToAnkle = clampedTarget - knee.position;

        if (currentKneeToAnkle.sqrMagnitude > 0.001f && desiredKneeToAnkle.sqrMagnitude > 0.001f)
        {
            Quaternion kneeRotDelta = Quaternion.FromToRotation(currentKneeToAnkle.normalized, desiredKneeToAnkle.normalized);
            knee.rotation = Quaternion.Slerp(knee.rotation, kneeRotDelta * knee.rotation, weight);
        }
    }

    /// <summary>
    /// Calculate adaptive pole vector for natural knee orientation.
    /// Now gravity-aware for wall-walking support.
    ///
    /// This method computes a pole direction that adapts to the leg's current geometry,
    /// preventing pigeon-toed artifacts on wide stances or deep crouches.
    ///
    /// The calculation blends between:
    /// - Character's forward direction (maintains forward-facing knees during normal walking)
    /// - Hip's local outward direction (pushes knees outward as legs spread)
    /// </summary>
    /// <param name="hip">The hip transform</param>
    /// <param name="hipToTargetDir">Normalized direction from hip to IK target</param>
    /// <param name="isLeftLeg">True for left leg, false for right</param>
    /// <returns>Normalized pole direction vector</returns>
    private Vector3 CalculateAdaptivePoleVector(Transform hip, Vector3 hipToTargetDir, bool isLeftLeg)
    {
        Vector3 characterForward = transform.forward;

        // ═══════════════════════════════════════════════════════════════════════
        // STEP 1: Compute leg plane normal using cross product
        // Cross(hipToTarget, gravityUp) gives us the natural lateral direction
        // Now gravity-aware for wall-walking support
        // ═══════════════════════════════════════════════════════════════════════
        Vector3 legPlaneNormal = Vector3.Cross(hipToTargetDir, GravityUp);

        // Flip for right leg so both legs point outward
        if (!isLeftLeg)
        {
            legPlaneNormal = -legPlaneNormal;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // STEP 2: Get hip's local outward direction
        // This represents the "natural" knee orientation from the bind pose
        // Left leg: local +X typically points outward (lateral)
        // Right leg: local -X typically points outward (lateral)
        // ═══════════════════════════════════════════════════════════════════════
        Vector3 hipOutward;
        if (isLeftLeg)
        {
            // Left leg: local +X points left (outward)
            hipOutward = hip.right;
        }
        else
        {
            // Right leg: local -X points right (outward)
            hipOutward = -hip.right;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // STEP 3: Blend between character forward and hip outward
        // hipForwardBlend = 0: pure character forward (original behavior)
        // hipForwardBlend = 1: pure hip outward (maximum knee spread)
        //
        // The leg plane normal provides additional context - if the leg is spread
        // wide, the normal will be more lateral, naturally pushing knees outward
        // ═══════════════════════════════════════════════════════════════════════
        Vector3 blendedDirection = Vector3.Lerp(characterForward, hipOutward, hipForwardBlend);

        // Add influence from leg plane normal for dynamic adaptation
        // This ensures knees track outward as legs spread during crouches
        if (legPlaneNormal.sqrMagnitude > 0.001f)
        {
            legPlaneNormal.Normalize();
            // Use leg plane as additional outward hint (scaled by blend factor)
            blendedDirection = Vector3.Lerp(blendedDirection, legPlaneNormal, hipForwardBlend * 0.5f);
        }

        // Ensure we have a valid direction
        if (blendedDirection.sqrMagnitude < 0.001f)
        {
            return characterForward;
        }

        return blendedDirection.normalized;
    }

    /// <summary>
    /// Soft IK distance calculation using exponential asymptotic approach.
    /// Prevents hard snapping when target is at maximum reach.
    ///
    /// Formula: softDistance = da + (maxReach - da) * (1 - e^(-x / (maxReach - da)))
    /// Where: da = maxReach * softRatio (soft threshold)
    ///        x = distance - da (overshoot beyond soft threshold)
    /// </summary>
    private float ApplySoftIK(float distance, float maxReach, float softRatio)
    {
        float da = maxReach * softRatio; // Soft distance threshold

        if (distance <= da)
            return distance; // No modification needed below threshold

        // Exponential asymptotic approach to maxReach
        float softRange = maxReach - da;
        float x = distance - da;
        float softDistance = da + softRange * (1f - Mathf.Exp(-x / softRange));

        return softDistance;
    }

    /// <summary>
    /// Align foot rotation to match ground slope using smoothed normal.
    /// Uses ankle→toesEnd vector to determine actual foot forward direction.
    /// </summary>
    private void AlignFootToGround(Transform ankle, Transform toesEnd, ref FootIKData footData, float deltaTime)
    {
        if (!footData.grounded || ankle == null || footAlignmentWeight < 0.01f)
            return;

        // Get the smoothed ground normal
        Vector3 targetUp = footData.smoothedNormal;

        // Use ankle→toesEnd vector as the actual foot forward direction
        Vector3 footForward;
        if (toesEnd != null)
        {
            footForward = (toesEnd.position - ankle.position).normalized;
        }
        else
        {
            // Fallback to character forward if no toe bone
            footForward = transform.forward;
        }

        // Project foot forward onto the ground plane to get slope-aligned direction
        footForward = Vector3.ProjectOnPlane(footForward, targetUp);

        if (footForward.sqrMagnitude < 0.001f)
        {
            // Fallback if foot forward is parallel to normal (foot pointing straight up/down)
            footForward = Vector3.ProjectOnPlane(transform.forward, targetUp);
            if (footForward.sqrMagnitude < 0.001f)
                footForward = Vector3.forward;
        }

        footForward.Normalize();

        // Create target rotation that aligns foot to ground slope
        Quaternion targetRotation = Quaternion.LookRotation(footForward, targetUp);

        // Apply weighted blend
        ankle.rotation = Quaternion.Slerp(ankle.rotation, targetRotation, footAlignmentWeight * ikWeight);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // TURN-IN-PLACE (TIP) SYSTEM - Industrial Standard Implementation
    //
    // Flow:
    // 1. VELOCITY CHECK: Only active when velocity < threshold
    // 2. SPINE-LEAD TWIST BUFFER: Upper body rotates freely, feet stay locked
    // 3. THRESHOLD CHECK: When twist > 40°, trigger stepping
    // 4. INSIDE-OUT STAGGERED STEPPING: Inside foot first, outside at 50%
    // 5. FOOT LOCKING: Non-stepping foot strictly locked to world space
    // 6. WEIGHT SHIFT: Pelvis shifts over stationary foot
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Main TIP update. Handles state transitions and delegates to appropriate handlers.
    /// </summary>
    private void UpdateTurnInPlace(float deltaTime)
    {
        if (deltaTime < 0.0001f) return;

        // After ResetTIP() is called (e.g. on idle entry), block re-activation until the
        // idle blend tree has had time to settle. Without this, TIP immediately re-enters
        // the next frame (velocity=0, input=0 → isStationary=true → EnterTwistBuffer),
        // locking feet at the mid-stride pose the animator hasn't finished blending away from.
        if (tipReactivationCooldown > 0f)
        {
            tipReactivationCooldown -= deltaTime;
            BlendOutTIP(deltaTime);
            return;
        }

        // Get character velocity (horizontal)
        float characterVelocity = CalculateCharacterVelocity(deltaTime);

        // Update debug values — only call ToString when state actually changes (avoids string alloc)
        debugVelocity = characterVelocity;
        if (tipState != _lastDebugTipState)
        {
            debugTIPState = tipState.ToString();
            _lastDebugTipState = tipState;
        }
        debugTIPBlendWeight = tipBlendWeight;
        debugTwist = accumulatedTwist;

        // ═══════════════════════════════════════════════════════════════════════
        // STATIONARY CHECK: input is the primary signal (same-frame response),
        // velocity is a secondary guard for external forces.
        // ═══════════════════════════════════════════════════════════════════════

        // Input responds the same frame the key is released — no physics lag.
        bool inputIsZero = playerController == null
            || playerController.InputData.IsMovePaused
            || playerController.moveAction == null
            || playerController.moveAction.ReadValue<Vector2>().sqrMagnitude < 0.01f;
        bool isStationary = inputIsZero && (characterVelocity < tipVelocityThreshold);

        // Exit threshold is 3x entry so TIP stepping-induced velocity spikes
        // do not self-cancel the system mid-step.
        bool shouldExitTIP = !inputIsZero || (characterVelocity > tipVelocityThreshold * 3f);

        if (debugTIP)
        {
            Debug.Log($"[TIP] vel={characterVelocity:F3} stationary={isStationary} inputZero={inputIsZero} state={tipState} blend={tipBlendWeight:F2} twist={accumulatedTwist:F1}° grounded=L:{leftFoot.grounded}/R:{rightFoot.grounded}");
        }

        if (shouldExitTIP)
        {
            // Blend out of TIP and return to normal IK
            BlendOutTIP(deltaTime);
            return;
        }

        // Blend into TIP mode (slower blend-in for smoother activation)
        tipBlendWeight = Mathf.MoveTowards(tipBlendWeight, 1f, tipBlendOutSpeed * 0.5f * deltaTime);

        // Must have both feet grounded for TIP
        if (!leftFoot.grounded || !rightFoot.grounded)
        {
            BlendOutTIP(deltaTime);
            return;
        }

        // NOTE: targetHeadingYaw is updated in UpdateTwistBuffer from movement controller
        // Don't read from transform.eulerAngles.y here - we're preventing rotation during TIP

        // ═══════════════════════════════════════════════════════════════════════
        // TIP STATE MACHINE
        // ═══════════════════════════════════════════════════════════════════════
        switch (tipState)
        {
            case TIPState.Inactive:
                EnterTwistBuffer();
                break;

            case TIPState.TwistBuffer:
                UpdateTwistBuffer(deltaTime);
                break;

            case TIPState.InsideFootStepping:
                UpdateInsideFootStep(deltaTime);
                break;

            case TIPState.OutsideFootStepping:
                UpdateOutsideFootStep(deltaTime);
                break;

            case TIPState.Settling:
                UpdateSettling(deltaTime);
                break;
        }

        // Update weight shift
        UpdateTIPWeightShift(deltaTime);

        // Apply spine twist distribution (gradual twist from spine to neck)
        ApplySpineTwist(deltaTime);

        // Apply head look-at toward camera direction
        if (enableHeadLookAt)
        {
            ApplyHeadLookAt(deltaTime);
        }
    }

    /// <summary>
    /// Injects TIP-calculated foot targets into the regular FootIKData.
    /// This replaces the normal IK targets so only ONE IK pass runs.
    /// Called BEFORE pelvis displacement and IK solving.
    /// </summary>
    private void InjectTIPTargets()
    {
        Vector3 upDir = GravityUp;

        // LEFT FOOT
        if (tipLeftFoot.isStepping)
        {
            float alpha = tipLeftFoot.stepAlpha;
            Vector3 targetPos = InterpolateStepPosition(
                tipLeftFoot.stepStartPosition,
                tipLeftFoot.stepTargetPosition,
                alpha, upDir
            );
            // Blend between normal IK target and TIP target
            leftFoot.targetAnklePosition = Vector3.Lerp(
                leftFoot.targetAnklePosition,
                targetPos,
                tipBlendWeight
            );
        }
        else if (tipLeftFoot.isLocked)
        {
            leftFoot.targetAnklePosition = Vector3.Lerp(
                leftFoot.targetAnklePosition,
                tipLeftFoot.lockedWorldPosition,
                tipBlendWeight
            );
        }

        // RIGHT FOOT
        if (tipRightFoot.isStepping)
        {
            float alpha = tipRightFoot.stepAlpha;
            Vector3 targetPos = InterpolateStepPosition(
                tipRightFoot.stepStartPosition,
                tipRightFoot.stepTargetPosition,
                alpha, upDir
            );
            rightFoot.targetAnklePosition = Vector3.Lerp(
                rightFoot.targetAnklePosition,
                targetPos,
                tipBlendWeight
            );
        }
        else if (tipRightFoot.isLocked)
        {
            rightFoot.targetAnklePosition = Vector3.Lerp(
                rightFoot.targetAnklePosition,
                tipRightFoot.lockedWorldPosition,
                tipBlendWeight
            );
        }
    }

    // Cached position for TIP velocity calculation
    private Vector3 previousTIPPosition;
    private bool tipPositionInitialized = false;

    /// <summary>
    /// Calculates character's horizontal velocity magnitude.
    /// Uses consistent position tracking (model transform, not pelvis bone).
    /// </summary>
    private float CalculateCharacterVelocity(float deltaTime)
    {
        if (deltaTime < 0.0001f) return 0f;

        Vector3 currentPos = transform.position;

        // Initialize on first call
        if (!tipPositionInitialized)
        {
            previousTIPPosition = currentPos;
            tipPositionInitialized = true;
            return 0f;
        }

        Vector3 upDir = GravityUp;
        Vector3 velocity = (currentPos - previousTIPPosition) / deltaTime;
        previousTIPPosition = currentPos;

        // Remove vertical component
        velocity = velocity - upDir * Vector3.Dot(velocity, upDir);
        return velocity.magnitude;
    }

    /// <summary>
    /// Smoothly blends out of TIP mode when character starts moving.
    /// </summary>
    private void BlendOutTIP(float deltaTime)
    {
        tipBlendWeight = Mathf.MoveTowards(tipBlendWeight, 0f, tipBlendOutSpeed * deltaTime);

        if (tipBlendWeight < 0.01f)
        {
            tipBlendWeight = 0f;
            tipState = TIPState.Inactive;

            // Reset foot locks
            tipLeftFoot.isLocked = false;
            tipLeftFoot.isStepping = false;
            tipRightFoot.isLocked = false;
            tipRightFoot.isStepping = false;
        }
    }

    /// <summary>
    /// Enters the Twist Buffer state - feet locked, accumulating twist.
    /// </summary>
    private void EnterTwistBuffer()
    {
        tipState = TIPState.TwistBuffer;
        // Use local Y rotation to match BasePlayerController's coordinate system
        // This ensures consistency when gravity rotates the player root
        rootHeadingYaw = transform.localEulerAngles.y;
        currentRotatingYaw = rootHeadingYaw;
        accumulatedTwist = 0f;

        // Reset head base cache for new turn (will re-cache when twist builds)
        headBaseCached = false;

        // Lock both feet at current world positions
        LockFoot(ref tipLeftFoot, leftAnkle);
        LockFoot(ref tipRightFoot, rightAnkle);
    }

    /// <summary>
    /// Locks a foot at its current world-space position.
    /// </summary>
    private void LockFoot(ref TIPFootData foot, Transform ankle)
    {
        if (ankle == null) return;

        foot.isLocked = true;
        foot.isStepping = false;
        foot.stepAlpha = 0f;
        foot.lockedWorldPosition = ankle.position;
        foot.lockedWorldRotation = ankle.rotation;
    }

    /// <summary>
    /// Updates the Twist Buffer state.
    /// Accumulates twist angle and triggers stepping when threshold exceeded.
    /// </summary>
    private void UpdateTwistBuffer(float deltaTime)
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Get TARGET heading from BasePlayerController's POV (where player WANTS to face)
        // This is the camera heading from Cinemachine POV component.
        // rootHeadingYaw is where feet are currently pointing (locked on TIP entry)
        // ═══════════════════════════════════════════════════════════════════════
        float desiredYaw = playerController != null
            ? playerController.TargetYawRotation
            : transform.eulerAngles.y;

        // Calculate twist between desired heading and current root heading
        accumulatedTwist = Mathf.DeltaAngle(rootHeadingYaw, desiredYaw);

        // Update target heading for step calculations
        targetHeadingYaw = desiredYaw;

        // ═══════════════════════════════════════════════════════════════════════
        // THRESHOLD CHECK: Trigger stepping when twist exceeds threshold
        // ═══════════════════════════════════════════════════════════════════════
        if (Mathf.Abs(accumulatedTwist) > twistThreshold)
        {
            // Determine turn direction using dot product
            // DotProduct(ActorRightVector, TargetDirection) > 0 means turning right
            Vector3 currentForward = Quaternion.Euler(0f, rootHeadingYaw, 0f) * Vector3.forward;
            Vector3 currentRight = Quaternion.Euler(0f, rootHeadingYaw, 0f) * Vector3.right;
            Vector3 targetDirection = Quaternion.Euler(0f, targetHeadingYaw, 0f) * Vector3.forward;
            float turnDot = Vector3.Dot(currentRight, targetDirection);
            turnDirection = turnDot > 0f ? 1 : -1; // +1 = right, -1 = left

            // ═══════════════════════════════════════════════════════════════════
            // INSIDE FOOT: The foot in the direction of the turn
            // When turning RIGHT, RIGHT foot is inside
            // When turning LEFT, LEFT foot is inside
            // ═══════════════════════════════════════════════════════════════════
            isInsideFootLeft = (turnDirection < 0);

            // Start inside foot stepping
            StartInsideFootStep();
        }
    }

    /// <summary>
    /// Initiates the inside foot step.
    /// Pre-calculates FINAL foot positions based on targetHeadingYaw.
    /// Begins root rotation interpolation (AAA standard: rotate DURING stepping).
    /// </summary>
    private void StartInsideFootStep()
    {
        tipState = TIPState.InsideFootStepping;

        // ═══════════════════════════════════════════════════════════════════════
        // AAA STANDARD: Cache starting yaw for smooth interpolation DURING stepping
        // Root rotation happens gradually as feet move, not snapping at the end
        // ═══════════════════════════════════════════════════════════════════════
        steppingStartYaw = rootHeadingYaw;
        currentRotatingYaw = rootHeadingYaw;

        ref TIPFootData insideFoot = ref (isInsideFootLeft ? ref tipLeftFoot : ref tipRightFoot);
        Transform insideAnkle = isInsideFootLeft ? leftAnkle : rightAnkle;

        if (insideAnkle == null) return;

        // ═══════════════════════════════════════════════════════════════════════
        // PRE-CALCULATE: Determine where BOTH feet will end up at turn completion
        // This is calculated ONCE at step start, based on final target heading
        // ═══════════════════════════════════════════════════════════════════════
        PreCalculateFinalFootPositions();

        // Setup step data
        insideFoot.isStepping = true;
        insideFoot.isLocked = false;
        insideFoot.stepAlpha = 0f;
        insideFoot.stepStartPosition = insideFoot.lockedWorldPosition;
        insideFoot.stepStartRotation = insideFoot.lockedWorldRotation;

        // Use pre-calculated final position (not dynamic IK target)
        insideFoot.stepTargetPosition = isInsideFootLeft ? leftFootFinalPosition : rightFootFinalPosition;
        insideFoot.stepTargetRotation = isInsideFootLeft ? leftFootFinalRotation : rightFootFinalRotation;
    }

    /// <summary>
    /// Pre-calculates where both feet WILL BE when root reaches targetHeadingYaw.
    /// Uses current root position + target heading rotation to find final foot positions.
    /// Ground height is determined via raycast at the final positions.
    /// </summary>
    private void PreCalculateFinalFootPositions()
    {
        if (root == null) return;

        Vector3 upDir = GravityUp;
        Vector3 rootPos = root.position;

        // Calculate the yaw delta from current to target
        float yawDelta = Mathf.DeltaAngle(rootHeadingYaw, targetHeadingYaw);
        Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, upDir);

        // LEFT FOOT: Calculate where it should end up
        if (leftAnkle != null)
        {
            // Current offset from root to foot (in world space)
            Vector3 leftOffset = tipLeftFoot.lockedWorldPosition - rootPos;

            // Rotate offset by yaw delta to get final position relative to root
            Vector3 rotatedOffset = yawRotation * leftOffset;
            leftFootFinalPosition = rootPos + rotatedOffset;

            // Raycast to find ground height at final position
            RaycastHit hit;
            Vector3 rayOrigin = leftFootFinalPosition + upDir * raycastOriginHeight;
            if (Physics.Raycast(rayOrigin, -upDir, out hit, raycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                float groundHeight = Vector3.Dot(hit.point, upDir);
                float ankleOffset = leftToesEnd != null
                    ? Vector3.Dot(leftAnkle.position - leftToesEnd.position, upDir)
                    : 0.08f;
                float ankleHeight = groundHeight + groundOffset + ankleOffset;

                // Adjust final position to correct ground height
                leftFootFinalPosition = leftFootFinalPosition
                    - upDir * Vector3.Dot(leftFootFinalPosition, upDir)
                    + upDir * ankleHeight;
            }

            // Calculate final rotation (preserve pitch/roll, apply yaw)
            leftFootFinalRotation = yawRotation * tipLeftFoot.lockedWorldRotation;
        }

        // RIGHT FOOT: Same calculation
        if (rightAnkle != null)
        {
            Vector3 rightOffset = tipRightFoot.lockedWorldPosition - rootPos;
            Vector3 rotatedOffset = yawRotation * rightOffset;
            rightFootFinalPosition = rootPos + rotatedOffset;

            RaycastHit hit;
            Vector3 rayOrigin = rightFootFinalPosition + upDir * raycastOriginHeight;
            if (Physics.Raycast(rayOrigin, -upDir, out hit, raycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                float groundHeight = Vector3.Dot(hit.point, upDir);
                float ankleOffset = rightToesEnd != null
                    ? Vector3.Dot(rightAnkle.position - rightToesEnd.position, upDir)
                    : 0.08f;
                float ankleHeight = groundHeight + groundOffset + ankleOffset;

                rightFootFinalPosition = rightFootFinalPosition
                    - upDir * Vector3.Dot(rightFootFinalPosition, upDir)
                    + upDir * ankleHeight;
            }

            rightFootFinalRotation = yawRotation * tipRightFoot.lockedWorldRotation;
        }
    }

    /// <summary>
    /// Calculates the target position and rotation for a stepping foot.
    /// Uses yaw-only rotation to preserve natural foot orientation and prevent deformation.
    /// </summary>
    private void CalculateStepTarget(ref TIPFootData foot, Transform ankle, bool isLeftFoot)
    {
        if (ankle == null) return;

        Vector3 upDir = GravityUp;

        // Get the IK-calculated target position (this accounts for ground height)
        ref FootIKData footIK = ref (isLeftFoot ? ref leftFoot : ref rightFoot);

        // Target position: IK target gives us proper ground placement
        foot.stepTargetPosition = footIK.targetAnklePosition;

        // ═══════════════════════════════════════════════════════════════════════
        // FOOT ROTATION FIX: Preserve natural foot orientation
        // Instead of using LookRotation (which forces foot into unnatural orientation),
        // we calculate only the YAW difference and apply it to the locked rotation.
        // This keeps the foot's natural pitch/roll alignment while rotating it
        // to face the new heading.
        // ═══════════════════════════════════════════════════════════════════════

        // Calculate yaw difference from root heading to target heading
        float yawDelta = Mathf.DeltaAngle(rootHeadingYaw, targetHeadingYaw);

        // Create a rotation that only affects yaw (around up axis)
        Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, upDir);

        // Apply yaw rotation to the locked rotation, preserving pitch/roll
        foot.stepTargetRotation = yawRotation * foot.stepStartRotation;

        // Optional: Slight alignment to ground normal for slope adaptation
        // We extract the foot's current forward and re-project it onto the ground plane
        Vector3 footForward = foot.stepTargetRotation * Vector3.forward;
        Vector3 footForwardOnGround = Vector3.ProjectOnPlane(footForward, footIK.smoothedNormal);

        if (footForwardOnGround.sqrMagnitude > 0.001f)
        {
            footForwardOnGround.Normalize();
            // Calculate how much to tilt for ground alignment (subtle, not full snap)
            Quaternion groundAlignment = Quaternion.FromToRotation(
                foot.stepTargetRotation * Vector3.up,
                footIK.smoothedNormal
            );
            // Blend only partially toward ground alignment to avoid overcorrection
            foot.stepTargetRotation = Quaternion.Slerp(
                foot.stepTargetRotation,
                groundAlignment * foot.stepTargetRotation,
                0.3f // Subtle ground alignment
            );
        }
    }

    /// <summary>
    /// Updates the inside foot during its step.
    /// Triggers outside foot at 50% completion.
    /// AAA STANDARD: Root rotation happens DURING stepping, synchronized with foot progress.
    /// </summary>
    private void UpdateInsideFootStep(float deltaTime)
    {
        ref TIPFootData insideFoot = ref (isInsideFootLeft ? ref tipLeftFoot : ref tipRightFoot);

        // Progress alpha — use cached reciprocal (avoids per-frame division)
        float alphaRate = _tipAlphaRate;
        insideFoot.stepAlpha += alphaRate * deltaTime;
        insideFoot.stepAlpha = Mathf.Clamp01(insideFoot.stepAlpha);

        // ═══════════════════════════════════════════════════════════════════════
        // AAA STANDARD: ROOT ROTATION DURING STEPPING
        // Interpolate root yaw from starting position toward target.
        // This creates smooth body rotation during the step, not snap at end.
        // ═══════════════════════════════════════════════════════════════════════
        float rotationAlpha = EaseOutCubic(insideFoot.stepAlpha);
        currentRotatingYaw = Mathf.LerpAngle(steppingStartYaw, targetHeadingYaw, rotationAlpha);

        // Update rootHeadingYaw to track current rotation progress
        rootHeadingYaw = currentRotatingYaw;

        // Sync with BasePlayerController during stepping (smooth rotation)
        if (playerController != null)
        {
            playerController.SyncRotationFromTIP(currentRotatingYaw);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // STAGGERED STEPPING: Trigger outside foot at 50%
        // ═══════════════════════════════════════════════════════════════════════
        if (insideFoot.stepAlpha >= 0.5f)
        {
            ref TIPFootData outsideFoot = ref (isInsideFootLeft ? ref tipRightFoot : ref tipLeftFoot);

            if (!outsideFoot.isStepping)
            {
                // Start outside foot using PRE-CALCULATED final position
                Transform outsideAnkle = isInsideFootLeft ? rightAnkle : leftAnkle;
                if (outsideAnkle != null)
                {
                    outsideFoot.isStepping = true;
                    outsideFoot.isLocked = false;
                    outsideFoot.stepAlpha = 0f;
                    outsideFoot.stepStartPosition = outsideFoot.lockedWorldPosition;
                    outsideFoot.stepStartRotation = outsideFoot.lockedWorldRotation;
                    // Use pre-calculated position instead of CalculateStepTarget
                    outsideFoot.stepTargetPosition = isInsideFootLeft ? rightFootFinalPosition : leftFootFinalPosition;
                    outsideFoot.stepTargetRotation = isInsideFootLeft ? rightFootFinalRotation : leftFootFinalRotation;

                    tipState = TIPState.OutsideFootStepping;
                }
            }
        }

        // Check if inside foot finished (handles edge case where outside didn't trigger)
        if (insideFoot.stepAlpha >= 1f)
        {
            CompleteFootStep(ref insideFoot);

            if (tipState == TIPState.InsideFootStepping)
            {
                // Outside foot didn't start, go to settling
                tipState = TIPState.Settling;
            }
        }
    }

    /// <summary>
    /// Updates both feet during the staggered stepping phase.
    /// AAA STANDARD: Continues root rotation interpolation using combined progress.
    /// </summary>
    private void UpdateOutsideFootStep(float deltaTime)
    {
        ref TIPFootData insideFoot = ref (isInsideFootLeft ? ref tipLeftFoot : ref tipRightFoot);
        ref TIPFootData outsideFoot = ref (isInsideFootLeft ? ref tipRightFoot : ref tipLeftFoot);

        float alphaRate = _tipAlphaRate; // cached reciprocal

        // Continue inside foot if not done
        if (insideFoot.isStepping)
        {
            insideFoot.stepAlpha += alphaRate * deltaTime;
            insideFoot.stepAlpha = Mathf.Clamp01(insideFoot.stepAlpha);

            if (insideFoot.stepAlpha >= 1f)
            {
                CompleteFootStep(ref insideFoot);
            }
        }

        // Progress outside foot
        if (outsideFoot.isStepping)
        {
            outsideFoot.stepAlpha += alphaRate * deltaTime;
            outsideFoot.stepAlpha = Mathf.Clamp01(outsideFoot.stepAlpha);

            if (outsideFoot.stepAlpha >= 1f)
            {
                CompleteFootStep(ref outsideFoot);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // AAA STANDARD: CONTINUE ROOT ROTATION
        // Use combined step progress (max of both feet) for smooth rotation
        // ═══════════════════════════════════════════════════════════════════════
        float combinedAlpha = Mathf.Max(insideFoot.stepAlpha, outsideFoot.stepAlpha);
        float rotationAlpha = EaseOutCubic(Mathf.Clamp01(combinedAlpha));
        currentRotatingYaw = Mathf.LerpAngle(steppingStartYaw, targetHeadingYaw, rotationAlpha);

        // Update rootHeadingYaw to track current rotation progress
        rootHeadingYaw = currentRotatingYaw;

        // Sync with BasePlayerController (smooth rotation continues)
        if (playerController != null)
        {
            playerController.SyncRotationFromTIP(currentRotatingYaw);
        }

        // Both feet done?
        if (!insideFoot.isStepping && !outsideFoot.isStepping)
        {
            tipState = TIPState.Settling;
        }
    }

    /// <summary>
    /// Completes a foot step, locking it at the target position.
    /// </summary>
    private void CompleteFootStep(ref TIPFootData foot)
    {
        foot.isStepping = false;
        foot.isLocked = true;
        foot.stepAlpha = 1f;
        foot.lockedWorldPosition = foot.stepTargetPosition;
        foot.lockedWorldRotation = foot.stepTargetRotation;
    }

    /// <summary>
    /// Updates the settling phase after stepping completes.
    /// AAA STANDARD: Root rotation already happened during stepping.
    /// This phase just finalizes state and resets head look-at.
    /// </summary>
    private void UpdateSettling(float deltaTime)
    {
        // ═══════════════════════════════════════════════════════════════════════
        // FINALIZE: Root rotation should already be at targetHeadingYaw
        // (interpolated during stepping phases). Just ensure consistency.
        // ═══════════════════════════════════════════════════════════════════════
        rootHeadingYaw = targetHeadingYaw;
        currentRotatingYaw = targetHeadingYaw;
        accumulatedTwist = 0f;

        // Final sync with BasePlayerController (should be nearly no-op)
        if (playerController != null)
        {
            playerController.SyncRotationFromTIP(targetHeadingYaw);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HEAD RESET: Turn is complete, head should return to neutral.
        // Setting accumulatedTwist = 0 means head target will be 0 degrees.
        // ApplyHeadLookAt will smoothly blend head back to neutral.
        // ═══════════════════════════════════════════════════════════════════════
        // (Head angles decay naturally since we're now facing target direction)

        // Update locked positions to current (correctly positioned after IK)
        if (leftAnkle != null && tipLeftFoot.isLocked)
        {
            tipLeftFoot.lockedWorldPosition = leftAnkle.position;
            tipLeftFoot.lockedWorldRotation = leftAnkle.rotation;
        }
        if (rightAnkle != null && tipRightFoot.isLocked)
        {
            tipRightFoot.lockedWorldPosition = rightAnkle.position;
            tipRightFoot.lockedWorldRotation = rightAnkle.rotation;
        }

        // Return to twist buffer to watch for more turning
        tipState = TIPState.TwistBuffer;
    }

    /// <summary>
    /// Updates pelvis weight shift during TIP.
    /// Shifts pelvis over the stationary (locked) foot.
    /// </summary>
    private void UpdateTIPWeightShift(float deltaTime)
    {
        Vector3 targetShift = Vector3.zero;
        float targetDip = 0f;

        // Determine which foot to shift toward (the planted/locked one)
        bool leftStepping = tipLeftFoot.isStepping;
        bool rightStepping = tipRightFoot.isStepping;

        if (leftStepping && !rightStepping)
        {
            // Left is stepping - shift weight to right
            float shiftAmount = CalculateWeightShiftAmount(tipLeftFoot.stepAlpha);
            targetShift = transform.right * shiftAmount * tipWeightShiftAmount;
            targetDip = Mathf.Sin(tipLeftFoot.stepAlpha * Mathf.PI) * tipPelvisDip;
        }
        else if (rightStepping && !leftStepping)
        {
            // Right is stepping - shift weight to left
            float shiftAmount = CalculateWeightShiftAmount(tipRightFoot.stepAlpha);
            targetShift = -transform.right * shiftAmount * tipWeightShiftAmount;
            targetDip = Mathf.Sin(tipRightFoot.stepAlpha * Mathf.PI) * tipPelvisDip;
        }
        else if (leftStepping && rightStepping)
        {
            // Both stepping (staggered) - interpolate shift
            float leftContrib = 1f - tipLeftFoot.stepAlpha;
            float rightContrib = 1f - tipRightFoot.stepAlpha;
            float netShift = (rightContrib - leftContrib) * tipWeightShiftAmount;
            targetShift = transform.right * netShift;
            targetDip = Mathf.Max(
                Mathf.Sin(tipLeftFoot.stepAlpha * Mathf.PI),
                Mathf.Sin(tipRightFoot.stepAlpha * Mathf.PI)
            ) * tipPelvisDip;
        }

        // Smooth the weight shift (gentler for more natural motion)
        tipWeightShiftOffset = Vector3.Lerp(tipWeightShiftOffset, targetShift, 5f * deltaTime);

        // Apply to pelvis (via root)
        if (root != null && tipBlendWeight > 0.01f)
        {
            Vector3 dipOffset = -GravityUp * targetDip;
            Vector3 totalOffset = (tipWeightShiftOffset + dipOffset) * tipBlendWeight;
            root.localPosition += root.parent.InverseTransformVector(totalOffset);
        }
    }

    /// <summary>
    /// Calculates weight shift amount based on step progress.
    /// Uses parabolic curve: peaks at mid-step.
    /// </summary>
    private float CalculateWeightShiftAmount(float stepAlpha)
    {
        // Parabola: 4 * alpha * (1 - alpha), peaks at 0.5
        return 4f * stepAlpha * (1f - stepAlpha);
    }

    /// <summary>
    /// Applies gradual spine twist distribution during TIP.
    /// Creates a natural "corkscrew" effect where lower spine twists less,
    /// upper spine/neck twists more, allowing the upper body to lead the turn
    /// while feet remain planted.
    /// </summary>
    private void ApplySpineTwist(float deltaTime)
    {
        if (tipBlendWeight < 0.01f || tipState == TIPState.Inactive)
        {
            // Smoothly reset spine twist when not in TIP
            currentSpineTwist = Mathf.MoveTowards(currentSpineTwist, 0f, spineTwistSpeed * 2f * deltaTime);
            if (Mathf.Abs(currentSpineTwist) < 0.1f)
            {
                currentSpineTwist = 0f;
                return;
            }
        }

        // Calculate target spine twist based on accumulated twist and lead amount
        // spineLeadAmount controls how much the spine can twist ahead of feet
        float targetSpineTwist = accumulatedTwist * spineLeadAmount;

        // Smoothly interpolate toward target
        currentSpineTwist = Mathf.Lerp(currentSpineTwist, targetSpineTwist, spineTwistSpeed * deltaTime);

        // Write distribution weights into pre-allocated array (no heap allocation).
        // Values come from a serialized Vector4 so they may be tweaked in the editor.
        _spineDistribution[0] = spineTwistDistribution.x;
        _spineDistribution[1] = spineTwistDistribution.y;
        _spineDistribution[2] = spineTwistDistribution.z;
        _spineDistribution[3] = spineTwistDistribution.w;

        // Cache original rotations on first valid frame
        if (!spineRotationsCached)
        {
            CacheSpineRotations();
        }

        // Apply twist to each spine bone based on distribution weights
        for (int i = 0; i < _spineBones.Length; i++)
        {
            Transform bone = _spineBones[i];
            if (bone == null) continue;

            // Calculate this bone's twist contribution
            float boneWeight = _spineDistribution[i];
            float boneTwist = currentSpineTwist * boneWeight * tipBlendWeight;

            // Smooth individual bone twist
            spineCurrentTwists[i] = Mathf.Lerp(spineCurrentTwists[i], boneTwist, spineTwistSpeed * deltaTime);

            // Apply local Y rotation (twist around spine axis)
            // We rotate around the bone's local up axis for proper twist
            Quaternion twistRotation = Quaternion.AngleAxis(spineCurrentTwists[i], Vector3.up);
            bone.localRotation = bone.localRotation * twistRotation;
        }
    }

    /// <summary>
    /// Caches spine bone original rotations for twist reference.
    /// </summary>
    private void CacheSpineRotations()
    {
        // Use pre-allocated _spineBones array (no heap allocation)
        bool allValid = true;

        for (int i = 0; i < _spineBones.Length; i++)
        {
            if (_spineBones[i] != null)
            {
                spineOriginalRotations[i] = _spineBones[i].localRotation;
            }
            else
            {
                allValid = false;
            }
        }

        spineRotationsCached = allValid;
    }

    /// <summary>
    /// Applies head look-at toward camera direction using ADDITIVE LOCAL rotation.
    /// AAA STANDARD: Head leads anticipation during twist, returns to neutral after turn.
    /// YAW ONLY - no pitch or tilt applied.
    /// </summary>
    private void ApplyHeadLookAt(float deltaTime)
    {
        if (head == null) return;

        // ═══════════════════════════════════════════════════════════════════════
        // CACHE HEAD BASE ROTATION
        // Capture the head's local rotation as set by the animator (before TIP).
        // This allows us to apply additive rotation without compounding.
        // ═══════════════════════════════════════════════════════════════════════
        if (!headBaseCached && tipState == TIPState.TwistBuffer && tipBlendWeight > 0.5f)
        {
            headBaseLocalRotation = head.localRotation;
            headBaseCached = true;
        }

        // Determine target head yaw based on TIP state (YAW ONLY - no tilt)
        float targetYaw = 0f;
        float interpSpeed = headLookAtSpeed;

        // ═══════════════════════════════════════════════════════════════════════
        // HEAD BEHAVIOR BY STATE:
        // - TwistBuffer: Head looks toward camera (anticipation)
        // - Stepping: Head continues looking toward camera
        // - Settling: Head returns to neutral (facing forward)
        // - Inactive: Head at neutral
        // ═══════════════════════════════════════════════════════════════════════
        if (tipState == TIPState.Settling || tipBlendWeight < 0.01f)
        {
            // HEAD RESET: Return to neutral after turn completes
            targetYaw = 0f;
            interpSpeed = headLookAtSpeed * 3f; // Faster reset
        }
        else if (tipBlendWeight > 0.01f && (tipState == TIPState.TwistBuffer ||
                 tipState == TIPState.InsideFootStepping || tipState == TIPState.OutsideFootStepping))
        {
            // HEAD ANTICIPATION: Look toward camera direction (YAW ONLY)
            Vector3 cameraForward = Vector3.forward;
            if (playerController != null && playerController.PlayerCinemachineBrain != null)
            {
                cameraForward = playerController.PlayerCinemachineBrain.transform.forward;
            }

            Vector3 upDir = GravityUp;
            Vector3 rootForward = Quaternion.Euler(0f, rootHeadingYaw, 0f) * Vector3.forward;

            // Project camera forward onto horizontal plane
            Vector3 cameraHorizontal = Vector3.ProjectOnPlane(cameraForward, upDir);
            if (cameraHorizontal.sqrMagnitude < 0.001f)
                cameraHorizontal = rootForward;
            cameraHorizontal.Normalize();

            // Calculate yaw: how much head needs to turn relative to current body
            targetYaw = Vector3.SignedAngle(rootForward, cameraHorizontal, upDir);
            targetYaw = Mathf.Clamp(targetYaw, -maxHeadTurnAngle, maxHeadTurnAngle);
        }

        // Smoothly interpolate head yaw only
        currentHeadYaw = Mathf.Lerp(currentHeadYaw, targetYaw, interpSpeed * deltaTime);

        // Skip rotation if angle is negligible
        if (Mathf.Abs(currentHeadYaw) < 0.5f)
        {
            // Reset cache when fully neutral
            if (tipBlendWeight < 0.01f)
            {
                headBaseCached = false;
            }
            return;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // APPLY ADDITIVE LOCAL ROTATION - YAW ONLY (NO TILT)
        // Use the cached base rotation + yaw offset to prevent compounding.
        // ═══════════════════════════════════════════════════════════════════════
        if (headBaseCached)
        {
            // Create additive rotation in local space - YAW ONLY
            Quaternion additiveRotation = Quaternion.Euler(0f, currentHeadYaw, 0f);

            // Apply: base * additive
            head.localRotation = headBaseLocalRotation * additiveRotation;
        }
        else
        {
            // Fallback: Apply small additive rotation if no base cached
            Quaternion smallAdditive = Quaternion.Euler(0f, currentHeadYaw * 0.3f, 0f);
            head.localRotation = head.localRotation * smallAdditive;
        }
    }

    /// <summary>
    /// Applies foot rotation overrides after IK.
    /// Position is already handled by InjectTIPTargets() before IK (single-pass approach).
    /// This only handles foot rotation blending for smooth orientation.
    /// </summary>
    private void ApplyTIPFootRotation()
    {
        if (tipBlendWeight < 0.01f) return;

        // ═══════════════════════════════════════════════════════════════════════
        // LEFT FOOT ROTATION
        // ═══════════════════════════════════════════════════════════════════════
        if (leftAnkle != null)
        {
            Quaternion targetRot;

            if (tipLeftFoot.isStepping)
            {
                float alpha = tipLeftFoot.stepAlpha;
                // Use ease-out for rotation - starts matching movement, ends gently
                targetRot = Quaternion.Slerp(
                    tipLeftFoot.stepStartRotation,
                    tipLeftFoot.stepTargetRotation,
                    EaseOutCubic(alpha)
                );
            }
            else if (tipLeftFoot.isLocked)
            {
                targetRot = tipLeftFoot.lockedWorldRotation;
            }
            else
            {
                targetRot = leftAnkle.rotation;
            }

            // Locked feet snap immediately; stepping feet use tipBlendWeight for the outer blend.
            float leftRotBlend = tipLeftFoot.isLocked ? 1.0f : tipBlendWeight;
            leftAnkle.rotation = Quaternion.Slerp(leftAnkle.rotation, targetRot, leftRotBlend);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // RIGHT FOOT ROTATION
        // ═══════════════════════════════════════════════════════════════════════
        if (rightAnkle != null)
        {
            Quaternion targetRot;

            if (tipRightFoot.isStepping)
            {
                float alpha = tipRightFoot.stepAlpha;
                targetRot = Quaternion.Slerp(
                    tipRightFoot.stepStartRotation,
                    tipRightFoot.stepTargetRotation,
                    EaseOutCubic(alpha)
                );
            }
            else if (tipRightFoot.isLocked)
            {
                targetRot = tipRightFoot.lockedWorldRotation;
            }
            else
            {
                targetRot = rightAnkle.rotation;
            }

            // Locked feet snap immediately; stepping feet use tipBlendWeight for the outer blend.
            float rightRotBlend = tipRightFoot.isLocked ? 1.0f : tipBlendWeight;
            rightAnkle.rotation = Quaternion.Slerp(rightAnkle.rotation, targetRot, rightRotBlend);
        }
    }

    /// <summary>
    /// Interpolates step position with parabolic Z-arc (vertical lift).
    /// </summary>
    /// <param name="start">Start position</param>
    /// <param name="end">End position</param>
    /// <param name="alpha">Interpolation value (0 to 1)</param>
    /// <param name="upDir">Up direction for arc</param>
    /// <returns>Interpolated position with arc</returns>
    private Vector3 InterpolateStepPosition(Vector3 start, Vector3 end, float alpha, Vector3 upDir)
    {
        // Smoothstep for horizontal interpolation
        float smoothAlpha = SmoothStepAlpha(alpha);

        // Base position (linear interpolation with smoothstep easing)
        Vector3 basePos = Vector3.Lerp(start, end, smoothAlpha);

        // ═══════════════════════════════════════════════════════════════════════
        // PARABOLIC ARC: height = 4 * alpha * (1 - alpha) * peakHeight
        // This creates a smooth up-and-down arc that peaks at alpha = 0.5
        // ═══════════════════════════════════════════════════════════════════════
        float arcHeight = 4f * alpha * (1f - alpha) * tipFootLiftHeight;

        return basePos + upDir * arcHeight;
    }

    /// <summary>
    /// Smoothstep function for easing alpha interpolation.
    /// Uses smootherstep (Ken Perlin's improved version) for more natural motion.
    /// </summary>
    private float SmoothStepAlpha(float alpha)
    {
        // Smootherstep: 6t⁵ - 15t⁴ + 10t³
        // Has zero first AND second derivative at endpoints for smoother motion
        return alpha * alpha * alpha * (alpha * (alpha * 6f - 15f) + 10f);
    }

    /// <summary>
    /// Ease-out cubic for foot rotation (faster start, gentle end).
    /// </summary>
    private float EaseOutCubic(float alpha)
    {
        float t = 1f - alpha;
        return 1f - t * t * t;
    }

    /// <summary>
    /// Resets TIP system when exiting TIP mode.
    /// </summary>
    private void ResetTIPSystem()
    {
        tipState = TIPState.Inactive;
        tipBlendWeight = 0f;
        accumulatedTwist = 0f;
        turnDirection = 0;
        tipWeightShiftOffset = Vector3.zero;

        tipLeftFoot.isLocked = false;
        tipLeftFoot.isStepping = false;
        tipRightFoot.isLocked = false;
        tipRightFoot.isStepping = false;
    }

    private void OnValidate()
    {
        // Ensure ray weights sum to approximately 1
        float totalWeight = toeRayWeight + ballRayWeight + heelRayWeight;
        if (Mathf.Abs(totalWeight - 1f) > 0.01f)
        {
            Debug.LogWarning("FootIKSystem: Ray weights should sum to 1.0 for proper averaging.");
        }

        // Refresh caches when serialized fields change in the editor
        _tipAlphaRate = tipStepDuration > 0.0001f ? 1f / tipStepDuration : 0f;
        if (Application.isPlaying)
            RefreshSpineBonesCache();
    }

    private void OnDrawGizmosSelected()
    {
        // Pole direction
        Gizmos.color = Color.yellow;
        Vector3 poleDir = transform.TransformDirection(kneeForwardAxis).normalized;
        Gizmos.DrawRay(transform.position, poleDir * 0.5f);

        // Left leg
        if (leftHip != null && leftKnee != null && leftAnkle != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(leftHip.position, leftKnee.position);
            Gizmos.DrawLine(leftKnee.position, leftAnkle.position);

            if (leftToesEnd != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(leftAnkle.position, leftToesEnd.position);
                Gizmos.DrawWireSphere(leftToesEnd.position, 0.02f);
            }

            // Multi-ray visualization (gravity-aware)
            if (useMultiRay)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 1f, 0.5f);
                Vector3 heelWorld = leftAnkle.TransformPoint(heelOffset);
                Vector3 ballWorld = leftAnkle.TransformPoint(ballOffset);
                Vector3 upDir = GravityUp;
                Vector3 downDir = GravityDown;
                Gizmos.DrawLine(heelWorld + upDir * raycastOriginHeight, heelWorld + downDir * raycastDistance);
                Gizmos.DrawLine(ballWorld + upDir * raycastOriginHeight, ballWorld + downDir * raycastDistance);
            }
        }

        // Right leg
        if (rightHip != null && rightKnee != null && rightAnkle != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(rightHip.position, rightKnee.position);
            Gizmos.DrawLine(rightKnee.position, rightAnkle.position);

            if (rightToesEnd != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(rightAnkle.position, rightToesEnd.position);
                Gizmos.DrawWireSphere(rightToesEnd.position, 0.02f);
            }

            // Multi-ray visualization (gravity-aware)
            if (useMultiRay)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.5f);
                Vector3 heelWorld = rightAnkle.TransformPoint(heelOffset);
                Vector3 ballWorld = rightAnkle.TransformPoint(ballOffset);
                Vector3 upDir = GravityUp;
                Vector3 downDir = GravityDown;
                Gizmos.DrawLine(heelWorld + upDir * raycastOriginHeight, heelWorld + downDir * raycastDistance);
                Gizmos.DrawLine(ballWorld + upDir * raycastOriginHeight, ballWorld + downDir * raycastDistance);
            }
        }

        // Play mode visualization
        if (Application.isPlaying)
        {
            if (leftFoot.grounded && leftToesEnd != null)
            {
                Gizmos.color = (isOnLedge && leftFootIsLower) ? Color.green : (isOnLedge ? Color.yellow : Color.green);
                Vector3 groundPos = new Vector3(leftToesEnd.position.x, leftFoot.groundHeight, leftToesEnd.position.z);
                Gizmos.DrawWireCube(groundPos, new Vector3(0.1f, 0.02f, 0.1f));

                // Ground normal visualization
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(groundPos, leftFoot.smoothedNormal * 0.2f);
            }

            if (rightFoot.grounded && rightToesEnd != null)
            {
                Gizmos.color = (isOnLedge && !leftFootIsLower) ? Color.green : (isOnLedge ? Color.yellow : Color.green);
                Vector3 groundPos = new Vector3(rightToesEnd.position.x, rightFoot.groundHeight, rightToesEnd.position.z);
                Gizmos.DrawWireCube(groundPos, new Vector3(0.1f, 0.02f, 0.1f));

                // Ground normal visualization
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(groundPos, rightFoot.smoothedNormal * 0.2f);
            }

            // Ledge mode indicator
            if (isOnLedge && root != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(root.position, 0.1f);
            }

            // Soft IK range visualization
            if (leftHip != null)
            {
                float maxReach = leftUpperLegLength + leftLowerLegLength;
                float softThreshold = maxReach * softIKRatio;
                Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
                Gizmos.DrawWireSphere(leftHip.position, softThreshold);
                Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
                Gizmos.DrawWireSphere(leftHip.position, maxReach);
            }

            // Ankle target visualization
            if (isOnLedge)
            {
                if (leftFootIsLower && rightFoot.grounded)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(rightFoot.targetAnklePosition, 0.05f);
                    Gizmos.DrawLine(rightHip.position, rightFoot.targetAnklePosition);
                }
                else if (!leftFootIsLower && leftFoot.grounded)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(leftFoot.targetAnklePosition, 0.05f);
                    Gizmos.DrawLine(leftHip.position, leftFoot.targetAnklePosition);
                }
            }
            else
            {
                if (leftFoot.grounded)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireSphere(leftFoot.targetAnklePosition, 0.03f);
                    if (leftAnkle != null)
                        Gizmos.DrawLine(leftAnkle.position, leftFoot.targetAnklePosition);
                }

                if (rightFoot.grounded)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(rightFoot.targetAnklePosition, 0.03f);
                    if (rightAnkle != null)
                        Gizmos.DrawLine(rightAnkle.position, rightFoot.targetAnklePosition);
                }
            }
        }
    }
}
