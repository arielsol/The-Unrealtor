#ifndef STAINEDGLASS_HELPERS_HLSL
#define STAINEDGLASS_HELPERS_HLSL

// ============================================================================
// Stained Glass Helper Functions
// For use with URP (Universal Render Pipeline) - Unity 6+
// ============================================================================

// --- Color Gradient Functions ---

// Blend between two colors based on UV coordinate and gradient parameters
float3 GradientBlend(float3 colorA, float3 colorB, float2 uv, float gradientAngle, float gradientOffset, float gradientSharpness)
{
    // Rotate UV based on gradient angle
    float rad = radians(gradientAngle);
    float cosA = cos(rad);
    float sinA = sin(rad);

    // Center UVs for rotation
    float2 centeredUV = uv - 0.5;
    float rotatedValue = centeredUV.x * cosA + centeredUV.y * sinA + 0.5;

    // Apply offset and sharpness
    float t = saturate((rotatedValue - gradientOffset) * gradientSharpness + 0.5);

    return lerp(colorA, colorB, t);
}

// Multi-zone color blending for stained glass panels
// Creates distinct color zones across the surface
float3 MultiZoneColor(float3 colorA, float3 colorB, float3 colorC, float2 uv, float zoneCount, float blendSoftness)
{
    // Create zone pattern based on UV
    float zoneValue = frac(uv.x * zoneCount + uv.y * zoneCount * 0.618);

    float3 result;
    float zone1 = smoothstep(0.0, blendSoftness, zoneValue);
    float zone2 = smoothstep(0.33, 0.33 + blendSoftness, zoneValue);
    float zone3 = smoothstep(0.66, 0.66 + blendSoftness, zoneValue);

    result = lerp(colorA, colorB, zone1);
    result = lerp(result, colorC, zone2);
    result = lerp(result, colorA, zone3);

    return result;
}

// --- Emission Functions ---

// Pulsing emission effect (optional breathing glow)
float EmissionPulse(float time, float speed, float minIntensity, float maxIntensity)
{
    float pulse = sin(time * speed) * 0.5 + 0.5;
    return lerp(minIntensity, maxIntensity, pulse);
}

// Edge-enhanced emission - brighter emission at panel edges
float EdgeEmission(float2 uv, float edgeWidth, float edgePower)
{
    float2 edgeDist = min(uv, 1.0 - uv);
    float edge = 1.0 - saturate(min(edgeDist.x, edgeDist.y) / edgeWidth);
    return pow(edge, edgePower);
}

// --- Light Transmission Functions ---

// Calculate how much light color should be tinted by the glass
float3 TransmissionTint(float3 lightColor, float3 glassColor, float tintStrength)
{
    // The transmitted light takes on the color of the glass
    float3 tintedLight = lightColor * glassColor;
    return lerp(lightColor, tintedLight, tintStrength);
}

// Fresnel-based transmission - more color at glancing angles
float FresnelTransmission(float3 normal, float3 viewDir, float power, float scale)
{
    float NdotV = saturate(dot(normal, viewDir));
    return scale * pow(1.0 - NdotV, power);
}

// --- Pattern Functions ---

// Simple lead/came line pattern for stained glass panel divisions
float LeadLines(float2 uv, float lineWidth, float gridSizeX, float gridSizeY)
{
    float2 grid = frac(uv * float2(gridSizeX, gridSizeY));
    float2 lines = smoothstep(0.0, lineWidth, grid) * smoothstep(0.0, lineWidth, 1.0 - grid);
    return lines.x * lines.y;
}

// Voronoi-based pattern for organic stained glass shapes
float2 VoronoiHash(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(p) * 43758.5453);
}

float VoronoiPattern(float2 uv, float scale, out float2 cellID)
{
    float2 g = floor(uv * scale);
    float2 f = frac(uv * scale);

    float minDist = 1.0;
    cellID = float2(0, 0);

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 offset = float2(x, y);
            float2 n = VoronoiHash(g + offset);
            float2 diff = offset + n - f;
            float dist = dot(diff, diff);

            if (dist < minDist)
            {
                minDist = dist;
                cellID = g + offset;
            }
        }
    }

    return sqrt(minDist);
}

// Per-cell random color from a palette using Voronoi cell IDs
float3 CellColor(float2 cellID, float3 colorA, float3 colorB, float3 colorC, float3 colorD)
{
    float hash = frac(sin(dot(cellID, float2(12.9898, 78.233))) * 43758.5453);

    float3 result;
    if (hash < 0.25)
        result = colorA;
    else if (hash < 0.5)
        result = colorB;
    else if (hash < 0.75)
        result = colorC;
    else
        result = colorD;

    return result;
}

// Smooth cell color with blending
float3 CellColorSmooth(float2 cellID, float3 colorA, float3 colorB, float3 colorC, float hash)
{
    float t = frac(hash * 2.3456);
    float3 result;
    if (t < 0.333)
        result = lerp(colorA, colorB, t * 3.0);
    else if (t < 0.666)
        result = lerp(colorB, colorC, (t - 0.333) * 3.0);
    else
        result = lerp(colorC, colorA, (t - 0.666) * 3.0);

    return result;
}

// --- Iridescence ---
// Thin-film interference approximation for iridescent stained glass
float3 ThinFilmIridescence(float NdotV, float thickness, float intensity)
{
    float phase = NdotV * thickness;
    float3 iridescence;
    iridescence.r = sin(phase * 6.28318 + 0.0) * 0.5 + 0.5;
    iridescence.g = sin(phase * 6.28318 + 2.094) * 0.5 + 0.5;
    iridescence.b = sin(phase * 6.28318 + 4.189) * 0.5 + 0.5;

    return lerp(float3(1, 1, 1), iridescence, intensity);
}

#endif // STAINEDGLASS_HELPERS_HLSL
