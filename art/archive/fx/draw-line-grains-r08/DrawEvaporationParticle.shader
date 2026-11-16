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
                float3 centerWS : TEXCOORD1;
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
                // 바깥 테두리1px와 경계 안티앨리어싱0.5px를 담을 화면 공간 확보
                float4 centerCS = TransformWorldToHClip(input.centerWS);
                float2 outward = sign(output.positionCS.xy / output.positionCS.w - centerCS.xy / centerCS.w);
                output.positionCS.xy += outward * (3.0 / _ScreenParams.xy) * output.positionCS.w;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 ParticleFragment(Varyings input) : SV_Target
            {
                // 확장한 쿼드에서 원래 원의 반지름을 복원해 중심 크기 보존
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float2 halfSizePixels = rcp(max(fwidth(centeredUv), 0.0001));
                float radiusPixels = length(centeredUv * halfSizePixels);
                float originalRadiusPixels = max(0.01, min(halfSizePixels.x, halfSizePixels.y) - 1.5);
                float radius = radiusPixels / originalRadiusPixels;
                half core = 1.0 - smoothstep(0.0, 0.55, radius);
                half3 centerColor = lerp(half3(0.18, 0.68, 1.0), half3(0.88, 1.0, 1.0), core);
                half centerAlpha = 1.0 - smoothstep(0.35, 1.0, radius);

                // 진한 청록 외곽을 화면 기준1px 둘러 밝은 타일 위에서도 원형 구분
                half3 color = lerp(half3(0.025, 0.24, 0.40), centerColor, centerAlpha);
                half alpha = 1.0 - smoothstep(originalRadiusPixels + 0.5, originalRadiusPixels + 1.5, radiusPixels);
                return half4(color * input.color.rgb, alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
