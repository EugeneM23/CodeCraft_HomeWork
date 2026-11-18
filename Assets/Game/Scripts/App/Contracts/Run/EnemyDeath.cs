using UnityEngine;

namespace Game
{
    /// <summary>
    /// An enemy died. Raised by AI through <see cref="IEnemyDeaths.Report"/>; heard by combat (experience orbs), the
    /// core (counters, marks), the menu (HUD «Давка ×N») and audio. A struct: raising it allocates nothing.
    /// </summary>
    public readonly struct EnemyDeath
    {
        public readonly EnemyKind Kind;

        /// <summary>Where it died, on the ground: the experience drops there.</summary>
        public readonly Vector3 Point;

        /// <summary>Killed by the tank's tracks (a crush), not by fire.</summary>
        public readonly bool Crushed;

        /// <summary>An elite (or a boss): it brings samples and fills the elite bar of the HUD.</summary>
        public readonly bool Elite;

        public EnemyDeath(EnemyKind kind, Vector3 point, bool crushed, bool elite)
        {
            Kind = kind;
            Point = point;
            Crushed = crushed;
            Elite = elite;
        }
    }
}
