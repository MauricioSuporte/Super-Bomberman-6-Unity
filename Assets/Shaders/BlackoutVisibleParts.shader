Shader "SuperBomberman/Blackout Visible Parts"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
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
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _SpriteBounds;
                float4 _VisibleRegion;
                float4 _RoomBounds;
                int _VisibleColorCount;
                float4 _VisibleColors[8];
                float _ColorTolerance;
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
                return raw * input.color;
            }
            ENDHLSL
        }
    }
}
