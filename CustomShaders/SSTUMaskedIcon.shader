Shader "SSTU/MaskedIcon"
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
		_Shininess ("Specular Shininess", Range (0.03, 1)) = 0.078125
		_MinX ("MinX", Range(0.000000,1.000000)) = 0.000000
		_MaxX ("MaxX", Range(0.000000,1.000000)) = 1.000000
		_MinY ("MinY", Range(0.000000,1.000000)) = 0.000000
		_MaxY ("MaxY", Range(0.000000,1.000000)) = 1.000000
	}
	
	SubShader
	{
		Tags { "QUEUE"="AlphaTest" "RenderType"="TransparentCutout" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM

		#pragma surface surf ColoredSpecular
		#pragma target 3.0
		#include "SSTUShaders.cginc"
		
		//ColoredSpecular lighting model and surface data struct found in SSTUShaders.cginc
		
		sampler2D _MainTex;
		sampler2D _MaskTex;
		sampler2D _SpecMap;
		sampler2D _BumpMap;		
		sampler2D _AOMap;

		half _Shininess;
		float4 _MaskColor1;
		float4 _MaskColor2;
		float4 _MaskColor3;
		float _MinX;
		float _MaxX;
		float _MinY;
		float _MaxY;
		
		struct Input
		{
			float2 uv_MainTex;
			float3 viewDir;
			float4 screenPos;
		};

		void surf (Input IN, inout ColoredSpecularSurfaceOutput o)
		{
			float4 color = tex2D(_MainTex,(IN.uv_MainTex));
			float4 mask = tex2D(_MaskTex, (IN.uv_MainTex));
			float4 spec = tex2D(_SpecMap, (IN.uv_MainTex));
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
			float3 ao = tex2D(_AOMap, (IN.uv_MainTex));
			
			float m = 1 - (mask.r + mask.g + mask.b);
			o.Albedo.r = (mask.r * _MaskColor1.r * _MaskColor1.a + mask.g * _MaskColor2.r * _MaskColor2.a + mask.b * _MaskColor3.r * _MaskColor3.a) * color.r * ao.r + color.r * m * ao.r;
			o.Albedo.g = (mask.r * _MaskColor1.g * _MaskColor1.a + mask.g * _MaskColor2.g * _MaskColor2.a + mask.b * _MaskColor3.g * _MaskColor3.a) * color.g * ao.g + color.g * m * ao.g;
			o.Albedo.b = (mask.r * _MaskColor1.b * _MaskColor1.a + mask.g * _MaskColor2.b * _MaskColor2.a + mask.b * _MaskColor3.b * _MaskColor3.a) * color.b * ao.b + color.b * m * ao.b;
			o.GlossColor = spec.rgb;
			o.Specular = _Shininess;
			o.Normal = normal;
			
			float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
			if(screenUV.x < _MinX || screenUV.y < _MinY || screenUV.x > _MaxX || screenUV.y > _MaxY)
			{
				clip(-1);
			}
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}