// Shared by the game's hand-written URP lit shaders (Game/Voxel, Game/MudGround, Game/TankDirt):
// the lighting call and the helpers their shadow, depth and depth-normals passes need. Each shader
// keeps its own vertex and surface code and only routes the result through here, so they all light
// the way URP's Lit does: the main light with its shadows, Forward+ additional lights, SSAO, fog.
#ifndef GAME_LIT_INCLUDED
#define GAME_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// What a surface function hands to GameLitFragment. Everything in world space.
struct GameSurface
{
    float3 positionWS;
    half3 normalWS;
    half3 albedo;
    half metallic;
    half smoothness;
    half occlusion;
    half3 emission;
};

GameSurface GameSurfaceDefault(float3 positionWS, half3 normalWS)
{
    GameSurface s;
    s.positionWS = positionWS;
    s.normalWS = normalWS;
    s.albedo = 1;
    s.metallic = 0;
    s.smoothness = 0.5;
    s.occlusion = 1;
    s.emission = 0;
    return s;
}

// ---- Built-in Standard's BRDF (UnityStandardBRDF.cginc, BRDF1_Unity_PBS) ----
// The game was art-directed under Built-in. URP's own BRDF is close, but its Fresnel rises with
// (1 - n.v)^4 instead of ^5 and its diffuse is plain Lambert, so glossy voxels (wet mud, roofs) came
// out with a grey sheen and sunlit walls brighter. These shaders keep Built-in's BRDF and take
// everything else from URP: shadows, Forward+ lights, SSAO, reflection probes, fog.

half GamePow5(half x) { half x2 = x * x; return x2 * x2 * x; }

half GameDisneyDiffuse(half nv, half nl, half lh, half perceptualRoughness)
{
    half fd90 = 0.5 + 2 * lh * lh * perceptualRoughness;
    return (1 + (fd90 - 1) * GamePow5(1 - nl)) * (1 + (fd90 - 1) * GamePow5(1 - nv));
}

float GameSmithJointGGXVisibility(float nl, float nv, float roughness)
{
    float a = roughness;
    float lambdaV = nl * (nv * (1 - a) + a);
    float lambdaL = nv * (nl * (1 - a) + a);
    return 0.5f / (lambdaV + lambdaL + 1e-5f);
}

float GameGGX(float nh, float roughness)
{
    float a2 = roughness * roughness;
    float d = (nh * a2 - nh) * nh + 1.0f;
    return INV_PI * a2 / (d * d + 1e-7f);
}

struct GameBrdf
{
    half3 diffuse;
    half3 specular;
    half perceptualRoughness;
    half roughness;
    half grazing;
};

GameBrdf GameBrdfSetup(GameSurface s)
{
    GameBrdf b;
    half oneMinusReflectivity = kDielectricSpec.a - s.metallic * kDielectricSpec.a;
    b.diffuse = s.albedo * oneMinusReflectivity;
    b.specular = lerp(kDielectricSpec.rgb, s.albedo, s.metallic);
    b.perceptualRoughness = 1 - s.smoothness;
    b.roughness = max(b.perceptualRoughness * b.perceptualRoughness, 0.002);
    b.grazing = saturate(s.smoothness + (1 - oneMinusReflectivity));
    return b;
}

// One light's contribution; radiance is the light colour times its attenuation and shadow.
half3 GameBrdfDirect(GameBrdf b, half3 n, half3 v, half3 l, half3 radiance)
{
    float3 h = SafeNormalize(float3(l) + float3(v));
    half nv = abs(dot(n, v));
    half nl = saturate(dot(n, l));
    float nh = saturate(dot(n, h));
    half lh = saturate(dot(l, h));

    half diffuseTerm = GameDisneyDiffuse(nv, nl, lh, b.perceptualRoughness) * nl;
    float specularTerm = GameSmithJointGGXVisibility(nl, nv, b.roughness) * GameGGX(nh, b.roughness) * PI;
    specularTerm = max(0, specularTerm * nl);
    specularTerm *= any(b.specular) ? 1.0 : 0.0;

    half3 fresnel = b.specular + (1 - b.specular) * GamePow5(1 - lh);
    return b.diffuse * radiance * diffuseTerm + specularTerm * radiance * fresnel;
}

// Opaque surface lit the Built-in way; fogFactor comes from ComputeFogFactor in the vertex stage.
half4 GameLitFragment(GameSurface s, float4 positionCS, half fogFactor)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = s.positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(s.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(s.positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(s.positionWS);
    inputData.fogCoord = fogFactor;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    half3 n = inputData.normalWS;
    half3 v = inputData.viewDirectionWS;
    GameBrdf b = GameBrdfSetup(s);

    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, s.occlusion);

    // Ambient and reflections.
    half3 giDiffuse = SampleSH(n) * ao.indirectAmbientOcclusion;
    half3 reflectVector = reflect(-v, n);
    half3 giSpecular = GlossyEnvironmentReflection(reflectVector, s.positionWS, b.perceptualRoughness, ao.indirectAmbientOcclusion, inputData.normalizedScreenSpaceUV);
    half surfaceReduction = 1.0 / (b.roughness * b.roughness + 1.0);
    half nv = abs(dot(n, v));
    half3 color = b.diffuse * giDiffuse + surfaceReduction * giSpecular * lerp(b.specular, b.grazing, GamePow5(1 - nv));

    // Sun.
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    mainLight.color *= ao.directAmbientOcclusion;
    color += GameBrdfDirect(b, n, v, mainLight.direction, mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation));

    // Other lights (Forward+ cluster loop or the per-object list).
    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, ao);
        color += GameBrdfDirect(b, n, v, light.direction, light.color * (light.distanceAttenuation * light.shadowAttenuation));
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, ao);
        color += GameBrdfDirect(b, n, v, light.direction, light.color * (light.distanceAttenuation * light.shadowAttenuation));
    LIGHT_LOOP_END
    #endif

    color += s.emission;
    color = MixFog(color, fogFactor);
    return half4(color, 1);
}

// Shadow caster: set by URP before each shadow map is drawn.
float3 _LightDirection;
float3 _LightPosition;

float4 GameShadowCasterPositionCS(float3 positionWS, float3 normalWS)
{
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
    float3 lightDirectionWS = _LightDirection;
    #endif

    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    #if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
    return positionCS;
}

// Depth-normals pass output (SSAO reads it): the world normal, packed the way URP's Lit packs it.
half4 GameDepthNormalsOutput(half3 normalWS)
{
    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
}

#endif
