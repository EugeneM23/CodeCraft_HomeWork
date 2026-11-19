using UnityEngine;

namespace Game
{
    /// <summary>
    /// The debug panel's «Забег» tab: the arena flow through the run's own commands (clear the arena, open the gate,
    /// leave — one step per state, so «Пропустить арену» works from the fight), a defeat, the victory (the last
    /// arena cleared, the run ends after its victory delay), and the meta progress's marks.
    /// </summary>
    public static class RunDebugCommands
    {
        public const string CATEGORY = "Забег";
        private const int MARKS_PER_PRESS = 1000;
        private static readonly string[] ArenaButtons = { "Пропустить арену", "Поражение", "Победа" };
        private static readonly string[] MetaButtons = { "+1000 марок" };

        public static void Register()
        {
            DebugPanelRegistry.AddInfo(CATEGORY, ArenaLine);
            DebugPanelRegistry.AddButtons(CATEGORY, null, ArenaButtons, PressArena, 0, ArenaButtonText);
            DebugPanelRegistry.AddInfo(CATEGORY, MetaLine, 10);
            DebugPanelRegistry.AddButtons(CATEGORY, null, MetaButtons, _ => AddMarks(), 11);
        }

        private static string ArenaLine()
        {
            if (!SkillDebugCommands.TryGetRun(out IRunService run))
                return "Забега нет.";

            return $"Арена {run.ArenaNumber}/{run.ArenaCount} ({run.State}), сила врагов ×{run.EnemyStrength:0.00}, " +
                   $"время {run.TimeSec:0} с, убито {run.Kills}, марки забега {run.Marks}, образцы {run.Samples}.\n" +
                   "«Пропустить» в бою зачищает арену, дальше открывает ворота и проводит танк; «Победа» — на последней арене.";
        }

        private static string ArenaButtonText(int index)
        {
            if (!SkillDebugCommands.TryGetRun(out IRunService run) || !run.IsRunning)
                return null;

            return index == 2 && run.ArenaNumber < run.ArenaCount ? null : ArenaButtons[index];
        }

        private static void PressArena(int button)
        {
            if (!SkillDebugCommands.TryGetRun(out IRunService run))
                return;

            switch (button)
            {
                case 0:
                    if (run.State == RunState.Fight)
                        run.MarkArenaCleared();
                    else if (run.State == RunState.Cleared)
                        run.MarkGateOpened();
                    else if (run.State == RunState.GateOpen)
                        run.MarkArenaLeft();
                    else
                        Debug.Log($"Debug panel: the arena is {run.State}, nothing to skip yet");
                    break;
                case 1:
                    run.EndRun(RunOutcome.Defeat);
                    break;
                default:
                    if (run.ArenaNumber >= run.ArenaCount && run.State == RunState.Fight)
                        run.MarkArenaCleared();
                    break;
            }
        }

        private static string MetaLine()
        {
            if (!AppContext.TryGet(out AppContext app) || !app.TryGetMetaProgress(out IMetaProgress meta))
                return "Мета-прогресса нет (уровень без приложения).";

            return $"Мета: марки {meta.Marks}, образцы {meta.Samples}, танк {meta.SelectedTank}.";
        }

        private static void AddMarks()
        {
            if (AppContext.TryGet(out AppContext app) && app.TryGetProgress(out IPlayerProgress progress))
                progress.AddCredits(MARKS_PER_PRESS);
        }
    }
}
