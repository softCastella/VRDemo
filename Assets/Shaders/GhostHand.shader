Shader "Custom/GhostHand"
{
    Properties
    {
        [Header(Ghost Base)]
        _BaseColor ("Base Color", Color) = (0.72, 0.88, 1.0, 0.22)
        [Header(Rim Glow)]
        _RimColor ("Rim Color", Color) = (0.85, 0.95, 1.0, 0.85)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.2
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 1.35
        [Header(Opacity)]
        _Opacity ("Opacity", Range(0, 1)) = 0.55
        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Float) = 1.4
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.18
        [Header(Flicker)]
        _FlickerSpeed ("Flicker Speed", Float) = 6.0
        _FlickerAmount ("Flicker Amount", Range(0, 0.35)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _Opacity;
                half _PulseSpeed;
                half _PulseAmount;
                half _FlickerSpeed;
                half _FlickerAmount;
            CBUFFER_END

            struct AttributesD
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsD
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsD vertDepth(AttributesD input)
            {
                VaryingsD output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetVertexPositionInputs(input.positionOS.xyz).positionCS;
                return output;
            }

            half4 fragDepth(VaryingsD input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half _Opacity;
                half _PulseSpeed;
                half _PulseAmount;
                half _FlickerSpeed;
                half _FlickerAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half GhostFlicker(float3 positionWS, half speed, half amount)
            {
                half n = frac(sin(dot(positionWS.xz, float2(12.9898, 78.233)) + _Time.y * speed) * 43758.5453);
                return 1.0 - amount * n;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _RimPower) * _RimIntensity;

                half pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                half pulseMod = lerp(1.0 - _PulseAmount, 1.0, pulse);
                half flicker = GhostFlicker(input.positionWS, _FlickerSpeed, _FlickerAmount);

                half3 color = _BaseColor.rgb + _RimColor.rgb * fresnel;
                half alpha = saturate((_BaseColor.a + fresnel * _RimColor.a) * _Opacity * pulseMod * flicker);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
