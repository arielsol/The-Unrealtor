using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class GetCharacterVectors : MonoBehaviour
{
    List<Renderer> mRenderers = new List<Renderer>();
    Bounds tempBound;
    [HideInInspector] public Bounds combinedBounds;
    [Range(0, 1)] public float shadowBias = 0.0001f;
    [Range(0, 20)] public float shadowSoftWeight = 0;
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(combinedBounds.min, 0.1f);
        Gizmos.DrawWireSphere(combinedBounds.max, 0.1f);
    }
    // Start is called before the first frame update
    void Start()
    {   
        mRenderers = GetComponentsInChildren<Renderer>().ToList();
    }

    // Update is called once per frame
    void Update()
    {
        Shader.SetGlobalVector("_Forward", transform.forward);
        Shader.SetGlobalVector("_Up", transform.up);
        Shader.SetGlobalVector("_Pos", transform.position);
        GetCombinedBounds();
        Matrix4x4 viewMatrix = ComputeViewMatrix(combinedBounds);
        Matrix4x4 projMatrix = ComputeProjectionMatrix(combinedBounds, viewMatrix);
    }

    public Bounds GetCombinedBounds()
    {
        tempBound = new Bounds(gameObject.transform.position, Vector3.zero);
        combinedBounds = tempBound;
        foreach (var renderer in mRenderers)
        {
            combinedBounds.Encapsulate(renderer.bounds);
        }

        //enlarge a bit
        combinedBounds.size += new Vector3(0.1f, 0.1f, 0.1f);
        return combinedBounds;
    }

    Matrix4x4 ComputeViewMatrix(Bounds bounds)
    {
        Light mainLight = FindFirstObjectByType<Light>();
        Matrix4x4 viewMatrix = Matrix4x4.Inverse(Matrix4x4.TRS(bounds.center, mainLight.transform.rotation, new Vector3(1, 1, 1)));
        return viewMatrix;
    }

    Matrix4x4 ComputeProjectionMatrix(Bounds bounds, Matrix4x4 viewMatrix)
    {
        Vector3 shadowMin = viewMatrix.MultiplyPoint(bounds.min);
        Vector3 shadowMax = viewMatrix.MultiplyPoint(bounds.max);
        float left = shadowMin.x;
        float right = shadowMax.x;
        float bottom = shadowMin.y;
        float top = shadowMax.y;
        float zNear = shadowMin.z;
        float zFar = shadowMax.z;
        Matrix4x4 projMatrix = Matrix4x4.Ortho(left, right, bottom, top, zNear, zFar);
        return projMatrix;
    }
}
