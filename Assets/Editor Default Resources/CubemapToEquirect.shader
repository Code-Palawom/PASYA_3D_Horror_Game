Shader "Hidden/CubemapToEquirect"
{
    Properties
    {
        _Cube ("Cubemap", CUBE) = "" {}
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _Cube;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float theta = i.uv.x * UNITY_TWO_PI - UNITY_PI; // -PI..PI (longitude)
                float phi   = (1.0 - i.uv.y) * UNITY_PI;        // 0..PI  (latitude, flipped to correct PNG row order)

                float3 dir;
                dir.x = sin(phi) * sin(theta);
                dir.y = cos(phi);
                dir.z = sin(phi) * cos(theta);

                return texCUBE(_Cube, dir);
            }
            ENDCG
        }
    }
    FallBack Off
}
