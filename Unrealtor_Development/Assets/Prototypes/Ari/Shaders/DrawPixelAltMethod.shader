Shader "Custom/DrawPixelAltMethod"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "black" {}
        _Color ("Draw Color", Color) = (0,0,0,1)
        _TargetUV ("Target UV", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0.01
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float2 _TargetUV;
            float _Radius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 existing = tex2D(_MainTex, i.uv);
                float dist = distance(i.uv, _TargetUV);

                if (dist < _Radius)
                {
                    return lerp(existing, _Color, _Color.a); // blend new color with old
                }
                else
                {
                    return existing; // keep what's already drawn
                }
            }
            ENDCG
        }
    }
    FallBack Off
}
