using System.Collections.Generic;
using AIKit.Mecanim;
using AIKit.Mecanim.Editor;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// Step 90's migration of the picture (step 90 recheck): what the clips cannot give is kept from the body's old
    /// numbers — the jump when only the climb could be measured, everything when nothing could — and the old numbers are
    /// emptied after; every old setting lands on its new owner; an old Animator outside the character is not given an AI
    /// Animator that can never find its character.
    /// </summary>
    public sealed class AnimationMigrationCoverageTests
    {
        private const float CLIMB_TIME = 2.3555558f, CLIMB_REACH = 1.7603904f, CLIMB_FROM_WALL = 1.2703944f;
        private const float TAKE_OFF = 0.20312501f, LANDING = 0.48003477f;

        private AnimationCoverageStand _stand;
        private readonly List<GameObject> _made = new();

        [SetUp]
        public void SetUp()
        {
            _stand = new AnimationCoverageStand();
            LogAssert.ignoreFailingMessages = true;                         // a NavMeshAgent without a baked mesh complains
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
            _stand.Destroy();
            LogAssert.ignoreFailingMessages = false;
        }

        // A character saved before step 90: a NavMesh body that climbs and jumps, with the numbers the lab measured then
        private AICharacter OldClimber(RuntimeAnimatorController controller)
        {
            var root = new GameObject("Old Climber") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(root);
            root.SetActive(false);
            root.AddComponent<NavMeshAgent>();
            var character = root.AddComponent<AICharacter>();
            character.Body = new NavMeshBody { ClimbsWalls = true, JumpsOverLinks = true };
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<Animator>().runtimeAnimatorController = controller;
            model.AddComponent<AIAnimator>();

            var data = new SerializedObject(character);
            data.FindProperty("_body.ClimbTime").floatValue = CLIMB_TIME;
            data.FindProperty("_body.ClimbReach").floatValue = CLIMB_REACH;
            data.FindProperty("_body.ClimbFromWall").floatValue = CLIMB_FROM_WALL;
            data.FindProperty("_body.JumpTakeOff").floatValue = TAKE_OFF;
            data.FindProperty("_body.JumpLanding").floatValue = LANDING;
            data.ApplyModifiedPropertiesWithoutUndo();
            return character;
        }

        private AnimatorController ControllerWith(AnimationClip climb, string name)
        {
            var controller = _stand.Keep(new AnimatorController { name = name });
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle");
            if (climb != null)
                controller.layers[0].stateMachine.AddState("Climb Up").motion = climb;
            return controller;
        }

        [Test]
        public void AnimationMigration_OnlyTheClimbMeasured_KeepsTheOldJumpNumbers()
        {
            AnimationClip climb = _stand.ClimbClip("Climb", 2.5f, 1.2f, 2f, 2.5f);
            AICharacter character = OldClimber(ControllerWith(climb, "Climb Only"));
            var log = new List<string>();

            Assert.IsTrue(AnimationMigration.Migrate(character, log));

            MeasuredTraversal now = character.Body.Traversal;
            Assert.IsTrue(now.HasClimb, string.Join("\n", log));
            Assert.AreEqual(2.5f, now.Climb.Height, 0.02f, "The climb from its clip");
            Assert.AreEqual(2.5f * 77f / TraversalMeasure.SAMPLES, now.Climb.Time, 0.03f);
            Assert.IsTrue(now.HasJump, "No jumping clips: the jump the body had is kept");
            Assert.AreEqual(TAKE_OFF, now.Jump.TakeOff, 1e-6f);
            Assert.AreEqual(LANDING, now.Jump.Landing, 1e-6f);
            Assert.IsFalse(((ILegacyTraversal)character.Body).TryTakeLegacyTraversal(out _), "The old numbers are emptied");
        }

        [Test]
        public void AnimationMigration_NoClipsToMeasure_KeepsTheBodysOldNumbers_ThenEmptiesThem()
        {
            AICharacter character = OldClimber(ControllerWith(null, "No Clips"));
            var log = new List<string>();

            Assert.IsTrue(AnimationMigration.Migrate(character, log));

            MeasuredTraversal now = character.Body.Traversal;
            Assert.AreEqual("the body's numbers before step 90", now.MeasuredFrom);
            Assert.AreEqual(CLIMB_TIME, now.Climb.Time, 1e-6f);
            Assert.AreEqual(CLIMB_REACH, now.Climb.Reach, 1e-6f);
            Assert.AreEqual(CLIMB_FROM_WALL, now.Climb.FromWall, 1e-6f);
            Assert.AreEqual(TAKE_OFF, now.Jump.TakeOff, 1e-6f);
            Assert.AreEqual(LANDING, now.Jump.Landing, 1e-6f);
            Assert.IsFalse(((ILegacyTraversal)character.Body).TryTakeLegacyTraversal(out _), "Taken once: emptied");
            Assert.That(string.Join("\n", log), Does.Contain("kept from the body's old numbers"));
        }

        [Test]
        public void AnimationMigration_CopySettings_EveryOldFieldLandsOnItsNewOwner()
        {
            var old = new LegacyAnimationSettings
            {
                Speed = "S", MoveRate = "MR", MoveX = "X", MoveZ = "Z", Stride = "St", Backing = "B", Attack = "A",
                Combat = "C", Braking = "Br", Weapon = "W", Climb = "Cl", Jump = "J", Locomotion = "Loco", ActionTag = "Act",
                Blend = 0.33f, ClimbUpState = "CU", ClimbDownState = "CD", ClipSpeed = 2.2f, SlowestRate = 0.6f,
                Smoothing = 0.07f, CalmAfter = 3f, BrakingAt = 0.9f
            };
            var model = new GameObject("Model") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(model);
            model.AddComponent<Animator>();
            var shown = model.AddComponent<AIAnimator>();

            AnimationMigration.CopySettings(old, shown);

            AnimatorParameters p = shown.Parameters;
            CollectionAssert.AreEqual(new[] { "S", "MR", "X", "Z", "St", "B", "A", "C", "Br", "W", "Cl", "J" },
                new[] { p.Speed, p.MoveRate, p.MoveX, p.MoveZ, p.Stride, p.Backing, p.Attack, p.Combat, p.Braking, p.Weapon, p.Climb, p.Jump });
            AnimatorStates s = shown.States;
            Assert.AreEqual("Loco", s.Locomotion);
            Assert.AreEqual("Act", s.ActionTag);
            Assert.AreEqual(0.33f, s.Blend);
            Assert.AreEqual("CU", s.ClimbUp);
            Assert.AreEqual("CD", s.ClimbDown);
            AnimatorTuning t = shown.Tuning;
            Assert.AreEqual(2.2f, t.ClipSpeed);
            Assert.AreEqual(0.6f, t.SlowestRate);
            Assert.AreEqual(0.07f, t.Smoothing);
            Assert.AreEqual(3f, t.CalmAfter);
            Assert.AreEqual(0.9f, t.BrakingAt);
            Assert.AreEqual("Jump Start", s.JumpStart, "What the old section had no field for keeps its default");
            Assert.AreEqual("Jump Land", s.JumpLand);
            Assert.AreEqual("", s.Hurt);
            Assert.AreEqual("", s.HurtLayer);
        }

        [Test]
        public void AnimationMigration_TheOldAnimatorOutsideTheCharacter_IsReportedNotSilentlyAttached()
        {
            var root = new GameObject("Villager") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(root);
            var elsewhere = new GameObject("Somebody Else's Model") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(elsewhere);
            Animator outside = elsewhere.AddComponent<Animator>();
            var character = root.AddComponent<AICharacter>();
            var data = new SerializedObject(character);
            data.FindProperty("_animated").boolValue = true;
            data.FindProperty("_animation.Animator").objectReferenceValue = outside;
            data.ApplyModifiedPropertiesWithoutUndo();
            var log = new List<string>();

            AnimationMigration.Migrate(character, log);

            var put = outside.GetComponent<AIAnimator>();
            string said = string.Join("\n", log);
            Assert.IsNull(put, "No AI Animator where it can never find its character");
            Assert.That(said, Does.Contain("outside"), "The log says why nothing shows the character");
            Assert.That(said, Does.Not.Contain($"AI Animator on {outside.name}"));
        }
    }
}
