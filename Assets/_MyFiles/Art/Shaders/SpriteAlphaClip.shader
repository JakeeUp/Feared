// Brawler/SpriteAlphaClip
// Opaque, alpha-tested sprite shader for SpriteRenderers billboarded in a 3D URP world.
// - Works with SpriteRenderer: reads _MainTex (set per-renderer), vertex color, unity_SpriteColor
//   (SpriteRenderer.color) and unity_SpriteProps.xy (SpriteRenderer.flipX/flipY), mirroring URP's own
//   Sprite-Unlit-Default so flip/color behave the same.
// - ZWrite On + clip(alpha - _Cutoff): sorts against 3D geometry and other billboards by depth, no
//   transparency sorting issues.
// - View-space depth bias (_DepthBias, world units): Forward/DepthOnly/DepthNormals move each vertex's DEPTH
//   toward the camera by _DepthBias along the view axis, keeping clip xy/w unchanged, so the sprite's on-screen
//   position and size are untouched. A flat billboard on a character ~0.5 deep otherwise slices through nearby
//   3D geometry (walls behind it, platform faces, the floor) and loses half its pixels to the depth test; with the
//   bias it wins against anything within _DepthBias behind its plane and is still hidden by geometry more than
//   _DepthBias in front of it. Computed through the projection matrix, so it is correct for perspective and
//   orthographic cameras and independent of UNITY_REVERSED_Z. Clamped so a vertex is never pulled past the
//   near plane. NOT applied in ShadowCaster (cast shadows stay where the sprite really is).
// - Cull Off: sprites are visible from both sides and negative-scale flipping is safe.
// - Simple half-lambert main light + SH ambient + fog. Casts directional shadows (alpha-clipped); does not
//   receive them (actors also use blob shadows as contact shadows).
// - SRP Batcher compatible (identical UnityPerMaterial CBUFFER in every pass), GPU instancing, WebGL2-safe.
Shader "Brawler/SpriteAlphaClip"
{
    Properties
    {
        [PerRendererData][MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        _Wrap ("Light Wrap (0 = lambert, 1 = half-lambert)", Range(0, 1)) = 0.5
        _FlashColor ("Flash (rgb = colour, a = amount)", Color) = (1, 1, 1, 0)
        _DepthBias ("Depth Bias Toward Camera (world units)", Range(0, 2)) = 0.5

        // Legacy properties SpriteRenderer may look for. Hidden.
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        // Must be identical in every pass for SRP Batcher compatibility.
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half _Cutoff;
            half _AmbientStrength;
            half _Wrap;
            half4 _FlashColor;
            float _DepthBias;
        CBUFFER_END

        // SpriteRenderer color/flip arrive per-draw in unity_SpriteColor / unity_SpriteProps (UnityPerDraw).
        // With GPU instancing they come from an instanced array instead (same layout as URP Core2D.hlsl).
        #ifdef UNITY_INSTANCING_ENABLED
            UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpriteRendererColorArray)
                UNITY_DEFINE_INSTANCED_PROP(float2, unity_SpriteFlipArray)
            UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
            #define BRAWLER_SPRITE_COLOR UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #define BRAWLER_SPRITE_FLIP  UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteFlipArray)
        #else
            #define BRAWLER_SPRITE_COLOR unity_SpriteColor
            #define BRAWLER_SPRITE_FLIP  unity_SpriteProps.xy
        #endif

        struct SpriteAttributes
        {
            float3 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
            half4  color      : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 FlipSprite(float3 positionOS)
        {
            return float3(positionOS.xy * BRAWLER_SPRITE_FLIP, positionOS.z);
        }

        // Sprites carry no normals. Billboards face the camera with their local -Z, so use that.
        float3 SpriteNormalWS()
        {
            return TransformObjectToWorldNormal(float3(0.0, 0.0, -1.0));
        }

        // Clip-space position with only the depth pulled _DepthBias toward the camera (see header).
        // positionCS.xyw stay exactly as TransformWorldToHClip makes them, so rasterized pixels and perspective-correct
        // interpolation are unchanged; z is replaced by the biased point's NDC depth rescaled by the original w.
        // Projected depth depends only on view-space z for both perspective and orthographic matrices, so moving the
        // view-space z alone is enough, and going through the projection matrix keeps reversed-Z/GL conventions right.
        float4 SpriteObjectToHClipDepthBiased(float3 positionOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float4 positionCS = TransformWorldToHClip(positionWS);

            // Unity view space looks down -Z: distance in front of the camera is -positionVS.z.
            float3 positionVS = TransformWorldToView(positionWS);
            float viewDepth = -positionVS.z;
            float nearLimit = _ProjectionParams.y + 0.001;
            if (viewDepth > nearLimit)
            {
                float biasedDepth = max(viewDepth - _DepthBias, nearLimit);
                float4 biasedCS = TransformWViewToHClip(float3(positionVS.xy, -biasedDepth));
                positionCS.z = biasedCS.z * (positionCS.w / biasedCS.w);
            }
            return positionCS;
        }

        half4 SampleSprite(float2 uv, half4 vertexColor)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * vertexColor;
        }
        ENDHLSL

        // ============ Forward ============
        Pass
        {
            Name "ForwardUnlitLambert"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(SpriteAttributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionOS = FlipSprite(input.positionOS);
                // Fog uses the true (unbiased) depth so the bias never changes a sprite's fog amount.
                output.fogFactor = ComputeFogFactor(TransformObjectToHClip(positionOS).z);
                output.positionCS = SpriteObjectToHClipDepthBiased(positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color * BRAWLER_SPRITE_COLOR;
                output.normalWS = (half3)SpriteNormalWS();
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleSprite(input.uv, input.color);
                clip(albedo.a - _Cutoff);

                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                // Cull Off + billboards: light both sides equally so a sprite never goes black.
                half ndotl = abs(dot(normalWS, mainLight.direction));
                half diffuse = saturate((ndotl + _Wrap) / (1.0h + _Wrap));
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;
                half3 lighting = mainLight.color * (diffuse * mainLight.distanceAttenuation) + ambient;

                half3 color = albedo.rgb * lighting;
                color = lerp(color, _FlashColor.rgb, _FlashColor.a); // HitFlash
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ============ ShadowCaster (sprites cast real directional shadows onto the 3D world) ============
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // No _DepthBias here: shadows are cast from the sprite's real position.
            // Set by URP for the light currently rendering its shadow map (declared per pass, as in URP's ShadowCasterPass).
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            ShadowVaryings ShadowVert(SpriteAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(FlipSprite(input.positionOS));
                float3 normalWS = SpriteNormalWS();

            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color * BRAWLER_SPRITE_COLOR;
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(SampleSprite(input.uv, input.color).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ============ DepthOnly (depth prepass / camera depth texture) ============
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(SpriteAttributes input)
            {
                DepthVaryings output = (DepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Same biased depth as Forward: depth priming (ZTest Equal) and the depth texture must match it.
                output.positionCS = SpriteObjectToHClipDepthBiased(FlipSprite(input.positionOS));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color * BRAWLER_SPRITE_COLOR;
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(SampleSprite(input.uv, input.color).a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ============ DepthNormals (SSAO / depth-normals prepass) ============
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                half3  normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthNormalsVaryings DepthNormalsVert(SpriteAttributes input)
            {
                DepthNormalsVaryings output = (DepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = SpriteObjectToHClipDepthBiased(FlipSprite(input.positionOS));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color * BRAWLER_SPRITE_COLOR;
                output.normalWS = (half3)SpriteNormalWS();
                return output;
            }

            half4 DepthNormalsFrag(DepthNormalsVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(SampleSprite(input.uv, input.color).a - _Cutoff);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
