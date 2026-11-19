using System;
using System.Collections.Generic;
using System.Text;

namespace Game
{
    /// <summary>
    /// The debug panel's «Навыки» tab: every skill of the game catalog with its level in the current run and
    /// −1 / +1 / max / 0, applied through <see cref="IRunService.SetSkillLevel"/> so the tank's modifiers change exactly as
    /// after a pick; a level-up (the real offer: the skill screen, or the run's own pick without one), the open offer's
    /// cards, experience and «reset all». The rows are added once the catalog is there (the first open of the panel).
    /// </summary>
    public static class SkillDebugCommands
    {
        public const string CATEGORY = "Навыки";
        private static readonly string[] LevelButtons = { "−1", "+1", "Макс", "0" };
        private static readonly string[] RunButtons = { "Уровень +1", "+10 опыта", "Сбросить все" };
        private static readonly string[] OfferButtons = { "", "", "" };

        private static bool s_skillsAdded;

        public static void Register()
        {
            DebugPanelRegistry.AddInfo(CATEGORY, RunLine, -10);
            DebugPanelRegistry.AddButtons(CATEGORY, null, RunButtons, PressRun, -9);
            DebugPanelRegistry.AddButtons(CATEGORY, () => "Предложение (выбрать):", OfferButtons, PressOffer, -8, OfferText)
                .Visible = () => TryGetRun(out IRunService run) && run.SkillOffer.Count > 0;
        }

        /// <summary>The per-skill rows, once: needs the game catalog (the app).</summary>
        public static void EnsureSkillRows()
        {
            if (s_skillsAdded || !AppContext.TryGet(out AppContext app) || !app.TryGetGameCatalog(out GameCatalog catalog))
                return;

            s_skillsAdded = true;
            var known = new HashSet<SkillId>(SkillEffectTable.MilestoneSkills);
            IReadOnlyList<SkillConfig> skills = catalog.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                SkillConfig config = skills[i];
                if (config == null || config.Id.IsNone)
                    continue;

                SkillId id = config.Id;
                string name = string.IsNullOrEmpty(config.DisplayName) ? id.Value : config.DisplayName;
                string note = known.Contains(id) ? "" : config.InPool ? "  <color=#9a948c>(без эффекта)</color>"
                    : "  <color=#9a948c>(не в игре)</color>";
                int order = known.Contains(id) ? 0 : 1;
                DebugPanelRegistry.AddButtons(CATEGORY, () => SkillLine(id, name, note), LevelButtons,
                    b => PressLevel(id, b), order);
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_skillsAdded = false;

        // ---------- Rows ----------

        private static string RunLine()
        {
            if (!TryGetRun(out IRunService run))
                return "Забега нет: уровень без RunBootstrap или ещё грузится.";

            var line = new StringBuilder();
            line.Append("Забег: ").Append(run.State).Append(", арена ").Append(run.ArenaNumber).Append('/').Append(run.ArenaCount)
                .Append(", уровень ").Append(run.Level).Append(", опыт ").Append(run.Experience).Append('/')
                .Append(run.ExperienceToNextLevel);
            if (run.PendingLevelUps > 0)
                line.Append(", ждут выбора ").Append(run.PendingLevelUps);
            line.Append(". Навыки: ");
            IReadOnlyList<SkillId> skills = run.Skills;
            if (skills.Count == 0)
                line.Append("нет");
            for (int i = 0; i < skills.Count; i++)
                line.Append(i > 0 ? ", " : "").Append(NameOf(skills[i])).Append(' ').Append(run.GetSkillLevel(skills[i]));
            return line.ToString();
        }

        private static string SkillLine(SkillId id, string name, string note)
        {
            int level = TryGetRun(out IRunService run) ? run.GetSkillLevel(id) : 0;
            string color = level > 0 ? "#ff8c1f" : "#9a948c";
            return $"{name}  <color={color}>{level}/{SkillConfig.MAX_LEVEL}</color>{note}";
        }

        private static string OfferText(int index)
        {
            if (!TryGetRun(out IRunService run) || index >= run.SkillOffer.Count)
                return null;

            SkillId skill = run.SkillOffer[index];
            return $"{NameOf(skill)} → {run.GetSkillLevel(skill) + 1}";
        }

        // ---------- Presses ----------

        private static void PressLevel(SkillId id, int button)
        {
            if (!TryGetRun(out IRunService run))
                return;

            int level = run.GetSkillLevel(id);
            int target = button switch
            {
                0 => level - 1,
                1 => level + 1,
                2 => SkillConfig.MAX_LEVEL,
                _ => 0
            };
            run.SetSkillLevel(id, Math.Clamp(target, 0, SkillConfig.MAX_LEVEL));
        }

        private static void PressRun(int button)
        {
            if (!TryGetRun(out IRunService run))
                return;

            switch (button)
            {
                case 0:
                    run.AddExperience(Math.Max(1, run.ExperienceToNextLevel - run.Experience));
                    break;
                case 1:
                    run.AddExperience(10);
                    break;
                default:
                    var taken = new List<SkillId>(run.Skills);
                    for (int i = 0; i < taken.Count; i++)
                        run.SetSkillLevel(taken[i], 0);
                    break;
            }
        }

        private static void PressOffer(int index)
        {
            if (TryGetRun(out IRunService run) && index < run.SkillOffer.Count)
                run.ChooseSkill(run.SkillOffer[index]);
        }

        // ---------- Lookups ----------

        public static bool TryGetRun(out IRunService run)
        {
            run = null;
            return GameContext.TryGetInstance(out GameContext context) && context.TryGetRun(out run) && run != null;
        }

        private static string NameOf(SkillId id)
        {
            if (AppContext.TryGet(out AppContext app) && app.TryGetGameCatalog(out GameCatalog catalog))
            {
                SkillConfig config = catalog.FindSkill(id);
                if (config != null && !string.IsNullOrEmpty(config.DisplayName))
                    return config.DisplayName;
            }

            return id.Value;
        }
    }
}
