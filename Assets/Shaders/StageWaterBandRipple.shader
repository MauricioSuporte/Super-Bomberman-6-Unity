Shader "Hidden/Stage Water Band Ripple"
{
    Properties
    {
        _BandHeightPixels ("Line Spacing (Pixels)", Range(1, 64)) = 16
        _BandScrollSpeed ("Band Scroll Speed (Pixels / Second)", Range(0, 128)) = 24
        _BandsPerDirection ("Accumulated Steps Per Direction", Range(1, 16)) = 5
        [HideInInspector] _LogicalPixelScale ("Logical Pixel Scale", Float) = 1
        [HideInInspector] _LogicalPixelOriginY ("Logical Pixel Origin Y", Float) = 0
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
                float _LogicalPixelOriginY;
                float _UnscaledTime;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenPixel = floor(input.texcoord / _BlitTexture_TexelSize.xy);
                float logicalPixelScale = max(round(_LogicalPixelScale), 1.0);
                float logicalPixelY = floor((screenPixel.y - _LogicalPixelOriginY) / logicalPixelScale);

                // Advance in whole logical pixels only. The invisible scan lines
                // enter through the bottom edge and travel to the top, one every
                // sixteen logical pixels. Only their full horizontal pixel row shifts.
                float travelledPixels = floor(_UnscaledTime * _BandScrollSpeed);
                float lineSpacingPixels = max(round(_BandHeightPixels), 1.0);
                float bandsPerDirection = max(round(_BandsPerDirection), 1.0);
                // A scan line changes one complete logical row at a time. Its
                // result remains on that row until the next scan line reaches it.
                // The value is a stepped triangle: 0, 1, 2, 3, 4, 5, 4, ... 1, 0.
                float passedLineCount = floor((travelledPixels - logicalPixelY) /
                                              lineSpacingPixels);
                float directionCycle = bandsPerDirection * 2.0;
                float cyclePosition = passedLineCount -
                                      floor(passedLineCount / directionCycle) * directionCycle;

                float accumulatedOffset = cyclePosition <= bandsPerDirection
                    ? cyclePosition
                    : directionCycle - cyclePosition;

                // Sample left by the accumulated offset so the completed row is
                // visibly shifted right. A post-process texture has no pixels
                // outside the camera's left edge. Clamping a negative source X to
                // zero made that first source column stretch across the exposed
                // area. Preserve the already visible target columns there instead;
                // this avoids inventing a repeated edge pixel until an overscan
                // camera render is available.
                float shiftedSourceX = screenPixel.x - accumulatedOffset * logicalPixelScale;
                screenPixel.x = shiftedSourceX >= 0.0 ? shiftedSourceX : screenPixel.x;

                // Re-sample every physical row of this logical SNES row from the
                // same source row. At 5x, for example, all five physical rows are
                // therefore displaced together in one atomic pixel-art step.
                screenPixel.y = _LogicalPixelOriginY + logicalPixelY * logicalPixelScale +
                                floor((logicalPixelScale - 1.0) * 0.5);

                // Sample exactly at the texel centre: no gradual interpolation and
                // no interpolation between physical screen pixels.
                float2 sampleUv = (screenPixel + 0.5) * _BlitTexture_TexelSize.xy;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, sampleUv);
            }
            ENDHLSL
        }
    }
}
