using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Combat events for sound, HUD and AI. Static like <see cref="GameSignals"/>; subscribers unsubscribe on
    /// dispose. Skill changes come from the run (IRunService.SkillChanged), not from here.
    /// </summary>
    public static class CombatSignals
    {
        /// <summary>The cannon fired: muzzle point, game time (GameTicks.Time). The elite hears it.</summary>
        public static event Action<Vector3, float> CannonFired;

        /// <summary>The tank was stunned for this many seconds.</summary>
        public static event Action<float> TankStunned;

        /// <summary>The tank picked up experience: point, amount.</summary>
        public static event Action<Vector3, int> XpPicked;

        /// <summary>Smoke launchers fired: centre, radius.</summary>
        public static event Action<Vector3, float> SmokeDeployed;

        public static void RaiseCannonFired(Vector3 muzzle, float time) => CannonFired?.Invoke(muzzle, time);
        public static void RaiseTankStunned(float seconds) => TankStunned?.Invoke(seconds);
        public static void RaiseXpPicked(Vector3 point, int amount) => XpPicked?.Invoke(point, amount);
        public static void RaiseSmokeDeployed(Vector3 center, float radius) => SmokeDeployed?.Invoke(center, radius);
    }
}
