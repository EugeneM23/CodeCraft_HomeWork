using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The life of an ability's run (the step 90 recheck of the fight): a target gone in the wind-up — a blow hits nobody,
    /// a lob still comes down where it was last aimed; cancelled in the wind-up nothing leaves the hand, its action is
    /// dropped and its pause starts from there; cancelled after the release its mark ends now and what flies still hurts
    /// where it lands; a new graph or a character switched off cancels what plays; the start delay rests from the graph
    /// given; the side, the ground and the line are conditions the agent checks.
    /// </summary>
    public sealed class CombatAbilityLifeTests
    {
        private GraphRig _rig;
        private Transform _target;
        private HitProbe _hits;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _target = _rig.CreateTargetAt(new Vector3(0f, 0f, 1.5f));
            _hits = _target.gameObject.AddComponent<HitProbe>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        private AbilityRunner Runner(AbilityAsset ability)
        {
            _created.Add(ability);
            var runner = new AbilityRunner(ability, _rig.Agent.transform);
            _rig.Abilities.Add(runner.Definition);
            _rig.Abilities.Register(runner);
            return runner;
        }

        private static AbilityAsset Ability(string name, float windUp, float recovery, params AbilityPart[] parts)
        {
            AbilityAsset ability = AbilityAsset.Create(name, parts);
            ability.Timing = new AbilityTiming { WindUp = windUp, Recovery = recovery };
            ability.Queue.AtOnce = 0;
            return ability;
        }

        // A graph that names the abilities (the character prepares their runners) but never uses them on its own: its
        // Brain sees nothing, so the Use Ability decisions never hold
        private static BehaviorGraph Naming(params AbilityAsset[] abilities)
        {
            var g = new BrainGraphBuilder();
            foreach (AbilityAsset ability in abilities)
                g.Do(g.Decision(new UseAbilityReasoner { Ability = ability }), g.Block(new UseAbilityTask()));
            return g.Build();
        }

        private static AbilityRunner RunnerOf(GraphRig rig, string id)
        {
            foreach (AbilityRunner runner in rig.Agent.AbilityRunners)
            {
                if (runner.Id == id)
                    return runner;
            }

            Assert.Fail($"{id}: no runner prepared");
            return null;
        }

        // ---------- the target gone in the wind-up ----------

        [Test]
        public void AbilityRunner_TargetDestroyedInTheWindUp_ABlowHitsNobody()
        {
            Runner(Ability("Smash", 0.2f, 0.1f, new StrikeDelivery { Reach = 2f }, new DamageEffect { Damage = 1f }));
            HitProbe bystander = _rig.Agent.gameObject.AddComponent<HitProbe>();
            Assert.IsTrue(_rig.Abilities.TryUse("Smash", _rig.Context));

            _rig.Tick(0.1f);
            _rig.DestroyTarget();
            Assert.DoesNotThrow(() => _rig.Tick(0.2f), "Released at nobody");

            Assert.AreEqual(0, bystander.Hits, "Nobody is hit in its place");
            Assert.AreEqual(AbilityPhase.None, _rig.Context.AttackPhase, "And the run is over");
        }

        [Test]
        public void AbilityRunner_TargetDestroyedInTheWindUp_ALobLandsWhereItWasLastAimed()
        {
            var launcher = new CombatCaptureLauncher();
            _rig.Context.Launcher = launcher;
            Runner(Ability("Grenade", 0.2f, 0.1f, new LobDelivery(), new DamageEffect()));
            Vector3 last = new(0f, 0f, 3f);
            Assert.IsTrue(_rig.Abilities.TryUse("Grenade", _rig.Context));

            _rig.Tick(0.1f);
            _rig.MoveTargetTo(last);                                     // it aims after the target while it winds up…
            _rig.Tick(0.05f);
            _rig.DestroyTarget();                                        // …until the target is gone
            _rig.Tick(0.1f);

            Assert.AreEqual(1, launcher.Launches.Count, "Thrown anyway");
            Assert.AreEqual(last, launcher.Launches[0].Point, "Where it was aimed last");
            Assert.IsNull(launcher.Launches[0].Target, "A lob to a place");
        }

        // ---------- cancelled ----------

        [Test]
        public void AbilityRunner_CancelledInTheWindUp_NothingLetGo_PhaseNone_ActionDropped_PauseStarts()
        {
            const float PAUSE = 2f;
            var launcher = new CombatCaptureLauncher();
            _rig.Context.Launcher = launcher;
            var log = new ActionLog(_rig.Context, "Grenade");
            var cue = new CombatTokenCue();
            AbilityRunner runner = Runner(Ability("Grenade", 0.3f, 0.2f, new LobDelivery(),
                new CooldownCondition { Seconds = PAUSE }, cue));
            Assert.IsTrue(_rig.Abilities.TryUse("Grenade", _rig.Context));
            _rig.Tick(0.1f);

            runner.Cancel();
            _rig.Abilities.Tick(0.02f);

            Assert.IsEmpty(launcher.Launches, "Nothing left the hand");
            Assert.IsEmpty(cue.Made, "No mark: nothing flies");
            Assert.AreEqual(AbilityPhase.None, _rig.Context.AttackPhase);
            Assert.AreEqual(ActionState.Kind.None, _rig.Context.Actions.Current, "Its action is dropped");
            Assert.IsFalse(_rig.Context.Actions.EndsGently, "…at once, not played to its end");
            Assert.AreEqual(1, log.Drops);
            Assert.AreEqual(PAUSE, _rig.Abilities.CooldownLeft("Grenade"), 1e-4f, "Its pause starts from the cancel, whole");
        }

        [Test]
        public void AbilityRunner_CancelledAfterTheRelease_MarkEndsNow_ProjectileStillHurtsWhereItLands()
        {
            var launcher = new CombatCaptureLauncher();
            _rig.Context.Launcher = launcher;
            var cue = new CombatTokenCue();
            AbilityRunner runner = Runner(Ability("Grenade", 0.1f, 0.5f, new LobDelivery(),
                new DamageEffect { Damage = 1f, HitRadius = 1f }, cue));
            _rig.Abilities.TryUse("Grenade", _rig.Context);
            _rig.Tick(0.15f);
            Assert.AreEqual(1, launcher.Launches.Count, "Set-up: released, in the air");

            runner.Cancel();
            Assert.AreEqual(1, cue.Ended.Count, "Its mark ends with the cancel (End is mandatory)");

            ProjectileLaunch launch = launcher.Launches[0];
            launch.Landing.Landed(launch, launch.Point, null);

            Assert.AreEqual(1, cue.Ended.Count, "…and is not ended again where it lands");
            Assert.AreEqual(1, _hits.Hits, "What flies still comes down and hurts");
        }

        [Test]
        public void AICharacter_NewGraphWhileAnAbilityPlays_ItIsCancelled_PhaseNone()
        {
            AbilityAsset slam = GraphRig.MakeAbility("Slam", new SelfDelivery());
            slam.Timing = new AbilityTiming { WindUp = 1f, Recovery = 1f };
            AbilityAsset kick = GraphRig.MakeAbility("Kick", new StrikeDelivery());
            _rig.SetGraph(Naming(slam));
            Assert.IsTrue(_rig.Abilities.TryUse("Slam", _rig.Context));
            _rig.StepSeconds(0.5f);
            AbilityRunner old = RunnerOf(_rig, "Slam");
            Assert.IsTrue(old.IsRunning, "Set-up: it plays");

            _rig.SetGraph(Naming(kick));

            Assert.IsFalse(_rig.Abilities.Has("Slam"), "The new graph does not name it");
            Assert.IsFalse(old.IsRunning, "What played is cancelled");
            Assert.AreEqual(AbilityPhase.None, _rig.Context.AttackPhase, "The status says no blow");
        }

        [Test]
        public void AICharacter_DisabledInTheWindUp_AbilityCancelled_NothingThrown()
        {
            AbilityAsset grenade = GraphRig.MakeAbility("Grenade", new LobDelivery());
            grenade.Timing = new AbilityTiming { WindUp = 0.5f, Recovery = 0.2f };
            grenade.Queue.AtOnce = 0;
            _rig.SetGraph(Naming(grenade));
            _rig.Agent.gameObject.SetActive(true);
            var launcher = new CombatCaptureLauncher();
            _rig.Context.Launcher = launcher;
            Assert.IsTrue(_rig.Abilities.TryUse("Grenade", _rig.Context));
            _rig.StepSeconds(0.2f);

            CombatWorldWeaponTests.SwitchOff(_rig.Agent);       // the engine's message, sent by hand in edit mode
            _rig.Tick(1f);

            Assert.IsFalse(RunnerOf(_rig, "Grenade").IsRunning, "Switched off: what played is cancelled");
            Assert.AreEqual(AbilityPhase.None, _rig.Context.AttackPhase);
            Assert.IsEmpty(launcher.Launches, "Nothing is thrown by a character switched off");
        }

        // ---------- the conditions ----------

        [Test]
        public void StartDelayPart_RestsItsDelayFromSetGraph_ThenReady()
        {
            const float DELAY = 1.5f;
            AbilityAsset jab = GraphRig.MakeAbility("Jab", new StartDelayCondition { Seconds = DELAY },
                new RangeCondition { Min = 0f, Max = 5f }, new StrikeDelivery());
            _rig.MoveTargetTo(new Vector3(0f, 0f, 2f));
            _rig.SetGraph(Naming(jab));

            Assert.AreEqual(AbilityBlock.Resting, _rig.Abilities.BlockOf("Jab", _rig.Context), "Not in its first seconds");
            Assert.AreEqual(DELAY, _rig.Abilities.RestLeft("Jab"), 1e-4f);
            for (int i = 0; i < 14; i++)
                _rig.Tick(0.1f);
            Assert.AreEqual(AbilityBlock.Resting, _rig.Abilities.BlockOf("Jab", _rig.Context), "Still resting at 1.4 s");
            for (int i = 0; i < 2; i++)
                _rig.Tick(0.1f);
            Assert.AreEqual(AbilityBlock.None, _rig.Abilities.BlockOf("Jab", _rig.Context), "Ready at 1.6 s");
        }

        [Test]
        public void AngleCondition_SideOnly_ReadyAtTheSide_WrongSideInFrontAndBehind()
        {
            Runner(Ability("Sweep", 0.1f, 0.1f, new AngleCondition { Front = false, Side = true, Back = false },
                new StrikeDelivery()));
            _rig.Agent.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            _rig.MoveTargetTo(new Vector3(1.5f, 0f, 0f));
            Assert.AreEqual(AbilityBlock.None, _rig.Abilities.BlockOf("Sweep", _rig.Context), "At the side: ready");
            _rig.MoveTargetTo(new Vector3(0f, 0f, 1.5f));
            Assert.AreEqual(AbilityBlock.WrongSide, _rig.Abilities.BlockOf("Sweep", _rig.Context), "In front: not");
            _rig.MoveTargetTo(new Vector3(0f, 0f, -1.5f));
            Assert.AreEqual(AbilityBlock.WrongSide, _rig.Abilities.BlockOf("Sweep", _rig.Context), "Behind: not");
        }

        [Test]
        public void GroundAndLineParts_InTheAirInAir_NoClearLineNoLine()
        {
            Runner(Ability("Leap", 0.1f, 0.1f, new GroundCondition(), new LineCondition(), new StrikeDelivery()));
            var sight = new CombatSwitchSight { Clear = false };
            _rig.Context.Sight = sight;

            _rig.Actuator.IsGrounded = false;
            Assert.AreEqual(AbilityBlock.InAir, _rig.Abilities.BlockOf("Leap", _rig.Context));
            _rig.Actuator.IsGrounded = true;
            Assert.AreEqual(AbilityBlock.NoLine, _rig.Abilities.BlockOf("Leap", _rig.Context));
            sight.Clear = true;
            Assert.AreEqual(AbilityBlock.None, _rig.Abilities.BlockOf("Leap", _rig.Context));
        }
    }
}
