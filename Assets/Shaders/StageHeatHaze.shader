Shader "UI/Stage Heat Haze"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Shimmer Color", Color) = (1, 0.72, 0.26, 0.5)
        _Intensity ("Intensity", Range(0, 1)) = 0.9
        _Speed ("Speed", Range(0, 5)) = 1.8
        _BandScale ("Shimmer Scale", Range(1, 32)) = 12
        [HideInInspector] _UnscaledTime ("Unscaled Time", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma target 3.0
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Intensity;
                float _Speed;
                float _BandScale;
                float _UnscaledTime;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float time = _UnscaledTime * _Speed;

                // High-contrast, wavy vertical shimmer suggests refraction from hot air.
                float flowA = sin(input.uv.x * _BandScale * 2.0
                    + sin(input.uv.y * 10.0 + time * 1.8) * 1.7
                    + time * 1.2);
                float flowB = sin(input.uv.x * _BandScale * 0.75
                    - input.uv.y * 7.0 + time * 0.85 + 1.4);
                float shimmer = saturate(0.5 + flowA * 0.3 + flowB * 0.2);
                float alpha = (0.3 + shimmer * 0.7) * _Intensity * _Color.a * sprite.a;

                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
