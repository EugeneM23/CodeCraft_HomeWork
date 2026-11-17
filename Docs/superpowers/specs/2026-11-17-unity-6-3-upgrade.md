# Перевод проекта на Unity 6000.3.28f1 (6.3 LTS)

**Дата:** 2026-11-17
**Команда:** Unity 6.3 (общий план: `Docs/superpowers/specs/2026-11-17-game-production-plan.md`)
**Было:** 6000.0.38f1, базовый коммит плана `b136fb616`

## Что изменилось

### Пакеты (`Packages/manifest.json`)

Версии поставил сам редактор 6.3 при открытии проекта: это версии, которые он рекомендует.

| Пакет | Было | Стало |
|---|---|---|
| com.unity.burst | 1.8.19 | 1.8.31 |
| com.unity.collections | 2.5.1 | 2.7.1 |
| com.unity.mathematics | 1.3.2 | 1.3.3 |
| com.unity.inputsystem | 1.13.1 | 1.20.1 |
| com.unity.ugui (вместе с TextMeshPro) | 2.0.0 | 2.0.0 |
| com.unity.test-framework | 1.4.6 | 1.6.0 |
| com.unity.timeline | 1.8.7 | 1.8.13 |
| com.unity.animation.rigging | 1.3.1 | 1.4.1 |
| com.unity.shadergraph | 17.0.3 | 17.3.0 |
| com.unity.postprocessing (v2, остаётся до URP) | 3.4.0 | 3.5.4 |
| com.unity.feature.2d | 2.0.1 | 2.0.2 |
| com.unity.ide.rider | 3.0.31 | 3.1.0 |
| com.unity.ide.visualstudio | 2.0.22 | 2.0.26 |
| com.unity.collab-proxy | 2.6.0 | 2.14.0 |
| com.unity.multiplayer.center | 1.0.0 | 1.0.1 |
| com.unity.pipeline (мост Unity CLI) | 0.10.0-exp.1 | без изменений |

Редактор добавил встроенные модули `com.unity.modules.adaptiveperformance` и `com.unity.modules.vectorgraphics`.

### Настройки (`ProjectSettings/`)

- `ProjectVersion.txt` — 6000.3.28f1.
- `ProjectSettings.asset` — новые поля 6.3 со значениями по умолчанию. Минимальные версии Android, iOS и macOS подняты до тех, что поддерживает 6.3; для ПК это не важно.
- Графический API на Windows. С 6.1 «Auto Graphics API» по умолчанию выбирает DirectX 12. При переводе Unity выключила «Auto» и оставила явный список: **D3D11, затем D3D12**. Так проект рисует так же, как раньше. Об этом редактор сообщил окном «Auto Graphics API Notice».
- `ShaderGraphSettings.asset` — новое поле Shader Graph 17.3 (`overrideShaderVariantLimit: 0`).

### Код

Устаревший API в нашем коде (`Assets/Game`, `Assets/Plugins/Atomic`) — два места, оба исправлены:

| Файл | Было | Стало |
|---|---|---|
| `Assets/Game/Voxels/Runtime/VoxelWorld.cs`, `SurfaceNetsJob.cs` | `Physics.BakeMesh(int, bool)`, идентификаторы мешей `GetInstanceID()` в `NativeArray<int>` | `Physics.BakeMesh(EntityId, bool)`, `GetEntityId()` в `NativeArray<EntityId>` |
| `Assets/Game/Voxels/Runtime/VoxelRain.cs` | `RaycastHit.colliderInstanceID != 0` | `RaycastHit.colliderEntityId != EntityId.None` |

API Updater исправил одну строку в OmniShade: `MaterialProperty.flags` заменён на `propertyFlags`.

После чистой пересборки всех скриптов в `Assets/Game` и `Assets/Plugins/Atomic` нет ни одного предупреждения об устаревшем API.

### Что осталось (сторонний код, не исправляем)

- `Assets/Plugins/UniTask/Editor/UniTaskTrackerTreeView.cs` — 11 предупреждений: необобщённые `TreeView`, `TreeViewItem` и `TreeViewState` устарели в 6.2. Это только окно трекера задач в редакторе. Уйдёт с новой версией UniTask (сейчас 2.5.7).
- `Assets/RayFire/Scripts/Components/RayfireCombine.cs:218` — `ParticleSystemRenderer.BakeMesh(Mesh, bool)`. Было и на 6.0. RayFire удаляет команда чистки.
- `Object.GetInstanceID()` в 6000.3.28f1 не помечен устаревшим, поэтому остальные вызовы в нашем коде (`GameFactory`, `CombatPoolPrewarm`, `MobileHighlights`, `VfxBudget`, `VoxelBody`) остаются. По данным Unity, в 6.4–6.5 он станет предупреждением, а потом ошибкой; замена — `GetEntityId()`.

## Сторонние ассеты и генераторы

Odin, HighlightPlus, OmniShade, KriptoFX, UniTask, DOTween и RayFire компилируются на 6.3 без ошибок.

Генераторы Atomic работают: пять DLL в `Assets/Plugins/Atomic/SourceGenerators` с лейблом `RoslynAnalyzer` и выключенными платформами, как требует `Project~/Docs/Setup.md`. Сгенерированный API (`EntityAPI` и другие) используется всем кодом, и тот компилируется.

## Тесты

| Набор | 6000.0.38f1 (до) | 6000.3.28f1 (после) |
|---|---|---|
| EditMode | 2453 из 2453 | 2453 из 2453 |
| PlayMode, весь набор | 552, красных 3 | 552, красных 4 (дважды одинаково) |

Красные **до** перевода (на 6.0):

1. `SandboxVoxelSmokeTest.Sandbox_CratersAreBowlsAndTheTankLeavesRuts`. Падает всегда, и в одиночку тоже. Колеи не появляются: число вокселей тайла не уменьшается, когда танк сдвигают через `MovePosition`.
2. `VoxelCarcassTests.Drip_DropsWhileItLastsThenGoes`. Падает только после смоук-теста, а в одиночку проходит. Смоук-тест оставляет загруженной сцену VoxelSandbox.
3. `VoxelDebrisKeepTests.Drop_FallingOffTheWorld_IsDropped`. Причина та же, что у пункта 2.

На 6.3 эти три ведут себя так же. Добавился четвёртый:

4. `VoxelTankCrushTests.Crushing_OffToTheRight_StainsTheRightMore`. В полном наборе на 6.3 падает (R 193 против L 171 при пороге R > 1.3·L), в одиночку упал в 1 запуске из 7. Разбор:
   - Точка давки в каждом запуске одна и та же: (0.49, 0.55, −0.25). Меняется только случайный результат. По запускам отношение R/L было от 1.0 до 4.6.
   - Случайность идёт из общего `UnityEngine.Random`: `VoxelBody.TryGetSurfacePoint` и `TankCrushBehaviour.Stain` берут числа из него. Сколько раз его вызовут между физическими шагами, зависит от числа кадров в реальном времени. Поэтому фиксированное зерно тест не чинит: при одном зерне получились R/L 172/186, 220/143 и 209/172.
   - Если брызг больше (1600 вместо 400), стороны насыщаются и отношение только падает (около 1.1–1.6).
   - Вывод: тест статистический и неустойчивый по построению. Поведение давки на 6.3 не изменилось. Но на 6.0 тест прогоняли один раз, так что доказательства «до» слабые.

## VoxelSandbox

В Play один и тот же сценарий на обеих версиях: 5 с езды вперёд, выстрел основного орудия на первой секунде, Regax поставлен в 10 м на пути. На 6.3: танк проехал 28.3 м (на 6.0 — 28.2 м), выстрел есть, Regax раздавлен с кровью, на танке кровь, в дороге колеи, грязь на месте. Авиаудары работают: после 54 с было 6665 летящих и 1895 лежащих обломков.

Скриншоты «до» и «после» с одних и тех же точек лежат в общей папке заметок сессий, `unity-6.3/shots-before-6000.0.38f1` и `unity-6.3/shots-after-6000.3.28f1`.

## Известное после перевода

- При первом запуске 6.3 на `Library` от 6.0 индексатор поиска редактора бросает `ArgumentOutOfRangeException` в `UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase`. Это внутренняя ошибка редактора, на игру она не влияет. Если мешает — удалить `Library/Search`.
- Старый Input Manager: в проекте `activeInputHandler: 2` (оба). Отдельного предупреждения 6.3 об устаревании в логе нет. Переход на Input System — задача команды меню.
