# 08. Elements: время, кулдауны, таймеры

Модуль `Atomic.Elements` / `Time` — это набор мелких пассивных объектов, которые хранят время и рассылают события об его изменении: кулдауны, таймеры вверх и вниз, секундомеры, циклические периоды и tick-метки. Ни один из них не тикает сам: `Tick(float deltaTime)` вызывает внешний код, обычно жизненный цикл сущности из `Atomic.Entities`. Все контракты собраны композицией из восьми крошечных «source»-интерфейсов, поэтому потребитель зависит ровно от той способности, которая ему нужна, а не от конкретного класса. Платой за эту композицию стала несогласованность реализаций: одно и то же имя метода на разных классах семантически означает разное, и именно это, а не сам API, съедает время на миграции. Эта глава в первую очередь про такие расхождения — и про то, какие формы кода их обходят.

Всё, что ниже, сверено с исходниками в `Assets/Plugins/Atomic/Elements/Scripts/Time` и `.../Scripts/Utils`. Там, где документация упстрима расходится с кодом, в тексте это помечено явно: **прав код**.

---

## Модель

**1. Элементы времени пассивны.** В модуле нет ни одного `MonoBehaviour`, ни одной подписки на `Update`. Единственный `MonoBehaviour` во всей зоне — `internal MainThreadDispatcher`, и он обслуживает не таймеры, а доставку событий из фоновых потоков. Время двигает вызывающий код:

```csharp
weapon.WhenFixedTick(_cooldown.Tick);
```

Отсюда три следствия, которые и были целью дизайна: детерминизм (нет скрытого источника времени), тестируемость (`Tick(0.1f)` в юнит-тесте без Unity-плеймода), и пауза «бесплатно» — просто перестаньте вызывать `Tick`.

**2. Три разные физические модели времени.** Это главное, что нужно держать в голове при выборе типа:

- **Остаток** — `ICooldown`. Нет конечного автомата вообще. Объект всегда «работает»; вы спрашиваете `IsCompleted()` и перезаряжаете через `ResetTime()`. Два `float` плюс четыре события.
- **Конечный автомат** — `ITimer` (`DownTimer`, `UpTimer`), `IStopwatch`, `IPeriod`. Есть состояние (`IDLE`/`PLAYING`/`PAUSED`[/`COMPLETED`]), есть `Start`/`Pause`/`Resume`/`Stop`, и любой переход вне разрешённого состояния **молча ничего не делает**. Девять событий плюс enum.
- **Абсолютный дедлайн** — `ITimestamp` (`FixedTimestamp`). Состояние — один `int` с номером тика окончания. Нет событий, нет `Tick`, нет подписок. Все запросы вычисляются из `Time.fixedTime`. Это то, чем закрывают десятки тысяч одновременных дедлайнов и чем делают rollback/prediction.

**3. Интерфейс фиксирует сигнатуру, но не семантику.** Это самое опасное место модуля. `ITimeSource.ResetTime()` формально один метод, но:

| Тип | `ResetTime()` ведёт к | `GetProgress()` в `PLAYING` |
|---|---|---|
| `Cooldown` | полной длительности | `_time / _duration` — **1 = только перезарядили, 0 = готово** |
| `ThreadSafeCooldown` | полной длительности | `_duration <= 0 ? 0 : _time / _duration` |
| `RandomCooldown` | только меняет длительность (дефект, см. ниже) | проксирует в `Cooldown` |
| `DownTimer` | полной длительности | `1 - time / duration` — **0 = начало, 1 = конец** |
| `UpTimer` | нулю | `currentTime / duration` |
| `Stopwatch` | нулю | нет (`IStopwatch` не наследует `IProgressSource`) |
| `Period` | нулю | `time / duration` |

Код, написанный против `ITimeSource`/`IProgressSource`, при подмене реализации меняет поведение — не компилируемость, а поведение. Выберите одну проектную конвенцию прогресса и оберните всё остальное.

**4. Политика валидации тоже не в интерфейсе.** Одинаковые по сигнатуре сеттеры ведут себя по-разному:

| Член | Отрицательный аргумент |
|---|---|
| `Cooldown.SetTime`, `ThreadSafeCooldown.SetTime` | `throw new ArgumentException` |
| `Cooldown.SetDuration`, `ThreadSafeCooldown.SetDuration` | `Math.Max(0, duration)` — молча зажимается |
| `DownTimer.SetDuration` | `throw new Exception` (именно базовый `Exception`) |
| `DownTimer.SetTime` | `Math.Clamp(time, 0, duration)` — молча зажимается |
| `UpTimer.SetDuration`, `UpTimer.SetTime`, `Period.SetDuration` | `return;` — молча игнорируется |
| `Stopwatch.SetTime` | `Math.Max(time, 0)` |
| `FixedTimestamp.StartFromSeconds`, `StartFromTicks` | `throw new ArgumentOutOfRangeException` |
| `DurationTimestamp.SetDuration` | `throw new Exception` |

Никогда не выводите поведение из интерфейса — откройте файл.

**5. Это классы с полями событий, а не структуры.** Один и тот же экземпляр по ссылке живёт в инсталлере, в контроллере и в презентере. Отсюда: каждая подписка в `Init` обязана быть снята в `Dispose`, и обязательно именованным методом.

**6. События элементов времени — обычные `event Action<float>`, а не `ISignal<T>`.** Поэтому идиома `Subscribe(...).AddTo(composite)` из `Atomic.Elements/Events` на них **не работает**: расширение `Subscribe` объявлено на `ISignal`, а `ICooldown.OnTimeChanged` — plain event. Отписка здесь всегда ручная, через `-=`.

---

## API

Пространство имён всюду `Atomic.Elements`. Файлы — относительно `Assets/Plugins/Atomic/Elements/Scripts/`.

### Source-интерфейсы (`Time/Sources/`)

```csharp
public interface ITimeSource
{
    event Action<float> OnTimeChanged;
    float GetTime();
    void SetTime(float time);
    void ResetTime();
}
```
Берите в потребителе, если нужно читать/писать текущее время и реагировать на его изменение. Помните пункт 3 модели про `ResetTime()`.

```csharp
public interface IDurationSource
{
    event Action<float> OnDurationChanged;
    float GetDuration();
    void SetDuration(float duration);
}
```
`SetDuration` **не** пересчитывает и не зажимает текущее время в `Cooldown`, `DownTimer`, `UpTimer`, `Period`. Единственное исключение — `ThreadSafeCooldown.SetDuration`, где есть `if (_time > _duration) _time = _duration;`.

```csharp
public interface ITickSource
{
    void Tick(float deltaTime);
}
```
Коллизия имён: в `Atomic.Entities` есть **другой** `ITickSource` — с `OnTicked`/`OnFixedTicked`/`OnLateTicked` **и** методами `Tick(float)`, `FixedTick(float)`, `LateTick(float)`. Совпадает не только имя типа, но и член `Tick(float)`, так что один метод удовлетворяет обоим интерфейсам сразу. В файле, где подключены оба namespace, квалифицируйте тип полностью.

```csharp
public interface IStartSource
{
    event Action OnStarted;
    event Action OnStopped;
    bool IsStarted();
    void Start(float time);
    void Start();
    void Stop();
}
```
`Start()` — не перезапуск. Используйте расширение `Restart()`.

```csharp
public interface IPauseSource
{
    event Action OnPaused;
    event Action OnResumed;
    bool IsPaused();
    void Pause();
    void Resume();
}
```
Есть у `ITimer`, `IStopwatch`, `IPeriod`; у `ICooldown` — нет.

```csharp
public interface ICompleteSource
{
    event Action OnCompleted;
    bool IsCompleted();
}
```
`IPeriod` сознательно его **не** наследует: период не завершается, он оборачивается.

```csharp
public interface IProgressSource
{
    event Action<float> OnProgressChanged;
    float GetProgress();
    void SetProgress(float progress);
}
```

```csharp
public interface IStateSource<out T> where T : Enum
{
    event Action<T> OnStateChanged;
    T GetState();
}
```
Ковариантный по `T` при ограничении `where T : Enum`. Потребляйте только закрытым генериком (`IStateSource<TimerState>`): через `IStateSource<Enum>` каждое `GetState()` и каждый вызов `OnStateChanged` боксирует значение.

### Кулдауны (`Time/Cooldown/`, `Utils/`)

```csharp
public interface ICooldown :
    IDurationSource, ITimeSource, IProgressSource, ICompleteSource, ITickSource
{
    bool IsPlaying();
}
```
`IsPlaying()` есть в коде, но в `ICooldown.md`/`Cooldown.md`/`RandomCooldown.md` его нет (документирован только в `ThreadSafeCooldown.md`). Если в проекте была своя реализация `ICooldown` — она перестанет компилироваться, пока не добавите `IsPlaying()`.

```csharp
[Serializable]
public class Cooldown : ICooldown
{
    public event Action<float> OnDurationChanged;
    public event Action<float> OnTimeChanged;
    public event Action<float> OnProgressChanged;
    public event Action OnCompleted;

    public float Duration    { get; set; }   // set => SetDuration(value)
    public float CurrentTime { get; set; }   // set => SetTime(value)
    public float Progress    { get; set; }   // get => GetProgress(), set => SetProgress(value)

    public Cooldown(float duration);                 // : this(duration, duration)
    public Cooldown(float duration, float current);  // _duration = Max(0, d); _time = Min(current, _duration)

    public static implicit operator Cooldown(float duration);
    public static implicit operator Cooldown(int duration);

    public bool  IsCompleted();        // _time <= 0
    public bool  IsPlaying();          // _time > 0
    public float GetProgress();        // _time / _duration — БЕЗ защиты от деления на ноль
    public float GetDuration();
    public float GetTime();
    public void  ResetTime();          // SetTime(_duration)
    public void  Tick(float deltaTime);
    public void  SetTime(float time);          // throw ArgumentException при time < 0
    public void  SetDuration(float duration);
    public void  SetProgress(float progress);
    public override string ToString();
}
```
Рабочая лошадь модуля. Параметрless-конструктора в коде **нет** (вопреки `Cooldown.md`): `new Cooldown()` не компилируется. Поля `_duration`/`_time` помечены `[SerializeField]` внутри `#if UNITY_5_3_OR_NEWER`, поэтому `[SerializeField] private Cooldown _cd;` в инсталлере работает через десериализацию полей, минуя конструктор. Сериализуется и `_time`, то есть частично истёкший кулдаун переживает инспектор и сейв.

Точное поведение `Tick`:

```csharp
public void Tick(float deltaTime)
{
    if (_time == 0)
        return;

    _time = Math.Max(0, _time - deltaTime);

    this.OnTimeChanged?.Invoke(_time);
    this.OnProgressChanged?.Invoke(this.GetProgress());

    if (_time <= 0)
        this.OnCompleted?.Invoke();
}
```
Ранний выход по `_time == 0` даёт ровно один `OnCompleted` на одну перезарядку, а истёкший кулдаун стоит одно сравнение в кадр.

```csharp
[Serializable]
public class RandomCooldown : ICooldown
{
    // все четыре события — add/remove-форвардинг во вложенный Cooldown
    public RandomCooldown(float minDuration, float maxDuration, IRandomizer randomizer);

    public float GetDuration();
    public void  SetDuration(float duration);
    public float GetTime();
    public void  SetTime(float time);
    public void  ResetTime();   // _cooldown.SetDuration(_randomizer.Range(_min, _max)) — и больше ничего
    public float GetProgress();
    public void  SetProgress(float progress);
    public bool  IsCompleted();
    public void  Tick(float deltaTime);
    public bool  IsPlaying();

    public static Builder StartBuild();

    public struct Builder
    {
        public Builder WithMinDuration(float minDuration);
        public Builder WithMaxDuration(float maxDuration);
        public Builder WithDuration(float minDuration, float maxDuration);
        public Builder WithRandomizer(IRandomizer randomizer);
        public RandomCooldown Build();
    }
}
```
Три расхождения с `RandomCooldown.md`: конструктор требует **третий** аргумент `IRandomizer` (документированный `new RandomCooldown(2f, 5f)` не компилируется); `Builder` вообще не документирован; `ResetTime()` сломан (разбор в «Грабли»). Каждый член — однострочный форвард во вложенный `Cooldown`, и именно поэтому дефект `ResetTime()` обходится снаружи.

```csharp
public sealed class ThreadSafeCooldown : ICooldown, MainThreadDispatcher.IFlushable
{
    public ThreadSafeCooldown(float duration);
    public ThreadSafeCooldown(float duration, float current);

    public float Duration    { get; set; }   // get: lock (_lock) return _duration;
    public float CurrentTime { get; set; }
    public float Progress    { get; set; }

    public bool  IsCompleted();   // lock
    public bool  IsPlaying();     // lock
    public void  Tick(float deltaTime);
    public void  ResetTime();     // SetTime(_duration) — ЕДИНСТВЕННОЕ чтение без lock
    public float GetDuration();   // через locked-property
    public void  SetDuration(float duration);
    public float GetTime();       // через locked-property
    public void  SetTime(float time);         // throw ArgumentException при time < 0
    public float GetProgress();   // через locked-property
    public void  SetProgress(float progress);
    public override string ToString();        // тоже берёт _lock

    void MainThreadDispatcher.IFlushable.Flush();   // явная реализация интерфейса
}
```
Не `[Serializable]`, нет implicit-операторов от `float`/`int` — только явная конструкция. Мутаторы меняют состояние под `_lock`, выставляют dirty-флаги и вызывают `MainThreadDispatcher.MarkDirty(this)`; события рассылает `Flush()` на главном потоке. `Flush()` — явная реализация, из вашего кода его не вызвать без приведения к `internal`-типу.

Три поведенческих отличия от `Cooldown`, а не только блокировки:
- `GetProgressInternal()` защищён: `_duration <= 0 ? 0 : _time / _duration`. У `Cooldown` защиты нет — две реализации одного интерфейса расходятся на нулевой длительности.
- `Tick(0f)` — no-op (`if (Math.Abs(newTime - _time) <= float.Epsilon) return;`), тогда как `Cooldown.Tick(0f)` поднимает `OnTimeChanged` + `OnProgressChanged`.
- `SetTime`/`SetProgress` выставляют `_completed = true`, когда попадают в ноль, то есть `OnCompleted` может прийти из обычного сеттера. У `Cooldown` `OnCompleted` приходит только из `Tick`.

```csharp
internal sealed class MainThreadDispatcher : MonoBehaviour
{
    public interface IFlushable { void Flush(); }

    public static bool IsMainThread { get; }   // ManagedThreadId == _mainThreadId
    public static void MarkDirty(IFlushable flushable);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize();   // создаёт DontDestroyOnLoad GameObject "[MainThreadDispatcher]"
}
```
Класс `internal`, из игровых сборок недоступен. Публичного `Flush()` нет — строка `MainThreadDispatcher.Flush();` из `ThreadSafeCooldown.md` не существует в коде. Писать код слива событий не нужно и невозможно: скрытый `DontDestroyOnLoad`-объект создаётся `BeforeSceneLoad` и сливает dirty-объекты в своём `Update()`. `IsMainThread` в кодовой базе не используется нигде — мёртвый член.

```csharp
public interface IRandomizer
{
    int   Range(int from, int to);
    float Range(float from, float to);
}

public sealed class UnityRandomizer : IRandomizer
{
    public static readonly UnityRandomizer Instance = new();
    public int   Range(int from, int to)     => Random.Range(from, to);
    public float Range(float from, float to) => Random.Range(from, to);
}
```
`Utils/IRandomizer.cs`. Детерминированной реализации в поставке **нет**: `DefaultRandomizer` на `ThreadLocal<System.Random>` в этом файле целиком закомментирован. Сидированный рандомайзер для тестов и для сетевой симуляции придётся написать самим — интерфейс для этого и существует.

### Таймеры (`Time/Timer/`)

```csharp
public interface ITimer :
    IStartSource, IPauseSource, ICompleteSource, IStateSource<TimerState>,
    ITimeSource, IDurationSource, IProgressSource, ITickSource
{
}

public enum TimerState { IDLE = 0, PLAYING = 1, PAUSED = 2, COMPLETED = 3 }
```
Идентификаторы в верхнем регистре с явными номерами (стабильны при сериализации и в сети). `TimerState.md` в оглавлении пишет `Idle`/`Playing`/`Paused`/`Completed` — это опечатка документации.

```csharp
[Serializable]
public class DownTimer : ITimer
{
    public event Action OnStarted, OnStopped, OnPaused, OnResumed, OnCompleted;
    public event Action<TimerState> OnStateChanged;
    public event Action<float> OnTimeChanged, OnDurationChanged, OnProgressChanged;

    public TimerState State      { get; }
    public float      Duration   { get; set; }
    public float      CurrentTime{ get; set; }
    public float      Progress   { get; set; }

    public DownTimer();
    public DownTimer(float duration);
    public static implicit operator DownTimer(float duration);
    public static implicit operator DownTimer(int duration);

    public TimerState GetState();
    public bool IsIdle();
    public bool IsStarted();
    public bool IsPaused();
    public bool IsCompleted();
    public float GetDuration();
    public float GetTime();

    public void Start();              // Start(this.duration)
    public void Start(float time);    // no-op, если state не IDLE и не COMPLETED
    public void Pause();              // no-op, если не PLAYING
    public void Resume();             // no-op, если не PAUSED
    public void Stop();               // no-op, если IDLE; иначе time = 0, state = IDLE
    public void Tick(float deltaTime);// no-op, если не PLAYING
    public void SetDuration(float duration);  // throw Exception при duration < 0
    public void SetTime(float time);          // Math.Clamp(time, 0, duration)
    public void SetProgress(float progress);
    public void ResetTime();          // SetTime(this.duration)
    public float GetProgress();
}
```
Берите, когда время — часть геймплейной логики: раунд, бафф, способность с паузой. Обратите внимание на `IsIdle()`: его нет ни в `IStartSource`, ни в `IStateSource<T>`, он доступен только через конкретный класс — и это единственный прямой способ спросить «подействует ли сейчас `Start()`».

`UpTimer` — зеркало `DownTimer` с тем же `ITimer`, но **публичная поверхность другая**:

```csharp
[Serializable]
public class UpTimer : ITimer
{
    public TimerState CurrentState { get; }      // у DownTimer это State
    public float      Time         { get; set; } // у DownTimer это CurrentTime
    public float      Duration     { get; set; }
    public float      Progress     { get; set; }

    public void Start();                      // Start(0), у DownTimer — Start(duration)
    public void Tick(float deltaTime);        // currentTime = Math.Min(duration, currentTime + deltaTime)
    public void SetDuration(float duration);  // return; при duration < 0
    public void SetTime(float time);          // return; при time < 0, затем Clamp
    public void ResetTime();                  // SetTime(0), у DownTimer — SetTime(duration)
    public float GetProgress();               // currentTime / duration
}
```
Приватные поля тоже называются иначе (`currentTime`, `currentState` против `time`, `state` у `DownTimer`, и `_time`, `_duration` у `Cooldown` — три схемы на четыре соседних класса). Пишите код через `GetState()`/`GetTime()`, если направление счёта может поменяться.

### Секундомер (`Time/Stopwatch/`)

```csharp
public interface IStopwatch :
    IStartSource, IPauseSource, ITimeSource, IStateSource<StopwatchState>, ITickSource
{
}

public enum StopwatchState { IDLE = 0, PLAYING = 1, PAUSED = 2 }
```
Нет `IDurationSource`, нет `IProgressSource`, нет `ICompleteSource`: у секундомера нет конца, поэтому этих способностей просто нет в контракте, и `stopwatch.IsCompleted()` не скомпилируется. `IStopwatch.md` в одном месте называет состояние `RUNNING` — настоящий идентификатор `PLAYING`.

```csharp
[Serializable]
public class Stopwatch : IStopwatch
{
    public event Action OnStarted, OnStopped, OnPaused, OnResumed;
    public event Action<float> OnTimeChanged;
    public event Action<StopwatchState> OnStateChanged;

    public StopwatchState State { get; }
    public float          Time  { get; set; }

    public StopwatchState GetState();
    public bool IsStarted();
    public bool IsPaused();
    public bool IsIdle();
    public float GetTime();

    public void Start();            // Start(0)
    public void Start(float time);  // no-op, если не IDLE; time = Math.Max(0, time)
    public void Pause();
    public void Resume();
    public void Stop();             // time = 0, state = IDLE
    public void Tick(float deltaTime);
    public void SetTime(float time);
    public void ResetTime();        // SetTime(0)
}
```
Единственный тип в зоне, который вообще не трогает `UnityEngine` — в `Stopwatch.cs` нет ни `using UnityEngine`, ни `[SerializeField]`. Из этого следует и то, что `[Serializable]`-пометка здесь ничего не сериализует: инспектор покажет пустой блок. Конструктора с длительностью нет (нечего задавать), implicit-операторов нет. `Tick` не зажимает время:

```csharp
public void Tick(float deltaTime)
{
    if (this.state == StopwatchState.PLAYING)
    {
        this.time += deltaTime;
        this.OnTimeChanged?.Invoke(this.time);
    }
}
```

### Период (`Time/Period/`)

```csharp
public interface IPeriod :
    IStartSource, IPauseSource, IStateSource<PeriodState>,
    ITimeSource, IProgressSource, IDurationSource, ITickSource
{
    /// <summary>Raised when the period completes and starts over.</summary>
    event Action OnPeriod;
}

public enum PeriodState { IDLE = 0, PLAYING = 1, PAUSED = 2 }
```
`OnPeriod` вместо наследования `ICompleteSource`, и в enum нет `COMPLETED`. «Заканчивается один раз» и «оборачивается навсегда» — разная семантика, и она закодирована в типе: написать `period.IsCompleted()` просто нельзя.

```csharp
[Serializable]
public class Period : IPeriod
{
    public event Action OnStarted, OnStopped, OnPaused, OnResumed, OnPeriod;
    public event Action<PeriodState> OnStateChanged;
    public event Action<float> OnTimeChanged, OnProgressChanged, OnDurationChanged;

    public PeriodState State    { get; }
    public float       Duration { get; set; }
    public float       Time     { get; set; }
    public float       Progress { get; set; }

    public Period();
    public Period(float duration);
    public static implicit operator Period(float duration);
    public static implicit operator Period(int duration);

    public PeriodState GetState();
    public bool IsStarted();
    public bool IsPaused();
    public bool IsIdle();
    public float GetDuration();
    public float GetTime();

    public void Start();              // Start(0)
    public void Start(float time);    // no-op, если не IDLE; time = Math.Clamp(time, 0, duration), БЕЗ OnTimeChanged
    public void Pause();
    public void Resume();
    public void Stop();               // no-op, если IDLE; time = 0, state = IDLE
    public void Tick(float deltaTime);
    public void SetDuration(float duration);  // return; при duration < 0
    public void SetTime(float time);          // поднимает ТОЛЬКО OnTimeChanged
    public float GetProgress();
    public void SetProgress(float progress);
    public void ResetTime();          // SetTime(0)
}
```
Весь pause/stop/query-набор (`IsIdle()`, `Pause()`, `Resume()`, `GetState()`, …) есть и используется — в digest-обзорах его часто теряют, потому что `Periods.md` показывает только `Start`/`Tick`.

Устройство цикла:

```csharp
public void Tick(float deltaTime)
{
    if (this.state != PeriodState.PLAYING)
        return;

    this.time = Math.Min(this.time + deltaTime, this.duration);
    this.OnTimeChanged?.Invoke(this.time);

    float progress = this.time / this.duration;
    this.OnProgressChanged?.Invoke(progress);

    if (progress >= 1)
        this.Complete();
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void Complete()
{
    this.OnPeriod?.Invoke();
    this.SetTime(this.time - this.duration);
}
```
`Math.Min` обрезает перелёт **до** `Complete()`, поэтому `time - duration` всегда ровно 0: подкадровый остаток выбрасывается. Это подтверждено тестом `PeriodTests.OnCycle` (`duration = 4`, двадцать тиков по `0.2` → `GetTime() == 0`, `GetProgress() == 0`). Практические последствия: период отстаёт до одного `deltaTime` за цикл и физически не может сработать дважды за один длинный кадр. Для фазово-точной периодики (например, стабильные 20 Гц при переменном fps) накапливайте остаток сами.

### Tick-метки (`Time/Timestamp/`)

```csharp
public interface ITimestamp
{
    int   EndTick          { get; }
    int   RemainingTicks   { get; }
    float RemainingSeconds { get; }

    void  StartFromSeconds(float seconds);
    void  StartFromTicks(int ticks);
    void  ResetEndTick();
    float GetProgress(float duration);
    bool  IsIdle();
    bool  IsPlaying();
    bool  IsExpired();
}
```
Документация устарела: `ITimestamp.md` и `FixedTimestamp.md` пишут `RemainingTime` и `Stop()`. В коде — `RemainingSeconds` и `ResetEndTick()`; метода `Stop()` у `FixedTimestamp` нет вообще, и примеры из этих двух файлов не компилируются.

```csharp
public interface IDurationTimestamp : ITimestamp, IDurationSource
{
    void  StartFromDuration();
    float GetProgress();
}
```
Ни одного `.md` на этот интерфейс не существует. Именно он нужен для прогресс-бара над таймстемпом: `GetProgress()` без аргумента.

```csharp
#if UNITY_5_3_OR_NEWER
public class FixedTimestamp : ITimestamp
{
    public int   EndTick          => _endTick;
    public int   RemainingTicks   => this.GetRemainingTicks();
    public float RemainingSeconds => this.GetRemainingSeconds();

    public FixedTimestamp(int endTick = -1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StartFromSeconds(float seconds);   // throw ArgumentOutOfRangeException при seconds < 0
    public void StartFromTicks(int ticks);         // StartFromSeconds(ticks * Time.fixedDeltaTime)
    public void ResetEndTick();                    // _endTick = -1

    public float GetProgress(float duration);      // 1 - GetRemainingSeconds() / duration, БЕЗ защиты от нуля
    public bool  IsIdle();                         // _endTick == -1
    public bool  IsPlaying();                      // _endTick > 0 && _endTick >  CurrentTick()
    public bool  IsExpired();                      // _endTick > 0 && _endTick <= CurrentTick()
}
#endif
```
Весь файл — внутри `#if UNITY_5_3_OR_NEWER`, потому что опирается на `Time.fixedTime`/`Time.fixedDeltaTime`. Состояние — один `int`, нет ни событий, ни `Tick`. Для нескольких тысяч одновременных дедлайнов это единственный вариант без per-frame работы.

```csharp
// Decorator
public class DurationTimestamp : IDurationTimestamp
{
    public event Action<float> OnDurationChanged;

    public DurationTimestamp(ITimestamp timestamp, float durationSeconds);

    public void  StartFromDuration();   // _timestamp.StartFromSeconds(_duration)
    public float GetProgress();         // _timestamp.GetProgress(_duration)
    public float GetDuration();
    public void  SetDuration(float duration);   // throw Exception при duration < 0
    // остальные члены ITimestamp — один-к-одному форвард в _timestamp
}
```
Тоже без документации. Образцовый декоратор: все девять членов `ITimestamp` — один-к-одному форвард в `_timestamp`, а всё новое (поле `_duration`, тройка `IDurationSource` плюс `StartFromDuration()` и `GetProgress()` без аргумента) живёт в декораторе.

### Расширения

```csharp
// Time/Extensions.cs — static partial class Extensions
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Restart(this IStartSource source, float time);   // Stop(); Start(time);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Restart(this IStartSource source);               // Stop(); Start();

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ObserveTimeChanged(this ITimeSource source, Action<float> action);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsNotCompleted(this ICompleteSource source);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsIdleOrExpired(this ITimestamp timestamp);
```

```csharp
// Time/Timestamp/Extensions.cs — тот же static partial class Extensions, БЕЗ MethodImpl
public static DurationTimestamp WithDuration(this ITimestamp timestamp, float durationSeconds) =>
    new(timestamp, durationSeconds);
```
`Extensions` — один `static partial class` на namespace, расщеплённый по файлам, а не `CooldownExtensions`/`TimerExtensions`. Из шести методов задокументированы только два `Restart` (`ExtensionsIStartSource.md`); `ObserveTimeChanged`, `IsNotCompleted`, `IsIdleOrExpired` и `WithDuration` не упоминаются в документации нигде.

---

## Идиомы

### Типизированное поле длительности через implicit-оператор

```csharp
[SerializeField] private Cooldown _cooldown = 0.5f;
[SerializeField] private Cooldown _gameCountdown = 60.0f;

DownTimer timer  = 30;
UpTimer   uptime = 30f;
Period    period = 10f;
```
`Cooldown`, `DownTimer`, `UpTimer` и `Period` объявляют `implicit operator T(float)` и `implicit operator T(int)`. Объявление поля читается как «длительность», то есть как единственное, что реально настраивают, а тип при этом несёт всю событийную поверхность. Для `Cooldown` это ещё и типовая миграционная цель.

Антипаттерн — то, что обычно и переносят из старого проекта:

```csharp
// ПЛОХО
[SerializeField] private float _cooldownSeconds = 0.5f;
private float _cooldownRemaining;
// ... _cooldownRemaining -= dt; if (_cooldownRemaining <= 0) { ... }
```
Теряются события, прогресс, инспекторные кнопки, и это переизобретается в каждой фиче заново.

### Tick через жизненный цикл сущности, method group вместо лямбды

```csharp
public sealed class WeaponInstaller : MonoEntityInstaller<IWeaponEntity>
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Cooldown _cooldown = 0.5f;

    public override void Install(IWeaponEntity weapon)
    {
        weapon.AddFireAction(new InlineAction(() =>
        {
            if (!_cooldown.IsCompleted())
                return;

            // TODO: логика выстрела

            _cooldown.ResetTime();
        }));

        weapon.WhenFixedTick(_cooldown.Tick);
    }
}
```
`weapon.WhenFixedTick(_cooldown.Tick)` передаёт группу методов прямо в параметр `Action<float>`: ни замыкания, ни захвата `this` — один делегат, созданный однажды в момент установки. Время двигается синхронно с жизненным циклом сущности и останавливается вместе с ней.

```csharp
// ПЛОХО
private void Update() { _cooldown.Tick(Time.deltaTime); }
```
Не синхронизировано с жизненным циклом, продолжает идти на выключенной сущности и тащит состояние времени на `MonoBehaviour`.

### Проверил — сделал — перезарядил, двумя отдельными подписками

```csharp
entity.AddFireCooldown(new Cooldown(_fireCooldown));
entity.AddFireRequest(new Request<IGameEntity>());
entity.AddFireEvent(new Event<IGameEntity>());

entity.WhenFixedTick(_ =>
{
    if (HealthUseCase.IsAlive(entity) &&
        entity.GetFireCooldown().IsCompleted() &&
        entity.GetFireRequest().Consume(out IGameEntity target))
    {
        CombatUseCase.DealDamage(entity, target);
        entity.GetFireCooldown().ResetTime();
        entity.GetFireEvent().Invoke(target);
    }
});

entity.WhenFixedTick(entity.GetFireCooldown().Tick);
```
Порядок в `&&` не случаен: сначала самые дешёвые предикаты, побочный `Consume` — последним, чтобы заявка списывалась только тогда, когда её реально обслужили. Важнее другое: **решение и продвижение времени — две разные подписки**. Кулдаун тикает даже тогда, когда сущность мертва или заявки нет, поэтому после возрождения он не «залипает» с момента смерти.

```csharp
// ПЛОХО: решение и тик сращены — у мёртвой/простаивающей сущности кулдаун замерзает
entity.WhenFixedTick(dt =>
{
    var cd = entity.GetFireCooldown();
    if (!CanFire(entity)) return;
    cd.Tick(dt);
    if (cd.IsCompleted()) { Fire(entity); cd.ResetTime(); }
});
```

> В `Docs/BestPractices/UsingCooldownInGameMechanics.md` этот пример написан как `new BaseEvent<IGameEntity>()` и `new BaseRequest<IGameEntity>()`. **Таких типов в репозитории нет** (`grep -rn BaseEvent --include=*.cs` даёт ноль попаданий). Реальные реализации — `Atomic.Elements.Event` / `Event<T>` (`Elements/Scripts/Events/Event.cs`) и `Atomic.Elements.Request` / `Request<T>` (`Elements/Scripts/Requests/Request.cs`).

### Полный инсталлер временного поля

```csharp
public sealed class GameContextInstaller : MonoEntityInstaller<IGameContext>
{
    [SerializeField]
    private Cooldown _gameCountdown = 60.0f; // сессия на 60 секунд

    public override void Install(IGameContext context)
    {
        context.AddGameCountdown(_gameCountdown);
        context.AddGameOverEvent(new Event());
        context.AddBehaviour<GameCountdownController>();
    }
}
```
Это та форма, в которую переезжает старый `SceneContextInstaller`. Инсталлер только связывает данные и поведения, логики в нём нет. `AddGameCountdown`/`AddGameOverEvent` — сгенерированные типизированные аксессоры, никаких строковых или целочисленных ключей на месте вызова. `AddBehaviour<T>()` объявлен как `where T : IEntityBehaviour, new()`, поэтому у контроллера должен быть публичный конструктор без параметров.

### Типизированный контроллер: `IEntityInit<T>` кэширует, `IEntityFixedTick` работает

```csharp
public sealed class CoinSpawnController : IEntityInit<IGameContext>, IEntityFixedTick
{
    private ICooldown _cooldown;

    public void Init(IGameContext context)
    {
        _cooldown = context.GetCoinSpawnCooldown();
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        _cooldown.Tick(deltaTime);
        if (_cooldown.IsCompleted())
        {
            this.SpawnCoin();
            _cooldown.ResetTime();
        }
    }

    private void SpawnCoin() { /* ... */ }
}
```
Поиск по контексту происходит один раз в `Init` и кладётся в поле — в `FixedTick` ноль словарных обращений за кадр. Поле типизировано **интерфейсом** `ICooldown`, а не классом, поэтому инсталлер может подменить `RandomCooldown` или `ThreadSafeCooldown`, не трогая контроллер. `IEntityInit<IGameContext>` даёт доступ к типу контекста без каста: дефолтная реализация `IEntityInit.Init(IEntity)` сама проверяет тип и бросает подробный `InvalidCastException`.

```csharp
// ПЛОХО
public void FixedTick(IEntity entity, float deltaTime)
{
    Cooldown cd = ((IGameContext) entity).GetCoinSpawnCooldown();  // поиск каждый кадр + привязка к классу
    cd.Tick(deltaTime);
}
```

### Обратный отсчёт, который перестаёт тикать в нуле

```csharp
public sealed class GameCountdownController : IEntityInit<IGameContext>, IEntityFixedTick
{
    private ICooldown _countdown;
    private IEvent _overEvent;

    public void Init(IGameContext context)
    {
        _countdown = context.GetGameCountdown();
        _overEvent = context.GetGameOverEvent();
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        if (_countdown.IsCompleted())
            _overEvent.Invoke();
        else
            _countdown.Tick(deltaTime);
    }
}
```
Оба коллаборанта разрешены один раз и закэшированы интерфейсами. `if/else` означает, что `Tick` не вызывается на истёкшем кулдауне, а условие завершения — читаемая ветка, а не подписка, которую надо не забыть снять. Учтите: как написано, `_overEvent` переподнимается **каждый кадр** после истечения. Если нужно ровно один раз — подпишитесь на `OnCompleted` или поставьте флаг.

### Презентер: именованный обработчик, симметричные `Init`/`Dispose`

```csharp
public class CountdownPresenter : IEntityInit<IUIContext>, IEntityDispose
{
    private ICooldown _countdown;
    private CountdownView _view;

    public void Init(IUIContext context)
    {
        _view = context.GetGameCountdownView();
        _countdown = GameContext.Instance.GetGameCountdown();
        _countdown.ObserveTimeChanged(this.OnTimeChanged);   // сразу отрисовать + подписаться
    }

    public void Dispose(IEntity entity)
    {
        _countdown.OnTimeChanged -= this.OnTimeChanged;
    }

    private void OnTimeChanged(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        _view.SetTime($"{minutes:00}:{seconds:00}");
    }
}
```
Обработчик — **именованный метод**, и только поэтому `-=` в `Dispose` действительно отцепляет его. `IEntityInit` и `IEntityDispose` реализованы парой, так что подписка и отписка структурно симметричны. View пассивный: ему сообщают готовую строку, решений он не принимает.

```csharp
// ПЛОХО
public void Init(IUIContext context)
{
    _countdown.OnTimeChanged += t => _view.SetTime(t.ToString());
    // снять такую подписку невозможно -> утечка и обновление мёртвого view
}
```

> Если презентер висит на `DownTimer`/`UpTimer`/`Period`, одного `OnTimeChanged` **недостаточно**: `Stop()` во всех трёх классах пишет `this.time = 0;` напрямую и поднимает только `OnStateChanged` + `OnStopped`. View останется со значением, которое было до `Stop()`. Подписывайтесь дополнительно на `OnStopped` (или на `OnStateChanged`) и перечитывайте `GetTime()`:

```csharp
_timer.OnStopped += this.OnStopped;
// ...
private void OnStopped() => this.OnTimeChanged(_timer.GetTime());
```

### `Restart()` вместо `Stop()` + `Start()`

```csharp
IStartSource countdown = timer;

countdown.Restart();     // Stop(); Start();  — от штатного начального времени
countdown.Restart(5f);   // Stop(); Start(5f);
```
`Start()` у `DownTimer`/`UpTimer` — no-op во всех состояниях кроме `IDLE` и `COMPLETED`, у `Stopwatch` и `Period` — только `IDLE`. То есть `Start()` на работающем или приостановленном объекте **молча ничего не делает** и ошибки не даёт. `Restart()` объявлен на `IStartSource`, поэтому работает одинаково для таймеров, секундомера и периода.

```csharp
// ПЛОХО
timer.Start();   // тихо проигнорируется, если timer в PLAYING или PAUSED
```

### Таймстемп для массовых дедлайнов и для предсказания

```csharp
public sealed class BuffController : IEntityInit<IGameEntity>, IEntityFixedTick
{
    private readonly ITimestamp _timestamp = new FixedTimestamp();

    public void Init(IGameEntity entity) => _timestamp.StartFromSeconds(5f);

    public void FixedTick(IEntity entity, float deltaTime)
    {
        if (_timestamp.IsExpired())
            _timestamp.ResetEndTick();
    }
}
```
Ничего не тикается и ничего не подписывается: всё состояние — один `int` с абсолютным тиком окончания, любой запрос выводится из `Time.fixedTime`. Отсюда O(1) памяти на дедлайн, нулевая работа на кадр для тысяч экземпляров, отсутствие проблем с порядком событий и тривиальные снапшот/rollback. Объявляйте поле как `ITimestamp`, чтобы можно было подставить реализацию на симулированных часах.

```csharp
// ПЛОХО: по Cooldown на каждый снаряд/бафф — тысячи тикаемых объектов
// по четыре поля-делегата каждый там, где достаточно int.
```

### Декорирование таймстемпа длительностью

```csharp
IDurationTimestamp reload = new FixedTimestamp().WithDuration(2.5f);

reload.StartFromDuration();
float fill = reload.GetProgress();   // аргумент не нужен
```
`FixedTimestamp` остаётся одним `int`, а UI получает `GetProgress()` без аргумента. Декоратор форвардит каждый член `ITimestamp` один-к-одному, поэтому его можно отдать куда угодно, где ждут `ITimestamp`. Расширение `WithDuration` — точка композиции: имя `DurationTimestamp` в коде вызова не фигурирует.

### Fluent-builder на структуре

```csharp
RandomCooldown cd = RandomCooldown.StartBuild()
    .WithDuration(2f, 5f)
    .WithRandomizer(UnityRandomizer.Instance)
    .Build();
```
`Builder` — `struct`, и каждый `WithXxx` возвращает `this` **по значению**, так что вся цепочка настройки живёт на стеке; куча задействуется только в `Build()`. Каждый `WithXxx` валидирует жадно, а `Build()` перепроверяет всё ещё раз (`_randomizer == null`, `_minDuration <= 0`, `_maxDuration <= 0`, `_maxDuration < _minDuration`) — невалидный кулдаун существовать не может. Поскольку возврат по значению, цепочку нельзя разрывать без переприсваивания:

```csharp
// ПЛОХО: первое звено потеряно, Build() бросит InvalidOperationException
var b = RandomCooldown.StartBuild();
b.WithDuration(2f, 5f);                     // результат выброшен
b.WithRandomizer(UnityRandomizer.Instance); // результат выброшен
RandomCooldown cd = b.Build();
```

Это та форма, которую стоит копировать для собственных настраиваемых элементов проекта.

### Способности композицией, с пустым телом интерфейса

```csharp
public interface ITimer :
    IStartSource, IPauseSource, ICompleteSource, IStateSource<TimerState>,
    ITimeSource, IDurationSource, IProgressSource, ITickSource
{
}

public interface IStopwatch :
    IStartSource, IPauseSource, ITimeSource, IStateSource<StopwatchState>, ITickSource
{
}
```
Разница между таймером и секундомером выражена **только** списком способностей. У секундомера нет длительности, прогресса и завершения — соответствующие интерфейсы просто отсутствуют, и `stopwatch.IsCompleted()` не компилируется. Потребители при этом зависят от самого узкого куска (`ICompleteSource`, `IProgressSource`, `ITickSource`), а не от `ITimer`, и поэтому переиспользуются между кулдаунами, таймерами и периодами. Это тот образец, по которому нужно описывать собственные контракты проекта.

```csharp
// ПЛОХО
public interface ITimer
{
    // один жирный интерфейс с девятью событиями и восемнадцатью методами:
    // потребители зависят от всего, а секундомеру приходится
    // реализовывать IsCompleted()/GetDuration() и возвращать мусор.
}
```

И отдельно — почему `IPeriod` не наследует `ICompleteSource`:

```csharp
// ПЛОХО
public interface IPeriod : ITimer { bool Loop { get; set; } }
// булев флаг, молча меняющий смысл OnCompleted и IsCompleted()
```

### Переэкспорт событий через add/remove

```csharp
public event Action OnCompleted
{
    add    => _cooldown.OnCompleted += value;
    remove => _cooldown.OnCompleted -= value;
}

public event Action<float> OnTimeChanged
{
    add    => _cooldown.OnTimeChanged += value;
    remove => _cooldown.OnTimeChanged -= value;
}
```
Так `RandomCooldown` удовлетворяет события `ICooldown`: форвардит аксессоры во вложенный `Cooldown` вместо того, чтобы держать свои делегаты и перевыбрасывать. Нет дублирующего хранилища делегатов, нет лишнего прыжка вызова, нет риска рассинхрона. Единственно правильная форма для любого декоратора над элементом с событиями.

```csharp
// ПЛОХО
public event Action OnCompleted;
public RandomCooldown(/*...*/) { _cooldown.OnCompleted += () => OnCompleted?.Invoke(); }
// удваивает хранилище делегатов, добавляет прыжок вызова
// и навсегда течёт подпиской на внутренний объект
```

### Снимок состояния и обработчиков под блокировкой, вызов — снаружи

```csharp
void MainThreadDispatcher.IFlushable.Flush()
{
    float duration, time, progress;
    bool durationDirty, timeDirty, progressDirty, completed;

    Action<float> durationHandler, timeHandler, progressHandler;
    Action completedHandler;

    lock (_lock)
    {
        duration = _duration;
        time = _time;
        progress = GetProgressInternal();

        durationDirty = _durationDirty;
        timeDirty = _timeDirty;
        progressDirty = _progressDirty;
        completed = _completed;

        _durationDirty = false;
        _timeDirty = false;
        _progressDirty = false;
        _completed = false;

        durationHandler = OnDurationChanged;
        timeHandler = OnTimeChanged;
        progressHandler = OnProgressChanged;
        completedHandler = OnCompleted;
    }

    if (durationDirty)  durationHandler?.Invoke(duration);
    if (timeDirty)      timeHandler?.Invoke(time);
    if (progressDirty)  progressHandler?.Invoke(progress);
    if (completed)      completedHandler?.Invoke();
}
```
Ссылки на делегаты копируются под блокировкой (делегаты иммутабельны, так что копия — корректный снимок), а пользовательские колбэки выполняются уже после её освобождения: ни один подписчик не может ни заблокировать кулдаун, ни застопорить `Tick` на другом потоке. Dirty-флаги читаются и сбрасываются в одной критической секции, поэтому уведомление нельзя ни потерять, ни удвоить. Это шаблон для любого потокобезопасного реактивного элемента, который команда будет писать сама.

```csharp
// ПЛОХО
lock (_lock)
{
    if (_timeDirty) { OnTimeChanged?.Invoke(_time); _timeDirty = false; }
}
// вызов произвольного пользовательского кода под блокировкой -> заготовка дедлока
```

### Сеттеры ходят через валидирующий метод, а не в поле

```csharp
public float Duration    { get => _duration;          set => this.SetDuration(value); }
public float CurrentTime { get => _time;              set => this.SetTime(value); }
public float Progress    { get => this.GetProgress(); set => this.SetProgress(value); }
```
Каждый сеттер делегирует в `SetXxx`, который владеет зажимом, проверкой на изменение и рассылкой событий. Мутировать поле можно ровно одним путём, поэтому инспектор (`[ShowInInspector]` от Odin), свойство и метод интерфейса не могут разойтись. Геттеры читают поле напрямую — лишней косвенности на горячем пути нет.

```csharp
// ПЛОХО
public float Duration { get => _duration; set => _duration = value; }
// обходит зажим и никогда не поднимает OnDurationChanged -> UI молча устаёт
```

### Epsilon-защита перед рассылкой

```csharp
public void SetTime(float time)
{
    if (time < 0)
        throw new ArgumentException($"Time can't be negative: {time}!", nameof(time));

    float newTime = Math.Clamp(time, 0, _duration);
    if (Math.Abs(newTime - _time) <= float.Epsilon)
        return;

    _time = newTime;
    this.OnTimeChanged?.Invoke(newTime);
    this.OnProgressChanged?.Invoke(this.GetProgress());
}
```
Порядок: проверить, зажать, выйти если ничего не изменилось, присвоить, уведомить. Ранний выход делает повторные записи бесплатными и не будит подписчиков — это важно, когда презентер на каждое уведомление форматирует строку. Обратите внимание на `?.Invoke` на каждом событии: элемент без подписчиков можно тикать бесплатно.

```csharp
// ПЛОХО
public void SetTime(float time)
{
    _time = time;                 // без проверки и без зажима
    OnTimeChanged.Invoke(time);   // NRE без подписчиков, срабатывает и на пустой записи
}
```

### Охраняемые переходы, которые не бросают, а ничего не делают

```csharp
public void Pause()
{
    if (this.state != TimerState.PLAYING)
        return;

    this.state = TimerState.PAUSED;
    this.OnStateChanged?.Invoke(TimerState.PAUSED);
    this.OnPaused?.Invoke();
}

public void Start(float time)
{
    if (this.state is not (TimerState.IDLE or TimerState.COMPLETED))
        return;

    this.SetTime(time);
    this.state = TimerState.PLAYING;
    this.OnStateChanged?.Invoke(TimerState.PLAYING);
    this.OnStarted?.Invoke();
}
```
Каждый переход называет своё легальное предусловие паттерн-матчингом (`is not (A or B)`), затем меняет состояние, затем поднимает общий `OnStateChanged` **перед** конкретным `OnPaused`/`OnStarted` — на этот порядок подписчики могут опираться. Повторные вызовы безопасны, поэтому вызывающему не нужна церемония `if (timer.IsPaused())`. Обратная сторона — тишина: см. «Грабли».

```csharp
// ПЛОХО
public void Pause()
{
    if (state != TimerState.PLAYING) throw new InvalidOperationException();
    // заставляет каждого вызывающего предпроверять состояние,
    // а двойной Pause() из двух систем в одном кадре валит игру
}
```

### Switch-выражение для производных от состояния значений

```csharp
public float GetProgress()
{
    return this.state switch
    {
        TimerState.PLAYING or TimerState.PAUSED => 1 - this.time / this.duration,
        TimerState.COMPLETED => 1,
        _ => 0
    };
}
```
Решение «что такое прогресс в каждом состоянии» живёт в одном месте, а не разбросано по вызывающим в виде `if (IsCompleted()) return 1;`. `or`-паттерны схлопывают два эквивалентных состояния без дублирования, `_` закрывает остаток.

```csharp
// ПЛОХО
public float GetProgress()
{
    if (state == TimerState.COMPLETED) return 1;
    if (state == TimerState.IDLE) return 0;
    return 1 - time / duration;   // PAUSED попадает сюда случайно, а не по решению
}
```

### Выбор: `Cooldown` или `Timer`

```csharp
// простой интервал между действиями -> Cooldown
[SerializeField] private Cooldown _cooldown = 0.5f;
if (_cooldown.IsCompleted()) { Fire(); _cooldown.ResetTime(); }

// управляемое, с состоянием, с паузой -> Timer
ITimer timer = new DownTimer(30);
timer.Start();
timer.Pause();
timer.Resume();
```
`Docs/BestPractices/ChosingBetweenTimerAndCooldown.md` формулирует это прямо: «Use `Cooldown` for simplicity, and `Timer` when you need control». `Cooldown` — два `float` и четыре события, без конечного автомата, поэтому его `Tick` ветвится минимально и он не может застрять в неверном состоянии. `ITimer` несёт девять событий и четыре состояния. Кулдаун — перезарядка оружия, откат способности, задержка респауна; таймер — временный бафф, игровой раунд, симуляция времени как объекта.

```csharp
// ПЛОХО: DownTimer там, где хватало Cooldown.
// Теперь каждая перезарядка — Stop() + Start() (или Restart()),
// охрана IDLE/COMPLETED может проглотить ваш Start(),
// и вы платите за пять событий, на которые никогда не подписывались.
```

---

## Производительность

**Сразу о статусе цифр.** Бенчмарков для этой зоны в репозитории **нет**: `Assets/Plugins/Atomic/Elements/Tests/Performance/` содержит только `ArrayPerformance.cs`, `DictionaryPerformance.cs`, `HashSetPerformance.cs`, `ListPerformance.cs` и реактивные коллекции — ни одного файла про `Time`. Поэтому ниже: «**в коде**» — вычитано из исходника (факт), «**вывод**» — следствие, которое никто не измерял.

**Структуры данных.** В коде: в `Elements/Scripts/Time` нет ни одной коллекции, ни одного энумератора, ни одного `in`/`ref`-параметра и ни одного пула. Каждый тип — маленький мутабельный класс над двумя-тремя `float` плюс поля событий. Разделов «struct-энумераторы» и «array-backed storage» здесь просто не к чему применить: они относятся к `Elements/Collections` (глава про коллекции). Единственные коллекции в зоне — две статики `MainThreadDispatcher`.

**Нулевые аллокации в установившемся режиме.** В коде: `Tick`, `SetTime`, `SetDuration`, `SetProgress`, `GetProgress`, все `IsXxx()` и все чтения состояния работают только с полями `float`/`int`/`enum`. Ни LINQ, ни замыканий, ни боксинга ни на одном горячем пути. Аллоцируют только конструирование, `+=` (создание делегата) и `ToString()`.

**Точная инвентаризация `AggressiveInlining`.** В коде помечены:
- `DownTimer.Complete()`, `UpTimer.Complete()`, `Period.Complete()` — приватные методы в две-три строки, вызываемые из `Tick`;
- `FixedTimestamp.StartFromSeconds`;
- в `Time/Extensions.cs`: оба `Restart`, `ObserveTimeChanged`, `IsNotCompleted`, `IsIdleOrExpired`.

Не помечены: `WithDuration` из `Time/Timestamp/Extensions.cs` (в этом файле вообще нет `using System.Runtime.CompilerServices;`) и **ни один** публичный `Tick`/`SetTime`/`SetDuration` во всей зоне. Это осознанная дисциплина: инлайнится только маленькое листовое тело; широковещательные `Tick` с ветвлениями и рассылкой — нет. Копируйте именно это правило, а не «AggressiveInlining на всё».

**Цена одного тика.** В коде: `Cooldown.Tick`, `DownTimer.Tick`, `UpTimer.Tick` и `Period.Tick` безусловно поднимают и `OnTimeChanged`, **и** `OnProgressChanged`, без epsilon-проверки, и считают прогресс делением. То есть на каждый тикающий объект в кадре — два вызова делегата и одно деление, даже если визуально ничего не изменилось. Единственное исключение — `Cooldown.Tick` с его `if (_time == 0) return;`: истёкший кулдаун стоит одно сравнение. Вывод: отписывайте `OnProgressChanged`, когда UI не виден — это самая дешёвая экономия в зоне.

**Цена записи.** В коде: `Cooldown.SetTime`/`SetDuration` и все мутаторы `ThreadSafeCooldown` выходят досрочно по `Math.Abs(new - old) <= float.Epsilon`, то есть повторная запись того же значения бесплатна. Исключения, которые **всегда** рассылают события: `Cooldown.SetProgress`, `DownTimer.SetProgress`, `UpTimer.SetProgress`, `Period.SetProgress` — все четыре пишут поле времени напрямую и безусловно поднимают два события. Не вызывайте `SetProgress` каждый кадр из UI-кода.

**Делегат против замыкания.** В коде: канонический пример документации — `weapon.WhenFixedTick(_cooldown.Tick)`, группа методов, один делегат на установку. Лямбда `dt => _cooldown.Tick(dt)` дополнительно аллоцирует объект замыкания с захваченным `this`. Вывод: разница — один объект на сущность на установку, то есть важна при массовом спавне, а не в единичном инсталлере.

**Подписки — структуры, и это ловушка боксинга.** В коде: `WhenTick`/`WhenFixedTick`/`WhenLateTick` (`Entities/Scripts/Lifecycle/Extensions.cs`, все `AggressiveInlining`) возвращают `readonly struct` — `TickSubscription`, `FixedTickSubscription`, `LateTickSubscription`, каждая реализует `IDisposable`. Пока вы игнорируете результат или держите его в переменной конкретного типа, аллокаций нет. **Любое** приведение к `IDisposable` боксирует:

```csharp
// аллокаций нет
weapon.WhenFixedTick(_cooldown.Tick);

// боксинг структуры в кучу на каждую строку
IDisposable sub = weapon.WhenFixedTick(_cooldown.Tick);
weapon.WhenFixedTick(_cooldown.Tick).AddTo(_composite);
```
То же касается `DisposableAction` (`Utils/DisposableAction.cs`) — тоже `readonly struct : IDisposable`. `DisposableComposite` объявлен как `DisposableComposite<IDisposable>`, то есть хранит `List<IDisposable>`: всё, что в него добавлено и является структурой, боксируется. Вывод: это ровно по одному объекту на подписку в момент установки, а не per-frame, но при тысячах сущностей это заметный пик при спавне.

**Боксинг на ковариантном enum.** В коде: `IStateSource<out T> where T : Enum`. Вывод (стандартная семантика C#): потребление через `IStateSource<Enum>` боксирует значение при каждом `GetState()` и при каждом вызове `OnStateChanged`. Потребляйте закрытый генерик.

**Размер объектов.** В коде: `Cooldown` — четыре поля событий + два `float`. `DownTimer`/`UpTimer` — девять полей событий + два `float` + `enum`. `Period` — девять полей событий + два `float` + `enum`. `Stopwatch` — шесть полей событий + `float` + `enum`. Вывод: объект формы `ITimer` примерно втрое «толще» `Cooldown` по полям, что количественно подтверждает рекомендацию предпочитать `Cooldown`.

**`ThreadSafeCooldown`: блокировка на каждом чтении.** В коде: `_lock` берут геттеры `Duration`/`CurrentTime`/`Progress`, `IsCompleted()`, `IsPlaying()` и `ToString()`; а `GetTime()`, `GetDuration()`, `GetProgress()` реализованы **через** эти блокирующие свойства, то есть потребитель, типизированный как `ICooldown`, платит блокировку за каждый запрос. Единственное чтение без блокировки — `ResetTime()`, который делает `SetTime(_duration)` и читает `_duration` снаружи критической секции (гонка с параллельным `SetDuration`). `Cooldown.ToString()` блокировок не берёт, `ThreadSafeCooldown.ToString()` — берёт, так что лог с рабочего потока конкурирует с `Tick`. Документация формулирует вывод прямо: «For single-threaded scenarios, consider using `Cooldown` instead».

В коде: `ThreadSafeCooldown.Tick` мутирует состояние под `_lock`, а `MainThreadDispatcher.MarkDirty(this)` вызывает **вне** него, и `MarkDirty` берёт второй, отдельный статический lock. Вывод: тик с рабочего потока стоит два незанятых захвата блокировки плюс `HashSet.Add`.

**`MainThreadDispatcher`: без аллокаций и с дедупликацией.** В коде:

```csharp
private static readonly HashSet<IFlushable> _dirty = new(256);
private static readonly List<IFlushable> _processing = new(256);
```
Обе коллекции предразмерены на 256 и переиспользуются (`_processing.Clear()` вместо переаллокации), слив — `_processing.AddRange(_dirty); _dirty.Clear();` под блокировкой, а `Flush()` вызывается уже снаружи. Следствие дедупликации через `HashSet`, не оптимизация, а семантика: N тиков с рабочих потоков между двумя `Update` сворачиваются в **один** набор событий. Промежуточные значения `OnTimeChanged` действительно теряются, а `OnCompleted` приходит максимум один раз за кадр. Никогда не используйте `ThreadSafeCooldown` там, где нужно наблюдать каждое промежуточное значение.

**`RandomCooldown`: цепочка на стеке, две аллокации на итог.** В коде: `Builder` — `struct`, каждый `WithXxx` возвращает `this` по значению; `UnityRandomizer.Instance` — `public static readonly` синглтон, инъекция рандомайзера не аллоцирует. Кучу задействует только `Build()`: `new RandomCooldown(...)`, который внутри создаёт ещё и `new Cooldown(...)` — два объекта на один случайный кулдаун.

**`FixedTimestamp`: дешёво, но не бесплатно.** В коде: состояние — один `int`, работы на кадр нет вообще, но каждый запрос пересчитывает `Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime)`. `RemainingSeconds` → `GetRemainingSeconds()` → `GetRemainingTicks()` — два чтения статических свойств Unity плюс деление и округление; `IsPlaying()` и `IsExpired()` каждый ещё раз зовут `CurrentTick()`. Вывод: не кэшируйте результат «на всякий случай», но и не опрашивайте `RemainingSeconds` трижды в одном методе — положите в локальную переменную. Опора на статику Unity делает тип принципиально главнопоточным.

**Пулы.** В коде: пулинга в этой зоне нет вообще. Если временных элементов нужно много и с коротким временем жизни — либо `FixedTimestamp` (состояние-`int`, нечего пулить), либо собственный пул поверх `Cooldown` с переиспользованием через `SetDuration` + `SetTime`; учтите, что подписчики на событиях переиспользуемого объекта обязаны быть сброшены вручную — `Dispose()` у элементов времени нет.

---

## Грабли

- **`ResetTime()` не означает «в ноль».** `Cooldown`, `ThreadSafeCooldown`, `DownTimer` → полная длительность. `UpTimer`, `Stopwatch`, `Period` → ноль. Код, написанный против `ITimeSource`, при подмене реализации поменяет поведение.
- **`GetProgress()` у `Cooldown` и у `DownTimer` инвертированы друг относительно друга.** `Cooldown`: `_time / _duration` (1 = только перезарядили, 0 = готово). `DownTimer`: `1 - time/duration` (0 = начало, 1 = конец). Общий презентер шкалы, привязанный к `IProgressSource`, нарисует одну из них наизнанку.
- **`Cooldown.GetProgress()` не защищён от деления на ноль, а `ThreadSafeCooldown` — защищён.** Две реализации одного интерфейса расходятся. `[Min(float.Epsilon)]` на `_duration` защищает только инспектор, не код. Практический сценарий: `SetDuration(0)` не трогает `_time`, поэтому дальше `GetProgress()` вернёт `+Infinity`, и это значение уйдёт в `OnProgressChanged` и в UI.
- **Нулевая длительность ломает завершение у всех трёх автоматов.** `DownTimer.Tick` считает `1 - time/duration`, `UpTimer.Tick` — `currentTime/duration`, `Period.Tick` — `time/duration`; при `duration == 0` это `NaN`, сравнение `NaN >= 1` ложно, и `Complete()` (а для периода — `OnPeriod`) **никогда** не срабатывает. Проверяйте длительность на входе сами.
- **`SetDuration` не пересчитывает текущее время** в `Cooldown`, `DownTimer`, `UpTimer`, `Period`. Уменьшение длительности в рантайме оставляет `time > duration`, и `GetProgress()` выйдет за 1 (или уйдёт в минус у `DownTimer`) до следующего `SetTime`. Единственный, кто зажимает, — `ThreadSafeCooldown`.
- **`Stop()` не уведомляет об изменении времени.** `DownTimer.Stop()`, `UpTimer.Stop()`, `Period.Stop()` и `Stopwatch.Stop()` пишут `time = 0` напрямую и поднимают только `OnStateChanged` + `OnStopped`. View, привязанный через `OnTimeChanged`/`ObserveTimeChanged`, навсегда останется со значением «до Stop».
- **`Start(float)` тоже уведомляет не везде.** `DownTimer.Start(time)` и `UpTimer.Start(time)` идут через `SetTime(...)` и поднимают `OnTimeChanged`; `Stopwatch.Start(time)` и `Period.Start(time)` присваивают поле напрямую и **не** поднимают. Четыре соседних класса, два разных поведения.
- **`Period.SetTime` поднимает только `OnTimeChanged`**, без `OnProgressChanged` — в отличие от `Cooldown`/`DownTimer`/`UpTimer`. Поскольку `Complete()` заканчивается на `SetTime(time - duration)`, а `ResetTime()`/`Stop()` идут тем же путём, шкала заполнения, привязанная к прогрессу, застревает на 1.0 после каждого витка и до следующего `Tick`.
- **`Period` дрейфует.** `Tick` обрезает перелёт `Math.Min` раньше, чем `Complete()` вычтет длительность, поэтому подкадровый остаток выбрасывается: до одного `deltaTime` потери за цикл, и дважды за длинный кадр период не сработает. Для фазово-точной периодики накапливайте остаток сами.
- **`Start()` — не перезапуск.** `DownTimer`/`UpTimer` стартуют только из `IDLE` и `COMPLETED`, `Stopwatch`/`Period` — только из `IDLE`. Вызов на `PLAYING`/`PAUSED` молча ничего не делает и не бросает. Используйте `Restart()`; для проверки «подействует ли `Start()`» есть `IsIdle()` — но он объявлен только на конкретных классах, не в интерфейсах.
- **`RandomCooldown.ResetTime()` сломан.** Он вызывает только `_cooldown.SetDuration(newRandom)`, а `Cooldown.SetDuration` не трогает `_time`. После первого завершения внутреннее время остаётся 0 и `IsCompleted()` навсегда true. Обход — через проброшенные члены: `cd.SetDuration(x); cd.SetTime(cd.GetDuration());` — или не используйте `RandomCooldown` вовсе, а бросайте кубик самостоятельно поверх обычного `Cooldown`. Любое место, где вы полагаетесь на `ICooldown.ResetTime()`, с `RandomCooldown` тихо перестанет работать.
- **`RandomCooldown` нельзя класть в `[SerializeField]`,** несмотря на `[Serializable]` и раздел «Inspector Settings» в `RandomCooldown.md`. Поля `_cooldown` и `_randomizer` — `readonly` ссылочные, без `[SerializeField]`/`[SerializeReference]`. У десериализованного Unity экземпляра `_cooldown == null`, и первое же обращение даст NRE. Конструируйте в коде, лучше через `StartBuild()`.
- **`new RandomCooldown(2f, 5f)` не компилируется** — конструктор требует третий аргумент `IRandomizer`. Передавайте `UnityRandomizer.Instance` или свою сидированную реализацию.
- **Детерминированного `IRandomizer` в поставке нет.** В `Utils/IRandomizer.cs` есть ровно один конкретный тип — `UnityRandomizer` (обёртка над `UnityEngine.Random`: глобальный сид, только главный поток). `DefaultRandomizer` на `ThreadLocal<System.Random>` в этом файле полностью закомментирован. Для тестов и сетевой симуляции рандомайзер придётся написать.
- **`MainThreadDispatcher.Flush()` не существует.** Строка из примера в `ThreadSafeCooldown.md` не скомпилируется: класс `internal sealed`, публичного `Flush` нет, а `ThreadSafeCooldown.Flush()` — явная реализация интерфейса. Слив автоматический, писать его не надо и нечем.
- **`ThreadSafeCooldown` не выдаёт событий в EditMode-тестах.** `MainThreadDispatcher` создаёт свой `DontDestroyOnLoad`-объект из `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`; в EditMode этот метод не выполняется, объекта нет, `Update()` не идёт, `Flush()` никто не вызывает. Обойти кастом к `MainThreadDispatcher.IFlushable` можно только из `Atomic.Elements.Tests.EditMode`/`.PlayMode`: в `Elements/Scripts/AssemblyInfo.cs` `InternalsVisibleTo` выписан ровно на эти две сборки. Из тестовой сборки проекта `ThreadSafeCooldown` проверяется только в PlayMode с реальным кадром.
- **`ThreadSafeCooldown` склеивает уведомления.** Дедупликация через `HashSet` + один слив на `Update` означают: много фоновых тиков между кадрами дают одно `OnTimeChanged` с последним значением и максимум одно `OnCompleted`. Не используйте там, где нужно каждое промежуточное значение.
- **`ThreadSafeCooldown.ResetTime()` — единственное чтение без блокировки** (`SetTime(_duration)` читает `_duration` снаружи `lock`). Гонка с параллельным `SetDuration` реальна.
- **`OnCompleted` у `ThreadSafeCooldown` может прийти из обычного сеттера**, а не только из `Tick`: `SetTime` и `SetProgress` выставляют `_completed = true`, когда попадают в ноль. У `Cooldown` это невозможно. Логика «одно завершение на одну перезарядку» между этими двумя реализациями не переносится.
- **`new Cooldown()` не компилируется.** Конструктора без параметров в коде нет, хотя `Cooldown.md` его документирует. Нужен нулевой — `new Cooldown(0f)` или десериализация поля. У `DownTimer`, `UpTimer`, `Period` и `Stopwatch` конструкторы без параметров настоящие.
- **Отрицательные аргументы обрабатываются по-разному у типов, реализующих один интерфейс.** `ArgumentException`, базовый `Exception`, молчаливый `return`, молчаливый `Clamp`, `ArgumentOutOfRangeException` — полная таблица в разделе «Модель». Не выводите политику из интерфейса.
- **`UpTimer` и `DownTimer` по-разному называют одно и то же.** `UpTimer.CurrentState`/`UpTimer.Time` против `DownTimer.State`/`DownTimer.CurrentTime`. Если направление счёта может поменяться — работайте через `GetState()`/`GetTime()`.
- **Коллизия имени `ITickSource`.** `Atomic.Elements.ITickSource` (один метод `Tick(float)`) и `Atomic.Entities.ITickSource` (три события плюс `Tick`/`FixedTick`/`LateTick`) — несвязанные типы с одним именем, и совпадает даже член `Tick(float)`: один метод удовлетворит оба интерфейса. `WhenTick`/`WhenFixedTick`/`WhenLateTick` объявлены на **entities**-версии. В файле с обоими `using` квалифицируйте тип.
- **`FixedTimestamp` имеет мёртвое состояние при `_endTick == 0`.** Достижимо через `new FixedTimestamp(0)` или `StartFromTicks(0)` при `Time.fixedTime == 0`: `IsIdle()` (`== -1`), `IsPlaying()` (`> 0 &&`) и `IsExpired()` (`> 0 &&`) — все три ложны одновременно. Сбрасывайте только через `ResetEndTick()` (он пишет `-1`), никогда нулём. Ветка `ticks != -1` в `GetRemainingSeconds` — мёртвый код: `GetRemainingTicks` возвращает `0`, а не `-1`.
- **`FixedTimestamp.GetProgress(float duration)` не защищён от нулевого аргумента.**
- **Документация `ITimestamp` устарела на уровне имён членов.** В `ITimestamp.md` и `FixedTimestamp.md` фигурируют `RemainingTime` и `Stop()`; в коде — `RemainingSeconds` и `ResetEndTick()`. Примеры из этих файлов не компилируются.
- **`SetProgress` обходит epsilon-защиту** у `Cooldown`, `DownTimer`, `UpTimer` и `Period`: пишет поле времени напрямую и всегда поднимает `OnTimeChanged` + `OnProgressChanged`, даже если значение не изменилось. Не вызывайте из UI каждый кадр.
- **Время `Stopwatch` не ограничено.** `Tick` делает просто `time += deltaTime` без зажима; на многочасовой сессии точность `float` деградирует. Для длинного хронометража накапливайте в `double` или в тиках.
- **Подписки снимаются только вручную и только именованным методом.** События элементов времени — plain `event Action<float>`, а не `ISignal<T>`, поэтому расширение `Subscribe(...)` и `AddTo(composite)` из `Elements/Events` к ним неприменимы. Лямбду отцепить нельзя: презентер утечёт и продолжит обновлять мёртвый view.
- **Odin-кнопки у `Stopwatch` и `Period` видны в Edit-режиме.** У `DownTimer`/`UpTimer` мутаторы помечены `[Button, HideInEditorMode]`, а у `Stopwatch` и `Period` — просто `[Button]`. Нажатие в Edit-режиме пишет мусор в сериализованный экземпляр.
- **`[SerializeField] private Stopwatch _sw;` ничего не настраивает.** `Stopwatch.cs` — единственный файл зоны без `using UnityEngine`, у его полей нет `[SerializeField]`, поэтому `[Serializable]`-пометка даёт пустой блок в инспекторе.
- **`RandomCooldown.cs` не защищён `#if UNITY_5_3_OR_NEWER`** — `using UnityEngine;`, `[Min(float.Epsilon)]` и `[SerializeField]` в нём безусловны, в отличие от `Cooldown.cs`, `DownTimer.cs`, `UpTimer.cs` и `Period.cs`. `FixedTimestamp.cs` целиком внутри этого guard. Практического значения внутри Unity это не имеет (`Atomic.Elements.asmdef` ссылается на движок), но станет важно, если вы будете выносить исходники в сборку без Unity.
- **Целый ряд возможностей не задокументирован вообще.** `IDurationTimestamp`, `DurationTimestamp`, `WithDuration`, `ObserveTimeChanged`, `IsNotCompleted`, `IsIdleOrExpired`, `RandomCooldown.Builder`/`StartBuild()` и `ICooldown.IsPlaying()` не упоминаются в `Docs/Elements/Time` ни разу (из кулдаунов `IsPlaying` документирован только в `ThreadSafeCooldown.md`; остальные вхождения слова в этой папке — это `ITimestamp.IsPlaying()`, другой член). Прежде чем решить, что возможности нет, откройте исходник.
- **`ThreadSafeCooldown` не упомянут в `Cooldowns.md`.** Его список API — только `ICooldown`, `Cooldown`, `RandomCooldown`. Полный список есть в `Manual.md`.
- **Регистры enum в документации перепутаны.** `TimerState.md` в оглавлении пишет `Idle`/`Playing`/`Paused`/`Completed`, `IStopwatch.md` упоминает `RUNNING`. Настоящие идентификаторы — `IDLE`, `PLAYING`, `PAUSED`, `COMPLETED`; `RUNNING` не существует.
- **`Period.md` документирует `ResetTime()` дважды** с разными описаниями («Resets the time source to its initial state» на строке 396 и «Resets the current cycle time to `0`» на строке 459). Верно второе.

---

## Где читать

Пути — относительно `C:/!!!Work/_AtomicUpstream`.

### Обзорные страницы

| Файл | Что внутри |
|---|---|
| `Docs/Elements/Time/Manual.md` | Точка входа в модуль. Пять сквозных примеров (Cooldown, Timer, Stopwatch, Period, Timestamp) плюс полное дерево ссылок на все остальные страницы в `<details>`-блоках. Единственное место, где `ThreadSafeCooldown` попал в список API. Читать первым. |
| `Docs/Elements/Time/Sources.md` | Восемнадцать строк: одно предложение про идею source-интерфейсов и список восьми ссылок с однострочным описанием каждого. Карта, не текст. |
| `Docs/Elements/Time/Cooldowns.md` | Семейство кулдаунов: два примера (`Cooldown` и `RandomCooldown`) и список API. Внимание: `ThreadSafeCooldown` в списке **отсутствует**, а пример `RandomCooldown` использует неверный двухаргументный конструктор. |
| `Docs/Elements/Time/Timers.md` | Семейство таймеров: один пример полного цикла `Start`/`Pause`/`Resume`/`Stop`, список из четырёх типов, ссылки на обе best-practice страницы. |
| `Docs/Elements/Time/Stopwatches.md` | Секундомер: объяснение, чем он отличается от `ITimer` (нет длительности, нет завершения), один пример с циклом тиков и `Thread.Sleep`, список API. Пятьдесят семь строк, файл целый и читаемый. |
| `Docs/Elements/Time/Periods.md` | Период: объяснение «автоматически зацикливается на завершении», один пример и список из трёх типов. Не показывает pause/stop-поверхность, из-за чего её легко не заметить. |
| `Docs/Elements/Time/Timestamps.md` | Таймстемпы: формулировка про tick-based системы и client-side prediction, один пример (`StartFromSeconds` + `IsExpired`/`IsPlaying`/`IsIdle` — он компилируется) и список из двух типов. `IDurationTimestamp` и `DurationTimestamp` не упомянуты. |

### Source-интерфейсы

| Файл | Что внутри |
|---|---|
| `Docs/Elements/Time/ITimeSource.md` | `OnTimeChanged`, `GetTime`, `SetTime`, `ResetTime`. Не пишет, что `ResetTime()` означает разное в разных реализациях — это самая важная вещь про этот интерфейс, и её там нет. |
| `Docs/Elements/Time/IDurationSource.md` | `OnDurationChanged`, `GetDuration`, `SetDuration`. Не предупреждает, что `SetDuration` не пересчитывает текущее время. |
| `Docs/Elements/Time/ITickSource.md` | Сорок восемь строк на один метод `Tick(float)`. Ничего не говорит о коллизии с `Atomic.Entities.ITickSource` — проверьте по исходникам обоих. |
| `Docs/Elements/Time/IStartSource.md` | `OnStarted`/`OnStopped`, `IsStarted`, `Start()`, `Start(float)`, `Stop()`. Заявляет «Stops the source and resets its time» — это правда, но не упоминает, что при сбросе `OnTimeChanged` не поднимается. |
| `Docs/Elements/Time/IPauseSource.md` | `OnPaused`/`OnResumed`, `IsPaused`, `Pause`, `Resume`. Короткая и точная. |
| `Docs/Elements/Time/ICompleteSource.md` | `OnCompleted`, `IsCompleted`. Короткая и точная. |
| `Docs/Elements/Time/IProgressSource.md` | `OnProgressChanged`, `GetProgress`, `SetProgress`, с формулировкой «Normalized progress (0–1)». Не упоминает, что направление нормализации у `Cooldown` и `DownTimer` противоположно. |
| `Docs/Elements/Time/IStateSource.md` | `OnStateChanged`, `GetState()`, ковариантность по `T : Enum`. Про боксинг при потреблении как `IStateSource<Enum>` не пишет. |
| `Docs/Elements/Time/ExtensionsIStartSource.md` | Два `Restart`-оверлоада с примерами. Остальные четыре метода из `Extensions` (`ObserveTimeChanged`, `IsNotCompleted`, `IsIdleOrExpired`, `WithDuration`) здесь отсутствуют — читайте исходник. |

### Кулдауны

| Файл | Что внутри |
|---|---|
| `Docs/Elements/Time/ICooldown.md` | Разбор контракта по членам: все четыре события и девять методов. `IsPlaying()` **не документирован**, хотя он есть в интерфейсе. |
| `Docs/Elements/Time/Cooldown.md` | Самая полезная страница по кулдаунам: пример использования, раздел «Inspector Settings», разбор каждого члена (включая `ToString()` и оба implicit-оператора). Два расхождения с кодом: документирует несуществующий `Cooldown()` без параметров и вообще не показывает настоящий двухаргументный `Cooldown(float, float)`. |
| `Docs/Elements/Time/RandomCooldown.md` | Описание случайного кулдауна. **Самая недостоверная страница зоны**: неверный конструктор `RandomCooldown(float, float)`, раздел «Inspector Settings» для типа, который нельзя сериализовать, отсутствие `Builder`/`StartBuild()` и ни слова о сломанном `ResetTime()`. |
| `Docs/Elements/Time/ThreadSafeCooldown.md` | Потокобезопасный кулдаун: конструкторы, события, свойства, методы — и единственное место, где документирован `IsPlaying()`. Пример в разделе «Example of Usage» содержит несуществующий вызов `MainThreadDispatcher.Flush();`. |

### Таймеры, секундомер, период

| Файл | Что внутри |
|---|---|
| `Docs/Elements/Time/ITimer.md` | 382 строки: композиция интерфейса из восьми source-интерфейсов, пример, разбор девяти событий и всех методов. Лучшая страница, чтобы понять замысел «интерфейс как список способностей». |
| `Docs/Elements/Time/DownTimer.md` | 495 строк: тип, «Inspector Settings», конструкторы, девять событий, свойства (`State`, `Duration`, `CurrentTime`, `Progress`), все методы, операторы. Достоверна по именам, но не предупреждает, что `SetDuration` бросает базовый `Exception`, а `Stop()` не поднимает `OnTimeChanged`. |
| `Docs/Elements/Time/UpTimer.md` | 502 строки, зеркало предыдущей. Сверяйте имена свойств: здесь `CurrentState` и `Time`, а не `State` и `CurrentTime` — страницы легко перепутать. |
| `Docs/Elements/Time/TimerState.md` | Четыре значения с описаниями. В оглавлении регистр неверный (`Idle`/`Playing`/…), в теле — верный. |
| `Docs/Elements/Time/IStopwatch.md` | 267 строк: композиция контракта и разбор всех членов. В описании `OnStateChanged` упомянуто несуществующее значение `RUNNING`. |
| `Docs/Elements/Time/Stopwatch.md` | 315 строк: реализация, события, свойства `State`/`Time`, все методы. Не говорит, что `Tick` не ограничивает время сверху. |
| `Docs/Elements/Time/StopwatchState.md` | Три значения: `IDLE`, `PLAYING`, `PAUSED`. |
| `Docs/Elements/Time/IPeriod.md` | 331 строка: почему `OnPeriod` вместо `ICompleteSource`, разбор восьми событий и всех методов. Лучшее объяснение разницы «заканчивается» и «оборачивается». |
| `Docs/Elements/Time/Period.md` | 484 строки: реализация, «Inspector Settings», события, свойства, методы, операторы. Ничего не говорит о дрейфе цикла. `ResetTime()` задокументирован дважды, с разными описаниями (строки 396 и 459). |
| `Docs/Elements/Time/PeriodState.md` | Три значения: `IDLE`, `PLAYING`, `PAUSED`. Отсутствие `COMPLETED` нигде не прокомментировано — объяснение есть только в `IPeriod.md`. |

### Таймстемпы

| Файл | Что внутри |
|---|---|
| `Docs/Elements/Time/ITimestamp.md` | Контракт по членам. **Устарел**: описывает `RemainingTime` и `Stop()` вместо реальных `RemainingSeconds` и `ResetEndTick()`. |
| `Docs/Elements/Time/FixedTimestamp.md` | Реализация на `Time.fixedTime`: конструктор, свойства, методы. Те же два устаревших имени. Про мёртвое состояние `_endTick == 0` не пишет. |

Документации на `IDurationTimestamp`, `DurationTimestamp` и `WithDuration` не существует — только исходники.

### Best practices

| Файл | Что внутри |
|---|---|
| `Docs/BestPractices/UsingCooldownInGameMechanics.md` | Четыре канонические схемы подключения кулдауна к сущностям: стрельба (`WeaponInstaller` + `InlineAction` + `WhenFixedTick`), ближний бой (`Request`/`Event`/`Consume`, разделение решения и тика), игровой таймер (контроллер + инсталлер + презентер) и спавнер монет. Самый полезный файл для миграции — именно его формы переносите в проект. Два предупреждения: примеры используют несуществующие `BaseEvent`/`BaseRequest` (реально — `Event`/`Request`), а презентер привязан только к `OnTimeChanged` и поэтому не переживает `Stop()`. Вторая половина файла — старая версия того же текста, закомментированная через `<!-- ... -->`; её можно игнорировать, но различия в именах use-case-классов (`LifeUseCase` против `HealthUseCase`) в ней заметны. |
| `Docs/BestPractices/ChosingBetweenTimerAndCooldown.md` | Три таблицы: «когда что брать», «ключевые отличия» (сложность, управление, события, сценарии) и итоговая сводка, плюс список конкретных применений. Короткий файл, ровно на один вопрос. В конце тоже есть закомментированная старая версия. |

Смежные страницы, которые стоит прочесть рядом при рефакторинге: `Docs/BestPractices/PreferAbstractInterfaces.md` (почему поле типизируют `ICooldown`, а не `Cooldown`), `ModularEntityInstallers.md`, `UsingSubscriptionsWithDisposeComposite.md` (общий случай симметричных подписок — но помните, что к plain-событиям элементов времени он не применяется), `RequestConditionActionEvent.md` и `UsingObserveWithReactiveValues.md`.

### Исходники — то, по чему надо сверяться

Пути относительно `Assets/Plugins/Atomic/Elements/Scripts/`.

| Файл | Зачем открывать |
|---|---|
| `Time/Sources/ITimeSource.cs`, `IDurationSource.cs`, `ITickSource.cs`, `IStartSource.cs`, `IPauseSource.cs`, `ICompleteSource.cs`, `IProgressSource.cs`, `IStateSource.cs` | Восемь файлов по 13–28 строк. Полный словарь способностей модуля; прочитать все за пять минут. |
| `Time/Extensions.cs` | 46 строк, пять расширений, все пять `AggressiveInlining` (шестое, `WithDuration`, лежит в `Time/Timestamp/Extensions.cs` и без атрибута). Четыре из шести не задокументированы. |
| `Time/Cooldown/ICooldown.cs` | Композиция контракта + `IsPlaying()`. |
| `Time/Cooldown/Cooldown.cs` | 232 строки. Эталон стиля всей зоны: `#if`-guard'ы, Odin-атрибуты, epsilon-защита, `Tick`. Если сомневаетесь, «как здесь принято писать» — смотрите сюда. |
| `Time/Cooldown/RandomCooldown.cs` | 192 строки. Настоящий конструктор, add/remove-форвардинг событий, `Builder`-структура и сломанный `ResetTime()` — всё в одном файле, который расходится с документацией сильнее остальных. |
| `Time/Cooldown/ThreadSafeCooldown.cs` | 338 строк. Единственный потокобезопасный элемент времени; обязательно прочитайте `Flush()` целиком, если будете писать свои реактивные элементы под многопоточность. |
| `Time/Timer/DownTimer.cs`, `Time/Timer/UpTimer.cs` | 299 и 358 строк. Читайте парой: отличия в именах свойств, в политике отрицательных аргументов и в `ResetTime()` видны только при сравнении. |
| `Time/Timer/TimerState.cs`, `Time/Stopwatch/StopwatchState.cs`, `Time/Period/PeriodState.cs` | Три enum'а по 14–27 строк с явными номерами значений. |
| `Time/Stopwatch/Stopwatch.cs` | 201 строка. Единственный файл зоны без `UnityEngine`; `Tick` без ограничения сверху. |
| `Time/Period/Period.cs` | 288 строк. Смотреть `Tick` и `Complete` вместе — там и живёт дрейф цикла. |
| `Time/Timestamp/ITimestamp.cs`, `IDurationTimestamp.cs`, `FixedTimestamp.cs`, `DurationTimestamp.cs`, `Timestamp/Extensions.cs` | 64, 8, 101, 57 и 7 строк. Единственный источник правды по таймстемпам: документация здесь устарела по именам членов и не знает о половине типов. |
| `Utils/IRandomizer.cs` | Интерфейс, `UnityRandomizer` и закомментированный `DefaultRandomizer`. Открыть, чтобы убедиться: сидированной реализации нет. |
| `Utils/MainThreadDispatcher.cs` | `internal sealed MonoBehaviour`, две статические коллекции на 256, `MarkDirty`, `Update`, `Initialize`. Объясняет и слияние уведомлений, и почему `ThreadSafeCooldown` молчит в EditMode. |
| `AssemblyInfo.cs` | Две строки `InternalsVisibleTo` — ровно `Atomic.Elements.Tests.EditMode` и `.PlayMode`. Отвечает на вопрос «могу ли я вызвать `Flush()` из своих тестов»: нет. |
| `../Tests/EditMode/Time/CooldownTests.cs`, `DownTimerTests.cs`, `UpTimerTests.cs`, `StopwatchTests.cs`, `PeriodTests.cs` | Исполняемая спецификация поведения. `PeriodTests.OnCycle` прямо фиксирует потерю остатка цикла. Тестов на `ThreadSafeCooldown`, `RandomCooldown` и таймстемпы в репозитории нет. |
| `../Tests/Performance/` | Только коллекции (`Array`, `List`, `Dictionary`, `HashSet` и реактивные версии). Бенчмарков модуля `Time` **не существует** — любые перф-утверждения о нём читаются из кода, а не измерены. |

Файлы, нужные со стороны `Atomic.Entities` (относительно `Assets/Plugins/Atomic/Entities/Scripts/`):

| Файл | Зачем |
|---|---|
| `Lifecycle/Extensions.cs` | `WhenTick`, `WhenFixedTick`, `WhenLateTick` — все `AggressiveInlining`, все возвращают struct-подписку. Главный способ двигать время. |
| `Lifecycle/Sources/ITickSource.cs` | Тот самый конфликтующий по имени интерфейс: три события плюс `Tick`/`FixedTick`/`LateTick`. |
| `Lifecycle/Subscriptions/FixedTickSubscription.cs` | `readonly struct : IDisposable` — источник ловушки боксинга. |
| ``Installers/MonoEntityInstaller`1.cs`` | `MonoEntityInstaller<E>` с `sealed override Install(IEntity)`: переопределять надо только типизированный `Install(E)`. |
| `Behaviours/IEntityInit.cs`, `IEntityFixedTick.cs`, `IEntityDispose.cs` | Нетипизированные и типизированные формы; дефолтные реализации интерфейсов делают проверку типа за вас. |
| `Entities/Extensions_Behaviours.cs` | `AddBehaviour<T>() where T : IEntityBehaviour, new()` — отсюда требование публичного конструктора без параметров. |
| `Entities/Unity/MonoEntitySingleton.cs` | `MonoEntitySingleton<E> where E : MonoEntitySingleton<E>` со статическим `Instance` — то, через что в примерах работает `GameContext.Instance`. |

И со стороны `Atomic.Elements` вне зоны `Time`: `Events/Event.cs` (реальные `Event` / `Event<T>`…`Event<T1,T2,T3,T4>`), `Requests/Request.cs` (`Request`, `Request<T>` с `Consume(out T)`), `Actions/Core/InlineAction.cs`, `Values/Const.cs` — всё, что встречается в примерах кулдаунов, но описано в других главах.
