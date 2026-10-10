Shader "ARPlanter/MoistureVolume"
{
    Properties
    {
        _MoistureTex ("Estimated soil moisture", 3D) = "white" {}
        _Opacity ("Density", Float) = 0.85
        _HasData ("All sensors fresh", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _Opacity, _HasData;
            CBUFFER_END
            TEXTURE3D(_MoistureTex);
            SAMPLER(sampler_MoistureTex);
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }
            float3 Heat(float t)
            {
                if (t < 0.33) return lerp(float3(0.08,0.16,0.95), float3(0,0.85,0.83), t / 0.33);
                if (t < 0.66) return lerp(float3(0,0.85,0.83), float3(0.8,0.96,0.12), (t - 0.33) / 0.33);
                return lerp(float3(0.8,0.96,0.12), float3(1,0.2,0.07), (t - 0.66) / 0.34);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                if (_HasData < 0.5) return half4(0,0,0,0);
                float3 origin = TransformWorldToObject(GetCameraPositionWS());
                float3 direction = normalize(input.positionOS - origin);
                float3 safeDir = float3(direction.x >= 0 ? 1 : -1, direction.y >= 0 ? 1 : -1, direction.z >= 0 ? 1 : -1) * max(abs(direction), 0.00001);
                float3 t0 = (-0.5 - origin) / safeDir;
                float3 t1 = (0.5 - origin) / safeDir;
                float3 nearT = min(t0, t1), farT = max(t0, t1);
                float entry = max(0, max(nearT.x, max(nearT.y, nearT.z)));
                float exitT = min(farT.x, min(farT.y, farT.z));
                if (exitT <= entry) return half4(0,0,0,0);
                float stepSize = (exitT - entry) / 32;
                float4 accumulated = 0;
                [loop] for (int i = 0; i < 32; i++)
                {
                    float3 p = origin + direction * (entry + (i + 0.5) * stepSize);
                    float moisture = SAMPLE_TEXTURE3D_LOD(_MoistureTex, sampler_MoistureTex, p + 0.5, 0).r;
                    float edge = max(abs(p.x), max(abs(p.y), abs(p.z))) * 2;
                    float envelope = 1 - smoothstep(0.94, 1, edge);
                    float alpha = 1 - exp(-envelope * _Opacity * stepSize * 2.5);
                    accumulated.rgb += (1 - accumulated.a) * Heat(saturate(moisture)) * alpha;
                    accumulated.a += (1 - accumulated.a) * alpha;
                }
                return accumulated;
            }
            ENDHLSL
        }
    }
}
