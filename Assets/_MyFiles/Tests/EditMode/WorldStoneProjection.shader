Shader "Hidden/Brawler/WorldStoneProjectionTest"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 _TopMap_ST;
            float4 _SideMap_ST;
            half4 _TopColor;
            half4 _SideColor;
            half _AmbientStrength;
            float3 _Position;
            half3 _Normal;
            #include "Assets/_MyFiles/Art/Shaders/WorldStone.hlsl"

            float4 Vert(float3 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS);
            }

            half4 Frag() : SV_Target
            {
                return half4(SampleWorldStone(_Position, _Normal), 1);
            }
            ENDHLSL
        }
    }
}
