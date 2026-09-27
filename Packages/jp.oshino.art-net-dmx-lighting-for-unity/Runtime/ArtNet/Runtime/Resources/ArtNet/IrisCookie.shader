Shader "Hidden/ArtNet/IrisCookie"
{
    Properties { _MainTex ("Source", 2D) = "white" {} _IrisShape ("Iris", Vector) = (1,0.01,0,0) }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "IrisMask.hlsl"
            sampler2D _MainTex;
            float4 _IrisShape;
            float4 frag(v2f_img i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * IrisTransmission(i.uv, _IrisShape);
            }
            ENDCG
        }
    }
    Fallback Off
}
