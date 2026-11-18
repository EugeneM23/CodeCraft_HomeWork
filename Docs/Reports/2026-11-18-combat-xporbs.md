# Опыт с убитых (`Combat.XpOrbs`): откуда 15 КБ

**Дата:** 2026-11-18. **Ветка:** `claude/combat-perf` от `game/develop` `14d0d3a80`. **Команда:** бой.
**Повод:** `Docs/Reports/2026-11-18-perf-fix.md`, «Что осталось», п. 5: `Combat.XpOrbs` — 15 КБ в кадре.

## Итог

Код сгустков в бюджете. 15 КБ под маркером `Combat.XpOrbs` — это **пауза ядра на повышении уровня**. Её поднимает `RunService.HoldPause` синхронно внутри `AddExperience`, а `AddExperience` зовёт подбор сгустка. Поэтому мусор оказывается под маркером сгустков.

Сам мусор делают морозилки модуля GameLoop:
- `ComponentFreezer<T>.OnPause`: `Object.FindObjectsByType<T>` даёт новый массив на каждую паузу;
- `SceneManager.sceneLoaded +=` и `-=`: склейка делегатов на каждую паузу и снятие паузы.

Владелец — ядро (модуль `Assets/Modules/GameLoop`). Бой код не менял.

## Замеры

Редактор на рабочей копии, свёрнутый, без других живых редакторов. CPU машины — 14 %. VoxelSandbox, URP Ultra, 2294×1291, Burst включён.

**Бой с ордой** (`Tools/PerfCheck/Bench/combat_bench.cs`): 60 Ползунов, все 6 навыков, 8 с езды. Танк убил 23 врага в секунду.

| Метрика | Значение |
|---|---|
| `Combat.XpOrbs`, среднее / максимум | 0.010 / 0.104 мс |
| PlayerLoop, среднее / p95 | 4.79 / 7.16 мс |
| GC игры, кадров с аллокацией | 94 из 1181, максимум 7.1 КБ |

`Combat.XpOrbs` в первой восьмёрке источников мусора нет. В верхних строках — `VoxelWorld.LateUpdate`, `VoxelBody..ctor`, `AddComponent`, `VoxelAirStrike`.

**Проба цепочки «подбор → уровень → выбор навыка».** Шесть повышений уровня со слушателем `SkillOffered`, как в замере перфа. Профайлер вёл стеки вызовов `GC.Alloc`, разбор шёл по сырым сэмплам.

| Источник | Байт за 6 повышений | Вызовов |
|---|---|---|
| `ComponentFreezer<T>.OnPause` (`FindObjectsByType`) ← `GameLoop.Pause` ← `RunService.HoldPause` | 30 976 | 238 |
| `SceneManager.add_sceneLoaded` ← `ComponentFreezer.OnPause` | 3 744 | 36 |
| `SceneManager.remove_sceneLoaded` ← `ComponentFreezer.OnResume` | 3 600 | 36 |
| `ComponentFreezer.OnResume` ← `RunService.LetPauseGo` | 2 304 | 18 |
| `AnimatorFreezer.Freeze` (рост словаря) | ~2 000 | 9 |
| `RunService.BuildOffer` / `ChooseSkill` (рост списков) | ~400 | 4 |
| Эффекты навыков боя (`SkillEffectsBehaviour`, `TankStatsApplier`) | 0 | 0 |

Получается около 7 КБ на одно повышение уровня с паузой. Бюджет кадра события — 4 КБ.

Без слушателя выбора, когда ядро выбирает навык само, эффекты навыков дают 52 Б. Это только при «Броне», и мусор делает `TankHudView.RefreshHealth` (TMP `SetText`) на смене максимума здоровья. Владелец — меню.

## Что предлагаю ядру

`ComponentFreezer<T>`:
- подписку на `SceneManager.sceneLoaded` держать постоянной и проверять `_paused` в обработчике, а не подписываться на каждую паузу;
- вместо `FindObjectsByType` на каждой паузе вести реестр компонентов. Другой вариант — оставить поиск, но признать паузу на выборе навыка кадром события и выровнять её до 4 КБ.

## Прочее

- `combat_bench.cs` поправлен под новые `HordeOptions`: вместо `attackDps`, `health` и `speed` теперь `tankDamageScale`, а числа Ползунов берутся из `HordeKindOptions` орды.
