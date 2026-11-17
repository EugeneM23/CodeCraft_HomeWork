#if SAVE_PARTICLES
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Built in: playing, paused or stopped, the time and the random seed; restored by fast-forwarding Simulate from
    /// that seed. Setting the seed turns useAutoRandomSeed off. With the "live particles" option the living
    /// particles are written too (the file grows with them). A system that has stopped emitting while its particles
    /// still fly always writes them: that is all the state it has left.
    /// </summary>
    public sealed class ParticleSystemSerializer : ComponentSerializer<ParticleSystem, ParticleSystemSerializer.Data>,
        ISerializerOptions
    {
        public const string LIVE_PARTICLES = "liveParticles";

        public enum Playback
        {
            Stopped,
            Playing,
            Paused
        }

        public sealed class Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float RemainingLifetime;
            public float StartLifetime;
            public Color32 Color;
            public float Size;
            public float Rotation;
            public float AngularVelocity;
            public uint Seed;
        }

        public sealed class Data
        {
            public Playback State;
            public float Time;

            /// <summary>The part of the Start Delay that has passed: the system's time stands at 0 while it waits.</summary>
            public float Delay;

            /// <summary>
            /// The emission rate multipliers as they stand: scripts drive them (steam of an overheating gun), and the
            /// fast-forward of a load has to emit at the rate of the save, not at the rate the scene starts with.
            /// </summary>
            public float? Rate;
            public float? RateOverDistance;
            public uint Seed;
            public List<Particle> Particles;
        }

        public IReadOnlyList<string> Options { get; } = new[] { LIVE_PARTICLES };

        protected override Data Serialize(ParticleSystem particles)
        {
            var data = new Data
            {
                State = particles.isPaused ? Playback.Paused : particles.isPlaying ? Playback.Playing : Playback.Stopped,
                Time = particles.time,
                Seed = particles.randomSeed
            };

            if (data.State != Playback.Stopped)
            {
                ParticleSystem.EmissionModule emission = particles.emission;
                data.Rate = emission.rateOverTimeMultiplier;
                data.RateOverDistance = emission.rateOverDistanceMultiplier;
            }

#if UNITY_2022_1_OR_NEWER
            // A system with a Start Delay (the second wave of an explosion): its time does not count the wait, so
            // without it the system would wait all over again after a load. A looping one adds its past loops to the
            // difference: no more than the delay itself is taken
            if (data.State != Playback.Stopped)
            {
                ParticleSystem.MainModule main = particles.main;
                float lead = particles.totalTime - particles.time;
                data.Delay = Mathf.Max(0f, main.loop ? Mathf.Min(lead, main.startDelay.constantMax) : lead);
            }
#endif

            // A burst that has stopped emitting while its particles still fly (the second half of an explosion): there
            // is no time to fast-forward to, so the particles themselves are the state — they are few and short-lived
            bool fading = data.State == Playback.Stopped && particles.particleCount > 0;
            if ((!fading && !SerializerOptions.IsOn(LIVE_PARTICLES)) || particles.particleCount == 0)
                return data;

            var live = new ParticleSystem.Particle[particles.particleCount];
            int count = particles.GetParticles(live);
            data.Particles = live.Take(count).Select(particle => new Particle
            {
                Position = particle.position,
                Velocity = particle.velocity,
                RemainingLifetime = particle.remainingLifetime,
                StartLifetime = particle.startLifetime,
                Color = particle.startColor,
                Size = particle.startSize,
                Rotation = particle.rotation,
                AngularVelocity = particle.angularVelocity,
                Seed = particle.randomSeed
            }).ToList();
            return data;
        }

        protected override void Deserialize(ParticleSystem particles, Data data)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bool hasParticles = data.Particles != null && data.Particles.Count > 0;
            if (data.State == Playback.Stopped)
            {
                // Stopped emitting with particles still in the air: they fly on and nothing new comes
                if (hasParticles)
                {
                    SetParticles(particles, data.Particles);
                    particles.Play(false);
                    particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }

                return;
            }

            // The seed can be set only on a stopped system that does not pick its own
            particles.useAutoRandomSeed = false;
            particles.randomSeed = data.Seed;
            ParticleSystem.EmissionModule emission = particles.emission;
            if (data.Rate.HasValue)
                emission.rateOverTimeMultiplier = data.Rate.Value;
            if (data.RateOverDistance.HasValue)
                emission.rateOverDistanceMultiplier = data.RateOverDistance.Value;
            particles.Simulate(data.Delay + data.Time, withChildren: true, restart: true, fixedTimeStep: true);

            if (hasParticles)
                SetParticles(particles, data.Particles);

            // Simulate leaves the system paused: a playing one goes on from that time
            if (data.State == Playback.Playing)
                particles.Play(true);
        }

        private static void SetParticles(ParticleSystem particles, List<Particle> saved)
        {
            ParticleSystem.Particle[] live = saved.Select(particle => new ParticleSystem.Particle
            {
                position = particle.Position,
                velocity = particle.Velocity,
                remainingLifetime = particle.RemainingLifetime,
                startLifetime = particle.StartLifetime,
                startColor = particle.Color,
                startSize = particle.Size,
                rotation = particle.Rotation,
                angularVelocity = particle.AngularVelocity,
                randomSeed = particle.Seed
            }).ToArray();
            particles.SetParticles(live, live.Length);
        }
    }
}
#endif
