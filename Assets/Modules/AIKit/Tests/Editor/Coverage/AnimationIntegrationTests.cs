using System.Collections.Generic;
using AIKit.Mecanim;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Nodes that show actions, on a real AI Animator (step 90 recheck; handbook §20.1 — the node asks the character, the
    /// presentation shows it): a charge whose controller has a gathering clip but no rushing one does not keep the
    /// gathering pose through the rush and after it; a Perform that lasts until its animation ends lasts its clip. The
    /// character's action state asks its view only what it declares.
    /// </summary>
    public sealed class AnimationIntegrationTests
    {
        private const float DT = 0.05f;
        private readonly List<Object> _made = new();
        private GraphRig _rig;
        private Animator _animator;
        private AIAnimator _ai;
        private readonly List<string> _changes = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
            _changes.Clear();
            GraphRig.DestroyAll();
        }

        // A rig's character shown by an AI Animator of this controller on a model under it
        private void Shown(AnimatorController controller)
        {
            _rig = new GraphRig(trace: false);
            _rig.Agent.gameObject.SetActive(true);                          // an Animator on a sleeping object does not step
            var model = new GameObject("Model");
            model.transform.SetParent(_rig.Agent.transform, false);
            _animator = model.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.Rebind();
            _animator.Update(0f);
            _ai = model.AddComponent<AIAnimator>();
            _ai.States.Locomotion = "Idle";
            _ai.ActionChanged += action => _changes.Add(action ?? "-");
            Assert.IsTrue(_ai.Attach(_rig.Agent));
        }

        private AnimatorController Controller()
        {
            var controller = new AnimatorController();
            _made.Add(controller);
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle");
            return controller;
        }

        private AnimationClip Clip(string name, float seconds)
        {
            var clip = new AnimationClip { name = name };
            _made.Add(clip);
            UnityEditor.AnimationUtility.SetEditorCurve(clip,
                UnityEditor.EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.x"), AnimationCurve.Constant(0f, seconds, 1f));
            return clip;
        }

        // One step of the character and one frame of its picture
        private TaskStatus Step(AgentTask task)
        {
            _rig.Context.Advance(DT);
            TaskStatus status = task.Tick(_rig.Context, DT);
            _rig.Actuator.Apply(DT);
            _rig.Agent.transform.position = _rig.Actuator.Position;
            _ai.FrameTick(DT);
            _animator.Update(DT);
            return status;
        }

        [Test]
        public void ChargeTask_AGatherClipButNoRushClip_TheGatherPoseDoesNotOutliveTheCharge()
        {
            AnimatorController controller = Controller();
            controller.layers[0].stateMachine.AddState("Charge Up").tag = "Action";     // it loops: it holds until left
            Shown(controller);
            var foe = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(foe);
            foe.transform.position = new Vector3(0f, 0f, 20f);
            _rig.Context.BrainTarget = foe.transform;
            var charge = new ChargeTask();

            TaskStatus status = TaskStatus.Running;
            float time = 0f;
            bool gathered = false;
            for (int i = 0; i < 200 && status == TaskStatus.Running; i++)
            {
                status = Step(charge);
                time += DT;
                gathered |= _animator.GetCurrentAnimatorStateInfo(0).IsName("Charge Up");
            }

            float expected = charge.ChargeTime.Get(null) + charge.Distance.Get(null) / charge.Speed.Get(null) + charge.Recovery.Get(null);
            Assert.AreEqual(TaskStatus.Success, status);
            Assert.IsTrue(gathered, "Set-up: the gathering was shown");
            Assert.AreEqual(expected, time, 0.2f, "Gather, rush, recover");
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("Charge Up"),
                "The rush has no clip of its own: the gathering pose ended with it, not after the charge");
            Assert.IsNull(_ai.CurrentAction);
            Assert.AreEqual("-", _changes[^1], "Nothing is shown at the end");
        }

        [Test]
        public void PerformTask_UntilTheAnimationEnds_WithTheAIAnimator_LastsTheClip()
        {
            AnimatorController controller = Controller();
            AnimatorState wave = controller.layers[0].stateMachine.AddState("Wave");
            wave.tag = "Action";
            wave.motion = Clip("Wave", 1f);
            AnimatorStateTransition back = wave.AddExitTransition();
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0f;
            Shown(controller);
            var perform = new PerformTask { Animation = "Wave", Duration = 0f };

            TaskStatus status = TaskStatus.Running;
            float time = 0f;
            for (int i = 0; i < 100 && status == TaskStatus.Running; i++)
            {
                status = Step(perform);
                time += DT;
            }

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.That(time, Is.InRange(1.1f, 1.45f), "As long as its clip (and the blend, a frame, the end read) — not 0.3 s");
        }

        /// <summary>A view that says what it was asked.</summary>
        private sealed class AskedView : IActionView
        {
            public readonly List<string> Has = new();
            public readonly List<int> Showing = new();

            bool IActionView.Has(string action)
            {
                Has.Add(action);
                return true;
            }

            public bool IsShowing(int serial)
            {
                Showing.Add(serial);
                return true;
            }
        }

        [Test]
        public void ActionState_WithoutAView_CanShowAndIsShownAreFalse_WithOne_TheyAskIt()
        {
            var actions = new ActionState();
            Assert.IsFalse(actions.CanShow("Dig"), "Nothing shows the character's actions");
            Assert.IsFalse(actions.IsShown);

            var view = new AskedView();
            actions.View = view;
            actions.Begin("Dig");
            actions.Begin("Dig");

            Assert.IsTrue(actions.IsShown);
            CollectionAssert.AreEqual(new[] { actions.Serial }, view.Showing, "Asked about the request of now");
            Assert.IsFalse(actions.CanShow(""), "No name: nothing to ask");
            CollectionAssert.IsEmpty(view.Has);
            Assert.IsTrue(actions.CanShow("Dig"));
            CollectionAssert.AreEqual(new[] { "Dig" }, view.Has);
        }
    }
}
