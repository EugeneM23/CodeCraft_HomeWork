# Loading

Композитная загрузка для Unity: загрузка — дерево маленьких операций с весами, общий прогресс и одна точка, где
видно, что за чем идёт. Узлы `SequenceOperation` и `ParallelOperation` — тоже операции, поэтому вкладываются.
Операции тикаются владельцем (`Tick(dt)`), без `async`: тесты шагают вручную, исключения не теряются, после
выхода из Play Mode ничего не «висит».

Модуль самостоятельный: не зависит ни от игры, ни от плагинов, только Unity. Проверено на Unity 6000.5.

## Состав

```
Loading/
├── Runtime/        (сборка Loading, namespace Loading)
│   ├── LoadingOperation    — база: вес, статус, прогресс, ошибка; Begin/Tick; исключение → Failed
│   ├── CompositeOperation  — база узлов: вес = сумма детей, прогресс — взвешенный
│   ├── SequenceOperation   — дети по очереди
│   ├── ParallelOperation   — дети вместе
│   ├── ActionOperation     — мгновенное действие
│   ├── WaitUntilOperation  — ждать, пока условие не станет истинным
│   ├── FadeOperation       — альфа CanvasGroup к цели за время
│   ├── LoadSceneOperation  — SceneManager.LoadSceneAsync по имени или пути, с проверкой Build Settings
│   ├── LoadingRunner       — запуск дерева: Run, Tick, IsRunning, Progress, Elapsed
│   └── LoadingResult, LoadingStatus
└── Tests/
    ├── Editor/     (Loading.Tests) — ядро на фейковых операциях
    └── Runtime/    (Loading.Tests.Runtime) — затемнение и загрузка сцены (своя LoadingTestScene)
```

## Быстрый старт

```csharp
using Loading;

public class LevelLoader : MonoBehaviour
{
    [SerializeField] private CanvasGroup _blackout;

    private readonly LoadingRunner _runner = new();

    public void LoadLevel(string scene)
    {
        var load = new SequenceOperation(
            new FadeOperation(_blackout, 1f, 0.25f),          // weight 0: not part of the progress
            new ParallelOperation(
                new LoadSceneOperation(scene),                // weight 1
                new WarmUpPoolsOperation(weight: 2f)),        // your own leaf
            new FadeOperation(_blackout, 0f, 0.25f));

        if (!_runner.Run(load, result => Debug.Log(result.Succeeded ? "loaded" : result.Error)))
            Debug.Log("already loading");
    }

    private void Update()
    {
        _runner.Tick(Time.unscaledDeltaTime);
        // _runner.Progress — 0..1 for a bar
    }
}
```

Своя операция:

```csharp
public sealed class WarmUpPoolsOperation : LoadingOperation
{
    private int _done;

    public WarmUpPoolsOperation(float weight) : base(weight) { }

    protected override void OnBegin() => _done = 0;           // may Succeed() or Fail() at once

    protected override void OnTick(float dt)
    {
        _done++;                                              // some work per frame
        if (_done == 10)
            Succeed();
    }

    protected override float MeasureProgress() => _done / 10f;
}
```

## Контракт

**LoadingOperation**
- `Begin()` — сброс и старт; во время `Running` — `InvalidOperationException`; после завершения — заново.
- `Tick(dt)` — только в `Running`.
- Исключение из `OnBegin`/`OnTick` — `Failed`, `Error` = «Тип: Исключение: сообщение», само — в `Exception`.
- `Succeed`/`Fail` вне `Running` игнорируются.
- `Progress`: `Idle` — 0, `Succeeded` — 1, иначе `MeasureProgress()` в 0..1.
- Вес < 0 — 0. Мгновенные операции и затемнения по умолчанию весят 0.

**Sequence / Parallel**
- Вес — сумма детей, прогресс — `Σ вес × прогресс / Σ вес` (нулевой вес — 0 до завершения).
- Последовательность: мгновенные дети проходят цепочкой в одном вызове; упавший ребёнок останавливает её,
  следующие не начинаются.
- Параллель: стартуют все; готова, когда готовы все; первая упавшая — `Failed`, остальные больше не тикают.
- Ошибка поднимается к корню с текстом и исключением листа. Пустой узел — сразу `Succeeded`; `null` ребёнок —
  `ArgumentNullException`.

**WaitUntilOperation**
- Условие проверяется в `Begin` и в каждом `Tick`: уже истинное — сразу `Succeeded`.
- Исключение в условии — `Failed`; вес по умолчанию 0.

**LoadSceneOperation**
- Сцена — имя или путь (`Assets/…/Level.unity`).
- Сцены нет в Build Settings — `Failed` в `Begin`, ничего не грузится, текущая сцена цела. В эдиторе путь
  к существующей сцене грузится и вне Build Settings (`EditorSceneManager.LoadSceneAsyncInPlayMode`).
- Прогресс — `AsyncOperation.progress / 0.9`, готова на `isDone`.

**LoadingRunner**
- `Run` во время загрузки — `false`, запрос не принят.
- `done` — ровно один раз, после сброса `IsRunning` (из него можно запустить следующую загрузку).
- Мгновенное дерево завершается прямо в `Run`.

## Чего нет (намеренно)

- Отмены: `AsyncOperation` сцены не отменить, остальное обычно мгновенно.
- Контекста между операциями: дерево строится на каждую загрузку, параметры — в конструкторах операций.
- `async`/`await`: опрос проще тестировать и не оставляет висящих задач.

## Тесты

*Window → General → Test Runner*: `Loading.Tests` (EditMode) и `Loading.Tests.Runtime` (PlayMode). Тест загрузки
сцены сам добавляет `LoadingTestScene` в Build Settings до Play Mode (`IPrebuildSetup`) и убирает после;
`LoadingOutsideBuildScene` в Build Settings не попадает никогда — на ней проверяется загрузка по пути в эдиторе.
