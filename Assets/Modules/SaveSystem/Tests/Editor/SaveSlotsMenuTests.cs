using System;
using System.Linq;
using NUnit.Framework;
using Saving.UI;
using Saving.UI.Editor;
using UnityEditor;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>The slot menu as the builder makes it, on a real service over memory: rows, save, load, delete, rename.</summary>
    public class SaveSlotsMenuTests
    {
        private static readonly DateTime SavedAt = new(2026, 10, 24, 12, 30, 0, DateTimeKind.Utc);

        private TempScene _scene;
        private InMemorySaveStorage _storage;
        private SaveLoadService _service;
        private SaveSlotsMenu _menu;

        [SetUp]
        public void SetUp()
        {
            _scene = new TempScene();
            _storage = new InMemorySaveStorage();
            _service = new SaveLoadService(_storage, "1.0", () => SavedAt) { SceneName = () => "Cave" };
            _menu = SaveSlotsMenuBuilder.Build();
            _menu.Use(_service);
        }

        [TearDown]
        public void TearDown() => _scene.Dispose();

        private void SaveSlot(int slot, int coins, string name = null)
        {
            _service.SelectSlot(slot);
            _service.Values.Set("coins", coins);
            if (name != null)
                _service.SlotName = name;

            _service.AddPlayTime(65);
            _service.Save();
        }

        private void SetMenu(string field, int value)
        {
            var serialized = new SerializedObject(_menu);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Builder_PartsWiredAndClosed()
        {
            Assert.IsFalse(_menu.IsOpen);
            Assert.IsNotNull(_menu.Confirm);
            Assert.IsFalse(_menu.Confirm.IsOpen);
            Assert.IsNotNull(_menu.GetComponent<Canvas>());
        }

        [Test]
        public void Load_RowsShowNamesDetailsAndEmptySlots()
        {
            SaveSlot(0, 5, "Hero");

            _menu.OpenLoad();

            Assert.IsTrue(_menu.IsOpen);
            Assert.AreEqual(5, _menu.Rows.Count(row => row.gameObject.activeSelf));
            SaveSlotView hero = _menu.RowOf(0);
            Assert.AreEqual("Hero", hero.Title);
            StringAssert.Contains("1:05", hero.Details);
            StringAssert.Contains("Cave", hero.Details);
            Assert.AreEqual("Load", hero.PickLabel);
            Assert.IsTrue(hero.PickButton.interactable);
            SaveSlotView empty = _menu.RowOf(1);
            Assert.AreEqual("Slot 2", empty.Title);
            Assert.AreEqual("Empty", empty.Details);
            Assert.IsFalse(empty.PickButton.interactable, "an empty slot cannot be loaded");
            Assert.IsFalse(empty.DeleteButton.gameObject.activeSelf);
        }

        [Test]
        public void Load_PickLoadsTheSlotAndCloses()
        {
            SaveSlot(2, 7);
            _service.SelectSlot(0);
            _menu.OpenLoad();

            _menu.RowOf(2).PickButton.onClick.Invoke();

            Assert.AreEqual(2, _service.CurrentSlot);
            Assert.AreEqual(7, _service.Values.Get("coins", -1));
            Assert.IsFalse(_menu.IsOpen);
        }

        [Test]
        public void Save_EmptySlotSavesAtOnce()
        {
            _service.Values.Set("coins", 3);
            _menu.OpenSave();
            Assert.AreEqual("Save", _menu.RowOf(1).PickLabel);

            _menu.RowOf(1).PickButton.onClick.Invoke();

            Assert.IsTrue(_service.HasSlot(1));
            Assert.AreEqual(1, _service.CurrentSlot);
            Assert.IsFalse(_menu.IsOpen);
        }

        [Test]
        public void Save_TakenSlotAsksFirst()
        {
            SaveSlot(0, 1);
            _service.Values.Set("coins", 9);
            _menu.OpenSave();
            Assert.AreEqual("Overwrite", _menu.RowOf(0).PickLabel);

            _menu.RowOf(0).PickButton.onClick.Invoke();
            Assert.IsTrue(_menu.Confirm.IsOpen);
            StringAssert.Contains("Slot 1", _menu.Confirm.Message);
            _menu.Confirm.Cancel();
            Assert.IsTrue(_menu.IsOpen, "no keeps the menu");

            _menu.RowOf(0).PickButton.onClick.Invoke();
            _menu.Confirm.Confirm();

            var reader = new SaveLoadService(_storage, "1.0");
            reader.Load();
            Assert.AreEqual(9, reader.Values.Get("coins", -1));
        }

        [Test]
        public void Delete_AsksThenDeletes()
        {
            SaveSlot(3, 1);
            _menu.OpenLoad();

            _menu.RowOf(3).DeleteButton.onClick.Invoke();
            Assert.IsTrue(_service.HasSlot(3), "nothing is deleted before yes");
            _menu.Confirm.Confirm();

            Assert.IsFalse(_service.HasSlot(3));
            Assert.AreEqual("Empty", _menu.RowOf(3).Details);
        }

        [Test]
        public void Delete_RowsShiftBeforeYes_DeletesTheSlotAskedAbout()
        {
            SetMenu("_slotCount", 0);
            SaveSlot(0, 1);
            SaveSlot(2, 1);
            SaveSlot(3, 1);
            _menu.OpenLoad();

            _menu.RowOf(3).DeleteButton.onClick.Invoke();
            SaveSlot(1, 1); // an autosave makes a new slot: the menu refreshes and the rows shift
            _menu.Confirm.Confirm();

            Assert.IsFalse(_service.HasSlot(3));
            Assert.IsTrue(_service.HasSlot(1));
            Assert.IsTrue(_service.HasSlot(2));
        }

        [Test]
        public void Rename_EndEditRenamesTheSlot()
        {
            SaveSlot(1, 1);
            _menu.OpenLoad();

            _menu.RowOf(1).NameField.onEndEdit.Invoke("Before the boss");

            Assert.AreEqual("Before the boss", _service.GetSlot(1).Name);
            Assert.AreEqual("Before the boss", _menu.RowOf(1).Title, "the menu refreshes on SlotsChanged");
        }

        [Test]
        public void Count_Zero_SavedSlotsAndANewSlotToSave()
        {
            SetMenu("_slotCount", 0);
            SaveSlot(0, 1);
            SaveSlot(4, 1);

            _menu.OpenSave();

            var rows = _menu.Rows.Where(row => row.gameObject.activeSelf).ToList();
            CollectionAssert.AreEqual(new[] { 0, 4, 1 }, rows.Select(row => row.Slot));
            Assert.AreEqual("New slot", rows[2].Title);

            _menu.OpenLoad();
            Assert.AreEqual(2, _menu.Rows.Count(row => row.gameObject.activeSelf), "no new slot to load");
        }

        [Test]
        public void FirstSlot_ShiftsTheRange()
        {
            SetMenu("_firstSlot", 10);
            SetMenu("_slotCount", 2);

            _menu.OpenSave();

            CollectionAssert.AreEqual(new[] { 10, 11 },
                _menu.Rows.Where(row => row.gameObject.activeSelf).Select(row => row.Slot));
        }

        [Test]
        public void CloseAndToggle_RaiseEvents()
        {
            int opened = 0, closed = 0;
            _menu.OnOpened.AddListener(() => opened++);
            _menu.OnClosed.AddListener(() => closed++);

            _menu.Toggle();
            _menu.Toggle();
            _menu.Close();

            Assert.AreEqual(1, opened);
            Assert.AreEqual(1, closed, "closing a closed menu is quiet");
        }

        [Test]
        public void Format_PlayTimeAndTitle()
        {
            Assert.AreEqual("0:05", SaveSlotFormat.PlayTime(5));
            Assert.AreEqual("1:05:12", SaveSlotFormat.PlayTime(3912));
            Assert.AreEqual("Slot 3", SaveSlotFormat.Title(2, null));
        }
    }
}
