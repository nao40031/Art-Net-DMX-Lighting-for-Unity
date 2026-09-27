Shader "Hidden/ArtNet/PrismCookieComposite"
{
    Properties
    {
        _GoboTex ("Gobo Texture", 2D) = "white" {}
        _FacetCount ("Facet Count", Float) = 1
        _Spread ("Spread", Float) = 0
        _FacetScale ("Facet Scale", Float) = 1
        _GoboRotationRad ("Gobo Rotation Rad", Float) = 0
        _GoboOffset ("Gobo Offset", Vector) = (0, 0, 0, 0)
        _PrismRotationRad ("Prism Rotation Rad", Float) = 0
        _IntensityScale ("Intensity Scale", Float) = 1
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

            sampler2D _GoboTex;
            float _FacetCount;
            float _Spread;
            float _FacetScale;
            float _GoboRotationRad;
            float4 _GoboOffset;
            float _PrismRotationRad;
            float _IntensityScale;

            float2 Rotate2D(float2 p, float a)
            {
                float s = sin(a);
                float c = cos(a);
                return float2((p.x * c) - (p.y * s), (p.x * s) + (p.y * c));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * 2.0;
                int count = clamp((int)round(_FacetCount), 1, 8);
                float scale = max(_FacetScale, 0.0001);
                fixed4 result = fixed4(0, 0, 0, 0);

                [unroll]
                for (int f = 0; f < 8; f++)
                {
                    if (f >= count)
                        continue;

                    float angle = ((6.28318530718 * f) / count) + _PrismRotationRad;
                    float2 center = float2(cos(angle), sin(angle)) * _Spread;
                    float2 local = p - center;
                    float2 rotatedLocal = Rotate2D(local, -_GoboRotationRad);
                    float2 sampleUv = (rotatedLocal / scale) * 0.5 + 0.5 + _GoboOffset.xy;

                    float inside = step(0.0, sampleUv.x) * step(sampleUv.x, 1.0) * step(0.0, sampleUv.y) * step(sampleUv.y, 1.0);
                    fixed4 sampleColor = tex2D(_GoboTex, sampleUv) * inside;
                    result = max(result, sampleColor);
                }

                return saturate(result * _IntensityScale);
            }
            ENDCG
        }
    }

    Fallback Off
}
