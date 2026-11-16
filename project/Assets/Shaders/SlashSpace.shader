Shader "Chrono/SlashSpace"
{
    Properties
    {
        _Displacement ("Half-plane displacement pixels", Float) = 18
        _CutGlow ("Cut glow pixels", Float) = 2.5
        _CutHalfWidthPixels ("Damage half width pixels", Float) = 12
        _CutProgress ("Unscaled cut progress", Range(0,1)) = 0
        _Flash ("Cut flash", Float) = 1
        _CutColor ("Cut color", Color) = (1,0.83137255,0.23137255,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "SlashWorldShear"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment SlashFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _CutStart;
            float4 _CutEnd;
            float4 _SlashScreenSize;
            float _Displacement;
            float _CutGlow;
            float _CutHalfWidthPixels;
            float _CutProgress;
            float _Flash;
            half4 _CutColor;
            float SlashHash(float value)
            {
                return frac(sin(value * 127.1 + 311.7) * 43758.5453);
            }
            half4 SlashFragment(Varyings input) : SV_Target
            {
                float2 resolution = max(_SlashScreenSize.xy, float2(1,1));
                float2 samplePixel = input.texcoord * resolution;
                float2 lineStart = _CutStart.xy * resolution;
                float2 delta = (_CutEnd.xy - _CutStart.xy) * resolution;
                float lengthPixels = max(length(delta), 0.001);
                float2 tangent = delta / lengthPixels;
                float2 normal = float2(-tangent.y, tangent.x);
                float signedDistance = dot(samplePixel - lineStart, normal);
                float side = signedDistance >= 0 ? 1 : -1;
                float2 offset = tangent * side * _Displacement / resolution;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(input.texcoord + offset));
                float along = dot(samplePixel - lineStart, tangent);
                // 어긋난 두 화면에 걸친 빛을 원래 선분 끝과 실제 판정 폭 안에서 소멸
                float progress = saturate(_CutProgress);
                float halfWidth = max(_CutHalfWidthPixels, 0.1);
                float antialias = min(max(fwidth(signedDistance), 0.5), halfWidth);
                float segment = step(0, along) * step(along, lengthPixels);
                float taperLength = max(1, min(40, lengthPixels * 0.13));
                float taper = smoothstep(0, taperLength, along) * (1 - smoothstep(lengthPixels - taperLength, lengthPixels, along));
                float withinWidth = 1 - smoothstep(halfWidth - antialias, halfWidth, abs(signedDistance));
                float life = pow(saturate(_Flash), 0.35) * _CutColor.a;
                float mask = segment * taper * withinWidth * life;

                // 흰 가는 중심과 금빛 halo가 시간에 따라 흔들리며 가늘어지는 잔광
                float ripple = sin(along * 0.055 - progress * 14) * sin(along * 0.019 + progress * 8) * halfWidth * 0.055 * progress;
                float distanceToLight = abs(signedDistance - ripple);
                float coreRadius = max(0.25, min(3, halfWidth * 0.3)) * lerp(1, 0.25, progress);
                float haloRadius = min(halfWidth, max(coreRadius * 2.2, _CutGlow * 1.4)) * lerp(1, 0.45, progress);
                float shimmer = 0.65 + 0.35 * pow(0.5 + 0.5 * sin(along * 0.09 - progress * 24), 2);
                float halo = exp2(-distanceToLight * distanceToLight / max(haloRadius * haloRadius, 0.01)) * mask * shimmer;
                float core = exp2(-distanceToLight * distanceToLight / max(coreRadius * coreRadius, 0.01) * 1.5) * mask;
                color.rgb = lerp(color.rgb, _CutColor.rgb, saturate(halo * 0.62));
                color.rgb = lerp(color.rgb, half3(1, 0.985, 0.85), saturate(core * 0.95));

                // 몇 개의 가는 빛 조각이 선을 따라 흐르며 양쪽 잔광 안에서 흩어짐
                float spacing = max(18, min(42, lengthPixels / 14));
                float cell = floor(along / spacing);
                float seed = SlashHash(cell);
                float localAlong = (frac(along / spacing) - frac(seed + progress * 0.65)) * spacing;
                float sparkSide = SlashHash(cell + 71) > 0.5 ? 1 : -1;
                float sparkAcross = sparkSide * halfWidth * progress * (0.35 + seed * 0.55);
                float sparkLength = lerp(1.5, 5, seed) * lerp(1, 0.4, progress);
                float sparkWidth = max(0.4, min(1.1, halfWidth * 0.08));
                float spark = exp2(-localAlong * localAlong / (sparkLength * sparkLength) * 2
                    - (signedDistance - sparkAcross) * (signedDistance - sparkAcross) / (sparkWidth * sparkWidth) * 2);
                spark *= step(0.53, seed) * mask * (0.5 + 0.5 * sin(progress * 17 + seed * 8));
                color.rgb = lerp(color.rgb, lerp(_CutColor.rgb, half3(1, 1, 0.9), 0.65), saturate(spark));
                return color;
            }
            ENDHLSL
        }
    }
}
