// Brawler/BlobShadow
// Unlit, transparent, procedural radial blob shadow for a flat quad (UV 0..1). No texture.
// Alpha = _Color.a * (1 - smoothstep(_Inner, 1, distance-from-center * 2)).
// ZWrite Off + polygon offset so it never z-fights the ground; SRP Batcher compatible; WebGL2-safe.
Shader "Brawler/BlobShadow"
{
    Properties
    {
        [MainColor] _Color ("Shadow Color (alpha = strength)", Color) = (0, 0, 0, 0.5)
        _Inner ("Solid Core Radius (0..1)", Range(0, 0.99)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Inner;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half r = (half)(length(input.uv - 0.5) * 2.0);
                half falloff = 1.0h - smoothstep(_Inner, 1.0h, r);
                return half4(_Color.rgb, _Color.a * falloff);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
