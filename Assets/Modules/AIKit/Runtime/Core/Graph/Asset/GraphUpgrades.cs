using System;
using System.Collections.Generic;
using System.Reflection;

namespace AIKit
{
    /// <summary>
    /// One step that brings a graph written in an older format to the current picture (handbook §29а.3, C.5): read
    /// the old data of the records (JsonUtility drops keys a node no longer has, so the step reads the record's Data
    /// itself) and put what it meant onto the graph as visible nodes and wires (a checkbox that was a hidden check
    /// becomes a wired condition). Runs when the asset is read, before the graph is linked; the editor then saves the
    /// upgraded graph, and a write stamps the current version, so a step runs once per asset. A step must be safe to
    /// run on a graph that does not need it (it changes nothing then).
    /// </summary>
    public interface IGraphUpgrade
    {
        /// <summary>The format version this step brings a graph to; steps run in the order of it.</summary>
        int Version { get; }

        /// <summary>
        /// Changes the graph just read from the asset. elements — every node and arrow read, by record id (new nodes the
        /// step adds get their ids when the graph is written). True — something changed.
        /// </summary>
        bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues);
    }

    /// <summary>The upgrade steps the code knows (every non-abstract IGraphUpgrade with a parameterless constructor).</summary>
    public static class GraphUpgrades
    {
        private static List<IGraphUpgrade> _steps;

        /// <summary>The steps in the order of their versions.</summary>
        public static IReadOnlyList<IGraphUpgrade> Steps => _steps ??= Collect();

        /// <summary>The current format: the highest version a step brings a graph to (0 — no steps).</summary>
        public static int Current
        {
            get
            {
                int current = 0;
                foreach (IGraphUpgrade step in Steps)
                    current = Math.Max(current, step.Version);
                return current;
            }
        }

        /// <summary>Runs every step newer than the asset's format on the graph read from it. True — something changed.</summary>
        public static bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
            List<GraphIssue> issues)
        {
            if (asset == null || graph == null)
                return false;

            bool changed = false;
            foreach (IGraphUpgrade step in Steps)
            {
                if (step.Version <= asset.FormatVersion)
                    continue;

                try
                {
                    changed |= step.Apply(asset, graph, elements, issues);
                }
                catch (Exception exception)
                {
                    issues?.Add(new GraphIssue(GraphIssueCode.OldModel, false, asset.RootId, step.GetType().Name,
                        $"the upgrade step {step.GetType().Name} failed: {exception.Message}"));
                }
            }

            // Every node a step added has an id of its own from now on — none of a record of the asset (the canvas finds a
            // node's place by its id: a new node must not take an old record's)
            if (changed)
                graph.AssignIds(RecordIds(asset));
            return changed;
        }

        /// <summary>The ids of every record of the asset (read or not).</summary>
        public static HashSet<int> RecordIds(BehaviorGraphAsset asset)
        {
            var ids = new HashSet<int>();
            if (asset == null)
                return ids;

            foreach (GraphRecord record in asset.Records)
            {
                if (record != null)
                    ids.Add(record.Id);
            }

            return ids;
        }

        private static List<IGraphUpgrade> Collect()
        {
            var steps = new List<IGraphUpgrade>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException partial)
                {
                    types = partial.Types;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface || !typeof(IGraphUpgrade).IsAssignableFrom(type) ||
                        type.GetConstructor(Type.EmptyTypes) == null)
                        continue;

                    steps.Add((IGraphUpgrade)Activator.CreateInstance(type));
                }
            }

            steps.Sort((left, right) => left.Version != right.Version
                ? left.Version.CompareTo(right.Version)
                : string.CompareOrdinal(left.GetType().FullName, right.GetType().FullName));
            return steps;
        }
    }
}
