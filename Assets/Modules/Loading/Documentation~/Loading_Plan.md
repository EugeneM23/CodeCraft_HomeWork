# Loading — план

Спек — [Loading.md](Loading.md). Каждая задача — TDD (тест красный → код → зелёный), полный прогон, локальный коммит.

**Тесты в worktree** `C:\!!!Work\Platformer-Loading` гоняются batchmode-Unity (в основной папке открыт другой
редактор): `Unity.exe -batchmode -projectPath <worktree> -runTests -testPlatform EditMode|PlayMode -testResults <xml>`.

| # | Задача | Файлы | Готово, когда |
|---|---|---|---|
| 1 | Ядро модуля | `Modules/Loading/Runtime`: `LoadingStatus`, `LoadingOperation`, `SequenceOperation`, `ParallelOperation`, `ActionOperation`, `LoadingResult`, `LoadingRunner`; `Tests/Editor` | EditMode-тесты ядра зелёные |
| 2 | Unity-листья | `FadeOperation`, `LoadSceneOperation`; `Tests/Runtime` + `LoadingTestScene.unity` | PlayMode-тесты модуля зелёные |
| 3 | `ISceneLoader` | `Scripts/GameSystem/Services/ISceneLoader.cs`, `SceneLoader.cs`, `GameServices`; дочерний `SceneLoader` в `Prefabs/Services.prefab`; `Game.asmdef`, тестовые asmdef → `Loading`; `SceneLoaderTests` | тесты загрузчика и префаба зелёные |
| 4 | Restart в паузе | `PauseScreen` (+ кнопка в префабе), `GameUI` (подтверждение, загрузчик, глушение ввода при загрузке); тесты | Restart перезагружает сцену в песочницах |
| 5 | Документация | `README.md` модуля, `Assets/AI/CLAUDE.md`, `UI.md` (часть 2 — загрузчик готов) | полный прогон зелёный |

Задача 4 зависит от `GameUI` (часть 1 UI, задача 7 — делает другая сессия в `feature/ui`): перед ней `feature/ui`
вливается в `feature/loading`.
