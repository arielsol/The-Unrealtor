Shader "Custom/Test"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color (Hint)", Color) = (1,1,1,1)
        _MinDist("Min Distance (full color)", Float) = 0.0
        _MaxDist("Max Distance (gray)", Float) = 10.0
        _ForceColor("Force Full Color", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "Forward"
            ZWrite On        
            ZTest LEqual     
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _TargetPosWS;
            float _MinDist, _MaxDist;
            float _ForceColor;

            struct Attributes {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 pws : TEXCOORD1;
            };

           Varyings Vert (Attributes IN)
        {
            Varyings OUT;

            float3 posWS = TransformObjectToWorld(IN.positionOS);

            OUT.positionCS = TransformWorldToHClip(posWS);
            OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
            OUT.pws = posWS;
            return OUT;
        }

            float3 ToGray(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114)).xxx;
            }

            float4 Frag (Varyings IN) : SV_Target
            {
                float3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                float3 albedo = tex * _BaseColor.rgb;

                float d = distance(IN.pws, _TargetPosWS.xyz);
                float t = saturate((d - _MinDist) / max(1e-5, (_MaxDist - _MinDist)));
                float sat = 1 - t;
                if (_ForceColor > 0.5) sat = 1.0;

                float3 gray = ToGray(albedo);
                float3 finalColor = lerp(gray, albedo, sat);

                return float4(finalColor, 1.0f);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
