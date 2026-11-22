using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GrayscaleRendererFeature : ScriptableRendererFeature
{
    [Serializable]
    public class Settings
    {
        public Material material;
        
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering;
        public bool onlyAffectGameCamera = true;
    }

    class Pass : ScriptableRenderPass
    {
        private readonly string _tag = "BW_Blit";
        private Material _mat;
        public bool gameOnly = true;

#if UNITY_2022_1_OR_NEWER
        RTHandle _temp;
        RTHandle _src;
#else
        int _tempId = Shader.PropertyToID("_BW_Temp");
        RenderTargetIdentifier _src;
#endif

        public Pass(Material m, RenderPassEvent evt)
        {
            _mat = m;
            renderPassEvent = evt;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            if (_mat == null) return;
            if (gameOnly && data.cameraData.cameraType != CameraType.Game) return;
            if (data.cameraData.renderType == CameraRenderType.Overlay) return; // skip overlays

#if UNITY_2022_1_OR_NEWER
            _src = data.cameraData.renderer.cameraColorTargetHandle;
            var desc = data.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _temp, desc, name: "_BW_Temp");
#else
            _src = data.cameraData.renderer.cameraColorTarget;
            var desc = data.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(_tempId, desc);
#endif
            ConfigureTarget(
        data.cameraData.renderer.cameraColorTargetHandle,
        data.cameraData.renderer.cameraDepthTargetHandle
    );
            ConfigureClear(ClearFlag.None, Color.black);

        }

        public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
        {

            if (_mat == null) return;
            if (gameOnly && data.cameraData.cameraType != CameraType.Game) return;
            if (data.cameraData.renderType == CameraRenderType.Overlay) return; // skip overlays
#if UNITY_EDITOR
            if (data.cameraData.cameraType == CameraType.Game)
                
            {//Debug.Log($"[BW] Exec on {data.cameraData.camera.name} @ {renderPassEvent}");
             }
#endif


            var cmd = CommandBufferPool.Get(_tag);
            try
            {
#if UNITY_2022_1_OR_NEWER
                Blitter.BlitCameraTexture(cmd, _src, _temp, _mat, 0);
                Blitter.BlitCameraTexture(cmd, _temp, _src);
#else
                cmd.Blit(_src, _tempId, _mat, 0);
                cmd.Blit(_tempId, _src);
#endif
                ctx.ExecuteCommandBuffer(cmd);
            }
            finally
            {
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
#if UNITY_2022_1_OR_NEWER
            _src = null;
            if (_temp != null) _temp.Release();
#else
            cmd.ReleaseTemporaryRT(_tempId);
#endif
        }
    }

    public Settings settings = new Settings();
    Pass _pass;

    public override void Create()
    {
        _pass = new Pass(settings.material, settings.injectionPoint) { gameOnly = settings.onlyAffectGameCamera };
    }

#if UNITY_EDITOR
    
    void OnValidate()
    {
        if (_pass != null)
        {
            _pass.renderPassEvent = settings.injectionPoint;
            _pass.gameOnly = settings.onlyAffectGameCamera;
        }
    }
#endif

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (settings.material == null) return;
        
        _pass.renderPassEvent = settings.injectionPoint;
        _pass.gameOnly = settings.onlyAffectGameCamera;
        renderer.EnqueuePass(_pass);
    }
}
