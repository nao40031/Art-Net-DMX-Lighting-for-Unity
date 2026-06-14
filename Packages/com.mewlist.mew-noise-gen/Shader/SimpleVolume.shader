Shader "Mewlist/SimpleVolume" {
	Properties {
		_BaseColor ("BaseColor", Color) = (1,1,1,1)
        _Solidity ("Solidity", Range(0, 100)) = 0
		_Volume ("Albedo (RGB)", 3D) = "" {}
		_AlphaOffset ("Alpha Offset", Range(-1, 1)) = 0.0
		_Transparency ("Transparency", Range(0,1)) = 0.0
		_Iteration ("Iteration", Range(1,500)) = 0.0
		_Zoom ("Zoom", Range(0.1, 10)) = 1
	}

	SubShader {
		ColorMask 0
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		Cull Back
		Blend SrcAlpha OneMinusSrcAlpha
		Zwrite Off
		ZTest Off
        ColorMask RGB

		CGPROGRAM
		#pragma surface surf NoLighting noforwardadd noambient alpha vertex:vert
		#pragma target 3.0

		sampler3D _Volume;
        float4 _Volume_ST;

		struct Input {
			float3 worldPos;
			float3 localPos;
			float3 worldNormal;
			float4 posScreen;
		};

        sampler2D _CameraDepthTexture;
        float4 _CameraDepthTexture_ST;

		half _Transparency;
		fixed4 _BaseColor;
		float _AlphaOffset;
		float _Iteration;
		float _AlphaPower;
		float _Zoom;
		float _Solidity;
		
		UNITY_INSTANCING_BUFFER_START(Props)
		UNITY_INSTANCING_BUFFER_END(Props)

        inline float4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten)
        {
            fixed4 c;
            c.rgb = s.Albedo;
            c.a = s.Alpha;
            return c;
        }

		void vert (inout appdata_full i, out Input o)
		{ 
		    float3 localPos = i.vertex;
		    UNITY_INITIALIZE_OUTPUT(Input, o);
		    o.localPos = localPos;
            o.posScreen = UnityObjectToClipPos(i.vertex);
		}
		
		float4 RayMarch(float3 from, float3 dir)
		{
		    float _CubeSize = 0.5;
		    float4 density = float4(0,0,0,0);
		    float4 color = _BaseColor;
		    float3 ray = from;
		    float3 offset = float3(_CubeSize, _CubeSize, _CubeSize);
		    density = 0;
		    for (int i = 0; i <= _Iteration; i++)
		    {
                ray = from + (_Iteration - i) * dir * 2 * _CubeSize / _Iteration;
		        float3 light = float3(1, -1, 0);
		        float3 scale = float3(_Volume_ST.x, _Volume_ST.y, _Volume_ST.x) / _Zoom;
		        float3 uv = ray * scale + offset;
    			fixed4 tex = tex3D (_Volume, uv);
    			fixed4 texslope = tex3D (_Volume, uv + 0.01 * light);
    			float4 slope = saturate(texslope - tex);
    			if (-_CubeSize <= ray.x && ray.x <= _CubeSize)
    			if (-_CubeSize <= ray.y && ray.y <= _CubeSize)
    			if (-_CubeSize <= ray.z && ray.z <= _CubeSize)
    			{
    			    float alphaScale = 1 / saturate(1 - _AlphaOffset);
    			    float4 a = alphaScale * pow(saturate(tex + _AlphaOffset), 0.0001 + _Transparency);
        			density += (1 - density) * a / _Iteration;
        			
        			float l = a / _Iteration * _Solidity;
        			float4 c = (1 - slope) * _BaseColor + slope * 100.0;
                    color = color * (1 - l) + c * l;
                }
		    }
		    color.a = max(density.r, max(density.g, density.b));
		    return color * density;
		}

		void surf (Input IN, inout SurfaceOutput o) {
		    float3 worldPos = IN.worldPos;
		    float3 cameraPos = _WorldSpaceCameraPos;

			float3 cameraToWorld = worldPos - cameraPos;
			float3 cameraDir = normalize(cameraToWorld);
            float3 cameraLocalPos = mul(unity_WorldToObject, float4(cameraPos, 1.0));
            float3 localCameraDir = normalize(IN.localPos - cameraLocalPos);

            float4 result = RayMarch(IN.localPos, localCameraDir);
		    o.Albedo.rgb = result.rgb;
		    o.Alpha = result.a;
		}

		ENDCG
	}
	FallBack "Diffuse"
}
