Shader "SuperBomberman/Blackout Visible Parts"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _BarrelTex ("Barrel Occlusion Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
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
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 spritePosition : TEXCOORD1;
                float2 worldPosition : TEXCOORD4;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            TEXTURE2D(_BarrelTex);
            SAMPLER(sampler_BarrelTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _SpriteBounds;
                float4 _VisibleRegion;
                float4 _RoomBounds;
                int _VisibleColorCount;
                float4 _VisibleColors[8];
                float _ColorTolerance;
                float _BarrelOcclusion;
                float4x4 _BarrelWorldToLocal;
                float4 _BarrelBounds;
                float4 _BarrelUV;
                float4 _BarrelFlip;
                float _BarrelAlpha;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                float2 spritePosition = (input.positionOS.xy - _SpriteBounds.xy) / max(_SpriteBounds.zw, 0.0001);
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                output.spritePosition = spritePosition;
                output.worldPosition = TransformObjectToWorld(input.positionOS).xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.spritePosition - _VisibleRegion.xy);
                clip(_VisibleRegion.zw - input.spritePosition);
                clip(input.worldPosition - _RoomBounds.xy);
                clip(_RoomBounds.zw - input.worldPosition);
                half4 raw = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float matches = _VisibleColorCount == 0 ? 1 : 0;
                for (int i = 0; i < _VisibleColorCount; i++)
                {
                    float3 delta = abs(raw.rgb - _VisibleColors[i].rgb);
                    matches = max(matches, step(max(delta.r, max(delta.g, delta.b)), _ColorTolerance));
                }
                clip(matches - 0.5);
                if (_BarrelOcclusion > 0.5)
                {
                    // Suppress only the reveal overlay under opaque barrel pixels.
                    // The original barrel remains below darkness and keeps its lighting.
                    float2 local = mul(_BarrelWorldToLocal, float4(input.worldPosition, 0, 1)).xy * _BarrelFlip.xy;
                    float2 position = (local - _BarrelBounds.xy) / max(_BarrelBounds.zw, 0.0001);
                    if (all(position >= 0) && all(position <= 1))
                    {
                        float2 uv = lerp(_BarrelUV.xy, _BarrelUV.zw, position);
                        raw.a *= 1 - SAMPLE_TEXTURE2D(_BarrelTex, sampler_BarrelTex, uv).a * _BarrelAlpha;
                    }
                }
                return raw * input.color;
            }
            ENDHLSL
        }
    }
}
