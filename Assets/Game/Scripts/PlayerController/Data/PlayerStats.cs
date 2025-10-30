namespace Game.Scripts.PlayerController
{
    [System.Serializable]
    public class PlayerStats
    {
        public float MaxSpeed;
        public float Acceleration;
        public float Deceleration;
        public float AirDeceleration;
        public float AirAcceleration;
        public float JumpPower;
        public float FallAcceleration;
        public float MaxFallSpeed;
        public float GrounderDistance;
        public float WallSlideSpeed;
        public int JumpFromWall;
        public int MaxJumps;
        public int LayerMask;
    }
}