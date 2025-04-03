using System;
using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [field: SerializeField] public float FireRate { get; private set; }

        [field: SerializeField] public BulletInfo _bulletInfo { get; private set; }
    }

    [Serializable]
    public struct BulletInfo
    {
        [field: SerializeField] public int Damage { get; private set; }
        [field: SerializeField] public Color BulletColor { get; private set; }
        [field: SerializeField] public PhysicsLayer PhysicsLayer { get; private set; }
        [field: SerializeField] public GameObject BulletPrefab { get; private set; }
        [field: SerializeField] public float BulletSpeed { get; private set; }
    }
}