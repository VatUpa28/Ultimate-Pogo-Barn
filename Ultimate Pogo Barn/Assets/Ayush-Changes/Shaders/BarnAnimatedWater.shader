Shader "Pogo Barn/Animated Water"
{
    Properties
    {
        _SeaColor ("Deep Water", Color) = (0.035, 0.43, 0.76, 1)
        _CrestColor ("Wave Color", Color) = (0.17, 0.79, 0.93, 1)
        _HorizonColor ("Horizon Color", Color) = (0.55, 0.72, 0.87, 1)
        _FoamColor ("Ripple and Foam", Color) = (0.71, 0.98, 1, 1)
        _WaveHeight ("Wave Height", Range(0, 0.3)) = 0.075
        _Detail ("Wave Detail", Range(0, 1)) = 1
        _ShoreFoam ("Shore Foam", Range(0, 1)) = 0.54
        [HideInInspector] _Splash ("Splash", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "Smooth Water"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SeaColor;
                half4 _CrestColor;
                half4 _FoamColor;
                half4 _HorizonColor;
                float _WaveHeight;
                half _Detail;
                half _ShoreFoam;
                float4 _Splash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 localXZ : TEXCOORD1;
            };

            float EdgeFade(float2 localXZ)
            {
                return 1.0 - smoothstep(75.0, 137.0, length(localXZ));
            }

            float3 WavePhases(float2 p, float t)
            {
                return float3(dot(p, float2(0.52, 0.26)) - t * 1.08,
                              dot(p, float2(-0.20, 0.72)) + t * 0.85,
                              dot(p, float2(0.93, -0.31)) - t * 0.68);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 phases = WavePhases(positionWS.xz, _Time.y);
                positionWS.y += _WaveHeight * EdgeFade(input.positionOS.xz)
                    * dot(sin(phases), float3(0.5, 0.31, 0.19));
                output.localXZ = input.positionOS.xz;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float detail = EdgeFade(input.localXZ) * _Detail;
                float t = _Time.y;
                float2 p = input.positionWS.xz;
                float3 phases = WavePhases(p, t);
                float3 slopes = cos(phases);
                float2 gradient = float2(0.52, 0.26) * slopes.x * 0.50
                                + float2(-0.20, 0.72) * slopes.y * 0.31
                                + float2(0.93, -0.31) * slopes.z * 0.19;
                float3 normal = normalize(float3(-gradient.x * 0.44, 1.0, -gradient.y * 0.44));
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(normal, view)), 3.0h);
                half swell = 0.5h + 0.5h * dot(sin(phases), float3(0.56, 0.29, 0.15));
                half3 color = lerp(_SeaColor.rgb, _CrestColor.rgb, saturate(swell * 0.64h + fresnel * 0.16h));

                float curl = phases.y * 1.7 + sin(phases.x * 1.4) * 0.75 + cos(phases.z) * 0.28;
                half ribbon = smoothstep(0.72h, 0.99h, sin(curl));
                ribbon *= smoothstep(0.25h, 0.84h, swell);
                half glossy = pow(saturate(dot(normal, normalize(view + float3(-0.41, 0.82, 0.40)))), 38.0h);
                color = lerp(color, _FoamColor.rgb, ribbon * 0.22h + glossy * 0.26h);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = -TransformWorldToView(input.positionWS).z;
                float thickness = sceneDepth - waterDepth;
                half shore = smoothstep(0.035, 0.14, thickness) * (1.0 - smoothstep(0.27, 0.83, thickness));
                shore *= 0.76h + 0.24h * sin(curl);
                color = lerp(color, _FoamColor.rgb, shore * _ShoreFoam);

                float radius = length(p - _Splash.xy);
                float distanceToRing = radius - _Splash.z * 4.4;
                half firstRing = 1.0h - smoothstep(0.04, 0.29, abs(distanceToRing));
                half secondRing = 1.0h - smoothstep(0.03, 0.20, abs(distanceToRing + 0.58));
                half ripple = saturate(firstRing * 0.86h + secondRing * 0.40h)
                            * saturate(1.0h - _Splash.z / 1.35h) * _Splash.w;
                color = lerp(color, _FoamColor.rgb, ripple);
                float distanceFromCamera = length(input.positionWS - GetCameraPositionWS());
                half horizon = smoothstep(75.0, 480.0, distanceFromCamera);
                half3 shadedWater = lerp(_SeaColor.rgb, color, detail);
                return half4(lerp(shadedWater, _HorizonColor.rgb, horizon), 1);
            }
            ENDHLSL
        }
    }
}
