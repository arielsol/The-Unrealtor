Shader "Hidden/URP/BlackWhite_Compat"
{Properties
    {
        _Strength("Strength (0-1)", Range(0,1)) = 1
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Overlay+100"
        }
        
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Stencil
            {
            Ref 1
            Comp NotEqual   
            Pass Keep
            ReadMask 255
            WriteMask 255
            }
            Name "BW_Fullscreen_Fixed"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float _Strength;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                
                
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                
                
                #if UNITY_UV_STARTS_AT_TOP
                //uv.y = 1.0 - uv.y;
                #endif
                
                output.texcoord = uv;
                return output;
            }

            float3 ToGray(float3 c)
            {
                return dot(c, float3(0.299, 0.587, 0.114)).xxx;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.texcoord);
                float3 gray = ToGray(col.rgb);
                col.rgb = lerp(col.rgb, gray, saturate(_Strength));
                return col;
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}
