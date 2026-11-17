using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Ignores collisions between pairs of colliders for a short while, then restores them.
    ///
    /// Bricks of a wall sit face to face with no gap. A brick knocked out of the wall is wedged
    /// between its neighbours, and their contacts stop it within a few centimetres even at 5 m/s.
    /// Letting it pass its neighbours for the first moments lets it actually leave the wall; by
    /// the time collisions come back it is well clear of them.
    /// </summary>
    public sealed class TemporaryCollisionIgnore : MonoBehaviour
    {
        private struct Pair
        {
            public Collider a;
            public Collider b;
            public float until;
        }

        private static TemporaryCollisionIgnore _instance;
        private readonly List<Pair> _pairs = new();

        public static void Ignore(Collider a, Collider b, float seconds)
        {
            if (a == null || b == null || a == b)
                return;

            if (_instance == null)
            {
                var go = new GameObject("[TemporaryCollisionIgnore]") { hideFlags = HideFlags.DontSave };
                _instance = go.AddComponent<TemporaryCollisionIgnore>();
            }

            Physics.IgnoreCollision(a, b, true);
            _instance._pairs.Add(new Pair { a = a, b = b, until = Time.time + seconds });
        }

        private void Update()
        {
            float now = Time.time;

            for (int i = _pairs.Count - 1; i >= 0; i--)
            {
                Pair pair = _pairs[i];
                if (pair.until > now)
                    continue;

                if (pair.a != null && pair.b != null)
                    Physics.IgnoreCollision(pair.a, pair.b, false);

                _pairs.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
