using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// Texts of the menus of milestone 1: main menu, hangar, slot assembly, upgrade, shop, chapter select, and the
    /// names of slots, modules, chapters and tanks by id (HangarLook). Keys of the level-based menus in
    /// UiStringTable.cs that these overwrite (slot.*, hangar.*) get the new texts here.
    /// </summary>
    public static partial class UiStringTable
    {
        static partial void AddHangarRussian(Dictionary<string, string> table)
        {
            // ---------- Slots (HangarLook keys by ModuleSlot) ----------
            table["slot.gun"] = "Орудие";
            table["slot.machine_gun"] = "Пулемёт";
            table["slot.armor"] = "Броня";
            table["slot.engine"] = "Двигатель";
            table["slot.tracks"] = "Ходовая";
            table["slot.special"] = "Спецмодуль";
            table["slot.gun.glyph"] = "ОР";
            table["slot.machine_gun.glyph"] = "ПЛ";
            table["slot.armor.glyph"] = "БР";
            table["slot.engine.glyph"] = "ДВ";
            table["slot.tracks.glyph"] = "ХД";
            table["slot.special.glyph"] = "E";

            // ---------- Modules (ModuleIds) ----------
            table["module.gun_88_he.name"] = "88-мм фугасная";
            table["module.gun_88_he.desc"] = "Штатное орудие «Тигра»: фугас с радиусом 5 м, оставляет воронку.";
            table["module.gun_ap.name"] = "Бронебойная пушка";
            table["module.gun_ap.desc"] = "250 урона, пробивает 3 цели, взрыв 2 м, перезарядка 1.6 с.";
            table["module.gun_howitzer.name"] = "Гаубица";
            table["module.gun_howitzer.desc"] = "Стреляет навесом: 300 урона, радиус 8 м, двойная воронка, перезарядка 3 с.";
            table["module.gun_howitzer.lock"] = "Откроется после вехи 1";
            table["module.mg_standard.name"] = "Пулемёт MG";
            table["module.mg_standard.desc"] = "Стартовое автооружие: 10 урона каждые 0.08 с по ближайшей цели.";
            table["module.mg_twin.name"] = "Спаренный пулемёт";
            table["module.mg_twin.desc"] = "Два ствола, разброс 4°: вдвое плотнее огонь.";
            table["module.mg_twin.lock"] = "Пройди элиту главы 1";
            table["module.armor_standard.name"] = "Штатная броня";
            table["module.armor_standard.desc"] = "Без прибавок. Уровни добавляют здоровье.";
            table["module.armor_skirts.name"] = "Экраны";
            table["module.armor_skirts.desc"] = "Навесные экраны: +40 % здоровья, −8 % скорости.";
            table["module.armor_dozer.name"] = "Таранный отвал";
            table["module.armor_dozer.desc"] = "Спереди −40 % урона, давка с 0.5 м/с, таран ×2.";
            table["module.armor_dozer.lock"] = "Откроется после вехи 1";
            table["module.engine_standard.name"] = "Штатный двигатель";
            table["module.engine_standard.desc"] = "Уровни добавляют скорость и форсаж.";
            table["module.engine_boosted.name"] = "Форсированный двигатель";
            table["module.engine_boosted.desc"] = "+20 % скорости и форсажа.";
            table["module.engine_boosted.lock"] = "Откроется после вехи 1";
            table["module.tracks_standard.name"] = "Штатная ходовая";
            table["module.tracks_standard.desc"] = "Уровни лучше держат грязь.";
            table["module.tracks_wide.name"] = "Широкие траки";
            table["module.tracks_wide.desc"] = "Грязь не замедляет, давка шире на 30 %.";
            table["module.tracks_wide.lock"] = "Пройди элиту главы 1";
            table["module.special_smoke.name"] = "Дымовые гранатомёты";
            table["module.special_smoke.desc"] = "Облако дыма на 12 с: Плевуны и элита теряют цель. Перезарядка 30 с.";
            table["module.special_repair.name"] = "Ремкомплект";
            table["module.special_repair.desc"] = "+30 % здоровья за 5 с. Перезарядка 60 с.";
            table["module.special_repair.lock"] = "Пройди элиту главы 1";
            table["module.special_shockwave.name"] = "Ударная волна";
            table["module.special_shockwave.desc"] = "Отброс и 60 урона в 8 м, оглушение элиты 1.5 с. Перезарядка 20 с.";
            table["module.special_shockwave.lock"] = "Откроется после вехи 1";

            // ---------- Chapters and tanks ----------
            table["chapter.outskirts.name"] = "Окраины";
            table["chapter.town.name"] = "Городок";
            table["chapter.quarry.name"] = "Карьер";
            table["tank.tiger.name"] = "Тигр";
            table["tank.hound.name"] = "Гончая";
            table["tank.fortress.name"] = "Крепость";

            // ---------- Common ----------
            table["hangar.marks_format"] = "Марки: {0}"; // TMP
            table["hangar.samples_format"] = "Образцы: {0}"; // TMP
            table["hangar.price_marks_format"] = "{0} марок"; // TMP and string.Format
            table["hangar.price_both_format"] = "{0} марок + {1} образцов"; // TMP and string.Format
            table["hangar.level_format"] = "ур. {0}/{1}"; // TMP
            table["hangar.close"] = "Закрыть";
            table["hangar.purchase.done"] = "Готово";
            table["hangar.purchase.not_enough_marks"] = "Не хватает марок";
            table["hangar.purchase.not_enough_samples"] = "Не хватает образцов";
            table["hangar.purchase.already_owned"] = "Уже куплено";
            table["hangar.purchase.not_owned"] = "Не куплено";
            table["hangar.purchase.max_level"] = "Макс. уровень";
            table["hangar.purchase.locked"] = "Закрыто";
            table["hangar.purchase.unknown"] = "Недоступно";
            table["hangar.stat.health"] = "Здоровье";
            table["hangar.stat.speed"] = "Скорость";
            table["hangar.stat.damage"] = "Урон";
            table["hangar.stat.reload"] = "Перезарядка";
            table["hangar.stat_label.health"] = "Здоровье";
            table["hangar.stat_label.speed"] = "Скорость, м/с";
            table["hangar.stat_label.damage"] = "Урон пушки";
            table["hangar.stat_label.reload"] = "Перезарядка, с";

            // ---------- Main menu ----------
            table["hangar.main.continue"] = "ПРОДОЛЖИТЬ";
            table["hangar.main.new_game"] = "Новая игра";
            table["hangar.new_game_title"] = "Новая игра";
            table["hangar.new_game_text"] = "Сохранение будет стёрто: марки, образцы, модули и главы начнутся заново.";
            table["hangar.new_game_yes"] = "Стереть и начать";

            // ---------- Hangar ----------
            table["hangar.title"] = "АНГАР";
            table["hangar.slots"] = "СЛОТЫ";
            table["hangar.slot_empty"] = "Пусто";
            table["hangar.to_shop"] = "Магазин";
            table["hangar.to_upgrade"] = "Улучшение";
            table["hangar.to_battle"] = "В БОЙ";

            // ---------- Slot assembly ----------
            table["hangar.compare"] = "БЫЛО → СТАЛО";
            table["hangar.install"] = "УСТАНОВИТЬ";
            table["hangar.installed"] = "Установлен";
            table["hangar.no_modules"] = "Нет купленных модулей";

            // ---------- Upgrade ----------
            table["hangar.upgrade_title"] = "УЛУЧШЕНИЕ МОДУЛЕЙ";
            table["hangar.upgrade"] = "Улучшить";
            table["hangar.max_level"] = "Высший уровень";

            // ---------- Shop ----------
            table["hangar.shop.title"] = "МАГАЗИН";
            table["hangar.shop.tab_guns"] = "Орудия";
            table["hangar.shop.tab_armor"] = "Броня";
            table["hangar.shop.tab_mobility"] = "Двигатель и ходовая";
            table["hangar.shop.tab_special"] = "Спецмодули";
            table["hangar.shop.tab_tanks"] = "Танки";
            table["hangar.shop.owned"] = "Куплено";
            table["hangar.shop.buy"] = "Купить";
            table["hangar.shop.no_change"] = "Не меняет сводку танка";
            table["hangar.shop.tank_owned"] = "В ангаре";
            table["hangar.shop.soon"] = "Скоро";
            table["hangar.buy_title"] = "Покупка";
            table["hangar.buy_text_format"] = "Купить «{0}» за {1}?\nОстанется {2} марок.";

            // ---------- Chapter select ----------
            table["hangar.chapter.title"] = "ВЫБОР ГЛАВЫ";
            table["hangar.chapter.item_format"] = "{0}. {1}";
            table["hangar.chapter.open"] = "Открыта";
            table["hangar.chapter.locked"] = "Закрыта: пройди предыдущую главу";
            table["hangar.chapter.completed"] = "Пройдена";
            table["hangar.chapter.record_format"] = "Рекорд: арена {0}/{1}, время {2}";
            table["hangar.chapter.no_record"] = "Рекорда пока нет";
            table["hangar.chapter.reward_format"] = "Награда за первое прохождение: {0}";
            table["hangar.chapter.reward_taken_format"] = "Награда за первое прохождение получена: {0}";
            table["hangar.chapter.tank_format"] = "Рекомендуемый танк: {0}";
        }
    }
}
