Shader "Custom/BT_Shader_Advanced"
{
    Properties
    {
        // Base Properties
        _BaseColor("Base Color", Color) = (0.1, 0.1, 0.2, 0.3)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        
        // Distortion Effects
        _NoiseTex("Distortion Noise", 2D) = "gray" {}
        _DistortionStrength("Distortion Strength", Range(0, 0.5)) = 0.1
        _DepthDistortion("Depth Distortion", Range(0, 2)) = 0.5
        _ChromaticShift("Chromatic Shift", Range(0, 0.1)) = 0.02
        
        // Dissolve & Emission
        _DissolveMap("Dissolve Map", 2D) = "white" {}
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0.5
        _EdgeWidth("Edge Width", Range(0, 0.2)) = 0.05
        _GlowColor("Glow Color", Color) = (0, 0.8, 1, 1)
        _GlowIntensity("Glow Intensity", Range(0, 20)) = 5
        _PulseFrequency("Pulse Frequency", Range(0, 5)) = 1
        _PulseAmplitude("Pulse Amplitude", Range(0, 5)) = 0.5
        
        // Chiral Effects
        _HandprintMap("Handprint Map", 2D) = "black" {}
        _HandprintIntensity("Handprint Intensity", Range(0, 5)) = 2
        _HandprintScale("Handprint Scale", Range(0.1, 10)) = 2
        _HandprintSpeed("Handprint Speed", Range(0, 5)) = 0.5
        
        // Rain Effects
        _RainRippleMap("Rain Ripple Map", 2D) = "gray" {}
        _RainIntensity("Rain Intensity", Range(0, 1)) = 0.3
        _RainSpeed("Rain Speed", Range(0, 5)) = 1
        
        // Proximity Effects
        _ProximityFadeDistance("Proximity Fade", Range(0.1, 10)) = 3
        _ProximityDistortion("Proximity Distortion", Range(0, 5)) = 2
        _ProximityGlow("Proximity Glow", Range(0, 5)) = 1.5
    }

    SubShader
    {
        Tags { 
            "Queue"="Transparent" 
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            // Properties
            sampler2D _MainTex, _NoiseTex, _DissolveMap, _HandprintMap, _RainRippleMap;
            float4 _MainTex_ST, _NoiseTex_ST, _DissolveMap_ST;
            half4 _BaseColor, _GlowColor;
            float _DistortionStrength, _DepthDistortion, _ChromaticShift;
            float _DissolveAmount, _EdgeWidth, _GlowIntensity;
            float _PulseFrequency, _PulseAmplitude;
            float _HandprintIntensity, _HandprintScale, _HandprintSpeed;
            float _RainIntensity, _RainSpeed;
            float _ProximityFadeDistance, _ProximityDistortion, _ProximityGlow;

            // Proximity calculation (would be set by script)
            float3 _PlayerPosition;
            
            // Timefall rain (global effect)
            float _GlobalRainIntensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Calculate proximity to player
                float playerDistance = distance(input.positionWS, _PlayerPosition);
                float proximityFactor = saturate(1 - playerDistance / _ProximityFadeDistance);
                float proximityDistortion = proximityFactor * _ProximityDistortion;
                float proximityGlow = proximityFactor * _ProximityGlow;
                
                // Apply holographic flickering
                float flicker = 0.95 + 0.05 * sin(_Time.y * 12) * cos(_Time.y * 7);
                
                // Sample distortion noise with scrolling
                float2 noiseUV = input.uv * 2.0 + float2(0, _Time.y * 0.3);
                float2 distortion = tex2D(_NoiseTex, noiseUV).rg * 2 - 1;
                
                // Apply proximity-based distortion
                distortion *= (_DistortionStrength + proximityDistortion) * flicker;
                
                // Create chromatic aberration effect
                float2 distortionR = distortion * (1 + _ChromaticShift);
                float2 distortionG = distortion;
                float2 distortionB = distortion * (1 - _ChromaticShift);
                
                // Get screen position with distortion
                float4 screenPos = input.screenPos;
                screenPos.xy /= screenPos.w;
                
                // Sample scene color with chromatic distortion
                half3 sceneColor;
                sceneColor.r = SampleSceneColor(screenPos.xy + distortionR).r;
                sceneColor.g = SampleSceneColor(screenPos.xy + distortionG).g;
                sceneColor.b = SampleSceneColor(screenPos.xy + distortionB).b;
                
                // Apply depth-based distortion
                float depth = SampleSceneDepth(screenPos.xy);
                float depthDistortion = saturate(depth * _DepthDistortion);
                distortion *= depthDistortion;
                
                // Sample dissolve map with animated UVs
                float2 dissolveUV = input.uv + float2(_Time.y * 0.1, 0);
                float dissolveValue = tex2D(_DissolveMap, dissolveUV).r;
                
                // Calculate dissolve edges with pulse
                float pulse = 1.0 + _PulseAmplitude * sin(_Time.y * _PulseFrequency);
                float dissolveEdge = smoothstep(
                    _DissolveAmount - _EdgeWidth, 
                    _DissolveAmount + _EdgeWidth, 
                    dissolveValue
                );
                
                // Calculate glow
                float edgeGlow = 1.0 - dissolveEdge;
                edgeGlow = pow(edgeGlow, 3) * _GlowIntensity * pulse * (1 + proximityGlow);
                
                // Add handprint effects (chiralium)
                float2 handprintUV = input.uv * _HandprintScale + float2(0, _Time.y * _HandprintSpeed);
                float handprints = tex2D(_HandprintMap, handprintUV).r;
                float handprintGlow = pow(handprints, 4) * _HandprintIntensity;
                
                // Combine glows
                float finalGlow = saturate(edgeGlow + handprintGlow);
                
                // Rain ripple effect
                float2 rainUV = input.uv * 3.0 + float2(_Time.y * _RainSpeed, 0);
                float rainRipples = tex2D(_RainRippleMap, rainUV).r;
                float rainEffect = saturate(rainRipples * _RainIntensity * _GlobalRainIntensity);
                
                // Combine rain with dissolve
                dissolveEdge = saturate(dissolveEdge + rainEffect * 0.3);
                
                // Sample base texture
                half4 baseColor = tex2D(_MainTex, input.uv) * _BaseColor;
                
                // Combine elements
                half3 emissive = finalGlow * _GlowColor.rgb;
                half3 finalColor = lerp(sceneColor, baseColor.rgb, dissolveEdge) + emissive;
                
                // Alpha calculation with proximity fade
                float alpha = baseColor.a * dissolveEdge * flicker;
                alpha *= saturate(playerDistance / _ProximityFadeDistance); // Fade out when player approaches
                
                // Add hologram effect to alpha
                alpha *= 0.7 + 0.3 * sin(_Time.y * 5 + input.positionWS.x * 2);
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}