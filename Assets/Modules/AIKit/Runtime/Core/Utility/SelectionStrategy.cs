using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// How one option is picked among scored ones (handbook §4.6). A strategy belongs to whoever owns the set of
    /// decisions — the agent's abilities — not to a graph node, so it is set once and every node that
    /// asks for a choice gets the same character. Scores at or below Threshold never win.
    /// </summary>
    public interface ISelectionStrategy
    {
        /// <summary>Index of the chosen score; -1 — nothing passes.</summary>
        int Select(IReadOnlyList<float> scores, AgentContext context);
    }

    /// <summary>Always the best (bosses, a fight that must look sharp). Ties go to the earlier option.</summary>
    [Serializable]
    public sealed class MaxSelection : ISelectionStrategy
    {
        public float Threshold;

        public MaxSelection(float threshold = 0f) => Threshold = threshold;

        public int Select(IReadOnlyList<float> scores, AgentContext context)
        {
            int best = -1;
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] > Threshold && (best < 0 || scores[i] > scores[best]))
                    best = i;
            }

            return best;
        }
    }

    /// <summary>Any option above the threshold, equally likely (a crowd that must not move in step).</summary>
    [Serializable]
    public sealed class RandomSelection : ISelectionStrategy
    {
        public float Threshold;

        public RandomSelection(float threshold = 0f) => Threshold = threshold;

        public int Select(IReadOnlyList<float> scores, AgentContext context)
        {
            int passing = 0;
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] > Threshold)
                    passing++;
            }

            if (passing == 0)
                return -1;

            int pick = context != null ? context.Range(0, passing - 1) : 0;
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] > Threshold && pick-- == 0)
                    return i;
            }

            return -1;
        }
    }

    /// <summary>A roulette by score: natural variety, a better option comes up more often.</summary>
    [Serializable]
    public sealed class RandomWeightedSelection : ISelectionStrategy
    {
        public float Threshold;

        public RandomWeightedSelection(float threshold = 0f) => Threshold = threshold;

        public int Select(IReadOnlyList<float> scores, AgentContext context) =>
            SelectionMath.Roulette(scores, null, scores.Count, Threshold, context);
    }

    /// <summary>
    /// A roulette among the best Count options only — the working default for a fight: sensible and not predictable.
    /// Options are ranked by score alone (ties keep their order), so the worst of three never comes up with Count 2.
    /// </summary>
    [Serializable]
    public sealed class TopRandomSelection : ISelectionStrategy
    {
        [Min(1)] public int Count = 2;
        public float Threshold;

        [NonSerialized] private readonly List<int> _order = new();

        public TopRandomSelection(int count = 2, float threshold = 0f)
        {
            Count = count;
            Threshold = threshold;
        }

        public int Select(IReadOnlyList<float> scores, AgentContext context)
        {
            _order.Clear();
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] > Threshold)
                    _order.Add(i);
            }

            // Insertion sort by score only, descending; equal scores keep their order — no allocation, small lists
            for (int i = 1; i < _order.Count; i++)
            {
                int index = _order[i];
                int j = i - 1;
                while (j >= 0 && scores[_order[j]] < scores[index])
                {
                    _order[j + 1] = _order[j];
                    j--;
                }

                _order[j + 1] = index;
            }

            return SelectionMath.Roulette(scores, _order, Mathf.Clamp(Count, 1, Math.Max(1, _order.Count)), Threshold,
                context);
        }
    }

    internal static class SelectionMath
    {
        /// <summary>Weighted roulette over the first `take` options (through `order` when given).</summary>
        public static int Roulette(IReadOnlyList<float> scores, List<int> order, int take, float threshold,
            AgentContext context)
        {
            int count = order?.Count ?? scores.Count;
            take = Math.Min(take, count);
            float total = 0f;
            int last = -1;
            for (int n = 0; n < take; n++)
            {
                int i = order != null ? order[n] : n;
                if (scores[i] <= threshold)
                    continue;

                total += scores[i];
                last = i;
            }

            if (last < 0)
                return -1;

            double roll = (context?.NextRandom() ?? 0.0) * total;
            for (int n = 0; n < take; n++)
            {
                int i = order != null ? order[n] : n;
                if (scores[i] <= threshold)
                    continue;

                roll -= scores[i];
                if (roll < 0d)
                    return i;
            }

            return last;                // rounding at the very end of the wheel
        }
    }
}
