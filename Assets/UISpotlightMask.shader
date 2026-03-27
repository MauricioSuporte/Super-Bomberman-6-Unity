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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_SPOTLIGHTS 16

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;
            float4 _Center;
            float _Radius;
            float _Softness;
            float _EllipseX;
            float _EllipseY;

            int _SpotlightCount;
            float4 _SpotlightCenters[MAX_SPOTLIGHTS];
            float4 _SpotlightHalfSize[MAX_SPOTLIGHTS];
            float _SpotlightSoftness[MAX_SPOTLIGHTS];

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            float ComputeCircularHole(float2 uv, float2 center, float radius, float softness, float ellipseX, float ellipseY)
            {
                float2 d = uv - center;

                float ex = max(ellipseX, 1e-5);
                float ey = max(ellipseY, 1e-5);

                d.x *= ex;
                d.y *= ey;

                float dist = length(d);
                float inner = radius;
                float outer = radius + max(softness, 1e-5);

                return 1.0 - smoothstep(inner, outer, dist);
            }

            float ComputeBoxHole(float2 uv, float2 center, float2 halfSize, float softness)
            {
                float2 q = abs(uv - center) - halfSize;

                float outside = length(max(q, 0.0));
                float inside = min(max(q.x, q.y), 0.0);
                float dist = outside + inside;

                return 1.0 - smoothstep(0.0, max(softness, 1e-5), dist);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                float hole = 0.0;

                hole = max(hole, ComputeCircularHole(i.uv, _Center.xy, _Radius, _Softness, _EllipseX, _EllipseY));

                [unroll]
                for (int idx = 0; idx < MAX_SPOTLIGHTS; idx++)
                {
                    if (idx >= _SpotlightCount)
                        break;

                    hole = max(
                        hole,
                        ComputeBoxHole(
                            i.uv,
                            _SpotlightCenters[idx].xy,
                            _SpotlightHalfSize[idx].xy,
                            _SpotlightSoftness[idx]));
                }

                fixed4 col = _Color;
                col.a *= (1.0 - hole);
                col.a *= baseCol.a;

                return col;
            }
            ENDCG
        }
    }
}