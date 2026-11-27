Shader "Chrono/DrawImageFx"
{
    Properties
    {
        [HideInInspector] _MainTex ("Unused legacy texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
        _CoreColor ("Core color", Color) = (1,1,1,1)
        _BeamColor ("Beam color", Color) = (0.2509804,0.8509804,1,1)
        _ShadowColor ("Under shadow color", Color) = (0.02,0.05,0.12,0.45)
        _AreaColor ("Hit area color", Color) = (0.2509804,0.8509804,1,0.12)
        _CoreWidth ("Core width screen pixels", Float) = 2.5
        _BeamWidth ("Beam width screen pixels", Float) = 6
        _GlowRadius ("Glow radius screen pixels", Float) = 9
        _GlowStrength ("Glow strength", Range(0,1)) = 0.55
        _ShadowWidth ("Under shadow width screen pixels", Float) = 12
        _TaperLength ("End taper world length", Float) = 0.7
        _PulseSpacing ("Shimmer spacing world", Float) = 1.4
        _PulseLength ("Shimmer length world", Float) = 0.22
        _PulseSpeed ("Shimmer speed world per second", Float) = 2.4
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex DrawVertex
            #pragma fragment DrawFragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 positionWS : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
                half4 _CoreColor;
                half4 _BeamColor;
                half4 _ShadowColor;
                half4 _AreaColor;
                float _CoreWidth;
                float _BeamWidth;
                float _GlowRadius;
                float _GlowStrength;
                float _ShadowWidth;
                float _TaperLength;
                float _PulseSpacing;
                float _PulseLength;
                float _PulseSpeed;
            CBUFFER_END
            float4 _LineOrigin; // 경로 시작점의 월드 좌표
            float4 _LineDirection; // 경로의 월드 단위 방향
            float _LineLength; // 경로의 월드 길이
            float _LineWidth; // 판정 폭과 같은 표시 메시의 월드 폭
            float _UnscaledTime; // 게임 정지 중에도 흐르는 실제 시간

            Varyings DrawVertex(Attributes input)
            {
                // LineRenderer 메시를 그대로 두고 조각마다 월드 좌표를 넘김
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.positionWS = world.xy;
                output.color = input.color * _Color;
                return output;
            }

            // 반지름 radius 안쪽을 화면 1px 폭으로 부드럽게 채우는 덮개
            float Band(float distance, float radius)
            {
                return saturate(radius - distance + 0.5);
            }

            // 아래 색 위에 알파 합성
            half4 Over(half4 below, half3 color, float alpha)
            {
                float outAlpha = alpha + below.a * (1 - alpha);
                half3 outColor = (color * alpha + below.rgb * below.a * (1 - alpha)) / max(outAlpha, 1e-4);
                return half4(outColor, outAlpha);
            }

            half4 DrawFragment(Varyings input) : SV_Target
            {
                // 화면 해상도 기준 거리로 계산해 계단 없이 매끈한 경계 유지
                float2 direction = normalize(_LineDirection.xy + float2(1e-6, 0));
                float2 normal = float2(-direction.y, direction.x);
                float2 local = input.positionWS - _LineOrigin.xy;
                float along = dot(local, direction);
                float pixelsPerWorld = 1 / max(length(float2(ddx(input.positionWS.x), ddy(input.positionWS.x))), 1e-5);
                float across = abs(dot(local, normal)) * pixelsPerWorld;
                float halfArea = _LineWidth * 0.5 * pixelsPerWorld;

                // 둥근 끝 메시 안에서 선분까지의 거리로 면·발광을 둥글게 감싸 끝이 잘린 듯 보이지 않게 함
                float beyond = max(max(-along, along - _LineLength), 0) * pixelsPerWorld;
                float capsule = sqrt(across * across + beyond * beyond);

                // 흰 심·청록 테는 양 끝에서 칼끝처럼 가늘어지며 사라짐
                float taperLength = max(_TaperLength, 1e-3);
                float taper = saturate(along / taperLength) * saturate((_LineLength - along) / taperLength);
                float tip = smoothstep(0, 1, sqrt(taper));

                // 판정 폭은 둥근 가장자리로 갈수록 옅어지는 면으로만 표시
                half4 color = half4(_AreaColor.rgb, 0);
                color = Over(color, _AreaColor.rgb, _AreaColor.a * (1 - smoothstep(halfArea * 0.35, halfArea, capsule)));

                // 밝은 바닥에서도 테가 묻히지 않도록 아주 옅은 그림자를 깖
                color = Over(color, _ShadowColor.rgb, _ShadowColor.a * Band(across, _ShadowWidth * 0.5 * tip) * tip);

                // 청록 발광은 끝에서 점점 작고 옅은 빛 덩어리로 줄어든 뒤 청록 테·흰 심을 덮음
                float glowRadius = max(_GlowRadius * lerp(0.55, 1, tip), 1e-3);
                float glow = exp2(-capsule * capsule / (glowRadius * glowRadius) * 2) * _GlowStrength * lerp(0.35, 1, tip);
                color = Over(color, _BeamColor.rgb, glow);
                // 반지름이 0에 가까워져도 1px 실선이 끝까지 삐져나오지 않도록 알파도 함께 줄임
                float beam = Band(across, _BeamWidth * 0.5 * tip) * tip;
                float core = Band(across, _CoreWidth * 0.5 * tip) * tip * tip;

                // 청록 테를 따라 부드러운 빛이 시작점에서 끝점으로 흐름
                float spacing = max(_PulseSpacing, 0.01);
                float offset = (frac((along - _UnscaledTime * _PulseSpeed) / spacing) - 0.5) * spacing;
                float shimmer = exp2(-offset * offset / max(_PulseLength * _PulseLength, 1e-4) * 2);
                color = Over(color, lerp(_BeamColor.rgb, _CoreColor.rgb, shimmer * 0.75), beam);
                color = Over(color, _CoreColor.rgb, core);

                color *= input.color;
                color.a *= _Opacity;
                return color;
            }
            ENDHLSL
        }
    }
}
