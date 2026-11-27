Shader "Chrono/ImpactDistortion"
{
    Properties
    {
        _Center ("Center (viewport UV)", Vector) = (0.5,0.5,0,0)
        _Ring ("Ring radius (screen heights)", Float) = 0
        _Width ("Ring width (screen heights)", Float) = 0.2
        _Strength ("Push distance (screen heights)", Float) = 0
        _Aspect ("Screen aspect", Float) = 1.7777778
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "ImpactRadialDistortion"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DistortionFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _Center;
            float _Ring;
            float _Width;
            float _Strength;
            float _Aspect;
            half4 DistortionFragment(Varyings input) : SV_Target
            {
                // 가로를 화면 높이 단위로 맞춘 뒤 중심에서의 거리와 방향 계산
                float2 delta = input.texcoord - _Center.xy;
                delta.x *= _Aspect;
                float distanceFromCenter = length(delta);
                float2 direction = distanceFromCenter > 0.00001 ? delta / distanceFromCenter : float2(0, 0);

                // 퍼져 나가는 고리 근처의 화면만 바깥쪽으로 밀어냄
                float band = saturate(1.0 - abs(distanceFromCenter - _Ring) / max(_Width, 0.00001));
                float2 offset = direction * band * band * _Strength;
                offset.x /= _Aspect;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(input.texcoord - offset));
            }
            ENDHLSL
        }
    }
}
