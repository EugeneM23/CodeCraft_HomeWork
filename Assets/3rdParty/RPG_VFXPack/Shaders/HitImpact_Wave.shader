// Built-in pipeline stand-in for RPG_VFXPack's Wave.shadergraph, which targets URP and so cannot
// compile in this project. Same as HitImpact_Particles, with the one difference the graph has: a
// second texture, inline on the sample node rather than exposed, whose red channel modulates the
// colour but not the alpha. Its property name is the one Shader Graph generated for that inline
// slot, so the pack's Wave material binds without being rewritten.

Shader "RPG_VFX/HitImpact Wave"
{
	Properties
	{
		_Texture ("Texture", 2D) = "white" {}
		_SampleTexture2D_1f4d5a9a2fe34e508c0d8b188f7af2d4_Texture2D ("Modulation", 2D) = "white" {}
		_Emissive ("Emissive", Float) = 1
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off
		Lighting Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			sampler2D _Texture;
			sampler2D _SampleTexture2D_1f4d5a9a2fe34e508c0d8b188f7af2d4_Texture2D;
			float _Emissive;

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				// Raw UV0, as in the graph.
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float mask = tex2D(_Texture, i.uv).r;
				float modulation = tex2D(_SampleTexture2D_1f4d5a9a2fe34e508c0d8b188f7af2d4_Texture2D, i.uv).r;
				float3 rgb = _Emissive * i.color.rgb * (modulation * mask);
				float alpha = i.color.a * mask;
				return fixed4(rgb, alpha);
			}
			ENDCG
		}
	}
}
