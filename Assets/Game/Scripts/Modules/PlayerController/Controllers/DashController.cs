using UnityEngine;

namespace Modules.PlayerController
{
    internal class DashController : ITickable
    {
        private readonly CharacterController2D _character;

        private float _lastSPressTime;
        private readonly float _doublePressThreshold = 0.3f; // Максимальное время между нажатиями

        public DashController(CharacterController2D character) => _character = character;

        public void Tick()
        {
            bool right = Input.GetKey(KeyCode.D) && Input.GetKeyDown(KeyCode.LeftShift);
            bool left = Input.GetKey(KeyCode.A) && Input.GetKeyDown(KeyCode.LeftShift);

            if (right || left)
            {
                if (right)
                    _character.Dash(Vector2.right * 50f);
                else if (left)
                    _character.Dash(Vector2.left * 50f);

                _lastSPressTime = -1f;
            }
        }
    }
}