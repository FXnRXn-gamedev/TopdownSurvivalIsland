Shader "Custom/MysteriousHumanoid"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Distortion ("Distortion", Range(0, 1)) = 0.5
        _GlowIntensity ("Glow Intensity", Range(1, 10)) = 3
        _GlowColor ("Glow Color", Color) = (0.1, 0.3, 1.0, 1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha

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
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Distortion;
            float _GlowIntensity;
            float4 _GlowColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Distortion effect
                float2 distortUV = i.uv + float2(
                    sin(_Time.y * 2 + i.worldPos.x) * _Distortion,
                    cos(_Time.y * 1.5 + i.worldPos.z) * _Distortion
                );

                // Main texture with distortion
                fixed4 col = tex2D(_MainTex, distortUV);
                
                // Edge glow
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(cross(ddx(i.worldPos), ddy(i.worldPos)));
                float rim = 1.0 - saturate(dot(viewDir, normal));
                float3 glow = _GlowColor.rgb * pow(rim, _GlowIntensity) * 3;
                
                // Final color
                col.rgb += glow;
                col.a = saturate(col.a + rim * 2);
                return col;
            }
            ENDCG
        }
    }
}