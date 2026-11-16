Shader "Chrono/SlashBladeGlint"
{
    Properties
    {
        _Tint ("Glint color", Color) = (0.75,0.95,1,1)
        _Intensity ("Glint intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex GlintVertex
            #pragma fragment GlintFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Intensity;
            CBUFFER_END
            Varyings GlintVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 GlintFragment(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2;
                float horizontal = exp2(-abs(p.y) * 55) * pow(saturate(1 - abs(p.x)), 2);
                float vertical = exp2(-abs(p.x) * 55) * pow(saturate(1 - abs(p.y)), 2);
                float core = exp2(-dot(p,p) * 100);
                float halo = exp2(-dot(p,p) * 12) * 0.25;
                float alpha = saturate((horizontal + vertical + core + halo) * _Intensity);
                return half4(lerp(_Tint.rgb, half3(1,1,1), saturate(core * 2)), alpha * _Tint.a);
            }
            ENDHLSL
        }
    }
}
