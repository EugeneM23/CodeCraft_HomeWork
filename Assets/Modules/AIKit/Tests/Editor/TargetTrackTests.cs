using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>Stage 3b: the target is a track — a subject that may vanish and a memory that stays.</summary>
    public class TargetTrackTests
    {
        private sealed class MapResolver : ITargetResolver
        {
            public Transform Answer;

            public Transform Resolve(int id) => id == 7 ? Answer : null;
        }

        private GraphRig _rig;

        [SetUp]
        public void SetUp() => _rig = new GraphRig();

        [Test]
        public void Track_FollowsALiveSubject()
        {
            Transform target = _rig.CreateTargetAt(new Vector3(3f, 0f, 0f));

            target.position = new Vector3(5f, 1f, 0f);
            _rig.Tick(0.02f);

            Assert.IsTrue(_rig.Context.HasTarget);
            Assert.AreEqual(new Vector3(5f, 1f, 0f), _rig.Context.Track.LastKnownPosition);
            Assert.AreEqual(0f, _rig.Context.Track.TimeSinceSeen);
        }

        [Test]
        public void TargetGoneToThePool_StopsBeingTheTarget_WithoutTheGame()
        {
            Transform target = _rig.CreateTargetAt(new Vector3(3f, 0f, 0f));
            _rig.Tick(0.02f);

            target.gameObject.SetActive(false);
            for (int i = 0; i < 25; i++)
                _rig.Tick(0.02f);

            Assert.IsFalse(_rig.Context.HasTarget);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), _rig.Context.TargetPosition, "Only the memory remains");
            Assert.That(_rig.Context.Track.TimeSinceSeen, Is.GreaterThan(0.4f));

            target.gameObject.SetActive(true);              // reused from the pool as someone else
            _rig.Tick(0.02f);

            Assert.IsFalse(_rig.Context.HasTarget, "A respawn is a new target, not the old one coming back");
        }

        [Test]
        public void SubjectById_IsResolvedLazily()
        {
            var resolver = new MapResolver();
            _rig.Context.Track.Resolver = resolver;
            _rig.Context.Track.Set(new TargetSubject(null, id: 7));

            Assert.IsFalse(_rig.Context.HasTarget, "Not loaded yet");

            var go = new GameObject("Loaded") { hideFlags = HideFlags.HideAndDontSave };
            Transform target = go.transform;
            resolver.Answer = target;

            Assert.IsTrue(_rig.Context.HasTarget, "The id found its entity");
            Assert.AreSame(target, _rig.Context.Target);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetTarget_IsAShortcut_ForAFullyAwareTrack()
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.transform.position = new Vector3(1f, 2f, 0f);
                _rig.Agent.SetTarget(go.transform);

                Assert.AreEqual(1f, _rig.Context.Track.Awareness);
                Assert.AreEqual(new Vector3(1f, 2f, 0f), _rig.Context.Track.LastKnownPosition);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
