Shader "Chrono/FeverAura"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _EmberMode ("Ember Shape", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex AuraVertex
            #pragma fragment AuraFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _EmberMode;
            CBUFFER_END

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

            Varyings AuraVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Tint;
                return output;
            }

            half4 AuraFragment(Varyings input) : SV_Target
            {
                // 부드러운 원형 대신 작은 정수 셀의 비대칭 불길과 십자 불씨 표시
                float ember = step(0.5, _EmberMode);
                float2 grid = lerp(float2(3.0, 8.0), float2(3.0, 3.0), ember);
                float2 cell = min(floor(saturate(input.uv) * grid), grid - 1.0);
                float center = 1.0 - step(0.5, abs(cell.x - 1.0));
                float leftBase = (1.0 - step(0.5, cell.x)) * (1.0 - step(3.0, cell.y));
                float rightBase = step(1.5, cell.x) * (1.0 - step(4.0, cell.y));
                float flameMask = saturate(center + leftBase + rightBase);
                float sparkMask = 1.0 - step(1.5, abs(cell.x - 1.0) + abs(cell.y - 1.0));
                half mask = lerp(flameMask, sparkMask, ember);

                // 입자 팔레트를 유지하면서 작은 중심만 연한 금빛으로 강조
                half core = center * (1.0 - step(5.0, cell.y));
                half3 color = lerp(input.color.rgb, half3(1.0, 0.96, 0.63) * _Tint.rgb, core * 0.3);
                half alpha = input.color.a * mask;
                clip(alpha - 0.01);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
