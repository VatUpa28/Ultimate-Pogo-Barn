Shader "Pogo Barn/Cartoon Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Color Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shaded Faces", Color) = (0.53, 0.65, 0.79, 1)
        _RimColor ("Edge Light", Color) = (0.67, 0.84, 1, 1)
        _RimStrength ("Edge Strength", Range(0, 1)) = 0.18
        _Gloss ("Cartoon Gloss", Range(0, 1)) = 0.28
        [HideInInspector] _Cull ("Cull", Float) = 2
        [HideInInspector] _Cutoff ("Cutoff", Float) = 0.5
        [HideInInspector] _Smoothness ("Smoothness", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Cartoon Lighting"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadeColor;
                half4 _RimColor;
                half _RimStrength;
                half _Gloss;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screenPosition : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.screenPosition = ComputeScreenPos(position.positionCS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = input.screenPosition;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light sun = GetMainLight(shadowCoord);
                half diffuse = saturate(dot(normal, sun.direction));
                half lightBand = smoothstep(0.16h, 0.35h, diffuse) * 0.61h
                               + smoothstep(0.53h, 0.71h, diffuse) * 0.39h;
                half3 light = lerp(_ShadeColor.rgb, saturate(sun.color * 0.23h + 0.77h),
                                   lightBand * sun.shadowAttenuation);
                half rim = smoothstep(0.55h, 0.93h, 1.0h - saturate(dot(normal, view)));
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 halfDirection = SafeNormalize(sun.direction + view);
                half gloss = pow(saturate(dot(normal, halfDirection)), lerp(18.0h, 44.0h, _Gloss));
                gloss *= smoothstep(0.08h, 0.35h, diffuse) * sun.shadowAttenuation * _Gloss;
                half3 shine = lerp(half3(0.78h, 0.89h, 1.0h), half3(1.0h, 0.96h, 0.84h), diffuse);
                return half4(albedo * light + _RimColor.rgb * rim * _RimStrength * (0.3h + 0.7h * diffuse)
                             + shine * gloss, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
