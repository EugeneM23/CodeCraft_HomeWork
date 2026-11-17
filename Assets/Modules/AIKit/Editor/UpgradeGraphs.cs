using System.Text;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// Opens every behavior graph of the project the way the canvas does (step 90): the upgrade steps bring a graph of an
    /// older format to the current picture and the document saves it — the graph is never rebuilt, so what the user drew
    /// stays (positions, bend points, links, labels). A graph the code cannot read is left as it is and listed. A graph
    /// changed only by a step of the editor (an ability named by a string, now its asset) counts as upgraded too, and
    /// what a step could not do (a name no asset has) is listed under the graph.
    /// </summary>
    public static class UpgradeGraphs
    {
        /// <summary>What a run did: how many graphs changed, and the report it logs.</summary>
        public readonly struct Result
        {
            public Result(int upgraded, string report)
            {
                Upgraded = upgraded;
                Report = report;
            }

            public int Upgraded { get; }

            public string Report { get; }
        }

        [MenuItem("Tools/AI Kit/Upgrade All Graphs")]
        public static void UpgradeAll() => Debug.Log(UpgradeIn(null).Report);

        /// <summary>The graphs of these folders (null — of the whole project), each opened as the canvas opens it.</summary>
        public static Result UpgradeIn(string[] folders)
        {
            var report = new StringBuilder("AI Kit — Upgrade All Graphs:\n");
            int upgraded = 0;
            string[] guids = folders != null
                ? AssetDatabase.FindAssets("t:BehaviorGraphAsset", folders)
                : AssetDatabase.FindAssets("t:BehaviorGraphAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<BehaviorGraphAsset>(path);
                if (asset == null)
                    continue;

                int before = asset.FormatVersion;
                string was = EditorJsonUtility.ToJson(asset);
                var document = new GraphDocument(asset);
                bool changed = EditorJsonUtility.ToJson(asset) != was;
                if (changed)
                {
                    upgraded++;
                    AssetDatabase.SaveAssetIfDirty(asset);      // this graph only: nothing else unsaved of the project
                }

                report.Append(path).Append(": format ").Append(before).Append(" → ").Append(asset.FormatVersion);
                if (changed && asset.FormatVersion == before)
                    report.Append(" (upgraded by the editor's steps)");
                if (document.IsReadOnly)
                    report.Append(" — read-only, not upgraded (").Append(document.Issues.Count).Append(" problems)");
                report.AppendLine();

                // What the steps said about this graph (each also logged with the asset as its context), and what is still
                // to fix by hand (an ability name no asset has, an old model)
                foreach (GraphIssue issue in document.Issues)
                {
                    if (issue.Code is GraphIssueCode.NoAbility or GraphIssueCode.OldModel || GraphDocument.IsUpgradeNote(issue))
                        report.Append("    ").AppendLine(issue.Message);
                }
            }

            report.Append("upgraded: ").Append(upgraded);
            return new Result(upgraded, report.ToString());
        }
    }
}
