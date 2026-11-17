using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AIKit.Tests
{
    /// <summary>
    /// The blackboard panel of the graph window: for every piece of data of a behavior — who writes it and who reads
    /// it; the panel on the canvas lists the same, one row per variable.
    /// </summary>
    public sealed class BlackboardUsageTests
    {
        private const string PLAYER = "Player";
        private const string ATTACKS = "Attacks";
        private const string PAUSE = "Pause";
        private const float WIRED_PAUSE = 7.5f;

        private BehaviorGraphAsset _asset;
        private GraphDocument _document;
        private AttackTask _strike;
        private WaitTask _wait;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
            _document.DeclareVariable(PLAYER, VariableType.Transform);
            _document.DeclareVariable(ATTACKS, VariableType.Float);
            _document.Graph.Root.AddTask(new RadiusSensorTask { StoreIn = PLAYER, IsBackground = true });

            AgentState chase = _document.AddStateWithTask(null, "aikit.task.chase", Vector2.zero);
            ((ChaseTask)chase.Tasks[0]).Target = PLAYER;
            AgentState strike = _document.AddStateWithTask(null, "aikit.task.attack", Vector2.zero);
            _strike = (AttackTask)strike.Tasks[0];
            _strike.Target = PLAYER;
            AgentState add = _document.AddStateWithTask(null, "aikit.task.add-number", Vector2.zero);
            ((AddNumberTask)add.Tasks[0]).Variable = ATTACKS;
            AgentState branch = _document.AddStateWithTask(null, "aikit.task.branch", Vector2.zero);
            var compare = (CompareCondition)_document.AddConditionTo(branch.Tasks[0], "aikit.condition.compare", Vector2.zero);
            compare.Variable = ATTACKS;
            _document.Touch();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        // ---------- the character's own numbers and states: the Character section ----------

        [Test]
        public void CharacterSection_EveryNumberAndStateOfTheStatus_TheBodysOwnNumbersBeforePlay_NothingCopied()
        {
            LogAssert.ignoreFailingMessages = true;      // a NavMeshAgent without a baked mesh complains
            var go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                go.SetActive(false);
                var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.speed = 3.5f;
                agent.acceleration = 12f;
                var character = go.AddComponent<AICharacter>();
                var body = new NavMeshBody { TurnSpeed = 360f };
                character.Body = body;

                IReadOnlyList<CharacterRow> rows = CharacterSection.Rows;
                Assert.AreEqual(System.Enum.GetValues(typeof(CharacterStat)).Length +
                                System.Enum.GetValues(typeof(CharacterState)).Length, rows.Count, "Every number and state");
                CharacterRow Row(string name) => rows.Single(row => row.Name == name);

                Assert.AreEqual("3.5", CharacterSection.DesignValue(Row("Run Speed"), character), "The body's own, as it describes it");
                Assert.AreEqual("360", CharacterSection.DesignValue(Row("Turn Speed"), character));
                Assert.AreEqual("12", CharacterSection.DesignValue(Row("Acceleration"), character));
                Assert.AreEqual("—", CharacterSection.DesignValue(Row("Health %"), character), "Nothing tells it before Play");
                Assert.AreEqual("—", CharacterSection.DesignValue(Row("Is Winding Up"), character));
                Assert.AreEqual("bool", Row("Is Winding Up").Kind);
                Assert.AreEqual("float", Row("Health").Kind);
                Assert.IsNull(character.Context, "Listed without waking the character up");
                CollectionAssert.IsEmpty(BlackboardUsage.OfCharacter(character), "No number of the character is a variable any more");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CharacterSection_InPlay_TheLiveValueOfTheStatus_NoDataSaysSo()
        {
            var rig = new GraphRig(trace: false);
            rig.Actuator.Move(Vector3.right, 1f);
            CharacterRow Row(string name) => CharacterSection.Rows.Single(row => row.Name == name);

            Assert.AreEqual("4", CharacterSection.LiveValue(Row("Speed"), rig.Agent));
            Assert.AreEqual("yes", CharacterSection.LiveValue(Row("Is Moving"), rig.Agent));
            Assert.AreEqual("no data", CharacterSection.LiveValue(Row("Health"), rig.Agent));
            Assert.AreEqual("no data", CharacterSection.LiveValue(Row("Is Swimming"), rig.Agent));
            GraphRig.DestroyAll();
        }

        [Test]
        public void CharacterSection_ANumberDroppedOnACompare_IsAGetInA_ThenInB_SavedWithTheGraph()
        {
            AgentState branch = _document.States().Single(state => state.Tasks[0] is BranchTask);
            var compare = (CompareCondition)((BranchTask)branch.Tasks[0]).Condition;
            CharacterRow health = CharacterSection.Rows.Single(row => row.Name == "Health %");

            AgentNode a = CharacterSection.Wire(_document, health, compare, new Vector2(40f, 50f));
            AgentNode b = CharacterSection.Wire(_document, CharacterSection.Rows.Single(row => row.Name == "Max Health"),
                compare, Vector2.zero);

            Assert.AreSame(a, compare.A);
            Assert.AreSame(b, compare.B);
            Assert.AreEqual(GetParameter.HealthPercent, ((GetValue)compare.A).Parameter);
            Assert.AreEqual(Whose.Me, ((GetValue)compare.A).Of, "About the character the graph runs on, until Of says else");
            Assert.AreEqual(new Vector2(40f, 50f), _document.PositionOfNode(a), "It stands where it was dropped");
            var reread = new GraphDocument(_asset);
            var readCompare = (CompareCondition)((BranchTask)reread.States().Single(state => state.Tasks[0] is BranchTask).Tasks[0]).Condition;
            Assert.IsInstanceOf<GetValue>(readCompare.A, "Saved with the graph");
        }

        [Test]
        public void CharacterSection_AStateDroppedOnABranchOrADecision_IsAnIsInItsQuestion_JoinedByAnAnd()
        {
            AgentState branch = _document.States().Single(state => state.Tasks[0] is BranchTask);
            CharacterRow blocking = CharacterSection.Rows.Single(row => row.Name == "Is Blocking");

            Assert.IsNotNull(CharacterSection.Wire(_document, blocking, branch, Vector2.zero));
            var question = ((BranchTask)branch.Tasks[0]).Condition as AllCondition;
            Assert.IsNotNull(question, "The Compare it had and the Is: both must hold");
            Assert.IsTrue(question.Conditions.Exists(condition => condition is IsCondition { State: CharacterState.Blocking }));

            AgentState decision = _document.AddStateWithTask(null, "aikit.reasoner.watch", Vector2.zero);
            Assert.IsNotNull(CharacterSection.Wire(_document, blocking, decision, Vector2.zero));
            Assert.IsInstanceOf<IsCondition>(decision.Reasoner.Condition, "Into the decision's Condition pin");
        }

        [Test]
        public void CharacterSection_ARowDroppedOnANodeThatDoesNotReadIt_IsNotWired()
        {
            AgentState chase = _document.States().Single(state => state.Tasks[0] is ChaseTask);

            Assert.IsNull(CharacterSection.Wire(_document, CharacterSection.Rows.Single(row => row.Name == "Health"), chase, Vector2.zero));
            AgentState branch = _document.States().Single(state => state.Tasks[0] is BranchTask);
            Assert.IsNull(CharacterSection.Wire(_document, CharacterSection.Rows.Single(row => row.Name == "Health"), branch, Vector2.zero),
                "A number is not a question");
        }

        [Test]
        public void Usage_AnObjectVariable_IsWrittenByItsSensor_AndReadByTheNodesThatTakeIt()
        {
            VariableUse player = BlackboardUsage.Collect(_document).Single(use => use.Name == PLAYER);

            Assert.AreEqual("Transform", player.Kind);
            Assert.IsTrue(player.Declared);
            Assert.AreEqual(1, player.Writers.Count, "The sensor that stores what it sees");
            StringAssert.StartsWith("Sensor", player.Writers[0]);
            CollectionAssert.AreEquivalent(new[] { "Chase", "Attack" }, player.Readers);
        }

        [Test]
        public void Usage_ANumber_IsWrittenByAdd_AndReadByTheBranchThatComparesIt()
        {
            VariableUse attacks = BlackboardUsage.Collect(_document).Single(use => use.Name == ATTACKS);

            Assert.AreEqual("float", attacks.Kind);
            Assert.IsTrue(attacks.Declared, "Declared on the blackboard: nodes only name it");
            CollectionAssert.AreEqual(new[] { "Add" }, attacks.Writers);
            CollectionAssert.AreEqual(new[] { "Branch" }, attacks.Readers, "The Branch whose Compare asks it");
        }

        [Test]
        public void Usage_DeltaTime_IsOnTheBlackboard_EvenWithNoOnTickWire()
        {
            VariableUse delta = BlackboardUsage.Collect(_document)
                .SingleOrDefault(use => use.Name == BehaviorGraphInstance.DELTA_TIME);

            Assert.IsNotNull(delta, "A chain that counts time needs it before its first On Tick wire");
            Assert.AreEqual("float", delta.Kind);
        }

        [Test]
        public void Numbers_WiredToAVariable_AreReadByTheRunningAgent()
        {
            // Brain → Daily Routine → Do → the Wait whose seconds are wired to Pause
            AgentState pause = WirePause();
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            AgentState always = _document.AddStateWithTask(null, "aikit.reasoner.daily-routine", Vector2.zero);
            _document.Connect(brain, always);
            _document.Connect(always, pause);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;    // the other test nodes are in no chain: the validator says so
            var rig = new GraphRig();
            rig.SetGraph(_asset.Compile());

            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Wait", 1f, 0.1f), "Set-up: the Wait runs");
            WaitTask task = rig.Instance.Graph.Root.Children.SelectMany(state => state.Tasks).OfType<WaitTask>().Single();

            Assert.AreEqual(WIRED_PAUSE, task.Seconds.Get(rig.Context), "The node reads the number from the agent's blackboard");
            GraphRig.DestroyAll();
        }

        [Test]
        public void Canvas_ANumberVariable_IsWiredIntoThePinThatReadsIt_AndUnpluggingGivesTheNumberBack()
        {
            WirePause();
            float own = new WaitTask().Seconds.Get(null);
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            Edge wire = canvas.edges.ToList().SingleOrDefault(edge => edge.output.node.title == PAUSE);
            Assert.IsNotNull(wire, "A pin that names a variable gets a visible wire from that variable's node");
            Assert.AreEqual("Seconds", wire.input.portName);

            _document.BindField(_wait, typeof(WaitTask).GetField(nameof(WaitTask.Seconds)), "");

            Assert.IsFalse(_wait.Seconds.UsesVariable, "Unplugged: a plain number of the node again");
            Assert.AreEqual(WIRED_PAUSE, _wait.Seconds.Get(null), "It keeps the variable's start value, not the old number");
            Assert.AreNotEqual(own, WIRED_PAUSE);
        }

        [Test]
        public void Variables_InTheirOwnWindow_ARowPerVariable_TheCanvasKeepsOnlyTheCharacter()
        {
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            Assert.IsEmpty(Variables(canvas), "The canvas lists no variable of the graph (15.11.2026): they have a window");

            var window = ScriptableObject.CreateInstance<VariablesWindow>();
            try
            {
                VariablesWindow.Follow(_document);
                List<string> rows = Variables(window.Panel);
                CollectionAssert.AreEquivalent(new[] { PLAYER, ATTACKS, BehaviorGraphInstance.DELTA_TIME }, rows);
                List<string> words = window.Panel.Query<Label>().ToList().Select(label => label.text).ToList();
                CollectionAssert.Contains(words, BlackboardPanel.OBJECTS, "The objects of the scene are listed together");
                Assert.IsFalse(words.Any(word => word.StartsWith("written by") || word.StartsWith("read by")),
                    "Who writes and who reads a variable is not spelled out in the panel");
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Panel_Search_LeavesTheVariablesWhoseNameHasTheText()
        {
            var panel = new BlackboardPanel(new GraphViewCanvas(), BlackboardPanel.Part.Variables);
            panel.Refresh(_document);

            panel.Filter("aTTac");
            CollectionAssert.AreEqual(new[] { ATTACKS },
                Variables(panel), "Any case, any part of the name");

            panel.Filter("");
            Assert.AreEqual(3, Variables(panel).Count, "Everything is back");
        }

        [Test]
        public void Panel_TheCharacterSection_OnTheCanvas_ReadOnly_NoSceneObjectToPick()
        {
            var canvas = new GraphViewCanvas();
            canvas.Show(_document);
            var panel = canvas.Q<BlackboardPanel>();

            List<BlackboardField> character = canvas.Query<BlackboardField>().ToList()
                .Where(field => BlackboardPanel.CharacterRowOf(field) != null).ToList();
            Assert.AreEqual(CharacterSection.Rows.Count, character.Count, "A row per number and state of the character");
            Assert.IsTrue(character.All(field => (field.capabilities & Capabilities.Renamable) == 0 &&
                                                 (field.capabilities & Capabilities.Deletable) == 0), "Read-only rows");
            CollectionAssert.Contains(canvas.Query<Label>().ToList().Select(label => label.text).ToList(), CharacterSection.TITLE);
            Assert.IsEmpty(canvas.Query<UnityEditor.UIElements.ObjectField>().ToList(),
                "No scene object is bound to a variable on the panel (C18)");

            panel.Filter("winding");
            CollectionAssert.AreEqual(new[] { "Is Winding Up" }, panel.RowNames.ToList(), "The search finds the character's rows too");
        }

        // The rows of the graph's own variables (the Character section's rows are not variables)
        private static List<string> Variables(VisualElement holder) =>
            holder.Query<BlackboardField>().ToList().Where(field => BlackboardPanel.VariableOf(field) != null)
                .Select(field => field.text).ToList();

        [Test]
        public void Graph_WritesAndAsksAnyNumberAndFlagOfTheBlackboard()
        {
            var rig = new GraphRig();
            var add = new AddNumberTask { Variable = "Rage", Value = 2f };
            var raise = new SetFlagTask { Variable = "Angry", Value = true };
            var enough = new CompareCondition("Rage", NumberComparison.GreaterOrEqual, 4f);
            var angry = new CompareCondition("Angry", VariableType.Bool);

            Assert.IsFalse(enough.Check(rig.Context));
            add.Tick(rig.Context, 0.1f);
            add.Tick(rig.Context, 0.1f);
            raise.Tick(rig.Context, 0.1f);

            Assert.IsTrue(enough.Check(rig.Context), "Two additions of 2 make 4");
            Assert.IsTrue(angry.Check(rig.Context));
            Assert.AreEqual("Rage is at least 4", ArrowPhrases.Phrase(enough, ""));
            Assert.AreEqual("Angry is on", ArrowPhrases.Phrase(angry, ""));
            GraphRig.DestroyAll();
        }

        [Test]
        public void Panel_Groups_ObjectsTogether_ANumberUnderTheNodeThatReadsIt()
        {
            List<VariableUse> uses = BlackboardUsage.Collect(_document);

            Assert.AreEqual(BlackboardPanel.OBJECTS, BlackboardPanel.GroupOf(uses.Single(use => use.Name == PLAYER)));
            Assert.AreEqual("Branch", BlackboardPanel.GroupOf(uses.Single(use => use.Name == ATTACKS)));
        }

        // A Wait whose seconds come from a number of the blackboard
        private AgentState WirePause()
        {
            AgentState pause = _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            _wait = (WaitTask)pause.Tasks[0];
            _document.DeclareVariable(PAUSE, VariableType.Float);
            _document.SetDefault(PAUSE, WIRED_PAUSE);
            _document.BindField(_wait, typeof(WaitTask).GetField(nameof(WaitTask.Seconds)), PAUSE);
            return pause;
        }
    }
}
