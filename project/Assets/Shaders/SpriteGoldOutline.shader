Shader "Chrono/SpriteGoldOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline color", Color) = (1,0.831,0.369,1)
        _SkinColor ("Skin color (alpha = strength)", Color) = (1,0.78,0.31,0.36)
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
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
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
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                half4 _SkinColor;
            CBUFFER_END
            Varyings OutlineVertex(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);

                // 상하좌우 이웃 중 하나라도 그림이 있으면 빈 칸을 테두리로 채움
                float2 texel = _MainTex_TexelSize.xy;
                half left = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(texel.x, 0)).a;
                half right = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texel.x, 0)).a;
                half down = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, texel.y)).a;
                half up = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, texel.y)).a;
                half neighbor = max(max(left, right), max(down, up));
                half body = step(0.01, color.a);
                half rim = (1 - body) * step(0.01, neighbor);

                // 몸 안쪽은 반투명 금빛을 덧입히고 바깥 1px는 금색 테두리로 표시
                color.rgb = lerp(color.rgb, _SkinColor.rgb, _SkinColor.a * body);
                color = lerp(color, half4(_OutlineColor.rgb, _OutlineColor.a * input.color.a), rim);
                return color;
            }
            ENDHLSL
        }
    }
}
