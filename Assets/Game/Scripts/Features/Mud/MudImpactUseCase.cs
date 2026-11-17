using UnityEngine;

namespace Game
{
    /// <summary>
    /// What hits do to mud: a bullet leaves a small dent and spits a few clumps, a blast leaves a
    /// crater and throws a fountain of them. Ground that is not mud is left alone.
    /// </summary>
    public static class MudImpactUseCase
    {
        /// <summary>
        /// What a blast or a foot can find mud under it on: everything but people, monsters,
        /// dummies, layer 6 (Interactable) and things in flight (layers 6–9, 11–13; ProjectSettings/TagManager.asset).
        /// </summary>
        public const int GROUND_MASK = ~((1 << PhysicsLayers.Interactable) | (1 << PhysicsLayers.Dummy) | (1 << PhysicsLayers.Enemy) | (1 << PhysicsLayers.Character) | (1 << PhysicsLayers.ThrowItem) | (1 << PhysicsLayers.Bullet) | (1 << PhysicsLayers.Weapon));

        /// <summary>How far (m) above the mud a blast still marks it.</summary>
        private const float BLAST_REACH = 1.5f;

        public static bool IsMud(Collider collider) =>
            collider != null && collider.TryGetComponent(out MudSurface _);

        /// <summary>Finds the mud straight below <paramref name="point"/>, within <paramref name="reach"/>.</summary>
        public static bool TryFindBelow(Vector3 point, float reach, out MudSurface surface, out Vector3 ground)
        {
            surface = null;
            ground = point;

            if (!Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, reach + 0.5f, GROUND_MASK,
                    QueryTriggerInteraction.Ignore))
                return false;

            ground = hit.point;
            return hit.collider.TryGetComponent(out surface);
        }

        /// <summary>
        /// A bullet into mud: a dent a little longer along its flight, and a spit of clumps back
        /// up the way it came.
        /// </summary>
        public static void Bullet(Collider collider, Vector3 point, Vector3 direction)
        {
            if (collider == null || !collider.TryGetComponent(out MudSurface surface))
                return;

            Vector3 flat = new(direction.x, 0f, direction.z);

            // The flatter it came in, the longer the gouge along its flight.
            float steep = direction.sqrMagnitude > 1e-6f ? Mathf.Abs(direction.normalized.y) : 1f;
            float size = Random.Range(0.75f, 1.3f);
            float length = 0.06f * size * Mathf.Lerp(2.2f, 1f, steep);
            float width = 0.06f * size * Random.Range(0.85f, 1.1f);

            surface.Dent(point, flat, length, width, Random.Range(0.5f, 0.85f), 2f, Random.Range(0.35f, 0.6f), 0.7f);

            Vector3 back = flat.sqrMagnitude > 1e-6f ? -flat.normalized : Vector3.zero;
            surface.Throw(point, Vector3.up * 3f + back * 1f, 1.4f, Random.Range(4, 9), 0.6f);
            GameSignals.RaiseMudHit(point, 0.1f);
        }

        /// <summary>
        /// A blast of radius <paramref name="radius"/> at <paramref name="point"/>: a crater down to
        /// the bottom of the mud with a rim round it, and a fountain of clumps.
        /// </summary>
        public static void Blast(Vector3 point, float radius)
        {
            if (!TryFindBelow(point, BLAST_REACH, out MudSurface surface, out Vector3 ground))
                return;
            GameSignals.RaiseMudHit(ground, radius);

            // Weaker the higher it went off.
            float closeness = 1f - Mathf.Clamp01((point.y - ground.y) / BLAST_REACH);
            if (closeness <= 0f)
                return;

            float crater = Mathf.Clamp(radius * 0.3f, 0.4f, 1.2f) * Random.Range(0.8f, 1.2f);
            Vector3 facing = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            float stretch = Random.Range(0.8f, 1.2f);

            surface.Dent(ground, facing, crater * stretch, crater / stretch, Random.Range(1.2f, 1.6f) * closeness, 2f,
                Random.Range(0.25f, 0.45f), 1f);

            // Smaller pits round it where chunks of the blast and the clods came down.
            int pits = Random.Range(3, 7);
            for (int i = 0; i < pits; i++)
            {
                Vector3 away = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                Vector3 at = ground + away * crater * Random.Range(0.9f, 1.8f);
                float pit = crater * Random.Range(0.12f, 0.3f);
                surface.Dent(at, away, pit * Random.Range(1f, 1.6f), pit, Random.Range(0.3f, 0.7f) * closeness, 2f,
                    0.4f, 0.8f);
            }

            surface.Throw(ground + Vector3.up * 0.1f, Vector3.up * 7f, 4.5f, Mathf.RoundToInt(50 * closeness), 1.3f);
        }
    }
}
