Shader "PostProcessing/RadialBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        
        ZWrite Off
        ZTest Always
        Cull Off
        
        Pass
        {
            Name "RadialBlur"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
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
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            float _BlurStrength;
            float2 _Center;
            int _Samples;
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float2 direction = input.uv - _Center;
                half4 color = half4(0, 0, 0, 0);
                float totalWeight = 0.0;
                
                // Sigma controls the spread of the Gaussian
                float sigma = float(_Samples) / 3.0;
                
                for(int i = 0; i < _Samples; i++)
                {
                    // Calculate sample position along the radial direction
                    float t = float(i) / float(_Samples - 1);
                    float2 offset = direction * _BlurStrength * (t - 0.5);
                    float2 sampleUV = input.uv + offset;
    
                    // Calculate Gaussian weight
                    float x = float(i) - float(_Samples) / 2.0;
                    float weight = exp(-(x * x) / (2.0 * sigma * sigma));
    
                    // Sample texture with weight
                    color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV) * weight;
                    totalWeight += weight;
                }
                
                // Normalize by total weight
                return totalWeight > 0.0 ? color / totalWeight : half4(0, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}