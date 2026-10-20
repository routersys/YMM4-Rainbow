Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);
Texture2D TableTexture : register(t1);
SamplerState TableSampler : register(s1);

cbuffer Constants : register(b0)
{
    float4 inputBounds : packoffset(c0);
    float3 antisolar : packoffset(c1.x);
    float halfFieldTangent : packoffset(c1.w);
    float amount : packoffset(c2.x);
    float tableStart : packoffset(c2.y);
    float tableSpan : packoffset(c2.z);
    float tableWidth : packoffset(c2.w);
};

static const float DegreesPerRadian = 57.29577951308232;

float3 SrgbToLinear(float3 encoded)
{
    float3 low = encoded / 12.92;
    float3 high = pow((encoded + 0.055) / 1.055, 2.4);
    return lerp(high, low, step(encoded, 0.04045));
}

float3 LinearToSrgb(float3 linearColor)
{
    float3 low = linearColor * 12.92;
    float3 high = 1.055 * pow(linearColor, 1.0 / 2.4) - 0.055;
    return lerp(high, low, step(linearColor, 0.0031308));
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0,
    float4 uv1 : TEXCOORD1
) : SV_TARGET
{
    float4 source = SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0);
    if (amount <= 0.0 || source.a <= 0.0)
        return source;

    float2 center = 0.5 * (inputBounds.xy + inputBounds.zw);
    float focalLength = 0.5 * (inputBounds.z - inputBounds.x) / halfFieldTangent;
    float3 direction = normalize(float3(scenePosition.xy - center, focalLength));
    float angle = acos(clamp(dot(direction, antisolar), -1.0, 1.0)) * DegreesPerRadian;
    float tablePosition = (angle - tableStart) / tableSpan;
    if (tablePosition < 0.0 || tablePosition > 1.0)
        return source;

    float2 tableUv = float2((tablePosition * (tableWidth - 1.0) + 0.5) / tableWidth, 0.5);
    float4 entry = TableTexture.SampleLevel(TableSampler, tableUv, 0);
    float3 linearColor = SrgbToLinear(saturate(source.rgb / source.a));
    linearColor = linearColor * lerp(1.0, entry.a, amount) + entry.rgb * amount;
    return float4(LinearToSrgb(saturate(linearColor)) * source.a, source.a);
}
