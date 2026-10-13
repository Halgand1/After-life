// Personnages, animaux et equipement : texture palette (filtre Point) x couleur de sommet
// R = occlusion (1 = degage), G = salete (0 = propre). Lumiere principale avec ombres, lumieres additionnelles, ambiance SH.
Shader "AfterCity/CharacterPalette"
{
    Properties
    {
        _BaseMap ("Palette", 2D) = "white" {}
        _BaseColor ("Teinte", Color) = (0.82, 0.82, 0.80, 1)
        _DirtColor ("Couleur de salete", Color) = (0.30, 0.24, 0.18, 1)
        _DirtStrength ("Force de la salete", Range(0, 1)) = 0.6
        _Smoothness ("Brillance", Range(0, 1)) = 0.15
        _Rim ("Liseré (lisibilite)", Range(0, 1)) = 0
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            // Ambiance du jeu (GameRoot, jour/nuit), la meme que le terrain
            half4 _AC_AmbientSky;
            half4 _AC_AmbientGround;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DirtColor;
                half _DirtStrength;
                half _Smoothness;
                half _Rim;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                albedo *= i.color.r;                                                   // occlusion
                albedo = lerp(albedo, albedo * _DirtColor.rgb * 2.0, i.color.g * _DirtStrength); // salete

                half3 n = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lit = light.color * saturate(dot(n, light.direction)) * light.shadowAttenuation
                           + lerp(_AC_AmbientGround.rgb, _AC_AmbientSky.rgb, n.y * 0.5 + 0.5) * 0.55;

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

                // Liseré clair sur les bords (personnages seulement) : se detachent du decor vu de loin
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0 - saturate(dot(n, v)), 3.0) * _Rim;
                half3 col = MixFog(albedo * lit + rim * half3(1.0, 0.92, 0.80), i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
