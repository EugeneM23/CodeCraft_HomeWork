using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Stuns a tank (the elite's leap): while stunned it neither drives nor fires and its turret stands. A new stun
    /// extends the current one to the later end, it does not add up.
    /// </summary>
    public static class TankStunUseCase
    {
        public static void Stun(IEntity tank, float seconds)
        {
            if (tank == null || seconds <= 0f)
                return;

            if (!tank.TryGetTankStun(out TankStun stun))
            {
                stun = new TankStun();
                tank.AddTankStun(stun);
            }

            float until = GameTicks.Time + seconds;
            if (until > stun.Until)
                stun.Until = until;

            CombatSignals.RaiseTankStunned(seconds);
        }

        public static bool IsStunned(IEntity tank) =>
            tank != null && tank.TryGetTankStun(out TankStun stun) && GameTicks.Time < stun.Until;
    }
}
