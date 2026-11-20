using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// The battle screens of milestone 1 (HUD, skill choice, pause, run results) and the run skills' texts.
    /// Skills by the core's SkillIds: skill.&lt;id&gt;.name, .glyph (two letters on the placeholder icon), .desc
    /// (level 1, GDD §3), .desc_up (each next level).
    /// </summary>
    public static partial class UiStringTable
    {
        static partial void AddRunRussian(Dictionary<string, string> table)
        {
            // ---------- HUD ----------
            table["run.hud_health"] = "Здоровье";
            table["run.hud_arena_format"] = "Арена {0:0}/{1:0}"; // TMP
            table["run.hud_time_format"] = "{0:00}:{1:00}"; // TMP
            table["run.hud_level_format"] = "Ур. {0:0}"; // TMP
            table["run.hud_xp_format"] = "{0:0}/{1:0}"; // TMP
            table["run.hud_marks_format"] = "Марки {0:0}"; // TMP
            table["run.hud_samples_format"] = "Образцы {0:0}"; // TMP
            table["run.hud_crush_format"] = "Давка ×{0:0}"; // TMP
            table["run.hud_elite"] = "ЭЛИТНЫЙ REGAX";
            table["run.hud_special"] = "СПЕЦ";
            table["run.hud_special_key"] = "E";
            table["run.hud_gate"] = "Ворота";
            table["run.hud_hint_crush"] = "Дави мелких — наезжай на них";
            table["run.hud_hint_destroy_cover"] = "Снеси укрытие — Плевуны прячутся за домами";
            table["run.hud_hints_format"] =
                "{0} — Пушка\n{1} — Пулемёт\n{2} — Форсаж\n{3} — Езда\n{4} — Спецмодуль\n{5} — Пауза\n{6} — скрыть";

            // ---------- Pause ----------
            table["run.pause_title"] = "ПАУЗА";
            table["run.pause_resume"] = "Продолжить";
            table["run.pause_settings"] = "Настройки";
            table["run.pause_exit"] = "Выйти в ангар";
            table["run.pause_exit_title"] = "Выйти в ангар?";
            table["run.pause_exit_text"] = "Забег окончится, собранное сохранится.";
            table["run.pause_exit_yes"] = "Выйти";
            table["run.pause_skills"] = "НАВЫКИ ЗАБЕГА";
            table["run.pause_no_skills"] = "Навыков пока нет";

            // ---------- Skill choice ----------
            table["run.choice_title"] = "НОВЫЙ УРОВЕНЬ";
            table["run.choice_hint"] = "Выберите навык: щелчок или клавиши 1, 2, 3";
            table["run.choice_new"] = "новый";
            table["run.choice_level_format"] = "{0} → {1}";
            table["run.choice_key_format"] = "[{0}]";
            table["run.skill_weapon"] = "Орудие";
            table["run.skill_upgrade"] = "Улучшение";

            // ---------- Run results ----------
            table["run.results_victory"] = "ПОБЕДА";
            table["run.results_defeat"] = "ТАНК УНИЧТОЖЕН";
            table["run.results_abandoned"] = "ЗАБЕГ ПРЕРВАН";
            table["run.results_arena_format"] = "Арена {0:0}/{1:0}"; // TMP
            table["run.results_time"] = "Время";
            table["run.results_kills"] = "Убито";
            table["run.results_crushes"] = "Раздавлено";
            table["run.results_elites"] = "Элита";
            table["run.results_marks"] = "Марки";
            table["run.results_samples"] = "Образцы";
            table["run.results_record"] = "НОВЫЙ РЕКОРД";
            table["run.results_to_hangar"] = "В ангар";
            table["run.results_again"] = "Ещё раз";

            // ---------- Run skills (GDD §3, milestone 1) ----------
            table["skill.turret.name"] = "Пулемётная турель";
            table["skill.turret.glyph"] = "Тр";
            table["skill.turret.desc"] = "Вторая турель: 8 урона каждые 0.12 с";
            table["skill.turret.desc_up"] = "Турель сильнее; на 3-м и 5-м уровне — ещё одна турель";

            table["skill.air_strike.name"] = "Авиаудар";
            table["skill.air_strike.glyph"] = "Ав";
            table["skill.air_strike.desc"] = "Раз в 10 с бомба по самой густой толпе: воронка 4 м, 400 урона";
            table["skill.air_strike.desc_up"] = "Удар на 1 с чаще; на 5-м уровне — 2 бомбы";

            table["skill.ricochet.name"] = "Рикошет";
            table["skill.ricochet.glyph"] = "Рк";
            table["skill.ricochet.desc"] = "Снаряд пушки отскакивает 1 раз";
            table["skill.ricochet.desc_up"] = "+1 отскок снаряда";

            table["skill.rate_of_fire.name"] = "Скорострельность";
            table["skill.rate_of_fire.glyph"] = "Ск";
            table["skill.rate_of_fire.desc"] = "Перезарядка пушки −12 %";
            table["skill.rate_of_fire.desc_up"] = "Перезарядка пушки ещё −8 %";

            table["skill.armor.name"] = "Броня";
            table["skill.armor.glyph"] = "Бр";
            table["skill.armor.desc"] = "+15 % здоровья, лечит на прибавку";
            table["skill.armor.desc_up"] = "Ещё +15 % здоровья, лечит на прибавку";

            table["skill.magnet.name"] = "Магнит";
            table["skill.magnet.glyph"] = "Мг";
            table["skill.magnet.desc"] = "Радиус сбора опыта +50 %";
            table["skill.magnet.desc_up"] = "Радиус сбора опыта ещё +30 %";

            table["skill.high_explosive.name"] = "Мощный фугас";
            table["skill.high_explosive.glyph"] = "Фг";
            table["skill.high_explosive.desc"] = "Кровь, ошмётки и обломки от фугаса летят на 30 % дальше, воронка в домах на 30 % больше";
            table["skill.high_explosive.desc_up"] = "Фугас ещё на 30 % сильнее";
        }
    }
}
