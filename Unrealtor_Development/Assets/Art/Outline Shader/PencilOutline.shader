// ============================================================================
// PENCIL EDGE OUTLINE SHADER
// Per-object edge detection outline that renders clean lines at edges only.
// Uses depth and normal discontinuity detection - no "panel" artifacts.
//
// SETUP:
// 1. Create material with this shader
// 2. Add as SECOND material slot on MeshRenderer
// 3. Adjust edge detection sensitivity and line appearance
//
// Compatible with Unity 2021+ URP
// ============================================================================

Shader "Custom/PencilOutline"
{
    Properties
    {
        [Header(LINE APPEARANCE)]
        [Space(5)]
        _OutlineColor ("Line Color", Color) = (0.1, 0.08, 0.06, 1.0)
        _OutlineWidth ("Line Width", Range(0.5, 5)) = 1.5
        _Softness ("Line Softness", Range(0, 1)) = 0.2
        [Space(10)]

        [Header(EDGE DETECTION)]
        [Space(5)]
        _DepthSensitivity ("Depth Sensitivity", Range(0, 50)) = 15
        _NormalSensitivity ("Normal Sensitivity", Range(0, 10)) = 3
        _DepthThreshold ("Depth Threshold", Range(0, 1)) = 0.1
        _NormalThreshold ("Normal Threshold", Range(0, 1)) = 0.3
        [Space(10)]

        [Header(SKETCH EFFECT)]
        [Space(5)]
        [Toggle] _EnableSketch ("Enable Sketch Wobble", Float) = 1
        _SketchStrength ("Wobble Strength", Range(0, 1)) = 0.3
        _SketchScale ("Wobble Scale", Range(1, 30)) = 10
        _SketchSpeed ("Wobble Speed", Range(0, 2)) = 0.4
        [Space(10)]

        [Header(WAVE EFFECT)]
        [Space(5)]
        [Toggle] _EnableWave ("Enable Wave", Float) = 1
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.3
        _WaveFrequency ("Wave Frequency", Range(0.5, 8)) = 2
        _WaveSpeed ("Wave Speed", Range(0, 2)) = 0.6
        [Space(10)]

        [Header(LINE VARIATION)]
        [Space(5)]
        _ThicknessVariation ("Thickness Variation", Range(0, 1)) = 0.3
        _OpacityVariation ("Opacity Variation", Range(0, 1)) = 0.2
        [Space(10)]

        [Header(DISTANCE SCALING)]
        [Space(5)]
        [Toggle] _EnableDistanceScale ("Scale Width With Distance", Float) = 1
        _ReferenceDistance ("Reference Distance", Float) = 5
        _MinWidthScale ("Minimum Width Scale", Range(0.1, 1)) = 0.3
        _MaxWidthScale ("Maximum Width Scale", Range(1, 3)) = 1.5
        [Space(10)]

        [Header(DISTANCE FADE)]
        [Space(5)]
        [Toggle] _EnableDistanceFade ("Fade Out At Distance", Float) = 1
        _FadeStart ("Fade Start", Float) = 30
        _FadeEnd ("Fade End", Float) = 100
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PencilEdgeOutline"

            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _ENABLESKETCH_ON
            #pragma multi_compile _ _ENABLEWAVE_ON
            #pragma multi_compile _ _ENABLEDISTANCESCALE_ON
            #pragma multi_compile _ _ENABLEDISTANCEFADE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _Softness;

                float _DepthSensitivity;
                float _NormalSensitivity;
                float _DepthThreshold;
                float _NormalThreshold;

                float _EnableSketch;
                float _SketchStrength;
                float _SketchScale;
                float _SketchSpeed;

                float _EnableWave;
                float _WaveStrength;
                float _WaveFrequency;
                float _WaveSpeed;

                float _ThicknessVariation;
                float _OpacityVariation;

                float _EnableDistanceScale;
                float _ReferenceDistance;
                float _MinWidthScale;
                float _MaxWidthScale;

                float _EnableDistanceFade;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            // ================================================================
            // NOISE FUNCTIONS
            // ================================================================
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * noise2D(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }

                return value;
            }

            // Calm wave function
            float calmWave(float2 uv, float time)
            {
                float wave = 0.0;
                wave += sin(uv.x * _WaveFrequency + time * _WaveSpeed) * 0.5;
                wave += sin(uv.y * _WaveFrequency * 0.7 - time * _WaveSpeed * 0.8) * 0.3;
                wave += sin((uv.x + uv.y) * _WaveFrequency * 0.5 + time * _WaveSpeed * 0.6) * 0.2;
                return wave;
            }

            // ================================================================
            // VERTEX SHADER
            // ================================================================
            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            // ================================================================
            // EDGE DETECTION (Roberts Cross)
            // ================================================================
            float ComputeEdge(float2 screenUV, float2 texelSize, float widthMult)
            {
                float2 offset = texelSize * _OutlineWidth * widthMult;

                // Clamp to prevent edge artifacts
                float2 uvMin = texelSize * 2;
                float2 uvMax = 1.0 - texelSize * 2;

                float2 uv00 = clamp(screenUV + float2(-1, -1) * offset, uvMin, uvMax);
                float2 uv11 = clamp(screenUV + float2( 1,  1) * offset, uvMin, uvMax);
                float2 uv01 = clamp(screenUV + float2(-1,  1) * offset, uvMin, uvMax);
                float2 uv10 = clamp(screenUV + float2( 1, -1) * offset, uvMin, uvMax);

                // Depth edge detection
                float d00 = Linear01Depth(SampleSceneDepth(uv00), _ZBufferParams);
                float d11 = Linear01Depth(SampleSceneDepth(uv11), _ZBufferParams);
                float d01 = Linear01Depth(SampleSceneDepth(uv01), _ZBufferParams);
                float d10 = Linear01Depth(SampleSceneDepth(uv10), _ZBufferParams);

                float depthEdge = abs(d00 - d11) + abs(d01 - d10);
                depthEdge = saturate((depthEdge * _DepthSensitivity - _DepthThreshold)
                            / max(1.0 - _DepthThreshold, 0.001));

                // Normal edge detection
                float3 n00 = SampleSceneNormals(uv00);
                float3 n11 = SampleSceneNormals(uv11);
                float3 n01 = SampleSceneNormals(uv01);
                float3 n10 = SampleSceneNormals(uv10);

                float3 nDiff1 = n00 - n11;
                float3 nDiff2 = n01 - n10;
                float normalEdge = sqrt(dot(nDiff1, nDiff1) + dot(nDiff2, nDiff2));
                normalEdge = saturate((normalEdge * _NormalSensitivity - _NormalThreshold)
                             / max(1.0 - _NormalThreshold, 0.001));

                return saturate(depthEdge + normalEdge);
            }

            // ================================================================
            // FRAGMENT SHADER
            // ================================================================
            half4 frag(Varyings input) : SV_Target
            {
                // Screen UV
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 texelSize = _CameraDepthTexture_TexelSize.xy;

                // Distance from camera
                float dist = length(_WorldSpaceCameraPos.xyz - input.positionWS);

                // Calculate line width multiplier with effects
                float widthMult = 1.0;

                // Distance-based width scaling (keeps outline proportional to object size)
                #if defined(_ENABLEDISTANCESCALE_ON)
                    // Scale width inversely with distance
                    // At reference distance, width = 1.0
                    // Closer = thicker (up to max), further = thinner (down to min)
                    float distanceScale = _ReferenceDistance / max(dist, 0.1);
                    distanceScale = clamp(distanceScale, _MinWidthScale, _MaxWidthScale);
                    widthMult *= distanceScale;
                #endif

                #if defined(_ENABLESKETCH_ON)
                    // Sketchy wobble - affects sampling offset
                    float2 sketchCoord = input.uv * _SketchScale + _Time.y * _SketchSpeed;
                    float sketchNoise = fbm(sketchCoord, 3);
                    widthMult += (sketchNoise - 0.5) * _SketchStrength;
                #endif

                #if defined(_ENABLEWAVE_ON)
                    // Calm wave effect
                    float wave = calmWave(input.uv, _Time.y);
                    widthMult += wave * _WaveStrength * 0.5;
                #endif

                // Thickness variation
                float thicknessNoise = fbm(input.uv * 8.0 + _Time.y * 0.1, 2);
                widthMult *= lerp(1.0 - _ThicknessVariation, 1.0 + _ThicknessVariation, thicknessNoise);

                widthMult = max(widthMult, 0.3); // Minimum width

                // Edge detection
                float edge = ComputeEdge(screenUV, texelSize, widthMult);

                // Apply softness
                edge = smoothstep(0.0, _Softness + 0.01, edge);

                // Opacity variation (pencil texture feel)
                float opacityNoise = fbm(screenUV * 100.0 + _Time.y * 0.05, 2);
                float opacity = lerp(1.0 - _OpacityVariation, 1.0, opacityNoise);

                // Distance fade (uses dist calculated earlier)
                #if defined(_ENABLEDISTANCEFADE_ON)
                    float fade = 1.0 - saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                    opacity *= fade;
                #endif

                // Final color
                float4 color = _OutlineColor;
                color.a *= edge * opacity;

                // Discard if nearly invisible
                clip(color.a - 0.01);

                return color;
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "PencilOutlineShaderGUI"
}
