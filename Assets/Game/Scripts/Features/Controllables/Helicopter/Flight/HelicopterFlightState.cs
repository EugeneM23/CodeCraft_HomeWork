namespace Game
{
    public enum HelicopterFlightState
    {
        /// <summary>On the ground, gravity on, nothing driven.</summary>
        Landed,
        /// <summary>Climbing to cruise height; already steerable.</summary>
        TakingOff,
        /// <summary>At cruise height, flown by the stick.</summary>
        Flying,
        /// <summary>Stick ignored, slowing down and coming down; the pilot gets out on touchdown.</summary>
        Landing
    }
}
