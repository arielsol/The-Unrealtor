using UnityEngine;

public class ExtinguisherPuzzleController: MatchBase
{
    [Header("Extinguisher")]
    [Header("Groups")]
    [SerializeField] private ExtinguisherFragmentGroup leftGroup;
    [SerializeField] private ExtinguisherFragmentGroup rightGroup;

    [Header("Player Zones")]
    [SerializeField] private PuzzlePlayerZone leftZone;
    [SerializeField] private PuzzlePlayerZone rightZone;
    [SerializeField] private PuzzlePlayerZone leftMatchZone;
    [SerializeField] private PuzzlePlayerZone rightMatchZone;

    [Header("Facing Check")]
    [SerializeField] private Transform referencePlane;
    [SerializeField] private Transform leftReferencePlane;
    [SerializeField] private Transform rightReferencePlane;
    private Camera leftCamera;
    private Camera rightCamera;

    [Header("Solve Settings")]
    [SerializeField] private float holdTimeSeconds = 1.0f;
    

    private float holdTimer = 0f;
    private bool alreadyMatched = false;

    void Start()
    {
        if (leftCamera == null || rightCamera == null)
        {
            var match = MatchManager.Instance;
            if (match != null)
            {
                if (leftCamera == null) leftCamera = CameraPrefabContext.Instance.LeftCinemachineBrain.GetComponent<Camera>();
                if (rightCamera == null) rightCamera = CameraPrefabContext.Instance.RightCinemachineBrain.GetComponent<Camera>();
            }
        }

        if (leftGroup != null) leftGroup.ConfigureIdleMotions();
        if (rightGroup != null) rightGroup.ConfigureIdleMotions();
    }

    void Update()
    {
        if (isSolved){
            return;
        }

        Transform leftPlaneToUse = referencePlane;
        if (leftReferencePlane != null){
            leftPlaneToUse = leftReferencePlane;
        }

        Transform rightPlaneToUse = referencePlane;
        if (rightReferencePlane != null)
        {
            rightPlaneToUse = rightReferencePlane;
        }
        bool leftAssemble = leftZone != null && leftZone.IsPlayerInside;
        bool rightAssemble = rightZone != null && rightZone.IsPlayerInside;

        bool leftReady = leftZone != null && leftMatchZone.IsPlayerInside && IsFacingPlaneNormal(leftCamera, leftPlaneToUse, leftFacingAngleThreshold);
        bool rightReady = rightZone != null && rightMatchZone.IsPlayerInside && IsFacingPlaneNormal(rightCamera, rightPlaneToUse, rightFacingAngleThreshold);

        if(leftAssemble){
            leftGroup.SetAssembleState(true);
        }
        else{
            leftGroup.SetAssembleState(false);
        }
        if(rightAssemble){
            rightGroup.SetAssembleState(true);
        }
        else{
            rightGroup.SetAssembleState(false);
        }

        if(leftReady && rightReady && alreadyMatched == false){
            Debug.Log("Valid Match");
            MatchManager.Instance.ValidMatchFound(this);
            MatchManager.Instance.SendMatchCompleteEvent();
            alreadyMatched = true;
            
        }
           
        }

        
    

    private bool IsFacingPlaneNormal(Camera cam, Transform plane, float angleThreshold)
    {
        if (cam == null || plane == null){
            Debug.LogError("Camera or plane is null");
            return false;
        }
        Vector3 planeNormal = -plane.up;
        float dot = Vector3.Dot(cam.transform.forward.normalized, planeNormal);
        float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
        return angle <= angleThreshold;
    }
}


