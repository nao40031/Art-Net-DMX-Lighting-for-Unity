Shader "Hidden/ArtNet/FrostCookie"
{
    Properties { _MainTex ("Cookie", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Blur;

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 d = float2(_Blur, 0.0);
                float4 color = tex2D(_MainTex, input.uv) * 0.20;
                color += tex2D(_MainTex, input.uv + d) * 0.10;
                color += tex2D(_MainTex, input.uv - d) * 0.10;
                color += tex2D(_MainTex, input.uv + d.yx) * 0.10;
                color += tex2D(_MainTex, input.uv - d.yx) * 0.10;
                float2 q = d * 0.70710678;
                color += tex2D(_MainTex, input.uv + float2(q.x, q.x)) * 0.10;
                color += tex2D(_MainTex, input.uv + float2(q.x, -q.x)) * 0.10;
                color += tex2D(_MainTex, input.uv + float2(-q.x, q.x)) * 0.10;
                color += tex2D(_MainTex, input.uv - float2(q.x, q.x)) * 0.10;
                return color;
            }
            ENDHLSL
        }
    }
}
