using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The attack tokens and the places of abilities, at their corners (the step 90 recheck of the fight): abilities that
    /// share a pool share its turns whatever order they ask in — the strictest of them counts; an ability's pool reaches
    /// as far as the ability, the others keep the common radius; a canvas node waits a moment for its turn and takes its
    /// request back; a pool held firmly is kept, a request not renewed is dropped, Release Attack Slot gives back the
    /// places to fight from and nothing else, a capacity set in code overrides the target until 0, a withdrawn request
    /// is gone while a granted token stays; a fight round what the game no longer counts as a target ends; a target out
    /// of the world hears no blow.
    /// </summary>
    public sealed class CombatTokensTests
    {
        private const string TARGET = "Target";
        private const string POOL = "Melee";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();

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

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        // An ability that reaches so far, plays 0.2 + 0.2 s, in a pool shared by name, so many at once
        private static AbilityAsset Thrown(string name, string pool, int atOnce, float reach = 9f)
        {
            AbilityAsset ability = GraphRig.MakeAbility(name, new RangeCondition { Min = 0f, Max = reach },
                new StrikeDelivery { Reach = reach });
            ability.Timing = new AbilityTiming { WindUp = 0.2f, Recovery = 0.2f };
            ability.Queue.Pool = pool;
            ability.Queue.AtOnce = atOnce;
            return ability;
        }

        // An awake agent of the world whose graph names the ability (its runner is prepared), and the block asking for it
        private static GraphRig Thrower(AIWorld world, Vector3 at, Transform target, AbilityAsset ability,
            out UseAbilityTask use)
        {
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            use = new UseAbilityTask(ability) { Target = TARGET };
            rig.SetGraph(GraphRig.OneState(use));
            rig.Agent.Blackboard.Set(TARGET, target);
            return rig;
        }

        // ---------- abilities of one pool ----------

        [Test]
        public void AbilityPlaces_TwoAbilitiesOfOnePool_TheCapacityDoesNotFollowTheLastAsk()
        {
            const string EXPLOSIVES = "Explosives";
            var world = new AIWorld();
            Transform target = Thing("Target", new Vector3(0f, 0f, 3f));
            AbilityAsset grenade = Thrown("Grenade", EXPLOSIVES, 1);
            AbilityAsset firebomb = Thrown("Firebomb", EXPLOSIVES, 2);
            GraphRig a = Thrower(world, new Vector3(-1f, 0f, 0f), target, grenade, out UseAbilityTask useA);
            GraphRig b = Thrower(world, new Vector3(1f, 0f, 0f), target, firebomb, out UseAbilityTask useB);
            GraphRig c = Thrower(world, new Vector3(0f, 0f, -1f), target, grenade, out UseAbilityTask useC);

            useA.Tick(a.Context, DT);
            Engagement engagement = world.Roster.FindEngagement(target);
            int afterGrenade = engagement.Tokens.CapacityOf(EXPLOSIVES);
            useB.Tick(b.Context, DT);
            int afterFirebomb = engagement.Tokens.CapacityOf(EXPLOSIVES);

            Assert.AreEqual(afterGrenade, afterFirebomb, "The pool's capacity does not follow whoever asked last");
            Assert.AreEqual(1, afterFirebomb, "A grenade and a firebomb share the pool: one thrower at a time");

            int most = 0;
            var rigs = new[] { a, b, c };
            var uses = new[] { useA, useB, useC };
            for (int step = 0; step < 100; step++)
            {
                world.BeginStep(DT);
                for (int i = 0; i < rigs.Length; i++)
                {
                    rigs[i].Tick(DT);
                    uses[i].Tick(rigs[i].Context, DT);
                }

                most = Mathf.Max(most, engagement.Tokens.Used(EXPLOSIVES));
            }

            Assert.AreEqual(1, most, "Never two throwers of the pool at once, whichever asked last");
        }

        [Test]
        public void AbilityPlaces_RangeTwelve_ItsPoolReachesTwelve_AskAtNineMetresWaitsNotRefused()
        {
            const float RANGE = 12f;
            var world = new AIWorld();
            float win = new AttackTokens(null).WinRadius;
            Transform target = Thing("Target", new Vector3(0f, 0f, 9f));
            Assume.That(9f, Is.GreaterThan(win), "Set-up: farther than the common radius, within the bolt's range");
            GraphRig rig = Thrower(world, Vector3.zero, target, Thrown("Bolt", "", 1, RANGE), out UseAbilityTask use);

            Assert.AreEqual(TaskStatus.Running, use.Tick(rig.Context, DT), "Filed: 9 m is within its 12 m");
            AttackTokens tokens = world.Roster.FindEngagement(target).Tokens;
            Assert.AreEqual(RANGE, tokens.ReachOf("Bolt"), 1e-4f, "Its pool reaches as far as the ability");
            Assert.AreEqual(win, tokens.WinRadius, 1e-4f, "The other pools of the target keep theirs");
            Assert.AreEqual(TokenResult.Refused, tokens.Request(rig.Agent, "Melee", 1, 0f, 9f), "Another pool at 9 m: refused");

            world.BeginStep(DT);
            Assert.AreEqual(TaskStatus.Running, use.Tick(rig.Context, DT));
            Assert.AreEqual(1, rig.Agent.AbilityRunners[0].Uses, "Its turn came: it plays");
        }

        [Test]
        public void UseAbilityTask_NoPlaceWithinAQuarterSecond_NotUsed_RequestWithdrawn()
        {
            var world = new AIWorld();
            Transform target = Thing("Target", new Vector3(0f, 0f, 3f));
            GraphRig rig = Thrower(world, new Vector3(-1f, 0f, 0f), target, Thrown("Grenade", "", 1), out UseAbilityTask use);
            GraphRig other = Member(world);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;
            tokens.LimitCapacity("Grenade", 1);
            tokens.Request(other.Agent, "Grenade", 1, 0f, 1f);
            world.BeginStep(DT);
            Assume.That(tokens.Holds(other.Agent, "Grenade"), "Set-up: the one turn is another's");

            float failedAt = -1f;
            for (int i = 1; i <= 50 && failedAt < 0f; i++)
            {
                world.BeginStep(DT);
                if (use.Tick(rig.Context, DT) == TaskStatus.Failure)
                    failedAt = i * DT;
            }

            Assert.That(failedAt, Is.InRange(0.24f, 0.28f), "A canvas node does not stand in a queue: not used, a moment on");
            Assert.AreEqual(0, rig.Agent.AbilityRunners[0].Uses);
            Assert.IsFalse(tokens.Holds(rig.Agent, "Grenade"));
            tokens.Release(other.Agent, "Grenade");
            world.BeginStep(DT);
            Assert.IsFalse(tokens.Holds(rig.Agent, "Grenade"), "Its request was taken back: the freed turn is not given to it");
        }

        // ---------- the tokens ----------

        // An awake member of the world (the roster steps its requests)
        private static GraphRig Member(AIWorld world, Vector3 at = default)
        {
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            return rig;
        }

        private Transform SlotsTarget(int slots, out AttackSlotCapacity capacity)
        {
            Transform target = Thing("Target", Vector3.zero);
            capacity = target.gameObject.AddComponent<AttackSlotCapacity>();
            capacity.Slots = slots;
            return target;
        }

        [Test]
        public void AttackTokens_FirmPool_ABetterRequestWaits_TheHolderKeeps()
        {
            var world = new AIWorld();
            Transform target = SlotsTarget(1, out _);
            GraphRig a = Member(world), b = Member(world);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;
            tokens.HoldFirmly(POOL);

            tokens.Request(a.Agent, POOL, 1, 0f, 5f);
            world.Roster.Step(DT);
            Assume.That(tokens.Holds(a.Agent, POOL), "Set-up: the first one holds it");
            tokens.Request(b.Agent, POOL, 1, 10f, 1f);           // better in every way: nearer, in reach, more urgent
            world.Roster.Step(DT);

            Assert.IsTrue(tokens.Holds(a.Agent, POOL), "Held firmly: kept until given back");
            Assert.IsFalse(tokens.Holds(b.Agent, POOL), "The better one waits");
        }

        [Test]
        public void AttackTokens_RequestNotRenewedForHalfASecond_Dropped_NotGrantedLater()
        {
            var world = new AIWorld();
            Transform target = SlotsTarget(0, out AttackSlotCapacity capacity);
            GraphRig a = Member(world);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;

            tokens.Request(a.Agent, POOL, 1, 0f, 1f);
            for (int i = 0; i < Mathf.CeilToInt((AttackTokens.STALE_AFTER + DT) / DT); i++)
                world.Roster.Step(DT);
            capacity.Slots = 1;
            world.Roster.Step(DT);

            Assert.IsFalse(tokens.Holds(a.Agent, POOL), "Nobody asked again: it is not granted to one gone elsewhere");
            CollectionAssert.IsEmpty(tokens.Pools.ToList(), "The request is gone");
        }

        [Test]
        public void AttackTokens_ReleasePlaces_GivesBackNearestGroups_NotAnAbilityPool()
        {
            var world = new AIWorld();
            Transform target = SlotsTarget(2, out _);
            GraphRig a = Member(world, new Vector3(1f, 0f, 0f));
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;
            tokens.NearestHolds(AttackSlots.POOL, AttackSlots.SWAP_MARGIN);

            void HoldBoth()
            {
                tokens.Request(a.Agent, AttackSlots.POOL, 1, 0f, 1f);
                tokens.Request(a.Agent, "Grenade", 1, 0f, 1f);
                world.Roster.Step(DT);
                Assume.That(tokens.Holds(a.Agent, AttackSlots.POOL) && tokens.Holds(a.Agent, "Grenade"),
                    "Set-up: a place to fight from and a grenade's turn");
            }

            HoldBoth();
            tokens.ReleasePlaces(a.Agent);
            Assert.IsFalse(tokens.Holds(a.Agent, AttackSlots.POOL), "The place to fight from goes back");
            Assert.IsTrue(tokens.Holds(a.Agent, "Grenade"), "An ability's turn is no place to fight from: it stays");

            tokens.Release(a.Agent, "Grenade");
            HoldBoth();
            new ReleaseAttackSlotTask().Tick(a.Context, DT);
            Assert.IsFalse(tokens.Holds(a.Agent, AttackSlots.POOL), "The node gives back the place the same way");
            Assert.IsTrue(tokens.Holds(a.Agent, "Grenade"));
        }

        [Test]
        public void AttackTokens_SetCapacity_OverridesTheTarget_ZeroGivesItBack()
        {
            var world = new AIWorld();
            Transform target = SlotsTarget(1, out _);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;

            tokens.SetCapacity(POOL, 3);
            Assert.AreEqual(3, tokens.CapacityOf(POOL), "Whatever the target says");
            tokens.SetCapacity(POOL, 0);
            Assert.AreEqual(1, tokens.CapacityOf(POOL), "0: the target's say again");
        }

        [Test]
        public void AttackTokens_LimitCapacity_TheStrictestCounts_InAnyOrder()
        {
            var world = new AIWorld();
            Transform target = SlotsTarget(5, out _);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;

            tokens.LimitCapacity(POOL, 2);
            tokens.LimitCapacity(POOL, 1);
            tokens.LimitCapacity(POOL, 3);
            tokens.LimitCapacity(POOL, 0);

            Assert.AreEqual(1, tokens.CapacityOf(POOL), "The strictest of those who named it, whatever the order");
            Assert.AreEqual(5, tokens.CapacityOf("Ranged"), "Another pool: the target's say");
        }

        [Test]
        public void AttackTokens_ReachFor_ItsPoolIsNotRefusedFromThatFar_TheOthersKeepWinRadius()
        {
            var world = new AIWorld();
            Transform target = SlotsTarget(5, out _);
            GraphRig a = Member(world), b = Member(world);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;
            float win = tokens.WinRadius;

            tokens.ReachFor("Bolt", win + 4f);
            tokens.ReachFor("Bolt", 2f);                         // a shorter one does not shorten it
            tokens.ReachFor("Heal", float.MaxValue);             // no range part: from anywhere

            Assert.AreEqual(win + 4f, tokens.ReachOf("Bolt"), 1e-4f);
            Assert.AreEqual(TokenResult.Pending, tokens.Request(a.Agent, "Bolt", 1, 0f, win + 3f), "Within its reach");
            Assert.AreEqual(TokenResult.Refused, tokens.Request(b.Agent, POOL, 1, 0f, win + 3f), "Another pool: WinRadius");
            Assert.AreEqual(TokenResult.Pending, tokens.Request(b.Agent, "Heal", 1, 0f, 1000f), "From anywhere");
        }

        [Test]
        public void AttackTokens_Withdraw_DropsAWaitingRequest_KeepsAGrantedToken()
        {
            var world = new AIWorld();
            Transform target = SlotsTarget(1, out AttackSlotCapacity capacity);
            GraphRig holder = Member(world), waiter = Member(world);
            AttackTokens tokens = world.Roster.EngagementOf(target).Tokens;
            tokens.Request(holder.Agent, POOL, 1, 0f, 1f);
            world.Roster.Step(DT);
            tokens.Request(waiter.Agent, POOL, 1, 0f, 1f);
            Assume.That(tokens.Holds(holder.Agent, POOL), "Set-up: one holds, one waits");

            tokens.Withdraw(waiter.Agent, POOL);
            tokens.Withdraw(holder.Agent, POOL);
            capacity.Slots = 2;
            world.Roster.Step(DT);

            Assert.IsTrue(tokens.Holds(holder.Agent, POOL), "A granted token stays (Release gives it back)");
            Assert.IsFalse(tokens.Holds(waiter.Agent, POOL), "The waiting request is gone: the room is not given to it");
        }

        // ---------- the fight round a target, the blow ----------

        private sealed class RejectAll : ITargetFilter
        {
            public bool Accepts(Transform candidate) => false;
        }

        [Test]
        public void Engagement_AMembersTargetFilterRejectsTheTarget_Ends()
        {
            var world = new AIWorld();
            GraphRig rig = Member(world);
            rig.Context.TargetFilter = new RejectAll();             // the game's rules: no longer a target ("only the living")
            Transform target = Thing("Target", Vector3.zero);
            world.Roster.EngagementOf(target);

            world.Roster.Step(DT);

            CollectionAssert.IsEmpty(world.Roster.Engagements, "No fight round what is no target");
        }

        [Test]
        public void DirectStriker_InactiveTarget_NobodyHearsTheBlow()
        {
            Transform target = Thing("Target", Vector3.zero);
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            target.gameObject.SetActive(false);

            Assert.DoesNotThrow(() => DirectStriker.Deliver(Thing("Attacker", Vector3.one), target, 1f));

            Assert.AreEqual(0, hits.Hits, "A target out of the world hears no blow");
        }
    }
}
