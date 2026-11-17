using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// Step 90 (C5, C6): a Use Ability decision or block of before named its ability by a string ("Ability":"Grenade" /
    /// "AbilityId":"Grenade" in its record); now it holds the ability asset. When such a graph is opened (or Tools → AI
    /// Kit → Upgrade All Graphs), the name is looked up among the project's ability assets and the asset is set — the
    /// graph is not rebuilt, only that link. An ability that asked for its user's attack place (the old Needs My Place)
    /// gets its decision an Attack Slot condition ("mine") wired in, the place now a visible part of the graph (C7). A
    /// name no asset has is left as a problem of the node.
    /// </summary>
    public sealed class AbilityNameUpgrade : IEditorGraphUpgrade
    {
        [Serializable]
        private sealed class OldDecision
        {
            public string Ability;
        }

        [Serializable]
        private sealed class OldBlock
        {
            public string AbilityId;
        }

        public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues) =>
            Apply(asset, graph, issues, ProjectAbilities);

        /// <summary>The same with the abilities found by name another way (tests).</summary>
        public static bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues,
            Func<string, AbilityAsset> find)
        {
            if (asset == null || graph == null)
                return false;

            bool changed = false;
            graph.VisitNodes(node =>
            {
                string name = node switch
                {
                    UseAbilityReasoner { Ability: null } => Old<OldDecision>(asset, node.Id)?.Ability,
                    UseAbilityTask { Ability: null } => Old<OldBlock>(asset, node.Id)?.AbilityId,
                    _ => null
                };
                if (string.IsNullOrEmpty(name))
                    return;

                AbilityAsset ability = find(name);
                if (ability == null)
                {
                    issues?.Add(IssueOf(node, name));
                    return;
                }

                if (node is UseAbilityReasoner decision)
                {
                    decision.Ability = ability;
                    if (ability.NeededMyPlace && decision.Condition == null)
                        decision.Condition = AttackSlotLink.Mine();
                }
                else
                {
                    ((UseAbilityTask)node).Ability = ability;
                }

                changed = true;
            });

            return changed;
        }

        // ---------- a name no asset has: kept in the graph, said on its node ----------

        /// <summary>A node that names an ability by the string of before, which no asset of the project had yet.</summary>
        public readonly struct Named
        {
            public Named(AgentNode node, string key, string name)
            {
                Node = node;
                Key = key;
                Name = name;
            }

            public AgentNode Node { get; }

            /// <summary>The key of the node's record the name is kept under ("Ability", "AbilityId").</summary>
            public string Key { get; }

            public string Name { get; }
        }

        /// <summary>
        /// The nodes of the graph that still name their ability by the old string (their record keeps it, and no asset is
        /// linked yet): read before a write makes their records anew (Keep puts the names back after it).
        /// </summary>
        public static List<Named> Unresolved(BehaviorGraphAsset asset, BehaviorGraph graph)
        {
            var named = new List<Named>();
            if (asset == null || graph == null)
                return named;

            graph.VisitNodes(node =>
            {
                switch (node)
                {
                    case UseAbilityReasoner { Ability: null }:
                        Add(named, node, nameof(OldDecision.Ability), Old<OldDecision>(asset, node.Id)?.Ability);
                        break;
                    case UseAbilityTask { Ability: null }:
                        Add(named, node, nameof(OldBlock.AbilityId), Old<OldBlock>(asset, node.Id)?.AbilityId);
                        break;
                }
            });
            return named;
        }

        /// <summary>
        /// The old names go back into the records a write made anew (the node has no field for them): an ability no asset
        /// has is never lost by a save — once one is picked on the node, the next write drops the name for good.
        /// </summary>
        public static void Keep(BehaviorGraphAsset asset, IReadOnlyList<Named> named)
        {
            if (asset == null || named == null)
                return;

            foreach (Named one in named)
            {
                GraphRecord record = asset.Find(one.Node.Id);
                if (record == null || one.Node is UseAbilityReasoner { Ability: not null } ||
                    one.Node is UseAbilityTask { Ability: not null })
                    continue;

                string data = string.IsNullOrEmpty(record.Data) ? "{}" : record.Data.TrimEnd();
                if (data.Contains($"\"{one.Key}\":"))
                    continue;

                int end = data.LastIndexOf('}');
                if (end < 0)
                    continue;

                string pair = $"\"{one.Key}\":\"{Escaped(one.Name)}\"";
                bool empty = data.Substring(0, end).Trim() == "{";
                record.Data = data.Substring(0, end) + (empty ? "" : ",") + pair + data.Substring(end);
            }
        }

        /// <summary>
        /// Each such node's problem, in place of a vaguer one of the node: the ability it named, which no asset has — pick
        /// it on the node.
        /// </summary>
        public static void Report(IReadOnlyList<Named> named, List<GraphIssue> issues)
        {
            if (named == null || issues == null)
                return;

            foreach (Named one in named)
            {
                issues.RemoveAll(issue => issue.Code == GraphIssueCode.NoAbility && issue.RecordId == one.Node.Id);
                issues.Add(IssueOf(one.Node, one.Name));
            }
        }

        private static GraphIssue IssueOf(AgentNode node, string name) =>
            new(GraphIssueCode.NoAbility, false, node.Id, GraphSerializer.TypeIdOf(node),
                $"'{node.DisplayName}' named the ability '{name}', and no ability asset of the project has that " +
                "name: pick it on the node");

        private static void Add(List<Named> named, AgentNode node, string key, string name)
        {
            if (!string.IsNullOrEmpty(name))
                named.Add(new Named(node, key, name));
        }

        private static string Escaped(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // The old data of the node's record (the key a string had before step 90)
        private static T Old<T>(BehaviorGraphAsset asset, int id) where T : class
        {
            GraphRecord record = asset.Find(id);
            if (record == null || string.IsNullOrEmpty(record.Data))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(record.Data);
            }
            catch (ArgumentException)
            {
                return null;                            // data this step cannot read: not its concern
            }
        }

        private static Dictionary<string, AbilityAsset> _byName;

        // The project's ability assets by name, kept between lookups; a name not found, or an asset renamed since, reads
        // the project anew (a name looked up is rare: an older graph opened)
        private static AbilityAsset ProjectAbilities(string name)
        {
            if (_byName != null && _byName.TryGetValue(name, out AbilityAsset kept) && kept != null && kept.Name == name)
                return kept;

            _byName = new Dictionary<string, AbilityAsset>();
            foreach (string guid in AssetDatabase.FindAssets("t:AbilityAsset"))
            {
                var ability = AssetDatabase.LoadAssetAtPath<AbilityAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (ability != null && !string.IsNullOrEmpty(ability.Name) && !_byName.ContainsKey(ability.Name))
                    _byName[ability.Name] = ability;
            }

            return _byName.TryGetValue(name, out AbilityAsset found) ? found : null;
        }
    }
}
