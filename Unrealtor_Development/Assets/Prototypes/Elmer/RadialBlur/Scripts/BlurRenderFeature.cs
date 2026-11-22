using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlurRenderFeature : ScriptableRendererFeature
{
    public Shader blurShader;
    private BlurRenderPass renderPass;
    private Material material;

    public override void Create()
    {
        //Debug.Log("BlurRenderFeature Create called");

        if (blurShader != null)
        {
            material = CoreUtils.CreateEngineMaterial(blurShader);
            renderPass = new BlurRenderPass(material);
            //Debug.Log("Material and RenderPass created successfully");
        }
        else
        {
            //Debug.LogError("Blur shader is NULL in Create!");
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        //Debug.Log("AddRenderPasses called");

        if (renderPass == null || material == null)
        {
            Debug.LogError($"Cannot add render pass - renderPass: {renderPass != null}, material: {material != null}");
            return;
        }

        //renderPass.SetSource(renderer.cameraColorTargetHandle);
        renderer.EnqueuePass(renderPass);

        //Debug.Log("Render pass enqueued!");
    }

    protected override void Dispose(bool disposing)
    {
        //Debug.Log("BlurRenderFeature disposed");
        renderPass?.Dispose();
        CoreUtils.Destroy(material);
    }
}