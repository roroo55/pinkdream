Shader "PinkDream/StartItemArtMagnifier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _LensTex ("Existing magnifier camera", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer tint", Color) = (1,1,1,1)
        _ApertureRadii ("Artwork opening radii in sprite UV", Vector) = (0.4,0.4,0,0)
        _ApertureAngle ("Artwork rotation in degrees", Float) = 0
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
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            TEXTURE2D(_LensTex); SAMPLER(sampler_LensTex);
            float4 _Color, _RendererColor, _LensTex_TexelSize, _ApertureRadii;
            float _ApertureAngle;
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv; output.color=input.color*_Color*_RendererColor; return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 offset=input.uv-0.5;
                float angle=radians(_ApertureAngle); float s=sin(angle),c=cos(angle);
                float2 aligned=float2(c*offset.x+s*offset.y,-s*offset.x+c*offset.y);
                float radius=length(aligned/max(_ApertureRadii.xy,0.001)); clip(1-radius);
                // Sample the original full-size UV. The camera field of view and
                // magnification stay unchanged; only the visible opening is masked.
                float2 scanUV=input.uv;
                #if UNITY_UV_STARTS_AT_TOP
                if(_LensTex_TexelSize.y<0)scanUV.y=1-scanUV.y;
                #endif
                half4 color=SAMPLE_TEXTURE2D(_LensTex,sampler_LensTex,scanUV)*input.color;
                color.a*=1-smoothstep(0.985,1,radius); return color;
            }
            ENDHLSL
        }
    }
}
