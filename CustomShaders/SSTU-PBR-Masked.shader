Shader "SSTU/PBR/Masked"
{
	Properties 
	{
		_MainTex("_MainTex (RGB)", 2D) = "white" {}
		_MaskTex("_MaskTex (Grayscale)", 2D) = "black" {}
		_SpecMap("_SpecMap (RGB)", 2D) = "white" {}
		_BumpMap("_BumpMap (NRM)", 2D) = "bump" {}
		_AOMap("_AOMap (Grayscale)", 2D) = "white" {}
		_MaskColor1 ("Mask Color 1", Color) = (1,1,1,1)
		_MaskColor2 ("Mask Color 2", Color) = (1,1,1,1)
		_MaskColor3 ("Mask Color 3", Color) = (1,1,1,1)
		_MaskMetallic ("Mask Metals", Vector) = (0,0,0,1)
		_Opacity("Emission Opacity", Range(0,1) ) = 1
		_RimFalloff("_RimFalloff", Range(0.01,5) ) = 0.1
		_RimColor("_RimColor", Color) = (0,0,0,0)
		_TemperatureColor("_TemperatureColor", Color) = (0,0,0,0)
		_BurnColor ("Burn Color", Color) = (1,1,1,1)
	}
	
	SubShader
	{
		Tags {"RenderType"="Opaque"}
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha

		CGPROGRAM

		#pragma surface surf Standard keepalpha
		#pragma target 3.0
		#include "SSTUShaders.cginc"
		
		//ColoredSpecular lighting model and surface data struct found in SSTUShaders.cginc
		
		sampler2D _MainTex;
		sampler2D _MaskTex;
		sampler2D _SpecMap;
		sampler2D _BumpMap;		
		sampler2D _AOMap;

		float _Opacity;
		float4 _MaskColor1;
		float4 _MaskColor2;
		float4 _MaskColor3;
		float4 _MaskMetallic;
		float4 _TemperatureColor;
		float4 _RimColor;
		float _RimFalloff;
		
		struct Input
		{
			float2 uv_MainTex;
			float3 viewDir;
		};

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
			fixed4 color = tex2D(_MainTex,(IN.uv_MainTex));
			fixed3 mask = tex2D(_MaskTex, (IN.uv_MainTex));
			fixed4 spec = tex2D(_SpecMap, (IN.uv_MainTex));
			fixed3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
			fixed4 ao = tex2D(_AOMap, (IN.uv_MainTex));
						
			fixed m = saturate(1 - (mask.r + mask.g + mask.b));
			fixed3 userColor = mask.rrr * _MaskColor1.rgb + mask.ggg * _MaskColor2.rgb + mask.bbb * _MaskColor3.rgb;
			fixed3 diffuseColor = color.rgb * m;
			fixed3 detailColor = (color.rgb - 0.5) * (1 - m);			
			
			fixed3 userSpec = mask.r * _MaskColor1.a + mask.g * _MaskColor2.a + mask.b * _MaskColor3.a;
			fixed3 baseSpec = spec.rgb * m;
			fixed3 detailSpec = (spec.rgb - 0.5) * (1 - m);
			
			//user metallic input
			fixed userMetallic = mask.r * _MaskMetallic.r + mask.g * _MaskMetallic.g + mask.b * _MaskMetallic.b;
			fixed baseMetallic = spec.a * m;
			fixed detailMetallic = (spec.a - 0.5) * (1 - m);
			
			o.Albedo = saturate(userColor + diffuseColor + detailColor);			
			o.Smoothness = saturate(userSpec + baseSpec + detailSpec).r;
			o.Metallic = saturate(userMetallic + baseMetallic + detailMetallic);
			o.Occlusion = ao.r;
			o.Normal = normal;
			o.Emission = stockEmit(IN.viewDir, normal, _RimColor, _RimFalloff, _TemperatureColor) * _Opacity;
			o.Alpha = _Opacity;
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}