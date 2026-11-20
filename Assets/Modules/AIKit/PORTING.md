# Перенос AIKit в CodeCraft_HomeWork

**Источник:** `C:\!!!Work\Platformer-AIKit`, ветка `feature/aikit-lab`, коммит `5b727e9d` (2026-11-17), путь `Assets/Game/Scripts/Modules/AIKit`.
**Документы модуля:** там же, `Assets/AI/Docs` (`AIKit.md`, `AI_Handbook.md`). Сюда не копировались.

## Что не перенесено

- `Demo/`, `Demo3D/` — сцены-примеры. Тестам не нужны.
- MontageKit и мост `AIKitMontage` — не нужны игре.

## Правки при переносе

Каждая правка помечена в коде комментарием `PORTING`. При переносе новой версии модуля их нужно повторить или проверить, что они больше не нужны.

| Файл | Правка | Почему |
|---|---|---|
| `Runtime/Core/Agent/AICheats.cs` | `FindObjectsByType<T>()` → `FindObjectsByType<T>(FindObjectsSortMode.None)` | в 6000.3 нет перегрузки без аргументов (модуль писался под 6000.5) |
| `Runtime/Core/Perception/ObjectFinder.cs` | добавлен `FindObjectsSortMode.None` | в 6000.3 нет перегрузки `(Type, FindObjectsInactive)` |
| `Editor/ValidateAll.cs`, `Editor/Objects/SceneObjectsMigration.cs`, `Editor/Graph/NodeOptions.cs`, `Editor/Graph/GraphDocument.cs`, тесты `ArcFlightTests`, `WeaponShotTests`, `Coverage/*` | `FindObjectsByType<T>(FindObjectsInactive.Include)` → `(FindObjectsInactive.Include, FindObjectsSortMode.None)` | то же |
| `Tests/Editor/Coverage/SensesDetectionTests.cs` | тег `Blue` → `Respawn` в `Sensor_AlsoTagUntaggedOrTheSameAsTag_IsNoSecondKind` | тега `Blue` нет в нашем TagManager; `Respawn` есть в любом проекте |
| `Tests/Editor/PerfSmokeTests.cs` | `Crowd_StepCost_StaysNearTheBaseline` стал `[Explicit]` | порог снят на машине лаборатории в 6000.5. У нас в 6000.3 при шести открытых редакторах — 3.15–3.53 мкс на агента против порога 2.75 |
| `Tests/Editor/EveryNodeTests.cs` (`EveryNode_HasAPlainName_AndAStableIdOfItsOwn`), `Tests/Editor/NodeMenuTests.cs` (`Everything_FoldersInTheOrderOfTheSpec_NodesByTheirOrder`) | проверяют только узлы самого модуля: пропускают типы не из сборок `AIKit*` и пункты меню из корня `Game/` | у игры свои узлы графа (`Features/AI/EliteRegax/Nodes`, id `game.*`, меню `Game/…`); в лаборатории узлов игры не было |

## Тесты

`AIKit.Tests`, EditMode: 1672 теста, прогон 2026-11-17: 1668 зелёных, 0 упавших. Не зелёные:
- `PerfSmokeTests.Crowd_StepCost_StaysNearTheBaseline` — `[Explicit]`, см. выше;
- `CanvasDocumentEditTests.GraphDocument_CheckKeys_AMoveNoWeaponHas_IsAWarningOnItsBlock` — Inconclusive (`Assume`): в проекте нет ассета оружия с приёмами.

Тесты со временной сценой (`TempScene`) требуют, чтобы открытая сцена не была безымянной несохранённой.
- `GraphDocumentTests.RenameAndDeleteVariable_TheGraphsOwn_NoCharacterOfTheSceneIsChanged` и `LifeNodeTests.Chat_NoWorldGiven_TheAgentsJoinTheScenesWorld_AndTalk` — Inconclusive в общем прогоне: какой-то тест раньше них оставляет безымянную сцену. По отдельности оба зелёные.

## Своё в этом проекте (не из Platformer-AIKit)

С 2026-11-20 в модуле есть сборка **`AIKit.Atomic`** (`Adapters/Atomic`, пространство имён `AIKit.Entities`) и её тесты (`Tests/Atomic` — EditMode, `Tests/AtomicPlayMode` — PlayMode). Это слой ядра для Atomic: агент на сущности, мир на сущности уровня, тело `EntityBody`. В Platformer-AIKit её нет: при переносе новой версии модуля папки `Adapters/Atomic`, `Tests/Atomic`, `Tests/AtomicPlayMode` не затирать. Ядро она не меняет. Документ — `Docs/AI/AIKit_Atomic.md`.
