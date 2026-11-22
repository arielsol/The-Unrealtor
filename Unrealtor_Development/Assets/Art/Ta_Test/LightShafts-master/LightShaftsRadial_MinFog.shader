Shader "Custom/URP/LightShaftsRadial_MinFog"
{
     Properties
    {
        _Intensity      ("Intensity", Float)             = 1.0
        _Radius         ("Screen Radius", Float)         = 0.4
        _Softness       ("Radial Softness", Range(1,8))  = 3
        _SampleCount    ("Sample Count", Int)            = 32
        _Threshold      ("Brightness Threshold", Range(0,1)) = 0.8
        _DepthStart     ("Depth Start", Range(0,1))      = 0.7
        _DepthEnd       ("Depth End", Range(0,1))        = 1.0
        _MinShaft       ("Min Shaft Cutoff", Range(0,0.1)) = 0.02
        _ShaftColor     ("Shaft Color", Color)           = (1,1,1,1)
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
            Name "LightShaftsThresholdRadial"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One One        // 只加色，不改原图

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D_X(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float4 _LightScreenPos; // x,y: [0,1] 屏幕位置, z: depth
            float  _Intensity;
            float  _Radius;
            float  _Softness;
            int    _SampleCount;
            float  _Threshold;
            float  _DepthStart;
            float  _DepthEnd;
            float  _MinShaft;
            float4 _ShaftColor;

            float Depth01(float d)
            {
            #if UNITY_REVERSED_Z
                return 1.0 - d;
            #else
                return d;
            #endif
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv  = i.uv;
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // 光在相机后面：直接不做
                if (_LightScreenPos.z <= 0.0)
                    return col;

                float2 lightPos = _LightScreenPos.xy;
                float2 delta    = uv - lightPos;
                float  dist     = length(delta);

                // 半径之外完全不做，避免全屏雾
                if (dist > _Radius)
                    return col;

                // 当前像素自身深度：近处不加体积光
                float rawDepthCur = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, uv).r;
                float depthCur01  = Depth01(rawDepthCur);
                float viewFade    = smoothstep(_DepthStart, _DepthEnd, depthCur01);
                if (viewFade <= 0.0)
                    return col;

                int   nSamples = max(_SampleCount, 1);
                float2 dir     = delta / max(dist, 1e-4);   // 从光向当前像素的方向
                float2 stepUV  = dir * (_Radius / nSamples);

                float2 cur = lightPos;
                float accum = 0.0;

                [loop]
                for (int s = 0; s < nSamples; ++s)
                {
                    cur += stepUV;

                    if (cur.x < 0 || cur.x > 1 || cur.y < 0 || cur.y > 1)
                        break;

                    float4 src  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, cur);
                    float  luma = dot(src.rgb, float3(0.299, 0.587, 0.114));

                    // 只有亮度高于阈值的点才贡献体积光
                    float bright = smoothstep(_Threshold, _Threshold + 0.1, luma);
                    accum += bright;
                }

                float shaft = accum / nSamples;

                // 删掉非常弱的小值，避免背景统一染一点点颜色
                shaft = max(shaft - _MinShaft, 0.0);

                // 屏幕空间 radial 衰减：越靠近光源越强
                float t = saturate(1.0 - dist / max(_Radius, 1e-4));
                float radial = pow(t, _Softness);

                shaft *= radial * viewFade;

                col.rgb += _ShaftColor.rgb * shaft * _Intensity;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
