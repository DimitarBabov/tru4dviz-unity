Shader "Custom/WindPathMeshRenderer"
{
    Properties
    {
        _Color0("Color0", Color) = (1,1,1,0)
        _Color1("Color1", Color) = (1,1,1,1)
        _Color2("Color2", Color) = (1,1,1,1)
        _Color3("Color3", Color) = (1,1,1,1)
        _Color4("Color4", Color) = (1,1,1,1)
        _Color5("Color5", Color) = (1,1,1,1)
        _AlfaCorrection("AlfaCorrection", float) = 0.75
        _LineWidth ("Line Width", Float) = 0.1
        _TextureAnimationSpeed ("Texture Animation Speed", Float) = 1.0
        _FlowScale ("Flow Scale", Float) = 3.0
        _FlowStrength ("Flow Strength", Float) = 0.8
        _EnableAnimation ("Enable Animation", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2: TEXCOORD1;
                float2 uv3: TEXCOORD2;
                float4 color : COLOR;
                float3 normal : NORMAL; // Line direction
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2: TEXCOORD1;
                float2 uv3: TEXCOORD2;
                float2 textureUV : TEXCOORD3; // Separate UV for texture sampling
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            fixed4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5;
            float _AlfaCorrection;
            float _LineWidth;
            float _TextureAnimationSpeed;
            float _FlowScale;
            float _FlowStrength;
            float _EnableAnimation;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Transform vertex to world space
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                // Get line direction from normal (already in object space)
                float3 lineDirection = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                
                // Get camera position
                float3 cameraPos = _WorldSpaceCameraPos;
                
                // Calculate vector from line point to camera
                float3 toCamera = normalize(cameraPos - worldPos);
                
                // Calculate perpendicular vector (billboard direction)
                float3 perpendicular = normalize(cross(lineDirection, toCamera));
                
                // Use UV.y to determine which side of the line this vertex is on
                // UV.y = 0 means one side, UV.y = 1 means other side
                float side = (v.uv.y - 0.5) * 2.0; // Convert 0-1 to -1 to 1
                
                // Offset the vertex position
                float3 offset = perpendicular * side * _LineWidth * 0.5;
                worldPos += offset;
                
                // Transform to clip space
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                
                // Pass through original UVs for billboard positioning and data
                o.uv = v.uv;
                o.uv2 = v.uv2;
                o.uv3 = v.uv3;
                
                // Calculate separate texture UV coordinates
                // Use UV.x for position along the line, and center the texture across the width
                float textureU = v.uv.x; // Position along the line (0 to 1)
                float textureV = 0.5; // Always sample from center of texture (avoid billboard UV conflict)
                
                // Add animation based on time and magnitude (only if animation is enabled)
                if (_EnableAnimation > 0.5)
                {
                    float avgMagNorm = v.uv2.y; // Get average magnitude normalization from UV2.y
                    float animationOffset = _Time.y * _TextureAnimationSpeed * avgMagNorm;
                    textureU += animationOffset;
                }
                
                // Store animation UV coordinates (no texture transform needed)
                o.textureUV = float2(textureU, textureV);
                
                o.color = v.color;
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Create procedural dissolved circle effect
                // Use UV.y for radial distance from center (0 = center, 0.5 = edge)
                float distanceFromCenter = abs(i.uv.y - 0.5) * 2.0; // 0 at center, 1 at edges
                
                // Create base circle with soft edges (static, no animation)
                float circleRadius = 0.8; // Adjust this to control circle size
                float softness = 0.3; // Adjust this to control edge softness
                float circleAlpha = 1.0 - smoothstep(circleRadius - softness, circleRadius + softness, distanceFromCenter);
                
                float proceduralAlpha = circleAlpha;
                
                // Add animated flowing interruptions along the path (only if animation is enabled)
                if (_EnableAnimation > 0.5)
                {
                    // Calculate animation offset (same as texture animation)
                    float avgMagNorm = i.uv2.y; // Get average magnitude normalization from UV2.y
                    float animationOffset = _Time.y * _TextureAnimationSpeed * avgMagNorm;
                    
                    float flowScale = _FlowScale; // Scale for the flowing pattern (controllable)
                    float flowStrength = _FlowStrength; // Strength of the interruption effect (controllable)
                    
                    // Create flowing interruption pattern that moves along the streamline
                    float flowU = i.textureUV.x + animationOffset; // Animated position along line
                    float flowPattern = sin(flowU * flowScale * 6.28318) * 0.5 + 0.5; // Sine wave pattern
                    
                    // Add secondary wave for more complex interruptions
                    float flowPattern2 = sin(flowU * flowScale * 2.0 * 6.28318 + 1.57) * 0.3 + 0.7;
                    
                    // Combine patterns to create interruptions
                    float combinedFlow = flowPattern * flowPattern2;
                    
                    // Apply flowing interruptions only along the path direction
                    // Don't affect the radial circle, just create gaps along the length
                    float pathAlpha = combinedFlow; // This creates the flowing interruptions
                    
                    // Combine circle (radial) and path interruptions (longitudinal)
                    proceduralAlpha = circleAlpha * pathAlpha;
                }
                
                // Calculate gradient color based on magnitude (independent of procedural effect)
                float h = i.uv2.x;
                float x;
                fixed4 gradientColor = fixed4(1, 1, 1, 1);
                
                if (h >= 0.0 && h <= 0.2)
                {
                    x = (h - 0.0) * 5;
                    gradientColor = (1 - x) * _Color0 + x * _Color1;
                }
                else if (h > 0.2 && h <= 0.4)
                {
                    x = (h - 0.2) * 5;
                    gradientColor = (1 - x) * _Color1 + x * _Color2;
                }
                else if (h > 0.4 && h <= 0.6)
                {
                    x = (h - 0.4) * 5;
                    gradientColor = (1 - x) * _Color2 + x * _Color3;
                }
                else if (h > 0.6 && h <= 0.8)
                {
                    x = (h - 0.6) * 5;
                    gradientColor = (1 - x) * _Color3 + x * _Color4;
                }
                else if (h > 0.8 && h <= 1)
                {
                    x = (h - 0.8) * 5;
                    gradientColor = (1 - x) * _Color4 + x * _Color5;
                }
                
                // Use gradient color for RGB, procedural alpha for transparency
                fixed4 col = gradientColor;
                col.a *= proceduralAlpha; // Apply procedural dissolved circle alpha
                
                // Apply base color and alpha correction
                col *= i.color;
                col.a *= _AlfaCorrection;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
} 