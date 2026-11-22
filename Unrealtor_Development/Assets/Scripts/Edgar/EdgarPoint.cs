using UnityEngine;

public class EdgarPoint : MonoBehaviour
{
    [Header("ID / Identifier")]
    public string pointId;   // e.g., Intro_Start, Hint1_WallFace, End_PuzzlePose

    [Header("Optional Defaults (used when step doesn't specify)")]
    public string defaultVoId;          // Default VO to play at this point (optional)
    public string defaultAnimation;    // Default animation to play at this point (optional)
    public bool lookAtPlayerByDefault;  // Whether to look at player by default (optional)
}


