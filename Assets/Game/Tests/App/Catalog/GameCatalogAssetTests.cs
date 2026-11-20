using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests
{
    /// <summary>
    /// The built game catalog (Tools → Game → Rebuild Game Catalog) holds together: unique ids, every default module
    /// exists and fits its slot, chapters chain and the milestone-1 content is there. Ignored until the asset is built.
    /// </summary>
    public class GameCatalogAssetTests
    {
        private const string CATALOG_PATH = "Assets/Game/Configs/Run/GameCatalogConfig.asset";

        private GameCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CATALOG_PATH);
            if (_catalog == null)
                Assert.Ignore($"{CATALOG_PATH} is not built yet");
        }

        [Test]
        public void HasNoEmptyEntries()
        {
            CollectionAssert.AllItemsAreNotNull(_catalog.Tanks);
            CollectionAssert.AllItemsAreNotNull(_catalog.Modules);
            CollectionAssert.AllItemsAreNotNull(_catalog.Skills);
            CollectionAssert.AllItemsAreNotNull(_catalog.Chapters);
        }

        [Test]
        public void Ids_AreSet_AndUnique()
        {
            AssertUnique("tank", _catalog.Tanks, t => t.Id.Value);
            AssertUnique("module", _catalog.Modules, m => m.Id.Value);
            AssertUnique("skill", _catalog.Skills, s => s.Id.Value);
            AssertUnique("chapter", _catalog.Chapters, c => c.Id.Value);

            var arenas = new List<ArenaConfig>();
            foreach (ChapterConfig chapter in _catalog.Chapters)
                arenas.AddRange(chapter.Arenas);
            CollectionAssert.AllItemsAreNotNull(arenas);
            AssertUnique("arena", arenas, a => a.Id.Value);
        }

        [Test]
        public void EveryDefaultModule_Exists_FitsItsSlot_AndIsStandard()
        {
            foreach (TankConfig tank in _catalog.Tanks)
            {
                for (int slot = 0; slot < TankConfig.SLOT_COUNT; slot++)
                {
                    ModuleId id = tank.DefaultModule((ModuleSlot)slot);
                    ModuleConfig module = _catalog.FindModule(id);

                    Assert.IsNotNull(module, $"{tank.Id}: no module '{id}' for the slot {(ModuleSlot)slot}");
                    Assert.AreEqual((ModuleSlot)slot, module.Slot, $"{tank.Id}: '{id}' does not fit {(ModuleSlot)slot}");
                    Assert.IsTrue(module.IsStandard, $"{tank.Id}: '{id}' is not owned from the start");
                }
            }
        }

        [Test]
        public void EverySlot_HasOneStandardModule()
        {
            var standard = new Dictionary<ModuleSlot, int>();
            foreach (ModuleConfig module in _catalog.Modules)
            {
                if (module.IsStandard)
                    standard[module.Slot] = standard.TryGetValue(module.Slot, out int count) ? count + 1 : 1;
            }

            for (int slot = 0; slot < TankConfig.SLOT_COUNT; slot++)
            {
                standard.TryGetValue((ModuleSlot)slot, out int count);
                Assert.AreEqual(1, count, $"standard modules of {(ModuleSlot)slot}");
            }
        }

        [Test]
        public void HasTheGddContent()
        {
            Assert.AreEqual(3, _catalog.Tanks.Count, "tanks");
            Assert.AreEqual(15, _catalog.Modules.Count, "modules");
            Assert.AreEqual(21, _catalog.Skills.Count, "skills: GDD §3's 20 and «Мощный фугас»");

            TankConfig tiger = _catalog.FindTank(TankIds.Tiger);
            Assert.IsNotNull(tiger);
            Assert.IsTrue(tiger.IsStarting);
            Assert.AreEqual(1000f, tiger.Health);

            ModuleConfig ap = _catalog.FindModule(ModuleIds.GunArmorPiercing);
            Assert.IsNotNull(ap);
            Assert.AreEqual(1200, ap.PriceMarks);
            Assert.AreEqual(600, ap.UpgradeCost(2).marks);
        }

        [Test]
        public void Milestone1_SellsTwoModules_AndOffersSevenSkills()
        {
            var inShop = new List<string>();
            foreach (ModuleConfig module in _catalog.Modules)
            {
                if (module.InShop)
                    inShop.Add(module.Id.Value);
            }

            var inPool = new List<string>();
            foreach (SkillConfig skill in _catalog.Skills)
            {
                if (skill.InPool)
                    inPool.Add(skill.Id.Value);
            }

            CollectionAssert.AreEquivalent(new[] { ModuleIds.GunArmorPiercing.Value, ModuleIds.ArmorSkirts.Value }, inShop);
            CollectionAssert.AreEquivalent(new[]
            {
                SkillIds.Turret.Value, SkillIds.AirStrike.Value, SkillIds.Ricochet.Value, SkillIds.RateOfFire.Value,
                SkillIds.Armor.Value, SkillIds.Magnet.Value, SkillIds.HighExplosive.Value
            }, inPool);
        }

        [Test]
        public void TheFirstChapter_IsPlayable_AndTheOthersChainOnIt()
        {
            IReadOnlyList<ChapterConfig> chapters = _catalog.Chapters;
            Assert.IsNotEmpty(chapters);

            ChapterConfig first = chapters[0];
            Assert.AreEqual(ChapterIds.Outskirts, first.Id);
            Assert.IsNull(first.RequiredChapter);
            Assert.IsNotNull(first.Level, "the first chapter has a level");
            Assert.IsNotEmpty(first.Arenas);
            Assert.AreEqual(ArenaKind.Elite, first.ArenaAt(first.ArenaCount).Kind, "milestone 1 ends on the elite");

            for (int i = 1; i < chapters.Count; i++)
                Assert.AreSame(chapters[i - 1], chapters[i].RequiredChapter, $"{chapters[i].Id} opens after {chapters[i - 1].Id}");
        }

        private static void AssertUnique<T>(string what, IEnumerable<T> items, System.Func<T, string> id)
            where T : UnityEngine.Object
        {
            var seen = new HashSet<string>();
            foreach (T item in items)
            {
                if (item == null)
                    continue;

                string value = id(item);
                Assert.IsFalse(string.IsNullOrEmpty(value), $"a {what} without an id");
                Assert.IsTrue(seen.Add(value), $"the {what} id '{value}' twice");
            }
        }
    }
}
