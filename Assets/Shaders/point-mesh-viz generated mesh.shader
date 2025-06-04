Shader "Unlit/point-mesh-viz generated"
{
    Properties
    {
        //_MainTex ("Texture", 2D) = "white" {}
     _MainTex("Base (RGB) Trans (A)", 2D) = "white" {}
      
        _Color0("Color0", Color) = (1,1,1,0)
        _Color1("Color1", Color) = (1,1,1,1)
        _Color2("Color2", Color) = (1,1,1,1)
        _Color3("Color3", Color) = (1,1,1,1)
        _Color4("Color4", Color) = (1,1,1,1)
        _Color5("Color5", Color) = (1,1,1,1)
        _AlfaCorrection("AlfaCorrection", float) = 0.75

        _MaxAltutude("MaxAltitude", Range(0.0, 1)) = 0.5
        _MinAltutude("MinAltitude", Range(0.0, 1)) = 0.0

        _xLeft("left", Range(0.0, 1)) = 0
        _xRight("right", Range(0.0, 1)) = 1

        _zFront("front", Range(0.0, 1)) = 0
        _zBack("back",Range(0.0, 1)) = 1

        _rangeStart("_rangeStart", Range(0.25, 1)) = 0
        _range("_range", Range(0.0, 1)) = 1

        _showSpeedRange("Show Range", int) = 0



        
    }
    SubShader
    {
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        //Tags { "RenderType"="Opaque" }        
        LOD 100
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2: TEXCOORD1;
                float2 uv3: TEXCOORD2;
                float2 uv4: TEXCOORD3;
                float2 uv5: TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID

            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2: TEXCOORD1;
                float2 uv3: TEXCOORD2;
                float2 uv4: TEXCOORD3;
                float2 uv5: TEXCOORD4;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color0, _Color1, _Color2, _Color3, _Color4, _Color5;
            float _AlfaCorrection, _MaxAltutude, _MinAltutude, _xLeft, _xRight, _zFront, _zBack, _rangeStart, _range;
            int _showSpeedRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv2 = TRANSFORM_TEX(v.uv2, _MainTex);
                o.uv3 = TRANSFORM_TEX(v.uv3, _MainTex);
                o.uv4 = TRANSFORM_TEX(v.uv4, _MainTex);
                o.uv5 = TRANSFORM_TEX(v.uv5, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
              
            

            float h = i.uv2.x;
            float x;
      

            float altNorm = i.uv3.x;

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

           
            if (!(altNorm < _MaxAltutude && altNorm >= _MinAltutude))
                col.a = 0;  

            if (!(i.uv4.x > _xLeft && i.uv4.x < _xRight))
                col.a = 0;

            if (!(i.uv5.x > _zFront && i.uv5.x < _zBack))
                col.a = 0;

           
            //Show only range of wind speed 
            if (_showSpeedRange == 1)
            {
                if (!(h > _rangeStart - _range /2 && h < _rangeStart + _range /2))
                    col.a = 0;
                else
                {   //Make mesh more opaque
                    col.a*= _AlfaCorrection *1.25;
                    if (col.a > 1)  col.a = 1;
                }

            }else
                col.a *= _AlfaCorrection;

            
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
