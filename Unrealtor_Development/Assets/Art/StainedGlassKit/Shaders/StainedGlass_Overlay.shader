// Stained Glass Overlay Shader for URP
// Renders procedural stained glass pattern textures as transparent overlays.
// Can be placed on any surface (wall, floor, mesh) without affecting the base material.
// Features: emissive glass cells, opaque lead lines, depth bias for z-fighting prevention.
// Compatible with Unity 6+ URP 17.x
Shader "StainedGlass/Overlay"
{
    Properties
    {
        [Header(Pattern)]
        _MainTex ("Pattern Texture", 2D) = "white" {}
        [Toggle] _FlipU ("Flip Horizontal", Float) = 0
        [Toggle] _FlipV ("Flip Vertical", Float) = 0

        [Header(Glass Appearance)]
        _ColorBoost ("Color Intensity", Range(0.5, 4)) = 1.8
        _GlassOpacity ("Glass Opacity", Range(0, 1)) = 0.85
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 2.0
        _Saturation ("Saturation", Range(0, 2)) = 1.3

        [Header(Lead Lines)]
        _LeadColor ("Lead Color", Color) = (0.03, 0.03, 0.03, 1)
        _LeadOpacity ("Lead Opacity", Range(0, 1)) = 1.0

        [Header(Overall)]
        _GlobalOpacity ("Global Opacity", Range(0, 1)) = 1.0
        [HDR] _TintColor ("Global Tint", Color) = (1, 1, 1, 1)

        [Header(Surface)]
        _DepthOffset ("Depth Offset", Range(-2, 0)) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "StainedGlassOverlay"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset [_DepthOffset], [_DepthOffset]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _FlipU;
                float  _FlipV;
                float  _ColorBoost;
                float  _GlassOpacity;
                float  _EmissionStrength;
                float  _Saturation;
                float4 _LeadColor;
                float  _LeadOpacity;
                float  _GlobalOpacity;
                float4 _TintColor;
                float  _DepthOffset;
            CBUFFER_END

            half3 AdjustSaturation(half3 color, half sat)
            {
                half lum = dot(color, half3(0.2126, 0.7152, 0.0722));
                return lerp(half3(lum, lum, lum), color, sat);
            }

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                float2 uv = TRANSFORM_TEX(IN.uv, _MainTex);
                uv.x = lerp(uv.x, 1.0 - uv.x, _FlipU);
                uv.y = lerp(uv.y, 1.0 - uv.y, _FlipV);
                o.uv = uv;

                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Alpha channel encodes: 1 = glass cell, 0 = lead line
                half isGlass = step(0.5, tex.a);

                // Glass color processing
                half3 glassColor = AdjustSaturation(tex.rgb, _Saturation) * _ColorBoost;
                glassColor *= _TintColor.rgb;

                // Emission adds brightness to glass cells
                half3 emission = glassColor * _EmissionStrength * isGlass;

                // Lead line color
                half3 leadColor = _LeadColor.rgb;

                // Compose: lead lines are dark + opaque, glass is colored + semi-transparent + emissive
                half3 finalColor = lerp(leadColor, glassColor, isGlass) + emission;
                half  finalAlpha = lerp(_LeadOpacity, _GlassOpacity, isGlass) * _GlobalOpacity;

                // Apply fog
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }

        // Shadow caster pass omitted: overlays should not cast shadows
        // Depth pass omitted: overlays are transparent
    }

    FallBack Off
}
