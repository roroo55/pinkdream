Shader "PinkDream/UltrasoundMagnifier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _LensTex ("Ultrasound camera", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            TEXTURE2D(_LensTex);
            SAMPLER(sampler_LensTex);
            float4 _Color;
            float4 _RendererColor;
            float4 _LensTex_TexelSize;
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv - 0.5);
                clip(0.5 - radius);
                float2 scanUV = input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_LensTex_TexelSize.y < 0)
                    scanUV.y = 1 - scanUV.y;
                #endif
                half4 lensColor = SAMPLE_TEXTURE2D(_LensTex, sampler_LensTex, scanUV) * input.color;
                lensColor.a *= 1 - smoothstep(0.49, 0.5, radius);
                return lensColor;
            }
            ENDHLSL
        }
    }
}
