Shader "Hidden/AdvancedDeathStrandingScan"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScanColor ("Scan Color", Color) = (1,0.8,0.2,1)
        _ScanWidth ("Scan Width", Range(0.1, 10)) = 2
        _GridSize ("Grid Size", Float) = 2
        _DistortionTex ("Distortion Texture", 2D) = "black" {}
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.1
        _NormalDistortion ("Normal Distortion", Range(0, 1)) = 0.3
        _RippleFrequency ("Ripple Frequency", Float) = 8
        _RippleAmplitude ("Ripple Amplitude", Range(0, 0.5)) = 0.05
    }
    
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        
        // Pass 0: Distortion effect
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_distortion
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
            float4 _ScanOrigin;
            float _ScanRadius;
            float _ScanIntensity;
            float _NormalDistortion;
            float _RippleFrequency;
            float _RippleAmplitude;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Calculate world position for scan effect
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xyz;
                
                return o;
            }

            float3 get_world_normal_from_depth(float2 uv, float depth)
            {
                float3 viewVector = mul(unity_CameraInvProjection, float4(uv * 2 - 1, 0, -1));
                viewVector = mul(unity_CameraToWorld, float4(viewVector, 0));
                return normalize(-viewVector);
            }

            float4 frag_distortion (v2f i) : SV_Target
            {
                // Base color
                float4 col = tex2D(_MainTex, i.uv);
                
                // Distance from scan origin
                float dist = distance(i.worldPos, _ScanOrigin.xyz);
                float scanProgress = saturate((_ScanRadius - dist) / 0.5);
                
                // Wave effect
                float wave = sin(dist * _RippleFrequency - _Time.y * 10) * _RippleAmplitude * _ScanIntensity;
                
                // Normal distortion effect
                float3 normal = get_world_normal_from_depth(i.uv, 0);
                float3 distortion = normal * _NormalDistortion * _ScanIntensity;
                
                // Combine effects
                float2 distortionUV = i.uv + distortion.xz * 0.05 + float2(0, wave);
                
                return float4(distortionUV, 0, 1);
            }
            ENDCG
        }
        
        // Pass 1: Main scan effect
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag_scan
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
            sampler2D _DistortionTex;
            float4 _MainTex_ST;
            float4 _ScanOrigin;
            float4 _ScanColor;
            float _ScanRadius;
            float _ScanIntensity;
            float _ScanWidth;
            float _GridSize;
            float _DistortionStrength;
            float _RippleFrequency;
            float _RippleAmplitude;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                
                // Calculate world position for scan effect
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = worldPos.xyz;
                
                return o;
            }

            float4 frag_scan (v2f i) : SV_Target
            {
                // Apply distortion to UVs
                float2 distortion = tex2D(_DistortionTex, i.uv).xy;
                float2 distortedUV = i.uv + distortion * _DistortionStrength;
                
                // Sample scene color with distortion
                float4 col = tex2D(_MainTex, distortedUV);
                
                // Distance from scan origin
                float dist = distance(i.worldPos, _ScanOrigin.xyz);
                
                // Scan wave effect
                float scanProgress = saturate((_ScanRadius - dist) / _ScanWidth);
                float waveFront = smoothstep(0.1, 0.9, scanProgress);
                float waveTrail = saturate(scanProgress * 1.5);
                
                // Grid effect
                float3 grid = frac(i.worldPos / _GridSize);
                grid = abs(grid - 0.5) * 2;
                float gridLines = pow(saturate(1 - min(min(grid.x, grid.y), grid.z)), 3);
                
                // Ripple effect
                float ripple = sin(dist * _RippleFrequency - _Time.y * 15) * 0.5 + 0.5;
                ripple *= _RippleAmplitude * _ScanIntensity;
                
                // Combine effects
                float3 scanEffect = _ScanColor.rgb * (waveFront + waveTrail + gridLines * 0.7 + ripple);
                scanEffect *= _ScanIntensity;
                
                // Apply scan effect with additive blending
                col.rgb += scanEffect;
                
                return col;
            }
            ENDCG
        }
    }
}