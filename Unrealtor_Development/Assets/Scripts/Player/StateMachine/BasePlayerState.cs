using System.Collections;
using UnityEngine;
using UnityHFSM;

public interface IPhysicsUpdatable
{
    void OnPhysicsUpdate();
}

public abstract class BasePlayerState : ActionState, IPhysicsUpdatable
{
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int InputXHash = Animator.StringToHash("InputX");
    private static readonly int InputYHash = Animator.StringToHash("InputY");

    protected BasePlayerController controller;

    public BasePlayerState(BasePlayerController controller) : base(needsExitTime: false)
    {
        this.controller = controller;
    }

    protected float GetEffectiveSpeed(float baseSpeed)
    {
        return baseSpeed;
    }

    protected Vector2 GetMovementInput()
    {
        // Respect pause even if InputUser / rebinding re-enabled the Move action (editor vs standalone timing).
        if (controller.InputData.IsMovePaused)
            return Vector2.zero;

        // In the new centralized input system, each player only receives their own input
        if (controller.PlayerManager.CurrentControllerType == PlayerInputManager.ControllerType.TwoPlayer)
        {
            return controller.moveAction.ReadValue<Vector2>();
        }
        else
        {
            if (controller.PlayerManager.CurrPlayerEnum == controller.playerID)
            {
                return controller.moveAction.ReadValue<Vector2>();
            }
            return Vector2.zero;
        }
    }

    protected Vector3 CalculateMovementDirection(Vector2 input)
    {
        if (input == Vector2.zero) return Vector3.zero;

        // Align movement to the plane perpendicular to current gravity
        // New up is opposite of gravity direction
        Vector3 upRelativeToGravity = (-controller.GravityDirection).normalized;

        Transform cameraTransform = controller.PlayerCinemachineBrain.transform;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        // Project camera forward onto the movement plane to avoid into-wall components
        Vector3 forwardOnPlane = Vector3.ProjectOnPlane(cameraForward, upRelativeToGravity);
        if (forwardOnPlane.sqrMagnitude < 1e-6f)
        {
            // Fallback: if looking along up, use camera right projected
            forwardOnPlane = Vector3.ProjectOnPlane(cameraRight, upRelativeToGravity);
            if (forwardOnPlane.sqrMagnitude < 1e-6f)
            {
                // Last resort: pick any vector perpendicular to up
                forwardOnPlane = Vector3.Cross(upRelativeToGravity, Vector3.right);
                if (forwardOnPlane.sqrMagnitude < 1e-6f)
                {
                    forwardOnPlane = Vector3.Cross(upRelativeToGravity, Vector3.forward);
                }
            }
        }
        forwardOnPlane.Normalize();

        // Build a right vector orthogonal on the plane for stable strafing
        Vector3 rightOnPlane = Vector3.Cross(upRelativeToGravity, forwardOnPlane).normalized;

        Vector3 desiredDirection = (forwardOnPlane * input.y) + (rightOnPlane * input.x);
        return desiredDirection;
    }

    protected void ApplyMovement(Vector3 movement)
    {
        controller.Rigidbody.MovePosition(controller.Rigidbody.position + movement * Time.deltaTime);
    }

    // Physics-tick movement helper (use from FixedUpdate-driven state logic)
    protected void ApplyMovementPhysics(Vector3 movement)
    {
        // Skip normal movement while smooth step is in progress
        // The step interpolation is handled by BasePlayerController.UpdateSmoothStep
        if (controller.IsSteppingUp)
            return;
       

        Rigidbody rb = controller.Rigidbody;
        Vector3 delta = movement * Time.fixedDeltaTime;
        float distance = delta.magnitude;
        if (distance <= 1e-6f)
            return;
        

        Vector3 direction = delta.normalized;
        float backoff = 0.01f;

        // Fall prevention: block the "into the ledge" component if the drop ahead is too large.
        // This keeps small step-downs allowed while preventing walking off tall drops.
        if (controller.EnableFallPrevention && controller.IsPlayerGrounded)
        {
            float dropHeight;
            if (controller.ShouldBlockMovementForFall(direction, distance, out dropHeight))
            {
                Vector3 up = (-controller.GravityDirection).normalized;
                Vector3 horizontalDir = Vector3.ProjectOnPlane(direction, up);
                if (horizontalDir.sqrMagnitude > 1e-6f)
                {
                    horizontalDir.Normalize();
                    float intoHazard = Vector3.Dot(delta, horizontalDir);
                    if (intoHazard > 0f)
                    {
                        delta -= horizontalDir * intoHazard;
                        distance = delta.magnitude;
                        if (distance <= 1e-6f)
                            return;
                        direction = delta.normalized;
                    }
                }
            }
        }

        // Auto-step handling before resolving primary sweep
        // Skip if already stepping up (smooth step in progress)
        if (controller.EnableAutoStep && controller.IsAutoStepGrounded && !controller.IsSteppingUp)
        {
            Vector3 raisedPos, finalPos;
            Collider stepCollider;
            if (TryResolveAutoStep(rb, direction, distance, out raisedPos, out finalPos, out stepCollider))
            {
                // Raise first, then smooth forward while preserving collision with railings/side colliders.
                controller.StartSmoothStepFromRaised(raisedPos, finalPos, stepCollider);
                return;
            }
        }

        RaycastHit hit;
        if (rb.SweepTest(direction, out hit, distance, QueryTriggerInteraction.Ignore))
        {
            // Try to slide along the obstacle surface
            Vector3 slideDelta = Vector3.ProjectOnPlane(delta, hit.normal);
            float slideDistance = slideDelta.magnitude;

            if (slideDistance > 1e-6f)
            {
                Vector3 slideDir = slideDelta.normalized;
                RaycastHit hit2;
                if (rb.SweepTest(slideDir, out hit2, slideDistance, QueryTriggerInteraction.Ignore))
                {
                    float moveDist = Mathf.Max(0f, hit2.distance - backoff);
                    rb.MovePosition(rb.position + slideDir * moveDist);
                }
                else
                {
                    rb.MovePosition(rb.position + slideDelta);
                }
            }
            else
            {
                // No valid slide; move up to the hit with a small backoff
                float moveDist = Mathf.Max(0f, hit.distance - backoff);
                rb.MovePosition(rb.position + direction * moveDist);
            }
        }
        else
        {
            rb.MovePosition(rb.position + delta);
        }
    }

    // Debug data for visualization
    private static Vector3 _debugStepStart;
    private static Vector3 _debugStepEnd;
    private static Vector3 _debugForwardHit;
    private static Vector3 _debugStepSurface;
    private static Vector3 _debugUp = Vector3.up;
    private static Vector3 _debugProbeLowStart;
    private static Vector3 _debugProbeHighStart;
    private static Vector3 _debugProbeDirection;
    private static float _debugProbeForwardDistance;
    private static float _debugProbeCastRadius;
    private static readonly Vector3[] _debugSurfaceProbeStarts = new Vector3[4];
    private static readonly Vector3[] _debugSurfaceProbeEnds = new Vector3[4];
    private static int _debugSurfaceProbeCount;
    private static bool _debugStepSuccess;
    private static string _debugStepFailReason;

    // Primary tag expected in this project for climbable stairs.
    private const string STAIR_TAG = "Staircase";
    // Backward-compat fallback tag so either naming style can work.
    private const string STAIR_TAG_FALLBACK = "stair";
    private const float STEP_HEIGHT_TOLERANCE = 0.08f;
    private readonly Collider[] stairOverlapHits = new Collider[16];
    private readonly RaycastHit[] stairProbeHits = new RaycastHit[16];

    private static IEnumerator ReenableCollisionDelayed(Collider a, Collider b, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (a != null && b != null)
        {
            Physics.IgnoreCollision(a, b, false);
        }
    }

    private void SetAutoStepFail(string reason)
    {
        _debugStepSuccess = false;
        _debugStepFailReason = reason;
        if (controller.EnableAutoStepDebug)
        {
            Debug.Log($"[AutoStep] FAIL: {reason}");
        }
    }

    private bool IsSelfColliderLocal(Collider c)
    {
        if (c == null) return false;
        Rigidbody rb = controller.Rigidbody;
        if (rb != null && c.attachedRigidbody == rb) return true;
        return c.transform.IsChildOf(controller.transform);
    }

    private bool TryFindForwardStairHit(Vector3 castOrigin, float castRadius, Vector3 horizontalMove, float forwardDist, int stairProbeMask, Collider excludedSupportCollider, ref RaycastHit bestHit, ref float bestDist, ref bool foundHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(castOrigin, castRadius, horizontalMove, stairProbeHits, forwardDist, stairProbeMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = stairProbeHits[i];
            Collider candidateCollider = candidate.collider;
            if (candidateCollider == null || IsSelfColliderLocal(candidateCollider))
                continue;
            if (excludedSupportCollider != null && candidateCollider == excludedSupportCollider)
                continue;
            if (!IsStairCollider(candidateCollider))
                continue;

            if (candidate.distance < bestDist)
            {
                bestDist = candidate.distance;
                bestHit = candidate;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private bool TryResolveAutoStep(Rigidbody rb, Vector3 moveDir, float moveDistance, out Vector3 raisedPosition, out Vector3 finalPosition, out Collider stepCollider)
    {
        raisedPosition = rb.position;
        finalPosition = rb.position;
        stepCollider = null;
        _debugStepStart = rb.position;
        _debugStepSuccess = false;
        _debugStepFailReason = "";
        _debugForwardHit = rb.position;
        _debugStepSurface = rb.position;
        _debugProbeLowStart = rb.position;
        _debugProbeHighStart = rb.position;
        _debugProbeDirection = Vector3.zero;
        _debugProbeForwardDistance = 0f;
        _debugProbeCastRadius = 0f;
        _debugSurfaceProbeCount = 0;

        Vector3 up = (-controller.GravityDirection).normalized;
        _debugUp = up;
        Vector3 horizontalMove = Vector3.ProjectOnPlane(moveDir, up).normalized;
        if (horizontalMove.sqrMagnitude < 1e-6f)
        {
            SetAutoStepFail("No horizontal movement");
            return false;
        }

        float maxStep = controller.MaxStepHeight;
        float radius = controller.GroundCheckRadius;
        float forwardDist = Mathf.Max(controller.AutoStepMinProbeDistance, moveDistance + controller.StepForwardExtra + radius);

        // Probe forward from two heights to make stair acquisition more reliable.
        RaycastHit hit;
        float castRadius = Mathf.Max(0.04f, radius * 0.45f);
        // Probe heights scale with MaxStepHeight so tuning it directly affects detection volume.
        float lowProbeHeight = Mathf.Max(0.03f, maxStep * 0.12f);
        float highProbeHeight = Mathf.Max(lowProbeHeight + 0.04f, maxStep * 0.55f);
        Vector3 castOriginLow = rb.position + up * lowProbeHeight;
        Vector3 castOriginHigh = rb.position + up * highProbeHeight;
        _debugProbeLowStart = castOriginLow;
        _debugProbeHighStart = castOriginHigh;
        _debugProbeDirection = horizontalMove;
        _debugProbeForwardDistance = forwardDist;
        _debugProbeCastRadius = castRadius;
        int stairProbeMask = ~0;
        Collider currentSupportCollider = null;
        RaycastHit supportHit;
        if (controller.TryGetGroundHit(out supportHit, extraDistance: 1.0f))
        {
            currentSupportCollider = supportHit.collider;
        }

        bool foundHit = false;
        float bestHitDist = float.PositiveInfinity;
        hit = default;
        TryFindForwardStairHit(castOriginLow, castRadius, horizontalMove, forwardDist, stairProbeMask, currentSupportCollider, ref hit, ref bestHitDist, ref foundHit);
        TryFindForwardStairHit(castOriginHigh, castRadius, horizontalMove, forwardDist, stairProbeMask, currentSupportCollider, ref hit, ref bestHitDist, ref foundHit);

        if (!foundHit)
        {
            // Fallback: gather nearby colliders near the stair front and choose the nearest stair-tagged one.
            Vector3 overlapCenter = rb.position + horizontalMove * Mathf.Max(radius * 1.2f, forwardDist * 0.55f) + up * 0.08f;
            float overlapRadius = Mathf.Max(radius * 1.1f, 0.22f);
            int overlapCount = Physics.OverlapSphereNonAlloc(overlapCenter, overlapRadius, stairOverlapHits, stairProbeMask, QueryTriggerInteraction.Ignore);

            Collider bestStair = null;
            float bestDistSq = float.PositiveInfinity;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider c = stairOverlapHits[i];
                if (c == null || IsSelfColliderLocal(c) || !IsStairCollider(c))
                    continue;
                if (currentSupportCollider != null && c == currentSupportCollider)
                    continue;

                Vector3 closest = c.ClosestPoint(rb.position);
                Vector3 planarToClosest = Vector3.ProjectOnPlane(closest - rb.position, up);
                float distSq = planarToClosest.sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestStair = c;
                    _debugForwardHit = closest;
                }
            }

            if (bestStair == null)
            {
                SetAutoStepFail("No step candidate ahead");
                return false;
            }

            stepCollider = bestStair;
            return StepOntoCollider(rb, bestStair, _debugForwardHit, horizontalMove, up, maxStep, out raisedPosition, out finalPosition);
        }

        _debugForwardHit = hit.point;
        stepCollider = hit.collider; // Return the collider we hit after filtering

        // Check if this collider (or parent chain) is a tagged stair.
        Collider hitCollider = hit.collider;
        bool isStair = IsStairCollider(hitCollider);

        if (controller.EnableAutoStepDebug)
        {
            Debug.Log($"[AutoStep] Hit: {hitCollider.name}, IsStair: {isStair}, Normal: {hit.normal}");
        }

        // Only climb stair-tagged objects.
        if (!isStair)
        {
            SetAutoStepFail($"Collider '{hitCollider.name}' is not tagged {STAIR_TAG}");
            return false;
        }

        // Step onto the top of the stair collider we hit.
        return StepOntoCollider(rb, hitCollider, hit.point, horizontalMove, up, maxStep, out raisedPosition, out finalPosition);
    }

    private bool IsStairCollider(Collider col)
    {
        if (col == null) return false;

        Transform current = col.transform;
        while (current != null)
        {
            if (current.CompareTag(STAIR_TAG) /*|| current.CompareTag(STAIR_TAG_FALLBACK)*/)
                return true;
            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// Simply step onto the top of the collider we hit
    /// Two-phase: returns raised position (up) and final position (forward onto step)
    /// </summary>
    private bool StepOntoCollider(Rigidbody rb, Collider stepCollider, Vector3 stepHitPoint, Vector3 horizontalMove, Vector3 up, float maxStep, out Vector3 raisedPosition, out Vector3 finalPosition)
    {
        raisedPosition = rb.position;
        finalPosition = rb.position;

        float playerHeight = Vector3.Dot(rb.position, up);

        if (controller.EnableAutoStepDebug)
        {
            Debug.Log($"[AutoStep] Evaluating stair '{stepCollider.name}' from hit point {stepHitPoint}");
        }

        // Probe for a nearby walkable point on top of this stair.
        // This avoids relying on full collider bounds, which can be too tall for whole staircase colliders.
        float rayDistance = maxStep + 0.6f;
        float rayStartHeight = maxStep + 0.25f;
        float[] forwardProbeOffsets = { 0.02f, 0.08f, 0.14f, 0.2f };
        bool foundSurface = false;
        RaycastHit bestSurface = default;
        float bestStepUp = float.MaxValue;

        for (int i = 0; i < forwardProbeOffsets.Length; i++)
        {
            float offset = forwardProbeOffsets[i];
            Vector3 rayStart = stepHitPoint + horizontalMove * offset + up * rayStartHeight;
            if (i < _debugSurfaceProbeStarts.Length)
            {
                _debugSurfaceProbeStarts[i] = rayStart;
                _debugSurfaceProbeEnds[i] = rayStart - up * rayDistance;
                _debugSurfaceProbeCount = i + 1;
            }
            RaycastHit surfaceHit;
            if (!Physics.Raycast(rayStart, -up, out surfaceHit, rayDistance, ~0, QueryTriggerInteraction.Ignore))
                continue;
            if (!IsStairCollider(surfaceHit.collider))
                continue;

            float surfaceUpDot = Vector3.Dot(surfaceHit.normal, up);
            if (surfaceUpDot < Mathf.Max(0.35f, controller.StepMaxUpDot))
                continue;

            float candidateSurfaceHeight = Vector3.Dot(surfaceHit.point, up);
            float stepUp = candidateSurfaceHeight - playerHeight;
            if (stepUp <= 0.01f || stepUp > (maxStep + STEP_HEIGHT_TOLERANCE))
                continue;

            if (stepUp < bestStepUp)
            {
                bestStepUp = stepUp;
                bestSurface = surfaceHit;
                foundSurface = true;
                if (i < _debugSurfaceProbeEnds.Length)
                {
                    _debugSurfaceProbeEnds[i] = surfaceHit.point;
                }
            }
        }

        if (!foundSurface)
        {
            SetAutoStepFail($"No valid stair surface within {maxStep:F2}m");
            return false;
        }

        Vector3 stepPoint = bestSurface.point;
        float actualStepUp = Vector3.Dot(stepPoint, up) - playerHeight;

        _debugStepSurface = stepPoint;

        // Check overhead clearance
        RaycastHit upHit;
        if (rb.SweepTest(up, out upHit, actualStepUp + 0.1f, QueryTriggerInteraction.Ignore))
        {
            SetAutoStepFail($"Blocked overhead by {upHit.collider.name}");
            return false;
        }

        float stepSurfaceHeight = Vector3.Dot(stepPoint, up);

        // PHASE 1: Raised position - move straight UP to clear the step
        // Go slightly higher than the step surface
        raisedPosition = rb.position + up * (actualStepUp + 0.05f);

        // PHASE 2: Final position - on top of step with forward movement
        float newHeight = stepSurfaceHeight + 0.02f;
        finalPosition = rb.position + up * (newHeight - playerHeight);
        float fwdOffset = Mathf.Clamp(controller.StepForwardExtra + 0.01f, 0.03f, 0.08f);
        finalPosition += horizontalMove * fwdOffset; // Controlled forward nudge for smoother feel

        _debugStepEnd = finalPosition;
        _debugStepSuccess = true;
        _debugStepFailReason = "";

        if (controller.EnableAutoStepDebug)
        {
            Debug.Log($"[AutoStep] SUCCESS onto {stepCollider.name}: StepUp={actualStepUp:F2}, target={finalPosition}");
        }

        return true;
    }

    // Call this from OnDrawGizmos in a MonoBehaviour to visualize
    public static void DrawAutoStepDebug()
    {
        if (_debugStepStart == Vector3.zero) return;

        if (_debugStepSuccess)
        {
            // Green: successful step path
            Debug.DrawLine(_debugStepStart, _debugStepEnd, Color.green, 0.1f);
            Debug.DrawLine(_debugStepStart, _debugForwardHit, Color.yellow, 0.1f);
            Debug.DrawLine(_debugStepSurface, _debugStepSurface + _debugUp * 0.2f, Color.cyan, 0.1f);
        }
        else
        {
            // Red: failed attempt
            Vector3 failDir = (_debugForwardHit - _debugStepStart);
            if (failDir.sqrMagnitude < 1e-6f)
                failDir = Vector3.Cross(_debugUp, Vector3.right);
            if (failDir.sqrMagnitude < 1e-6f)
                failDir = Vector3.Cross(_debugUp, Vector3.forward);
            Debug.DrawLine(_debugStepStart, _debugStepStart + failDir.normalized * 0.5f, Color.red, 0.1f);
        }

        // Forward spherecast probes (height + forward distance).
        Vector3 forwardEndLow = _debugProbeLowStart + _debugProbeDirection * _debugProbeForwardDistance;
        Vector3 forwardEndHigh = _debugProbeHighStart + _debugProbeDirection * _debugProbeForwardDistance;
        if (_debugProbeDirection.sqrMagnitude > 1e-6f && _debugProbeForwardDistance > 0f)
        {
            Debug.DrawLine(_debugProbeLowStart, forwardEndLow, new Color(1f, 0.5f, 0f), 0.1f);   // orange: low cast
            Debug.DrawLine(_debugProbeHighStart, forwardEndHigh, new Color(1f, 0f, 1f), 0.1f);    // magenta: high cast

            // Tiny bars in "up" direction to hint cast radius at start/end.
            Vector3 upBar = _debugUp * _debugProbeCastRadius;
            Debug.DrawLine(_debugProbeLowStart - upBar, _debugProbeLowStart + upBar, new Color(1f, 0.5f, 0f), 0.1f);
            Debug.DrawLine(forwardEndLow - upBar, forwardEndLow + upBar, new Color(1f, 0.5f, 0f), 0.1f);
            Debug.DrawLine(_debugProbeHighStart - upBar, _debugProbeHighStart + upBar, new Color(1f, 0f, 1f), 0.1f);
            Debug.DrawLine(forwardEndHigh - upBar, forwardEndHigh + upBar, new Color(1f, 0f, 1f), 0.1f);
        }

        // Downward surface probes (how high and how far in front the surface checks are).
        for (int i = 0; i < _debugSurfaceProbeCount; i++)
        {
            Debug.DrawLine(_debugSurfaceProbeStarts[i], _debugSurfaceProbeEnds[i], Color.white, 0.1f);
        }
    }

    // Scene-view Gizmos version for easier probe visualization.
    public static void DrawAutoStepGizmos()
    {
        if (_debugStepStart == Vector3.zero) return;

        if (_debugStepSuccess)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_debugStepStart, _debugStepEnd);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_debugStepStart, _debugForwardHit);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(_debugStepSurface, _debugStepSurface + _debugUp * 0.2f);
            Gizmos.DrawWireSphere(_debugStepSurface, 0.03f);
        }
        else
        {
            Vector3 failDir = (_debugForwardHit - _debugStepStart);
            if (failDir.sqrMagnitude < 1e-6f)
                failDir = Vector3.Cross(_debugUp, Vector3.right);
            if (failDir.sqrMagnitude < 1e-6f)
                failDir = Vector3.Cross(_debugUp, Vector3.forward);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(_debugStepStart, _debugStepStart + failDir.normalized * 0.5f);
        }

        Vector3 forwardEndLow = _debugProbeLowStart + _debugProbeDirection * _debugProbeForwardDistance;
        Vector3 forwardEndHigh = _debugProbeHighStart + _debugProbeDirection * _debugProbeForwardDistance;
        if (_debugProbeDirection.sqrMagnitude > 1e-6f && _debugProbeForwardDistance > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawLine(_debugProbeLowStart, forwardEndLow);
            Gizmos.color = new Color(1f, 0f, 1f);
            Gizmos.DrawLine(_debugProbeHighStart, forwardEndHigh);

            Vector3 upBar = _debugUp * _debugProbeCastRadius;
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawLine(_debugProbeLowStart - upBar, _debugProbeLowStart + upBar);
            Gizmos.DrawLine(forwardEndLow - upBar, forwardEndLow + upBar);
            Gizmos.color = new Color(1f, 0f, 1f);
            Gizmos.DrawLine(_debugProbeHighStart - upBar, _debugProbeHighStart + upBar);
            Gizmos.DrawLine(forwardEndHigh - upBar, forwardEndHigh + upBar);
        }

        Gizmos.color = Color.white;
        for (int i = 0; i < _debugSurfaceProbeCount; i++)
        {
            Gizmos.DrawLine(_debugSurfaceProbeStarts[i], _debugSurfaceProbeEnds[i]);
            Gizmos.DrawWireSphere(_debugSurfaceProbeStarts[i], 0.015f);
        }
    }


    protected void UpdateAnimatorSpeed(float targetSpeed)
    {
        if (controller.Animator == null) return;
        
        // Set the Speed parameter for the blend tree
        controller.Animator.SetFloat("Speed", targetSpeed);
    }

    protected void UpdateAnimatorGrounded(bool isGrounded)
    {
        if (controller.Animator == null) return;
        
        // Set the Grounded parameter for air/ground state transitions
        //controller.Animator.SetBool(GroundedHash, isGrounded);
    }

    private void GetMovementAxesOnPlane(out Vector3 forwardOnPlane, out Vector3 rightOnPlane)
    {
        Vector3 upRelativeToGravity = (-controller.GravityDirection).normalized;
        Transform cameraTransform = controller.PlayerCinemachineBrain.transform;
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        forwardOnPlane = Vector3.ProjectOnPlane(cameraForward, upRelativeToGravity);
        if (forwardOnPlane.sqrMagnitude < 1e-6f)
        {
            forwardOnPlane = Vector3.ProjectOnPlane(cameraRight, upRelativeToGravity);
            if (forwardOnPlane.sqrMagnitude < 1e-6f)
            {
                forwardOnPlane = Vector3.Cross(upRelativeToGravity, Vector3.right);
                if (forwardOnPlane.sqrMagnitude < 1e-6f)
                {
                    forwardOnPlane = Vector3.Cross(upRelativeToGravity, Vector3.forward);
                }
            }
        }

        forwardOnPlane.Normalize();
        rightOnPlane = Vector3.Cross(upRelativeToGravity, forwardOnPlane).normalized;
    }

    protected void UpdateAnimatorLocomotionFromVelocity(Vector3 worldVelocity, float referenceSpeed, float dampTime = 0.08f)
    {
        if (controller.Animator == null) return;

        GetMovementAxesOnPlane(out Vector3 forwardOnPlane, out Vector3 rightOnPlane);
        Vector3 upRelativeToGravity = (-controller.GravityDirection).normalized;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(worldVelocity, upRelativeToGravity);

        float safeReferenceSpeed = Mathf.Max(0.001f, referenceSpeed);
        float inputX = Mathf.Clamp(Vector3.Dot(planarVelocity, rightOnPlane) / safeReferenceSpeed, -1f, 1f);
        float inputY = Mathf.Clamp(Vector3.Dot(planarVelocity, forwardOnPlane) / safeReferenceSpeed, -1f, 1f);

        // Scale circle-normalized input to square corners so diagonals reach (1,1)
        // This ensures diagonal movement blends properly to walk animations
        float maxComponent = Mathf.Max(Mathf.Abs(inputX), Mathf.Abs(inputY));
        if (maxComponent > 0.01f)
        {
            float magnitude = Mathf.Sqrt(inputX * inputX + inputY * inputY);
            if (magnitude > 0.01f)
            {
                float scale = maxComponent / magnitude;
                inputX *= scale;
                inputY *= scale;
            }
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Feed signed normalized values for 2D blend tree params.
        controller.Animator.SetFloat(InputXHash, inputX, dampTime, dt);
        controller.Animator.SetFloat(InputYHash, inputY, dampTime, dt);
    }

    protected float CalculateAnimatorSpeed(Vector2 input, float speedMultiplier = 1f)
    {
        // Locomotion blend tree: 0 = idle, 1 = walk, 2 = run
        float inputMagnitude = Mathf.Clamp01(input.magnitude);

        if (inputMagnitude <= 0.1f)
            return 0f; // Idle

        // Smoothly blend within walk (0-1) or run (1-2) ranges
        return speedMultiplier <= 1f
            ? Mathf.Lerp(0f, 1f, inputMagnitude)
            : Mathf.Lerp(1f, 2f, inputMagnitude);
    }

    // Optional physics hook. State machines can call this during FixedUpdate.
    public virtual void OnPhysicsUpdate() { }
}
