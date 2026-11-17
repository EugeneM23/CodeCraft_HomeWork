using System.Collections.Generic;
using AIKit.Mecanim;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The AI Animator on a real Animator (step 90, C22: the animation is a component of its own that knows the model and
    /// reads it — nothing is pushed into it; handbook §20.1–20.2): every frame it pulls the character's state — the brain
    /// has a target (the combat stance, kept a while after), the weapon in the hand and the clip set to show its moves
    /// with, the body's climb and jump, the actions the character asks for, the hurt — and writes the controller. The
    /// walk is measured from the body, root motion goes only to a body that takes it. Ported from the old
    /// CharacterAnimationTests, which pushed the same facts in by hand.
    /// </summary>
    public sealed class AIAnimatorTests
    {
        private const float DT = 0.1f;

        private readonly List<Object> _made = new();
        private GameObject _owner;
        private AICharacter _character;
        private Animator _animator;
        private AIAnimator _ai;
        private PictureBody _body;

        [SetUp]
        public void SetUp() => Make(new PictureBody(), Controller());

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
        }

        // A character with this body, and below it a model with an Animator of this controller and an AI Animator
        private void Make(AgentBody body, RuntimeAnimatorController controller, float calmAfter = 1f)
        {
            if (_owner != null)
                Object.DestroyImmediate(_owner);

            // Active (an Edit Mode test runs no Awake or Update): the Animator is bound to its controller by hand
            _owner = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(_owner);
            _character = _owner.AddComponent<AICharacter>();
            _character.Simulation = AICharacter.SimulationMode.Manual;
            _character.Body = body;
            _body = body as PictureBody;
            _character.Initialize();
            var model = new GameObject("Body");
            model.transform.SetParent(_owner.transform, false);
            _animator = model.AddComponent<Animator>();
            _animator.runtimeAnimatorController = controller;
            _animator.Rebind();
            _animator.Update(0f);
            _ai = model.AddComponent<AIAnimator>();
            _ai.Tuning.CalmAfter = calmAfter;
            Assert.IsTrue(_ai.Attach(), "the AI Animator finds its character above it");
        }

        private AnimatorController Controller(params string[] actionStates)
        {
            var controller = new AnimatorController();
            _made.Add(controller);
            controller.AddLayer("Base");
            foreach (string parameter in new[] { "MoveRate", "MoveX", "MoveZ", "Stride", "Backing", "Speed", "Climb" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            foreach (string parameter in new[] { "Combat", "Braking" })
                controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter("Weapon", AnimatorControllerParameterType.Int);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Int);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            machine.AddState("Idle");
            foreach (string state in actionStates)
                machine.AddState(state).tag = "Action";
            return controller;
        }

        private bool Shown => _animator.GetBool("Combat");

        // ---------- the clip set of the weapon ----------

        [Test]
        public void Motions_AWeaponsSet_PutsItsClipsOnTheSameStates_NoneGivesTheControllersOwnBack()
        {
            var idle = new AnimationClip { name = "Sword Idle" };
            var swing = new AnimationClip { name = "Sword Swing" };
            var bowIdle = new AnimationClip { name = "Bow Idle" };
            _made.AddRange(new Object[] { idle, swing, bowIdle });
            var controller = new AnimatorController();
            _made.Add(controller);
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle").motion = idle;
            controller.layers[0].stateMachine.AddState("Attack 1").motion = swing;
            _animator.runtimeAnimatorController = controller;
            var bow = new AnimatorOverrideController(controller) { [idle] = bowIdle };
            _made.Add(bow);

            _ai.ShowMotions(bow);

            var shown = _animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.That(shown, Is.Not.Null, "the character plays its own override of the controller");
            Assert.That(shown.runtimeAnimatorController, Is.SameAs(controller), "one controller for every weapon");
            Assert.That(shown[idle], Is.SameAs(bowIdle), "the bow's clip on the same state");
            Assert.That(shown[swing], Is.SameAs(swing), "a clip the set leaves alone is the controller's own");
            Assert.That(_ai.ShownMotions, Is.SameAs(bow));

            _ai.ShowMotions(null);
            Assert.That(_animator.runtimeAnimatorController, Is.SameAs(shown), "the override stays, its clips change");
            Assert.That(shown[idle], Is.SameAs(idle));
        }

        [Test]
        public void Motions_TheCharacterAsksForTheNewWeaponsClipsAndItsDraw_InOneStep_TheDrawIsShownWithThem()
        {
            // Switch Weapon asks for the new weapon's clips and its draw in the same step (the old risk of step 86: a
            // set swapped a frame late showed the draw with the old weapon's clips)
            var draw = new AnimationClip { name = "Sword Draw" };
            var bowDraw = new AnimationClip { name = "Bow Draw" };
            _made.AddRange(new Object[] { draw, bowDraw });
            AnimatorController controller = Controller();
            controller.layers[0].stateMachine.AddState("Weapon Draw").motion = draw;
            controller.layers[0].stateMachine.states[1].state.tag = "Action";
            Make(new PictureBody(), controller);
            var bowSet = new AnimatorOverrideController(controller) { [draw] = bowDraw };
            _made.Add(bowSet);
            var bow = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" });
            bow.Motions = bowSet;
            bow.DrawAction = "Weapon Draw";
            _made.Add(bow);
            _ai.FrameTick(DT);

            _character.ShowMotionsOf(bow);
            _character.Context.Actions.Begin(bow.DrawAction);
            _ai.FrameTick(DT);

            Assert.That(_ai.CurrentAction, Is.EqualTo("Weapon Draw"), "the draw is shown");
            Assert.That(_ai.MotionsAtLastAction, Is.SameAs(bowSet), "…with the new weapon's clips already in place");
            Assert.That(((AnimatorOverrideController)_animator.runtimeAnimatorController)[draw], Is.SameAs(bowDraw));
        }

        [Test]
        public void Weapon_TheOneInTheHandIsPulled_ItsPlaceInTheListIsShown()
        {
            var sword = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" });
            var bow = WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" });
            _made.Add(sword);
            _made.Add(bow);
            _character.Weapons.Add(new CharacterWeapon { Weapon = sword });
            _character.Weapons.Add(new CharacterWeapon { Weapon = bow });

            _character.Weapon = bow;
            _ai.FrameTick(DT);
            Assert.That(_animator.GetInteger("Weapon"), Is.EqualTo(1), "the bow: second in the list");

            _character.Weapon = sword;
            _ai.FrameTick(DT);
            Assert.That(_animator.GetInteger("Weapon"), Is.EqualTo(0));
        }

        // ---------- the combat stance ----------

        [Test]
        public void Combat_TheBrainHasATarget_TheStanceIsCombat_AndStaysForCalmAfterOnceItIsNot()
        {
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(target);
            _ai.FrameTick(DT);
            Assert.IsFalse(Shown, "No target: the calm stance");

            _character.Context.BrainTarget = target.transform;
            _ai.FrameTick(DT);
            Assert.IsTrue(Shown, "A target: the combat stance at once");

            _character.Context.BrainTarget = null;
            for (float time = 0f; time < _ai.Tuning.CalmAfter - DT * 1.5f; time += DT)
                _ai.FrameTick(DT);
            Assert.IsTrue(Shown, "The target just gone: the sword stays out for Calm After");

            _ai.FrameTick(DT);
            _ai.FrameTick(DT);
            Assert.IsFalse(Shown, "Calm After has passed: the calm stance again");
        }

        [Test]
        public void Combat_TheControllerHasNoSuchParameter_NothingIsSet_NothingBreaks()
        {
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(target);
            _ai.Parameters.Combat = "No Such Parameter";
            _ai.ReadParameters();
            _character.Context.BrainTarget = target.transform;

            Assert.DoesNotThrow(() => _ai.FrameTick(DT));
            Assert.IsFalse(Shown);
        }

        // ---------- the walk ----------

        [Test]
        public void RootMotion_TheStepOfAnActionsClip_IsNotShownAsAWalk_ARealWalkStillIs()
        {
            // A blow that steps forward 1.2 m/s by its clip: shown as a walk, the animator would walk out of the blow
            const float FRAME = 0.02f;
            _ai.Tuning.Smoothing = 0f;
            for (int i = 0; i < 10; i++)
            {
                Vector3 step = _owner.transform.forward * (1.2f * FRAME);
                _owner.transform.position += step;
                _ai.MovedByClip(step);
                _ai.FrameTick(FRAME);
            }

            Assert.AreEqual(0f, _ai.ShownSpeed, 0.01f, "The clip's own step is no walk");

            Walk(1.5f, FRAME);
            Assert.AreEqual(1.5f, _ai.ShownSpeed, 0.01f, "The agent's walk is measured as ever");
        }

        [Test]
        public void RootMotion_ABodyThatTakesIt_GetsTheAnimatorsMotion_OneThatDoesNot_LeavesTheAnimatorAlone()
        {
            _animator.applyRootMotion = false;
            Assert.IsFalse(_ai.DrivesBody);
            Assert.IsFalse(_animator.applyRootMotion, "No root motion: the animator is left as it is");

            Make(new PictureBody { TakesRootMotion = true }, Controller());
            Assert.IsTrue(_ai.DrivesBody, "The motion of the root is handed to the body");
            Assert.IsTrue(_animator.applyRootMotion);
        }

        [Test]
        public void Braking_ASteadySlowWalkIsNot_SlowingDownIs_StandingIsNot()
        {
            // A body walking slowly at a steady pace (a wary sidestep, 0.6 m/s) is walking, not stopping: the stop clip
            // is for a body that brakes
            const float FRAME = 0.02f;
            float speed = 0.6f;
            for (int i = 0; i < 50; i++)
                Walk(speed, FRAME);
            Assert.IsFalse(_ai.ShowsBraking, "A steady slow walk is not braking");
            Assert.IsFalse(_animator.GetBool("Braking"));

            for (int i = 0; i < 10 && speed > 0f; i++)
            {
                speed = Mathf.Max(0f, speed - 2f * FRAME);                 // braking at 2 m/s² (a body's ramp)
                Walk(speed, FRAME);
            }

            Assert.IsTrue(_ai.ShowsBraking, "Slowing down is braking");
            Assert.IsTrue(_animator.GetBool("Braking"));

            for (int i = 0; i < 50; i++)
                Walk(0f, FRAME);
            Assert.IsFalse(_ai.ShowsBraking, "Standing is not braking");
        }

        [Test]
        public void Stride_ASlowWalk_ShorterStepsAtNoSlowerThanSlowestRate_NotASlowMotionWalk()
        {
            const float FRAME = 0.02f;
            _ai.Tuning.Smoothing = 0f;
            float slow = 0.6f;
            for (int i = 0; i < 50; i++)
                Walk(slow, FRAME);

            float rate = _animator.GetFloat("MoveRate");
            float stride = _animator.GetFloat("Stride");
            Assert.AreEqual(_ai.Tuning.SlowestRate, rate, 1e-3f, "The legs keep their pace: no slow motion");
            Assert.AreEqual(slow / (_ai.Tuning.SlowestRate * _ai.Tuning.ClipSpeed), stride, 0.01f,
                "…and the steps are as much shorter as the body is slower");
            Assert.AreEqual(1f, _animator.GetFloat("MoveX"), 0.01f, "The direction stays a direction: sideways");

            float fast = _ai.Tuning.ClipSpeed * 1.5f;
            for (int i = 0; i < 50; i++)
                Walk(fast, FRAME);

            Assert.AreEqual(1f, _animator.GetFloat("Stride"), 1e-3f, "Full steps");
            Assert.AreEqual(1.5f, _animator.GetFloat("MoveRate"), 0.01f, "…at the pace the speed asks");
        }

        [Test]
        public void Stride_TheControllerHasNoStride_TheRateIsHeldAtHalfAsBefore()
        {
            var controller = new AnimatorController();
            _made.Add(controller);
            controller.AddLayer("Base");
            controller.AddParameter("MoveRate", AnimatorControllerParameterType.Float);
            controller.layers[0].stateMachine.AddState("Idle");
            _animator.runtimeAnimatorController = controller;
            _ai.ReadParameters();
            _ai.Tuning.Smoothing = 0f;

            for (int i = 0; i < 50; i++)
                Walk(0.3f, 0.02f);

            Assert.AreEqual(0.5f, _animator.GetFloat("MoveRate"), 1e-3f, "No stance to blend with: the old floor");
        }

        [Test]
        public void Backing_RoundTheSide_TheHalfShownStays_ClearlyBackOrForward_ItTurnsOverInBackingBlend()
        {
            const float FRAME = 0.02f;
            _ai.Tuning.Smoothing = 0f;
            float lean = AIAnimator.BACKING_SWITCH * 0.5f;
            for (int i = 0; i < 50; i++)
                Walk(1.5f, FRAME, new Vector3(1f, 0f, i % 2 == 0 ? lean : -lean));
            Assert.AreEqual(0f, _animator.GetFloat("Backing"), 1e-3f, "Round the side, starting forward: stays forward");

            int frames = Mathf.CeilToInt(AIAnimator.BACKING_BLEND / FRAME);
            for (int i = 0; i < frames; i++)
                Walk(1.5f, FRAME, new Vector3(1f, 0f, -1f));
            Assert.AreEqual(1f, _animator.GetFloat("Backing"), 1e-3f, "Clearly back: the backward half, in Backing Blend");

            for (int i = 0; i < 50; i++)
                Walk(1.5f, FRAME, new Vector3(-1f, 0f, i % 2 == 0 ? lean : -lean));
            Assert.AreEqual(1f, _animator.GetFloat("Backing"), 1e-3f, "Round the side again: stays back");

            Walk(1.5f, FRAME, new Vector3(0f, 0f, 1f));
            Assert.Less(_ai.ShownBacking, 1f, "Clearly forward: turns over…");
            Assert.Greater(_ai.ShownBacking, 0f, "…sliding there, not at once");
        }

        // The body goes this fast, sideways, for one frame
        private void Walk(float speed, float dt) => Walk(speed, dt, Vector3.right);

        private void Walk(float speed, float dt, Vector3 way)
        {
            _owner.transform.position += way.normalized * (speed * dt);
            _ai.FrameTick(dt);
        }

        [Test]
        public void RootMotion_TheBodyStandsButIsAskedToWalk_TheAnimatorIsShownTheWalk_AndHandsTheRootToTheBody()
        {
            var body = new PictureBody { TakesRootMotion = true, WalksByClips = true, Asked = Vector3.forward * 1.2f };
            Make(body, Controller());
            _ai.Tuning.Smoothing = 0f;

            for (int i = 0; i < 5; i++)
                _ai.FrameTick(DT);                       // the body has not moved: the clips have not carried it yet

            Assert.That(_ai.ShownSpeed, Is.EqualTo(1.2f).Within(1e-4f),
                "Shown the speed asked, not the one the body had: or a standing body would never start");
            Assert.IsTrue(_ai.DrivesBody);
            Assert.IsTrue(_animator.applyRootMotion);
        }

        [Test]
        public void RootMotion_ABodyTheAgentMoves_IsShownWhatItReallyWent()
        {
            Assert.IsFalse(_ai.DrivesBody);

            _ai.FrameTick(DT);
            _owner.transform.position += Vector3.forward * 0.1f;
            _ai.FrameTick(DT);
            Assert.That(_ai.ShownSpeed, Is.GreaterThan(0f), "measured from where the body went, as before");
        }

        // ---------- the climb and the jump: the body's facts, pulled ----------

        [Test]
        public void Climb_OnAWall_ShowsTheClimbAndNoWalk_OffIt_TheWalkIsMeasuredFromWhereItLeftTheBody()
        {
            _ai.Tuning.Smoothing = 0f;
            _ai.FrameTick(DT);
            _body.Clip = 1;
            for (int i = 0; i < 10; i++)
            {
                _owner.transform.position += new Vector3(0f, 0.25f, 0.06f);     // the wall carries the body up
                _ai.FrameTick(0.1f);
                Assert.AreEqual(1f, _animator.GetFloat("Climb"), "Up: the climb is shown");
                Assert.AreEqual(0f, _animator.GetFloat("Speed"), "No walk while on the wall");
            }

            _body.Clip = -1;
            _ai.FrameTick(0.1f);
            Assert.AreEqual(-1f, _animator.GetFloat("Climb"), "Down");

            _owner.transform.position += Vector3.up * 2f;                       // the end of the climb puts it there
            _body.Clip = 0;
            _ai.FrameTick(0.1f);
            Assert.AreEqual(0f, _animator.GetFloat("Climb"));
            Assert.AreEqual(0f, _animator.GetFloat("Speed"), "The climb's last step is not taken for a run");

            _owner.transform.position += Vector3.forward * 0.2f;
            _ai.FrameTick(0.1f);
            Assert.AreEqual(2f, _animator.GetFloat("Speed"), 1e-3f, "Off the wall the walk is measured again");
        }

        [Test]
        public void Jump_TheBodysPhaseIsPulled_TakeOffAirLanding_NoWalkMeanwhile()
        {
            _ai.Tuning.Smoothing = 0f;
            foreach (JumpPhase phase in new[] { JumpPhase.TakeOff, JumpPhase.InAir, JumpPhase.Landing })
            {
                _body.Phase = phase;
                _owner.transform.position += Vector3.forward * 0.5f;            // the jump carries it, fast
                _ai.FrameTick(DT);
                Assert.AreEqual((int)phase, _animator.GetInteger("Jump"));
                Assert.AreEqual(0f, _animator.GetFloat("Speed"), "No walk in a jump");
            }

            _body.Phase = JumpPhase.None;
            _ai.FrameTick(DT);
            Assert.AreEqual(0, _animator.GetInteger("Jump"), "On its feet again");
        }

        // ---------- actions: the character's state, read once a frame ----------

        [Test]
        public void Actions_TheAnimatorIsTheirView_WhileEnabled_NotAfter()
        {
            Assert.That(_character.Context.Actions.View, Is.SameAs(_ai), "it shows the character's actions");
            Assert.IsTrue(_ai.Attach(), "attached again: nothing changes");
            Assert.That(_character.Context.Actions.View, Is.SameAs(_ai));

            _ai.Detach();
            Assert.That(_character.Context.Actions.View, Is.Null, "switched off: nobody shows them");
            Assert.IsFalse(_character.Context.Actions.HasView);
        }

        [Test]
        public void Actions_ARequestIsShownByItsState_UntilTheAnimatorLeavesIt_ANameItLacksIsNot()
        {
            Make(new PictureBody(), Controller("Dig"));
            var changes = new List<string>();
            _ai.ActionChanged += action => changes.Add(action ?? "-");
            ActionState actions = _character.Context.Actions;

            actions.Begin("Dig");
            Assert.IsTrue(actions.CanShow("Dig"), "the controller has the state");
            Assert.IsTrue(actions.IsShown, "a request not read yet counts as shown");
            _ai.FrameTick(DT);
            Assert.That(_ai.CurrentAction, Is.EqualTo("Dig"));
            Assert.IsTrue(actions.IsShown, "…and is shown");
            CollectionAssert.AreEqual(new[] { "Dig" }, changes);

            actions.Begin("Juggle");
            Assert.IsFalse(actions.CanShow("Juggle"), "a name the controller lacks");
            _ai.FrameTick(DT);
            Assert.IsFalse(actions.IsShown, "…is not shown: whoever asked waits by its own numbers");
        }

        [Test]
        public void Actions_TheSwingOfAnAttack_IsShownByTheTrigger()
        {
            _character.Context.Actions.Swing();
            _ai.FrameTick(DT);

            Assert.That(_ai.Swings, Is.EqualTo(1));
            Assert.That(_ai.CurrentAction, Is.EqualTo("Attack"));
            Assert.IsTrue(_character.Context.Actions.IsShown);
        }

        [Test]
        public void Actions_AskedBeforeTheAnimatorCame_AreNotReplayed()
        {
            Make(new PictureBody(), Controller("Dig"));
            _character.Context.Actions.Begin("Dig");
            _ai.Detach();
            var late = new GameObject("Late");
            late.transform.SetParent(_owner.transform, false);
            Animator lateAnimator = late.AddComponent<Animator>();
            lateAnimator.runtimeAnimatorController = Controller("Dig");
            lateAnimator.Rebind();
            lateAnimator.Update(0f);
            var second = late.AddComponent<AIAnimator>();

            second.Attach();
            second.FrameTick(DT);

            Assert.That(second.CurrentAction, Is.Null, "it shows what is asked from now on");
        }

        // ---------- the hurt: a flinch on its own layer ----------

        [Test]
        public void Hurt_TheCharacterIsHurt_TheFlinchPlaysOnItsLayer_OnceAHurt()
        {
            AnimatorController controller = Controller();
            controller.AddLayer("Hurt");
            controller.layers[1].stateMachine.AddState("Flinch");
            Make(new PictureBody(), controller);
            _ai.States.HurtLayer = "Hurt";
            _ai.States.Hurt = "Flinch";
            _ai.FrameTick(DT);
            Assert.That(_ai.Flinches, Is.Zero, "never hurt: no flinch");

            _character.Context.HurtAt = 1f;
            _ai.FrameTick(DT);
            _ai.FrameTick(DT);
            Assert.That(_ai.Flinches, Is.EqualTo(1), "hurt: one flinch, not one a frame");

            _character.Context.HurtAt = 2f;
            _ai.FrameTick(DT);
            Assert.That(_ai.Flinches, Is.EqualTo(2), "hurt again: again");
        }

        [Test]
        public void NoCharacterAbove_NothingIsShown_NothingBreaks()
        {
            var lone = new GameObject("Lone") { hideFlags = HideFlags.HideAndDontSave };
            _made.Add(lone);
            lone.AddComponent<Animator>();
            var ai = lone.AddComponent<AIAnimator>();

            Assert.IsFalse(ai.Attach());
            Assert.DoesNotThrow(() => ai.FrameTick(DT));
        }

        // ---------- a body as the picture sees it ----------

        // Says what a test sets: its climb, its jump, whether the clips carry it and the walk it is asked for
        private sealed class PictureBody : AgentBody
        {
            public int Clip;
            public JumpPhase Phase;
            public bool TakesRootMotion;
            public bool WalksByClips;
            public Vector3 Asked;
            public int Moves;

            public override IAgentActuator Actuator => null;

            public override bool UsesRootMotion => TakesRootMotion;

            public override bool WalksByAnimation => WalksByClips;

            public override Vector3 ShownVelocity => Asked;

            public override int ClimbDirection => Clip;

            public override JumpPhase Jump => Phase;

            public override void Attach(Transform owner)
            {
            }

            public override Vector3 MoveByAnimation(Vector3 deltaPosition, Quaternion deltaRotation, bool inAction, float dt)
            {
                Moves++;
                return Vector3.zero;
            }
        }
    }
}
