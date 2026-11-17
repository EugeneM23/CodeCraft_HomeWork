using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AIKit.Tests
{
    /// <summary>
    /// What the graph window does to the asset: a graph that did not read whole is not written over, a node that is
    /// removed leaves nothing under its id, a variable that is renamed or deleted follows on every character of the
    /// graph, a Compare's inputs are checked like any variable, and a problem of a task is shown on its node.
    /// </summary>
    public sealed class GraphDocumentTests
    {
        private const string TARGET = "Target";

        private BehaviorGraphAsset _asset;
        private GraphDocument _document;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
            _document.DeclareVariable(TARGET, VariableType.Transform);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
            Object.DestroyImmediate(_asset);
            ElementInspector.Character = null;
        }

        // ---------- a graph that did not read whole ----------

        [Test]
        public void Document_AnAssetWithARecordOfAGoneNodeType_OpensReadOnly_AndKeepsTheRecord()
        {
            _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            _asset.Records.Add(new GraphRecord { Id = 900, Type = "aikit.task.long-gone", Data = "{}" });
            int records = _asset.Records.Count;
            LogAssert.ignoreFailingMessages = true;     // the validator names the unknown record

            var reopened = new GraphDocument(_asset);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("opened read-only"));
            reopened.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);

            Assert.IsTrue(reopened.IsReadOnly, "A write would drop what was not read");
            Assert.AreEqual(records, _asset.Records.Count, "Nothing is written over");
            Assert.IsTrue(_asset.Records.Exists(record => record.Type == "aikit.task.long-gone"));
        }

        [Test]
        public void Document_AWholeAsset_IsEditable()
        {
            _document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);

            Assert.IsFalse(new GraphDocument(_asset).IsReadOnly);
        }

        // ---------- a node that is removed ----------

        [Test]
        public void Remove_ADecision_NoOtherDecisionIsStillNotFromIt_AndNoWireOrFrameKeepsItsId()
        {
            AgentState brain = _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);
            AgentState fight = _document.AddStateWithTask(null, "aikit.reasoner.fight", Vector2.zero);
            AgentState grenade = _document.AddStateWithTask(null, "aikit.reasoner.use-ability", Vector2.zero);
            _document.Connect(brain, fight);
            _document.Connect(brain, grenade);
            grenade.Reasoner.NotFrom.Add(fight.Id);
            _asset.VariableNodes.Add(new GraphVariableNode { Variable = TARGET, Readers = { fight.Tasks[0].Id } });
            _asset.Frames.Add(new GraphFrame { Title = "Frame", Members = { fight.Id } });
            GraphBreakpoints.Set(_asset, fight.Id, true);
            int id = fight.Id;

            _document.Remove(fight);

            CollectionAssert.DoesNotContain(grenade.Reasoner.NotFrom, id, "A node made later with the same id is not inherited");
            Assert.IsFalse(_asset.Frames[0].Members.Contains(id));
            Assert.IsFalse(GraphBreakpoints.Has(_asset, id));
        }

        // ---------- a variable renamed or deleted ----------

        [Test]
        public void RenameAndDeleteVariable_TheGraphsOwn_NoCharacterOfTheSceneIsChanged()
        {
            // Step 90 (C18): a character keeps no objects for the graph's variables — a variable is the graph's alone
            using var scene = new TempScene();
            Fresh();                                    // a new scene unloads what was made before it
            AICharacter first = Character("First");
            AICharacter second = Character("Second");
            ElementInspector.Character = first;
            string before = UnityEditor.EditorJsonUtility.ToJson(second);

            Assert.IsTrue(_document.RenameVariable(TARGET, "Enemy"));
            Assert.IsTrue(_document.DeleteVariable("Enemy"));

            Assert.AreEqual(before, UnityEditor.EditorJsonUtility.ToJson(second), "The characters are left as they were");
            Assert.IsFalse(_document.Graph.Variables.Exists(variable => variable.Name == "Enemy"));
        }

        [Test]
        public void RenameVariable_ReachesBothInputsOfACompare()
        {
            _document.DeclareVariable("Rage", VariableType.Float);
            _document.DeclareVariable("Limit", VariableType.Float);
            AgentState branch = _document.AddStateWithTask(null, "aikit.task.branch", Vector2.zero);
            var compare = (CompareCondition)_document.AddConditionTo(branch.Tasks[0], "aikit.condition.compare", Vector2.zero);
            compare.Variable = "Rage";
            compare.Other = "Limit";

            _document.RenameVariable("Rage", "Fury");
            _document.RenameVariable("Limit", "Cap");

            Assert.AreEqual("Fury", compare.Variable);
            Assert.AreEqual("Cap", compare.Other);
        }

        // ---------- the check of the graph ----------

        [Test]
        public void Validator_ACompareOfAVariableNobodyDeclared_IsAnError()
        {
            AgentState branch = _document.AddStateWithTask(null, "aikit.task.branch", Vector2.zero);
            var compare = (CompareCondition)_document.AddConditionTo(branch.Tasks[0], "aikit.condition.compare", Vector2.zero);
            compare.Variable = "Typo";
            LogAssert.ignoreFailingMessages = true;
            _document.Touch();

            Assert.IsTrue(_document.Issues.Exists(issue => issue.Code == GraphIssueCode.UnknownVariable && issue.Message.Contains("Typo")),
                "Input A is checked as input B always was");
        }

        [Test]
        public void Canvas_AProblemOfATask_IsMarkedOnItsNode()
        {
            AgentState strike = _document.AddStateWithTask(null, "aikit.task.attack", Vector2.zero);
            ((AttackTask)strike.Tasks[0]).Target = "Typo";
            LogAssert.ignoreFailingMessages = true;
            _document.Touch();
            Assert.IsTrue(_document.Issues.Exists(issue => issue.RecordId == strike.Tasks[0].Id),
                "Set-up: the problem is filed under the task's own id");

            var canvas = new GraphViewCanvas();
            canvas.Show(_document);

            List<Label> marks = canvas.Query<Label>().ToList().Where(label => label.text == "!").ToList();
            Assert.IsTrue(marks.Any(mark => mark.tooltip.Contains("Typo")), "The Strike's problem is on the Strike's node");
        }

        // (step 90: the ability picker reads the project's abilities, not the character's — NodeOptionsTests)

        private void Fresh()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
            _document.DeclareVariable(TARGET, VariableType.Transform);
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private AICharacter Character(string name)
        {
            var character = NewObject(name).AddComponent<AICharacter>();
            character.GraphAsset = _asset;
            return character;
        }
    }
}
