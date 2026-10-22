#ifndef BRAWLER_WORLD_STONE_INCLUDED
#define BRAWLER_WORLD_STONE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

TEXTURE2D(_TopMap);
SAMPLER(sampler_TopMap);
TEXTURE2D(_SideMap);
SAMPLER(sampler_SideMap);

struct StoneAttributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct StoneVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    half fogFactor : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

StoneVaryings StoneVert(StoneAttributes input)
{
    StoneVaryings output = (StoneVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS);
    output.positionCS = positions.positionCS;
    output.positionWS = positions.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    output.fogFactor = ComputeFogFactor(positions.positionCS.z);
    return output;
}

float2 StoneUV(float3 positionWS, half3 normalWS)
{
    half3 axis = abs(normalWS);
    if (axis.y >= max(axis.x, axis.z))
        return float2(positionWS.x, normalWS.y < 0 ? -positionWS.z : positionWS.z);
    if (axis.x > axis.z)
        return float2(normalWS.x < 0 ? -positionWS.z : positionWS.z, positionWS.y);
    return float2(normalWS.z > 0 ? -positionWS.x : positionWS.x, positionWS.y);
}

half3 SampleWorldStone(float3 positionWS, half3 normalWS)
{
    float2 uv = StoneUV(positionWS, normalWS);
    if (normalWS.y > 0.7h)
        return SAMPLE_TEXTURE2D(_TopMap, sampler_TopMap, uv * _TopMap_ST.xy + _TopMap_ST.zw).rgb * _TopColor.rgb;
    return SAMPLE_TEXTURE2D(_SideMap, sampler_SideMap, uv * _SideMap_ST.xy + _SideMap_ST.zw).rgb * _SideColor.rgb;
}

half3 StoneLighting(float3 positionWS, half3 normalWS, float4 positionCS)
{
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
#else
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
#endif
    Light mainLight = GetMainLight(shadowCoord);
    half3 lighting = SampleSH(normalWS) * _AmbientStrength
        + LightingLambert(mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation), mainLight.direction, normalWS);

#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
        lighting += LightingLambert(light.color * (light.distanceAttenuation * light.shadowAttenuation), light.direction, normalWS);
    }
#endif
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
        lighting += LightingLambert(light.color * (light.distanceAttenuation * light.shadowAttenuation), light.direction, normalWS);
    LIGHT_LOOP_END
#endif
    return lighting;
}

half4 StoneDepthNormal(half3 normalWS)
{
    normalWS = NormalizeNormalPerPixel(normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    return half4(PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5)), 0);
#else
    return half4(normalWS, 0);
#endif
}

#endif
