using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// The character's own say about itself (ICharacterStatus) in its corners (step-90 recheck): the game's health found
    /// late or replaced, a health with no maximum, a weapon taken from the world, a jump on a body that says it stands,
    /// a body with no run speed, and a NavMesh body walking at a pace.
    /// </summary>
    public sealed class SensesStatusTests
    {
        private readonly Things _things = new();

        [TearDown]
        public void TearDown()
        {
            _things.DestroyAll();
            GraphRig.DestroyAll();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Status_HealthAddedAfterTheFirstAsk_FoundAfterOneSecondOfTheAgentsClock_ADestroyedOneAtOnce()
        {
            var rig = new GraphRig(trace: false);
            Assert.IsFalse(rig.Agent.TryGetStat(CharacterStat.Health, out _), "No health of the game yet: no data");

            StatusTests.Health health = rig.Agent.gameObject.AddComponent<StatusTests.Health>();
            health.Current = 3f;
            health.Max = 4f;
            rig.Context.Advance(0.5f);
            Assert.IsFalse(rig.Agent.TryGetStat(CharacterStat.Health, out _),
                "Half a second later: not looked for again yet (at most once a second while there is none)");

            rig.Context.Advance(0.5f);
            Assert.IsTrue(rig.Agent.TryGetStat(CharacterStat.Health, out float found), "A second of its clock: looked for, found");
            Assert.AreEqual(3f, found);

            Object.DestroyImmediate(health);
            StatusTests.Health replaced = rig.Agent.gameObject.AddComponent<StatusTests.Health>();
            replaced.Current = 5f;
            replaced.Max = 5f;
            Assert.IsTrue(rig.Agent.TryGetStat(CharacterStat.Health, out float again), "A destroyed one is looked for again at once");
            Assert.AreEqual(5f, again);
        }

        [Test]
        public void Status_HealthPercentWithNoMax_NoData_DeadAtZeroOrBelow()
        {
            var rig = new GraphRig(trace: false);
            StatusTests.Health health = rig.Agent.gameObject.AddComponent<StatusTests.Health>();
            health.Current = 3f;
            health.Max = 0f;

            Assert.IsTrue(rig.Agent.TryGetStat(CharacterStat.Health, out float current));
            Assert.AreEqual(3f, current);
            Assert.IsTrue(rig.Agent.TryGetStat(CharacterStat.MaxHealth, out float max), "A maximum of 0 is said");
            Assert.AreEqual(0f, max);
            Assert.IsFalse(rig.Agent.TryGetStat(CharacterStat.HealthPercent, out _), "A share of nothing: no data");
            Assert.IsTrue(rig.Agent.TryGetState(CharacterState.Dead, out bool dead));
            Assert.IsFalse(dead);

            health.Current = -1f;
            health.Max = 4f;
            Assert.IsTrue(rig.Agent.TryGetStat(CharacterStat.HealthPercent, out float percent));
            Assert.AreEqual(-25f, percent, 1e-4f, "Below zero is said as it is");
            Assert.IsTrue(rig.Agent.TryGetState(CharacterState.Dead, out dead));
            Assert.IsTrue(dead, "At zero or below: dead");
        }

        [Test]
        public void Status_ARockFromTheWorld_UsesLeftAndHoldsPickedWeapon_ThroughGetAndIs()
        {
            var rig = new GraphRig(trace: false);
            WeaponAsset rock = _things.Keep(WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 12f }));
            rock.Uses = 1;
            Transform model = _things.Make("Rock", Vector3.zero);
            Assume.That(rig.Agent.TakeUpFromWorld(rock, model, 0, false), Is.True, "Set-up: the rock is in the hand");

            Assert.IsTrue(new GetValue(GetParameter.UsesLeft).TryGetNumber(rig.Context, out float left));
            Assert.AreEqual(1f, left, "One throw in it");
            Assert.AreEqual(Truth.Yes, new IsCondition(CharacterState.HoldsPickedWeapon).Ask(rig.Context));
            Assert.AreEqual(Truth.Yes, new IsCondition(CharacterState.Armed).Ask(rig.Context));

            rig.Agent.SpendUse();

            Assert.IsFalse(new GetValue(GetParameter.UsesLeft).TryGetNumber(rig.Context, out _), "Spent: nothing to count");
            Assert.AreEqual(Truth.No, new IsCondition(CharacterState.HoldsPickedWeapon).Ask(rig.Context), "Let go of");
        }

        [Test]
        public void Status_InAir_DuringAJumpPhase_EvenWhenTheBodySaysGrounded()
        {
            var body = new JumpingBody();
            body.Mover.IsGrounded = true;
            AICharacter character = Character(body);

            foreach (JumpPhase phase in new[] { JumpPhase.TakeOff, JumpPhase.InAir, JumpPhase.Landing })
            {
                body.Phase = phase;
                Assert.IsTrue(character.TryGetState(CharacterState.InAir, out bool inAir));
                Assert.IsTrue(inAir, $"{phase}: in the air, whatever the ground probe says");
            }

            body.Phase = JumpPhase.None;
            Assert.IsTrue(character.TryGetState(CharacterState.InAir, out bool standing));
            Assert.IsFalse(standing, "No jump, on the ground: not in the air");
        }

        [Test]
        public void Status_NoRunSpeedAnywhere_RunningIsNoData_MovingStillAnswered()
        {
            var rig = new GraphRig(trace: false, actuator: new TestActuator { Speed = 0f });

            Assert.IsFalse(rig.Agent.TryGetStat(CharacterStat.RunSpeed, out _), "No number of it, no top speed: no data");
            Assert.IsFalse(rig.Agent.TryGetState(CharacterState.Running, out _), "Running needs a run speed: no data");
            Assert.IsTrue(rig.Agent.TryGetState(CharacterState.Moving, out bool moving), "Moving is measured: answered");
            Assert.IsFalse(moving);
        }

        [Test]
        public void Status_NavMeshBody_RunSpeedIsTheFullSpeedWhileAPaceIsSet()
        {
            LogAssert.ignoreFailingMessages = true;          // a NavMeshAgent without a baked mesh complains
            AICharacter character = NavMeshCharacter(out NavMeshBody body, out NavMeshAgent agent);
            float full = agent.speed;
            body.Pathing.Pace = 0.4f;
            Assume.That(agent.speed, Is.EqualTo(full * 0.4f).Within(1e-4f), "Set-up: it walks at a pace");

            Assert.IsTrue(character.TryGetStat(CharacterStat.RunSpeed, out float run));
            Assert.AreEqual(full, run, 1e-4f, "Its run speed is the full speed, not the pace it walks at");
            Assert.IsTrue(character.TryGetStat(CharacterStat.TurnSpeed, out float turn));
            Assert.AreEqual(body.TurnSpeed, turn, 1e-4f);
            Assert.IsTrue(character.TryGetStat(CharacterStat.Acceleration, out float acceleration));
            Assert.AreEqual(agent.acceleration, acceleration, 1e-4f);
            Assert.IsTrue(character.TryGetState(CharacterState.Running, out bool running));
            Assert.IsFalse(running, "Walking at the pace is no run (below 0.7 of the full speed)");
        }

        // ---------- helpers ----------

        private AICharacter Character(AgentBody body)
        {
            var go = _things.Keep(new GameObject("Bodied") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            character.Body = body;
            character.Initialize();
            return character;
        }

        private AICharacter NavMeshCharacter(out NavMeshBody body, out NavMeshAgent agent)
        {
            var go = _things.Keep(new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            agent = go.AddComponent<NavMeshAgent>();
            body = new NavMeshBody();
            var character = go.AddComponent<AICharacter>();
            character.Body = body;
            character.Initialize();
            return character;
        }

        // A body whose jump the test sets, standing on the ground all the while
        private sealed class JumpingBody : AgentBody
        {
            public readonly TestActuator Mover = new();
            public JumpPhase Phase;

            public override void Attach(Transform owner)
            {
            }

            public override IAgentActuator Actuator => Mover;

            public override JumpPhase Jump => Phase;
        }
    }
}
