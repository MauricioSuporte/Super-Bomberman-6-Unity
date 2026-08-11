Shader "Hidden/Stage Water Band Ripple"
{
    Properties
    {
        _BandHeightPixels ("Line Spacing (Pixels)", Range(1, 64)) = 16
        _BandScrollSpeed ("Band Scroll Speed (Pixels / Second)", Range(0, 128)) = 24
        _BandsPerDirection ("Bands Per Direction", Range(1, 16)) = 5
        [HideInInspector] _LogicalPixelScale ("Logical Pixel Scale", Float) = 1
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
            Name "WaterBandRipple"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BandHeightPixels;
                float _BandScrollSpeed;
                float _BandsPerDirection;
                float _LogicalPixelScale;
                float _UnscaledTime;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenPixel = floor(input.texcoord / _BlitTexture_TexelSize.xy);
                int logicalPixelScale = max((int)round(_LogicalPixelScale), 1);
                int logicalPixelY = (int)screenPixel.y / logicalPixelScale;

                // Advance in whole screen pixels only. Each scan line travels from
                // the bottom to the top, with a new full-width line every 16px.
                int travelledPixels = (int)floor(_UnscaledTime * _BandScrollSpeed);
                int lineSpacingPixels = max((int)round(_BandHeightPixels), 1);
                int bandsPerDirection = max((int)round(_BandsPerDirection), 1);
                int lineRelativePixel = logicalPixelY - travelledPixels;
                int lineIndex = (int)floor((float)lineRelativePixel / lineSpacingPixels);
                int lineOffset = lineRelativePixel % lineSpacingPixels;
                if (lineOffset < 0)
                    lineOffset += lineSpacingPixels;

                int directionCycle = bandsPerDirection * 2;
                int cyclePosition = lineIndex % directionCycle;
                if (cyclePosition < 0)
                    cyclePosition += directionCycle;

                // Lines 0-4 move up; lines 5-9 move down. Only the current
                // one-pixel scan line changes, and its full width changes together.
                if (lineOffset == 0)
                {
                    float verticalDirection = cyclePosition < bandsPerDirection ? 1.0 : -1.0;
                    screenPixel.y = clamp(screenPixel.y - verticalDirection * logicalPixelScale, 0.0,
                                          _BlitTexture_TexelSize.w - 1.0);
                }

                // Sample exactly at the texel centre: no gradual interpolation and
                // no horizontal displacement.
                float2 sampleUv = (screenPixel + 0.5) * _BlitTexture_TexelSize.xy;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, sampleUv);
            }
            ENDHLSL
        }
    }
}
