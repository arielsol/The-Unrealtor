#ifndef GLASS_V1_HLSL
#define GLASS_V1_HLSL

// ...

//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Refraction.hlsl"

// ...

void GetRefractionModelSphereRay_float(float3 viewDir, float3 position, float3 normal, float ior, float thickness, out float3 output)
{
    RefractionModelResult refractionModelResult;
    refractionModelResult = RefractionModelSphere(viewDir, position, normal, ior, thickness);

    output = refractionModelResult.rayWS * refractionModelResult.dist;
}

#endif