Shader "Chrono/SpriteFlashSpread"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _FlashCenter ("Flash center (world)", Vector) = (0,0,0,0)
        _FlashRadius ("Flash radius (world units)", Float) = 0
        _FlashStrength ("Flash strength", Range(0,1)) = 1
        _PixelsPerUnit ("Pixels per unit", Float) = 16
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
            #pragma vertex FlashVertex
            #pragma fragment FlashFragment
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
                float2 flashWorld : TEXCOORD6;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _FlashCenter;
                float _FlashRadius;
                float _FlashStrength;
                float _PixelsPerUnit;
            CBUFFER_END
            Varyings FlashVertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                output.flashWorld = TransformObjectToWorld(input.positionOS).xy;
                return output;
            }
            half4 FlashFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);

                // 원본 픽셀 한 칸이 한 번에 밝아지도록 월드 좌표를 픽셀 격자 중심으로 맞춤
                float2 cell = (floor(input.flashWorld * _PixelsPerUnit) + 0.5) / _PixelsPerUnit;
                float inside = step(distance(cell, _FlashCenter.xy), _FlashRadius);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), saturate(inside * _FlashStrength));
                return color;
            }
            ENDHLSL
        }
    }
}
