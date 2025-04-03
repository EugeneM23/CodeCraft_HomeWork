using System;
using UnityEngine;

namespace Gameplay
{
    public class PlayerInput : MonoBehaviour
    {
        private static int count;
        public event Action<Vector3> OnShoot;
        public event Action<Vector3> OnMove;
        public event Action OnNextWeapon;
        public event Action OnPreviousWepon;

        private void Update()
        {
            OnMove?.Invoke(new Vector3(Input.GetAxis("Horizontal"), 0, 0));

            Fire();
            
            WeaponSwitch();
        }

        private void Fire()
        {
            if (Input.GetKey(KeyCode.Space))
                OnShoot?.Invoke(Vector3.up);
        }

        private  void WeaponSwitch()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");

            if (scroll > 0f)
                OnNextWeapon?.Invoke();
            
            if (scroll < 0f) 
                OnPreviousWepon?.Invoke();
        }
    }
}