using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// What the senses hear and find (step-90 recheck): the signal bus when a listener broadcasts while it hears, and the
    /// default finder of objects by tag — what it leaves out, how far it looks, an undefined tag.
    /// </summary>
    public sealed class SensesSignalTests
    {
        private const string KIND = "Respawn";          // a built-in tag no other edit-mode test carries

        private readonly List<GameObject> _scene = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject made in _scene)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _scene.Clear();
        }

        // ---------- the bus ----------

        [Test]
        public void SignalBus_AListenerBroadcastsWhileHearing_TheFirstSignalStillReachesEveryone()
        {
            var bus = new SignalBus();
            var relay = new Listener();
            var a = new Listener();
            var b = new Listener();
            bool relayed = false;
            relay.OnHear = signal =>
            {
                if (signal.Id != "Alarm" || relayed)
                    return;

                relayed = true;                                     // the first to hear passes it on at once
                bus.Broadcast(new Signal("Relay", Vector3.zero), SignalArea.Within(0f));
            };
            bus.Subscribe(relay);
            bus.Subscribe(a);
            bus.Subscribe(b);

            int heard = bus.Broadcast(new Signal("Alarm", Vector3.zero), SignalArea.Within(0f));

            Assert.AreEqual(3, heard, "Everyone heard the alarm, whatever the first of them said meanwhile");
            CollectionAssert.AreEqual(new[] { "Relay", "Alarm" }, a.Heard, "The relay first (said while the alarm went round)");
            CollectionAssert.AreEqual(new[] { "Relay", "Alarm" }, b.Heard);
        }

        // ---------- the finder by tag ----------

        [Test]
        public void TagObjectFinder_FindAllAndNearest_SelfChildrenInactiveAndFarLeftOut_MaxValueIsAnywhere()
        {
            GameObject self = Tagged("Self", Vector3.zero);
            GameObject child = Tagged("Child", new Vector3(0.5f, 0f, 0f));
            child.transform.SetParent(self.transform);
            GameObject sleeping = Tagged("Inactive", new Vector3(1f, 0f, 0f));
            sleeping.SetActive(false);
            GameObject near = Tagged("Near", new Vector3(5f, 0f, 0f));
            GameObject far = Tagged("Far", new Vector3(50f, 0f, 0f));
            var finder = new TagObjectFinder();
            var found = new List<Transform>();

            finder.FindAll(KIND, Vector3.zero, 10f, self.transform, found);
            CollectionAssert.AreEquivalent(new[] { near.transform }, Mine(found),
                "Within 10 m: not itself, not its child, not the inactive one");

            finder.FindAll(KIND, Vector3.zero, float.MaxValue, self.transform, found);
            CollectionAssert.AreEquivalent(new[] { near.transform, far.transform }, Mine(found), "Anywhere: no overflow");

            finder.FindAll(KIND, Vector3.zero, 0f, self.transform, found);
            CollectionAssert.IsEmpty(found, "A radius of 0: nothing");

            Assert.AreSame(near.transform, finder.FindNearest(KIND, Vector3.zero, 60f, self.transform));
        }

        [Test]
        public void TagObjectFinder_AnUndefinedTag_NothingFound_SaidOnce()
        {
            const string UNDEFINED = "AIKit Coverage Undefined Tag";
            var finder = new TagObjectFinder();
            var found = new List<Transform> { null };

            LogAssert.Expect(LogType.Warning, new Regex(UNDEFINED));
            finder.FindAll(UNDEFINED, Vector3.zero, 10f, null, found);
            Assert.IsNull(finder.FindNearest(UNDEFINED, Vector3.zero, 10f, null));

            CollectionAssert.IsEmpty(found);
            LogAssert.NoUnexpectedReceived();
        }

        // ---------- helpers ----------

        private GameObject Tagged(string name, Vector3 at)
        {
            var go = new GameObject(name) { tag = KIND };
            go.transform.position = at;
            _scene.Add(go);
            return go;
        }

        // Only what this test made (the scene may hold other objects of the kind)
        private List<Transform> Mine(List<Transform> found)
        {
            var mine = new List<Transform>();
            foreach (Transform transform in found)
            {
                if (_scene.Contains(transform.gameObject))
                    mine.Add(transform);
            }

            return mine;
        }

        private sealed class Listener : ISignalListener
        {
            public readonly List<string> Heard = new();
            public Action<Signal> OnHear;

            public Transform Root => null;

            public Vector3 ListenPosition => Vector3.zero;

            public int Faction => 0;

            public void Hear(in Signal signal)
            {
                Heard.Add(signal.Id);
                OnHear?.Invoke(signal);
            }
        }
    }
}
