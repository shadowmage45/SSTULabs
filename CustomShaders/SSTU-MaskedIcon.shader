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
		_Multiplier("Multiplier", Float) = 2
	}
	
	SubShader
	{
		Tags { "QUEUE"="AlphaTest" "RenderType"="TransparentCutout" }
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha 

		CGPROGRAM

		#pragma surface surf Icon
		#pragma target 3.0
		#include "SSTUShaders.cginc"
		
		//ColoredSpecular lighting model and surface data struct found in SSTUShaders.cginc
		
		sampler2D _MainTex;
		sampler2D _MaskTex;
		sampler2D _SpecMap;
		sampler2D _BumpMap;		
		sampler2D _AOMap;

		half _Shininess;
		half _Multiplier;
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

		void surf (Input IN, inout IconSurfaceOutput o)
		{
			//as the clip test needs to be performed regardless of the surface properties, run it first as an early exit
			//should save some texture sampling and processing of data that would just be discarded anyway.
			float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
			screenUV.y = 1 - screenUV.y;
			
			#ifdef SHADER_API_GLCORE
			screenUV.y = 1 - screenUV.y;
			#endif
			
			if(screenUV.x < _MinX || screenUV.y < _MinY || screenUV.x > _MaxX || screenUV.y > _MaxY)
			{
				clip(-1);
				return;
			}
			float4 color = tex2D(_MainTex,(IN.uv_MainTex));
			float4 mask = tex2D(_MaskTex, (IN.uv_MainTex));
			float4 spec = tex2D(_SpecMap, (IN.uv_MainTex));
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
			float3 ao = tex2D(_AOMap, (IN.uv_MainTex));
			
			float m = saturate(1 - (mask.r + mask.g + mask.b));
			half3 userColor = mask.rrr * _MaskColor1.rgb + mask.ggg * _MaskColor2.rgb + mask.bbb * _MaskColor3.rgb;
			half3 diffuseColor = color * m;
			half3 detailColor = (color - 0.5) * (1 - m);

			half3 userSpec = mask.r * _MaskColor1.a + mask.g * _MaskColor2.a + mask.b * _MaskColor3.a;
			half3 baseSpec = spec * m;
			half3 detailSpec = (spec - 0.5) * (1 - m);

			o.Multiplier = _Multiplier;
			o.Albedo = saturate(userColor + diffuseColor + detailColor) * ao;
			o.GlossColor = spec.rgb;
			o.Specular = _Shininess;
			o.Normal = normal;
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}