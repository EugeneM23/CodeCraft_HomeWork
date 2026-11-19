using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The debug panel's «Мир» tab: time scale, an air strike at the aim point (the air strike skill's own bomb and
    /// damage), the HUD hidden, the FPS corner, and the level's entity enemies (spawn zombies in front of the tank,
    /// kill all). The horde of an arena adds its own buttons from its assembly (HordeDebugCommands).
    /// </summary>
    public static class WorldDebugCommands
    {
        public const string CATEGORY = "Мир";
        public const string ENEMIES = "Враги";
        private const int ZOMBIES_PER_PRESS = 5;
        private static readonly string[] TimeButtons = { "×0.25", "×0.5", "×1", "×2" };
        private static readonly float[] TimeScales = { 0.25f, 0.5f, 1f, 2f };
        private static readonly string[] StrikeButtons = { "Авиаудар по прицелу" };
        private static readonly string[] EnemyButtons = { "Зомби ×5 перед танком", "Убить всех врагов" };
        private static readonly List<IEntity> s_enemies = new();

        private static bool s_hudHidden;
        private static CanvasGroup s_hud;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_hudHidden = false;
            s_hud = null;
        }

        public static void Register()
        {
            DebugPanelRegistry.AddButtons(CATEGORY, () => $"Время ×{Time.timeScale:0.##}", TimeButtons,
                i => Time.timeScale = TimeScales[i]);
            DebugPanelRegistry.AddButtons(CATEGORY, null, StrikeButtons, _ => AirStrike());
            DebugPanelRegistry.AddToggle(CATEGORY, "Скрыть HUD", () => s_hudHidden, SetHudHidden);
            DebugPanelRegistry.AddToggle(CATEGORY, "FPS в углу (при закрытой панели)", () => DebugPanel.ShowFpsCorner,
                v => DebugPanel.ShowFpsCorner = v);

            DebugPanelRegistry.AddInfo(ENEMIES, EnemiesLine, -10);
            DebugPanelRegistry.AddButtons(ENEMIES, null, EnemyButtons, PressEnemies, -5);
            DebugPanelRegistry.Ticked += Tick;
        }

        // ---------- Air strike ----------

        private static void AirStrike()
        {
            VoxelAirStrike strike = Object.FindFirstObjectByType<VoxelAirStrike>();
            if (strike == null)
            {
                Debug.LogWarning("Debug panel: no VoxelAirStrike in the level");
                return;
            }

            if (!DebugLookup.TryGetAimPoint(out Vector3 point))
                return;

            WeaponSettings settings = CombatConfig.Load().Weapons;
            strike.Strike(point, settings.airStrikeDamage, settings.airStrikeRadius, settings.airStrikeMinFactor);
        }

        // ---------- HUD ----------

        private static void SetHudHidden(bool hidden)
        {
            s_hudHidden = hidden;
            if (!hidden && s_hud != null)
                s_hud.alpha = 1f;
            Tick();
        }

        // The HUD screen's fade may set its alpha back when the stack shows it: held at 0 while hidden
        private static void Tick()
        {
            if (!s_hudHidden)
                return;

            if (s_hud == null)
            {
                HudScreen hud = Object.FindFirstObjectByType<HudScreen>();
                if (hud == null || !hud.TryGetComponent(out s_hud))
                    return;
            }

            if (s_hud.alpha != 0f)
                s_hud.alpha = 0f;
        }

        // ---------- Enemies ----------

        private static string EnemiesLine()
        {
            int count = GameContext.TryGetInstance(out GameContext context) && context.TryGetEnemyFilter(out EntityFilter filter)
                ? filter.Count
                : 0;
            return $"Врагов-сущностей в уровне: {count}. Зомби песочницы стоят без AI.";
        }

        private static void PressEnemies(int button)
        {
            if (button == 0)
                SpawnZombies();
            else
                KillEntityEnemies();
        }

        private static void SpawnZombies()
        {
            GameObject prefab = DebugPanel.ZombiePrefab;
            if (prefab == null || !DebugLookup.TryGetTank(out IEntity tank))
                return;

            Transform root = DebugLookup.RootOf(tank);
            if (root == null)
                return;

            Vector3 forward = root.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < ZOMBIES_PER_PRESS; i++)
            {
                Vector3 at = root.position + forward * 14f + right * ((i - (ZOMBIES_PER_PRESS - 1) * 0.5f) * 2.5f);
                at = DebugLookup.Ground(at);
                Quaternion facing = Quaternion.LookRotation(-forward);
                Object.Instantiate(prefab, at, facing);
            }
        }

        // A copy first: a dying enemy leaves the filter
        private static void KillEntityEnemies()
        {
            if (!GameContext.TryGetInstance(out GameContext context) || !context.TryGetEnemyFilter(out EntityFilter filter))
                return;

            s_enemies.Clear();
            foreach (IEntity enemy in filter)
                s_enemies.Add(enemy);

            for (int i = 0; i < s_enemies.Count; i++)
            {
                if (s_enemies[i].TryGetHealth(out Health health) && health.GetCurrent() > 0)
                    health.Reduce(health.GetCurrent());
            }

            s_enemies.Clear();
        }
    }
}
