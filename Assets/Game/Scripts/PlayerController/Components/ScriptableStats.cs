using UnityEngine;

namespace Game.Scripts.PlayerController
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Stats/PlayerStats")]
    public class ScriptableStats : ScriptableObject
    {
        public float MaxSpeed = 5f;
        public float Acceleration = 20f;
        public float GroundDeceleration = 30f;
        public float AirDeceleration = 10f;
        public float AirAcceleration = 10f;
        public float JumpPower = 10f;
        public float FallAcceleration = 30f;
        public float MaxFallSpeed = 20f;
        public float GrounderDistance = 0.1f;
        public LayerMask PlayerLayer;
    }
}