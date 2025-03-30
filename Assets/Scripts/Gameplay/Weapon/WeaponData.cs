using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Weapons/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [SerializeField] public int Damage;
        [SerializeField] public Color BulletColor;
        [SerializeField] public PhysicsLayer PhysicsLayer;
        [SerializeField] public float FireRate;
    }
}