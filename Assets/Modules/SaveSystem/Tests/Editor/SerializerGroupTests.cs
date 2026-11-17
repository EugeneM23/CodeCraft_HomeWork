using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Saving.Tests
{
    /// <summary>The shared loop: capture with versions, check and migrate on a copy, restore by order.</summary>
    public class SerializerGroupTests
    {
        [Test]
        public void SerializerGroup_Capture_WritesSectionWithVersion()
        {
            var sections = new Dictionary<string, SaveSection>();

            SerializerGroup.CaptureInto(new IGameSerializer[] { new FlakySerializer("value") { Value = 5 } }, sections);

            Assert.AreEqual(1, sections["value"].V);
            Assert.AreEqual(5, (int)sections["value"].Data);
        }

        [Test]
        public void SerializerGroup_CaptureThrows_KeepsPreviousSection()
        {
            var previous = new SaveSection { V = 1, Data = new JValue(1) };
            var sections = new Dictionary<string, SaveSection> { ["value"] = previous };
            var flaky = new FlakySerializer("value") { Value = 2, FailCapture = true };

            LogAssert.Expect(LogType.Error, new Regex("Saving 'value' failed"));
            SerializerGroup.CaptureInto(new IGameSerializer[] { flaky }, sections);

            Assert.AreSame(previous, sections["value"]);
        }

        [Test]
        public void SerializerGroup_PrepareSameVersion_ReturnsCopy()
        {
            var section = new SaveSection { V = 1, Data = new JObject { ["a"] = 1 } };

            SectionStatus status = SerializerGroup.Prepare(new FlakySerializer("value"), section, out JToken data, out _);

            Assert.AreEqual(SectionStatus.Ok, status);
            Assert.IsTrue(JToken.DeepEquals(section.Data, data));
            Assert.AreNotSame(section.Data, data);
        }

        [Test]
        public void SerializerGroup_PrepareNewerSection_Refused()
        {
            var section = new SaveSection { V = 2, Data = new JValue(1) };

            SectionStatus status = SerializerGroup.Prepare(new FlakySerializer("value"), section, out _, out string problem);

            Assert.AreEqual(SectionStatus.NewerVersion, status);
            StringAssert.Contains("newer", problem);
        }

        [Test]
        public void SerializerGroup_PrepareOlderSection_UpgradesStepByStepOnCopy()
        {
            var steps = new StepSerializer("steps");
            var original = new JObject();

            SectionStatus status = SerializerGroup.Prepare(steps, new SaveSection { V = 1, Data = original },
                out JToken data, out _);

            Assert.AreEqual(SectionStatus.Ok, status);
            CollectionAssert.AreEqual(new[] { 1, 2 }, steps.Steps);
            Assert.IsTrue((bool)data["step1"] && (bool)data["step2"]);
            Assert.AreEqual(0, original.Count, "the section in the snapshot must stay untouched");
        }

        [Test]
        public void SerializerGroup_UpgradeThrows_MigrationFailed()
        {
            var steps = new StepSerializer("steps") { FailUpgrade = true };

            SectionStatus status = SerializerGroup.Prepare(steps, new SaveSection { V = 1, Data = new JObject() },
                out _, out string problem);

            Assert.AreEqual(SectionStatus.MigrationFailed, status);
            StringAssert.Contains("Migrating 'steps' from version 1 failed", problem);
        }

        [Test]
        public void SerializerGroup_VersionBelowOne_MigrationFailed()
        {
            SectionStatus status = SerializerGroup.Prepare(new FlakySerializer("value"),
                new SaveSection { V = 0, Data = new JValue(1) }, out _, out _);

            Assert.AreEqual(SectionStatus.MigrationFailed, status);
        }

        [Test]
        public void SerializerGroup_Restore_ByOrderAndSkipsThrowing()
        {
            var restored = new List<string>();
            var late = new FlakySerializer("late", order: 5) { OnRestore = () => restored.Add("late") };
            var broken = new FlakySerializer("broken") { FailRestore = true };
            var early = new FlakySerializer("early", order: -1) { OnRestore = () => restored.Add("early") };

            LogAssert.Expect(LogType.Error, new Regex("Loading 'broken' failed"));
            SerializerGroup.RestoreInOrder(new (IGameSerializer, JToken)[]
            {
                (late, new JValue(1)), (broken, new JValue(2)), (early, new JValue(3))
            });

            CollectionAssert.AreEqual(new[] { "early", "late" }, restored);
            Assert.AreEqual(3, early.Value);
        }
    }
}
