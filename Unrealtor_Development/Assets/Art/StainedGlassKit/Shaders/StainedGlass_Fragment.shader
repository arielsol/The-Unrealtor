// Stained Glass Fragment Shader for URP (PBR Glass)
// For individual glass fragment pieces. Uses vertex color alpha as
// edge distance (0 = border, 1 = center) to render lead frame borders.
// Full PBR glass: translucency, refraction, fresnel, emission, light reactivity.
// Compatible with Unity 6+ URP 17.x
Shader "StainedGlass/Fragment"
{
    Properties
    {
        [Header(Glass Color)]
        [Space(5)]
        [HDR] _GlassColor ("Glass Color", Color) = (0.1, 0.3, 0.9, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.02
        _Smoothness ("Smoothness", Range(0, 1)) = 0.97

        [Header(Glass Transparency)]
        [Space(5)]
        _GlassOpacity ("Glass Opacity", Range(0, 1)) = 0.85
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _FresnelScale ("Fresnel Opacity Boost", Range(0, 1)) = 0.4

        [Header(Emission)]
        [Space(5)]
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 3.5

        [Header(Refraction)]
        [Space(5)]
        _Refraction ("Refraction Strength", Range(0, 1)) = 0.15
        _RefractionIOR ("IOR", Range(1, 2)) = 1.52
        _GlassTintStrength ("Glass Tint Strength", Range(0, 1)) = 1.0

        [Header(Translucency)]
        [Space(5)]
        _Translucency ("Translucency Strength", Range(0, 50)) = 20
        _TransNormal ("Normal Distortion", Range(0, 1)) = 0.25
        _TransScattering ("Scattering", Range(1, 50)) = 3
        _TransDirect ("Direct Light", Range(0, 1)) = 0.9
        _TransAmbient ("Ambient Light", Range(0, 1)) = 0.6
        _TransShadow ("Shadow Strength", Range(0, 1)) = 0.5

        [Header(Fresnel Reflection)]
        [Space(5)]
        [HDR] _FresnelColor ("Fresnel Color", Color) = (1, 1, 1, 1)
        _FresnelReflectPower ("Fresnel Reflect Power", Float) = 3
        _FresnelReflectScale ("Fresnel Reflect Scale", Range(0, 3)) = 1.5

        [Header(Lead Border)]
        [Space(5)]
        _BorderWidth ("Border Width", Range(0, 0.4)) = 0
        _BorderSoftness ("Border Softness", Range(0.001, 0.15)) = 0.001
        _BorderColor ("Border Color", Color) = (0.03, 0.03, 0.03, 1)

        [Header(Surface)]
        [Space(5)]
        _DepthOffset ("Depth Offset", Range(-2, 0)) = -1
    }

    SubShader
    {
        LOD 300

        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }

        ZWrite Off
        ZTest LEqual
        AlphaToMask Off

        HLSLINCLUDE
        #pragma target 3.5
        #pragma prefer_hlslcc gles

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        ENDHLSL

        // ====================================================================
        // PASS 0: Forward Lit
        // ====================================================================
        Pass
        {
            Name "StainedGlassFragment"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            ColorMask RGBA
            Cull Off
            Offset [_DepthOffset], [_DepthOffset]

            HLSLPROGRAM

            #define _NORMAL_DROPOFF_TS 1
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #define ASE_FOG 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ASE_TRANSLUCENCY 1
            #define _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma vertex vert
            #pragma fragment frag

            #define SHADERPASS SHADERPASS_FORWARD

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 texcoord   : TEXCOORD0;
                float4 texcoord1  : TEXCOORD1;
                float4 texcoord2  : TEXCOORD2;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct PackedVaryings
            {
                float4 positionCS              : SV_POSITION;
                float4 clipPosV                : TEXCOORD0;
                float4 lightmapUVOrVertexSH    : TEXCOORD1;
                half4  fogFactorAndVertexLight  : TEXCOORD2;
                float4 tSpace0                 : TEXCOORD3;
                float4 tSpace1                 : TEXCOORD4;
                float4 tSpace2                 : TEXCOORD5;
                float  edgeDist                : TEXCOORD9;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord         : TEXCOORD6;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    float2 dynamicLightmapUV   : TEXCOORD7;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                    float4 probeOcclusion      : TEXCOORD8;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassColor;
                float  _Metallic;
                float  _Smoothness;
                float  _GlassOpacity;
                float  _FresnelPower;
                float  _FresnelScale;
                float  _EmissionStrength;
                float  _Refraction;
                float  _RefractionIOR;
                float  _GlassTintStrength;
                float  _Translucency;
                float  _TransNormal;
                float  _TransScattering;
                float  _TransDirect;
                float  _TransAmbient;
                float  _TransShadow;
                float4 _FresnelColor;
                float  _FresnelReflectPower;
                float  _FresnelReflectScale;
                float  _BorderWidth;
                float  _BorderSoftness;
                float4 _BorderColor;
                float  _DepthOffset;
            CBUFFER_END

            sampler2D _CameraOpaqueTexture;

            // Additional lights helper for light transmission tinting
            float3 AdditionalLightsFlat(float3 WorldPosition, float2 ScreenUV, float4 ShadowMask)
            {
                float3 Color = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.normalizedScreenSpaceUV = ScreenUV;
                    inputData.positionWS = WorldPosition;
                    uint meshRenderingLayers = GetMeshRenderingLayer();
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_FORWARD_PLUS
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                        Light light = GetAdditionalLight(lightIndex, WorldPosition, ShadowMask);
                        #ifdef _LIGHT_LAYERS
                        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                        #endif
                        {
                            Color += light.color * (light.distanceAttenuation * light.shadowAttenuation);
                        }
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, WorldPosition, ShadowMask);
                        #ifdef _LIGHT_LAYERS
                        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                        #endif
                        {
                            Color += light.color * (light.distanceAttenuation * light.shadowAttenuation);
                        }
                    LIGHT_LOOP_END
                #endif
                return Color;
            }

            // ----------------------------------------------------------------
            // Vertex Shader
            // ----------------------------------------------------------------
            PackedVaryings vert(Attributes input)
            {
                PackedVaryings output = (PackedVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.tSpace0 = float4(normalInput.normalWS, vertexInput.positionWS.x);
                output.tSpace1 = float4(normalInput.tangentWS, vertexInput.positionWS.y);
                output.tSpace2 = float4(normalInput.bitangentWS, vertexInput.positionWS.z);

                // Pass vertex color alpha as edge distance (0=edge, 1=center)
                output.edgeDist = input.color.a;

                #if defined(LIGHTMAP_ON)
                    OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif

                OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

                #ifdef ASE_FOG
                    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #else
                    half fogFactor = 0;
                #endif

                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                output.positionCS = vertexInput.positionCS;
                output.clipPosV = vertexInput.positionCS;
                return output;
            }

            // ----------------------------------------------------------------
            // Fragment Shader
            // ----------------------------------------------------------------
            half4 frag(PackedVaryings input
                #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
                #endif
                , bool facing : SV_IsFrontFace
                ) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                // Reconstruct world-space data
                float3 WorldNormal = normalize(input.tSpace0.xyz);
                float3 WorldTangent = input.tSpace1.xyz;
                float3 WorldBiTangent = input.tSpace2.xyz;

                // Flip normal for back faces
                WorldNormal = facing ? WorldNormal : -WorldNormal;

                float3 WorldPosition = float3(input.tSpace0.w, input.tSpace1.w, input.tSpace2.w);
                float3 WorldViewDirection = SafeNormalize(_WorldSpaceCameraPos.xyz - WorldPosition);

                float4 ShadowCoords = float4(0, 0, 0, 0);
                float4 ClipPos = input.clipPosV;
                float4 ScreenPos = ComputeScreenPos(input.clipPosV);
                float2 NormalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    ShadowCoords = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    ShadowCoords = TransformWorldToShadowCoord(WorldPosition);
                #endif

                // =============================================================
                // EDGE DISTANCE - vertex color alpha lead border system
                // =============================================================
                float edge = input.edgeDist;
                float isGlass = smoothstep(_BorderWidth - _BorderSoftness, _BorderWidth + _BorderSoftness, edge);

                float3 glassColor = _GlassColor.rgb;
                float3 borderColor = _BorderColor.rgb;
                float3 surfaceColor = lerp(borderColor, glassColor, isGlass);

                // =============================================================
                // FRESNEL - core glass behaviour
                // Glass is transparent at center, reflective at edges
                // =============================================================
                float NdotV = saturate(dot(WorldNormal, WorldViewDirection));
                float fresnel = pow(1.0 - NdotV, _FresnelReflectPower) * _FresnelReflectScale;
                float fresnelOpacity = pow(1.0 - NdotV, _FresnelPower) * _FresnelScale;

                // =============================================================
                // REFRACTION - see the world through tinted glass
                // =============================================================
                float4 screenPosNorm = ScreenPos / ScreenPos.w;
                screenPosNorm.z = (UNITY_NEAR_CLIP_VALUE >= 0) ? screenPosNorm.z : screenPosNorm.z * 0.5 + 0.5;

                float3 normalizedWorldNormal = normalize(WorldNormal);
                float3 refractionDir = refract(-WorldViewDirection, normalizedWorldNormal, 1.0 / _RefractionIOR);
                float3 refractionOffset = refractionDir * _Refraction;
                float3 worldToViewRefraction = mul(UNITY_MATRIX_V, float4(refractionOffset, 0)).xyz;
                float aspectRatio = _ScreenParams.x / _ScreenParams.y;
                float2 refractionUV = float2(worldToViewRefraction.x / aspectRatio, worldToViewRefraction.y);

                float2 refractedUV = screenPosNorm.xy + refractionUV;
                float2 boundary = min(refractedUV, 1.0 - refractedUV);
                float edgeFade = smoothstep(0.0, 0.15, min(boundary.x, boundary.y));
                float2 safeUV = lerp(screenPosNorm.xy, refractedUV, edgeFade);
                float3 sceneColor = tex2D(_CameraOpaqueTexture, safeUV).rgb;

                // Tint the refracted scene by the glass color (looking through colored glass)
                float3 tintedScene = lerp(sceneColor, sceneColor * glassColor, _GlassTintStrength) * isGlass;

                // =============================================================
                // EMISSION - subtle self-glow, NOT the dominant visual
                // The refracted scene is the main visual; emission is just
                // a faint colored glow on top.
                // =============================================================
                float3 emissionColor = glassColor * _EmissionStrength * isGlass;

                // =============================================================
                // Fresnel reflection contribution
                // =============================================================
                float3 fresnelContribution = _FresnelColor.rgb * _FresnelColor.a * fresnel * isGlass;

                // =============================================================
                // COMBINE: glass transparency model
                // The refracted/tinted scene is the BASE — this is what makes
                // the glass look see-through. Emission adds a gentle colored
                // glow. Fresnel adds bright reflective edges.
                // =============================================================
                float3 combinedEmission = tintedScene + emissionColor + fresnelContribution;

                // Add light-reactive transmission tint (lights tinted by glass color)
                Light mainLight = GetMainLight(ShadowCoords);
                float mainLightAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                half4 shadowMaskValue = half4(1, 1, 1, 1);
                #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
                    shadowMaskValue = inputData.shadowMask;
                #elif !defined(LIGHTMAP_ON)
                    shadowMaskValue = unity_ProbesOcclusion;
                #endif

                float3 additionalLightColor = AdditionalLightsFlat(WorldPosition, NormalizedScreenSpaceUV, shadowMaskValue);
                float3 totalLightColor = mainLight.color * mainLightAtten + additionalLightColor;
                float3 tintedLightContribution = totalLightColor * glassColor * 0.3 * isGlass;
                combinedEmission += tintedLightContribution;

                // =============================================================
                // ALPHA: glass transparency with fresnel edges
                // Low alpha center = see-through glass
                // Higher alpha edges = reflective rim (like real glass)
                // Lead borders = fully opaque
                // =============================================================
                float glassAlpha = _GlassOpacity + fresnelOpacity;
                float finalAlpha = lerp(1.0, saturate(glassAlpha), isGlass);

                // =============================================================
                // Setup surface data for URP PBR
                // =============================================================
                float3 Normal = float3(0, 0, 1);
                if (!facing) Normal = float3(0, 0, -1);

                InputData inputData = (InputData)0;
                inputData.positionWS = WorldPosition;
                inputData.positionCS = input.positionCS;
                inputData.viewDirectionWS = WorldViewDirection;
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(Normal, half3x3(WorldTangent, WorldBiTangent, WorldNormal)));

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = ShadowCoords;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                #ifdef ASE_FOG
                    inputData.fogCoord = input.fogFactorAndVertexLight.x;
                #endif
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;

                float3 SH = input.lightmapUVOrVertexSH.xyz;
                #if defined(DYNAMICLIGHTMAP_ON)
                    inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    inputData.bakedGI = SAMPLE_GI(SH, GetAbsolutePositionWS(inputData.positionWS),
                        inputData.normalWS, inputData.viewDirectionWS, input.positionCS.xy,
                        input.probeOcclusion, inputData.shadowMask);
                #else
                    inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
                #endif

                inputData.normalizedScreenSpaceUV = NormalizedScreenSpaceUV;

                #if defined(DEBUG_DISPLAY)
                    #if defined(DYNAMICLIGHTMAP_ON)
                        inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
                    #endif
                    #if defined(LIGHTMAP_ON)
                        inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
                    #else
                        inputData.vertexSH = SH;
                    #endif
                    #if defined(USE_APV_PROBE_OCCLUSION)
                        inputData.probeOcclusion = input.probeOcclusion;
                    #endif
                #endif

                SurfaceData surfaceData;
                surfaceData.albedo = surfaceColor;
                surfaceData.metallic = saturate(_Metallic);
                surfaceData.specular = float3(0.5, 0.5, 0.5);
                surfaceData.smoothness = saturate(_Smoothness);
                surfaceData.occlusion = 1;
                surfaceData.emission = combinedEmission;
                surfaceData.alpha = saturate(finalAlpha);
                surfaceData.normalTS = Normal;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 1;

                #ifdef _DBUFFER
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // =============================================================
                // Translucency - colored light passing through the glass
                // =============================================================
                #ifdef ASE_TRANSLUCENCY
                {
                    float shadow = _TransShadow;
                    float normal = _TransNormal;
                    float scattering = _TransScattering;
                    float direct = _TransDirect;
                    float ambient = _TransAmbient;
                    float strength = _Translucency;
                    float3 Translucency = glassColor;

                    #define SUM_LIGHT_TRANSLUCENCY(Light)\
                        float3 atten = Light.color * Light.distanceAttenuation;\
                        atten = lerp(atten, atten * Light.shadowAttenuation, shadow);\
                        half3 lightDir = Light.direction + inputData.normalWS * normal;\
                        half VdotL = pow(saturate(dot(inputData.viewDirectionWS, -lightDir)), scattering);\
                        half3 translucency = atten * (VdotL * direct + inputData.bakedGI * ambient) * Translucency;\
                        color.rgb += surfaceColor * translucency * strength * isGlass;

                    SUM_LIGHT_TRANSLUCENCY(GetMainLight(inputData.shadowCoord));

                    #if defined(_ADDITIONAL_LIGHTS)
                        uint meshRenderingLayers = GetMeshRenderingLayer();
                        uint pixelLightCount = GetAdditionalLightsCount();
                        #if USE_FORWARD_PLUS
                            [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                            {
                                FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                                Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                                #ifdef _LIGHT_LAYERS
                                if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                                #endif
                                {
                                    SUM_LIGHT_TRANSLUCENCY(light);
                                }
                            }
                        #endif
                        LIGHT_LOOP_BEGIN(pixelLightCount)
                            Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                            #ifdef _LIGHT_LAYERS
                            if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                            #endif
                            {
                                SUM_LIGHT_TRANSLUCENCY(light);
                            }
                        LIGHT_LOOP_END
                    #endif
                }
                #endif

                #ifdef ASE_FOG
                    color.rgb = MixFog(color.rgb, input.fogFactorAndVertexLight.x);
                #endif

                #ifdef _WRITE_RENDERING_LAYERS
                    uint renderingLayers = GetMeshRenderingLayer();
                    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
                #endif

                return color;
            }

            ENDHLSL
        }

        // ====================================================================
        // PASS 1: ShadowCaster
        // ====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            AlphaToMask Off
            ColorMask 0
            Cull Off

            HLSLPROGRAM

            #define _NORMAL_DROPOFF_TS 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #define _SURFACE_TYPE_TRANSPARENT 1

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex vert
            #pragma fragment frag

            #define SHADERPASS SHADERPASS_SHADOWCASTER

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            #if defined(LOD_FADE_CROSSFADE)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassColor; float _Metallic; float _Smoothness;
                float _GlassOpacity; float _FresnelPower; float _FresnelScale;
                float _EmissionStrength; float _Refraction; float _RefractionIOR; float _GlassTintStrength;
                float _Translucency; float _TransNormal; float _TransScattering;
                float _TransDirect; float _TransAmbient; float _TransShadow;
                float4 _FresnelColor; float _FresnelReflectPower; float _FresnelReflectScale;
                float _BorderWidth; float _BorderSoftness; float4 _BorderColor;
                float _DepthOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 _LightDirection;
            float3 _LightPosition;

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            PackedVaryings vert(Attributes input)
            {
                PackedVaryings output = (PackedVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 frag(PackedVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }

            ENDHLSL
        }

        // ====================================================================
        // PASS 2: DepthOnly
        // ====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM

            #define _NORMAL_DROPOFF_TS 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #define _SURFACE_TYPE_TRANSPARENT 1

            #pragma vertex vert
            #pragma fragment frag
            #define SHADERPASS SHADERPASS_DEPTHONLY

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassColor; float _Metallic; float _Smoothness;
                float _GlassOpacity; float _FresnelPower; float _FresnelScale;
                float _EmissionStrength; float _Refraction; float _RefractionIOR; float _GlassTintStrength;
                float _Translucency; float _TransNormal; float _TransScattering;
                float _TransDirect; float _TransAmbient; float _TransShadow;
                float4 _FresnelColor; float _FresnelReflectPower; float _FresnelReflectScale;
                float _BorderWidth; float _BorderSoftness; float4 _BorderColor;
                float _DepthOffset;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct PackedVaryings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            PackedVaryings vert(Attributes input)
            {
                PackedVaryings output = (PackedVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(PackedVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }

            ENDHLSL
        }

        // ====================================================================
        // PASS 3: DepthNormals
        // ====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM

            #define _NORMAL_DROPOFF_TS 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _NORMALMAP 1

            #pragma vertex vert
            #pragma fragment frag
            #define SHADERPASS SHADERPASS_DEPTHNORMALS

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _GlassColor; float _Metallic; float _Smoothness;
                float _GlassOpacity; float _FresnelPower; float _FresnelScale;
                float _EmissionStrength; float _Refraction; float _RefractionIOR; float _GlassTintStrength;
                float _Translucency; float _TransNormal; float _TransScattering;
                float _TransDirect; float _TransAmbient; float _TransShadow;
                float4 _FresnelColor; float _FresnelReflectPower; float _FresnelReflectScale;
                float _BorderWidth; float _BorderSoftness; float4 _BorderColor;
                float _DepthOffset;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct PackedVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            PackedVaryings vert(Attributes input)
            {
                PackedVaryings output = (PackedVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(PackedVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                return half4(normalWS, 0);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
