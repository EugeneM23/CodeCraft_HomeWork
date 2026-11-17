using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Gameplay moments that have no entity event of their own, for listeners outside the gameplay code:
    /// sound, camera, HUD. Raised on the main thread; no allocation per raise.
    /// </summary>
    public static class GameSignals
    {
        // Blasts are not here: every one comes through ExplosionEvents.Exploded (shells and the air strike).

        /// <summary>Something struck mud: point, size of the hit (m) — a bullet is small, a blast is its radius.</summary>
        public static event Action<Vector3, float> MudHit;

        /// <summary>The tank crushed an enemy to death: point, tank speed (m/s).</summary>
        public static event Action<Vector3, float> Crushed;

        /// <summary>The air strike was switched on.</summary>
        public static event Action AirStrikeCalled;

        /// <summary>An air bomb was dropped: the bomb, alive until it explodes.</summary>
        public static event Action<Transform> AirBombDropped;

        public static void RaiseMudHit(Vector3 point, float size) => MudHit?.Invoke(point, size);

        public static void RaiseCrushed(Vector3 point, float speed) => Crushed?.Invoke(point, speed);

        public static void RaiseAirStrikeCalled() => AirStrikeCalled?.Invoke();

        public static void RaiseAirBombDropped(Transform bomb) => AirBombDropped?.Invoke(bomb);
    }
}
