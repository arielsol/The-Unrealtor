using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ExceptionStencilFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("在哪个阶段重绘 Exception 层；一般放在黑白之后")]
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        [Tooltip("要在黑白之后恢复彩色的 Layer")]
        public LayerMask exceptionLayer = ~0;

        [Tooltip("覆盖材质；为空则使用对象自身材质（推荐）")]
        public Material overrideMaterial = null;

        [Tooltip("只对 Game 相机生效")]
        public bool onlyGameCamera = true;
    }

    class Pass : ScriptableRenderPass
    {
        Settings _settings;
        FilteringSettings _filter;
        readonly ShaderTagId[] _tags =
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        public Pass(Settings settings)
        {
            _settings = settings;
            _filter = new FilteringSettings(RenderQueueRange.all, settings.exceptionLayer);
            renderPassEvent = settings.injectionPoint;
        }

        DrawingSettings CreateDrawingSettings(SortingSettings sorting)
        {
            var ds = new DrawingSettings(_tags[0], sorting)
            {
                overrideMaterial = _settings.overrideMaterial,   // 为 null 时=用对象原材质
                overrideMaterialPassIndex = 0
            };
            for (int i = 1; i < _tags.Length; i++)
                ds.SetShaderPassName(i, _tags[i]);
            return ds;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            if (_settings.onlyGameCamera && data.cameraData.cameraType != CameraType.Game)
                return;

            var cam = data.cameraData.camera;

            // 不透明：按正常不透明顺序
            {
                var sorting = new SortingSettings(cam)
                {
                    criteria = SortingCriteria.CommonOpaque
                };

                var ds = CreateDrawingSettings(sorting);
                var filter = new FilteringSettings(RenderQueueRange.opaque, _settings.exceptionLayer);

                context.DrawRenderers(data.cullResults, ref ds, ref filter);
            }

            // 透明：按正常透明顺序
            {
                var sorting = new SortingSettings(cam)
                {
                    criteria = SortingCriteria.CommonTransparent
                };

                var ds = CreateDrawingSettings(sorting);
                var filter = new FilteringSettings(RenderQueueRange.transparent, _settings.exceptionLayer);

                context.DrawRenderers(data.cullResults, ref ds, ref filter);
            }
        }
    }

    public Settings settings = new Settings();
    Pass _pass;

    public override void Create()
    {
        _pass = new Pass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (settings.onlyGameCamera && data.cameraData.cameraType != CameraType.Game)
            return;

        renderer.EnqueuePass(_pass);
    }
}
