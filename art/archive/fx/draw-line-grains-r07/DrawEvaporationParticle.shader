Shader "Chrono/DrawEvaporationParticle"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex ParticleVertex
            #pragma fragment ParticleFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ParticleVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 ParticleFragment(Varyings input) : SV_Target
            {
                // 가로세로가 같은 작은 원에 밝은 중심과 청록 잔광을 표시
                float radius = length(input.uv * 2.0 - 1.0);
                half core = 1.0 - smoothstep(0.0, 0.55, radius);
                half3 color = lerp(half3(0.18, 0.68, 1.0), half3(0.88, 1.0, 1.0), core);
                half alpha = 1.0 - smoothstep(0.35, 1.0, radius);
                return half4(color * input.color.rgb, alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
