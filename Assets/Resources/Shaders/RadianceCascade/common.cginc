#define TWO_PI 6.28318530718

float2 Direction2D(float a) {
	float angle = a * TWO_PI;
	return float2(cos(angle), sin(angle));
}

void MergeRays(inout float4 near, in float4 far) {
	near.xyz += near.w * far.xyz;
	near.w *= far.w;
}

void RayTraceStep(
	in Texture2D<float4> tex,
	in SamplerState samplerState,
	inout float4 radiance,
	inout float2 uv,
	in float2 step
) {
	float4 pointRadiance = tex.SampleLevel(samplerState, uv, 0);

	pointRadiance.w = 1.0 - pointRadiance.w;

	MergeRays(radiance, pointRadiance);

	uv += step;
}

float4 RayTrace(
	in Texture2D<float4> tex,
	in SamplerState samplerState,
	float2 uv,
	float2 step,
	uint quarterSampleCount
) {
	float4 radiance = float4(0, 0, 0, 1);

	for (uint i = 0; i != quarterSampleCount; ++i) {
		RayTraceStep(tex, samplerState, radiance, uv, step);
		RayTraceStep(tex, samplerState, radiance, uv, step);
		RayTraceStep(tex, samplerState, radiance, uv, step);
		RayTraceStep(tex, samplerState, radiance, uv, step);
	}

	return radiance;
}

float4 BilinearInterpolation(
	in float4 radianceLU,
	in float4 radianceRU,
	in float4 radianceLL,
	in float4 radianceRL,
	in float2 uv
) {
	float4 u4 = uv.x;
	float4 v4 = uv.y;
	return lerp(
		lerp(radianceLL, radianceRL, u4),
		lerp(radianceLU, radianceRU, u4),
		v4
	);
}

uint2 TexSize(in Texture2D<float4> tex2D) {
	uint2 size;
	tex2D.GetDimensions(size.x, size.y);
	return size;
}

uint2 TexSize(in RWTexture2D<float4> tex2D) {
	uint2 size;
	tex2D.GetDimensions(size.x, size.y);
	return size;
}

uint3 TexSize(in RWTexture3D<half4> tex3D) {
	uint3 size;
	tex3D.GetDimensions(size.x, size.y, size.z);
	return size;
}
