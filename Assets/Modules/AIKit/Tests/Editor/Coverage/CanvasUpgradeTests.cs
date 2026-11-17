using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The editor's upgrade steps when the canvas opens a graph (GraphDocument.Reload): a step runs once — a graph of the
    /// current format keeps the user's own variables of the old names, a squad's command graph keeps what its AI Squad
    /// binds; an ability name no asset has is kept in the graph and shown on its node after every save, not erased by the
    /// save the opening makes.
    /// </summary>
    public sealed class CanvasUpgradeTests
    {
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
        }

        private BehaviorGraphAsset NewAsset()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            return asset;
        }

        private BehaviorGraphAsset Written(BehaviorGraph graph)
        {
            BehaviorGraphAsset asset = NewAsset();
            GraphSerializer.Write(graph, asset);
            return asset;
        }

        // ---------- the scene-object variables step runs once ----------

        [Test]
        public void SceneObjectVariablesUpgrade_ACurrentFormatGraphsPointA_SurvivesReopen()
        {
            BehaviorGraphAsset asset = NewAsset();
            var document = new GraphDocument(asset);
            document.DeclareVariable("Point A", VariableType.Transform);
            AgentState move = document.AddStateWithTask(null, "aikit.task.move-to", Vector2.zero);
            var moveTo = (MoveToTask)move.Tasks[0];
            GraphVariableNode node = document.AddVariableNodeAt("Point A", new Vector2(-200f, 0f));
            document.SetReader(node, moveTo);
            document.BindField(moveTo, typeof(MoveToTask).GetField(nameof(MoveToTask.Target)), "Point A");
            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion, "Set-up: a graph of the current format");

            var reopened = new GraphDocument(asset);           // what a reopening or an Undo does

            Assert.IsTrue(reopened.IsDeclared("Point A"), "The user's own variable stays");
            var read = (MoveToTask)reopened.States().Single(state => state.Tasks.FirstOrDefault() is MoveToTask).Tasks[0];
            Assert.AreEqual("Point A", read.Target, "…and the pin that reads it");
            Assert.IsTrue(asset.VariableNodes.Exists(kept => kept.Variable == "Point A" && kept.Readers.Contains(moveTo.Id)),
                "Its node and its wire stay");
        }

        [Test]
        public void SceneObjectVariablesUpgrade_ASquadsCommandGraphOfBefore_KeepsWhatItsSquadBinds()
        {
            var g = new BrainGraphBuilder(new SquadBrainTask());
            g.Variable("Patrol Zone", VariableType.Transform);
            var march = new MarchToTargetTask { Target = "Patrol Zone" };
            g.Chain(g.Decision(new GatherDecision(), 1), march);
            BehaviorGraph graph = g.Build();
            BehaviorGraphAsset asset = Written(graph);
            asset.FormatVersion = 0;                            // written before step 90

            bool changed = new SceneObjectVariablesUpgrade().Apply(asset, graph, new List<GraphIssue>());

            Assert.IsFalse(changed, "An AI Squad binds its command graph's objects: nothing of it goes");
            Assert.IsTrue(graph.Variables.Exists(variable => variable.Name == "Patrol Zone"));
            Assert.AreEqual("Patrol Zone", march.Target);
        }

        [Test]
        public void SceneObjectVariablesUpgrade_AVariableOfTheNameButNotAnObject_Stays()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Point A", VariableType.Float);
            var wait = new WaitTask(1f);
            wait.Seconds = (BlackboardValue<float>)ValueFields.ToVariable(wait.Seconds, "Point A");
            g.Chain(g.Decision(new DailyRoutineReasoner(), 1), wait);
            BehaviorGraph graph = g.Build();
            BehaviorGraphAsset asset = Written(graph);
            asset.FormatVersion = 0;                            // written before step 90

            Assert.IsFalse(new SceneObjectVariablesUpgrade().Apply(asset, graph, new List<GraphIssue>()),
                "A number of that name was never a scene object of the character");
            Assert.IsTrue(graph.Variables.Exists(variable => variable.Name == "Point A"));
            Assert.AreEqual("Point A", ValueFields.NameOf(wait.Seconds), "The pin still reads it");
        }

        // ---------- the steps themselves ----------

        // Steps of the tests: a parameter in their constructors keeps them off every real graph (they are given by hand)
        private sealed class Throwing : IEditorGraphUpgrade
        {
            public Throwing(string why) => Why = why;

            public string Why { get; }

            public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues) =>
                throw new System.InvalidOperationException(Why);
        }

        private sealed class Changing : IEditorGraphUpgrade
        {
            public Changing(int runs) => Runs = runs;

            public int Runs { get; private set; }

            public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues)
            {
                Runs++;
                return true;
            }
        }

        [Test]
        public void EditorGraphUpgrades_AStepThatThrows_IsAWarning_TheOthersRun()
        {
            var g = new BrainGraphBuilder();
            g.Chain(g.Decision(new DailyRoutineReasoner(), 1), new WaitTask(1f));
            BehaviorGraph graph = g.Build();
            BehaviorGraphAsset asset = Written(graph);
            var issues = new List<GraphIssue>();
            var changing = new Changing(0);

            bool changed = EditorGraphUpgrades.Apply(asset, graph, issues, new IEditorGraphUpgrade[] { new Throwing("boom"), changing });

            Assert.IsTrue(changed, "The step after it ran and changed the graph");
            Assert.AreEqual(1, changing.Runs);
            GraphIssue failed = issues.Single(issue => issue.Code == GraphIssueCode.OldModel);
            Assert.IsFalse(failed.IsError, "A warning: the graph opens");
            StringAssert.Contains(nameof(Throwing), failed.Message, "It names the step");
            StringAssert.Contains("boom", failed.Message);
        }

        // A step of the tests the editor could make by itself: it is no step of a real graph
        public sealed class TestOnlyStep : IEditorGraphUpgrade
        {
            public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues) => false;
        }

        [Test]
        public void EditorGraphUpgrades_Steps_NoneOfTheTests()
        {
            Assert.IsFalse(EditorGraphUpgrades.Steps.Any(step => step.GetType().Assembly == typeof(CanvasUpgradeTests).Assembly),
                "A step of a test assembly never runs on the project's graphs");
            Assert.IsTrue(EditorGraphUpgrades.Steps.Any(step => step is AbilityNameUpgrade), "The module's own are there");
        }

        // ---------- Upgrade All Graphs ----------

        [Test]
        public void UpgradeGraphs_AGraphChangedOnlyByAnEditorStep_IsSavedAndCounted()
        {
            const string ROOT = "Assets/AIKitTempUpgradeTest";
            if (UnityEditor.AssetDatabase.IsValidFolder(ROOT))
                UnityEditor.AssetDatabase.DeleteAsset(ROOT);
            UnityEditor.AssetDatabase.CreateFolder("Assets", "AIKitTempUpgradeTest");
            try
            {
                AbilityAsset ability = AbilityAsset.Create("Zz Upgrade Grenade", new SelfDelivery());
                UnityEditor.AssetDatabase.CreateAsset(ability, ROOT + "/Grenade.asset");

                var block = new UseAbilityTask();
                var g = new BrainGraphBuilder();
                g.Chain(g.Decision(new UseAnyAbilityReasoner(), 1), block);
                BehaviorGraph built = g.Build();
                NodeTitles.NameByType(built);
                var named = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
                GraphSerializer.Write(built, named);
                named.Find(block.Id).Data = "{\"Name\":\"Use Ability\",\"AbilityId\":\"Zz Upgrade Grenade\"}";
                UnityEditor.AssetDatabase.CreateAsset(named, ROOT + "/Named.asset");
                Assert.AreEqual(GraphUpgrades.Current, named.FormatVersion, "Set-up: the current format");

                var old = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
                OldModelAssets.Write(old, OldModelAssets.REFLEX);
                UnityEditor.AssetDatabase.CreateAsset(old, ROOT + "/Old.asset");
                string oldBefore = JsonUtility.ToJson(old);
                LogAssert.ignoreFailingMessages = true;

                UpgradeGraphs.Result result = UpgradeGraphs.UpgradeIn(new[] { ROOT });

                Assert.AreEqual(1, result.Upgraded, "Changed by the editor's step only: counted");
                Assert.IsFalse(UnityEditor.EditorUtility.IsDirty(named), "…and saved");
                named.MarkChanged();
                AgentNode read = null;
                GraphSerializer.Read(named, new List<GraphIssue>()).VisitNodes(node =>
                {
                    if (node is UseAbilityTask)
                        read = node;
                });
                Assert.AreSame(ability, ((UseAbilityTask)read).Ability, "Saved with the asset linked");
                StringAssert.Contains(ROOT + "/Old.asset", result.Report);
                StringAssert.Contains("read-only", result.Report, "A graph the code cannot read is listed");
                Assert.AreEqual(oldBefore, JsonUtility.ToJson(old), "…and left as it is");
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(ROOT);
            }
        }

        // ---------- an ability name no asset has ----------

        [Test]
        public void AbilityNameUpgrade_AnUnresolvedName_IsKeptAndShown_AfterTheOpeningSave()
        {
            var block = new UseAbilityTask();
            var g = new BrainGraphBuilder();
            AgentState[] chain = g.Chain(g.Decision(new UseAnyAbilityReasoner(), 1), block);
            chain[0].Name = "Old Name";                         // an older graph's own name: the opening saves
            BehaviorGraphAsset asset = Written(g.Build());
            asset.Find(block.Id).Data = "{\"Name\":\"Use Ability\",\"AbilityId\":\"Fireball\"}";
            asset.MarkChanged();

            for (int open = 1; open <= 2; open++)
            {
                var document = new GraphDocument(asset);

                Assert.IsTrue(document.Issues.Any(issue => issue.Code == GraphIssueCode.NoAbility &&
                                                           issue.RecordId == block.Id &&
                                                           issue.Message.Contains("Fireball")),
                    $"Open {open}: the block says which ability it named");
                StringAssert.Contains("Fireball", asset.Find(block.Id).Data, $"Open {open}: the name is kept in the graph");
            }
        }

        [Test]
        public void AbilityNameUpgrade_AnAssetGivenTheNameLater_IsFoundAtTheNextOpen()
        {
            const string ROOT = "Assets/AIKitTempAbilityName";
            if (UnityEditor.AssetDatabase.IsValidFolder(ROOT))
                UnityEditor.AssetDatabase.DeleteAsset(ROOT);
            UnityEditor.AssetDatabase.CreateFolder("Assets", "AIKitTempAbilityName");
            try
            {
                AbilityAsset ability = AbilityAsset.Create("Zz Not Yet", new SelfDelivery());
                UnityEditor.AssetDatabase.CreateAsset(ability, ROOT + "/Fire.asset");
                var block = new UseAbilityTask();
                var g = new BrainGraphBuilder();
                g.Chain(g.Decision(new UseAnyAbilityReasoner(), 1), block);
                BehaviorGraph built = g.Build();
                NodeTitles.NameByType(built);
                BehaviorGraphAsset asset = Written(built);
                asset.Find(block.Id).Data = "{\"Name\":\"Use Ability\",\"AbilityId\":\"Zz Fireball\"}";
                asset.MarkChanged();
                Assert.IsTrue(new GraphDocument(asset).Issues.Any(issue => issue.Code == GraphIssueCode.NoAbility),
                    "Set-up: no asset has the name yet");

                ability.Name = "Zz Fireball";                  // the asset's name put right: no asset more, no asset less

                var reopened = new GraphDocument(asset);
                AgentNode read = null;
                reopened.Graph.VisitNodes(node =>
                {
                    if (node is UseAbilityTask)
                        read = node;
                });
                Assert.AreSame(ability, ((UseAbilityTask)read).Ability, "Found by its name at the next open");
                Assert.IsFalse(reopened.Issues.Any(issue => issue.Code == GraphIssueCode.NoAbility && issue.Message.Contains("Zz Fireball")));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(ROOT);
            }
        }
    }
}
