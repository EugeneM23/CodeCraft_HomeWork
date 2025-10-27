using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class SpeedComponent
    {
        private readonly ScriptableStats _stats;

        private float _horizontalSpeed;

        public SpeedComponent(ScriptableStats stats)
        {
            _stats = stats;
        }

        public float CalculateSpeed(float inputX)
        {
            float targetSpeed = inputX * _stats.MaxSpeed;

            _horizontalSpeed =
                Mathf.MoveTowards(_horizontalSpeed, targetSpeed, _stats.Acceleration * Time.fixedDeltaTime);

            return _horizontalSpeed;
        }
    }
}