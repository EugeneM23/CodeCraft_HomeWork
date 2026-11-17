using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;

namespace AIKit.Editor
{
    /// <summary>
    /// The nodes of a graph asset Play pauses at when a character comes into them. Kept for the editor session only
    /// (they live through entering Play), per asset by its GUID — not in the asset, so they are never committed. A graph
    /// that is no file yet (made in memory) keeps its own in memory: two such graphs never share their breakpoints.
    /// </summary>
    public static class GraphBreakpoints
    {
        private const string KEY = "AIKit.Breakpoints.";

        // The breakpoints of the graphs that have no file (no GUID to keep them by), each graph its own
        private static readonly ConditionalWeakTable<BehaviorGraphAsset, HashSet<int>> Unsaved = new();

        public static bool Has(BehaviorGraphAsset asset, int id) => asset != null && Of(asset).Contains(id);

        public static void Set(BehaviorGraphAsset asset, int id, bool on)
        {
            if (asset == null)
                return;

            HashSet<int> ids = Of(asset);
            if (on)
                ids.Add(id);
            else
                ids.Remove(id);

            if (!IsUnsaved(asset))
                SessionState.SetString(Key(asset), string.Join(",", ids));
        }

        private static HashSet<int> Of(BehaviorGraphAsset asset)
        {
            if (IsUnsaved(asset))
                return Unsaved.GetValue(asset, _ => new HashSet<int>());

            string saved = SessionState.GetString(Key(asset), "");
            return new HashSet<int>(saved.Split(',').Where(part => int.TryParse(part, out _)).Select(int.Parse));
        }

        private static bool IsUnsaved(BehaviorGraphAsset asset) => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset));

        private static string Key(BehaviorGraphAsset asset) =>
            KEY + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
    }
}
