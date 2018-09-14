Shader "Hexi/EmissiveTex" {
	Properties {
	    _MainTex ("Base (RGB)", 2D) = "white" {}
        _EmitScale ("Emission Level", Float) = 2
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 150

		CGPROGRAM
		#pragma surface surf Lambert noforwardadd

		struct Input {
			float3 viewDir;
	        float2 uv_MainTex;
		};

        sampler2D _MainTex;
        sampler2D _EmitTex;
        fixed _EmitScale;

		void surf (Input IN, inout SurfaceOutput o) {
	        fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			half rim = saturate(dot (normalize(IN.viewDir), o.Normal));
			half rim2 = pow(rim, 2);
			o.Albedo = c.rgb * (0.8 + rim2 * 0.2);
			o.Emission = c.rgb * (0.1 + rim2) * c.a * _EmitScale;
		}

		ENDCG
	}

	Fallback "KT/Mobile/DiffuseTint"
}
