Shader "SB6/VideoScreenFilter"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI texture", 2D) = "white" {}
        _Filter ("Filter", Float) = 0
        _Strength ("Intensity", Range(0, 1)) = 0.5
        _ReferenceSize ("Logical resolution", Vector) = (256, 224, 0, 0)
        _OutputSize ("Output resolution", Vector) = (1280, 1120, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Retro display overlay"
            Cull Off ZWrite Off ZTest Always
            Blend DstColor Zero
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _Filter;
                half _Strength;
                float4 _ReferenceSize;
                float4 _OutputSize;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 logical = input.uv * _ReferenceSize.xy;
                float2 scale = _OutputSize.xy / max(_ReferenceSize.xy, 1.0);
                // Fade repeating patterns near native resolution to avoid moire at small sizes.
                half visible = saturate((min(scale.x, scale.y) - 1.0) * 0.5);
                half strength = saturate(_Strength) * visible;
                half3 mask = 1.0h;
                if (_Filter > 0.5 && _Filter < 3.5)
                {
                    half scanline = 0.5h + 0.5h * cos(logical.y * 6.2831853);
                    mask *= 1.0h - scanline * strength * 0.36h;
                    if (_Filter > 1.5)
                    {
                        float2 centered = input.uv * 2.0 - 1.0;
                        half edge = saturate(dot(centered * centered, centered * centered) * 0.3);
                        mask *= 1.0h - edge * saturate(_Strength) * 0.25h;
                    }
                    if (_Filter > 2.5)
                    {
                        // RGB aperture grille in physical pixels; never a resampled mask texture.
                        float column = fmod(floor(input.uv.x * _OutputSize.x), 3.0);
                        half3 phosphor = half3(column < 0.5, column >= 0.5 && column < 1.5, column >= 1.5);
                        mask *= lerp(half3(1,1,1), 0.78h + 0.22h * phosphor, strength);
                    }
                }
                else if (_Filter > 3.5)
                {
                    float2 cell = abs(frac(logical) - 0.5);
                    float2 antialias = max(fwidth(logical), 0.001) * 0.5;
                    float2 edges = smoothstep(0.44 - antialias, 0.49 + antialias, cell);
                    half grid = max(edges.x, edges.y);
                    mask *= 1.0h - grid * strength * 0.38h;
                }
                return half4(mask, 1.0h);
            }
            ENDHLSL
        }
    }
}
