using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[ExecuteInEditMode]
[RequireComponent(typeof(Light))]
public partial class LightShafts : MonoBehaviour
{
    // ====== 静态实例列表，用于 Renderer Feature 查找 ======
    private static readonly List<LightShafts> s_Instances = new List<LightShafts>();
    public static IReadOnlyList<LightShafts> Instances => s_Instances;

    [Header("Target Light")]
    public Light targetLight;

    [Header("Effect Settings")]
    [Range(0f, 5f)]
    public float intensity = 1.0f;

    [Range(0.1f, 1.5f)]
    public float maxRadius = 0.75f;

    [Range(0.6f, 1.0f)]
    public float decay = 0.95f;

    [Range(8, 128)]
    public int sampleCount = 64;

    [Tooltip("Use light color as shaft color")]
    public bool useLightColor = true;

    [ColorUsage(false, true)]
    public Color shaftTint = Color.white;

    [Header("Shader")]
    [Tooltip("If left empty, will try to Shader.Find(\"Custom/URP/LightShaftsRadial\")")]
    public Shader effectShader;

    Material _material;
    public Material EffectMaterial => _material;
    [Header("Energy Filter")]
    [Range(0f, 2f)] public float threshold = 0.85f;
    [Range(0f, 2f)] public float softness = 0.20f;
    [Range(0f, 1f)] public float edgeFade = 0.15f;
    void OnEnable()
    {
        if (!s_Instances.Contains(this))
            s_Instances.Add(this);

        if (targetLight == null)
            targetLight = GetComponent<Light>();

        EnsureMaterial();
    }

    void OnDisable()
    {
        s_Instances.Remove(this);

        if (_material != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_material);
            else
#endif
                Destroy(_material);
        }
        _material = null;
    }

    void EnsureMaterial()
    {
        if (effectShader == null)
        {
            effectShader = Shader.Find("Custom/URP/LightShaftsRadial");
        }

        if (effectShader != null && (_material == null || _material.shader != effectShader))
        {
            if (_material != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(_material);
                else
#endif
                    Destroy(_material);
            }

            _material = new Material(effectShader);
            _material.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    /// <summary>
    /// 当前相机是否应该渲染这个体积光
    /// </summary>
    public bool IsCameraSupported(Camera cam)
{
    if (!isActiveAndEnabled) return false;
    if (targetLight == null) return false;
    if (cam == null) return false;

    // 只在 Game View 的主相机上渲染（避免 SceneView、预览窗口等）
    if (cam.cameraType != CameraType.Game)
        return false;

    // 可选：只对带 MainCamera 标签的相机生效
    if (!cam.CompareTag("MainCamera"))
        return false;

    return true;
}

    /// <summary>
    /// 每帧由 RenderPass 调用，用来设置材质参数
    /// </summary>
    public void SetupMaterialForCamera(Camera cam)
    {
        EnsureMaterial();
        if (_material == null || targetLight == null || cam == null) return;

        Vector3 screenPos;

        
        if (targetLight.type == LightType.Directional)
        {
            
            Vector3 farPos = cam.transform.position - targetLight.transform.forward * 1000f;
            screenPos = cam.WorldToViewportPoint(farPos);
        }
        else
        {
            
            screenPos = cam.WorldToViewportPoint(targetLight.transform.position);
        }

        
        if (screenPos.z <= 0f)
        {
            _material.SetVector("_LightScreenPos", new Vector4(0, 0, -1, 0));
        }
        else
        {
            _material.SetVector("_LightScreenPos", new Vector4(screenPos.x, screenPos.y, screenPos.z, 1));
        }

        _material.SetFloat("_Intensity", intensity);
        _material.SetFloat("_MaxRadius", maxRadius);
        _material.SetFloat("_Decay", decay);
        _material.SetInt("_SampleCount", Mathf.Clamp(sampleCount, 1, 128));

        Color c = useLightColor ? targetLight.color : shaftTint;
        c *= targetLight.intensity;
        _material.SetColor("_ShaftColor", c);

        _material.SetFloat("_Threshold", threshold);
        _material.SetFloat("_Softness", softness);
        _material.SetFloat("_EdgeFade", edgeFade);
    }
}
