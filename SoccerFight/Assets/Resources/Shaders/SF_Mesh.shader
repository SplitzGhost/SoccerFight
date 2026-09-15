// Unlit shader for generated meshes, trails and lines (vertex colors only).
// Textures are premultiplied alpha. With _UseUvIntensity = 1 the particle system
// passes a per-vertex HDR multiplier in uv.z.
Shader "SoccerFight/Mesh"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        _UseUvIntensity ("Use UV.z Intensity", Float) = 0
        _EnvGraded ("Environment Grade", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
                float _UseUvIntensity;
                float _EnvGraded;
            CBUFFER_END

            // environment grade (stage themes): rgb' = M rgb + t * coverage, only on graded clones
            float4x4 _SF_EnvGrade;
            half3 EnvGrade(half3 rgb, half coverage)
            {
                float3 g = mul((float3x3)_SF_EnvGrade, (float3)rgb) + _SF_EnvGrade._m03_m13_m23 * coverage;
                return lerp(rgb, (half3)max(g, 0.0), (half)_EnvGraded);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv.xy = TRANSFORM_TEX(v.uv.xy, _MainTex);
                o.uv.z = lerp(1.0, v.uv.z, _UseUvIntensity) * _Intensity;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv.xy);
                return half4(EnvGrade(tex.rgb * i.color.rgb, tex.a) * (i.color.a * i.uv.z), tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
