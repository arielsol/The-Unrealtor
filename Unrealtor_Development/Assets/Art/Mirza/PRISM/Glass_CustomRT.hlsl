#ifndef GLASS_CUSTOM_RT_HLSL
#define GLASS_CUSTOM_RT_HLSL

// Whatever needed that can't also have an include.
// Nothing here can/should rely on includes...

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

void GaussianBlur_float(sampler2D tex, float2 uv, float2 texelSize, SamplerState samplerState, int quality, float2 radius, float2 directionBias, out float4 output)
{
    // If no blur, return current pixel as-is.
    
    // Very useful when masking the radius. 
    // More masking (to zero radius) -> more FPS.
    
    // Noticeable (beneficial) difference in performance
    // when using this check on 4096 x 4096 with 8 quality.
    
    // Up to +20 FPS on RTX 4090 with a simple radial mask w/ pow(mask, 2.0).
    
    if (radius.x <= 0.0 && radius.y <= 0.0)
    {
        output = tex2D(tex, uv);
        return;
    }
    
	// Pre-calculations.
    
    float4 colour = 0.0;
    float kernelWeightSum = 0.0;
    
    float2 directionBiasSign = sign(directionBias);
    float2 oneMinusDirectionBiasAbsMax = max(0.0, 1.0 - abs(directionBias));
    
    radius *= texelSize / quality;

    // Pre-compute Gaussian kernel.

    const int kernelSize = (MAX_GAUSSIAN_BLUR_QUALITY * 2) + 1;

    float kernel[kernelSize];

    float sigma = quality / 2.0;
    float sigmaSqr = sigma * sigma;

    float oneOverTauSigmaSqr = 1.0 / (TAU * sigmaSqr);
    
    for (int i = 0; i <= quality * 2; i++)
    {
        float x = float(i) - quality;
        kernel[i] = oneOverTauSigmaSqr * exp(-x * x / (2.0 * sigmaSqr));
    }

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
            colour += tex2D(tex, uv + offset) * weight;

            kernelWeightSum += weight;
        }
    }

	// Normalize accumulated blur colour by iterations and return.

    output = colour / kernelWeightSum;
}

#endif