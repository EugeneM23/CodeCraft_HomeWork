using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// "Who is around" without physics: the candidates are the transforms listed here, within the radius. For prototypes,
    /// a top-down sketch without colliders and tests; a body hands it over as its eyes (a game with colliders sees with
    /// its physics instead).
    /// </summary>
    public sealed class ListTargetScanner : ITargetScanner
    {
        private readonly List<Transform> _candidates;

        /// <summary>candidates — the list to look in; read on every scan, so what is added later is seen too.</summary>
        public ListTargetScanner(List<Transform> candidates = null) =>
            _candidates = candidates ?? new List<Transform>();

        public List<Transform> Candidates => _candidates;

        public int Scan(Vector3 center, float radius, List<Transform> results)
        {
            results.Clear();
            float limit = radius * radius;
            for (int i = 0; i < _candidates.Count; i++)
            {
                Transform candidate = _candidates[i];
                if (candidate != null && candidate.gameObject.activeInHierarchy &&
                    (candidate.position - center).sqrMagnitude <= limit)
                    results.Add(candidate);
            }

            return results.Count;
        }
    }
}
