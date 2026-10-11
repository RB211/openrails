// COPYRIGHT 2010, 2011, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

////////////////////////////////////////////////////////////////////////////////
//                     S H A D O W   M A P   S H A D E R                      //
////////////////////////////////////////////////////////////////////////////////

////////////////////    G L O B A L   V A L U E S    ///////////////////////////

#include "ShaderModels.fxh"

float4x4 WorldViewProjection;  // model -> world -> view -> projection
float3   SideVector;
float    ImageBlurStep;  // = 1 / shadow map texture width and height
Texture2D  ImageTexture : register(t0);
// The blur passes read the shadow map through a different sampler. Vulkan (via MonoGame) needs each
// sampler paired with its own texture, so the shadow map has its own parameter.
Texture2D  BlurTexture : register(t1);

SamplerState ImageSampler : register(s0) = sampler_state
{
	Texture = (ImageTexture);
	MagFilter = Linear;
	MinFilter = Anisotropic;
	MipFilter = Linear;
	MaxAnisotropy = 16;
};

SamplerState ShadowMapSampler : register(s1) = sampler_state
{
	Texture = (BlurTexture);
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Point;
};

////////////////////    V E R T E X   I N P U T S    ///////////////////////////

struct VERTEX_INPUT
{
	float4 Position : POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal   : NORMAL;
	// Instance transform rows, declared separately (not as one float4x4 input) so that every row is
	// reflected as its own vertex attribute; MonoGame's Vulkan backend binds one location per attribute.
	float4 Instance0 : TEXCOORD1;
	float4 Instance1 : TEXCOORD2;
	float4 Instance2 : TEXCOORD3;
	float4 Instance3 : TEXCOORD4;
};

struct VERTEX_INPUT_FOREST
{
	float4 Position : POSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal   : NORMAL;
};

struct VERTEX_INPUT_BLUR
{
	float4 Position : POSITION;
	float2 TexCoord : TEXCOORD0;
};

////////////////////    V E R T E X   O U T P U T S    /////////////////////////

struct VERTEX_OUTPUT
{
	float4 Position       : SV_POSITION;
	float3 TexCoord_Depth : TEXCOORD0;
};

struct VERTEX_OUTPUT_BLUR
{
	float4 Position     : SV_POSITION;
	float2 SampleCentre : TEXCOORD0;
	float2 Sample_01    : TEXCOORD1;
	float2 Sample_23    : TEXCOORD2;
};

////////////////////    V E R T E X   S H A D E R S    /////////////////////////

VERTEX_OUTPUT VSShadowMap(in VERTEX_INPUT In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;

	if (determinant(float4x4(In.Instance0, In.Instance1, In.Instance2, In.Instance3)) != 0) {
		In.Position = mul(In.Position, transpose(float4x4(In.Instance0, In.Instance1, In.Instance2, In.Instance3)));
		In.Normal = mul(In.Normal, (float3x3)transpose(float4x4(In.Instance0, In.Instance1, In.Instance2, In.Instance3)));
	}

	Out.Position = mul(In.Position, WorldViewProjection);
	Out.TexCoord_Depth.xy = In.TexCoord;
	Out.TexCoord_Depth.z = Out.Position.z;

	return Out;
}

VERTEX_OUTPUT VSShadowMapForest(in VERTEX_INPUT_FOREST In)
{
	VERTEX_OUTPUT Out = (VERTEX_OUTPUT)0;

	// Start with the three vectors of the view.
	float3 upVector = float3(0, -1, 0);

	// Move the vertex left/right/up/down based on the normal values (tree size).
	float3 newPosition = In.Position.xyz;
	newPosition += (In.TexCoord.x - 0.5f) * SideVector * In.Normal.x;
	newPosition += (In.TexCoord.y - 1.0f) * upVector * In.Normal.y;
	In.Position = float4(newPosition, 1);

	// Project vertex with fixed w=1 and normal=eye.
	Out.Position = mul(In.Position, WorldViewProjection);
	Out.TexCoord_Depth.xy = In.TexCoord;
	Out.TexCoord_Depth.z = Out.Position.z;

	return Out;
}

VERTEX_OUTPUT_BLUR VSShadowMapHorzBlur(in VERTEX_INPUT_BLUR In)
{
	VERTEX_OUTPUT_BLUR Out;
	
	float2 offsetTexCoord = In.TexCoord + float2(0.5, 0.5);

	Out.Position = mul(In.Position, WorldViewProjection);
	Out.SampleCentre = offsetTexCoord * ImageBlurStep;
	Out.Sample_01 = (offsetTexCoord - float2(1.5, 0)) * ImageBlurStep;
	Out.Sample_23 = (offsetTexCoord + float2(1.5, 0)) * ImageBlurStep;

	return Out;
}

VERTEX_OUTPUT_BLUR VSShadowMapVertBlur(in VERTEX_INPUT_BLUR In)
{
	VERTEX_OUTPUT_BLUR Out;
	
	float2 offsetTexCoord = In.TexCoord + float2(0.5, 0.5);

	Out.Position = mul(In.Position, WorldViewProjection);
	Out.SampleCentre = offsetTexCoord * ImageBlurStep;
	Out.Sample_01 = (offsetTexCoord - float2(0, 1.5)) * ImageBlurStep;
	Out.Sample_23 = (offsetTexCoord + float2(0, 1.5)) * ImageBlurStep;

	return Out;
}

////////////////////    P I X E L   S H A D E R S    ///////////////////////////

float4 PSShadowMap(in VERTEX_OUTPUT In) : SV_TARGET0
{
	float alpha = ImageTexture.Sample(ImageSampler, In.TexCoord_Depth.xy).a;
	
	if(alpha < 0.25)
		discard;
	
	return float4(In.TexCoord_Depth.z, In.TexCoord_Depth.z * In.TexCoord_Depth.z, 0, 0);
}

float4 PSShadowMapBlocker() : SV_TARGET0
{
	return 0;
}

float4 PSShadowMapBlur(in VERTEX_OUTPUT_BLUR In) : SV_TARGET0
{
	float2 centreTap =	BlurTexture.Sample(ShadowMapSampler, In.SampleCentre).rg	* 0.4430448;
	float2 tap01 =		BlurTexture.Sample(ShadowMapSampler, In.Sample_01).rg * 0.2784776;
	float2 tap23 =		BlurTexture.Sample(ShadowMapSampler, In.Sample_23).rg * 0.2784776;
		
	return float4(tap01 + centreTap + tap23, 0, 0);
}

////////////////////    T E C H N I Q U E S    /////////////////////////////////

technique ShadowMap {
	pass Pass_0 {
		VertexShader = compile VS_LOW VSShadowMap();
		PixelShader = compile PS_LOW PSShadowMap();
	}
}

technique ShadowMapForest {
	pass Pass_0 {
		VertexShader = compile VS_LOW VSShadowMapForest();
		PixelShader = compile PS_LOW PSShadowMap();
	}
}

technique ShadowMapBlocker {
	pass Pass_0 {
		VertexShader = compile VS_LOW VSShadowMap();
		PixelShader = compile PS_LOW PSShadowMapBlocker();
	}
}

technique ShadowMapBlur {
	pass Blur_X {
		VertexShader = compile VS_LOW VSShadowMapHorzBlur();
		PixelShader = compile PS_LOW PSShadowMapBlur();
	}
	pass Blur_Y {
		VertexShader = compile VS_LOW VSShadowMapVertBlur();
		PixelShader = compile PS_LOW PSShadowMapBlur();
	}
}
