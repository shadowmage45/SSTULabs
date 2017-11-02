Shader "SSTU/Standard2" {
	Properties {
		_MainTex("_MainTex (RGB)", 2D) = "white" {}
		_Metallic("_MetalRough (RGB)", 2D) = "white" {}
		_BumpMap("_BumpMap (RGB)", 2D) = "white" {}
		_Opacity("_Opacity", Range(0,1) ) = 1		
		_Color ("Main Color", Color) = (1,1,1,1)
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha
		
		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;
        sampler2D _Metallic;
		sampler2D _BumpMap;
		sampler2D _ValueMask;
		
		struct Input {
			float2 uv_MainTex;
			float2 uv_BumpMap;
			float2 uv_Emissive;
			float3 viewDir;
		};

		half _Shininess;
		fixed4 _Color;
		fixed4 _BurnColor;
		fixed4 _RimColor;
		fixed4 _TemperatureColor;
		float _Opacity;
		float _RimFalloff;
		float _Smoothness;
		float _ColorSaturation;
		float _GlobalBrightness;
        
        // struct SurfaceOutputStandard
        // {
            // fixed3 Albedo;		// base (diffuse or specular) color
            // fixed3 Normal;		// tangent space normal, if written
            // half3 Emission;
            // half Metallic;		// 0=non-metal, 1=metal
            // half Smoothness;	// 0=rough, 1=smooth
            // half Occlusion;		// occlusion (default 1)
            // fixed Alpha;		// alpha for transparencies
        // };

		void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex);
            
            fixed3 gammaColor = pow(_Color.rgb, 0.454545);
                        
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            
            fixed3 diffuse = albedo.rgb - pow(fixed3(0.5, 0.5, 0.5), 0.454545);
            
			o.Albedo.rgb = gammaColor + diffuse;
            o.Metallic = tex2D(_Metallic, IN.uv_MainTex).r;
			o.Smoothness = tex2D(_Metallic, IN.uv_MainTex).a;
            
			o.Normal = normal;
			o.Alpha = _Opacity;
		}
		ENDCG
	} 
	Fallback "Bumped Specular"
}
