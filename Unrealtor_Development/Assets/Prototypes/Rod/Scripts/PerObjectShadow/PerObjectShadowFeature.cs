using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
//using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PerObjectShadowFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public int shadowMapSize = 1000;
        [Range(0, 16)] public int maxCharacterCount = 16;
        public Shader screenSpaceShadowShader = null;
        public bool useSoftShadow = true;
    }

    public Settings settings = new Settings();
    PerObjectShadowPass compositeShadowAtlasPass;
    DrawPerObjectShadowPass drawShadowPass;

    public override void Create()
    {
        compositeShadowAtlasPass = new PerObjectShadowPass(settings);
        drawShadowPass = new DrawPerObjectShadowPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(compositeShadowAtlasPass);
        renderer.EnqueuePass(drawShadowPass);
    }

    public class PerObjectShadowPass : ScriptableRenderPass
    {
        static string renderTag = "PerObjectShadow";
        static int m_sceneShadowRTID = Shader.PropertyToID("m_sceneShadowRTID");
        static int m_selfShadowRTID = Shader.PropertyToID("m_selfShadowRTID");
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        private Settings setting;
        private int characterCount;
        private GetCharacterVectors[] characters = FindObjectsOfType<GetCharacterVectors>();
        private int shadowTileRow = 1;
        int padding = 5;
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            characters = FindObjectsOfType<GetCharacterVectors>();
            characterCount = Mathf.Min(characters.Count(), setting.maxCharacterCount);
            shadowTileRow = Mathf.CeilToInt(Mathf.Sqrt(characterCount));
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 16;
            desc.width = setting.shadowMapSize;
            desc.height = setting.shadowMapSize;
            desc.colorFormat = RenderTextureFormat.Depth;
            cmd.GetTemporaryRT(m_sceneShadowRTID, desc);
            cmd.GetTemporaryRT(m_selfShadowRTID, desc);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //init cmd buffer
            var commandBuffer = CommandBufferPool.Get(renderTag);
            commandBuffer.SetGlobalTexture("_PerObjectShadowTex", m_sceneShadowRTID);
            commandBuffer.SetGlobalTexture("_PerObjectSelfShadowTex", m_selfShadowRTID);

            commandBuffer.SetRenderTarget(m_sceneShadowRTID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            commandBuffer.ClearRenderTarget(true, true, Color.black);

            commandBuffer.SetRenderTarget(m_selfShadowRTID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            commandBuffer.ClearRenderTarget(true, true, Color.black);

            //init shadowMVP Matricies
            int maxCharacterCount = setting.maxCharacterCount;
            Matrix4x4[] shadowMVPs = new Matrix4x4[maxCharacterCount];
            Matrix4x4[] inverseProjectMats = new Matrix4x4[maxCharacterCount];
            Matrix4x4[] viewprojectMats = new Matrix4x4[maxCharacterCount];
            float[] ranges = new float[maxCharacterCount];
            float[] widths = new float[maxCharacterCount];
            float[] shadowBiases = new float[maxCharacterCount];
            float[] shadowSoftWeights = new float[maxCharacterCount];
            Vector4[] shadowAtalsPaddingSizes = new Vector4[maxCharacterCount];
            //Render shadows atlas for all characters
            for (int i = 0; i < characterCount; i++)
            {
                
                GetCharacterVectors character = characters[i];
                Vector3 shadowMin, shadowMax;
                
                Bounds combinedBounds = character.GetCombinedBounds();

                shadowBiases[i] = character.shadowBias;
                shadowSoftWeights[i] = character.shadowSoftWeight;

                viewMatrix = ComputeViewMatrix(combinedBounds);
                ComputeShadowMinMax(combinedBounds, viewMatrix, out shadowMin, out shadowMax);
                projMatrix = ComputeProjectionMatrix(combinedBounds, shadowMin, shadowMax);
                ranges[i] = ComputeRange(combinedBounds, shadowMin, shadowMax);
                widths[i] = ComputeWidth(combinedBounds, shadowMin, shadowMax);
                viewprojectMats[i] = projMatrix * viewMatrix;
                
                if (SystemInfo.usesReversedZBuffer)
                    projMatrix.SetRow(2, -projMatrix.GetRow(2));

                Vector2 tileSize = new Vector2(setting.shadowMapSize / shadowTileRow, setting.shadowMapSize / shadowTileRow);
                Vector2 tilePos = new Vector2(i % shadowTileRow, i / shadowTileRow);
                Rect tile = new Rect(tilePos * tileSize, tileSize);

                shadowAtalsPaddingSizes[i] = new Vector2(padding / tileSize.x, padding / tileSize.x);

                //Render scene shadow
                commandBuffer.SetRenderTarget(m_sceneShadowRTID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                commandBuffer.SetViewport(tile);
                
                commandBuffer.EnableScissorRect(new Rect(tile.x + padding, tile.y + padding, tile.width - padding * 2, tile.height - padding * 2));
                commandBuffer.SetViewMatrix(viewMatrix);
                commandBuffer.SetProjectionMatrix(projMatrix);
                foreach (var renderer in character.GetComponentsInChildren<Renderer>())
                {
                    Material curMat = renderer.sharedMaterial;
                    var passId = curMat.FindPass("PerObjectShadowCaster");
                    if (-1 != passId)
                    {
                        commandBuffer.DrawRenderer(renderer, curMat, 0, passId);
                    }     
                }
                commandBuffer.DisableScissorRect();

                //Render self shadow
                commandBuffer.SetRenderTarget(m_selfShadowRTID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                commandBuffer.SetViewport(tile);
                commandBuffer.EnableScissorRect(new Rect(tile.x + padding, tile.y + padding, tile.width - padding * 2, tile.height - padding * 2));
                viewMatrix = ComputeSelfShadowViewMatrix(combinedBounds, character.gameObject, renderingData);
                commandBuffer.SetViewMatrix(viewMatrix);
                commandBuffer.SetProjectionMatrix(projMatrix);
                foreach (var renderer in character.GetComponentsInChildren<Renderer>())
                {
                    Material curMat = renderer.sharedMaterial;
                    var passId = curMat.FindPass("PerObjectShadowCaster");
                    if (-1 != passId)
                    {
                        commandBuffer.DrawRenderer(renderer, curMat, 0, passId);
                    }
                }
                commandBuffer.DisableScissorRect();

                Matrix4x4 shadowCoordMat = Matrix4x4.identity;
                shadowCoordMat.SetRow(0, new Vector4(1f / shadowTileRow, 0.0f, 0.0f, tilePos.x / shadowTileRow));
                shadowCoordMat.SetRow(1, new Vector4(0.0f, 1f / shadowTileRow, 0.0f, tilePos.y / shadowTileRow));
                shadowCoordMat.SetRow(2, new Vector4(0.0f, 0.0f, 1, 0));
                shadowCoordMat.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                shadowMVPs[i] = shadowCoordMat;

                Matrix4x4 inverseProjectMat = Matrix4x4.Inverse(projMatrix);
                inverseProjectMats[i] = inverseProjectMat;
            }

            //Pass variables to shader
            if(characterCount > 0)
            {
                Shader.SetGlobalMatrixArray("_ShadowCoordMatricies", shadowMVPs);
                Shader.SetGlobalMatrixArray("_InverseProjMatricies", inverseProjectMats);
                Shader.SetGlobalMatrixArray("_ViewprojectMatricies", viewprojectMats);
                Shader.SetGlobalMatrix("_MainLightViewMatrix", Matrix4x4.TRS(Vector3.zero, FindFirstObjectByType<Light>().transform.rotation, new Vector3(1, 1, 1)));
                Shader.SetGlobalInt("_PerObjectShadowCount", characterCount);
                Shader.SetGlobalFloatArray("_BoundsRanges", ranges);
                Shader.SetGlobalFloatArray("_LightWidths", widths);
                Shader.SetGlobalFloatArray("_ShadowBiases", shadowBiases);
                Shader.SetGlobalFloatArray("_ShadowSoftWeights", shadowSoftWeights);
                Shader.SetGlobalVectorArray("_ShadowPaddingSizes", shadowAtalsPaddingSizes);
                if (setting.useSoftShadow)
                {
                    Shader.EnableKeyword("_USE_SOFT_SHADOW");
                }
                else
                {
                    Shader.DisableKeyword("_USE_SOFT_SHADOW");
                }
            }

            commandBuffer.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            commandBuffer.SetViewMatrix(renderingData.cameraData.GetViewMatrix());
            commandBuffer.SetProjectionMatrix(renderingData.cameraData.GetProjectionMatrix());
            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(m_sceneShadowRTID);
            cmd.ReleaseTemporaryRT(m_selfShadowRTID);
        }

        public PerObjectShadowPass(Settings settings)
        {
            renderPassEvent = settings.renderPassEvent;
            this.setting = settings;
        }

        Matrix4x4 ComputeViewMatrix(Bounds bounds)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.InstanceID);
            Light mainLight = null;
            foreach (var light in lights)
            {
                if(light.type == LightType.Directional)
                {
                    mainLight = light;
                }
            }
            Matrix4x4 viewMatrix = Matrix4x4.Inverse(Matrix4x4.TRS(bounds.center, mainLight.transform.rotation, new Vector3(1, 1, 1)));
            Matrix4x4 flipZmatrix = Matrix4x4.identity;
            flipZmatrix.m22 = -1;
            viewMatrix = flipZmatrix * viewMatrix;
            return viewMatrix;
        }

        Matrix4x4 ComputeSelfShadowViewMatrix(Bounds bounds, GameObject character, RenderingData renderingData)
        {
            Light mainLight = FindFirstObjectByType<Light>();
            Camera camera = renderingData.cameraData.camera;
            Vector3 viewForward = Vector3.Normalize(bounds.center - camera.transform.position);
            Vector3 forward = Vector3.Normalize(Vector3.Lerp(viewForward, mainLight.transform.forward, 0.2f));
            float cosAngle = Vector3.Dot(forward, character.transform.up);
            float cosAngleClamped = Mathf.Clamp(cosAngle, -0.866f, 0);
            forward = Vector3.Normalize(forward + (cosAngleClamped - cosAngle) * character.transform.up);
            Quaternion lightRotation = Quaternion.LookRotation(forward, camera.transform.up);

            Matrix4x4 viewMatrix = Matrix4x4.Inverse(Matrix4x4.TRS(bounds.center, lightRotation, new Vector3(1, 1, 1)));
            Matrix4x4 flipZmatrix = Matrix4x4.identity;
            flipZmatrix.m22 = -1;
            viewMatrix = flipZmatrix * viewMatrix;
            return viewMatrix;
        }

        void ComputeShadowMinMax(Bounds bounds, Matrix4x4 viewMatrix, out Vector3 shadowMin, out Vector3 shadowMax)
        {
            Vector3 extents = bounds.extents;
            Vector3[] boundsWS = new Vector3[8];
            boundsWS[0] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(-extents.x, extents.y, -extents.z));
            boundsWS[1] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(extents.x, extents.y, -extents.z));
            boundsWS[2] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(-extents.x, -extents.y, -extents.z));
            boundsWS[3] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(extents.x, -extents.y, -extents.z));
            boundsWS[4] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(-extents.x, extents.y, extents.z));
            boundsWS[5] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(extents.x, extents.y, extents.z));
            boundsWS[6] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(-extents.x, -extents.y, extents.z));
            boundsWS[7] = viewMatrix.MultiplyPoint(bounds.center + new Vector3(extents.x, -extents.y, extents.z));
            
            shadowMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            shadowMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < 8; i++)
            {
                shadowMin = Vector3.Min(shadowMin, boundsWS[i]);
                shadowMax = Vector3.Max(shadowMax, boundsWS[i]);
            }
        }

        Matrix4x4 ComputeProjectionMatrix(Bounds bounds, Vector3 shadowMin, Vector3 shadowMax)
        {
            float left = shadowMin.x;
            float right = shadowMax.x;
            float bottom = shadowMin.y;
            float top = shadowMax.y;
            float zNear = shadowMin.z;
            float zFar = shadowMax.z + 10.0f;
            Matrix4x4 projMatrix = Matrix4x4.Ortho(left, right, bottom, top, zNear, zFar);
            projMatrix = GL.GetGPUProjectionMatrix(projMatrix, false);
            return projMatrix;
        }

        float ComputeRange(Bounds bounds, Vector3 shadowMin, Vector3 shadowMax)
        {
            float zNear = shadowMin.z;
            float zFar = shadowMax.z + 10.0f;
            return zFar - zNear;
        }

        float ComputeWidth(Bounds bounds, Vector3 shadowMin, Vector3 shadowMax)
        {
            float left = shadowMin.x;
            float right = shadowMax.x;
            return Mathf.Abs(right  - left);
        }
    }

    public class DrawPerObjectShadowPass : ScriptableRenderPass
    {
        static string renderTag = "DrawPerObjectShadowPass";
        static int PenumbraMaskTargetID = Shader.PropertyToID("PenumbraMaskTargetID");
        static int TempTargetID1 = Shader.PropertyToID("DrawPerObjectShadowPassRTID1");
        static int TempTargetID2 = Shader.PropertyToID("DrawPerObjectShadowPassRTID2");
        static int gaussianXTargetID = Shader.PropertyToID("gaussianXTargetID"),
                   gaussianYTargetID = Shader.PropertyToID("gaussianYTargetID");
        private Settings setting;
        Material m_Material;
        public DrawPerObjectShadowPass(Settings settings)
        {
            setting = settings;
            m_Material = CoreUtils.CreateEngineMaterial(setting.screenSpaceShadowShader);
            renderPassEvent = settings.renderPassEvent;
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.colorFormat = RenderTextureFormat.RHalf;
            desc.width =(int)Mathf.Round(desc.width *1);
            desc.height = (int)Mathf.Round(desc.height * 1);
            cmd.GetTemporaryRT(TempTargetID1, desc);
            cmd.GetTemporaryRT(TempTargetID2, desc);
            cmd.GetTemporaryRT(gaussianXTargetID, desc);
            cmd.GetTemporaryRT(gaussianYTargetID, desc);

            desc.colorFormat = RenderTextureFormat.R8;
            desc.width /= 4;
            desc.height /= 4;
            cmd.GetTemporaryRT(PenumbraMaskTargetID, desc);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_Material == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get(renderTag);

            
            var passId = m_Material.FindPass("PerObjectSceneShadowComposite");
            var penumbraPassID = m_Material.FindPass("PenumbraMask");
            var blurXPassID = m_Material.FindPass("GaussianX");
            var blurYPassID = m_Material.FindPass("GaussianY");
            var penumbraBlurXPassID = m_Material.FindPass("PenumbraGaussianX");
            var penumbraBlurYPassID = m_Material.FindPass("PenumbraGaussianY");

            if (setting.useSoftShadow)
            {
                //Draw PenumbraMask
                cmd.Blit(TempTargetID1, PenumbraMaskTargetID, m_Material, penumbraPassID);
                cmd.SetGlobalTexture("_PerObjectPenumbraMask", PenumbraMaskTargetID);
                //Blur PenumbraMask
                cmd.Blit(PenumbraMaskTargetID, gaussianXTargetID);
                for (int i = 0; i < 2; i++)
                {
                    cmd.Blit(gaussianXTargetID, gaussianYTargetID, m_Material, penumbraBlurXPassID);
                    cmd.SetGlobalTexture("_PerObjectPenumbraMask", gaussianYTargetID);
                    cmd.Blit(gaussianYTargetID, gaussianXTargetID, m_Material, penumbraBlurYPassID);
                    cmd.SetGlobalTexture("_PerObjectPenumbraMask", gaussianXTargetID);
                }
            }
            

            //Composite scene shadow
            cmd.Blit(TempTargetID1, TempTargetID2, m_Material, passId);
            cmd.SetGlobalTexture("_PerObjectSceneShadowMap", TempTargetID2);

            if (setting.useSoftShadow)
            {
                //Blur the PCSS result
                cmd.Blit(TempTargetID2, gaussianXTargetID);
                for (int i = 0; i < 5; i++)
                {
                    cmd.Blit(gaussianXTargetID, gaussianYTargetID, m_Material, blurXPassID);
                    cmd.SetGlobalTexture("_PerObjectSceneShadowMap", gaussianYTargetID);
                    cmd.Blit(gaussianYTargetID, gaussianXTargetID, m_Material, blurYPassID);
                    cmd.SetGlobalTexture("_PerObjectSceneShadowMap", gaussianXTargetID);
                }
                cmd.SetGlobalTexture("_PerObjectSceneShadowMap", gaussianXTargetID);
            }

            cmd.SetRenderTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(gaussianXTargetID);
            cmd.ReleaseTemporaryRT(gaussianYTargetID);
            cmd.ReleaseTemporaryRT(TempTargetID1);
            cmd.ReleaseTemporaryRT(PenumbraMaskTargetID);
        }
    }

}
