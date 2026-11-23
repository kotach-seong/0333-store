// 설정의 밝기(ScreenBrightness): 화면을 덮는 막. 블렌드 방식은 머티리얼 값으로 바꾼다(곱하기 / 밝히기).
// 출력 색은 _Value 그대로(선형 값, 색 변환 없음).
Shader "Night0333/ScreenTint"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Value ("Value", Vector) = (1, 1, 1, 1)
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 2
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend [_SrcBlend] [_DstBlend]
        ColorMask RGB

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Value;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(_Value.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
