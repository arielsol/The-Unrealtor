// Fullscreen Stylization Post-Processing Shader for URP
// Anime outlines (depth+normal edge detection), posterization,
// saturation boost, halftone shadows, film grain.
// Compatible with Unity 6+ URP 17.x
Shader "Hidden/StainedGlass/Stylization"
{
    Properties
    {
        _MainTex ("Scene Color", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "StylizationPass"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            // --- Uniforms (set from C# StylizationFeature) ---
            float  _StylIntensity;

            // Outlines
            float4 _StylOutlineColor;
            float  _StylOutlineWidth;
            float  _StylDepthSens;
            float  _StylNormalSens;
            float  _StylDepthThresh;
            float  _StylNormalThresh;

            // Posterization
            float  _StylPosterLevels;
            float  _StylPosterStr;

            // Saturation
            float  _StylSatBoost;

            // Noise
            float  _StylNoiseStr;
            float  _StylNoiseScale;
            float  _StylNoiseSpeed;

            // Halftone
            float  _StylHalftoneStr;
            float  _StylHalftoneScale;
            float  _StylHalftoneShadowThresh;

            // Distance fade
            float  _StylOutlineFadeStart;
            float  _StylOutlineFadeEnd;

            // Rim Light
            float4 _StylRimColor;
            float  _StylRimIntensity;
            float  _StylRimPower;

            // =================================================================
            // Vertex
            // =================================================================
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // =================================================================
            // Helper: Roberts Cross Edge Detection (Depth + Normals)
            // 4 diagonal taps each — clean anime-style outlines
            // =================================================================
            float ComputeEdge(float2 uv)
            {
                float2 texel = _CameraDepthTexture_TexelSize.xy * _StylOutlineWidth;

                // Clamp UV to prevent edge artifacts
                float2 uvMin = texel;
                float2 uvMax = 1.0 - texel;

                float2 uv00 = clamp(uv + float2(-1, -1) * texel, uvMin, uvMax);
                float2 uv11 = clamp(uv + float2( 1,  1) * texel, uvMin, uvMax);
                float2 uv01 = clamp(uv + float2(-1,  1) * texel, uvMin, uvMax);
                float2 uv10 = clamp(uv + float2( 1, -1) * texel, uvMin, uvMax);

                // --- Depth edges ---
                float d00 = Linear01Depth(SampleSceneDepth(uv00), _ZBufferParams);
                float d11 = Linear01Depth(SampleSceneDepth(uv11), _ZBufferParams);
                float d01 = Linear01Depth(SampleSceneDepth(uv01), _ZBufferParams);
                float d10 = Linear01Depth(SampleSceneDepth(uv10), _ZBufferParams);

                float depthEdge = abs(d00 - d11) + abs(d01 - d10);
                depthEdge = saturate(
                    (depthEdge * _StylDepthSens - _StylDepthThresh)
                    / max(1.0 - _StylDepthThresh, 0.001));

                // --- Normal edges ---
                float3 n00 = SampleSceneNormals(uv00);
                float3 n11 = SampleSceneNormals(uv11);
                float3 n01 = SampleSceneNormals(uv01);
                float3 n10 = SampleSceneNormals(uv10);

                float3 nDiff1 = n00 - n11;
                float3 nDiff2 = n01 - n10;
                float normalEdge = sqrt(dot(nDiff1, nDiff1) + dot(nDiff2, nDiff2));
                normalEdge = saturate(
                    (normalEdge * _StylNormalSens - _StylNormalThresh)
                    / max(1.0 - _StylNormalThresh, 0.001));

                return saturate(depthEdge + normalEdge);
            }

            // =================================================================
            // Helper: Color Posterization
            // =================================================================
            float3 Posterize(float3 color, float levels, float strength)
            {
                float3 posterized = floor(color * levels + 0.5) / levels;
                return lerp(color, posterized, strength);
            }

            // =================================================================
            // Helper: Saturation Adjustment (BT.709 luminance)
            // =================================================================
            float3 AdjustSaturation(float3 color, float boost)
            {
                float lum = dot(color, float3(0.2126, 0.7152, 0.0722));
                return lerp(float3(lum, lum, lum), color, boost);
            }

            // =================================================================
            // Helper: Hash-based Film Grain (no texture needed)
            // =================================================================
            float FilmGrain(float2 uv, float scale, float speed)
            {
                float2 p = uv * scale + float2(_Time.y * speed, _Time.y * speed * 0.7);
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // =================================================================
            // Helper: Halftone / Crosshatch Pattern for Shadow Areas
            // =================================================================
            float Halftone(float2 screenPos, float luminance, float scale, float threshold)
            {
                float2 p = screenPos * scale / 100.0;

                // Primary circular dot pattern
                float2 grid = frac(p) - 0.5;
                float dotPattern = length(grid);

                // Secondary crosshatch at 45 degrees
                float s = 0.7071; // sin(45)
                float c = 0.7071; // cos(45)
                float2 rotP = float2(p.x * c - p.y * s, p.x * s + p.y * c);
                float2 grid2 = frac(rotP) - 0.5;
                float crossPattern = min(abs(grid2.x), abs(grid2.y));

                // Blend both patterns
                float pattern = lerp(dotPattern, crossPattern * 2.0, 0.5);

                // Only in shadow areas (below luminance threshold)
                float shadowMask = 1.0 - smoothstep(threshold - 0.1, threshold, luminance);

                return shadowMask * step(pattern, 0.5 - luminance * 0.4);
            }

            // =================================================================
            // Fragment Shader — compose all effects
            // =================================================================
            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 1. Read scene color
                float4 sceneColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float3 color = sceneColor.rgb;

                // 2. Edge detection (anime outlines) with distance fade
                float edge = ComputeEdge(uv);

                // Fade outlines based on camera distance (LinearEyeDepth = world units)
                float centerDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float fadeFactor = 1.0 - saturate(
                    (centerDepth - _StylOutlineFadeStart)
                    / max(_StylOutlineFadeEnd - _StylOutlineFadeStart, 0.001));
                edge *= fadeFactor;

                // 3. Posterize colors (cel-shading)
                color = Posterize(color, _StylPosterLevels, _StylPosterStr);

                // 4. Saturation boost (vivid colors)
                color = AdjustSaturation(color, _StylSatBoost);

                // 5. Halftone/crosshatch in shadow areas
                float lum = dot(color, float3(0.2126, 0.7152, 0.0722));
                float2 screenPixel = uv * _ScreenParams.xy;
                float ht = Halftone(screenPixel, lum,
                                    _StylHalftoneScale, _StylHalftoneShadowThresh);
                color = lerp(color, color * 0.3, ht * _StylHalftoneStr);

                // 6. Rim light (fresnel-based, using scene normals + reconstructed view dir)
                if (_StylRimIntensity > 0.001)
                {
                    float3 normalWS = SampleSceneNormals(uv);
                    // Reconstruct world position from depth, then derive view direction
                    float rawDepth = SampleSceneDepth(uv);
                    float2 posNDC = uv * 2.0 - 1.0;
                    #if UNITY_UV_STARTS_AT_TOP
                        posNDC.y = -posNDC.y;
                    #endif
                    float4 posCS = float4(posNDC, rawDepth, 1.0);
                    float4 posWS = mul(UNITY_MATRIX_I_VP, posCS);
                    posWS.xyz /= posWS.w;
                    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - posWS.xyz);

                    float NdotV = saturate(dot(normalWS, viewDir));
                    float rim = pow(1.0 - NdotV, _StylRimPower) * _StylRimIntensity;
                    color += _StylRimColor.rgb * rim;
                }

                // 7. Apply outlines on top (darken toward outline color)
                color = lerp(color, _StylOutlineColor.rgb, edge);

                // 8. Film grain (last before master blend)
                float grain = FilmGrain(uv, _StylNoiseScale, _StylNoiseSpeed);
                color += (grain - 0.5) * 2.0 * _StylNoiseStr;

                // 9. Master blend with original scene
                color = lerp(sceneColor.rgb, color, _StylIntensity);

                return half4(color, sceneColor.a);
            }

            ENDHLSL
        }
    }
    FallBack Off
}
