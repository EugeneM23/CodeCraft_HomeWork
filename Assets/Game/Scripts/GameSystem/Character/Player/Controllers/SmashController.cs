using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class SmashController : ITickable
    {
        [Inject] private readonly CharacterController2D _controller;
        [Inject] private readonly SmashComponent _smash;

        private float _lastSPressTime;
        private readonly float _doublePressThreshold = 0.3f;

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.S))
            {
                float currentTime = Time.time;

                if (currentTime - _lastSPressTime <= _doublePressThreshold && !_controller.IsGrounded)
                {
                    _smash.Smash(Vector2.down * 70f);
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