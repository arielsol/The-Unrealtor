#ifndef OUTLINE_BASE_PASS
#define OUTLINE_BASE_PASS

#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"

sampler2D _BaseMap;
float4 _BaseMap_ST;
float4 _OutlineColor;
float _OutlineWidth;

struct OutlineAppData
{
    float3 vertex : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};

struct OutlineV2F
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
};

// Vertex shader for outline extrusion
OutlineV2F OutlineBaseVert(OutlineAppData v)
{
    OutlineV2F o;
    float4 pos = UnityObjectToClipPos(v.vertex);

    float xySquared = dot(v.uv1.xy, v.uv1.xy);
    float smoothZ = sqrt(1 - saturate(xySquared));
    float3 smoothNormal = normalize(float3(v.uv1.x, v.uv1.y, smoothZ));

    float3 normalWS = normalize(UnityObjectToWorldNormal(v.normal));
    float3 tangentWS = normalize(UnityObjectToWorldDir(v.tangent.xyz));
    float3 bitangentWS = normalize(cross(normalWS, tangentWS));
    float3x3 tbn = transpose(float3x3(tangentWS, bitangentWS, normalWS));
    float3 normalWSTEX = normalize(mul(tbn, smoothNormal));
    float3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWSTEX));
    float3 ndcNormal = normalize(TransformViewToProjection(normalVS.xyz));

    pos.xy += clamp(0.01 * ndcNormal.xy * pos.w * _OutlineWidth, -0.01 / pos.w, 0.01 / pos.w);

    o.vertex = pos;
    o.uv = v.uv;
    o.uv1 = v.uv1;
    return o;
}

// Fragment shader for outline coloring
float4 OutlineBaseFrag(OutlineV2F i) : SV_Target
{
    return tex2D(_BaseMap, i.uv) * _OutlineColor;
}

#endif 
