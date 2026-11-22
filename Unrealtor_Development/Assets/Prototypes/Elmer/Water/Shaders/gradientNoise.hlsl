float2 GradientNoiseDir(float2 p)
{
    p = fmod(p, 289.0);
    float x = fmod((34.0 * p.x + 1.0) * p.x, 289.0) + p.y;
    x = fmod((34.0 * x + 1.0) * x, 289.0);
    x = frac(x / 41.0) * 2.0 - 1.0;
    return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
}

void UnityGradientNoise_float(float2 UV, float Scale, out float Out)
{
    Out = 0.0;

    
    float2 p = UV * Scale;
    float2 ip = floor(p);
    float2 fp = frac(p);
    
    float d00 = dot(GradientNoiseDir(ip), fp);
    float d01 = dot(GradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
    float d10 = dot(GradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
    float d11 = dot(GradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
    
    fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
    
    Out = lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
}