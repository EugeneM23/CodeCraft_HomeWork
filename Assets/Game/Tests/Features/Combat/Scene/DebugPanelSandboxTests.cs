using System.Collections;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// The debug panel in VoxelSandbox with the core's run (made here from the game catalog, swapped in as RunBootstrap
    /// does): SetSkillLevel changes the tank's modifiers at once, the panel opens with its tabs, and the god mode,
    /// time scale and zombie entries do what they say. No end-of-frame waits: runs in batchmode.
    /// </summary>
    public sealed class DebugPanelSandboxTests
    {
        private const string SCENE = "Assets/Game/Voxels/Scenes/VoxelSandbox.unity";
        private const string CATALOG = "Assets/Game/Configs/Run/GameCatalogConfig.asset";

        private RunService _run;
        private DebugPanel _panel;
        private readonly List<DebugEntry> _entries = new();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (_panel != null)
                Object.Destroy(_panel.gameObject);
            _run?.Dispose();
            _run = null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator SkillsAndCheats_FromThePanel_ChangeTheSandboxAtOnce()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(SCENE, new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++)
                yield return new WaitForFixedUpdate();

            GameContext context = GameContext.Instance;
            Assert.That(context, Is.Not.Null);
            Assert.IsTrue(DebugLookup.TryGetTank(out IEntity tank), "the player's tank");
            Assert.IsTrue(tank.TryGetCombatModifiers(out CombatModifiers modifiers));
            Assert.IsTrue(tank.TryGetHealth(out Health health));
            int baseMax = health.GetMax();

            // The core's run in place of the stand-in, as RunBootstrap does it
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CATALOG);
            Assert.That(catalog, Is.Not.Null, CATALOG);
            if (context.TryGetRun(out IRunService standIn))
            {
                (standIn as System.IDisposable)?.Dispose();
                context.DelRun();
            }

            _run = new RunService(catalog, context.GetEnemyDeaths());
            context.AddRun(_run);
            Assert.IsTrue(context.TryGetBehaviour(out CombatBehaviour combat));
            combat.RebindRun();
            Assert.IsTrue(_run.StartRun(FirstChapter(catalog), TankIds.Tiger));

            // ---------- Skills ----------
            Assert.IsTrue(_run.SetSkillLevel(SkillIds.Ricochet, 3));
            Assert.IsTrue(_run.SetSkillLevel(SkillIds.AirStrike, SkillConfig.MAX_LEVEL));
            Assert.IsTrue(_run.SetSkillLevel(SkillIds.Armor, SkillConfig.MAX_LEVEL));
            yield return null;
            Assert.AreEqual(3, modifiers.RicochetBounces, "ricochet 3");
            Assert.AreEqual(5, modifiers.AirStrikeLevel, "air strike 5");
            Assert.Greater(health.GetMax(), baseMax, "armour 5 raises the max health");

            Assert.IsTrue(_run.SetSkillLevel(SkillIds.Armor, 0));
            Assert.IsTrue(_run.SetSkillLevel(SkillIds.Ricochet, 0));
            yield return null;
            Assert.AreEqual(0, modifiers.RicochetBounces, "ricochet taken away");
            Assert.That(modifiers.MaxHealthFactor, Is.EqualTo(1f).Within(1e-4f), "armour taken away");

            // ---------- The panel ----------
            DebugCommands.RegisterOnce();
            var prefab = Resources.Load<DebugPanel>(DebugPanel.RESOURCE_PATH);
            Assert.That(prefab, Is.Not.Null, "Resources/" + DebugPanel.RESOURCE_PATH);
            _panel = Object.Instantiate(prefab);
            yield return null;
            _panel.Open();
            yield return null;
            yield return null;
            Assert.IsTrue(DebugPanel.IsOpen);
            CollectionAssert.IsSubsetOf(new[] { SkillDebugCommands.CATEGORY, TankDebugCommands.CATEGORY,
                WorldDebugCommands.CATEGORY, RunDebugCommands.CATEGORY }, DebugPanelRegistry.Categories);
            Assert.Greater(_panel.GetComponentsInChildren<UnityEngine.UI.Button>(false).Length,
                DebugPanelRegistry.Categories.Count, "tabs and the first tab's rows are drawn");

            // God mode: a killing blow through DamageTakenEvent leaves the tank full
            Entry(TankDebugCommands.CATEGORY, DebugEntryKind.Toggle, "Бессмертие").SetToggle(true);
            yield return null;
            IEvent<TakeDamageArgs> damaged = tank.GetDamageTakenEvent();
            health.Reduce(health.GetMax() * 2, () => damaged.Invoke(default));
            Assert.IsFalse(health.IsEmpty(), "god mode: alive after a killing blow");
            Assert.IsTrue(health.IsFull());
            Entry(TankDebugCommands.CATEGORY, DebugEntryKind.Toggle, "Бессмертие").SetToggle(false);

            // Time scale ×0.5
            Entry(WorldDebugCommands.CATEGORY, DebugEntryKind.Buttons, "Время").Pressed(1);
            Assert.AreEqual(0.5f, Time.timeScale);
            Time.timeScale = 1f;

            // Zombies ×5 in front of the tank
            int before = context.GetEnemyFilter().Count;
            DebugEntry enemies = Entry(WorldDebugCommands.ENEMIES, DebugEntryKind.Buttons, null);
            enemies.Pressed(0);
            for (int i = 0; i < 5; i++)
                yield return null;
            Assert.AreEqual(before + 5, context.GetEnemyFilter().Count, "five zombies spawned");

            // Kill all: every enemy's health emptied, the filter drains
            enemies.Pressed(1);
            for (int i = 0; i < 60 && context.GetEnemyFilter().Count > 0; i++)
                yield return null;
            Assert.AreEqual(0, context.GetEnemyFilter().Count, "all enemies dead");

            _panel.Close();
            Assert.IsFalse(DebugPanel.IsOpen);
        }

        private DebugEntry Entry(string category, DebugEntryKind kind, string labelStart)
        {
            DebugPanelRegistry.Collect(category, _entries);
            foreach (DebugEntry entry in _entries)
            {
                if (entry.Kind != kind)
                    continue;
                string label = entry.ReadLabel();
                if (labelStart == null ? label == null : label != null && label.StartsWith(labelStart))
                    return entry;
            }

            Assert.Fail($"no {kind} '{labelStart}' in {category}");
            return null;
        }

        private static ChapterId FirstChapter(GameCatalog catalog)
        {
            foreach (ChapterConfig chapter in catalog.Chapters)
                if (chapter != null)
                    return chapter.Id;
            return default;
        }
    }
}
