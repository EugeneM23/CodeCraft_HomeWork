using UnityEngine;

namespace Gameplay
{
    public interface IWeapon
    {
        public void Shoot(Vector3 fireDirection);
    }
}