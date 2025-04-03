using System;
using UnityEngine;

namespace Gameplay
{
    public class PlayerInput : MonoBehaviour
    {
        public event Action<Vector3> OnShoot;
        public event Action<Vector3> OnMove;

        private void Update()
        {
            OnMove?.Invoke(new Vector3(Input.GetAxis("Horizontal"), 0, 0));

            if (Input.GetKey(KeyCode.Space))
                OnShoot?.Invoke(Vector3.up);
        }
    }
}