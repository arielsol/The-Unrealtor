Shader "Unlit/PBRTest"
{
    Properties
    {
        _MainTex ("BaseColor", 2D) = "white" {}
        _Metalic ("Metalic", 2D) = "white" {}
        _MetalicStrength("Metalic Strength", Range(0, 1)) = 1
        _Roughness ("Roughness", 2D) = "white" {}
        _SmoothnessStrength("Roughness Strength", Range(0, 1)) = 1
        _Normal ("Normal", 2D) = "white" {}
        _Occlusion ("Occlusion", 2D) = "white" {}
        _AnisotropyWidth ("Anisotropy Width", Range(0.1, 1)) = 1
        [Toggle] _EnvSpecON ("EnvSpecOn", Integer) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode" = "UniversalForward"}
        LOD 100

        Pass
        {
            Name "DepthOnlyPass"
            Tags { "LightMode" = "DepthOnly" }

            // Write depth to the depth buffer
            ZWrite On

            // Don't write to the color buffer
            ColorMask 0

        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma shader_feature USE_PLANAR_REFLECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
            #include "HLSLUtils/BRDFUtil.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float3 normalWS : NORMAL;
                float3 tangentWS : TANGENT;
                float4 screenPos :TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _Metalic;
            float4 _Metalic_ST;
            sampler2D _Roughness;
            float4 _Roughness_ST;
            sampler2D _Normal;
            float4 _Normal_ST;
            sampler2D _Occlusion;
            float4 _Occlusion_ST;

            float3 _ReflectionCameraWS;
            float3 _MirrorNormalWS;

            sampler2D _PerObjectSceneShadowMap;
            float4 _PerObjectSceneShadowMap_ST;

            float _SmoothnessStrength;
            float _MetalicStrength;
            float _AnisotropyWidth;

            int _EnvSpecON;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = normalize(mul(unity_ObjectToWorld, v.normal));
                o.tangentWS = normalize(mul(unity_ObjectToWorld, v.tangent.xyz));
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangent.w;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                //Sample Textures
                float4 albedoCol = tex2D(_MainTex, i.uv);
                float3 normalCol = UnpackNormal(tex2D(_Normal, i.uv));
                float metalic = tex2D(_Metalic, i.uv).r * (_MetalicStrength);
                float roughness = 1 - tex2D(_Roughness, i.uv).r * max((_SmoothnessStrength), 0.001);
                float smoothness = max(0.01, 1 - roughness);
                float occlusion = tex2D(_Occlusion, i.uv).r;

                 //Vectors
                half3 normalTS = normalize(normalCol.rgb);
                float3 bitangentWS = i.bitangentWS;
                float3x3 tbn = transpose(float3x3(i.tangentWS, bitangentWS, i.normalWS));
                float3 normalWS = normalize(mul(tbn, normalTS));
                float3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, i.normalWS));
                float3 tangentWS = normalize(cross(bitangentWS, normalWS));
                float3 bitangentWSTexture = normalize(cross(normalWS, tangentWS));
                float3 camPosWS = _WorldSpaceCameraPos.xyz;
                float3 viewDir = normalize(camPosWS - i.worldPos.xyz);
                #if USE_PLANAR_REFLECTION
                    normalWS = reflect(normalWS, _MirrorNormalWS);
                #endif
                
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfVec = normalize(lightDir + viewDir);
                float cameraDist = length(camPosWS - i.worldPos.xyz);
                
                //Dot products
                float NdotV = max(dot(normalWS, viewDir), 0.0f);
                float LdotH = max(dot(lightDir, halfVec), 0.0f);
                float NdotH = max(dot(normalWS, halfVec), 0.0f);
                float HdotV = max(dot(halfVec, viewDir), 0.0f);
                float NdotL_raw = dot(normalWS, lightDir);
                float VdotL = dot(viewDir, lightDir);
                float NdotL = saturate(NdotL_raw);
                float halfLambert_vertex = dot(i.normalWS, lightDir) * 0.5 + 0.5;
                float halfLambert_texture = NdotL_raw * 0.5 + 0.5;

                //Shadow
                float2 shadowUV = i.screenPos.xy / i.screenPos.w;
                float shadowAtten = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.worldPos));
                float perObjShadowAtten = tex2D(_PerObjectSceneShadowMap, shadowUV).r;
                shadowAtten = min(perObjShadowAtten, shadowAtten);
                #if USE_PLANAR_REFLECTION
                    shadowAtten = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.worldPos));
                #endif
                //Disney Diffuse
                float disneyDiffuse = min(CustomDisneyDiffuse(NdotV, NdotL, LdotH, roughness), 1);
                float3 directDiffuseColor = albedoCol.rgb * halfLambert_texture;

                //Environmental Diffuse
                float3 SH = SampleSH(normalWS);
                float3 envDiffuse = SH * directDiffuseColor * occlusion;

                //Final Diffuse
                float3 finalDiffuse = directDiffuseColor * mainLight.color * shadowAtten + envDiffuse; 

                //Anisotropy
                float anisotropy = StrandSpecular(bitangentWSTexture, viewDir, lightDir, smoothness, 1 / _AnisotropyWidth);
                //Direct Specular
                float D = GGX(NdotH, smoothness);
                float G = schlickBeckmannGAF(NdotV, roughness) * schlickBeckmannGAF(NdotL, roughness);
                float3 F0 = lerp(0.04, albedoCol.rgb, metalic);
                float3 F = FresnelTermColor(F0, NdotV);
                float3 nom = D * G * F;
                float3 denom = (4 * max(NdotV, 0.0) * max(NdotL, 0.0) + 0.1);
                float3 directSpec = nom / denom;
                float3  KD  = float3(1, 1, 1) - F;
                KD *= 1.0 - metalic;
                //Env Specular
                half3 envSpec = GlossyEnvironmentReflection(reflect(-viewDir, normalWS), i.worldPos, smoothness, occlusion);
                
                //Final Specular
                float3 finalSpec = directSpec * mainLight.color * shadowAtten + envSpec * F * roughness * _EnvSpecON;

                //Final Composite
                float3 finalColor = finalDiffuse * KD + finalSpec;

                //Additional Lights
                int additionalLightCount = GetAdditionalLightsCount();
                for (int index = 0; index < additionalLightCount; index++) {
                    Light light = GetAdditionalLight(index, i.worldPos);
                    float3 lightDir = normalize(light.direction);
                    float3 halfVec = normalize(viewDir + lightDir);
                    float atten = light.distanceAttenuation;
                    float3 radiance = light.color * occlusion * atten;

                    float NdotL = saturate(dot(i.normalWS, lightDir));
                    float NdotV = saturate(dot(i.normalWS, viewDir));
                    float NdotH = max(dot(normalWS, halfVec), 0.0f);

                    float D = GGX(NdotH, smoothness);
                    float G = schlickBeckmannGAF(NdotV, roughness) * schlickBeckmannGAF(NdotL, roughness);
                    float3 F0 = lerp(0.04, albedoCol.rgb, metalic);
                    float3 F = FresnelTermColor(F0, NdotV);
                    float3 denom = (4 * max(NdotV, 0.0) * max(NdotL, 0.0) + 0.001);
                    float3 specular = D * G * F / denom;

                    float3 kS = F;
                    float3 kD = float3(1, 1, 1) - kS;
                    kD *= 1.0 - metalic;
                    
                    finalColor += (kD * albedoCol.rgb / PI + specular) * radiance * NdotL;
                }
                
                return float4(finalColor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PerObjectShadowCaster"
            Tags{"LightMode" = "PerObjectShadowCaster"}
            ZWrite On
            ColorMask 0
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
