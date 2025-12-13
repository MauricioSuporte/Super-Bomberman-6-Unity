Shader "UI/SpotlightMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0,0,0,0.8)
        _Center ("Center (UV 0-1)", Vector) = (0.5,0.5,0,0)
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * i.color;

                float2 d = i.uv - _Center.xy;

                float ex = max(_EllipseX, 1e-5);
                float ey = max(_EllipseY, 1e-5);
                d.x *= ex;
                d.y *= ey;

                float dist = length(d);

                float inner = _Radius;
                float outer = _Radius + max(_Softness, 1e-5);

                float a = smoothstep(inner, outer, dist);

                fixed4 col = _Color;
                col.a *= a;

                col.a *= baseCol.a;

                return col;
            }
            ENDCG
        }
    }
}
