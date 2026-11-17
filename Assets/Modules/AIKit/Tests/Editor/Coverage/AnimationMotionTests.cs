using System.Collections.Generic;
using AIKit.Mecanim;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// What the AI Animator reads off the character's body and writes into the controller (step 90 recheck; handbook
    /// §20.1–20.2): the root's motion goes to the body with the frame's own time (none while the game is paused), a clip
    /// set of a character variant keeps the variant's own clips under a weapon's set, a weapon taken from the world shows
    /// its clips while it is held, a jump's landing starts at the moment its clip's feet touch the ground, and the walk —
    /// teleports, smoothing, fast bodies, standing still, the climb, the frame after a jump, the combat stance on a wall,
    /// a reset, the hurt, the weapon's place, a late character, the clip sets — is written as the body really goes.
    /// </summary>
    public sealed class AnimationMotionTests
    {
        private const float DT = 0.1f;
        private AnimationCoverageStand _stand;

        [SetUp]
        public void SetUp() => _stand = new AnimationCoverageStand();

        [TearDown]
        public void TearDown()
        {
            _stand.Destroy();
            LogAssert.ignoreFailingMessages = false;
        }

        private AnimationCoverageStand Plain(RecordingBody body = null) => _stand.Make(_stand.Controller(), body);

        // The owner goes this far this frame (flat), then the AI Animator reads it
        private void Walk(Vector3 step, float dt)
        {
            _stand.Owner.transform.position += step;
            _stand.AI.FrameTick(dt);
        }

        // ---------- the root's motion ----------

        [Test]
        public void AIAnimator_Paused_OnAnimatorMoveGivesTheBodyNoTime()
        {
            LogAssert.ignoreFailingMessages = true;                         // Edit Mode: a message to it is "not to run"
            Plain(new RecordingBody { TakesRootMotion = true });
            _stand.AI.FrameTick(0.02f);
            _stand.AI.FrameTick(0f);                                        // a paused frame (Time.timeScale 0)

            _stand.Model.SendMessage("OnAnimatorMove");

            Assert.AreEqual(1, _stand.Body.Moves, "Set-up: the root's motion is handed to the body");
            Assert.AreEqual(0f, _stand.Body.LastDt, "Paused: no time passed — a dash does not go on, nobody is pushed");
        }

        [Test]
        public void AIAnimator_OnAnimatorMove_GivesTheClipsStepWithInActionAndFrameDt_ItsReturnIsNoWalk()
        {
            LogAssert.ignoreFailingMessages = true;                         // Edit Mode: a message to it is "not to run"
            AnimatorController controller = _stand.Controller();
            _stand.State(controller, "Dig");
            _stand.Make(controller, new RecordingBody { TakesRootMotion = true, Returns = new Vector3(0.024f, 0f, 0f) });

            _stand.AI.FrameTick(0.02f);
            _stand.Owner.transform.position += new Vector3(0.024f, 0f, 0f);  // the body put itself where the clip led
            _stand.Model.SendMessage("OnAnimatorMove");
            Assert.AreEqual(1, _stand.Body.Moves);
            Assert.AreEqual(0.02f, _stand.Body.LastDt, 1e-6f, "The frame's own time");
            Assert.IsFalse(_stand.Body.LastInAction, "No action: the walk");

            _stand.AI.FrameTick(0.02f);
            Assert.AreEqual(0f, _stand.AI.ShownSpeed, 1e-4f, "The clip's step is not a walk of 1.2 m/s");

            _stand.Actions.Begin("Dig");
            _stand.AI.FrameTick(0.02f);
            _stand.Animator.Update(0.2f);
            _stand.Model.SendMessage("OnAnimatorMove");
            Assert.AreEqual(2, _stand.Body.Moves);
            Assert.IsTrue(_stand.Body.LastInAction, "An action tagged Action plays");

            var other = new AnimationCoverageStand();
            try
            {
                other.Make(other.Controller(), new RecordingBody());
                other.Animator.applyRootMotion = false;
                other.AI.FrameTick(0.02f);
                other.Model.SendMessage("OnAnimatorMove");
                Assert.AreEqual(0, other.Body.Moves, "A body that takes no root motion is handed none");
                Assert.IsFalse(other.Animator.applyRootMotion, "…and the animator is left as it is");
            }
            finally
            {
                other.Destroy();
            }
        }

        // ---------- clip sets ----------

        [Test]
        public void AIAnimator_AnOverrideControllerOnTheAnimator_KeepsItsOwnClipsUnderAWeaponsSet()
        {
            AnimationClip idle = _stand.Clip("Idle", 1f), swing = _stand.Clip("Swing", 1f);
            AnimationClip variantIdle = _stand.Clip("Variant Idle", 1f), bowSwing = _stand.Clip("Bow Swing", 1f);
            var controller = _stand.Keep(new AnimatorController());
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle").motion = idle;
            controller.layers[0].stateMachine.AddState("Attack 1").motion = swing;
            var variant = _stand.Keep(new AnimatorOverrideController(controller) { [idle] = variantIdle });
            var set = _stand.Keep(new AnimatorOverrideController(controller) { [swing] = bowSwing });
            _stand.Make(variant);

            _stand.AI.ShowMotions(set);
            var shown = (AnimatorOverrideController)_stand.Animator.runtimeAnimatorController;
            Assert.AreSame(variantIdle, shown[idle], "The variant's own idle stays");
            Assert.AreSame(bowSwing, shown[swing], "The weapon's swing is shown");

            _stand.AI.ShowMotions(null);
            shown = (AnimatorOverrideController)_stand.Animator.runtimeAnimatorController;
            Assert.AreSame(variantIdle, shown[idle], "No set: the variant's clips, not the base controller's");
            Assert.AreSame(swing, shown[swing]);
        }

        [Test]
        public void AIAnimator_AWorldWeaponsClipSet_IsShownWhileItIsHeld()
        {
            AnimationClip throwClip = _stand.Clip("Throw", 1f), swing = _stand.Clip("Swing", 1f), heavy = _stand.Clip("Heavy", 1f);
            var controller = _stand.Keep(new AnimatorController());
            controller.AddLayer("Base");
            controller.layers[0].stateMachine.AddState("Idle");
            controller.layers[0].stateMachine.AddState("Attack 1").motion = swing;
            var swordSet = _stand.Keep(new AnimatorOverrideController(controller) { [swing] = heavy });
            var rockSet = _stand.Keep(new AnimatorOverrideController(controller) { [swing] = throwClip });
            _stand.Make(controller);
            var sword = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Attack 1" }));
            sword.Motions = swordSet;
            var rock = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Attack 1" }));
            rock.Motions = rockSet;
            _stand.Character.Weapon = sword;
            _stand.AI.FrameTick(DT);
            Assert.AreSame(swordSet, _stand.AI.ShownMotions, "Set-up: the sword's clips");

            Assert.IsTrue(_stand.Character.TakeUpFromWorld(rock, null, 1, false));
            _stand.AI.FrameTick(DT);
            Assert.AreSame(rockSet, _stand.AI.ShownMotions, "The rock in the hand: its throw on the slot");

            _stand.Character.SpendUse();
            _stand.AI.FrameTick(DT);
            Assert.AreSame(swordSet, _stand.AI.ShownMotions, "Thrown: the sword's clips again");
        }

        [Test]
        public void AIAnimator_NoSetEverAsked_TheControllerIsLeftAsItIs()
        {
            AnimatorController controller = _stand.Controller();
            _stand.Make(controller);
            _stand.Character.Weapon = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" }));

            _stand.AI.FrameTick(DT);
            _stand.AI.FrameTick(DT);

            Assert.AreSame(controller, _stand.Animator.runtimeAnimatorController, "No override made");
            Assert.IsNull(_stand.AI.ShownMotions);
        }

        [Test]
        public void AIAnimator_TheFirstSetSwapsTheController_TheParametersAreStillWritten()
        {
            AnimatorController controller = _stand.Controller();
            AnimationClip idle = _stand.Clip("Idle", 1f);
            controller.layers[0].stateMachine.states[0].state.motion = idle;
            Plain();
            _stand.Animator.runtimeAnimatorController = controller;
            _stand.AI.ReadParameters();
            var set = _stand.Keep(new AnimatorOverrideController(controller) { [idle] = _stand.Clip("Bow Idle", 1f) });
            var foe = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _stand.Keep(foe);

            _stand.AI.ShowMotions(set);
            _stand.Character.Context.BrainTarget = foe.transform;
            Walk(new Vector3(0.15f, 0f, 0f), DT);

            Assert.AreNotSame(controller, _stand.Animator.runtimeAnimatorController, "Set-up: its own override now");
            Assert.AreEqual(1.5f, _stand.Animator.GetFloat("Speed"), 1e-3f, "The speed on the new override");
            Assert.IsTrue(_stand.Animator.GetBool("Combat"));
        }

        [Test]
        public void AIAnimator_MotionsAtLastAction_StaysTheSetOfThatAction()
        {
            AnimationClip draw = _stand.Clip("Draw", 1f);
            AnimatorController controller = _stand.Controller();
            _stand.State(controller, "Weapon Draw").motion = draw;
            _stand.Make(controller);
            var bowSet = _stand.Keep(new AnimatorOverrideController(controller) { [draw] = _stand.Clip("Bow Draw", 1f) });
            var swordSet = _stand.Keep(new AnimatorOverrideController(controller) { [draw] = _stand.Clip("Sword Draw", 1f) });
            var bow = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" }));
            bow.Motions = bowSet;
            var sword = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" }));
            sword.Motions = swordSet;

            _stand.Character.ShowMotionsOf(bow);
            _stand.Actions.Begin("Weapon Draw");
            _stand.AI.FrameTick(DT);
            _stand.Character.ShowMotionsOf(sword);
            _stand.AI.FrameTick(DT);

            Assert.AreSame(bowSet, _stand.AI.MotionsAtLastAction, "The draw was shown with the bow's clips");
            Assert.AreSame(swordSet, _stand.AI.ShownMotions);
        }

        // ---------- the jump's landing ----------

        [Test]
        public void AIAnimator_ALanding_StartsItsClipWhereTheFeetTouch()
        {
            const float TOUCH = 0.3f;
            AnimatorController controller = _stand.Controller();
            _stand.State(controller, "Jump Land", action: false, seconds: 1f);
            var body = new RecordingBody
            {
                Climbs = true,
                Measured = new MeasuredTraversal
                {
                    MeasuredFrom = "the test", HasJump = true,
                    Jump = new JumpMotion { TakeOff = 0.2f, Touch = TOUCH, Landing = 0.4f }
                }
            };
            _stand.Make(controller, body);

            body.Phase = JumpPhase.InAir;
            _stand.Frame(DT);
            body.Phase = JumpPhase.Landing;
            _stand.AI.FrameTick(DT);
            _stand.Animator.Update(0.05f);

            AnimatorStateInfo now = _stand.Animator.IsInTransition(0)
                ? _stand.Animator.GetNextAnimatorStateInfo(0)
                : _stand.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.IsTrue(now.IsName("Jump Land"), "The landing is shown the moment the body is down");
            Assert.AreEqual(TOUCH, now.normalizedTime * now.length, 0.06f,
                "From where its feet touch the ground: not from its start, with the feet still in the air");
        }

        // ---------- the walk as the body really goes ----------

        [Test]
        public void AIAnimator_ATeleport_IsNoRunAndNoBraking()
        {
            Plain();
            for (int i = 0; i < 50; i++)
                Walk(new Vector3(1.5f * 0.02f, 0f, 0f), 0.02f);

            Walk(new Vector3(5f, 0f, 0f), 0.02f);                          // 250 m/s: put somewhere
            Assert.AreEqual(0f, _stand.AI.ShownSpeed, "A teleport is no run");
            Assert.AreEqual(_stand.AI.Tuning.SlowestRate, _stand.Animator.GetFloat("MoveRate"), 1e-4f);
            Assert.AreEqual(0f, _stand.Animator.GetFloat("Stride"), 1e-4f);
            Assert.IsFalse(_stand.Animator.GetBool("Braking"), "…and no braking");

            Walk(new Vector3(1.5f * 0.02f, 0f, 0f), 0.02f);
            Assert.AreEqual(1.5f, _stand.AI.ShownSpeed, 1e-3f, "Walking on from where it was put");
            Assert.IsFalse(_stand.Animator.GetBool("Braking"));
        }

        [Test]
        public void AIAnimator_Smoothing_TheShownSpeedCatchesUpExponentially()
        {
            Plain();
            _stand.AI.Tuning.Smoothing = 0.1f;

            Walk(new Vector3(0.2f, 0f, 0f), DT);
            Assert.AreEqual(2f * (1f - Mathf.Exp(-1f)), _stand.AI.ShownSpeed, 1e-3f);
            Walk(new Vector3(0.2f, 0f, 0f), DT);
            Assert.AreEqual(2f * (1f - Mathf.Exp(-2f)), _stand.AI.ShownSpeed, 1e-3f);
        }

        [Test]
        public void AIAnimator_AFastBody_MoveRateHeldAtThree_FullStride()
        {
            Plain();
            _stand.AI.Tuning.ClipSpeed = 1.6f;

            Walk(new Vector3(0.6f, 0f, 0f), DT);

            Assert.AreEqual(6f, _stand.Animator.GetFloat("Speed"), 1e-3f);
            Assert.AreEqual(3f, _stand.Animator.GetFloat("MoveRate"), 1e-4f, "3.75 times the clip's pace, held at 3");
            Assert.AreEqual(1f, _stand.Animator.GetFloat("Stride"), 1e-4f);
        }

        [Test]
        public void AIAnimator_StandingOrCreeping_KeepsTheLastDirection()
        {
            Plain();
            Walk(new Vector3(0.15f, 0f, 0f), DT);                           // sideways (it looks +Z)
            Assert.AreEqual(1f, _stand.Animator.GetFloat("MoveX"), 1e-3f);

            for (int i = 0; i < 10; i++)
                Walk(Vector3.zero, DT);
            Assert.AreEqual(1f, _stand.Animator.GetFloat("MoveX"), 1e-3f, "Standing: the last way stays");
            Assert.AreEqual(0f, _stand.Animator.GetFloat("MoveZ"), 1e-3f);
            Assert.AreEqual(0f, _stand.Animator.GetFloat("Speed"), 1e-4f);

            Walk(new Vector3(0f, 0f, -0.005f), DT);                         // creeping back at 0.05 m/s
            Assert.AreEqual(1f, _stand.Animator.GetFloat("MoveX"), 1e-3f, "Too slow to be a way");
            Assert.AreEqual(0f, _stand.Animator.GetFloat("MoveZ"), 1e-3f);
            Assert.AreEqual(0.05f, _stand.Animator.GetFloat("Speed"), 1e-3f);
        }

        [Test]
        public void AIAnimator_ClimbStates_StartedOncePerClimbFromTheirStart()
        {
            AnimatorController controller = _stand.Controller();
            _stand.State(controller, "Climb Up", action: false, seconds: 2f);
            _stand.State(controller, "Climb Down", action: false, seconds: 2f);
            _stand.Make(controller);

            _stand.Body.Clip = 1;
            _stand.Frame(DT);
            AnimatorStateInfo first = _stand.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.IsTrue(first.IsName("Climb Up"), "Up the wall: Climb Up from its start");
            Assert.LessOrEqual(first.normalizedTime, 0.05f + 0.01f, "From its start");
            _stand.Frame(DT);
            Assert.AreEqual(first.normalizedTime + DT / 2f, _stand.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 0.01f,
                "Played on a frame's worth: not started again");

            _stand.Body.Clip = -1;
            _stand.Frame(DT);
            Assert.IsTrue(_stand.Animator.GetCurrentAnimatorStateInfo(0).IsName("Climb Down"));
            _stand.Body.Clip = 0;
            _stand.Frame(DT);

            var plain = new AnimationCoverageStand();
            try
            {
                plain.Make(plain.Controller());
                plain.Body.Clip = 1;
                var logs = new List<string>();
                void Listen(string message, string stack, LogType type) => logs.Add(message);
                Application.logMessageReceived += Listen;
                try
                {
                    plain.Frame(DT);
                }
                finally
                {
                    Application.logMessageReceived -= Listen;
                }

                Assert.AreEqual(1f, plain.Animator.GetFloat("Climb"), "No climbing states: only the float is written");
                CollectionAssert.IsEmpty(logs);
            }
            finally
            {
                plain.Destroy();
            }
        }

        [Test]
        public void AIAnimator_TheFrameAfterAJump_IsNotARun()
        {
            Plain();
            _stand.Body.Phase = JumpPhase.InAir;
            Walk(new Vector3(0.5f, 0f, 0f), DT);
            Assert.AreEqual(0f, _stand.Animator.GetFloat("Speed"), "In the air: no walk");

            _stand.Body.Phase = JumpPhase.None;
            Walk(new Vector3(0.3f, 0f, 0f), DT);
            Assert.AreEqual(0f, _stand.Animator.GetFloat("Speed"), "The jump's last step is not taken for a run");

            Walk(new Vector3(0.1f, 0f, 0f), DT);
            Assert.AreEqual(1f, _stand.Animator.GetFloat("Speed"), 1e-3f, "Then the walk is measured again");
        }

        [Test]
        public void AIAnimator_OnAWall_NoBraking_TheCombatStanceStillCountsDown()
        {
            Plain();
            _stand.AI.Tuning.CalmAfter = 1f;
            var foe = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _stand.Keep(foe);
            _stand.Character.Context.BrainTarget = foe.transform;
            Walk(new Vector3(0.3f, 0f, 0f), DT);
            _stand.Body.Clip = 1;
            _stand.Character.Context.BrainTarget = null;

            int combatFrames = 0;
            for (int i = 0; i < 20; i++)
            {
                Walk(new Vector3(0.1f * (1f - i / 20f), 0.2f, 0f), DT);    // carried up the wall, slower and slower
                if (_stand.Animator.GetBool("Combat"))
                    combatFrames++;
                Assert.IsFalse(_stand.Animator.GetBool("Braking"), "On a wall: no braking");
                Assert.AreEqual(0f, _stand.Animator.GetFloat("Speed"), "…and no walk");
            }

            Assert.That(combatFrames, Is.InRange(9, 11), "The combat stance stays Calm After (1 s) on the wall too");
        }

        [Test]
        public void AIAnimator_ResetPosition_TheNextFrameMeasuresNoSpeedFromTheOldPlace()
        {
            Plain();
            _stand.AI.FrameTick(DT);
            _stand.Owner.transform.position += new Vector3(2f, 0f, 0f);    // 20 m/s in a frame: under the teleport limit
            _stand.AI.ResetPosition();

            _stand.AI.FrameTick(DT);

            Assert.AreEqual(0f, _stand.AI.ShownSpeed, "Put there: no walk measured from the old place");
        }

        [Test]
        public void AIAnimator_Hurt_WithoutALayerOrState_NoFlinch_NotReplayedOnceOneIsSet()
        {
            AnimatorController controller = _stand.Controller();
            controller.AddLayer("Hurt");
            controller.layers[1].stateMachine.AddState("Flinch");
            _stand.Make(controller);
            _stand.AI.States.Hurt = "Flinch";
            _stand.AI.States.HurtLayer = "";

            _stand.Character.Context.HurtAt = 1f;
            _stand.AI.FrameTick(DT);
            Assert.AreEqual(0, _stand.AI.Flinches, "No layer: no flinch");

            _stand.AI.States.HurtLayer = "Hurt";
            _stand.AI.FrameTick(DT);
            Assert.AreEqual(0, _stand.AI.Flinches, "The hurt before the layer was given is over");

            _stand.Character.Context.HurtAt = 2f;
            _stand.AI.FrameTick(DT);
            Assert.AreEqual(1, _stand.AI.Flinches, "A new hurt: one flinch");
        }

        [Test]
        public void AIAnimator_Hurt_BeforeItAttached_IsNotPlayed()
        {
            AnimatorController controller = _stand.Controller();
            controller.AddLayer("Hurt");
            controller.layers[1].stateMachine.AddState("Flinch");
            _stand.Make(controller, attach: false);
            _stand.AI.States.Hurt = "Flinch";
            _stand.AI.States.HurtLayer = "Hurt";
            _stand.Character.Context.HurtAt = 3f;

            Assert.IsTrue(_stand.AI.Attach());
            _stand.AI.FrameTick(DT);

            Assert.AreEqual(0, _stand.AI.Flinches, "Hurt before it came: not shown");
        }

        [Test]
        public void AIAnimator_Weapon_NotOnTheList_IsTheListsCount_NoneIsZero()
        {
            Plain();
            var sword = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" }));
            var bow = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" }));
            var axe = _stand.Keep(WeaponAsset.Create<WeaponAsset>(new WeaponMove { Name = "Normal" }));
            _stand.Character.Weapons.Add(new CharacterWeapon { Weapon = sword });
            _stand.Character.Weapons.Add(new CharacterWeapon { Weapon = bow });

            _stand.Character.Weapon = axe;
            _stand.AI.FrameTick(DT);
            Assert.AreEqual(2, _stand.Animator.GetInteger("Weapon"), "Not on the list: past its end");

            _stand.Character.Weapon = null;
            _stand.AI.FrameTick(DT);
            Assert.AreEqual(0, _stand.Animator.GetInteger("Weapon"), "None: the first");
        }

        [Test]
        public void AIAnimator_AttachedBeforeTheCharacterHasAContext_AttachesOnALaterFrame()
        {
            var owner = _stand.Keep(new GameObject("Late") { hideFlags = HideFlags.HideAndDontSave });
            owner.SetActive(false);                                         // no Awake: no context yet
            var character = owner.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.Body = new RecordingBody();
            var model = new GameObject("Body");
            model.transform.SetParent(owner.transform, false);
            Animator animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = _stand.Controller();
            var ai = model.AddComponent<AIAnimator>();

            Assert.IsFalse(ai.Attach(), "No context yet");
            Assert.DoesNotThrow(() => ai.FrameTick(DT));
            Assert.IsFalse(ai.IsAttached);

            character.Initialize();
            ai.FrameTick(DT);
            Assert.IsTrue(ai.IsAttached, "It links up once the character has its context");
            Assert.AreSame(ai, character.Context.Actions.View);
        }

        [Test]
        public void AIAnimator_HasParameter_NameAndKindMustBothMatch()
        {
            AnimatorController controller = _stand.Controller();
            Plain();
            _stand.Animator.runtimeAnimatorController = controller;

            Assert.IsTrue(AIAnimator.HasParameter(_stand.Animator, "Speed", AnimatorControllerParameterType.Float));
            Assert.IsFalse(AIAnimator.HasParameter(_stand.Animator, "Speed", AnimatorControllerParameterType.Int));
            Assert.IsFalse(AIAnimator.HasParameter(_stand.Animator, "Jump", AnimatorControllerParameterType.Float));
            Assert.IsFalse(AIAnimator.HasParameter(_stand.Animator, "", AnimatorControllerParameterType.Float));

            _stand.Animator.runtimeAnimatorController = null;
            Assert.IsFalse(AIAnimator.HasParameter(_stand.Animator, "Speed", AnimatorControllerParameterType.Float));
        }

        [Test]
        public void AIAnimator_ReadParameters_AfterARename_WritesTheNewName()
        {
            AnimatorController controller = _stand.Controller();
            controller.AddParameter("Velocity", AnimatorControllerParameterType.Float);
            _stand.Make(controller);
            _stand.AI.Parameters.Speed = "Velocity";
            _stand.AI.ReadParameters();

            Walk(new Vector3(0.15f, 0f, 0f), DT);

            Assert.AreEqual(1.5f, _stand.Animator.GetFloat("Velocity"), 1e-3f);
            Assert.AreEqual(0f, _stand.Animator.GetFloat("Speed"), "The old name is written no more");
        }
    }
}
