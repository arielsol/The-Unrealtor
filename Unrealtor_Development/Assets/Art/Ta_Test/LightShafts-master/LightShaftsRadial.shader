Shader "Custom/URP/LightShaftsRadial"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 1.0
        _MaxRadius ("Max Radius", Float) = 0.75
        _Decay ("Decay", Float) = 0.95
        _SampleCount ("Sample Count", Int) = 64
        _ShaftColor ("Shaft Color", Color) = (1,1,1,1)
        _Threshold ("Threshold", Range(0,2)) = 0.75
           _Softness  ("Softness",  Range(0,2)) = 0.20
        _EdgeFade  ("Edge Fade", Range(0,1)) = 0.15
        _StrengthPow("Strength Power", Range(0.5, 6)) = 2.2
        
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "LightShafts"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One   // additive

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            // --- TEXTURES ---
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            // --- UNIFORMS ---
            float4 _LightScreenPos;   // x,y = screen pos; z = depth
            float _Intensity;
            float _MaxRadius;
            float _Decay;
            int _SampleCount;
            float4 _ShaftColor;
            float _Threshold;
float _Softness;
float _EdgeFade;
            float _StrengthPow;
            // 我们自己的 0~1 深度函数（避免调用不存在的 Linear01Depth）
            float Depth01(float rawDepth)
            {
                // 对大多数 URP 情况，这样已经足够用来分出“近处几何体 vs 远处天空”
                #if UNITY_REVERSED_Z
                    return 1.0 - rawDepth;
                #else
                    return rawDepth;
                #endif
            }

            float4 Frag(Varyings i) : SV_Target
            {
               float2 uv = i.uv;

                    // 原画
                    float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                    // 光在相机后方，直接不做
                    if (_LightScreenPos.z <= 0.0)
                        return col;

                    float2 lightPos = _LightScreenPos.xy;
                    float2 delta = lightPos - uv;
                    float dist = length(delta);

                    // ===== 1) 半径外硬截断：彻底避免“范围外仍泛白” =====
                    if (dist >= _MaxRadius)
                        return col;

                    // 平滑边缘（避免硬边）：edgeFade 是 [0,1]，表示半径的百分比
                    float edge = max(_MaxRadius , 1e-5);
                    float mask = smoothstep(_MaxRadius, _MaxRadius - edge, dist); // dist 越小 mask 越大

                    // 当前像素深度（你原来的 viewFade 保留）
                    float rawDepthCur = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                    float depthCur01  = Depth01(rawDepthCur);
                    float viewFade    = smoothstep(0.7, 1.0, depthCur01);

                    if (dist < 1e-6)
                        return col;

                    int nSamples = clamp(_SampleCount, 1, 128);

                    float2 dir = delta / dist;
                    float2 stepUV = dir * (_MaxRadius / nSamples);

                    float2 cur = uv;
                    float illuminationDecay = 1.0;

                    float accum = 0.0;
                    float weightSum = 0.0;

                    [loop]
                    for (int s = 0; s < nSamples; ++s)
                    {
                        cur += stepUV;

                        if (cur.x < 0 || cur.x > 1 || cur.y < 0 || cur.y > 1)
                            break;

                        // 采样颜色并提取“高光能量”（重要：暗部不再贡献 shafts）
                        float3 srcRGB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, cur).rgb;
                        float  luma   = dot(srcRGB, float3(0.2126, 0.7152, 0.0722));

                        // soft threshold：只有足够亮的地方才会产生 shafts
                        float bright = smoothstep(_Threshold, _Threshold + max(_Softness, 1e-4), luma);

                        // 深度：只允许非常远（天空/远平面）贡献（你原逻辑保留）
                        float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, cur).r;
                        float linear01 = Depth01(rawDepth);
                        float occlusion = step(0.98, linear01);

                        // ===== 2) 关键：bright 必须乘进去，否则远平面会造成全屏淡雾 =====
                        float w = illuminationDecay;
                        accum += occlusion *0.28 * w;
                        weightSum += w;

                        illuminationDecay *= _Decay;

                        // 可选：衰减太小就提前停止（省一点性能，也更干净）
                        
                    }

                    // ===== 3) 归一化：避免 sampleCount 变化导致底雾抬升 =====
                    float shaftStrength = (weightSum > 1e-5) ? (accum / weightSum) : 0.0;

                    // 你原来的 bias 保留
                    shaftStrength = max(shaftStrength - 0.03, 0.0);
                    const float SHAFT_THRESHOLD = 0.03;   // 0.02~0.08 之间调

                    shaftStrength = max(shaftStrength - SHAFT_THRESHOLD, 0.0);
                    shaftStrength = shaftStrength * shaftStrength;
                    // 半径内 radial（你原来用的也可以保留），再乘 mask（平滑边缘）
                    float radial = saturate((_MaxRadius - dist) / max(_MaxRadius, 1e-4));
                    radial = radial * radial * radial;

                    float3 shafts = _ShaftColor.rgb * (shaftStrength * _Intensity) * radial * viewFade ;

                    col.rgb += shafts;
                    return float4(shafts,1);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
