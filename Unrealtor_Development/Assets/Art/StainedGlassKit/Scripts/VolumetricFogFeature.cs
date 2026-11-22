using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Scriptable Renderer Feature for volumetric fog.
/// Performs screen-space ray marching using shadow maps to create
/// god rays, volumetric light shafts, and colored fog from any light source.
///
/// Setup:
/// 1. Add this feature to your URP Renderer (Universal Renderer Data asset)
/// 2. Add a Volume with VolumetricFogVolume component to control settings
/// 3. Ensure "Depth Texture" is enabled in your URP Pipeline Asset
/// </summary>
[System.Serializable]
public class VolumetricFogFeature : ScriptableRendererFeature
{
    [Tooltip("Assign the VolumetricFog shader. If empty, auto-finds 'Hidden/StainedGlass/VolumetricFog'.")]
    public Shader fogShader;

    private VolumetricFogPass fogPass;
    private Material fogMaterial;

    public override void Create()
    {
        if (fogShader == null)
            fogShader = Shader.Find("Hidden/StainedGlass/VolumetricFog");

        if (fogShader != null && fogMaterial == null)
            fogMaterial = CoreUtils.CreateEngineMaterial(fogShader);

        fogPass = new VolumetricFogPass(fogMaterial);
        fogPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (fogMaterial == null || fogPass == null) return;

        // Skip for preview cameras, reflection probes, etc.
        if (renderingData.cameraData.cameraType != CameraType.Game &&
            renderingData.cameraData.cameraType != CameraType.SceneView)
            return;

        // Check if volumetric fog is active in the Volume stack
        var stack = VolumeManager.instance.stack;
        var fogVolume = stack.GetComponent<VolumetricFogVolume>();
        if (fogVolume == null || !fogVolume.IsActive()) return;

        fogPass.Setup(fogVolume);
        renderer.EnqueuePass(fogPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && fogMaterial != null)
        {
            CoreUtils.Destroy(fogMaterial);
            fogMaterial = null;
        }
    }

    // =========================================================================
    // The actual render pass
    // =========================================================================
    class VolumetricFogPass : ScriptableRenderPass
    {
        private Material material;
        private VolumetricFogVolume volume;
        private static readonly int TempTexId = Shader.PropertyToID("_VolumetricFogTemp");

        // Shader property IDs
        private static readonly int PropDensity = Shader.PropertyToID("_VFogDensity");
        private static readonly int PropMaxDistance = Shader.PropertyToID("_VFogMaxDistance");
        private static readonly int PropAnisotropy = Shader.PropertyToID("_VFogAnisotropy");
        private static readonly int PropSteps = Shader.PropertyToID("_VFogSteps");
        private static readonly int PropLightIntensity = Shader.PropertyToID("_VFogLightIntensity");
        private static readonly int PropFogColor = Shader.PropertyToID("_VFogColor");
        private static readonly int PropHeightFalloff = Shader.PropertyToID("_VFogHeightFalloff");
        private static readonly int PropBaseHeight = Shader.PropertyToID("_VFogBaseHeight");
        private static readonly int PropNoiseScale = Shader.PropertyToID("_VFogNoiseScale");
        private static readonly int PropNoiseIntensity = Shader.PropertyToID("_VFogNoiseIntensity");
        private static readonly int PropJitter = Shader.PropertyToID("_VFogJitter");
        private static readonly int PropAdditionalIntensity = Shader.PropertyToID("_VFogAdditionalIntensity");
        private static readonly int PropAmbientIntensity = Shader.PropertyToID("_VFogAmbientIntensity");
        private static readonly int PropMaxAdditionalLights = Shader.PropertyToID("_VFogMaxAdditionalLights");

        public VolumetricFogPass(Material mat)
        {
            material = mat;
            profilingSampler = new ProfilingSampler("VolumetricFog");
        }

        public void Setup(VolumetricFogVolume vol)
        {
            volume = vol;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || volume == null) return;

            var cmd = CommandBufferPool.Get("VolumetricFog");

            // Set fog parameters from Volume
            material.SetFloat(PropDensity, volume.density.value);
            material.SetFloat(PropMaxDistance, volume.maxDistance.value);
            material.SetFloat(PropAnisotropy, volume.scatteringAnisotropy.value);
            material.SetFloat(PropSteps, volume.steps.value);
            material.SetFloat(PropLightIntensity, volume.lightIntensity.value);
            material.SetColor(PropFogColor, volume.fogColor.value);
            material.SetFloat(PropHeightFalloff, volume.heightFalloff.value);
            material.SetFloat(PropBaseHeight, volume.baseHeight.value);
            material.SetFloat(PropNoiseScale, volume.noiseScale.value);
            material.SetFloat(PropNoiseIntensity, volume.noiseIntensity.value);
            material.SetFloat(PropJitter, volume.temporalJitter.value);
            material.SetFloat(PropAdditionalIntensity, volume.additionalLightsIntensity.value);
            material.SetFloat(PropAmbientIntensity, volume.ambientIntensity.value);
            material.SetInt(PropMaxAdditionalLights, volume.maxAdditionalLights.value);

            // Get camera target
            var cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            // Allocate temp RT, copy scene to it, apply fog back to camera target
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
