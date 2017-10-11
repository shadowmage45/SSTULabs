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
        
        inline half PerceptualRoughnessToSpecPower (half perceptualRoughness)
        {
            half m = perceptualRoughness * perceptualRoughness;
            half sq = max(1e-4f, m*m);
            half n = (2.0 / sq) - 2.0;
            n = max(n, 1e-4f);
            return n;
        }
        
        half DisneyDiffuse(half NdotV, half NdotL, half LdotH, half perceptualRoughness)
        {
            half fd90 = 0.5 + 2 * LdotH * LdotH * perceptualRoughness;
            half lightScatter   = (1 + (fd90 - 1) * Pow5(1 - NdotL));
            half viewScatter    = (1 + (fd90 - 1) * Pow5(1 - NdotV));

            return lightScatter * viewScatter;
        }
        
        // half4 BRDF1_Unity_PBS2 (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness, half3 normal, half3 viewDir, UnityLight light, UnityIndirect gi)
        // {
            // half perceptualRoughness = 1 - smoothness;
            // half3 halfDir = Unity_SafeNormalize (light.dir + viewDir);
            
            
            // half nv = abs(dot(normal, viewDir));
            // half nl = saturate(dot(normal, light.dir));
            // half nh = saturate(dot(normal, halfDir));

            // half lv = saturate(dot(light.dir, viewDir));
            // half lh = saturate(dot(light.dir, halfDir));

            // half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

            // half roughness = perceptualRoughness * perceptualRoughness;
            
            // half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
            // half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));

            // half specularTerm = V*D * UNITY_PI; 

            // specularTerm = max(0, specularTerm * nl);
            
            // half surfaceReduction = 1.0 / (roughness*roughness + 1.0);

            // specularTerm *= any(specColor) ? 1.0 : 0.0;

            // half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
            // half3 color =   diffColor * (gi.diffuse + light.color * diffuseTerm)
                            // + specularTerm * light.color * FresnelTerm (specColor, lh)
                            // + surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);
            // return half4(color, 1);
        // }
        
        // inline half4 LightingColoredSpecular2 (CSSO s, half3 viewDir, UnityGI gi)
        // {
            // s.Normal = normalize(s.Normal);

            // half oneMinusReflectivity;
            // oneMinusReflectivity = 1 - max (max (s.Specular.r, s.Specular.g), s.Specular.b);
            // s.Albedo = s.Albedo * (half3(1,1,1) - s.Specular);
            
            // s.Albedo *= s.Alpha;
            
            // half outputAlpha;
            // outputAlpha = 1 - oneMinusReflectivity + s.Alpha * oneMinusReflectivity;

            // half4 c = BRDF1_Unity_PBS2 (s.Albedo, s.Specular, oneMinusReflectivity, s.Smoothness, s.Normal, viewDir, gi.light, gi.indirect);
            // c.a = outputAlpha;
            // return c;
        // }
        
        //http://www.codinglabs.net/article_physically_based_rendering.aspx
        //http://www.codinglabs.net/article_physically_based_rendering_cook_torrance.aspx
        inline half4 LightingColoredSpecular (CSSO s, half3 lightDir, half3 viewDir, half atten)
        {
            s.Albedo = s.Albedo * (half3(1,1,1) - s.Specular);// * atten;
            s.Albedo *= s.Alpha;
            //s.Specular *= atten;
            
            half oneMinusReflectivity = 1 - max (max (s.Specular.r, s.Specular.g), s.Specular.b);
            half perceptualRoughness = 1 - s.Smoothness;
            half3 halfDir = Unity_SafeNormalize (lightDir + viewDir);            
            half diff = max (0, dot (normalize(s.Normal), lightDir));
            half nv = abs(dot(s.Normal, viewDir));
            half nl = saturate(dot(s.Normal, lightDir));
            half nh = saturate(dot(s.Normal, halfDir));

            half lv = saturate(dot(lightDir, viewDir));
            half lh = saturate(dot(lightDir, halfDir));

            half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

            half roughness = perceptualRoughness * perceptualRoughness;
            
            half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
            half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));

            half specularTerm = V*D * UNITY_PI; 

            specularTerm = max(0, specularTerm * nl);
            
            half surfaceReduction = 1.0 / (roughness*roughness + 1.0);

            specularTerm *= (s.Specular.r>0 || s.Specular.g>0 || s.Specular.b>0) ? 1.0 : 0.0;

            half grazingTerm = saturate(s.Smoothness + (1-oneMinusReflectivity));
            half3 color =   s.Albedo * (diff + _LightColor0.rgb * diffuseTerm)
                            + specularTerm * _LightColor0.rgb * FresnelTerm (s.Specular, lh)
                            + surfaceReduction * s.Specular * FresnelLerp (s.Specular, grazingTerm, nv);
            return half4(color*atten, s.Alpha);
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
			o.Specular = specColor;// + reflcol * (1-roughness);
			o.Smoothness = roughness;
			o.Normal = normal;
            o.Emission = reflcol * (roughness*roughness) * specColor;
			o.Alpha = 1;
		}
		ENDCG
	}
	Fallback "Bumped Specular"
}