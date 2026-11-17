using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit
{
    /// <summary>
    /// Debug commands for AI (handbook §33.3), for a console, a debug panel or the editor's cheat window:
    /// "ai.trace on|off" — every agent records its decisions; "ai.freeze on|off" — every agent stops (or resumes)
    /// thinking, bodies stay; "ai.spawn name [x y z]" — a prefab the game registered under that name, at a point.
    /// Execute returns what happened as text; an unknown command says so instead of throwing.
    /// </summary>
    public static class AICheats
    {
        private static readonly Dictionary<string, GameObject> Spawnables = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>What "ai.spawn" can make (the game registers its enemy prefabs).</summary>
        public static void Register(string name, GameObject prefab)
        {
            if (!string.IsNullOrEmpty(name) && prefab != null)
                Spawnables[name] = prefab;
        }

        public static void Unregister(string name) => Spawnables.Remove(name ?? string.Empty);

        public static IEnumerable<string> SpawnableNames => Spawnables.Keys;

        public static string Execute(string line)
        {
            string[] words = (line ?? string.Empty).Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return "ai: nothing to do (ai.trace, ai.freeze, ai.spawn)";

            switch (words[0].ToLowerInvariant())
            {
                case "ai.trace":
                {
                    bool on = Switch(words);
                    int count = ForEachAgent(agent => agent.IsTracing = on);
                    return $"ai.trace {(on ? "on" : "off")}: {count} agents";
                }

                case "ai.freeze":
                {
                    bool on = Switch(words);
                    int count = ForEachAgent(agent => agent.IsThinking = !on);
                    return $"ai.freeze {(on ? "on" : "off")}: {count} agents";
                }

                case "ai.spawn":
                    return Spawn(words);

                default:
                    return $"ai: unknown command '{words[0]}'";
            }
        }

        private static bool Switch(string[] words) => words.Length < 2 || words[1] != "off";

        private static int ForEachAgent(Action<AICharacter> action)
        {
            AICharacter[] agents = Object.FindObjectsByType<AICharacter>(FindObjectsSortMode.None); // PORTING: 6000.3 has no parameterless overload
            foreach (AICharacter agent in agents)
                action(agent);

            return agents.Length;
        }

        private static string Spawn(string[] words)
        {
            if (words.Length < 2 || !Spawnables.TryGetValue(words[1], out GameObject prefab))
                return $"ai.spawn: unknown name (known: {string.Join(", ", Spawnables.Keys)})";

            Vector3 position = Vector3.zero;
            for (int i = 0; i < 3 && i + 2 < words.Length; i++)
            {
                if (float.TryParse(words[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                    position[i] = value;
            }

            GameObject spawned = Object.Instantiate(prefab, position, Quaternion.identity);
            return $"ai.spawn: {spawned.name} at {position}";
        }
    }
}
