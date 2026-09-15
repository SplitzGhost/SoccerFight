// Character sprite shader. Same premultiplied-alpha contract as SoccerFight/Sprite, plus scene
// lighting so the rigged player sits in the moonlit world instead of looking pasted on:
//  - moon rim: edges that face a world-space direction are lit by cool light. The edge test samples the
//    alpha a little way towards the light in the sprite's own UV space (mapped through screen-space
//    derivatives), so it stays correct for every bone rotation and for the facing flip.
//  - bounce: a faint teal fill on edges that face the grass
//  - ambient: cool tint and contact darkening close to the pitch (world y = 0)
// _Solid = 1 fills the sprite with the renderer color (hit flashes).
Shader "SoccerFight/Character"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Solid ("Solid Fill", Range(0, 1)) = 0
        _RimColor ("Rim Color", Color) = (0.74, 0.95, 1, 1)
        _RimDir ("Rim Direction (world)", Vector) = (0.62, 0.78, 0, 0)
        _RimWidth ("Rim Width (world units)", Float) = 0.055
        _RimStrength ("Rim Strength", Float) = 0.85
        _BounceColor ("Bounce Color", Color) = (0.3, 0.66, 0.54, 1)
        _BounceWidth ("Bounce Width (world units)", Float) = 0.04
        _BounceStrength ("Bounce Strength", Float) = 0.4
        _Ambient ("Ambient Tint", Color) = (0.8, 0.87, 0.95, 1)
        _GroundColor ("Ground Occlusion", Color) = (0.6, 0.7, 0.76, 1)
        _GroundHeight ("Occlusion Height", Float) = 0.5
        _FloorY ("Floor Height (set per frame)", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend One OneMinusSrcAlpha
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
                float2 worldXY : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Solid;
                half4 _RimColor;
                float4 _RimDir;
                float _RimWidth;
                float _RimStrength;
                half4 _BounceColor;
                float _BounceWidth;
                float _BounceStrength;
                half4 _Ambient;
                half4 _GroundColor;
                float _GroundHeight;
                float _FloorY;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.worldXY = TransformObjectToWorld(input.positionOS).xy;
                return o;
            }

            // UV offset that corresponds to a world-space offset, through the screen-space Jacobian
            // (sprites are affine, so the derivatives are constant across each part).
            float2 WorldToUvOffset(float2 uv, float2 worldXY, float2 worldOffset)
            {
                float2 uvdx = ddx(uv), uvdy = ddy(uv);
                float2 wdx = ddx(worldXY), wdy = ddy(worldXY);
                float det = wdx.x * wdy.y - wdy.x * wdx.y;
                if (abs(det) < 1e-12) return float2(0, 0);
                float px = (wdy.y * worldOffset.x - wdy.x * worldOffset.y) / det;
                float py = (wdx.x * worldOffset.y - wdx.y * worldOffset.x) / det;
                return uvdx * px + uvdy * py;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half a = tex.a;

                // edges facing the moon: three taps for a soft falloff. The band is wide enough to reach
                // past the dark contour into the coloured surface.
                float2 rimOff = WorldToUvOffset(i.uv, i.worldXY, normalize(_RimDir.xy) * _RimWidth);
                half r1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + rimOff * 0.34).a;
                half r2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + rimOff * 0.67).a;
                half r3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + rimOff).a;
                half rim = saturate(a - (r1 + r2 + r3) * (half)0.3333);

                // edges facing the grass
                float2 bounceOff = WorldToUvOffset(i.uv, i.worldXY, float2(0, -_BounceWidth));
                half bounce = saturate(a - SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + bounceOff).a);

                // light, not paint: the surface colour is multiplied by ambient + moon + bounce, so the
                // dark contour stays dark where the moon hits it instead of turning into a pale outline
                half3 light = _Ambient.rgb * lerp(_GroundColor.rgb, half3(1, 1, 1), (half)smoothstep(_FloorY, _FloorY + _GroundHeight, i.worldXY.y));
                light += _RimColor.rgb * (rim * (half)_RimStrength) + _BounceColor.rgb * (bounce * (half)_BounceStrength);
                half3 lit = tex.rgb * light + _RimColor.rgb * (a * rim * (half)0.06);

                half3 rgb = lerp(lit, tex.aaa, (half)_Solid) * i.color.rgb;
                return half4(rgb * i.color.a, a * i.color.a);
            }
            ENDHLSL
        }
    }
}
