using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The blackboard of the graph window: the kind and the start value of a variable, a fresh name, renaming and deleting
    /// (refused when it would break a name), where the panel lists a variable and that a redraw keeps the search; who
    /// reads an object a Get in a Compare asks about; the Character section's rows — each a Get or an Is of its own
    /// number or state, live values said as the status says them.
    /// </summary>
    public sealed class CanvasBlackboardTests
    {
        private BehaviorGraphAsset _asset;
        private GraphDocument _document;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        private DetectionTask DetectionWriting(string variable)
        {
            _document.AddShape("aikit.sensor.radius", new Vector2(-400f, 0f));
            DetectionTask detection = _document.Detection();
            _document.SetSensorVariable(detection, variable);
            return detection;
        }

        // ---------- the kind and the start value ----------

        [Test]
        public void GraphDocument_TypeOfVariable_AndDefaultOf()
        {
            _document.DeclareVariable("Alarm", VariableType.Bool);
            _document.SetDefault("Alarm", true);
            _document.DeclareVariable("Rage", VariableType.Float);
            _document.SetDefault("Rage", 2.5f);
            _document.DeclareVariable("Ammo", VariableType.Int);
            _document.SetDefault("Ammo", 3);
            _document.DeclareVariable("Target", VariableType.Transform);
            DetectionWriting("Seen");
            AgentState add = _document.AddStateWithTask(null, "aikit.task.add-number", Vector2.zero);
            ((AddNumberTask)add.Tasks[0]).Variable = "Attacks";
            _document.Touch();

            Assert.AreEqual(VariableType.Bool, _document.TypeOfVariable("Alarm"));
            Assert.AreEqual(VariableType.Float, _document.TypeOfVariable("Rage"));
            Assert.AreEqual(VariableType.Int, _document.TypeOfVariable("Ammo"));
            Assert.AreEqual(VariableType.Transform, _document.TypeOfVariable("Target"));
            Assert.AreEqual(VariableType.Transform, _document.TypeOfVariable("Seen"), "What a sensor fills is an object");
            Assert.AreEqual(VariableType.Float, _document.TypeOfVariable("Attacks"), "A counter of an Add is a number");
            Assert.AreEqual(VariableType.Float, _document.TypeOfVariable("Nothing"));

            Assert.AreEqual(true, _document.DefaultOf("Alarm"));
            Assert.AreEqual(2.5f, _document.DefaultOf("Rage"));
            Assert.AreEqual(3, _document.DefaultOf("Ammo"));
            Assert.IsNull(_document.DefaultOf("Target"), "An object has no start value");
            Assert.IsNull(_document.DefaultOf("Seen"), "Nor has a name nobody declared");
        }

        // ---------- names ----------

        [Test]
        public void GraphDocument_DeclareUnique_CountsUp_AndWaitsForTouch()
        {
            _document.DeclareVariable("Target", VariableType.Transform);

            Assert.AreEqual("Target 2", _document.DeclareUnique("Target", VariableType.Transform));
            Assert.AreEqual("Target 3", _document.DeclareUnique("Target", VariableType.Transform));
            Assert.IsFalse(_asset.Variables.Exists(variable => variable.Name == "Target 2"), "Not saved by itself");

            _document.Touch();
            Assert.IsTrue(_asset.Variables.Exists(variable => variable.Name == "Target 2"));
            Assert.IsTrue(_asset.Variables.Exists(variable => variable.Name == "Target 3"));
        }

        [Test]
        public void GraphDocument_RenameVariable_RefusesEmptyTakenOrUndeclared()
        {
            _document.DeclareVariable("Rage", VariableType.Float);
            AgentState add = _document.AddStateWithTask(null, "aikit.task.add-number", Vector2.zero);
            ((AddNumberTask)add.Tasks[0]).Variable = "Attacks";
            _document.Touch();
            string before = JsonUtility.ToJson(_asset);

            Assert.IsFalse(_document.RenameVariable("Rage", ""), "Empty");
            Assert.IsFalse(_document.RenameVariable("Rage", "  "), "Blank");
            Assert.IsFalse(_document.RenameVariable("Rage", "Attacks"), "A name a node uses");
            Assert.IsFalse(_document.RenameVariable("Nope", "X"), "Not declared");
            Assert.IsFalse(_document.RenameVariable("Rage", "Rage"), "The same name");
            Assert.AreEqual(before, JsonUtility.ToJson(_asset), "Nothing changed");
        }

        [Test]
        public void GraphDocument_DeleteVariable_ASensorsStoreInLetsGo()
        {
            _document.DeclareVariable("Seen", VariableType.Transform);
            DetectionTask detection = DetectionWriting("Seen");
            AgentState chase = _document.AddStateWithTask(null, "aikit.task.chase", Vector2.zero);
            var task = (ChaseTask)chase.Tasks[0];
            task.Target = "Seen";
            _document.Touch();
            _document.EnsureVariableNodes();
            Assert.IsTrue(_document.VariableNodes.Any(node => node.Variable == "Seen"), "Set-up: its nodes are on the canvas");

            Assert.IsTrue(_document.DeleteVariable("Seen"));

            Assert.AreEqual("", detection.StoreIn, "Detection writes nowhere now");
            Assert.AreEqual("", task.Target, "The pin that read it is empty");
            Assert.IsFalse(_document.VariableNodes.Any(node => node.Variable == "Seen"), "Its nodes go");
            Assert.IsFalse(_document.DeleteVariable("Undeclared"), "A name nobody declared is not the graph's to delete");
        }

        // ---------- the panel ----------

        [Test]
        public void BlackboardPanel_GroupOf_AndRefreshKeepsTheSearch()
        {
            _document.DeclareVariable("Player", VariableType.Transform);
            _document.DeclareVariable("Spare", VariableType.Float);
            _document.DeclareVariable("Rage", VariableType.Float);
            AgentState add = _document.AddStateWithTask(null, "aikit.task.add-number", Vector2.zero);
            ((AddNumberTask)add.Tasks[0]).Variable = "Attacks";
            AgentState branch = _document.AddStateWithTask(null, "aikit.task.branch", Vector2.zero);
            var compare = (CompareCondition)_document.AddConditionTo(branch.Tasks[0], "aikit.condition.compare", Vector2.zero);
            compare.Variable = "Rage";
            _document.Touch();
            List<VariableUse> uses = BlackboardUsage.Collect(_document);
            string GroupOf(string name) => BlackboardPanel.GroupOf(uses.Single(use => use.Name == name));

            Assert.AreEqual(BlackboardPanel.OBJECTS, GroupOf("Player"));
            Assert.AreEqual(BlackboardPanel.COUNTERS, GroupOf("Attacks"), "Undeclared: a counter a node keeps");
            Assert.AreEqual(BlackboardPanel.UNUSED, GroupOf("Spare"));
            Assert.AreEqual("Branch", GroupOf("Rage"), "Under the node that reads it");

            var panel = new BlackboardPanel(new GraphViewCanvas(), BlackboardPanel.Part.Variables);
            panel.Refresh(_document);
            panel.Filter("att");
            panel.Refresh(_document);                       // a redraw of the canvas
            CollectionAssert.AreEqual(new[] { "Attacks" }, panel.RowNames.ToList(), "The search is kept through the redraw");

            var character = new BlackboardPanel(new GraphViewCanvas(), BlackboardPanel.Part.Character);
            character.Refresh(_document);
            character.Filter("att");
            CollectionAssert.AreEqual(CharacterSection.Rows
                    .Where(row => row.Name.IndexOf("att", System.StringComparison.OrdinalIgnoreCase) >= 0).Select(row => row.Name),
                character.RowNames.ToList(), "The character's rows: on the canvas's panel only");
        }

        [Test]
        public void BlackboardUsage_AGetWiredIntoACompare_ItsObjectIsReadByTheBranch()
        {
            _document.DeclareVariable("Target", VariableType.Transform);
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, "aikit.reasoner.daily-routine", new Vector2(400f, 0f));
            AgentState set = _document.AddStateWithTask(null, "aikit.task.set-flag", new Vector2(800f, 0f));
            _document.Connect(brain, routine);
            _document.Connect(routine, set, TransitionTrigger.Tick);
            AgentState branch = _document.AddStateWithTask(null, "aikit.task.branch", new Vector2(0f, 400f));
            var compare = (CompareCondition)_document.AddConditionTo(branch.Tasks[0], "aikit.condition.compare", Vector2.zero);
            var get = (GetValue)_document.AddValueTo(compare, nameof(CompareCondition.A), "aikit.value.get", Vector2.zero);
            get.Of = Whose.WiredObject;
            get.Object = "Target";
            _document.Touch();

            List<VariableUse> uses = BlackboardUsage.Collect(_document);

            CollectionAssert.Contains(uses.Single(use => use.Name == "Target").Readers, "Branch",
                "The Get in the Compare reads it for the Branch");
            CollectionAssert.AreEqual(new[] { routine.DisplayName },
                uses.Single(use => use.Name == BehaviorGraphInstance.DELTA_TIME).Writers, "The decision's On Tick chain");
        }

        // ---------- the Character section ----------

        [Test]
        public void CharacterSection_NodeOf_EveryStatRow_IsAGetOfThatParameter()
        {
            int numbers = System.Enum.GetValues(typeof(CharacterStat)).Length;
            IReadOnlyList<CharacterRow> rows = CharacterSection.Rows;

            for (int i = 0; i < rows.Count; i++)
            {
                CharacterRow row = rows[i];
                Assert.AreEqual(i >= numbers, row.IsState, $"{row.Name}: the numbers first, then the states");
                if (!row.IsState)
                {
                    var get = (GetValue)CharacterSection.NodeOf(row);
                    Assert.AreEqual(row.Stat.ToString(), get.Parameter.ToString(), row.Name);
                    Assert.AreEqual(Whose.Me, get.Of);
                }
                else
                {
                    var asked = (IsCondition)CharacterSection.NodeOf(row);
                    Assert.AreEqual(row.State, asked.State, row.Name);
                    Assert.AreEqual(Whose.Me, asked.Of);
                }
            }

            Assert.AreEqual("Health %", CharacterSection.NameOf(CharacterStat.HealthPercent));
            Assert.AreEqual("Run Speed", CharacterSection.NameOf(CharacterStat.RunSpeed));
        }

        [Test]
        public void CharacterSection_LiveAndDesignValues_NoData()
        {
            var status = new SaidStatus();
            status.Stats[CharacterStat.Health] = 12.5f;
            status.States[CharacterState.Blocking] = true;
            CharacterRow Row(string name) => CharacterSection.Rows.Single(row => row.Name == name);
            var go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var character = go.AddComponent<AICharacter>();

                Assert.AreEqual("12.5", CharacterSection.LiveValue(Row("Health"), status));
                Assert.AreEqual("yes", CharacterSection.LiveValue(Row("Is Blocking"), status));
                Assert.AreEqual("no data", CharacterSection.LiveValue(Row("Is Swimming"), status), "Not said: no data, not no");
                Assert.AreEqual("no data", CharacterSection.LiveValue(Row("Health"), null));
                Assert.AreEqual("—", CharacterSection.DesignValue(Row("Is Blocking"), character), "A state has no value before Play");
                Assert.AreEqual("—", CharacterSection.DesignValue(Row("Health"), character), "Nothing tells the health before Play");
                Assert.AreEqual("—", CharacterSection.DesignValue(Row("Run Speed"), null));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // A status that says what it was told, and nothing else
        private sealed class SaidStatus : ICharacterStatus
        {
            public readonly Dictionary<CharacterStat, float> Stats = new();
            public readonly Dictionary<CharacterState, bool> States = new();

            public bool TryGetStat(CharacterStat stat, out float value) => Stats.TryGetValue(stat, out value);

            public bool TryGetState(CharacterState state, out bool value) => States.TryGetValue(state, out value);

            public bool TryGetCustom(string name, out float value)
            {
                value = 0f;
                return false;
            }
        }
    }
}
