using NUnit.Framework.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Unity.Burst.Intrinsics.X86.Avx;
//using static UnityEditor.Rendering.CameraUI;

public class PlanarReflectionFeature : ScriptableRendererFeature
{
    class PlanarReflectionPass : ScriptableRenderPass
    {
        private readonly string profilerTag = "PlanarReflectionPass";
        private RenderTargetIdentifier cameraColorTarget;
        static int TempTargetID;
        private RenderTexture reflectionTexture;
        private Camera reflectionCamera;
        private Material mirrorMaterial;
        private Transform mirrorTransform;
        private int reflectionTexID = Shader.PropertyToID("_ReflectionTex");

        public PlanarReflectionPass(Material mat, int resolution = 1024)
        {
        }

        public void Setup(in RenderTargetIdentifier currentTarget)
        {
            this.cameraColorTarget = currentTarget;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Setup(renderingData.cameraData.renderer.cameraColorTargetHandle);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
        }
    }

    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material mirrorMaterial;
        [Range(256, 2048)] public int resolution = 1024;
    }

    public Settings settings = new Settings();
    private PlanarReflectionPass pass;

    public override void Create()
    {
        if (settings.mirrorMaterial != null)
        {
            pass = new PlanarReflectionPass(settings.mirrorMaterial, settings.resolution);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass != null)
        {
            renderer.EnqueuePass(pass);
        }
    }
}