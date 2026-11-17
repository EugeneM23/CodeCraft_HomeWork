using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using AIKit.Mecanim;
using AIKit.Mecanim.Editor;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// Measuring a character's climbing clip in the editor (step 90 recheck; handbook §20.3 — the numbers of a move come
    /// from its clip): the climb's time leaves the standing tail out, a clip that does not climb is no climb, the
    /// fingerprint follows only the measured states' clips and reads the same in every culture, a character variant
    /// (an override controller) is measured by the clip it plays, a model of another scale is measured anew, a cached
    /// result is a copy, nothing is written when there is nothing to measure, and a body with no clips to measure is not
    /// told to measure again.
    /// </summary>
    public sealed class AnimationMeasureTests
    {
        private const float HEIGHT = 2.5f, REACH = 1.2f, CLIMB = 2f, LENGTH = 2.5f;
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

        private GameObject Model(string name = "Model")
        {
            var model = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(model);
            model.AddComponent<Animator>();
            return model;
        }

        // A controller of an Idle and a Climb Up of this clip
        private AnimatorController ClimbController(AnimationClip climb, string name = "Climber")
        {
            var controller = _stand.Keep(new AnimatorController { name = name });
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle").motion = _stand.Clip("Idle", 1f);
            controller.layers[0].stateMachine.AddState("Climb Up").motion = climb;
            return controller;
        }

        // A model with an Animator of this controller and an AI Animator on it
        private AIAnimator Shown(RuntimeAnimatorController controller, Vector3? scale = null)
        {
            GameObject model = Model();
            model.transform.localScale = scale ?? Vector3.one;
            model.GetComponent<Animator>().runtimeAnimatorController = controller;
            return model.AddComponent<AIAnimator>();
        }

        // A character whose NavMesh body climbs (or jumps), its model below it shown by an AI Animator of this controller
        private AICharacter Climber(RuntimeAnimatorController controller, bool climbs = true, bool jumps = false)
        {
            var root = new GameObject("Climber") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(root);
            root.SetActive(false);
            root.AddComponent<NavMeshAgent>();
            var character = root.AddComponent<AICharacter>();
            character.Body = new NavMeshBody { ClimbsWalls = climbs, JumpsOverLinks = jumps };
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<Animator>().runtimeAnimatorController = controller;
            model.AddComponent<AIAnimator>();
            return character;
        }

        [Test]
        public void TraversalMeasure_MeasureClimb_AClipThatRisesThenStands_TimeWithoutItsTail()
        {
            AnimationClip clip = _stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH);
            var report = new List<string>();

            Assert.IsTrue(TraversalMeasure.MeasureClimb(Model(), null, clip, out ClimbMotion climb, report), string.Join("; ", report));

            Assert.AreEqual(LENGTH * 77f / TraversalMeasure.SAMPLES, climb.Time, 0.03f, "Up to where the body stops: the tail left out");
            Assert.AreEqual(HEIGHT, climb.Height, 0.01f);
            Assert.AreEqual(REACH, climb.Reach, 0.01f);
            Assert.AreEqual(0f, climb.FromWall, 1e-4f, "No human bones: nothing measured off the wall");
            Assert.AreEqual(0f, climb.RiseAt(0f), 1e-4f);
            Assert.AreEqual(1f, climb.RiseAt(1f), 1e-4f);
            Assert.AreEqual(0.5f, climb.RiseAt(0.5f), 0.02f, "Evenly up");
            Assert.AreEqual(0.5f, climb.AdvanceAt(0.5f), 0.02f, "Evenly forward");
        }

        [Test]
        public void TraversalMeasure_MeasureClimb_TooLowOrNotForward_IsNoClimb()
        {
            var report = new List<string>();
            Assert.IsFalse(TraversalMeasure.MeasureClimb(Model(), null, _stand.ClimbClip("Low", 0.4f, REACH, CLIMB, LENGTH),
                out _, report));
            Assert.That(string.Join("; ", report), Does.Contain("not a climb onto a wall"));

            report.Clear();
            Assert.IsFalse(TraversalMeasure.MeasureClimb(Model(), null, _stand.ClimbClip("Straight Up", HEIGHT, 0.02f, CLIMB, LENGTH),
                out _, report));
            Assert.That(string.Join("; ", report), Does.Contain("not a climb onto a wall"));
        }

        [Test]
        public void TraversalMeasure_Fingerprint_IsTheSameInEveryCulture()
        {
            AIAnimator animator = Shown(ClimbController(_stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH)));
            CultureInfo was = Thread.CurrentThread.CurrentCulture;
            string russian, invariant;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
                russian = TraversalMeasure.Fingerprint(animator);
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                invariant = TraversalMeasure.Fingerprint(animator);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = was;
            }

            Assert.AreEqual(invariant, russian, "A comma or a dot: the same clips are the same clips");
        }

        [Test]
        public void TraversalMeasure_AStoredFingerprintOfAnotherCulture_IsNotStale()
        {
            // Measured before on a machine with a comma (the fingerprints the scenes keep from before the fix)
            AnimationClip clip = _stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH);
            AICharacter character = Climber(ClimbController(clip));
            Assert.IsNotNull(TraversalMeasure.MeasureInto(character));
            Assert.IsTrue(character.Body.Traversal.IsMeasured, "Set-up: measured");
            character.Body.Traversal.Fingerprint = character.Body.Traversal.Fingerprint.Replace("2.500", "2,500");

            Assert.IsFalse(TraversalMeasure.IsStale(character), "The same clips, written with a comma: not changed");
        }

        [Test]
        public void TraversalMeasure_AnOverrideControllerOnTheAnimator_MeasuresTheClipItPlays()
        {
            AnimationClip a = _stand.ClimbClip("Climb A", HEIGHT, REACH, CLIMB, LENGTH);
            AnimationClip b = _stand.ClimbClip("Climb B", 3f, 1f, CLIMB, LENGTH);
            AnimatorController controller = ClimbController(a, "Base Climber");
            var variant = _stand.Keep(new AnimatorOverrideController(controller) { name = "Variant", [a] = b });
            AIAnimator animator = Shown(variant);

            Assert.IsTrue(TraversalMeasure.Measure(animator, out MeasuredTraversal measured, null));
            Assert.AreEqual(3f, measured.Climb.Height, 0.02f, "The variant's clip climbs 3 m");
            Assert.AreEqual(1f, measured.Climb.Reach, 0.02f);
            Assert.That(TraversalMeasure.Fingerprint(animator), Does.Contain("Climb B"), "The fingerprint names the clip played");
        }

        [Test]
        public void TraversalMeasure_AModelOfAnotherScale_IsMeasuredAnew_NotTakenFromTheCache()
        {
            AnimatorController controller = ClimbController(_stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH), "Scaled Climber");
            var report = new List<string>();
            Assert.IsTrue(TraversalMeasure.Measure(Shown(controller), out _, report));

            report.Clear();
            Assert.IsTrue(TraversalMeasure.Measure(Shown(controller, Vector3.one * 1.2f), out _, report));

            Assert.That(string.Join("; ", report), Does.Not.Contain("as measured before"),
                "A bigger model's bones are elsewhere: measured, not the first one's numbers");
        }

        [Test]
        public void TraversalMeasure_Measure_ACachedResultIsACopy()
        {
            AIAnimator animator = Shown(ClimbController(_stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH), "Cached Climber"));
            Assert.IsTrue(TraversalMeasure.Measure(animator, out MeasuredTraversal first, null));
            float time = first.Climb.Time;
            first.Climb.Time = 99f;
            Keyframe key = first.Climb.Rise.keys[1];
            key.value = 0.9f;
            first.Climb.Rise.MoveKey(1, key);

            var report = new List<string>();
            Assert.IsTrue(TraversalMeasure.Measure(animator, out MeasuredTraversal second, report));

            Assert.That(string.Join("; ", report), Does.Contain("as measured before"), "Set-up: from the cache");
            Assert.AreEqual(time, second.Climb.Time, 1e-5f, "The first result's change did not reach the cache");
            Assert.AreNotSame(first.Climb.Rise, second.Climb.Rise);
            Assert.AreNotEqual(0.9f, second.Climb.Rise.keys[1].value);
        }

        [Test]
        public void TraversalMeasure_Fingerprint_FollowsTheMeasuredStatesClips_IsStaleOnlyThen()
        {
            AnimationClip climb = _stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH);
            AnimatorController controller = ClimbController(climb, "Followed Climber");
            AICharacter character = Climber(controller);

            Assert.That(TraversalMeasure.MeasureInto(character), Does.StartWith("measured"));
            Assert.IsFalse(TraversalMeasure.IsStale(character), "Just measured");
            Assert.AreEqual(controller.name, character.Body.Traversal.MeasuredFrom);

            controller.layers[0].stateMachine.states[0].state.motion = _stand.Clip("Other Idle", 1.5f);
            Assert.IsFalse(TraversalMeasure.IsStale(character), "Another idle: the climb is the same");

            controller.layers[0].stateMachine.states[1].state.motion = _stand.ClimbClip("Shorter", HEIGHT, REACH, 1.5f, 2f);
            Assert.IsTrue(TraversalMeasure.IsStale(character), "Another climbing clip: measure again");
        }

        [Test]
        public void TraversalMeasure_MeasureInto_NothingToMeasureWith_BodyUntouched()
        {
            AICharacter walker = Climber(ClimbController(_stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH)), climbs: false);
            Assert.IsNull(TraversalMeasure.MeasureInto(walker), "A body that does not climb or jump");

            AICharacter noAnimator = Climber(null);
            Object.DestroyImmediate(noAnimator.GetComponentInChildren<AIAnimator>(true));
            Assert.IsNull(TraversalMeasure.MeasureInto(noAnimator), "No AI Animator");

            AICharacter noController = Climber(null);
            Assert.That(TraversalMeasure.MeasureInto(noController), Is.EqualTo("nothing measured: no animator controller to measure"));
            Assert.IsFalse(noController.Body.Traversal.IsMeasured);

            var plain = _stand.Keep(new AnimatorController { name = "Plain" });
            plain.AddLayer("Base");
            plain.layers[0].stateMachine.AddState("Idle");
            AICharacter noClips = Climber(plain);
            Assert.That(TraversalMeasure.MeasureInto(noClips), Is.EqualTo(
                "nothing measured: no climbing clip (state 'Climb Up'); no jumping clips (states 'Jump Start', 'Jump Land')"));
            Assert.IsFalse(noClips.Body.Traversal.IsMeasured, "Nothing written");
        }

        [Test]
        public void AnimationEditorHook_ATraversingBodyWithNoClips_IsNotToldToMeasureAgain()
        {
            var hook = new AnimationEditorHook();
            var plain = _stand.Keep(new AnimatorController { name = "Plain" });
            plain.AddLayer("Base");
            plain.layers[0].stateMachine.AddState("Idle");
            AICharacter noClips = Climber(plain, climbs: false, jumps: true);
            TraversalMeasure.MeasureInto(noClips);

            string problem = hook.Problem(noClips);
            Assert.That(problem ?? "", Does.Not.Contain("Measure again"),
                "No clips to measure: the plain way is valid, measuring again would change nothing");

            AICharacter withClips = Climber(ClimbController(_stand.ClimbClip("Climb", HEIGHT, REACH, CLIMB, LENGTH), "Hooked Climber"));
            Assert.That(hook.Problem(withClips), Does.Contain("Measure again"), "Clips never measured: say so");
            TraversalMeasure.MeasureInto(withClips);
            Assert.IsNull(hook.Problem(withClips), "Measured: no problem");
        }
    }
}
