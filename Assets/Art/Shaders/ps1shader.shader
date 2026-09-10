Shader "Custom/PS1/Lit"
{
    Properties
    {
        [Header(Main)]
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        [Header(PS1 Geometry)]
        _VertexSnap ("Vertex Snap", Range(0, 0.1)) = 0.02

        [Header(PS1 Texture)]
        _UVSnap ("Texture Pixelation", Range(0, 512)) = 0

        [Header(PS1 Lighting)]
        _LightSteps ("Lighting Steps", Range(1, 16)) = 4
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.35
        _LightContrast ("Light Contrast", Range(0, 2)) = 1

        [Header(PS1 Colour)]
        _ColorSteps ("Color Steps", Range(1, 32)) = 8

        [Header(Surface)]
        _Emission ("Emission", Color) = (0,0,0,1)
        _EmissionStrength ("Emission Strength", Range(0, 5)) = 0

        [Header(Fog)]
        _FogStrength ("Fog Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        LOD 200

        CGPROGRAM

        #pragma surface surf PS1 fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;

        fixed4 _Color;
        fixed4 _Emission;

        half _VertexSnap;
        half _UVSnap;

        half _LightSteps;
        half _AmbientStrength;
        half _LightContrast;

        half _ColorSteps;

        half _EmissionStrength;
        half _FogStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };


        // ---------------------------------------------------------
        // PS1 VERTEX SNAPPING
        // ---------------------------------------------------------

        void vert(inout appdata_full v)
        {
            if (_VertexSnap > 0.0001)
            {
                // Convert vertex position to world space.
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Snap world position to a low-resolution grid.
                worldPosition = floor(
                    worldPosition / _VertexSnap + 0.5
                ) * _VertexSnap;

                // Convert back to object space.
                v.vertex = mul(
                    unity_WorldToObject,
                    float4(worldPosition, 1.0)
                );
            }
        }


        // ---------------------------------------------------------
        // PS1 QUANTIZATION
        // ---------------------------------------------------------

        half Quantize(half value, half steps)
        {
            if (steps <= 1)
                return value;

            value = saturate(value);

            return floor(value * steps) / steps;
        }


        half3 QuantizeColor(half3 color, half steps)
        {
            if (steps <= 1)
                return color;

            return floor(saturate(color) * steps) / steps;
        }


        // ---------------------------------------------------------
        // CUSTOM PS1 LIGHTING
        // ---------------------------------------------------------

        inline half4 LightingPS1(
            SurfaceOutput s,
            half3 lightDir,
            half atten
        )
        {
            // Normalized surface normal.
            half3 normal = normalize(s.Normal);

            // Basic Lambert lighting.
            half NdotL = dot(normal, lightDir);

            // Remove negative lighting.
            NdotL = saturate(NdotL);

            // Increase/decrease contrast.
            NdotL = saturate(
                pow(NdotL, max(0.01, _LightContrast))
            );

            // Turn smooth lighting into chunky PS1-style lighting.
            NdotL = Quantize(NdotL, _LightSteps);

            // Unity's light colour.
            half3 directLight = _LightColor0.rgb;

            // Apply attenuation.
            directLight *= atten;

            // Final direct lighting.
            half3 lighting = directLight * NdotL;

            // Surface colour.
            half3 color = s.Albedo * lighting;

            // Add a small amount of ambient light.
            //
            // This is deliberately kept low so that dark areas
            // remain dark instead of looking like an unlit shader.
            half3 ambient = ShadeSH9(
                half4(normal, 1.0)
            );

            ambient *= _AmbientStrength;

            color += s.Albedo * ambient;

            // Emission.
            color += s.Emission;

            // PS1-style colour quantization.
            color = QuantizeColor(color, _ColorSteps);

            return half4(color, s.Alpha);
        }


        // ---------------------------------------------------------
        // SURFACE
        // ---------------------------------------------------------

        void surf(Input IN, inout SurfaceOutput o)
        {
            float2 uv = IN.uv_MainTex;

            // Optional UV pixelation.
            //
            // 0 = normal UVs
            // Higher values = increasingly low resolution texture
            // sampling.
            if (_UVSnap > 0.5)
            {
                uv = floor(uv * _UVSnap + 0.5) / _UVSnap;
            }

            fixed4 tex = tex2D(_MainTex, uv);

            o.Albedo = tex.rgb * _Color.rgb;

            o.Alpha = tex.a * _Color.a;

            o.Emission = _Emission.rgb * _EmissionStrength;

            o.Normal = half3(0, 0, 1);
        }

        ENDCG
    }

    FallBack "Diffuse"
}