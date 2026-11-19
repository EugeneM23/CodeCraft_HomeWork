using System.Collections.Generic;
using UnityEngine;

namespace Ticking
{
    /// <summary>
    /// Drives a <see cref="GameLoop"/> from Unity: its FixedUpdate, Update and LateUpdate call the loop's, and the
    /// engine freezers ticked here stop what Unity moves by itself while the game is paused. Lives on an object that
    /// lasts as long as the game (the services object); who registers <see cref="Loop"/> as a service is up to the
    /// game. Destroyed, it lets every pause go, so the freezers give the engine back (a physics mode changed in Play
    /// Mode would stay in Project Settings). Switching the runner off stops the ticks but freezes nothing: pause through
    /// the loop.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    [AddComponentMenu("Game Loop/Game Loop Runner")]
    public sealed class GameLoopRunner : MonoBehaviour
    {
        [Header("Freeze while paused")]
        [Tooltip("Physics2D.simulationMode is Script while paused: bodies keep their velocities and contacts")]
        [SerializeField] private bool _physics2D = true;
        [Tooltip("Physics.simulationMode is Script while paused")]
        [SerializeField] private bool _physics3D = true;
        [Tooltip("Animators get speed 0; those on unscaled time (menus) keep going")]
        [SerializeField] private bool _animators = true;
        [Tooltip("Legacy Animation components: their states get speed 0")]
        [SerializeField] private bool _legacyAnimations = true;
        [Tooltip("Particle systems pause; those on unscaled time (menus) keep going")]
        [SerializeField] private bool _particles = true;
        [Tooltip("AudioListener.pause: every AudioSource but ignoreListenerPause ones. Leave off when the game pauses its own audio engine")]
        [SerializeField] private bool _audioListener;

        private readonly List<IPauseListener> _freezers = new();
        private GameLoop _loop;

        /// <summary>The loop this runner drives; made on first use, so it can be registered before Awake.</summary>
        public GameLoop Loop => _loop ??= new GameLoop();

        /// <summary>Sets the freezers up from code (tests) — before Awake, the component switched off.</summary>
        public void Configure(bool physics2D, bool physics3D, bool animators, bool legacyAnimations, bool particles,
            bool audioListener)
        {
            _physics2D = physics2D;
            _physics3D = physics3D;
            _animators = animators;
            _legacyAnimations = legacyAnimations;
            _particles = particles;
            _audioListener = audioListener;
        }

        private void Awake() => AddFreezers();

        private void FixedUpdate() => Loop.FixedUpdate(Time.fixedDeltaTime);

        private void Update() => Loop.Update(Time.deltaTime);

        private void LateUpdate() => Loop.LateUpdate(Time.deltaTime);

        private void OnDestroy()
        {
            Loop.ResumeAll();
            foreach (IPauseListener freezer in _freezers)
            {
                Loop.Remove(freezer);
                (freezer as System.IDisposable)?.Dispose();
            }

            _freezers.Clear();
        }

        private void AddFreezers()
        {
#if GAMELOOP_PHYSICS_2D
            if (_physics2D)
                AddFreezer(new Physics2DFreezer());
#endif
#if GAMELOOP_PHYSICS
            if (_physics3D)
                AddFreezer(new PhysicsFreezer());
#endif
#if GAMELOOP_ANIMATION
            if (_animators)
                AddFreezer(new AnimatorFreezer());
            if (_legacyAnimations)
                AddFreezer(new LegacyAnimationFreezer());
#endif
#if GAMELOOP_PARTICLES
            if (_particles)
                AddFreezer(new ParticleSystemFreezer());
#endif
#if GAMELOOP_AUDIO
            if (_audioListener)
                AddFreezer(new AudioListenerFreezer());
#endif
        }

        private void AddFreezer(IPauseListener freezer)
        {
            _freezers.Add(freezer);
            Loop.Add(freezer);
        }
    }
}
