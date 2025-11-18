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
        [OdinSerialize] private Dictionary<WeaponID, SceneEntity> _weapons = new();
        [OdinSerialize] private Dictionary<WeaponID, SceneEntity> _pickUpWeapons = new();

        public SceneEntity GetPickUpWeapon(WeaponID id)
        {
            return SceneEntity.Create(_pickUpWeapons[id], transform.position, transform.rotation);
        }

        public SceneEntity GetWeapon(WeaponID id) => _weapons[id];
    }
}