using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PostProcessBW : MonoBehaviour
{
    private Material _material;
    public Shader _shader;

    void Start()
    {
        if (_shader != null)
        {
            _material = new Material(_shader) { hideFlags = HideFlags.DontSave };
        }
    }

    // 当摄像机完成图像渲染时调用
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination);
    }

   
}
