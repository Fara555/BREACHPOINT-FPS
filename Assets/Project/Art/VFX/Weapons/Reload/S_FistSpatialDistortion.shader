Shader "BREACHPOINT/VFX/Fist Spatial Distortion"
{
    Properties
    {
        [NoScaleOffset] _FlowMap("Distortion Flow", 2D) = "gray" {}
        _Strength("Radial Strength (Pixels)", Range(0, 40)) = 12
        _SwirlStrength("Swirl Strength (Pixels)", Range(0, 20)) = 3
        _FlowStrength("Flow Detail (Pixels)", Range(0, 10)) = 1.5
        _Blur("Blur", Range(0, 1)) = 0.04
        _Speed("Flow Speed", Range(-2, 2)) = 0.28
        _Radius("Radius", Range(0.1, 0.75)) = 0.50
        _Feather("Edge Feather", Range(0.01, 0.4)) = 0.20
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 2.2
        _PulseAmount("Pulse Amount", Range(0, 0.5)) = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
        }

        Pass
        {
            Name "DistortionVectors"
            Tags { "LightMode" = "DistortionVectors" }

            Stencil
            {
                WriteMask 4
                Ref 4
                Comp Always
                Pass Replace
            }

            Blend One One, One One
            BlendOp Add, Add
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 vulkan metal
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Builtin/BuiltinData.hlsl"

            TEXTURE2D(_FlowMap);
            SAMPLER(sampler_FlowMap);

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _SwirlStrength;
                float _FlowStrength;
                float _Blur;
                float _Speed;
                float _Radius;
                float _Feather;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 RotateAroundCenter(float2 uv, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                float2 centeredUv = uv - 0.5;

                return float2(
                    centeredUv.x * cosine - centeredUv.y * sine,
                    centeredUv.x * sine + centeredUv.y * cosine) + 0.5;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float scaleX = length(float3(
                    objectToWorld._m00,
                    objectToWorld._m10,
                    objectToWorld._m20));
                float scaleY = length(float3(
                    objectToWorld._m01,
                    objectToWorld._m11,
                    objectToWorld._m21));
                float3 cameraRight = mul(
                    (float3x3)GetViewToWorldMatrix(),
                    float3(1.0, 0.0, 0.0));
                float3 cameraUp = mul(
                    (float3x3)GetViewToWorldMatrix(),
                    float3(0.0, 1.0, 0.0));
                float3 centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 positionWS = centerWS
                    + cameraRight * input.positionOS.x * scaleX
                    + cameraUp * input.positionOS.y * scaleY;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = _TimeParameters.x * _Speed;
                float2 uvA = RotateAroundCenter(input.uv, time);
                float2 uvB = RotateAroundCenter(input.uv, -time * 0.63 + 1.7);
                float4 flowA = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, uvA);
                float4 flowB = SAMPLE_TEXTURE2D(_FlowMap, sampler_FlowMap, uvB);

                float2 centeredUv = input.uv - 0.5;
                float radialDistance = length(centeredUv);
                float innerRadius = max(0.0, _Radius - _Feather);
                float radialMask = 1.0 - smoothstep(innerRadius, _Radius, radialDistance);
                float textureMask = saturate(max(flowA.a, flowB.a));
                float mask = radialMask * textureMask;

                float2 flowDirectionA = flowA.rg * 2.0 - 1.0;
                float2 flowDirectionB = flowB.rg * 2.0 - 1.0;
                float2 flowDetail = (flowDirectionA + flowDirectionB * 0.45) / 1.45;

                float safeDistance = max(radialDistance, 0.001);
                float2 radialDirection = centeredUv / safeDistance;
                float2 tangentDirection = float2(
                    -radialDirection.y,
                    radialDirection.x);

                // Keep the very center stable and concentrate the refraction
                // in a soft lens-shaped ring around the fist.
                float centerFade = smoothstep(0.025, 0.13, radialDistance);
                float normalizedRadius = saturate(
                    radialDistance / max(_Radius, 0.001));
                float lensProfile = pow(1.0 - normalizedRadius, 0.65);
                lensProfile *= centerFade * radialMask * textureMask;

                float flowVariation = saturate(
                    dot(flowDetail, float2(0.7071, 0.7071)) * 0.5 + 0.5);
                float swirlVariation = lerp(0.45, 1.0, flowVariation);

                float pulse = 1.0 + sin(_TimeParameters.x * _PulseSpeed) * _PulseAmount;
                float2 radialDistortion = -radialDirection * _Strength;
                float2 swirlDistortion = tangentDirection
                    * (_SwirlStrength * swirlVariation);
                float2 detailDistortion = flowDetail * _FlowStrength;
                float2 distortion = (radialDistortion
                    + swirlDistortion
                    + detailDistortion)
                    * (lensProfile * pulse);
                float blur = saturate(_Blur * mask);

                float4 distortionBuffer;
                EncodeDistortion(distortion, blur, mask > 0.001, distortionBuffer);
                return distortionBuffer;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
