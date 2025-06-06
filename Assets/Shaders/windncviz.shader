Shader "Unlit/windncviz"
{
    Properties
    {
        _MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
        _Color0("Color0", Color) = (1,1,1,0)
        _Color1("Color1", Color) = (1,1,1,1)
        _Color2("Color2", Color) = (1,1,1,1)
        _Color3("Color3", Color) = (1,1,1,1)
        _Color4("Color4", Color) = (1,1,1,1)
        _Color5("Color5", Color) = (1,1,1,1)
        _AlfaCorrection("AlfaCorrection", float) = 0.75

        [Header(Visualization Trimming)]
        _MaxAltitude("Max Altitude", Range(0, 1)) = 1.0
        _MinAltitude("Min Altitude", Range(0, 1)) = 0.0
        [Toggle] _EnableSpeedTrim("Enable Speed Trim", Float) = 0.0
        _SpeedTrimRange("Speed Trim Range", Range(0, 1)) = 0.85
        _SpeedTrimWidth("Speed Trim Width", Range(0.01, 0.5)) = 0.1
    }
    SubShader
    {
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        LOD 100
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2: TEXCOORD1;
                float2 uv3: TEXCOORD2;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5;
            float _AlfaCorrection;

            // Trimming parameters
            float _MaxAltitude;
            float _MinAltitude;
            float _EnableSpeedTrim;
            float _SpeedTrimRange;
            float _SpeedTrimWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv2 = TRANSFORM_TEX(v.uv2, _MainTex);
                o.uv3 = TRANSFORM_TEX(v.uv3, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Apply altitude trimming using normalized MSL
                float altitudeTrim = step(_MinAltitude, i.uv3.x) * step(i.uv3.x, _MaxAltitude);
                
                // Apply speed trimming using normalized magnitude
                float speedTrim = 1.0;
                if (_EnableSpeedTrim > 0.5)
                {
                    float speedDiff = abs(i.uv2.x - _SpeedTrimRange);
                    speedTrim = 1.0 - saturate(speedDiff / _SpeedTrimWidth);
                }

                // Check altitude bounds using MSL data (UV3.x)
                float mslHeight = i.uv3.x;
                if (mslHeight < _MinAltitude || mslHeight > _MaxAltitude)
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

                fixed4 col = tex2D(_MainTex, i.uv);
                float h = i.uv2.x;
                float x;
                if (h >= 0.0 && h <= 0.2)
                {
                    x = (h - 0.0) * 5;
                    col *= (1 - x) * _Color0 + x * _Color1;
                }
                else if (h > 0.2 && h <= 0.4)
                {
                    x = (h - 0.2) * 5;
                    col *= (1 - x) * _Color1 + x * _Color2;
                }
                else if (h > 0.4 && h <= 0.6)
                {
                    x = (h - 0.4) * 5;
                    col *= (1 - x) * _Color2 + x * _Color3;
                }
                else if (h > 0.6 && h <= 0.8)
                {
                    x = (h - 0.6) * 5;
                    col *= (1 - x) * _Color3 + x * _Color4;
                }
                else if (h > 0.8 && h <= 1)
                {
                    x = (h - 0.8) * 5;
                    col *= (1 - x) * _Color4 + x * _Color5;
                }

                // Apply final alpha with trimming
                col.a *= _AlfaCorrection * altitudeTrim * speedTrim;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
} 