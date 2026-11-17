using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// Every decision's chain of every graph of the project collapsed into a subgraph named after the decision (16.11.2026,
    /// the user's word: "collapse everything in the current graphs into subgraphs"). Only a look of the editor: the graphs
    /// run as before. A chain already in a subgraph stays as it is; a graph opened for viewing only is left alone.
    /// </summary>
    public static class CollapseChainsMenu
    {
        [MenuItem("Tools/AI Kit/Collapse Decision Chains Into Subgraphs")]
        public static void CollapseAll()
        {
            int graphs = 0, parts = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BehaviorGraphAsset), new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BehaviorGraphAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                    continue;

                int made = new GraphDocument(asset).CollapseDecisionChains();
                if (made == 0)
                    continue;

                graphs++;
                parts += made;
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"AI Kit: {parts} decision chains collapsed into subgraphs in {graphs} graphs");
        }
    }
}
