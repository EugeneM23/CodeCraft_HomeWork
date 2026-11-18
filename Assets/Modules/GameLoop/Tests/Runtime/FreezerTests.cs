using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ticking.Tests
{
    /// <summary>Engine freezers: what Unity moves by itself stands still while paused and goes on after.</summary>
    public class FreezerTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

#if GAMELOOP_PHYSICS_2D
        [UnityTest]
        public IEnumerator Physics2D_Paused_BodyStandsAndKeepsVelocity()
        {
            var runnerObject = new GameObject("Runner");
            GameLoop loop = runnerObject.AddComponent<GameLoopRunner>().Loop;
            _go = new GameObject("Body");
            var body = _go.AddComponent<Rigidbody2D>();
            body.interpolation = RigidbodyInterpolation2D.None;
            try
            {
                for (int i = 0; i < 5; i++)
                    yield return new WaitForFixedUpdate();

                loop.Pause(this);
                Vector2 position = body.position;
                Vector2 velocity = body.linearVelocity;
                Assume.That(velocity.y, Is.LessThan(0f), "the body was falling");

                for (int i = 0; i < 5; i++)
                    yield return new WaitForFixedUpdate();

                Assert.AreEqual(position, body.position, "stands while paused");
                Assert.AreEqual(velocity, body.linearVelocity, "keeps its velocity");

                loop.Resume(this);
                for (int i = 0; i < 3; i++)
                    yield return new WaitForFixedUpdate();

                Assert.Less(body.position.y, position.y, "falls on after the pause");
                Assert.Less(body.linearVelocity.y, velocity.y, "from the same velocity");
            }
            finally
            {
                Object.DestroyImmediate(runnerObject);
            }
        }

        [Test]
        public void Physics2D_Resume_GivesTheOldModeBack()
        {
            SimulationMode2D mode = Physics2D.simulationMode;
            var freezer = new Physics2DFreezer();
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Update;

                freezer.OnPause();
                Assert.AreEqual(SimulationMode2D.Script, Physics2D.simulationMode);

                freezer.OnResume();
                Assert.AreEqual(SimulationMode2D.Update, Physics2D.simulationMode);
            }
            finally
            {
                Physics2D.simulationMode = mode;
            }
        }
#endif

#if GAMELOOP_PHYSICS_2D && UNITY_EDITOR
        [Test]
        public void Physics2D_FreezerLostWhilePaused_ModeComesBackOnLeavingPlayMode()
        {
            SimulationMode2D mode = Physics2D.simulationMode;
            new Physics2DFreezer().OnPause();   // and never resumed: a script reload in Play Mode loses the loop
            Assume.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));

            FrozenPhysicsModes.RestoreForgotten();   // what leaving Play Mode does

            Assert.AreEqual(mode, Physics2D.simulationMode);
        }

        [Test]
        public void Physics2D_Resumed_NothingLeftToRestore()
        {
            SimulationMode2D original = Physics2D.simulationMode;
            var freezer = new Physics2DFreezer();
            freezer.OnPause();
            freezer.OnResume();
            Physics2D.simulationMode = SimulationMode2D.Update;
            try
            {
                FrozenPhysicsModes.RestoreForgotten();

                Assert.AreEqual(SimulationMode2D.Update, Physics2D.simulationMode, "a later mode is not touched");
            }
            finally
            {
                Physics2D.simulationMode = original;
            }
        }
#endif

#if GAMELOOP_PHYSICS
        [Test]
        public void Physics_Resume_GivesTheOldModeBack()
        {
            SimulationMode mode = Physics.simulationMode;
            var freezer = new PhysicsFreezer();
            try
            {
                Physics.simulationMode = SimulationMode.Update;

                freezer.OnPause();
                Assert.AreEqual(SimulationMode.Script, Physics.simulationMode);

                freezer.OnResume();
                Assert.AreEqual(SimulationMode.Update, Physics.simulationMode);
            }
            finally
            {
                Physics.simulationMode = mode;
            }
        }
#endif

#if GAMELOOP_ANIMATION
        [Test]
        public void Animator_Paused_SpeedZeroThenBack()
        {
            _go = new GameObject("Animated");
            var animator = _go.AddComponent<Animator>();
            animator.speed = 1.5f;
            var freezer = new AnimatorFreezer();

            freezer.OnPause();
            Assert.AreEqual(0f, animator.speed);

            freezer.OnResume();
            Assert.AreEqual(1.5f, animator.speed);
        }

        [Test]
        public void Animator_OnUnscaledTime_KeepsGoing()
        {
            _go = new GameObject("Menu");
            var animator = _go.AddComponent<Animator>();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            var freezer = new AnimatorFreezer();

            freezer.OnPause();

            Assert.AreEqual(1f, animator.speed);
            freezer.OnResume();
        }

        [Test]
        public void ComponentFreezer_SceneArrivesWhilePaused_StandsToo()
        {
            var freezer = new AnimatorFreezer();
            freezer.OnPause();
            _go = new GameObject("ArrivedWithScene");
            var animator = _go.AddComponent<Animator>();

            freezer.FreezeScene(_go.scene);
            Assert.AreEqual(0f, animator.speed);

            freezer.OnResume();
            Assert.AreEqual(1f, animator.speed);
        }

        [Test]
        public void ComponentFreezer_SceneWhileRunning_LeftAlone()
        {
            var freezer = new AnimatorFreezer();
            _go = new GameObject("Animated");
            var animator = _go.AddComponent<Animator>();

            freezer.FreezeScene(_go.scene);

            Assert.AreEqual(1f, animator.speed);
        }

        [Test]
        public void ComponentFreezer_FoundTwice_ThawedOnce()
        {
            _go = new GameObject("Animated");
            var animator = _go.AddComponent<Animator>();
            animator.speed = 2f;
            var freezer = new AnimatorFreezer();

            freezer.OnPause();
            freezer.FreezeScene(_go.scene);
            freezer.OnResume();

            Assert.AreEqual(2f, animator.speed, "the second find must not remember speed 0");
        }

        [Test]
        public void ComponentFreezer_ObjectsMadeAfterAPause_StandOnTheNext()
        {
            var freezer = new AnimatorFreezer();
            freezer.OnPause();
            freezer.OnResume();

            _go = new GameObject("SpawnedLater");
            var child = new GameObject("Child");
            child.transform.SetParent(_go.transform);
            var animator = child.AddComponent<Animator>();

            freezer.OnPause();
            Assert.AreEqual(0f, animator.speed, "a nested animator made after the scene started is frozen");

            freezer.OnResume();
            Assert.AreEqual(1f, animator.speed);
            freezer.Dispose();
        }

        [Test]
        public void ComponentFreezer_DontDestroyOnLoadObject_StandsToo()
        {
            if (!Application.isPlaying)
                Assert.Ignore("DontDestroyOnLoad needs Play Mode");

            _go = new GameObject("Persistent");
            Object.DontDestroyOnLoad(_go);
            var animator = _go.AddComponent<Animator>();
            var freezer = new AnimatorFreezer();

            freezer.OnPause();
            Assert.AreEqual(0f, animator.speed);

            freezer.OnResume();
            Assert.AreEqual(1f, animator.speed);
            freezer.Dispose();
        }

        [Test]
        public void ComponentFreezer_InactiveObject_LeftAlone()
        {
            _go = new GameObject("Off");
            var animator = _go.AddComponent<Animator>();
            animator.speed = 1.25f;
            _go.SetActive(false);
            var freezer = new AnimatorFreezer();

            freezer.OnPause();
            Assert.AreEqual(1.25f, animator.speed);
            freezer.OnResume();
            freezer.Dispose();
        }

        [Test]
        public void ComponentFreezer_PauseAndResume_AllocateNothingAfterWarmUp()
        {
            _go = new GameObject("Crowd");
            for (int i = 0; i < 40; i++)
            {
                var member = new GameObject("Member" + i);
                member.transform.SetParent(_go.transform);
                member.AddComponent<Animator>();
            }

            var freezer = new AnimatorFreezer();
            for (int i = 0; i < 3; i++)
            {
                freezer.OnPause();
                freezer.OnResume();
            }

            Assert.That(() =>
            {
                freezer.OnPause();
                freezer.OnResume();
            }, new NUnit.Framework.Constraints.NotConstraint(new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint()));
            freezer.Dispose();
        }

        [Test]
        public void Runner_PauseAndResume_AllocateNothingAfterWarmUp()
        {
            var runnerObject = new GameObject("Runner");
            GameLoop loop = runnerObject.AddComponent<GameLoopRunner>().Loop;
            _go = new GameObject("Scene");
            for (int i = 0; i < 20; i++)
            {
                var member = new GameObject("Member" + i);
                member.transform.SetParent(_go.transform);
                member.AddComponent<Animator>();
#if GAMELOOP_PARTICLES
                member.AddComponent<ParticleSystem>().Play();
#endif
            }

            try
            {
                for (int i = 0; i < 3; i++)
                {
                    loop.Pause(this);
                    loop.Resume(this);
                }

                Assert.That(() =>
                {
                    loop.Pause(this);
                    loop.Resume(this);
                }, new NUnit.Framework.Constraints.NotConstraint(new UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint()));
            }
            finally
            {
                Object.DestroyImmediate(runnerObject);
            }
        }

        [Test]
        public void LegacyAnimation_Paused_StateSpeedZeroThenBack()
        {
            _go = new GameObject("Saw");
            var animation = _go.AddComponent<Animation>();
            var clip = new AnimationClip { legacy = true, wrapMode = WrapMode.Loop };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            animation.AddClip(clip, "Spin");
            animation.Play("Spin");
            animation["Spin"].speed = 2f;
            var freezer = new LegacyAnimationFreezer();
            try
            {
                freezer.OnPause();
                Assert.AreEqual(0f, animation["Spin"].speed);

                freezer.OnResume();
                Assert.AreEqual(2f, animation["Spin"].speed);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
#endif

#if GAMELOOP_PARTICLES
        [Test]
        public void Particles_Paused_PauseThenPlayOn()
        {
            _go = new GameObject("Dust");
            var particles = _go.AddComponent<ParticleSystem>();
            particles.Play();
            var freezer = new ParticleSystemFreezer();

            freezer.OnPause();
            Assert.IsTrue(particles.isPaused);

            freezer.OnResume();
            Assert.IsTrue(particles.isPlaying);
        }

        [Test]
        public void Particles_OnUnscaledTime_KeepPlaying()
        {
            _go = new GameObject("MenuSparkles");
            var particles = _go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.useUnscaledTime = true;
            particles.Play();
            var freezer = new ParticleSystemFreezer();

            freezer.OnPause();

            Assert.IsTrue(particles.isPlaying);
            freezer.OnResume();
        }

        [Test]
        public void Particles_StoppedBeforePause_StayStopped()
        {
            _go = new GameObject("Done");
            var particles = _go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var freezer = new ParticleSystemFreezer();

            freezer.OnPause();
            freezer.OnResume();

            Assert.IsFalse(particles.isPlaying);
        }
#endif

#if GAMELOOP_AUDIO
        [Test]
        public void AudioListener_Paused_PausedThenBack()
        {
            bool before = AudioListener.pause;
            var freezer = new AudioListenerFreezer();
            try
            {
                AudioListener.pause = false;

                freezer.OnPause();
                Assert.IsTrue(AudioListener.pause);

                freezer.OnResume();
                Assert.IsFalse(AudioListener.pause);
            }
            finally
            {
                AudioListener.pause = before;
            }
        }
#endif
    }
}
