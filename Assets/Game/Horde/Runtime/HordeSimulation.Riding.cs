using Unity.Mathematics;

namespace Game.Horde
{
    /// <summary>
    /// Riders: monsters that left the ground for the tank (<see cref="HordeState.Riding"/>). Their owner (the game's
    /// <c>HordeRiders</c>) places them every frame between <see cref="Complete"/> and <see cref="Schedule"/>; the step
    /// only lands hits and blasts on them and reports their deaths. Main thread, while no step runs.
    /// </summary>
    public sealed partial class HordeSimulation
    {
        /// <summary>
        /// A walking or attacking monster leaves the ground for the tank: true if it did (not an emerging, riding or dead
        /// one). It stands where it is until <see cref="SetRidePose"/> moves it.
        /// </summary>
        public bool BeginRide(int index)
        {
            ThrowIfRunning();
            if (index < 0 || index >= Capacity)
                return false;
            HordeState state = _state[index];
            if (state != HordeState.Walk && state != HordeState.Attack)
                return false;
            _state[index] = HordeState.Riding;
            _velocity[index] = float2.zero;
            _attackDamage[index] = 0f;
            return true;
        }

        /// <summary>Puts a rider's feet at <paramref name="feet"/> facing <paramref name="heading"/> (radians, 0 faces +Z); the target queries see it there at once.</summary>
        public void SetRidePose(int index, float3 feet, float heading)
        {
            ThrowIfRunning();
            if (index < 0 || index >= Capacity || _state[index] != HordeState.Riding)
                return;
            _position[index] = feet;
            _nextPosition[index] = feet;
            _snapPosition[index] = feet;
            _heading[index] = heading;
        }

        /// <summary>A rider is back on the ground at <paramref name="feet"/> and walks again. False if the slot holds no rider.</summary>
        public bool EndRide(int index, float3 feet, float heading)
        {
            ThrowIfRunning();
            if (index < 0 || index >= Capacity || _state[index] != HordeState.Riding)
                return false;
            _state[index] = HordeState.Walk;
            _position[index] = feet;
            _nextPosition[index] = feet;
            _snapPosition[index] = feet;
            _heading[index] = heading;
            _velocity[index] = float2.zero;
            return true;
        }

        /// <summary>Seconds into its clips the monster is (the step advances it while it rides too).</summary>
        public float GetAnimTime(int index) => _animTime[index];

        /// <summary>The monster's damage scale (the spawn's).</summary>
        public float GetDamageScale(int index) => _damageScale[index];
    }
}
