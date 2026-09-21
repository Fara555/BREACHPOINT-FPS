Shader "Breachpoint/VFX/SP6 Energy Mote"
{
    Properties
    {
        _MainTex("Soft Particle", 2D) = "white" {}
        [HDR] _EnergyColor("Energy Color", Color) = (0.05, 0.75, 3.5, 0.8)
        _Emission("Emission", Range(0, 12)) = 4
        [HideInInspector] _Visibility("Visibility", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+25"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _EnergyColor;
                float _Emission;
                float _Visibility;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float textureMask = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv).r;
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float radialMask = saturate(1.0 - dot(centeredUv, centeredUv));
                radialMask *= radialMask;
                float mask = textureMask * radialMask * input.color.a *
                    _EnergyColor.a * _Visibility;
                float core = pow(saturate(radialMask), 3.0);
                float3 color = _EnergyColor.rgb * input.color.rgb *
                    _Emission * (0.55 + core * 1.45) * _Visibility;
                return float4(color, mask);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
