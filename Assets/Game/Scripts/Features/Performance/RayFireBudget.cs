using System.Collections.Generic;
using RayFire;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Budgets RayFire's impact debris and dust, and its logging, for the active performance tier.
    /// </summary>
    /// <remarks>
    /// RayFire bakes almost everything about debris and dust into one template particle system per
    /// emitter (RFPoolingEmitter.psMain) and into the clones of it the emitter pools, when the
    /// destructible initializes. The walls initialize in Awake (init AtStart); a MeshRoot copies its
    /// RayfireDebris and RayfireDust onto most of its pieces right then
    /// (RFPoolingParticles.CopyParticlesMeshroot), and pieces split off later copy them from their
    /// parent at demolition (CopyParticlesRigid). Every copy shares its root's emitter. When a piece or
    /// an impact emits, RayFire takes only the burst settings and filters (amounts, size threshold,
    /// visibility, percentage) from the component and the pool cap and skip flag from the shared
    /// emitter; lifetime, size, particle cap, collision and shadows all come from the template.
    ///
    /// So the budget goes in after Awake and before the first frame, in two places: on every
    /// component, roots and copies, for whatever is built from them later; and on every existing
    /// emitter: its template, the clones waiting in its queue and any system already out playing.
    ///
    /// Every tier: players stop logging (RayfireMan.Log prints in release builds while debugState is
    /// on, three concatenated strings with a stack trace per demolition). Desktop keeps everything
    /// else as authored. Mobile, one value per knob as the analysis merged them: debris and dust stop
    /// casting shadows (the lit debris chips were drawn into the shadow map, and the dust billboards
    /// went through shadow culling); debris lives 1-2.5 s instead of up to 13 s, at most 8 per system,
    /// from a pool no bigger than before (6) that skips a burst instead of instantiating a new system
    /// when it runs dry, with Low world collision against static colliders only; dust lives 1-2 s at
    /// 0.3-1.2 m, does not collide (RayFire gives it no bounce and no friction, so colliding bought
    /// little besides raycasts) and has the same pool rule. Runtime fragments under 0.25 m stop
    /// casting shadows.
    /// </remarks>
    internal static class RayFireBudget
    {
        private const float DEBRIS_LIFE_MIN = 1f;
        private const float DEBRIS_LIFE_MAX = 2.5f;
        private const int DEBRIS_MAX_PARTICLES = 8;
        // A budget never grows a pool: the scene's emitters hold 6, and a bigger cap would only make
        // RayFire instantiate more clones after load.
        private const int DEBRIS_POOL_CAP = 6;

        private const float DUST_LIFE_MIN = 1f;
        private const float DUST_LIFE_MAX = 2f;
        private const float DUST_SIZE_MIN = 0.3f;
        private const float DUST_SIZE_MAX = 1.2f;

        // RayfireMan turns shadows off for runtime fragments whose bounds diagonal is under this.
        private const float FRAGMENT_SHADOW_SIZE = 0.25f;

        private static readonly HashSet<RFPoolingEmitter> Patched = new();

        private static bool _loggingSilenced;
        private static bool _loggingBefore;

        /// <summary>
        /// Gives RayfireMan's static log switch back its value from before play: the editor keeps
        /// statics after leaving play mode, and RayFire's edit-mode tools log through the same switch.
        /// </summary>
        public static void RestoreLogging()
        {
            if (!_loggingSilenced)
                return;

            RayfireMan.debugStateStatic = _loggingBefore;
            _loggingSilenced = false;
        }

        public static void Apply(bool mobile)
        {
            RayfireMan manager = RayfireMan.inst;
            if (manager != null)
                ApplyManager(manager, mobile);

            // Desktop keeps RayFire's own debris and dust, shadows included: only player logging
            // changes there (see ApplyManager).
            if (!mobile)
                return;

            foreach (RayfireDebris debris in Object.FindObjectsByType<RayfireDebris>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // A prefab open in Prefab Mode stays editable during play; a change there would be
                // saved into the asset.
                if (!Application.IsPlaying(debris))
                    continue;

                ApplyDebris(debris, mobile);
                PatchEmitter(debris.pool?.emitter, false, mobile);
            }

            foreach (RayfireDust dust in Object.FindObjectsByType<RayfireDust>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!Application.IsPlaying(dust))
                    continue;

                ApplyDust(dust, mobile);
                PatchEmitter(dust.pool?.emitter, true, mobile);
            }

            // Systems already out playing were cloned from the old template, and they go back into
            // their emitter's queue exactly as they are.
            List<ParticleSystem> playing = manager != null ? manager.particles?.resetList : null;
            if (playing != null)
            {
                foreach (ParticleSystem system in playing)
                    PatchSystem(system, IsDust(system), mobile);
            }

            Patched.Clear();
        }

        private static void ApplyManager(RayfireMan manager, bool mobile)
        {
            // RayfireMan copies the flag into a static in Awake, and Log reads only the static.
            if (mobile || !Application.isEditor)
            {
                if (!_loggingSilenced)
                {
                    _loggingBefore = RayfireMan.debugStateStatic;
                    _loggingSilenced = true;
                }

                manager.debugState = false;
                RayfireMan.debugStateStatic = false;
            }

            if (mobile && manager.advancedDemolitionProperties != null)
            {
                RFManDemolition demolition = manager.advancedDemolitionProperties;
                demolition.sizeThreshold = Mathf.Max(demolition.sizeThreshold, FRAGMENT_SHADOW_SIZE);
            }
        }

        private static void ApplyDebris(RayfireDebris debris, bool mobile)
        {
            debris.rendering.castShadows = false;

            if (!mobile)
                return;

            RFParticleEmission emission = debris.emission;
            emission.lifeMin = Mathf.Min(emission.lifeMin, DEBRIS_LIFE_MIN);
            emission.lifeMax = Mathf.Max(emission.lifeMin, Mathf.Min(emission.lifeMax, DEBRIS_LIFE_MAX));

            debris.limitations.maxParticles = Mathf.Min(debris.limitations.maxParticles, DEBRIS_MAX_PARTICLES);
            debris.pool.cap = Mathf.Min(debris.pool.cap, DEBRIS_POOL_CAP);
            debris.pool.skip = true;
            debris.collision.quality = ParticleSystemCollisionQuality.Low;
        }

        private static void ApplyDust(RayfireDust dust, bool mobile)
        {
            dust.rendering.castShadows = false;

            if (!mobile)
                return;

            RFParticleEmission emission = dust.emission;
            emission.lifeMin = Mathf.Min(emission.lifeMin, DUST_LIFE_MIN);
            emission.lifeMax = Mathf.Max(emission.lifeMin, Mathf.Min(emission.lifeMax, DUST_LIFE_MAX));
            emission.sizeMin = Mathf.Min(emission.sizeMin, DUST_SIZE_MIN);
            emission.sizeMax = Mathf.Max(emission.sizeMin, Mathf.Min(emission.sizeMax, DUST_SIZE_MAX));

            // RayFire switches dust collision on whenever it builds an emitter; Low is the cheapest a
            // component can ask for. The existing emitters get it switched off below.
            dust.collision.quality = ParticleSystemCollisionQuality.Low;

            // Same pool budget as debris: under sustained fire an empty pool skips a puff instead of
            // instantiating another particle system mid-frame.
            dust.pool.cap = Mathf.Min(dust.pool.cap, DEBRIS_POOL_CAP);
            dust.pool.skip = true;
        }

        private static void PatchEmitter(RFPoolingEmitter emitter, bool dust, bool mobile)
        {
            if (emitter == null || !Patched.Add(emitter))
                return;

            PatchSystem(emitter.psMain, dust, mobile);

            if (emitter.queue != null)
            {
                foreach (ParticleSystem system in emitter.queue)
                    PatchSystem(system, dust, mobile);
            }

            if (!mobile)
                return;

            // The cap only ever shrinks here, so the queue never needs topping up.
            emitter.cap = Mathf.Min(emitter.cap, DEBRIS_POOL_CAP);
            emitter.skip = true;
        }

        private static void PatchSystem(ParticleSystem system, bool dust, bool mobile)
        {
            if (system == null)
                return;

            if (system.TryGetComponent(out ParticleSystemRenderer renderer))
                renderer.shadowCastingMode = ShadowCastingMode.Off;

            if (!mobile)
                return;

            ParticleSystem.MainModule main = system.main;
            ParticleSystem.CollisionModule collision = system.collision;

            if (dust)
            {
                main.startLifetime = Clamp(main.startLifetime, DUST_LIFE_MIN, DUST_LIFE_MAX);
                main.startSize = Clamp(main.startSize, DUST_SIZE_MIN, DUST_SIZE_MAX);
                collision.enabled = false;
            }
            else
            {
                main.startLifetime = Clamp(main.startLifetime, DEBRIS_LIFE_MIN, DEBRIS_LIFE_MAX);
                main.maxParticles = Mathf.Min(main.maxParticles, DEBRIS_MAX_PARTICLES);

                // RayFire hard-codes dynamic colliders on for debris (RFParticles.SetCollisionDebris).
                collision.quality = ParticleSystemCollisionQuality.Low;
                collision.enableDynamicColliders = false;
            }
        }

        // RayFire renders debris as meshes and dust as billboards (RFParticles.SetParticleRenderer*).
        private static bool IsDust(ParticleSystem system) =>
            system != null
            && system.TryGetComponent(out ParticleSystemRenderer renderer)
            && renderer.renderMode == ParticleSystemRenderMode.Billboard;

        // Never lengthens: RayFire writes these as a range between two constants (RFParticles.SetMain).
        private static ParticleSystem.MinMaxCurve Clamp(ParticleSystem.MinMaxCurve value, float min, float max)
        {
            switch (value.mode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                {
                    float low = Mathf.Min(value.constantMin, min);
                    return new ParticleSystem.MinMaxCurve(low, Mathf.Max(low, Mathf.Min(value.constantMax, max)));
                }
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(Mathf.Min(value.constant, max));
                default:
                    return value;
            }
        }
    }
}
