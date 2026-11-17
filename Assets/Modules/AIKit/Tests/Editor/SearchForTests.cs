using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Search For (step 90, C15 — "the brain decides to search"): it holds on need — its wired condition — whether or not
    /// an object is known (finding one is its chain's); it holds while its chain runs; it rests after it lets go and
    /// after its chain found nothing. The whole rock thrower on a real agent: search for a rock, take it (a weapon from
    /// the world), the Attack decision takes over and throws it, the rock is spent and the search holds again.
    /// </summary>
    public sealed class SearchForTests
    {
        private const string TARGET = "Target";
        private const string FOUND = "Found";
        private const float DT = 0.02f;
        private const float STEP = 0.05f;

        private readonly List<Object> _created = new();
        private WorldObjects _registry;

        [SetUp]
        public void SetUp() => _registry = new WorldObjects();

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

        [Test]
        public void SearchFor_HoldsOnItsCondition_ThoughNoObjectIsKnown()
        {
            GraphRig rig = Rig();
            var need = new FlagCondition { Value = true };
            var search = new SearchForReasoner { Condition = need };
            var query = new ReasonerQuery(rig.Context, TargetFacts.None, false);

            Assert.IsEmpty(_registry.All, "Set-up: the world has no objects");
            Assert.IsTrue(search.HoldsWithCondition(query, out _), "The need holds: it holds — finding is its chain's");
            Assert.IsTrue(search.AsksConditionFirst, "Its need is asked first");
            Assert.IsFalse(search.NeedsTarget, "A search for a rock holds while patrolling too");

            need.Value = false;
            Assert.IsFalse(search.HoldsWithCondition(query, out string whyNot), "No need: no search");
            Assert.That(whyNot, Does.StartWith("only if"));
            Assert.IsTrue(new SearchForReasoner().HoldsWithCondition(query, out _), "Nothing wired: it holds when not resting");
        }

        [Test]
        public void SearchFor_HoldsWhileItsChainRuns_RestsAfterItEndsFindingNothing()
        {
            GraphRig rig = Rig();
            var search = new SearchForReasoner { Rest = 2f };
            var find = new FindTask
            {
                What = new WhatValue { Offer = Offer.Take, Radius = 30f }, How = FindTask.Way.Wander, WanderRadius = 6f,
                GiveUpAfter = 3f, StoreIn = FOUND
            };
            AgentState searching = Build(rig, search, new AgentTask[] { find });

            rig.Step(DT, 2);                                        // the first think starts the graph, the next decides
            Assert.AreSame(searching, rig.Instance.Decision, "Search For is taken");
            float started = rig.Context.LocalTime;
            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision != searching, 6f, DT), "It lets go in the end");
            Assert.That(rig.Context.LocalTime - started, Is.GreaterThanOrEqualTo(find.GiveUpAfter.Get(rig.Context) - DT),
                "Held while its chain ran (the wander), not cut short");
            float left = rig.Context.LocalTime;

            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == searching, 5f, DT), "Taken again once rested");
            Assert.That(rig.Context.LocalTime - left, Is.EqualTo(search.Rest).Within(0.1f),
                "Not before its Rest after the search found nothing");
        }

        [Test]
        public void SearchFor_RestsAfterItLetsGo_ThoughItsNeedStillHolds()
        {
            GraphRig rig = Rig();
            var urgent = new FlagCondition();
            var search = new SearchForReasoner { Rest = 1.5f };
            var g = new BrainGraphBuilder();
            AgentState more = g.Decision(new DailyRoutineReasoner { Condition = urgent }, 1);
            g.Do(more, g.Block(new WaitTask(100f)));
            AgentState searching = g.Decision(search, 2);
            g.Do(searching, g.Block(new WaitTask(100f)));
            g.Do(g.Decision(new PatrolReasoner(), 3), g.Block(new WaitTask(100f)));
            rig.SetGraph(g.Build());

            rig.Step(DT, 2);
            Assert.AreSame(searching, rig.Instance.Decision);
            urgent.Value = true;
            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == more, 1f, DT), "Something more important");
            urgent.Value = false;
            float left = rig.Context.LocalTime;

            Assert.IsTrue(rig.StepUntil(() => rig.Instance.Decision == searching, 4f, DT));
            Assert.That(rig.Context.LocalTime - left, Is.EqualTo(search.Rest).Within(0.1f),
                "Let go: it rests before it searches again");
        }

        [Test]
        public void RockThrower_SearchesTakesARock_AttackThrowsIt_TheRockSpent_SearchesAgain()
        {
            GraphRig rig = Rig();
            WorldRack rack = rig.UseWorldRack();
            rig.GiveWeapon(reach: 1.5f, windUp: 0.2f, recovery: 0.2f);       // its own sword: no throw from afar
            WeaponAsset rockWeapon = WeaponAsset.Create(new WeaponMove
            {
                Name = "Attack 1", Reach = 12f, WindUp = 0.3f, Recovery = 0.2f, Damage = 1f
            });
            rockWeapon.Uses = 1;                                // one throw (the weapon owns its uses)
            _created.Add(rockWeapon);
            foreach (Vector3 at in new[]
                     {
                         new Vector3(-3f, 0f, 2f), new Vector3(-2f, 0f, -3f), new Vector3(2f, 0f, -5f),
                         new Vector3(-1f, 0f, 1f)          // every one within a throw of the victim
                     })
                Rock(at, rockWeapon);

            GameObject victim = Thing("Victim", new Vector3(8f, 0f, 0f));
            HitProbe hits = victim.AddComponent<HitProbe>();
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Variable(FOUND, VariableType.Transform);
            AgentState attack = g.Decision(new AttackInReach(), 1);
            g.Do(attack, g.Block(new AttackTask()));
            var search = new SearchForReasoner { Rest = 0.3f, Condition = new HoldsWorldWeapon { Not = true } };
            AgentState searching = g.Decision(search, 2);
            g.Chain(searching,
                new FindTask { What = new WhatValue { Offer = Offer.Take, Radius = 30f }, StoreIn = FOUND },
                new MoveToTask { Target = FOUND },
                new InteractTask { Object = FOUND, Interaction = Offer.Take });
            g.Do(g.Decision(new PatrolReasoner(), 3), g.Block(new WaitTask(100f)));
            rig.SetGraph(g.Build());
            rig.Agent.Blackboard.Set(TARGET, victim.transform);
            var decisions = new List<AgentState>();
            rig.Instance.StateChanged += (_, _) =>
            {
                AgentState now = rig.Instance.Decision;
                if (now != null && (decisions.Count == 0 || decisions[decisions.Count - 1] != now))
                    decisions.Add(now);
            };

            int spent = 0;
            bool struckWithTheSword = false;
            Assert.IsTrue(rig.StepUntil(() =>
            {
                // The rock is spent by its throw: AttackRunner spends the world weapon's use after the delivered move. A hit
                // with no rock taken up for it would be the sword's (its own weapon reaches 1.5 m)
                if (hits.Hits > spent)
                {
                    spent = hits.Hits;
                    struckWithTheSword |= rack.TakenUp.Count < hits.Hits;
                }

                return hits.Hits >= 3;
            }, 40f, STEP), $"Three rocks thrown (hits {hits.Hits}, decision {rig.Instance.Decision?.DisplayName})");

            Assert.IsFalse(struckWithTheSword, "Every hit was a rock's, from afar");
            Assert.GreaterOrEqual(rack.TakenUp.Count, 3, "A rock taken up for every throw");
            Assert.That(rack.TakenUp.TrueForAll(taken => taken.Weapon == rockWeapon && !taken.Mounted));
            int searches = decisions.FindAll(decision => decision == searching).Count;
            int throws = decisions.FindAll(decision => decision == attack).Count;
            Assert.GreaterOrEqual(searches, 3, "Search, throw, search again…");
            Assert.GreaterOrEqual(throws, 3);
            Assert.AreSame(searching, decisions[0], "It searched first: a sword does not reach");
        }

        [Test]
        public void GraphCheck_AFindWithNoWhat_OrNoVariable_ASearchWithNoFind_AreProblemsOfTheGraph()
        {
            var g = new BrainGraphBuilder();
            g.Variable(FOUND, VariableType.Transform);
            AgentState lost = g.Decision(new SearchForReasoner(), 1);
            g.Chain(lost, new WaitTask(1f));
            AgentState blind = g.Decision(new SearchForReasoner(), 2);
            AgentState[] blindChain = g.Chain(blind, new FindTask { StoreIn = FOUND }, new FindTask { What = new WhatValue() });
            AgentState good = g.Decision(new SearchForReasoner(), 3);
            g.Chain(good, new FindTask { What = new WhatValue(), StoreIn = FOUND },
                new InteractTask { Object = FOUND });
            BehaviorGraph graph = g.Build();
            var issues = new List<GraphIssue>();

            ObjectGraphChecks.Check(graph, issues);

            List<int> named = issues.FindAll(issue => issue.Code == GraphIssueCode.NothingToSearch)
                .ConvertAll(issue => issue.RecordId);
            Assert.That(named, Is.EquivalentTo(new[] { lost.Id, blindChain[0].Id, blindChain[1].Id }),
                "A search with no Find; a Find with no What; a Find that writes nowhere");
            Assert.IsFalse(issues.Exists(issue => issue.IsError), "Problems to show, not errors that block");
            Assert.That(named, Has.No.Member(good.Id));
        }

        // ---------- helpers ----------

        private sealed class FlagCondition : AgentCondition
        {
            public bool Value;

            protected override bool Evaluate(AgentContext context) => Value;
        }

        /// <summary>
        /// The Attack decision as the conditions lane makes it ("Closer 0 — the reach of the weapon in the hand"): it holds
        /// while the weapon in the hand reaches the Brain's target.
        /// </summary>
        private sealed class AttackInReach : ReasonerTask
        {
            public override bool Holds(in ReasonerQuery query, out string whyNot)
            {
                WeaponAsset weapon = query.Context?.Weapon;
                bool reaches = query.Facts.Sees && weapon != null && query.Facts.Distance <= weapon.ShortestReach;
                whyNot = reaches ? null : "out of the weapon's reach";
                return reaches;
            }
        }

        private GraphRig Rig()
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            rig.WakeUpAt(Vector3.zero);
            rig.UseObjects(_registry);
            return rig;
        }

        // A graph of the one decision with its chain, and a patrol that holds always (the rest of the time)
        private static AgentState Build(GraphRig rig, ReasonerTask decision, AgentTask[] chain)
        {
            var g = new BrainGraphBuilder();
            g.Variable(FOUND, VariableType.Transform);
            AgentState taken = g.Decision(decision, 1);
            g.Chain(taken, chain);
            g.Do(g.Decision(new PatrolReasoner(), 2), g.Block(new WaitTask(100f)));
            rig.SetGraph(g.Build());
            return taken;
        }

        private void Rock(Vector3 at, WeaponAsset weapon)
        {
            var rock = Thing("Rock", at).AddComponent<PickableItem>();
            rock.Weapon = weapon;
            rock.TakeTime = 0.2f;
            _registry.Register(rock);
        }

        private GameObject Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go;
        }
    }
}
