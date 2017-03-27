#include "SSTUColoredSpecularLighting.cginc"

Shader "SSTU/Masked"
{
	Properties 
	{
		_MainTex("_MainTex (RGB)", 2D) = "white" {}
		_MaskTex("_MaskTex (Grayscale)", 2D) = "white" {}
		_SpecMap("_SpecMap (RGB)", 2D) = "white" {}
		_BumpMap("_BumpMap (NRM)", 2D) = "bump" {}
		_AOMap("_AOMap (Grayscale)", 2D) = "white" {}
		_Color ("Diffuse Color", Color) = (1,1,1,1)
		_MaskColor ("Mask Color", Color) = (1,1,1,1)
		_Shininess ("Specular Shininess", Range (0.03, 1)) = 0.078125
		_Opacity("Emission Opacity", Range(0,1) ) = 1
		_RimFalloff("_RimFalloff", Range(0.01,5) ) = 0.1
		_RimColor("_RimColor", Color) = (0,0,0,0)
		_TemperatureColor("_TemperatureColor", Color) = (0,0,0,0)
		_BurnColor ("Burn Color", Color) = (1,1,1,1)
	}
	
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM

		#pragma surface surf ColoredSpecular
		#pragma target 3.0
		
		//colored specular lighting model found in SSTUColoredSpecularLighting.cginc
		
		sampler2D _MainTex;
		sampler2D _MaskTex;
		sampler2D _SpecMap;
		sampler2D _BumpMap;		
		sampler2D _AOMap;

		half _Shininess;
		float _Opacity;
		float4 _Color;
		float4 _MaskColor;
		float4 _TemperatureColor;
		float4 _BurnColor;
		float4 _RimColor;
		float _RimFalloff;
		
		struct Input
		{
			float2 uv_MainTex;
			float2 uv_MaskTex;
			float2 uv_SpecMap;
			float2 uv_BumpMap;
			float2 uv_AOMap;
			float3 viewDir;
		};

		void surf (Input IN, inout ColoredSpecularSurfaceOutput o)
		{
			float4 color = tex2D(_MainTex,(IN.uv_MainTex)) * _BurnColor;
			float4 maskColor = tex2D(_MaskTex, (IN.uv_MaskTex));
			float4 spec = tex2D(_SpecMap, (IN.uv_SpecMap));
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
			float3 ao = tex2D(_AOMap, (IN.uv_AOMap));
			
			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));			
			float3 emission = _RimColor.rgb * pow(rim, _RimFalloff) * _RimColor.a + _TemperatureColor.rgb * _TemperatureColor.a;

			o.Albedo = ((1 - maskColor.rbg) * color.rgb + maskColor.rbg * _MaskColor.rgb) * ao.rgb * _Color.rgb;
			o.GlossColor = spec.rgb;
			o.Specular = _Shininess;
			o.Normal = normal;
			o.Emission = emission;
			o.Emission *= _Opacity;
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}