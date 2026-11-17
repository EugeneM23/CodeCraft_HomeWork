using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using ConstraintIs = UnityEngine.TestTools.Constraints.Is;

namespace AIKit.Tests
{
    /// <summary>
    /// The instant ask for a place (Take Attack Slot with Wait Time 0 on a decision's On Tick, step 90) at its corners (the
    /// recheck of the fight): fighters taking turns at one place think without allocating; a character given a graph
    /// again and again listens for its decision leaving once, not once per graph; a request left behind on the old target
    /// when the Brain's target changes goes stale by itself.
    /// </summary>
    public sealed class CombatSlotAskTests
    {
        private const string TARGET = "Target";
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

        private Transform Target(Vector3 at, int places)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            go.AddComponent<AttackSlotCapacity>().Slots = places;
            return go.transform;
        }

        private static GraphRig Fighter(AIWorld world, Vector3 at)
        {
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.Context.ExplainsDecisions = false;
            return rig;
        }

        // Attack (my place): a moment, then the place back. Circle (no place of mine): it asks on its On Tick and waits
        private static BehaviorGraph TakingTurns()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState attack = g.Decision(new FightReasoner
            {
                Closer = 100f, LetGoAfter = 100f, Condition = new AttackSlotCondition(AttackSlotIs.Mine)
            }, 1);
            AgentState strike = g.Block(new WaitTask(0.3f), "Strike");
            g.Do(attack, strike);
            g.Then(strike, g.Block(new ReleaseAttackSlotTask(), "Give Back"));
            AgentState circle = g.Decision(new CircleReasoner { Condition = new AttackSlotCondition(AttackSlotIs.NotMine) }, 2);
            g.Do(circle, g.Block(new WaitTask(100f), "Wait"));
            g.OnTick(circle, g.Block(new TakeAttackSlotTask { WaitTime = 0f }, "Ask"));
            return g.Build();
        }

        // Circle only: it asks on its On Tick and waits
        private static BehaviorGraph Asking()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState circle = g.Decision(new CircleReasoner { Condition = new AttackSlotCondition(AttackSlotIs.NotMine) }, 1);
            g.Do(circle, g.Block(new WaitTask(100f), "Wait"));
            g.OnTick(circle, g.Block(new TakeAttackSlotTask { WaitTime = 0f }, "Ask"));
            return g.Build();
        }

        private static void Run(AIWorld world, GraphRig[] rigs, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                world.BeginStep(DT);
                for (int r = 0; r < rigs.Length; r++)
                    rigs[r].Step(DT);
            }
        }

        private static int Listeners(AgentContext context)
        {
            FieldInfo field = typeof(AgentContext).GetField("DecisionLeft", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "The event's field");
            var handler = (System.Action)field.GetValue(context);
            return handler?.GetInvocationList().Length ?? 0;
        }

        // ---------- no allocation ----------

        [Test]
        public void CircleOnTickAsk_TwoFightersTakingTurns_ThinksAllocateNothing()
        {
            var world = new AIWorld();
            Transform target = Target(Vector3.zero, places: 1);
            GraphRig[] rigs = { Fighter(world, new Vector3(2f, 0f, 0f)), Fighter(world, new Vector3(-2f, 0f, 0f)) };
            BehaviorGraph graph = TakingTurns();
            foreach (GraphRig rig in rigs)
            {
                rig.SetGraph(graph);
                rig.Agent.Blackboard.Set(TARGET, target);
            }

            Run(world, rigs, Mathf.CeilToInt(5f / DT));         // warm up: every decision taken, every list grown
            Engagement engagement = world.Roster.FindEngagement(target);
            Assert.IsNotNull(engagement, "Set-up: they ask for the place");

            int turns = 0;
            AICharacter holder = null;
            var holders = new List<AICharacter>(4);
            for (int i = 0; i < Mathf.CeilToInt(3f / DT); i++)
            {
                Run(world, rigs, 1);
                engagement.Tokens.HoldersOf(AttackSlots.POOL, holders);
                AICharacter now = holders.Count > 0 ? holders[0] : null;
                if (now != null && now != holder)
                    turns++;
                holder = now;
            }

            Assert.GreaterOrEqual(turns, 2, "Set-up: the two take turns at the one place");

            // The profiler's own count of allocations (GC.GetAllocatedBytesForCurrentThread reads 0 in this runtime)
            int steps = Mathf.CeilToInt(3f / DT);
            Assert.That(() => Run(world, rigs, steps), ConstraintIs.Not.AllocatingGCMemory(),
                "Asking, waiting and taking turns allocate nothing (trace off, nobody looking)");
        }

        // ---------- the listener of the decision leaving ----------

        [Test]
        public void TakeAttackSlot_InstantAsk_SetGraphTwice_OneDecisionLeftListener()
        {
            var world = new AIWorld();
            Transform target = Target(Vector3.zero, places: 1);
            GraphRig holder = Fighter(world, new Vector3(1f, 0f, 0f));
            holder.Agent.Blackboard.Set(TARGET, target);
            new TakeAttackSlotTask { Target = TARGET, WaitTime = 0f }.Tick(holder.Context, DT);
            world.BeginStep(DT);
            Assert.IsTrue(world.Roster.FindEngagement(target).Tokens.Holds(holder.Agent, AttackSlots.POOL),
                "Set-up: the one place is another's");
            GraphRig rig = Fighter(world, new Vector3(3f, 0f, 0f));
            BehaviorGraph graph = Asking();
            GraphRig[] asking = { rig };
            int own = Listeners(rig.Context);                       // the character's own (its claims on objects)

            rig.SetGraph(graph);
            rig.Agent.Blackboard.Set(TARGET, target);
            Run(world, asking, 10);
            Assert.AreEqual(own + 1, Listeners(rig.Context), "Set-up: its ask waits and listens for the decision leaving");

            rig.SetGraph(graph);
            rig.Agent.Blackboard.Set(TARGET, target);
            Run(world, asking, 10);
            Assert.AreEqual(own + 1, Listeners(rig.Context), "Given the graph again: still one listener, not one per graph");

            rig.SetGraph(graph);
            rig.Agent.Blackboard.Set(TARGET, target);
            Run(world, asking, 10);
            Assert.AreEqual(own + 1, Listeners(rig.Context), "…and again");
        }

        // ---------- a request left behind ----------

        [Test]
        public void TakeAttackSlot_InstantAsk_BrainTargetChanges_OldRequestGoneWithinHalfASecond()
        {
            var world = new AIWorld();
            Transform first = Target(new Vector3(5f, 0f, 0f), places: 1);
            Transform second = Target(new Vector3(-5f, 0f, 0f), places: 1);
            GraphRig holderOfFirst = Fighter(world, new Vector3(6f, 0f, 0f));
            GraphRig holderOfSecond = Fighter(world, new Vector3(-6f, 0f, 0f));
            var ask = new TakeAttackSlotTask { WaitTime = 0f };
            holderOfFirst.Context.BrainTarget = first;
            holderOfSecond.Context.BrainTarget = second;
            ask.Tick(holderOfFirst.Context, DT);
            ask.Tick(holderOfSecond.Context, DT);
            world.BeginStep(DT);
            AttackTokens onFirst = world.Roster.FindEngagement(first).Tokens;
            AttackTokens onSecond = world.Roster.FindEngagement(second).Tokens;
            Assume.That(onFirst.Holds(holderOfFirst.Agent, AttackSlots.POOL) &&
                        onSecond.Holds(holderOfSecond.Agent, AttackSlots.POOL), "Set-up: both places are others'");
            GraphRig rig = Fighter(world, Vector3.zero);

            rig.Context.BrainTarget = first;
            ask.Tick(rig.Context, DT);                              // filed on the first
            rig.Context.BrainTarget = second;                       // the Brain looks at another now
            for (int i = 0; i < Mathf.CeilToInt((AttackTokens.STALE_AFTER + 2f * DT) / DT); i++)
            {
                world.BeginStep(DT);
                ask.Tick(rig.Context, DT);                          // it asks the second every think
            }

            onFirst.Release(holderOfFirst.Agent, AttackSlots.POOL);
            onSecond.Release(holderOfSecond.Agent, AttackSlots.POOL);
            world.BeginStep(DT);

            Assert.IsFalse(onFirst.Holds(rig.Agent, AttackSlots.POOL),
                "Its request on the first went stale: the freed place there is not given to one gone elsewhere");
            Assert.IsTrue(onSecond.Holds(rig.Agent, AttackSlots.POOL), "Its request on the second, asked every think, is");
        }
    }
}
