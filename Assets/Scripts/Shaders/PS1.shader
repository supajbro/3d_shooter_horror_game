Shader "PS1/RetroSurface"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _SnapStrength ("Vertex Snap", Float) = 120
        _ColorDepth ("Color Depth", Float) = 32
        _LightSteps ("Light Steps", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float4 _Color;

            float _SnapStrength;
            float _ColorDepth;
            float _LightSteps;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float lighting : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float4 clipPos = UnityObjectToClipPos(v.vertex);

                float2 snapped =
                    floor((clipPos.xy / clipPos.w) * _SnapStrength)
                    / _SnapStrength;

                clipPos.xy = snapped * clipPos.w;

                o.pos = clipPos;

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float3 worldNormal =
                    UnityObjectToWorldNormal(v.normal);

                float3 lightDir =
                    normalize(_WorldSpaceLightPos0.xyz);

                float NdotL =
                    saturate(dot(worldNormal, lightDir));

                NdotL =
                    floor(NdotL * _LightSteps)
                    / _LightSteps;

                o.lighting = NdotL;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                fixed4 col = tex * _Color;

                col.rgb *= lerp(0.25, 1.0, i.lighting);

                col.rgb =
                    floor(col.rgb * _ColorDepth)
                    / _ColorDepth;

                return col;
            }

            ENDCG
        }
    }
}