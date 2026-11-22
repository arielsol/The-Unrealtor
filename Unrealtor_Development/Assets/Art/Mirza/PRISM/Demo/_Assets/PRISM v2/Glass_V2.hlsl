#ifndef GLASS_HLSL
#define GLASS_HLSL

// ...

//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
//#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Refraction.hlsl"

// CCW rotation matrix.

float2x2 GetRotationMatrix2D(float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    
    return float2x2(c, -s, s, c);
}

// ...

float3 RotateAroundAxis(float3 center, float3 original, float3 u, float angle)
{
    original -= center;

    float c = cos(angle);
    float s = sin(angle);

    float t = 1.0 - c;

    float m00 = t * u.x * u.x + c;

    float m01 = t * u.x * u.y - s * u.z;
    float m02 = t * u.x * u.z + s * u.y;
    float m10 = t * u.x * u.y + s * u.z;

    float m11 = t * u.y * u.y + c;

    float m12 = t * u.y * u.z - s * u.x;
    float m20 = t * u.x * u.z - s * u.y;
    float m21 = t * u.y * u.z + s * u.x;

    float m22 = t * u.z * u.z + c;

    float3x3 finalMatrix = float3x3(m00, m01, m02, m10, m11, m12, m20, m21, m22);

    return mul(finalMatrix, original) + center;
}

// Smooth RGB split/shift along the refracted ray direction.
// Direction should be the 2D view space normal in case of refraction.

void RGBSplit_float(Texture2D tex, float2 uv, float rgbSplit, int rgbSplitSteps, float2 direction, out float3 output)
{
    float3 colour = SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, uv).rgb;

    float2 rgbSplitDirection = direction * rgbSplit;

    for (int i = 1; i <= rgbSplitSteps; i++)
    {
        float progress = i / (float)rgbSplitSteps;

        float2 rgbSplitDirectionProgress = progress * rgbSplitDirection;

        colour.r += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, uv + rgbSplitDirectionProgress).r;
        colour.b += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, uv - rgbSplitDirectionProgress).b;
    }

    output = colour;

    output.rb /= (rgbSplitSteps + 1);
}

// Backup.

void RGBSplit2_float(Texture2D tex, float2 uv, float2 rgbSplit, int rgbSplitSteps, float2 direction, out float3 output)
{
    float3 colour = SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, uv).rgb;

    float2 rgbSplitDirection = direction * rgbSplit;

    for (int i = 1; i <= rgbSplitSteps; i++)
    {
        float progress = i / (float)rgbSplitSteps;

        float rgbSplitDirectionProgress = progress * length(rgbSplitDirection);

        float rgbSplitProgressX = rgbSplit.x * rgbSplitDirectionProgress;
        float rgbSplitProgressY = rgbSplit.y * rgbSplitDirectionProgress;

        // Left/right.

        colour.r += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, float2(uv.x + rgbSplitProgressX, uv.y)).r;
        colour.g += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, float2(uv.x - rgbSplitProgressX, uv.y)).g;

        // Up.

        colour.b += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, float2(uv.x, uv.y + rgbSplitProgressY)).b;
    }

    output = colour / (rgbSplitSteps + 1);
}

// Smooth RGB split/shift.

float3 RGBSplit(sampler2D tex, float2 uv, float2 rgbSplit, int rgbSplitSteps)
{
    float3 colour = 0.0;
    float rgbSplutStepsMinusOne = max(1.0, rgbSplitSteps - 1);

    for (int i = 1; i <= rgbSplitSteps; i++)
    {
        float progress = i / (float)rgbSplitSteps;

        float rgbSplitProgressX = rgbSplit.x * progress;
        float rgbSplitProgressY = rgbSplit.y * progress;

        // Left/right.

        colour.r += tex2D(tex, float2(uv.x + rgbSplitProgressX, uv.y)).r;
        colour.g += tex2D(tex, float2(uv.x - rgbSplitProgressX, uv.y)).r;

        // Up.

        colour.b += tex2D(tex, float2(uv.x, uv.y + rgbSplitProgressY)).r;
    }

    return colour / rgbSplitSteps;
}

// Smooth RGB split/shift effect using a cube texture (which requires offset as rotation, not translation off sampling coordinates).

float3 RGBSplitCube(samplerCUBE tex, float3 normal, float2 rgbSplit, int rgbSplitSteps)
{
    float3 colour = 0.0;

    const float3 ZERO = 0.0;

    const float3 UP = float3(0.0, 1.0, 0.0);
    const float3 RIGHT = float3(1.0, 0.0, 0.0);

    for (int i = 1; i <= rgbSplitSteps; i++)
    {
        float progress = i / (float)rgbSplitSteps;

        float rgbSplitProgressX = rgbSplit.x * progress;
        float rgbSplitProgressY = rgbSplit.y * progress;

        // Left/right.

        float3 rotatedNormalR = RotateAroundAxis(ZERO, normal, UP, rgbSplitProgressX * +1.0);
        float3 rotatedNormalB = RotateAroundAxis(ZERO, normal, UP, rgbSplitProgressX * -1.0);

        // Up.

        float3 rotatedNormalG = RotateAroundAxis(ZERO, normal, RIGHT, -rgbSplitProgressY);

        colour.r += texCUBE(tex, rotatedNormalR).r;
        colour.g += texCUBE(tex, rotatedNormalG).r;
        colour.b += texCUBE(tex, rotatedNormalB).r;
    }

    return colour / rgbSplitSteps;
}

// Gaussian blur.

#define PI 3.14159265359
#define TAU 2.0 * PI

// Blur radius directly controls quality and blur amount.

// When using Amplify Shader Editor + _CameraOpaqueTexture,
// enable 'Use Sampling Macros' on the shader to force ASE to declare as texture2D, vs. sampler2D.

// Bias controls blur direction:

//  0.0 = blur in both directions of that axis.
//  1.0 = blur only in positive direction of that axis.
// -1.0 = blur only in negative direction of that axis.

#define MAX_GAUSSIAN_BLUR_QUALITY 32
#define GAUSSIAN_BLUR_KERNEL_SIZE (MAX_GAUSSIAN_BLUR_QUALITY * 2) + 1

void FillGaussianKernel(int quality, out float kernel[MAX_GAUSSIAN_BLUR_QUALITY * 2 + 1])
{
    // Pre-compute Gaussian kernel.

    float sigma = quality / 2.0;
    float sigmaSqr = sigma * sigma;

    float oneOverTauSigmaSqr = 1.0 / (TAU * sigmaSqr);
    
    for (int i = 0; i <= quality * 2; i++)
    {
        float x = float(i) - quality;
        kernel[i] = oneOverTauSigmaSqr * exp(-x * x / (2.0 * sigmaSqr));
    }
}

void GaussianBlur_float(Texture2D tex, float2 uv, float2 texelSize, SamplerState samplerState, int quality, float2 radius, float2 directionBias, out float4 output)
{
    // If no blur, return current pixel as-is.
    
    // Very useful when masking the radius. 
    // More masking (to zero radius) -> more FPS.
    
    // Noticeable (beneficial) difference in performance
    // when using this check on 4096 x 4096 with 8 quality.
    
    // Up to +20 FPS on RTX 4090 with a simple radial mask w/ pow(mask, 2.0).
    
    if (radius.x <= 0.0 && radius.y <= 0.0)
    {
        output = SAMPLE_TEXTURE2D(tex, samplerState, uv);
        return;
    }
    
	// Pre-calculations.
    
    float4 colour = 0.0;
    float kernelWeightSum = 0.0;
    
    float2 directionBiasSign = sign(directionBias);
    float2 oneMinusDirectionBiasAbsMax = max(0.0, 1.0 - abs(directionBias));
    
    radius *= texelSize / quality;

    // Pre-compute Gaussian kernel.

    float kernel[GAUSSIAN_BLUR_KERNEL_SIZE];
    FillGaussianKernel(quality, kernel);

    // Blur (average) the pixels around current pixel based on Gaussian kernel.
    
    [loop] // Force loop required for radius == 0.0 bool.
    for (int y = -quality; y <= quality; y++)
    {
        [loop]
        for (int x = -quality; x <= quality; x++)
        {
            // Apply bias to blur direction.                   
            
            // I could make the last part just float(x > 0) vs.            
            // (float(x) * sign(xBias)) > 0.0, but in that case I can't
            // automatically handle negative values for bias being used
            // to determine blur direction and would need to invert the
            // input radius what that axis. I figure the convenience
            // and clarity is worth the extra sign calculation.
            
            float2 bias = lerp(1.0, oneMinusDirectionBiasAbsMax, (float2(x, y) * directionBiasSign) > 0.0);
            
            float weightBias = bias.x * bias.y;
            
            // Skip if no blur in this direction.
            
            if (weightBias <= 0.0)
            {
                continue;
            }

            float2 offset = float2(x, y) * radius;

            float weight = kernel[quality + x] * kernel[quality + y] * weightBias;
            colour += SAMPLE_TEXTURE2D(tex, samplerState, uv + offset) * weight;

            kernelWeightSum += weight;
        }
    }

	// Normalize accumulated blur colour by iterations and return.

    output = colour / kernelWeightSum;
}

// Pass in existing kernel, already filled with the correct values for the designated quality.

void GaussianBlurPreKernel_float(Texture2D tex, float2 uv, float2 texelSize, SamplerState samplerState, int quality, float2 radius, float2 directionBias, float kernel[GAUSSIAN_BLUR_KERNEL_SIZE], out float3 output)
{
    if (radius.x <= 0.0 && radius.y <= 0.0)
    {
        output = SAMPLE_TEXTURE2D(tex, samplerState, uv).rgb;
        return;
    }
    
	// Pre-calculations.
    
    float3 colour = 0.0;
    float kernelWeightSum = 0.0;
    
    float2 directionBiasSign = sign(directionBias);
    float2 oneMinusDirectionBiasAbsMax = max(0.0, 1.0 - abs(directionBias));
    
    radius *= texelSize / quality;
    
    // Blur (average) the pixels around current pixel based on Gaussian kernel.
    
    [loop] // Force loop required for radius == 0.0 bool.
    for (int y = -quality; y <= quality; y++)
    {
        [loop]
        for (int x = -quality; x <= quality; x++)
        {
            // Apply bias to blur direction.
            
            float2 bias = lerp(1.0, oneMinusDirectionBiasAbsMax, (float2(x, y) * directionBiasSign) > 0.0);
            
            float weightBias = bias.x * bias.y;
            
            // Skip if no blur in this direction.
            
            if (weightBias <= 0.0)
            {
                continue;
            }

            float2 offset = float2(x, y) * radius;

            float weight = kernel[quality + x] * kernel[quality + y] * weightBias;
            colour += SAMPLE_TEXTURE2D(tex, samplerState, uv + offset).rgb * weight;

            kernelWeightSum += weight;
        }
    }

	// Normalize accumulated blur colour by iterations and return.

    output = colour / kernelWeightSum;
}

// NOTE: Unity's Random.hlsl also has InterleavedGradientNoise(), but that one has some banding with high noise strength.

float IGNoise(float2 position, int frame)
{
    position += (float(frame) * 5.588238f);
    return frac(52.9829189f * frac(0.06711056f * float(position.x) + 0.00583715f * float(position.y)));
}

// ...

float InverseLerp(float from, float to, float value)
{
    return (value - from) / (to - from);
}

// https://docs.unity3d.com/Packages/com.unity.shadergraph@17.0/manual/Saturation-Node.html

void Unity_Saturation_float(float3 In, float Saturation, out float3 Out)
{
    float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
    Out =  luma.xxx + Saturation.xxx * (In - luma.xxx);
}

// Fresnel.

float Fresnel(float3 normal, float3 viewDir)
{
    return 1.0 - dot(normal, viewDir);
}
float Fresnel(float3 normal, float3 viewDir, float power)
{
    return pow(Fresnel(normal, viewDir), power);
}

// Calculates and adds translucent light contribution to colour.

#define SUM_LIGHT_TRANSLUCENCY(light, strength)\
{\
    float3 translucencyAttenuation = light.color * light.distanceAttenuation;\
    translucencyAttenuation = lerp(translucencyAttenuation, translucencyAttenuation * light.shadowAttenuation, translucencyShadows);\
    float3 distortedLightDirection = light.direction + (inputData.normalWS * translucencyNormalDistortion);\
    float VdotL = pow(saturate(dot(inputData.viewDirectionWS, -distortedLightDirection)), translucencyScattering);\
    float3 translucencyLight = translucencyColour.rgb * ((VdotL * translucencyDirect) + (inputData.bakedGI * translucencyAmbient));\
    colour.rgb += ((translucencyLight * translucencyAttenuation) * (translucencyStrength * translucencyColour.a)) * strength;\
}

// Glass.
// IOR from (source) -> to medium.

void Glass_float(

    Texture2D tex, float2 uv,
    float3 position, float3 normal, float3 viewDir,

    float ior, float thickness, float refraction, int refractionModel,

    float chromaticDispersion, int chromaticDispersionSteps,

    float blurRadius, int blurQuality, float blurRefractionBias, float blurDirectionRefractionBias,

    float4 unlitFresnelColour, float unlitFresnelPower,
    float unlitFresnelRemapMin, float unlitFresnelRemapMax,

    float4 litFresnelColour, float litFresnelPower,
    float litFresnelRemapMin, float litFresnelRemapMax,

    float litFresnelRemapSmoothStep,

    float litFresnelLightingBlend,

    float litFresnelMainLightStrength, float litFresnelAdditionalLightStrength,
    float litFresnelMainLightSaturation, float litFresnelAdditionalLightSaturation,

    bool litFresnelPriority,

    float4 translucencyColour, float translucencyStrength, float translucencyNormalDistortion, float translucencyScattering, float translucencyDirect, float translucencyAmbient, float translucencyShadows,

    float translucencyMainLightStrength,
    float translucencyAdditionalLightStrength,

    float2 uv_static, float2 uv_dynamic,

    float volumetricLightingRaymarchDistance, int volumetricLightingRaymarchSteps,
    float volumetricLightingRaymarchRefraction, float volumetricLightingScattering,
    float volumetricLightingMainLightStrength, float volumetricLightingAdditionalLightStrength,

    out float3 output)
{
    // Set texture.
    // Use _CameraOpaqueTexture by default.

#ifndef _CUSTOM_CAMERA_TEXTURE
    tex = _CameraOpaqueTexture;
#endif

    // Calculate view direction.

    float3 cameraPosition = GetCameraPositionWS();
    float3 cameraToSurface = position - cameraPosition;    

    float cameraDistanceToSurface = length(cameraToSurface);    
    float3 cameraDirectionToSurface = cameraToSurface / cameraDistanceToSurface;

    viewDir = -cameraDirectionToSurface;

    // View direction = surface -> camera position.
    // Camera -> surface (position of vertex or fragment).

    float3 inverseViewDirection = cameraDirectionToSurface;

    // Refraction.

    float3 refractedRay_r;
    float3 refractedRay_g;
    float3 refractedRay_b;

    // Smooth, shifting refraction loop (via view-space IOR raymarching sum and averaging, aka: 2D refraction blur).

    float3 colour = 0.0;

    for (int r = 0; r < chromaticDispersionSteps; r++)
    {
        float t = (r + 1.0) / chromaticDispersionSteps;
        float chromaticDispersionOffset = t * chromaticDispersion;

        float ior_r = ior + chromaticDispersionOffset;
        float ior_g = ior + chromaticDispersionOffset / 2.0;
        float ior_b = ior - chromaticDispersionOffset;

        RefractionModelResult refractionModelResult;

        // Refraction model.

        // 0 = default.
        // 1 = sphere.
        // 2 = box.

        if (refractionModel == 0)
        {
            refractedRay_r = refract(inverseViewDirection, normal, 1.0 / ior_r);
            refractedRay_g = refract(inverseViewDirection, normal, 1.0 / ior_g);
            refractedRay_b = refract(inverseViewDirection, normal, 1.0 / ior_b);
        }
        else if (refractionModel == 1)
        {
            refractionModelResult = RefractionModelSphere(viewDir, position, normal, ior_r, thickness);
            refractedRay_r = refractionModelResult.rayWS * refractionModelResult.dist;

            refractionModelResult = RefractionModelSphere(viewDir, position, normal, ior_g, thickness);
            refractedRay_g = refractionModelResult.rayWS * refractionModelResult.dist;

            refractionModelResult = RefractionModelSphere(viewDir, position, normal, ior_b, thickness);
            refractedRay_b = refractionModelResult.rayWS * refractionModelResult.dist;
        }
        else if (refractionModel == 2)
        {
            refractionModelResult = RefractionModelBox(viewDir, position, normal, ior_r, thickness);
            refractedRay_r = refractionModelResult.rayWS * refractionModelResult.dist;

            refractionModelResult = RefractionModelBox(viewDir, position, normal, ior_g, thickness);
            refractedRay_g = refractionModelResult.rayWS * refractionModelResult.dist;

            refractionModelResult = RefractionModelBox(viewDir, position, normal, ior_b, thickness);
            refractedRay_b = refractionModelResult.rayWS * refractionModelResult.dist;
        }   

        // Invalid refraction model.
        // Force magenta output... (lol).

        else
        {
            output = float3(1.0, 0.0, 1.0); return;
        }

        // Transform refraction from world to view space.

        float2 refractedRay_r_viewSpace = TransformWorldToViewDir(refractedRay_r);
        float2 refractedRay_g_viewSpace = TransformWorldToViewDir(refractedRay_g);
        float2 refractedRay_b_viewSpace = TransformWorldToViewDir(refractedRay_b);

        // Aspect ratio correction.

        float aspectRatio = _ScreenParams.x / _ScreenParams.y;

        refractedRay_r_viewSpace.x /= aspectRatio;
        refractedRay_g_viewSpace.x /= aspectRatio;
        refractedRay_b_viewSpace.x /= aspectRatio;

        // Scale refraction strength.

        refractedRay_r_viewSpace *= refraction;
        refractedRay_g_viewSpace *= refraction;
        refractedRay_b_viewSpace *= refraction;

        // Sample texture at the refracted ray direction.

        float2 refractedUV_g_raw = uv + refractedRay_g_viewSpace;
        float2 edgeBoundaryV2 = min(refractedUV_g_raw, 1.0 - refractedUV_g_raw);
        float edgeFadeV2 = saturate(min(edgeBoundaryV2.x, edgeBoundaryV2.y) / 0.05);
        float2 refractedUV_r = lerp(uv, uv + refractedRay_r_viewSpace, edgeFadeV2);
        float2 refractedUV_g = lerp(uv, refractedUV_g_raw, edgeFadeV2);
        float2 refractedUV_b = lerp(uv, uv + refractedRay_b_viewSpace, edgeFadeV2);

        // Blur. 

        // On mobile, performance is annihilated if I use an if-statement to separate the two below.
        // Hence, the compile-time preprocessor stuff.

#ifdef _BLUR_MODE_GAUSSIAN
            // Internally, the 2D blurRadius is scaled by this.
            // We can use this to implement aspect ratio correction.
            // 1.0 / textureSize == texelSize, but I want aspect ratio scaling
            // only, not interpreting blurRadius as pixel radius.

            // Interpreting blurRadius as pixel (radius) is the result of scaling by texelSize.
            // It's good for pixel-number input accuracy, but it changes the blur based on
            // resolution (even with the same aspect ratio), so I prefer to not do that
            // and keep it a relative scale (radius) to the screen.

            // So, I simply don't use the actual texelSize, and instead scale against 1.0.
            // And yes, I need to divide to properly match the aspect ratio scale on that axis.
            // Alternatively, I could set a 2D blur radius, with the X-axis scaled directly.

            float2 gaussianBlurRadiusScale = float2(1.0 / aspectRatio, 1.0);

            // Refraction-biased blur radius (no refraction = no blur, and vice-versa).

            // Per-channel blur radius variation from refraction is tested to be visually negligible.
            // -> Just pick one and use the same radius for all three (green is middle, use that).

            // Prevent divide by zero.

            float scaledBlurRadius = refraction == 0.0 ?
                blurRadius : lerp(blurRadius, blurRadius * (length(refractedRay_g_viewSpace) / refraction), blurRefractionBias);

            float3 gaussianBlur;

            // Much faster to share a kernel between the three RGB channel blurs that have identical quality parameters.
            // -> Because a kernel is unique to the int quality (which itself determines blur iterations).

            float gaussianBlurKernel[GAUSSIAN_BLUR_KERNEL_SIZE];
            FillGaussianKernel(blurQuality, gaussianBlurKernel);        

            GaussianBlurPreKernel_float(tex, refractedUV_r, gaussianBlurRadiusScale, sampler_LinearClamp, blurQuality, scaledBlurRadius, refractedRay_r_viewSpace * blurDirectionRefractionBias, gaussianBlurKernel, gaussianBlur);
            colour.r += gaussianBlur.r;
    
            GaussianBlurPreKernel_float(tex, refractedUV_g, gaussianBlurRadiusScale, sampler_LinearClamp, blurQuality, scaledBlurRadius, refractedRay_g_viewSpace * blurDirectionRefractionBias, gaussianBlurKernel, gaussianBlur);
            colour.g += gaussianBlur.g;

            GaussianBlurPreKernel_float(tex, refractedUV_b, gaussianBlurRadiusScale, sampler_LinearClamp, blurQuality, scaledBlurRadius, refractedRay_b_viewSpace * blurDirectionRefractionBias, gaussianBlurKernel, gaussianBlur);
            colour.b += gaussianBlur.b;
#else
            colour.r += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, refractedUV_r).r;
            colour.g += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, refractedUV_g).g;
            colour.b += SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, refractedUV_b).b;
#endif
    }

    colour /= chromaticDispersionSteps;

    // Lighting.

    InputData inputData = (InputData)0;

    inputData.positionWS = position;
    inputData.normalWS = normal;
    inputData.viewDirectionWS = viewDir;

    inputData.normalizedScreenSpaceUV = uv;
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);

    // Static and dynamic are by default UV1 (not 0) and UV2.
    // After normal, you have the screen space pixel position (screen UV * screen resolution).

    float2 pixelCoordinates = inputData.normalizedScreenSpaceUV * _ScreenParams.xy;

    inputData.bakedGI = SHADERGRAPH_BAKED_GI(inputData.positionWS, inputData.normalWS, pixelCoordinates, uv_static, uv_dynamic, true);

    // Main light.

    Light mainLight = GetMainLight(inputData.shadowCoord);

#ifdef _TRANSLUCENCY_ENABLED
    SUM_LIGHT_TRANSLUCENCY(mainLight, translucencyMainLightStrength);
#endif

    float3 mainLighting;

    mainLighting = mainLight.color;
    mainLighting *= mainLight.shadowAttenuation * mainLight.distanceAttenuation;

    // Volumetric raymarching translucency (main light).

#ifdef _VOLUMETRIC_LIGHTING_ENABLED

    // Raymarch setup.
    // -> Re-uses middle/green channel ray for refraction.

    float rayStep = volumetricLightingRaymarchDistance / volumetricLightingRaymarchSteps;

    // Add noise to ray step to vary distance randomly, across/depending on screen UVs, every frame.

    // Added as offset to calculated ray step in loop.
    // Simulates up to an additional step's worth of (random) distance.
  
    float interleavedGradientNoise = InterleavedGradientNoise(pixelCoordinates, (_Time.y * 60) % 60);
    float rayStepNoise = rayStep * interleavedGradientNoise;

    float3 rayDirection = normalize(lerp(inverseViewDirection, refractedRay_g, volumetricLightingRaymarchRefraction));

    // Reset.

    float raymarchScattering = 0.0;
    float raymarchTransmittance = 1.0;

    // March! -- main light.

    for (int i = 0; i < volumetricLightingRaymarchSteps; i++)
    {
        // Calculate where we are.

        float raymarchDepth = i * rayStep;
        raymarchDepth += rayStepNoise;

        float3 raymarchPosition = inputData.positionWS;
        raymarchPosition += rayDirection * raymarchDepth;

        // Sample.

#ifndef _VOLUMETRIC_LIGHTING_TRANSMITTANCE
        
        // Simple accumulation and averaging.

        float sample = exp(-raymarchDepth * volumetricLightingScattering);
        sample *= MainLightRealtimeShadow(TransformWorldToShadowCoord(raymarchPosition));

        raymarchScattering += sample;
#else
        // Transmittance.

        float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(raymarchPosition));
        float stepExtinction = volumetricLightingScattering * rayStep;

        // Exponential scattering accumulation for constant density.

        float transmittance = raymarchTransmittance * shadow;
        raymarchScattering += transmittance * (1.0 - exp(-stepExtinction));

        // Update for next step.

        raymarchTransmittance *= exp(-stepExtinction);
#endif
    }

#ifndef _VOLUMETRIC_LIGHTING_TRANSMITTANCE
    raymarchScattering /= volumetricLightingRaymarchSteps;
#endif

    colour += mainLight.color * (raymarchScattering * volumetricLightingMainLightStrength);
#endif

    // Additional lights.

    uint meshRenderingLayers = GetMeshRenderingLayer();
    uint pixelLightCount = GetAdditionalLightsCount();

    float3 additionalLighting = 0.0;
    float3 additionalLightingVolumetric = 0.0;

    // Light loop.

    LIGHT_LOOP_BEGIN(pixelLightCount)
    {
        Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);

        #ifdef _LIGHT_LAYERS
        {
            if (!IsMatchingLightLayer(additionalLight.layerMask, meshRenderingLayers))
            {
                continue;
            }
        }
        #endif

        additionalLight.shadowAttenuation = AdditionalLightRealtimeShadow(

            lightIndex,

            inputData.positionWS,
            additionalLight.direction,

            GetAdditionalLightShadowParams(lightIndex),
            GetAdditionalLightShadowSamplingData(lightIndex)
        );

#ifdef _TRANSLUCENCY_ENABLED
        SUM_LIGHT_TRANSLUCENCY(additionalLight, translucencyAdditionalLightStrength);
#endif

        float3 baseContribution = additionalLight.color * (additionalLight.shadowAttenuation * additionalLight.distanceAttenuation);
        additionalLighting += baseContribution;

        // Volumetric raymarching translucency (additional lights).

#ifdef _VOLUMETRIC_LIGHTING_ENABLED
        // Reset.

        raymarchScattering = 0.0;
        raymarchTransmittance = 1.0;

        // March! -- additional lights.

        for (int i = 0; i < volumetricLightingRaymarchSteps; i++)
        {
            // Calculate where we are.

            float raymarchDepth = i * rayStep;
            raymarchDepth += rayStepNoise;

            float3 raymarchPosition = inputData.positionWS;
            raymarchPosition += rayDirection * raymarchDepth;

            // Sample.

            additionalLight.shadowAttenuation = AdditionalLightRealtimeShadow(

                lightIndex,

                raymarchPosition,
                additionalLight.direction,

                GetAdditionalLightShadowParams(lightIndex),
                GetAdditionalLightShadowSamplingData(lightIndex)
            );

#ifndef _VOLUMETRIC_LIGHTING_TRANSMITTANCE
            float sample = exp(-raymarchDepth * volumetricLightingScattering);
            sample *= additionalLight.shadowAttenuation * additionalLight.distanceAttenuation;

            raymarchScattering += sample;
#else
            float stepExtinction = volumetricLightingScattering * rayStep;

            float transmittance = raymarchTransmittance * additionalLight.shadowAttenuation * additionalLight.distanceAttenuation;
            raymarchScattering += transmittance * (1.0 - exp(-stepExtinction));

            raymarchTransmittance *= exp(-stepExtinction);
#endif
        }

#ifndef _VOLUMETRIC_LIGHTING_TRANSMITTANCE
        raymarchScattering /= volumetricLightingRaymarchSteps;
#endif

        additionalLightingVolumetric += baseContribution * raymarchScattering;
#endif


    }
    LIGHT_LOOP_END

#ifdef _VOLUMETRIC_LIGHTING_ENABLED
    colour += additionalLightingVolumetric * volumetricLightingAdditionalLightStrength;
#endif

    // Fresnel.

    float fresnel = Fresnel(normal, viewDir);

    // Unlit fresnel.

#ifdef _UNLIT_FRESNEL_ENABLED
    float unlitFresnel = pow(fresnel, unlitFresnelPower);
    unlitFresnel = smoothstep(unlitFresnelRemapMin, unlitFresnelRemapMax, unlitFresnel) * unlitFresnelColour.a;
#endif

    // Lit fresnel.

#ifdef _LIT_FRESNEL_ENABLED
    float litFresnelTerm = pow(fresnel, litFresnelPower);

    // If I don't saturate inverseLerp, I get bad values for anything other than when smoothstep = 1.0 (max).

    float litFresnelTermRemapSmoothStep = smoothstep(litFresnelRemapMin, litFresnelRemapMax, litFresnelTerm);
    float litFresnelTermRemapInverseLerp = saturate(InverseLerp(litFresnelRemapMin, litFresnelRemapMax, litFresnelTerm));

    litFresnelTerm = lerp(litFresnelTermRemapInverseLerp, litFresnelTermRemapSmoothStep, litFresnelRemapSmoothStep);

    float3 litFresnel = litFresnelColour * (litFresnelTerm * litFresnelColour.a);

    // Lit fresnel saturation.

    float3 litFresnelLighting_main = mainLighting;
    float3 litFresnelLighting_additional = additionalLighting;

    Unity_Saturation_float(litFresnelLighting_main, litFresnelMainLightSaturation,  litFresnelLighting_main);
    Unity_Saturation_float(litFresnelLighting_additional, litFresnelAdditionalLightSaturation, litFresnelLighting_additional);

    // Lit fresnel strength.

    litFresnelLighting_main *= litFresnelMainLightStrength;
    litFresnelLighting_additional *= litFresnelAdditionalLightStrength;

    // Lit fresnel final composition.
    // Combine lit fresnel lighting, and blend with lighting.

    float3 litFresnelLighting = litFresnelLighting_main + litFresnelLighting_additional;
    litFresnel = lerp(litFresnel, litFresnel * litFresnelLighting, litFresnelLightingBlend);
#endif

    // Apply fresnel terms.

    if (litFresnelPriority)
    {
#ifdef _UNLIT_FRESNEL_ENABLED
        colour = lerp(colour, unlitFresnelColour.rgb, unlitFresnel);
#endif
#ifdef _LIT_FRESNEL_ENABLED
        colour += litFresnel;
#endif
    }
    else
    {
#ifdef _LIT_FRESNEL_ENABLED
        colour += litFresnel;
#endif
#ifdef _UNLIT_FRESNEL_ENABLED
        colour = lerp(colour, unlitFresnelColour.rgb, unlitFresnel);
#endif
    }

    // Output.

    output = colour;
}

// Backup.

void GlassSimple_float(Texture2D tex, float2 uv, float2 textureSize, float3 position, float3 normal, float3 viewDir, float ior, float thickness, float refraction, int refractionModel, float rgbSplit, float4 fresnelColour, float fresnelPower, out float3 output)
{
    // Refraction.

    float3 refractedRay;
    RefractionModelResult refractionModelResult;

    // Refraction model.

    // 0 = default.
    // 1 = sphere.
    // 2 = box.

    if (refractionModel == 0)
    {
        float eta = 1.0 / ior;
        refractedRay = refract(-viewDir, normal, eta);
    }
    else if (refractionModel == 1)
    {
        refractionModelResult = RefractionModelSphere(viewDir, position, normal, ior, thickness);
        refractedRay = refractionModelResult.rayWS * refractionModelResult.dist;
    }
    else if (refractionModel == 2)
    {
        refractionModelResult = RefractionModelBox(viewDir, position, normal, ior, thickness);
        refractedRay = refractionModelResult.rayWS * refractionModelResult.dist;
    }   

    // Invalid refraction model.
    // Force magenta output... (lol).

    else
    {
        output = float3(1.0, 0.0, 1.0); return;
    }

    // Transform refraction from world to view space.

    float2 refractedRay_viewSpace = TransformWorldToViewDir(refractedRay);

    // Aspect ratio correction.

    float aspectRatio = textureSize.x / textureSize.y;
    refractedRay_viewSpace.x /= aspectRatio;

    // Scale refraction strength.

    refractedRay_viewSpace *= refraction;

    // Sample texture at the refracted ray direction.

    float2 refractedUV = uv + refractedRay_viewSpace;

    // Sample texture.

    float3 colour = SAMPLE_TEXTURE2D(tex, sampler_LinearClamp, refractedUV).rgb;

    // RGB split/shift effect.

    RGBSplit_float(tex, refractedUV, rgbSplit, 8, refractedRay_viewSpace, colour);

    // Mix in fresnel/rim lighting.

    float fresnel = Fresnel(normal, viewDir, fresnelPower);
    colour = lerp(colour, fresnelColour.rgb, fresnel * fresnelColour.a);

    // Output.

    output = colour;
}

#endif