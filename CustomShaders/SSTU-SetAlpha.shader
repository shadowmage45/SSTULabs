Shader "SSTU/SetAlpha" {
Properties 
{ 
	_Alpha("Alpha", Float)=1.0
}
SubShader 
{
    Pass 
	{
        ZTest Always Cull Off ZWrite Off
        ColorMask A
        SetTexture [_Dummy] 
		{
            constantColor(0,0,0,[_Alpha]) combine constant 
		}
    }
}
}