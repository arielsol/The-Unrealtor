using UnityEngine;

/// <summary>
/// Hand IK system for placing hands on surfaces during interactions.
/// Activates when near interactable objects to prevent hand clipping.
/// Uses two-bone IK solver pattern from FootIKSystem.
/// </summary>
public class HandIKSystem : MonoBehaviour
{
    [Header("IK Settings")]
    [SerializeField] private bool enableHandIK = true;
    [SerializeField] [Range(0f, 1f)] private float maxIKWeight = 1f;
    [SerializeField] private float ikWeightSmoothTime = 0.15f;

    [Header("Gravity Reference")]
    [Tooltip("Reference to the player controller for gravity direction. If null, will search in parent.")]
    [SerializeField] private BasePlayerController playerController;

    [Header("Left Arm Bones")]
    [SerializeField] private Transform leftShoulder;
    [SerializeField] private Transform leftUpperArm;
    [SerializeField] private Transform leftForeArm;
    [SerializeField] private Transform leftHand;

    [Header("Right Arm Bones")]
    [SerializeField] private Transform rightShoulder;
    [SerializeField] private Transform rightUpperArm;
    [SerializeField] private Transform rightForeArm;
    [SerializeField] private Transform rightHand;

    [Header("Surface Detection")]
    [SerializeField] private LayerMask surfaceLayerMask = ~0;
    [Tooltip("Maximum distance to search for surfaces")]
    [SerializeField] private float detectionRadius = 1.0f;
    [Tooltip("Distance at which IK starts to activate")]
    [SerializeField] private float activationDistance = 0.6f;
    [Tooltip("Minimum distance - below this, IK fades out to prevent clipping")]
    [SerializeField] private float minimumSurfaceDistance = 0.15f;
    [Tooltip("Minimum dot product between character forward and surface normal")]
    [SerializeField] [Range(-1f, 1f)] private float minSurfaceFacingDot = 0.2f;

    [Header("Hand Placement")]
    [Tooltip("Offset from surface (how far hand hovers above surface)")]
    [SerializeField] private float handSurfaceOffset = 0.02f;
    [Tooltip("Preferred hand height relative to character (shoulder height)")]
    [SerializeField] private float preferredHandHeight = 1.2f;
    [Tooltip("How fast hand positions smooth to targets")]
    [SerializeField] private float handPositionSmoothTime = 0.1f;

    [Header("Elbow Direction")]
    [Tooltip("Blend between character back (0) and outward (1) for elbow hint")]
    [SerializeField] [Range(0f, 1f)] private float elbowOutwardBlend = 0.5f;

    [Header("Soft IK")]
    [Tooltip("Ratio of max reach where soft falloff begins")]
    [SerializeField] [Range(0.8f, 0.99f)] private float softIKRatio = 0.95f;

    [Header("Interaction Mode")]
    [Tooltip("Only activate when near interactable objects")]
    [SerializeField] private bool interactionOnlyMode = true;
    [SerializeField] private LayerMask interactableLayerMask;

    // Runtime - arm lengths
    private float leftUpperArmLength;
    private float leftForeArmLength;
    private float rightUpperArmLength;
    private float rightForeArmLength;

    // Per-hand IK data
    private struct HandIKData
    {
        public bool isActive;
        public float currentWeight;
        public float targetWeight;
        public float weightVelocity;
        public Vector3 targetHandPosition;
        public Vector3 smoothedHandPosition;
        public Vector3 positionVelocity;
        public Vector3 surfaceNormal;
        public bool hasValidSurface;
    }

    private HandIKData leftHandData;
    private HandIKData rightHandData;

    // Interaction state
    private bool isNearInteractable;
    private Transform currentInteractableSurface;

    // Gravity-aware direction properties
    private Vector3 GravityUp => playerController != null ? -playerController.GravityDirection : Vector3.up;

    private void Start()
    {
        CacheArmLengths();

        // Auto-find player controller if not assigned
        if (playerController == null)
        {
            playerController = GetComponentInParent<BasePlayerController>();
        }

        // Initialize smoothed positions
        if (leftHand != null) leftHandData.smoothedHandPosition = leftHand.position;
        if (rightHand != null) rightHandData.smoothedHandPosition = rightHand.position;
    }

    [ContextMenu("Auto Find Arm Bones")]
    public void AutoFindArmBones()
    {
        Transform[] allBones = GetComponentsInChildren<Transform>();

        foreach (Transform bone in allBones)
        {
            string name = bone.name.ToLower();

            // Left arm
            if (leftShoulder == null && ContainsAny(name, "leftshoulder", "left_shoulder", "l_shoulder", "shoulder_l", "leftclavicle"))
                leftShoulder = bone;
            else if (leftUpperArm == null && ContainsAny(name, "leftarm", "leftupperarm", "left_arm", "l_arm", "arm_l") && !name.Contains("fore") && !name.Contains("lower"))
                leftUpperArm = bone;
            else if (leftForeArm == null && ContainsAny(name, "leftforearm", "leftlowerarm", "left_forearm", "l_forearm", "forearm_l"))
                leftForeArm = bone;
            else if (leftHand == null && ContainsAny(name, "lefthand", "left_hand", "l_hand", "hand_l") && !name.Contains("thumb") && !name.Contains("index") && !name.Contains("middle") && !name.Contains("ring") && !name.Contains("pinky"))
                leftHand = bone;

            // Right arm
            if (rightShoulder == null && ContainsAny(name, "rightshoulder", "right_shoulder", "r_shoulder", "shoulder_r", "rightclavicle"))
                rightShoulder = bone;
            else if (rightUpperArm == null && ContainsAny(name, "rightarm", "rightupperarm", "right_arm", "r_arm", "arm_r") && !name.Contains("fore") && !name.Contains("lower"))
                rightUpperArm = bone;
            else if (rightForeArm == null && ContainsAny(name, "rightforearm", "rightlowerarm", "right_forearm", "r_forearm", "forearm_r"))
                rightForeArm = bone;
            else if (rightHand == null && ContainsAny(name, "righthand", "right_hand", "r_hand", "hand_r") && !name.Contains("thumb") && !name.Contains("index") && !name.Contains("middle") && !name.Contains("ring") && !name.Contains("pinky"))
                rightHand = bone;
        }

        Debug.Log("HandIKSystem: Auto-find complete. Please verify bone assignments.");
    }

    private bool ContainsAny(string source, params string[] patterns)
    {
        foreach (string pattern in patterns)
            if (source.Contains(pattern))
                return true;
        return false;
    }

    private void CacheArmLengths()
    {
        if (leftUpperArm != null && leftForeArm != null && leftHand != null)
        {
            leftUpperArmLength = Vector3.Distance(leftUpperArm.position, leftForeArm.position);
            leftForeArmLength = Vector3.Distance(leftForeArm.position, leftHand.position);
        }

        if (rightUpperArm != null && rightForeArm != null && rightHand != null)
        {
            rightUpperArmLength = Vector3.Distance(rightUpperArm.position, rightForeArm.position);
            rightForeArmLength = Vector3.Distance(rightForeArm.position, rightHand.position);
        }
    }

    private void LateUpdate()
    {
        if (!enableHandIK)
        {
            leftHandData.currentWeight = 0f;
            rightHandData.currentWeight = 0f;
            return;
        }

        float deltaTime = Time.deltaTime;

        // Check if we should be active (interaction mode check)
        if (interactionOnlyMode)
        {
            CheckForNearbyInteractables();
            if (!isNearInteractable)
            {
                // Smoothly fade out IK
                FadeOutIK(ref leftHandData, deltaTime);
                FadeOutIK(ref rightHandData, deltaTime);
                return;
            }
        }

        // Detect surfaces for each hand
        DetectSurfaceForHand(leftUpperArm, leftHand, ref leftHandData, true);
        DetectSurfaceForHand(rightUpperArm, rightHand, ref rightHandData, false);

        // Evaluate IK weights based on distance (edge case handling)
        EvaluateIKWeight(ref leftHandData, deltaTime);
        EvaluateIKWeight(ref rightHandData, deltaTime);

        // Smooth hand positions
        SmoothHandPosition(ref leftHandData, deltaTime);
        SmoothHandPosition(ref rightHandData, deltaTime);

        // Apply IK
        if (leftHandData.currentWeight > 0.01f)
        {
            ApplyArmIK(leftUpperArm, leftForeArm, leftHand, leftUpperArmLength, leftForeArmLength,
                       leftHandData, true);
        }

        if (rightHandData.currentWeight > 0.01f)
        {
            ApplyArmIK(rightUpperArm, rightForeArm, rightHand, rightUpperArmLength, rightForeArmLength,
                       rightHandData, false);
        }
    }

    /// <summary>
    /// Checks for nearby interactable objects to determine if hand IK should be active.
    /// </summary>
    private void CheckForNearbyInteractables()
    {
        // Use overlap sphere to find interactables
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, interactableLayerMask, QueryTriggerInteraction.Collide);

        isNearInteractable = false;
        currentInteractableSurface = null;

        foreach (Collider hit in hits)
        {
            // Check if this is an interactable
            IInteractable interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanInteract())
            {
                isNearInteractable = true;
                currentInteractableSurface = hit.transform;
                break;
            }
        }
    }

    /// <summary>
    /// Detects surface in front of the character for hand placement.
    /// </summary>
    private void DetectSurfaceForHand(Transform shoulder, Transform hand, ref HandIKData handData, bool isLeftHand)
    {
        handData.hasValidSurface = false;

        if (shoulder == null || hand == null) return;

        Vector3 upDir = GravityUp;
        Vector3 characterForward = transform.forward;

        // Calculate ray origin at preferred hand height
        Vector3 rayOrigin = transform.position + upDir * preferredHandHeight;

        // Offset slightly to left or right based on hand
        Vector3 sideOffset = isLeftHand ? -transform.right * 0.2f : transform.right * 0.2f;
        rayOrigin += sideOffset;

        // Cast forward to find surface
        if (Physics.Raycast(rayOrigin, characterForward, out RaycastHit hit, detectionRadius, surfaceLayerMask))
        {
            // Check if surface is facing us (not a back face)
            float facingDot = Vector3.Dot(-hit.normal, characterForward);
            if (facingDot >= minSurfaceFacingDot)
            {
                handData.hasValidSurface = true;
                handData.surfaceNormal = hit.normal;

                // Calculate hand target position on surface
                Vector3 surfacePoint = hit.point + hit.normal * handSurfaceOffset;

                // Adjust height to be reachable
                float shoulderHeight = Vector3.Dot(shoulder.position, upDir);
                float surfacePointHeight = Vector3.Dot(surfacePoint, upDir);

                // Clamp to reachable height range
                float maxReach = leftUpperArmLength + leftForeArmLength;
                float heightDiff = Mathf.Abs(surfacePointHeight - shoulderHeight);
                if (heightDiff > maxReach * 0.8f)
                {
                    // Adjust to be within reach
                    float targetHeight = shoulderHeight;
                    surfacePoint = surfacePoint - upDir * Vector3.Dot(surfacePoint, upDir) + upDir * targetHeight;
                }

                handData.targetHandPosition = surfacePoint;
            }
        }
    }

    /// <summary>
    /// Evaluates IK weight based on distance to surface.
    /// KEY EDGE CASE FIX: Fades out IK when too close to prevent clipping.
    /// </summary>
    private void EvaluateIKWeight(ref HandIKData handData, float deltaTime)
    {
        float targetWeight = 0f;

        if (handData.hasValidSurface)
        {
            Transform shoulder = handData.targetHandPosition == leftHandData.targetHandPosition ? leftUpperArm : rightUpperArm;
            if (shoulder != null)
            {
                float distanceToSurface = Vector3.Distance(shoulder.position, handData.targetHandPosition);

                // ═══════════════════════════════════════════════════════════════════
                // EDGE CASE FIX: Distance-based IK weight
                //
                // Too close (< minimumSurfaceDistance): IK weight = 0
                //   - Character is too close, hand can't reach properly
                //   - This prevents the clipping issue
                //
                // Optimal range (minimum to activation): IK weight ramps up
                //   - Smooth transition as character approaches
                //
                // Too far (> activationDistance): IK weight = 0
                //   - Surface out of reach, no need for IK
                // ═══════════════════════════════════════════════════════════════════

                if (distanceToSurface < minimumSurfaceDistance)
                {
                    // TOO CLOSE - disable IK to prevent clipping
                    targetWeight = 0f;
                }
                else if (distanceToSurface < activationDistance)
                {
                    // OPTIMAL RANGE - calculate weight based on distance
                    float normalizedDistance = (distanceToSurface - minimumSurfaceDistance) /
                                               (activationDistance - minimumSurfaceDistance);
                    // Closer = higher weight (inverted)
                    targetWeight = 1f - normalizedDistance;
                    targetWeight = Mathf.Clamp01(targetWeight) * maxIKWeight;
                }
                else
                {
                    // TOO FAR - no IK needed
                    targetWeight = 0f;
                }
            }
        }

        handData.targetWeight = targetWeight;

        // Smooth weight transition
        handData.currentWeight = Mathf.SmoothDamp(
            handData.currentWeight,
            handData.targetWeight,
            ref handData.weightVelocity,
            ikWeightSmoothTime
        );
    }

    /// <summary>
    /// Smoothly interpolates hand position to prevent jitter.
    /// </summary>
    private void SmoothHandPosition(ref HandIKData handData, float deltaTime)
    {
        if (!handData.hasValidSurface)
        {
            // No surface - keep current smoothed position
            return;
        }

        handData.smoothedHandPosition = Vector3.SmoothDamp(
            handData.smoothedHandPosition,
            handData.targetHandPosition,
            ref handData.positionVelocity,
            handPositionSmoothTime
        );
    }

    /// <summary>
    /// Fades out IK weight when not near interactables.
    /// </summary>
    private void FadeOutIK(ref HandIKData handData, float deltaTime)
    {
        handData.targetWeight = 0f;
        handData.currentWeight = Mathf.SmoothDamp(
            handData.currentWeight,
            0f,
            ref handData.weightVelocity,
            ikWeightSmoothTime
        );
    }

    /// <summary>
    /// Applies two-bone IK to position the hand on the surface.
    /// </summary>
    private void ApplyArmIK(Transform upperArm, Transform foreArm, Transform hand,
                            float upperLen, float lowerLen, HandIKData handData, bool isLeftArm)
    {
        if (upperArm == null || foreArm == null || hand == null) return;
        if (handData.currentWeight < 0.01f) return;

        Vector3 targetPos = handData.smoothedHandPosition;
        SolveTwoBoneIK(upperArm, foreArm, hand, targetPos, upperLen, lowerLen,
                       handData.currentWeight, isLeftArm);

        // Align hand rotation to surface if we have a valid normal
        if (handData.hasValidSurface)
        {
            AlignHandToSurface(hand, handData.surfaceNormal, handData.currentWeight);
        }
    }

    /// <summary>
    /// Two-bone IK solver (adapted from FootIKSystem).
    /// </summary>
    private void SolveTwoBoneIK(Transform upperArm, Transform foreArm, Transform hand,
                                 Vector3 targetPos, float upperLen, float lowerLen, float weight,
                                 bool isLeftArm)
    {
        if (weight < 0.01f) return;

        Vector3 upperArmPos = upperArm.position;
        Vector3 toTarget = targetPos - upperArmPos;
        float targetDistance = toTarget.magnitude;

        // Calculate reach limits
        float maxReach = upperLen + lowerLen - 0.001f;
        float minReach = Mathf.Abs(upperLen - lowerLen) + 0.001f;

        // Apply soft IK
        float softDistance = ApplySoftIK(targetDistance, maxReach, softIKRatio);
        softDistance = Mathf.Max(softDistance, minReach);

        Vector3 targetDirection = toTarget.normalized;
        Vector3 clampedTarget = upperArmPos + targetDirection * softDistance;

        // Law of cosines for shoulder angle
        float cosShoulderAngle = (upperLen * upperLen + softDistance * softDistance - lowerLen * lowerLen)
                                  / (2f * upperLen * softDistance);
        cosShoulderAngle = Mathf.Clamp(cosShoulderAngle, -1f, 1f);
        float shoulderAngle = Mathf.Acos(cosShoulderAngle);

        // Calculate elbow hint direction (pole vector)
        Vector3 elbowHint = CalculateElbowHint(upperArm, targetDirection, isLeftArm);

        // Determine bend axis
        Vector3 bendAxis = Vector3.Cross(targetDirection, elbowHint);
        if (bendAxis.sqrMagnitude < 0.001f)
        {
            bendAxis = isLeftArm ? transform.up : -transform.up;
        }
        bendAxis.Normalize();

        // Calculate elbow position
        Vector3 upperToElbowDir = Quaternion.AngleAxis(-shoulderAngle * Mathf.Rad2Deg, bendAxis) * targetDirection;
        Vector3 newElbowPos = upperArmPos + upperToElbowDir * upperLen;

        // Apply upper arm rotation
        Vector3 currentUpperToElbow = foreArm.position - upperArmPos;
        Vector3 desiredUpperToElbow = newElbowPos - upperArmPos;

        if (currentUpperToElbow.sqrMagnitude > 0.001f && desiredUpperToElbow.sqrMagnitude > 0.001f)
        {
            Quaternion upperArmRotDelta = Quaternion.FromToRotation(currentUpperToElbow.normalized, desiredUpperToElbow.normalized);
            upperArm.rotation = Quaternion.Slerp(upperArm.rotation, upperArmRotDelta * upperArm.rotation, weight);
        }

        // Apply forearm rotation
        Vector3 currentElbowToHand = hand.position - foreArm.position;
        Vector3 desiredElbowToHand = clampedTarget - foreArm.position;

        if (currentElbowToHand.sqrMagnitude > 0.001f && desiredElbowToHand.sqrMagnitude > 0.001f)
        {
            Quaternion foreArmRotDelta = Quaternion.FromToRotation(currentElbowToHand.normalized, desiredElbowToHand.normalized);
            foreArm.rotation = Quaternion.Slerp(foreArm.rotation, foreArmRotDelta * foreArm.rotation, weight);
        }
    }

    /// <summary>
    /// Calculates elbow hint direction for natural arm pose.
    /// </summary>
    private Vector3 CalculateElbowHint(Transform upperArm, Vector3 targetDirection, bool isLeftArm)
    {
        // Blend between backward and outward for elbow direction
        Vector3 backward = -transform.forward;
        Vector3 outward = isLeftArm ? -transform.right : transform.right;
        Vector3 down = -GravityUp;

        // Elbows naturally point backward and slightly outward/down
        Vector3 hint = Vector3.Lerp(backward, outward, elbowOutwardBlend);
        hint = Vector3.Lerp(hint, down, 0.3f); // Add some downward bias

        return hint.normalized;
    }

    /// <summary>
    /// Aligns hand rotation to face the surface.
    /// </summary>
    private void AlignHandToSurface(Transform hand, Vector3 surfaceNormal, float weight)
    {
        // Hand forward should point into the surface (opposite of normal)
        Vector3 handForward = -surfaceNormal;

        // Hand up should generally match gravity up
        Vector3 handUp = GravityUp;

        // If surface is horizontal (floor/ceiling), adjust
        float upDot = Vector3.Dot(surfaceNormal, GravityUp);
        if (Mathf.Abs(upDot) > 0.9f)
        {
            handUp = transform.forward;
        }

        // Project hand up onto plane perpendicular to hand forward
        handUp = Vector3.ProjectOnPlane(handUp, handForward).normalized;

        if (handUp.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(handForward, handUp);
            hand.rotation = Quaternion.Slerp(hand.rotation, targetRotation, weight * 0.5f);
        }
    }

    /// <summary>
    /// Soft IK distance calculation (from FootIKSystem).
    /// </summary>
    private float ApplySoftIK(float distance, float maxReach, float softRatio)
    {
        float da = maxReach * softRatio;

        if (distance <= da)
            return distance;

        float softRange = maxReach - da;
        float x = distance - da;
        float softDistance = da + softRange * (1f - Mathf.Exp(-x / softRange));

        return softDistance;
    }

    /// <summary>
    /// Public method to notify the system of interaction start.
    /// Can be called by PlayerInteractor.
    /// </summary>
    public void OnInteractionStart(IInteractable interactable)
    {
        isNearInteractable = true;
        if (interactable is MonoBehaviour mb)
        {
            currentInteractableSurface = mb.transform;
        }
    }

    /// <summary>
    /// Public method to notify the system of interaction end.
    /// </summary>
    public void OnInteractionEnd()
    {
        // Let the system naturally fade out via distance checks
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Left arm
        if (leftUpperArm != null && leftForeArm != null && leftHand != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(leftUpperArm.position, leftForeArm.position);
            Gizmos.DrawLine(leftForeArm.position, leftHand.position);

            if (leftHandData.hasValidSurface)
            {
                Gizmos.color = new Color(0f, 1f, 0f, leftHandData.currentWeight);
                Gizmos.DrawWireSphere(leftHandData.smoothedHandPosition, 0.05f);
                Gizmos.DrawRay(leftHandData.smoothedHandPosition, leftHandData.surfaceNormal * 0.2f);
            }
        }

        // Right arm
        if (rightUpperArm != null && rightForeArm != null && rightHand != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(rightUpperArm.position, rightForeArm.position);
            Gizmos.DrawLine(rightForeArm.position, rightHand.position);

            if (rightHandData.hasValidSurface)
            {
                Gizmos.color = new Color(0f, 1f, 0f, rightHandData.currentWeight);
                Gizmos.DrawWireSphere(rightHandData.smoothedHandPosition, 0.05f);
                Gizmos.DrawRay(rightHandData.smoothedHandPosition, rightHandData.surfaceNormal * 0.2f);
            }
        }

        // Detection radius
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position + GravityUp * preferredHandHeight, detectionRadius);

        // Activation distance
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position + GravityUp * preferredHandHeight, activationDistance);

        // Minimum distance (edge case zone)
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position + GravityUp * preferredHandHeight, minimumSurfaceDistance);
    }
}
