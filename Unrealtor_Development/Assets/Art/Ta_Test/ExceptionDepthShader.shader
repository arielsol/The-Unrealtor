Shader "Custom/ExceptionDepthShader"
{
     Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _MinDist("Min Distance (full color)", Float) = 0.0
        _MaxDist("Max Distance (gray)", Float) = 10.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
            "RenderType"="Opaque"
        }

        ZWrite On
        ZTest LEqual
        Cull Back

        Pass
        {
            Name "Forward_DepthAware"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            sampler2D _BaseMap;
            float4    _BaseMap_ST;
            float4    _BaseColor;

            float4 _TargetPosWS;
            float  _MinDist;
            float  _MaxDist;

            
            sampler2D _CameraDepthTexture;
            float4 _ZBufferParams;

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct V2F
            {
                float4 posCS  : SV_POSITION; // ??????????UV&????
                float2 uv     : TEXCOORD0;
                float3 posWS  : TEXCOORD1;
            };

            V2F Vert (AppData v)
            {
                V2F o;
                float4 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1));
                o.posCS = mul(unity_MatrixVP, worldPos);
                o.uv    = TRANSFORM_TEX(v.uv, _BaseMap);
                o.posWS = worldPos.xyz;
                return o;
            }

            float3 ToGray(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114)).xxx;
            }

            fixed4 Frag (V2F i) : SV_Target
            {
                // ====== ???????? _CameraDepthTexture ?? ======
                float2 uv = i.posCS.xy / i.posCS.w;
                uv = uv * 0.5 + 0.5;

                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                // ???????0-1?
                float sceneDepth01 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                float sceneDepth   = LinearEyeDepth(sceneDepth01, _ZBufferParams);

                // ??????
                float fragDepth01 = i.posCS.z / i.posCS.w;
                float fragDepth   = LinearEyeDepth(fragDepth01, _ZBufferParams);

                // ??????????????????? ? ????
                if (fragDepth > sceneDepth + 1e-3)
                    discard;

                // ====== ??????? ======
                float3 albedo = tex2D(_BaseMap, i.uv).rgb * _BaseColor.rgb;

                float d  = distance(i.posWS, _TargetPosWS.xyz);
                float t  = saturate((d - _MinDist) / max(1e-4, (_MaxDist - _MinDist)));
                float sat = 1.0 - t;

                float3 gray  = ToGray(albedo);
                float3 final = lerp(gray, albedo, sat);

                return float4(final, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
