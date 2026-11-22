Shader "Hidden/GlassOrbLens"
{
	Properties
	{
		_SceneTex ("Scene", 2D) = "black" {}
		_Center ("Center (0-1)", Vector) = (0.5, 0.5, 0, 0)
		_Radius ("Radius", Float) = 0.33
		_Edge ("Edge Feather", Float) = 0.01
		_Distortion ("Distortion", Float) = 0.35
		_InvertInside ("Invert Inside", Float) = 1
		_OutlineColor ("Outline Color (rgb) a=intensity", Color) = (0.8,0.9,1,0.85)
		_OutlineThickness ("Outline Thickness", Float) = 0.01
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
		Cull Off Lighting Off ZWrite Off ZTest Always
		Blend SrcAlpha OneMinusSrcAlpha

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
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			sampler2D _SceneTex;
			float4 _SceneTex_ST;
			float2 _Center;
			float _Radius;
			float _Edge;
			float _Distortion;
			float _InvertInside;
			float4 _OutlineColor;
			float _OutlineThickness;

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _SceneTex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float2 uv = i.uv; // 0..1 across the screen
				float2 center = _Center;
				float2 v = uv - center;
				float d = length(v);
				float r = _Radius;

				// Circular mask: 1 inside, 0 outside with feather
				float mask = 1.0 - smoothstep(r, r + max(1e-5, _Edge), d);

				// Spherical lens distortion (simple radial model)
				float2 dir = (d > 1e-5) ? v / d : float2(0, 0);
				float strength = 1.0 + _Distortion * (1.0 - saturate(d / max(1e-5, r)));
				float2 refracted = center + dir * d * strength;

				// Optional inversion inside the orb
				float2 sampleUv = refracted;
				if (_InvertInside > 0.5)
				{
					sampleUv = center - (refracted - center);
				}
				sampleUv = clamp(sampleUv, 0.0, 1.0);

				fixed4 col = tex2D(_SceneTex, sampleUv);
				col.a *= mask;

				// Outline ring on the boundary
				float t = max(1e-4, _OutlineThickness);
				float ring = smoothstep(r - t, r, d) - smoothstep(r, r + t, d);
				fixed4 outCol = col;
				outCol.rgb = lerp(outCol.rgb, _OutlineColor.rgb, _OutlineColor.a * ring);
				outCol.a = max(outCol.a, _OutlineColor.a * ring);

				return outCol;
			}
			ENDCG
		}
	}

	Fallback Off
}


