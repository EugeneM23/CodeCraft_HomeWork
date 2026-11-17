using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving
{
    /// <summary>
    /// The loop shared by global sections and (later) component sections: capture with versions, check and migrate
    /// on a copy, restore in ascending Order. One failing serializer is logged and does not stop the others.
    /// </summary>
    public static class SerializerGroup
    {
        public static void CaptureInto(IEnumerable<IGameSerializer> serializers, IDictionary<string, SaveSection> sections)
        {
            foreach (IGameSerializer serializer in serializers)
            {
                try
                {
                    sections[serializer.Key] = new SaveSection { V = serializer.Version, Data = serializer.Capture() };
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Saving '{serializer.Key}' failed, its previous data is kept: {exception.Message}");
                }
            }
        }

        /// <summary>Checks the section's version and migrates a copy step by step; applies nothing.</summary>
        public static SectionStatus Prepare(IGameSerializer serializer, SaveSection section, out JToken data,
            out string problem)
        {
            data = null;
            problem = null;

            if (section.V > serializer.Version)
            {
                problem = $"'{serializer.Key}' was saved with version {section.V}, newer than {serializer.Version}";
                return SectionStatus.NewerVersion;
            }

            if (section.V < 1)
            {
                problem = $"'{serializer.Key}' has an invalid version {section.V}";
                return SectionStatus.MigrationFailed;
            }

            JToken current = section.Data?.DeepClone();
            for (int version = section.V; version < serializer.Version; version++)
            {
                try
                {
                    current = serializer.Upgrade(current, version);
                }
                catch (Exception exception)
                {
                    problem = $"Migrating '{serializer.Key}' from version {version} failed: {exception.Message}";
                    return SectionStatus.MigrationFailed;
                }
            }

            data = current;
            return SectionStatus.Ok;
        }

        public static void RestoreInOrder(IEnumerable<(IGameSerializer Serializer, JToken Data)> prepared)
        {
            // OrderBy is stable: equal orders keep registration order
            foreach ((IGameSerializer serializer, JToken data) in prepared.OrderBy(item => item.Serializer.Order).ToList())
            {
                try
                {
                    serializer.Restore(data);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Loading '{serializer.Key}' failed, it is skipped: {exception.Message}");
                }
            }
        }
    }
}
