// Built-in pipeline stand-in for RPG_VFXPack's Particles.shadergraph, which targets URP and so
// cannot compile in this project. The graph is reproduced exactly: the texture's red channel is
// the mask, the particle's vertex colour tints it, _Emissive scales the result, and the whole
// thing is alpha blended, unlit and double sided. Property names match the graph's so the pack's
// materials bind without being rewritten.

Shader "RPG_VFX/HitImpact Particles"
{
	Properties
	{
		_Texture ("Texture", 2D) = "white" {}
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
			float _Emissive;

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				// The graph samples with raw UV0 -- no tiling or offset node -- so the
				// material's scale and offset are deliberately not applied here.
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float mask = tex2D(_Texture, i.uv).r;
				float3 rgb = _Emissive * i.color.rgb * mask;
				float alpha = i.color.a * mask;
				return fixed4(rgb, alpha);
			}
			ENDCG
		}
	}
}
