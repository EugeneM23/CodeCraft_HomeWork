using Atomic.Entities;
using Modules.Common;
using UnityEngine;

namespace Game.Scripts.Gameplay
{
    public class MoveController : MonoBehaviour
    {
        [SerializeField] private Joystick _joystick;
        [SerializeField] private SceneEntity _character;

        private void Update() => Move();

        private void Move()
        {
            Vector3 direction = new Vector3(_joystick.Horizontal, 0, _joystick.Vertical);
            float deltaTime = Time.deltaTime;

            _character.Move(direction, deltaTime);
            _character.Rotate(direction, deltaTime);
        }
    }
}