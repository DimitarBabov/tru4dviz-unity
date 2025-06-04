Shader "Custom/TerrainShader"
{
    Properties
    {
        _HeightScale ("Height Scale", Float) = 10.0
        _BaseHeight ("Base Height", Float) = 0.0
        
        // Level textures (up to 10)
        _LevelTexture0 ("Level 0 Texture", 2D) = "white" {}
        _LevelTexture1 ("Level 1 Texture", 2D) = "white" {}
        _LevelTexture2 ("Level 2 Texture", 2D) = "white" {}
        _LevelTexture3 ("Level 3 Texture", 2D) = "white" {}
        _LevelTexture4 ("Level 4 Texture", 2D) = "white" {}
        _LevelTexture5 ("Level 5 Texture", 2D) = "white" {}
        _LevelTexture6 ("Level 6 Texture", 2D) = "white" {}
        _LevelTexture7 ("Level 7 Texture", 2D) = "white" {}
        _LevelTexture8 ("Level 8 Texture", 2D) = "white" {}
        _LevelTexture9 ("Level 9 Texture", 2D) = "white" {}
        
        // Terrain colors
        _TerrainColor ("Terrain Color", Color) = (0.5, 0.5, 0.5, 1)
        _SolidColor ("Solid Color", Color) = (0.8, 0.6, 0.4, 1)
        
        // Visual settings
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        
        // Make the shader double-sided so it can be viewed from below
        Cull Off
        
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        
        // Level textures
        sampler2D _LevelTexture0, _LevelTexture1, _LevelTexture2, _LevelTexture3, _LevelTexture4;
        sampler2D _LevelTexture5, _LevelTexture6, _LevelTexture7, _LevelTexture8, _LevelTexture9;
        
        // Terrain properties
        float _HeightScale;
        float _BaseHeight;
        float4 _TerrainSize;
        float3 _TerrainCenter;
        
        // Level altitudes
        float3 _LevelAltitudes0; // Levels 0, 1, 2
        float3 _LevelAltitudes1; // Levels 3, 4, 5
        float3 _LevelAltitudes2; // Levels 6, 7, 8
        float _LevelAltitude9;   // Level 9
        int _LevelCount;
        
        // Colors
        fixed4 _TerrainColor;
        fixed4 _SolidColor;
        half _Glossiness;
        half _Metallic;
        
        struct Input
        {
            float2 uv_LevelTexture0;
            float3 worldPos;
            float terrainHeight;
        };
        
        // Function to get level altitude by index
        float GetLevelAltitude(int index)
        {
            if (index == 0) return _LevelAltitudes0.x;
            if (index == 1) return _LevelAltitudes0.y;
            if (index == 2) return _LevelAltitudes0.z;
            if (index == 3) return _LevelAltitudes1.x;
            if (index == 4) return _LevelAltitudes1.y;
            if (index == 5) return _LevelAltitudes1.z;
            if (index == 6) return _LevelAltitudes2.x;
            if (index == 7) return _LevelAltitudes2.y;
            if (index == 8) return _LevelAltitudes2.z;
            if (index == 9) return _LevelAltitude9;
            return 0.0;
        }
        
        // Function to sample level texture by index
        float4 SampleLevelTexture(int index, float2 uv)
        {
            // Flip UV around X axis (flip Y coordinate)
            float2 flippedUV = float2(uv.x, 1.0 - uv.y);
            
            if (index == 0) return tex2Dlod(_LevelTexture0, float4(flippedUV, 0, 0));
            if (index == 1) return tex2Dlod(_LevelTexture1, float4(flippedUV, 0, 0));
            if (index == 2) return tex2Dlod(_LevelTexture2, float4(flippedUV, 0, 0));
            if (index == 3) return tex2Dlod(_LevelTexture3, float4(flippedUV, 0, 0));
            if (index == 4) return tex2Dlod(_LevelTexture4, float4(flippedUV, 0, 0));
            if (index == 5) return tex2Dlod(_LevelTexture5, float4(flippedUV, 0, 0));
            if (index == 6) return tex2Dlod(_LevelTexture6, float4(flippedUV, 0, 0));
            if (index == 7) return tex2Dlod(_LevelTexture7, float4(flippedUV, 0, 0));
            if (index == 8) return tex2Dlod(_LevelTexture8, float4(flippedUV, 0, 0));
            if (index == 9) return tex2Dlod(_LevelTexture9, float4(flippedUV, 0, 0));
            return float4(1, 1, 1, 1); // Default white
        }
        
        // Function to check if a pixel represents solid/missing data
        bool IsSolidPixel(float4 color)
        {
            // White pixels (RGBA = 1,1,1,1) represent missing/solid data
            return (color.r >= 0.99 && color.g >= 0.99 && color.b >= 0.99 && color.a >= 0.99);
        }
        
        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            
            float2 uv = v.texcoord.xy;
            float terrainHeight = _BaseHeight;
            
            // Find the topmost solid level (iterate from highest to lowest)
            for (int i = _LevelCount - 1; i >= 0; i--)
            {
                float4 levelColor = SampleLevelTexture(i, uv);
                
                // If this level has solid data (white pixel), use its altitude
                if (IsSolidPixel(levelColor))
                {
                    float levelAltitude = GetLevelAltitude(i);
                    terrainHeight = _BaseHeight + levelAltitude * _HeightScale;
                    break; // Use the first (topmost) solid level found
                }
            }
            
            // Apply height to vertex
            v.vertex.y = terrainHeight;
            o.terrainHeight = terrainHeight;
            
            // Recalculate normal based on neighboring heights (simplified)
            float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            
            // Sample neighboring points for normal calculation
            float offset = 1.0 / 256.0; // Adjust based on mesh resolution
            
            float heightL = _BaseHeight;
            float heightR = _BaseHeight;
            float heightU = _BaseHeight;
            float heightD = _BaseHeight;
            
            // Sample left neighbor
            float2 uvL = uv + float2(-offset, 0);
            if (uvL.x >= 0.0)
            {
                for (int j = _LevelCount - 1; j >= 0; j--)
                {
                    float4 colorL = SampleLevelTexture(j, uvL);
                    if (IsSolidPixel(colorL))
                    {
                        heightL = _BaseHeight + GetLevelAltitude(j) * _HeightScale;
                        break;
                    }
                }
            }
            
            // Sample right neighbor
            float2 uvR = uv + float2(offset, 0);
            if (uvR.x <= 1.0)
            {
                for (int k = _LevelCount - 1; k >= 0; k--)
                {
                    float4 colorR = SampleLevelTexture(k, uvR);
                    if (IsSolidPixel(colorR))
                    {
                        heightR = _BaseHeight + GetLevelAltitude(k) * _HeightScale;
                        break;
                    }
                }
            }
            
            // Sample up neighbor
            float2 uvU = uv + float2(0, offset);
            if (uvU.y <= 1.0)
            {
                for (int l = _LevelCount - 1; l >= 0; l--)
                {
                    float4 colorU = SampleLevelTexture(l, uvU);
                    if (IsSolidPixel(colorU))
                    {
                        heightU = _BaseHeight + GetLevelAltitude(l) * _HeightScale;
                        break;
                    }
                }
            }
            
            // Sample down neighbor
            float2 uvD = uv + float2(0, -offset);
            if (uvD.y >= 0.0)
            {
                for (int m = _LevelCount - 1; m >= 0; m--)
                {
                    float4 colorD = SampleLevelTexture(m, uvD);
                    if (IsSolidPixel(colorD))
                    {
                        heightD = _BaseHeight + GetLevelAltitude(m) * _HeightScale;
                        break;
                    }
                }
            }
            
            // Calculate normal from height differences
            float3 tangentX = float3(offset * _TerrainSize.x, heightR - heightL, 0);
            float3 tangentZ = float3(0, heightU - heightD, offset * _TerrainSize.y);
            float3 normal = normalize(cross(tangentZ, tangentX));
            
            v.normal = normal;
        }
        
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_LevelTexture0;
            
            // Find the topmost solid level (iterate from highest to lowest)
            bool hasSolidData = false;
            float topLevel = -1;
            
            for (int i = _LevelCount - 1; i >= 0; i--)
            {
                float4 levelColor = SampleLevelTexture(i, uv);
                if (IsSolidPixel(levelColor))
                {
                    hasSolidData = true;
                    topLevel = i;
                    break; // Use the first (topmost) solid level found
                }
            }
            
            // Color based on whether there's solid data
            if (hasSolidData)
            {
                // Use solid color with slight variation based on top level
                float levelVariation = topLevel / max(1.0, _LevelCount - 1.0);
                fixed4 color = lerp(_SolidColor, _SolidColor * 1.2, levelVariation);
                o.Albedo = color.rgb;
            }
            else
            {
                // Use terrain color for areas without solid data
                o.Albedo = _TerrainColor.rgb;
            }
            
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        
        ENDCG
    }
    
    FallBack "Diffuse"
} 