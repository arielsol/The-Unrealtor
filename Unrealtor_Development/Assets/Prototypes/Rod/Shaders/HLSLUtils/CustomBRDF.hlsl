#ifndef CUSTOM_BRDF
#define CUSTOM_BRDF

#define RAMP_MAINLIGHT_DIFFUSE     0.125
#define RAMP_LIGHT_SPECULAR        0.375
#define RAMP_ENV_SPECULAR          0.625
#define RAMP_ADDLIGHT_DIFFUSE      0.875

#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04)


struct ToonBRDFData
{
    half3 albedo;
    half3 diffuse;
    half3 specular;
    half occulsion;
    half reflectivity;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;

    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne;    // roughness^2 - 1.0
};

struct ToonAnisoBRDFData
{
    half3 albedo;
    half3 diffuse;
    half3 specular;
    half occulsion;
    half reflectivity;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half roughnessT;
    half roughnessB;
    half grazingTerm;

    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne;    // roughness^2 - 1.0
};

inline void InitializeToonBRDFData(half3 albedo, half metallic, half smoothness, half ao, out ToonBRDFData outBRDFData)
{
    outBRDFData = (ToonBRDFData)0;
    outBRDFData.albedo = albedo;
    outBRDFData.reflectivity = half(1.0) - (half(1.0) - metallic) * kDielectricSpec.w;
    outBRDFData.diffuse = albedo * (half(1.0) - outBRDFData.reflectivity);
    outBRDFData.specular = lerp(kDieletricSpec.rgb, albedo, metallic);
    outBRDFData.occulsion = ao;

    outBRDFData.perceptualRoughness = 1.0 - smoothness;
    outBRDFData.roughness           = max(outBRDFData.perceptualRoughness * outBRDFData.perceptualRoughness, HALF_MIN_SQRT);
    outBRDFData.roughness2          = max(outBRDFData.roughness * outBRDFData.roughness, HALF_MIN);
    outBRDFData.grazingTerm         = saturate(smoothness + outBRDFData.reflectivity);
    outBRDFData.normalizationTerm   = outBRDFData.roughness * half(4.0) + half(2.0);
    outBRDFData.roughness2MinusOne  = outBRDFData.roughness2 - half(1.0);
}

void InitializeToonAnisoBRDFData(half3 albedo, half metallic, half smoothness, half ao, half AnisoDir, out ToonAnisoBRDFData outBRDFData)
{
    outBRDFData = (ToonAnisoBRDFData)0;
    outBRDFData.albedo = albedo;
    outBRDFData.reflectivity = half(1.0) - (half(1.0) - metallic) * kDielectricSpec.w;
    outBRDFData.diffuse = albedo * (half(1.0) - outBRDFData.reflectivity);
    outBRDFData.specular = lerp(kDieletricSpec.rgb, albedo, metallic);
    outBRDFData.occulsion = ao;

    outBRDFData.perceptualRoughness = 1.0 - smoothness;
    outBRDFData.roughness           = max(outBRDFData.perceptualRoughness * outBRDFData.perceptualRoughness, HALF_MIN_SQRT);
    outBRDFData.roughness2          = max(outBRDFData.roughness * outBRDFData.roughness, HALF_MIN);
    outBRDFData.roughnessT          = max(outBRDFData.roughness * (1.0 + AnisoDir), HALF_MIN);
    outBRDFData.roughnessB          = max(outBRDFData.roughness * (1.0 - AnisoDir), HALF_MIN);
    outBRDFData.grazingTerm         = saturate(smoothness + outBRDFData.reflectivity);
    outBRDFData.normalizationTerm   = outBRDFData.roughness * half(4.0) + half(2.0);
    outBRDFData.roughness2MinusOne  = outBRDFData.roughness2 - half(1.0);
}

half3 ToonBRDFSpecular(ToonBRDFData brdfData, float LoH, float NoH, half speOffset, half speSmooth, TEXTURE2D_PARAM(RampTex, RampSampler), half RampRow)
{
    //float3 lightDirectionWSFloat3 = float3(lightDirectionWS);
    //float3 halfDir = SafeNormalize(lightDirectionWSFloat3 + float3(viewDirectionWS));
    //float NoH = saturate(dot(float3(normalWS), halfDir));
    //half LoH = half(saturate(dot(lightDirectionWSFloat3, halfDir)));
    
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    LoH = max(saturate(LoH), 1e-5);
    NoH = max(saturate(NoH), 1e-5);

    float d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001f;
    float D = brdfData.roughness2 / (d * d);
    float remapD = D / PI;
    
    #ifdef _USERAMPTEX_ON
        half3 RemapD = SAMPLE_TEXTURE2D(RampTex, RampSampler, half2(remapD, RampRow)).rgb;
    #else
        remapD = smoothstep(speOffset, speOffset + speSmooth, remapD);
        half3 RemapD = half3(remapD, remapD, remapD);
    #endif

    RemapD *= PI;
    
    half LoH2 = LoH * LoH;
    half3 specularTerm = RemapD / (max(0.1h, LoH2) * brdfData.normalizationTerm);
    //half smoothSpecularTerm = smoothstep(speOffset, speOffset + speSmooth, specularTerm);
    //remapSpecularTerm *= max(1.0, specularTerm);
    
    return specularTerm;
}

half3 ToonBRDFSpecular(ToonBRDFData brdfData, float LoH, float NoH, half speOffset, half speSmooth, half RampRow)
{
    //float3 lightDirectionWSFloat3 = float3(lightDirectionWS);
    //float3 halfDir = SafeNormalize(lightDirectionWSFloat3 + float3(viewDirectionWS));
    //float NoH = saturate(dot(float3(normalWS), halfDir));
    //half LoH = half(saturate(dot(lightDirectionWSFloat3, halfDir)));
    
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    LoH = max(saturate(LoH), 1e-5);
    NoH = max(saturate(NoH), 1e-5);

    float d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001f;
    float D = brdfData.roughness2 / (d * d);
    float remapD = D / PI;
    
    remapD = smoothstep(speOffset, speOffset + speSmooth, remapD);
    half3 RemapD = half3(remapD, remapD, remapD);

    RemapD *= PI;
    
    half LoH2 = LoH * LoH;
    half3 specularTerm = RemapD / (max(0.1h, LoH2) * brdfData.normalizationTerm);
    //half smoothSpecularTerm = smoothstep(speOffset, speOffset + speSmooth, specularTerm);
    //remapSpecularTerm *= max(1.0, specularTerm);
    
    return specularTerm;
}


half3 ToonAnisoBRDFSpecular(
    ToonAnisoBRDFData brdfData,
    float LoH,
    float NoH,
    float NoV,
    float VoH,
    float NoL,
    float ToV,
    float ToL,
    float ToH,
    float BoV,
    float BoL,
    float BoH,
    half speOffset,
    half speSmooth,
    half RampRow)
{
    float D = D_GGXAniso(ToH, BoH, NoH, brdfData.roughnessT, brdfData.roughnessB);
    float Vis = V_SmithJointGGXAniso(ToV, BoV, NoV, ToL, BoL, NoL, brdfData.roughnessT, brdfData.roughnessB);
    float3 F =  F_Transm_Schlick(brdfData.specular, VoH);
    
    half remapD = smoothstep(speOffset, speOffset + speSmooth, D);
    half3 RemapD = half3(remapD, remapD, remapD);
    
    half3 specularTerm = RemapD * Vis * F * PI;
    
    return F;
    //return half3(1,1,1) * D * Vis * F;
}

half3 SH_IndirectionDiff(float3 normalWS)
{
    return SampleSH(normalWS);
}

half3 IndirSpeCube(float3 normalWS,float3 viewWS,float perceptualRoughness,float occlusion)
{
    float3 reflectDirWS = reflect(-viewWS, normalWS);
    return GlossyEnvironmentReflection(reflectDirWS, perceptualRoughness, occlusion);
}

#endif