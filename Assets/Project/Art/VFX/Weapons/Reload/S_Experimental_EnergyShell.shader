Shader "Breachpoint/VFX/Experimental Energy Shell"
{
    Properties
    {
        [HDR] _EnergyColor("Energy Color", Color) = (0.03, 0.8, 4.0, 0.55)
        _Emission("Emission", Range(0, 12)) = 4
        _BaseOpacity("Base Opacity", Range(0, 1)) = 0.025
        _RimPower("Rim Power", Range(0.5, 8)) = 2.2
        _RimOpacity("Rim Opacity", Range(0, 2)) = 0.6
        _RimEmission("Rim Emission", Range(0, 6)) = 2.8
        _NoiseScale("Noise Scale", Range(0.5, 12)) = 4.8
        _NoiseSpeed("Noise Speed", Range(0, 5)) = 1.1
        _Displacement("Surface Displacement", Range(0, 0.2)) = 0.075
        _FilamentThreshold("Filament Threshold", Range(0, 1)) = 0.68
        _FilamentOpacity("Filament Opacity", Range(0, 1)) = 0.22
        _FilamentEmission("Filament Emission", Range(0, 8)) = 2.4
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 3.5
        _PulseAmount("Pulse Amount", Range(0, 0.5)) = 0.12
        [HideInInspector] _Visibility("Visibility", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EnergyColor;
                float _Emission;
                float _BaseOpacity;
                float _RimPower;
                float _RimOpacity;
                float _RimEmission;
                float _NoiseScale;
                float _NoiseSpeed;
                float _Displacement;
                float _FilamentThreshold;
                float _FilamentOpacity;
                float _FilamentEmission;
                float _PulseSpeed;
                float _PulseAmount;
                float _Visibility;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float ValueNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 local = frac(position);
                local = local * local * (3.0 - 2.0 * local);

                float n000 = Hash31(cell + float3(0, 0, 0));
                float n100 = Hash31(cell + float3(1, 0, 0));
                float n010 = Hash31(cell + float3(0, 1, 0));
                float n110 = Hash31(cell + float3(1, 1, 0));
                float n001 = Hash31(cell + float3(0, 0, 1));
                float n101 = Hash31(cell + float3(1, 0, 1));
                float n011 = Hash31(cell + float3(0, 1, 1));
                float n111 = Hash31(cell + float3(1, 1, 1));

                float n00 = lerp(n000, n100, local.x);
                float n10 = lerp(n010, n110, local.x);
                float n01 = lerp(n001, n101, local.x);
                float n11 = lerp(n011, n111, local.x);
                return lerp(lerp(n00, n10, local.y), lerp(n01, n11, local.y), local.z);
            }

            float FractalNoise(float3 position)
            {
                float noise = ValueNoise(position) * 0.62;
                noise += ValueNoise(position * 2.03 + 17.1) * 0.27;
                noise += ValueNoise(position * 4.01 - 9.7) * 0.11;
                return noise;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float time = _TimeParameters.x * _NoiseSpeed;
                float3 normalOS = normalize(input.normalOS);
                float surfaceNoise = FractalNoise(
                    input.positionOS * (_NoiseScale * 0.72)
                    + float3(time * 0.23, -time * 0.17, time * 0.19));
                float pulse = sin(_TimeParameters.x * _PulseSpeed) * _PulseAmount;
                float displacement = (surfaceNoise - 0.5) * _Displacement + pulse * 0.025;
                float3 displacedPositionOS = input.positionOS + normalOS * displacement;

                output.positionOS = displacedPositionOS;
                output.positionWS = TransformObjectToWorld(displacedPositionOS);
                output.normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = pow(
                    1.0 - saturate(abs(dot(normalize(input.normalWS), viewDirection))),
                    _RimPower);

                float time = _TimeParameters.x * _NoiseSpeed;
                float3 flowingPosition = input.positionOS * _NoiseScale
                    + float3(time * 0.31, -time * 0.21, time * 0.27);
                float broadNoise = FractalNoise(flowingPosition);
                float detailNoise = FractalNoise(flowingPosition * 1.83 + 6.2);
                float ridge = 1.0 - abs(detailNoise * 2.0 - 1.0);
                float filament = smoothstep(_FilamentThreshold, 1.0, ridge);
                filament *= smoothstep(0.22, 0.78, broadNoise);

                float pulse = 1.0 + sin(
                    _TimeParameters.x * _PulseSpeed
                    + broadNoise * 5.0) * _PulseAmount;
                float opacity = _BaseOpacity
                    + fresnel * _RimOpacity
                    + filament * _FilamentOpacity;
                float brightness = 0.22
                    + fresnel * _RimEmission
                    + filament * _FilamentEmission;

                float3 color = _EnergyColor.rgb * _Emission * brightness
                    * pulse * _Visibility;
                float alpha = saturate(
                    opacity * _EnergyColor.a * pulse * _Visibility);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
