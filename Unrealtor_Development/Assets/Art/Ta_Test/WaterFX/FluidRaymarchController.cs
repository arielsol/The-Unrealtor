using UnityEngine;

public class FluidRaymarchController : MonoBehaviour
{
    public static SPH_Compute SPH;   // 供 Feature 读取
    public SPH_Compute sph;

    void OnEnable()
    {
        SPH = sph;
    }

    void OnDisable()
    {
        if (SPH == sph) SPH = null;
    }
}
