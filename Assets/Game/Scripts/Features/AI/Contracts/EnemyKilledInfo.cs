using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>An enemy died: what, where, whether the tank ran it down, whether it was the elite.</summary>
    public readonly struct EnemyKilledInfo
    {
        public readonly AIEnemyKind Kind;
        public readonly Vector3 Point;
        public readonly bool Crushed;
        public readonly bool Elite;

        public EnemyKilledInfo(AIEnemyKind kind, Vector3 point, bool crushed, bool elite)
        {
            Kind = kind;
            Point = point;
            Crushed = crushed;
            Elite = elite;
        }
    }
}
