Shader "Chrono/SlashSpace"
{
    Properties
    {
        _CrackTex ("Crack profile (RGBA = four jagged profiles)", 2D) = "gray" {}
        _CrackScale ("Art pixels per crack texture repeat", Float) = 128
        _Shear ("Tangent shear screen pixels", Float) = 72
        _ArtPixel ("Screen pixels per art pixel", Float) = 6
        _Desaturate ("Whole screen desaturation", Range(0,1)) = 0
        _ScreenFlash ("Whole screen flash", Range(0,1)) = 0
        _DesaturateTint ("Desaturated tint", Color) = (0.86,0.9,1,1)
        _CutTime ("Unscaled seconds since impact", Float) = 0
        _NegativeEnd ("Negative frame end seconds", Float) = 0.025
        _ImpactEnd ("Thick impact end seconds", Float) = 0.06
        _CloseEnd ("Tear closed seconds", Float) = 0.4
        _LightEnd ("Tear vanished seconds", Float) = 0.8
        _CoreThickness ("Impact white core art pixels", Float) = 5
        _HaloThickness ("Impact total art pixels", Float) = 11
        _RimThickness ("Black rim art pixels", Float) = 1.5
        _TearLength ("Torn end length art pixels", Float) = 28
        _Roughness ("Tear edge roughness", Float) = 1.6
        _VoidScale ("Open void size relative to impact", Float) = 0.85
        _ScarGap ("Closed tear size relative to open void", Float) = 0.15
        _ImpactFlash ("Local flash during impact", Range(0,1)) = 0.35
        _CutColor ("Cut color", Color) = (0.2509804,0.8509804,1,1)
        _VoidColor ("Void color", Color) = (0.01,0.015,0.04,1)
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
            TEXTURE2D(_CrackTex);
            SAMPLER(sampler_CrackTex);
            float4 _CutStart;
            float4 _CutEnd;
            float4 _SlashScreenSize;
            float _CrackScale;
            float _Shear;
            float _ArtPixel;
            float _Desaturate;
            float _ScreenFlash;
            half4 _DesaturateTint;
            float _CutTime;
            float _NegativeEnd;
            float _ImpactEnd;
            float _CloseEnd;
            float _LightEnd;
            float _CoreThickness;
            float _HaloThickness;
            float _RimThickness;
            float _TearLength;
            float _Roughness;
            float _VoidScale;
            float _ScarGap;
            float _ImpactFlash;
            half4 _CutColor;
            half4 _VoidColor;

            // 안쪽이 양수인 거리 값을 화면 1px 폭으로 부드럽게 덮개로 바꾸고, 폭이 1px보다 좁은 끝은 진하기도 줄여 실선이 남지 않게 함
            float Cover(float value, float width)
            {
                return saturate(value + min(width, 0.5)) * saturate(width * 2);
            }

            // 선 방향 위치를 원본 px 단위 반복 좌표로 바꿔 각진 균열 단면 네 개를 읽음
            float4 Crack(float alongPx, float art, float frequency, float shift)
            {
                float u = alongPx * frequency / (art * max(_CrackScale, 1)) + shift;
                return SAMPLE_TEXTURE2D_LOD(_CrackTex, sampler_CrackTex, float2(u, 0.5), 0);
            }

            // 하나로 이어진 찢김 윤곽의 안쪽 거리(화면 px, 안쪽 양수)
            // 위아래 경계는 각각 한 함수로만 정의해 끊김이 없고, 양 끝으로 갈수록 톱니가 깊어지며 뾰족하게 모임
            // scale은 모양 전체(끝 포함)를 같은 비율로 키우거나 줄임
            float TearValue(float signedPx, float alongPx, float lengthPx, float art, float halfWidth, float scale, out float width)
            {
                // 피해 선분 밖 찢김 구간에서의 진행도 0(몸통)~1(뾰족한 끝)
                float tearPx = max(_TearLength * art, 1);
                float beyond = max(max(-alongPx, alongPx - lengthPx), 0);
                float u = beyond / tearPx;
                width = 0;
                if (u >= 1) return -abs(signedPx) - 1;

                // 끝으로 갈수록 중심이 휘고 폭은 뾰족하게 줄어듦
                float roughness = max(_Roughness, 0);
                float4 coarse = Crack(alongPx, art, 1, 0);
                float4 fine = Crack(alongPx, art, 2.7, 0.53);
                float4 shard = Crack(alongPx, art, 6.1, 0.21);
                float center = (coarse.a - 0.5) * art * (3 + 3 * roughness) * u * scale;
                float offset = signedPx - center;
                float envelope = pow(1 - u, 0.85);

                // 위아래가 서로 다른 톱니 단면을 쓰고, 굵은 톱니·촘촘한 톱니·잔 가시를 겹쳐 거칠게 찢긴 경계를 만듦
                float sideCoarse = offset >= 0 ? coarse.r : coarse.g;
                float sideFine = offset >= 0 ? fine.b : fine.r;
                float sideShard = offset >= 0 ? shard.a : shard.g;
                float profile = lerp(sideCoarse, sideFine, saturate(0.35 * roughness + u * 0.5));
                profile = lerp(profile, sideShard, saturate(0.25 * roughness));
                float depth = saturate(lerp(0.35, 0.85, u) * (0.6 + 0.6 * roughness));
                float spike = 1.4 + 0.5 * roughness;
                width = halfWidth * scale * envelope * max((1 - depth) + depth * spike * profile, 0.05);
                return width - abs(offset);
            }

            half4 SlashFragment(Varyings input) : SV_Target
            {
                // 화면 픽셀 기준 무한 선의 방향·수직 거리·선분 위치
                float2 resolution = max(_SlashScreenSize.xy, float2(1,1));
                float2 samplePixel = input.texcoord * resolution;
                float2 lineStart = _CutStart.xy * resolution;
                float2 delta = (_CutEnd.xy - _CutStart.xy) * resolution;
                float lengthPx = max(length(delta), 0.001);
                float2 tangent = delta / lengthPx;
                float2 normal = float2(-tangent.y, tangent.x);
                float signedPx = dot(samplePixel - lineStart, normal);
                float alongPx = dot(samplePixel - lineStart, tangent);
                float side = signedPx >= 0 ? 1 : -1;
                float art = max(_ArtPixel, 0.01);

                // 두 반평면을 선 방향으로 서로 반대로 밀어 공간이 어긋난 듯 표시
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(input.texcoord - tangent * side * _Shear / resolution));

                // 화면 전체의 채도를 낮추고 임팩트 순간 함께 밝혀 청록 빛만 원색으로 남김
                half luminance = dot(color.rgb, half3(0.299, 0.587, 0.114));
                color.rgb = lerp(color.rgb, luminance * _DesaturateTint.rgb, saturate(_Desaturate));
                color.rgb = lerp(color.rgb, half3(1, 1, 1), saturate(_ScreenFlash));

                // 찢김 모양에서 먼 조각은 빛 계산 생략
                float tearPx = max(_TearLength * art, 1);
                float halfWidth = _HaloThickness * art * 0.5;
                float beyond = max(max(-alongPx, alongPx - lengthPx), 0);
                if (beyond > tearPx + halfWidth * 4 || abs(signedPx) > halfWidth * 5) return color;

                // 단계 시각: 한 모양이 반전 → 임팩트 → 틈으로 열림 → 같이 오므라듦 → 같이 사라짐
                float t = _CutTime;
                float negative = step(t, _NegativeEnd);
                float positive = step(t, _ImpactEnd) * (1 - negative);
                float after = step(_ImpactEnd, t) * step(t, _LightEnd);
                float closing = smoothstep(0, 1, saturate((t - _ImpactEnd) / max(_CloseEnd - _ImpactEnd, 0.001)));
                float vanish = 1 - smoothstep(0, 1, saturate((t - _CloseEnd) / max(_LightEnd - _CloseEnd, 0.001)));

                // 임팩트 순간 모양 주변만 짧게 밝힘
                float haloWidth;
                float halo = TearValue(signedPx, alongPx, lengthPx, art, halfWidth, 1, haloWidth);
                float flashRadius = halfWidth * 3;
                color.rgb = lerp(color.rgb, half3(1, 1, 1), saturate(_ImpactFlash) * positive * exp2(-min(halo, 0) * min(halo, 0) / (flashRadius * flashRadius) * 2));

                // 같은 모양을 흰 심 크기로도 읽음
                float coreWidth;
                float core = TearValue(signedPx, alongPx, lengthPx, art, halfWidth, _CoreThickness / max(_HaloThickness, 1), coreWidth);

                // 첫 프레임은 반전: 검은 심과 흰 테로 공간이 찢긴 순간을 강조
                color.rgb = lerp(color.rgb, half3(1, 1, 1), Cover(halo, haloWidth) * negative);
                color.rgb = lerp(color.rgb, _VoidColor.rgb, Cover(core, coreWidth) * negative);

                // 이어서 검은 가장자리 → 청록 발광·테 → 흰 심으로 터짐
                float rimPx = _RimThickness * art;
                float outside = max(-halo, 0);
                float glow = exp2(-outside * outside / max(halfWidth * halfWidth * 0.5, 1) * 2) * 0.5;
                color.rgb = lerp(color.rgb, _VoidColor.rgb, Cover(halo + rimPx, haloWidth + rimPx) * saturate(haloWidth) * positive);
                color.rgb = lerp(color.rgb, _CutColor.rgb, saturate(glow * step(halo, 0) * saturate(haloWidth / art) + Cover(halo, haloWidth)) * positive * _CutColor.a);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), Cover(core, coreWidth) * positive);

                // 같은 모양이 검은 틈과 청록 테두리로 남아 통째로 오므라든 뒤 함께 사라짐
                float open = lerp(_VoidScale, _VoidScale * _ScarGap, closing);
                float tearWidth;
                float tear = TearValue(signedPx, alongPx, lengthPx, art, halfWidth, open, tearWidth);
                float thin = max(0.5 * art, 1);
                float alpha = after * vanish;
                float borderGlow = exp2(-tear * tear / max(art * art * 2.25, 1) * 2) * 0.4;
                color.rgb = lerp(color.rgb, _CutColor.rgb, borderGlow * saturate(tearWidth * 2 / thin) * alpha * _CutColor.a);
                color.rgb = lerp(color.rgb, _VoidColor.rgb, Cover(tear, tearWidth) * alpha);
                color.rgb = lerp(color.rgb, lerp(_CutColor.rgb, half3(1, 1, 1), 0.55), Cover(thin * 0.5 - abs(tear), thin * 0.5) * saturate(tearWidth * 2 / thin) * alpha);
                return color;
            }
            ENDHLSL
        }
    }
}
