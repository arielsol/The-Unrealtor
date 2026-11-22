// Stained Glass Emissive Shader for URP
// Features: True glass transparency, self-emission, light reactivity,
//           colored light transmission, fresnel opacity, refraction,
//           color gradients, Voronoi patterns, and iridescence
// Compatible with Unity 6+ URP 17.x
Shader "StainedGlass/Emissive"
{
    Properties
    {
        [Header(Glass Appearance)]
        [Space(5)]
        _BaseColor("Glass Tint Color", Color) = (0.8, 0.1, 0.1, 0.25)
        _ColorB("Gradient Color B", Color) = (0.1, 0.1, 0.8, 0.25)
        _ColorC("Gradient Color C", Color) = (0.1, 0.8, 0.1, 0.25)
        _ColorD("Gradient Color D", Color) = (0.8, 0.8, 0.1, 0.25)
        _Metallic("Metallic", Range(0, 1)) = 0.1
        _Smoothness("Smoothness", Range(0, 1)) = 0.98

        [Header(Glass Transparency)]
        [Space(5)]
        _GlassOpacity("Glass Opacity", Range(0, 1)) = 0.2
        _GlassTintStrength("Glass Tint Strength", Range(0, 1)) = 0.6
        _FresnelOpacityPower("Fresnel Opacity Power", Range(0.5, 8)) = 3
        _FresnelOpacityScale("Fresnel Opacity Boost", Range(0, 1)) = 0.6
        [Toggle(_DOUBLE_SIDED)] _DoubleSided("Double Sided (3D objects)", Float) = 1

        [Header(Color Variation Mode)]
        [Space(5)]
        [KeywordEnum(Solid, Gradient, Voronoi, MultiZone)] _ColorMode("Color Mode", Float) = 0
        _GradientAngle("Gradient Angle", Range(0, 360)) = 45
        _GradientOffset("Gradient Offset", Range(-1, 1)) = 0
        _GradientSharpness("Gradient Sharpness", Range(0.1, 10)) = 1
        _VoronoiScale("Voronoi Scale", Range(1, 20)) = 5
        _VoronoiEdgeWidth("Voronoi Edge Width (Lead Lines)", Range(0, 0.15)) = 0.04
        _MultiZoneCount("Multi Zone Count", Range(1, 8)) = 3
        _MultiZoneBlend("Multi Zone Blend Softness", Range(0.01, 0.5)) = 0.1

        [Header(Self Emission)]
        [Space(5)]
        [HDR] _EmissionColor("Emission Color", Color) = (1, 0.3, 0.1, 1)
        _EmissionIntensity("Emission Intensity", Range(0, 10)) = 0.8
        [Toggle(_EMISSION_PULSE_ON)] _EmissionPulseEnabled("Enable Emission Pulse", Float) = 0
        _EmissionPulseSpeed("Pulse Speed", Range(0.1, 5)) = 1
        _EmissionPulseMin("Pulse Min Intensity", Range(0, 1)) = 0.5
        [Toggle(_EMISSION_EDGE_ON)] _EmissionEdgeEnabled("Enable Edge Emission", Float) = 0
        _EmissionEdgeWidth("Edge Width", Range(0.01, 0.3)) = 0.1
        _EmissionEdgePower("Edge Power", Range(0.5, 5)) = 2
        [Toggle(_USE_GLASS_COLOR_EMISSION)] _UseGlassColorEmission("Use Glass Color as Emission", Float) = 1

        [Header(Fresnel Reflection)]
        [Space(5)]
        [HDR] _FresnelColor("Fresnel Color", Color) = (1, 1, 1, 1)
        _FresnelPower("Fresnel Power", Float) = 4
        _FresnelScale("Fresnel Scale", Range(0, 3)) = 1.5

        [Header(Light Transmission and Translucency)]
        [Space(5)]
        _TransmissionTintStrength("Transmission Tint Strength", Range(0, 1)) = 0.7
        _Translucency("Translucency Strength", Range(0, 50)) = 5
        _TransNormal("Normal Distortion", Range(0, 1)) = 0.3
        _TransScattering("Scattering", Range(1, 50)) = 3
        _TransDirect("Direct Light", Range(0, 1)) = 0.8
        _TransAmbient("Ambient Light", Range(0, 1)) = 0.2
        _TransShadow("Shadow Strength", Range(0, 1)) = 0.5

        [Header(Refraction)]
        [Space(5)]
        _Refraction("Refraction Strength", Range(0, 1)) = 0.25
        _RefractionIOR("IOR", Range(1, 2)) = 1.52

        [Header(Iridescence)]
        [Space(5)]
        [Toggle(_IRIDESCENCE_ON)] _IridescenceEnabled("Enable Iridescence", Float) = 0
        _IridescenceThickness("Iridescence Thickness", Range(0.1, 5)) = 1.5
        _IridescenceIntensity("Iridescence Intensity", Range(0, 1)) = 0.3

        [Header(Lead Lines Pattern)]
        [Space(5)]
        [Toggle(_LEADLINES_ON)] _LeadLinesEnabled("Enable Lead Lines", Float) = 0
        _LeadLineWidth("Line Width", Range(0.001, 0.1)) = 0.02
        _LeadLineGridX("Grid X", Range(1, 20)) = 4
        _LeadLineGridY("Grid Y", Range(1, 20)) = 4
        _LeadLineColor("Lead Line Color", Color) = (0.1, 0.1, 0.1, 1)

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

            // Double-sided for 3D objects
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
            #define ASE_TRANSLUCENCY 1
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

            // Stained Glass feature keywords
            #pragma shader_feature_local _COLORMODE_SOLID _COLORMODE_GRADIENT _COLORMODE_VORONOI _COLORMODE_MULTIZONE
            #pragma shader_feature_local _EMISSION_PULSE_ON
            #pragma shader_feature_local _EMISSION_EDGE_ON
            #pragma shader_feature_local _USE_GLASS_COLOR_EMISSION
            #pragma shader_feature_local _IRIDESCENCE_ON
            #pragma shader_feature_local _LEADLINES_ON
            #pragma shader_feature_local _DOUBLE_SIDED

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

            // Our stained glass helpers
            #include "StainedGlass_Helpers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 texcoord : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 clipPosV : TEXCOORD0;
                float4 lightmapUVOrVertexSH : TEXCOORD1;
                half4 fogFactorAndVertexLight : TEXCOORD2;
                float4 tSpace0 : TEXCOORD3;
                float4 tSpace1 : TEXCOORD4;
                float4 tSpace2 : TEXCOORD5;
                float2 uv : TEXCOORD9;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD6;
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    float2 dynamicLightmapUV : TEXCOORD7;
                #endif
                #if defined(USE_APV_PROBE_OCCLUSION)
                    float4 probeOcclusion : TEXCOORD8;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Uniform buffer
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ColorB;
                float4 _ColorC;
                float4 _ColorD;
                float _Metallic;
                float _Smoothness;
                float _GlassOpacity;
                float _GlassTintStrength;
                float _FresnelOpacityPower;
                float _FresnelOpacityScale;
                float _GradientAngle;
                float _GradientOffset;
                float _GradientSharpness;
                float _VoronoiScale;
                float _VoronoiEdgeWidth;
                float _MultiZoneCount;
                float _MultiZoneBlend;
                float4 _EmissionColor;
                float _EmissionIntensity;
                float _EmissionPulseSpeed;
                float _EmissionPulseMin;
                float _EmissionEdgeWidth;
                float _EmissionEdgePower;
                float4 _FresnelColor;
                float _FresnelPower;
                float _FresnelScale;
                float _TransmissionTintStrength;
                float _Refraction;
                float _RefractionIOR;
                float _IridescenceThickness;
                float _IridescenceIntensity;
                float _LeadLineWidth;
                float _LeadLineGridX;
                float _LeadLineGridY;
                float4 _LeadLineColor;

                // Translucency
                float _Translucency;
                float _TransNormal;
                float _TransScattering;
                float _TransDirect;
                float _TransAmbient;
                float _TransShadow;
            CBUFFER_END

            sampler2D _CameraOpaqueTexture;

            // Additional lights helper
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

            half4 GetShadowMaskValue()
            {
                #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
                    half4 shadowMask = inputData.shadowMask;
                #elif !defined(LIGHTMAP_ON)
                    half4 shadowMask = unity_ProbesOcclusion;
                #else
                    half4 shadowMask = half4(1, 1, 1, 1);
                #endif
                return shadowMask;
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
                output.uv = input.texcoord.xy;

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
                float2 uv = input.uv;

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
                // FRESNEL - core glass behaviour
                // Glass is transparent at center, reflective at edges
                // =============================================================
                float NdotV = saturate(dot(WorldNormal, WorldViewDirection));
                float fresnel = pow(1.0 - NdotV, _FresnelPower) * _FresnelScale;
                float fresnelOpacity = pow(1.0 - NdotV, _FresnelOpacityPower) * _FresnelOpacityScale;

                // =============================================================
                // Calculate Stained Glass Color based on selected mode
                // =============================================================
                float3 glassColor = _BaseColor.rgb;
                float leadLineMask = 1.0;

                #if defined(_COLORMODE_GRADIENT)
                    glassColor = GradientBlend(_BaseColor.rgb, _ColorB.rgb, uv, _GradientAngle, _GradientOffset, _GradientSharpness);
                #elif defined(_COLORMODE_VORONOI)
                    float2 cellID;
                    float voronoiDist = VoronoiPattern(uv, _VoronoiScale, cellID);
                    float cellHash = frac(sin(dot(cellID, float2(12.9898, 78.233))) * 43758.5453);
                    glassColor = CellColorSmooth(cellID, _BaseColor.rgb, _ColorB.rgb, _ColorC.rgb, cellHash);
                    float voronoiEdge = smoothstep(_VoronoiEdgeWidth, _VoronoiEdgeWidth + 0.01, voronoiDist);
                    leadLineMask = voronoiEdge;
                #elif defined(_COLORMODE_MULTIZONE)
                    glassColor = MultiZoneColor(_BaseColor.rgb, _ColorB.rgb, _ColorC.rgb, uv, _MultiZoneCount, _MultiZoneBlend);
                #endif

                #ifdef _LEADLINES_ON
                    float leadPattern = LeadLines(uv, _LeadLineWidth, _LeadLineGridX, _LeadLineGridY);
                    leadLineMask *= leadPattern;
                #endif

                // Lead lines are opaque dark, glass areas are transparent
                float3 surfaceColor = lerp(_LeadLineColor.rgb, glassColor, leadLineMask);

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

                // Tint the refracted scene by the glass color
                // This is the key "looking through colored glass" effect
                float3 tintedScene = lerp(sceneColor, sceneColor * glassColor, _GlassTintStrength);

                // =============================================================
                // EMISSION - subtle self-glow
                // =============================================================
                float3 emissionColor;
                #ifdef _USE_GLASS_COLOR_EMISSION
                    emissionColor = glassColor * _EmissionColor.rgb;
                #else
                    emissionColor = _EmissionColor.rgb;
                #endif

                float emissionIntensity = _EmissionIntensity;

                #ifdef _EMISSION_PULSE_ON
                    emissionIntensity *= EmissionPulse(_Time.y, _EmissionPulseSpeed, _EmissionPulseMin, 1.0);
                #endif

                #ifdef _EMISSION_EDGE_ON
                    float edgeGlow = EdgeEmission(uv, _EmissionEdgeWidth, _EmissionEdgePower);
                    emissionIntensity += edgeGlow * _EmissionIntensity * 0.5;
                #endif

                emissionIntensity *= leadLineMask;
                float3 finalEmission = emissionColor * emissionIntensity;

                // =============================================================
                // IRIDESCENCE
                // =============================================================
                #ifdef _IRIDESCENCE_ON
                    float grazingFade = smoothstep(0.0, 0.3, NdotV);
                    float3 iridescence = ThinFilmIridescence(NdotV, _IridescenceThickness, _IridescenceIntensity * grazingFade);
                    surfaceColor *= iridescence;
                    finalEmission *= iridescence;
                #endif

                // =============================================================
                // Fresnel reflection contribution
                // =============================================================
                float3 fresnelContribution = _FresnelColor.rgb * _FresnelColor.a * fresnel;

                // =============================================================
                // COMBINE: glass transparency model
                //
                // The key insight for realistic glass:
                // - The refracted/tinted scene shows through (weighted by transparency)
                // - Emission adds a soft colored glow on top
                // - Fresnel adds bright edges
                // - Translucency adds light from behind
                // =============================================================

                // Refracted scene is the BASE of our emission channel
                // (transparent objects in URP use emission for see-through)
                float3 combinedEmission = tintedScene + finalEmission + fresnelContribution;

                // Add light-reactive transmission tint (lights tinted by glass color)
                Light mainLight = GetMainLight(ShadowCoords);
                float mainLightAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half4 shadowMaskValue = GetShadowMaskValue();
                float3 additionalLightColor = AdditionalLightsFlat(WorldPosition, NormalizedScreenSpaceUV, shadowMaskValue);
                float3 totalLightColor = mainLight.color * mainLightAtten + additionalLightColor;
                float3 tintedLightContribution = totalLightColor * glassColor * _TransmissionTintStrength * 0.3;
                combinedEmission += tintedLightContribution;

                // =============================================================
                // ALPHA: glass transparency with fresnel edges
                //
                // Low alpha center = see-through glass
                // Higher alpha edges = reflective rim (like real glass)
                // Lead lines = fully opaque
                // =============================================================
                float glassAlpha = _GlassOpacity + fresnelOpacity;
                float finalAlpha = lerp(1.0, saturate(glassAlpha), leadLineMask);

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
                // Translucency - colored light passing through
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
                        color.rgb += surfaceColor * translucency * strength;

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
            Tags { "LightMode"="ShadowCaster" }

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
            #define _NORMALMAP 1

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
                float4 _BaseColor; float4 _ColorB; float4 _ColorC; float4 _ColorD;
                float _Metallic; float _Smoothness;
                float _GlassOpacity; float _GlassTintStrength;
                float _FresnelOpacityPower; float _FresnelOpacityScale;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness;
                float _VoronoiScale; float _VoronoiEdgeWidth;
                float _MultiZoneCount; float _MultiZoneBlend;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _EmissionEdgeWidth; float _EmissionEdgePower;
                float4 _FresnelColor; float _FresnelPower; float _FresnelScale;
                float _TransmissionTintStrength; float _Refraction; float _RefractionIOR;
                float _IridescenceThickness; float _IridescenceIntensity;
                float _LeadLineWidth; float _LeadLineGridX; float _LeadLineGridY;
                float4 _LeadLineColor;
                float _Translucency; float _TransNormal; float _TransScattering;
                float _TransDirect; float _TransAmbient; float _TransShadow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
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
            Tags { "LightMode"="DepthOnly" }

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
                float4 _BaseColor; float4 _ColorB; float4 _ColorC; float4 _ColorD;
                float _Metallic; float _Smoothness;
                float _GlassOpacity; float _GlassTintStrength;
                float _FresnelOpacityPower; float _FresnelOpacityScale;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness;
                float _VoronoiScale; float _VoronoiEdgeWidth;
                float _MultiZoneCount; float _MultiZoneBlend;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _EmissionEdgeWidth; float _EmissionEdgePower;
                float4 _FresnelColor; float _FresnelPower; float _FresnelScale;
                float _TransmissionTintStrength; float _Refraction; float _RefractionIOR;
                float _IridescenceThickness; float _IridescenceIntensity;
                float _LeadLineWidth; float _LeadLineGridX; float _LeadLineGridY;
                float4 _LeadLineColor;
                float _Translucency; float _TransNormal; float _TransScattering;
                float _TransDirect; float _TransAmbient; float _TransShadow;
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
            Tags { "LightMode"="DepthNormals" }

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
                float4 _BaseColor; float4 _ColorB; float4 _ColorC; float4 _ColorD;
                float _Metallic; float _Smoothness;
                float _GlassOpacity; float _GlassTintStrength;
                float _FresnelOpacityPower; float _FresnelOpacityScale;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness;
                float _VoronoiScale; float _VoronoiEdgeWidth;
                float _MultiZoneCount; float _MultiZoneBlend;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _EmissionEdgeWidth; float _EmissionEdgePower;
                float4 _FresnelColor; float _FresnelPower; float _FresnelScale;
                float _TransmissionTintStrength; float _Refraction; float _RefractionIOR;
                float _IridescenceThickness; float _IridescenceIntensity;
                float _LeadLineWidth; float _LeadLineGridX; float _LeadLineGridY;
                float4 _LeadLineColor;
                float _Translucency; float _TransNormal; float _TransScattering;
                float _TransDirect; float _TransAmbient; float _TransShadow;
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

        // ====================================================================
        // PASS 4: Meta (lightmap GI)
        // ====================================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off

            HLSLPROGRAM

            #define _NORMAL_DROPOFF_TS 1
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _EMISSION

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

            #include "StainedGlass_Helpers.hlsl"

            #pragma shader_feature_local _COLORMODE_SOLID _COLORMODE_GRADIENT _COLORMODE_VORONOI _COLORMODE_MULTIZONE
            #pragma shader_feature_local _USE_GLASS_COLOR_EMISSION

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float4 _ColorB; float4 _ColorC; float4 _ColorD;
                float _Metallic; float _Smoothness;
                float _GlassOpacity; float _GlassTintStrength;
                float _FresnelOpacityPower; float _FresnelOpacityScale;
                float _GradientAngle; float _GradientOffset; float _GradientSharpness;
                float _VoronoiScale; float _VoronoiEdgeWidth;
                float _MultiZoneCount; float _MultiZoneBlend;
                float4 _EmissionColor; float _EmissionIntensity;
                float _EmissionPulseSpeed; float _EmissionPulseMin;
                float _EmissionEdgeWidth; float _EmissionEdgePower;
                float4 _FresnelColor; float _FresnelPower; float _FresnelScale;
                float _TransmissionTintStrength; float _Refraction; float _RefractionIOR;
                float _IridescenceThickness; float _IridescenceIntensity;
                float _LeadLineWidth; float _LeadLineGridX; float _LeadLineGridY;
                float4 _LeadLineColor;
                float _Translucency; float _TransNormal; float _TransScattering;
                float _TransDirect; float _TransAmbient; float _TransShadow;
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
                float3 glassColor = _BaseColor.rgb;

                #if defined(_COLORMODE_GRADIENT)
                    glassColor = GradientBlend(_BaseColor.rgb, _ColorB.rgb, uv, _GradientAngle, _GradientOffset, _GradientSharpness);
                #elif defined(_COLORMODE_VORONOI)
                    float2 cellID;
                    VoronoiPattern(uv, _VoronoiScale, cellID);
                    float cellHash = frac(sin(dot(cellID, float2(12.9898, 78.233))) * 43758.5453);
                    glassColor = CellColorSmooth(cellID, _BaseColor.rgb, _ColorB.rgb, _ColorC.rgb, cellHash);
                #elif defined(_COLORMODE_MULTIZONE)
                    glassColor = MultiZoneColor(_BaseColor.rgb, _ColorB.rgb, _ColorC.rgb, uv, _MultiZoneCount, _MultiZoneBlend);
                #endif

                float3 emissionColor;
                #ifdef _USE_GLASS_COLOR_EMISSION
                    emissionColor = glassColor * _EmissionColor.rgb;
                #else
                    emissionColor = _EmissionColor.rgb;
                #endif

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = glassColor;
                metaInput.Emission = emissionColor * _EmissionIntensity;
                return UnityMetaFragment(metaInput);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
    CustomEditor "UnityEditor.ShaderGraphLitGUI"
}
