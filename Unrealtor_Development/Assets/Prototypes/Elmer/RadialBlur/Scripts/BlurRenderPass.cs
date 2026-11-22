using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlurRenderPass : ScriptableRenderPass
{
    private Material material;
    private BlurSettings blurSettings;
    private RTHandle tempRT;

    public BlurRenderPass(Material mat)
    {
        material = mat;
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        //Debug.Log("BlurRenderPass created!");
    }

    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.postProcessEnabled)
            return;

        //Debug.Log("OnCameraSetup called");

        blurSettings = VolumeManager.instance.stack.GetComponent<BlurSettings>();

        if (blurSettings == null)
        {
            Debug.LogError("BlurSettings is NULL!");
            return;
        }

        //Debug.Log($"BlurSettings found - IsActive: {blurSettings.IsActive()}, Strength: {blurSettings.strength.value}");

        if (!blurSettings.IsActive())
            return;

        var desc = renderingData.cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;

        RenderingUtils.ReAllocateHandleIfNeeded(ref tempRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_BlurTex");
        //Debug.Log("TempRT allocated");
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        //Debug.Log("=== Execute called ===");

        if (!renderingData.cameraData.postProcessEnabled)
            return;

        if (material == null)
        {
            Debug.LogError("Material is NULL in Execute!");
            return;
        }

        if (blurSettings == null)
        {
            Debug.LogError("BlurSettings is NULL in Execute!");
            return;
        }

        if (!blurSettings.IsActive())
        {
            Debug.LogWarning($"BlurSettings not active in Execute! Strength: {blurSettings.strength.value}");
            return;
        }

        if (tempRT == null)
        {
            Debug.LogError("tempRT is NULL! Was not allocated in OnCameraSetup!");
            return;
        }

        //Debug.Log($"Applying blur: Strength={blurSettings.strength.value}, Center={blurSettings.center.value}, Samples={blurSettings.samples.value}");

        RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

        CommandBuffer cmd = CommandBufferPool.Get("Radial Blur");

        // Set shader parameters
        material.SetFloat("_BlurStrength", blurSettings.strength.value);
        material.SetVector("_Center", blurSettings.center.value);
        material.SetInt("_Samples", blurSettings.samples.value);

        // Simple approach: Use cmd.Blit directly with RTHandles
        cmd.Blit(source, tempRT, material, 0);
        cmd.Blit(tempRT, source);

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

        //Debug.Log("Blur executed successfully!");
    }

    public void Dispose()
    {
        tempRT?.Release();
        //Debug.Log("BlurRenderPass disposed");
    }
}