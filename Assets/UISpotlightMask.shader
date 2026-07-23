Shader "UI/SpotlightMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,0.8)
        _Center ("Center (legacy)", Vector) = (0.5,0.5,0,0)
        _Radius ("Radius", Float) = 0.2
        _Softness ("Softness", Float) = 0.03
        _EllipseX ("Ellipse X", Float) = 1
        _EllipseY ("Ellipse Y", Float) = 1
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

            #define MAX_SPOTLIGHTS 36

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
                float4 _Center;
                float _Radius;
                float _Softness;
                float _EllipseX;
                float _EllipseY;
                int _SpotlightCount;
                float4 _SpotlightCenters[MAX_SPOTLIGHTS];
                float4 _SpotlightHalfSize[MAX_SPOTLIGHTS];
                float _SpotlightSoftness[MAX_SPOTLIGHTS];
                float _SpotlightIntensity[MAX_SPOTLIGHTS];
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

            float ComputeCircularHole(float2 uv, float2 center, float radius, float softness, float ellipseX, float ellipseY)
            {
                float2 d = uv - center;
                d.x *= max(ellipseX, 1e-5);
                d.y *= max(ellipseY, 1e-5);
                float outer = radius + max(softness, 1e-5);
                return 1.0 - smoothstep(radius, outer, length(d));
            }

            float ComputeBoxHole(float2 uv, float2 center, float2 halfSize, float softness)
            {
                float2 q = abs(uv - center) - halfSize;
                float outside = length(max(q, 0.0));
                float inside = min(max(q.x, q.y), 0.0);
                return 1.0 - smoothstep(0.0, max(softness, 1e-5), outside + inside);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                float hole = ComputeCircularHole(input.uv, _Center.xy, _Radius, _Softness, _EllipseX, _EllipseY);
                int count = min(_SpotlightCount, MAX_SPOTLIGHTS);

                for (int index = 0; index < count; index++)
                {
                    float boxHole = ComputeBoxHole(input.uv, _SpotlightCenters[index].xy,
                        _SpotlightHalfSize[index].xy, _SpotlightSoftness[index]);
                    hole = max(hole, boxHole * saturate(_SpotlightIntensity[index]));
                }

                half4 color = _Color;
                color.a *= (1.0 - hole) * baseColor.a;
                return color;
            }
            ENDHLSL
        }
    }
}
