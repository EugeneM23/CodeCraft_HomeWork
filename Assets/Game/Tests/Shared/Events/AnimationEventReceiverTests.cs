using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class AnimationEventReceiverTests
    {
        private GameObject _gameObject;
        private AnimationEventReceiver _receiver;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Animator");
            _receiver = _gameObject.AddComponent<AnimationEventReceiver>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gameObject);

        /// <summary>What an animation clip event does: Unity calls the private ReceiveEvent by name.</summary>
        private void Receive(string message) =>
            typeof(AnimationEventReceiver)
                .GetMethod("ReceiveEvent", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_receiver, new object[] { message });

        [Test]
        public void Event_ReachesEveryHandlerOfThatName_AndNoOther()
        {
            var calls = new List<string>();
            _receiver.Subscribe("Step", () => calls.Add("left"));
            _receiver.Subscribe("Step", () => calls.Add("right"));
            _receiver.Subscribe("Attack", () => calls.Add("attack"));

            Receive("Step");

            Assert.That(calls, Is.EqualTo(new[] { "left", "right" }));
        }

        [Test]
        public void Unsubscribe_StopsOnlyThatHandler()
        {
            var calls = new List<string>();
            void Left() => calls.Add("left");
            _receiver.Subscribe("Step", Left);
            _receiver.Subscribe("Step", () => calls.Add("right"));

            _receiver.Unsubscribe("Step", Left);
            Receive("Step");

            Assert.That(calls, Is.EqualTo(new[] { "right" }));
        }

        [Test]
        public void OnEvent_HearsEveryMessage_HandledOrNot()
        {
            var heard = new List<string>();
            _receiver.OnEvent += heard.Add;
            _receiver.Subscribe("Step", () => { });

            Receive("Step");
            Receive("Unknown");

            Assert.That(heard, Is.EqualTo(new[] { "Step", "Unknown" }));
        }

        [Test]
        public void LastHandlerUnsubscribed_TheEventStillArrivesWithoutAnError()
        {
            var heard = new List<string>();
            void Handler() { }
            _receiver.OnEvent += heard.Add;
            _receiver.Subscribe("Step", Handler);
            _receiver.Unsubscribe("Step", Handler);

            // Removing the last handler stores null under "Step", and ReceiveEvent then calls it.
            Assert.DoesNotThrow(() => Receive("Step"));
            Assert.That(heard, Is.EqualTo(new[] { "Step" }));
        }

        [Test]
        public void SubscribeAgainAfterTheLastHandlerLeft_Works()
        {
            int calls = 0;
            void Handler() => calls++;
            _receiver.Subscribe("Step", Handler);
            _receiver.Unsubscribe("Step", Handler);

            _receiver.Subscribe("Step", Handler);
            Receive("Step");

            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
