using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The Get node (step 90, C11 / C12): a number about a character read live where it lives — its status, or a
    /// relation to the agent worked out by the module — with "no data" an answer of its own, answered once per think,
    /// no allocation in a think; and its readers: Compare (A / B), Set / Add (a snapshot), Debug Log; and the asset keeps
    /// the wires.
    /// </summary>
    public sealed class GetTests
    {
        private const string TARGET = "Target";
        private readonly List<Object> _created = new();
        private GraphRig _rig;
        private Transform _target;
        private Status _status;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            var go = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            _status = go.AddComponent<Status>();
            _target = go.transform;
            _target.position = new Vector3(3f, 0f, 4f);
            _rig.Agent.Blackboard.Set(TARGET, _target);
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

        private static GetValue Of(GetParameter parameter) => new(parameter, TARGET);

        private float Number(GetValue get)
        {
            Assert.IsTrue(get.TryGetNumber(_rig.Context, out float value), $"{get.Parameter}: has data");
            return value;
        }

        // ---------- each parameter ----------

        [Test]
        public void Get_EveryNumberOfTheStatus_OfTheWiredObject_IsItsOwnSay()
        {
            float said = 1f;
            foreach (CharacterStat stat in System.Enum.GetValues(typeof(CharacterStat)))
                _status.Say(stat, said++);

            foreach (CharacterStat stat in System.Enum.GetValues(typeof(CharacterStat)))
                Assert.AreEqual(_status.Said(stat), Number(Of((GetParameter)(int)stat)), stat.ToString());
        }

        [Test]
        public void Get_OfMe_ReadsTheAgentsOwnStatus()
        {
            _rig.Actuator.Move(Vector3.right, 0.5f);

            Assert.AreEqual(_rig.Actuator.Speed * 0.5f, Number(new GetValue(GetParameter.Speed)), 1e-4f);
            Assert.AreEqual(_rig.Actuator.Body.Height, Number(new GetValue(GetParameter.Height)));
        }

        [Test]
        public void Get_Relations_DistanceAngleFromMyFacingFacesMe_WorkedOutByTheModule()
        {
            _rig.Actuator.Position = Vector3.zero;
            _rig.Agent.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _rig.Actuator.LooksIn3D = true;                                  // a body in 3D looking along +Z (step-90
            _rig.Actuator.Face(Vector3.forward);                             // recheck: angles go by the body's look)
            _target.position = new Vector3(0f, 0f, 5f);
            _target.rotation = Quaternion.LookRotation(Vector3.back);       // it faces me

            Assert.AreEqual(5f, Number(Of(GetParameter.Distance)), 1e-4f);
            Assert.AreEqual(0f, Number(Of(GetParameter.AngleFromMyFacing)), 1e-3f, "Straight ahead of me");
            Assert.AreEqual(0f, Number(Of(GetParameter.FacesMe)), 1e-3f, "It faces me squarely");

            _target.position = new Vector3(5f, 0f, 0f);
            _target.rotation = Quaternion.LookRotation(Vector3.right);      // it looks away from me
            Assert.AreEqual(90f, Number(Of(GetParameter.AngleFromMyFacing)), 1e-3f, "On my right");
            Assert.AreEqual(180f, Number(Of(GetParameter.FacesMe)), 1e-3f, "Its back to me");
        }

        // ---------- no data ----------

        [Test]
        public void Get_NoData_NothingWired_NothingInTheVariable_ItDoesNotSay_ARelationToMyself()
        {
            Assert.IsFalse(new GetValue { Of = Whose.WiredObject }.TryGetNumber(_rig.Context, out _),
                "Wired Object with nothing wired: no data — never a silent \"me\"");

            Assert.IsFalse(Of(GetParameter.Health).TryGetNumber(_rig.Context, out _), "It does not say its health");

            _rig.Agent.Blackboard.Set(TARGET, (Transform)null);
            Assert.IsFalse(Of(GetParameter.Distance).TryGetNumber(_rig.Context, out _), "Nothing in the variable");

            Assert.IsFalse(new GetValue(GetParameter.Distance).TryGetNumber(_rig.Context, out _), "A distance to myself says nothing");

            var plain = new GameObject("Plain") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(plain);
            _rig.Agent.Blackboard.Set(TARGET, plain.transform);
            Assert.IsFalse(Of(GetParameter.Health).TryGetNumber(_rig.Context, out _), "An object with no status of its own");
        }

        // ---------- once per think, no allocation ----------

        [Test]
        public void Get_InOneThink_AnsweredOnce_TheNextThinkReadsAnew()
        {
            _status.Say(CharacterStat.Health, 5f);
            GetValue health = Of(GetParameter.Health);

            _rig.Context.BeginThink();
            Assert.AreEqual(5f, Number(health));
            _status.Say(CharacterStat.Health, 2f);
            Assert.AreEqual(5f, Number(health), "Asked again in the same think: the same answer");
            _rig.Context.EndThink();

            _rig.Context.BeginThink();
            Assert.AreEqual(2f, Number(health), "A new think reads it anew");
            _rig.Context.EndThink();
        }

        [Test]
        public void Get_ThinkAfterThink_AllocatesNothing()
        {
            _status.Say(CharacterStat.Health, 5f);
            GetValue health = Of(GetParameter.Health);
            GetValue distance = Of(GetParameter.Distance);
            var compare = new CompareCondition(health, NumberComparison.Greater, 1f);
            for (int i = 0; i < 3; i++)
                Think(health, distance, compare);              // the first asks find the status and make the records

            // Unity's own count (GC.GetAllocatedBytesForCurrentThread reads 0 in this editor whatever is allocated)
            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                    Think(health, distance, compare);
            }, NoAllocation.Constraint, "A think reads the numbers without allocating");
        }

        private void Think(GetValue health, GetValue distance, CompareCondition compare)
        {
            _rig.Context.BeginThink();
            health.TryGetNumber(_rig.Context, out _);
            distance.TryGetNumber(_rig.Context, out _);
            compare.Ask(_rig.Context);
            _rig.Context.EndThink();
        }

        // ---------- the readers ----------

        [Test]
        public void Compare_AGetInA_ComparesItsNumber_NoData_IsNoData_NotNo()
        {
            _status.Say(CharacterStat.HealthPercent, 20f);
            var low = new CompareCondition(Of(GetParameter.HealthPercent), NumberComparison.Less, 30f);
            Assert.AreEqual(Truth.Yes, low.Ask(_rig.Context));

            _status.Say(CharacterStat.HealthPercent, 80f);
            Assert.AreEqual(Truth.No, low.Ask(_rig.Context));

            _status.Forget(CharacterStat.HealthPercent);
            Assert.AreEqual(Truth.NoData, low.Ask(_rig.Context), "It does not say: no data, and a decision does not hold");
            Assert.IsFalse(new BranchTask(low).Ask(_rig.Context), "A Branch goes to False");
        }

        [Test]
        public void Compare_AGetInB_IsWhatAIsComparedWith()
        {
            _status.Say(CharacterStat.RunSpeed, 6f);
            _rig.Actuator.Move(Vector3.right, 1f);                       // my speed: 4
            var slower = new CompareCondition(new GetValue(GetParameter.Speed), NumberComparison.Less, 0f)
            {
                B = Of(GetParameter.RunSpeed)
            };

            Assert.AreEqual(Truth.Yes, slower.Ask(_rig.Context), "I am slower than it runs");
            _status.Forget(CharacterStat.RunSpeed);
            Assert.AreEqual(Truth.NoData, slower.Ask(_rig.Context));
        }

        [Test]
        public void SetAndAdd_AGetInNumber_ASnapshotIntoTheVariable_NoData_LeavesItAsItIs()
        {
            _status.Say(CharacterStat.Health, 7f);
            var set = new SetNumberTask { Variable = "Seen Health", Number = Of(GetParameter.Health) };
            Assert.AreEqual(TaskStatus.Success, set.Tick(_rig.Context, 0.1f));
            Assert.AreEqual(7f, _rig.Context.Blackboard.Get("Seen Health", 0f));

            var add = new AddNumberTask { Variable = "Seen Health", Number = Of(GetParameter.Health) };
            add.Tick(_rig.Context, 0.1f);
            Assert.AreEqual(14f, _rig.Context.Blackboard.Get("Seen Health", 0f), "Value is not used: the Get is");

            _status.Forget(CharacterStat.Health);
            set.Tick(_rig.Context, 0.1f);
            Assert.AreEqual(14f, _rig.Context.Blackboard.Get("Seen Health", 0f), "No data: the variable stays as it was");
        }

        [Test]
        public void DebugLog_AGetInNumber_WritesItsNumberAfterTheMessage()
        {
            _status.Say(CharacterStat.Health, 3f);
            var log = new DebugLogTask { Message = "Seen:", Number = Of(GetParameter.Health) };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Seen: health of Target = 3"));
            log.Tick(_rig.Context, 0.1f);
        }

        // ---------- the asset keeps the wires ----------

        [Test]
        public void Graph_ACompareOfAGet_ASetOfAGet_AreWrittenAndReadBack()
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            g.Variable("Seen Health", VariableType.Float);
            AgentState always = g.Always();
            var low = new CompareCondition(Of(GetParameter.HealthPercent), NumberComparison.Less, 30f)
            {
                B = new GetValue(GetParameter.MaxHealth)
            };
            AgentState branch = g.Block(new BranchTask(low));
            AgentState set = g.Block(new SetNumberTask { Variable = "Seen Health", Number = Of(GetParameter.Health) });
            g.Do(always, branch);
            g.Then(branch, set);
            BehaviorGraph graph = g.Build();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);

            GraphSerializer.Write(graph, asset);
            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            CollectionAssert.IsEmpty(issues.FindAll(issue => issue.IsError), string.Join("\n", issues));
            var readBranch = (BranchTask)read.Root.Children.Find(node => node.Tasks[0] is BranchTask).Tasks[0];
            var readCompare = (CompareCondition)readBranch.Condition;
            Assert.IsInstanceOf<GetValue>(readCompare.A);
            Assert.AreEqual(GetParameter.HealthPercent, ((GetValue)readCompare.A).Parameter);
            Assert.AreEqual(Whose.WiredObject, ((GetValue)readCompare.A).Of);
            Assert.AreEqual(TARGET, ((GetValue)readCompare.A).Object);
            Assert.AreEqual(GetParameter.MaxHealth, ((GetValue)readCompare.B).Parameter);
            var readSet = (SetNumberTask)read.Root.Children.Find(node => node.Tasks[0] is SetNumberTask).Tasks[0];
            Assert.AreEqual(GetParameter.Health, ((GetValue)readSet.Number).Parameter);
        }

        [Test]
        public void Validator_AGetOfTheWiredObject_WithNothingWired_IsAProblem()
        {
            var g = new BrainGraphBuilder();
            AgentState always = g.Always();
            g.Do(always, g.Block(new SetNumberTask
            {
                Variable = "Seen", Number = new GetValue { Of = Whose.WiredObject, Parameter = GetParameter.Health }
            }));
            g.Variable("Seen", VariableType.Float);
            var issues = new List<GraphIssue>();

            ConditionChecks.Check(g.Build(), issues);

            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.UnwiredValue), string.Join("\n", issues));
        }

        /// <summary>A character the game describes (the player): its numbers and states as the test sets them.</summary>
        internal sealed class Status : MonoBehaviour, ICharacterStatus
        {
            private readonly float[] _stats = new float[System.Enum.GetValues(typeof(CharacterStat)).Length];
            private readonly bool[] _hasStat = new bool[System.Enum.GetValues(typeof(CharacterStat)).Length];
            private readonly bool[] _states = new bool[System.Enum.GetValues(typeof(CharacterState)).Length];
            private readonly bool[] _hasState = new bool[System.Enum.GetValues(typeof(CharacterState)).Length];

            public void Say(CharacterStat stat, float value)
            {
                _stats[(int)stat] = value;
                _hasStat[(int)stat] = true;
            }

            public void Forget(CharacterStat stat) => _hasStat[(int)stat] = false;

            public float Said(CharacterStat stat) => _stats[(int)stat];

            public void Say(CharacterState state, bool value)
            {
                _states[(int)state] = value;
                _hasState[(int)state] = true;
            }

            public void Forget(CharacterState state) => _hasState[(int)state] = false;

            public bool TryGetStat(CharacterStat stat, out float value)
            {
                value = _stats[(int)stat];
                return _hasStat[(int)stat];
            }

            public bool TryGetState(CharacterState state, out bool value)
            {
                value = _states[(int)state];
                return _hasState[(int)state];
            }

            public bool TryGetCustom(string name, out float value)
            {
                value = 0f;
                return false;
            }
        }
    }
}
