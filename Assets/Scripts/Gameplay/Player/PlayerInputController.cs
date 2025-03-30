using System;
using UnityEngine;

namespace Gameplay
{
    public class PlayerInputController : MonoBehaviour
    {
        public event Action OnShoot;
        public event Action<Vector3> OnMove;

        private Vector3 _previousMoveDirection;
        public Vector3 MoveDirection { get; private set; }

        private void Update()
        {
            MoveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, 0);
            OnMove?.Invoke(MoveDirection);

            if (Input.GetKey(KeyCode.Space))
                OnShoot?.Invoke();
        }
    }
}