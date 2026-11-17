using System.Collections.Generic;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The graph window and the inspector pickers: whether the followed character runs the shown graph is answered without
    /// compiling the asset when the character was given it (a compile logs every problem of a half-edited graph again);
    /// selecting an object beside characters does not switch the window to one of them; a tag picker does not write
    /// "Untagged" into an empty tag by being drawn.
    /// </summary>
    public sealed class CanvasWindowTests
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

        private GameObject Made(string name, Transform parent = null)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            if (parent != null)
                go.transform.SetParent(parent, false);
            else
                _created.Add(go);
            return go;
        }

        // ---------- the followed character runs the shown graph ----------

        [Test]
        public void GraphWindow_ACharacterGivenTheShownAsset_RunsItsGraph_WithoutACompile()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            var document = new GraphDocument(asset);
            AgentState branch = document.AddStateWithTask(null, "aikit.task.branch", Vector2.zero);
            var compare = (CompareCondition)document.AddConditionTo(branch.Tasks[0], "aikit.condition.compare", Vector2.zero);
            compare.Variable = "Typo";
            document.Touch();                               // an edit: the next compile reads the asset anew
            var running = new BehaviorGraph(new AgentState("Root"));    // the graph the character started with

            Assert.IsTrue(BehaviorGraphWindow.RunsThisGraph(running, asset, asset));

            LogAssert.NoUnexpectedReceived();               // a compile would log the graph's error again
        }

        [Test]
        public void GraphWindow_ACharacterOfAnotherAsset_RunsTheShownGraphOnlyIfItIsTheCompiledOne()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            var other = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            _created.Add(other);
            var document = new GraphDocument(asset);
            document.AddStateWithTask(null, "aikit.task.wait", Vector2.zero);
            LogAssert.ignoreFailingMessages = true;          // the graph has no Brain: its compile says so

            Assert.IsFalse(BehaviorGraphWindow.RunsThisGraph(new BehaviorGraph(new AgentState("Root")), other, asset));
            Assert.IsTrue(BehaviorGraphWindow.RunsThisGraph(asset.Compile(), other, asset));
        }

        // ---------- the character a selected object belongs to ----------

        [Test]
        public void GraphWindow_SelectingAnObjectBesideCharacters_KeepsTheFollowedOne()
        {
            Transform island = Made("Island 2").transform;
            GameObject zone = Made("Patrol Zone", island);
            GameObject skeleton = Made("Skeleton 1", island);
            var first = skeleton.AddComponent<AICharacter>();
            var second = Made("Skeleton 2", island).AddComponent<AICharacter>();
            GameObject hand = Made("Hand", skeleton.transform);

            Assert.IsNull(BehaviorGraphWindow.CharacterOf(zone), "An object beside the characters is none of theirs");
            Assert.IsNull(BehaviorGraphWindow.CharacterOf(island.gameObject), "A group of characters is no one of them");
            Assert.AreSame(first, BehaviorGraphWindow.CharacterOf(hand), "A part of a character is its");
            Assert.AreSame(second, BehaviorGraphWindow.CharacterOf(second.gameObject));
        }

        [Test]
        public void GraphWindow_ACharacterUnderItsOwnRoot_IsFoundFromTheRoot()
        {
            Transform root = Made("Grunt").transform;
            var character = Made("Model", root).AddComponent<AICharacter>();

            Assert.AreSame(character, BehaviorGraphWindow.CharacterOf(root.gameObject), "The one character under it");
        }

        // ---------- a tag picker writes nothing it was not given ----------

        [Test]
        public void PickerDrawer_AnEmptyTag_StaysEmpty_WhenNothingIsPicked()
        {
            string shown = PickerDrawer.ShownTag("");

            Assert.AreEqual("Untagged", shown, "The field has no word for none");
            Assert.AreEqual("", PickerDrawer.TagToStore("", shown), "Drawn and left alone: still empty (hits nobody)");
            Assert.AreEqual("Enemy", PickerDrawer.TagToStore("", "Enemy"), "A tag picked is stored");
            Assert.AreEqual("Player", PickerDrawer.TagToStore("Player", "Player"));
        }
    }
}
