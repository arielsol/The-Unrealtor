Shader "Unlit/ReflectionTest"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Index("Index", Int) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _PlanarReflectionTexture;
            UNITY_DECLARE_TEX2DARRAY(_ReflectionTextureArray);

            float _Index;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex) / o.vertex.w;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = float2(i.screenPos.x, i.screenPos.y);
                fixed4 col = tex2D(_PlanarReflectionTexture, screenUV);
                return UNITY_SAMPLE_TEX2DARRAY(_ReflectionTextureArray, float3(screenUV, _Index));
                return col;
            }
            ENDCG
        }
    }
}
