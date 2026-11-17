using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A decision that the Brain has just taken holds while its chain runs (whole-plugin recheck, brain): Charge and
    /// Switch Weapon answer "no" as the current decision only once their chain has come to its end, so a chain that
    /// starts with a Wait or a Turn To Target before the charge or the switch runs to that block and through it — the
    /// Brain does not drop the decision in the think its first block ends.
    /// </summary>
    public sealed class BrainChainHoldTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.05f;

        private GraphRig _rig;
        private GameObject _victim;
        private BrainGraphBuilder _graph;
        private readonly List<(string Name, float Time)> _entered = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            _rig.Context.ExplainsDecisions = true;
            _victim = new GameObject("Victim") { hideFlags = HideFlags.HideAndDontSave };
            _graph = new BrainGraphBuilder();
            _graph.BrainTask.Target = TARGET;
            _graph.Variable(TARGET, VariableType.Transform);
            _entered.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_victim);
            GraphRig.DestroyAll();
        }

        [Test]
        public void ChargeReasoner_ChainWaitThenCharge_TheChargeRunsToItsEnd()
        {
            const float WAIT = 0.2f, GATHER = 0.5f, DISTANCE = 9f, SPEED = 12f, RECOVERY = 0.5f;
            _rig.GiveWeapon(1.6f, 0.2f, 0.3f);                 // a weapon with no Rush: the node's own numbers
            var charge = new ChargeTask
            {
                ChargeTime = GATHER, AimSpeed = 180f, Distance = DISTANCE, Speed = SPEED, Recovery = RECOVERY
            };
            AgentState charging = _graph.Decision(new ChargeReasoner { Farther = 6f, Within = 25f, Every = 4f }, 1);
            AgentState wait = _graph.Block(new WaitTask(WAIT), "Wait Block");
            AgentState rush = _graph.Block(charge, "Charge Block");
            _graph.Do(charging, wait);
            _graph.Then(wait, rush);
            _graph.Do(_graph.Decision(new ChaseReasoner(), 2), _graph.Block(new ChaseTask { StopDistance = 1.2f }, "Chase Block"));
            _victim.transform.position = new Vector3(12f, 0f, 0f);
            Run();

            Assert.IsTrue(_rig.StepUntil(() => _rig.Instance.Decision == charging, 1f, DT), "Far: the Charge decision");
            float taken = _rig.Context.LocalTime;

            Assert.IsTrue(_rig.StepUntil(() => charge.PhaseOf(_rig.Context) == ChargeTask.Phase.Rushing,
                    WAIT + GATHER + 2f * DT, DT),
                $"The Wait done, the chain goes on to the charge, and it rushes ({Path()})");
            Assert.AreEqual(charging, _rig.Instance.Decision, "Still the Charge decision");

            float end = WAIT + GATHER + DISTANCE / SPEED + RECOVERY;
            Assert.IsTrue(_rig.StepUntil(() => _rig.Instance.Decision != charging, end + 1f, DT));
            Assert.GreaterOrEqual(_rig.Context.LocalTime - taken, end - 2f * DT, "Held to the end of the charge");
            Assert.Greater(TimeOf("Chase Block"), TimeOf("Charge Block"), "The chase only after the charge");
        }

        [Test]
        public void ChangeWeaponReasoner_ChainWaitThenSwitchWeapon_TheWeaponIsSwitched()
        {
            const float WAIT = 0.2f;
            (WeaponAsset sword, WeaponAsset bow) = TwoWeapons();
            var pause = new WaitTask(WAIT);
            AgentState change = _graph.Decision(new ChangeWeaponReasoner { Every = 1f, EveryMax = 1f }, 1);
            AgentState waiting = _graph.Block(pause, "Wait Block");
            _graph.Do(change, waiting);
            _graph.Then(waiting, _graph.Block(new SwitchWeaponTask { Weapon = SwitchWeaponTask.NEXT }, "Switch Block"));
            _graph.Do(_graph.Decision(new FightReasoner { Closer = 1.5f, LetGoAfter = 2f }, 2),
                _graph.Block(new WaitTask(100f), "Fight Block"));
            _victim.transform.position = new Vector3(1f, 0f, 0f);
            Run();
            var waits = new List<TaskStatus>();
            _rig.Instance.StateChanged += (from, _) =>
            {
                if (from == waiting)
                    waits.Add(pause.StatusOf(_rig.Context));
            };

            float switching = Mathf.Max(sword.SheatheTime, sword.SheatheAt) + Mathf.Max(bow.DrawTime, bow.DrawAt);
            Assert.IsTrue(_rig.StepUntil(() => _rig.Context.Weapon == bow, 1f + WAIT + switching + 0.2f, DT),
                $"Its time come, the chain waits, then switches the weapon ({Path()})");
            CollectionAssert.IsNotEmpty(waits, "The Wait block was left");
            Assert.AreEqual(TaskStatus.Success, waits[0], "The Wait ran to its end, it was not cut short");
        }

        // ---------- the graph ----------

        private (WeaponAsset Sword, WeaponAsset Bow) TwoWeapons()
        {
            WeaponAsset sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1" });
            sword.name = "Sword";
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 14f });
            bow.name = "Bow";
            bow.Shoots = true;
            _rig.GiveWeapon(sword);
            _rig.Carry(sword);
            _rig.Carry(bow);
            return (sword, bow);
        }

        private void Run()
        {
            _rig.SetGraph(_graph.Build());
            _rig.Agent.Blackboard.Set(TARGET, _victim.transform);
            _rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null)
                    _entered.Add((to.DisplayName, _rig.Context.LocalTime));
            };
        }

        private float TimeOf(string state) =>
            _entered.Any(entry => entry.Name == state) ? _entered.First(entry => entry.Name == state).Time : float.MaxValue;

        private string Path() => string.Join(" → ", _entered.Select(entry => $"{entry.Name}@{entry.Time:0.00}"));
    }
}
