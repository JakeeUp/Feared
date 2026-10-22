Shader "Brawler/OccluderDither"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _TopMap ("Top Map", 2D) = "white" {}
        _SideMap ("Side Map", 2D) = "white" {}
        _TopColor ("Top Color", Color) = (1, 1, 1, 1)
        _SideColor ("Side Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _WorldProjection ("World Projection", Float) = 0
        _Fade ("Fade (1 = solid)", Range(0, 1)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
        }
        Cull Back
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _TopMap_ST;
            float4 _SideMap_ST;
            half4 _BaseColor;
            half4 _TopColor;
            half4 _SideColor;
            half _WorldProjection;
            half _Fade;
            half _AmbientStrength;
        CBUFFER_END
        #include "WorldStone.hlsl"

        void DitherClip(float4 positionCS)
        {
            static const half bayer[16] =
            {
                 0.0h,  8.0h,  2.0h, 10.0h,
                12.0h,  4.0h, 14.0h,  6.0h,
                 3.0h, 11.0h,  1.0h,  9.0h,
                15.0h,  7.0h, 13.0h,  5.0h
            };
            uint2 p = (uint2)positionCS.xy % 4u;
            half threshold = (bayer[p.y * 4u + p.x] + 0.5h) / 16.0h;
            clip(_Fade - threshold);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLambert"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StoneVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            half4 Frag(StoneVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                DitherClip(input.positionCS);
                half3 normalWS = normalize(input.normalWS);
                half3 albedo = _WorldProjection > 0.5h
                    ? SampleWorldStone(input.positionWS, normalWS)
                    : SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * _BaseColor.rgb;
                half3 color = albedo * StoneLighting(input.positionWS, normalWS, input.positionCS);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StoneVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            half DepthFrag(StoneVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                DitherClip(input.positionCS);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex StoneVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 DepthNormalsFrag(StoneVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                DitherClip(input.positionCS);
                return StoneDepthNormal(input.normalWS);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
