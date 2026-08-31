Shader "Breachpoint/VFX/Bullet Air Trail"
{
    Properties
    {
        [MainTexture] _Noise("Noise", 2D) = "white" {}
        [HDR] _TrailColor("Trail Color", Color) = (1.2, 1.25, 1.3, 1)
        _Tiling("Tiling", Vector) = (1, 1, 0, 0)
        _NoiseSpeed("Noise Speed", Vector) = (-0.5, 0.01, 0, 0)
        _Dissolve("Dissolve", Range(0, 1)) = 0.18
        _DissolveSoftness("Dissolve Softness", Range(0.001, 1)) = 0.42
        _PathDissolve("Path Dissolve", Range(0.001, 0.5)) = 0.12
        _Emission("Emission", Range(0, 10)) = 1.35
        _Opacity("Opacity", Range(0, 1)) = 1
        _NoiseOffset("Noise Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            CBUFFER_START(UnityPerMaterial)
                float4 _Noise_ST;
                float4 _TrailColor;
                float4 _Tiling;
                float4 _NoiseSpeed;
                float _Dissolve;
                float _DissolveSoftness;
                float _PathDissolve;
                float _Emission;
                float _Opacity;
                float _NoiseOffset;
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
                float2 scrollingUv = input.uv * _Tiling.xy;
                scrollingUv += _TimeParameters.x * _NoiseSpeed.xy;
                scrollingUv.x += _NoiseOffset;

                float primaryNoise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, scrollingUv).r;
                float detailNoise = SAMPLE_TEXTURE2D(
                    _Noise,
                    sampler_Noise,
                    scrollingUv * float2(1.83, 1.27) + float2(0.37, 0.61)).r;
                float noise = saturate(primaryNoise * 0.72 + detailNoise * 0.38);

                float edgeDistance = 1.0 - abs(input.uv.y * 2.0 - 1.0);
                float softEdge = smoothstep(0.0, 0.48, edgeDistance);
                softEdge *= lerp(0.72, 1.0, softEdge);

                float pathStart = smoothstep(0.0, _PathDissolve, input.uv.x);
                float pathEnd = smoothstep(0.0, _PathDissolve, 1.0 - input.uv.x);
                float pathMask = pathStart * pathEnd;

                float dissolve = smoothstep(
                    _Dissolve,
                    _Dissolve + max(_DissolveSoftness, 0.001),
                    noise);
                float vapor = saturate(dissolve * 0.74 + noise * 0.26);
                float alpha = vapor * softEdge * pathMask * input.color.a *
                              _TrailColor.a * _Opacity;

                float3 color = _TrailColor.rgb * _Emission;
                color *= lerp(0.58, 1.0, noise) * input.color.rgb;
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
}
