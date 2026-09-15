// Vegetation shader for the static foliage meshes.
// Every vertex carries a bend weight (0 at the root / anchor, 1 at the tip), a phase, a stiffness
// and an interaction amount (uv1). The GPU animates travelling wind gusts, fine leaf flutter and
// pushes plants away from the player and the ball — thousands of blades for one draw call.
// Textures are premultiplied alpha. _Glow = 1 turns the mesh into a softly pulsing emissive layer.
Shader "SoccerFight/Foliage"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        _Glow ("Glow Pulse", Float) = 0
        _FogColor ("Fog Color", Color) = (0.47, 0.79, 0.83, 1)
        _FogAmount ("Fog Amount", Range(0, 1)) = 0
        _WindScale ("Wind Scale", Float) = 1
        _Tint ("Depth Tint", Color) = (1, 1, 1, 1)
        _EnvGraded ("Environment Grade", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
                float2 uv : TEXCOORD0;
                float4 bend : TEXCOORD1;   // x weight, y phase, z stiffness, w push
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float pulse : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Intensity;
                float _Glow;
                half4 _FogColor;
                float _FogAmount;
                float _WindScale;
                half4 _Tint;
                float _EnvGraded;
            CBUFFER_END

            // environment grade (stage themes): rgb' = M rgb + t * coverage, only on graded clones
            float4x4 _SF_EnvGrade;
            half3 EnvGrade(half3 rgb, half coverage)
            {
                float3 g = mul((float3x3)_SF_EnvGrade, (float3)rgb) + _SF_EnvGrade._m03_m13_m23 * coverage;
                return lerp(rgb, (half3)max(g, 0.0), (half)_EnvGraded);
            }

            // set every frame by WorldEnvironment
            float4 _SF_Wind;   // x base lean, y gust strength, z flutter strength, w time
            float4 _SF_Push0;  // xy position, z radius, w strength  (player)
            float4 _SF_Push1;  // ball

            Varyings vert(Attributes v)
            {
                Varyings o;
                float w = v.bend.x;
                float phase = v.bend.y;
                float stiff = v.bend.z;
                float t = _SF_Wind.w;
                float lx = v.positionOS.x;   // layer-local x: waves stay put when parallax layers scroll

                // gusts travel across the scene; flutter is fast and per-plant
                float gust = sin(t * 0.85 - lx * 0.21 + phase * 0.35) * 0.55
                           + sin(t * 1.95 - lx * 0.53 + phase * 1.3) * 0.28
                           + sin(t * 0.31 - lx * 0.07) * 0.35;
                float flutter = sin(t * 5.7 + phase * 6.2832 + v.positionOS.y * 4.0) * 0.5
                              + sin(t * 9.3 + phase * 3.1) * 0.25;
                float sway = (_SF_Wind.x + gust * _SF_Wind.y + flutter * _SF_Wind.z) * w * stiff * _WindScale;

                float3 wpos = TransformObjectToWorld(v.positionOS);

                // plants bend away from the player and the ball
                float2 d0 = wpos.xy - _SF_Push0.xy;
                float f0 = saturate(1.0 - abs(d0.x) / max(_SF_Push0.z, 1e-3)) * saturate(1.0 - abs(d0.y) / 1.5) * _SF_Push0.w;
                float2 d1 = wpos.xy - _SF_Push1.xy;
                float f1 = saturate(1.0 - abs(d1.x) / max(_SF_Push1.z, 1e-3)) * saturate(1.0 - abs(d1.y) / 0.9) * _SF_Push1.w;
                sway += (sign(d0.x) * f0 + sign(d1.x) * f1) * w * v.bend.w;

                wpos.x += sway;
                wpos.y -= abs(sway) * 0.28 * w;   // bending shortens the plant slightly

                o.positionCS = TransformWorldToHClip(wpos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                float p = 0.6 + 0.4 * sin(t * (1.1 + frac(phase * 7.13) * 1.6) + phase * 6.2832);
                o.pulse = lerp(1.0, p, _Glow);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half a = tex.a * i.color.a;
                half3 rgb = tex.rgb * i.color.rgb;
                rgb = EnvGrade(lerp(rgb, _FogColor.rgb * tex.a, _FogAmount) * _Tint.rgb, tex.a);
                return half4(rgb * (i.color.a * _Intensity * i.pulse), a);
            }
            ENDHLSL
        }
    }
}
