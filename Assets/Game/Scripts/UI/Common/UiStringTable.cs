using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// All interface texts: one dictionary per UiLanguage, key to text, grouped by screen. Keys are dotted
    /// lowercase (screen.what); a key ending in _format has {0}, {1}… placeholders. Formats marked "TMP" go
    /// to TMP_Text.SetText only ({0:1} there means one decimal); the others go to string.Format.
    /// A new language: a dictionary with the same keys and a case in For; screens stay as they are.
    /// </summary>
    public static class UiStringTable
    {
        public static readonly IReadOnlyDictionary<string, string> Russian = new Dictionary<string, string>
        {
            // ---------- Common ----------
            ["game.title"] = "ВОКСЕЛЬНЫЕ ТАНКИ",
            ["common.back"] = "Назад",
            ["common.cancel"] = "Отмена",
            ["common.yes"] = "Да",
            ["common.no"] = "Нет",
            ["common.credits_format"] = "Кредиты {0}", // TMP

            // ---------- Key names (KeyNameUseCase) ----------
            ["key.lmb"] = "ЛКМ",
            ["key.rmb"] = "ПКМ",
            ["key.mmb"] = "СКМ",

            // ---------- Tank slots and stats (hangar, shop) ----------
            ["slot.gun"] = "Пушка",
            ["slot.machine_gun"] = "Пулемёт",
            ["slot.hull"] = "Корпус",
            ["slot.engine"] = "Двигатель",
            ["slot.tracks"] = "Гусеницы",
            ["slot.armor"] = "Броня",
            ["stat.health"] = "Прочность",
            ["stat.armor"] = "Броня",
            ["stat.speed"] = "Скорость",
            ["stat.damage"] = "Урон",
            ["stat.reload"] = "Перезарядка",
            ["stat.fire_rate"] = "Темп МГ",
            ["stat.reload_seconds"] = "Перезарядка (с)",
            ["stat.fire_rate_per_second"] = "Темп МГ (/с)",
            ["stat.unit_seconds"] = " с",
            ["stat.unit_per_second"] = "/с",

            // ---------- Level goals (level select, results) ----------
            ["goal.kill_format"] = "Цель: уничтожить {0} Regax", // TMP and string.Format
            ["goal.survive"] = "Цель: выжить",

            // ---------- Main menu ----------
            ["main.title"] = "ГЛАВНОЕ МЕНЮ",
            ["main.play"] = "ИГРАТЬ",
            ["main.hangar"] = "Ангар",
            ["main.shop"] = "Магазин",
            ["main.settings"] = "Настройки",
            ["main.quit"] = "Выход",
            ["main.version_format"] = "Версия {0}",
            ["main.quit_title"] = "Выход",
            ["main.quit_text"] = "Выйти из игры?",
            ["main.quit_yes"] = "Выйти",

            // ---------- Level select ----------
            ["level.title"] = "ВЫБОР УРОВНЯ",
            ["level.difficulty_format"] = "Сложность {0}/{1}",
            ["level.card_reward_format"] = "Награда: {0}", // TMP
            ["level.card_locked"] = "ЗАКРЫТ",
            ["level.reward_format"] = "Награда: {0} кредитов", // TMP
            ["level.unlock_after_format"] = "Откроется после победы на уровне «{0}»",
            ["level.locked"] = "Уровень закрыт",
            ["level.to_hangar"] = "В ангар",
            ["level.start"] = "НАЧАТЬ",

            // ---------- Hangar ----------
            ["hangar.title"] = "АНГАР",
            ["hangar.shop"] = "Магазин",
            ["hangar.slots"] = "СЛОТЫ",
            ["hangar.stats"] = "ХАРАКТЕРИСТИКИ",
            ["hangar.tank_placeholder"] = "3D-танк — сцена ангара на URP позже",
            ["hangar.slot_modules_format"] = "МОДУЛИ СЛОТА «{0}»",
            ["hangar.slot_value_format"] = "{0}  ур. {1}",
            ["hangar.module_installed"] = "установлен",
            ["hangar.module_owned_format"] = "куплен, ур. {0}", // TMP
            ["hangar.module_in_shop_format"] = "в магазине — {0}", // TMP
            ["hangar.no_modules"] = "Нет модулей",
            ["hangar.level_format"] = "ур. {0}/{1}", // TMP
            ["hangar.not_owned"] = "не куплен",
            ["hangar.to_shop"] = "В МАГАЗИН",
            ["hangar.install"] = "УСТАНОВИТЬ",
            ["hangar.installed"] = "УСТАНОВЛЕН",
            ["hangar.upgrade_format"] = "УЛУЧШИТЬ — {0}", // TMP and string.Format

            // ---------- Shop ----------
            ["shop.title"] = "МАГАЗИН",
            ["shop.tab_gun"] = "Пушки",
            ["shop.tab_machine_gun"] = "Пулемёты",
            ["shop.tab_hull"] = "Корпуса",
            ["shop.tab_engine"] = "Двигатели",
            ["shop.tab_tracks"] = "Гусеницы",
            ["shop.tab_armor"] = "Броня",
            ["shop.card_price_format"] = "{0} кр.", // TMP
            ["shop.card_buy"] = "Купить",
            ["shop.card_bought"] = "Куплено",
            ["shop.empty"] = "Нет товаров",
            ["shop.level_header_format"] = "Уровень {0}:",
            ["shop.no_bonuses"] = "Без прибавок",
            ["shop.price_format"] = "Цена: {0} кредитов", // TMP
            ["shop.buy"] = "КУПИТЬ",
            ["shop.bought"] = "КУПЛЕНО",
            ["shop.buy_title"] = "Покупка",
            ["shop.buy_text_format"] = "Купить «{0}» за {1}?\nОстанется {2} кредитов.",
            ["shop.buy_yes"] = "Купить",

            // ---------- Settings ----------
            ["settings.title"] = "НАСТРОЙКИ",
            ["settings.tab_graphics"] = "Графика",
            ["settings.tab_audio"] = "Звук",
            ["settings.tab_controls"] = "Управление",
            ["settings.defaults"] = "По умолчанию",
            ["settings.apply"] = "Применить",
            ["settings.leave_title"] = "Применить изменения?",
            ["settings.leave_text"] = "Изменения настроек ещё не применены.",
            ["settings.leave_yes"] = "Применить",
            ["settings.leave_no"] = "Отменить",
            ["settings.defaults_title"] = "Сбросить настройки?",
            ["settings.defaults_text"] = "Графика, звук и клавиши вернутся к исходным. Потом нажмите «Применить».",
            ["settings.defaults_yes"] = "Сбросить",
            // Graphics
            ["settings.resolution"] = "Разрешение",
            ["settings.window_mode"] = "Режим окна",
            ["settings.window_fullscreen"] = "Полный экран",
            ["settings.window_borderless"] = "Окно без рамки",
            ["settings.window_windowed"] = "Окно",
            ["settings.quality"] = "Качество",
            ["settings.vsync"] = "Вертикальная синхронизация",
            ["settings.vsync_on"] = "Вкл.",
            ["settings.frame_limit"] = "Ограничение FPS",
            ["settings.frame_limit_none"] = "Нет",
            // Audio, in the AudioChannel order
            ["settings.volume_master"] = "Общая",
            ["settings.volume_music"] = "Музыка",
            ["settings.volume_effects"] = "Эффекты",
            ["settings.volume_ui"] = "Интерфейс",
            ["settings.volume_ambient"] = "Окружение",
            // Controls
            ["settings.mouse_sensitivity"] = "Чувствительность мыши",
            ["settings.reset_all"] = "Сбросить всё",
            ["settings.reset_key"] = "Сброс",
            ["settings.rebind_wait_format"] = "Нажмите клавишу для «{0}»…\nEsc — отмена",
            ["settings.sandbox_format"] = "{0} (песочница)",
            ["settings.action_forward"] = "Вперёд",
            ["settings.action_back"] = "Назад",
            ["settings.action_left"] = "Влево",
            ["settings.action_right"] = "Вправо",
            ["settings.action_boost"] = "Форсаж",
            ["settings.action_fire_main"] = "Пушка",
            ["settings.action_fire_secondary"] = "Пулемёт",
            ["settings.action_hints"] = "Подсказки",
            ["settings.action_air_strike"] = "Авиаудар",
            ["settings.action_smoke"] = "Дым",
            ["settings.action_rain"] = "Дождь",

            // ---------- Battle HUD ----------
            ["hud.health"] = "ПРОЧНОСТЬ",
            ["hud.armor"] = "БРОНЯ",
            ["hud.gun"] = "ПУШКА",
            ["hud.machine_gun"] = "МГ",
            ["hud.boost"] = "ФОРСАЖ",
            ["hud.ready"] = "ГОТОВО",
            ["hud.reload_seconds_format"] = "{0:1} с", // TMP
            ["hud.kills_target_format"] = "Regax: {0:0}/{1:0}", // TMP
            ["hud.kills_format"] = "Regax: {0:0}", // TMP
            ["hud.time_format"] = "Время {0:00}:{1:00}", // TMP
            ["hud.hints_format"] = "{0} — Пушка\n{1} — Пулемёт\n{2} — Форсаж\n{3} — Езда\n{4} — Пауза\n{5} — скрыть",
            ["hud.debug_fps_format"] = "FPS {0:0}   кадр {1:1} мс", // TMP
            ["hud.debug_voxels_format"] = "Воксели: {0:0} в {1:0} телах ({2:0} кусков)", // TMP
            ["hud.debug_debris_format"] = "Обломки: {0:0}", // TMP
            ["hud.debug_remesh_format"] = "Перестройка: {0:0} чанков за {1:1} мс ({2:0} в работе)", // TMP
            ["hud.debug_smoke_format"] = "Дым: {0:0} облаков, {1:0} кубов", // TMP

            // ---------- Pause and battle questions ----------
            ["pause.title"] = "ПАУЗА",
            ["pause.resume"] = "Продолжить",
            ["pause.settings"] = "Настройки",
            ["pause.restart"] = "Заново",
            ["pause.menu"] = "В меню",
            ["pause.quit"] = "Выйти из игры",
            ["pause.progress_lost"] = "Прогресс этого боя будет потерян.",
            ["pause.restart_title"] = "Начать заново?",
            ["pause.restart_yes"] = "Заново",
            ["pause.menu_title"] = "Выйти в меню?",
            ["pause.menu_yes"] = "В меню",
            ["pause.quit_title"] = "Выйти из игры?",
            ["pause.quit_yes"] = "Выйти",

            // ---------- Battle results ----------
            ["results.victory"] = "ПОБЕДА",
            ["results.defeat"] = "ПОРАЖЕНИЕ",
            ["results.goal_with_level_format"] = "{0}. {1}",
            ["results.time_format"] = "Время: {0}",
            ["results.kills_target_format"] = "Уничтожено Regax: {0}/{1}",
            ["results.kills_format"] = "Уничтожено Regax: {0}",
            ["results.reward_format"] = "+{0} кредитов",
            ["results.no_reward"] = "Награды нет",
            ["results.again"] = "Ещё раз",
            ["results.to_hangar"] = "В ангар",
            ["results.next"] = "Следующий уровень",
        };

        /// <summary>The table of the language; Russian for any language without one yet.</summary>
        public static IReadOnlyDictionary<string, string> For(UiLanguage language)
        {
            switch (language)
            {
                case UiLanguage.Russian:
                default:
                    return Russian;
            }
        }
    }
}
