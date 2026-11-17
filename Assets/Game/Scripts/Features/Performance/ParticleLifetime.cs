using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// How long a pooled effect can still show anything after it is activated, read from its
    /// particle systems.
    /// </summary>
    /// <remarks>
    /// A non-looping system emits from its start delay until the end of its duration (a burst-only
    /// system until its last burst, which can only be earlier), and its last particle dies at most one
    /// maximum start lifetime later: stretched by simulation speed, extended by trails that outlive
    /// their particles. The effect is finished when its slowest system is.
    ///
    /// Anything those settings cannot bound leaves the caller's limit in charge: a looping system that
    /// renders or spawns anything, ring-buffer particles (they never die), sub-emitters (their
    /// particles start when a parent particle dies), unscaled-time systems (the pool's lifetime timer
    /// runs on scaled time), and parts other than particles that can be seen or heard: mesh, trail,
    /// line and sprite renderers, lights (ExplosionFlash keeps its duration to itself), animators and
    /// audio. A looping system without a renderer, particle lights or sub-emitters draws nothing (the
    /// root container of the impact templates is one) and does not count.
    /// </remarks>
    public static class ParticleLifetime
    {
        // Every particle is already dead at the computed end; the margin only covers the frame in
        // which the pool's timer runs out.
        private const float END_MARGIN = 0.1f;

        private static readonly List<ParticleSystem> Systems = new();
        private static readonly List<Renderer> Renderers = new();
        private static readonly List<Light> Lights = new();
        private static readonly List<AudioSource> AudioSources = new();

        /// <summary>
        /// Seconds after activation until every particle under <paramref name="root"/> has died,
        /// never more than <paramref name="limit"/>; <paramref name="limit"/> itself when that cannot
        /// be bounded from the particle settings.
        /// </summary>
        public static float Resolve(GameObject root, float limit)
        {
            if (root == null || HasUnboundedParts(root))
                return limit;

            Systems.Clear();
            root.GetComponentsInChildren(true, Systems);

            float end = 0f;
            bool bounded = true;
            for (int i = 0; i < Systems.Count && bounded; i++)
                bounded = TryAccumulate(Systems[i], ref end);

            Systems.Clear();

            // Nothing that emits at all: nothing to measure the effect by.
            if (!bounded || end <= 0f)
                return limit;

            return Mathf.Min(limit, end + END_MARGIN);
        }

        private static bool TryAccumulate(ParticleSystem system, ref float end)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            bool continuous = emission.rateOverTimeMultiplier > 0f || emission.rateOverDistanceMultiplier > 0f;

            // Emits nothing, so it shows nothing.
            if (!emission.enabled || (!continuous && emission.burstCount == 0))
                return true;

            ParticleSystem.MainModule main = system.main;

            if (main.loop)
                return !ShowsAnything(system);

            if (main.ringBufferMode != ParticleSystemRingBufferMode.Disabled || main.useUnscaledTime)
                return false;

            ParticleSystem.SubEmittersModule subEmitters = system.subEmitters;
            if (subEmitters.enabled && subEmitters.subEmittersCount > 0)
                return false;

            float speed = main.simulationSpeed;
            if (speed <= 0f)
                return false;

            float emitting = continuous ? main.duration : Mathf.Min(main.duration, LastBurst(emission, main.duration));
            float lifetime = ParticleCurves.Max(main.startLifetime);
            float seconds = ParticleCurves.Max(main.startDelay) + emitting + lifetime;

            ParticleSystem.TrailModule trails = system.trails;
            if (trails.enabled && !trails.dieWithParticles)
                seconds += lifetime * ParticleCurves.Max(trails.lifetime);

            end = Mathf.Max(end, seconds / speed);
            return true;
        }

        // A burst fires at its time and then every repeatInterval for cycleCount cycles; a cycleCount
        // of 0 repeats it until the duration ends.
        private static float LastBurst(ParticleSystem.EmissionModule emission, float duration)
        {
            float last = 0f;

            for (int i = 0, count = emission.burstCount; i < count; i++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(i);
                float time = burst.cycleCount == 0
                    ? duration
                    : burst.time + (burst.cycleCount - 1) * burst.repeatInterval;

                last = Mathf.Max(last, time);
            }

            return last;
        }

        private static bool ShowsAnything(ParticleSystem system)
        {
            if (system.TryGetComponent(out ParticleSystemRenderer renderer) && renderer.enabled)
                return true;

            if (system.lights.enabled)
                return true;

            ParticleSystem.SubEmittersModule subEmitters = system.subEmitters;
            return subEmitters.enabled && subEmitters.subEmittersCount > 0;
        }

        private static bool HasUnboundedParts(GameObject root)
        {
            bool unbounded = false;

            Renderers.Clear();
            root.GetComponentsInChildren(true, Renderers);
            for (int i = 0; i < Renderers.Count && !unbounded; i++)
                unbounded = Renderers[i].enabled && Renderers[i] is not ParticleSystemRenderer;
            Renderers.Clear();

            Lights.Clear();
            root.GetComponentsInChildren(true, Lights);
            for (int i = 0; i < Lights.Count && !unbounded; i++)
                unbounded = Lights[i].enabled;
            Lights.Clear();

            AudioSources.Clear();
            root.GetComponentsInChildren(true, AudioSources);
            for (int i = 0; i < AudioSources.Count && !unbounded; i++)
                unbounded = AudioSources[i].enabled && AudioSources[i].clip != null;
            AudioSources.Clear();

            return unbounded
                || root.GetComponentInChildren<Animator>(true) != null
                || root.GetComponentInChildren<Animation>(true) != null;
        }
    }
}
