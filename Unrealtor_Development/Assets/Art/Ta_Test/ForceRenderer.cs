using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ForceRenderer : MonoBehaviour
{
    public int rendererIndex = 1; // 在 URP Pipeline Asset > Renderer List 的序号（0 开始）

    UniversalAdditionalCameraData _uac;

    void Awake()
    {
        var cam = GetComponent<Camera>();
        if (!cam) return;

        _uac = cam.GetComponent<UniversalAdditionalCameraData>();
        if (_uac == null)
        {
            try { _uac = cam.GetUniversalAdditionalCameraData(); } catch { }
        }
    }

    void OnPreCull()
    {
        if (_uac == null) return;

        // 优先 SetRenderer(int)
        var mi = typeof(UniversalAdditionalCameraData)
            .GetMethod("SetRenderer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null) { mi.Invoke(_uac, new object[] { rendererIndex }); return; }

        // 旧版本回退：renderer 字段/属性
        var fi = typeof(UniversalAdditionalCameraData).GetField("renderer",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (fi != null) { fi.SetValue(_uac, rendererIndex); return; }

        var pi = typeof(UniversalAdditionalCameraData).GetProperty("renderer",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (pi != null && pi.CanWrite) { pi.SetValue(_uac, rendererIndex, null); return; }
    }
}
