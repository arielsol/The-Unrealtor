// Stained Glass Crack Overlay Shader for URP
// Renders a Voronoi-based fractured glass pattern.
// Cracks/fissures are fully transparent — base material shows through.
// Intact glass pieces between cracks have stained glass coloring + emission.
// Each cell gets a random color from a 4-color palette.
// Compatible with Unity 6+ URP 17.x
Shader "StainedGlass/CrackOverlay"
{
    Properties
    {
        [Header(Crack Pattern)]
        _CrackScale ("Crack Density", Range(1, 30)) = 8
        _CrackWidth ("Crack Width", Range(0.01, 0.25)) = 0.06
        _CrackSoftness ("Crack Edge Softness", Range(0.001, 0.15)) = 0.03

        [Header(Glass Colors)]
        [HDR] _ColorA ("Color A", Color) = (0.85, 0.15, 0.15, 1)
        [HDR] _ColorB ("Color B", Color) = (0.15, 0.45, 0.85, 1)
        [HDR] _ColorC ("Color C", Color) = (0.15, 0.75, 0.35, 1)
        [HDR] _ColorD ("Color D", Color) = (0.90, 0.65, 0.10, 1)
        _ColorBoost ("Color Intensity", Range(0.5, 4)) = 1.5
        _Saturation ("Saturation", Range(0, 2)) = 1.3

        [Header(Glass Cells)]
        _GlassOpacity ("Glass Opacity", Range(0, 1)) = 0.9
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 1.0

        [Header(Overall)]
        _GlobalOpacity ("Global Opacity", Range(0, 1)) = 1.0
        [HDR] _TintColor ("Global Tint", Color) = (1, 1, 1, 1)

        [Header(Surface)]
        _DepthOffset ("Depth Offset", Range(-2, 0)) = -0.5
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
            Name "StainedGlassCrackOverlay"
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
            #include "StainedGlass_Helpers.hlsl"

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

            CBUFFER_START(UnityPerMaterial)
                float  _CrackScale;
                float  _CrackWidth;
                float  _CrackSoftness;
                float4 _ColorA;
                float4 _ColorB;
                float4 _ColorC;
                float4 _ColorD;
                float  _ColorBoost;
                float  _Saturation;
                float  _GlassOpacity;
                float  _EmissionStrength;
                float  _GlobalOpacity;
                float4 _TintColor;
                float  _DepthOffset;
            CBUFFER_END

            // =================================================================
            // Voronoi with F1 + F2 for crack edge detection
            // F2 - F1 is small at cell boundaries (= cracks)
            // =================================================================
            void VoronoiCrack(float2 uv, float scale,
                              out float f1, out float f2, out float2 cellID)
            {
                float2 g = floor(uv * scale);
                float2 f = frac(uv * scale);

                float d1 = 10.0;
                float d2 = 10.0;
                cellID = float2(0, 0);

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 n = VoronoiHash(g + offset);
                        float2 diff = offset + n - f;
                        float dist = dot(diff, diff);

                        if (dist < d1)
                        {
                            d2 = d1;
                            d1 = dist;
                            cellID = g + offset;
                        }
                        else if (dist < d2)
                        {
                            d2 = dist;
                        }
                    }
                }

                f1 = sqrt(d1);
                f2 = sqrt(d2);
            }

            // =================================================================
            // Saturation helper (BT.709)
            // =================================================================
            half3 AdjustSat(half3 color, half sat)
            {
                half lum = dot(color, half3(0.2126, 0.7152, 0.0722));
                return lerp(half3(lum, lum, lum), color, sat);
            }

            // =================================================================
            // Vertex
            // =================================================================
            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = IN.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // =================================================================
            // Fragment
            // =================================================================
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // --- Voronoi crack pattern ---
                float f1, f2;
                float2 cellID;
                VoronoiCrack(uv, _CrackScale, f1, f2, cellID);

                // glassMask: 1 = intact glass piece, 0 = crack/fissure
                float edgeDist = f2 - f1;
                float glassMask = smoothstep(_CrackWidth, _CrackWidth + _CrackSoftness, edgeDist);

                // --- Per-cell stained glass color ---
                half3 cellCol = CellColor(cellID, _ColorA.rgb, _ColorB.rgb, _ColorC.rgb, _ColorD.rgb);
                cellCol = AdjustSat(cellCol, _Saturation) * _ColorBoost;
                cellCol *= _TintColor.rgb;

                // --- Compose ---
                // Glass pieces: colored stained glass with emission
                // Cracks: fully transparent (alpha = 0), base material shows through
                half3 finalColor = cellCol + cellCol * _EmissionStrength;

                // Alpha: glass pieces are opaque, cracks are transparent
                float finalAlpha = glassMask * _GlassOpacity * _GlobalOpacity;

                // Fog
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }

        // Transparent overlay: no shadow caster or depth passes needed
    }

    FallBack Off
}
