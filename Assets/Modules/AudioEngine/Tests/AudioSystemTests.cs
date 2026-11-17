using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AudioEngine.Tests
{
    public sealed class AudioSystemTests
    {
        private const string BANK = "test";
        private const string SHOT = "shot";
        private const string LOOP = "loop";

        private GameObject _root;
        private AudioSystem _system;
        private AudioBank _bank;
        private AudioClip _clip;
        private AudioSourceEvent _shot;
        private AudioSourceEvent _loop;

        [SetUp]
        public void SetUp()
        {
            _clip = AudioClip.Create("Clip", 44100, 1, 44100, false); // 1 second

            _shot = MakeEvent(_clip, loop: false);
            _loop = MakeEvent(_clip, loop: true);

            _bank = ScriptableObject.CreateInstance<AudioBank>();
            _bank.identifier = BANK;
            _bank.eventParameters = new[]
            {
                new EventParameter { identifier = SHOT, value = _shot },
                new EventParameter { identifier = LOOP, value = _loop }
            };
            _bank.boolParameters = new BoolParameter[0];
            _bank.intParameters = new IntParameter[0];
            _bank.floatParameters = new[] { new FloatParameter { identifier = "rpm", value = 0.25f } };
            _bank.callbacks = new string[0];

            _root = new GameObject("AudioSystem");
            _system = _root.AddComponent<AudioSystem>();
            Set(_system, "initialBanks", new AudioBank[0]);
            _system.Initialize();
            _system.LoadBank(_bank);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_bank);
            Object.DestroyImmediate(_shot);
            Object.DestroyImmediate(_loop);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void LoadBank_RegistersEventsAndParameters_UnderBankDotId()
        {
            Assert.IsTrue(_system.PlayEvent("test.shot"));
            Assert.AreEqual(0.25f, _system.GetFloat("test.rpm"));
        }

        [Test]
        public void UnloadBank_ForgetsItsEventsAndParameters()
        {
            _system.UnloadBank(_bank);

            Assert.IsFalse(_system.PlayEvent("test.shot"));
            Assert.IsFalse(_system.HasFloat("test.rpm"));
        }

        [Test]
        public void PlayEvent_UnknownId_ReturnsFalse()
        {
            Assert.IsFalse(_system.PlayEvent("test.missing"));
        }

        [Test]
        public void PlayEvent_WithMaxFrequency_SkipsTheRepeatUntilTheWindowPasses()
        {
            Assert.IsTrue(_system.PlayEvent("test.shot", 0.5f));
            Assert.IsFalse(_system.PlayEvent("test.shot", 0.5f));
        }

        [Test]
        public void PlayEvent_WithZeroDuration_LastsAsLongAsTheClip()
        {
            _system.PlayEvent("test.shot", out AudioEventHandle handle);

            Tick(handle, 0.5f);
            Assert.IsTrue(handle.IsValid, "half of the clip played: the event is still alive");

            Tick(handle, 0.6f);
            Assert.IsFalse(handle.IsValid, "the clip is over: the event went back to the pool");
        }

        [Test]
        public void LoopEvent_KeepsPlayingPastTheClipLength()
        {
            _system.PlayEvent("test.loop", out AudioEventHandle handle);

            Tick(handle, 0.7f);
            Tick(handle, 0.7f);
            Tick(handle, 0.7f);

            Assert.IsTrue(handle.IsValid);
            Assert.IsTrue(handle.IsPlaying);
        }

        [Test]
        public void HandleDispose_DisposesItsOwnEvent_NotAnotherWithTheSameId()
        {
            _system.PlayEvent("test.loop", out AudioEventHandle first);
            _system.PlayEvent("test.loop", out AudioEventHandle second);

            second.Dispose();

            Assert.IsTrue(first.IsValid);
            Assert.IsFalse(second.IsValid);
        }

        [Test]
        public void PauseAndResume_KeepEventsAlive()
        {
            _system.PlayEvent("test.loop", out AudioEventHandle handle);

            _system.Pause();
            Assert.IsFalse(_system.enabled, "a paused system stops its Update");
            _system.Resume();

            Assert.IsTrue(_system.enabled);
            Assert.IsTrue(handle.IsValid);
        }

        [Test]
        public void DestroyedPivot_DoesNotThrow_AndTheEventStillFinishes()
        {
            GameObject pivot = new GameObject("Pivot");
            _system.PlayEvent("test.shot", pivot.transform, out AudioEventHandle handle);
            Object.DestroyImmediate(pivot);

            Assert.DoesNotThrow(() => _system.TickForTests(0.5f));
            Assert.IsTrue(handle.IsValid, "the event keeps playing where its pivot was");

            _system.TickForTests(0.6f);
            Assert.IsFalse(handle.IsValid, "the clip is over: the event went back to the pool");
        }

        [Test]
        public void DisposeWithFade_LowersTheVolume_ThenReleasesTheEvent()
        {
            _system.PlayEvent("test.loop", out AudioEventHandle handle);
            AudioSourceEvent evt = EventOf(handle);
            AudioSource source = evt.Source;

            handle.Dispose(1f);
            Assert.IsFalse(handle.IsValid, "disposing forgets the event at once");
            Assert.IsTrue(evt.IsValid, "but it is still fading, not back in the pool yet");
            Assert.AreEqual(1, _system.FadingCount);

            _system.TickForTests(0.5f);
            Assert.AreEqual(0.5f, source.volume, 0.01f, "linear fade: half way, half volume");

            _system.TickForTests(0.6f);
            Assert.AreEqual(0, _system.FadingCount);
            Assert.IsFalse(evt.IsValid, "the fade ended: the event went back to the pool");
            Assert.IsNull(evt.Source, "the fade ended: the AudioSource went back to its pool");
        }

        [Test]
        public void DisposeWithFade_ThenPause_FreezesTheFade()
        {
            _system.PlayEvent("test.loop", out AudioEventHandle handle);
            AudioSourceEvent evt = EventOf(handle);
            AudioSource source = evt.Source;

            handle.Dispose(0.5f);
            _system.Pause();

            Assert.IsFalse(_system.enabled, "a paused system stops its Update");
            Assert.AreEqual(1, _system.FadingCount, "the fading event is still held by the system");

            _system.TickForTests(10f);
            Assert.AreEqual(1, _system.FadingCount, "a paused system does not advance its fades");
            Assert.AreEqual(1f, source.volume, 0.01f);
            Assert.IsTrue(evt.IsValid);

            _system.Resume();
            _system.TickForTests(0.6f);
            Assert.AreEqual(0, _system.FadingCount);
            Assert.IsFalse(evt.IsValid);
        }

        [Test]
        public void StopWithFade_ThenPlay_CancelsTheFade()
        {
            _system.PlayEvent("test.loop", out AudioEventHandle handle);
            AudioSource source = EventOf(handle).Source;

            handle.Stop(1f);
            _system.TickForTests(0.5f);
            Assert.AreEqual(1, _system.FadingCount);

            handle.Play();
            Assert.AreEqual(0, _system.FadingCount, "starting the event cancels its stop-fade");
            Assert.IsTrue(handle.IsPlaying);
            Assert.AreEqual(1f, source.volume, 0.01f, "and brings the volume back");
        }

        [Test]
        public void AudioEventKey_ConvertsToBankDotEvent()
        {
            var key = new AudioEventKey();
            object boxed = key;
            Set(boxed, "bank", _bank);
            Set(boxed, "eventId", SHOT);
            ((ISerializationCallbackReceiver)boxed).OnAfterDeserialize();

            Assert.AreEqual("test.shot", (string)(AudioEventKey)boxed);
        }

        private static AudioSourceEvent MakeEvent(AudioClip clip, bool loop)
        {
            AudioSourceEvent evt = ScriptableObject.CreateInstance<AudioSourceEvent>();
            ClipConst provider = new ClipConst();
            Set(provider, "clip", clip);
            Set(evt, "clip", provider);
            Set(evt, "loop", loop);
            Set(evt, "duration", 0f);
            Set(evt, "behaviours", System.Array.CreateInstance(Field(evt, "behaviours").FieldType.GetElementType(), 0));
            Set(evt, "actions", System.Array.CreateInstance(Field(evt, "actions").FieldType.GetElementType(), 0));
            return evt;
        }

        // Update() does not run in EditMode: advance the event the way AudioSystem.Update does.
        private static void Tick(AudioEventHandle handle, float deltaTime)
        {
            object evt = typeof(AudioEventHandle)
                .GetField("audioEvent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(handle);
            if (evt is AudioEventBase audioEvent && audioEvent.IsValid)
                audioEvent.OnUpdate(deltaTime);
        }

        private static AudioSourceEvent EventOf(AudioEventHandle handle) =>
            (AudioSourceEvent)typeof(AudioEventHandle)
                .GetField("audioEvent", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(handle);

        private static FieldInfo Field(object target, string name)
        {
            for (System.Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (field != null)
                    return field;
            }

            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);

        private sealed class MissingFieldException : System.Exception
        {
            public MissingFieldException(string type, string field) : base($"{type}.{field} not found") { }
        }
    }
}
