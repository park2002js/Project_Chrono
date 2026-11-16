Shader "Chrono/DrawImageFx"
{
    Properties
    {
        _MainTex ("Draw FX Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _DissolveProgress ("Dissolve Progress", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #pragma vertex DrawVertex
            #pragma fragment DrawFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 worldPosition : TEXCOORD1;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                float _DissolveProgress;
            CBUFFER_END

            Varyings DrawVertex(Attributes input)
            {
                // 기존 URP2D 텍스처·정렬·개체 색을 그대로 유지
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                output.worldPosition = TransformObjectToWorld(input.positionOS).xy;
                return output;
            }

            float DrawNoise(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 DrawFragment(Varyings input) : SV_Target
            {
                // 분해 시작 전에는 선택한 R04 이미지의 알파와 색을 그대로 표시
                float progress = saturate(_DissolveProgress);
                half4 color = CommonUnlitFragment(input, input.color);
                if (progress <= 0.0001)
                {
                    color.a *= _Opacity;
                    return color;
                }
                if (progress >= 0.9999) return half4(0, 0, 0, 0);

                // 길이가 달라도 월드 크기가 일정한 작은 빛 조각으로 원래 FX 분리
                float2 grid = input.worldPosition * 18.0;
                float2 cell = floor(grid);
                float noise = DrawNoise(cell) * 0.82 + DrawNoise(floor(input.worldPosition * 72.0)) * 0.18;
                float alive = 1.0 - smoothstep(noise * 0.84, noise * 0.84 + 0.16, progress);
                float2 withinCell = frac(grid);
                float boundary = min(min(withinCell.x, 1.0 - withinCell.x), min(withinCell.y, 1.0 - withinCell.y));
                float separation = lerp(1.0, smoothstep(0.0, 0.10, boundary), smoothstep(0.0, 0.65, progress));

                // 같은 광원 텍스처를 조금씩 흐트러뜨리고 조각마다 사라지는 시점 분산
                float2 drift = float2(DrawNoise(cell + 17.3), DrawNoise(cell + 41.7)) - 0.5;
                float2 uv = input.uv + drift * progress * progress * float2(0.007, 0.14);
                color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * input.color;
                color.a *= _Opacity * alive * separation;
                return color;
            }
            ENDHLSL
        }
    }
}
