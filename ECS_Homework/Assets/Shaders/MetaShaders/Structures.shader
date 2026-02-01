Shader "ReduxTest/structures"
{
	Properties
	{
		_ColorTexture("Color Texture", 2D) = "white" {}
		_ColorTint("Color Tint", Color) = (0,0,0,0)
		_GlowIntensity("Glow Intensity", Range( 0 , 20)) = 0
		_Shadowcolor("Shadow color", Color) = (0,0,0,0)
		_ShadowOpacity("Shadow Opacity", Range( 0 , 1)) = 1
		_RimlightColor("Rimlight Color", Color) = (1,1,1,1)
		_RimlightColor2("Rimlight Color 2", Color) = (1,1,1,0.2509804)
		_RimlightDirection("RimlightDirection", Vector) = (1,0,0,0)
		_RimlightSoftness("Rimlight Softness", Range( 0.5 , 2)) = 1.108258
		_RimlightThickness("Rimlight Thickness", Range( 0 , 1)) = 0		
		_FogColor("FogColor", Color) = (0,0.8364415,1,0.01176471)
		_FogSoftness("Fog Softness", Range( 0 , 3000)) = 0
		_FogDistance("Fog Distance", Range( 0 , 2000)) = 0
		_ForegroundFogColor("Foreground Fog Color", Color) = (0,0,0,0)
		_ForegroundFogSoftness("Foreground Fog Softness", Range( 0 , 100)) = 10
		_ForegroundFogDistance("Foreground Fog Distance", Range( 0 , 200)) = 7
		_AOcolor("AO color", Color) = (0.3686275,0.01176471,0.2078432,1)
		_AOspread("AO spread", Range( 0.2 , 2)) = 0.2
		_AOpower("AO power", Range( 0 , 1)) = 0
		[Toggle(_VERTEXANIMATION_ON)] _VertexAnimation("Vertex Animation", Float) = 0
		_Speed("Speed", Range( 0 , 3)) = 0
		_Frequency("Frequency", Range( 0 , 5)) = 0
		_SwayAmount("SwayAmount", Range( 0 , 1)) = 0
		_HorizontalVertical("Horizontal / Vertical", Range( 0 , 1)) = 0
	}
	
	SubShader
	{
		Tags { "RenderType"="Opaque" "IgnoreProjector" = "True" }
		
		Pass
		{			
			Name "ForwardBase"
			Tags { "LightMode"="ForwardBase" }

			Blend Off
			Cull Back
			ColorMask RGBA
			ZWrite On
			ZTest LEqual
			
			CGPROGRAM			
			#pragma target 3.0
			#pragma vertex vert
			#pragma fragment frag
			
			#define DIRECTIONAL
			#pragma multi_compile_instancing;										
            #pragma multi_compile __ SHADOWS_SCREEN
						
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"

			#pragma shader_feature _VERTEXANIMATION_ON
						
            // Available quality macros: _SP_SHADER_QUALITY_LOW _SP_SHADER_QUALITY_MEDIUM _SP_SHADER_QUALITY_HIGH
			#pragma multi_compile __ _SP_SHADER_QUALITY_LOW
						
			#define _AO_ON
			#define _LAMBERT_ON
			
			#ifndef _SP_SHADER_QUALITY_LOW
                #define _LIGHT_ATTEN_ON
                #define _RIMLIGHT_ON
                #define _FOG_ON
                #define _VERTEX_ANIM_ON
                #define _GLOW_ON			
            #endif

            uniform sampler2D _ColorTexture;
			uniform fixed4 _ColorTexture_ST;
            uniform fixed4 _ColorTint;

            #ifdef _VERTEX_ANIM_ON
                uniform half _Frequency;
                uniform half _Speed;
                uniform half _SwayAmount;
                uniform half _HorizontalVertical;
			#endif
			
			#ifdef _GLOW_ON
			    uniform fixed _GlowIntensity;
            #endif
			
			#ifdef _AO_ON
			    uniform fixed4 _AOcolor;
			    uniform fixed _AOspread;
			    uniform fixed _AOpower;
			#endif
			
			#if defined(_LIGHT_ATTEN_ON) || defined(_LAMBERT_ON)
			    uniform fixed4 _Shadowcolor;
			    uniform fixed _ShadowOpacity;
			#endif
			
			#ifdef _RIMLIGHT_ON
                uniform fixed _RimlightSoftness;
                uniform fixed _RimlightThickness;
                uniform fixed4 _RimlightColor;
                uniform fixed3 _RimlightDirection;
                uniform fixed4 _RimlightColor2;
			#endif
			
			#ifdef _FOG_ON
                uniform fixed4 _FogColor;
                uniform fixed _FogSoftness;
                uniform fixed _FogDistance;
                uniform fixed4 _ForegroundFogColor;
                uniform fixed _ForegroundFogSoftness;
                uniform fixed _ForegroundFogDistance;
			#endif


			struct appdata
			{
				half4 vertex : POSITION;
				fixed3 normal : NORMAL;
				fixed2 uv1 : TEXCOORD1;
				fixed2 uv0 : TEXCOORD0;
				fixed4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				half4 pos : SV_POSITION;
				fixed4 color : COLOR; // RGB - ambient occlusion, A - lambert value
				fixed2 uv : TEXCOORD0; // XY - texture coords 								                
				    
                #ifdef _RIMLIGHT_ON
                    fixed3 rimColor : TEXCOORD1;
                #endif
                
                #ifdef _LIGHT_ATTEN_ON
                    fixed3 uvWorldPos : TEXCOORD2; // worldPos is required for light attenuation on ps
                #endif
                
                #ifdef _FOG_ON
                    fixed3 uvFogValues : TEXCOORD3; // Fog parameters packed into xyz values
                #endif			
                
                #ifdef _LIGHT_ATTEN_ON
                    #ifdef SHADOWS_SCREEN
		                SHADOW_COORDS(4)
	                #endif
                #endif
			};			
			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_SETUP_INSTANCE_ID(v);
                
                #ifdef _VERTEX_ANIM_ON
				    #ifdef _VERTEXANIMATION_ON
                        half timeSpeed = ( _Time.z * _Speed );
                        half4 swayCalc = (fixed4(( sin( ( ( ( v.vertex.xyz.x + v.vertex.xyz.z ) * _Frequency ) + timeSpeed ) ) * _SwayAmount * sin( ( -0.6 * v.uv1.x ) ) ) , 0.0 , 0.0 , 0.0));
                        v.vertex.xyz += lerp( swayCalc , fixed4( ( fixed3(0,1,0) * sin( timeSpeed ) * _SwayAmount ) , 0.0 ) , _HorizontalVertical);
				    #endif
                #endif
								
                o.color = fixed4( 1.0, 1.0, 1.0, 1.0 );
            
                // Common values for both lambert and rim light
                #if defined(_LAMBERT_ON) || defined(_RIMLIGHT_ON) || defined(_LIGHT_ATTEN_ON)
                    fixed3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                    fixed3 worldNormal = UnityObjectToWorldNormal(v.normal);
                    
                    #ifdef _LIGHT_ATTEN_ON
                        o.uvWorldPos = worldPos;
                    #endif
                #endif
            
                // Conditional: Ambient Occlusion
                #ifdef _AO_ON
                    fixed aoValue = clamp( lerp( 1.0 , pow( v.color.g , _AOspread ) , _AOpower), 0.0, 1.0 );
                    fixed3 aoColorMultiplier = lerp( _AOcolor.rgb , fixed3(1.0,1.0,1.0) , aoValue);
                    o.color.rgb = aoColorMultiplier;
                    
                #endif

                // Conditional: Precompute lambert value in vertex                    
                #ifdef _LAMBERT_ON
                    fixed lambertAmount = max( 0.0, dot( worldNormal , _WorldSpaceLightPos0.xyz ) );
                                                  
                    // If light attenuation is enabled, we need to do it per pixel, so we only store lambert amount                  
                    #ifdef _LIGHT_ATTEN_ON
                        o.color.a = lambertAmount;
                        
                    // Only lambert is enabled, we can precalculate the final value
                    #else
                        fixed steppedLambertAttenuation = smoothstep( 0.4 , 0.6 , lambertAmount );
                        o.color.a = lerp( 1.0 , steppedLambertAttenuation , _ShadowOpacity);
                        
                    #endif
                    
                #endif 
                
                // Conditional: Rim Light
                #ifdef _RIMLIGHT_ON                  
                    fixed3 worldViewDir = normalize( UnityWorldSpaceViewDir(worldPos) );
                    fixed fresnel = ( pow( 1.0 - dot( worldNormal, worldViewDir ), 1.0 ) );
                    fixed rim = smoothstep( ( 1.0 - _RimlightSoftness ) , _RimlightSoftness , ( fresnel + ( _RimlightThickness - 0.5 ) ));
                    
                    fixed3 objectToViewDir = normalize( mul( UNITY_MATRIX_IT_MV, fixed4( v.normal, 0 ) ).xyz );
                    fixed3 rimlightDir = normalize( _RimlightDirection );
                    
                    fixed viewDotRim = dot( objectToViewDir, rimlightDir );
                    fixed smoothViewDotRim = smoothstep( 0.0 , 1.0 , clamp( viewDotRim , 0.0 , 1.0 ));
                    fixed smoothInverseViewDotRim = smoothstep( 0.0 , 1.0 , clamp( ( viewDotRim * -1.0 ) , 0.0 , 1.0 ));
                    o.rimColor = clamp( ( rim * ( ( (_RimlightColor).rgb * smoothViewDotRim * ( _RimlightColor.a * 2.0 ) ) + ( smoothInverseViewDotRim * (_RimlightColor2).rgb * ( _RimlightColor2.a * 2.0 ) ) ) ) , fixed3( 0,0,0 ) , fixed3( 1.5,1.5,1.5 ) );                         
                #endif
                
                // Conditional: Fog
                #ifdef _FOG_ON
                    // Eye depth used in branch and again in final calculation
                    fixed eyeDepth = -UnityObjectToViewPos(v.vertex.xyz).z;
                    
                    // Static Fog -> fog is based on distance from world (0,0,0) 
                    fixed3 objecToWorld = mul( unity_ObjectToWorld, fixed4( v.vertex.xyz, 0 ) ).xyz;
                    fixed4 objectToWorldZero = mul(unity_ObjectToWorld,fixed4( 0,0,0,1 ));
                    o.uvFogValues.x = clamp( ( ( length( ( (objectToWorldZero).xyzw + fixed4( objecToWorld , 0.0 ) ) ) - ( _FogDistance / 10.0 ) ) / ( _FogSoftness / 10.0 ) ) , 0.0 , 1.0 );
                    
                    o.uvFogValues.y = 1.0 - clamp((( eyeDepth -_ProjectionParams.y - _ForegroundFogDistance ) / _ForegroundFogSoftness), 0.0, 1.0 );
                    o.uvFogValues.z = clamp( ( ( distance( _WorldSpaceCameraPos , fixed3(0,0,0) ) - 3.0 ) / 3.0 ) , 0.0 , 10.0 );                                        
                #endif   
                                        
                // Transfer shadow coords
                #ifdef _LIGHT_ATTEN_ON
                    TRANSFER_SHADOW(o);
                #endif
                								
				o.uv.xy = v.uv0 * _ColorTexture_ST.xy + _ColorTexture_ST.zw;
				o.pos = UnityObjectToClipPos(v.vertex);											
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{													
                fixed4 sampleColor = tex2D( _ColorTexture, i.uv );
                fixed4 tintedColor = ( _ColorTint * sampleColor ); 	
                
                fixed4 outColor = tintedColor * i.color;
             
                // Clamp color
                outColor = clamp( outColor , fixed4( 0.05,0.05,0.05,0 ) , fixed4( 0.95,0.95,0.95,0 ) );
             
                // Conditional: Lighting (Attenuation, Shadows, Lambert)
                // If doing vertex shading only lambert is precomputed, light attenuation is still per pixel				
                // Full lighting
                #if defined(_LIGHT_ATTEN_ON) && defined(_LAMBERT_ON)				
                    fixed3 worldPos = i.uvWorldPos;
                    
                    UNITY_LIGHT_ATTENUATION(lightAttenuation, i, worldPos);
                                            
                    fixed steppedLambertAttenuation = smoothstep( 0.4 , 0.6 , min( lightAttenuation , i.color.a ));
                    fixed shadowAmount = lerp( 1.0 , steppedLambertAttenuation , _ShadowOpacity);
                    outColor = lerp( ( outColor * _Shadowcolor ) , outColor , shadowAmount);
                
                // Unity light attenuation + shadows only
                #elif defined (_LIGHT_ATTEN_ON)
                    fixed3 worldPos = i.uvWorldPos; 			
                    
                    UNITY_LIGHT_ATTENUATION(lightAttenuation, i, worldPos);
                    
                    fixed steppedLambertAttenuation = smoothstep( 0.4 , 0.6 , lightAttenuation );
                    fixed shadowAmount = lerp( 1.0 , steppedLambertAttenuation , _ShadowOpacity);
                    outColor = lerp( ( outColor * _Shadowcolor ) , outColor , shadowAmount);
                    
                // Lambert only
                #elif defined (_LAMBERT_ON) 
                    outColor = lerp( ( outColor * _Shadowcolor ) , outColor , i.color.a );
                    
                #endif                  
             
                // Add sky ambient
                outColor = outColor + ( tintedColor * ( unity_AmbientSky / 1.333 ) );
                
                // Apply rim 
                #ifdef _RIMLIGHT_ON
                    outColor += fixed4( i.rimColor, 0.0 );
                #endif
                
                // Apply fog
                #ifdef _FOG_ON
                    fixed4 appliedFogColor = lerp( outColor, _FogColor , i.uvFogValues.x);
                    fixed4 foregroundToAppliedFogColor = lerp( appliedFogColor , _ForegroundFogColor , i.uvFogValues.y );
                    outColor = lerp( appliedFogColor , foregroundToAppliedFogColor , min( _ForegroundFogColor.a , i.uvFogValues.z ));
            
                #endif       
                                  
                 // Conditional: Glow - both in vertex shading and pixel shading
                 #ifdef _GLOW_ON
                    fixed3 glowColor = ( ( sampleColor.a * _GlowIntensity ) * tintedColor.xyz );
                    outColor += fixed4( glowColor, 0.0 );
                                                                    
                 #endif
                 
				return outColor;
			}
			ENDCG
		}
		
		Pass
		{			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }
			ZWrite On
			ZTest LEqual
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"

			#pragma shader_feature _VERTEXANIMATION_ON
			#pragma multi_compile __ _SP_SHADER_QUALITY_LOW
			#pragma multi_compile_instancing;
			
			#ifndef _SP_SHADER_QUALITY_LOW
			    #define _VERTEX_ANIM_ON
			#endif
			
			#ifdef _VERTEX_ANIM_ON
                uniform half _Frequency;
                uniform half _Speed;
                uniform half _SwayAmount;
                uniform half _HorizontalVertical;
			#endif

			struct appdata
			{
				half4 vertex : POSITION;
				fixed3 normal : NORMAL;
                fixed2 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				V2F_SHADOW_CASTER;
			};

			
			v2f vert ( appdata v )
			{
				v2f o;
				
				UNITY_SETUP_INSTANCE_ID(v);
				
                #ifdef _VERTEX_ANIM_ON
				    #ifdef _VERTEXANIMATION_ON
                        half timeSpeed = ( _Time.z * _Speed );
                        half4 swayCalc = (fixed4(( sin( ( ( ( v.vertex.xyz.x + v.vertex.xyz.z ) * _Frequency ) + timeSpeed ) ) * _SwayAmount * sin( ( -0.6 * v.uv1.x ) ) ) , 0.0 , 0.0 , 0.0));
                        v.vertex.xyz += lerp( swayCalc , fixed4( ( fixed3(0,1,0) * sin( timeSpeed ) * _SwayAmount ) , 0.0 ) , _HorizontalVertical);
				    #endif
                #endif
				
				TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				SHADOW_CASTER_FRAGMENT(i)
			}
			ENDCG
		}
		
	}
}