using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class DashController : ITickable
    {
        private readonly PlayerController _player;

        private float _lastSPressTime;
        private readonly float _doublePressThreshold = 0.3f; // Максимальное время между нажатиями

        public DashController(PlayerController player) => _player = player;

        public void Tick()
        {
            bool right = Input.GetKey(KeyCode.D) && Input.GetKeyDown(KeyCode.LeftShift);
            bool left = Input.GetKey(KeyCode.A) && Input.GetKeyDown(KeyCode.LeftShift);

            if (right.Log() || left)
            {

                if (right)
                    _player.AddImpulse(Vector2.right * 50f);
                else if (left)
                    _player.AddImpulse(Vector2.left * 50f);

                _lastSPressTime = -1f;
            }
        }
    }
}