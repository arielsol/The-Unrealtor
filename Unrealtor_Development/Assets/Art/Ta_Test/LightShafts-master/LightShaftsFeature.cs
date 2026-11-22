using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LightShaftsFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("When to execute this pass")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    class LightShaftsPass : ScriptableRenderPass
    {
        readonly string _profilerTag = "Light Shafts (URP)";
        Settings _settings;

        RenderTargetIdentifier _source;        // camera color
        int _tempRTId = Shader.PropertyToID("_LightShaftsTempRT");
        bool _shouldRender;
        int _shaftRTId = Shader.PropertyToID("_LightShaftsShaftRT");

        public LightShaftsPass(Settings settings)
        {
            _settings = settings;
            renderPassEvent = settings.renderPassEvent;

            // 告诉 URP：这个 pass 需要深度纹理
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var renderer = renderingData.cameraData.renderer;
            _source = renderer.cameraColorTargetHandle;

            // 当前相机下是否有任何需要渲染的 LightShafts
            _shouldRender = false;
            var cam = renderingData.cameraData.camera;
            var instances = LightShafts.Instances;

            if (instances != null && instances.Count > 0)
            {
                for (int i = 0; i < instances.Count; ++i)
                {
                    var ls = instances[i];
                    if (ls != null && ls.IsCameraSupported(cam))
                    {
                        _shouldRender = true;
                        break;
                    }
                }
            }

            if (!_shouldRender)
                return;

            // 创建临时 RT：作为“原始画面”
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(_tempRTId, desc, FilterMode.Bilinear);

            // 将当前 camera color 拷贝到 temp，只做一次！
            cmd.Blit(_source, _tempRTId);
            cmd.SetRenderTarget(_shaftRTId);
            cmd.ClearRenderTarget(false, true, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!_shouldRender)
                return;

            var cam = renderingData.cameraData.camera;
            var instances = LightShafts.Instances;
            if (instances == null || instances.Count == 0)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(_profilerTag);

            // 对每个 LightShafts，从“原图 tempRT” 采样，再加到 _source 上
            for (int i = 0; i < instances.Count; ++i)
            {
                var ls = instances[i];
                if (ls == null || !ls.IsCameraSupported(cam))
                    continue;

                var mat = ls.EffectMaterial;
                if (mat == null)
                    continue;

                // 为当前 camera 设置光的位置、颜色等
                ls.SetupMaterialForCamera(cam);

                // 注意：始终用 tempRT 作为输入（原始场景），
                // 输出到 _source，上面已经是“之前所有光”的叠加结果
                cmd.Blit(_tempRTId, _source, mat, 0);
            cmd.ReleaseTemporaryRT(_shaftRTId);
                
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (!_shouldRender)
                return;

            if (cmd == null)
                return;

            cmd.ReleaseTemporaryRT(_tempRTId);
            cmd.ReleaseTemporaryRT(_shaftRTId);
        }
    }

    public Settings settings = new Settings();
    LightShaftsPass _pass;

    public override void Create()
    {
        _pass = new LightShaftsPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 如果场景中没有任何 LightShafts，就不加这个 pass
        var instances = LightShafts.Instances;
        if (instances == null || instances.Count == 0)
            return;

        renderer.EnqueuePass(_pass);
    }
}

