#ifndef BRDF_UTIL
#define BRDF_UTIL

#define PI 3.14159265359
#define EPS 0.000001

half3 Pow5(half3 x)
{
    return x * x * x * x * x;
}

inline half PerceptualRoughnessToSpecPower(half perceptualRoughness)
{
    half m = perceptualRoughness * perceptualRoughness;   // m is the true academic roughness.
    half sq = max(1e-4f, m * m);
    half n = (2.0 / sq) - 2.0;                          // https://dl.dropboxusercontent.com/u/55891920/papers/mm_brdf.pdf
    n = max(n, 1e-4f);                                  // prevent possible cases of pow(0,0), which could happen when roughness is 1.0 and NdotH is zero
    return n;
}

float trowbridgeReitzNDF(float NdotH, float roughness)
{
    float alpha = PerceptualRoughnessToSpecPower(roughness);
    float alpha2 = alpha * alpha;

    float NdotH2 = NdotH * NdotH;
    float d = NdotH * NdotH * (alpha2 - 1.0) + 1;
    float d2 = max(d * d, EPS * 100);
    return alpha2 / (PI * d2);
}

half CustomDisneyDiffuse(half NdotV, half NdotL, half LdotH, half perceptualRoughness)
{
    half fd90 = 0.5 + 2 * LdotH * LdotH * perceptualRoughness;
    half lightScatter = 1 + (fd90 - 1) * Pow5(1 - NdotL);
    half viewScatter = 1 + (fd90 - 1) * Pow5(1 - NdotV);
    return lightScatter * viewScatter;
}

half3 FresnelTermColor(half3 F0, half cosA)
{
    half t = Pow5(1 - cosA);   // ala Schlick interpoliation
    return F0 + (1 - F0) * t;
}

half FresnelTerm(half F0, half cosA)
{
    half t = Pow5(1 - cosA);   // ala Schlick interpoliation
    return F0 + (1 - F0) * t;
}

float FakeFresnel(float NdotV, float metalic, float range, float fade, float intensity) {
    return smoothstep(range, range + fade * metalic, 1 - NdotV) * intensity;
}

float schlick(float NdotV, float f0)
{
    if (f0 > 1.0)
    {
        float cos2_t = 1.0 - f0 * (1.0 - NdotV * NdotV);
        if (cos2_t < 0.0)
            return 1.0;
        NdotV = sqrt(cos2_t);
    }

    float x = 1.0 - NdotV;
    float x2 = x * x;
    float x5 = x2 * x2 * x;

    return f0 + (1.0 - f0) * x5;
}

float SmithJointGGXVisibilityTerm(float NdotV, float NdotL, float roughness) {
    float a = roughness;
    float GGXV = NdotL * (NdotV * (1.0 - a) + a);
    float GGXL = NdotV * (NdotL * (1.0 - a) + a);
    return 0.5 / (GGXV + GGXL);
}

float GGX(float NdotH, float roughness) {
    float a2 = roughness * roughness;
    float d = (NdotH * NdotH * (a2 - 1.0) + 1.0);
    float denom = PI * d * d;
    float num = a2;
    return num / denom;
    return min(1 / PI * a2 / (d * d + 1e-7), 1.0);
}

float schlickBeckmannGAF(float dotProduct, float roughness)
{
    float alpha = (roughness + 1) * (roughness + 1);
    float k = alpha / 8;
    return dotProduct / (dotProduct * (1 - k) + k);
}

float cookTorranceGAF(float NdotH, float NdotV, float HdotV, float NdotL)
{
    float firstTerm = 2 * NdotH * NdotV / HdotV;
    float secondTerm = 2 * NdotH * NdotL / HdotV;
    return min(1, min(firstTerm, secondTerm));
}

float3 shiftTangent(float3 T, float3 N, float shift)
{
    return normalize(T + shift * N);
}

float StrandSpecular(float3 T, float3 V, float L, float exponent, float specWidth)
{
    float3 H = normalize(L + V);
    float dotTH = dot(T, H);
    float sinTH = sqrt(1.0 - dotTH * dotTH);
    float dirAtten = smoothstep(-1.0, 0.0, dot(T, H));

    return dirAtten * saturate(pow(sinTH, exponent * 100 * specWidth));
}

float StrandSpecular(float3 T, float3 V, float L, float exponent)
{
    float3 H = normalize(L + V);
    float dotTH = dot(T, H);
    float sinTH = sqrt(1.0 - dotTH * dotTH);
    float dirAtten = smoothstep(-1.0, 0.0, dot(T, H));

    return dirAtten * saturate(pow(sinTH, exponent * 100));
}
#endif