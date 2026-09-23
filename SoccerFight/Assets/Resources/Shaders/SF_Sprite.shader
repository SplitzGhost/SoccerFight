// Unlit sprite shader for SpriteRenderers.
// Textures are premultiplied alpha. _Intensity pushes colors into HDR so Bloom picks them up.
// _Solid = 1 fills the sprite with the renderer color (hit flashes, afterimages).
// Blend is configurable: One/OneMinusSrcAlpha = normal, One/One = additive.
Shader "SoccerFight/Sprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        _Solid ("Solid Fill", Range(0, 1)) = 0
        _EnvGraded ("Environment Grade", Float) = 0
        _Haze ("Haze Amount", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Solid;
                float _EnvGraded;
                float _Haze;
            CBUFFER_END

            // aerial perspective: far layers fade into the global haze colour (set per stage)
            half4 _SF_Haze;

            // environment grade (stage themes): rgb' = M rgb + t * coverage, only on graded clones
            // _EnvGraded = 1: the stage grade (backdrop), 2: the sky grade (the gradient maps onto the stage's sky colours)
            float4x4 _SF_EnvGrade;
            float4x4 _SF_SkyGrade;
            half3 EnvGrade(half3 rgb, half coverage)
            {
                float4x4 m = _EnvGraded > 1.5 ? _SF_SkyGrade : _SF_EnvGrade;
                float3 g = mul((float3x3)m, (float3)rgb) + m._m03_m13_m23 * coverage;
                return lerp(rgb, (half3)max(g, 0.0), (half)saturate(_EnvGraded));
            }

            Varyings vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 rgb = lerp(tex.rgb, tex.aaa, (half)_Solid) * i.color.rgb;
                rgb = lerp(EnvGrade(rgb, tex.a), _SF_Haze.rgb * tex.a, (half)_Haze);   // haze last: far layers fade into the stage's own haze colour
                return half4(rgb * (i.color.a * _Intensity), tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
