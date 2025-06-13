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
        _MinAnimationSpeed ("Min Animation Speed", Range(0, 1)) = 0.2
        _BlackThreshold ("Black Threshold", Range(0, 1)) = 0.1
        _EnableAnimation ("Enable Animation", Float) = 1.0

        [Header(Visualization Trimming)]
        _MaxAltitude("Max Altitude", Range(0, 1)) = 1.0
        _MinAltitude("Min Altitude", Range(0, 1)) = 0.0
        _MinLowestAltitude("Min Lowest Altitude", Range(0, 1)) = 0.0
        _BoundsLeft("Bounds Left", Range(0, 1)) = 0.0
        _BoundsRight("Bounds Right", Range(0, 1)) = 1.0
        _BoundsFront("Bounds Front", Range(0, 1)) = 0.0
        _BoundsBack("Bounds Back", Range(0, 1)) = 1.0
        _SpeedTrimLower("Speed Trim Lower", Range(0, 1)) = 0.0
        _SpeedTrimUpper("Speed Trim Upper", Range(0, 1)) = 1.0
        _FlowDirectionChangeThreshold("Flow Direction Change Threshold", Range(0, 0.15)) = 0.0
        
        [Header(Width Variation)]
        [Toggle] _EnableWidthTrim("Enable Width Trimming", Float) = 1.0
        [Range(0.1, 1.0)]
        _MinWidthScale("Minimum Width Scale", Float) = 0.3
        
        [Header(World Bounds)]
        _WorldBoundsMin("World Bounds Min", Vector) = (0,0,0,0)
        _WorldBoundsMax("World Bounds Max", Vector) = (1,1,1,0)
        
        [Header(Global Color Mapping)]
        _GlobalMinWindMagnitude("Global Min Wind Magnitude", Float) = 0.0
        _GlobalMaxWindMagnitude("Global Max Wind Magnitude", Float) = 20.0
        _UseGlobalMagnitudeRange("Use Global Magnitude Range", Float) = 0.0
        _DataMinWindMagnitude("Data Min Wind Magnitude", Float) = 0.0
        _DataMaxWindMagnitude("Data Max Wind Magnitude", Float) = 20.0
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
                float2 uv2 : TEXCOORD1; // For magnitude normalization and animation speed
                float2 uv3 : TEXCOORD2; // For MSL normalization and cumulative distance
                float2 uv4 : TEXCOORD3; // For direction changes and lowest altitude
                float2 uv5 : TEXCOORD4; // For random texture offset
                float3 normal : NORMAL; // Line direction
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2: TEXCOORD1;
                float2 uv3: TEXCOORD2;
                float2 uv4: TEXCOORD3;
                float2 uv5: TEXCOORD4;
                float2 textureUV : TEXCOORD5; // Separate UV for texture sampling
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD6; // Added for spatial bounds check
                UNITY_FOG_COORDS(7)
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            fixed4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5;
            float _AlfaCorrection;
            float _LineWidth;
            float _TextureAnimationSpeed;
            sampler2D _FlowTexture;
            float4 _FlowTexture_ST;
            float _FlowTiling;
            float _MinAnimationSpeed;
            float _BlackThreshold;
            float _EnableAnimation;
            
            // Visualization trimming parameters
            float _MaxAltitude;
            float _MinAltitude;
            float _MinLowestAltitude;
            float _BoundsLeft;
            float _BoundsRight;
            float _BoundsFront;
            float _BoundsBack;
            float _SpeedTrimLower;
            float _SpeedTrimUpper;
            float _FlowDirectionChangeThreshold;
            float _EnableWidthTrim;
            float _MinWidthScale;
            float4 _WorldBoundsMin;
            float4 _WorldBoundsMax;
            
            // Global color mapping parameters
            float _GlobalMinWindMagnitude;
            float _GlobalMaxWindMagnitude;
            float _UseGlobalMagnitudeRange;
            float _DataMinWindMagnitude;
            float _DataMaxWindMagnitude;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // UV Channel Usage:
                // UV.x = Position along streamline (0-1)
                // UV.y = Billboard side (0 or 1)
                // UV2.x = Normalized wind magnitude (0-1) for color
                // UV2.y = Normalized average wind speed per streamline (0-1)
                // UV3.x = Normalized MSL (0-1)
                // UV3.y = Actual cumulative distance along streamline (world units)
                // UV4.x = Flow direction change intensity (0-1, sine-based: 0=straight, 1=90° turn, realistic wind flow)
                // UV4.y = Normalized lowest altitude per streamline (0-1) for trimming
                // UV5.x = Random texture offset per streamline (0-randomOffsetRange) for pattern variation
                
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
                o.uv4 = v.uv4;
                o.uv5 = v.uv5;
                
                // Calculate separate texture UV coordinates for flow texture sampling
                // Use actual cumulative distance along streamline for consistent texture density
                
                // Get actual cumulative distance along streamline from UV3.y (in world units)
                float cumulativeDistance = v.uv3.y;
                
                // Get random texture offset for this streamline from UV5.x
                float randomOffset = v.uv5.x;
                
                // Apply tiling based on actual cumulative distance along the curved streamline path
                // This ensures consistent texture density regardless of streamline length or curvature
                float textureU = cumulativeDistance * _FlowTiling * 0.01; // Scale factor to control tiling density
                float textureV = v.uv.y; // Width across the line (0 to 1)
                
                // Add random texture offset to prevent synchronized patterns between streamlines
                textureU += randomOffset;
                
                // Add animation based on time and average wind speed (only if animation is enabled)
                if (_EnableAnimation > 0.5)
                {
                    // Use normalized average wind speed for consistent animation along entire streamline
                    // This ensures proper flow direction and consistent speed per streamline
                    float averageWindSpeed = v.uv2.y; // Normalized average wind speed (0-1)
                    
                    // Apply minimum speed to ensure all streamlines have visible flow
                    // Lerp between minimum speed and full speed based on wind magnitude
                    float finalAnimationSpeed = lerp(_MinAnimationSpeed, 1.0, averageWindSpeed);
                    
                    // Apply animation speed proportional to wind speed with minimum threshold
                    float animationOffset = _Time.y * _TextureAnimationSpeed * finalAnimationSpeed;
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
                
                // Apply minimum lowest altitude trimming using normalized lowest altitude per streamline
                float lowestAltitudeTrim = step(i.uv4.y, _MinLowestAltitude);
                
                // Apply speed trimming using normalized magnitude
                float speedTrim = 1.0;
                float speed = i.uv2.x; // Normalized wind magnitude (0-1)
                
                // Hide speeds below lower threshold or above upper threshold
                if (speed < _SpeedTrimLower || speed > _SpeedTrimUpper)
                {
                    speedTrim = 0.0;
                }
                
                // Apply flow direction change trimming
                // When threshold = 0.0, show all streamlines (minimum flow direction change = 0.0)
                // When threshold > 0.0, hide straighter streamlines (require higher flow direction change)
                float directionTrim = 1.0;
                float directionChange = i.uv4.x; // Flow direction change intensity (0-1, sine-based: 0=straight, 1=90° turn)
                
                // Use threshold as minimum flow direction change required to show
                // threshold=0.0 shows all (changes>=0.0, everything visible)
                // threshold=0.2 shows changes>=0.2 (hides very straight lines)
                // threshold=0.8 shows changes>=0.8 (shows only highly curved sections)
                if (directionChange < _FlowDirectionChangeThreshold)
                {
                    directionTrim = 0.0; // Hide sections below threshold flow direction change
                }
                
                // Combine all trimming factors
                float trimFactor = spatialTrim * altitudeTrim * lowestAltitudeTrim * speedTrim * directionTrim;
                
                // Check altitude bounds using MSL data (UV3.x)
                float mslHeight = i.uv3.x;
                if (mslHeight < _MinAltitude || mslHeight > _MaxAltitude)
                    discard;
                
                // Check minimum lowest altitude using UV4.y
                if (i.uv4.y > _MinLowestAltitude)
                    discard;
                
                // Check spatial bounds using normalized position
                if (normalizedPos.x < _BoundsLeft || normalizedPos.x > _BoundsRight ||
                    normalizedPos.z < _BoundsFront || normalizedPos.z > _BoundsBack)
                    discard;
                
                // Check speed trim bounds
                if (speed < _SpeedTrimLower || speed > _SpeedTrimUpper)
                    discard;
                
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
                
                // Apply width trimming based on wind magnitude
                float circleRadius = 0.8; // Base circle size
                if (_EnableWidthTrim > 0.5)
                {
                    // Get wind magnitude (0-1) from UV2.x
                    float windMagnitude = i.uv2.x;
                    
                    // Calculate width scale: interpolate between minWidthScale and 1.0 based on magnitude
                    float widthScale = lerp(_MinWidthScale, 1.0, windMagnitude);
                    
                    // Adjust circle radius based on width scale
                    // Lower magnitude = smaller radius = thinner streamline
                    circleRadius *= widthScale;
                }
                
                float softness = 0.3; // Adjust this to control edge softness
                float circleAlpha = 1.0 - smoothstep(circleRadius - softness, circleRadius + softness, distanceFromCenter);
                
                // Combine texture alpha with circle alpha (using processed texture alpha)
                float finalAlpha = circleAlpha * textureAlpha;
                
                // Calculate gradient color based on magnitude (independent of texture effect)
                float h = i.uv2.x;
                
                // Apply global magnitude range remapping if enabled
                if (_UseGlobalMagnitudeRange > 0.5)
                {
                    // Convert data-normalized magnitude back to physical magnitude
                    float physicalMagnitude = _DataMinWindMagnitude + h * (_DataMaxWindMagnitude - _DataMinWindMagnitude);
                    
                    // Remap to global range
                    float globalRange = _GlobalMaxWindMagnitude - _GlobalMinWindMagnitude;
                    if (globalRange > 0.0)
                    {
                        // Clamp to global range and normalize to 0-1
                        float clampedMagnitude = clamp(physicalMagnitude, _GlobalMinWindMagnitude, _GlobalMaxWindMagnitude);
                        h = (clampedMagnitude - _GlobalMinWindMagnitude) / globalRange;
                    }
                }
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