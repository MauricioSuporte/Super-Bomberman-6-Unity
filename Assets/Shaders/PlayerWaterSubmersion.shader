Shader "SuperBomberman/PlayerWaterSubmersion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WaterSurfaceY ("Water Surface World Y", Float) = 0.5
        _SurfaceLineHeight ("Surface Line Height", Float) = 0.125
        _UnderwaterTint ("Underwater Tint", Color) = (0.48,0.8,1,1)
        _UnderwaterStrength ("Underwater Tint Strength", Range(0,1)) = 0.55
        _UnderwaterOpacity ("Underwater Opacity", Range(0,1)) = 0.72
        _SurfaceTint ("Surface Line Tint", Color) = (0.18,0.72,1,1)
        _SurfaceOpacity ("Surface Line Opacity", Range(0,1)) = 0.72
        _UseSurfaceLine ("Use Surface Line", Float) = 1
        _PremultiplyUnderwaterOpacity ("Premultiply Underwater Opacity", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float worldY : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WaterSurfaceY;
                float _SurfaceLineHeight;
                half4 _UnderwaterTint;
                float _UnderwaterStrength;
                float _UnderwaterOpacity;
                half4 _SurfaceTint;
                float _SurfaceOpacity;
                float _UseSurfaceLine;
                float _PremultiplyUnderwaterOpacity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                output.worldY = TransformObjectToWorld(input.positionOS).y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float alpha = source.a;

                if (input.worldY >= _WaterSurfaceY)
                {
                    source.rgb *= alpha;
                    return source;
                }

                float lineHeight = max(_SurfaceLineHeight, 0.0001);
                float surfaceLine = _UseSurfaceLine > 0.5
                    ? smoothstep(_WaterSurfaceY - lineHeight, _WaterSurfaceY, input.worldY)
                    : 0.0;
                float tintStrength = lerp(_UnderwaterStrength, 1.0, surfaceLine);
                half3 targetTint = lerp(_UnderwaterTint.rgb, _SurfaceTint.rgb, surfaceLine);
                half3 rgb = lerp(source.rgb, targetTint, tintStrength);
                float opacity = lerp(_UnderwaterOpacity, _SurfaceOpacity, surfaceLine);
                float rgbOpacity = lerp(1.0, opacity, saturate(_PremultiplyUnderwaterOpacity));

                source.rgb = rgb * alpha * rgbOpacity;
                source.a = alpha * opacity;
                return source;
            }
            ENDHLSL
        }
    }
}
