Shader "Custom/FragileCargoOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,0.5,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.03
        _OutlineIntensity ("Outline Intensity", Range(0.0, 2.0)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0.0, 1.0)) = 0.3
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }
        
        // Outline Pass
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            
            Cull Front
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineIntensity;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Calculate pulsing effect
                float pulse = sin(_Time.y * _PulseSpeed) * _PulseAmount + 1.0;
                float finalOutlineWidth = _OutlineWidth * pulse;
                
                // Expand vertex along normal for outline
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * finalOutlineWidth;
                
                output.positionHCS = TransformWorldToHClip(positionWS);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Pulsing outline color
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float4 finalColor = _OutlineColor * _OutlineIntensity;
                finalColor.rgb *= (1.0 + pulse * _PulseAmount);
                
                return finalColor;
            }
            ENDHLSL
        }
        
        // Main Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            
            Cull Back
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineIntensity;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample main texture
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // Simple lighting calculation
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 normal = normalize(input.normalWS);
                float NdotL = saturate(dot(normal, lightDir));
                
                float3 lighting = mainLight.color * NdotL + unity_AmbientSky.rgb;
                albedo.rgb *= lighting;
                
                return albedo;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}