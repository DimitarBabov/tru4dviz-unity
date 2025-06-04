Shader "Custom/WindStreamlineTexture"
{
    Properties
    {
        _Color0("Color0", Color) = (1,1,1,0)
        _Color1("Color1", Color) = (1,1,1,1)
        _Color2("Color2", Color) = (1,1,1,1)
        _Color3("Color3", Color) = (1,1,1,1)
        _Color4("Color4", Color) = (1,1,1,1)
        _Color5("Color5", Color) = (1,1,1,1)
        _AlfaCorrection("Alpha Correction", Range(0, 1)) = 0.75
        _LineWidth ("Line Width", Float) = 0.1
        _TextureAnimationSpeed ("Texture Animation Speed", Float) = 1.0
        _FlowTexture ("Flow Texture", 2D) = "white" {}
        _FlowTiling ("Flow Tiling", Float) = 1.0
        _BlackThreshold ("Black Threshold", Range(0, 1)) = 0.1
        _EnableAnimation ("Enable Animation", Float) = 1.0

        [Header(Visualization Trimming)]
        _MaxAltitude("Max Altitude", Range(0, 1)) = 1.0
        _MinAltitude("Min Altitude", Range(0, 1)) = 0.0
        _BoundsLeft("Bounds Left", Range(0, 1)) = 0.0
        _BoundsRight("Bounds Right", Range(0, 1)) = 1.0
        _BoundsFront("Bounds Front", Range(0, 1)) = 0.0
        _BoundsBack("Bounds Back", Range(0, 1)) = 1.0
        [Toggle] _EnableSpeedTrim("Enable Speed Trim", Float) = 0.0
        _SpeedTrimRange("Speed Trim Range", Range(0, 1)) = 0.85
        _SpeedTrimWidth("Speed Trim Width", Range(0.01, 0.5)) = 0.1
        
        [Header(World Bounds)]
        _WorldBoundsMin("World Bounds Min", Vector) = (0,0,0,0)
        _WorldBoundsMax("World Bounds Max", Vector) = (1,1,1,0)
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
                float3 worldPos : TEXCOORD4; // Added for spatial bounds check
                UNITY_FOG_COORDS(5)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            fixed4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5;
            float _AlfaCorrection;
            float _LineWidth;
            float _TextureAnimationSpeed;
            sampler2D _FlowTexture;
            float4 _FlowTexture_ST;
            float _FlowTiling;
            float _BlackThreshold;
            float _EnableAnimation;
            
            // Visualization trimming parameters
            float _MaxAltitude;
            float _MinAltitude;
            float _BoundsLeft;
            float _BoundsRight;
            float _BoundsFront;
            float _BoundsBack;
            float _EnableSpeedTrim;
            float _SpeedTrimRange;
            float _SpeedTrimWidth;
            float4 _WorldBoundsMin;
            float4 _WorldBoundsMax;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // UV Channel Usage:
                // UV.x = Position along streamline (0-1)
                // UV.y = Billboard side (0 or 1)
                // UV2.x = Normalized wind magnitude (0-1) for color
                // UV2.y = Combined animation speed (magnitude * length multiplier)
                // UV3.x = Normalized MSL (0-1)
                // UV3.y = Random texture offset per streamline
                
                // Transform vertex to world space
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                // Store world position for bounds check
                o.worldPos = worldPos;
                
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
                
                // Calculate separate texture UV coordinates for flow texture sampling
                // Use UV.x for position along the line, UV.y for width across the line
                float textureU = v.uv.x * _FlowTiling; // Position along the line with tiling
                float textureV = v.uv.y; // Width across the line (0 to 1)
                
                // Add random offset per streamline from UV3.y
                float randomOffset = v.uv3.y;
                textureU += randomOffset;
                
                // Add animation based on time and magnitude (only if animation is enabled)
                if (_EnableAnimation > 0.5)
                {
                    float combinedAnimSpeed = v.uv2.y; // Get combined animation speed (avg magnitude * length multiplier) from UV2.y
                    float animationOffset = _Time.y * _TextureAnimationSpeed * combinedAnimSpeed;
                    textureU += animationOffset;
                }
                
                // Apply texture transform and store UV coordinates
                o.textureUV = TRANSFORM_TEX(float2(textureU, textureV), _FlowTexture);
                
                o.color = v.color;
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Get normalized position for spatial trimming
                float3 boundsSize = _WorldBoundsMax.xyz - _WorldBoundsMin.xyz;
                float3 normalizedPos = (i.worldPos - _WorldBoundsMin.xyz) / boundsSize;
                
                // Apply spatial bounds trimming
                float leftTrim = step(_BoundsLeft, normalizedPos.x);
                float rightTrim = step(normalizedPos.x, _BoundsRight);
                float frontTrim = step(_BoundsFront, normalizedPos.z);
                float backTrim = step(normalizedPos.z, _BoundsBack);
                float spatialTrim = leftTrim * rightTrim * frontTrim * backTrim;
                
                // Apply altitude trimming using normalized MSL
                float altitudeTrim = step(_MinAltitude, i.uv3.x) * step(i.uv3.x, _MaxAltitude);
                
                // Apply speed trimming using normalized magnitude
                float speedTrim = 1.0;
                if (_EnableSpeedTrim > 0.5)
                {
                    float speedDiff = abs(i.uv2.x - _SpeedTrimRange);
                    speedTrim = 1.0 - saturate(speedDiff / _SpeedTrimWidth);
                }
                
                // Combine all trimming factors
                float trimFactor = spatialTrim * altitudeTrim * speedTrim;
                
                // Check altitude bounds using MSL data (UV3.x)
                float mslHeight = i.uv3.x;
                if (mslHeight < _MinAltitude || mslHeight > _MaxAltitude)
                    discard;
                
                // Check spatial bounds using normalized position
                if (normalizedPos.x < _BoundsLeft || normalizedPos.x > _BoundsRight ||
                    normalizedPos.z < _BoundsFront || normalizedPos.z > _BoundsBack)
                    discard;
                
                // Check speed trim if enabled
                if (_EnableSpeedTrim > 0.5)
                {
                    float speed = i.uv2.x; // Normalized wind magnitude
                    float trimMin = _SpeedTrimRange - _SpeedTrimWidth;
                    float trimMax = _SpeedTrimRange + _SpeedTrimWidth;
                    if (speed < trimMin || speed > trimMax)
                        discard;
                }
                
                // Sample the flow texture (greyscale - only use alpha)
                fixed4 flowTexture = tex2D(_FlowTexture, i.textureUV);
                
                // Trim out values close to black using threshold
                // Convert alpha to 0-1 range and check against threshold
                float textureAlpha = flowTexture.a;
                if (textureAlpha < _BlackThreshold)
                {
                    textureAlpha = 0.0; // Make it fully transparent
                }
                else
                {
                    // Remap remaining values to 0-1 range for smoother transition
                    textureAlpha = (textureAlpha - _BlackThreshold) / (1.0 - _BlackThreshold);
                }
                
                // Create base circle with soft edges for billboard effect
                // Use UV.y for radial distance from center (0 = center, 0.5 = edge)
                float distanceFromCenter = abs(i.uv.y - 0.5) * 2.0; // 0 at center, 1 at edges
                
                float circleRadius = 0.8; // Adjust this to control circle size
                float softness = 0.3; // Adjust this to control edge softness
                float circleAlpha = 1.0 - smoothstep(circleRadius - softness, circleRadius + softness, distanceFromCenter);
                
                // Combine texture alpha with circle alpha (using processed texture alpha)
                float finalAlpha = circleAlpha * textureAlpha;
                
                // Calculate gradient color based on magnitude (independent of texture effect)
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
                
                // Use gradient color for RGB, texture alpha for flow pattern
                fixed4 col = gradientColor;
                col.a *= finalAlpha; // Apply combined alpha (circle + processed texture)
                
                // Apply base color and alpha correction
                col *= i.color;
                col.a *= _AlfaCorrection;
                
                // Apply final alpha
                col.a *= trimFactor * _AlfaCorrection;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
} 