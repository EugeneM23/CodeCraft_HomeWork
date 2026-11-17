using System.Collections.Generic;
using UnityEngine;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// The perception questions of the core answered by 3D physics: who is around (an overlap on the target layers —
    /// triggers included — keeping only entities with an IPerceivable; a Senses sensor of the graph asks it), line of
    /// sight and ground under a point. The character's own colliders never answer; queries write into buffers made
    /// once. Reads its layers from the NavMeshBody live.
    /// </summary>
    public sealed class PhysicsEyes3D : ISightProbe, IGroundProbe, ITargetScanner, IAimProbe
    {
        private const int MAX_HITS = 8;
        private const int MAX_CANDIDATES = 32;

        private readonly RaycastHit[] _hits = new RaycastHit[MAX_HITS];
        private readonly Collider[] _overlaps = new Collider[MAX_CANDIDATES];
        private readonly Transform _root;
        private readonly NavMeshBody _settings;

        public PhysicsEyes3D(Transform owner, NavMeshBody settings)
        {
            _root = owner;
            _settings = settings;
        }

        public int Scan(Vector3 center, float radius, List<Transform> results)
        {
            results.Clear();
            LayerMask targets = _settings.TargetLayers;
            if (targets.value == 0 || radius <= 0f)
                return 0;

            int count = Physics.OverlapSphereNonAlloc(center, radius, _overlaps, targets, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                IPerceivable perceivable = _overlaps[i].GetComponentInParent<IPerceivable>();
                if (perceivable is not Component component)
                    continue;

                Transform candidate = component.transform;
                if (candidate == _root || candidate.IsChildOf(_root) || results.Contains(candidate))
                    continue;

                results.Add(candidate);
            }

            return results.Count;
        }

        /// <summary>The centre of the target's collider (its IPerceivable says better when it has one).</summary>
        public Vector3 AimPoint(Transform target) =>
            target.TryGetComponent(out Collider body) ? body.bounds.center : target.position;

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            return distance < 1e-4f || !FirstForeignHit(from, delta / distance, distance, _settings.SightBlockers, out _);
        }

        public bool FindGround(Vector3 point, Vector3 up, float maxDepth, out Vector3 ground)
        {
            if (FirstForeignHit(point, -up.normalized, maxDepth, _settings.GroundLayers, out RaycastHit hit))
            {
                ground = hit.point;
                return true;
            }

            ground = point;
            return false;
        }

        private bool FirstForeignHit(Vector3 origin, Vector3 direction, float distance, LayerMask mask,
            out RaycastHit first)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            first = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider.transform.IsChildOf(_root) || hit.distance >= best)
                    continue;

                best = hit.distance;
                first = hit;
            }

            return best < float.MaxValue;
        }
    }
}
