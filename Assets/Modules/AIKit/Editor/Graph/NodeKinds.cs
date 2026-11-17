using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>What a node is on the canvas: the deciding layer, a block of a chain, a sensor, a question, a value.</summary>
    public enum NodeKind
    {
        Brain,
        Decision,
        Block,
        Sensor,
        Condition,
        Value
    }

    /// <summary>
    /// The kind of a node type and how the canvas paints it (step 90, spec 4.1): by what the type is — a Brain, a decision,
    /// a sensor, a condition, a value node, or a block coloured by the top folder of its path (Combat, Movement, Squad…) —
    /// never by pieces of its path picked out by hand. The list of nodes shows the same colour as a tile before each name.
    /// </summary>
    public static class NodeKinds
    {
        private static readonly Color BrainColor = new(0.55f, 0.4f, 0.05f);
        private static readonly Color DecisionColor = new(0.3f, 0.3f, 0.36f);
        private static readonly Color SensorColor = new(0.45f, 0.35f, 0.12f);
        private static readonly Color ConditionColor = new(0.1f, 0.36f, 0.34f);
        private static readonly Color ValueColor = new(0.08f, 0.33f, 0.45f);
        private static readonly Color BranchColor = new(0.62f, 0.13f, 0.13f);
        private static readonly Color PlainColor = new(0.25f, 0.25f, 0.25f);

        // The blocks by the group of their path (the folder their name stands in): a blow — orange, movement — blue, the
        // squad — purple, data — teal, objects — brown, everyday life — green; Flow and Debug keep the plain title (Branch is red, the user's word)
        private static readonly Dictionary<string, Color> BlockColors = new()
        {
            { "Combat", new Color(0.6f, 0.33f, 0.08f) },
            { "Movement", new Color(0.12f, 0.32f, 0.55f) },
            { "Squad", new Color(0.36f, 0.22f, 0.55f) },
            { "Variables", new Color(0.08f, 0.38f, 0.36f) },
            { "Data", new Color(0.08f, 0.38f, 0.36f) },
            { "Objects", new Color(0.42f, 0.28f, 0.16f) },
            { "Life", new Color(0.2f, 0.4f, 0.18f) }
        };

        /// <summary>The kind of a node type (null or not a node — a block).</summary>
        public static NodeKind Of(Type type)
        {
            if (type == null)
                return NodeKind.Block;
            if (typeof(CombatBrainTask).IsAssignableFrom(type))
                return NodeKind.Brain;
            if (typeof(ReasonerTask).IsAssignableFrom(type))
                return NodeKind.Decision;
            if (typeof(SensorTask).IsAssignableFrom(type))
                return NodeKind.Sensor;
            if (typeof(AgentCondition).IsAssignableFrom(type))
                return NodeKind.Condition;
            if (typeof(AgentValue).IsAssignableFrom(type))
                return NodeKind.Value;

            return NodeKind.Block;
        }

        /// <summary>The group of the type in the list: the folder its name stands in ("Combat" of "AI/Actions/Combat/Attack"); "" — none.</summary>
        public static string FolderOf(Type type)
        {
            var info = type != null
                ? (AIKitNodeAttribute)Attribute.GetCustomAttribute(type, typeof(AIKitNodeAttribute))
                : null;
            if (info == null || string.IsNullOrEmpty(info.Path))
                return "";

            int last = info.Path.LastIndexOf('/');
            if (last <= 0)
                return "";

            int before = info.Path.LastIndexOf('/', last - 1);
            return info.Path.Substring(before + 1, last - before - 1);
        }

        /// <summary>The colour of a node's title by what it is; null — the plain title (Wait, Random, Debug Log).</summary>
        public static Color? TitleColor(Type type)
        {
            switch (Of(type))
            {
                case NodeKind.Brain:
                    return BrainColor;
                case NodeKind.Decision:
                    return DecisionColor;
                case NodeKind.Sensor:
                    return SensorColor;
                case NodeKind.Condition:
                    return ConditionColor;
                case NodeKind.Value:
                    return ValueColor;
            }

            if (type == typeof(BranchTask))
                return BranchColor;

            return BlockColors.TryGetValue(FolderOf(type), out Color color) ? color : null;
        }

        /// <summary>The colour of the tile before the node's name in the list (always one: plain grey for the plain ones).</summary>
        public static Color Tint(Type type) => TitleColor(type) ?? PlainColor;

        /// <summary>The tile of a condition made of a flag of the blackboard (a Compare "is on").</summary>
        public static Color FlagTint => ConditionColor;

        /// <summary>The word the list adds to a name another kind of node shares ("Chase (decision)"); null — a block.</summary>
        public static string Word(NodeKind kind) => kind switch
        {
            NodeKind.Brain => "brain",
            NodeKind.Decision => "decision",
            NodeKind.Sensor => "sensor",
            NodeKind.Condition => "condition",
            NodeKind.Value => "value",
            _ => null
        };
    }
}
