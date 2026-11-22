using System.Collections.Generic;
using UnityEngine;

public class GravitySwitchZone : MonoBehaviour
{
    public enum StairsType
    {
        C_BEDROOM,
        C_BEDROOM1,
        C_BEDROOM2,
        C_BEDROOM3,
        ULDR_5,
        ULDR_6,
        ULDR_7,
        ULDR_11,
        ULDR_13,
        ULDR_14,
        ULDR_15,
        ULDR_16,
        ULDR_17,
        ULDR_18,
        ULDR_19,
        ULDR_20,
        ULDR_21,
        ULDR_22,
        ULDR_23,
        ULDR_24,
        ULDR_127,
        NONE,
        Extinguisher
    }

    [SerializeField] public StairsType ThisStairsType;
    
    [Header("Stair Path (Bottom → Top)")]
    [SerializeField] private Transform startPoint;   // Bottom of the stairs
    [SerializeField] private Transform endPoint;     // Top of the stairs

    [Header("Gravity Directions")]
    [Tooltip("Gravity when going bottom→top")] [SerializeField] private Vector3 bottomToTopGravity = Vector3.down;  // Gravity when going bottom→top
    [Tooltip("Gravity when going top→bottom")] [SerializeField] private Vector3 topToBottomGravity = Vector3.up;    // Gravity when going top→bottom

    [Header("Entry Rules")]
    [SerializeField] private LayerMask playerLayer = ~0;
    [SerializeField] private float minForwardSpeed = 0.1f;          // Required speed along stair direction
    [SerializeField] private float entryDistanceFromStart = 1.0f;   // How far from start we still accept entry
    [Range(0f, 1f)]
    [SerializeField] private float activationDistanceFromEndPercent = 0.1f; // % of path length from end

    [Header("Path blend")]
    [Tooltip("From the moment you enter with a valid direction, gravity Slerps from entry to target along the full path to the far end.")]
    [SerializeField] private bool usePathBlend = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color pathColor = Color.cyan;
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private bool debugLogs = false;

    private readonly Dictionary<BasePlayerController, int> entryDirection =
        new Dictionary<BasePlayerController, int>(); // 1 = bottom→top, -1 = top→bottom, 0 = invalid
    private readonly Dictionary<BasePlayerController, bool> gravityApplied =
        new Dictionary<BasePlayerController, bool>();
    private readonly Dictionary<BasePlayerController, Vector3> gravityAtEntry =
        new Dictionary<BasePlayerController, Vector3>();
    private readonly Dictionary<BasePlayerController, float> pathTAtEntry =
        new Dictionary<BasePlayerController, float>();
    private readonly HashSet<BasePlayerController> fallPreventionSuppressedControllers =
        new HashSet<BasePlayerController>();

    private Vector3 cachedDir;
    private float cachedLength;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        if (bottomToTopGravity != Vector3.zero)
        {
            bottomToTopGravity = bottomToTopGravity.normalized;
        }
        if (topToBottomGravity != Vector3.zero)
        {
            topToBottomGravity = topToBottomGravity.normalized;
        }

        activationDistanceFromEndPercent = Mathf.Clamp01(activationDistanceFromEndPercent);
        RecalculatePath();
    }

    private void Awake()
    {
        RecalculatePath();
    }

    private void RecalculatePath()
    {
        if (startPoint != null && endPoint != null)
        {
            cachedDir = endPoint.position - startPoint.position;
            cachedLength = cachedDir.magnitude;
            if (cachedLength > 0.0001f)
            {
                cachedDir /= cachedLength;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        BasePlayerController controller = FindPlayerController(other);
        if (controller == null) return;
        if (!IsAllowedByLayer(other.gameObject.layer)) return;
        if (!IsPathValid()) return;

        Vector3 position = GetControllerPosition(controller);
        Vector3 velocity = GetControllerVelocity(controller);
        velocity.Normalize();

        float t = GetDistanceAlongPath(position); // 0 at start, ~length at end
        float forwardDot = Vector3.Dot(velocity, cachedDir);
        float backwardDot = Vector3.Dot(velocity, -cachedDir);

        int direction = 0;
        
        // Check for entry from bottom (moving toward top)
        if (t <= entryDistanceFromStart && forwardDot > minForwardSpeed)
        {
            direction = 1; // bottom→top
        }
        // Check for entry from top (moving toward bottom)
        else if (t >= cachedLength - entryDistanceFromStart && backwardDot > minForwardSpeed)
        {
            direction = -1; // top→bottom
        }
        
        entryDirection[controller] = direction;
        gravityApplied[controller] = false;
        if (direction != 0)
        {
            gravityAtEntry[controller] = controller.GravityDirection.normalized;
            pathTAtEntry[controller] = Mathf.Clamp(t, 0f, cachedLength);
            if (!fallPreventionSuppressedControllers.Contains(controller))
            {
                controller.SetFallPreventionSuppressed(true);
                fallPreventionSuppressedControllers.Add(controller);
            }
        }

        DebugLog(
            $"Enter: t={t:F3}, forwardDot={forwardDot:F3}, backwardDot={backwardDot:F3}, direction={direction}",
            controller);
    }

    private void OnTriggerStay(Collider other)
    {
        BasePlayerController controller = FindPlayerController(other);
        if (controller == null) return;
        if (!IsAllowedByLayer(other.gameObject.layer)) return;
        if (!IsPathValid()) return;

        int direction;
        if (!entryDirection.TryGetValue(controller, out direction) || direction == 0)
        {
            // This player did not enter correctly from either end
            if (debugLogs)
            {
                Vector3 debugPosition = GetControllerPosition(controller);
                float debugT = GetDistanceAlongPath(debugPosition);
                DebugLog(
                    $"Stay ignored: direction=0. t={debugT:F3}, startTol={entryDistanceFromStart:F3}, endTol={cachedLength - entryDistanceFromStart:F3}",
                    controller);
            }
            return;
        }

        bool alreadySwitched = false;
        gravityApplied.TryGetValue(controller, out alreadySwitched);
        if (!usePathBlend && alreadySwitched) return;

        Vector3 position = GetControllerPosition(controller);
        float t = Mathf.Clamp(GetDistanceAlongPath(position), 0f, cachedLength);
        float activationDistanceFromEnd = GetActivationDistanceFromEnd();
        float dt = Time.fixedDeltaTime;

        if (usePathBlend)
        {
            if (!gravityAtEntry.TryGetValue(controller, out Vector3 fromGrav))
            {
                fromGrav = controller.GravityDirection.normalized;
            }

            if (!pathTAtEntry.TryGetValue(controller, out float tEntry))
            {
                tEntry = direction == 1 ? 0f : cachedLength;
            }

            if (direction == 1)
            {
                Vector3 toGrav = NormalizeGravity(bottomToTopGravity);
                float span = Mathf.Max(cachedLength - tEntry, 1e-4f);
                float alpha = Mathf.Clamp01((t - tEntry) / span);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                Vector3 g = Vector3.Slerp(fromGrav, toGrav, alpha);
                controller.SetBlendedGravityDirection(g, dt);
                DebugLog($"Stay blend b->t: t={t:F3}, tEntry={tEntry:F3}, alpha={alpha:F3}", controller);
            }
            else if (direction == -1)
            {
                Vector3 toGrav = NormalizeGravity(topToBottomGravity);
                float span = Mathf.Max(tEntry, 1e-4f);
                float alpha = Mathf.Clamp01((tEntry - t) / span);
                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                Vector3 g = Vector3.Slerp(fromGrav, toGrav, alpha);
                controller.SetBlendedGravityDirection(g, dt);
                DebugLog($"Stay blend t->b: t={t:F3}, tEntry={tEntry:F3}, alpha={alpha:F3}", controller);
            }

            return;
        }

        if (alreadySwitched) return;

        if (direction == 1)
        {
            if (t >= cachedLength - activationDistanceFromEnd)
            {
                ApplyGravity(controller, bottomToTopGravity);
            }
            else
            {
                DebugLog(
                    $"Stay bottom->top: t={t:F3}, need >= {(cachedLength - activationDistanceFromEnd):F3}",
                    controller);
            }
        }
        else if (direction == -1)
        {
            if (t <= activationDistanceFromEnd)
            {
                ApplyGravity(controller, topToBottomGravity);
            }
            else
            {
                DebugLog(
                    $"Stay top->bottom: t={t:F3}, need <= {activationDistanceFromEnd:F3}",
                    controller);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        BasePlayerController controller = FindPlayerController(other);
        if (controller == null) return;

        if (usePathBlend &&
            entryDirection.TryGetValue(controller, out int direction) &&
            direction != 0 &&
            IsPathValid())
        {
            Vector3 resolvedGravity = controller.GravityDirection;
            if (gravityAtEntry.TryGetValue(controller, out Vector3 entryGravity))
            {
                resolvedGravity = NormalizeGravity(entryGravity);
            }

            float t = Mathf.Clamp(GetDistanceAlongPath(GetControllerPosition(controller)), 0f, cachedLength);
            float midpoint = cachedLength * 0.5f;

            if (direction == 1 && t >= midpoint)
            {
                resolvedGravity = NormalizeGravity(bottomToTopGravity);
            }
            else if (direction == -1 && t <= midpoint)
            {
                resolvedGravity = NormalizeGravity(topToBottomGravity);
            }

            controller.SwitchGravity(resolvedGravity);
        }

        entryDirection.Remove(controller);
        gravityApplied.Remove(controller);
        gravityAtEntry.Remove(controller);
        pathTAtEntry.Remove(controller);
        if (fallPreventionSuppressedControllers.Remove(controller))
        {
            controller.SetFallPreventionSuppressed(false);
        }
    }

    private void OnDisable()
    {
        foreach (BasePlayerController controller in fallPreventionSuppressedControllers)
        {
            if (controller != null)
            {
                controller.SetFallPreventionSuppressed(false);
            }
        }
        fallPreventionSuppressedControllers.Clear();
    }

    private static Vector3 NormalizeGravity(Vector3 g)
    {
        if (g.sqrMagnitude < 1e-8f) return Vector3.down;
        return g.normalized;
    }

    private void ApplyGravity(BasePlayerController controller, Vector3 gravityDirection)
    {
        Vector3 dir = gravityDirection;
        if (dir == Vector3.zero)
        {
            dir = Vector3.down;
        }

        controller.SwitchGravity(dir.normalized);
        gravityApplied[controller] = true;
        MusicManager.Instance?.PlaySFX(SoundID.GravityStairsUsed, Vector3.zero);
    }

    private void DebugLog(string message, BasePlayerController controller)
    {
        if (!debugLogs) return;
        Debug.Log($"[GravitySwitchZone] {message}", controller);
    }

    private BasePlayerController FindPlayerController(Collider other)
    {
        BasePlayerController controller = other.GetComponent<BasePlayerController>();
        if (controller == null)
        {
            controller = other.GetComponentInParent<BasePlayerController>();
        }
        return controller;
    }

    private bool IsAllowedByLayer(int layer)
    {
        return (playerLayer.value & (1 << layer)) != 0;
    }

    private bool IsPathValid()
    {
        return startPoint != null && endPoint != null && cachedLength > 0.0001f;
    }

    private Vector3 GetControllerPosition(BasePlayerController controller)
    {
        if (controller.Rigidbody != null)
        {
            return controller.Rigidbody.position;
        }
        return controller.transform.position;
    }

    private Vector3 GetControllerVelocity(BasePlayerController controller)
    {
        if (controller.Rigidbody != null)
        {
            return controller.Rigidbody.linearVelocity;
        }
        // Fallback when no rigidbody: use forward as "movement direction"
        return controller.transform.forward;
    }

    private float GetDistanceAlongPath(Vector3 worldPos)
    {
        if (!IsPathValid()) return 0f;
        Vector3 fromStart = worldPos - startPoint.position;
        return Vector3.Dot(fromStart, cachedDir);
    }

    private float GetActivationDistanceFromEnd()
    {
        return Mathf.Clamp01(activationDistanceFromEndPercent) * cachedLength;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (startPoint == null || endPoint == null) return;

        RecalculatePath();

        Gizmos.color = pathColor;
        Gizmos.DrawLine(startPoint.position, endPoint.position);
        Gizmos.DrawSphere(startPoint.position, 0.1f);
        Gizmos.DrawSphere(endPoint.position, 0.1f);

        // Draw gravity direction for bottom→top travel (at end point)
        Vector3 bottomToTopDir = bottomToTopGravity != Vector3.zero ? bottomToTopGravity : Vector3.down;
        Gizmos.color = activeColor;
        Gizmos.DrawRay(endPoint.position, bottomToTopDir.normalized * 0.5f);
        
        // Draw gravity direction for top→bottom travel (at start point)
        Vector3 topToBottomDir = topToBottomGravity != Vector3.zero ? topToBottomGravity : Vector3.up;
        Gizmos.color = Color.yellow; // Different color for reverse direction
        Gizmos.DrawRay(startPoint.position, topToBottomDir.normalized * 0.5f);
    }
}