using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 9: the editor's document — every edit of the graph window lands in the asset (nodes, the Brain's wires, a
    /// decision's Do, a block's outputs, a Branch's question, sensors wired into Detection, variables, removal with the
    /// arrows into a removed node and the conditions wired into an AND); a graph of the old model opens for viewing
    /// only and no edit touches it; and the AI cheats (trace, freeze, spawn).
    /// </summary>
    public class GraphEditorTests
    {
        private const string BRAIN = "aikit.task.combat-brain";
        private const string ROUTINE = "aikit.reasoner.daily-routine";
        private const string WAIT = "aikit.task.wait";
        private const string BRANCH = "aikit.task.branch";
        private const string COMPARE = "aikit.condition.compare";

        private readonly List<Object> _created = new();
        private BehaviorGraphAsset _asset;
        private GraphDocument _document;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _asset.name = "Edited";
            _created.Add(_asset);
            _document = new GraphDocument(_asset);
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

        // The asset read back as it is now (no logging: some tests leave a graph the validator would warn about)
        private BehaviorGraph Compiled() => GraphSerializer.Read(_asset, new List<GraphIssue>());

        [Test]
        public void Edits_LandInTheAsset()
        {
            AgentState brain = _document.AddStateWithTask(null, BRAIN, Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, ROUTINE, Vector2.zero);
            AgentState wait = _document.AddStateWithTask(null, WAIT, new Vector2(10f, 20f));
            AgentState branch = _document.AddStateWithTask(null, BRANCH, Vector2.zero);
            _document.Connect(brain, routine);                                      // the Brain's wire
            _document.Connect(routine, wait);                                       // the decision's Do
            _document.Connect(wait, branch, TransitionTrigger.TasksSucceeded);      // the block's Done
            _document.AddConditionTo(branch.Tasks[0], COMPARE, Vector2.zero);
            _document.AddShape("aikit.sensor.radius", Vector2.zero);
            _document.DeclareVariable("Seen", VariableType.Transform);
            _document.SetSensorVariable(_document.Detection(), "Seen");
            _document.DeclareVariable("Speed", VariableType.Float);

            BehaviorGraph graph = Compiled();

            CollectionAssert.AreEqual(new[] { "Brain", "Daily Routine", "Wait", "Branch" },
                graph.Root.Children.Select(state => state.DisplayName));
            Assert.AreEqual(brain.Id, graph.Brain?.Id, "The graph starts in its Brain");
            AgentState decision = graph.Brain.Transitions.Single().Target;
            Assert.IsInstanceOf<DailyRoutineReasoner>(decision.Reasoner);
            AgentState block = decision.Transitions.Single().Target;
            Assert.IsInstanceOf<WaitTask>(block.Tasks.Single());
            Transition done = block.Transitions.Single();
            Assert.AreEqual(TransitionTrigger.TasksSucceeded, done.Trigger);
            Assert.IsInstanceOf<CompareCondition>(((BranchTask)done.Target.Tasks.Single()).Condition);
            DetectionTask detection = graph.Root.Tasks.OfType<DetectionTask>().Single();
            Assert.AreEqual("Seen", detection.StoreIn);
            Assert.IsInstanceOf<RadiusSensorTask>(detection.Sensors.Single());
            CollectionAssert.AreEqual(new[] { "Seen", "Speed" }, graph.Variables.Select(variable => variable.Name));
            Assert.AreEqual(new Vector2(10f, 20f), _asset.Find(wait.Id).Position, "Where it was put");
        }

        [Test]
        public void Remove_ANode_TakesTheArrowsIntoIt()
        {
            AgentState brain = _document.AddStateWithTask(null, BRAIN, Vector2.zero);
            AgentState routine = _document.AddStateWithTask(null, ROUTINE, Vector2.zero);
            AgentState wait = _document.AddStateWithTask(null, WAIT, Vector2.zero);
            _document.Connect(brain, routine);
            _document.Connect(routine, wait);

            _document.Remove(routine);
            BehaviorGraph graph = Compiled();

            CollectionAssert.AreEqual(new[] { "Brain", "Wait" }, graph.Root.Children.Select(state => state.DisplayName));
            CollectionAssert.IsEmpty(graph.Brain.Transitions, "The Brain's wire into it went with it");
            Assert.IsTrue(_document.Issues.Exists(issue => issue.Code == GraphIssueCode.NotInChain && issue.RecordId == wait.Id),
                "Its chain is gone: no decision leads to the block any more");
        }

        [Test]
        public void Remove_AConditionWiredIntoAnAnd_TheOtherStays()
        {
            AgentState branch = _document.AddStateWithTask(null, BRANCH, Vector2.zero);
            var ask = (BranchTask)branch.Tasks[0];
            AgentCondition first = _document.AddConditionTo(ask, COMPARE, Vector2.zero);
            AgentCondition second = _document.AddConditionTo(ask, COMPARE, Vector2.zero);
            Assert.IsInstanceOf<AllCondition>(ask.Condition, "Set-up: a second question makes an AND of the two");

            _document.Remove(first);

            var read = (BranchTask)Compiled().Root.Children.Single().Tasks.Single();
            var and = (AllCondition)read.Condition;
            Assert.AreEqual(1, and.Conditions.Count);
            Assert.AreEqual(second.Id, and.Conditions[0].Id);
        }

        [TestCase(OldModelAssets.REFLEX)]
        [TestCase(OldModelAssets.NESTED_NODE)]
        [TestCase(OldModelAssets.CONDITIONS_ON_AN_ARROW)]
        [TestCase(OldModelAssets.CONDITIONS_ON_ENTERING)]
        [TestCase(OldModelAssets.SIGNAL_ARROW)]
        [TestCase(OldModelAssets.CHANGE_ARROW)]
        public void Document_AGraphOfTheOldModel_OpensReadOnly_AndAnEditChangesNothing(string part)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            asset.name = "Old";
            _created.Add(asset);
            OldModelAssets.Write(asset, part);
            string saved = JsonUtility.ToJson(asset);

            var document = new GraphDocument(asset);
            AgentState added = document.AddStateWithTask(null, WAIT, Vector2.zero);

            Assert.IsTrue(document.IsReadOnly, "Opened for viewing only");
            Assert.IsTrue(document.Issues.Exists(issue => issue.Code == GraphIssueCode.OldModel),
                string.Join(" | ", document.Issues));
            Assert.IsNull(added, "The edit is refused: writing the graph back would drop the old parts");
            Assert.AreEqual(saved, JsonUtility.ToJson(asset), "The asset is as it was saved");
        }

        [Test]
        public void ElementInspector_FieldsOfANode_DrawWithoutErrors()
        {
            // The inspector is IMGUI: drawing outside OnGUI throws, so only the field walk is checked here
            var wait = new WaitTask(1f) { Seconds = BlackboardValue<float>.Variable("T") };
            Assert.Contains("T", GraphValidator.VariablesOf(wait).ToList());
        }

        // ---------- cheats ----------

        private AICharacter SceneAgent(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.SetActive(false);
            var agent = go.AddComponent<AICharacter>();
            agent.Simulation = AICharacter.SimulationMode.Manual;
            var body = new TestActuator();
            agent.SetParts(body, new DirectNavigator(body));
            agent.Initialize();
            go.SetActive(true);
            return agent;
        }

        [Test]
        public void Cheats_TraceAndFreeze_EveryAgent()
        {
            AICharacter first = SceneAgent("First");
            AICharacter second = SceneAgent("Second");

            StringAssert.Contains("on", AICheats.Execute("ai.trace on"));
            Assert.IsTrue(first.IsTracing && second.IsTracing);
            AICheats.Execute("ai.freeze on");
            Assert.IsFalse(first.IsThinking || second.IsThinking);
            AICheats.Execute("ai.freeze off");
            AICheats.Execute("ai.trace off");

            Assert.IsTrue(first.IsThinking);
            Assert.IsFalse(first.IsTracing);
        }

        [Test]
        public void Cheats_Spawn_ARegisteredPrefab_AtThePoint()
        {
            var prefab = new GameObject("Grunt");
            _created.Add(prefab);
            AICheats.Register("grunt", prefab);
            try
            {
                string answer = AICheats.Execute("ai.spawn grunt 1 2 3");

                GameObject spawned = GameObject.Find("Grunt(Clone)");
                _created.Add(spawned);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), spawned.transform.position, answer);
                StringAssert.Contains("unknown name", AICheats.Execute("ai.spawn nobody"));
                StringAssert.Contains("unknown command", AICheats.Execute("ai.dance"));
                StringAssert.Contains("nothing to do", AICheats.Execute(""));
            }
            finally
            {
                AICheats.Unregister("grunt");
            }
        }
    }
}
