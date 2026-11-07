Shader "Hidden/SB6/VideoBackgroundBlur"
{
    Properties
    {
        _MainTex ("Captured game", 2D) = "white" {}
        _BlurDirection ("Blur direction", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            float4 _BlurDirection;
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = 0;
                float weightSum = 0;
                // Nine adjacent Gaussian taps; runtime alternates horizontal/vertical passes.
                for (int tap = -4; tap <= 4; tap++)
                {
                    float weight = exp(-float(tap * tap) / 8.0);
                    float2 uv = saturate(input.uv + _BlurDirection.xy * _MainTex_TexelSize.xy * tap);
                    color += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb * weight;
                    weightSum += weight;
                }
                return half4(color / weightSum, 1);
            }
            ENDHLSL
        }
    }
}
