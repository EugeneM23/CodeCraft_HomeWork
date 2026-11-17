using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 3a, the blackboard contract (§8.1 of the handbook): the first write is a change, Set/Has/Remove for every
    /// type, tags, in-place access to a struct slot, keys that survive Remove and are dropped by Clear.
    /// </summary>
    public class BlackboardContractTests
    {
        private struct Memory
        {
            public int Hits;
            public Vector3 LastSeen;
        }

        [Test]
        public void Set_ByName_FirstWrite_IsAChange()
        {
            var blackboard = new Blackboard();
            int changes = 0;
            blackboard.Changed += _ => changes++;

            blackboard.Set("IsHurt", false);            // even the default value: the fact did not exist before

            Assert.AreEqual(1, changes);
            Assert.IsTrue(blackboard.TryGetKey("IsHurt", out BlackboardKey<bool> key));
            Assert.That(blackboard.VersionOf(key.Index), Is.GreaterThan(0));
        }

        private static void AssertSetHasRemove<T>(T value, T fallback)
        {
            var blackboard = new Blackboard();

            blackboard.Set("x", value);
            Assert.IsTrue(blackboard.Has("x"), typeof(T).Name);
            Assert.AreEqual(value, blackboard.Get("x", fallback), typeof(T).Name);

            Assert.IsTrue(blackboard.Remove("x"), typeof(T).Name);
            Assert.IsFalse(blackboard.Has("x"), typeof(T).Name);
            Assert.AreEqual(fallback, blackboard.Get("x", fallback), typeof(T).Name);
            Assert.IsFalse(blackboard.Remove("x"), $"{typeof(T).Name}: nothing left to remove");
        }

        [Test]
        public void SetHasRemove_EveryType()
        {
            AssertSetHasRemove(true, false);
            AssertSetHasRemove(3, -1);
            AssertSetHasRemove(2.5f, -1f);
            AssertSetHasRemove(new Vector2(1f, 2f), Vector2.zero);
            AssertSetHasRemove(new Vector3(1f, 2f, 3f), Vector3.zero);
            AssertSetHasRemove("order", "none");
        }

        [Test]
        public void Remove_RaisesChanged_AndTheKeyStaysValid()
        {
            var blackboard = new Blackboard();
            BlackboardKey<float> key = blackboard.Declare("Alert", 1f);
            int changes = 0;
            blackboard.Changed += _ => changes++;

            Assert.IsTrue(blackboard.Remove(key));
            Assert.AreEqual(1, changes);
            Assert.IsFalse(blackboard.TryGet(key, out _));

            blackboard.Set(key, 0.5f);

            Assert.AreEqual(2, changes, "Writing a removed variable brings it back — a change");
            Assert.IsTrue(blackboard.TryGet(key, out float value));
            Assert.AreEqual(0.5f, value);
        }

        [Test]
        public void Tags_SetHasRemove_ChangedOnlyOnChange()
        {
            var blackboard = new Blackboard();
            int changes = 0;
            blackboard.Changed += _ => changes++;

            Assert.IsFalse(blackboard.HasTag("Stunned"));
            blackboard.SetTag("Stunned");
            blackboard.SetTag("Stunned");

            Assert.IsTrue(blackboard.HasTag("Stunned"));
            Assert.AreEqual(1, changes);

            Assert.IsTrue(blackboard.RemoveTag("Stunned"));
            Assert.IsFalse(blackboard.RemoveTag("Stunned"));
            Assert.IsFalse(blackboard.HasTag("Stunned"));
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void Tag_ByKey_MatchesByName()
        {
            var blackboard = new Blackboard();
            TagKey key = blackboard.DeclareTag("InCover");

            blackboard.SetTag(key);

            Assert.IsTrue(blackboard.HasTag("InCover"));
            Assert.IsTrue(blackboard.HasTag(key));
        }

        [Test]
        public void Ref_ChangesAStructInPlace_NotifyChangedIsExplicit()
        {
            var blackboard = new Blackboard();
            BlackboardKey<Memory> key = blackboard.Declare("Memory", new Memory());
            int changes = 0;
            blackboard.Changed += _ => changes++;

            ref Memory memory = ref blackboard.Ref(key);
            memory.Hits++;
            memory.LastSeen = Vector3.one;

            Assert.AreEqual(1, blackboard.Get(key).Hits);
            Assert.AreEqual(Vector3.one, blackboard.Get(key).LastSeen);
            Assert.AreEqual(0, changes, "A write through the reference is silent");

            blackboard.NotifyChanged(key.Index);

            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Variable_ReadsWhatAppearsLater_WithoutDeclaringIt()
        {
            var blackboard = new Blackboard();
            var variable = new BlackboardVariable<float>();

            Assert.AreEqual(-1f, variable.Get(blackboard, "Range", -1f));
            Assert.AreEqual(0, blackboard.Count, "Reading through a variable declares nothing");

            blackboard.Set("Range", 2f);

            Assert.AreEqual(2f, variable.Get(blackboard, "Range", -1f));
        }

        [Test]
        public void Variable_AfterClear_DoesNotReadAStaleSlot()
        {
            var blackboard = new Blackboard();
            var variable = new BlackboardVariable<float>();
            blackboard.Set("A", 1f);
            blackboard.Set("Range", 2f);
            Assert.AreEqual(2f, variable.Get(blackboard, "Range", -1f));

            blackboard.Clear();
            blackboard.Set("Range", 5f);                // now at the index "A" had

            Assert.AreEqual(5f, variable.Get(blackboard, "Range", -1f));
        }

        [Test]
        public void Variable_FollowsTheBlackboardItIsAskedFor()
        {
            var first = new Blackboard();
            var second = new Blackboard();
            var variable = new BlackboardVariable<float>();
            first.Set("Range", 1f);
            second.Set("Range", 2f);

            Assert.AreEqual(1f, variable.Get(first, "Range", 0f));
            Assert.AreEqual(2f, variable.Get(second, "Range", 0f));
        }

        [Test]
        public void Variable_Set_WritesThroughTheKey()
        {
            var blackboard = new Blackboard();
            var variable = new BlackboardVariable<int>();

            variable.Set(blackboard, "Ammo", 3);
            variable.Set(blackboard, "Ammo", 4);

            Assert.AreEqual(4, blackboard.Get("Ammo", 0));
            Assert.AreEqual(1, blackboard.Count);
        }
    }
}
