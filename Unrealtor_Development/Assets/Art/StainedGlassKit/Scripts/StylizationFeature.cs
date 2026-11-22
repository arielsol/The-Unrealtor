using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Scriptable Renderer Feature for fullscreen stylization.
/// Adds anime-style outlines, posterization, saturation boost,
/// halftone shadow patterns, and film grain.
///
/// Setup:
/// 1. Add this feature to your URP Renderer (Universal Renderer Data asset)
/// 2. Add a Volume with StylizationVolume component to control settings
/// 3. Ensure "Depth Texture" is enabled in your URP Pipeline Asset
/// </summary>
[System.Serializable]
public class StylizationFeature : ScriptableRendererFeature
{
    [Tooltip("Assign the Stylization shader. If empty, auto-finds 'Hidden/StainedGlass/Stylization'.")]
    public Shader stylizationShader;

    private StylizationPass stylizationPass;
    private Material stylizationMaterial;

    public override void Create()
    {
        if (stylizationShader == null)
            stylizationShader = Shader.Find("Hidden/StainedGlass/Stylization");

        if (stylizationShader != null && stylizationMaterial == null)
            stylizationMaterial = CoreUtils.CreateEngineMaterial(stylizationShader);

        stylizationPass = new StylizationPass(stylizationMaterial);
        stylizationPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (stylizationMaterial == null || stylizationPass == null) return;

        // Skip for preview cameras, reflection probes, etc.
        if (renderingData.cameraData.cameraType != CameraType.Game &&
            renderingData.cameraData.cameraType != CameraType.SceneView)
            return;

        // Check if stylization is active in the Volume stack
        var stack = VolumeManager.instance.stack;
        var volume = stack.GetComponent<StylizationVolume>();
        if (volume == null || !volume.IsActive()) return;

        // Request depth + normals textures from URP (needed for edge detection)
        stylizationPass.ConfigureInput(
            ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);

        stylizationPass.Setup(volume);
        renderer.EnqueuePass(stylizationPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && stylizationMaterial != null)
        {
            CoreUtils.Destroy(stylizationMaterial);
            stylizationMaterial = null;
        }
    }

    // =========================================================================
    // The actual render pass
    // =========================================================================
    class StylizationPass : ScriptableRenderPass
    {
        private Material material;
        private StylizationVolume volume;
        private static readonly int TempTexId = Shader.PropertyToID("_StylizationTemp");

        // Shader property IDs
        private static readonly int PropIntensity           = Shader.PropertyToID("_StylIntensity");
        private static readonly int PropOutlineColor        = Shader.PropertyToID("_StylOutlineColor");
        private static readonly int PropOutlineWidth        = Shader.PropertyToID("_StylOutlineWidth");
        private static readonly int PropDepthSens           = Shader.PropertyToID("_StylDepthSens");
        private static readonly int PropNormalSens          = Shader.PropertyToID("_StylNormalSens");
        private static readonly int PropDepthThresh         = Shader.PropertyToID("_StylDepthThresh");
        private static readonly int PropNormalThresh        = Shader.PropertyToID("_StylNormalThresh");
        private static readonly int PropPosterLevels        = Shader.PropertyToID("_StylPosterLevels");
        private static readonly int PropPosterStr           = Shader.PropertyToID("_StylPosterStr");
        private static readonly int PropSatBoost            = Shader.PropertyToID("_StylSatBoost");
        private static readonly int PropNoiseStr            = Shader.PropertyToID("_StylNoiseStr");
        private static readonly int PropNoiseScale          = Shader.PropertyToID("_StylNoiseScale");
        private static readonly int PropNoiseSpeed          = Shader.PropertyToID("_StylNoiseSpeed");
        private static readonly int PropHalftoneStr         = Shader.PropertyToID("_StylHalftoneStr");
        private static readonly int PropHalftoneScale       = Shader.PropertyToID("_StylHalftoneScale");
        private static readonly int PropHalftoneShadowThresh = Shader.PropertyToID("_StylHalftoneShadowThresh");
        private static readonly int PropOutlineFadeStart    = Shader.PropertyToID("_StylOutlineFadeStart");
        private static readonly int PropOutlineFadeEnd      = Shader.PropertyToID("_StylOutlineFadeEnd");
        private static readonly int PropRimColor            = Shader.PropertyToID("_StylRimColor");
        private static readonly int PropRimIntensity        = Shader.PropertyToID("_StylRimIntensity");
        private static readonly int PropRimPower            = Shader.PropertyToID("_StylRimPower");

        public StylizationPass(Material mat)
        {
            material = mat;
            profilingSampler = new ProfilingSampler("Stylization");
        }

        public void Setup(StylizationVolume vol)
        {
            volume = vol;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || volume == null) return;

            var cmd = CommandBufferPool.Get("Stylization");

            // Push all volume parameters to shader
            material.SetFloat(PropIntensity,           volume.intensity.value);
            material.SetColor(PropOutlineColor,        volume.outlineColor.value);
            material.SetFloat(PropOutlineWidth,        volume.outlineWidth.value);
            material.SetFloat(PropDepthSens,           volume.depthSensitivity.value);
            material.SetFloat(PropNormalSens,          volume.normalSensitivity.value);
            material.SetFloat(PropDepthThresh,         volume.depthThreshold.value);
            material.SetFloat(PropNormalThresh,        volume.normalThreshold.value);
            material.SetFloat(PropPosterLevels,        volume.posterizeLevels.value);
            material.SetFloat(PropPosterStr,           volume.posterizeStrength.value);
            material.SetFloat(PropSatBoost,            volume.saturationBoost.value);
            material.SetFloat(PropNoiseStr,            volume.noiseStrength.value);
            material.SetFloat(PropNoiseScale,          volume.noiseScale.value);
            material.SetFloat(PropNoiseSpeed,          volume.noiseSpeed.value);
            material.SetFloat(PropHalftoneStr,         volume.halftoneStrength.value);
            material.SetFloat(PropHalftoneScale,       volume.halftoneScale.value);
            material.SetFloat(PropHalftoneShadowThresh, volume.halftoneShadowThreshold.value);
            material.SetFloat(PropOutlineFadeStart,    volume.outlineFadeStart.value);
            material.SetFloat(PropOutlineFadeEnd,      volume.outlineFadeEnd.value);
            material.SetColor(PropRimColor,            volume.rimColor.value);
            material.SetFloat(PropRimIntensity,        volume.rimIntensity.value);
            material.SetFloat(PropRimPower,            volume.rimPower.value);

            // Ping-pong blit
            var cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            cmd.GetTemporaryRT(TempTexId, descriptor, FilterMode.Bilinear);
            cmd.Blit(cameraTarget, TempTexId);
            cmd.Blit(TempTexId, cameraTarget, material);
            cmd.ReleaseTemporaryRT(TempTexId);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}
