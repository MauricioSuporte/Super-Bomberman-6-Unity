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
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float worldY : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float _WaterSurfaceY;
            float _SurfaceLineHeight;
            fixed4 _UnderwaterTint;
            float _UnderwaterStrength;
            float _UnderwaterOpacity;
            fixed4 _SurfaceTint;
            float _SurfaceOpacity;

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                output.worldY = mul(unity_ObjectToWorld, input.vertex).y;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.texcoord) * input.color;
                float alpha = source.a;

                // Pixels above the surface must use the same premultiplied
                // output as the original sprite, without any water treatment.
                if (input.worldY >= _WaterSurfaceY)
                {
                    source.rgb *= alpha;
                    return source;
                }

                // Only pixels below the surface receive the blue tint and
                // transparency. The final two pixels form the waterline.
                float lineHeight = max(_SurfaceLineHeight, 0.0001);
                float surfaceLine = smoothstep(_WaterSurfaceY - lineHeight, _WaterSurfaceY, input.worldY);

                float tintStrength = lerp(_UnderwaterStrength, 1.0, surfaceLine);
                fixed3 targetTint = lerp(_UnderwaterTint.rgb, _SurfaceTint.rgb, surfaceLine);
                fixed3 rgb = lerp(source.rgb, targetTint, tintStrength);
                float opacity = lerp(_UnderwaterOpacity, _SurfaceOpacity, surfaceLine);

                source.rgb = rgb * alpha;
                source.a = alpha * opacity;
                return source;
            }
            ENDCG
        }
    }
}
