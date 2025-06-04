Shader "Custom/missingdata"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        [Toggle] _UseVertexColors ("Use Vertex Colors", Float) = 1
        _VertexColorStrength ("Vertex Color Strength", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off  // This disables backface culling

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        
        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
            float4 color : COLOR; // Vertex colors
        };

        half _Metallic;
        half _Smoothness;
        fixed4 _Color;
        float _UseVertexColors;
        float _VertexColorStrength;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // Mix in vertex colors if enabled
            if (_UseVertexColors > 0.5)
            {
                c.rgb = lerp(c.rgb, c.rgb * IN.color.rgb, _VertexColorStrength);
            }
            
            o.Albedo = c.rgb;
            
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = c.a;
            
            // For double-sided lighting, flip normal for back faces
            if (IN.facing < 0.5)
                o.Normal *= -1;
        }
        ENDCG
    }
    FallBack "Diffuse"
} 