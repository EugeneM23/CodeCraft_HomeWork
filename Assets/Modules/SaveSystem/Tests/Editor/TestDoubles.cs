using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>A game system to save: level, name and a position.</summary>
    internal sealed class Progress
    {
        public int Level;
        public string Name;
        public Vector3 Position;
    }

    internal sealed class ProgressData
    {
        public int Level;
        public string Name;
        public Vector3 Position;
    }

    /// <summary>Version 2 of the progress section: version 1 stored the level as "lvl".</summary>
    internal sealed class ProgressSerializer : GameSerializer<Progress, ProgressData>
    {
        public const string KEY = "progress";

        public ProgressSerializer(Progress progress, int order = 0) : base(progress, KEY, version: 2, order: order)
        {
        }

        protected override ProgressData Serialize(Progress progress) =>
            new() { Level = progress.Level, Name = progress.Name, Position = progress.Position };

        protected override void Deserialize(Progress progress, ProgressData data)
        {
            progress.Level = data.Level;
            progress.Name = data.Name;
            progress.Position = data.Position;
        }

        protected override JToken Upgrade(JToken data, int fromVersion)
        {
            if (fromVersion != 1)
                return base.Upgrade(data, fromVersion);

            var json = (JObject)data;
            json["level"] = json["lvl"];
            json.Remove("lvl");
            return json;
        }
    }

    internal sealed class LegacyProgressData
    {
        public int Lvl;
        public string Name;
        public Vector3 Position;
    }

    /// <summary>The same section the way an older build wrote it: version 1 with "lvl".</summary>
    internal sealed class LegacyProgressSerializer : GameSerializer<Progress, LegacyProgressData>
    {
        public LegacyProgressSerializer(Progress progress) : base(progress, ProgressSerializer.KEY, version: 1)
        {
        }

        protected override LegacyProgressData Serialize(Progress progress) =>
            new() { Lvl = progress.Level, Name = progress.Name, Position = progress.Position };

        protected override void Deserialize(Progress progress, LegacyProgressData data)
        {
            progress.Level = data.Lvl;
            progress.Name = data.Name;
            progress.Position = data.Position;
        }
    }

    /// <summary>One int under a key; can be told to throw on save or on load and reports its restores.</summary>
    internal sealed class FlakySerializer : IGameSerializer
    {
        public int Value;
        public bool FailCapture;
        public bool FailRestore;
        public Action OnRestore;

        public FlakySerializer(string key, int order = 0)
        {
            Key = key;
            Order = order;
        }

        public string Key { get; }
        public int Version => 1;
        public int Order { get; }

        public JToken Capture() => FailCapture ? throw new InvalidOperationException("capture broke") : new JValue(Value);

        public JToken Upgrade(JToken data, int fromVersion) => throw new SaveMigrationException(Key, fromVersion);

        public void Restore(JToken data)
        {
            if (FailRestore)
                throw new InvalidOperationException("restore broke");

            Value = (int)data;
            OnRestore?.Invoke();
        }
    }

    /// <summary>Version 3: records every migration step and marks the data with it.</summary>
    internal sealed class StepSerializer : IGameSerializer
    {
        public readonly List<int> Steps = new();
        public JToken Restored;
        public bool FailUpgrade;

        public StepSerializer(string key) => Key = key;

        public string Key { get; }
        public int Version => 3;
        public int Order => 0;

        public JToken Capture() => new JObject();

        public JToken Upgrade(JToken data, int fromVersion)
        {
            if (FailUpgrade)
                throw new InvalidOperationException("upgrade broke");

            Steps.Add(fromVersion);
            var json = (JObject)data;
            json[$"step{fromVersion}"] = true;
            return json;
        }

        public void Restore(JToken data) => Restored = data;
    }

    /// <summary>In-memory storage whose reads or writes can be made to fail like a broken disk.</summary>
    internal sealed class FailingStorage : ISaveStorage
    {
        public readonly InMemorySaveStorage Inner = new();
        public bool FailWrites;
        public bool FailReads;

        public IReadOnlyList<int> ListSlots() => Inner.ListSlots();

        public bool TryRead(int slot, string file, out string text)
        {
            if (FailReads)
                throw new IOException("read failed");

            return Inner.TryRead(slot, file, out text);
        }

        public bool TryReadBackup(int slot, string file, out string text)
        {
            if (FailReads)
                throw new IOException("read failed");

            return Inner.TryReadBackup(slot, file, out text);
        }

        public void Write(int slot, string file, string text)
        {
            if (FailWrites)
                throw new IOException("disk full");

            Inner.Write(slot, file, text);
        }

        public void Quarantine(int slot, string file, bool backup) => Inner.Quarantine(slot, file, backup);
        public void DeleteSlot(int slot) => Inner.DeleteSlot(slot);
    }

    /// <summary>A layer with one int under "$fake"; can refuse a load, hold it open or throw on capture.</summary>
    internal sealed class FakeExtension : ISaveExtension
    {
        public const string KEY = "$fake";

        public SaveLoadService Service;
        public int Value;
        public int Restored = -1;
        public int Captures;
        public int Begins;
        public bool Disposed;
        public bool FailCapture;
        public bool FinishAtOnce = true;
        public LoadOutcome CheckResult = LoadOutcome.Loaded;
        public Action Finish;

        public void CaptureInto(IDictionary<string, SaveSection> sections)
        {
            Captures++;
            if (FailCapture)
                throw new InvalidOperationException("capture broke");

            sections[KEY] = new SaveSection { V = 1, Data = new JValue(Value) };
        }

        public LoadOutcome Check(IReadOnlyDictionary<string, SaveSection> sections, out string problem)
        {
            problem = CheckResult == LoadOutcome.Loaded ? null : "the fake layer refused";
            return CheckResult;
        }

        public void BeginLoad(Action finished)
        {
            Begins++;
            Restored = Service.TryGetSection(KEY, out SaveSection section) ? (int)section.Data : -1;
            if (FinishAtOnce)
                finished();
            else
                Finish = finished;
        }

        public void Dispose() => Disposed = true;
    }
}
