Shader "SuperBomberman/World Blackout"
{
    Properties
    {
        _Color ("Darkness", Color) = (0, 0, 0, 0.92)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                int _SpotlightCount;
                float4 _SpotlightCenters[36];
                float4 _SpotlightHalfSize[36];
                float _SpotlightIntensity[36];
                float _ExplosionSoftness;
                int _PlayerCount;
                float4 _PlayerCircles[6];
                float _PlayerSoftness;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 worldPosition : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(world);
                output.worldPosition = world.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float light = 0;
                for (int p = 0; p < _PlayerCount; p++)
                {
                    float distanceToPlayer = distance(input.worldPosition, _PlayerCircles[p].xy);
                    float radius = _PlayerCircles[p].z;
                    light = max(light, 1 - smoothstep(radius, radius + max(_PlayerSoftness, 0.001), distanceToPlayer));
                }
                for (int i = 0; i < _SpotlightCount; i++)
                {
                    float2 delta = abs(input.worldPosition - _SpotlightCenters[i].xy) - _SpotlightHalfSize[i].xy;
                    float outsideDistance = length(max(delta, 0));
                    float hole = 1 - smoothstep(0, max(_ExplosionSoftness, 0.001), outsideDistance);
                    light = max(light, hole * _SpotlightIntensity[i]);
                }
                return half4(_Color.rgb, _Color.a * (1 - saturate(light)));
            }
            ENDHLSL
        }
    }
}
