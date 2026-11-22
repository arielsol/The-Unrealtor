using System;
using System.Collections.Generic;
using UnityEngine;

public enum EdgarActionType
{
    // Movement / Position
    GoToPoint,              // Walk to a point using NavMesh
    SnapToPoint,            // Teleport/instant move to a point (no movement animation)

    // Appearance / Visibility
    ShowFullBodyAtPoint,    // Show full body at a point (uses point.role to determine behavior)
    ShowFaceAtPoint,        // Show face/upper body at a point (wall face appearance)
    HideEdgar,             // Hide Edgar (or play disappear animation then hide)

    // VO / Audio
    PlayVO,                 // Play a VO line (can use point.defaultVoId if step.voId is empty)

    // Animation / Pose
    PlayAnimation,          // Play an Animator Trigger
    PlayPointDefaultAnimation, // Play the point's default animation (if point.defaultAnimation exists)

    // Look / Orientation
    LookAtPlayer,           // Turn to face player
    LookAtPoint,            // Turn to face a specific point
    RotateEdgar,            // Rotate Edgar to a specific facing direction (Y rotation in degrees)
    FacePlayer,             // Find nearest player and look at it

    // State / Timing
    SetTalking,             // Toggle talking state (for lip sync)
    WaitSeconds             // Wait for specified time
}

[Serializable]
public class EdgarStep
{
    [Header("Action Type")]
    public EdgarActionType actionType;

    [Header("Point Reference (required for some actions, optional for others)")]
    [EdgarPointId]
    public string pointId;  // Reference to an EdgarPoint's pointId

    [Header("VO (for PlayVO)")]
    public string voId;     // If empty, will try to use point.defaultVoId

    [Header("Animation (for PlayAnimation)")]
    public string animationTrigger;  // e.g., "Punch", "SpecialPose", "Sink"

    [Header("Talking State (for SetTalking)")]
    public bool talking;

    [Header("Wait Time (for WaitSeconds)")]
    public float waitSeconds = 1.0f;

    [Header("Rotation (for RotateEdgar)")]
    public float rotationY = 0.0f;  // Y rotation in degrees (0 = forward, 90 = right, etc.)
}


