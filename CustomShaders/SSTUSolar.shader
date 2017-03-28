Shader "SSTU/SolarShader"
{
	Properties 
	{
		_MainTex("_MainTex (RGB)", 2D) = "white" {}
		_SpecMap("_SpecMap ", 2D) = "white" {}
		_BumpMap("_BumpMap", 2D) = "bump" {}
		_Emissive("_Emissive", 2D) = "black" {}
		_AOMap("_AOMap Grayscale", 2D) = "white" {}
		_Color ("Main Color", Color) = (1,1,1,1)
		_Shininess ("Shininess", Range (0.03, 1)) = 0.078125		
		_Opacity("Opacity", Range(0,1) ) = 1
		_RimFalloff("Rim Falloff", Range(0.01,5) ) = 0.1
		_RimColor("Rim Color", Color) = (0,0,0,0)		
		_BacklightClamp("Backlight Clamp", Range(0,1) ) = 0.5		
		_TemperatureColor("Temperature Color", Color) = (0,0,0,0)
		_BurnColor ("Burn Color", Color) = (1,1,1,1)
	}
	
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM

		#pragma surface surf ColoredSolar
		#pragma target 3.0
		#include "SSTUShaders.cginc"

		sampler2D _MainTex;
		sampler2D _SpecMap;
		sampler2D _BumpMap;	
		sampler2D _Emissive;	
		sampler2D _AOMap;

		half _Shininess;
		float _Opacity;
		float4 _Color;
		float4 _TemperatureColor;
		float4 _BurnColor;
		float4 _RimColor;
		float _RimFalloff;
		float _BacklightClamp;
		
		struct Input
		{
			float2 uv_MainTex;
			float2 uv_SpecMap;
			float2 uv_BumpMap;
			float2 uv_Emissive;
			float2 uv_AOMap;
			float3 viewDir;
		};

		void surf (Input IN, inout SolarSurfaceOutput o)
		{
			float4 color = tex2D(_MainTex,(IN.uv_MainTex)) * _BurnColor;
			float3 spec = tex2D(_SpecMap, (IN.uv_SpecMap));
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
			float3 glow = tex2D(_Emissive, (IN.uv_Emissive));
			float3 ao = tex2D(_AOMap, (IN.uv_AOMap));
			
			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));			
			float3 emission = _RimColor.rgb * pow(rim, _RimFalloff) * _RimColor.a + _TemperatureColor.rgb * _TemperatureColor.a;

			o.Albedo = color.rgb * ao.rgb * _Color.rgb;
			o.GlossColor = spec.rgb;
			o.Specular = _Shininess;
			o.Normal = normal;
			o.Emission = emission;
			o.Emission *= _Opacity;
			o.BackLight = glow * _RimFalloff;
			o.BackClamp = _BacklightClamp;
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}