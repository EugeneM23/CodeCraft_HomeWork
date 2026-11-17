// Rain drops and their splashes: unlit, see-through, one colour per instance (_InstColor,
// alpha included) times _Color. A touch of the scene's ambient light keeps them from glowing
// in the dark.
Shader "Game/VoxelRain"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _InstColor ("Instance Colour", Color) = (1, 1, 1, 1)
        _Ambient ("Ambient Share", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            fixed4 _Color;
            half _Ambient;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 c = UNITY_ACCESS_INSTANCED_PROP(Props, _InstColor) * _Color;
                float3 light = lerp(1.0, saturate(ShadeSH9(float4(0, 1, 0, 1)) * 1.6), _Ambient);
                c.rgb *= light;
                UNITY_APPLY_FOG(unity_FogColor, c);
                return c;
            }
            ENDCG
        }
    }
}
