Shader "SportFighter/CharacterIdleLoop"
{
    Properties
    {
        _MainTex ("Originalansicht", 2D) = "white" {}
        _LoopPhase ("Schleifenphase", Float) = 0
        _Character ("Charakter", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _LoopPhase, _Character;

            float glow(float2 p, float2 center, float2 radius)
            {
                float2 d = (p-center)/radius;
                return exp(-dot(d,d)*3.0);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 p = float2(i.uv.x*1672.0, (1.0-i.uv.y)*941.0);
                float t = _LoopPhase*6.28318530718;
                float offset = _Character*0.73;
                // Sehr kleine, weiche Bewegungen. Boden, Ballauflage und rechte UI bleiben fest.
                float center = 510.0 + (_Character == 2.0 ? 12.0 : 0.0);
                float width = lerp(230.0, 150.0, 1.0-smoothstep(220.0,650.0,p.y));
                float body = 1.0-smoothstep(width*0.73,width,abs(p.x-center));
                body *= smoothstep(35.0,90.0,p.y)*(1.0-smoothstep(810.0,890.0,p.y));
                float anchor = saturate((885.0-p.y)/720.0);
                float breath = sin(t*2.0+offset);
                float sway = sin(t+offset)*2.6;
                float2 move = float2(sway*anchor, breath*(2.2+anchor*2.0));
                move.x += (p.x-center)*breath*0.0035;
                float2 samplePos = p-move*body;
                fixed4 col = tex2D(_MainTex,float2(samplePos.x/1672.0,1.0-samplePos.y/941.0));

                // Fackelschein nur im linken Ruinenhof; keine pulsierenden Menüsymbole.
                float flame = 0.035*sin(t*7.0+offset)+0.022*sin(t*11.0+1.4);
                float fire = glow(p,float2(25,384),float2(90,140));
                fire += glow(p,float2(179,458),float2(75,125));
                fire += glow(p,float2(340,586),float2(80,115));
                col.rgb += float3(1.0,0.53,0.16)*fire*flame;

                // Einzelne langsame Lichtpunkte bewegen sich auf geschlossenen Bahnen.
                float dust = 0.0;
                for (int n=0;n<9;n++)
                {
                    float a = n*2.399963+offset;
                    float2 pos = float2(110.0+fmod(n*137.0,710.0),260.0+fmod(n*97.0,500.0));
                    pos += float2(sin(t+a)*13.0,cos(t+a)*22.0);
                    float twinkle = pow(0.5+0.5*sin(t*2.0+a),2.0);
                    dust += glow(p,pos,float2(2.5,2.5))*twinkle;
                }
                // Partikel hinter dem Motiv wirken außerhalb der Körpermitte am stärksten.
                col.rgb += float3(0.55,0.88,1.0)*dust*(1.0-body*0.8)*0.32;
                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }
}
