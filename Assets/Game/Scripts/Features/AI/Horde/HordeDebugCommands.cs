using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The horde's lines on the debug panel's «Враги» tab (shown only in a level with a horde: the arenas): a wave far
    /// from the tank as the director releases them, ten crawlers right in front of it, and kill all. An example of a
    /// feature adding its own entries from its own assembly.
    /// </summary>
    public static class HordeDebugCommands
    {
        private const int WAVE = 30;
        private const int CLOSE = 10;
        private static readonly string[] Buttons = { "Волна ×30", "Ползуны ×10 рядом", "Убить орду" };

        private static bool s_registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_registered = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            if (s_registered)
                return;

            s_registered = true;
            DebugEntry info = DebugPanelRegistry.AddInfo(WorldDebugCommands.ENEMIES,
                () => TryGetHorde(out IArenaHorde horde) ? $"Орда: живых {horde.AliveCount}." : null, 0);
            info.Visible = () => TryGetHorde(out _);
            DebugEntry buttons = DebugPanelRegistry.AddButtons(WorldDebugCommands.ENEMIES, null, Buttons, Press, 1);
            buttons.Visible = () => TryGetHorde(out _);
        }

        private static void Press(int button)
        {
            if (!TryGetHorde(out IArenaHorde horde))
                return;

            switch (button)
            {
                case 0:
                    (horde as HordeBehaviour)?.SpawnWave(WAVE);
                    break;
                case 1:
                    SpawnClose(horde);
                    break;
                default:
                    (horde as HordeBehaviour)?.KillAll();
                    break;
            }
        }

        private static void SpawnClose(IArenaHorde horde)
        {
            if (!DebugLookup.TryGetTank(out IEntity tank))
                return;

            Transform root = DebugLookup.RootOf(tank);
            if (root == null)
                return;

            Vector3 forward = root.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            for (int i = 0; i < CLOSE; i++)
            {
                Vector3 at = root.position + forward * 12f + right * ((i - (CLOSE - 1) * 0.5f) * 1.5f);
                horde.Spawn(EnemyKind.Crawler, DebugLookup.Ground(at), 1f, 1f);
            }
        }

        private static bool TryGetHorde(out IArenaHorde horde)
        {
            horde = null;
            IEntity level = LevelAI.Current;
            return level != null && level.TryGetLevelHorde(out horde) && horde != null;
        }
    }
}
