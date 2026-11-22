// Volumetric Fog - Screen-space ray marching for URP
// Supports: Directional light god rays, point/spot light scattering,
//           height-based density, noise variation, Henyey-Greenstein phase.
// Works with all Unity light types including stained glass colored lights.
Shader "Hidden/StainedGlass/VolumetricFog"
{
    Properties
    {
        _MainTex("Scene Color", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "VolumetricFogPass"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            // Shadow keywords for main light
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // Additional lights
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            // Forward+
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Fog parameters (set from C# via VolumetricFogFeature)
            float _VFogDensity;
            float _VFogMaxDistance;
            float _VFogAnisotropy;
            float _VFogSteps;
            float _VFogLightIntensity;
            float4 _VFogColor;
            float _VFogHeightFalloff;
            float _VFogBaseHeight;
            float _VFogNoiseScale;
            float _VFogNoiseIntensity;
            float _VFogJitter;
            float _VFogAdditionalIntensity;
            float _VFogAmbientIntensity;
            int _VFogMaxAdditionalLights;

            // --------------------------------------------------------
            // Henyey-Greenstein phase function
            // g > 0: forward scattering (god rays toward camera)
            // g < 0: backward scattering
            // g = 0: isotropic
            // --------------------------------------------------------
            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = 1.0 + g2 - 2.0 * g * cosTheta;
                return (1.0 - g2) / (4.0 * PI * pow(max(denom, 1e-5), 1.5));
            }

            // --------------------------------------------------------
            // Simple 3D value noise for fog density variation
            // --------------------------------------------------------
            float Hash3D(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float n000 = Hash3D(i);
                float n100 = Hash3D(i + float3(1, 0, 0));
                float n010 = Hash3D(i + float3(0, 1, 0));
                float n110 = Hash3D(i + float3(1, 1, 0));
                float n001 = Hash3D(i + float3(0, 0, 1));
                float n101 = Hash3D(i + float3(1, 0, 1));
                float n011 = Hash3D(i + float3(0, 1, 1));
                float n111 = Hash3D(i + float3(1, 1, 1));

                float n00 = lerp(n000, n100, f.x);
                float n10 = lerp(n010, n110, f.x);
                float n01 = lerp(n001, n101, f.x);
                float n11 = lerp(n011, n111, f.x);

                float n0 = lerp(n00, n10, f.y);
                float n1 = lerp(n01, n11, f.y);

                return lerp(n0, n1, f.z);
            }

            // --------------------------------------------------------
            // Fog density at a world position
            // --------------------------------------------------------
            float GetFogDensity(float3 worldPos)
            {
                float density = _VFogDensity;

                // Height-based exponential falloff
                float heightDelta = worldPos.y - _VFogBaseHeight;
                float heightFactor = exp(-max(heightDelta, 0) * _VFogHeightFalloff);
                // Below base height, density is full
                heightFactor = heightDelta < 0 ? 1.0 : heightFactor;
                density *= heightFactor;

                // Noise variation (slowly animated)
                if (_VFogNoiseIntensity > 0.001)
                {
                    float3 noiseCoord = worldPos * _VFogNoiseScale + float3(0, 0, _Time.y * 0.15);
                    float noise = ValueNoise3D(noiseCoord);
                    // Two-octave for richer variation
                    noise = noise * 0.7 + ValueNoise3D(noiseCoord * 2.3 + 7.7) * 0.3;
                    density *= lerp(1.0, saturate(noise + 0.3), _VFogNoiseIntensity);
                }

                return max(density, 0);
            }

            // --------------------------------------------------------
            // Vertex / Fragment
            // --------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Scene color
                float4 sceneColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Scene depth → world position
                float rawDepth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 camPos = GetCameraPositionWS();

                float3 toScene = worldPos - camPos;
                float rayLength = length(toScene);
                float3 rayDir = toScene / max(rayLength, 1e-5);

                // Clamp ray length (handles sky pixels & far geometry)
                rayLength = min(rayLength, _VFogMaxDistance);

                // Check if this is a sky pixel (very far)
                float linearDepth = Linear01Depth(rawDepth, _ZBufferParams);
                if (linearDepth > 0.999)
                    rayLength = _VFogMaxDistance;

                // Ray march parameters
                int steps = (int)_VFogSteps;
                float stepSize = rayLength / max(float(steps), 1.0);

                // Temporal jitter to reduce banding
                float jitter = Hash3D(float3(uv * _ScreenParams.xy, frac(_Time.y * 47.3))) * _VFogJitter;

                // Main light info for phase function
                Light mainLight = GetMainLight();
                float mainCosTheta = dot(rayDir, mainLight.direction);
                float mainPhase = HenyeyGreenstein(mainCosTheta, _VFogAnisotropy);

                // Also compute a softer isotropic phase for fill
                float isoPhase = 1.0 / (4.0 * PI);

                // Accumulate scattering and transmittance
                float3 totalScattering = 0;
                float totalTransmittance = 1.0;

                for (int i = 0; i < steps; i++)
                {
                    float t = (float(i) + 0.5 + jitter) / float(steps);
                    float3 samplePos = camPos + rayDir * t * rayLength;

                    // Fog density at this point
                    float density = GetFogDensity(samplePos);
                    if (density < 1e-5) continue;

                    // --- Main directional light contribution ---
                    // Sample shadow map at this position
                    float4 shadowCoord = TransformWorldToShadowCoord(samplePos);
                    float shadow = MainLightRealtimeShadow(shadowCoord);

                    float3 mainScatter = mainLight.color * shadow * mainPhase * _VFogLightIntensity;

                    // --- Additional lights (point, spot) ---
                    // This is how stained glass colored lights interact with the fog
                    float3 additionalScatter = 0;
                    #if defined(_ADDITIONAL_LIGHTS)
                    {
                        uint lightCount = min(GetAdditionalLightsCount(), (uint)_VFogMaxAdditionalLights);
                        for (uint j = 0; j < lightCount; j++)
                        {
                            Light addLight = GetAdditionalLight(j, samplePos);
                            float addCos = dot(rayDir, addLight.direction);
                            float addPhase = HenyeyGreenstein(addCos, _VFogAnisotropy * 0.5);
                            float addAtten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                            additionalScatter += addLight.color * addAtten * addPhase;
                        }
                    }
                    #endif
                    additionalScatter *= _VFogAdditionalIntensity;

                    // --- Ambient / fog base color ---
                    float3 ambientScatter = _VFogColor.rgb * isoPhase * _VFogAmbientIntensity;

                    // Total in-scattering at this step
                    float3 stepScatter = (mainScatter + additionalScatter + ambientScatter) * density;

                    // Beer-Lambert extinction
                    float extinction = density * stepSize;
                    float stepTransmittance = exp(-extinction);

                    // Energy-conserving accumulation
                    // Using the improved integration: (scatter / extinction) * (1 - transmittance)
                    float3 scatterIntegrated = stepScatter * (1.0 - stepTransmittance) / max(density, 1e-5);
                    totalScattering += scatterIntegrated * totalTransmittance;
                    totalTransmittance *= stepTransmittance;

                    // Early exit when fog is very thick
                    if (totalTransmittance < 0.01) break;
                }

                // Composite: scene * remaining transmittance + accumulated scattering
                float3 result = sceneColor.rgb * totalTransmittance + totalScattering;

                return half4(result, sceneColor.a);
            }

            ENDHLSL
        }
    }
    FallBack Off
}
