#ifndef OUTLINE_MASKED_PASS
#define OUTLINE_MASKED_PASS

#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"

// Textures & uniforms
sampler2D _BaseMap;
float4 _BaseMap_ST;
sampler2D _OutlineMask;
float4 _OutlineMask_ST;

float4 _OutlineColor;
float _OutlineWidth;

// Input structs
struct OutlineAppData
{
    float3 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
};

struct OutlineV2F
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
};

// Vertex function with mask control
OutlineV2F OutlineMaskedVert(OutlineAppData v)
{
    OutlineV2F o;
    float4 pos = UnityObjectToClipPos(v.vertex);

    float4 worldPos = mul(unity_ObjectToWorld, float4(v.vertex, 1));
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);

    float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal.xyz);
    float3 ndcNormal = normalize(TransformViewToProjection(viewNormal.xyz));

    float4 mask = tex2Dlod(_OutlineMask, float4(v.uv, 0, 0));

    float widthScale = mask.r * (1 - mask.g);

    pos.xy += clamp(0.01 * ndcNormal.xy * pos.w * _OutlineWidth * widthScale, 0, 2);

    o.vertex = pos;
    o.uv = v.uv;
    return o;
}

// Fragment
float4 OutlineMaskedFrag(OutlineV2F i) : SV_Target
{
    return tex2D(_BaseMap, i.uv) * _OutlineColor;
}

#endif