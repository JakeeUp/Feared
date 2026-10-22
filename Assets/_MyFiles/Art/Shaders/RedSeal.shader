Shader "Brawler/RedSeal"
{
    // Devil May Cry style arena seal: an additive red energy wall with streaks rising through it, brightest at the
    // floor and the rim. _Reveal (0..1, driven by SealBarrier) raises it out of the floor behind a burning edge.
    Properties
    {
        [HDR] _Color ("Color", Color) = (3, 0.15, 0.1, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (6, 1.2, 0.4, 1)
        _Reveal ("Reveal", Range(0, 1)) = 1
        _StreakScale ("Streak Scale", Float) = 3
        _Speed ("Rise Speed", Float) = 1.4
        _Opacity ("Opacity", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                half _Reveal;
                half _StreakScale;
                half _Speed;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float height : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.positionOS.y + 0.5; // unit cube: 0 at the floor, 1 at the top
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float h = input.height;
                float t = _Time.y;

                // Rising out of the floor: hidden above the reveal line, with a hot band just below it.
                float revealTop = _Reveal * 1.08;
                clip(revealTop - h);
                float edge = saturate(1.0 - (revealTop - h) / 0.08) * step(_Reveal, 0.999);

                // Vertical streaks that climb (narrow along the wall, stretched in height).
                float across = input.positionWS.x + input.positionWS.z;
                float2 uv = float2(across * _StreakScale, input.positionWS.y * 0.6 - t * _Speed);
                float streaks = Noise(uv) * 0.65 + Noise(uv * 2.3 + 7.1) * 0.35;
                streaks = smoothstep(0.35, 0.95, streaks);

                // Rim: brighter where the wall is seen edge-on, so the volume reads from the side camera.
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float rim = 1.0 - abs(dot(normalize(input.normalWS), viewDir));

                float floorGlow = pow(1.0 - h, 3.0);
                float flicker = 0.85 + 0.15 * sin(t * 13.0 + across * 3.0);
                float alpha = saturate((0.25 + streaks * 0.75) * (1.0 - h * 0.55) + floorGlow * 0.6 + rim * 0.35);
                alpha *= _Opacity * flicker;

                half3 color = _Color.rgb * (0.6 + streaks) + _EdgeColor.rgb * (edge + floorGlow * 0.4);
                return half4(color, saturate(alpha + edge));
            }
            ENDHLSL
        }
    }
}
