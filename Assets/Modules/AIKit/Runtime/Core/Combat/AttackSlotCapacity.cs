using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A target's say on how many may attack it at once — the one place this number is set (handbook §18: as many
    /// attack as the target allows; the nodes only ask). Put it on the object the agents fight (the player), set the
    /// number; a group of places of its own (the archers' "Ranged") gets its own number in Pools (§18.2, rule 1: a pool
    /// per kind of attack, each with its limit). Agents ask for a place with Take Attack Slot or wait for one circling;
    /// those who find every place taken do something else and ask again. Without this component a target lets one
    /// attack per three agents of the group.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/Attack Slots")]
    public sealed class AttackSlotCapacity : MonoBehaviour, ITokenCapacity
    {
        /// <summary>The places of one named group (the Pool field of the nodes that ask).</summary>
        [Serializable]
        public sealed class PoolSlots
        {
            [Tooltip("The name of the group, as in the Pool field of the nodes")]
            public string Pool;

            [Tooltip("How many agents of this group may attack at the same time")]
            [Min(0)] public int Slots = 1;
        }

        [Tooltip("How many agents may attack this object at the same time (the common places)")]
        [Min(0)]
        [SerializeField] private int _slots = 2;

        [Tooltip("Groups of places with a number of their own (archers: \"Ranged\")")]
        [SerializeField] private List<PoolSlots> _pools = new();

        /// <summary>The common places.</summary>
        public int Slots
        {
            get => _slots;
            set => _slots = Mathf.Max(0, value);
        }

        /// <summary>The places of a named group (set or added).</summary>
        public void SetPool(string pool, int slots)
        {
            PoolSlots found = _pools.Find(entry => entry.Pool == pool);
            if (found == null)
                _pools.Add(found = new PoolSlots { Pool = pool });

            found.Slots = Mathf.Max(0, slots);
        }

        public int CapacityOf(string pool)
        {
            for (int i = 0; i < _pools.Count; i++)
            {
                if (_pools[i] != null && _pools[i].Pool == pool)
                    return _pools[i].Slots;
            }

            return _slots;
        }
    }
}
