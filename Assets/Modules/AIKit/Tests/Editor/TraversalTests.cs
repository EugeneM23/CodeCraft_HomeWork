using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The climb and the jump are the clips' (step 90, C20; handbook §13.3, §20.3): their numbers are measured from the
    /// character's clips by the editor and kept by the body as its own data; a body with none gets over an edge the
    /// plain way. The fall is gravity's — nothing speeds it up — and a jump over a link is a ballistic arc whose height
    /// comes from the link itself. (The bodies' moves on a real NavMesh: the lab's ClimbBodyTests and JumpLinkTests.)
    /// </summary>
    public sealed class TraversalTests
    {
        private const float G = 9.81f;

        private static AnimationCurve Linear(params (float Time, float Value)[] keys)
        {
            var curve = new AnimationCurve();
            foreach ((float time, float value) in keys)
                curve.AddKey(new Keyframe(time, value));
            for (int i = 0; i < curve.length; i++)
            {
                UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Linear);
                UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, i, UnityEditor.AnimationUtility.TangentMode.Linear);
            }

            return curve;
        }

        [Test]
        public void Traversal_NeverMeasured_NothingToGoBy_ThePlainWay()
        {
            var traversal = new MeasuredTraversal();

            Assert.IsFalse(traversal.IsMeasured);
            Assert.IsFalse(traversal.TryClimbUp(out _), "no climbing clip: a straight climb at the body's speed");
            Assert.IsFalse(traversal.TryClimbDown(out _));
            Assert.IsFalse(traversal.TryJump(out JumpMotion jump), "no jumping clips…");
            Assert.AreEqual(0f, jump.TakeOff, "…no take-off time");
            StringAssert.Contains("not measured", traversal.Describe());
        }

        [Test]
        public void Traversal_Measured_TheClimbUp_TheWayDownOnlyWithItsClip_TheJump_OneLineSaysSo()
        {
            var traversal = new MeasuredTraversal
            {
                MeasuredFrom = "Controller",
                HasClimb = true,
                Climb = new ClimbMotion { Time = 2.36f, Height = 2.67f, Reach = 1.76f, FromWall = 1.27f },
                HasJump = true,
                Jump = new JumpMotion { TakeOff = 0.2f, Touch = 0.1f, Landing = 0.48f }
            };

            Assert.IsTrue(traversal.TryClimbUp(out ClimbMotion up));
            Assert.AreEqual(2.36f, up.Time);
            Assert.IsFalse(traversal.TryClimbDown(out _), "no climbing-down clip measured");
            traversal.HasClimbDown = true;
            Assert.IsTrue(traversal.TryClimbDown(out ClimbMotion down));
            Assert.AreEqual(up.Reach, down.Reach, "down is the way up played back");
            Assert.IsTrue(traversal.TryJump(out JumpMotion jump));
            Assert.AreEqual(0.48f, jump.Landing);
            Assert.AreEqual("measured from Controller: climb up 2.36 s · down ✓ · jump ✓", traversal.Describe());
        }

        [Test]
        public void ClimbMotion_ThePartDoneAtEachPointOfTheClip_ItsCurves_EvenlyWithNone()
        {
            var climb = new ClimbMotion
            {
                Rise = Linear((0f, 0f), (0.5f, 0.8f), (1f, 1f)),
                Advance = Linear((0f, 0f), (0.5f, 0.1f), (1f, 1f))
            };

            Assert.AreEqual(0.8f, climb.RiseAt(0.5f), 1e-4f, "up first…");
            Assert.AreEqual(0.1f, climb.AdvanceAt(0.5f), 1e-4f, "…forward at the end");
            Assert.AreEqual(1f, climb.RiseAt(2f), 1e-4f, "past the end: the end");

            var even = new ClimbMotion();
            Assert.AreEqual(0.3f, even.RiseAt(0.3f), 1e-4f);
            Assert.AreEqual(0.3f, even.AdvanceAt(0.3f), 1e-4f);
        }

        [Test]
        public void Ballistics_AStepOffAnEdge_FallsInFreeFallTime_AHopUpTakesLonger()
        {
            const float DROP = 2.67f;

            Assert.AreEqual(Mathf.Sqrt(2f * DROP / G), Ballistics.FallTime(DROP, 0f, G), 1e-4f, "from rest: √(2h/g)");
            Assert.Greater(Ballistics.FallTime(DROP, 1.5f, G), Ballistics.FallTime(DROP, 0f, G), "up first, then down");
            Assert.AreEqual(Ballistics.FallTime(DROP, 0f, G) / 2f, Ballistics.FallTime(DROP, 0f, 4f * G), 1e-4f,
                "four times the gravity: half the time");
            Assert.AreEqual(DROP - 0.5f * G, Ballistics.HeightAt(DROP, 0f, G, 1f), 1e-4f, "h − g t² / 2");
        }

        [Test]
        public void Ballistics_AnArcOverALink_RisesItsClearanceOverTheHigherEnd_LandsAtTheOther()
        {
            var from = new Vector3(0f, 0f, 0f);
            var to = new Vector3(4f, 1f, 0f);
            const float CLEARANCE = 0.5f;

            Ballistics.Arc(from, to, CLEARANCE, G, out float up, out float time);

            float apex = up * up / (2f * G);
            Assert.AreEqual(to.y + CLEARANCE, apex, 1e-3f, "the top of the arc: the clearance over the higher end");
            Assert.AreEqual(to.y, Ballistics.HeightAt(from.y, up, G, time), 1e-3f, "down on the far end at the end");
            Assert.Greater(time, 0f);
        }

        [Test]
        public void JumpLink_TheArcsClearance_FromTheLinksLength_AHopAtTheLeast()
        {
            Assert.AreEqual(NavMeshLinkJumper.MIN_CLEARANCE,
                NavMeshLinkJumper.ClearanceFor(Vector3.zero, new Vector3(1f, 0f, 0f)), 1e-4f, "a short hop: the least");
            Assert.AreEqual(NavMeshLinkJumper.CLEARANCE_PER_METRE * 6f,
                NavMeshLinkJumper.ClearanceFor(Vector3.zero, new Vector3(6f, 3f, 0f)), 1e-4f,
                "a long jump: its part of the length (over the ground, not the height)");
        }

        [Test]
        public void NavMeshBody_KeepsItsMeasuredClips_AsItsOwnData_ReadThroughTheCore()
        {
            LogAssert.ignoreFailingMessages = true;            // no baked mesh: the agent complains
            var go = new GameObject("Climber") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.SetActive(false);
                go.AddComponent<UnityEngine.AI.NavMeshAgent>();
                var character = go.AddComponent<AICharacter>();
                var body = new NavMeshBody { ClimbsWalls = true };
                character.Body = body;
                AgentBody asBody = character.Body;
                Assert.IsTrue(asBody.Traverses, "a climber: its clips are measured into it");
                Assert.IsFalse(new NavMeshBody().Traverses, "a body that neither climbs nor jumps needs none");

                var measured = new MeasuredTraversal { HasClimb = true, Climb = new ClimbMotion { Time = 1.5f } };
                asBody.Traversal = measured;                    // what the editor's Measure does
                character.Initialize();

                Assert.That(body.Motion, Is.SameAs(measured), "the climber and the jumper read the body's own data");
                Assert.IsTrue(body.Motion.TryClimbUp(out ClimbMotion climb));
                Assert.AreEqual(1.5f, climb.Time);
                asBody.Traversal = null;
                Assert.IsNotNull(body.Traversal, "never null: unmeasured");
            }
            finally
            {
                Object.DestroyImmediate(go);
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void NavMeshBody_TheOldTypedNumbers_GoIntoTheMeasuredData_OnceOnly()
        {
            var body = new NavMeshBody();
            var legacy = (ILegacyTraversal)body;
            Assert.IsFalse(legacy.TryTakeLegacyTraversal(out _), "a new body had no old numbers");

            // A scene saved before step 90 kept them under their old names (the serialized data of the body)
            var json = "{\"ClimbTime\":2.3555558,\"ClimbReach\":1.7603904,\"ClimbFromWall\":1.2703944," +
                       "\"JumpTakeOff\":0.20312501,\"JumpLanding\":0.48003477}";
            JsonUtility.FromJsonOverwrite(json, body);

            Assert.IsTrue(legacy.TryTakeLegacyTraversal(out MeasuredTraversal old));
            Assert.IsTrue(old.TryClimbUp(out ClimbMotion climb));
            Assert.AreEqual(2.3555558f, climb.Time, 1e-6f);
            Assert.AreEqual(1.7603904f, climb.Reach, 1e-6f);
            Assert.AreEqual(1.2703944f, climb.FromWall, 1e-6f);
            Assert.IsTrue(old.TryJump(out JumpMotion jump));
            Assert.AreEqual(0.48003477f, jump.Landing, 1e-6f);

            legacy.ClearLegacyTraversal();
            Assert.IsFalse(legacy.TryTakeLegacyTraversal(out _), "moved once");
        }
    }
}
