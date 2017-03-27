Shader "SSTU/MaskedEmissive"
{
	Properties 
	{
		_MainTex("_MainTex (RGB)", 2D) = "white" {}
		_MaskTex("_MaskTex (Grayscale)", 2D) = "white" {}
		_SpecMap("_SpecMap (RGB)", 2D) = "white" {}
		_BumpMap("_BumpMap (NRM)", 2D) = "bump" {}
		_AOMap("_AOMap (Grayscale)", 2D) = "white" {}
		_EmissiveMap("_EmissiveMap (RGB)", 2D) = "black" {}
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
		
		struct CustomSurfaceOutput {
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Specular;
			half3 GlossColor;
			half Alpha;
		};
		
		inline half4 LightingColoredSpecular (CustomSurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
		{
			//diffuse light intensity, from surface normal and light direction
			half diff = max (0, dot (s.Normal, lightDir));
			//specular light calculations
			half3 h = normalize (lightDir + viewDir);
			float nh = max (0, dot (s.Normal, h));
			float spec = pow (nh, s.Specular * 128);
			half3 specCol = spec * s.GlossColor;
			
			//output fragment color; Unity adds Emission to it through some other method
			half4 c;
			c.rgb = (s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * specCol) * atten;
			c.a = s.Alpha;
			return c;
		}
		 
		inline half4 LightingColoredSpecular_PrePass (CustomSurfaceOutput s, half4 light)
		{
			half3 spec = light.a * s.GlossColor;
		   
			half4 c;
			c.rgb = (s.Albedo * light.rgb + light.rgb * spec) * 0.5;
			c.a = s.Alpha + spec * _SpecColor.a;
			return c;
		}
		
		sampler2D _MainTex;
		sampler2D _MaskTex;
		sampler2D _SpecMap;
		sampler2D _BumpMap;		
		sampler2D _AOMap;
		sampler2D _EmissiveMap;

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
			float2 uv_EmissiveMap
			float3 viewDir;
		};

		void surf (Input IN, inout CustomSurfaceOutput o)
		{
			float4 color = tex2D(_MainTex,(IN.uv_MainTex)) * _BurnColor;
			float4 maskColor = tex2D(_MaskTex, (IN.uv_MaskTex));
			float4 spec = tex2D(_SpecMap, (IN.uv_SpecMap));
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
			float3 ao = tex2D(_AOMap, (IN.uv_AOMap));
			float3 emit = tex2D(_EmissiveMap, (IN.uv_EmissiveMap));
			
			half rim = 1.0 - saturate(dot (normalize(IN.viewDir), normal));			
			float3 emission = _RimColor.rgb * pow(rim, _RimFalloff) * _RimColor.a + _TemperatureColor.rgb * _TemperatureColor.a + emit;

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