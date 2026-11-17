# Loading — композитная загрузка и смена сцен

**Статус:** реализовано 2026-10-24, все задачи плана. `feature/ui` (с `GameUI`) влита в ветку; Restart в паузе —
подтверждение и `ReloadActiveScene`, пока идёт загрузка, `GameUI` не читает Pause/Cancel и не ставит паузу при потере
фокуса. Кнопка Restart стоит между Resume и Quit — до Quit теперь два шага вниз.
Ветка `feature/loading` (от `feature/ui`). План — [Loading_Plan.md](Loading_Plan.md).

**Дальше (2026-10-24, [Bootstrap.md](Bootstrap.md)):** загрузка разворачивает всё приложение — сцена `Boot`, ход игры
`IGameFlow`, каждый переход — дерево операций. `ISceneLoader` стал экраном загрузки для любого дерева: `Run(work,
mode)` с режимами `Fade` (переходы) и `Cover` (старт: сразу чёрный и полоска), полоска плавная и доезжает до конца;
`ReloadActiveScene` убран — Restart и «Main Menu» идут через `IGameFlow`. В модуле — `WaitUntilOperation`, а
`LoadSceneOperation` принимает путь и в эдиторе грузит сцену не из Build Settings. Разделы 4 и 6 ниже — исходный
вариант, актуальное устройство — в Bootstrap.md, раздел 4.2.

Модуль `Modules/Loading` — загрузка как дерево маленьких операций с весами и общим прогрессом. Первый пользователь —
сервис `ISceneLoader` в `[Services]`: затемнение, загрузка сцены с полоской прогресса, проявление; кнопка «Restart» в
паузе перезагружает уровень через него. Это часть 2 из [UI.md](UI.md) (раздел 6), сделанная раньше главного меню.

## 1. Откуда идея: разбор старой системы

Источник — `Assets/_Project/Scripts/Modules/Loading` из старого проекта `github.com/EugeneM23/SPrototype`.
`GameLauncher` проходит список `IloadingOperation` по очереди; у операции `Load(LoadingBundle) → LoadingResult`, вес и
прогресс; общий прогресс — веса готовых плюс вес текущей × её прогресс; полоску рисует `LoadingScreen`. Список
собирает Zenject-инсталлер, данные между шагами — `LoadingBundle` (`Dictionary<BundleKeys, object>`).

**Берём идею:** загрузка — список маленьких операций, порядок собирается в одном месте; взвешенный прогресс;
результат вместо исключения; экран загрузки не знает об операциях.

**Не берём код — в нём ошибки:**

| # | Проблема старого кода | Как у нас |
|---|---|---|
| 1 | Не композит: плоский список, операцию из операций не собрать, параллельно не запустить | `SequenceOperation` и `ParallelOperation` — тоже операции, вкладываются |
| 2 | В `LoadSceneOperation` `GetWeight()`/`GetProgress()` без `public` — это приватные методы класса, а не реализация default-методов интерфейса: через интерфейс вес 1 и прогресс 1, полоска прыгает | Базовый класс с `Weight` в конструкторе и `MeasureProgress()` под `override` — компилятор не даст ошибиться |
| 3 | Дефолт `GetProgress() => 1`: идущая операция считается готовой | Прогресс идущей — `MeasureProgress()` (по умолчанию 0), готовой — 1 |
| 4 | `_currentWeight` не сбрасывается — второй запуск с 100% | Состояние в операции, `Begin` его сбрасывает; на каждую загрузку — новое дерево |
| 5 | Ошибка не видна: `PrintError` и сразу `Hide()` | Ошибка — в консоль и вызвавшему (`LoadingResult`), экран проявляется |
| 6 | Исключение в операции: `async`-задачу никто не ждёт, экран висит вечно | `Begin`/`Tick` ловят исключение и переводят операцию в `Failed` |
| 7 | Двойной вызов — две загрузки; `StartButton` подписывает лямбду в каждом `OnEnable` | `Run`/`Load` во время загрузки возвращают `false` |
| 8 | `LoadingBundle`: глобальный enum ключей игры внутри модуля, `object` и каст | Контекста нет: параметры операции — в её конструкторе (раздел 4) |
| 9 | Мёртвый код (`StartGameOperation`, `Show/HideLoadingScreenOperation`), магическое `BootLevel = 2` | — |
| 10 | Zenject и UniTask | Без зависимостей, тики вместо `async` (раздел 2) |

## 2. Решения

| Тема | Решение |
|---|---|
| Асинхронность | **Тики, не `async`.** Операция — объект с `Begin()` и `Tick(dt)`, его опрашивает `LoadingRunner`. Как `SpriteAnimationPlayer`, `FadeTransition` и FSM врагов: тесты шагают вручную, без кадров и ожиданий; нет «висящих» задач, необработанных исключений и продолжений после выхода из Play Mode. `Awaitable` не умеет `WhenAll`, `Task` в Unity требует аккуратности с контекстом — для 5–10 операций загрузки опрос проще |
| Композит | Абстрактный `LoadingOperation` — и лист, и узел. Узлы: `SequenceOperation` (по очереди), `ParallelOperation` (вместе) |
| Вес | Лист — из конструктора (по умолчанию 1, мгновенные действия и затемнения — 0). Узел — сумма весов детей. Прогресс узла — `Σ вес × прогресс / Σ вес`; при нулевом весе 0, готовый — 1 |
| Ошибка | Операция падает через `Fail(text)` или исключение (ловит база). Последовательность останавливается на упавшей, параллель — на первой упавшей (остальные больше не тикают). Ошибка поднимается к корню с текстом листа |
| Отмена | Нет: `AsyncOperation` загрузки сцены не отменить, а остальное мгновенно. Добавим, когда появится долгая отменяемая операция |
| Контекст между операциями | Нет: дерево строится на каждую загрузку, параметры — в конструкторах, общее — в замыканиях. Типизированные ключи — когда два шага разных систем начнут обмениваться данными |
| Время | `dt` от владельца: `SceneLoader` даёт `Time.unscaledDeltaTime` (игра может стоять на паузе) |
| Сцена | `LoadSceneOperation` по имени; до старта проверяет `Application.CanStreamedLevelBeLoaded` — ошибка до загрузки и старая сцена цела |
| Прогресс-полоска | Чёрный экран; полоска и проценты — только если загрузка идёт дольше `ProgressDelay` (0.5 с) после полного затемнения. Быстрые переходы — просто затемнение |
| Ошибка загрузки сцены | `Debug.LogError`, экран проявляется, игрок остаётся в текущей сцене; вызвавший получает `LoadingResult` и решает сам |
| Пауза | Загрузчик держит паузу игрового цикла на всё время перехода (затемнение, загрузка, проявление) — ввод и игра возвращаются вместе ([GameLoop.md](GameLoop.md)). Меню паузы отпускает свою, умирая со старой сценой. После «Restart» игра идёт |
| Ввод во время загрузки | Оверлей ловит клики (`blocksRaycasts`), общий `EventSystem` выключен — клавиатура и геймпад не нажмут кнопку меню; `GameUI` не реагирует на Esc/Start, пока `IsLoading` |
| Первое применение | «Restart» в меню паузы с подтверждением: перезагрузка активной сцены |

## 3. Модуль `Modules/Loading`

Namespace `Loading`, сборки `Loading` (рантайм, без ссылок), `Loading.Tests` (EditMode), `Loading.Tests.Runtime`
(PlayMode). Модуль ничего не знает об игре.

```
Modules/Loading/
├── README.md
├── Runtime/   Loading.asmdef
│   ├── LoadingStatus.cs        — Idle, Running, Succeeded, Failed
│   ├── LoadingOperation.cs     — база: вес, статус, прогресс, ошибка, Begin/Tick, перехват исключений
│   ├── SequenceOperation.cs    — дети по очереди
│   ├── ParallelOperation.cs    — дети вместе
│   ├── ActionOperation.cs      — мгновенное действие (вес 0)
│   ├── FadeOperation.cs        — альфа CanvasGroup к цели за время (вес 0)
│   ├── LoadSceneOperation.cs   — SceneManager.LoadSceneAsync, прогресс сцены
│   ├── LoadingResult.cs        — Succeeded + Error
│   └── LoadingRunner.cs        — запуск корня, Tick, IsRunning, Progress, Elapsed, done-колбэк
└── Tests/
    ├── Editor/   Loading.Tests.asmdef          — ядро на фейковых операциях
    └── Runtime/  Loading.Tests.Runtime.asmdef  — FadeOperation, LoadSceneOperation (+ LoadingTestScene.unity)
```

### 3.1. Контракт операции

```csharp
public abstract class LoadingOperation
{
    protected LoadingOperation(float weight = 1f);   // вес < 0 → 0

    public virtual float Weight { get; }
    public LoadingStatus Status { get; }
    public bool IsDone { get; }                        // Succeeded или Failed
    public string Error { get; }                       // null, пока не упала
    public Exception Exception { get; }                // если упала исключением
    public float Progress { get; }                     // 0..1: Succeeded → 1, иначе MeasureProgress()

    public void Begin();        // сброс и старт; во время Running — InvalidOperationException
    public void Tick(float dt); // только в Running

    protected abstract void OnBegin();                 // может сразу позвать Succeed/Fail
    protected virtual void OnTick(float dt) { }
    protected virtual float MeasureProgress() => 0f;
    protected void Succeed();
    protected void Fail(string error);
}
```

- Исключение из `OnBegin`/`OnTick` → `Failed`, `Error` = «`Тип`: сообщение», само исключение — в `Exception`.
- `Succeed`/`Fail` вне `Running` игнорируются: упавшая операция не «оживает».
- Повторный `Begin` после завершения — заново (операцию можно переиспользовать).

### 3.2. Узлы

- `SequenceOperation(params LoadingOperation[] children)` — `Begin` стартует первого; мгновенные дети проходят цепочкой
  в том же кадре; упал ребёнок — упала последовательность с его `Error`. Пустая — сразу `Succeeded`.
- `ParallelOperation(params LoadingOperation[] children)` — `Begin` стартует всех; `Tick` тикает идущих; все готовы —
  `Succeeded`; первый упавший — `Failed`. Пустая — сразу `Succeeded`.
- `null` среди детей — `ArgumentNullException` в конструкторе.

### 3.3. Листья

- `ActionOperation(Action action, float weight = 0)` — зовёт действие в `Begin` и сразу готова.
- `FadeOperation(CanvasGroup group, float target, float duration, float weight = 0)` — `alpha` к `target` со скоростью
  «полный диапазон за `duration`» (как `FadeTransition`); `duration ≤ 0` или уже на цели — сразу.
- `LoadSceneOperation(string sceneName, LoadSceneMode mode = Single, float weight = 1)` — нет сцены в Build Settings —
  `Failed` («Scene '…' is not in Build Settings») без загрузки; прогресс — `AsyncOperation.progress / 0.9`; готова на
  `isDone`.

### 3.4. `LoadingRunner`

```csharp
public sealed class LoadingRunner
{
    public bool IsRunning { get; }
    public float Progress { get; }   // прогресс корня; 0 без загрузки
    public float Elapsed { get; }    // секунды с Run по сумме dt
    public bool Run(LoadingOperation root, Action<LoadingResult> done = null); // false — уже идёт
    public void Tick(float dt);
}
```

`done` зовётся ровно один раз, после сброса `IsRunning` — из него можно сразу запустить следующую загрузку.
Мгновенное дерево завершается прямо в `Run`.

## 4. Игра: `ISceneLoader`

```csharp
public interface ISceneLoader
{
    bool IsLoading { get; }
    bool Load(string sceneName, Action<LoadingResult> done = null);  // false — загрузка уже идёт
    bool ReloadActiveScene(Action<LoadingResult> done = null);
}
```

`SceneLoader` (`Scripts/GameSystem/Services`) — `MonoBehaviour` на дочернем объекте `SceneLoader` префаба `[Services]`, регистрирует
его `GameServices` (`Register<ISceneLoader>`). Свой Canvas (Screen Space Overlay, Sort Order 100 — поверх `GameUI`):

```
SceneLoader            — SceneLoader, Canvas, CanvasScaler, GraphicRaycaster
├── Screen             — Image (чёрный, на весь экран), CanvasGroup (затемнение)
└── Progress           — CanvasGroup (видимость полоски)
    ├── Bar            — Image (фон)
    │   └── Fill       — Image (Filled, Horizontal) — значение
    └── Percent        — TMP «42%»
```

Поля: `_screen` (CanvasGroup), `_progress` (CanvasGroup), `_fill` (Image), `_percent` (TMP), `_eventSystem`
(общий `EventSystem` из `[Services]`), `_fadeDuration` (0.25 с), `_progressDelay` (0.5 с). Ограничения — в `OnValidate`.

**Две фазы** (иначе при ошибке экран остался бы чёрным):

1. «Скрыть и загрузить»: `Sequence(блок ввода, Fade(screen → 1), отметка «чёрно», LoadScene(name))`.
2. «Показать» — запускается из `done` первой фазы при любом исходе: `Sequence(спрятать полоску, Fade(screen → 0))`,
   затем ввод возвращается, `IsLoading = false`, ошибка — в `Debug.LogError`, вызвавшему — результат первой фазы.

`Update` тикает раннер на `Time.unscaledDeltaTime` и рисует полоску: видна, когда с момента «чёрно» прошло
`_progressDelay`; значение — прогресс первой фазы (вес есть только у сцены). Для тестов: `Configure(fade, delay)`,
`ScreenAlpha`, `IsProgressVisible`.

**Restart.** `PauseScreen` — кнопка `Restart` между `Resume` и `Quit`, событие `OnRestart`. `GameUI` спрашивает
подтверждение («Restart the level?»), на «Yes» — `ServiceLocator.TryGet<ISceneLoader>` и `ReloadActiveScene()`; меню
паузы остаётся открытым до смены сцены (игра стоит во время затемнения). Пока `IsLoading`, `GameUI` не читает
Pause/Cancel.

## 5. Тесты

- `Loading.Tests` (EditMode, фейковая операция с ручным `Succeed`/`Fail`/прогрессом):
  статусы и `Progress` листа; исключение в `OnBegin`/`OnTick` → `Failed` с текстом и `Exception`; `Succeed` после
  `Fail` не оживляет; повторный `Begin` во время `Running` — исключение, после завершения — заново;
  последовательность: порядок, цепочка мгновенных в одном тике, остановка на ошибке с её текстом, взвешенный прогресс,
  нулевой вес, пустая; параллель: старт всех, готова, когда все, первая ошибка; `ActionOperation`;
  раннер: `Run` во время загрузки — `false`, `done` один раз и после сброса `IsRunning`, перезапуск из `done`,
  `Elapsed`, `Progress`, мгновенное дерево.
- `Loading.Tests.Runtime` (PlayMode): `FadeOperation` — альфа за время, мгновенно при 0; `LoadSceneOperation` —
  неизвестная сцена `Failed` без загрузки; тестовая сцена модуля грузится аддитивно, прогресс доходит до 1.
- `Game.Tests.PlayMode`: `[Services]` регистрирует `ISceneLoader`; неизвестная сцена — `LoadingResult` с ошибкой,
  лог-ошибка, экран проявлен, ввод возвращён; `Load` во время загрузки — `false`; загрузка сцены: экран затемнён до
  смены сцены, после — проявлен, пауза снята; полоска видна при `delay = 0` и не видна при большом; `GameUI`: Restart →
  подтверждение → `ReloadActiveScene` (фейковый загрузчик), «No» — ничего; Esc во время загрузки игнорируется.
- EditMode: префаб `[Services]` — у `SceneLoader` назначены ссылки; `PauseScreen` — кнопка Restart назначена.

## 6. Вне рамок

Отмена загрузки, контекст с типизированными ключами, аддитивные сцены уровня, загрузка ассетов (Addressables),
анимированный экран загрузки. Boot-сцена, главное меню и «не мелькающая» полоска — сделаны в
[Bootstrap.md](Bootstrap.md).
