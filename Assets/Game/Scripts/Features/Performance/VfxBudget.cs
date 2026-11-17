using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Trims a pooled instance (impacts, explosions, bullets, casings) to the active performance
    /// tier, once, when its pool creates it.
    /// </summary>
    /// <remarks>
    /// Desktop keeps every effect exactly as authored. On mobile, particle renderers stop casting
    /// shadows: Particles/Standard Unlit has a shadow caster pass, so glows, sparks and blood droplets
    /// were drawn into the shadow map as dithered blobs, and the mobile particle shaders, which have no
    /// caster pass, still went through shadow-caster culling.
    ///
    /// Mobile also halves every particle system's capacity and burst sizes; drops particle collision
    /// to Low quality against static colliders only (High raycasts every particle every frame and
    /// ignores the raycast budget); switches off lights that are not marked Important, which on a
    /// level with one per-pixel light would only add per-vertex and SH work; stops meshes smaller
    /// than 0.3 m (bullets, casings, rocket tips) from casting shadows nobody can make out from this
    /// camera; and applies the mobile HighlightPlus settings.
    ///
    /// Halving cannot be repeated safely, so each instance is changed once: the pool calls this for
    /// every instance it creates and never on rent, and a second call for an instance does nothing.
    /// </remarks>
    public static class VfxBudget
    {
        private const float SMALL_SHADOW_CASTER = 0.3f;
        private const float THIN_SHADOW_CASTER = 0.08f;

        private static readonly HashSet<int> MobileApplied = new();
        private static readonly List<ParticleSystemRenderer> ParticleRenderers = new();
        private static readonly List<ParticleSystem> Systems = new();
        private static readonly List<Light> Lights = new();
        private static readonly List<MeshRenderer> MeshRenderers = new();

        public static void Apply(GameObject root)
        {
            if (root == null)
                return;

            // Desktop keeps every pooled effect exactly as authored.
            if (PerformanceTier.IsMobile && MobileApplied.Add(root.GetInstanceID()))
            {
                StopParticleShadows(root);
                HalveParticleBudget(root);
                SwitchOffMinorLights(root);
                StopSmallShadowCasters(root);
                MobileHighlights.ApplyToHierarchy(root);
            }
        }

        internal static void Reset() => MobileApplied.Clear();

        private static void StopParticleShadows(GameObject root)
        {
            ParticleRenderers.Clear();
            root.GetComponentsInChildren(true, ParticleRenderers);

            for (int i = 0; i < ParticleRenderers.Count; i++)
                ParticleRenderers[i].shadowCastingMode = ShadowCastingMode.Off;

            ParticleRenderers.Clear();
        }

        private static void HalveParticleBudget(GameObject root)
        {
            Systems.Clear();
            root.GetComponentsInChildren(true, Systems);

            for (int i = 0; i < Systems.Count; i++)
            {
                ParticleSystem system = Systems[i];

                ParticleSystem.MainModule main = system.main;
                main.maxParticles = Mathf.Max(1, main.maxParticles / 2);

                ParticleSystem.EmissionModule emission = system.emission;
                for (int b = 0, count = emission.burstCount; b < count; b++)
                {
                    ParticleSystem.Burst burst = emission.GetBurst(b);
                    burst.count = HalveCount(burst.count);
                    emission.SetBurst(b, burst);
                }

                ParticleSystem.CollisionModule collision = system.collision;
                if (collision.enabled)
                {
                    collision.quality = ParticleSystemCollisionQuality.Low;
                    collision.enableDynamicColliders = false;
                }
            }

            Systems.Clear();
        }

        // A burst size is a constant, a random range or a curve over the system's duration.
        private static ParticleSystem.MinMaxCurve HalveCount(ParticleSystem.MinMaxCurve count)
        {
            switch (count.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    count.constant = HalveCountValue(count.constant);
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    count.constantMin = HalveCountValue(count.constantMin);
                    count.constantMax = HalveCountValue(count.constantMax);
                    break;
                default:
                {
                    // The curve is scaled by its multiplier: halve that, but keep at least one
                    // particle at the curve's peak so the burst never rounds away.
                    float peak = count.mode == ParticleSystemCurveMode.Curve
                        ? ParticleCurves.Peak(count.curve)
                        : Mathf.Max(ParticleCurves.Peak(count.curveMin), ParticleCurves.Peak(count.curveMax));
                    float multiplier = count.curveMultiplier;

                    if (peak > 0f && multiplier * peak > 1f)
                        count.curveMultiplier = Mathf.Max(multiplier * 0.5f, 1f / peak);
                    break;
                }
            }

            return count;
        }

        // Rounds up, so a burst of one stays one and no burst that emitted anything stops emitting.
        private static float HalveCountValue(float value) => value < 1f ? value : Mathf.Ceil(value * 0.5f);

        private static void SwitchOffMinorLights(GameObject root)
        {
            Lights.Clear();
            root.GetComponentsInChildren(true, Lights);

            for (int i = 0; i < Lights.Count; i++)
            {
                Light light = Lights[i];
                if (light.renderMode == LightRenderMode.ForcePixel)
                    continue;

                light.enabled = false;

                // ExplosionFlash switches its light back on every time the pooled effect is rented.
                if (light.TryGetComponent(out ExplosionFlash flash))
                    flash.enabled = false;
            }

            Lights.Clear();
        }

        private static void StopSmallShadowCasters(GameObject root)
        {
            MeshRenderers.Clear();
            root.GetComponentsInChildren(true, MeshRenderers);

            for (int i = 0; i < MeshRenderers.Count; i++)
            {
                MeshRenderer renderer = MeshRenderers[i];
                ShadowCastingMode mode = renderer.shadowCastingMode;

                // A ShadowsOnly renderer exists only to cast its shadow.
                if (mode == ShadowCastingMode.Off || mode == ShadowCastingMode.ShadowsOnly)
                    continue;

                if (!TryGetSize(renderer, out Vector3 size))
                    continue;

                // Small things (casings, rocket tips) and thin ones (bullets: 0.6 m long, a few cm
                // across) throw a shadow nobody can make out from a camera 15 m up.
                float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float middle = size.x + size.y + size.z - largest - Mathf.Min(size.x, Mathf.Min(size.y, size.z));

                if (largest < SMALL_SHADOW_CASTER || middle < THIN_SHADOW_CASTER)
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            MeshRenderers.Clear();
        }

        // Read from the mesh rather than Renderer.bounds, which stays empty for a renderer on an
        // inactive object, and parts of a pooled prefab can start out inactive.
        private static bool TryGetSize(MeshRenderer renderer, out Vector3 size)
        {
            size = default;
            if (!renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                return false;

            Vector3 scaled = Vector3.Scale(filter.sharedMesh.bounds.size, renderer.transform.lossyScale);
            size = new Vector3(Mathf.Abs(scaled.x), Mathf.Abs(scaled.y), Mathf.Abs(scaled.z));
            return true;
        }
    }
}
