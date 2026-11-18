using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Saving;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class PlayerProgressTests
    {
        [Serializable]
        public sealed class GarageExtension
        {
            public string paint;
            public List<int> stickers = new();
        }

        private static PlayerProgress Loaded(InMemorySaveStorage storage)
        {
            var progress = new PlayerProgress(storage, "1.0");
            progress.Load();
            return progress;
        }

        [Test]
        public void SaveAndLoad_KeepCurrenciesModulesSlotsAndLevels()
        {
            var storage = new InMemorySaveStorage();
            PlayerProgress progress = Loaded(storage);
            progress.AddCredits(300);
            progress.AddCurrency("scrap", 7);
            progress.SetModuleLevel("cannon_heavy", 2);
            progress.InstallModule("gun", "cannon_heavy");
            progress.UnlockLevel("level_02");
            progress.MarkLevelCompleted("level_01");
            Assert.AreEqual(SaveOutcome.Saved, progress.Save());

            PlayerProgress read = Loaded(storage);

            Assert.AreEqual(300, read.Credits);
            Assert.AreEqual(300, read.GetCurrency(PlayerProgress.CREDITS));
            Assert.AreEqual(7, read.GetCurrency("scrap"));
            Assert.AreEqual(2, read.GetModuleLevel("cannon_heavy"));
            Assert.IsTrue(read.OwnsModule("cannon_heavy"));
            Assert.AreEqual("cannon_heavy", read.GetInstalledModule("gun"));
            Assert.IsTrue(read.IsLevelUnlocked("level_02"));
            Assert.IsTrue(read.IsLevelCompleted("level_01"));
            Assert.IsFalse(read.IsLevelCompleted("level_02"));
            CollectionAssert.AreEqual(new[] { "level_02" }, read.UnlockedLevels);
        }

        [Test]
        public void UnknownIds_ReadAsEmpty()
        {
            PlayerProgress progress = Loaded(new InMemorySaveStorage());

            Assert.AreEqual(0, progress.GetCurrency("gems"));
            Assert.AreEqual(0, progress.GetModuleLevel("armor"));
            Assert.IsFalse(progress.OwnsModule("armor"));
            Assert.IsNull(progress.GetInstalledModule("hull"));
            Assert.IsNull(progress.Get<GarageExtension>("garage"));
        }

        [Test]
        public void TrySpend_WithoutEnough_ReturnsFalse_AndKeepsTheAmount()
        {
            PlayerProgress progress = Loaded(new InMemorySaveStorage());
            progress.AddCredits(50);
            int changes = 0;
            progress.Changed += () => changes++;

            Assert.IsFalse(progress.TrySpendCredits(80));
            Assert.AreEqual(50, progress.Credits);
            Assert.AreEqual(0, changes);

            Assert.IsTrue(progress.TrySpendCredits(30));
            Assert.AreEqual(20, progress.Credits);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Extension_SurvivesSaveAndLoad_AsACopy()
        {
            var storage = new InMemorySaveStorage();
            PlayerProgress progress = Loaded(storage);
            var garage = new GarageExtension { paint = "olive", stickers = { 3, 5 } };
            progress.Set("garage", garage);
            garage.paint = "changed after Set";
            progress.Save();

            GarageExtension read = Loaded(storage).Get<GarageExtension>("garage");

            Assert.AreEqual("olive", read.paint);
            CollectionAssert.AreEqual(new[] { 3, 5 }, read.stickers);
        }

        [Test]
        public void Changes_MakeItDirty_SaveAndLoadLeaveItClean()
        {
            var storage = new InMemorySaveStorage();
            PlayerProgress progress = Loaded(storage);
            Assert.IsFalse(progress.IsDirty);

            progress.UnlockLevel("level_01");
            Assert.IsTrue(progress.IsDirty);

            progress.Save();
            Assert.IsFalse(progress.IsDirty);
            Assert.IsTrue(storage.Has(0, SaveFiles.SAVE));

            progress.UnlockLevel("level_01");
            Assert.IsFalse(progress.IsDirty, "Unlocking twice changes nothing.");

            Assert.IsFalse(Loaded(storage).IsDirty);
        }

        [Test]
        public void Load_AnOldFileWithoutNewFields_ReadsWithEmptyCollections()
        {
            var storage = new InMemorySaveStorage();
            storage.Put(0, SaveFiles.SAVE, "{\"formatVersion\":1,\"gameVersion\":\"0.1\",\"sections\":{\"progress\":" +
                                           "{\"v\":1,\"data\":{\"currencies\":{\"credits\":5},\"modules\":null}}}}");
            var progress = new PlayerProgress(storage, "1.0");

            Assert.AreEqual(LoadOutcome.Loaded, progress.Load());
            Assert.AreEqual(5, progress.Credits);
            Assert.AreEqual(0, progress.GetModuleLevel("armor"));
            Assert.IsEmpty(progress.UnlockedLevels);
            progress.SetModuleLevel("armor", 1);
            Assert.IsTrue(progress.OwnsModule("armor"));
        }

        [Test]
        public void Load_ASectionOfANewerGame_IsNotApplied_AndNotOverwrittenUntilReset()
        {
            var storage = new InMemorySaveStorage();
            string newer = "{\"formatVersion\":1,\"gameVersion\":\"9.0\",\"sections\":{\"progress\":" +
                           "{\"v\":" + (ProgressSerializer.VERSION + 1) + ",\"data\":{\"currencies\":{\"credits\":5}}}}}";
            storage.Put(0, SaveFiles.SAVE, newer);
            var progress = new PlayerProgress(storage, "1.0");
            LogAssert.Expect(LogType.Error, new Regex("will not be overwritten"));

            Assert.AreEqual(LoadOutcome.NewerVersion, progress.Load());
            Assert.AreEqual(0, progress.Credits);
            Assert.IsTrue(progress.IsReadOnly);

            progress.AddCredits(1);
            Assert.AreEqual(SaveOutcome.Blocked, progress.Save());
            Assert.IsTrue(progress.IsDirty);
            Assert.IsTrue(storage.TryRead(0, SaveFiles.SAVE, out string text));
            Assert.AreEqual(newer, text);

            progress.ResetProgress();
            Assert.AreEqual(SaveOutcome.Saved, progress.Save());
            Assert.AreEqual(0, Loaded(storage).Credits);
        }

        [Test]
        public void ResetProgress_EmptiesEverything_AndRaisesChanged()
        {
            PlayerProgress progress = Loaded(new InMemorySaveStorage());
            progress.AddCredits(10);
            progress.Set("garage", new GarageExtension());
            bool changed = false;
            progress.Changed += () => changed = true;

            progress.ResetProgress();

            Assert.IsTrue(changed);
            Assert.AreEqual(0, progress.Credits);
            Assert.IsFalse(progress.Has("garage"));
        }
    }
}
