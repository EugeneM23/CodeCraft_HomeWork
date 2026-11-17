Shader "Game/FogOfWar/Overlay"
{
    // Full-screen pass: every pixel's world position comes back from the depth texture and is
    // compared, in the ground plane, with how far the viewer's ray in that direction reached.
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.85)
        _Softness ("Edge Softness (m)", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+100" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _FogRayTex;      // R = reach of the ray, G = far side of what it hit
            half4 _Color;
            float _Softness;
            float4x4 _FogInvViewProj;  // OpenGL-style, uv.y = 0 at the bottom
            float4 _FogCameraPos;
            float4 _FogCameraForward;
            float4 _FogViewer;         // xz = viewer, y = floor height + tolerance
            float _FogRays;

            struct v2f { float4 pos : SV_POSITION; float4 screen : TEXCOORD0; };

            v2f vert(float4 positionOS : POSITION)
            {
                v2f o;
                // The quad's local xy in [-1, 1] goes straight to clip space: it always covers the screen.
                o.pos = float4(positionOS.xy, UNITY_NEAR_CLIP_VALUE, 1);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float3 WorldAt(float2 uv, float rawDepth)
            {
                float2 ndc = uv * 2 - 1;
                float4 nearW = mul(_FogInvViewProj, float4(ndc, -1, 1));
                float4 farW = mul(_FogInvViewProj, float4(ndc, 1, 1));
                float3 ray = farW.xyz / farW.w - nearW.xyz / nearW.w;
                float eye = LinearEyeDepth(rawDepth);
                return _FogCameraPos.xyz + ray * (eye / dot(ray, _FogCameraForward.xyz));
            }

            // Smooth along a continuous wall; across a jump (an obstacle's edge) take the nearer
            // ray so the edge does not smear into a wedge that swims as the viewer moves.
            float2 Blend(float2 a, float2 b, float t)
            {
                float2 jump = step(0.5, abs(a - b));
                return lerp(lerp(a, b, t), min(a, b), jump);
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                float3 world = WorldAt(uv, SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));

                float2 d = world.xz - _FogViewer.xz;
                float dist = length(d);
                float f = frac(atan2(d.y, d.x) / (2 * UNITY_PI)) * _FogRays;
                float i0 = floor(f);
                float2 r0 = tex2Dlod(_FogRayTex, float4((i0 + 0.5) / _FogRays, 0.5, 0, 0)).rg;
                float2 r1 = tex2Dlod(_FogRayTex, float4((i0 + 1.5) / _FogRays, 0.5, 0, 0)).rg;
                float2 ray = Blend(r0, r1, f - i0);

                float darkFloor = saturate((dist - ray.r) / _Softness + 0.5);
                // A raised pixel still within the obstacle the ray hit is that obstacle's top.
                float raised = step(_FogViewer.y, world.y);
                float darkTop = lerp(1, saturate((dist - ray.g) / _Softness + 0.5), raised);

                return half4(_Color.rgb, _Color.a * min(darkFloor, darkTop));
            }
            ENDCG
        }
    }
}
