using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Tests
{
    public class DebugPanelRegistryTests
    {
        private readonly List<DebugEntry> _entries = new();
        private List<DebugEntry> _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = new List<DebugEntry>(DebugPanelRegistry.Entries);
            DebugPanelRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DebugPanelRegistry.Clear();
            foreach (DebugEntry entry in _saved)
                DebugPanelRegistry.Add(entry);
        }

        [Test]
        public void Entries_AreGroupedByCategory_InOrderThenRegistration()
        {
            DebugPanelRegistry.AddButton("A", "first", () => { });
            DebugPanelRegistry.AddToggle("B", "toggle", () => true, _ => { });
            DebugPanelRegistry.AddInfo("A", () => "top", -1);
            DebugPanelRegistry.AddSlider("A", "slider", 0f, 1f, () => 0.5f, _ => { });

            CollectionAssert.AreEqual(new[] { "A", "B" }, DebugPanelRegistry.Categories);
            DebugPanelRegistry.Collect("A", _entries);
            Assert.AreEqual(3, _entries.Count);
            Assert.AreEqual("top", _entries[0].ReadLabel());
            Assert.AreEqual(DebugEntryKind.Buttons, _entries[1].Kind);
            Assert.AreEqual("slider", _entries[2].ReadLabel());
        }

        [Test]
        public void ButtonRow_PassesTheIndex_AndLiveTextOverridesTheCaptions()
        {
            int pressed = -1;
            DebugEntry entry = DebugPanelRegistry.AddButtons("A", null, new[] { "x", "y" }, i => pressed = i, 0,
                i => i == 0 ? "live" : null);

            entry.Pressed(1);

            Assert.AreEqual(1, pressed);
            Assert.AreEqual("live", entry.ReadButton(0));
            Assert.IsNull(entry.ReadButton(1), "null hides the button");
        }

        [Test]
        public void Remove_DropsAnEmptyCategory_AndRaisesChanged()
        {
            int changed = 0;
            DebugEntry entry = DebugPanelRegistry.AddButton("Gone", "b", () => { });
            DebugPanelRegistry.Changed += Count;
            try
            {
                DebugPanelRegistry.Remove(entry);
            }
            finally
            {
                DebugPanelRegistry.Changed -= Count;
            }

            Assert.AreEqual(1, changed);
            CollectionAssert.DoesNotContain(DebugPanelRegistry.Categories, "Gone");

            void Count() => changed++;
        }

        [Test]
        public void ALabelThatThrows_ReadsAsAnError_AndAVisibleThatThrows_Hides()
        {
            DebugEntry entry = DebugPanelRegistry.AddInfo("A", () => throw new System.InvalidOperationException());
            entry.Visible = () => throw new System.InvalidOperationException();

            StringAssert.Contains("InvalidOperationException", entry.ReadLabel());
            Assert.IsFalse(entry.IsVisible);
        }
    }
}
