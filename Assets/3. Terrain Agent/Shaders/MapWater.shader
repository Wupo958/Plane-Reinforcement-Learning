Shader "Terrain/MapWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.14, 0.46, 0.64, 0.74)
        _DeepColor("Deep Color", Color) = (0.02, 0.13, 0.28, 0.95)
        _WaveAmplitude("Wave Amplitude", Float) = 1.2
        _WaveFrequency("Wave Frequency", Float) = 0.012
        _WaveSpeed("Wave Speed", Float) = 0.8
        _RippleStrength("Ripple Strength", Float) = 0.35
        _RippleFrequency("Ripple Frequency", Float) = 0.12
        _Smoothness("Smoothness", Range(0,1)) = 0.92
        _SpecularBoost("Specular Boost", Float) = 2.2
        _FresnelPower("Fresnel Power", Float) = 4.0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _RippleStrength;
                float _RippleFrequency;
                float _Smoothness;
                float _SpecularBoost;
                float _FresnelPower;
            CBUFFER_END

            // Coarse swell only. The mesh is 80 m per cell, so anything shorter than a few hundred
            // metres would alias in the vertex stage; fine detail is added per pixel instead.
            float SwellHeight(float2 p, float t)
            {
                float h = sin(p.x * _WaveFrequency + t) * 0.55;
                h += sin(p.y * _WaveFrequency * 1.27 - t * 0.83) * 0.35;
                h += sin((p.x + p.y) * _WaveFrequency * 0.61 + t * 1.21) * 0.22;
                return h * _WaveAmplitude;
            }

            float3 RippleNormal(float2 p, float t)
            {
                float a = sin(p.x * _RippleFrequency + t * 1.7) + sin(p.y * _RippleFrequency * 1.4 - t * 1.3);
                float b = sin((p.x - p.y) * _RippleFrequency * 0.9 + t * 2.1);
                return float3(a * _RippleStrength * 0.05, 0.0, b * _RippleStrength * 0.05);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;

                float step = 12.0;
                float here = SwellHeight(positionWS.xz, t);
                float alongX = SwellHeight(positionWS.xz + float2(step, 0.0), t);
                float alongZ = SwellHeight(positionWS.xz + float2(0.0, step), t);

                positionWS.y += here;

                OUT.positionWS = positionWS;
                OUT.normalWS = normalize(float3(here - alongX, step, here - alongZ));
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float3 normalWS = normalize(IN.normalWS + RippleNormal(IN.positionWS.xz, t));
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                float4 baseColor = lerp(_DeepColor, _ShallowColor, saturate(fresnel + 0.22));

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(normalWS, mainLight.direction));
                float3 color = baseColor.rgb * (0.38 + 0.62 * ndotl) * mainLight.color;

                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float specular = pow(saturate(dot(normalWS, halfDir)), lerp(16.0, 900.0, _Smoothness));
                color += mainLight.color * specular * _SpecularBoost;

                color += baseColor.rgb * SampleSH(normalWS) * 0.35;

                float alpha = saturate(baseColor.a + fresnel * 0.28);
                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
