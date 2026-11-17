using System;
using System.Collections.Generic;
using UnityEditor;

namespace AIKit.Editor
{
    /// <summary>
    /// An upgrade step that needs the editor (an ability named by a string found as an asset): runs when the graph is
    /// opened on the canvas, after the runtime steps (GraphUpgrades) — the document saves what it changed. Must change
    /// nothing on a graph that does not need it.
    /// </summary>
    public interface IEditorGraphUpgrade
    {
        /// <summary>The graph as opened (the records of the asset still hold the old data). True — something changed.</summary>
        bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues);
    }

    /// <summary>
    /// The editor's upgrade steps: every non-abstract IEditorGraphUpgrade with a parameterless constructor — not the
    /// tests' (an assembly that references NUnit): a step of the tests never runs on a real graph.
    /// </summary>
    public static class EditorGraphUpgrades
    {
        private static List<IEditorGraphUpgrade> _steps;

        public static IReadOnlyList<IEditorGraphUpgrade> Steps => _steps ??= Collect();

        public static bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues) =>
            Apply(asset, graph, issues, Steps);

        /// <summary>
        /// The given steps on the graph, in order: a step that throws is a warning of the graph (it names the step), and
        /// the others still run. True — something changed.
        /// </summary>
        public static bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, List<GraphIssue> issues,
            IEnumerable<IEditorGraphUpgrade> steps)
        {
            if (asset == null || graph == null || steps == null)
                return false;

            bool changed = false;
            foreach (IEditorGraphUpgrade step in steps)
            {
                try
                {
                    changed |= step.Apply(asset, graph, issues);
                }
                catch (Exception exception)
                {
                    issues?.Add(new GraphIssue(GraphIssueCode.OldModel, false, asset.RootId, step.GetType().Name,
                        $"the upgrade step {step.GetType().Name} failed: {exception.Message}"));
                }
            }

            return changed;
        }

        private static List<IEditorGraphUpgrade> Collect()
        {
            var steps = new List<IEditorGraphUpgrade>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IEditorGraphUpgrade>())
            {
                if (!type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null &&
                    !IsTestAssembly(type.Assembly))
                    steps.Add((IEditorGraphUpgrade)Activator.CreateInstance(type));
            }

            steps.Sort((left, right) => string.CompareOrdinal(left.GetType().FullName, right.GetType().FullName));
            return steps;
        }

        // An assembly of tests (one that references NUnit): its steps are the tests' own
        private static bool IsTestAssembly(System.Reflection.Assembly assembly)
        {
            foreach (System.Reflection.AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                if (referenced.Name == "nunit.framework")
                    return true;
            }

            return false;
        }
    }
}
