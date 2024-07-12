Shader "Hidden/AlphaBlendShader"
{
    Properties
    {
        _MainTex ("Background Texture", 2D) = "white" {}
        _ForegroundTex ("Foreground Texture", 2D) = "white" {}
        _ForegroundScale ("Foreground Scale", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 backgroundUV : TEXCOORD0;
                float2 foregroundUV : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            float4 _ForegroundScale;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.backgroundUV = v.uv;
                o.foregroundUV = v.uv * _ForegroundScale.xy;
                return o;
            }

            sampler2D _MainTex;
            sampler2D _ForegroundTex;
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 background = tex2D(_MainTex, i.backgroundUV);
                fixed4 foreground = tex2D(_ForegroundTex, i.foregroundUV);

                return fixed4(
                    lerp(background.xyz, foreground.xyz, foreground.w),
                    background.w + foreground.w * (1.0 - background.w)
                );
            }
            ENDCG
        }
    }
}
