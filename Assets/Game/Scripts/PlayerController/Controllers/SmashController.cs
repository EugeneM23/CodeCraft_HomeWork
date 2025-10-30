using UnityEngine;

namespace PlayerController
{
    public class SmashController : ITickable
    {
        private readonly PlayerController _player;

        private float _lastSPressTime;
        private readonly float _doublePressThreshold = 0.3f; // Максимальное время между нажатиями

        public SmashController(PlayerController player) => _player = player;

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                float currentTime = Time.time;

                if (currentTime - _lastSPressTime <= _doublePressThreshold)
                {
                    _player.AddImpulse(Vector2.down * 70f);
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