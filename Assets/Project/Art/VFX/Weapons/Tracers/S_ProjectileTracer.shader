Shader "Breachpoint/VFX/Projectile Tracer"
{
    Properties
    {
        [HDR] _TracerColor("Tracer Color", Color) = (1.0, 0.9, 0.65, 1)
        _Emission("Emission", Range(0, 20)) = 8
        _EdgeSoftness("Edge Softness", Range(0.01, 1)) = 0.42
        _EndSoftness("End Softness", Range(0.01, 0.5)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+30"
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

            CBUFFER_START(UnityPerMaterial)
                float4 _TracerColor;
                float _Emission;
                float _EdgeSoftness;
                float _EndSoftness;
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
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float distanceFromCenter = abs(input.uv.y * 2.0 - 1.0);
                float softEdgeStart = saturate(1.0 - _EdgeSoftness);
                float crossSection = 1.0 - smoothstep(
                    softEdgeStart,
                    1.0,
                    distanceFromCenter);

                float startMask = smoothstep(0.0, _EndSoftness, input.uv.x);
                float endMask = smoothstep(
                    0.0,
                    _EndSoftness,
                    1.0 - input.uv.x);
                float shape = crossSection * startMask * endMask;

                float core = 1.0 - smoothstep(0.0, 0.32, distanceFromCenter);
                float intensity = shape * (1.0 + core * 1.6);
                float3 color = _TracerColor.rgb * _Emission * input.color.rgb;
                float alpha = intensity * _TracerColor.a * input.color.a;

                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
