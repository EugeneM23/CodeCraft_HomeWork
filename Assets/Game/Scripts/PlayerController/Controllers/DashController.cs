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
            bool right = Input.GetKeyDown(KeyCode.D);
            bool left = Input.GetKeyDown(KeyCode.A);

            if (right || left)
            {
                float currentTime = Time.time;

                if (currentTime - _lastSPressTime <= _doublePressThreshold)
                {
                    if (right)
                        _player.AddImpulse(Vector2.right * 50f);
                    else if (left)
                        _player.AddImpulse(Vector2.left * 50f);

                    _lastSPressTime = -1f;
                }
                else
                {
                    _lastSPressTime = currentTime;
                }
            }
        }
    }
}