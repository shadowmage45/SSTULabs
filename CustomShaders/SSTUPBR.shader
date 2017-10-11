Shader "SSTU/PBR"
{
	Properties 
	{
		_MainTex("_MainTex (RGB)", 2D) = "white" {}
		_SpecMap("_SpecMap (RGB)", 2D) = "black" {}
        _GlossMap("_GlossMap (RGB)", 2D) = "black" {}
		_BumpMap("_BumpMap (NRM)", 2D) = "bump" {}
		_AOMap("_AOMap (Grayscale)", 2D) = "white" {}
        _Cube("Reflection Cubemap", Cube) = "_Skybox" { }
	}
	
	SubShader
	{
		Tags {"RenderType"="Opaque"}
		ZWrite On
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha

		CGPROGRAM

		#pragma surface surf ColoredSpecular keepalpha
		#pragma target 3.0
        #include "UnityPBSLighting.cginc"
        #include "UnityShaderVariables.cginc"
        #include "UnityStandardConfig.cginc"
        #include "UnityLightingCommon.cginc"
        #include "UnityGBuffer.cginc"
        #include "UnityGlobalIllumination.cginc"
        #include "AutoLight.cginc"
        //#include "UnityImageBasedLighting.cginc"
        
        struct CSSO 
        {
            fixed3 Albedo;
            fixed3 Specular;
            fixed3 Normal;
            half3 Emission;
            half Smoothness;
            half Occlusion;
            fixed Alpha;
        };
        
        inline half4 LightingColoredSpecular (CSSO s, half3 viewDir, UnityGI gi)
        {
            s.Normal = normalize(s.Normal);

            // energy conservation
            half oneMinusReflectivity;
            s.Albedo = EnergyConservationBetweenDiffuseAndSpecular (s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);

            // shader relies on pre-multiply alpha-blend (_SrcBlend = One, _DstBlend = OneMinusSrcAlpha)
            // this is necessary to handle transparency in physically correct way - only diffuse component gets affected by alpha
            half outputAlpha;
            s.Albedo = PreMultiplyAlpha (s.Albedo, s.Alpha, oneMinusReflectivity, /*out*/ outputAlpha);

            half4 c = UNITY_BRDF_PBS (s.Albedo, s.Specular, oneMinusReflectivity, s.Smoothness, s.Normal, viewDir, gi.light, gi.indirect);
            c.a = outputAlpha;
            return c;
        }
        
        struct GED
        {
            half    roughness;
            half3   reflUVW;
        };

        GED GEDS(half Smoothness, half3 worldViewDir, half3 Normal, half3 fresnel0)
        {
            GED g;
            g.roughness = 1 - Smoothness;
            g.reflUVW = reflect(-worldViewDir, Normal);
            return g;
        }
        
        inline void LightingColoredSpecular_GI (CSSO s, UnityGIInput data, inout UnityGI gi)
        {
            GED g = GEDS(s.Smoothness, data.worldViewDir, s.Normal, s.Specular);
            gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal, g);
        
           // gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal);
        }
        
        half STPR(half smoothness)
        {
            return (1 - smoothness);
        }
		
		sampler2D _MainTex;
		sampler2D _SpecMap;
		sampler2D _GlossMap;
		sampler2D _BumpMap;		
		sampler2D _AOMap;
        samplerCUBE _Cube;
		
		struct Input
		{
			float2 uv_MainTex;
			float3 viewDir;
            float3 worldPos;
            float3 worldRefl;
            INTERNAL_DATA
		};

		void surf (Input IN, inout CSSO o)
		{
			float4 color = tex2D(_MainTex,(IN.uv_MainTex));
			float4 spec = tex2D(_SpecMap, (IN.uv_MainTex));
            float3 gloss = tex2D(_GlossMap, (IN.uv_MainTex));
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
			float3 ao = tex2D(_AOMap, (IN.uv_MainTex));
            
            float3 specColor = spec.rgb;
            float roughness = gloss.rgb;
			
            float3 worldRefl = WorldReflectionVector(IN, o.Normal);
            float3 reflcol = texCUBE(_Cube, worldRefl);
                        
			o.Albedo = color;
            o.Occlusion = ao;
			o.Specular = spec;
			o.Smoothness = roughness;
			o.Normal = normal;
            //o.Emission = reflcol * (roughness) * specColor;
			o.Alpha = 1;
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}