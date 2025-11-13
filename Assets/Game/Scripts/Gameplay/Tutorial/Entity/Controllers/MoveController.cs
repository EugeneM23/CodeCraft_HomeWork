using System;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Common;
using SampleGame;
using UnityEngine;

namespace Game
{
    public class MoveController : MonoBehaviour
    {
        [SerializeField] private Joystick _joystick;
        [SerializeField] private SceneEntity _character;

        private void Update() => Move();

        private void Move()
        {
            Vector3 direction = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

            if (direction == Vector3.zero)
                direction = new Vector3(_joystick.Horizontal, 0, _joystick.Vertical);

            _character.GetMoveDirection().Value = direction;
        }
    }
}