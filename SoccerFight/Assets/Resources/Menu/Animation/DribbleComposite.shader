Shader "SportFighter/DribbleComposite"
{
    Properties { _MainTex ("Animation", 2D)="white" {} _OriginalTex ("Original", 2D)="white" {} _Restore ("Originalpose", Float)=0 }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_OriginalTex;
            float _Restore;
            fixed4 frag(v2f_img i):SV_Target
            {
                fixed4 original=tex2D(_OriginalTex,i.uv);
                fixed4 animated=tex2D(_MainTex,i.uv);
                // Die komplette rechte Oberfläche sowie Zurück bleiben pixelstabil.
                float fixedUi=smoothstep(0.575,0.59,i.uv.x);
                fixedUi=max(fixedUi,(1.0-smoothstep(0.075,0.105,i.uv.x))*smoothstep(0.84,0.89,i.uv.y));
                return lerp(animated,original,max(_Restore,fixedUi));
            }
            ENDCG
        }
    }
}
