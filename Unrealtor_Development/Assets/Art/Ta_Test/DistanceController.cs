using UnityEngine;

public class DistanceController : MonoBehaviour
{
    public Transform target;
    public float minDist = 0f;
    public float maxDist = 10f;

    [Tooltip("When enabled, uses the assigned target transform for the shader distance effect.")]
    public bool useAssignedTarget = true;

    int _idTargetPos, _idMin, _idMax;

    void Awake()
    {
        _idTargetPos = Shader.PropertyToID("_TargetPosWS");
        _idMin = Shader.PropertyToID("_MinDist");
        _idMax = Shader.PropertyToID("_MaxDist");
    }

    void LateUpdate()
    {
        Transform effectiveTarget = useAssignedTarget ? target : null;
        if (effectiveTarget == null && Camera.main != null)
        {
            effectiveTarget = Camera.main.transform;
        }

        Vector3 p = effectiveTarget ? effectiveTarget.position : Vector3.zero;
        Shader.SetGlobalVector(_idTargetPos, new Vector4(p.x, p.y, p.z, 0));
        Shader.SetGlobalFloat(_idMin, minDist);
        Shader.SetGlobalFloat(_idMax, Mathf.Max(minDist + 1e-4f, maxDist));
    }
}
