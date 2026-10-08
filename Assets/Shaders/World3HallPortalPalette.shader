Shader "SuperBomberman/World3 Hall Portal Palette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _RevealY ("Reveal lower world Y", Float) = -100000
        [HideInInspector] _RevealMode ("Reveal mode: portal or beam", Float) = 0
        [HideInInspector] _BeamMaskEnabled ("Use portal pixel mask", Float) = 0
        [HideInInspector] _PortalMaskTex ("Portal pixel mask", 2D) = "white" {}
        [HideInInspector] _PortalWorldRect ("Portal world rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _PortalUVRect ("Portal texture rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _PortalBaseMaskHeight ("Portal ground outline height", Float) = 0
        [HideInInspector] _RenderWorldRect ("Rendered sprite world rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _RenderUVRect ("Rendered sprite texture rectangle", Vector) = (0,0,1,1)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RevealY;
                float _RevealMode;
                float _BeamMaskEnabled;
                float4 _PortalWorldRect;
                float4 _PortalUVRect;
                float _PortalBaseMaskHeight;
                float4 _RenderWorldRect;
                float4 _RenderUVRect;
            CBUFFER_END
            TEXTURE2D(_PortalMaskTex);
            SAMPLER(sampler_PortalMaskTex);
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Reconstruct from sprite UVs: SpriteRenderer batching must not change
                // the coordinate system used by the portal and the taller beam.
                float2 spriteUV = (input.uv - _RenderUVRect.xy) / _RenderUVRect.zw;
                float2 worldXY = _RenderWorldRect.xy + spriteUV * _RenderWorldRect.zw;
                // Both renderers use the same pixel-row boundary and opposite tests.
                // A row belongs exclusively to the portal or to the beam.
                if (_RevealMode > 0.5)
                {
                    float row = floor((worldXY.y - _PortalWorldRect.y) * 16.0 + 0.0001);
                    float cutoffRow = floor((_RevealY - _PortalWorldRect.y) * 16.0 + 0.0001);
                    bool portalRow = row >= cutoffRow;
                    if ((_RevealMode < 1.5 && !portalRow) || (_RevealMode >= 1.5 && portalRow))
                        discard;
                }
                half4 raw = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // Only the footprint follows sprite alpha. The portal's jagged,
                // transparent upper edge must not punch holes in the solid beam.
                if (_BeamMaskEnabled > 0.5 && worldXY.y < _PortalWorldRect.y + _PortalBaseMaskHeight)
                {
                    float2 portalUV = (worldXY - _PortalWorldRect.xy) / _PortalWorldRect.zw;
                    clip(portalUV);
                    clip(1.0 - portalUV);
                    half portalAlpha = SAMPLE_TEXTURE2D(_PortalMaskTex, sampler_PortalMaskTex,
                        _PortalUVRect.xy + portalUV * _PortalUVRect.zw).a;
                    raw.a *= step(0.001, portalAlpha);
                }
                // The source palette runs from magenta to white. Its green channel
                // retains each highlight level when replacing magenta with a theme.
                half3 themed = lerp(input.color.rgb, half3(1,1,1), raw.g) * raw.r;
                return half4(themed, raw.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
