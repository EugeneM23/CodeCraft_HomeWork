namespace Game.Scripts.Modules.PlayerController.Data
{
    [System.Serializable]
    public class PlayerStats
    {
        public float MaxSpeed;
        public float Acceleration;
        public float AirAcceleration;
        public float JumpPower;
        public float FallAcceleration;
        public float MaxFallSpeed;
        public float GrounderDistance;
        public float WallSlideSpeed;
        public int JumpFromWall;
        public int MaxJumps;
        public int LayerMask;
        public float Gravity = 50;
    }
}