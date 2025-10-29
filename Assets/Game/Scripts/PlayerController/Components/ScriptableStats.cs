using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Scripts.PlayerController
{
    [CreateAssetMenu(fileName = "PlayerStats", menuName = "Stats/PlayerStats")]
    public class ScriptableStats : ScriptableObject
    {
        public float MaxSpeed = 5f;
        public float Acceleration = 20f;

        [FormerlySerializedAs("GroundDeceleration")]
        public float Deceleration = 30f;

        public float AirDeceleration = 10f;
        public float AirAcceleration = 10f;
        public float JumpPower = 10f;
        public float FallAcceleration = 30f;
        public float MaxFallSpeed = 20f;
        public float GrounderDistance = 0.1f;
        public LayerMask PlayerLayer;
        public float WallSlideSpeed = 2f;
        public int JumpFromWall = 60;
        public int MaxJumps = 2;
    }
}