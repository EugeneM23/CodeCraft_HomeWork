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
    }
}