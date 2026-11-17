using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The character's actions as the AI Animator shows them (step 90 recheck; handbook §20.1–20.2: the body writes its
    /// state, the presentation reads it): a request it cannot show ends the action on screen (nothing is shown — the
    /// contract of ActionState.Begin), a gentle end plays the action's ending and counts as shown until the animator has
    /// left it, a long frame does not end an action the animator has not started yet, a missing locomotion state still
    /// lets the action go, a swing nobody consumed does not fire later, re-enabled it replays nothing, and asking whether
    /// it can show an action allocates nothing.
    /// </summary>
    public sealed class AnimationActionTests
    {
        private const float DT = 0.1f;
        private AnimationCoverageStand _stand;

        [SetUp]
        public void SetUp() => _stand = new AnimationCoverageStand();

        [TearDown]
        public void TearDown() => _stand.Destroy();

        // Idle (the default) and Dig — tagged Action, no motion, no exits: it holds until it is left
        private AnimationCoverageStand DigStand(bool attack = true, string locomotion = "Idle")
        {
            AnimatorController controller = _stand.Controller(attack);
            _stand.State(controller, "Dig");
            _stand.Make(controller);
            _stand.AI.States.Locomotion = locomotion;
            return _stand;
        }

        // Dig asked and shown (the animator in it)
        private void DigShown()
        {
            _stand.Actions.Begin("Dig");
            _stand.Frames(3, DT);
            Assert.IsTrue(_stand.IsIn("Dig"), "Set-up: the animator plays Dig");
            Assert.AreEqual("Dig", _stand.AI.CurrentAction);
        }

        [Test]
        public void AIAnimator_AnActionTheControllerLacks_EndsTheOneOnScreen_BackToLocomotion()
        {
            DigStand();
            DigShown();

            _stand.Actions.Begin("Juggle");
            _stand.Frames(4, DT);

            Assert.IsTrue(_stand.IsInOrEntering("Idle"), "Nothing to show for Juggle: the animator leaves Dig");
            Assert.IsNull(_stand.AI.CurrentAction);
            CollectionAssert.AreEqual(new[] { "Dig", "-" }, _stand.Changes);
            Assert.IsFalse(_stand.Actions.CanShow("Juggle"));
            Assert.IsFalse(_stand.Actions.IsShown, "Nothing is shown");
        }

        [Test]
        public void AIAnimator_EndAtOnceAndAnUnknownRequestInOneStep_TheOldActionStillEnds()
        {
            DigStand();
            DigShown();

            _stand.Actions.End(true);
            _stand.Actions.Begin("Juggle");                                 // the same think: only the last is read
            _stand.Frames(3, DT);

            Assert.IsTrue(_stand.IsIn("Idle"), "Dig dropped within its blend and a frame");
            Assert.IsNull(_stand.AI.CurrentAction);
        }

        [Test]
        public void AIAnimator_EndAtOnceAndASwingNothingConsumes_InOneStep_TheOldActionStillEnds()
        {
            DigStand();                                                     // an Attack trigger no transition takes
            DigShown();

            _stand.Actions.End(true);
            _stand.Actions.Swing();
            _stand.Frames(4, DT);

            Assert.IsTrue(_stand.IsIn("Idle"), "The swing leaves the dig for the walk, where a swing could start");
            Assert.IsNull(_stand.AI.CurrentAction, "Nothing tagged plays: the swing shown is over");
        }

        [Test]
        public void AIAnimator_AnActionTheControllerLacks_WithoutAnAttackTrigger_ASwingEndsTheOneOnScreen()
        {
            DigStand(attack: false);
            DigShown();

            _stand.Actions.Swing();
            _stand.Frames(3, DT);

            Assert.IsTrue(_stand.IsIn("Idle"));
            Assert.IsNull(_stand.AI.CurrentAction);
            CollectionAssert.AreEqual(new[] { "Dig", "-" }, _stand.Changes);
        }

        [Test]
        public void AIAnimator_AHitchFrame_DoesNotEndAnActionTheAnimatorHasNotStarted()
        {
            DigStand();

            _stand.Actions.Begin("Dig");
            _stand.AI.FrameTick(0.3f);                                     // below Time.maximumDeltaTime, above the grace

            Assert.AreEqual("Dig", _stand.AI.CurrentAction);
            Assert.IsTrue(_stand.Actions.IsShown);
            CollectionAssert.AreEqual(new[] { "Dig" }, _stand.Changes);

            _stand.Animator.Update(0.3f);
            _stand.AI.FrameTick(DT);
            Assert.AreEqual("Dig", _stand.AI.CurrentAction, "The animator plays it now");
        }

        [Test]
        public void AIAnimator_LocomotionStateMissing_AnEndStillLeavesTheAction()
        {
            DigStand(locomotion: "Locomotion");                            // the default name; the controller's is Idle
            DigShown();
            var warnings = new List<string>();
            void Listen(string message, string stack, LogType type) => warnings.Add(message);
            Application.logMessageReceived += Listen;
            try
            {
                _stand.Actions.End(true);
                _stand.Frames(4, DT);
            }
            finally
            {
                Application.logMessageReceived -= Listen;
            }

            Assert.IsTrue(_stand.IsIn("Idle"), "Back to the state it came from");
            Assert.IsNull(_stand.AI.CurrentAction);
            CollectionAssert.IsEmpty(warnings.FindAll(m => m.Contains("could not be found")), "No missing state asked for");
        }

        [Test]
        public void AIAnimator_ReenabledAfterASwingAndAHurtWhileOff_ReplaysNeither()
        {
            AnimatorController controller = _stand.Controller();
            controller.AddLayer("Hurt");
            controller.layers[1].stateMachine.AddState("Flinch");
            _stand.Make(controller);
            _stand.AI.States.HurtLayer = "Hurt";
            _stand.AI.States.Hurt = "Flinch";
            _stand.Actions.Swing();
            _stand.AI.FrameTick(DT);
            Assert.AreEqual(1, _stand.AI.Swings, "Set-up: one swing shown");

            _stand.AI.Detach();
            _stand.Actions.Swing();
            _stand.Character.Context.HurtAt = 1f;
            Assert.IsTrue(_stand.AI.Attach());
            _stand.AI.FrameTick(DT);

            Assert.AreEqual(1, _stand.AI.Swings, "What happened while it was off is over: no swing replayed");
            Assert.AreEqual(0, _stand.AI.Flinches, "…and no flinch");
            Assert.IsFalse(_stand.Animator.GetBool("Attack"), "No swing waits to fire");
        }

        [Test]
        public void AIAnimator_ANewRequestAfterASwing_ClearsTheAttackTrigger()
        {
            DigStand();
            _stand.Actions.Swing();
            _stand.AI.FrameTick(DT);
            Assert.IsTrue(_stand.Animator.GetBool("Attack"), "Set-up: the swing waits for a transition that never takes it");

            _stand.Actions.Begin("Dig");
            _stand.AI.FrameTick(DT);
            Assert.IsFalse(_stand.Animator.GetBool("Attack"), "Another action: the swing does not fire later on its own");

            _stand.Actions.Swing();
            _stand.AI.FrameTick(DT);
            _stand.Actions.End(true);
            _stand.AI.FrameTick(DT);
            Assert.IsFalse(_stand.Animator.GetBool("Attack"), "Ended: nor after the end");
        }

        [Test]
        public void AIAnimator_CanShow_AllocatesNothing()
        {
            DigStand();
            Assert.IsTrue(_stand.Actions.CanShow("Dig"));
            Assert.IsFalse(_stand.Actions.CanShow("Juggle"));               // each name asked once

            ActionState actions = _stand.Actions;
            Action ask = () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    actions.CanShow("Dig");
                    actions.CanShow("Juggle");
                }
            };
            ask();                                                          // compiled once (the runtime's own garbage)

            Assert.AreEqual(0, Allocations.Count(ask), "Asked inside the agent's step: no garbage");
        }

        // ---------- what the presentation shows and for how long ----------

        [Test]
        public void AIAnimator_GentleEnd_PlaysItsStopState_IsShownUntilTheAnimatorLeavesIt()
        {
            AnimatorController controller = _stand.Controller();
            _stand.State(controller, "Dig");
            AnimationCoverageStand.ExitsWhenOver(_stand.State(controller, "Dig Stop", seconds: 0.5f));
            _stand.Make(controller);
            _stand.AI.States.Locomotion = "Idle";
            DigShown();

            _stand.Actions.End(false);
            _stand.Frame(DT);
            Assert.IsTrue(_stand.IsInOrEntering("Dig Stop"), "The dig is put down: its ending plays");

            var shown = new List<bool>();
            for (int i = 0; i < 9; i++)
            {
                shown.Add(_stand.Actions.IsShown);                            // 0.1 … 0.9 s after the end
                _stand.Frame(DT);
            }

            Assert.IsTrue(shown[2], "0.3 s after: the ending is on screen");
            Assert.IsTrue(shown[4], "0.5 s after: still");
            Assert.IsFalse(_stand.Actions.IsShown, "By 0.9 s it is over");
            CollectionAssert.AreEqual(new[] { "Dig", "-" }, _stand.Changes);
        }

        [Test]
        public void AIAnimator_EndAtOnce_SkipsTheStopState_GoesToLocomotion()
        {
            AnimatorController controller = _stand.Controller();
            _stand.State(controller, "Dig");
            AnimationCoverageStand.ExitsWhenOver(_stand.State(controller, "Dig Stop", seconds: 0.5f));
            _stand.Make(controller);
            _stand.AI.States.Locomotion = "Idle";
            DigShown();

            _stand.Actions.End(true);
            bool stopped = false;
            for (int i = 0; i < 6; i++)
            {
                _stand.Frame(DT);
                stopped |= _stand.IsInOrEntering("Dig Stop");
            }

            Assert.IsFalse(stopped, "Dropped: no ending");
            Assert.IsTrue(_stand.IsIn("Idle"));
            Assert.IsFalse(_stand.Actions.IsShown);
        }

        [Test]
        public void AIAnimator_AnActionIsOverOnlyWhenTheAnimatorLeavesItsTaggedStates()
        {
            AnimatorController controller = _stand.Controller();
            AnimationCoverageStand.ExitsWhenOver(_stand.State(controller, "Wave", seconds: 0.6f));
            _stand.State(controller, "Wave2", action: false);
            _stand.Make(controller);
            _stand.AI.States.Locomotion = "Idle";

            _stand.Actions.Begin("Wave");
            float over = float.NaN, leftAt = float.NaN;
            for (float time = 0.05f; time <= 1.2f + 1e-4f; time += 0.05f)
            {
                _stand.Frame(0.05f);
                if (float.IsNaN(leftAt) && _stand.IsIn("Idle") && !_stand.Animator.IsInTransition(0) && time > 0.2f)
                    leftAt = time;
                if (float.IsNaN(over) && _stand.AI.CurrentAction == null)
                    over = time;
            }

            Assert.That(over, Is.GreaterThan(0.25f), "Never ended before the animator could show it");
            Assert.That(over, Is.GreaterThanOrEqualTo(0.6f), "Not before its clip is through");
            Assert.That(over - leftAt, Is.InRange(-1e-4f, 0.05f + 1e-4f), "Over within a frame of the animator leaving it");

            _stand.Changes.Clear();
            _stand.Actions.Begin("Wave2");                                  // a state that is not tagged
            _stand.AI.FrameTick(DT);
            _stand.Animator.Update(DT);
            _stand.AI.FrameTick(DT);
            Assert.AreEqual("Wave2", _stand.AI.CurrentAction, "Given the blend's time to begin");
            _stand.Animator.Update(DT);
            _stand.AI.FrameTick(DT);
            _stand.Animator.Update(DT);
            _stand.AI.FrameTick(DT);
            Assert.IsNull(_stand.AI.CurrentAction, "Then over: nothing tagged plays");
        }

        [Test]
        public void AIAnimator_IsShowing_AnUnreadRequestIs_AReplacedOneIsNot()
        {
            DigStand();
            _stand.Actions.Begin("Dig");
            int first = _stand.Actions.Serial;
            _stand.AI.FrameTick(DT);
            _stand.Actions.Begin("Dig");
            int second = _stand.Actions.Serial;

            Assert.IsTrue(_stand.AI.IsShowing(first), "Read and on screen");
            Assert.IsTrue(_stand.AI.IsShowing(second), "Not read yet: counts as on screen");

            _stand.AI.FrameTick(DT);
            Assert.IsFalse(_stand.AI.IsShowing(first), "Replaced by a newer request");
            Assert.IsTrue(_stand.AI.IsShowing(second));
            Assert.IsTrue(_stand.AI.IsShowing(second + 1), "A serial not read yet");
        }

        [Test]
        public void AIAnimator_TwoRequestsInOneFrame_OnlyTheLastIsShown()
        {
            AnimatorController controller = _stand.Controller();
            _stand.State(controller, "Dig");
            _stand.State(controller, "Wave");
            _stand.Make(controller);

            _stand.Actions.Begin("Dig");
            _stand.Actions.Begin("Wave");
            _stand.AI.FrameTick(DT);

            Assert.AreEqual("Wave", _stand.AI.CurrentAction);
            CollectionAssert.AreEqual(new[] { "Wave" }, _stand.Changes, "Raised once");
        }

        [Test]
        public void AIAnimator_ABeginState_IsEnteredFirst_ItCountsAsTheAction()
        {
            AnimatorController controller = _stand.Controller();
            AnimatorState begin = _stand.State(controller, "Dig Begin", seconds: 0.5f);
            AnimatorState dig = _stand.State(controller, "Dig");
            AnimatorStateTransition on = begin.AddTransition(dig);
            on.hasExitTime = true;
            on.exitTime = 1f;
            _stand.Make(controller);
            _stand.AI.States.Locomotion = "Idle";

            Assert.IsTrue(_stand.AI.Has("Dig"));
            _stand.Actions.Begin("Dig");
            _stand.Frame(DT);
            Assert.IsTrue(_stand.IsInOrEntering("Dig Begin"), "Its beginning first");
            Assert.AreEqual("Dig", _stand.AI.CurrentAction);

            _stand.Frames(8, DT);
            _stand.Actions.End(false);                                      // no "Dig Stop": back to the walk
            _stand.Frames(3, DT);
            Assert.IsTrue(_stand.IsIn("Idle"), "There is no Dig Stop (nor Dig Begin Stop): the walk again");

            var only = new AnimationCoverageStand();
            try
            {
                AnimatorController lone = only.Controller();
                only.State(lone, "Dig Begin");
                only.Make(lone);
                Assert.IsTrue(only.AI.Has("Dig"), "A beginning alone is the action too");
            }
            finally
            {
                only.Destroy();
            }
        }

        [Test]
        public void AIAnimator_Swing_WithoutAnAttackTrigger_ShowsNothing()
        {
            DigStand(attack: false);
            _stand.Actions.Swing();
            _stand.AI.FrameTick(DT);

            Assert.AreEqual(1, _stand.AI.Swings);
            Assert.IsNull(_stand.AI.CurrentAction);
            Assert.IsFalse(_stand.Actions.IsShown);
            CollectionAssert.IsEmpty(_stand.Changes);
        }
    }
}
