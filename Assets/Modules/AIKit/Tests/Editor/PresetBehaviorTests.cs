using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The presets are the "five minutes" promise of the module, so they are tested as behavior: it patrols, it closes
    /// in, it strikes and pauses, it backs off and shoots, a guard turns to the target and holds its ground, a sentry
    /// fires. Each preset is a brain graph: the Brain takes a decision (Use Any Ability over the tuning's abilities, Chase,
    /// Watch, Patrol…) and the decision's chain of blocks runs — the nodes are called by their types. Numbers come from
    /// the tuning, never from magic constants in the test.
    /// </summary>
    public class PresetBehaviorTests
    {
        // Seconds for the start (the first think enters the Brain) and the Brain's first choice on the next think
        private const float DECIDE = 0.5f;

        // The reach of the test's melee abilities (the presets' Tuning has no attack range: an ability's own range is it)
        private const float REACH = 1.6f;

        // The attack decision of every preset (step 90): taken when one of the tuning's abilities is ready
        private const string FIGHT = "Use Any Ability";

        // The ability is the test's runner; the preset names it through the tuning (an asset the runner plays)
        private TestAbilityRunner Give(BehaviorPresets.Tuning tuning, string id, float duration = 0f, float cooldown = 0.5f,
            float maxRange = 2f)
        {
            tuning.Abilities.Add(GraphRig.Named(id));
            return _rig.AddAbility(id, duration, cooldown, maxRange);
        }

        private GraphRig _rig;

        [SetUp]
        public void SetUp() => _rig = new GraphRig();

        [TearDown]
        public void TearDown() => _rig.DestroyTarget();

        [Test]
        public void MeleeChaser_NoTarget_Patrols()
        {
            _rig.SetGraph(BehaviorPresets.MeleeChaser(new BehaviorPresets.Tuning()));

            Assert.IsTrue(StepUntilDoing("Patrol", "Patrol"), Doing());
        }

        [Test]
        public void MeleeChaser_TargetInSight_ClosesIn()
        {
            var tuning = new BehaviorPresets.Tuning();
            Give(tuning, "Melee", duration: 0.2f, maxRange: REACH);
            _rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            _rig.CreateTargetAt(new Vector3(6f, 0f, 0f));

            Assert.IsTrue(StepUntilDoing("Chase", "Chase"), Doing());

            float before = _rig.Actuator.Position.x;
            _rig.StepSeconds(0.3f);

            Assert.Greater(_rig.Actuator.Position.x, before, "The agent must be closing in on its target");
        }

        [Test]
        public void MeleeChaser_TargetInReach_Strikes()
        {
            var tuning = new BehaviorPresets.Tuning();
            TestAbilityRunner melee = Give(tuning, "Melee", duration: 0.2f, maxRange: REACH);
            _rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            _rig.CreateTargetAt(new Vector3(REACH * 0.8f, 0f, 0f));

            Assert.IsTrue(StepUntilDoing(FIGHT, "Use Ability"), Doing());

            Assert.AreEqual(1, melee.Starts);
        }

        [Test]
        public void MeleeChaser_AfterTheStrike_PausesForItsRecovery_WhileAnotherOfItsAbilitiesIsReady()
        {
            var tuning = new BehaviorPresets.Tuning { Recovery = 0.5f };
            const float BLOW = 0.1f;
            TestAbilityRunner melee = Give(tuning, "Melee", duration: BLOW, cooldown: 2f, maxRange: REACH);
            TestAbilityRunner jab = Give(tuning, "Jab", duration: BLOW, cooldown: 2f, maxRange: REACH);
            _rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            _rig.CreateTargetAt(new Vector3(REACH * 0.8f, 0f, 0f));
            Assert.IsTrue(StepUntilDoing(FIGHT, "Use Ability"), Doing());

            Assert.IsTrue(StepUntilDoing(FIGHT, "Wait", BLOW + 0.3f), "The strike is over, so the agent takes its pause: " +
                Doing());
            _rig.StepSeconds(tuning.Recovery * 0.5f);

            Assert.AreEqual("Wait", _rig.StateName, "…for its Recovery");
            Assert.AreEqual(1, melee.Starts + jab.Starts, "No blow in the pause");
        }

        [Test]
        public void MeleeChaser_ItsOnlyAbilityRests_NoBlowUntilItHasRested_ThenTheNext()
        {
            var tuning = new BehaviorPresets.Tuning();
            const float COOLDOWN = 1f;
            TestAbilityRunner melee = Give(tuning, "Melee", duration: 0.1f, cooldown: COOLDOWN, maxRange: REACH);
            _rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            _rig.CreateTargetAt(new Vector3(REACH * 0.8f, 0f, 0f));
            Assert.IsTrue(_rig.StepUntil(() => melee.Starts == 1, DECIDE), Doing());

            _rig.StepSeconds(COOLDOWN * 0.8f);
            Assert.AreEqual(1, melee.Starts, "Resting: the decision lets go, nothing is used");

            Assert.IsTrue(_rig.StepUntil(() => melee.Starts == 2, COOLDOWN), "Rested: the next blow — " + Doing());
        }

        [Test]
        public void MeleeChaser_TargetLost_GoesBackToPatrol()
        {
            var tuning = new BehaviorPresets.Tuning();
            Give(tuning, "Melee", maxRange: REACH);
            _rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            _rig.CreateTargetAt(new Vector3(5f, 0f, 0f));
            Assert.IsTrue(StepUntilDoing("Chase", "Chase"), Doing());

            _rig.DestroyTarget();

            Assert.IsTrue(StepUntilDoing("Patrol", "Patrol", 0.2f), Doing());
        }

        [Test]
        public void MeleeChaser_TargetBeyondSight_KeepsPatrolling()
        {
            var tuning = new BehaviorPresets.Tuning();
            _rig.GivePerception(PerceptionProfile.Create(p =>
            {
                p.FocusRange = 12f;
                p.FocusHalfAngle = 180f;
                p.LoseRange = 12f;
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
            }));
            _rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            _rig.CreateTargetAt(new Vector3(24f, 0f, 0f));

            Assert.IsTrue(StepUntilDoing("Patrol", "Patrol"), Doing());
            _rig.StepSeconds(0.3f);

            Assert.AreEqual("Patrol", _rig.Instance.Decision?.DisplayName, "Beyond its sight the target is not known");
        }

        [Test]
        public void RangedKiter_TargetTooClose_BacksOff_ThenShootsFromTheBand()
        {
            var tuning = new BehaviorPresets.Tuning { KeepMin = 4f, KeepMax = 8f };
            TestAbilityRunner shot = Give(tuning, "Shot", duration: 0.2f, maxRange: tuning.KeepMax);
            _rig.SetGraph(BehaviorPresets.RangedKiter(tuning));
            Transform target = _rig.CreateTargetAt(new Vector3(1f, 0f, 0f));

            Assert.IsTrue(StepUntilDoing(FIGHT, "Kite"), Doing());
            float before = _rig.Actuator.Position.x;
            _rig.StepSeconds(0.3f);
            Assert.Less(_rig.Actuator.Position.x, before, "Too close: the agent must give ground");
            Assert.AreEqual(0, shot.Starts, "…before it shoots");

            Assert.IsTrue(_rig.StepUntil(() => shot.Starts > 0, 5f), "Back in the band, it shoots: " + Doing());
            Assert.AreEqual("Use Ability", _rig.StateName);
            Assert.GreaterOrEqual(Vector3.Distance(_rig.Actuator.Position, target.position), tuning.KeepMin,
                "It shoots from the band, not from too close");
        }

        [Test]
        public void RangedKiter_TargetInBand_Shoots()
        {
            var tuning = new BehaviorPresets.Tuning { KeepMin = 4f, KeepMax = 8f };
            TestAbilityRunner shot = Give(tuning, "Shot", duration: 0.2f, maxRange: tuning.KeepMax);
            _rig.SetGraph(BehaviorPresets.RangedKiter(tuning));
            _rig.CreateTargetAt(new Vector3(6f, 0f, 0f));

            Assert.IsTrue(StepUntilDoing(FIGHT, "Use Ability"), Doing());

            Assert.AreEqual(1, shot.Starts);
            Assert.AreEqual(0f, _rig.Actuator.Position.x, 1e-3f, "In the band already: it shoots from where it stands");
        }

        [Test]
        public void PatrolGuard_SeesTarget_TurnsToIt_AndHoldsItsGroundWithoutChasing()
        {
            var tuning = new BehaviorPresets.Tuning();
            _rig.SetGraph(BehaviorPresets.PatrolGuard(tuning));
            Transform target = _rig.CreateTargetAt(new Vector3(-6f, 0f, 0f));     // out of reach, off its nose

            Assert.IsTrue(StepUntilDoing("Watch", "Turn To Target"), Doing());
            Vector3 standsAt = _rig.Actuator.Position;
            Assert.IsTrue(StepUntilDoing("Watch", "Stand", 2f), "The turn done, it stands: " + Doing());
            Assert.Less(Vector3.Angle(_rig.Actuator.Facing, target.position - standsAt), 1f, "It has turned to the target");

            _rig.StepSeconds(2f);

            Assert.AreEqual("Watch", _rig.Instance.Decision?.DisplayName, "Out of reach it only watches");
            Assert.AreEqual(0f, Vector3.Distance(standsAt, _rig.Actuator.Position), 1e-3f, "A guard holds its ground");
        }

        [Test]
        public void Sentry_StandsWithNoTarget_TargetInBand_Fires()
        {
            var tuning = new BehaviorPresets.Tuning { KeepMin = 2f, KeepMax = 10f };
            TestAbilityRunner gun = Give(tuning, "Gun", duration: 0.2f, maxRange: tuning.KeepMax);
            _rig.SetGraph(BehaviorPresets.Sentry(tuning));
            Assert.IsTrue(StepUntilDoing("Daily Routine", "Stand"), "Nothing to fire at: it stands — " + Doing());

            _rig.CreateTargetAt(new Vector3(5f, 0f, 0f));

            Assert.IsTrue(StepUntilDoing(FIGHT, "Use Ability", 1f), Doing());
            Assert.AreEqual(1, gun.Starts);
            Assert.AreEqual(0f, _rig.Actuator.Position.x, 1e-3f, "A sentry does not move");
        }

        [Test]
        public void Patrol_WithoutRoute_PacesAroundWhereItStarted()
        {
            _rig.SetGraph(BehaviorPresets.MeleeChaser(new BehaviorPresets.Tuning { PauseAtPoint = 0f }));
            Vector3 start = _rig.Actuator.Position;
            float farthest = 0f;

            for (int i = 0; i < 100; i++)
            {
                _rig.Step();
                farthest = Mathf.Max(farthest, Vector3.Distance(start, _rig.Actuator.Position));
            }

            Assert.AreEqual("Patrol", _rig.StateName, Doing());
            Assert.Greater(farthest, 0.1f, "An agent without a route still moves");
            Assert.Less(farthest, 5f, "…but it stays around its spot");
        }

        [Test]
        public void Trace_RecordsTheWireTheBrainTook()
        {
            var tuning = new BehaviorPresets.Tuning();
            Give(tuning, "Melee", maxRange: REACH);
            _rig.SetGraph(BehaviorPresets.MeleeChaser(tuning));
            _rig.CreateTargetAt(new Vector3(5f, 0f, 0f));
            Assert.IsTrue(StepUntilDoing("Chase", "Chase"), Doing());

            _rig.DestroyTarget();
            Assert.IsTrue(StepUntilDoing("Patrol", "Patrol", 0.2f), Doing());

            DecisionTrace trace = _rig.Context.Trace;
            bool wireToPatrol = false;
            for (int i = 0; i < trace.Count; i++)
            {
                Transition wire = trace[i].Transition;
                wireToPatrol |= trace[i].Kind == DecisionKind.Transition && wire != null &&
                                wire.Target?.Reasoner is PatrolReasoner;
            }

            Assert.IsTrue(wireToPatrol, "The decision trace is what makes AI debuggable: it names the wire taken");
        }

        // ---------- the rig ----------

        // Steps until the Brain has taken the decision and its chain is in the block (a decision and its first block may
        // share a title: Chase, Patrol)
        private bool StepUntilDoing(string decision, string block, float seconds = DECIDE) =>
            _rig.StepUntil(() => _rig.Instance.Decision?.DisplayName == decision &&
                                 _rig.Instance.Current != null && _rig.Instance.Current.Reasoner == null &&
                                 _rig.StateName == block, seconds);

        private string Doing() =>
            $"decision '{_rig.Instance?.Decision?.DisplayName ?? "none"}', node '{_rig.StateName ?? "none"}'";
    }
}
