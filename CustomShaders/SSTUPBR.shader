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
            // Two schlick fresnel term
            half lightScatter   = (1 + (fd90 - 1) * Pow5(1 - NdotL));
            half viewScatter    = (1 + (fd90 - 1) * Pow5(1 - NdotV));

            return lightScatter * viewScatter;
        }
        
        half4 BRDF1_Unity_PBS2 (half3 diffColor, half3 specColor, half oneMinusReflectivity, half smoothness, half3 normal, half3 viewDir, UnityLight light, UnityIndirect gi)
        {
            half perceptualRoughness = 1 - smoothness;
            half3 halfDir = Unity_SafeNormalize (light.dir + viewDir);
            
            
            half nv = abs(dot(normal, viewDir));
            half nl = saturate(dot(normal, light.dir));
            half nh = saturate(dot(normal, halfDir));

            half lv = saturate(dot(light.dir, viewDir));
            half lh = saturate(dot(light.dir, halfDir));

            // Diffuse term
            half diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;

            // Specular term
            // HACK: theoretically we should divide diffuseTerm by Pi and not multiply specularTerm!
            // BUT 1) that will make shader look significantly darker than Legacy ones
            // and 2) on engine side "Non-important" lights have to be divided by Pi too in cases when they are injected into ambient SH
            half roughness = perceptualRoughness * perceptualRoughness;
            
            half V = SmithBeckmannVisibilityTerm (nl, nv, roughness);
            half D = NDFBlinnPhongNormalizedTerm (nh, PerceptualRoughnessToSpecPower(perceptualRoughness));

            half specularTerm = V*D * UNITY_PI; // Torrance-Sparrow model, Fresnel is applied later

            specularTerm = max(0, specularTerm * nl);
            
            half surfaceReduction = 1.0 / (roughness*roughness + 1.0);           // fade \in [0.5;1]

            // To provide true Lambert lighting, we need to be able to kill specular completely.
            specularTerm *= any(specColor) ? 1.0 : 0.0;

            half grazingTerm = saturate(smoothness + (1-oneMinusReflectivity));
            half3 color =   diffColor * (gi.diffuse + light.color * diffuseTerm)
                            + specularTerm * light.color * FresnelTerm (specColor, lh)
                            + surfaceReduction * gi.specular * FresnelLerp (specColor, grazingTerm, nv);
            return half4(color, 1);
        }
        
        inline half4 LightingColoredSpecular (CSSO s, half3 viewDir, UnityGI gi)
        {
            s.Normal = normalize(s.Normal);

            // energy conservation
            half oneMinusReflectivity;
            oneMinusReflectivity = 1 - max (max (s.Specular.r, s.Specular.g), s.Specular.b);
            s.Albedo = s.Albedo * (half3(1,1,1) - s.Specular);
            
            //premult alpha
            s.Albedo *= s.Alpha;
            
            //final post reflection alpha
            half outputAlpha;
            outputAlpha = 1 - oneMinusReflectivity + s.Alpha * oneMinusReflectivity;

            //half4 c = UNITY_BRDF_PBS (s.Albedo, s.Specular, oneMinusReflectivity, s.Smoothness, s.Normal, viewDir, gi.light, gi.indirect);
            half4 c = BRDF1_Unity_PBS2 (s.Albedo, s.Specular, oneMinusReflectivity, s.Smoothness, s.Normal, viewDir, gi.light, gi.indirect);
            c.a = outputAlpha;
            return c;
        }
        
        struct GED
        {
            half roughness;
            half3 reflUVW;
        };
        
        inline void LightingColoredSpecular_GI (CSSO s, UnityGIInput data, inout UnityGI gi)
        {
            GED g;
            g.roughness = 1 - s.Smoothness;
            g.reflUVW = reflect(-data.worldViewDir, s.Normal);
            gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal, g);
        }
        
        // inline half4 LightingColoredSpecular2 (CSSO s, half3 lightDir, half3 viewDir, half atten)
        // {
            // fixed3 fN = normalize(s.Normal);
            // half diff = max (0, dot (fN, lightDir));
            // half3 h = normalize (lightDir + viewDir);
            // float nh = max (0, dot (fN, h));
            // float spec = pow (nh, s.Specular * 128);
            // half3 specCol = spec * s.GlossColor;
            
            // half4 c;
            // c.rgb = (s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * specCol) * atten;
            // c.a = s.Alpha;
            // return c;
        // }
		
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
			
            //float3 worldRefl = WorldReflectionVector(IN, o.Normal);
            //float3 reflcol = texCUBE(_Cube, worldRefl);
                        
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