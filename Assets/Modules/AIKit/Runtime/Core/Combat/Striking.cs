using UnityEngine;

namespace AIKit
{
    /// <summary>A blow that landed: who struck and how hard.</summary>
    public readonly struct HitInfo
    {
        public readonly Transform Attacker;
        public readonly float Damage;

        public HitInfo(Transform attacker, float damage)
        {
            Attacker = attacker;
            Damage = damage;
        }
    }

    /// <summary>
    /// Something that can be hit by the Attack node: a component on the target (health, a dummy, a door). An AI
    /// character is one itself (it remembers the hit: AICharacter.TakeHit); a game's health next to it is another.
    /// </summary>
    public interface IHitReceiver
    {
        void ReceiveHit(in HitInfo hit);
    }

    /// <summary>How a blow reaches the target by default (a weapon's plain Strike, an ability's blow, a projectile).</summary>
    public static class DirectStriker
    {
        // One delivery at a time uses the shared list; a receiver that strikes back from inside its ReceiveHit gets its own
        private static readonly System.Collections.Generic.List<IHitReceiver> Receivers = new();
        private static int _depth;

        /// <summary>
        /// Hands the hit to every receiver on the target and above it (step 90: one hit path — the game's health and the
        /// AI character's memory of hits each hear the one blow, nobody reports it twice). None — the blow hits nothing.
        /// </summary>
        public static void Deliver(Transform attacker, Transform target, float damage)
        {
            if (target == null)
                return;

            var hit = new HitInfo(attacker, damage);
            if (_depth > 0)
            {
                foreach (IHitReceiver nested in target.GetComponentsInParent<IHitReceiver>())
                    nested.ReceiveHit(hit);
                return;
            }

            _depth++;
            try
            {
                target.GetComponentsInParent(false, Receivers);
                for (int i = 0; i < Receivers.Count; i++)
                    Receivers[i].ReceiveHit(hit);
            }
            finally
            {
                Receivers.Clear();
                _depth--;
            }
        }
    }
}
