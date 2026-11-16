Shader "Chrono/DrawImageFx"
{
    Properties
    {
        _MainTex ("Draw FX Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0,1)) = 1
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
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Opacity;
            CBUFFER_END

            Varyings DrawVertex(Attributes input)
            {
                // 기존 URP2D 텍스처·정렬·개체 색을 그대로 유지
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 DrawFragment(Varyings input) : SV_Target
            {
                // R04 원본 색과 광륜은 유지하고 경고·입자 전환에서 알파만 변경
                half4 color = CommonUnlitFragment(input, input.color);
                color.a *= _Opacity;
                return color;
            }
            ENDHLSL
        }
    }
}
