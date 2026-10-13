// Shader du terrain en blocs : couleur par sommet, lumiere principale avec ombres, ambiance douce.
Shader "AfterCity/VertexColorLit"
{
    Properties
    {
        _BaseColor ("Teinte", Color) = (1, 1, 1, 1)
        _Ambient ("Ambiance", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Ambiance reglee par le jeu (cycle jour/nuit)
            half4 _AC_AmbientSky;
            half4 _AC_AmbientGround;
            half _AC_Night;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Ambient;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color * _BaseColor;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half3 n = normalize(i.normalWS);
                half ndl = saturate(dot(n, light.direction));
                half3 sky = lerp(_AC_AmbientGround.rgb, _AC_AmbientSky.rgb, n.y * 0.5 + 0.5);
                half3 lit = light.color * ndl * light.shadowAttenuation + sky * _Ambient;

                // Lumieres additionnelles (feu de camp, lanternes)
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light add = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                    lit += add.color * add.distanceAttenuation * (saturate(dot(n, add.direction)) * 0.8 + 0.2);
                LIGHT_LOOP_END
                #endif

                half3 col = i.color.rgb * lit;
                // Alpha 0 = surface emissive (flammes, fenetres eclairees) : pas d'ombre, assez forte pour le bloom
                // Alpha 0,1 = emissive seulement la nuit (fenetres)
                half emissive = max(step(i.color.a, 0.01), step(0.05, i.color.a) * step(i.color.a, 0.15) * _AC_Night);
                col = lerp(col, i.color.rgb * 3.0, emissive);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
