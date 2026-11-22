// Made with Amplify Shader Editor v1.9.7.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Custom Render Texture (RGB Split)"
{
    Properties
    {
		_Texture("Texture", CUBE) = "white" {}
		_RGBSplit("RGB Split", Range( -0.2 , 0.2)) = 0
		_RGBSplitScaleY("RGB Split Scale Y", Range( 0 , 1)) = 1
		_RGBSplitSteps("RGB Split Steps", Range( 1 , 8)) = 1

    }

	SubShader
	{
		LOD 0

		
		CGINCLUDE
		#pragma target 3.0
		ENDCG
		Blend Off
		AlphaToMask Off
		Cull Back
		ColorMask RGBA
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		
		
        Pass
        {
			Name "Custom RT Update"
            CGPROGRAM
            #define ASE_VERSION 19701

            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex ASECustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0
			#include "../../Glass_CustomRT.hlsl"


			struct ase_appdata_customrendertexture
			{
				uint vertexID : SV_VertexID;
				
			};

			struct ase_v2f_customrendertexture
			{
				float4 vertex           : SV_POSITION;
				float3 localTexcoord    : TEXCOORD0;    // Texcoord local to the update zone (== globalTexcoord if no partial update zone is specified)
				float3 globalTexcoord   : TEXCOORD1;    // Texcoord relative to the complete custom texture
				uint primitiveID        : TEXCOORD2;    // Index of the update zone (correspond to the index in the updateZones of the Custom Texture)
				float3 direction        : TEXCOORD3;    // For cube textures, direction of the pixel being rendered in the cubemap
				
			};

			uniform samplerCUBE _Texture;
			uniform float _RGBSplit;
			uniform float _RGBSplitScaleY;
			uniform float _RGBSplitSteps;


			ase_v2f_customrendertexture ASECustomRenderTextureVertexShader(ase_appdata_customrendertexture IN  )
			{
				ase_v2f_customrendertexture OUT;
				
			#if UNITY_UV_STARTS_AT_TOP
				const float2 vertexPositions[6] =
				{
					{ -1.0f,  1.0f },
					{ -1.0f, -1.0f },
					{  1.0f, -1.0f },
					{  1.0f,  1.0f },
					{ -1.0f,  1.0f },
					{  1.0f, -1.0f }
				};

				const float2 texCoords[6] =
				{
					{ 0.0f, 0.0f },
					{ 0.0f, 1.0f },
					{ 1.0f, 1.0f },
					{ 1.0f, 0.0f },
					{ 0.0f, 0.0f },
					{ 1.0f, 1.0f }
				};
			#else
				const float2 vertexPositions[6] =
				{
					{  1.0f,  1.0f },
					{ -1.0f, -1.0f },
					{ -1.0f,  1.0f },
					{ -1.0f, -1.0f },
					{  1.0f,  1.0f },
					{  1.0f, -1.0f }
				};

				const float2 texCoords[6] =
				{
					{ 1.0f, 1.0f },
					{ 0.0f, 0.0f },
					{ 0.0f, 1.0f },
					{ 0.0f, 0.0f },
					{ 1.0f, 1.0f },
					{ 1.0f, 0.0f }
				};
			#endif

				uint primitiveID = IN.vertexID / 6;
				uint vertexID = IN.vertexID % 6;
				float3 updateZoneCenter = CustomRenderTextureCenters[primitiveID].xyz;
				float3 updateZoneSize = CustomRenderTextureSizesAndRotations[primitiveID].xyz;
				float rotation = CustomRenderTextureSizesAndRotations[primitiveID].w * UNITY_PI / 180.0f;

			#if !UNITY_UV_STARTS_AT_TOP
				rotation = -rotation;
			#endif

				// Normalize rect if needed
				if (CustomRenderTextureUpdateSpace > 0.0) // Pixel space
				{
					// Normalize xy because we need it in clip space.
					updateZoneCenter.xy /= _CustomRenderTextureInfo.xy;
					updateZoneSize.xy /= _CustomRenderTextureInfo.xy;
				}
				else // normalized space
				{
					// Un-normalize depth because we need actual slice index for culling
					updateZoneCenter.z *= _CustomRenderTextureInfo.z;
					updateZoneSize.z *= _CustomRenderTextureInfo.z;
				}

				// Compute rotation

				// Compute quad vertex position
				float2 clipSpaceCenter = updateZoneCenter.xy * 2.0 - 1.0;
				float2 pos = vertexPositions[vertexID] * updateZoneSize.xy;
				pos = CustomRenderTextureRotate2D(pos, rotation);
				pos.x += clipSpaceCenter.x;
			#if UNITY_UV_STARTS_AT_TOP
				pos.y += clipSpaceCenter.y;
			#else
				pos.y -= clipSpaceCenter.y;
			#endif

				// For 3D texture, cull quads outside of the update zone
				// This is neeeded in additional to the preliminary minSlice/maxSlice done on the CPU because update zones can be disjointed.
				// ie: slices [1..5] and [10..15] for two differents zones so we need to cull out slices 0 and [6..9]
				if (CustomRenderTextureIs3D > 0.0)
				{
					int minSlice = (int)(updateZoneCenter.z - updateZoneSize.z * 0.5);
					int maxSlice = minSlice + (int)updateZoneSize.z;
					if (_CustomRenderTexture3DSlice < minSlice || _CustomRenderTexture3DSlice >= maxSlice)
					{
						pos.xy = float2(1000.0, 1000.0); // Vertex outside of ncs
					}
				}

				OUT.vertex = float4(pos, 0.0, 1.0);
				OUT.primitiveID = asuint(CustomRenderTexturePrimitiveIDs[primitiveID]);
				OUT.localTexcoord = float3(texCoords[vertexID], CustomRenderTexture3DTexcoordW);
				OUT.globalTexcoord = float3(pos.xy * 0.5 + 0.5, CustomRenderTexture3DTexcoordW);
			#if UNITY_UV_STARTS_AT_TOP
				OUT.globalTexcoord.y = 1.0 - OUT.globalTexcoord.y;
			#endif
				OUT.direction = CustomRenderTextureComputeCubeDirection(OUT.globalTexcoord.xy);

				return OUT;
			}

            float4 frag(ase_v2f_customrendertexture IN ) : COLOR
            {
				float4 finalColor;
				samplerCUBE tex59 = _Texture;
				float3 Cubemap_Normal31 = IN.direction;
				float3 normal59 = Cubemap_Normal31;
				float2 appendResult67 = (float2(_RGBSplit , ( _RGBSplit * _RGBSplitScaleY )));
				float2 rgbSplit59 = appendResult67;
				int rgbSplitSteps59 = (int)_RGBSplitSteps;
				float3 localRGBSplitCube59 = RGBSplitCube( tex59 , normal59 , rgbSplit59 , rgbSplitSteps59 );
				
                finalColor = float4( localRGBSplitCube59 , 0.0 );
				return finalColor;
            }
            ENDCG
		}
    }
	
	CustomEditor "ASEMaterialInspector"
	Fallback Off
}
/*ASEBEGIN
Version=19701
Node;AmplifyShaderEditor.TexturePropertyNode;23;-1536,-128;Inherit;True;Property;_Texture;Texture;0;0;Create;True;0;0;0;False;0;False;None;None;False;white;LockedToCube;Cube;-1;0;2;SAMPLERCUBE;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.TexCoordVertexDataNode;30;-1536,128;Inherit;False;3;3;0;5;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;39;-2048,640;Inherit;False;Property;_RGBSplit;RGB Split;1;0;Create;True;0;0;0;False;0;False;0;0;-0.2;0.2;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;65;-2048,720;Inherit;False;Property;_RGBSplitScaleY;RGB Split Scale Y;2;0;Create;True;0;0;0;False;0;False;1;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;25;-1280,-128;Inherit;False;Texture;-1;True;1;0;SAMPLERCUBE;;False;1;SAMPLERCUBE;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;31;-1280,128;Inherit;False;Cubemap Normal;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;66;-1664,768;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;67;-1408,640;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;26;-1536,384;Inherit;False;25;Texture;1;0;OBJECT;;False;1;SAMPLERCUBE;0
Node;AmplifyShaderEditor.GetLocalVarNode;32;-1536,512;Inherit;False;31;Cubemap Normal;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;64;-1408,768;Inherit;False;Property;_RGBSplitSteps;RGB Split Steps;3;0;Create;True;0;0;0;False;0;False;1;1;1;8;0;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;59;-1024,384;Inherit;False; ;3;File;4;True;tex;SAMPLERCUBE;;In;;Inherit;False;True;normal;FLOAT3;0,0,0;In;;Inherit;False;True;rgbSplit;FLOAT2;0,0;In;;Inherit;False;True;rgbSplitSteps;INT;0;In;;Inherit;False;RGBSplitCube;False;False;0;f30f91b2a792677448273889a7e48069;False;4;0;SAMPLERCUBE;;False;1;FLOAT3;0,0,0;False;2;FLOAT2;0,0;False;3;INT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;-768,384;Float;False;True;-1;2;ASEMaterialInspector;0;2;Custom Render Texture (RGB Split);32120270d1b3a8746af2aca8bc749736;True;Custom RT Update;0;0;Custom RT Update;1;False;True;0;1;False;;0;False;;0;1;False;;0;False;;True;0;False;;0;False;;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;True;2;False;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;0;;0;0;Standard;0;0;1;True;False;;False;0
WireConnection;25;0;23;0
WireConnection;31;0;30;0
WireConnection;66;0;39;0
WireConnection;66;1;65;0
WireConnection;67;0;39;0
WireConnection;67;1;66;0
WireConnection;59;0;26;0
WireConnection;59;1;32;0
WireConnection;59;2;67;0
WireConnection;59;3;64;0
WireConnection;0;0;59;0
ASEEND*/
//CHKSM=A209DA7102F47677CCC09DEED19F6C8DAD96E966