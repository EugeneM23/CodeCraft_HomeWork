using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The rules of single decisions, asked directly and on a real agent (whole-plugin recheck, brain): Kite's band with
    /// its slack (handbook §17), Switch Weapon's draw between Every and Every Max and its own checks, Charge's reach from
    /// the weapon's rush and its "held while its chain runs", the cheap gate first (invariant 5), the reactions' windows
    /// and ranges, Notice of a new object; and the world: Reset, the clock of its step, the scene's world one for all.
    /// </summary>
    public sealed class BrainDecisionRulesTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private readonly List<Object> _created = new();
        private readonly List<(string Name, float Time)> _entered = new();

        [SetUp]
        public void SetUp() => _entered.Clear();

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

        // ---------- Kite ----------

        [Test]
        public void KiteReasoner_Within_TakenOnlyNear_LetGoOnlyBeyondLetGoAfter()
        {
            var rig = new GraphRig(trace: false);
            (WeaponAsset sword, WeaponAsset bow) = TwoWeapons(rig);
            rig.Agent.Weapon = bow;
            var kite = new KiteReasoner { Within = 6f, LetGoAfter = 9f };

            Assert.IsTrue(Holds(kite, rig, 6f, false, out _));
            Assert.IsFalse(Holds(kite, rig, 7f, false, out string far));
            Assert.AreEqual("target 7.0 m, needs 6.0 or closer", far);
            Assert.IsTrue(Holds(kite, rig, 7f, true, out _), "Taken: held to Let Go After");
            Assert.IsTrue(Holds(kite, rig, 9f, true, out _));
            Assert.IsFalse(Holds(kite, rig, 9.1f, true, out _));

            var below = new KiteReasoner { Within = 6f, LetGoAfter = 4f };
            Assert.IsTrue(Holds(below, rig, 6f, true, out _), "Let Go After below Within: no slack, Within");
            Assert.IsFalse(Holds(below, rig, 6.1f, true, out _));

            Assert.IsTrue(Holds(new KiteReasoner { Within = 0f }, rig, 50f, false, out _), "Within 0: at any distance");

            rig.Agent.Weapon = sword;
            Assert.IsFalse(Holds(kite, rig, 3f, false, out string blade));
            Assert.AreEqual("my weapon does not shoot", blade);
        }

        // ---------- Switch Weapon ----------

        [Test]
        public void ChangeWeaponReasoner_TheNextChangeIsDrawnBetweenEveryAndEveryMax_TheSameSeedTheSameTime()
        {
            float first = ChangeAt(7, 2f, 4f);
            float again = ChangeAt(7, 2f, 4f);
            float other = ChangeAt(8, 2f, 4f);
            float swapped = ChangeAt(7, 4f, 2f);

            Assert.That(first, Is.InRange(2f, 4f + DT + 1e-3f), "Between Every and Every Max of fighting");
            Assert.That(other, Is.InRange(2f, 4f + DT + 1e-3f));
            Assert.That(swapped, Is.InRange(2f, 4f + DT + 1e-3f), "Every above Every Max: the same range");
            Assert.AreEqual(first, again, 1e-5f, "One seed, one time");
        }

        [Test]
        public void ChangeWeaponReasoner_OneWeaponOrBackWhenCalmOff_IsNeverTaken_AndSaysWhy()
        {
            // One weapon carried, in a fight: never
            var single = new GraphRig(trace: false) { ThinkInterval = 0f };
            single.Context.ExplainsDecisions = true;
            WeaponAsset sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1" });
            single.GiveWeapon(sword);
            single.Carry(sword);
            BrainGraphBuilder g = Graph();
            g.Do(g.Decision(new ChangeWeaponReasoner { Every = 0f, EveryMax = 0f }, 1), g.Block(new WaitTask(1f), "Change"));
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(new WaitTask(100f), "Patrol Block"));
            Run(single, g, new Vector3(1f, 0f, 0f));
            single.StepSeconds(1f, DT);
            Assert.AreEqual("Patrol Block", single.StateName);
            Assert.AreEqual("one weapon only", WhyNot(single, g.Brain, "Switch Weapon"));
            CollectionAssert.DoesNotContain(Names(), "Change");

            // Two weapons, the bow in the hand, no target, Back When Calm off: it stays as it is
            _entered.Clear();
            var calm = new GraphRig(trace: false) { ThinkInterval = 0f };
            calm.Context.ExplainsDecisions = true;
            (_, WeaponAsset bow) = TwoWeapons(calm);
            calm.Agent.Weapon = bow;
            BrainGraphBuilder h = Graph();
            h.Do(h.Decision(new ChangeWeaponReasoner { Every = 0f, EveryMax = 0f, BackWhenCalm = false }, 1),
                h.Block(new SwitchWeaponTask { Weapon = SwitchWeaponTask.NEXT }, "Change"));
            h.Do(h.Decision(new PatrolReasoner(), 2), h.Block(new WaitTask(100f), "Patrol Block"));
            Run(calm, h, null);
            calm.StepSeconds(1f, DT);
            Assert.AreEqual("Patrol Block", calm.StateName);
            Assert.AreEqual("no target", WhyNot(calm, h.Brain, "Switch Weapon"));
            Assert.AreSame(bow, calm.Context.Weapon, "The bow stays in the hand");
        }

        // ---------- Charge ----------

        [Test]
        public void ChargeReasoner_WithinZero_TheWeaponsRushReach_NoRush_NeverTaken()
        {
            const float RUSH = 10f;
            var rig = new GraphRig(trace: false);
            WeaponAsset charger = WeaponAsset.Create(new WeaponMove { Name = "Attack 1" });
            charger.Charges = true;
            charger.Rush = new WeaponRush { ChargeTime = 1f, AimSpeed = 90f, Distance = RUSH, Speed = 10f, Recovery = 1f };
            rig.GiveWeapon(charger);
            var charge = new ChargeReasoner { Farther = 6f, Within = 0f, Every = 4f };

            Assert.IsTrue(Holds(charge, rig, 9f, false, out _));
            Assert.IsFalse(Holds(charge, rig, 11f, false, out string far));
            Assert.AreEqual("target 11.0 m, needs 10 or nearer", far);
            Assert.IsFalse(Holds(charge, rig, 5f, false, out string near));
            Assert.AreEqual("target 5.0 m, needs 6 or farther", near);
            Assert.AreEqual("within: the weapon's rush", charge.Requirement);

            Assert.IsTrue(Holds(charge, rig, 9f, true, out _), "Taken: held while its chain runs");
            Assert.IsFalse(Holds(charge, rig, 9f, true, out string done, chainEnded: true));
            Assert.AreEqual("charged just now", done, "Its chain over: it lets go");

            rig.GiveWeapon(WeaponAsset.Create(new WeaponMove { Name = "Attack 1" }));
            Assert.IsFalse(Holds(charge, rig, 9f, false, out string blade));
            Assert.AreEqual("my weapon does not charge", blade);
        }

        // ---------- the cheap gate first ----------

        [Test]
        public void ReasonerTask_OwnSettingsFail_TheWiredConditionIsNotEvaluated_AsksConditionFirstTheOtherWay()
        {
            var rig = new GraphRig(trace: false);
            var yes = new CountingCondition { Value = true };
            var fight = new FightReasoner { Closer = 1.5f, LetGoAfter = 1.5f, Condition = yes };

            Assert.IsFalse(Asked(fight, rig, 5f, out _));
            Assert.AreEqual(0, yes.Evaluations, "Its own distance said no: the wired condition is not asked");
            Assert.IsTrue(Asked(fight, rig, 1f, out _));
            Assert.AreEqual(1, yes.Evaluations);

            var no = new CountingCondition { Value = false };
            var search = new ConditionFirst { Condition = no };
            Assert.IsFalse(Asked(search, rig, 1f, out string why));
            Assert.AreEqual(0, search.Asked, "A decision that asks the world asks the cheap wired condition first");
            Assert.AreEqual(1, no.Evaluations);
            StringAssert.StartsWith("only if", why);
        }

        // ---------- the reactions ----------

        [Test]
        public void GotHitReaction_AHitOlderThanTheWindow_IsNotAnswered_WhenATargetComesLater()
        {
            GameObject attacker = Thing("Attacker", new Vector3(2f, 0f, 0f));
            GraphRig rig = ReactionRig();
            BrainGraphBuilder g = Graph();
            var reaction = new GotHitReaction { Window = 1f, Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f, Cooldown = 0f };
            g.Do(g.Decision(reaction, 1), g.Block(new WaitTask(0.2f), "Turn"));
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(new WaitTask(100f), "Rest"));
            Run(rig, g, null);
            rig.StepSeconds(0.2f, DT);

            rig.Agent.TakeHit(new HitInfo(attacker.transform, 1f));
            rig.StepSeconds(1.2f, DT);
            rig.Agent.Blackboard.Set(TARGET, attacker.transform);
            rig.StepSeconds(0.3f, DT);

            CollectionAssert.DoesNotContain(Names(), "Turn", "Seen only after the window: the hit is not answered");
            string why = WhyNot(rig, g.Brain, "Got Hit");
            StringAssert.StartsWith("hit ", why);
            StringAssert.EndsWith("answers within 1.0", why);
        }

        [Test]
        public void TargetOpensUpReaction_OpenButBeyondItsRange_IsNoEvent_SaysHowFar()
        {
            GameObject target = Thing("Player", new Vector3(4f, 0f, 0f));
            FakeStatus status = target.AddComponent<FakeStatus>();
            status.Recovering = true;
            GraphRig rig = ReactionRig();
            BrainGraphBuilder g = Graph();
            g.Do(g.Decision(new TargetOpensUpReaction { Range = 3f, Chance = 1f, ReactionTime = 0f, ReactionTimeMax = 0f }, 1),
                g.Block(new WaitTask(0.2f), "Punish"));
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(new WaitTask(100f), "Rest"));
            Run(rig, g, target.transform);

            rig.StepSeconds(0.5f, DT);
            Assert.AreEqual("target 4.0 m, needs 3.0 or closer", WhyNot(rig, g.Brain, "Target Opens Up"));
            CollectionAssert.DoesNotContain(Names(), "Punish");

            target.transform.position = new Vector3(2f, 0f, 0f);
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Punish", 0.2f, DT), "Within reach: punished");
        }

        // ---------- Notice ----------

        [Test]
        public void NoticeReasoner_TheBrainsTargetSwitchesToAnotherObject_IsNoticedAgain()
        {
            const float REACT = 0.5f;
            GameObject a = Thing("A", new Vector3(1f, 0f, 0f));
            GameObject b = Thing("B", new Vector3(0f, 0f, 1f));
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            BrainGraphBuilder g = Graph();
            g.Do(g.Decision(new NoticeReasoner { React = REACT }, 1), g.Block(new WaitTask(REACT), "Notice Block"));
            g.Do(g.Decision(new FightReasoner { Closer = 1.5f, LetGoAfter = 2f }, 2), g.Block(new WaitTask(100f), "Fight Block"));
            Run(rig, g, a.transform);
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Fight Block", 2f, DT), "Noticed A, then the fight");
            rig.StepSeconds(2f, DT);

            rig.Agent.Blackboard.Set(TARGET, b.transform);              // another object, never none between
            rig.Step(DT);

            Assert.Less(CombatBrainTask.FactsOf(g.Brain, rig.Context).SeenFor, 0.2f, "A new target's time starts anew");
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Notice Block", 0.2f, DT), "Noticed anew");
            Assert.AreEqual(2, Names().Count(name => name == "Notice Block"));
        }

        // ---------- the world ----------

        [Test]
        public void AIWorld_Reset_ClearsTheBlackboard_HandsTheSameSeedsAgain_KeepsTheAgents()
        {
            var world = new AIWorld(9);
            int s1 = world.NextAgentSeed();
            int s2 = world.NextAgentSeed();
            world.Blackboard.Set("Alert", 1f);
            var rig = new GraphRig(trace: false);
            rig.Agent.SetWorld(world);

            world.Reset();

            Assert.AreEqual(0, world.Blackboard.Count, "The shared facts are forgotten");
            Assert.AreEqual(s1, world.NextAgentSeed(), "The seeds are handed from the beginning: a level replays");
            Assert.AreEqual(s2, world.NextAgentSeed());
            Assert.IsTrue(world.Scheduler.Contains(rig.Agent), "The agents stay registered");
            CollectionAssert.Contains(world.Roster.Members.ToList(), rig.Agent);
        }

        [Test]
        public void AIWorld_BeginStep_AdvancesTheObjectsClockAndMaturesTokens_ByDt()
        {
            var world = new AIWorld { Objects = new WorldObjects() };
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            Transform target = Thing("Target", new Vector3(1f, 0f, 0f)).transform;
            Engagement engagement = world.Roster.EngagementOf(target);
            engagement.Tokens.MaturationWindow = 0.4f;
            Assert.AreEqual(TokenResult.Pending, engagement.Tokens.Request(rig.Agent, "Melee", 1, 0f, 1f));

            for (int i = 0; i < 10; i++)
                world.BeginStep();                  // dt 0: neither the clock nor the request's age moves
            Assert.AreEqual(0f, world.Objects.Clock);
            Assert.IsFalse(engagement.Tokens.Holds(rig.Agent, "Melee"), "Not matured with no time passing");

            engagement.Tokens.Request(rig.Agent, "Melee", 1, 0f, 1f);
            world.BeginStep(0.25f);
            Assert.IsFalse(engagement.Tokens.Holds(rig.Agent, "Melee"), "0.25 s old: the window is 0.4");
            engagement.Tokens.Request(rig.Agent, "Melee", 1, 0f, 1f);
            world.BeginStep(0.25f);
            Assert.IsTrue(engagement.Tokens.Holds(rig.Agent, "Melee"), "0.5 s old: matured, granted");
            world.BeginStep(0.25f);
            world.BeginStep(0.25f);
            Assert.AreEqual(1f, world.Objects.Clock, 1e-5f, "The objects' clock goes by the world's steps");
            rig.Agent.gameObject.SetActive(false);
        }

        [Test]
        public void AIWorldRunner_Join_KeepsAGivenWorld_AgentsWithoutOneShareOneSceneWorld()
        {
            DestroySceneWorlds();
            try
            {
                var given = new AIWorld();
                var a = new GraphRig(trace: false, world: given);
                var b = new GraphRig(trace: false);
                var c = new GraphRig(trace: false);

                AIWorldRunner.Join(a.Agent);
                AIWorldRunner.Join(b.Agent);
                AIWorldRunner.Join(c.Agent);
                IAIWorld scene = b.Agent.World;
                AIWorldRunner.Join(b.Agent);

                Assert.AreSame(given, a.Agent.World, "A world a game gave is kept");
                Assert.IsNotNull(scene);
                Assert.AreSame(scene, c.Agent.World, "Every agent without one: the scene's one world");
                Assert.AreSame(scene, b.Agent.World, "Joining again changes nothing");
                Assert.AreEqual(1, Object.FindObjectsByType<AIWorldRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
            }
            finally
            {
                DestroySceneWorlds();
            }
        }

        // ---------- the rig ----------

        private static bool Holds(ReasonerTask decision, GraphRig rig, float distance, bool current, out string why,
            bool chainEnded = false)
        {
            Transform target = rig.CreateTargetAt(new Vector3(distance, 0f, 0f));
            return decision.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), current, true, chainEnded),
                out why);
        }

        private static bool Asked(ReasonerTask decision, GraphRig rig, float distance, out string why)
        {
            Transform target = rig.CreateTargetAt(new Vector3(distance, 0f, 0f));
            return decision.HoldsWithCondition(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false),
                out why);
        }

        // The fight time (since the Brain first looked) at which Switch Weapon is entered
        private float ChangeAt(int seed, float every, float everyMax)
        {
            var rig = new GraphRig(seed, trace: false) { ThinkInterval = 0f };
            TwoWeapons(rig);
            BrainGraphBuilder g = Graph();
            AgentState change = g.Decision(new ChangeWeaponReasoner { Every = every, EveryMax = everyMax }, 1);
            g.Do(change, g.Block(new SwitchWeaponTask { Weapon = SwitchWeaponTask.NEXT }, "Switch Block"));
            g.Do(g.Decision(new FightReasoner { Closer = 1.5f, LetGoAfter = 2f }, 2), g.Block(new WaitTask(100f), "Fight Block"));
            rig.SetGraph(g.Build());
            Transform target = Thing($"Foe {seed}", new Vector3(1f, 0f, 0f)).transform;
            rig.Agent.Blackboard.Set(TARGET, target);
            float firstLook = float.NaN;
            Assert.IsTrue(rig.StepUntil(() =>
            {
                if (float.IsNaN(firstLook) && rig.StateName == "Fight Block")
                    firstLook = rig.Context.LocalTime;
                return rig.Instance.Decision == change;
            }, 6f, DT));
            return rig.Context.LocalTime - firstLook;
        }

        private static (WeaponAsset Sword, WeaponAsset Bow) TwoWeapons(GraphRig rig)
        {
            WeaponAsset sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1" });
            sword.name = "Sword";
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 14f });
            bow.name = "Bow";
            bow.Shoots = true;
            rig.GiveWeapon(sword);
            rig.Carry(sword);
            rig.Carry(bow);
            return (sword, bow);
        }

        private static GraphRig ReactionRig()
        {
            var rig = new GraphRig(7, trace: false) { ThinkInterval = 0f };
            rig.Context.ExplainsDecisions = true;
            if (rig.Context.Self != null)
                rig.Context.Self.rotation = Quaternion.LookRotation(rig.Actuator.Facing);
            return rig;
        }

        private static BrainGraphBuilder Graph()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            return g;
        }

        private void Run(GraphRig rig, BrainGraphBuilder g, Vector3 target) =>
            Run(rig, g, Thing(TARGET, target).transform);

        private void Run(GraphRig rig, BrainGraphBuilder g, Transform target)
        {
            rig.SetGraph(g.Build());
            rig.Agent.Blackboard.Set(TARGET, target);
            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null)
                    _entered.Add((to.DisplayName, rig.Context.LocalTime));
            };
        }

        private GameObject Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go;
        }

        private static string WhyNot(GraphRig rig, AgentState brain, string decision) =>
            CombatBrainTask.VerdictsOf(brain, rig.Context).Single(verdict => verdict.State.DisplayName == decision).WhyNot;

        private List<string> Names() => _entered.Select(entry => entry.Name).ToList();

        private static void DestroySceneWorlds()
        {
            foreach (AIWorldRunner runner in Object.FindObjectsByType<AIWorldRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(runner.gameObject);
        }

        private sealed class CountingCondition : AgentCondition
        {
            public bool Value;
            public int Evaluations;

            protected override bool Evaluate(AgentContext context)
            {
                Evaluations++;
                return Value;
            }
        }

        /// <summary>A decision whose own question looks into the world: it asks its wired condition first.</summary>
        private sealed class ConditionFirst : ReasonerTask
        {
            public int Asked;

            public override bool AsksConditionFirst => true;

            public override bool Holds(in ReasonerQuery query, out string whyNot)
            {
                Asked++;
                whyNot = null;
                return true;
            }
        }
    }
}
