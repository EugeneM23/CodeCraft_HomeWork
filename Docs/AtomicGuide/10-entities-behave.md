# 10. Entities: поведения и жизненный цикл

Вся игровая логика в Atomic живёт в поведениях (behaviours) — маленьких классах, каждый из которых реализует один или несколько интерфейсов жизненного цикла и прикрепляется к конкретному экземпляру сущности в рантайме. Сама сущность — это состояние (теги + значения) плюс список поведений плюс диспетчер, который по фазам кадра зовёт нужные поведения; наследования от сущности для добавления логики не предполагается. Модуль Lifecycle задаёт контракты-источники (`IInitSource`, `IEnableSource`, `ITickSource`, `IGizmosSource`), которые потребляют сущности и миры: `IEntity : IInitSource, IEnableSource, ITickSource`, а `IEntityWorld<E> : IEntityCollection<E>, IEnableSource, ITickSource` — без `IInitSource`; `IGizmosSource` во всей кодовой базе реализует один тип — `MonoEntity`. Плюс восемь `readonly struct`-подписок и методы `When*` — чтобы код, не являющийся поведением, тоже мог подключиться к событиям жизненного цикла. Порядок фиксирован и важен: `Init` → `Enable` → N × `Tick`/`FixedTick`/`LateTick` → `Disable` → `Dispose`, причём `Enable()` сам сначала вызывает `Init()`.

Эта глава описывает контракты поведений, диспетчер внутри `Entity`, мост `MonoEntity`, подписки и системы — то есть всё, что придётся переписывать в первую очередь при переносе логики с `MonoBehaviour` на Atomic.

## Модель

**Сущность — контейнер, а не базовый класс.** `Entity` реализует `IEntity` и хранит поведения в массиве `IEntityBehaviour[] _behaviours` с отдельным счётчиком `_behaviourCount`. Семантика — списковая: порядок сохраняется, дубликаты разрешены (документация `Docs/Entities/Entities/EntityBehaviours.md` прямо это оговаривает), все поиски по типу линейные.

**`IEntityBehaviour` — пустой маркер.** Он не объявляет ни одного члена; от него наследуются восемь контрактов:

| интерфейс | фаза | когда вызывается |
|---|---|---|
| `IEntityInit` | инициализация | один раз на `Init()` |
| `IEntityEnable` | активация | на каждом `Enable()` |
| `IEntityTick` | `Update` | каждый кадр, пока сущность enabled |
| `IEntityFixedTick` | `FixedUpdate` | каждый фиксированный шаг |
| `IEntityLateTick` | `LateUpdate` | каждый кадр после основной логики |
| `IEntityDisable` | деактивация | на каждом `Disable()` |
| `IEntityDispose` | освобождение | на `Deinitialize()` и при удалении поведения |
| `IEntityGizmos` | отладочная отрисовка | из `MonoEntity.OnDrawGizmos`/`OnDrawGizmosSelected`, только в редакторе |

У каждого есть обобщённый вариант `IEntityX<in E> where E : IEntity`, который через default interface implementation (C# 8) делает `entity is not E` и бросает `InvalidCastException` с текстом, где указаны ожидаемый и полученный типы. Default interface implementation требует C# 8; сам пакет в `Assets/Plugins/Atomic/package.json` объявляет `"unity": "6.0"`. Это и причина, по которой вы никогда не пишете каст руками.

**Две пары, которые нельзя смешивать.** `Init`/`Dispose` — одна на инициализацию. `Enable`/`Disable` — на каждый цикл активации; с пулингом их может быть сотни между одним `Init` и одним `Dispose`. Что захватили в `Init` — отпускаете в `Dispose`; что включили в `Enable` — выключаете в `Disable`. Перекрёстная пара (захват в `Enable`, освобождение в `Dispose`) — это утечка на каждое переиспользование из пула.

**Проверки типов платятся один раз.** `Entity.EnableBehaviour` делает `is IEntityEnable` / `is IEntityTick` / `is IEntityFixedTick` / `is IEntityLateTick` и раскладывает поведение в три отдельных массива `_updates` / `_fixedUpdates` / `_lateUpdates`. Сами `Tick`/`FixedTick`/`LateTick` потом проходят по этим массивам по индексу без единой проверки типа. Следствие для планирования: стоимость кадра пропорциональна числу *тикающих* поведений, а не общему числу поведений. Поведение, реализующее только `IEntityInit`, не стоит в кадре ничего.

**Источники жизненного цикла потребляются, а не реализуются.** `IInitSource`, `IEnableSource`, `ITickSource` помечены в документации как «core framework interface. You should not implement it manually». Вы реализуете контракты поведений и подписываетесь на источники.

**Композиция — дело установщика, не поведения.** `IEntityInstaller.Install(IEntity)` добавляет теги, значения и поведения. Поведение в своём `Init` только *разрешает* ссылки на уже существующее состояние. Отсюда порядок сборки: теги → значения → поведения.

**Один насос на всю игру.** `TickableManager` — `internal sealed MonoBehaviour`, который сам себя создаёт (`HideFlags.HideAndDontSave`, `DontDestroyOnLoad`) и в своих `Update`/`FixedUpdate`/`LateUpdate` проходит по `ITickSource[] _sources`. Вместо N нативных вызовов `MonoBehaviour.Update` — один. Регистрация идёт через `AddIfAbsent`, так что двойная регистрация невозможна.

**`MonoEntity` — не наследник `Entity`.** Это `public partial class MonoEntity : MonoBehaviour, IEntity` (`Entities/Scripts/Entities/Unity/MonoEntity.cs:19`). Он заново реализует и хранилище поведений, и диспетчер — в `MonoEntity_Behaviours.cs` и `MonoEntity_Lifecycle.cs`, с другими именами тиковых полей (`updates`, `fixedUpdates`, `lateUpdates`, `updateCount`, `lateUpdateCount` — без ведущего подчёркивания; хранилище поведений при этом названо так же, `_behaviours`/`_behaviourCount`) и со своими копиями статических компараторов. Нельзя переносить выводы про `Entity` на `MonoEntity` по построению — только проверкой. Практический вывод: там, где сериализация Unity не нужна, берите обычный `Entity` плюс тонкий драйвер, а не `MonoEntity`.

**`OnStateChanged`.** `IEntity` объявляет `event Action<IEntity> OnStateChanged` (`Entities/Scripts/Entities/Interface/IEntity.cs:27`). Он срабатывает на каждой мутации тега/значения/поведения **и** на каждом переходе `Init`/`Enable`/`Disable`/`Dispose`. Это единственное событие, через которое инспектор, отладка или сериализация могут реагировать на любое изменение сущности, не подписываясь на десяток конкретных событий. У него нет ни struct-подписки, ни `When*`-расширения.

## API

### Маркер и контракты поведений

```csharp
public interface IEntityBehaviour
{
}
```

Пустой корень иерархии; это же тип хранения внутри `Entity`. Класс, реализующий только маркер, будет храниться в `_behaviours`, но не получит ни одного колбэка. Документация (`Docs/Entities/Behaviours/IEntityBehaviour.md`) говорит «Don't implement this interface» — но это утверждение только .md: XML-док в исходнике (`Entities/Scripts/Behaviours/IEntityBehaviour.cs`) формулирует обратное («Defines a unit of logic that can be attached to an `IEntity`»), а тесты сознательно используют голую реализацию-маркер (`Tests/Substitutions/Behaviours/EntityBehaviourDummy.cs`: `public sealed class EntityBehaviourDummy : IEntityBehaviour {}`). Практический итог тот же: маркер без фазового интерфейса бесполезен.

```csharp
public interface IEntityInit : IEntityBehaviour
{
    void Init(IEntity entity);
}

public interface IEntityInit<in E> : IEntityInit where E : IEntity
{
    void Init(E entity);

    void IEntityInit.Init(IEntity entity)
    {
        if (entity is not E e)
            throw new InvalidCastException(
                $"[IEntityInit<{typeof(E).Name}>] Invalid entity type for {this.GetType().Name}.\n" +
                $"Expected: {typeof(E).FullName}\n" +
                $"Received: {entity?.GetType().FullName ?? "null"}\n" +
                "Please make sure the correct IEntityInit is used for this entity type."
            );

        this.Init(e);
    }
}
```

Разовая настройка: единственное место, где поведение должно разрешать и кэшировать свои хэндлы на значения. Обратите внимание на ограничение: `where E : IEntity`, без `class` — более жёсткое `where E : class, IEntity` стоит только на классах `MonoEntityInstaller<E>` и `ScriptableEntityInstaller<E>`, тогда как сам `IEntityInstaller<in E>` объявлен как `where E : IEntity`. Тот же каст-мост (`is not E` → `InvalidCastException`) дословно повторён в остальных семи контрактах.

```csharp
public interface IEntityEnable : IEntityBehaviour
{
    void Enable(IEntity entity);
}

public interface IEntityDisable : IEntityBehaviour
{
    void Disable(IEntity entity);
}
```

Активация/деактивация. Вызываются из `Entity.EnableBehaviour` / `Entity.DisableBehaviour`; `DisableBehaviour` при этом ещё и вынимает поведение из тиковых массивов, поэтому отключённая сущность не стоит ничего ни на уровне кадра, ни на уровне отдельных поведений.

```csharp
public interface IEntityTick : IEntityBehaviour
{
    void Tick(IEntity entity, float deltaTime);
}

public interface IEntityFixedTick : IEntityBehaviour
{
    void FixedTick(IEntity entity, float deltaTime);
}

public interface IEntityLateTick : IEntityBehaviour
{
    void LateTick(IEntity entity, float deltaTime);
}
```

`deltaTime` **передаётся снаружи** — `Time.deltaTime`/`Time.fixedDeltaTime` читает `TickableManager` один раз на фазу. Внутри поведения к `Time` обращаться не надо. Параметр `IEntityFixedTick` в исходнике называется `deltaTime`, хотя документация (`Docs/Entities/Behaviours/IEntityFixedTick.md`) называет его `fixedDeltaTime`; ориентируйтесь на исходник.

```csharp
public interface IEntityDispose : IEntityBehaviour
{
    void Dispose(IEntity entity);
}
```

Отпустить ссылки, отписаться от событий, вернуть ресурсы в пул. Зовётся из `Entity.Deinitialize()`, а также из `DelBehaviour`/`DelBehaviourAt`/`ClearBehaviours`, когда `_initialized == true`. Парен с `IEntityInit`, не с `IEntityDisable`.

```csharp
#if UNITY_5_3_OR_NEWER
public interface IEntityGizmos : IEntityBehaviour
{
    void DrawGizmos(IEntity entity);
}
#endif
```

Отладочная визуализация без `MonoBehaviour`. Сам интерфейс компилируется и в плеере (`UNITY_5_3_OR_NEWER`), но вся точка вызова — `Entities/Scripts/Entities/Unity/MonoEntity_Gizmos.cs` — целиком обёрнута в `#if UNITY_EDITOR`; там же объявлены `MonoEntity : IGizmosSource` и `event Action OnGizmosDraw`. То есть в плеер-билде `IEntityGizmos`-поведение — мёртвый груз, которое никто не вызовет. Оборачивайте свои gizmos-поведения в `#if UNITY_EDITOR`.

### Контракты жизненного цикла (источники)

```csharp
public interface IInitSource : IDisposable
{
    event Action OnInitialized;
    event Action OnDisposed;
    bool Initialized { get; }
    void Init();
}
```

`Dispose()` приходит из `IDisposable` и на интерфейсе не переобъявлен.

```csharp
public interface IEnableSource
{
    event Action OnEnabled;
    event Action OnDisabled;
    bool Enabled { get; }
    void Enable();
    void Disable();
}
```

```csharp
public interface ITickSource
{
    event Action<float> OnTicked;
    event Action<float> OnFixedTicked;
    event Action<float> OnLateTicked;
    void Tick(float deltaTime);
    void FixedTick(float deltaTime);
    void LateTick(float deltaTime);
}
```

Именно этот тип хранит и прокачивает `TickableManager`. Так как тик — обычный вызов метода интерфейса, а не сообщение Unity, весь граф сущностей можно прогонять из EditMode-теста без плеер-лупа.

```csharp
#if UNITY_EDITOR
public interface IGizmosSource
{
    event Action OnGizmosDraw;
}
#endif
```

Только редактор. Любой код, который его упоминает, должен быть под тем же `#if`.

### Подписки и `When*`

Все восемь подписок — байт в байт одна форма. Канонический пример:

```csharp
public readonly struct InitSubscription : IDisposable
{
    private readonly IInitSource _source;
    private readonly Action _callback;

    public InitSubscription(IInitSource source, Action callback)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        _source.OnInitialized += _callback;
    }

    public void Dispose()
    {
        _source.OnInitialized -= _callback;
    }
}
```

Остальные отличаются только источником, событием и типом делегата:

| тип | источник | событие | делегат |
|---|---|---|---|
| `InitSubscription` | `IInitSource` | `OnInitialized` | `Action` |
| `DisposeSubscription` | `IInitSource` | `OnDisposed` | `Action` |
| `EnableSubscription` | `IEnableSource` | `OnEnabled` | `Action` |
| `DisableSubscription` | `IEnableSource` | `OnDisabled` | `Action` |
| `TickSubscription` | `ITickSource` | `OnTicked` | `Action<float>` |
| `FixedTickSubscription` | `ITickSource` | `OnFixedTicked` | `Action<float>` |
| `LateTickSubscription` | `ITickSource` | `OnLateTicked` | `Action<float>` |
| `GizmosSubscription` | `IGizmosSource` | `OnGizmosDraw` | `Action` (под `#if UNITY_EDITOR`) |

`DisposeSubscription` берёт `IInitSource`, а не отдельный `IDisposeSource` — тип `IDisposeSource`, на который ссылается XML-док в `IEntityDispose.cs`, в кодовой базе отсутствует. `GizmosSubscription` существует в исходниках, но у неё нет .md-файла и её нет в списке API в `Docs/Entities/Lifecycle/Manual.md`.

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static InitSubscription WhenInit(this IInitSource source, Action action) => new(source, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisposeSubscription WhenDispose(this IInitSource source, Action action) => new(source, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EnableSubscription WhenEnable(this IEnableSource source, Action action) => new(source, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DisableSubscription WhenDisable(this IEnableSource source, Action action) => new(source, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TickSubscription WhenTick(this ITickSource source, Action<float> action) => new(source, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedTickSubscription WhenFixedTick(this ITickSource source, Action<float> action) => new(source, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LateTickSubscription WhenLateTick(this ITickSource source, Action<float> action) => new(source, action);

#if UNITY_EDITOR
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WhenGizmos(this IGizmosSource source, Action action) => source.OnGizmosDraw += action;
#endif
}
```

Каждый `When*` возвращает конкретную структуру (не `IDisposable`), так что подписка не кладёт хэндл в хип. `WhenGizmos` — исключение: возвращает `void` и не отдаёт `GizmosSubscription`, поэтому отписаться через него нельзя; если нужна отписка — конструируйте `new GizmosSubscription(source, action)` напрямую.

### Хранилище поведений на `Entity`

```csharp
public partial class Entity
{
    public event Action<IEntity, IEntityBehaviour> OnBehaviourAdded;
    public event Action<IEntity, IEntityBehaviour> OnBehaviourDeleted;
    public int BehaviourCount => _behaviourCount;

    public bool HasBehaviour(IEntityBehaviour behaviour);
    public bool HasBehaviour<T>() where T : IEntityBehaviour;
    public void AddBehaviour(IEntityBehaviour behaviour);
    public bool DelBehaviour(IEntityBehaviour behaviour);
    public bool DelBehaviour<T>() where T : IEntityBehaviour;
    public void DelBehaviours<T>() where T : IEntityBehaviour;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DelBehaviourAt(int index);
    public void ClearBehaviours();
    public T GetBehaviour<T>() where T : IEntityBehaviour;
    public bool TryGetBehaviour<T>(out T behaviour) where T : IEntityBehaviour;
    public IEntityBehaviour GetBehaviourAt(int index);
    public IEntityBehaviour[] GetBehaviours();
    public T[] GetBehaviours<T>() where T : IEntityBehaviour;
    public int CopyBehaviours(IEntityBehaviour[] results);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CopyBehaviours<T>(T[] results) where T : IEntityBehaviour;
    public BehaviourEnumerator GetBehaviourEnumerator();
}
```

Что нужно знать про поведение этих методов (`Entities/Scripts/Entities/Core/Entity_Behaviours.cs`):

- `AddBehaviour(null)` бросает `ArgumentNullException` первой же строкой (это зафиксировано тестом `Tests/EditMode/Entity/EntityTests_Behaviours.cs:49` `OnBehaviourAdded_IsNotInvoked_WhenNullBehaviour`).
- `AddBehaviour` догоняет состояние: если `_initialized` — сразу зовёт `init.Init(this)`, если `_enabled` — сразу `EnableBehaviour(behaviour)`. То есть ваша игровая логика исполняется *внутри* вызова `AddBehaviour`. `DelBehaviour`/`DelBehaviourAt`/`ClearBehaviours` зеркально синхронно зовут `Disable`, затем `Dispose`.
- Все четыре мутатора плюс `AddBehaviour` в конце дёргают `OnStateChanged?.Invoke(this)`.
- `GetBehaviour<T>()` бросает голый `throw new Exception($"Entity Behaviour of type {typeof(T).Name} is not found!")`, а не возвращает `null`. Там, где отсутствие легально, — только `TryGetBehaviour<T>(out T)`.
- `GetBehaviourAt(index)` бросает `IndexOutOfRangeException`, тогда как `DelBehaviourAt(index)` на плохом индексе просто возвращает `false`.
- `DelBehaviours<T>()` содержит баг — см. «Грабли».

```csharp
public struct BehaviourEnumerator : IEnumerator<IEntityBehaviour>
{
    public IEntityBehaviour Current => _current;
    object IEnumerator.Current => _current;

    private readonly Entity _entity;
    private int _index;
    private IEntityBehaviour _current;

    public BehaviourEnumerator(Entity entity)
    {
        _entity = entity;
        _index = -1;
        _current = null;
    }

    public bool MoveNext()
    {
        if (_index + 1 == _entity._behaviourCount)
            return false;

        _current = _entity._behaviours[++_index];
        return true;
    }

    public void Reset()
    {
        _index = -1;
        _current = null;
    }

    public void Dispose()
    {
        //Nothing...
    }
}
```

Два варианта получения существуют сознательно: публичный `public BehaviourEnumerator GetBehaviourEnumerator() => new(this);` возвращает структуру по значению, а явная реализация `IEnumerator<IEntityBehaviour> IEntity.GetBehaviourEnumerator() => new BehaviourEnumerator(this);` боксирует. В горячем коде обращайтесь к конкретному `Entity`. Версионного поля у энумератора нет: он читает `_entity._behaviourCount` живым, поэтому мутация во время перебора не бросит исключение — она тихо пропустит элемент.

### Диспетчер `Entity`

```csharp
public partial class Entity
{
    public event Action OnInitialized;
    public event Action OnDisposed;
    public event Action OnEnabled;
    public event Action OnDisabled;
    public event Action<float> OnTicked;
    public event Action<float> OnFixedTicked;
    public event Action<float> OnLateTicked;

    public bool Initialized => _initialized;
    public bool Enabled => _enabled;

    public void Init();
    public void Enable();
    public void Disable();
    public void Tick(float deltaTime);
    public void FixedTick(float deltaTime);
    public void LateTick(float deltaTime);
    public void Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnDispose() { }
}
```

`Enable()` начинается с `this.Init()`; `Init()` идемпотентен (`if (_initialized) return;`), поэтому вызывать `Init()` вручную перед `Enable()` не нужно и нельзя полагаться на то, что в момент `Enable` инициализация ещё не прошла. `Tick`/`FixedTick`/`LateTick` при `!_enabled` сразу выходят.

`Dispose()` (`Entity_Lifecycle.cs`) выполняет строго такую последовательность:

```csharp
public void Dispose()
{
    this.OnDispose();
    this.Deinitialize();            // Disable() + IEntityDispose на всех поведениях, _initialized = false, OnDisposed

    if (_settings.disposeValues)
        this.DisposeValues();

    this.ClearTags();
    this.ClearValues();
    this.ClearBehaviours();

    this.OnStateChanged?.Invoke(this);

    this.UnsubscribeEvents();       // обнуляет ВСЕ поля событий

    if (_instanceId > 0)
        EntityRegistry.Instance.Unregister(this);
}
```

`UnsubscribeEvents()` зануляет `OnStateChanged`, `OnInitialized`, `OnEnabled`, `OnDisabled`, `OnTicked`, `OnFixedTicked`, `OnLateTicked`, `OnDisposed`, `OnBehaviourAdded`, `OnBehaviourDeleted`, `OnValueAdded`, `OnValueDeleted`, `OnValueChanged`, `OnTagAdded`, `OnTagDeleted`.

### Конструкторы и `Settings`

```csharp
[Serializable]
public struct Settings
{
    public bool disposeValues;
}

public Entity(
    string name = null,
    int tagCapacity = 0,
    int valueCapacity = 0,
    int behaviourCapacity = 0,
    Settings? settings = null
);

public Entity(
    string name,
    IEnumerable<string> tags,
    IEnumerable<KeyValuePair<string, object>> values,
    IEnumerable<IEntityBehaviour> behaviours,
    Settings? settings = null
);

public Entity(
    string name,
    IEnumerable<int> tags,
    IEnumerable<KeyValuePair<int, object>> values,
    IEnumerable<IEntityBehaviour> behaviours,
    Settings? settings = null
);
```

Основной конструктор делает `_settings = settings ?? new Settings {disposeValues = true};` — то есть по умолчанию `Dispose()` диспозит сохранённые значения, реализующие `IDisposable`. Кроме того, `EntityRegistry.Instance.Register(this)` стоит последней строкой основного (ёмкостного) конструктора, а два `IEnumerable`-конструктора делегируют ему через `: this(...)` и добавляют теги, значения и поведения уже после регистрации. То есть создание любой сущности имеет глобальный побочный эффект, и `IsValid`/`InstanceID` зависят от него.

`behaviourCapacity` — единственный способ избежать реаллокаций в `AddBehaviour`: `ConstructBehaviours(capacity)` делает `new IEntityBehaviour[capacity]`, а при переполнении `AddBehaviour` выполняет `Array.Resize(ref _behaviours, capacity == 0 ? 1 : capacity * 2)`. Конструкторы с `IEnumerable` считают ёмкость через LINQ `.Count()`, то есть перечисляют коллекции дважды.

### Внутренний диспатч: `EnableBehaviour` / `DisableBehaviour`

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void EnableBehaviour(IEntityBehaviour behaviour)
{
    if (behaviour is IEntityEnable enable)
        enable.Enable(this);

    if (behaviour is IEntityTick update)
        Add(ref _updates, ref _updateCount, update);

    if (behaviour is IEntityFixedTick fixedUpdate)
        Add(ref _fixedUpdates, ref _fixedUpdateCount, fixedUpdate);

    if (behaviour is IEntityLateTick lateUpdate)
        Add(ref _lateUpdates, ref _lateUpdateCount, lateUpdate);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private void DisableBehaviour(IEntityBehaviour behaviour)
{
    if (behaviour is IEntityDisable disable)
        disable.Disable(this);

    if (behaviour is IEntityTick update)
        Remove(ref _updates, ref _updateCount, update, s_updateComparer);

    if (behaviour is IEntityFixedTick fixedUpdate)
        Remove(ref _fixedUpdates, ref _fixedUpdateCount, fixedUpdate, s_fixedUpdateComparer);

    if (behaviour is IEntityLateTick lateUpdate)
        Remove(ref _lateUpdates, ref _lateUpdateCount, lateUpdate, s_lateUpdateComparer);
}
```

Это ядро производительности модуля. Обратите внимание: используется `Add`, а не `AddIfAbsent`, — один и тот же экземпляр, добавленный дважды, будет тикать дважды.

### `TickableManager`

```csharp
#if UNITY_5_3_OR_NEWER
[AddComponentMenu("")]
[DisallowMultipleComponent]
internal sealed class TickableManager : MonoBehaviour
{
    internal static TickableManager Instance { get; }
    internal void Register(ITickSource tickSource);
    internal void Unregister(ITickSource tickSource);

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = 0; i < _count; i++)
            _sources[i].Tick(deltaTime);
    }
    // FixedUpdate: Time.fixedDeltaTime; LateUpdate: Time.deltaTime — те же циклы
}
#endif
```

`internal`, то есть из игрового кода недоступен — и не нужен: `MonoEntity` регистрирует и снимает себя сам. `Register`/`Unregister` под `#if UNITY_EDITOR` сразу выходят, если `!EditorApplication.isPlaying`. `[InitializeOnEnterPlayMode]` сбрасывает флаг `_spawned`, чтобы работали проекты с отключённым domain reload. Асимметрия: `Register` проверяет `tickSource == null`, `Unregister` — нет.

### `MonoEntity`: мост в Unity

```csharp
public partial class MonoEntity : ISerializationCallbackReceiver
{
    private bool _started;

    private protected virtual void Awake()
    {
        this.Register();
        if (this.installOnAwake)
            this.Install();
    }

    private protected virtual void OnEnable()
    {
        if (this.useUnityLifecycle && _started)
        {
            this.Enable();
            TickableManager.Instance.Register(this);
        }
    }

    private protected virtual void Start()
    {
        if (this.useUnityLifecycle)
        {
            this.Init();
            this.Enable();
            TickableManager.Instance.Register(this);
        }
        _started = true;
    }

    private protected virtual void OnDisable()
    {
        if (this.useUnityLifecycle && _started)
        {
            this.Disable();
            TickableManager.Instance.Unregister(this);
        }
    }

    private protected virtual void OnDestroy()
    {
        if (this.useUnityLifecycle && _started)
        {
            this.OnDispose();
            this.Deinitialize();

            if (this.disposeValues)
                this.DisposeValues();
        }

        if (this.uninstallOnDestroy)
            this.Uninstall();

        this.ClearTags();
        this.ClearValues();
        this.ClearBehaviours();

        this.OnStateChanged?.Invoke(this);
        this.UnsubscribeEvents();
        this.Unregister();
    }

    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        this.Construct();
        this.Register();
    }
}
```

Ключевое: `Init` поведений происходит в `Start()`, не в `Awake()` — `Awake` только регистрирует и (опционально) устанавливает. Флаг `_started` нужен, чтобы самый первый `OnEnable`, который Unity вызывает до `Start`, не позвал `Enable` раньше `Init`. Обратите внимание, что «диспозящая» половина `OnDestroy` закрыта условием `useUnityLifecycle && _started`, а чистка состояния — нет, и что здесь используются сериализованные поля `disposeValues` / `uninstallOnDestroy`, а не `Entity.Settings`.

Все эти колбэки — `private protected virtual`: переопределить их можно только из той же сборки. Игровая сборка не может переопределить `Awake`/`Start`/`OnEnable`/`OnDisable`/`OnDestroy` у `MonoEntity`; расширяйтесь установщиками и поведениями.

### Установщики

```csharp
public interface IEntityInstaller
{
    void Install(IEntity entity);
}

public interface IEntityInstaller<in E> : IEntityInstaller where E : IEntity
{
    void Install(E entity);
    // default interface implementation с тем же is-not-E → InvalidCastException
}
```

```csharp
#if UNITY_5_3_OR_NEWER
public abstract class MonoEntityInstaller : MonoBehaviour, IEntityInstaller
{
#if UNITY_EDITOR
    internal Action onValidateCallback;
#endif
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
    protected virtual void OnValidate();
}

public abstract class MonoEntityInstaller<E> : MonoEntityInstaller, IEntityInstaller<E> where E : class, IEntity
{
    public sealed override void Install(IEntity entity);    // каст + InvalidCastException
    public sealed override void Uninstall(IEntity entity);
    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}
#endif
```

Это установщик, который кладут рядом с `MonoEntity` на сцене. В обобщённой версии неогобщённые `Install`/`Uninstall` помечены `sealed override` — реализуете вы только `Install(E)`. `Uninstall` виртуальный с пустым телом, то есть опционален.

```csharp
#if UNITY_5_3_OR_NEWER
public abstract class ScriptableEntityInstaller : ScriptableObject, IEntityInstaller
{
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
}

public abstract class ScriptableEntityInstaller<E> : ScriptableEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
{
    public sealed override void Install(IEntity entity);
    public sealed override void Uninstall(IEntity entity);
    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}
#endif
```

Асcетный вариант — правильный выбор, когда конфигурация установщика общая для нескольких префабов и сцен, а не дублируется на каждом `GameObject`. Для рефакторинга, где цель — переиспользование, это основной инструмент. Есть ещё `Installers/MonoEntityInstallerConfigurable.cs` — конфигурируемый сценовый установщик.

### Системы: альтернатива N поведениям

```csharp
public abstract class EntitySystemBase<E> : IDisposable where E : IEntity
{
    [Serializable]
    public class Settings
    {
        [SerializeField] public float frameBudget = 0.03f;
        [SerializeField] public AdaptiveBatching batching = new();

        [Serializable]
        public sealed class AdaptiveBatching
        {
            public int minSize = 1024;
            public int maxSize = 2048;
            public int scaleDown = 2;
            public int stepUp = 256;
        }
    }

    protected EntitySystemBase(IReadOnlyEntityCollection<E> source, Settings settings);

    public void Enable();
    public void Disable();
    public void Update(float deltaTime);

    protected abstract void OnUpdate(int batchSize, float deltaTime);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnEnable() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnDisable() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnAddEntity(E entity) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnRemoveEntity(E entity) { }

    public abstract void Dispose();
}
```

`Update` сам себя мерит и подстраивает размер пакета:

```csharp
_batchSize = frameSize > _settings.frameBudget
    ? Math.Max(_batchSize / _settings.batching.scaleDown, _settings.batching.minSize)
    : Math.Min(_batchSize + _settings.batching.stepUp, _settings.batching.maxSize);
```

Важные детали, которые видны только в исходнике: у `_batchSize` нет инициализатора, поэтому на **первом** `Update` он равен 0 и `EntitySystem<E>.OnUpdate` с его `for (int i = 0; i < batchSize; i++)` не обработает ни одной сущности; дальше он растёт 0 → 256 → 512 …, а ветка уменьшения зажата снизу `minSize` (1024), так что устойчивый минимум — 1024, ниже не опустится. Конструктор бросает `ArgumentNullException` и на `source`, и на `settings`.

```csharp
[Serializable]
public abstract class EntitySystem<E> : EntitySystemBase<E>, IDisposable where E : IEntity
{
    protected EntitySystem(IReadOnlyEntityCollection<E> source, Settings settings);

    protected abstract void Update(E entity, float deltaTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected sealed override void OnUpdate(int batchSize, float deltaTime);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected sealed override void OnAddEntity(E entity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected sealed override void OnRemoveEntity(E entity);

    public override void Dispose();
}
```

Круговой обход: `E[] _entities` + `Dictionary<E, int> _lookup`, курсор `_cursor` переносится между кадрами, удаление — swap-with-last плюс одна правка индекса в словаре (O(1)). Вы реализуете только `Update(E entity, float deltaTime)`.

```csharp
[Serializable]
public abstract class PriorityEntitySystem<E> : EntitySystemBase<E>, IDisposable where E : IEntity
{
    [Serializable]
    public new class Settings : EntitySystemBase<E>.Settings
    {
        public float cooldown = 0.25f;
        public int highPercent = 70;
        public int midPercent = 20;
        public int lowPercent => 100 - this.highPercent - this.midPercent;
    }

    protected PriorityEntitySystem(
        IReadOnlyEntityCollection<E> source,
        Settings settings,
        params IEntityTrigger<E>[] triggers
    );

    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected abstract void Update(E entity, float deltaTime);
    protected abstract EntityUpdatePriority EvaluatePriority(E entity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected void RecalculatePriorities();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected void ChangePriority(E entity);

    public override void Dispose();
}

public enum EntityUpdatePriority : byte
{
    Low = 0,
    Medium = 1,
    High = 2
}
```

Три отдельных ведра сущностей (`_highEntities`/`_midEntities`/`_lowEntities`), каждому даётся свой процент кадрового пакета, переклассификация идёт через `IEntityTrigger<E>` с кулдауном `cooldown`. Это то, что нужно для толпы, где важность зависит от дистанции или видимости, — не `EntitySystem<E>`. Во время обхода (`_isUpdating`) добавления, удаления и смены приоритета складываются в `List<Command> _commands` и применяются после, так что мутация во время `Update` безопасна.

```csharp
public static void AddTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
    where TContext : IEntity
    where TEntity : IEntity
{
    entity.WhenEnable(system.Enable);
    entity.WhenTick(system.Update);
    entity.WhenDisable(system.Disable);
    entity.WhenDispose(system.Dispose);
}
```

Плюс `AddFixedTickSystem` (через `WhenFixedTick`) и `AddLateTickSystem` (через `WhenLateTick`). Это санкционированный способ подвесить систему на жизненный цикл контекстной сущности. Две оговорки: все четыре возвращённых struct-подписки отбрасываются — отцепить систему нечем; и, в отличие от `Lifecycle/Extensions.cs`, на этих трёх расширениях нет `AggressiveInlining`.

### Прочее

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class EditModeEntityBehaviourAttribute : Attribute
{
}
```

Помечает поведение, чьи `Init`/`Enable`/`Disable`/`Dispose` должны вызываться и в редакторе вне Play Mode. Тики — нет. Проверка идёт через `InternalUtils.IsEditModeBehaviourDefined(behaviour)` → `obj.GetType().IsDefined(...)`, причём **внутри цикла по поведениям** в `MonoEntity_Editor.InitInEditMode` (строка 98), `EnableInEditMode` (116), `DisableInEditMode` (134) и `DisposeInEditMode` (152). То есть один reflection-вызов на поведение на каждый проход редакторного жизненного цикла, который `Compile`/`Reset`/`OnValidate` могут дёргать многократно. В рантайме это ничего не стоит — весь файл под `#if UNITY_EDITOR`. XML-док атрибута ссылается на `IEntityBehaviour{T}` — такого типа в кодовой базе нет.

```csharp
internal static class InternalUtils
{
    internal static readonly float DeltaTick = 1f / Stopwatch.Frequency;
    internal static readonly int[] PrimeTable;

    internal static long GetTimestamp();
    internal static int CeilToPrime(int value, out int index);
    internal static bool IsEditModeBehaviourDefined(object obj);

    internal static void Add<T>(ref T[] array, ref int count, T item);
    internal static bool AddIfAbsent<T>(ref T[] array, ref int count, T item, IEqualityComparer<T> comparer,
        int initialCapacity = 1);
    internal static bool Contains<T>(T[] array, T item, int count, IEqualityComparer<T> comparer);
    internal static void Expand<T>(ref T[] array);
    internal static bool Remove<T>(ref T[] array, ref int count, T item, IEqualityComparer<T> comparer);
}
```

`internal` — из игрового кода недоступен, но это домашний стиль, который стоит копировать: `ref T[] array` и `ref int count` передаются вместе, рост — удвоение с зажимом на `int.MaxValue`, компаратор передаётся явным параметром (никакого бокса на struct-T). `Remove` — это та половина, которая объясняет семантику тиковых массивов: компакция через `Array.Copy` с сохранением порядка плюс `array[count] = default` (сброс ссылки для GC). `CeilToPrime`/`PrimeTable` — другая политика роста, для хэш-хранилища значений.

## Идиомы

### 1. Разрешить один раз в `Init`, в тике использовать кэш

Это главный паттерн фреймворка и единственный пример в документации (`Docs/Entities/Behaviours/Manual.md`), который в тиковом пути сделан правильно.

```csharp
//Controller that moves entity by its direction
public sealed class MoveBehaviour : IEntityInit, IEntityFixedTick
{
    private IVariable<Vector3> _position;
    private IValue<float> _moveSpeed;
    private IValue<Vector3> _moveDirection;

    //Calls when Entity.Init()
    public void Init(IEntity entity)
    {
        _position = entity.GetValue<IVariable<Vector3>>("Position");
        _moveSpeed = entity.GetValue<IValue<float>>("MoveSpeed");
        _moveDirection = entity.GetValue<IValue<Vector3>>("MoveDirection");
    }

    //Calls when Entity.FixedTick()
    public void FixedTick(IEntity entity, float deltaTime)
    {
        Vector3 direction = _moveDirection.Value;
        if (direction != Vector3.zero)
            _position.Value += _moveSpeed.Value * deltaTime * direction;
    }
}
```

Правильной эту форму делают четыре вещи. `sealed` — нет виртуального диспатча, JIT может девиртуализовать. Все разрешения значений вынесены в `Init`, поэтому в кадре нет ни одного обращения к словарю ключей. Поведение держит **реактивный хэндл** (`IVariable<Vector3>` / `IValue<float>`), а не снимок значения, — читает живое состояние без повторного разрешения, и при этом не боксирует, потому что `IVariable<T>`/`IValue<T>` — ссылочные типы. И реализовано ровно два интерфейса под одну ответственность, причём `FixedTick`, а не `Tick`, потому что это интегрирование движения по времени. Проверка `direction != Vector3.zero` отсекает запись в простое — иначе реактивная переменная зря дёргает событие изменения.

**Антипаттерн** — то, что показано на страницах отдельных интерфейсов (`IEntityFixedTick.md`, `IEntityFixedTick`1.md`) и что переносить в проект нельзя:

```csharp
// ПЛОХО
public class MoveBehaviour : IEntityFixedTick
{
    public void FixedTick(IEntity entity, float deltaTime)
    {
        Transform transform = entity.GetValue<Transform>("Transform");
        float speed = entity.GetValue<float>("Speed");
        Vector3 direction = entity.GetValue<Vector3>("MoveDirection");
        transform.position += direction * (speed * deltaTime);
    }
}
```

Три разрешения по строковому ключу каждый фиксированный кадр; `GetValue<float>` и `GetValue<Vector3>` на значимых типах ещё и распаковывают (`Entity_Values.cs:117` — `return (T) slot.value;`, хранилище типизировано как `object`). Не `sealed`. Умножьте на число сущностей. Примеры в документации написаны для краткости, не для пропускной способности.

### 2. Типизированное поведение под конкретный тип сущности

```csharp
public class UnitEntity : Entity
{
}

public class InitColorBehaviour : IEntityInit<UnitEntity>
{
    public void Init(UnitEntity entity)
    {
        var renderer = entity.GetValue<Renderer>("Renderer");
        var color = entity.GetValue<Color>("Color");
        renderer.material.color = color;
    }
}
```

`IEntityInit<E>` вместо `IEntityInit` даёт типизацию параметра бесплатно: мост по умолчанию делает `is not E` и бросает `InvalidCastException` с указанием ожидаемого и полученного типов. Берите обобщённую форму, когда поведение имеет смысл только на одном типе сущности, — ошибка всплывёт понятным исключением на `Init`, а не `NullReferenceException` где-то глубоко в тике. Неогобщённую форму — для действительно универсальных поведений.

**Антипаттерн** — каст руками, ровно то, для устранения чего существует обобщённый интерфейс:

```csharp
// ПЛОХО
public class InitColorBehaviour : IEntityInit
{
    public void Init(IEntity entity)
    {
        var unit = (UnitEntity) entity; // InvalidCastException без контекста
        // ...
    }
}
```

### 3. `Enable`/`Disable` как симметричная пара в одном классе

```csharp
public sealed class RendererToggleBehaviour : IEntityInit, IEntityEnable, IEntityDisable
{
    private Renderer _renderer;

    public void Init(IEntity entity) => _renderer = entity.GetValue<Renderer>("Renderer");

    public void Enable(IEntity entity) => _renderer.enabled = true;

    public void Disable(IEntity entity) => _renderer.enabled = false;
}
```

В документации это разнесено на два класса (`EnableRendererBehaviour` в `IEntityEnable.md` и `DisableRendererBehaviour` в `IEntityDisable.md`) только потому, что у каждого интерфейса своя страница. В рабочем коде пара живёт в одном классе: `Renderer` кэширован в `Init` (ссылочный тип, он не меняется), а `Enable`/`Disable` остаются тривиальными. Правило симметрии: что включил `Enable`, то выключает `Disable`; что захватил `Init`, то отпускает `Dispose`.

**Антипаттерн** — захват в `Enable`, освобождение в `Dispose`. `Enable` может сработать сто раз на один `Dispose`, то есть это утечка одной подписки на каждое переиспользование из пула.

### 4. `Tick` пишет намерение, `FixedTick` его потребляет

```csharp
public sealed class MoveController : IEntityInit, IEntityTick
{
    private IVariable<Vector3> _moveDirection;

    public void Init(IEntity entity) =>
        _moveDirection = entity.GetValue<IVariable<Vector3>>("MoveDirection");

    public void Tick(IEntity entity, float deltaTime)
    {
        float dx = Input.GetAxis("Horizontal");
        float dz = Input.GetAxis("Vertical");
        _moveDirection.Value = new Vector3(dx, 0f, dz);
    }
}
```

Разделение труда: ввод или ИИ работает в `Tick` и только **пишет** намерение в состояние сущности; механика работает в `FixedTick` и это намерение **читает** (`MoveBehaviour` из идиомы 1). Друг друга они не вызывают — общаются исключительно через значения сущности. Именно это делает поведения независимо добавляемыми, удаляемыми и заменяемыми в рантайме: подменив `MoveController` на ИИ-поведение, пишущее тот же `"MoveDirection"`, вы не тронете движение.

Запись идёт в разрешённую один раз `IVariable<Vector3>`, а не через `entity.SetValue(...)`. **Пример из документации (`Docs/Entities/Behaviours/IEntityTick.md:31`) здесь не компилируется:**

```csharp
// НЕ КОМПИЛИРУЕТСЯ
entity.SetValue<Vector3>("MoveDirection", moveDirection);
```

Для строкового ключа объявлен только неогобщённый `public static void SetValue(this IEntity entity, string key, object value)` (`Entities/Extensions_Values.cs:160`). Обобщённые перегрузки — `SetValue<T>(this IEntity, ValueKey<T>, T)` (164) и `SetValue<E, T>(this E, ValueKey<E, T>, T)` (168) — первым аргументом `string` не принимают, поэтому явный аргумент типа `<Vector3>` ломает разрешение перегрузок. Компилирующиеся варианты: `entity.SetValue("MoveDirection", moveDirection)` (боксирует `Vector3` в `object`) или типизированный ключ (идиома 11). Заметьте: `GetValue<T>(this IEntity, string)` существует (строка 92), так что Get-примеры в документации корректны — сломан только `SetValue`.

### 5. `LateTick` для камеры и презентации

```csharp
public sealed class CameraFollowBehaviour : IEntityInit, IEntityLateTick
{
    private Camera _camera;
    private Transform _target;
    private IValue<float> _smoothSpeed;

    public void Init(IEntity entity)
    {
        _camera = entity.GetValue<Camera>("Camera");
        _target = entity.GetValue<Transform>("Target");
        _smoothSpeed = entity.GetValue<IValue<float>>("SmoothSpeed");
    }

    public void LateTick(IEntity entity, float deltaTime)
    {
        Transform cameraTransform = _camera.transform;
        cameraTransform.position = Vector3.Lerp(
            cameraTransform.position,
            _target.position,
            _smoothSpeed.Value * deltaTime
        );
    }
}
```

Выбор фазы здесь — вопрос корректности, а не стиля: всё, что читает трансформ после того, как геймплей его подвинул, должно быть в `LateTick` — камера, коррекция анимации, партиклы, SFX, UI, синхронизация трансформов. Пример в `IEntityLateTick.md` делает три `GetValue` внутри `LateTick`; здесь они вынесены в `Init`, потому что `Camera` и `Transform` — ссылочные типы, которые не меняются.

**Антипаттерн** — та же логика в `Tick`: камера будет гнаться за позицией прошлого кадра.

### 6. Gizmos-поведение, завязанное на тег

```csharp
#if UNITY_EDITOR
public sealed class SelectionGizmoBehaviour : IEntityGizmos
{
    public void DrawGizmos(IEntity entity)
    {
        if (entity.HasTag("Selected"))
        {
            Vector3 position = entity.GetValue<Vector3>("Position");
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(position, 0.5f);
        }
    }
}
#endif
```

Отладочная визуализация без `MonoBehaviour` и без `OnDrawGizmos`-бойлерплейта на ваших типах, управляемая состоянием сущности, поэтому своего состояния у поведения нет.

**Антипаттерн — само объявление в документации** (`Docs/Entities/Lifecycle/Sources/IGizmosSource.md`): там написано `public sealed class SelectionGizmoBehaviour : IEntityBehaviour, IEntityGizmos`, а `IEntityGizmos` уже наследует `IEntityBehaviour` — упоминание маркера избыточно и копировать его не надо. Второе: без `#if UNITY_EDITOR` класс попадёт в плеер-билд, где его никто никогда не вызовет (точка вызова — `MonoEntity_Gizmos.cs` — целиком редакторная).

### 7. Хранить подписку как конкретную структуру, отписываться через `Dispose`

```csharp
//Assume we have an instance of entity
IEntity entity = ...;

// Subscribe to initialization
InitSubscription initSubscription = entity.WhenInit(() => Console.WriteLine("Initialized!"));

//Unsubscribe from initialization
initSubscription.Dispose();

// Subscribe to frame updates
TickSubscription tickSubscription = entity.WhenTick(delta => Console.WriteLine($"Tick: {delta}"));

// Unsubscribe from frame updates
tickSubscription.Dispose();
```

Обратите внимание на объявленный тип: `InitSubscription`, `TickSubscription` — конкретная `readonly struct`, никогда не `IDisposable`. Так как `When*` помечен `AggressiveInlining` и возвращает структуру по значению, подписка не аллоцирует ничего, кроме самого делегата. Хэндл ещё и делает отписку невозможной для ошибки: нельзя случайно сделать `-=` другой лямбды, чем та, которую сделали `+=`.

**Антипаттерн** — боксировать структуру, тем самым обнуляя весь смысл дизайна:

```csharp
// ПЛОХО: структура уезжает в хип
IDisposable sub = entity.WhenTick(delta => { /* ... */ });

// ТОЖЕ ПЛОХО: сырые события, которые подписки и призваны заменить
entity.OnTicked += this.OnTick;
// ...позже, в другом месте, надо вспомнить ровно тот же делегат:
entity.OnTicked -= this.OnTick;
```

### 8. Конструировать подписку напрямую

```csharp
//Assume we have an instance of IEnableSource
IEnableSource enableSource = ...;

// Subscribe to the disable event
var subscription = new DisableSubscription(enableSource, () =>
    Console.WriteLine("Source has been disabled"));

// Disabling source triggers the subscription
enableSource.Disable();

// Later, we can unsubscribe from the source
subscription.Dispose();
```

Эквивалент `enableSource.WhenDisable(...)`; конструктор — более низкоуровневая форма, оба варианта проверяют `source` и `callback` на `null` с `ArgumentNullException`. Здесь `var` допустим, потому что тип структуры уже назван в правой части. В обычном коде предпочитайте `When*`.

**Антипаттерн** — обернуть подписку в `using`. Структура реализует `IDisposable`, так что компилятор это позволит, но для подписок на жизненный цикл `using` почти всегда неправильно: он диспозит в конце блока, то есть подписка исчезнет раньше, чем событие успеет сработать. Держите хэндл в поле. Документация подписок про `using` ничего не говорит — единственное её примечание к `Dispose` это «Safe to call multiple times».

### 9. Сборка сущности: теги → значения → поведения

```csharp
//Create a new entity with pre-sized behaviour storage
var character = new Entity("Character", behaviourCapacity: 4);

//Add tags
character.AddTag("Moveable");

//Add properties
character.AddValue("Position", new ReactiveVariable<Vector3>());
character.AddValue("MoveSpeed", new Const<float>(3.5f));
character.AddValue("MoveDirection", new ReactiveVariable<Vector3>());

//Add behaviour
character.AddBehaviour<MoveBehaviour>();
```

Порядок — это контракт: поведения разрешают значения в `Init`, значит значения должны быть на месте. Выбор типа значения тоже важен: `Const<float>` для того, что не меняется (нет события изменения, нет реактивной обвязки), `ReactiveVariable<Vector3>` для состояния, за которым наблюдают; взять `Const`, где он применим, — бесплатная производительность. `AddBehaviour<MoveBehaviour>()` использует расширение с ограничением `where T : IEntityBehaviour, new()`, поэтому поведениям нужен публичный конструктор без параметров, а всю проводку знает только установщик.

`behaviourCapacity: 4` — отличие от примера в документации (`new Entity("Character")`), который стартует с массива длины 0: сущность с шестью поведениями реаллоцируется на 1, 2, 4 и 8. У `MonoEntity` аналог — сериализованное `initialBehaviourCapacity`, которое выставляет `MonoEntity_Editor.PrecomputeCapacities()`.

**Антипаттерн** — поведения раньше значений: на уже инициализированной сущности `AddBehaviour` немедленно зовёт `Init`, и `MoveBehaviour.Init` не найдёт `"Position"`.

### 10. Прогон жизненного цикла в тесте

```csharp
[Test]
public void MoveBehaviour_MovesPositionAlongDirection()
{
    var entity = new Entity("Character", behaviourCapacity: 1);
    entity.AddValue("Position", new ReactiveVariable<Vector3>());
    entity.AddValue("MoveSpeed", new Const<float>(2f));
    entity.AddValue("MoveDirection", new ReactiveVariable<Vector3>(Vector3.right));
    entity.AddBehaviour<MoveBehaviour>();

    entity.Enable();            // Enable() сам вызовет Init()
    entity.FixedTick(0.5f);

    Assert.AreEqual(Vector3.right, entity.GetValue<IValue<Vector3>>("Position").Value);
}
```

Так как тик — обычный вызов метода на `ITickSource`, а не сообщение Unity, весь граф сущностей прогоняется из EditMode-теста без плеер-лупа, Play Mode и кадров. Эта тестируемость — основная причина предпочитать поведения `MonoBehaviour`-ам, и команде стоит писать тесты именно так. Обратите внимание, что `FixedTick` берёт свой шаг, отдельный от `deltaTime`.

### 11. Типизированные ключи вместо строк

```csharp
public static class CharacterAPI
{
    public static readonly ValueKey<IVariable<Vector3>> Position = new("Position");
    public static readonly ValueKey<IValue<Vector3>> MoveDirection = new("MoveDirection");
}

public sealed class MoveBehaviour : IEntityInit, IEntityFixedTick
{
    private IVariable<Vector3> _position;
    private IValue<Vector3> _moveDirection;

    public void Init(IEntity entity)
    {
        _position = entity.GetValue(CharacterAPI.Position);
        _moveDirection = entity.GetValue(CharacterAPI.MoveDirection);
    }

    public void FixedTick(IEntity entity, float deltaTime) =>
        _position.Value += _moveDirection.Value * deltaTime;
}
```

`ValueKey<T>` и `ValueKey<E, T>` — `readonly struct` с единственным полем `public readonly int Id`; конструктор от строки один раз делает `EntityKeyStore.NameToId(name)`. Дальше `GetValue<T>(this IEntity, ValueKey<T>)` идёт прямо по `key.Id`, минуя статический `Dictionary<string, int>`. Это компилируемо-проверяемый путь, и для рефакторинга, целящегося в производительность, направление движения — от строк к ключам. Для `ValueKey<E, T>` есть пара, привязывающая ещё и тип сущности, и `public static ref T GetValueUnsafe<T>(this IEntity entity, ValueKey<T> key) where T : class` — `ref`-доступ для ссылочных значений.

### 12. Подвесить систему на жизненный цикл контекста

```csharp
public sealed class MovementSystem : EntitySystem<IEntity>
{
    public MovementSystem(IReadOnlyEntityCollection<IEntity> source, Settings settings)
        : base(source, settings)
    {
    }

    protected override void Update(IEntity entity, float deltaTime)
    {
        IVariable<Vector3> position = entity.GetValue<IVariable<Vector3>>("Position");
        IValue<Vector3> direction = entity.GetValue<IValue<Vector3>>("MoveDirection");
        position.Value += direction.Value * deltaTime;
    }
}

// в установщике контекстной сущности:
gameContext.AddFixedTickSystem(new MovementSystem(world, new EntitySystemBase<IEntity>.Settings()));
```

Когда тысячи сущностей делают одно и то же, одна система по отфильтрованной коллекции дешевле N поведений `IEntityTick`: горячий цикл идёт по `E[]` по индексу, удаление O(1), размер пакета подстраивается под `frameBudget`, а курсор переносится между кадрами, так что работа растягивается вместо пика. `AddFixedTickSystem` связывает `WhenEnable → Enable`, `WhenFixedTick → Update`, `WhenDisable → Disable`, `WhenDispose → Dispose` — четыре строки вместо ручной проводки.

Оговорка по `Update` в этом примере: разрешения значений здесь остались в горячем пути, потому что система не имеет своего `Init` на сущность. Если это измеримо, разрешайте хэндлы в `OnAddEntity(E entity)` и держите их в словаре или в самой сущности.

### 13. Фильтрованный запрос поведений без аллокаций

```csharp
public sealed class BehaviourReportBuilder
{
    private readonly IEntityTick[] _buffer = new IEntityTick[64];

    public int Collect(Entity entity)
    {
        int count = entity.CopyBehaviours<IEntityTick>(_buffer);
        for (int i = 0; i < count; i++)
        {
            // ... работа с _buffer[i]
        }
        return count;
    }
}
```

`CopyBehaviours<T>(T[] results)` заполняет переиспользуемый буфер и возвращает число записанных элементов, уважая `results.Length` как верхнюю границу, — это полностью безаллокационный вариант. `GetBehaviours<T>()` для разовых запросов тоже сделан аккуратно (собирает индексы совпадений в `Span<int> indexes = stackalloc int[_behaviourCount]` и аллоцирует результат ровно один раз нужного размера, возвращая `Array.Empty<T>()` на пустой сущности), но в повторяющемся коде берите `CopyBehaviours<T>`.

## Производительность

Отмечено, что именно сказано в исходнике, а что — вывод.

**Проверки типов — один раз, на `Enable`.** `Entity.EnableBehaviour` (`Entities/Scripts/Entities/Core/Entity_Behaviours.cs`) делает `is IEntityEnable` / `is IEntityTick` / `is IEntityFixedTick` / `is IEntityLateTick` и добавляет в три выделенных массива через `InternalUtils.Add`. `Tick`/`FixedTick`/`LateTick` в `Entity_Lifecycle.cs` проходят по `IEntityTick[] _updates` и т. д. по индексу без единой проверки типа. *Вывод:* кадровая стоимость пропорциональна числу тикающих поведений, не общему; `IEntityInit`-only поведение не стоит в кадре ничего.

**Фазовые массивы — обычные массивы с явным счётчиком.** `IEntityTick[] _updates; int _updateCount;` — непрерывная память, обход по индексу, никаких энумераторов, никаких version-проверок `List<T>`. То же для самого `_behaviours`. Источник: `Entity_Lifecycle.cs`, `Entity_Behaviours.cs`.

**Отключённая сущность стоит один `bool`.** Все три тика начинаются с `if (!_enabled) return;`. Сверх того `DisableBehaviour` вынимает поведение из тиковых массивов, так что отключённую сущность не обходят даже на уровне отдельных поведений. Источник: `Entity_Lifecycle.cs`.

**Границы циклов читаются заново на каждой итерации.** `for (int i = 0; i < _updateCount; i++)` — поле, не локальная копия. Это то, что останавливает фазу, если поведение внутри вызвало `entity.Disable()`: `Disable()` проходит `DisableBehaviour` по всем поведениям, `InternalUtils.Remove` уменьшает счётчик, счётчик падает до 0, цикл заканчивается. Это зафиксированный контракт, одинаковый для всех трёх фаз: в `Tests/EditMode/Entity/EntityTests_Lifecycle.cs` тесты `OnUpdate_StopsCallingIfEntityDisabledMidLoop` (1010), `OnFixedUpdate_StopsIfEntityDisabledMidIteration` (1077) и `OnLateUpdate_StopsIteration_WhenDisabledMidUpdate` (1164) ставят `DisableDuringTickTestDouble` / `DisableDuringFixedTickTestDouble` / `DisableDuringLateTickTestDouble` первым, спай вторым, и во всех трёх случаях утверждают, что спай не был вызван. Условие `&& _enabled` в `Entity.LateTick` — избыточно, а в `MonoEntity.LateTick` (`MonoEntity_Lifecycle.cs:243`) его вообще нет; никакой асимметрии между фазами здесь не существует.

**Обратная сторона того же механизма.** Если поведение во время `Tick` вызывает `DelBehaviour` на себе или на соседе, оно мутирует перебираемый массив; `Remove` сдвигает элементы влево, а `i` всё равно инкрементится, поэтому поведение, переехавшее в освободившийся слот, в этом кадре будет **пропущено**. *Вывод из кода `InternalUtils.Remove` + формы циклов; теста на это нет.*

**`BehaviourEnumerator` — struct с пустым `Dispose`.** Ловушка бокса реальна и заложена двумя перегрузками: `public BehaviourEnumerator GetBehaviourEnumerator() => new(this);` против `IEnumerator<IEntityBehaviour> IEntity.GetBehaviourEnumerator() => new BehaviourEnumerator(this);`. Версионного поля нет — энумератор читает `_entity._behaviourCount` живым, поэтому «перебрать и удалить» не бросит исключение, а тихо пропустит элемент. Источник: `Entity_Behaviours.cs`.

**Все восемь подписок — `readonly struct` с двумя ссылочными полями.** 16 байт на x64, нулевая аллокация хэндла, и `readonly` позволяет JIT не делать защитных копий. Единственная аллокация при подписке — сам делегат/замыкание. Источник: `Entities/Scripts/Lifecycle/Subscriptions/*.cs`. *Размер в байтах — вывод из layout, в исходнике не заявлен.*

**`AggressiveInlining` проставлен широко.** Все семь `When*` в `Lifecycle/Extensions.cs` плюс `WhenGizmos`; `AddBehaviour<T>`, оба `AddBehaviours`, оба `DelBehaviours`, `DelBehaviourAt`, `CopyBehaviours<T>` в `Extensions_Behaviours.cs` и `Entity_Behaviours.cs`; `ConstructBehaviours`, `EnableBehaviour`, `DisableBehaviour`, `Deinitialize`, `UnsubscribeEvents`, `OnDispose`; все методы `InternalUtils`; `OnEnable`/`OnDisable`/`OnAddEntity`/`OnRemoveEntity` в `EntitySystemBase`. Исключение, которое стоит знать: на `AddTickSystem`/`AddFixedTickSystem`/`AddLateTickSystem` в `Systems/Extensions.cs` атрибута **нет**.

**Компараторы для удаления закэшированы в статических полях.** `s_updateComparer = EqualityComparer<IEntityTick>.Default`, и так же для fixed/late (`Entity_Lifecycle.cs`); `TickableManager` делает то же через `s_comparer` для `ITickSource`. Каждый `Remove` не переразрешает `EqualityComparer<T>.Default`, а явная передача компаратора означает отсутствие бокса. Заметьте, что `MonoEntity` держит собственные копии этих статиков.

**Рост массивов — удвоение с зажимом на переполнение.** `InternalUtils.Expand`: `int newCapacity = array.Length == 0 ? 1 : array.Length * 2; if (newCapacity < 0) newCapacity = int.MaxValue;`. `Entity.AddBehaviour` повторяет ту же политику инлайн: `Array.Resize(ref _behaviours, capacity == 0 ? 1 : capacity * 2)` — то есть не через `Expand`. `ConstructBehaviours(int capacity = 0)` по умолчанию делает массив нулевой длины, поэтому путь `new Entity("Name")` реаллоцируется на 1, 2, 4, 8 … — передавайте `behaviourCapacity`.

**Удаление из списка поведений сохраняет порядок.** Компакция через `Array.Copy`, O(n), плюс `_behaviours[_behaviourCount] = null` — ссылка сбрасывается, чтобы её собрал GC. `EntitySystem<E>`, наоборот, использует swap-with-last плюс правку индекса в `Dictionary<E, int>` и получает O(1), потому что порядок ему не нужен.

**`EntitySystem<E>` — безаллокационный горячий цикл.** `E[] _entities` плюс `Dictionary<E, int> _lookup`, словарь предразмерен от `source.Count`, пол массива 4, курсор переносится между кадрами. Замер времени делается через `Stopwatch.GetTimestamp() * InternalUtils.DeltaTick` (предвычисленное `1f / Stopwatch.Frequency`), а не через `Time.realtimeSinceStartup`. `ProfilerMarker` создаётся один раз в конструкторе под `#if ENABLE_PROFILER` (`new ProfilerMarker(this.GetType().Name + ".Update")`) — копируйте этот приём для своих систем. Шероховатость: swap оставляет устаревшую дублирующую ссылку в хвостовом слоте, и чистит её только `Array.Clear` в `Dispose`, так что удалённые сущности остаются достижимы для GC, пока слот не перезапишут.

**Адаптивный батчинг стартует с нуля.** У `_batchSize` в `EntitySystemBase<E>` нет инициализатора — на первом `Update` он 0, и `EntitySystem.OnUpdate` с `for (int i = 0; i < batchSize; i++)` не обработает ни одной сущности. Далее 0 → 256 → 512 …, а ветка уменьшения зажата `minSize` (1024) — устойчивый минимум 1024, ниже не бывает. Это важно, если сущностей заметно меньше 1024: пакет всё равно будет не меньше `minSize`, то есть при `count < batchSize` код обрежет пакет до `count` и обойдёт всех за кадр.

**Один `Update` вместо N.** `TickableManager` заменяет N колбэков `MonoBehaviour.Update` одним скрытым `MonoBehaviour`, который идёт по `ITickSource[] _sources` по индексу в `Update`/`FixedUpdate`/`LateUpdate`, подняв `Time.deltaTime`/`Time.fixedDeltaTime` один раз на фазу. Регистрация через `AddIfAbsent`, так что дубликаты не накапливаются. *Утверждение «это крупнейший выигрыш фреймворка по пропускной способности» — вывод (устранение нативного message dispatch Unity), а не замер. Набор бенчмарков в репозитории есть — `Tests/Performance/` на `Unity.PerformanceTesting` (`EntityPerformance_Behaviours`/`_Tags`/`_Values`, `EntityCollectionPerformance`, `GameObject_Performance`), — но ни один из них не измеряет диспатч тиков: там Add/Del/Get/Has/перебор поведений, тегов и значений.*

**Бокс, за которым надо следить в коде поведений.** Хранилище значений типизировано как `object` (`Entity_Values.cs:117` — `return (T) slot.value;`), поэтому `entity.GetValue<float>("Speed")` и `GetValue<Vector3>(...)` на значимых типах распаковывают на каждом чтении, а `entity.SetValue("key", vector)` боксирует на каждой записи. Канонический `MoveBehaviour` из `Manual.md` этого избегает, кэшируя `IValue<float>`/`IVariable<Vector3>` в `Init`. Плюс каждый строковый доступ — это сначала `EntityKeyStore.NameToId` (проба статического `Dictionary<string, int>`), потом проба бакета самой сущности; типизированные ключи первую пробу убирают.

**Хэш-хранилище значений растёт по таблице простых.** `InternalUtils.PrimeTable` плюс `CeilToPrime(int value, out int index)` через `Array.BinarySearch` — размеры предвычислены, а не считаются на каждой вставке. Это другая политика, чем удвоение для поведений.

**`EntitySystem<E>.Dispose()` не отписывается от источника.** Он делает только `Array.Clear`, `_lookup.Clear()` и `_entityCount = 0`; отписка `_source.OnAdded` / `_source.OnRemoved` живёт в `Disable()`. Диспозить, не отключив, означает оставить два делегата висеть на коллекции-источнике.

## Грабли

- **`Entity.DelBehaviours<T>()` пропускает элементы.** `for (int i = 0; i < _behaviourCount; i++) if (_behaviours[i] is T) this.DelBehaviourAt(i);` — `DelBehaviourAt` компактит массив через `Array.Copy`, но `i` всё равно инкрементится, поэтому из двух **соседних** подходящих поведений второе останется. Тот же баг дословно продублирован в `MonoEntity_Behaviours.cs:102`. Теста на `DelBehaviours<T>` нет нигде в `Tests/EditMode/Entity/` — поэтому он и жив. Идите по списку вниз сами или вызывайте `DelBehaviour(instance)` поштучно.
- **`GetBehaviour<T>()` бросает, а не возвращает `null`** — и бросает голый `throw new Exception($"Entity Behaviour of type {typeof(T).Name} is not found!")`, не типизированное исключение. На любом пути, где отсутствие легально, нужен `TryGetBehaviour<T>(out T)`.
- **Все поиски по поведениям линейные.** `HasBehaviour<T>`, `GetBehaviour<T>`, `DelBehaviour<T>` сканируют `_behaviours` с `is T`. Никогда в кадре — разрешайте в `Init`.
- **`AddBehaviour` на живой сущности исполняет игровую логику внутри себя.** `if (_initialized && behaviour is IEntityInit init) init.Init(this);`, затем `if (_enabled) this.EnableBehaviour(behaviour);`. `DelBehaviour` симметрично синхронно зовёт `Disable`, затем `Dispose`. Если вы делаете это из середины тика, учитывайте переупорядочивание.
- **`AddBehaviour(null)` бросает `ArgumentNullException`**, тогда как `DelBehaviour(null)` просто возвращает `false`, а `DelBehaviourAt` на плохом индексе — тоже `false`, зато `GetBehaviourAt` бросает `IndexOutOfRangeException`. Три разных способа отреагировать на плохой аргумент в одном файле.
- **`EnableBehaviour` использует `Add`, а не `AddIfAbsent`.** Один и тот же **экземпляр** поведения, добавленный на сущность дважды, тикает дважды за кадр без единого предупреждения. Документация `EntityBehaviours.md` явно разрешает дубликаты.
- **`Enable()` неявно вызывает `Init()`.** Не вызывайте `Init()` вручную перед `Enable()` и не предполагайте, что в момент срабатывания `Enable` инициализация ещё не прошла.
- **`Enable`/`Disable` срабатывают на каждом цикле активации, `Init`/`Dispose` — раз на инициализацию.** С пулингом между одним `Init` и одним `Dispose` может пройти сотня `Enable`/`Disable`. Пара «захват/освобождение» должна лежать внутри одной пары, иначе утечка.
- **Мутация массива во время обхода тихо пропускает элементы.** `DelBehaviour` из `Tick` сдвигает элементы влево при инкрементирующемся `i` — переехавшее поведение пропущено. Энумератор `BehaviourEnumerator` версионного поля не имеет, поэтому `InvalidOperationException` вы не получите тоже.
- **`entity.Disable()` из любого тика останавливает оставшиеся поведения этой фазы** — одинаково в `Tick`, `FixedTick` и `LateTick` (зафиксировано тремя тестами). Полагаться на это как на механизм управления потоком не стоит, но знать надо.
- **`Dispose()` зануляет все поля событий** через `UnsubscribeEvents()`. Любой хэндл подписки, который вы ещё держите, после этого мёртв, и его `Dispose()` работает по сущности без подписчиков. Диспозьте свои хэндлы до сущности.
- **`default(TickSubscription).Dispose()` бросает `NullReferenceException`.** Документация обещает «Safe to call multiple times», но `Dispose` делает голый `_source.OnX -= _callback` без проверки на `null`; `null`-гарды есть только в конструкторе. Не оставляйте поле подписки неинициализированным.
- **`Extensions.WhenGizmos` возвращает `void`** и не отдаёт `GizmosSubscription` — отписаться через него нельзя. Нужна отписка — `new GizmosSubscription(source, action)` напрямую, под `#if UNITY_EDITOR`.
- **`GizmosSubscription` не документирована** — нет .md-файла, нет в списке API в `Lifecycle/Manual.md`. Список документации не равен поверхности API; проверяйте исходники.
- **Несовпадение директив компиляции вокруг gizmos.** `IEntityGizmos` под `#if UNITY_5_3_OR_NEWER` (компилируется в плеере), а `IGizmosSource`, `GizmosSubscription` и вся точка вызова `MonoEntity_Gizmos.cs` — под `#if UNITY_EDITOR`. Код, касающийся и того и другого, должен быть под `#if UNITY_EDITOR`, иначе плеер-билд сломается; а `IEntityGizmos`-поведение в плеере — мёртвый груз.
- **Gizmos ещё и слушается сериализованных флагов.** `OnDrawGizmos` отрисовывает только если `!onlySelectedGizmos`, а `OnDrawGizmosSelected` выходит при `EditorApplication.isPlaying && onlyEditModeGizmos`. Весь цикл обёрнут в `try/catch`, который гасит исключение в `Debug.LogWarning` — ваше упавшее gizmos-поведение не уронит редактор, но и не бросится в глаза.
- **Устаревшие XML-кросс-ссылки в исходниках, которые введут в заблуждение по IDE-тултипу.** `IEntityDisable<E>` ссылается на `IEnable.Disable`, `IEntityEnable<E>` — на `_IEnable.Enable`, `IEntityDispose<E>` — на `IDisposeSource.Dispose`, `EditModeEntityBehaviourAttribute` — на `IEntityBehaviour{T}`. Ни `IEnable`, ни `_IEnable`, ни `IDisposeSource`, ни `IEntityBehaviour<T>` в этой кодовой базе не существуют.
- **Документация называет параметр фиксированного тика `fixedDeltaTime`, исходник — `deltaTime`.** Это касается и `IEntityFixedTick`, и `IEntityFixedTick<E>`. При реализации ориентируйтесь на исходник; читать `Time.fixedDeltaTime` внутри метода не нужно — значение передано.
- **`entity.SetValue<Vector3>("key", value)` из `IEntityTick.md` не компилируется.** Обобщённые `SetValue` принимают только `ValueKey<T>` / `ValueKey<E, T>`, строковая перегрузка одна и неогобщённая.
- **`EditModeEntityBehaviourAttribute` включает только `Init`/`Enable`/`Disable`/`Dispose`, не тики.** Симуляция в редакторе не «идёт» — она только отрабатывает переходы состояний.
- **Редакторный `InitInEditMode` проверяет `if (_initialized) return;`, но `_initialized = true` не ставит.** В чистом edit mode гард не включается, и повторный `Compile`/`OnValidate` прогоняет `Init` заново. Зеркально `DisposeInEditMode` стоит под `if (!_initialized) return;` и в чистом edit mode не делает ничего.
- **Проверка атрибута — reflection внутри цикла по поведениям**, а не один раз при регистрации: `IsEditModeBehaviourDefined(behaviour)` вызывается в `InitInEditMode` (98), `EnableInEditMode` (116), `DisableInEditMode` (134), `DisposeInEditMode` (152). В рантайме бесплатно, в редакторе при `autoCompile` — нет.
- **`MonoEntity` инициализирует поведения в `Start()`, не в `Awake()`.** `Awake` только `Register()` и (опционально) `Install()`. Значит, на `Init` поведения нельзя рассчитывать из `Awake` другого объекта.
- **Колбэки Unity у `MonoEntity` — `private protected virtual`**, переопределяемы только внутри той же сборки. Игровая сборка не может переопределить `Awake`/`Start`/`OnEnable`/`OnDisable`/`OnDestroy`; расширяйтесь установщиками и поведениями.
- **`MonoEntity` — не наследник `Entity`.** Это отдельная реализация `IEntity` со своим хранилищем и своим диспетчером. Любой вывод про `Entity` на `MonoEntity` надо проверять, а не переносить. Побочно: любая оптимизация на уровне фреймворка здесь должна применяться дважды — довод в пользу обычного `Entity` плюс тонкий драйвер там, где сериализация Unity не нужна.
- **Каждый конструктор `Entity` делает `EntityRegistry.Instance.Register(this)`.** Создание сущности — глобальный побочный эффект; `Dispose` снимает регистрацию только при `_instanceId > 0`.
- **`Entity.Settings.disposeValues` по умолчанию `true`** в параметрическом конструкторе (`_settings = settings ?? new Settings {disposeValues = true}`). Если вы кладёте в значения `IDisposable`, которым владеет кто-то другой, передайте `new Settings { disposeValues = false }` явно. У `MonoEntity` это отдельное сериализованное поле `disposeValues`, не `Entity.Settings`.
- **`EntitySystem<E>.Dispose()` не отписывается от `_source.OnAdded`/`OnRemoved`** — это делает только `Disable()`. Диспозить без отключения — утечка двух делегатов.
- **`MonoEntitySingleton<E>.Instance` и `MonoEntityWorldSingleton.Instance` падают назад на `FindAnyObjectByType` и бросают, если ничего не нашли.** Разрешайте синглтоны один раз в `Init`, никогда в кадре: это скан сцены.
- **`TickableManager` — `internal sealed`.** Зарегистрировать свой `ITickSource` из игрового кода нельзя; гоняйте свои источники через `MonoEntityWorld` или собственный `MonoBehaviour`. Побочно: `Register` проверяет аргумент на `null`, `Unregister` — нет.
- **`AddTickSystem`/`AddFixedTickSystem`/`AddLateTickSystem` выбрасывают четыре struct-подписки**, которые они создают, — отцепить систему от контекста потом нечем, кроме `Dispose` самого контекста.
- **`IEntityBehaviour` сам по себе бесполезен.** Класс, реализующий только маркер, попадёт в `_behaviours` и не получит ни одного колбэка. Документация формулирует это как запрет («Don't implement this interface»), исходник и тесты — нет; практический итог один и тот же.
- **`IInitSource`, `IEnableSource`, `ITickSource` не реализуются руками.** В .md-файлах у всех трёх стоит явный NOTE: «This is a core framework interface. You should not implement it manually». Реализуйте контракты поведений, потребляйте контракты-источники.

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Документация: поведения

- `Docs/Entities/Behaviours/Manual.md` — входная точка в тему. Три шага сборки (`new Entity("Character")` → теги/значения → `AddBehaviour<MoveBehaviour>()`), полный текст канонического `MoveBehaviour` (единственный корректный тиковый пример во всей документации), таблица семи событий жизненного цикла с назначением каждого и список ссылок на все 17 страниц интерфейсов (`IEntityBehaviour` плюс восемь пар «неогобщённый + `<E>`») плюс `EditModeEntityBehaviourAttribute`.
- `Docs/Entities/Behaviours/IEntityBehaviour.md` — объявление пустого маркера и перечень восьми производных интерфейсов с однострочным описанием каждого. Здесь же фраза «Don't implement this interface», которой нет в исходнике.
- `Docs/Entities/Behaviours/IEntityInit.md` — `InitColorBehaviour`, настраивающий цвет материала. Сигнатура `void Init(IEntity entity)`.
- `Docs/Entities/Behaviours/IEntityInit`1.md` — тот же пример, но через `IEntityInit<UnitEntity>`, с предварительным объявлением `public class UnitEntity : Entity {}`. Лучшая страница, чтобы понять смысл обобщённых вариантов.
- `Docs/Entities/Behaviours/IEntityEnable.md`, `IEntityDisable.md` — `EnableRendererBehaviour` / `DisableRendererBehaviour`: разнесённая на два класса пара включения/выключения `Renderer`. Полезны как иллюстрация контракта, но не как образец структуры кода.
- `Docs/Entities/Behaviours/IEntityEnable`1.md`, `IEntityDisable`1.md` — те же два примера на `UnitEntity` через обобщённые интерфейсы.
- `Docs/Entities/Behaviours/IEntityDispose.md`, `IEntityDispose`1.md` — `DisposeColliderBehaviour`: освобождение ресурса в `Dispose`.
- `Docs/Entities/Behaviours/IEntityTick.md` — `MoveController`, читающий `Input.GetAxis` и пишущий `"MoveDirection"`. Именно здесь (строка 31) лежит некомпилирующийся `entity.SetValue<Vector3>(...)`.
- `Docs/Entities/Behaviours/IEntityTick`1.md` — тот же `MoveController` на `PlayerEntity` через `IEntityTick<PlayerEntity>`.
- `Docs/Entities/Behaviours/IEntityFixedTick.md` — два примера: `ApplyForceBehaviour` (`Rigidbody.AddForce`) и `MoveBehaviour` (интегрирование `Transform.position`). Оба разрешают значения внутри `FixedTick` — это антипаттерн; здесь же в разделе Methods параметр назван `fixedDeltaTime`, расходясь с исходником.
- `Docs/Entities/Behaviours/IEntityFixedTick`1.md` — те же два примера на `UnitEntity`; примечательно, что внутри одного файла параметр назван то `fixedDeltaTime`, то `deltaTime`.
- `Docs/Entities/Behaviours/IEntityLateTick.md`, `IEntityLateTick`1.md` — `CameraFollowBehaviour` с `Vector3.Lerp`; правильный выбор фазы, неправильное место разрешения значений.
- `Docs/Entities/Behaviours/IEntityGizmos.md`, `IEntityGizmos`1.md` — `DrawSphereGizmos`; сигнатура `void DrawGizmos(IEntity entity)` и указание, что вызов идёт из `MonoEntity.OnDrawGizmos`/`OnDrawGizmosSelected`.

### Документация: жизненный цикл

- `Docs/Entities/Lifecycle/Manual.md` — обзор модуля: примеры работы с `IInitSource`, `IEnableSource`, `ITickSource` (включая прогон `Tick`/`FixedTick`/`LateTick` вручную), пример с `new DisableSubscription(...)`, примеры `WhenInit`/`WhenEnable`/`WhenTick` и полный список API. В этом списке нет `GizmosSubscription` — удобный способ убедиться, что документация неполна.
- `Docs/Entities/Lifecycle/Extensions.md` — семь разделов с примерами (`WhenInit`, `WhenDispose`, `WhenEnable`, `WhenDisable`, `WhenTick`, `WhenFixedTick`, `WhenLateTick`) и их сигнатуры в API Reference. `WhenGizmos` здесь отсутствует.
- `Docs/Entities/Lifecycle/Sources/IInitSource.md`, `IEnableSource.md`, `ITickSource.md` — по контракту на файл, с полным объявлением и NOTE-блоком «You should not implement it manually». `ITickSource.md` содержит пример ручного прогона трёх фаз — основа для EditMode-тестов.
- `Docs/Entities/Lifecycle/Sources/IGizmosSource.md` — объявление под `#if UNITY_EDITOR`, пример подписки через `OnGizmosDraw +=` и пример `SelectionGizmoBehaviour` (с избыточным `: IEntityBehaviour, IEntityGizmos`).
- `Docs/Entities/Lifecycle/Subscriptions/` — семь файлов (`InitSubscription.md`, `DisposeSubscription.md`, `EnableSubscription.md`, `DisableSubscription.md`, `TickSubscription.md`, `FixedTickSubscription.md`, `LateTickSubscription.md`), каждый с объявлением структуры, конструктором, `Dispose` и примером. Содержимое почти идентично между файлами; достаточно прочитать один. Файла `GizmosSubscription.md` не существует.

### Документация: сопутствующее

- `Docs/Entities/Entities/EntityBehaviours.md` — API хранилища поведений на `Entity`: события `OnBehaviourAdded`/`OnBehaviourDeleted`, свойство `BehaviourCount`, все методы `Has`/`Add`/`Del`/`Get`/`Copy`/перебор. Важный IMPORTANT-блок в начале: сущность ведёт себя как `List`, сложность операций списковая, дубликаты одного экземпляра разрешены.
- `Docs/Entities/Entities/EntityLifecycle.md` — API диспетчера: семь событий, свойства `Initialized`/`Enabled`, методы переходов и фазы обновления.
- `Docs/Entities/Entities/IEntityBehaviours.md`, `Docs/Entities/Entities/IEntityLifecycle.md` — те же поверхности, но на уровне интерфейса `IEntity`; читать, если вы пишете против `IEntity`, а не против `Entity`.
- `Docs/Entities/Entities/ExtensionsBehaviours.md` — `AddBehaviour<T>()`, обе перегрузки `AddBehaviours` и обе `DelBehaviours`, с сигнатурами. Расхождение: документация объявляет `public static void AddBehaviour<T>(...)`, а исходник (`Extensions_Behaviours.cs`) возвращает созданный экземпляр — `public static T AddBehaviour<T>(this IEntity entity) where T : IEntityBehaviour, new()`.
- `Docs/Entities/EditMode/EditModeEntityBehaviourAttribute.md` — пример `[EditModeEntityBehaviour]` на `InitColorBehaviour` и предупреждение не путать с юнитивским `[RunInEditMode]`.
- `Docs/Entities/Installers/Manual.md` плюс `IEntityInstaller.md`, `IEntityInstaller`1.md`, `MonoEntityInstaller.md`, `MonoEntityInstaller`1.md`, `ScriptableEntityInstaller.md`, `ScriptableEntityInstaller`1.md`, `MonoEntityInstallerConfigurable.md` — где именно происходит композиция. Асcетные (`Scriptable*`) варианты читать обязательно: документация по поведениям их не упоминает, а для переиспользования конфигурации между префабами нужны именно они.
- `Docs/Entities/Systems/Manual.md` — обзор систем с тремя разделами примеров: round-robin система, приоритетная система и подключение к контексту. Плюс раздел Best Practices.
- `Docs/Entities/Systems/EntitySystemBase.md`, `EntitySystem.md`, `PriorityEntitySystem.md`, `EntityUpdatePriority.md` — API систем по классу на файл, включая `Settings` и `AdaptiveBatching` со значениями по умолчанию.
- `Docs/Entities/Systems/Extensions.md` — `AddTickSystem<TContext, TEntity>`, `AddFixedTickSystem`, `AddLateTickSystem` с примерами подключения `MovementSystem` к `gameContext`. Это недостающее звено между «поведения» и «системы».

### Исходники: поведения и жизненный цикл

- `Assets/Plugins/Atomic/Entities/Scripts/Behaviours/` — `IEntityBehaviour.cs` плюс восемь файлов, в каждом неогобщённый и обобщённый интерфейс рядом, с текстом `InvalidCastException`-моста. Короткие файлы, читаются за минуту каждый.
- `Assets/Plugins/Atomic/Entities/Scripts/Lifecycle/Sources/` — `IInitSource.cs`, `IEnableSource.cs`, `ITickSource.cs`, `IGizmosSource.cs`.
- `Assets/Plugins/Atomic/Entities/Scripts/Lifecycle/Subscriptions/` — восемь файлов, включая недокументированный `GizmosSubscription.cs`.
- `Assets/Plugins/Atomic/Entities/Scripts/Lifecycle/Extensions.cs` — все восемь `When*` с `AggressiveInlining`.
- `Assets/Plugins/Atomic/Entities/Scripts/Lifecycle/TickableManager.cs` — насос кадра целиком, ~90 строк. Читать обязательно: это ответ на вопрос «кто и когда вызывает мои тики».

### Исходники: `Entity` и `MonoEntity`

- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Core/Entity.cs` — три конструктора, `struct Settings`, `Register` в `EntityRegistry`, `Equals`/`GetHashCode` по `InstanceID`.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Core/Entity_Behaviours.cs` — всё хранилище поведений, `BehaviourEnumerator`, `ConstructBehaviours`, `EnableBehaviour`, `DisableBehaviour`. Самый важный файл главы.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Core/Entity_Lifecycle.cs` — статические компараторы, семь событий, `Init`/`Enable`/`Disable`/три тика/`Dispose`/`Deinitialize`/`UnsubscribeEvents`. Второй по важности.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Core/Entity_Values.cs` — хранилище значений; строка 117 (`return (T) slot.value;`) и `SetValue(int key, object value)` на строке 402 объясняют, откуда берётся бокс.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Extensions_Behaviours.cs` — `AddBehaviour<T>()` и bulk-перегрузки.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Extensions_Values.cs` — все перегрузки `GetValue`/`TryGetValue`/`GetValueUnsafe`/`SetValue`; строки 92–168 нужны, чтобы понять, какие формы существуют и какие нет.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Unity/MonoEntity.cs` — объявление класса (`: MonoBehaviour, IEntity`, `[DefaultExecutionOrder(-1000)]`) и свойства.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Unity/MonoEntity_UnityLifecycle.cs` — `Awake`/`OnEnable`/`Start`/`OnDisable`/`OnDestroy`/`OnAfterDeserialize`. Короткий и обязательный к прочтению файл.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Unity/MonoEntity_Behaviours.cs`, `MonoEntity_Lifecycle.cs` — независимые копии хранилища и диспетчера; сверяйтесь с ними, а не с `Entity`, когда работаете с `MonoEntity`.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Unity/MonoEntity_Gizmos.cs` — вся точка вызова `IEntityGizmos`, целиком под `#if UNITY_EDITOR`, с флагами `onlySelectedGizmos`/`onlyEditModeGizmos` и `try/catch`.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Unity/MonoEntity_Editor.cs` — `Compile()`, `Reset()`, `PrecomputeCapacities()` и четыре `*InEditMode` метода со строками 98/116/134/152, где делается reflection-проверка атрибута.
- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Interface/IEntity.cs` — объявление `OnStateChanged` (строка 27) и полный контракт сущности.

### Исходники: прочее

- `Assets/Plugins/Atomic/Entities/Scripts/Utils/InternalUtils.cs` — `Add`, `AddIfAbsent`, `Contains`, `Expand`, `Remove`, `CeilToPrime`, `PrimeTable`, `DeltaTick`, `GetTimestamp`, `IsEditModeBehaviourDefined`. Домашний стиль работы с массивами целиком в одном файле.
- `Assets/Plugins/Atomic/Entities/Scripts/Systems/EntitySystemBase.cs` — `Settings`/`AdaptiveBatching`, `Enable`/`Disable`, адаптивный `Update` с `Stopwatch` и `ProfilerMarker`.
- `Assets/Plugins/Atomic/Entities/Scripts/Systems/EntitySystem.cs` — массив плюс словарь, курсор, swap-with-last.
- `Assets/Plugins/Atomic/Entities/Scripts/Systems/PriorityEntitySystem.cs` — три ведра, очередь отложенных команд, `EvaluatePriority`/`ChangePriority`/`RecalculatePriorities`.
- `Assets/Plugins/Atomic/Entities/Scripts/Systems/EntityUpdatePriority.cs`, `Systems/Extensions.cs` — перечисление приоритетов и три `Add*TickSystem`.
- `Assets/Plugins/Atomic/Entities/Scripts/Installers/` — семь файлов установщиков, включая `ScriptableEntityInstaller.cs` / `ScriptableEntityInstaller`1.cs` и `MonoEntityInstallerConfigurable.cs`.
- `Assets/Plugins/Atomic/Entities/Scripts/KeyStore/Keys/ValueKey.cs`, `ValueKey'1.cs` — `readonly struct` типизированных ключей (обратите внимание: в именах файлов здесь апостроф, а не обратная кавычка, как в именах файлов интерфейсов).
- `Assets/Plugins/Atomic/Entities/Scripts/EditMode/EditModeEntityBehaviourAttribute.cs` — 18 строк, из которых важна только ссылка на несуществующий `IEntityBehaviour{T}` в XML-доке.
- `Assets/Plugins/Atomic/Entities/Tests/EditMode/Entity/EntityTests_Lifecycle.cs` — зафиксированный контракт жизненного цикла, включая три теста прерывания фазы: `OnUpdate_StopsCallingIfEntityDisabledMidLoop` (строка 1010), `OnFixedUpdate_StopsIfEntityDisabledMidIteration` (1077) и `OnLateUpdate_StopsIteration_WhenDisabledMidUpdate` (1164). Это самый надёжный источник правды о порядке вызовов. Параллельный набор для `MonoEntity` лежит в `Tests/PlayMode/SceneEntity/SceneEntityTests_Lifecycle.cs`.
- `Assets/Plugins/Atomic/Entities/Tests/EditMode/Entity/EntityTests_Behaviours.cs` — контракт хранилища поведений, включая `OnBehaviourAdded_IsNotInvoked_WhenNullBehaviour` (строка 49). Тестов на `DelBehaviours<T>` здесь нет.
- `Assets/Plugins/Atomic/Entities/Tests/Substitutions/Behaviours/` — тестовые дублёры (`EntityBehaviourDummy`, `EntityTickSpy`, `DisableDuringTickTestDouble` и другие); готовые заготовки для ваших собственных тестов.
