using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FluidRaymarchFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Compute")]
        public ComputeShader raymarching;
        [Tooltip("Compute kernel index. If you use FindKernel, you can set this to -1 and fill kernelName.")]
        public int kernelIndex = 0;
        public string kernelName = ""; // 可选：写 kernel 名字，例如 "CSMain"。非空则优先用它。

        [Header("Visual Params")]
        public float viewRadius = 0.2f;
        public float blendStrength = 1f;
        public Color waterColor = Color.cyan;
        public Color ambientLight = Color.gray;

        [Header("Lighting")]
        public Light lightSource;

        [Header("When to Render")]
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;

        [Header("Raymarch Threads (must match [numthreads(x,y,1)] in compute)")]
        public int threadsX = 8;
        public int threadsY = 8;
    }

    public Settings settings = new Settings();

    class Pass : ScriptableRenderPass
    {
        private readonly Settings s;

        private int kernel;

        private RTHandle tempRT;
        Light cachedSun;
        // Property IDs (match your compute shader names)
        private static readonly int NumParticlesID = Shader.PropertyToID("numParticles");
        private static readonly int ParticleRadiusID = Shader.PropertyToID("particleRadius");
        private static readonly int BlendStrengthID = Shader.PropertyToID("blendStrength");
        private static readonly int WaterColorID = Shader.PropertyToID("waterColor");
        private static readonly int AmbientID = Shader.PropertyToID("_AmbientLight");
        private static readonly int LightID = Shader.PropertyToID("_Light");
        private static readonly int CamPosID = Shader.PropertyToID("_CameraPos");
        private static readonly int CamToWorldID = Shader.PropertyToID("_CameraToWorld");
        private static readonly int CamInvProjID = Shader.PropertyToID("_CameraInverseProjection");

        // optional depth binding
        // your compute uses SetTextureFromGlobal in old code, but here we will set it from global each frame
        // If your compute expects a texture named "_DepthTexture", keep this.
        private static readonly int DepthTexID = Shader.PropertyToID("_DepthTexture");

        public Pass(Settings settings)
        {
            s = settings;
        }

        private void EnsureKernel()
        {
            if (kernel != 0 && kernel != -999) return; // already set (hacky guard)
            kernel = -999;

            if (s.raymarching == null) return;

            if (!string.IsNullOrEmpty(s.kernelName))
            {
                kernel = s.raymarching.FindKernel(s.kernelName);
            }
            else
            {
                kernel = s.kernelIndex; // default
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.enableRandomWrite = true;            // ✅ 关键：允许 Compute 写入（UAV）
            desc.msaaSamples = 1;                     // ✅ 建议：UAV 不支持 MSAA，强制 1
            RenderingUtils.ReAllocateIfNeeded(ref tempRT, desc, name: "_FluidRaymarchTemp");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            EnsureKernel();

            var sph = FluidRaymarchController.SPH;

            if (s.raymarching == null) return;
            if (kernel < 0) return;
            if (sph == null || sph._particlesBuffer == null || sph.particles == null) return;

            var cam = renderingData.cameraData.camera;
            var renderer = renderingData.cameraData.renderer;

            var cmd = CommandBufferPool.Get("FluidRaymarchPass");

            // URP camera color target
            var source = renderer.cameraColorTargetHandle;
            if (cachedSun == null || !cachedSun.enabled)
            {
                cachedSun = RenderSettings.sun;
                if (cachedSun == null)
                {
                    foreach (var l in Object.FindObjectsOfType<Light>())
                        if (l && l.type == LightType.Directional && l.enabled) { cachedSun = l; break; }
                }
            }
            Vector3 lightDir = cachedSun ? cachedSun.transform.forward : Vector3.down;
            s.raymarching.SetVector(LightID, lightDir);

            // Copy camera color into tempRT (so compute can read it)
            Blitter.BlitCameraTexture(cmd, source, tempRT);

            // --- Bind data to compute ---
            s.raymarching.SetBuffer(kernel, "particles", sph._particlesBuffer);
            s.raymarching.SetInt(NumParticlesID, sph.particles.Length);

            s.raymarching.SetFloat(ParticleRadiusID, s.viewRadius);
            s.raymarching.SetFloat(BlendStrengthID, s.blendStrength);
            s.raymarching.SetVector(WaterColorID, (Vector4)s.waterColor);
            s.raymarching.SetVector(AmbientID, (Vector4)s.ambientLight);

            if (s.lightSource != null)
                s.raymarching.SetVector(LightID, s.lightSource.transform.forward);

            s.raymarching.SetVector(CamPosID, cam.transform.position);
            s.raymarching.SetMatrix(CamToWorldID, cam.cameraToWorldMatrix);

            // Important for SRP: use GPU projection matrix
            var gpuProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
            s.raymarching.SetMatrix(CamInvProjID, gpuProj.inverse);

            // Depth texture (URP needs Depth Texture enabled in URP Asset/Renderer)
            // This sets compute texture slot "_DepthTexture" = global _CameraDepthTexture
            // If your compute expects different names, change them.
            cmd.SetGlobalTexture("_DepthTexture", Shader.PropertyToID("_CameraDepthTexture"));

            // Bind Source/Destination textures expected by compute
            // Note: compute uses RenderTexture; RTHandle.rt is the underlying RenderTexture
            s.raymarching.SetTexture(kernel, "Source", tempRT.rt);
            s.raymarching.SetTexture(kernel, "Destination", tempRT.rt);
            s.raymarching.SetTexture(kernel, "_DepthTexture", Shader.GetGlobalTexture("_CameraDepthTexture"));

            // --- Dispatch ---
            int tx = Mathf.Max(1, s.threadsX);
            int ty = Mathf.Max(1, s.threadsY);

            int threadGroupsX = Mathf.CeilToInt(cam.pixelWidth / (float)tx);
            int threadGroupsY = Mathf.CeilToInt(cam.pixelHeight / (float)ty);

            cmd.DispatchCompute(s.raymarching, kernel, threadGroupsX, threadGroupsY, 1);

            // Write back to camera target
            Blitter.BlitCameraTexture(cmd, tempRT, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }
    }

    private Pass pass;

    public override void Create()
    {
        pass = new Pass(settings)
        {
            renderPassEvent = settings.passEvent
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(pass);
    }
}