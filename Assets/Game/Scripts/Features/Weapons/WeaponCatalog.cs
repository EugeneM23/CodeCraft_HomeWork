using System;
using System.Collections.Generic;
using Atomic.Entities;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class WeaponCatalog : SerializedMonoBehaviour
    {
        [OdinSerialize] private Dictionary<WeaponID, MonoEntity> _weapons = new();
        [OdinSerialize] private Dictionary<WeaponID, MonoEntity> _pickUpWeapons = new();

        public MonoEntity GetPickUpWeapon(WeaponID id)
        {
            return MonoEntity.Create(_pickUpWeapons[id], transform.position, transform.rotation);
        }

        public MonoEntity GetWeapon(WeaponID id)
        {
            return MonoEntity.Create(_weapons[id], transform.position, transform.rotation);
        }

        /// <summary>
        /// Adds every weapon prefab the catalog hands out to <paramref name="results"/>: what a pickup
        /// can put in the player's hands. CombatPoolPrewarm reads their bullets and casings off them.
        /// </summary>
        public void CollectWeaponPrefabs(List<MonoEntity> results)
        {
            if (_weapons == null)
                return;

            foreach (MonoEntity weapon in _weapons.Values)
                if (weapon != null)
                    results.Add(weapon);
        }
    }
}