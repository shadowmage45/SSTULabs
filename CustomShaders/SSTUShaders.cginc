struct IconSurfaceOutput 
{
	half3 Albedo;
	half3 Normal;
	half3 Emission;
	half Specular;
	half3 GlossColor;
	fixed Alpha;
	half Multiplier;
};

struct ColoredSpecularSurfaceOutput 
{
	half3 Albedo;
	half3 Normal;
	half3 Emission;
	half Specular;
	half3 GlossColor;
	fixed Alpha;
};
	
struct SolarSurfaceOutput 
{
	half3 Albedo;
	half3 Normal;
	half3 Emission;
	half Specular;
	half3 GlossColor;
	half Alpha;
	half3 BackLight;
	half BackClamp;
};	

inline half4 LightingIcon (IconSurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
{
    //fixed normal, as Unity de-normalizes it somewhere between the lighting functions
	fixed3 fN = normalize(s.Normal);
	//diffuse light intensity, from surface normal and light direction
	half diff = max (0, dot (fN, lightDir));
	//specular light calculations
	half3 h = normalize (lightDir + viewDir);
	float nh = max (0, dot (fN, h));
	float spec = pow (nh, s.Specular * 128);
	half3 specCol = spec * s.GlossColor;
	
	//output fragment color; Unity adds Emission to it through some other method
	half4 c;
	c.rgb = (s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * specCol) * atten * s.Multiplier;
	c.a = s.Alpha;
	return c;
}
		
inline half4 LightingColoredSpecular (ColoredSpecularSurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
{
    //fixed normal, as Unity de-normalizes it somewhere between the lighting functions
	fixed3 fN = normalize(s.Normal);
	//diffuse light intensity, from surface normal and light direction
	half diff = max (0, dot (fN, lightDir));
	//specular light calculations
	half3 h = normalize (lightDir + viewDir);
	float nh = max (0, dot (fN, h));
	float spec = pow (nh, s.Specular * 128);
	half3 specCol = spec * s.GlossColor;
	
	//output fragment color; Unity adds Emission to it through some other method
	half4 c;
	c.rgb = (s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * specCol) * atten;
	c.a = s.Alpha;
	return c;
}
 
inline half4 LightingColoredSpecular_PrePass (ColoredSpecularSurfaceOutput s, half4 light)
{
	half3 spec = light.a * s.GlossColor;
 
	half4 c;
	c.rgb = (s.Albedo * light.rgb + light.rgb * spec) * 0.5;
	c.a = s.Alpha + spec * _SpecColor.a;
	return c;
}

inline half4 LightingColoredSolar (SolarSurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
{
    //fixed normal, as Unity de-normalizes it somewhere between the lighting functions
	fixed3 fN = normalize(s.Normal);
	//diffuse light intensity, from surface normal and light direction
	half diff = max (0, dot (fN, lightDir));
	//specular light calculations
	half3 h = normalize (lightDir + viewDir);
	float nh = max (0, dot (fN, h));
	float spec = pow (nh, s.Specular * 128);
	half3 specCol = spec * s.GlossColor;
	
	//output fragment color; Unity adds Emission to it through some other method
	half4 c;
	
	//half clamp = min(0.001, s.BackClamp);
	half norm = s.BackClamp==0? 1 : 1 / (1 - s.BackClamp);
	//half backLight = max(0, (-dot(viewDir, lightDir) - s.BackClamp) * norm);
	half backLight = max(0, -dot(s.Normal, lightDir));
	//backLight *= (max(0, -dot(viewDir,  lightDir))+0.25)*0.8;
	backLight *= (max(0, -dot(s.Normal, -viewDir))+0.25)*0.8;
	half3 backColor = backLight * s.GlossColor * _LightColor0.rgb * s.BackLight;
	c.rgb = ((s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * specCol) + backColor) * atten;
	c.a = s.Alpha;
	return c;
}
 
inline half4 LightingColoredSolar_PrePass (SolarSurfaceOutput s, half4 light)
{
	half3 spec = light.a * s.GlossColor;
   
	half4 c;
	c.rgb = (s.Albedo * light.rgb + light.rgb * spec) * 0.5;
	c.a = s.Alpha + spec * _SpecColor.a;
	return c;
}
		
inline half3 stockEmit (float3 viewDir, float3 normal, half4 rimColor, half rimFalloff, half4 tempColor)
{
	half rim = 1.0 - saturate(dot (normalize(viewDir), normal));
	return rimColor.rgb * pow(rim, rimFalloff) * rimColor.a + tempColor.rgb * tempColor.a;
}

