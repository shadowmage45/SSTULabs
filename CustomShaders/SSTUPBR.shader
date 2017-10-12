Shader "SSTU/PBR"
{
	Properties 
	{
		_MainTex("_MainTex (RGB)", 2D) = "white" {}
		_SpecMap("_SpecMap (RGB)", 2D) = "black" {}
        _GlossMap("_GlossMap (RGB)", 2D) = "black" {}
		_BumpMap("_BumpMap (NRM)", 2D) = "bump" {}
		_AOMap("_AOMap (Grayscale)", 2D) = "white" {}
        _Cube("Reflection Cubemap", Cube) = "_Skybox" {}
        _MainColor("_Albedo", Color) = (0,0,0,1)
        _GlossColor("_Specular", Color) = (0,0,0,1)
        _Roughness ("_Roughness", Range (0, 1)) = 0.5
        _Metallic ("_Metallic", Range (0, 1)) = 0.5
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
        // #include "UnityPBSLighting.cginc"
        // #include "UnityShaderVariables.cginc"
        // #include "UnityStandardConfig.cginc"
        // #include "UnityLightingCommon.cginc"
        
        struct CSSO 
        {
            fixed3 Albedo;
            fixed3 Specular;
            fixed3 Normal;
            half3 Emission;
            half Smoothness;
            half Occlusion;
            half Metal;
            fixed3 Reflect;
            fixed Alpha;
        };
        
        //-----------------------------------------------------------------------------------------------------------
        //  SPECULAR SHADING FUNCTIONS
        
        float GGX(float roughness, float NdotH)
        {
            float rSqr = roughness * roughness;
            float ndhs = NdotH * NdotH;
            float TanNdotHSqr = (1-ndhs)/ndhs;
            return (1.0/3.1415926535) * sqrt(roughness/(ndhs * (rSqr + TanNdotHSqr)));
        }
        
        float3 specular(CSSO s, half nh, out float3 specColor)
        {
            float roughness = 1 - (s.Smoothness * s.Smoothness);
            roughness = roughness * roughness;
            
            float3 diffuseColor = s.Albedo * (1 - s.Metal);
            specColor = lerp(s.Specular.rgb, s.Albedo.rgb, s.Metal * 0.5);
            
            float3 SpecularDistribution = specColor;
            SpecularDistribution *= GGX(roughness, nh);
            return float3(SpecularDistribution.rgb);
        }
        
        //-----------------------------------------------------------------------------------------------------------
        //  GEOMETRY SHADING FUNCTIONS
        
        //lambert geometry shading
        float geometry (float NdotL, float NdotV)
        {
            return (NdotL*NdotV);
        }
        
        //cook-torrence geometry shading
        float geometry2 (float NdotL, float NdotV, float VdotH, float NdotH)
        {
            return min(1.0, min(2*NdotH*NdotV / VdotH, 2*NdotH*NdotL / VdotH));
        }
        
        //schlick geometry shading
        float geometry3 (float NdotL, float NdotV, float roughness)
        {
            float roughnessSqr = roughness*roughness;
            float SmithL = (NdotL)/(NdotL * (1-roughnessSqr) + roughnessSqr);
            float SmithV = (NdotV)/(NdotV * (1-roughnessSqr) + roughnessSqr);
            return (SmithL * SmithV); 
        }
        
        //GGXGeometricShadowingFunction
        float geometry4 (float NdotL, float NdotV, float roughness)
        {
            float roughnessSqr = roughness*roughness;
            float NdotLSqr = NdotL*NdotL;
            float NdotVSqr = NdotV*NdotV;
            float SmithL = (2 * NdotL)/ (NdotL + sqrt(roughnessSqr + ( 1-roughnessSqr) * NdotLSqr));
            float SmithV = (2 * NdotV)/ (NdotV + sqrt(roughnessSqr + ( 1-roughnessSqr) * NdotVSqr));
            return SmithL * SmithV;
        }
        
        float3 geometryShading(float roughness, float NdotL, float NdotV, float VdotH, float NdotH)
        {
            float3 color;
            //color.rgb = geometry(NdotL, NdotV);
            //color.rgb = geometry2(NdotL, NdotV, VdotH, NdotH);
            //color.rgb = geometry3(NdotL, NdotV, roughness);
            color.rgb = geometry4(NdotL, NdotV, roughness);
            return color;
        }
        
        //-----------------------------------------------------------------------------------------------------------
        // FRESNEL SHADING FUNCTIONS

        float MixFunction(float i, float j, float x) 
        {
             return  j * x + i * (1.0 - x);
        }

        float SchlickFresnel(float i)
        {
            float x = clamp(1.0-i, 0.0, 1.0);
            float x2 = x*x;
            return x2*x2*x;
        }

        //normal incidence reflection calculation
        float F0 (float NdotL, float NdotV, float LdotH, float roughness)
        {
            float FresnelLight = SchlickFresnel(NdotL); 
            float FresnelView = SchlickFresnel(NdotV);
            float FresnelDiffuse90 = 0.5 + 2.0 * LdotH*LdotH * roughness;
            return  MixFunction(1, FresnelDiffuse90, FresnelLight) * MixFunction(1, FresnelDiffuse90, FresnelView);
        }
        
        float3 SchlickFresnelFunction(float3 SpecularColor,float LdotH)
        {
            return SpecularColor + (1 - SpecularColor) * SchlickFresnel(LdotH);
        }
        
        //-----------------------------------------------------------------------------------------------------------
        // LIGHTING MODEL
        
        //http://www.codinglabs.net/article_physically_based_rendering.aspx
        //http://www.codinglabs.net/article_physically_based_rendering_cook_torrance.aspx
        //http://www.jordanstevenstechart.com/physically-based-rendering
        inline half4 LightingColoredSpecular (CSSO s, half3 lightDir, half3 viewDir, half atten)
        {
            s.Normal = normalize(s.Normal);
            
            float3 h = Unity_SafeNormalize (lightDir + viewDir);
            half nh = saturate(dot(s.Normal, h));
            half nv = saturate(dot(s.Normal, viewDir));
            half nl = saturate(dot(s.Normal, lightDir));
            half lh = saturate(dot(lightDir, h));
            half vh = saturate(dot(viewDir, h));
            
            float3 attenLight = _LightColor0.rgb * atten;
            float roughness = 1-s.Smoothness;
            float3 indirectDiffuse = float3(1,1,1);
            float3 indirectSpecular = float3(1,1,1);
            
            float3 specColor;
            float3 spec = specular(s, nh, specColor);            
            float3 geom = geometryShading(1 - s.Smoothness, nl, nv, vh, nh);
            float f0 = F0(nl, nv, lh, roughness);
            float3 fresnel = SchlickFresnelFunction(specColor, lh);
            
            float3 specularity = (spec * geom * fresnel) / (4* (nl * nv));
            float grazingTerm = saturate(roughness + s.Metal);
            float3 unityIndirectSpecularity =  indirectSpecular * FresnelLerp(specColor,grazingTerm,nv) * max(0.15,s.Metal) * (1-roughness*roughness* roughness);
            
            float3 diffuseColor = s.Albedo * (1 - s.Metal) * f0;
            
            float3 lightingModel = ((diffuseColor) + specularity + (unityIndirectSpecularity * 1));
            lightingModel *= nl;
            
            float4 finalDiffuse = float4(lightingModel * attenLight, 1);
            return finalDiffuse;
        }
		
		sampler2D _MainTex;
		sampler2D _SpecMap;
		sampler2D _GlossMap;
		sampler2D _BumpMap;		
		sampler2D _AOMap;
        samplerCUBE _Cube;
        
        half _Roughness;
        half _Metallic;
		float4 _MainColor;
        float4 _GlossColor;
		
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
            color = _MainColor;
			float4 spec = tex2D(_SpecMap, (IN.uv_MainTex));
            spec = _GlossColor;
            float3 gloss = tex2D(_GlossMap, (IN.uv_MainTex));
            gloss = 1-_Roughness;
			float3 normal = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
			float3 ao = tex2D(_AOMap, (IN.uv_MainTex));
            			
            float3 worldRefl = WorldReflectionVector(IN, o.Normal);
            float3 reflcol = texCUBE(_Cube, worldRefl);
                        
			o.Albedo = color;
            o.Occlusion = ao;
			o.Specular = spec;
			o.Smoothness = gloss;
			o.Normal = normal;
            o.Emission = float4(0,0,0,0);
            o.Metal = _Metallic;
            o.Reflect = reflcol;
			o.Alpha = 1;
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}