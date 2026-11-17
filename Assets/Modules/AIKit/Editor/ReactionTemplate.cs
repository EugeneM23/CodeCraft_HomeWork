using System;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// Adds the ready set of reactions to a graph with a Brain — added through the document, never rebuilt: whatever the
    /// author drew stays as it is (step 90, C8). Target Attacks → Random → Dash (Aside) / Dash (Back), a dash that cannot
    /// be made → Block (handbook §19.2: aside or back, never toward the attacker; no dodge — a block, not nothing); Got
    /// Hit (from behind) → Turn To Target; Detection gets the Hit By shape, so whoever struck is seen. The reactions take
    /// the place of the most important decision whose chain attacks — it and every less important decision move down
    /// (the order the author gave stays), so the reactions cut in on the fight; the nodes go below everything on the
    /// canvas. A reaction (or the shape) the graph has already is kept and not added again.
    /// </summary>
    public static class ReactionTemplate
    {
        /// <summary>What was added (null — it was there already, or could not be added).</summary>
        public sealed class Added
        {
            public AgentState TargetAttacks;
            public AgentState Random;
            public AgentState DashAside;
            public AgentState DashBack;
            public AgentState Block;
            public AgentState GotHit;
            public AgentState Turn;
            public SensorTask HitBy;

            public bool Any => TargetAttacks != null || GotHit != null || HitBy != null;
        }

        /// <summary>The share of the dodges that go aside; the rest go back (handbook §19.2: aside 0.7).</summary>
        public const float ASIDE = 0.7f;

        // The nodes laid out from the first one: the decisions in a column, their chains to the right
        private static readonly Vector2 Chain = new(420f, 0f);
        private static readonly Vector2 Row = new(0f, 420f);

        /// <summary>
        /// Adds what the graph lacks of the set; nothing on a graph with no Brain, one opened read-only, or a squad's
        /// command graph (its Squad Brain takes a squad's decisions only — a character's reaction is no part of it).
        /// </summary>
        public static Added AddTo(GraphDocument document)
        {
            var added = new Added();
            AgentState brain = document?.Graph?.Brain;
            if (brain == null || document.IsReadOnly || brain.Tasks.Exists(task => task is SquadBrainTask))
                return added;

            bool attacks = !Has<TargetAttacksReaction>(brain);
            bool gotHit = !Has<GotHitReaction>(brain);
            AgentState root = document.Graph.Root;
            Vector2 origin = BelowEverything(document, brain);
            if (attacks || gotHit)
            {
                int place = MakeRoom(brain, (attacks ? 1 : 0) + (gotHit ? 1 : 0));
                if (attacks)
                {
                    AddTargetAttacks(document, root, brain, place++, origin, added);
                    origin += Row;
                }

                if (gotHit)
                {
                    added.GotHit = document.AddStateWithTask(root, IdOf<GotHitReaction>(), origin);
                    var reaction = (GotHitReaction)added.GotHit.Reasoner;
                    reaction.Priority = place;
                    reaction.FromBehindOnly = true;         // a blow from the front was seen coming
                    added.Turn = document.AddStateWithTask(root, IdOf<TurnToTargetTask>(), origin + Chain);
                    document.Connect(brain, added.GotHit);
                    document.Connect(added.GotHit, added.Turn);
                }
            }

            DetectionTask detection = document.Detection();
            if (detection != null && !detection.Sensors.Exists(shape => shape is HitBySensorTask))
            {
                Vector2 at = document.PositionOfNode(detection) + new Vector2(-360f, 260f);
                added.HitBy = document.AddShape(IdOf<HitBySensorTask>(), at, detection);
            }

            if (added.Any)
                document.Touch("Add Reactions");
            return added;
        }

        // Target Attacks → Random: Dash (Aside) / Dash (Back); either dash that cannot be made → Block
        private static void AddTargetAttacks(GraphDocument document, AgentState root, AgentState brain, int priority,
            Vector2 at, Added added)
        {
            added.TargetAttacks = document.AddStateWithTask(root, IdOf<TargetAttacksReaction>(), at);
            added.TargetAttacks.Reasoner.Priority = priority;
            added.Random = document.AddStateWithTask(root, IdOf<PickRandomTask>(), at + Chain);
            added.DashAside = document.AddStateWithTask(root, IdOf<DashTask>(), at + 2f * Chain + new Vector2(0f, -140f));
            added.DashBack = document.AddStateWithTask(root, IdOf<DashTask>(), at + 2f * Chain + new Vector2(0f, 160f));
            added.Block = document.AddStateWithTask(root, IdOf<BlockTask>(), at + 3f * Chain);

            PickRandomTask random = added.Random.Random;
            while (random.Outputs.Count < 2)
                random.Outputs.Add(new RandomOutput());
            random.Outputs[0].Weight = ASIDE;
            random.Outputs[1].Weight = 1f - ASIDE;
            ((DashTask)added.DashAside.Tasks[0]).Direction = DashDirection.Aside;
            ((DashTask)added.DashBack.Tasks[0]).Direction = DashDirection.Back;

            document.Connect(brain, added.TargetAttacks);
            document.Connect(added.TargetAttacks, added.Random);
            document.ConnectOutput(added.Random, added.DashAside, 0);
            document.ConnectOutput(added.Random, added.DashBack, 1);
            document.Connect(added.DashAside, added.Block, TransitionTrigger.TasksFailed);
            document.Connect(added.DashBack, added.Block, TransitionTrigger.TasksFailed);
        }

        /// <summary>
        /// Frees `count` priorities for the reactions and returns the first: the place of the most important decision
        /// whose chain attacks (else of the most important decision) — it and every less important one move down.
        /// </summary>
        public static int MakeRoom(AgentState brain, int count)
        {
            int place = int.MaxValue;
            int first = int.MaxValue;
            foreach (Transition wire in brain.Transitions)
            {
                ReasonerTask decision = wire.Target?.Reasoner;
                if (decision == null)
                    continue;

                first = Math.Min(first, decision.Priority);
                if (decision is not ReactionDecision && GraphValidator.ChainAttacks(wire.Target))
                    place = Math.Min(place, decision.Priority);
            }

            if (place == int.MaxValue)
                place = first == int.MaxValue ? 1 : first;

            foreach (Transition wire in brain.Transitions)
            {
                ReasonerTask decision = wire.Target?.Reasoner;
                if (decision != null && decision.Priority >= place)
                    decision.Priority += count;
            }

            return place;
        }

        private static bool Has<T>(AgentState brain) where T : ReasonerTask =>
            brain.Transitions.Exists(wire => wire.Target?.Reasoner is T);

        // Under the lowest node of the canvas, at the Brain's left edge: the new nodes never land on the author's
        private static Vector2 BelowEverything(GraphDocument document, AgentState brain)
        {
            float bottom = float.MinValue;
            foreach (AgentState state in document.States())
            {
                if (state != document.Graph.Root)
                    bottom = Mathf.Max(bottom, document.PositionOf(state).y);
            }

            foreach (GraphRecord record in document.Asset.Records)
                bottom = Mathf.Max(bottom, record.Position.y);
            foreach (GraphVariableNode node in document.Asset.VariableNodes)
                bottom = Mathf.Max(bottom, node.Position.y);

            float x = document.PositionOf(brain).x;
            return new Vector2(x, (bottom == float.MinValue ? 0f : bottom) + 400f);
        }

        private static string IdOf<T>() where T : AgentNode =>
            ((AIKitNodeAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(AIKitNodeAttribute))).Id;
    }
}
