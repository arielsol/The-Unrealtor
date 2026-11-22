// Crystal Gem Shader for URP
// A physically-inspired crystal / gemstone shader featuring:
//   - Opaque core, transparent rim  (the OPPOSITE of typical glass)
//   - Chromatic refraction with RGB channel split (gem dispersion / fire)
//   - Prismatic iridescence driven by view angle
//   - Multi-layer sparkle highlights through high-frequency normal sampling
//   - Depth color variation (angle-dependent colour depth)
//   - Parallax / height map support for surface relief
// Compatible with Unity 6+ URP 17.x
Shader "StainedGlass/Crystal"
{
    Properties
    {
        [Header(Crystal Appearance)]
        [Space(5)]
        _BaseColor("Crystal Color", Color) = (0.35, 0.55, 0.95, 1.0)
        _ColorB("Gradient Color B", Color) = (0.6, 0.25, 0.9, 1.0)
        _ColorC("Voronoi Color C", Color) = (0.2, 0.85, 0.6, 1.0)
        _DepthColorDarken("Depth Darkening at Rim", Range(0, 1)) = 0.35
        _Metallic("Metallic", Range(0, 1)) = 0.0
        _Smoothness("Smoothness", Range(0, 1)) = 0.99

        [Header(Normal Map)]
        [Space(5)]
        [Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 10)) = 1.5

        [Header(Height Map Parallax)]
        [Space(5)]
        [Toggle(_PARALLAX_ON)] _ParallaxEnabled("Enable Parallax / Height Map", Float) = 0
        _HeightMap("Height Map", 2D) = "gray" {}
        _HeightStrength("Parallax Strength", Range(0, 0.15)) = 0.03

        [Header(Crystal Transparency)]
        [Space(5)]
        _CoreOpacity("Core Opacity (Center)", Range(0, 1)) = 0.92
        _RimTransparencyPower("Rim Falloff  (higher = sharper fade)", Range(0.1, 8)) = 1.5

        [Header(Refraction and Chromatic Aberration)]
        [Space(5)]
        _RefractionStrength("Refraction Strength", Range(0, 1)) = 0.35
        _RefractionIOR("IOR   1.52=glass  1.85=gem  2.42=diamond", Range(1, 3)) = 1.85
        _ChromaticAberration("Chromatic Aberration (RGB split)", Range(0, 0.5)) = 0.15

        [Header(Specular and Sparkle)]
        [Space(5)]
        [HDR] _SpecularColor("Specular / Sparkle Color", Color) = (2.5, 2.5, 2.5, 1)
        _PrimarySpecPower("Primary Specular Power (broad highlight)", Range(10, 500)) = 120
        _SparkleIntensity("Sparkle Intensity", Range(0, 10)) = 4.0
        _SparkleScale("Sparkle UV Scale (facet density)", Range(1, 80)) = 30
        _SparklePower("Sparkle Sharpness (higher = tinier sparks)", Range(50, 3000)) = 900

        [Header(Prismatic Iridescence)]
        [Space(5)]
        _IridescenceIntensity("Iridescence Intensity", Range(0, 2)) = 0.8
        _IridescenceThickness("Dispersion Thickness", Range(0.1, 10)) = 3.0
        _IridescenceShift("Phase Shift (hue rotate)", Range(0, 6.28)) = 0.0

        [Header(Inner Glow)]
        [Space(5)]
        [HDR] _EmissionColor("Inner Glow Color", Color) = (0.35, 0.55, 1.0, 1)
        _EmissionIntensity("Glow Intensity", Range(0, 5)) = 0.5
        [Toggle(_EMISSION_PULSE_ON)] _EmissionPulseEnabled("Enable Glow Pulse", Float) = 0
        _EmissionPulseSpeed("Pulse Speed", Range(0.1, 5)) = 1.0
        _EmissionPulseMin("Pulse Min Intensity", Range(0, 1)) = 0.3

        [Header(Color Variation Mode)]
        [Space(5)]
        [KeywordEnum(Solid, Gradient, Voronoi)] _ColorMode("Color Mode", Float) = 0
        _GradientAngle("Gradient Angle", Range(0, 360)) = 45
        _GradientOffset("Gradient Offset", Range(-1, 1)) = 0
        _GradientSharpness("Gradient Sharpness", Range(0.1, 10)) = 1
        _VoronoiScale("Voronoi Scale", Range(1, 20)) = 5

        [HideInInspector][ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1
        [HideInInspector][ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1
        [HideInInspector][ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0
        [HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1
        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        LOD 300

        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "UniversalMaterialType"="Lit" }

        ZWrite Off
        ZTest LEqual
        Offset 0, 0
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
            Name "Forward"
            Tags { "LightMode"="UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            ColorMask RGBA
            Cull Off

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
            #define _EMISSION
            #define _NORMALMAP 1

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

            #pragma shader_feature_local _COLORMODE_SOLID _COLORMODE_GRADIENT _COLORMODE_VORONOI
            #pragma shader_feature_local _EMISSION_PULSE_ON
            #pragma shader_feature_local _PARALLAX_ON

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

            #include "../StainedGlass_Helpers.hlsl"

            // ----------------------------------------------------------------
            // Vertex / Fragment structures
            // ----------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 texcoord   : TEXCOORD0;
                float4 texcoord1  : TEXCOORD1;
                float4 texcoord2  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct PackedVaryings
            {
                float4 positionCS            : SV_POSITION;
                float4 clipPosV              : TEXCOORD0;
                float4 lightmapUVOrVertexSH  : TEXCOORD1;
                half4  fogFactorAndVertexLight: TEXCOORD2;
                float4 tSpace0               : TEXCOORD3;  // (normalWS.xyz,  posWS.x)
                float4 tSpace1               : TEXCOORD4;  // (tangentWS.xyz, posWS.y)
                float4 tSpace2               : TEXCOORD5;  // (bitangWS.xyz,  posWS.z)
                float2 uv                    : TEXCOORD9;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord       : TEXCOORD6;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    float2 dynamicLightmapUV : TEXCOORD7;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                    float4 probeOcclusion    : TEXCOORD8;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ----------------------------------------------------------------
            // Material constant buffer  (all properties must be listed here
            // for SRP Batcher compatibility)
            // ----------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ColorB;
                float4 _ColorC;
                float  _DepthColorDarken;
                float  _Metallic;
                float  _Smoothness;

                float  _NormalStrength;
                float4 _NormalMap_ST;

                float  _HeightStrength;
                float4 _HeightMap_ST;

                float  _CoreOpacity;
                float  _RimTransparencyPower;

                float  _RefractionStrength;
                float  _RefractionIOR;
                float  _ChromaticAberration;

                float4 _SpecularColor;
                float  _PrimarySpecPower;
                float  _SparkleIntensity;
                float  _SparkleScale;
                float  _SparklePower;

                float  _IridescenceIntensity;
                float  _IridescenceThickness;
                float  _IridescenceShift;

                float4 _EmissionColor;
                float  _EmissionIntensity;
                float  _EmissionPulseSpeed;
                float  _EmissionPulseMin;

                float  _GradientAngle;
                float  _GradientOffset;
                float  _GradientSharpness;
                float  _VoronoiScale;

                InputData inputData;
            CBUFFER_END

            sampler2D _CameraOpaqueTexture;
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);
            TEXTURE2D(_HeightMap);  SAMPLER(sampler_HeightMap);

            // ----------------------------------------------------------------
            // Helper: sum additional lights for sparkle
            // ----------------------------------------------------------------
            float3 AdditionalLightsFlat(float3 WorldPosition, float2 ScreenUV)
            {
                float3 col = 0;
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData tmpData = (InputData)0;
                    tmpData.normalizedScreenSpaceUV = ScreenUV;
                    tmpData.positionWS = WorldPosition;
                    uint meshRenderingLayers = GetMeshRenderingLayer();
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_FORWARD_PLUS
                    [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                    {
                        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                        Light light = GetAdditionalLight(li, WorldPosition, unity_ProbesOcclusion);
                        #ifdef _LIGHT_LAYERS
                        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                        #endif
                        { col += light.color * (light.distanceAttenuation * light.shadowAttenuation); }
                    } 
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, WorldPosition, unity_ProbesOcclusion);
                        #ifdef _LIGHT_LAYERS
                        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers)) 
                        #endif
                        { col += light.color * (light.distanceAttenuation * light.shadowAttenuation); }
                    LIGHT_LOOP_END
                #endif
                return col;
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
                VertexNormalInputs   normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.tSpace0 = float4(normalInput.normalWS,   vertexInput.positionWS.x);
                output.tSpace1 = float4(normalInput.tangentWS,  vertexInput.positionWS.y);
                output.tSpace2 = float4(normalInput.bitangentWS,vertexInput.positionWS.z);
                output.uv      = input.texcoord.xy;

                #if defined(LIGHTMAP_ON)
                    OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif

                OUTPUT_SH4(vertexInput.positionWS, normalInput.normalWS.xyz,
                    GetWorldSpaceNormalizeViewDir(vertexInput.positionWS),
                    output.lightmapUVOrVertexSH.xyz, output.probeOcclusion);

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
                output.clipPosV   = vertexInput.positionCS;
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

                // ── Reconstruct world-space TBN and position ──────────────
                float3 WorldNormal    = normalize(input.tSpace0.xyz);
                float3 WorldTangent   = input.tSpace1.xyz;
                float3 WorldBiTangent = input.tSpace2.xyz;
                WorldNormal = facing ? WorldNormal : -WorldNormal;

                float3 WorldPosition     = float3(input.tSpace0.w, input.tSpace1.w, input.tSpace2.w);
                float3 WorldViewDirection = SafeNormalize(_WorldSpaceCameraPos.xyz - WorldPosition);
                float2 uv = input.uv;

                float4 ShadowCoords             = float4(0, 0, 0, 0);
                float4 ScreenPos                = ComputeScreenPos(input.clipPosV);
                float2 NormalizedScreenSpaceUV  = GetNormalizedScreenSpaceUV(input.positionCS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    ShadowCoords = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    ShadowCoords = TransformWorldToShadowCoord(WorldPosition);
                #endif

                // ── 1. PARALLAX UV offset ─────────────────────────────────
                #ifdef _PARALLAX_ON
                {
                    float3x3 worldToTangent = float3x3(WorldTangent, WorldBiTangent, WorldNormal);
                    float3   viewTS = mul(worldToTangent, WorldViewDirection);
                    float    height = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, TRANSFORM_TEX(uv, _HeightMap)).r;
                    uv += (height - 0.5) * _HeightStrength * (viewTS.xy / max(viewTS.z, 0.1));
                }
                #endif

                // ── 2. Base crystal colour (colour-mode variations) ───────
                float3 crystalColor = _BaseColor.rgb;
                #if defined(_COLORMODE_GRADIENT)
                    crystalColor = GradientBlend(_BaseColor.rgb, _ColorB.rgb, uv,
                                                 _GradientAngle, _GradientOffset, _GradientSharpness);
                #elif defined(_COLORMODE_VORONOI)
                    float2 cellID;
                    VoronoiPattern(uv, _VoronoiScale, cellID);
                    float cellHash = frac(sin(dot(cellID, float2(12.9898, 78.233))) * 43758.5453);
                    crystalColor = CellColorSmooth(cellID, _BaseColor.rgb, _ColorB.rgb, _ColorC.rgb, cellHash);
                #endif

                // ── 3. Normal map ─────────────────────────────────────────
                float4 normalSample = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(uv, _NormalMap));
                float3 normalTS     = UnpackNormalScale(normalSample, _NormalStrength);
                if (!facing) normalTS.z = -normalTS.z;
                float3 normalWS = NormalizeNormalPerPixel(
                    TransformTangentToWorld(normalTS, half3x3(WorldTangent, WorldBiTangent, WorldNormal)));

                // ── 4. NdotV  (the heart of the crystal transparency) ─────
                //   NdotV ≈ 1  →  looking straight at the face  →  OPAQUE
                //   NdotV ≈ 0  →  grazing / rim angle           →  TRANSPARENT
                float NdotV = saturate(dot(normalWS, WorldViewDirection));

                // ── 5. Depth colour variation ─────────────────────────────
                //   Crystal body appears slightly richer when facing us,
                //   and slightly darker/hazier toward the transparent rim.
                float3 depthColor = crystalColor * (1.0 - _DepthColorDarken * (1.0 - NdotV));

                // ── 6. Prismatic iridescence (can be disabled) ─────
                float3 iridescence = float3(1, 1, 1);
                if (_IridescenceIntensity > 0.001)
                {
                    float  iridPhase = NdotV * _IridescenceThickness + _IridescenceShift;
                    float3 iridColor;
                    iridColor.r = sin(iridPhase * 6.28318 + 0.0)   * 0.5 + 0.5;
                    iridColor.g = sin(iridPhase * 6.28318 + 2.094) * 0.5 + 0.5;
                    iridColor.b = sin(iridPhase * 6.28318 + 4.189) * 0.5 + 0.5;
                    iridescence = lerp(float3(1, 1, 1), iridColor, _IridescenceIntensity);
                }
                float3 finalColor   = depthColor * iridescence;

                // ── 7. Chromatic refraction ───────────────────────────────
                //   R / G / B each offset the background UV by a slightly
                //   different amount, creating prismatic dispersion.
                float4 screenPosNorm = ScreenPos / ScreenPos.w;
                screenPosNorm.z = (UNITY_NEAR_CLIP_VALUE >= 0) ? screenPosNorm.z : screenPosNorm.z * 0.5 + 0.5;

                float3 refractDir     = refract(-WorldViewDirection, normalize(WorldNormal), 1.0 / _RefractionIOR);
                float3 refractOffset  = refractDir * _RefractionStrength;
                float3 refractView    = mul(UNITY_MATRIX_V, float4(refractOffset, 0)).xyz;
                float  aspect         = _ScreenParams.x / _ScreenParams.y;
                float2 refractUV      = float2(refractView.x / aspect, refractView.y);

                float2 baseRefractedUV = screenPosNorm.xy + refractUV;
                float2 boundary = min(baseRefractedUV, 1.0 - baseRefractedUV);
                float edgeFade = saturate(min(boundary.x, boundary.y) / 0.05);

                float ca = _ChromaticAberration;
                float3 sceneColor;
                if (ca < 0.001)
                {
                    // No chromatic aberration - single sample
                    float2 safeUV = lerp(screenPosNorm.xy, baseRefractedUV, edgeFade);
                    sceneColor = tex2D(_CameraOpaqueTexture, safeUV).rgb;
                }
                else
                {
                    // Chromatic aberration - RGB split (edge-safe)
                    float2 uvR = lerp(screenPosNorm.xy, screenPosNorm.xy + refractUV * (1.0 + ca), edgeFade);
                    float2 uvG = lerp(screenPosNorm.xy, screenPosNorm.xy + refractUV,               edgeFade);
                    float2 uvB = lerp(screenPosNorm.xy, screenPosNorm.xy + refractUV * (1.0 - ca), edgeFade);
                    float sceneR = tex2D(_CameraOpaqueTexture, uvR).r;
                    float sceneG = tex2D(_CameraOpaqueTexture, uvG).g;
                    float sceneB = tex2D(_CameraOpaqueTexture, uvB).b;
                    sceneColor = float3(sceneR, sceneG, sceneB);
                }

                // Tint the refracted background through the crystal body
                float3 tintedScene = sceneColor * lerp(float3(1, 1, 1), finalColor, 0.6);

                // ── 8. Inner glow emission ────────────────────────────────
                float emissionIntensity = _EmissionIntensity;
                #ifdef _EMISSION_PULSE_ON
                    emissionIntensity *= EmissionPulse(_Time.y, _EmissionPulseSpeed, _EmissionPulseMin, 1.0);
                #endif
                float3 finalEmission = _EmissionColor.rgb * finalColor * emissionIntensity;

                // Emission channel carries refracted scene + inner glow
                float3 combinedEmission = tintedScene + finalEmission;

                // ── 9. Crystal alpha ──────────────────────────────────────
                //   pow(NdotV, power)  →  1.0 when looking straight at face
                //                     →  0.0 at grazing rim angles
                float crystalAlpha = saturate(_CoreOpacity * pow(saturate(NdotV), _RimTransparencyPower));

                // ── 10. InputData / SurfaceData for URP PBR ──────────────
                InputData inputData = (InputData)0;
                inputData.positionWS             = WorldPosition;
                inputData.positionCS             = input.positionCS;
                inputData.viewDirectionWS        = WorldViewDirection;
                inputData.normalWS               = normalWS;

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
                    inputData.bakedGI    = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
                    inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                    inputData.bakedGI    = SAMPLE_GI(SH, GetAbsolutePositionWS(inputData.positionWS),
                        inputData.normalWS, inputData.viewDirectionWS, input.positionCS.xy,
                        input.probeOcclusion, inputData.shadowMask);
                #else
                    inputData.bakedGI    = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
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
                surfaceData.albedo              = finalColor;
                surfaceData.metallic            = saturate(_Metallic);
                surfaceData.specular            = float3(0.5, 0.5, 0.5);
                surfaceData.smoothness          = saturate(_Smoothness);
                surfaceData.occlusion           = 1;
                surfaceData.emission            = combinedEmission;
                surfaceData.alpha               = crystalAlpha;
                surfaceData.normalTS            = normalTS;
                surfaceData.clearCoatMask       = 0;
                surfaceData.clearCoatSmoothness = 1;

                #ifdef _DBUFFER
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // ── 11. POST-PBR: sparkle / facet highlights ──────────────
                //   Sample the normal map at a much higher frequency to get
                //   many tiny normal variations simulating gem facets.
                //   Each variation produces a very tight specular lobe —
                //   the result is scattered bright points like cut gemstones.
                {
                    Light mainLight = GetMainLight(ShadowCoords);
                    float mainAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                    // High-frequency normal for sparkle positions
                    float2 sparkleUV = uv * _SparkleScale;
                    float4 sparkleSample  = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, sparkleUV);
                    float3 sparkleNormalTS  = UnpackNormalScale(sparkleSample, _NormalStrength * 2.5);
                    float3 sparkleNormalWS  = NormalizeNormalPerPixel(
                        TransformTangentToWorld(sparkleNormalTS, half3x3(WorldTangent, WorldBiTangent, WorldNormal)));

                    float3 halfDir       = normalize(mainLight.direction + WorldViewDirection);

                    // Very tight lobe → tiny sparkle point
                    float  NDotH_sparkle = saturate(dot(sparkleNormalWS, halfDir));
                    float  sparkle       = pow(NDotH_sparkle, _SparklePower) * _SparkleIntensity * mainAtten;

                    // Tint sparkle with prismatic iridescence → colored fire
                    float3 sparkleCol = iridescence * _SpecularColor.rgb * mainLight.color;
                    color.rgb += sparkleCol * sparkle * crystalAlpha;

                    // Broad smooth highlight on the actual surface normal
                    float  NDotH_broad = saturate(dot(normalWS, halfDir));
                    float  broadSpec   = pow(NDotH_broad, _PrimarySpecPower) * mainAtten;
                    color.rgb += _SpecularColor.rgb * mainLight.color * broadSpec * crystalAlpha;

                    // Additional lights: sparkle contribution
                    #if defined(_ADDITIONAL_LIGHTS)
                    {
                        uint pixelLightCount = GetAdditionalLightsCount();
                        #if USE_FORWARD_PLUS
                        [loop] for (uint li2 = 0; li2 < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li2++)
                        {
                            FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
                            Light addLight = GetAdditionalLight(li2, WorldPosition, unity_ProbesOcclusion);
                            float addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                            float3 addHalf   = normalize(addLight.direction + WorldViewDirection);
                            float  addNDotH  = saturate(dot(sparkleNormalWS, addHalf));
                            float  addSparkle = pow(addNDotH, _SparklePower) * _SparkleIntensity * addAtten;
                            color.rgb += iridescence * _SpecularColor.rgb * addLight.color * addSparkle * crystalAlpha;
                            float  addBroad = pow(saturate(dot(normalWS, addHalf)), _PrimarySpecPower) * addAtten;
                            color.rgb += _SpecularColor.rgb * addLight.color * addBroad * crystalAlpha;
                        }
                        #endif
                        LIGHT_LOOP_BEGIN(pixelLightCount)
                            Light addLight = GetAdditionalLight(lightIndex, WorldPosition, unity_ProbesOcclusion);
                            float addAtten  = addLight.distanceAttenuation * addLight.shadowAttenuation;
                            float3 addHalf  = normalize(addLight.direction + WorldViewDirection);
                            float addNDotH  = saturate(dot(sparkleNormalWS, addHalf));
                            float addSparkle = pow(addNDotH, _SparklePower) * _SparkleIntensity * addAtten;
                            color.rgb += iridescence * _SpecularColor.rgb * addLight.color * addSparkle * crystalAlpha;
                            float addBroad = pow(saturate(dot(normalWS, addHalf)), _PrimarySpecPower) * addAtten;
                            color.rgb += _SpecularColor.rgb * addLight.color * addBroad * crystalAlpha;
                        LIGHT_LOOP_END
                    }
                    #endif
                }

                // ── 12. Fresnel rim catch-light ───────────────────────────
                //   The transparent rim still catches a thin bright halo.
                {
                    float rimFresnel = pow(1.0 - NdotV, 5.0);
                    color.rgb += _SpecularColor.rgb * iridescence * rimFresnel * 0.5;
                }

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
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            AlphaToMask Off
            ColorMask 0
            Cull Off

            HLSLPROGRAM

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE
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
                float4 _BaseColor; float4 _ColorB; float4 _ColorC;
                float _DepthColorDarken; float _Metallic; float _Smoothness;
                float _NormalStrength; float4 _NormalMap_ST;
                float _HeightStrength; float4 _HeightMap_ST;
                float _CoreOpacity; float _RimTransparencyPower;
                float _RefractionStrength; float _RefractionIOR; float _ChromaticAberration;
                float4 _SpecularColor; float _PrimarySpecPower;
                float _SparkleIntensity; float _SparkleScale; float _SparklePower;
                float _IridescenceIntensity; float _IridescenceThickness; float _IridescenceShift;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness; float _VoronoiScale;
                InputData inputData;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct PackedVaryings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            float3 _LightDirection;
            float3 _LightPosition;

            PackedVaryings vert(Attributes input)
            {
                PackedVaryings output = (PackedVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.positionCS = posCS;
                return output;
            }

            half4 frag(PackedVaryings input) : SV_Target { UNITY_SETUP_INSTANCE_ID(input); return 0; }

            ENDHLSL
        }

        // ====================================================================
        // PASS 2: DepthOnly
        // ====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM

            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

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
                float4 _BaseColor; float4 _ColorB; float4 _ColorC;
                float _DepthColorDarken; float _Metallic; float _Smoothness;
                float _NormalStrength; float4 _NormalMap_ST;
                float _HeightStrength; float4 _HeightMap_ST;
                float _CoreOpacity; float _RimTransparencyPower;
                float _RefractionStrength; float _RefractionIOR; float _ChromaticAberration;
                float4 _SpecularColor; float _PrimarySpecPower;
                float _SparkleIntensity; float _SparkleScale; float _SparklePower;
                float _IridescenceIntensity; float _IridescenceThickness; float _IridescenceShift;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness; float _VoronoiScale;
                InputData inputData;
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

            half4 frag(PackedVaryings input) : SV_Target { UNITY_SETUP_INSTANCE_ID(input); return 0; }

            ENDHLSL
        }

        // ====================================================================
        // PASS 3: DepthNormals
        // ====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            ZWrite On
            Cull Off

            HLSLPROGRAM

            #define _NORMALMAP 1
            #pragma multi_compile_instancing
            #pragma multi_compile _ LOD_FADE_CROSSFADE

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
                float4 _BaseColor; float4 _ColorB; float4 _ColorC;
                float _DepthColorDarken; float _Metallic; float _Smoothness;
                float _NormalStrength; float4 _NormalMap_ST;
                float _HeightStrength; float4 _HeightMap_ST;
                float _CoreOpacity; float _RimTransparencyPower;
                float _RefractionStrength; float _RefractionIOR; float _ChromaticAberration;
                float4 _SpecularColor; float _PrimarySpecPower;
                float _SparkleIntensity; float _SparkleScale; float _SparklePower;
                float _IridescenceIntensity; float _IridescenceThickness; float _IridescenceShift;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness; float _VoronoiScale;
                InputData inputData;
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
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(PackedVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }

            ENDHLSL
        }

        // ====================================================================
        // PASS 4: Meta (lightmap GI bake)
        // ====================================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off

            HLSLPROGRAM

            #define _EMISSION
            #pragma shader_feature_local _COLORMODE_SOLID _COLORMODE_GRADIENT _COLORMODE_VORONOI

            #pragma vertex vert
            #pragma fragment frag
            #define SHADERPASS SHADERPASS_META

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

            #include "../StainedGlass_Helpers.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float4 _ColorB; float4 _ColorC;
                float _DepthColorDarken; float _Metallic; float _Smoothness;
                float _NormalStrength; float4 _NormalMap_ST;
                float _HeightStrength; float4 _HeightMap_ST;
                float _CoreOpacity; float _RimTransparencyPower;
                float _RefractionStrength; float _RefractionIOR; float _ChromaticAberration;
                float4 _SpecularColor; float _PrimarySpecPower;
                float _SparkleIntensity; float _SparkleScale; float _SparklePower;
                float _IridescenceIntensity; float _IridescenceThickness; float _IridescenceShift;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness; float _VoronoiScale;
                InputData inputData;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct PackedVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            PackedVaryings vert(Attributes input)
            {
                PackedVaryings output = (PackedVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1.xy, input.uv2.xy);
                output.uv = input.uv0;
                return output;
            }

            half4 frag(PackedVaryings input) : SV_Target
            {
                float2 uv = input.uv;
                float3 crystalColor = _BaseColor.rgb;

                #if defined(_COLORMODE_GRADIENT)
                    crystalColor = GradientBlend(_BaseColor.rgb, _ColorB.rgb, uv, _GradientAngle, _GradientOffset, _GradientSharpness);
                #elif defined(_COLORMODE_VORONOI)
                    float2 cellID;
                    VoronoiPattern(uv, _VoronoiScale, cellID);
                    float cellHash = frac(sin(dot(cellID, float2(12.9898, 78.233))) * 43758.5453);
                    crystalColor = CellColorSmooth(cellID, _BaseColor.rgb, _ColorB.rgb, _ColorC.rgb, cellHash);
                #endif

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo   = crystalColor;
                metaInput.Emission = _EmissionColor.rgb * crystalColor * _EmissionIntensity;
                return UnityMetaFragment(metaInput);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
    CustomEditor "UnityEditor.ShaderGraphLitGUI"
}
