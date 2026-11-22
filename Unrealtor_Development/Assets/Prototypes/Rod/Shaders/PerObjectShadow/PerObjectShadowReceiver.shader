Shader "Unlit/PerObjectShadowReceiver"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Name "PerObjectSceneShadowComposite"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma shader_feature _USE_SOFT_SHADOW

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float4 ndcPos : TEXCOORD2;
            };

            #define MAX_PER_OBJECT_SHADOW_COUNT 16

            sampler2D _PerObjectShadowTex;
            SAMPLER(sampler_PerObjectShadowTex);
            float4 _PerObjectShadowTex_ST;
            sampler2D _PerObjectPenumbraMask;
            float4 _PerObjectPenumbraMask_ST;

            float4x4 _ShadowCoordMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float4x4 _InverseProjMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float4x4 _ViewprojectMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float _BoundsRanges[MAX_PER_OBJECT_SHADOW_COUNT];
            float _LightWidths[MAX_PER_OBJECT_SHADOW_COUNT];
            float _ShadowBiases[MAX_PER_OBJECT_SHADOW_COUNT];
            float _ShadowSoftWeights[MAX_PER_OBJECT_SHADOW_COUNT];
            float4 _ShadowPaddingSizes[MAX_PER_OBJECT_SHADOW_COUNT];
            int _PerObjectShadowCount;

            #define POISSON_DISK_SIZE 16
            static float2 poissonDisk[16] = {
                float2(-0.94201624, -0.39906216),
                float2(0.94558609, -0.76890725),
                float2(-0.094184101, -0.92938870),
                float2(0.34495938, 0.29387760),
                float2(-0.91588581, 0.45771432),
                float2(-0.81544232, -0.87912464),
                float2(-0.38277543, 0.27676845),
                float2(0.97484398, 0.75648379),
                float2(0.44323325, -0.97511554),
                float2(0.53742981, -0.47373420),
                float2(-0.26496911, -0.41893023),
                float2(0.79197514, 0.19090188),
                float2(-0.24188840, 0.99706507),
                float2(-0.81409955, 0.91437590),
                float2(0.19984126, 0.78641367),
                float2(0.14383161, -0.14100790)
            };

            float RandomVal(const float4 seed4)
            {
                float dot_product = dot(seed4, float4(12.9898, 78.233, 45.164, 94.673));
                return frac(sin(dot_product) * 43758.5453);
            }

            float RandomVal(const float2 seed2)
            {
                return frac(sin(dot(seed2, float2(12.9898f, 78.233f))) * 43758.5453123);
            }

            float2 RotatedOffset(const float3 seed3, int k) {
                float angle = 6.2831 * RandomVal(float4(seed3 * _ScreenParams.xyy, k));
                float2x2 rot = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
                int index = POISSON_DISK_SIZE * RandomVal(float4(seed3, k)) % POISSON_DISK_SIZE;
                return mul(rot, poissonDisk[index]);
            }

            float2 AverageBlockerDepth(float2 shadowUV, sampler2D _shadowtex, float d_shadingPoint, float searchWidth, int objIndex, float distanceToCamera)
            {
                float pixelSize = 1.0 / max(_ScreenParams.x, _ScreenParams.y);
                float2 uv = shadowUV;
                float step = 3.0;
                float d_average = 0.0;
                float count = 0.0005;   // ·ÀÖ¹ ¡Â 0
                for (int i = 0; i < POISSON_DISK_SIZE; i++) {
                    float2 offset = poissonDisk[i] / POISSON_DISK_SIZE * searchWidth;
                    float shadowMapDepth = tex2D(_shadowtex, shadowUV + offset).r;
                    #if defined(UNITY_REVERSED_Z)
                        if (shadowMapDepth > d_shadingPoint)
                    #else
                        if (shadowMapDepth < d_shadingPoint)
                    #endif
                    {
                        count += 1;
                        d_average += shadowMapDepth;
                    }
                }
                return float2(d_average/count, count);
            }

            float3 reconstructPositionWS(float2 posSS)
            {
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(posSS);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(posSS));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(posSS, depth, UNITY_MATRIX_I_VP);
                return worldPos;
            }


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.ndcPos = GetVertexPositionInputs(v.vertex).positionNDC;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float penumbraMask = tex2D(_PerObjectPenumbraMask, i.uv).r;
                float2 posSS = i.ndcPos.xy / i.ndcPos.w;

                float3 objectPositionWS = reconstructPositionWS(posSS);
                float distanceToCamera = length(_WorldSpaceCameraPos - objectPositionWS);
                float pixelSize = 1.0 / max(_ScreenParams.x, _ScreenParams.y);
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);

                float perObjDepth = 0;
                int tileRowCount = ceil(sqrt(_PerObjectShadowCount));
                float tileSize = 1.0 / tileRowCount;
                float lightSpaceDepth = 0;
                float zDiff = 0;
                float3 positionLS;
                float2 shadowCoord;
                float4 objectPositionLS;
                float2 shadowUV;
                float perObjDepthCombined = 0;
                float sceneDepthCombined = 0;
                float2 blocker;
                for (int j = 0; j < _PerObjectShadowCount; j++) {

                    objectPositionLS = mul(_ViewprojectMatricies[j], float4(objectPositionWS, 1));
                    objectPositionLS.z = objectPositionLS.z * 0.5 + 0.5;
                    objectPositionLS.xy = objectPositionLS.xy * 0.5 + 0.5;

                    float3 shadowCoordLS = mul(_ShadowCoordMatricies[j], objectPositionLS);

                    shadowCoord = shadowCoordLS.xy;

                    float2 index = float2(j % tileRowCount, j / tileRowCount);
                    float2 minPoint = index * tileSize;
                    float2 maxPoint = minPoint + float2(tileSize, tileSize);
                    shadowUV = clamp(shadowCoord.xy, minPoint, maxPoint);


                    #if UNITY_REVERSED_Z
                        perObjDepth = tex2D(_PerObjectShadowTex, shadowUV).r;
                    #else
                        perObjDepth = tex2D(_PerObjectShadowTex, shadowUV).r;
                        perObjDepth = lerp(-0.5, 1, perObjDepth);
                    #endif
                    
                    float shadowAtten = 0;
                    #if _USE_SOFT_SHADOW
                        if (penumbraMask * (perObjDepth > objectPositionLS.z + _ShadowBiases[j]) > 0)
                        {
                            blocker = AverageBlockerDepth(shadowUV, _PerObjectShadowTex, shadowCoordLS.z, 0.01 * max(_BoundsRanges[j], _LightWidths[j]), j, distanceToCamera);

                            float zReceiverNDC = objectPositionLS.z * 2 - 1;
                            float zBlockerNDC = blocker.x * 2.0 - 1.0;

                            float w = _LightWidths[j] * (blocker.x - objectPositionLS.z) * 10 / blocker.x;
                            float radius = w / _BoundsRanges[j];

                            float2 paddingSize = _ShadowPaddingSizes[j].xy;

                            //PCF
                            UNITY_UNROLL
                            for (int k = 0; k < POISSON_DISK_SIZE; k++)
                            {
                                float2 rotatedOffset = RotatedOffset(i.ndcPos.xyy, k);
                                float2 offset = rotatedOffset * pixelSize * radius * (_ShadowSoftWeights[j] + 1) * 20; //weight offset
                                float2 sample_shadow_coord = shadowUV + offset;
                                float shadowSample = tex2D(_PerObjectShadowTex, sample_shadow_coord);

                                if (sample_shadow_coord.x > 1 - paddingSize.x 
                                    || sample_shadow_coord.x < 0 + paddingSize.x 
                                    || sample_shadow_coord.y > 1 - paddingSize.y 
                                    || sample_shadow_coord.y < 0 + paddingSize.y) {
                                    shadowSample = 0;
                                }
                                shadowAtten += (shadowSample) / POISSON_DISK_SIZE;
                            }
                        }
                        else 
                        {
                            shadowAtten = tex2D(_PerObjectShadowTex, shadowUV);
                            shadowAtten = (shadowAtten > objectPositionLS.z + _ShadowBiases[j]) ? 1 : 0;
                        }
                    #else
                        shadowAtten = tex2D(_PerObjectShadowTex, shadowUV);
                        shadowAtten = (shadowAtten > objectPositionLS.z + _ShadowBiases[j]) ? 1 : 0;
                    #endif

                    lightSpaceDepth = max(lightSpaceDepth, shadowAtten);
                }

                return float4(1 - lightSpaceDepth, 0, 0, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "PerObjectSelfShadowComposite"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_PER_OBJECT_SHADOW_COUNT 16

            float3 reconstructPositionWS(float2 posSS)
            {
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(posSS);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(posSS));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(posSS, depth, UNITY_MATRIX_I_VP);
                return worldPos;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float4 ndcPos : TEXCOORD2;
            };

            TEXTURE2D(_PerObjectSelfShadowTex);
            SAMPLER(sampler_PerObjectSelfShadowTex);
            float4 _PerObjectShadowTex_ST;

            float4x4 _ShadowCoordMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float4x4 _InverseProjMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float4x4 _ViewprojectMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float _BoundsRanges[MAX_PER_OBJECT_SHADOW_COUNT];
            int _PerObjectShadowCount;


            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.ndcPos = GetVertexPositionInputs(v.vertex).positionNDC;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 posSS = i.ndcPos.xy / i.ndcPos.w;

                float3 objectPositionWS = reconstructPositionWS(posSS);

                float perObjDepth = 0;
                int tileRowCount = ceil(sqrt(_PerObjectShadowCount));
                float tileSize = 1.0 / tileRowCount;
                float lightSpaceDepth = 1;
                float zDiff = 0;
                float3 positionLS;
                float2 shadowCoord;
                float4 objectPositionLS;
                float2 shadowUV;
                float perObjDepthCombined = 0;
                float sceneDepthCombined = 0;
                for (int j = 0; j < _PerObjectShadowCount; j++) {

                    objectPositionLS = mul(_ViewprojectMatricies[j], float4(objectPositionWS, 1));
                    objectPositionLS.z = objectPositionLS.z * 0.5 + 0.5;
                    objectPositionLS.xy = objectPositionLS.xy * 0.5 + 0.5;

                    shadowCoord = mul(_ShadowCoordMatricies[j], objectPositionLS).xy;

                    float2 index = float2(j % tileRowCount, j / tileRowCount);
                    float2 minPoint = index * tileSize;
                    float2 maxPoint = minPoint + float2(tileSize, tileSize);
                    shadowUV = clamp(shadowCoord.xy, minPoint, maxPoint);


                    #if UNITY_REVERSED_Z
                        perObjDepth = SAMPLE_TEXTURE2D(_PerObjectSelfShadowTex, sampler_PerObjectSelfShadowTex, shadowUV).r;
                    #else
                        perObjDepth = SAMPLE_TEXTURE2D(_PerObjectSelfShadowTex, sampler_PerObjectSelfShadowTex, shadowUV).r;
                        perObjDepth = lerp(-0.5, 1, perObjDepth);
                    #endif


                    perObjDepthCombined = max(perObjDepthCombined, perObjDepth);

                    sceneDepthCombined = max(sceneDepthCombined, objectPositionLS.z);

                    zDiff = (perObjDepth - 0.01f > objectPositionLS.z) ? 0.1 : 1;

                    lightSpaceDepth = min(lightSpaceDepth, zDiff);
                }
                return lightSpaceDepth;
            }
            ENDHLSL
        }

        Pass
        {
            name "PenumbraMask"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #define MAX_PER_OBJECT_SHADOW_COUNT 16
            #define EPS 0.00001

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
                float4 ndcPos : TEXCOORD2;
            };

            float4x4 _ShadowCoordMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float4x4 _InverseProjMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float4x4 _ViewprojectMatricies[MAX_PER_OBJECT_SHADOW_COUNT];
            float _ShadowSoftWeights[MAX_PER_OBJECT_SHADOW_COUNT];
            float _BoundsRanges[MAX_PER_OBJECT_SHADOW_COUNT];
            int _PerObjectShadowCount;

            sampler2D _PerObjectShadowTex;
            float4 _PerObjectShadowTex_ST;

            static float2 offset[4] = { float2(-1, 1), float2(1, 1), float2(-1, -1), float2(1, -1) };

            float3 reconstructPositionWS(float2 posSS)
            {
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(posSS);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(posSS));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(posSS, depth, UNITY_MATRIX_I_VP);
                return worldPos;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.ndcPos = GetVertexPositionInputs(v.vertex).positionNDC;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 posSS = i.ndcPos.xy / i.ndcPos.w;
                float3 objectPositionWS = reconstructPositionWS(posSS);
                float distanceToCamera = length(_WorldSpaceCameraPos - objectPositionWS);
                float2 pixelSize = float2(4.0 / _ScreenParams.x, 4.0 / _ScreenParams.y);
                float4 objectPositionLS;
                float2 shadowCoord;
                float2 shadowUV;
                int tileRowCount = ceil(sqrt(_PerObjectShadowCount));
                float tileSize = 1.0 / tileRowCount;
                float perObjDepth;
                float shadowAtten;
                float penumbraMask = 0;
                float finalShadowAtten;
                for (int j = 0; j < _PerObjectShadowCount; j++) {
                    shadowAtten = 0;
                    for (int k = 0; k < 4; k++) {
                        float2 posSS = i.ndcPos.xy / i.ndcPos.w;
                        float2 depthUV = posSS + offset[k] * pixelSize * (1.0 / (distanceToCamera * distanceToCamera * 0.02 + EPS));
                        float3 objectPositionWS = reconstructPositionWS(depthUV);
                        
                        objectPositionLS = mul(_ViewprojectMatricies[j], float4(objectPositionWS, 1));
                        objectPositionLS.z = objectPositionLS.z * 0.5 + 0.5;
                        objectPositionLS.xy = objectPositionLS.xy * 0.5 + 0.5;

                        float3 shadowCoordLS = mul(_ShadowCoordMatricies[j], objectPositionLS);

                        shadowCoord = shadowCoordLS.xy;

                        float2 index = float2(j % tileRowCount, j / tileRowCount);
                        float2 minPoint = index * tileSize;
                        float2 maxPoint = minPoint + float2(tileSize, tileSize);
                        shadowUV = clamp(shadowCoord.xy, minPoint, maxPoint);


                        #if UNITY_REVERSED_Z
                            perObjDepth = tex2D(_PerObjectShadowTex, shadowUV).r;
                        #else
                            perObjDepth = tex2D(_PerObjectShadowTex, shadowUV).r;
                            perObjDepth = lerp(-0.5, 1, perObjDepth);
                        #endif

                            shadowAtten += 0.25 * ((perObjDepth - 0.01f > objectPositionLS.z) ? 1 : 0) * (objectPositionLS.z > 0);
                    }
                    finalShadowAtten = 1 - (shadowAtten > 1 - EPS || shadowAtten < EPS);
                    penumbraMask = max(penumbraMask, finalShadowAtten);
                }

                return penumbraMask;
            }


            ENDHLSL
        }

        Pass
        {
            name "GaussianX"
            HLSLPROGRAM
            #pragma vertex vert_GaussianX
            #pragma fragment frag_GaussianX

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 ndcPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _PerObjectSceneShadowMap;
            float4 _PerObjectSceneShadowMap_ST;

            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            float3 reconstructPositionWS(float2 posSS)
            {
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(posSS);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(posSS));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(posSS, depth, UNITY_MATRIX_I_VP);
                return worldPos;
            }

            v2f vert_GaussianX(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.ndcPos = GetVertexPositionInputs(v.vertex).positionNDC;
                o.uv = v.uv;
                return o;
            }

            float4 frag_GaussianX(v2f i) : SV_Target
            {
                float4 accumResult = tex2D(_PerObjectSceneShadowMap, i.uv) * weights[0];
                float2 texOffset = 1.0 / _ScreenParams.xy;

                float2 posSS = i.ndcPos.xy / i.ndcPos.w;
                float3 objectPositionWS = reconstructPositionWS(posSS);

                for (int index = 1; index < 5; index++) {
                    float2 offset = float2(texOffset.x * index, 0.0);
                    float2 uvSample = posSS + offset;
                    float3 objectPositionWSSample = reconstructPositionWS(uvSample);
                    float weightWpos = 1 / (1 + distance(objectPositionWSSample, objectPositionWS));
                    accumResult.rgb += tex2D(_PerObjectSceneShadowMap, i.uv + offset).rgb * weights[index];
                    accumResult.rgb += tex2D(_PerObjectSceneShadowMap, i.uv - offset).rgb * weights[index];
                    
                }
                
                return accumResult;
            }
            ENDHLSL
        }

        Pass
        {
            name "GaussianY"
            HLSLPROGRAM
            #pragma vertex vert_GaussianY
            #pragma fragment frag_GaussianY

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 ndcPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _PerObjectSceneShadowMap;
            float4 _PerObjectSceneShadowMap_ST;

            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            float3 reconstructPositionWS(float2 posSS)
            {
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(posSS);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(posSS));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(posSS, depth, UNITY_MATRIX_I_VP);
                return worldPos;
            }

            v2f vert_GaussianY(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.ndcPos = GetVertexPositionInputs(v.vertex).positionNDC;
                o.uv = v.uv;
                return o;
            }

            float4 frag_GaussianY(v2f i) : SV_Target
            {
                float4 accumResult = tex2D(_PerObjectSceneShadowMap, i.uv) * weights[0];
                float2 texOffset = 1.0 / _ScreenParams.xy;

                float2 posSS = i.ndcPos.xy / i.ndcPos.w;
                float3 objectPositionWS = reconstructPositionWS(posSS);

                for (int index = 1; index < 5; index++) {
                    float2 offset = float2(0.0, texOffset.y * index);
                    float2 uvSample = posSS + offset;
                    float3 objectPositionWSSample = reconstructPositionWS(uvSample);
                    float weightWpos = 1 / (1 + distance(objectPositionWSSample, objectPositionWS));
                    accumResult.rgb += tex2D(_PerObjectSceneShadowMap, i.uv + offset).rgb * weights[index];
                    accumResult.rgb += tex2D(_PerObjectSceneShadowMap, i.uv - offset).rgb * weights[index];
                    
                }
                
                return accumResult;
            }
            ENDHLSL
        }

        Pass
        {
            name "PenumbraGaussianX"
            HLSLPROGRAM
            #pragma vertex vert_PenumbraGaussianX
            #pragma fragment frag_PenumbraGaussianX

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 ndcPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _PerObjectPenumbraMask;
            float4 _PerObjectPenumbraMask_ST;

            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            float3 reconstructPositionWS(float2 posSS)
            {
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(posSS);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(posSS));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(posSS, depth, UNITY_MATRIX_I_VP);
                return worldPos;
            }

            v2f vert_PenumbraGaussianX(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.ndcPos = GetVertexPositionInputs(v.vertex).positionNDC;
                o.uv = v.uv;
                return o;
            }

            float4 frag_PenumbraGaussianX(v2f i) : SV_Target
            {
                float4 accumResult = tex2D(_PerObjectPenumbraMask, i.uv) * weights[0];
                float2 texOffset = 1.0 / _ScreenParams.xy;

                float2 posSS = i.ndcPos.xy / i.ndcPos.w;
                float3 objectPositionWS = reconstructPositionWS(posSS);

                for (int index = 1; index < 5; index++) {
                    float2 offset = float2(texOffset.x * index, 0.0);
                    accumResult.rgb += tex2D(_PerObjectPenumbraMask, i.uv + offset).rgb * weights[index];
                    accumResult.rgb += tex2D(_PerObjectPenumbraMask, i.uv - offset).rgb * weights[index];
                    
                }
                
                return accumResult;
            }
            ENDHLSL
        }

        Pass
        {
            name "PenumbraGaussianY"
            HLSLPROGRAM
            #pragma vertex vert_PenumbraGaussianY
            #pragma fragment frag_PenumbraGaussianY

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 ndcPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _PerObjectPenumbraMask;
            float4 _PerObjectPenumbraMask_ST;

            static const float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            float3 reconstructPositionWS(float2 posSS)
            {
                #if UNITY_REVERSED_Z
                    float depth = SampleSceneDepth(posSS);
                #else
                    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(posSS));
                #endif

                float3 worldPos = ComputeWorldSpacePosition(posSS, depth, UNITY_MATRIX_I_VP);
                return worldPos;
            }

            v2f vert_PenumbraGaussianY(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.ndcPos = GetVertexPositionInputs(v.vertex).positionNDC;
                o.uv = v.uv;
                return o;
            }

            float4 frag_PenumbraGaussianY(v2f i) : SV_Target
            {
                float4 accumResult = tex2D(_PerObjectPenumbraMask, i.uv) * weights[0];
                float2 texOffset = 1.0 / _ScreenParams.xy;

                float2 posSS = i.ndcPos.xy / i.ndcPos.w;
                float3 objectPositionWS = reconstructPositionWS(posSS);

                for (int index = 1; index < 5; index++) {
                    float2 offset = float2(0.0, texOffset.y * index);
                    accumResult.rgb += tex2D(_PerObjectPenumbraMask, i.uv + offset).rgb * weights[index];
                    accumResult.rgb += tex2D(_PerObjectPenumbraMask, i.uv - offset).rgb * weights[index];
                    
                }
                
                return accumResult;
            }
            ENDHLSL
        }
    }
}
