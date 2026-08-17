Shader "Hidden/Stage Heat Distortion"
{
    Properties
    {
        _DistortionPixels ("Distortion (Pixels)", Range(0, 3)) = 0.75
        _Speed ("Speed", Range(0, 5)) = 1.8
        _WaveScale ("Wave Scale", Range(1, 32)) = 11
        [HideInInspector] _UnscaledTime ("Unscaled Time", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "HeatDistortion"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _DistortionPixels;
                float _Speed;
                float _WaveScale;
                float _UnscaledTime;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = _UnscaledTime * _Speed;
                float2 uv = input.texcoord;
                float verticalWave = sin(uv.y * _WaveScale * 6.2831853 + time * 1.7);
                float horizontalWave = sin(uv.x * (_WaveScale * 0.65) - time * 1.15);

                float2 offsetPixels = float2(
                    verticalWave * 0.75 + horizontalWave * 0.25,
                    sin(uv.x * _WaveScale * 0.45 + time * 0.8) * 0.18);
                float2 distortedUv = uv + offsetPixels * _DistortionPixels * _BlitTexture_TexelSize.xy;

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, distortedUv);
            }
            ENDHLSL
        }
    }
}
