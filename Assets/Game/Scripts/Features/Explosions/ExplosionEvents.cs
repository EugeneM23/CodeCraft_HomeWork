using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Every blast in the level, whoever set it off: shells and grenades (ExplosionUseCase) and the
    /// air strike. Monsters listen to flinch and leap away, the horde takes its damage from it.
    /// Raised on the main thread; listeners must not allocate.
    /// </summary>
    public static class ExplosionEvents
    {
        /// <summary>A blast at a point, with its radius in metres.</summary>
        public static event Action<Vector3, float> Exploded;

        public static void Raise(Vector3 point, float radius) => Exploded?.Invoke(point, radius);

        /// <summary>Listeners of the last Play session are dropped (with domain reload off they would survive).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Exploded = null;
    }
}
