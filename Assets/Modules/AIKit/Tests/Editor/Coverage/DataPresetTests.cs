using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The ready presets (whole-plugin recheck, data; handbook §32.3 — a preset works right after it is dropped in):
    /// the ranged kiter keeps its band while its ability cannot be used (too near for it, resting) — it backs off
    /// instead of standing in melee; every setting of the Tuning is read by a preset (invariant 12: a setting nobody
    /// reads is a bug); a preset built with no abilities says so, and its graph shows the empty set.
    /// </summary>
    public sealed class DataPresetTests
    {
        private GraphRig _rig;
        private readonly List<Object> _created = new();

        [SetUp]
        public void SetUp() => _rig = new GraphRig();

        [TearDown]
        public void TearDown()
        {
            _rig.DestroyTarget();
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        [Test]
        public void BehaviorPresets_RangedKiter_TargetInsideItsAbilitysMinRange_BacksOffIntoTheBand_ThenShoots()
        {
            const float MIN_RANGE = 3f;
            var tuning = new BehaviorPresets.Tuning { KeepMin = 4f, KeepMax = 8f };
            TestAbilityRunner shot = Give(tuning, "Shot", duration: 0.2f, cooldown: 0.5f, maxRange: tuning.KeepMax);
            _rig.Abilities.DefinitionOf("Shot").MinRange = MIN_RANGE;
            _rig.SetGraph(BehaviorPresets.RangedKiter(tuning));
            Transform target = _rig.CreateTargetAt(new Vector3(1.5f, 0f, 0f));
            float firstShotAt = float.NaN;

            Assert.IsTrue(_rig.StepUntil(() =>
            {
                if (shot.Starts > 0 && float.IsNaN(firstShotAt))
                    firstShotAt = Vector3.Distance(_rig.Actuator.Position, target.position);
                return shot.Starts >= 1 && Vector3.Distance(_rig.Actuator.Position, target.position) >= tuning.KeepMin;
            }, 5f), "It backs off into its band, then shoots: " + Doing());

            Assert.GreaterOrEqual(firstShotAt, MIN_RANGE, "Never a shot from inside the ability's min range");
        }

        [Test]
        public void BehaviorPresets_RangedKiter_WhileItsOnlyAbilityRests_ATargetWalkingIn_IsKitedAway()
        {
            var tuning = new BehaviorPresets.Tuning { KeepMin = 4f, KeepMax = 8f };
            TestAbilityRunner shot = Give(tuning, "Shot", duration: 0.2f, cooldown: 3f, maxRange: tuning.KeepMax);
            _rig.SetGraph(BehaviorPresets.RangedKiter(tuning));
            Transform target = _rig.CreateTargetAt(new Vector3(6f, 0f, 0f));
            Assert.IsTrue(_rig.StepUntil(() => shot.Starts == 1, 2f), "Set-up: the first shot — " + Doing());
            var blocks = new List<string>();
            _rig.Instance.StateChanged += (_, to) => blocks.Add(to?.DisplayName);

            _rig.MoveTargetTo(_rig.Actuator.Position + new Vector3(2f, 0f, 0f));      // walks in while the shot rests
            _rig.StepSeconds(1.5f);

            Assert.GreaterOrEqual(Vector3.Distance(_rig.Actuator.Position, target.position), tuning.KeepMin,
                "Kited away into its band: " + Doing());
            CollectionAssert.Contains(blocks, "Kite", "It backed off");
            Assert.AreNotEqual("Chase", _rig.Instance.Decision?.DisplayName, "Not standing in melee as a chaser");
            Assert.AreEqual(1, shot.Starts, "Still resting: no second shot");
        }

        [Test]
        public void BehaviorPresets_EveryTuningSetting_IsReadByAPreset()
        {
            var tuning = new BehaviorPresets.Tuning();
            var sentinels = new Dictionary<string, string>();
            foreach (FieldInfo field in typeof(BehaviorPresets.Tuning).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(float))
                {
                    // Sixteenths: written into the node data exactly as they are, and like no default of a node
                    float value = 7.0625f + sentinels.Count * 0.125f;
                    field.SetValue(tuning, value);
                    sentinels[field.Name] = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (field.FieldType == typeof(string))
                {
                    field.SetValue(tuning, "Sentinel");
                    sentinels[field.Name] = "Sentinel";
                }
                else if (field.FieldType == typeof(Vector3[]))
                {
                    field.SetValue(tuning, new[] { new Vector3(11.5625f, 0f, 11.6875f) });
                    sentinels[field.Name] = "11.6875";
                }
            }

            AbilityAsset ability = GraphRig.Named("Sentinel Ability");
            tuning.Abilities.Add(ability);

            var data = new List<string>();
            bool abilityWired = false;
            foreach (BehaviorPresets.Preset preset in Enum.GetValues(typeof(BehaviorPresets.Preset)))
            {
                BehaviorGraph graph = BehaviorPresets.Build(preset, tuning);
                graph.VisitNodes(node =>
                {
                    data.Add(JsonUtility.ToJson(node));
                    abilityWired |= node is AbilityValue { Ability: { } wired } && wired == ability;
                });
            }

            string all = string.Join("\n", data);
            foreach (KeyValuePair<string, string> sentinel in sentinels)
                StringAssert.Contains(sentinel.Value, all, $"Tuning.{sentinel.Key} is read by no preset (invariant 12)");
            Assert.IsTrue(abilityWired, "Tuning.Abilities is the set of the presets' Use Any Ability");
        }

        [Test]
        public void BehaviorPresets_BuiltWithNoAbilities_SaysSo()
        {
            foreach (BehaviorPresets.Preset preset in Enum.GetValues(typeof(BehaviorPresets.Preset)))
            {
                LogAssert.Expect(LogType.Warning, new Regex("no abilities"));
                BehaviorPresets.Build(preset, new BehaviorPresets.Tuning());
            }

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BehaviorPresets_BuiltWithAnAbility_SaysNothing()
        {
            var tuning = new BehaviorPresets.Tuning();
            tuning.Abilities.Add(GraphRig.Named("Swing"));
            foreach (BehaviorPresets.Preset preset in Enum.GetValues(typeof(BehaviorPresets.Preset)))
                BehaviorPresets.Build(preset, tuning);

            LogAssert.NoUnexpectedReceived();
        }

        // ---------- the rig ----------

        private TestAbilityRunner Give(BehaviorPresets.Tuning tuning, string id, float duration, float cooldown,
            float maxRange)
        {
            tuning.Abilities.Add(GraphRig.Named(id));
            return _rig.AddAbility(id, duration, cooldown, maxRange);
        }

        private string Doing() =>
            $"decision '{_rig.Instance?.Decision?.DisplayName ?? "none"}', node '{_rig.StateName ?? "none"}', " +
            $"at {_rig.Actuator.Position}";
    }
}
