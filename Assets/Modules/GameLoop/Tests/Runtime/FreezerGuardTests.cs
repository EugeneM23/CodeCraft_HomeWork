using NUnit.Framework;
using UnityEngine;

namespace Ticking.Tests
{
    /// <summary>
    /// Freezers told twice or out of turn: a second pause must not remember the frozen state as the one to give back,
    /// a resume without a pause must leave the engine alone.
    /// </summary>
    public class FreezerGuardTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

#if GAMELOOP_PHYSICS_2D
        [Test]
        public void Physics2D_PausedTwice_ResumeGivesTheFirstModeBack()
        {
            SimulationMode2D original = Physics2D.simulationMode;
            var freezer = new Physics2DFreezer();
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Update;

                freezer.OnPause();
                freezer.OnPause();
                freezer.OnResume();

                Assert.AreEqual(SimulationMode2D.Update, Physics2D.simulationMode);
            }
            finally
            {
                Physics2D.simulationMode = original;
            }
        }

        [Test]
        public void Physics2D_ResumeWithoutPause_LeavesModeAlone()
        {
            SimulationMode2D original = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Update;

                new Physics2DFreezer().OnResume();

                Assert.AreEqual(SimulationMode2D.Update, Physics2D.simulationMode);
            }
            finally
            {
                Physics2D.simulationMode = original;
            }
        }
#endif

#if GAMELOOP_PHYSICS
        [Test]
        public void Physics_PausedTwice_ResumeGivesTheFirstModeBack()
        {
            SimulationMode original = Physics.simulationMode;
            var freezer = new PhysicsFreezer();
            try
            {
                Physics.simulationMode = SimulationMode.Update;

                freezer.OnPause();
                freezer.OnPause();
                freezer.OnResume();

                Assert.AreEqual(SimulationMode.Update, Physics.simulationMode);
            }
            finally
            {
                Physics.simulationMode = original;
            }
        }

        [Test]
        public void Physics_ResumeWithoutPause_LeavesModeAlone()
        {
            SimulationMode original = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Update;

                new PhysicsFreezer().OnResume();

                Assert.AreEqual(SimulationMode.Update, Physics.simulationMode);
            }
            finally
            {
                Physics.simulationMode = original;
            }
        }
#endif

#if GAMELOOP_PHYSICS && UNITY_EDITOR
        [Test]
        public void Physics_FreezerLostWhilePaused_ModeComesBackOnLeavingPlayMode()
        {
            SimulationMode original = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Update;
                new PhysicsFreezer().OnPause();   // and never resumed: a script reload in Play Mode loses the loop
                Assume.That(Physics.simulationMode, Is.EqualTo(SimulationMode.Script));

                FrozenPhysicsModes.RestoreForgotten();   // what leaving Play Mode does

                Assert.AreEqual(SimulationMode.Update, Physics.simulationMode);
            }
            finally
            {
                Physics.simulationMode = original;
            }
        }
#endif

#if GAMELOOP_ANIMATION
        [Test]
        public void ComponentFreezer_PausedTwice_ResumeGivesTheOldSpeedBack()
        {
            _go = new GameObject("Animated");
            var animator = _go.AddComponent<Animator>();
            animator.speed = 1.5f;
            var freezer = new AnimatorFreezer();

            freezer.OnPause();
            freezer.OnPause();
            Assert.AreEqual(0f, animator.speed);

            freezer.OnResume();
            Assert.AreEqual(1.5f, animator.speed);
        }

        [Test]
        public void ComponentFreezer_ResumeWithoutPause_LeavesComponentsAlone()
        {
            _go = new GameObject("Animated");
            var animator = _go.AddComponent<Animator>();
            animator.speed = 0.5f;

            new AnimatorFreezer().OnResume();

            Assert.AreEqual(0.5f, animator.speed);
        }

        [Test]
        public void LegacyAnimation_SwitchedOff_LeftAlone()
        {
            _go = new GameObject("Saw");
            var animation = _go.AddComponent<Animation>();
            var clip = new AnimationClip { legacy = true, wrapMode = WrapMode.Loop };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            animation.AddClip(clip, "Spin");
            animation["Spin"].speed = 2f;
            animation.enabled = false;
            var freezer = new LegacyAnimationFreezer();
            try
            {
                freezer.OnPause();
                Assert.AreEqual(2f, animation["Spin"].speed, "a switched-off component is not frozen");

                freezer.OnResume();
                Assert.AreEqual(2f, animation["Spin"].speed);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
#endif

#if GAMELOOP_AUDIO
        [Test]
        public void AudioListener_PausedTwice_ResumeGivesTheOldStateBack()
        {
            bool before = AudioListener.pause;
            var freezer = new AudioListenerFreezer();
            try
            {
                AudioListener.pause = false;

                freezer.OnPause();
                freezer.OnPause();
                freezer.OnResume();

                Assert.IsFalse(AudioListener.pause);
            }
            finally
            {
                AudioListener.pause = before;
            }
        }

        [Test]
        public void AudioListener_ResumeWithoutPause_LeavesListenerAlone()
        {
            bool before = AudioListener.pause;
            try
            {
                AudioListener.pause = true;

                new AudioListenerFreezer().OnResume();

                Assert.IsTrue(AudioListener.pause);
            }
            finally
            {
                AudioListener.pause = before;
            }
        }

        [Test]
        public void Runner_AudioListenerSwitchedOn_PauseStopsAudioAndDeathGivesItBack()
        {
            bool before = AudioListener.pause;
            AudioListener.pause = false;
            var go = new GameObject("Runner");
            try
            {
                go.SetActive(false);
                var runner = go.AddComponent<GameLoopRunner>();
                runner.Configure(physics2D: false, physics3D: false, animators: false, legacyAnimations: false,
                    particles: false, audioListener: true);
                go.SetActive(true);

                runner.Loop.Pause(this);
                Assert.IsTrue(AudioListener.pause);

                Object.DestroyImmediate(go);
                Assert.IsFalse(AudioListener.pause);
            }
            finally
            {
                if (go != null)
                    Object.DestroyImmediate(go);
                AudioListener.pause = before;
            }
        }
#endif
    }
}
