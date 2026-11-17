using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Each decision of the Brain on its own: when it holds, and the reason it gives when it does not — the line the
    /// canvas shows under it in Play. The facts about the target are handed in, as the Brain hands them in once per
    /// think (handbook §3.2), so every case is one call.
    /// </summary>
    public sealed class ReasonerTests
    {
        private const string GRENADE = "Grenade";
        private const string TIMER = "Grenade Timer";
        private const float MIN_RANGE = 4f;
        private const float MAX_RANGE = 9f;

        private GraphRig _rig;
        private Transform _target;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _target = _rig.CreateTargetAt(new Vector3(3f, 0f, 0f));
        }

        [TearDown]
        public void TearDown() => GraphRig.DestroyAll();

        // ---------- the checks every decision shares ----------

        [Test]
        public void EveryDecisionButPatrol_NoTarget_DoesNotHold_AndSaysSo()
        {
            ReasonerTask[] needTarget =
            {
                new FightReasoner(), new UseAbilityReasoner(), new ChaseReasoner(), new FlankReasoner(), new CircleReasoner()
            };

            foreach (ReasonerTask decision in needTarget)
            {
                Assert.IsFalse(decision.Holds(Query(TargetFacts.None), out string why), decision.GetType().Name);
                Assert.AreEqual("no target", why, decision.GetType().Name);
            }

            Assert.IsTrue(new PatrolReasoner().Holds(Query(TargetFacts.None), out string none), "Patrol holds always");
            Assert.IsNull(none);
        }

        [Test]
        public void NotFrom_BlocksOnlyFromTheListedDecisions()
        {
            var fight = new AgentState("Fight") { Id = 6 };
            var chase = new AgentState("Chase") { Id = 10 };
            var grenade = new UseAbilityReasoner();
            grenade.NotFrom.Add(fight.Id);

            Assert.IsTrue(grenade.IsBlockedFrom(fight));
            Assert.IsFalse(grenade.IsBlockedFrom(chase));
            Assert.IsFalse(grenade.IsBlockedFrom(null), "From no decision — the Brain is free to take it");
        }

        // ---------- Fight ----------

        [Test]
        public void Fight_TakenOnlyCloserThanCloser_ButLetGoOnlyFartherThanLetGoAfter()
        {
            var fight = new FightReasoner { Closer = 1.9f, LetGoAfter = 2.2f };

            Assert.IsTrue(fight.Holds(Query(Facts(1.9f)), out _), "At the threshold it is taken");
            Assert.IsFalse(fight.Holds(Query(Facts(2f)), out string far), "Between the two thresholds it is not taken…");
            Assert.AreEqual("target 2.0 m, needs 1.9 or closer", far);
            Assert.IsTrue(fight.Holds(Query(Facts(2f), current: true), out _), "…but kept while the agent fights");
            Assert.IsTrue(fight.Holds(Query(Facts(2.2f), current: true), out _));
            Assert.IsFalse(fight.Holds(Query(Facts(2.21f), current: true), out string gone), "Farther than Let Go After — let go");
            StringAssert.Contains("needs 2.2 or closer", gone);
        }

        [Test]
        public void Fight_LetGoAfterBelowCloser_UsesCloserForBoth()
        {
            var fight = new FightReasoner { Closer = 2f, LetGoAfter = 1f };

            Assert.IsTrue(fight.Holds(Query(Facts(2f), current: true), out _), "The far threshold is never nearer than the near one");
        }

        [Test]
        public void Attack_HoldsWithoutAPlaceCheckOfItsOwn_TheWiredAttackSlotSaysIt_AndTheVerdictNamesIt()
        {
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world);
            Transform target = rig.CreateTargetAt(new Vector3(1f, 0f, 0f));
            var facts = new TargetFacts(target, 1f);

            Assert.IsTrue(new FightReasoner().HoldsWithCondition(new ReasonerQuery(rig.Context, facts, false), out _),
                "No checkbox any more: nothing wired, nothing extra asked");

            var attack = new FightReasoner { Condition = new AttackSlotCondition(AttackSlotIs.Mine) };
            Assert.IsFalse(attack.HoldsWithCondition(new ReasonerQuery(rig.Context, facts, false), out string why),
                "A group, no place given: the wired condition says no");
            Assert.AreEqual("only if my attack slot — no", why);
            Assert.IsNull(world.Roster.FindEngagement(target), "The question made no engagement");
        }

        [Test]
        public void Attack_CloserZero_IsTakenWithinMyWeaponsReach_AndWithoutAWeaponAtTheOldDefault()
        {
            const float REACH = 3.5f;
            var attack = new FightReasoner { Closer = 0f, LetGoAfter = 0f };

            Assert.IsTrue(attack.Holds(Query(Facts(FightReasoner.NO_WEAPON_CLOSER)), out _), "No weapon: the old 1.9 m");
            Assert.IsFalse(attack.Holds(Query(Facts(FightReasoner.NO_WEAPON_CLOSER + 0.1f)), out _));

            _rig.GiveWeapon(REACH, 0.5f, 0.5f);
            Assert.IsTrue(attack.Holds(Query(Facts(REACH)), out _), "The reach of the weapon in the hand");
            Assert.IsFalse(attack.Holds(Query(Facts(REACH + 0.1f)), out string why));
            StringAssert.Contains("my weapon's reach", why);
            Assert.IsFalse(attack.Holds(Query(Facts(REACH + 0.1f), current: true), out _), "Let Go After 0: no slack");
            StringAssert.Contains("my weapon's reach", attack.Requirement, "The node says so");
            Assert.IsNull(new FightReasoner { Closer = 2f }.Requirement, "A number of its own: nothing to say");
        }

        // ---------- Use Ability ----------

        [Test]
        public void UseAbility_TheAbilityIsNotPreparedOnTheCharacter_DoesNotHold()
        {
            Assert.IsFalse(Grenade().Holds(Query(Facts(5f)), out string why));
            Assert.AreEqual($"{GRENADE} is not prepared on the character", why);
        }

        [Test]
        public void UseAbility_NoAbilityOnTheNode_DoesNotHold_AndSaysSo()
        {
            Assert.IsFalse(new UseAbilityReasoner().Holds(Query(Facts(5f)), out string why));
            Assert.AreEqual("no ability chosen", why);
        }

        [Test]
        public void UseAbility_TimerNotWired_TheAbilitysOwnPauseFromItsEnd()
        {
            GiveGrenade();
            var grenade = new UseAbilityReasoner { Ability = GraphRig.Named(GRENADE) };
            Assert.IsTrue(grenade.Holds(Query(Facts(5f)), out _), "Rested: the ability's own pause is over");

            Assert.IsTrue(_rig.Abilities.TryUse(GRENADE, _rig.Context));
            _rig.Tick(0.02f);                                   // an instant one: over, its pause starts from its end

            Assert.IsFalse(grenade.Holds(Query(Facts(5f)), out string why), "No wire: the ability's Cooldown is the pause");
            Assert.AreEqual("rests 6.0 s", why);
        }

        [Test]
        public void UseAbility_HoldsOnlyWithinTheCardsRange()
        {
            GiveGrenade();
            _rig.Context.Blackboard.Set(TIMER, 0f);
            UseAbilityReasoner grenade = Grenade();

            Assert.IsTrue(grenade.Holds(Query(Facts(MIN_RANGE)), out _));
            Assert.IsTrue(grenade.Holds(Query(Facts(MAX_RANGE)), out _));
            Assert.IsFalse(grenade.Holds(Query(Facts(MIN_RANGE - 0.5f)), out string near));
            Assert.AreEqual("target 3.5 m, needs 4–9", near);
            Assert.IsFalse(grenade.Holds(Query(Facts(MAX_RANGE + 0.5f)), out _));
        }

        [Test]
        public void UseAbility_WhileTheAbilityPlays_TheTakenDecisionHolds_WhateverTheTimerSays()
        {
            TestAbilityRunner runner = GiveGrenade(duration: 1f);
            runner.Begin(_rig.Context);                   // the throw has started
            _rig.Context.Blackboard.Set(TIMER, 6f);      // and the chain has set the pause already

            Assert.IsTrue(runner.IsRunning);
            Assert.IsTrue(Grenade().Holds(Query(Facts(2f), current: true), out _), "The throw runs to its end");
            Assert.IsFalse(Grenade().Holds(Query(Facts(2f)), out _), "Another agent's choice is not kept by it");
        }

        [Test]
        public void UseAbility_TheBodyCannotStartIt_DoesNotHold_BeforeTheChoice()
        {
            TestAbilityRunner runner = GiveGrenade();
            _rig.Context.Blackboard.Set(TIMER, 0f);
            runner.Blocked = true;

            Assert.IsFalse(Grenade().Holds(Query(Facts(5f)), out string why), "Taken now, it could not act: not taken at all");
            Assert.AreEqual("the body is busy", why);

            runner.Blocked = false;
            Assert.IsTrue(Grenade().Holds(Query(Facts(5f)), out _));
        }

        [Test]
        public void UseAbility_ItsPoolHasNoFreePlace_DoesNotHold()
        {
            const string POOL = "Grenades";
            GiveGrenade();
            AbilityDefinition card = _rig.Abilities.DefinitionOf(GRENADE);
            card.TokenPool = POOL;
            card.TokenCapacity = 1;
            _rig.Context.Blackboard.Set(TIMER, 0f);
            var other = new GraphRig(trace: false, world: _rig.Agent.World);
            AttackTokens tokens = AttackSlots.EngagementOf(_rig.Context, _target).Tokens;
            tokens.SetCapacity(POOL, 1);
            tokens.Request(other.Agent, POOL, 1, 0f, 1f);
            other.Agent.gameObject.SetActive(true);
            tokens.Step(0.02f);
            Assert.IsTrue(tokens.Holds(other.Agent, POOL), "Somebody else throws now");

            Assert.IsFalse(Grenade().Holds(Query(Facts(5f)), out string why), "Its turn would only come later: it circles meanwhile");
            Assert.AreEqual("no free place to use Grenade", why);
        }

        // ---------- Chase, Flank, Circle ----------

        [Test]
        public void ChaseFlankCircle_HoldOnSight_WhoTakesWhichIsTheirWiredCondition()
        {
            foreach (ReasonerTask decision in new ReasonerTask[] { new ChaseReasoner(), new FlankReasoner(), new CircleReasoner() })
            {
                Assert.IsTrue(decision.Holds(Query(Facts(6f)), out _), decision.GetType().Name);
                Assert.IsFalse(decision.Holds(Query(TargetFacts.None), out string why), decision.GetType().Name);
                Assert.AreEqual("no target", why);
            }
        }

        // ---------- the facts ----------

        [Test]
        public void TargetFacts_SayWhatTheBrainSees_HowFarAndHowLong()
        {
            TargetFacts facts = TargetFacts.Of(_rig.Context, _target, 1.5f);

            Assert.IsTrue(facts.Sees);
            Assert.AreEqual(3f, facts.Distance, 1e-4f);
            Assert.AreEqual(1.5f, facts.SeenFor, 1e-4f);
        }

        [Test]
        public void TargetFacts_NoTarget_IsNone_Far()
        {
            TargetFacts facts = TargetFacts.Of(_rig.Context, null);

            Assert.IsFalse(facts.Sees);
            Assert.AreEqual(SensorTask.FAR, facts.Distance);
            Assert.AreEqual(0f, facts.SeenFor);
        }

        private ReasonerQuery Query(TargetFacts facts, bool current = false) => new(_rig.Context, facts, current);

        // The target stands where the facts say: what the card asks itself (the range, the line) is measured for real
        private TargetFacts Facts(float distance)
        {
            _target.position = new Vector3(distance, 0f, 0f);
            return new TargetFacts(_target, distance);
        }

        private static UseAbilityReasoner Grenade() =>
            new() { Ability = GraphRig.Named(GRENADE), Condition = new CompareCondition(TIMER, NumberComparison.LessOrEqual, 0f) };

        private TestAbilityRunner GiveGrenade(float duration = 0f)
        {
            _rig.Abilities.Add(new AbilityDefinition
            {
                Id = GRENADE, Range = new RangeBand(MIN_RANGE, MAX_RANGE), Cooldown = 6f, RequiresGround = false
            });
            var runner = new TestAbilityRunner(GRENADE, duration);
            _rig.Abilities.Register(runner);
            return runner;
        }
    }
}
