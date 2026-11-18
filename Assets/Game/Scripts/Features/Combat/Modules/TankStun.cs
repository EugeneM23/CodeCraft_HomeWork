namespace Game
{
    /// <summary>Stun state of a tank: until when it can neither drive nor fire. Use <see cref="TankStunUseCase"/>.</summary>
    public sealed class TankStun
    {
        /// <summary>Game time (GameTicks.Time) the stun ends.</summary>
        public float Until;
    }
}
