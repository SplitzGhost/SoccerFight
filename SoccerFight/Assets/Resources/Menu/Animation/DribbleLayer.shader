Shader "SportFighter/DribbleLayer"
{
    Properties { _MainTex ("Ebene", 2D) = "white" {} _SoftCutout ("Saubere Freistellkante", Float) = 0 _Shadow ("Bodenschatten", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _SoftCutout,_Shadow;
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
            fixed4 frag(Output i):SV_Target
            {
                if (_Shadow>0.0)
                {
                    float2 p=(i.uv-0.5)*2.0;
                    return fixed4(0.025,0.075,0.09,exp(-dot(p,p)*3.5)*_Shadow);
                }
                fixed4 c=tex2D(_MainTex,i.uv);
                if (_SoftCutout>0.5)
                {
                    float2 d=_MainTex_TexelSize.xy*1.15;
                    float a=min(tex2D(_MainTex,i.uv+float2(d.x,0)).a,tex2D(_MainTex,i.uv-float2(d.x,0)).a);
                    a=min(a,min(tex2D(_MainTex,i.uv+float2(0,d.y)).a,tex2D(_MainTex,i.uv-float2(0,d.y)).a));
                    c.a=smoothstep(0.04,0.65,min(c.a,a));
                }
                return c;
            }
            ENDCG
        }
    }
}
