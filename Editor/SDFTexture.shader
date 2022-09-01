Shader "Hidden/SDFTexture"
{
	Properties
	{
	}

    HLSLINCLUDE
    #pragma vertex vert
    #pragma fragment frag

    #include "UnityCG.cginc"

    struct appdata
    {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD;
    };

    struct v2f
    {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD;
    };

    float _Z;
    float _DistanceScale;
    sampler3D _SDF;

    int _Axis;
    #define AXIS_X 0
    #define AXIS_Y 1

    #define COLOR_POS 1
    #define COLOR_NEG float3(0.72, 0, 1)
    
    v2f vert(appdata v)
    {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.texcoord = v.texcoord;
        return o;
    }
    
    float4 frag(v2f i) : SV_Target
    {
        float3 uvw = float3(i.texcoord, _Z);

        if (_Axis == AXIS_X)
            uvw = float3(_Z, i.texcoord.yx);
        else if (_Axis == AXIS_Y)
            uvw = float3(i.texcoord.x, _Z, i.texcoord.y);
        
        float dist = tex3D(_SDF, uvw).r * _DistanceScale;
        float3 color = dist > 0.0 ? dist * COLOR_POS : -dist * COLOR_NEG;
        return float4(color, 1);
    }
    ENDHLSL

	SubShader
	{
        Cull Off
		Pass
		{
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
			HLSLPROGRAM
			ENDHLSL
		}

        Pass
		{
            Name "DepthForwardOnly"
            Tags{ "LightMode" = "DepthForwardOnly" }
			HLSLPROGRAM
			ENDHLSL
		}
	}
}
