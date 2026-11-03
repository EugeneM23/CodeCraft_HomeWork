# 09. Entities: IEntity, ключи, хранилище

`Atomic.Entities` — это контейнер вместо наследования. Сущность не наследуется от базового `Character`/`Unit`, а состоит из трёх хранилищ: теги (множество `int`), значения (`int -> object`) и поведения (`IEntityBehaviour`), плюс собственный жизненный цикл `Init -> Enable -> Tick/FixedTick/LateTick -> Disable -> Dispose`. Есть две независимые реализации: `Entity` (чистый C#) и `MonoEntity` (`MonoBehaviour`, собирается в инспекторе из инсталлеров). Подсистема `KeyStore` один раз превращает строковые имена в `int`-идентификаторы, после чего весь рантайм работает только с целыми ключами, а Roslyn-генератор по полям-ключам выпускает типизированные `AddX/GetX/SetX/HasXTag`. Всё хранилище — руками написанное открытое хеширование по массивам со struct-энумераторами; цель — покадровый обход без аллокаций.

Эта глава описывает только ядро: `IEntity`, `Entity`, `MonoEntity`, `MonoEntityProxy<E>`, инсталлеры, `EntityKeyStore` и ключи. Миры (`EntityWorld`), фильтры, пулы, фабрики, аспекты и UI — отдельные главы.

---

## Модель

Шесть вещей, которые нужно удерживать в голове, прежде чем трогать API.

**1. Сущность — это данные, поведение — это логика без состояния.** Поведение (`IEntityBehaviour`-наследник) кеширует ссылки в `Init` и больше ничего не хранит. Всё игровое состояние живёт в значениях сущности: оно инспектируемо, его можно сохранить, его видят все поведения. Поведение, которое держит свой `float _health`, ломает эту модель.

**2. Ключ — это `int`, и этот `int` сам является хешем.** В каждом поиске (`Entity_Tags.cs`, `Entity_Values.cs` и их `MonoEntity`-двойники) стоит `int hash = key & 0x7FFFFFFF; int bucket = hash % _capacity;`. `GetHashCode` не вызывается никогда. Отсюда весь смысл `EntityKeyStore`: имя разрешается в `int` один раз, дальше — маска, деление и обращение к массиву.

**3. Значения хранятся как `object`.** В этой версии нет ни `AddValue<T>(int, T) where T : struct`, ни `SetValue<T>` со struct-ограничением (на `IEntity`, `Entity`, `MonoEntity` — только `object`-перегрузки). Любой struct, который вы положили в сущность, упаковывается один раз при `AddValue` и распаковывается при каждом типизированном `GetValue<T>`. Для часто читаемого числового состояния идиоматический ответ — положить ссылочную обёртку из `Atomic.Elements` (`Const<T>`, `ReactiveVariable<T>`, `IValue<T>`) и закешировать её в `Init`.

**4. `Entity` и `MonoEntity` не связаны ничем, кроме интерфейса.** `MonoEntity` не оборачивает и не наследует `Entity`: теги, значения, поведения, жизненный цикл и вложенные `TagSlot`/`ValueSlot`/`TagEnumerator`/`ValueEnumerator`/`BehaviourEnumerator` переписаны у него заново в своих `partial`-файлах. У `Entity` поля диспетчеризации называются `_updates`/`_updateCount`, у `MonoEntity` — `updates`/`updateCount`. Практический вывод: приведение между ними невозможно, и любая правка в одной реализации нужна дважды.

**5. Идентичность — это `InstanceID`, выданный `EntityRegistry`.** `GetHashCode() => _instanceId`, `Equals` сравнивает только `InstanceID`. Регистрация происходит в конструкторе `Entity` и в `OnAfterDeserialize`/`Awake` у `MonoEntity`. `Unregister` сбрасывает `InstanceID` в `0` и **возвращает id в стек переиспользования** — то есть устаревшая ссылка и новая сущность могут оказаться `Equals` и совпасть по хешу. `InstanceID` — это дескриптор слота, а не уникальная за сессию личность.

**6. Диспетчеризация тиков предварительно отсортирована.** `EnableBehaviour` один раз делает проверки `is IEntityTick` / `is IEntityFixedTick` / `is IEntityLateTick` и раскладывает поведение в три отдельных массива. `Tick(float)` после этого — плоский цикл по нужному подмножеству, без проверок типов. Цена: `Disable()` снимает поведения линейным поиском, то есть O(n²) по сравнениям.

---

## API

### Жизненный цикл: базовые интерфейсы `IEntity`

`IEntity` наследует три интерфейса из `Scripts/Lifecycle/Sources/` — именно там живут `Init`, `Enable`, `Tick`, `Initialized`, `Enabled`. В документации они называются `I*Lifecycle`; в исходниках это `I*Source`.

```csharp
// Scripts/Lifecycle/Sources/IInitSource.cs
public interface IInitSource : IDisposable
{
    event Action OnInitialized;
    event Action OnDisposed;
    bool Initialized { get; }
    void Init();
}

// Scripts/Lifecycle/Sources/IEnableSource.cs
public interface IEnableSource
{
    event Action OnEnabled;
    event Action OnDisabled;
    bool Enabled { get; }
    void Enable();
    void Disable();
}

// Scripts/Lifecycle/Sources/ITickSource.cs
public interface ITickSource
{
    event Action<float> OnTicked;
    event Action<float> OnFixedTicked;
    event Action<float> OnLateTicked;
    void Tick(float deltaTime);
    void FixedTick(float deltaTime);
    void LateTick(float deltaTime);
}

// Scripts/Lifecycle/Sources/IGizmosSource.cs — целиком под #if UNITY_EDITOR
public interface IGizmosSource
{
    event Action OnGizmosDraw;
}
```

Важное следствие: `IEntity` является `IDisposable` — через `IInitSource`. `IGizmosSource` существует только в редакторе, и `IEntity` его не наследует.

### `IEntity`: ядро

```csharp
// Scripts/Entities/Interface/IEntity.cs
public partial interface IEntity : IInitSource, IEnableSource, ITickSource
{
    event Action<IEntity> OnStateChanged;
    int InstanceID { get; protected internal set; }
    string Name { get; set; }
    bool IsValid { get; }
}
```

`IsValid` в документации не описан, но есть в исходнике и реализован как `_instanceId > 0`. Сеттер `InstanceID` — `protected internal`, то есть снаружи сборки id не присвоить: это делает только `EntityRegistry`.

### `IEntity`: теги

```csharp
// Scripts/Entities/Interface/IEntity_Tags.cs
public partial interface IEntity
{
    event Action<IEntity, int> OnTagAdded;
    event Action<IEntity, int> OnTagDeleted;
    int TagCount { get; }
    bool HasTag(int key);
    bool AddTag(int key);          // false, если тег уже есть
    bool DelTag(int key);
    void ClearTags();
    int[] GetTags();               // аллоцирует
    int CopyTags(int[] results);   // не аллоцирует
    IEnumerator<int> GetTagEnumerator();   // явная реализация -> упаковывает
}
```

`AddTag` возвращает `false` при дубликате, а не бросает исключение — в отличие от `AddValue`.

### `IEntity`: значения

```csharp
// Scripts/Entities/Interface/IEntity_Values.cs
public partial interface IEntity
{
    event Action<IEntity, int, object> OnValueAdded;
    event Action<IEntity, int, object> OnValueDeleted;
    event Action<IEntity, int, object> OnValueChanged;
    int ValueCount { get; }

    T GetValue<T>(int key);
    object GetValue(int key);
    ref T GetValueUnsafe<T>(int key) where T : class;

    bool TryGetValue<T>(int key, out T value);
    bool TryGetValue(int key, out object value);
    bool TryGetValueUnsafe<T>(int key, out T value) where T : class;

    void AddValue(int key, object value);
    void SetValue(int key, object value);
    bool HasValue(int key);
    bool DelValue(int key);
    void ClearValues();

    KeyValuePair<int, object>[] GetValues();                  // аллоцирует
    int CopyValues(KeyValuePair<int, object>[] results);       // не аллоцирует
    IEnumerator<KeyValuePair<int, object>> GetValueEnumerator();  // упаковывает
}
```

События значений несут три аргумента — `(сущность, ключ, значение)`; в документации показаны два.

Предусловия `Entity.AddValue`/`SetValue` (`Scripts/Entities/Core/Entity_Values.cs`, подтверждено тестами `Tests/EditMode/Entity/EntityTests_Values.cs`):

```csharp
// AddValue
if (value == null) throw new ArgumentNullException(nameof(value));
// ... если ключ уже есть:
throw ValueAlreadyAddedException(key);   // ArgumentException
```

```csharp
// SetValue — это UPSERT, а не только обновление
if (value == null) throw new ArgumentNullException(nameof(value));
if (this.FindValueIndex(key, out int index))
{
    ref ValueSlot slot = ref _valueSlots[index];
    if (!slot.value.Equals(value))      // сравнение УПАКОВАННЫХ значений
    {
        slot.value = value;
        this.OnValueChanged?.Invoke(this, key, value);
        this.OnStateChanged?.Invoke(this);
    }
}
else { /* ... вставка нового слота ... */ this.OnValueAdded?.Invoke(this, key, value); }
```

Три вывода: `null` в сущности лежать не может; `SetValue` по отсутствующему ключу не бросает, а добавляет и шлёт `OnValueAdded`; `SetValue` равным значением — молчаливый no-op без событий.

`GetValue<T>` — это `(T) slot.value`, то есть распаковка на каждое чтение. `GetValue(int)` возвращает упакованный `object`. `GetValueUnsafe<T>` ограничен `where T : class` и реализован как переинтерпретация ссылки, а не как ref-доступ к struct:

```csharp
// Scripts/Entities/Core/Entity_Values.cs
public ref T GetValueUnsafe<T>(int key) where T : class
{
    // ... обход цепочки бакета ...
    ref ValueSlot slot = ref _valueSlots[index];
    if (slot.exists && slot.key == key)
        return ref Unsafe.As<object, T>(ref slot.value);
    // ...
    throw this.ValueNotFoundException(key);
}
```

Неверный `T` здесь даёт испорченную ссылку, а не `InvalidCastException`.

### `IEntity`: поведения

```csharp
// Scripts/Entities/Interface/IEntity_Behaviours.cs
public partial interface IEntity
{
    event Action<IEntity, IEntityBehaviour> OnBehaviourAdded;
    event Action<IEntity, IEntityBehaviour> OnBehaviourDeleted;
    int BehaviourCount { get; }

    void AddBehaviour(IEntityBehaviour behaviour);
    T GetBehaviour<T>() where T : IEntityBehaviour;
    IEntityBehaviour GetBehaviourAt(int index);          // именно ...At, не GetBehaviour(int)
    bool TryGetBehaviour<T>(out T behaviour) where T : IEntityBehaviour;
    bool HasBehaviour(IEntityBehaviour behaviour);
    bool HasBehaviour<T>() where T : IEntityBehaviour;
    bool DelBehaviour(IEntityBehaviour behaviour);
    bool DelBehaviour<T>() where T : IEntityBehaviour;
    void DelBehaviours<T>() where T : IEntityBehaviour;  // см. «Грабли»: пропускает элементы
    void ClearBehaviours();

    IEntityBehaviour[] GetBehaviours();                               // аллоцирует
    T[] GetBehaviours<T>() where T : IEntityBehaviour;                // аллоцирует
    int CopyBehaviours(IEntityBehaviour[] results);                   // не аллоцирует
    int CopyBehaviours<T>(T[] results) where T : IEntityBehaviour;    // не аллоцирует
    IEnumerator<IEntityBehaviour> GetBehaviourEnumerator();           // упаковывает
}
```

`DelBehaviourAt(int)` на интерфейсе **нет**, но он публичен и на `Entity` (`Entity_Behaviours.cs:113`), и на `MonoEntity` (`MonoEntity_Behaviours.cs:113`). Достать его можно только через конкретный тип — это нужно, если вы меняете поведения по индексу в паре с `GetBehaviourAt(int)`.

### Контракты поведений

```csharp
// Scripts/Behaviours/*.cs
public interface IEntityBehaviour { }   // маркер, без членов

public interface IEntityInit      : IEntityBehaviour { void Init(IEntity entity); }
public interface IEntityEnable    : IEntityBehaviour { void Enable(IEntity entity); }
public interface IEntityTick      : IEntityBehaviour { void Tick(IEntity entity, float deltaTime); }
public interface IEntityFixedTick : IEntityBehaviour { void FixedTick(IEntity entity, float deltaTime); }
public interface IEntityLateTick  : IEntityBehaviour { void LateTick(IEntity entity, float deltaTime); }
public interface IEntityDisable   : IEntityBehaviour { void Disable(IEntity entity); }
public interface IEntityDispose   : IEntityBehaviour { void Dispose(IEntity entity); }
public interface IEntityGizmos    : IEntityBehaviour { void DrawGizmos(IEntity entity); }
```

У каждого есть контравариантный типизированный двойник; приведение делается один раз в default interface member:

```csharp
// Scripts/Behaviours/IEntityTick.cs
public interface IEntityTick<in E> : IEntityTick where E : IEntity
{
    void Tick(E entity, float deltaTime);

    void IEntityTick.Tick(IEntity entity, float deltaTime)
    {
        if (entity is not E e)
            throw new InvalidCastException(
                $"[IEntityTick<{typeof(E).Name}>] Invalid entity type for {this.GetType().Name}.\n" +
                $"Expected: {typeof(E).FullName}\n" +
                $"Received: {entity?.GetType().FullName ?? "null"}\n" +
                "Please make sure the correct IEntityTick is used for this entity type.");

        this.Tick(e, deltaTime);
    }
}
```

Используйте типизированные варианты в типизированном проекте: они убирают приведение из каждого метода. Платой является требование C# 8 default interface members — отсюда и жёсткое требование Unity 6 / .NET 7+.

### `Entity`

```csharp
// Scripts/Entities/Core/Entity.cs
public partial class Entity : IEntity
{
    public event Action<IEntity> OnStateChanged;
    public int InstanceID => _instanceId;
    public string Name { get => _name; set { /* ... + OnStateChanged только при изменении */ } }
    public bool IsValid => _instanceId > 0;

    [Serializable]
    public struct Settings
    {
        public bool disposeValues;
    }

    public Entity(string name, IEnumerable<string> tags,
                  IEnumerable<KeyValuePair<string, object>> values,
                  IEnumerable<IEntityBehaviour> behaviours, Settings? settings = null);

    public Entity(string name, IEnumerable<int> tags,
                  IEnumerable<KeyValuePair<int, object>> values,
                  IEnumerable<IEntityBehaviour> behaviours, Settings? settings = null);

    public Entity(string name = null, int tagCapacity = 0, int valueCapacity = 0,
                  int behaviourCapacity = 0, Settings? settings = null);

    public override string ToString();   // "_name: X, _instanceId: N" — печатает имена полей
    public bool Equals(IEntity other) => other != null && _instanceId == other.InstanceID;
    public override int GetHashCode() => _instanceId;
}
```

Настоящий конструктор — третий (capacity). Два коллекционных делегируют в него через `tags?.Count() ?? 0` (LINQ `Count()`), то есть перечисляют коллекцию ещё раз — передавайте массивы и списки, а не ленивые запросы. `_name = name ?? string.Empty`, поэтому на `Entity` имя никогда не `null` (в отличие от `MonoEntity`, где `Name` — это `gameObject.name`). `settings == null` означает `disposeValues = true`.

Конструктор вызывает `EntityRegistry.Instance.Register(this)` — именно там выдаётся `InstanceID`.

### Жизненный цикл `Entity`

```csharp
// Scripts/Entities/Core/Entity_Lifecycle.cs
public bool Initialized => _initialized;
public bool Enabled => _enabled;

public void Init();        // Init на всех IEntityInit, затем OnStateChanged, OnInitialized
public void Enable();      // сначала вызывает Init(), потом раскладывает поведения по массивам тиков
public void Disable();
public void Dispose();

[MethodImpl(MethodImplOptions.AggressiveInlining)]
protected virtual void OnDispose();   // ваш хук в подклассе

public void Tick(float deltaTime)
{
    if (!_enabled)
        return;

    for (int i = 0; i < _updateCount; i++)
        _updates[i].Tick(this, deltaTime);

    this.OnTicked?.Invoke(deltaTime);
}
```

`Tick`/`FixedTick`/`LateTick` при `!_enabled` **молча возвращаются** (документация утверждает, что они бросают — это неверно). `LateTick` отличается от двух других: его условие цикла `i < _lateUpdateCount && _enabled`, то есть он один перепроверяет флаг на каждой итерации.

Порядок `Dispose()` ровно такой:

```csharp
public void Dispose()
{
    this.OnDispose();
    this.Deinitialize();                 // Disable(), затем IEntityDispose.Dispose на всех поведениях
    if (_settings.disposeValues)
        this.DisposeValues();
    this.ClearTags();
    this.ClearValues();
    this.ClearBehaviours();
    this.OnStateChanged?.Invoke(this);
    this.UnsubscribeEvents();            // обнуляет ВСЕ поля событий
    if (_instanceId > 0)
        EntityRegistry.Instance.Unregister(this);
}
```

### struct-энумераторы

У `Entity` и `MonoEntity` по три публичных struct-энумератора и по два варианта геттера: явная реализация интерфейса упаковывает, публичный метод — нет.

```csharp
// Scripts/Entities/Core/Entity_Tags.cs
public struct TagEnumerator : IEnumerator<int>
{
    private readonly Entity _entity;
    private int _index;
    private int _current;
    public int Current => _current;
    object IEnumerator.Current => _current;
    public TagEnumerator(Entity entity);
    public bool MoveNext();    // пропускает слоты с exists == false
    public void Reset();
    public void Dispose();     // пустой
}

IEnumerator<int> IEntity.GetTagEnumerator() => new TagEnumerator(this);  // упаковывает
public TagEnumerator GetTagEnumerator();                                 // не упаковывает
```

Аналогично `Entity.ValueEnumerator` (`IEnumerator<KeyValuePair<int, object>>`) и `Entity.BehaviourEnumerator` (`IEnumerator<IEntityBehaviour>`), и такие же три типа отдельно объявлены внутри `MonoEntity`.

### `MonoEntity`

```csharp
// Scripts/Entities/Unity/MonoEntity.cs
[AddComponentMenu("Atomic/Entities/Entity")]
[DisallowMultipleComponent, DefaultExecutionOrder(-1000)]
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Entities/Entities/MonoEntity.md")]
public partial class MonoEntity : MonoBehaviour, IEntity
{
    public event Action<IEntity> OnStateChanged;
    public int InstanceID { get => _instanceId; internal set => _instanceId = value; }
    public string Name { get => this.name; set { /* ... + OnStateChanged */ } }
    public bool IsValid => this != null && _instanceId > 0;
}
```

Unity-колбэки объявлены `private protected virtual`, поэтому переопределить их могут только типы внутри той же сборки (например `MonoEntitySingleton<E>`) — ваши игровые подклассы `Awake`/`Start`/`OnEnable`/`OnDisable`/`OnDestroy` переопределить не могут.

```csharp
// Scripts/Entities/Unity/MonoEntity_UnityLifecycle.cs
public partial class MonoEntity : ISerializationCallbackReceiver
{
    private bool _started;

    private protected virtual void Awake()
    {
        this.Register();
        if (this.installOnAwake) this.Install();
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
        if (this.useUnityLifecycle && _started)       // <- ВАЖНО: условие
        {
            this.OnDispose();
            this.Deinitialize();
            if (this.disposeValues) this.DisposeValues();
        }

        if (this.uninstallOnDestroy) this.Uninstall();

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

    void ISerializationCallbackReceiver.OnBeforeSerialize() { }
}
```

Читайте условие в `OnDestroy` внимательно: при `useUnityLifecycle = false` (режим, который как раз рекомендуется для пулов и симуляции) `OnDestroy` **не** вызывает ни `OnDispose`, ни `IEntityDispose.Dispose` на поведениях, ни `DisposeValues`. Сущности, которые вы тикаете сами, обязаны сами и диспозить.

Поля инспектора (`Scripts/Entities/Unity/MonoEntity_Inspector.cs`):

| Поле | Доступ | По умолчанию |
|---|---|---|
| `useUnityLifecycle` | `internal` | `true` |
| `disposeValues` | `internal` | `true` |
| `installOnAwake` | `internal` | `true` |
| `uninstallOnDestroy` | `internal` | `true` |
| `scriptableInstallers` (`List<ScriptableEntityInstaller>`) | `internal` | `null` |
| `sceneInstallers` (`List<MonoEntityInstaller>`) | `internal`, `[FormerlySerializedAs("installers")]` | `null` |
| `onlySelectedGizmos`, `onlyEditModeGizmos` | `private` | `false` |
| `autoCompile` | `private` | `false` |
| `initialTagCapacity` | `private`, `[Min(1)]` | `1` |
| `initialValueCapacity` | `private`, `[Min(1)]` | `1` |
| `initialBehaviourCapacity` | `private`, `[Min(0)]` | `0` |

Поля `children` в этой версии нет, хотя `MonoEntityInstalling.md` его описывает.

### `MonoEntity`: установка

```csharp
// Scripts/Entities/Unity/MonoEntity_Installing.cs
public bool Installed => _installed;

public void Install();                  // идемпотентно: сразу выходит, если _installed
protected virtual void OnInstall();
public void WhenInstall(Action action);  // если уже установлено — вызывает немедленно

public void Uninstall();                // идемпотентно: сразу выходит, если !_installed
protected virtual void OnUninstall();
public void WhenUninstall(Action action);

public static void InstallAll(Scene scene, bool includeInactive = false);
public static void InstallAll<E>(Scene scene, bool includeInactive = false) where E : MonoEntity;
public static void InstallAll(Component component, bool includeInactive = false);
```

`Install()` выставляет `_installed = true` **до** прогона инсталлеров, поэтому повторный `Install()` изнутри инсталлера — no-op, а не бесконечная рекурсия. Порядок: сначала `scriptableInstallers`, потом `sceneInstallers`; `Uninstall()` идёт в обратном порядке (сначала `sceneInstallers`, потом `scriptableInstallers`), и вызывает `OnUninstall()` до них. Пропущенный (`null`) элемент списка логируется через `Debug.LogWarning` и не прерывает установку.

### `MonoEntity`: создание

```csharp
// Scripts/Entities/Unity/MonoEntity_Creation.cs
[Serializable]
public struct CreateArgs
{
    public string name;
    public IEnumerable<int> tags;
    public IReadOnlyDictionary<int, object> values;
    public IEnumerable<IEntityBehaviour> behaviours;
    public List<MonoEntityInstaller> sceneInstallers;
    public List<ScriptableEntityInstaller> scriptableInstallers;

    public int initialTagCapacity;
    public int initialValueCapacity;
    public int initialBehaviourCapacity;

    public bool installOnAwake;
    public bool uninstallOnDestroy;
    public bool disposeValues;
    public bool useUnityLifecycle;
}

public static MonoEntity Create(in CreateArgs args);
public static E Create<E>(in CreateArgs args) where E : MonoEntity;
```

Имена полей — camelCase, а не PascalCase, как в документации. `in` передаёт struct без копии.

Параметризованные перегрузки — те, которыми стоит пользоваться, потому что у них все `bool`-умолчания равны `true`. Существуют обе: обобщённая и необобщённая.

```csharp
public static E Create<E>(
    string name = null,
    IEnumerable<int> tags = null,
    IReadOnlyDictionary<int, object> values = null,
    IEnumerable<IEntityBehaviour> behaviours = null,
    bool installOnAwake = true,
    bool uninstallOnDestroy = true,
    bool disposeValues = true,
    bool useUnityLifecycle = true,
    int initialTagCount = 1,
    int initialValueCount = 1,
    int initialBehaviourCount = 1
) where E : MonoEntity;

public static MonoEntity Create(
    string name = null,
    IEnumerable<int> tags = null,
    IReadOnlyDictionary<int, object> values = null,
    IEnumerable<IEntityBehaviour> behaviours = null,
    bool installOnAwake = true,
    bool uninstallOnDestroy = true,
    bool disposeValues = true,
    bool useUnityLifecycle = true,
    int initialTagCount = 1,
    int initialValueCount = 1,
    int initialBehaviourCount = 1
);
```

Префабные перегрузки:

```csharp
public static MonoEntity Create(MonoEntity prefab, Transform parent = null);
public static E Create<E>(E prefab, Transform parent = null) where E : MonoEntity;
public static MonoEntity Create(MonoEntity prefab, Vector3 position, Quaternion rotation, Transform parent = null);
public static E Create<E>(E prefab, Vector3 position, Quaternion rotation, Transform parent = null) where E : MonoEntity;
public static E Create<E>(E prefab, Transform point, Transform parent) where E : MonoEntity;
public static E Create<E>(E prefab, Scene scene) where E : MonoEntity;
public static async ValueTask<E[]> CreateAsync<E>(E prefab, int count, Transform parent = null) where E : MonoEntity;
```

`Create<E>(in CreateArgs)` создаёт `GameObject`, сразу делает `SetActive(false)`, добавляет компонент, пишет ёмкости, вызывает `Construct()`, заливает теги/значения/поведения, регистрирует — и только потом `SetActive(true)`. То есть `Awake`/`Start` видят уже полностью собранную сущность.

### `MonoEntity`: уничтожение и `Dispose`

```csharp
// Scripts/Entities/Unity/MonoEntity_Destruction.cs
public static void Destroy(IEntity entity, float t = 0) => Destroy(Cast(entity), t);

public static void Destroy(MonoEntity entity, float t = 0)
{
    if (entity)
    {
        entity.Uninstall();
        Destroy(entity.gameObject, t);
    }
}
```

Это правильный путь разрушения. Обычный `Object.Destroy(entity.gameObject)` пропускает `Uninstall()`, и подписки, которые держал инсталлер, утекают. Перегрузка с `IEntity` сначала делает `Cast()`, поэтому прозрачно разворачивает `MonoEntityProxy`.

```csharp
// Scripts/Entities/Unity/MonoEntity_Lifecycle.cs:266
public void Dispose()
{
    this.OnDispose();
    this.Deinitialize();
    if (this.disposeValues) this.DisposeValues();
    this.ClearTags();
    this.ClearValues();
    this.ClearBehaviours();
    this.OnStateChanged?.Invoke(this);
    this.UnsubscribeEvents();
}
```

Сравните с `Entity.Dispose()`: здесь **нет** `EntityRegistry.Unregister` и, разумеется, нет уничтожения `GameObject`. Вызов `Dispose()` на `MonoEntity` оставляет зарегистрированный живой компонент с валидным `InstanceID` и пустыми хранилищами. Для настоящего разрушения — `MonoEntity.Destroy`.

### `MonoEntity`: приведение типов

```csharp
// Scripts/Entities/Unity/MonoEntity_Casting.cs
public static MonoEntity Cast(IEntity entity) => Cast<MonoEntity>(entity);

public static E Cast<E>(IEntity entity) where E : MonoEntity => entity switch
{
    null => null,
    E sceneEntity => sceneEntity,
    MonoEntityProxy<E> proxy => proxy.Source,
    _ => throw new InvalidCastException($"Can't cast {entity.Name} to {typeof(E).Name}")
};

public static bool TryCast(IEntity entity, out MonoEntity result);
public static bool TryCast<E>(IEntity entity, out E result) where E : MonoEntity;
```

Прямое `(MonoEntity) entity` неверно везде, где есть прокси. В коде коллизий и триггеров используйте `TryCast`.

### `MonoEntityProxy<E>`

```csharp
// Scripts/Entities/Unity/MonoEntityProxy`1.cs
public abstract class MonoEntityProxy<E> : MonoBehaviour, IEntity where E : MonoEntity
{
    public E Source { get => _source; set => _source = value; }   // сеттер ЕСТЬ, вопреки докам
    public string Name { get => _source.Name; set => _source.Name = value; }
    public bool IsValid => _source.IsValid;

    [SerializeField] private E _source;

    public override bool Equals(object obj) => obj is IEntity entity && _source.InstanceID == entity.InstanceID;
    public override int GetHashCode() => _source.GetHashCode();
    private void Reset() => _source = this.GetComponentInParent<E>();
}

// Scripts/Entities/Unity/MonoEntityProxy.cs
[AddComponentMenu("Atomic/Entities/Entity Proxy")]
[DisallowMultipleComponent]
public class MonoEntityProxy : MonoEntityProxy<MonoEntity>
```

`Reset()` автоматически подтягивает `Source` через `GetComponentInParent<E>()`, когда вы добавляете компонент. Прокси с `Source == null` бросает `NullReferenceException` на первом же обращении.

Есть две нетривиальные перегрузки, которых нет больше **ни на одном** типе этого модуля:

```csharp
// MonoEntityProxy`1.cs:135 и :138
public void AddValue<T>(int key, T value) where T : struct => _source.AddValue(key, value);
public void SetValue<T>(int key, T value) where T : struct => _source.SetValue(key, value);
```

Это не члены `IEntity`, и они бесполезны: каждая просто перенаправляет вызов в `object`-перегрузку, так что `proxy.AddValue(key, 5)` всё равно упаковывает. Но при перегрузочном разрешении на ссылке типа прокси они **выигрывают** у `object`-версий. Один и тот же код, написанный против `MonoEntity` и против прокси, молча связывается с разными методами.

### `MonoEntitySingleton<E>`

```csharp
// Scripts/Entities/Unity/MonoEntitySingleton.cs
public abstract class MonoEntitySingleton<E> : MonoEntity where E : MonoEntitySingleton<E>
{
    [SerializeField] private bool dontDestroyOnLoad;
    private static E _instance;

    public static E Instance { get; }            // FindAnyObjectByType<E>(FindObjectsInactive.Exclude), бросает если не нашёл
    public static bool TryGetInstance(out E instance);

    private protected override void Awake();     // дубликаты логируют ошибку и уничтожают себя
    private protected override void OnDestroy(); // сбрасывает _instance
#if UNITY_EDITOR
    private protected override void Reset();
#endif
}
```

Это весь публичный состав типа. Задокументированных `isGlobal` и `Resolve(Component)` / `Resolve(GameObject)` / `Resolve(Scene)` в исходнике **нет** — не проектируйте вокруг них разрешение контекста по сценам.

### `EntityRegistry`

Единственный публичный путь `InstanceID -> IEntity`. Он нужен сетевому слою, системе сохранений и отладочным панелям, поэтому его стоит знать, хотя в документации он почти не упоминается.

```csharp
// Scripts/Registry/EntityRegistry.cs
public sealed class EntityRegistry : IReadOnlyEntityCollection<IEntity>
{
    public static EntityRegistry Instance { get; }

    public event Action OnStateChanged;
    public event Action<IEntity> OnAdded;
    public event Action<IEntity> OnRemoved;

    public int Count { get; }
    public IEntity this[int index] { get; }
    public bool TryGetAt(int index, out IEntity entity);

    public bool Contains(IEntity entity);
    public bool Contains(int id);
    public IEntity Get(int id);
    public bool TryGet(int id, out IEntity entity);
    public T GetUnsafe<T>(int id) where T : class, IEntity;
    public bool TryGetUnsafe<T>(int id, out T entity) where T : class, IEntity;

    public void CopyTo(ICollection<IEntity> results);
    public void CopyTo(IEntity[] array, int arrayIndex);

    public struct Enumerator : IEnumerator<IEntity>
    {
        public Enumerator(EntityRegistry registry);
        public IEntity Current { get; }
        public bool MoveNext();
        public void Reset();
        public void Dispose();
    }

    public IEnumerator<IEntity> GetEnumerator() => new Enumerator(this);   // упаковывает
}
```

`Register`, `Unregister`, `Clear` и `ResetAll` — `internal`; всё перечисленное выше публично. Обратите внимание: единственный `GetEnumerator()` объявлен с возвратом `IEnumerator<IEntity>`, то есть упаковывает. Чтобы обойти реестр без аллокации, конструируйте struct напрямую: `new EntityRegistry.Enumerator(EntityRegistry.Instance)`.

### Инсталлеры

```csharp
// Scripts/Installers/IEntityInstaller.cs
public interface IEntityInstaller
{
    void Install(IEntity entity);
}

// Scripts/Installers/IEntityInstaller`1.cs
public interface IEntityInstaller<in E> : IEntityInstaller where E : IEntity
{
    void Install(E entity);

    void IEntityInstaller.Install(IEntity entity)
    {
        if (entity is not E e)
            throw new InvalidCastException(
                $"[IEntityInstaller<{typeof(E).Name}>] Invalid entity type for {this.GetType().Name}.\n" +
                $"Expected: {typeof(E).FullName}\n" +
                $"Received: {entity?.GetType().FullName ?? "null"}\n" +
                "Please make sure the correct IEntityInstaller is used for this entity type.");

        this.Install(e);
    }
}
```

```csharp
// Scripts/Installers/MonoEntityInstaller.cs
public abstract class MonoEntityInstaller : MonoBehaviour, IEntityInstaller
{
#if UNITY_EDITOR
    internal Action onValidateCallback;
#endif
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }

    protected virtual void OnValidate();   // вне play mode и вне компиляции дёргает onValidateCallback
}

// Scripts/Installers/MonoEntityInstaller`1.cs
public abstract class MonoEntityInstaller<E> : MonoEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
{
    public sealed override void Install(IEntity entity);     // приводит тип, затем Install(E)
    public sealed override void Uninstall(IEntity entity);   // приводит тип, затем Uninstall(E)
    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}

// Scripts/Installers/ScriptableEntityInstaller.cs
public abstract class ScriptableEntityInstaller : ScriptableObject, IEntityInstaller
{
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
}

// Scripts/Installers/ScriptableEntityInstaller`1.cs
public abstract class ScriptableEntityInstaller<E> : ScriptableEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
{
    public sealed override void Install(IEntity entity);
    public sealed override void Uninstall(IEntity entity);
    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}

// Scripts/Installers/MonoEntityInstallerConfigurable.cs
public sealed class MonoEntityInstallerConfigurable : MonoEntityInstaller
{
    [SerializeReference] private IEntityInstaller[] _installers;
    public override void Install(IEntity entity);   // null-безопасный веер по _installers
}
```

Два момента, которые стоят целого вечера отладки. Первый: `Install(E entity)` в типизированных инсталлерах — `abstract`, поэтому `override` обязателен (без него CS0534 плюс CS0114). Второй: `MonoEntityInstallerConfigurable` **не** переопределяет `Uninstall`, то есть вложенные через `[SerializeReference]` инсталлеры никогда не получат деинсталляцию. Также: ограничение у базового интерфейса `where E : IEntity`, а у обоих абстрактных классов — `where E : class, IEntity` (struct-сущности исключены).

### `EntityKeyStore` и ключи

```csharp
// Scripts/KeyStore/EntityKeyStore.cs
public static class EntityKeyStore
{
    public static void SetAlgorithm(IEntityKeyAlgorithm algorithm);   // ArgumentNullException; ОЧИЩАЕТ оба кеша

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NameToId(string name)
    {
        if (name == null) throw new ArgumentNullException(nameof(name));
        if (_nameToId.TryGetValue(name, out int id)) return id;
        id = _algorithm.NameToId(name);
        _nameToId[name] = id;
        _idToName[id] = name;
        return id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string IdToName(int id) =>
        _idToName.TryGetValue(id, out string name) ? name : $"#Unknown:{id}";

    public static void Reset();   // чистит кеши и вызывает _algorithm.Reset()

#if UNITY_EDITOR
    [InitializeOnEnterPlayMode]
    private static void ResetOnEnterPlayMode(EnterPlayModeOptions options)
    {
        if ((options & EnterPlayModeOptions.DisableDomainReload) != 0) return;
        Reset();
    }
#endif
}
```

`IdToName` никогда не бросает — возвращает `#Unknown:{id}`; именно эта строка попадает в сообщения исключений сущности.

```csharp
// Scripts/KeyStore/Keys/TagKey.cs
public readonly struct TagKey : IEquatable<TagKey>
{
    public readonly int Id;                                 // ПОЛЕ, не свойство
    public TagKey(string name) => Id = EntityKeyStore.NameToId(name);
    public TagKey(int id) => this.Id = id;
    public override string ToString() => EntityKeyStore.IdToName(Id);
    public bool Equals(TagKey other) => Id == other.Id;
    public override int GetHashCode() => Id;
}

// Scripts/KeyStore/Keys/TagKey'1.cs
public readonly struct TagKey<E> : IEquatable<TagKey<E>> where E : IEntity { /* то же самое */ }

// Scripts/KeyStore/Keys/ValueKey.cs
public readonly struct ValueKey<T> : IEquatable<ValueKey<T>> { /* то же самое */ }

// Scripts/KeyStore/Keys/ValueKey'1.cs
public readonly struct ValueKey<E, T> : IEquatable<ValueKey<E, T>> where E : IEntity { /* то же самое */ }
```

Все четыре — `readonly struct` с единственным полем `int`, поэтому передача по значению бесплатна. Параметры типа — чисто компиляторная информация: само хранилище остаётся `int -> object`, `T` даёт только типизированные перегрузки расширений. Обратите внимание на асимметрию ограничений: у ключей `T` не ограничен, но у перегрузок `GetValueUnsafe` добавляется `where T : class`.

```csharp
// Scripts/KeyStore/Algorithms/IEntityKeyAlgorithm.cs
public interface IEntityKeyAlgorithm
{
    int NameToId(string name);   // doc-комментарий требует детерминизма
    void Reset();
}

// Scripts/KeyStore/Algorithms/SequentialEntityKeyAlgorithm.cs
public sealed class SequentialEntityKeyAlgorithm : IEntityKeyAlgorithm
{
    private const int INITIAL_ID = 1;
    private int _nextId;
    public SequentialEntityKeyAlgorithm(int nextId = INITIAL_ID) => _nextId = nextId;
    public int NameToId(string name) => _nextId++;   // name игнорируется полностью
    public void Reset() => _nextId = INITIAL_ID;
}
```

Поставляемая по умолчанию реализация сознательно нарушает требование детерминизма из своего же интерфейса: `name` не используется, id выдаются в порядке первого обращения и начинаются с `1`. Отсюда и `IsValid => _instanceId > 0`: ноль означает «не зарегистрирован».

### Расширения (`Atomic.Entities.Extensions`)

Это один `partial`-класс, размазанный по десяти файлам: `Entities/Extensions.cs`, `Entities/Extensions_Tags.cs`, `Entities/Extensions_Values.cs`, `Entities/Extensions_Behaviours.cs`, `Lifecycle/Extensions.cs`, `Aspects/Extensions.cs`, `Collections/Extensions.cs`, `Systems/Extensions.cs`, `UI/View/Extensions.cs`, `World/Extensions.cs`. Две части (`Extensions_Tags.cs:7` и `World/Extensions.cs:5`) объявлены как `public partial class Extensions` без `static` — модификатор объединяется по частям, так что класс всё равно статический, но при grep по `static partial class` вы эти два файла не найдёте.

Теги:

```csharp
public static bool AddTag(this IEntity entity, string tag);
public static bool AddTag(this IEntity entity, TagKey tag);
public static bool AddTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static bool AddTag(this IEntity entity, string tag, out int id);
public static void AddTags(this IEntity entity, IEnumerable<int> tags);
public static void AddTags(this IEntity entity, IEnumerable<string> tags);
public static void AddTags(this IEntity entity, IEnumerable<TagKey> tags);
public static void AddTags<E>(this E entity, IEnumerable<TagKey<E>> tags) where E : IEntity;
public static bool DelTag(this IEntity entity, string tag);
public static bool DelTag(this IEntity entity, TagKey tag);
public static bool DelTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static bool HasTag(this IEntity entity, string tag);
public static bool HasTag(this IEntity entity, TagKey tag);
public static bool HasTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static bool HasAllTags(this IEntity entity, params int[] tags);
public static bool HasAllTags(this IEntity entity, params string[] tags);
public static bool HasAllTags(this IEntity entity, params TagKey[] tags);
public static bool HasAllTags<E>(this E entity, params TagKey<E>[] tags) where E : IEntity;
public static bool HasAnyTag(this IEntity entity, params int[] tags);
public static bool HasAnyTag(this IEntity entity, params string[] tags);
public static bool HasAnyTag(this IEntity entity, params TagKey[] tags);
public static bool HasAnyTag<E>(this E entity, params TagKey<E>[] tags) where E : IEntity;
```

Значения:

```csharp
public static void AddValue(this IEntity entity, string key, object value);
public static void AddValue<T>(this IEntity entity, ValueKey<T> key, T value);
public static void AddValue<E, T>(this E entity, ValueKey<E, T> key, T value) where E : IEntity;
public static void AddValue(this IEntity entity, string key, object value, out int id);
public static void AddValues(this IEntity entity, IEnumerable<KeyValuePair<int, object>> values);
public static void AddValues(this IEntity entity, IEnumerable<KeyValuePair<string, object>> values);
public static bool DelValue(this IEntity entity, string key);
public static bool DelValue<T>(this IEntity entity, ValueKey<T> key);
public static bool DelValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static T GetValue<T>(this IEntity entity, string key);
public static object GetValue(this IEntity entity, string key);
public static T GetValue<T>(this IEntity entity, ValueKey<T> key);
public static T GetValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static bool TryGetValue<T>(this IEntity entity, string key, out T value);
public static bool TryGetValue(this IEntity entity, string key, out object value);
public static bool TryGetValue<T>(this IEntity entity, ValueKey<T> key, out T value);
public static bool TryGetValue<E, T>(this E entity, ValueKey<E, T> key, out T value) where E : IEntity;
public static ref T GetValueUnsafe<T>(this IEntity entity, string key) where T : class;
public static ref T GetValueUnsafe<T>(this IEntity entity, ValueKey<T> key) where T : class;
public static T GetValueUnsafe<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity where T : class;
public static void SetValue(this IEntity entity, string key, object value);
public static void SetValue<T>(this IEntity entity, ValueKey<T> key, T value);
public static void SetValue<E, T>(this E entity, ValueKey<E, T> key, T value) where E : IEntity;
public static bool HasValue(this IEntity entity, string key);
public static bool HasValue<T>(this IEntity entity, ValueKey<T> key);
public static bool HasValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static void DisposeValues(this IEntity entity);
```

Асимметрия, на которой легко споткнуться: две первые перегрузки `GetValueUnsafe` возвращают `ref T`, а третья (с `ValueKey<E, T>`) — `T` **по значению**.

Поведения и прочее:

```csharp
public static T AddBehaviour<T>(this IEntity entity) where T : IEntityBehaviour, new();  // ВОЗВРАЩАЕТ новый T
public static void AddBehaviours(this IEntity entity, IEntityBehaviour[] behaviours, int startIndex, int count);
public static void AddBehaviours(this IEntity entity, IEnumerable<IEntityBehaviour> behaviours);
public static void DelBehaviours(this IEntity entity, IEnumerable<IEntityBehaviour> behaviours);
public static void DelBehaviours(this IEntity entity, IEntityBehaviour[] behaviours, int startIndex, int count);

public static void Clear(this IEntity entity);                  // ClearTags + ClearValues + ClearBehaviours
public static IEntity Install(this IEntity entity, IEntityInstaller installer);   // цепочка
public static void Install(this IEntity entity, IEnumerable<IEntityInstaller> installers);
public static void InstallFromScene(this IEntity entity, Scene scene, bool includeInactive = true);
public static void InstallFromScene<T>(this T entity, Scene scene, bool includeInactive = true) where T : class, IEntity;
public static bool TryGetEntity(this GameObject gameObject, out IEntity entity);
public static bool TryGetEntity(this Component component, out IEntity entity);
public static bool TryGetEntity(this Collision2D collision2D, out IEntity entity);
public static bool TryGetEntity(this Collision collision, out IEntity entity);
public static bool FindEntityInParent(this GameObject gameObject, out IEntity entity);
public static bool FindEntityInParent(this Component component, out IEntity entity);
public static bool FindEntityInParent(this Collision2D collision2D, out IEntity entity);
public static bool FindEntityInParent(this Collision collision, out IEntity entity);
public static void DrawGizmos(this IEntity entity);
```

Обратите внимание на умолчания `includeInactive`: у `Extensions.InstallFromScene` оно `true`, а у `MonoEntity.InstallAll` — `false`.

### Подписки на фазы жизненного цикла

Целое семейство, которое легко пропустить (`Scripts/Lifecycle/Extensions.cs`). Это способ подцепиться к одной фазе из инсталлера, не заводя класс-поведение, и каждый метод (кроме `WhenGizmos`) возвращает disposable-подписку, которую можно отцепить.

```csharp
public static InitSubscription      WhenInit(this IInitSource source, Action action);
public static DisposeSubscription   WhenDispose(this IInitSource source, Action action);
public static EnableSubscription    WhenEnable(this IEnableSource source, Action action);
public static DisableSubscription   WhenDisable(this IEnableSource source, Action action);
public static TickSubscription      WhenTick(this ITickSource source, Action<float> action);
public static FixedTickSubscription WhenFixedTick(this ITickSource source, Action<float> action);
public static LateTickSubscription  WhenLateTick(this ITickSource source, Action<float> action);
public static void                  WhenGizmos(this IGizmosSource source, Action action);
```

Поскольку `IEntity` наследует `IInitSource`, `IEnableSource` и `ITickSource`, все они работают прямо на сущности.

### `TickableManager`

```csharp
// Scripts/Lifecycle/TickableManager.cs
[AddComponentMenu("")]
[DisallowMultipleComponent]
internal sealed class TickableManager : MonoBehaviour
{
    internal ITickSource[] _sources;
    internal static TickableManager Instance { get; }   // лениво создаёт скрытый HideAndDontSave GameObject
    internal void Register(ITickSource tickSource);     // AddIfAbsent; вне play mode — no-op
    internal void Unregister(ITickSource tickSource);

    private void Update()      { float dt = Time.deltaTime;      for (int i = 0; i < _count; i++) _sources[i].Tick(dt); }
    private void FixedUpdate() { float dt = Time.fixedDeltaTime;  for (int i = 0; i < _count; i++) _sources[i].FixedTick(dt); }
    private void LateUpdate()  { float dt = Time.deltaTime;       for (int i = 0; i < _count; i++) _sources[i].LateTick(dt); }
}
```

Тип `internal`, напрямую вы его не трогаете: `MonoEntity` сам регистрируется в `Start`/`OnEnable` и снимается в `OnDisable`, когда `useUnityLifecycle == true`. Это и есть замена per-object Unity `Update` — один скрытый `MonoBehaviour` вместо N. `Register` возвращается немедленно вне play mode (`#if UNITY_EDITOR` + `!EditorApplication.isPlaying`).

### Атрибуты кодогенерации

```csharp
// Scripts/Codegen/EntityExtensionsAPIAttribute.cs
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityExtensionsAPIAttribute : Attribute
{
    public bool Unsafe { get; set; }                       // по умолчанию false
    public bool AggressiveInlining { get; set; } = true;   // по умолчанию TRUE
}

// Scripts/Codegen/UnsafeAttribute.cs
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class UnsafeAttribute : Attribute { }        // БЕЗ параметров конструктора

// Scripts/Codegen/EntityDomainBehavioursAttribute.cs
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityDomainBehavioursAttribute : Attribute
{
    public Type EntityType { get; }
    public EntityDomainBehavioursAttribute(Type entityType);
}
```

Тип поля определяет форму генерации: `TagKey<E>` -> методы тегов на `E`; `TagKey` -> методы тегов на `IEntity`; `ValueKey<E, T>` -> методы значения `T` на `E`; `ValueKey<T>` -> методы значения `T` на `IEntity`.

Установка генераторов: в `Assets/Plugins/Atomic/SourceGenerators/` лежат **пять** DLL — `EntityAPIAnalyzer.dll`, `EntityAPIGenerator.dll`, `EntityDomainGenerator.dll`, `EventAPIAnalyzer.dll`, `EventAPIGenerator.dll`. Каждой нужна метка `RoslynAnalyzer`, снятые «Any Platform» и все отдельные платформы, затем Apply и перезапуск Unity. Upstream-`Setup.md` говорит «the same steps apply to all four assemblies» и перечисляет четыре — это ошибка в документации: пропущен `EntityDomainGenerator.dll`, то есть именно тот, который реализует `[EntityDomainBehaviours]`. Пропустите его — и атрибут молча не сгенерирует ничего. Чтобы посмотреть сгенерированный код, определите `ATOMIC_OUTPUT_SOURCEGEN_FILES`; файлы попадут в `Temp/GeneratedCode/`.

---

## Идиомы

### Поведение — логика без состояния, ссылки кешируются в `Init`

Это центральный паттерн модуля.

```csharp
// ХОРОШО
public sealed class MoveBehaviour : IEntityInit, IEntityTick
{
    private IVariable<Vector3> _position;
    private IValue<float> _moveSpeed;
    private IValue<Vector3> _moveDirection;

    public void Init(IEntity entity)
    {
        _position      = entity.GetValue<IVariable<Vector3>>("Position");
        _moveSpeed     = entity.GetValue<IValue<float>>("MoveSpeed");
        _moveDirection = entity.GetValue<IValue<Vector3>>("MoveDirection");
    }

    public void Tick(IEntity entity, float deltaTime)
    {
        Vector3 direction = _moveDirection.Value;
        if (direction != Vector3.zero)
            _position.Value += _moveSpeed.Value * deltaTime * direction;
    }
}
```

Почему именно так: поиск в хранилище, приведение типа и распаковка происходят один раз в `Init`; `Tick` трогает только закешированные ссылочные обёртки, то есть покадровая цена — чтения полей и арифметика. Класс `sealed` (девиртуализуется). Он не хранит игрового состояния — всё состояние в значениях сущности, поэтому инспектируемо и доступно другим поведениям. Реализованы ровно две нужные фазы, поэтому сущность не будет дёргать его в `FixedTick` и `LateTick`.

```csharp
// ПЛОХО: поиск + приведение + распаковка на каждом кадре, для каждой сущности
public sealed class MoveBehaviour : IEntityTick
{
    public void Tick(IEntity entity, float deltaTime)
    {
        float speed  = entity.GetValue<float>("MoveSpeed");        // хеш + обход цепочки + распаковка, каждый кадр
        Vector3 dir  = entity.GetValue<Vector3>("MoveDirection");  // то же ещё раз
        entity.SetValue("Position",
            entity.GetValue<Vector3>("Position") + speed * deltaTime * dir);  // упаковка Vector3 каждый кадр
    }
}
```

### Физика — в `IEntityFixedTick`

```csharp
public sealed class MoveBehaviour : IEntityInit, IEntityFixedTick
{
    private Transform _transform;
    private IValue<float> _moveSpeed;
    private IValue<Vector3> _moveDirection;

    public void Init(IEntity entity)
    {
        _transform     = entity.GetValue<Transform>("Transform");
        _moveSpeed     = entity.GetValue<IValue<float>>("MoveSpeed");
        _moveDirection = entity.GetValue<IValue<Vector3>>("MoveDirection");
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        Vector3 direction = _moveDirection.Value;
        if (direction != Vector3.zero)
            _transform.position += _moveSpeed.Value * deltaTime * direction;
    }
}
```

Форма та же, меняется только интерфейс фазы. Поведение переносимо между `Entity` и `MonoEntity`, потому что говорит с `IEntity`, а не с `MonoBehaviour`. Выбор `IEntityFixedTick` помещает его в массив `_fixedUpdates`, который гонит `TickableManager.FixedUpdate` с `Time.fixedDeltaTime`.

### Композицией владеет инсталлер; поведения не добавляют себе данные

```csharp
public sealed class CharacterInstaller : MonoEntityInstaller
{
    [SerializeField] private Transform _transform;
    [SerializeField] private Const<float> _moveSpeed = 5.0f;              // неизменяемое
    [SerializeField] private ReactiveVariable<Vector3> _moveDirection;    // изменяемое, с подпиской

    public override void Install(IEntity entity)
    {
        entity.AddTag("Character");
        entity.AddTag("Moveable");

        entity.AddValue("Transform", _transform);
        entity.AddValue("MoveSpeed", _moveSpeed);
        entity.AddValue("MoveDirection", _moveDirection);

        entity.AddBehaviour<MoveBehaviour>();
    }
}
```

Один файл читает инспектор и пишет в сущность, поэтому полный контракт сущности аудируется в одном месте. Настроечные числа — это `[SerializeField]`-обёртки, а не константы в коде: обёртка ссылочная, значит добавляется один раз без упаковки и читается без распаковки. `AddBehaviour<MoveBehaviour>()` использует расширение с ограничением `new()` и возвращает созданный экземпляр, если он нужен.

### Типизированный инсталлер + типизированное поведение

Это целевая форма для проекта, а не строковая.

```csharp
public sealed class TransformEntityInstaller : MonoEntityInstaller<IGameEntity>
{
    [SerializeField] private Const<float> _scale = 1;

    public override void Install(IGameEntity entity)   // override ОБЯЗАТЕЛЕН: базовый метод abstract
    {
        entity.AddPosition(new ReactiveVector3());
        entity.AddRotation(new ReactiveQuaternion());
        entity.AddScale(_scale);

        entity.AddBehaviour<TransformGizmos>();
    }
}

public sealed class TransformGizmos : IEntityGizmos<IGameEntity>
{
    public void DrawGizmos(IGameEntity entity)
    {
        Vector3 center = entity.GetPosition().Value;
        float scale = entity.GetScale().Value;
        Handles.DrawWireDisc(center, Vector3.up, scale);
    }
}
```

`MonoEntityInstaller<IGameEntity>` и `IEntityGizmos<IGameEntity>` убирают все приведения типов. `AddPosition`/`GetScale` генерируются из полей `ValueKey<IGameEntity, T>`, то есть проверяются компилятором, инлайнятся и переживают переименования. Гизмо — это обычное поведение, а не переопределение `OnDrawGizmos`, поэтому отладочная отрисовка композируется и деинсталлируется как всё остальное.

```csharp
// ПЛОХО — не компилируется
[Serializable]                                                      // бессмысленно: это MonoBehaviour
public sealed class TransformEntityInstaller : MonoEntityInstaller<IGameEntity>
{
    public void Install(IGameEntity entity) { /* ... */ }            // CS0534 + CS0114: нет override
}
```

### Ключи объявляются один раз в статическом API-классе

```csharp
[EntityExtensionsAPI]
public static partial class GameEntityAPI
{
    public static readonly TagKey IsEnemy = new(nameof(IsEnemy));
    public static readonly TagKey<IGameEntity> IsSelectable = new(nameof(IsSelectable));
    public static readonly ValueKey<IGameEntity, int> Health = new(nameof(Health));
    public static readonly ValueKey<IGameEntity, float> MoveSpeed = new(nameof(MoveSpeed));
}
```

`static readonly` + `nameof(Field)` означает, что разрешение имени в id происходит ровно один раз при инициализации типа, имя ключа физически не может разойтись с именем поля, а переименование поля переименовывает ключ. `TagKey<IGameEntity>` / `ValueKey<IGameEntity, T>` делают ключ непригодным для чужого типа сущности. `partial` нужен, чтобы генератор дописал расширения в тот же класс.

```csharp
// ПЛОХО — анализатор отвергает оба варианта
public static readonly TagKey IsEnemy;                  // EAPI0001: нет инициализатора
public static readonly ValueKey<int> Health = new();     // EAPI0002: new() / default оставляет Id = 0

// и то, что всё это заменяет:
const int Health = 1;   // магические числа, нет типобезопасности, коллизии молча
```

Оба правила — уровня Error, оба идут с code fix.

### Доступ через сгенерированные расширения

```csharp
entity.AddTag(GameEntityAPI.IsEnemy);
entity.AddValue(GameEntityAPI.Health, 100);

int health = entity.GetValue(GameEntityAPI.Health);
float moveSpeed = entity.GetMoveSpeed();
bool selectable = entity.HasTag(GameEntityAPI.IsSelectable);
```

Оба стиля вызова равноправны: передать struct-ключ в обобщённое расширение или вызвать сгенерированный именованный аксессор. В любом случае `T` выводится из ключа (не нужно писать `GetValue<int>`), компилятор отвергает несовпадение типа, а цепочка `AggressiveInlining` сворачивается до сырой `int`-перегрузки.

```csharp
// ПЛОХО: медленнее и без проверок — строка хешируется через EntityKeyStore при каждом вызове,
// а опечатка компилируется
int health = entity.GetValue<int>("Health");
entity.AddTag("Enemy");   // это было "Enemy" или "IsEnemy"?
```

### Итерация только через конкретный тип

```csharp
// ХОРОШО: struct-энумераторы, ноль аллокаций
Entity entity = ...;

Entity.TagEnumerator tags = entity.GetTagEnumerator();
Entity.ValueEnumerator values = entity.GetValueEnumerator();
Entity.BehaviourEnumerator behaviours = entity.GetBehaviourEnumerator();

while (tags.MoveNext())
{
    int tagId = tags.Current;
    // ...
}
```

```csharp
// ПЛОХО: явные реализации интерфейса упаковывают каждый энумератор
IEntity entity = ...;

IEnumerator<int> tags = entity.GetTagEnumerator();
IEnumerator<KeyValuePair<int, object>> values = entity.GetValueEnumerator();
IEnumerator<IEntityBehaviour> behaviours = entity.GetBehaviourEnumerator();
// -> аллокация в куче на каждый вызов + давление на GC
```

Держите результат именно в его struct-типе. `var` через интерфейсную ссылку или локальная переменная типа `IEnumerator<T>` ломают всю затею.

### Предразмерить сущность, а не давать ей расти

```csharp
var entity = new Entity(
    "Character",
    tagCapacity: 2,
    valueCapacity: 2,
    behaviourCapacity: 2,
    new Entity.Settings { disposeValues = false }   // для пулов: значения живут дольше сущности
);
```

Каждый рост ёмкости — это `Array.Resize` и слотов, и бакетов, плюс полный рехеш живых слотов. Для сущностей, которые создаются массово, однократное указание ёмкости убирает всё это. В Unity-пути ту же роль играют `initialTagCapacity` / `initialValueCapacity` / `initialBehaviourCapacity`.

### Измерить ёмкости в редакторе вместо угадывания

У инспектора `MonoEntity` есть контекстные команды (описаны в `Docs/Entities/Entities/MonoEntityEditor.md`):

```text
Compile -> 1. Disable + Dispose в Edit Mode (если это не префаб; только для поведений
              с [EditModeEntityBehaviourAttribute])
           2. Uninstall предыдущего состояния
           3. Install нового состояния
           4. Предрасчёт capacity / tags / values / behaviours
           5. Init + Enable в Edit Mode (те же условия, что в шаге 1)

Reset   -> 1. Disable + Dispose в Edit Mode (те же условия, что в шаге 1 Compile)
           2. Uninstall
           3. Сброс всех параметров в значения по умолчанию
           4. Сбор всех MonoEntityInstaller и дочерних сущностей
```

Вместо угадывания `initial*Capacity` соберите инсталлеры и нажмите `Compile` — компонент сам запишет измеренные ёмкости в сериализованные поля. `autoCompile` при этом держите выключенным (он и так выключен по умолчанию): он перезапускает `Install()` на каждом `OnValidate`.

### Синглтон-сущность как глобальный контекст

```csharp
public class GameContext : MonoEntitySingleton<GameContext>
{
}

// доступ
var context = GameContext.Instance;
context.AddValue("Score", 42);
int score = context.GetValue<int>("Score");
```

Контекст — это просто сущность: он бесплатно получает значения, поведения и полный цикл `Init/Enable/Tick`, виден в инспекторе и собирается теми же инсталлерами. Пустой подкласс нужен только чтобы задать статический `Instance`.

```csharp
// ПЛОХО — форма старого Atomic.Contexts, которой больше не существует:
// public sealed class GameContextInstaller : SceneContextInstaller { ... }
// public sealed class GameContext : SingletonSceneContext<GameContext> { ... }
// context.AddSystem<MySystem>(); context.GetService<Foo>();
```

### Прокси на дочерних коллайдерах

```csharp
public interface IGameEntity : IEntity
{
}

public class GameEntity : MonoEntity, IGameEntity
{
}

public sealed class GameEntityProxy : MonoEntityProxy<GameEntity>, IGameEntity
{
}

public class SomeTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider collider)
    {
        if (collider.TryGetComponent(out IGameEntity entity))   // попадёт и в прокси, и в корень
        {
            // ...
        }
    }
}
```

Объявление подкласса как `: MonoEntityProxy<GameEntity>, IGameEntity` делает так, что `TryGetComponent(out IGameEntity)` срабатывает на дочерних хитбоксах одним локальным поиском компонента — без обхода `GetComponentInParent` в коде коллизий, и код попадания не должен знать, попал он в корень или в конечность. При добавлении компонента `Reset()` сам подтягивает `Source`.

Важно: обращайтесь к прокси через `IGameEntity` или `IEntity`, а не через тип `GameEntityProxy` — иначе struct-перегрузки `AddValue<T>`/`SetValue<T>` прокси выиграют перегрузочное разрешение (см. «Грабли»).

### Корректное разрушение `MonoEntity`

```csharp
// ХОРОШО
MonoEntity.Destroy(entity);          // Uninstall(), затем Object.Destroy(gameObject)
MonoEntity.Destroy(entity, 1.5f);    // с задержкой
MonoEntity.Destroy(someIEntity);     // Cast() внутри — разворачивает MonoEntityProxy
```

```csharp
// ПЛОХО
Object.Destroy(entity.gameObject);   // пропускает Uninstall(): подписки инсталлеров утекают
entity.Dispose();                    // чистит хранилища, но НЕ снимает регистрацию и не убивает GameObject
```

### Цепочка установки и массовая установка сцены

```csharp
entity.Install(installerA).Install(installerB);   // Install(IEntityInstaller) возвращает IEntity

MonoEntity.InstallAll(scene);                 // все ещё не установленные MonoEntity в сцене
MonoEntity.InstallAll<GameEntity>(scene);     // только этот тип
MonoEntity.InstallAll(someComponent);         // всё поддерево под компонентом

entity.WhenInstall(() => { /* выполнится сейчас, если уже установлено, иначе при Install() */ });
```

Возврат `IEntity` из `Install` делает композицию инсталлеров одним выражением. Статические `InstallAll` позволяют бутстрапперу задать порядок установки явно (при `installOnAwake = false` на компонентах) вместо зависимости от порядка `Awake` в Unity. `WhenInstall` — чистый выход для системы, которая обязана отреагировать на сущность, чьим временем установки она не владеет: без опроса и без ожидания в `Update`.

### Подписка на одну фазу без класса-поведения

```csharp
public sealed class HudInstaller : MonoEntityInstaller<IGameEntity>
{
    private DisableSubscription _subscription;

    public override void Install(IGameEntity entity)
    {
        _subscription = entity.WhenDisable(() => Hud.Hide());
    }

    public override void Uninstall(IGameEntity entity)
    {
        _subscription.Dispose();
    }
}
```

Когда нужна одна реакция на одну фазу, целый класс-поведение — избыточен. `When*` возвращает подписку, и парный `Uninstall` её снимает. Это же тот случай, ради которого `Uninstall` вообще существует: он вызывается `MonoEntity.Uninstall()` и через `uninstallOnDestroy`.

### Переопределение `OnValidate` в инсталлере — только через `base`

```csharp
// ХОРОШО
public sealed class CharacterInstaller : MonoEntityInstaller
{
    public override void Install(IEntity entity) { /* ... */ }

    protected override void OnValidate()
    {
        base.OnValidate();     // иначе autoCompile у MonoEntity молча перестаёт работать
        // своя валидация
    }
}
```

```csharp
// ПЛОХО
private void OnValidate() { /* ... */ }   // скрывает protected virtual OnValidate() базового класса
```

Базовый `MonoEntityInstaller.OnValidate()` дёргает `onValidateCallback`, на котором держится редакторный `autoCompile` у `MonoEntity`. Своя `private void OnValidate()` его перекрывает, и перестановка переустановки в Edit Mode тихо пропадает.

### Сброс key store в тестах

```csharp
[SetUp]
public void SetUp()
{
    EntityKeyStore.Reset();
    EntityKeyStore.SetAlgorithm(new SequentialEntityKeyAlgorithm());
}
```

Поскольку алгоритм по умолчанию выдаёт id в порядке первого обращения, два теста, которые трогают ключи в разном порядке, увидят разные id. Сброс на каждый тест делает их изолированными и воспроизводимыми. В редакторе есть автоматический сброс при входе в Play Mode — но он пропускается, если включён `EnterPlayModeOptions.DisableDomainReload`.

---

## Производительность

Разделяю то, что **измерено/заявлено апстримом**, и то, что является **выводом из чтения кода**. Ссылки на исходники даны относительно `C:/!!!Work/_AtomicUpstream/Assets/Plugins/Atomic/Entities/Scripts`, ссылки на тесты (`Tests/...`) — относительно `C:/!!!Work/_AtomicUpstream/Assets/Plugins/Atomic/Entities`.

### Заявленные замеры

`Docs/Entities/Entities/Manual.md#-performance`: MacBook с Apple M1, 1000 элементов на контейнер, медианное время в микросекундах.

Теги — быстрее `HashSet<int>` почти везде:

| Операция | HashSet | Tags |
|---|---|---|
| Contains | 47.85 | 3.80 |
| Add | 57.40 | 8.30 |
| Remove | 50.45 | 5.40 |
| Clear | 1.10 | **2.80** |
| Enumerator | 29.75 | 2.30 |

Значения — быстрее на чтении, существенно **медленнее** на записи:

| Операция | Dictionary | Values |
|---|---|---|
| Get | 7.40 | 4.10 (object) |
| Get + Cast | 8.25 | 12.00 (ref) / 4.70 (value) |
| Get + Unsafe Cast | 7.80 | 4.20 (ref) / 4.50 (value) |
| Contains | 6.90 | 4.00 |
| Remove | 6.70 | 5.20 (ref) / 5.50 (value) |
| Add | 34.10 | **62.15 (ref) / 178.45 (value)** |
| Set | 37.50 | **62.50 (ref) / 187.35 (value)** |
| Enumerator | 56.60 | 56.80 (ref) / **171.75 (value)** |

Практический вывод, прямо следующий из таблицы: это хранилище оптимизировано под **частое чтение и редкую запись**. Соберите сущность один раз и читайте. `AddValue`/`SetValue` — не покадровая операция.

Поведения — смешанно:

| Операция | List | Behaviours |
|---|---|---|
| Contains | 1825.95 | 650.60 |
| Remove | 312.63 | 243.91 |
| Add | 29.30 | 34.30 |
| Clear | 0.40 | 1.20 |
| Get At | 1.60 | 2.30 |
| Enumerator | 29.95 | 28.80 |

Та же страница прямо пишет, что часть операций над поведениями в худшем случае O(n). Держите число поведений небольшим и никогда не ищите поведение покадрово.

Упаковка struct-значений — заявленная реальность этой версии, а не недосмотр: `Tests/Performance/Entity/EntityPerformance_Values.cs` специально замеряет `GetValue_AsObject`, `GetValue_AsReference` и `GetValue_Primitive` на N = 1000, причём примитивный случай строится через `entity.AddValue(i, 777)`. То есть апстрим сам бенчмаркает упакованный `int`. Это сильнейший из доступных аргументов за хранение ссылочных обёрток `Atomic.Elements` вместо сырых struct.

`Docs/BestPractices/IteratingOverEntity.md` формулирует правило итерации таблицей: обход через интерфейс `IEntity` вызывает упаковку и аллокации GC, обход напрямую через `Entity` или `MonoEntity` — без упаковки и без аллокаций.

### Подтверждено чтением исходников

**`int`-ключ сам является хешем.** Каждый поиск в `Entities/Core/Entity_Tags.cs`, `Entities/Core/Entity_Values.cs` и их `MonoEntity`-двойниках делает `int hash = key & 0x7FFFFFFF; int bucket = hash % _capacity;` и ни разу не вызывает `GetHashCode`. Одна маска, одно целочисленное деление, одно обращение к массиву. Самая дорогая инструкция горячего пути — именно `% _capacity`: ёмкость — рантайм-прайм, поэтому компилятор не может заменить деление сдвигами (вывод).

**Чтения слотов не копируют struct.** Все пути чтения (`HasTag`, `HasValue`, `GetValue`, `GetValue<T>`, `TryGetValue`, `CopyValues`, `MoveNext` обоих энумераторов) берут слот как `ref readonly TagSlot` / `ref readonly ValueSlot`. `GetValueUnsafe`/`TryGetValueUnsafe` используют обычный `ref`, потому что отдают наружу `ref Unsafe.As<object, T>(ref slot.value)`. Пути мутации пишут голову бакета на месте через `ref int next = ref _tagBuckets[bucket]`.

**`AggressiveInlining` поставлен выборочно, а не повсюду.** На `Entity` он есть у `ConstructTags`/`ConstructValues`/`ConstructBehaviours`, `IncreaseTagCapacity`/`IncreaseValueCapacity`, `FindValueIndex`, `ClearValues`, `CopyValues`, `CopyBehaviours<T>`, `DelBehaviourAt`, `EnableBehaviour`/`DisableBehaviour`, `Deinitialize`, `UnsubscribeEvents`, `OnDispose` и у обеих фабрик исключений. У самих горячих аксессоров (`HasTag`, `AddTag`, `DelTag`, `GetValue`, `GetValue<T>`, `TryGetValue`, `GetValueUnsafe`, `HasValue`, `AddValue`, `SetValue`, `DelValue`, `Tick`/`FixedTick`/`LateTick`, `AddBehaviour`) его **нет** — они оставлены JIT. Зато он есть у **каждого** ключевого метода в `Entities/Extensions_Tags.cs` и `Entities/Extensions_Values.cs`, а также у `EntityKeyStore.NameToId`/`IdToName` — именно это заставляет обёртку `TagKey`/`ValueKey` исчезнуть при компиляции.

**Очистки без аллокаций, двумя разными техниками.** `Entity_Tags.ClearTags` берёт `Span<int> removedTags = stackalloc int[_tagCount]`. `Entity_Values.ClearValues` арендует буфер у `ArrayPool<KeyValuePair<int, object>>.Shared` и возвращает его в `finally`. `Entity_Behaviours.GetBehaviours<T>()` берёт `Span<int> indexes = stackalloc int[_behaviourCount]`. Все три выделяют на стеке пропорционально числу живых элементов, поэтому патологически большой счётчик даёт stack overflow (вывод; документация отмечает риск только для `GetBehaviours<T>`).

**Диспетчеризация тиков предфильтрована.** `EnableBehaviour`/`DisableBehaviour` делают проверки `is IEntityTick` / `is IEntityFixedTick` / `is IEntityLateTick` один раз, на `Enable`, и раскладывают поведение в три отдельных массива через `InternalUtils.Add`/`Remove`. `Tick()` после этого — просто `for (i < _updateCount) _updates[i].Tick(this, dt)`: один интерфейсный вызов на поведение, без проверок типов и без аллокаций.

**Безопасность `Disable()` посреди цикла обеспечена счётчиком, а не проверкой.** Тест `Tests/EditMode/Entity/EntityTests_Lifecycle.cs:1010` (`OnUpdate_StopsCallingIfEntityDisabledMidLoop`) проходит потому, что `Disable()` прогоняет `DisableBehaviour` по всем поведениям, каждое из них снимается из `_updates`, и `_updateCount` падает до нуля — условие цикла перестаёт выполняться. Явной перепроверки в `Entity.Tick` нет (в отличие от `LateTick`, у которого `&& _enabled` в условии).

**`Disable()` квадратичен по сравнениям.** `InternalUtils.Remove`, который `DisableBehaviour` вызывает для всех трёх фаз, — это линейный поиск с `comparer.Equals` плюс компактизация `Array.Copy`, то есть O(n) на поведение, и O(n²) интерфейсных сравнений на сущность с n тикающими поведениями. Сравнители закешированы в статиках (`s_updateComparer` и соседи), так что хотя бы аллокаций на вызов нет (вывод).

**Регистрация в `TickableManager` тоже квадратична.** `Register` идёт через `InternalUtils.AddIfAbsent` — линейный проход по всем уже зарегистрированным `ITickSource` с `EqualityComparer<ITickSource>.Default` перед каждой вставкой; `Unregister` через `InternalUtils.Remove` — так же. Спавн N сущностей в один кадр даёт O(N²) интерфейсных сравнений равенства (вывод). Для массового спавна ставьте `useUnityLifecycle = false` и гоните тики своим циклом или через `EntityWorld`.

**Семантика ёмкости.** `InternalUtils.CeilToPrime` делает `Array.BinarySearch` по `PrimeTable` и берёт `~index`, то есть округляет запрос **вверх** до следующего прайма: `0 -> 2`, `1 -> 2`, `4 -> 7`, `10 -> 17`. Значит `initialTagCapacity = 1` выделяет 2 слота и 2 бакета. Запрос больше `1610612741` даёт `InvalidOperationException`. Рост — `PrimeTable[++_primeIndex]` плюс `Array.Resize` обоих массивов, `Array.Fill` бакетов и полную перепривязку всех живых слотов к новым бакетам. Массив поведений растёт иначе: `Array.Resize(ref _behaviours, capacity == 0 ? 1 : capacity * 2)`.

**`in`/`ref` в параметрах применяются узко.** Только `MonoEntity_Creation.cs` передаёт `CreateArgs` по `in` (`Create(in CreateArgs)` и `Create<E>(in CreateArgs)`). Больше в этой области ничего не берёт `in`/`ref`-параметров — `TagKey`/`ValueKey` передаются по значению, что бесплатно: это `readonly struct` с единственным полем `int` (подтверждено во всех четырёх файлах ключей).

**Что именно аллоцирует в публичном API чтения.** `GetTags()` -> `int[_tagCount]`. `GetValues()` -> `KeyValuePair<int, object>[_valueCount]` плюс `CopyValues` в него. `GetBehaviours()` -> `IEntityBehaviour[_behaviourCount]`. `GetBehaviours<T>()` -> результирующий `T[]` (при нулевом счётчике возвращает `Array.Empty<T>()`). Безаллокационные двойники — `CopyTags`, `CopyValues`, `CopyBehaviours`, `CopyBehaviours<T>`; именно их и надо звать покадрово. `Extensions.DisposeValues` идёт через `GetValues()`, то есть аллоцирует — он для `Dispose`/`OnDestroy`, не для кадра.

**Хранилище `MonoEntity` строится в `OnAfterDeserialize`.** `ISerializationCallbackReceiver.OnAfterDeserialize()` вызывает `Construct()` и `Register()`, то есть до `Awake` и к тому же в редакторе при каждой десериализации. Значит `MonoEntity` в сцене уже держит выделенные массивы слотов/бакетов и id реестра в Edit Mode, и его `InstanceID` присваивается вне Play Mode. `MonoEntity_Registration.Register` защищён условием `if (_registered && _instanceId > 0) return;`, что делает повторные редакторные вызовы идемпотентными.

**`Entity` и `MonoEntity` — две рукописные копии.** Поля диспетчеризации `MonoEntity` называются без подчёркивания (`updates`, `fixedUpdates`, `lateUpdates`, `updateCount`, ...) против `_updates`/`_updateCount` у `Entity`. Это два продублированных руками файла, а не общий код — любое исправление придётся вносить дважды.

**`EntityRegistry` держит плотный индекс.** Id переиспользуются через LIFO-стек `int[] _recycledIds`, а параллельный массив `_order` с `orderIndex` в слоте позволяет `Unregister` делать O(1) swap-remove, не разрежая индексатор. Побочное наблюдение: цикл в `Unregister` после найденного совпадения не делает `return`, а переписывает `slot.next = _freeList` и продолжает идти по свободному списку — безвредно (id уникальны, второго совпадения не будет), но это лишняя работа на каждой разрегистрации.

**Про пулы.** В этой области есть ровно один относящийся к пулам рычаг: `Entity.Settings.disposeValues` (и инспекторный `disposeValues` у `MonoEntity`), который надо выставить в `false`, когда значения должны пережить сущность. Сам модуль пулов (`Scripts/Pooling`, `EntityPool<TEntity, TArgs>` и соседи) — отдельная тема. Учтите при этом ошибку в `ClearTags`/`ClearValues`, описанную в «Граблях»: переиспользование сущности через `Clear()` в этой версии небезопасно.

---

## Грабли

- **Расхождение документации и исходников — системное.** Проверяйте каждую сигнатуру по `.cs`, прежде чем на неё опираться. Найденные случаи: базовые интерфейсы `IEntity` — это `IInitSource`/`IEnableSource`/`ITickSource`, а не `I*Lifecycle`; `IEntity.IsValid` не задокументирован; события значений несут три аргумента, а не два; метод называется `GetBehaviourAt(int)`, а не `GetBehaviour(int)`; `AddBehaviour<T>()` возвращает `T`, а не `void`; у `MonoEntityProxy.Source` есть сеттер; у `MonoEntity.InstallAll` есть лишний параметр `includeInactive`.

- **Задокументированного struct-API значений не существует.** Ни `AddValue<T>(int, T) where T : struct`, ни `SetValue<T>` со struct-ограничением нет на `IEntity`, `Entity` и `MonoEntity`. Утверждение «добавление struct через обобщённое API избегает упаковки» для этой версии неверно. Храните ссылочные обёртки (`Const<T>`, `ReactiveVariable<T>`, `IValue<T>`) для всего, что читается покадрово.

- **Исключение из предыдущего пункта — `MonoEntityProxy<E>`, и это ловушка.** Файл ``MonoEntityProxy`1.cs``, строки 135 и 138, объявляют `AddValue<T>(int, T) where T : struct` и `SetValue<T>(int, T) where T : struct`. Они не члены `IEntity` и ничего не экономят (каждая просто вызывает `object`-перегрузку, упаковка остаётся), но на ссылке типа прокси выигрывают перегрузочное разрешение. Один и тот же вызов `x.AddValue(key, 5)` связывается с разными методами в зависимости от статического типа `x`. Работайте с прокси через `IEntity`/`IGameEntity`.

- **`GetValueUnsafe<T>` — это не проверяемое приведение, а переинтерпретация.** `where T : class` плюс `Unsafe.As<object, T>(ref slot.value)`. Неверный `T` даёт испорченную ссылку, а не `InvalidCastException`. Применяйте только там, где хранимый тип гарантирован, в остальных местах — `GetValue<T>`.

- **`Unsafe = true` на `[EntityExtensionsAPI]` для value-типов даёт некомпилируемый код.** Генератор (`SourceGenerators/Project~/Docs/EntityAPIGenerator.md`) выпускает `entity.GetValueUnsafe<int>(...)` и `ref int RefHealth(...)` для ключа `ValueKey<IEntity, int>`, но `GetValueUnsafe<T>` ограничен `where T : class` (`Scripts/Entities/Interface/IEntity_Values.cs`, `Scripts/Entities/Extensions_Values.cs`). Отдельно: описанный в том же doc'е пофайловый опт-аут `[Unsafe(false)]` тоже не скомпилируется — `Scripts/Codegen/UnsafeAttribute.cs` объявлен как `public sealed class UnsafeAttribute : Attribute { }` без параметров конструктора, несмотря на таблицу «`value` bool, default true» в документации. Поле можно только включить в unsafe-режим, выключить обратно нельзя.

- **`Tick`/`FixedTick`/`LateTick` при выключенной сущности молча ничего не делают** (`if (!_enabled) return;`). Документация утверждает, что они бросают. Система, которая тикает ещё не включённую сущность, просто не даст симуляции — без единой ошибки в логе.

- **`SetValue` — upsert с проверкой по упакованному равенству.** По отсутствующему ключу он добавляет значение и шлёт `OnValueAdded`, а не бросает. Проверка изменения — `!slot.value.Equals(value)`, то есть равенство упакованных значений: `SetValue` равным struct — молчаливый no-op без событий. Для ссылочного типа, который переопределяет `Equals` по значению, установка другого-но-равного экземпляра оставит в сущности **старый** экземпляр. Предпочитайте мутацию хранимой `ReactiveVariable<T>` вместо `SetValue` нового объекта.

- **`AddValue(key, null)` и повторный `AddValue` бросают.** `ArgumentNullException` на `null` и `ArgumentException` на существующий ключ — его собирает приватная фабрика `Entity.ValueAlreadyAddedException(int)`, отдельного типа исключения в модуле нет. При этом `AddTag` на дубликат просто возвращает `false`. Асимметрия намеренная, но её легко забыть.

- **`DelBehaviours<T>()` пропускает элементы.** Реализация — `for (int i = 0; i < _behaviourCount; i++) if (_behaviours[i] is T) this.DelBehaviourAt(i);`, а `DelBehaviourAt` компактизует массив через `Array.Copy`. Элемент, сдвинувшийся в освободившийся слот `i`, уже не проверяется. Два подряд идущих поведения типа `T` — и второе останется подключённым. Баг присутствует и в `Entity_Behaviours.cs`, и в `MonoEntity_Behaviours.cs`, и в `Tests/EditMode/Entity/EntityTests_Behaviours.cs` нет теста на `DelBehaviours`. Обходите массив назад сами или вызывайте в цикле до `!HasBehaviour<T>()`.

- **`ClearTags()`/`ClearValues()` могут оставить висячую голову бакета.** Цикл сброса бакетов — `for (int i = 0; i < _tagLastIndex; i++) _tagBuckets[i] = UNDEFINED_INDEX;`: он идёт до `_lastIndex`, тогда как бакеты индексируются `hash % _capacity` и покрывают всю ёмкость. Голова бакета с индексом `>= _lastIndex` переживает очистку как неотрицательный индекс слота, и следующий `AddTag`/`AddValue` подцепит новый слот к ней. Воспроизводится так: ёмкость 7, один тег с ключом 5 при `_lastIndex == 1`, затем `ClearTags()`, затем добавление снова. Проблема одинакова в `Entity_Tags.cs`, `Entity_Values.cs` и в двойниках `MonoEntity`. Следствие: переиспользование сущности через `Extensions.Clear()` в этой версии небезопасно — создавайте новую.

- **`MonoEntity.Dispose()` не снимает регистрацию.** В отличие от `Entity.Dispose()`, здесь нет `EntityRegistry.Unregister`, и `GameObject` остаётся. Ручной `Dispose()` на `MonoEntity` оставляет зарегистрированный живой компонент с валидным `InstanceID` и пустыми хранилищами. Для настоящего разрушения — `MonoEntity.Destroy(entity)`.

- **При `useUnityLifecycle = false` `OnDestroy` не диспозит.** Первые три действия (`OnDispose`, `Deinitialize`, `DisposeValues`) закрыты условием `if (this.useUnityLifecycle && _started)`. В этом режиме `OnDestroy` только `Uninstall`-ит, чистит хранилища, отписывает события и снимает регистрацию. `IEntityDispose.Dispose` на поведениях не вызывается, `IDisposable`-значения не диспозятся. Сущности, которые вы тикаете вручную, обязаны вызывать `Dispose()` сами.

- **`Object.Destroy(entity.gameObject)` пропускает `Uninstall()`.** Подписки и пулёные объекты, которые держал инсталлер, утекают. Используйте `MonoEntity.Destroy` — он к тому же разворачивает `MonoEntityProxy` через `Cast()`.

- **По умолчанию `CreateArgs` всё выключено.** `useUnityLifecycle`, `installOnAwake`, `uninstallOnDestroy`, `disposeValues` — обычные `bool`-поля struct, значит `new CreateArgs { name = "Enemy" }` даёт сущность, которая никогда не тикает и никогда не устанавливается. Берите параметризованные `Create<E>(...)`/`Create(...)`, у которых все умолчания `true`, либо задавайте каждый `bool` явно.

- **Unity-колбэки `MonoEntity` недоступны для переопределения из вашей сборки.** Они `private protected virtual`, так что перекрыть `Awake`/`Start`/`OnEnable`/`OnDisable`/`OnDestroy` могут только типы внутри `Atomic.Entities` (например `MonoEntitySingleton`). Используйте `OnInstall`/`OnUninstall`/`OnDispose` или `WhenInstall`/`WhenUninstall`.

- **`private void OnValidate()` в инсталлере ломает `autoCompile`.** Он скрывает `protected virtual OnValidate()` базового `MonoEntityInstaller`, который дёргает `onValidateCallback`. Правильная форма — `protected override void OnValidate() { base.OnValidate(); ... }`.

- **`MonoEntityInstallerConfigurable` не пробрасывает `Uninstall`.** Он переопределяет только `Install`, поэтому вложенные через `[SerializeReference]` инсталлеры никогда не деинсталлируются. Если вложенный инсталлер держит подписки, снимайте их иначе.

- **`Install(E entity)` в типизированных инсталлерах — `abstract`, `override` обязателен.** `public void Install(IGameEntity entity)` вместо `public override void Install(...)` даёт CS0534 (абстрактный член не реализован) плюс CS0114 (скрытие унаследованного). И `[Serializable]` на наследнике `MonoEntityInstaller` бессмысленен — это `MonoBehaviour`, он не может быть вложенным сериализуемым классом.

- **У `MonoEntity.Install()` нет установки дочерних сущностей, и поля `children` нет**, хотя `MonoEntityInstalling.md` и `MonoEntityCreateArgs.md` описывают и то, и другое. Собирайте иерархии через `MonoEntity.InstallAll(component)` или явными вызовами.

- **У `MonoEntitySingleton<E>` нет ни `isGlobal`, ни `Resolve(Component)`/`Resolve(GameObject)`/`Resolve(Scene)`**, хотя документация описывает все четыре. Разрешение контекста по сценам придётся строить иначе (`MonoEntityWorld` или собственный lookup).

- **`EntitySingleton<E> : Entity` задокументирован, но исходника нет.** `Docs/Entities/Entities/EntitySingleton.md` описывает его целиком, включая `SetFactory(IEntityFactory<E>)`, `DisposeInstance`, `DropInstance`, `ResetInstance`, — но в `Scripts/Entities` единственный синглтон-тип это `MonoEntitySingleton<E>`. Не планируйте чистый C#-синглтон-контекст вокруг него: держите ссылку сами или используйте модуль фабрик.

- **`EntityNames.NameToId(...)` из `Docs/Entities/Entities/EntityCore.md` не существует.** Класс называется `EntityKeyStore` (`Scripts/KeyStore/EntityKeyStore.cs`), статические методы — `NameToId`, `IdToName`, `SetAlgorithm`, `Reset`.

- **Id ключей нестабильны.** `SequentialEntityKeyAlgorithm.NameToId(string name) => _nextId++` полностью игнорирует `name`: id зависят от порядка первого обращения и не совпадают между сессиями, сборками, клиентом и сервером. Никогда не сериализуйте, не сохраняйте и не отправляйте по сети id тега или значения — сериализуйте имя и разрешайте id при загрузке. Нужны стабильные id — реализуйте детерминированный (например FNV-1a) `IEntityKeyAlgorithm` и поставьте его на старте, **до** инициализации первого поля-ключа.

- **`EntityKeyStore.SetAlgorithm` чистит оба кеша.** Вызов после того, как хотя бы один ключ уже разрешён, молча обнуляет все закешированные id, тогда как уже созданные struct-ключи `TagKey`/`ValueKey` продолжают держать старые. Вызывать — ровно один раз, на старте, до любого обращения к ключам.

- **Автосброс key store в редакторе пропускается при выключенном domain reload.** `ResetOnEnterPlayMode` сразу возвращается, если выставлен `EnterPlayModeOptions.DisableDomainReload`. В этой конфигурации id переживают вход в Play Mode, а статические поля-ключи при этом не переинициализируются — ждите труднообъяснимых несовпадений.

- **`InstanceID` переиспользуется.** `EntityRegistry.Register` берёт `_recycledCount > 0 ? _recycledIds[--_recycledCount] : ++_maxId`, а `Unregister` кладёт освободившийся id в стек. Значит устаревшая ссылка на `IEntity` и свежесозданная сущность могут оказаться `Equals` и совпасть по хешу. Плюс `Unregister` сбрасывает `InstanceID` в `0`, так что разрегистрированная сущность хешируется в ноль. Никогда не оставляйте disposed-сущность ключом словаря или элементом `HashSet`.

- **Ничто в этой области не потокобезопасно.** `EntityKeyStore` (обычные `Dictionary`), `Entity`, `MonoEntity`, `SequentialEntityKeyAlgorithm`, `MonoEntitySingleton` (статический кеш) — всё держите на главном потоке или синхронизируйте снаружи.

- **Коллекционные конструкторы `Entity` перечисляют коллекцию дважды.** Оба делегируют в capacity-конструктор через LINQ `tags?.Count() ?? 0`. Для ленивого запроса это второй полный проход. Передавайте материализованные массивы/списки либо используйте capacity-конструктор и добавляйте потом.

- **`stackalloc` пропорционален числу живых элементов.** `Entity.ClearTags()` делает `stackalloc int[_tagCount]`, `GetBehaviours<T>()` — `stackalloc int[_behaviourCount]`. На нормальных размерах это выигрыш, на патологических — stack overflow.

- **Запрошенные ёмкости округляются вверх до прайма** из `InternalUtils.PrimeTable` (`1 -> 2`, `4 -> 7`, `10 -> 17`), а `MonoEntity.Create` дополнительно зажимает их через `Mathf.Max(1, ...)`. Числа ёмкости — рекомендательные. И `initialBehaviourCapacity = 0` означает `new IEntityBehaviour[0]`, то есть переаллокацию на первом `AddBehaviour`.

- **Самодельные id могут выродить поиск в O(n).** Ключ используется как свой хеш (`key & 0x7FFFFFFF`) с бакетизацией `% capacity`. Последовательные id распределяются идеально, а назначенные руками с общим шагом (например все кратные прайм-ёмкости) соберутся в одну цепочку.

- **`autoCompile` перезапускает `Install()` на каждом `OnValidate`.** Если инсталлеры создают Unity-объекты или другие тяжёлые объекты, это завалит редактор. Оставляйте выключенным — он и по умолчанию выключен, и инспектор показывает про это предупреждение.

- **Odin Inspector опционален, но без него нет отладочного UI.** Все панели Tags/Values/Behaviours и отображение `InstanceID` закрыты `#if ODIN_INSPECTOR` (а `Entity_Debug.cs` — целиком под `#if UNITY_EDITOR && ODIN_INSPECTOR`). Без Odin вы теряете ровно ту инспекцию состояния, которую показывает документация.

- **Жёсткое требование Unity 6 / .NET 7+** (`Docs/Entities/Manual.md#-requirements`). Типизированные интерфейсы поведений и инсталлеров построены на default interface members, `MonoEntity.CreateAsync` использует `InstantiateAsync`/`AsyncInstantiateOperation<E>` из Unity 6, `MonoEntitySingleton` — `FindAnyObjectByType`. На Unity 2022.3 модуль не соберётся.

- **Имена файлов с арностью непоследовательны.** В `Scripts/KeyStore/Keys/` используется **апостроф** (U+0027): `TagKey'1.cs`, `ValueKey'1.cs`. Во всех остальных папках — **обратная кавычка** (U+0060): ``MonoEntityProxy`1.cs``, ``IEntityInstaller`1.cs``, ``MonoEntityInstaller`1.cs``, ``ScriptableEntityInstaller`1.cs``, ``EntityWorld`1.cs``, ``EntityPool`1.cs``. Копирование пути из документа в shell или в «открыть файл» IDE ломается, если угадать не тот символ. Проверено прямым чтением кодов символов в именах файлов: 39 для двух файлов ключей, 96 для остальных.

- **Переименование `installers` -> `sceneInstallers`.** На поле стоит `[FormerlySerializedAs("installers")]`, поэтому старые сцены и префабы миграцию переживут автоматически. Но ваш код, который лез в это поле рефлексией или через `SerializedProperty("installers")`, сломается.

- **Порядок инсталлеров.** `Install()` гонит `scriptableInstallers`, затем `sceneInstallers`; `Uninstall()` — `OnUninstall()`, затем `sceneInstallers`, затем `scriptableInstallers`. Если ScriptableObject-инсталлер зависит от того, что положил scene-инсталлер, порядок будет против вас.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Обзорные документы

- `Docs/Entities/Manual.md` — корневой документ модуля. Содержит раздел Requirements (Unity 6 / .NET 7+), заметку про опциональность Odin, полное описание кодогенерации Entity API (объявление ключей, свойства `Unsafe`/`AggressiveInlining`, правила анализатора `EAPI0001`/`EAPI0002`, шаги установки DLL), раздел про `[EntityDomainBehaviours]`, индекс API и ссылки на Best Practices. Начинать читать нужно отсюда, но помните: раздел Setup перечисляет четыре DLL вместо пяти.
- `Docs/Entities/Entities/Manual.md` — обзор четырёх типов сущностей с рабочими примерами (`Entity`, `MonoEntity`, `MonoEntityProxy`, `MonoEntitySingleton`), затем сводный API Reference и, главное, **раздел Performance** с тремя таблицами замеров (Tags / Values / Behaviours) на M1, 1000 элементов, медианы в микросекундах. Единственное место, где эти числа опубликованы.
- `Docs/Entities/KeyStore/Manual.md` — обзор подсистемы ключей: пример с последовательным алгоритмом, пример использования `TagKey`/`ValueKey` и API Reference, разбитый на Store / Keys / Algorithms.
- `Docs/BestPractices/IteratingOverEntity.md` — правило итерации с таблицей «через `IEntity` — упаковка и аллокации GC / через `Entity` или `MonoEntity` — без упаковки, ноль аллокаций». Короткий документ, обязательный к прочтению для всех, кто пишет покадровый код.

### Интерфейс `IEntity`

- `Docs/Entities/Entities/IEntity.md` — тип в целом, пример использования, список «модулей» (разбивка на Core/Tags/Values/Behaviours/Lifecycle) и Notes. Базовые интерфейсы здесь названы `I*Lifecycle` — в исходнике это `I*Source`.
- `Docs/Entities/Entities/IEntityCore.md` — `InstanceID`, `Name`, `OnStateChanged`. `IsValid` не описан.
- `Docs/Entities/Entities/IEntityTags.md` — теги на интерфейсе; три примера доступа (числовые ключи, строковые имена, сгенерированный Entity API), затем события, свойства, методы.
- `Docs/Entities/Entities/IEntityValues.md` — то же для значений: три стиля доступа плюс полный список событий/свойств/методов. События в документе показаны с двумя аргументами, в исходнике их три.
- `Docs/Entities/Entities/IEntityBehaviours.md` — поведения на интерфейсе: базовое использование, использование через расширения, затем события/свойства/методы.
- `Docs/Entities/Entities/IEntityLifecycle.md` — фазы `Init/Enable/Tick/FixedTick/LateTick/Disable/Dispose`, их события и свойства. Утверждение, что `Tick` бросает при выключенной сущности, неверно.

### `Entity` (чистый C#)

- `Docs/Entities/Entities/Entity.md` — Quick Start по классу, тип, разбивка на модули и Notes.
- `Docs/Entities/Entities/EntityCore.md` — конструкторы (все три), события, свойства. Именно здесь встречается несуществующий `EntityNames.NameToId(...)`.
- `Docs/Entities/Entities/EntityTags.md`, `EntityValues.md`, `EntityBehaviours.md` — почти дословные повторы соответствующих `IEntity*`-документов. Их ценность — конкретные типы возвращаемых struct-энумераторов (`Entity.TagEnumerator` и прочие).
- `Docs/Entities/Entities/EntityLifecycle.md` — жизненный цикл конкретного класса: пример, события, свойства, методы.
- `Docs/Entities/Entities/EntityDebug.md` — отладочные панели для `Entity` (Core/Tag/Value/Behaviour) и таблица параметров отладки. Всё описанное существует только при `UNITY_EDITOR && ODIN_INSPECTOR`.
- `Docs/Entities/Entities/EntitySingleton.md` — **документ без исходника.** Полностью описывает `EntitySingleton<E> : Entity`, включая конструкторы, `SetFactory(IEntityFactory<E>)`, `DisposeInstance`/`DropInstance`/`ResetInstance` и пример со сбросом синглтона в тестах. В `Scripts/Entities` такого типа нет. Читать как материал по паттерну, не как API.

### Расширения `IEntity`

- `Docs/Entities/Entities/ExtensionsCore.md` — `Clear(this IEntity)` и прочее из `Entities/Extensions.cs`.
- `Docs/Entities/Entities/ExtensionsTags.md` — все перегрузки тегов: `string`, `TagKey`, `TagKey<E>`, плюс `HasAllTags`/`HasAnyTag`.
- `Docs/Entities/Entities/ExtensionsValues.md` — все перегрузки значений, включая три формы `GetValueUnsafe` (две возвращают `ref T`, третья — `T` по значению) и `DisposeValues`.
- `Docs/Entities/Entities/ExtensionsBehaviours.md` — `AddBehaviour<T>()` (в документе объявлен как `void`, в исходнике возвращает `T`) и пакетные `AddBehaviours`/`DelBehaviours`.
- `Docs/Entities/Entities/ExtensionsInstalling.md` — `Install(IEntityInstaller)` с цепочкой, `Install(IEnumerable<...>)`, `InstallFromScene` (по умолчанию `includeInactive = true`).
- `Docs/Entities/Entities/ExtensionsRetrieval.md` — `TryGetEntity` и `FindEntityInParent` для `GameObject`, `Component`, `Collision`, `Collision2D`. Это то, что нужно коду коллизий.

### `MonoEntity`

- `Docs/Entities/Entities/MonoEntity.md` — Quick Start по компоненту, тип, разбивка на модули, Notes. Ссылка из атрибута `[HelpURL]` ведёт сюда.
- `Docs/Entities/Entities/MonoEntityCore.md` — `InstanceID`, `Name` (= имя `GameObject`), `OnStateChanged`.
- `Docs/Entities/Entities/MonoEntityTags.md`, `MonoEntityValues.md`, `MonoEntityBehaviours.md` — повторы соответствующих `Entity*`-документов, но с добавленным разделом Inspector Settings (`initialTagCapacity` и соседи).
- `Docs/Entities/Entities/MonoEntityLifecycle.md` — фазы и их связь с Unity-колбэками, раздел Inspector Settings (`useUnityLifecycle`, `disposeValues`).
- `Docs/Entities/Entities/MonoEntityInstalling.md` — `Installed`, `Install`/`Uninstall`, `WhenInstall`/`WhenUninstall`, protected-хуки `OnInstall`/`OnUninstall`, статические `InstallAll`, Inspector Settings (`installOnAwake`, `uninstallOnDestroy`, списки инсталлеров). **Описывает установку дочерних сущностей и поле `children`, которых в этой версии нет.**
- `Docs/Entities/Entities/MonoEntityCreation.md` — два примера (через `CreateArgs` и через префаб) плюс полный список статических `Create*`.
- `Docs/Entities/Entities/MonoEntityCreateArgs.md` — таблица полей `CreateArgs`. Имена показаны в PascalCase, в исходнике они camelCase; поля `children` нет.
- `Docs/Entities/Entities/MonoEntityDestruction.md` — пример и обе статические `Destroy`. Короткий, но важный документ: он объясняет, почему нельзя звать `Object.Destroy` напрямую.
- `Docs/Entities/Entities/MonoEntityCasting.md` — четыре примера (`Cast`, `Cast<E>`, `TryCast`, `TryCast<E>`) и их сигнатуры. Читать вместе с документами про прокси.
- `Docs/Entities/Entities/MonoEntityGizmos.md` — пример гизмо-поведения и Inspector Settings (`onlySelectedGizmos`, `onlyEditModeGizmos`).
- `Docs/Entities/Entities/MonoEntityDebug.md` — отладочные панели для `MonoEntity`, аналог `EntityDebug.md`; тоже только с Odin.
- `Docs/Entities/Entities/MonoEntityEditor.md` — **самый практически полезный из редакторных документов.** Описывает параметр `autoCompile` с предупреждением и, главное, пошагово расписывает контекстные команды `Compile` и `Reset`. `Compile` — это способ получить измеренные `initial*Capacity` вместо угаданных.

### Прокси и синглтон

- `Docs/Entities/Entities/MonoEntityProxies.md` — раздел Theory о том, зачем нужны прокси при дочерних коллайдерах, настройка в Unity и пример взаимодействия. Файл содержит два слипшихся варианта текста (`# MonoEntityProxies` и ниже `# MonoEntity Proxies`), так что не удивляйтесь дублям.
- `Docs/Entities/Entities/MonoEntityProxy.md` — необобщённый `MonoEntityProxy : MonoEntityProxy<MonoEntity>`: пример, Inspector Settings, свойства.
- ``Docs/Entities/Entities/MonoEntityProxy`1.md`` — обобщённый `MonoEntityProxy<E>`: пример типизированного прокси, Inspector Settings, свойства. `Source` описан как get-only — в исходнике у него есть сеттер. Про struct-перегрузки `AddValue<T>`/`SetValue<T>` документ не говорит ничего.
- `Docs/Entities/Entities/MonoEntitySingleton.md` — два примера (глобальный синглтон и «Resolving Singletons»), Inspector Settings, статические свойства и методы, Notes. **Второй пример и описанные `isGlobal` / `Resolve(...)` не соответствуют исходнику** — их там нет.

### KeyStore

- `Docs/Entities/KeyStore/EntityKeyStore.md` — пример использования и методы `NameToId`/`IdToName`/`SetAlgorithm`/`Reset`.
- `Docs/Entities/KeyStore/TagKey.md` — `TagKey` и `TagKey<E>`: тип, оба конструктора (`string` и `int`), свойство `Id`, методы. Важно: `Id` описан в разделе Properties, но в исходнике это публичное readonly **поле**.
- `Docs/Entities/KeyStore/ValueKey.md` — то же для `ValueKey<T>` и `ValueKey<E, T>`.
- `Docs/Entities/KeyStore/IEntityKeyAlgorithm.md` — контракт алгоритма с длинным разделом Notes и примером собственной реализации. Здесь же сформулировано требование детерминизма, которое штатная реализация нарушает.
- `Docs/Entities/KeyStore/SequentialEntityKeyAlgorithm.md` — последовательный алгоритм, его конструктор с `nextId` и Notes. Читать обязательно перед тем, как решать вопрос о стабильности id.

### Документация генераторов (вне `Docs/`)

- `Assets/Plugins/Atomic/SourceGenerators/Project~/Docs/Setup.md` — пошаговая установка DLL: метка `RoslynAnalyzer`, снятые платформы, Apply, перезапуск. Содержит ошибку «all four assemblies» — DLL пять.
- `Assets/Plugins/Atomic/SourceGenerators/Project~/Docs/EntityAPIGenerator.md` — что именно генерируется из каждого типа ключа, таблица свойств `[EntityExtensionsAPI]`, раздел Unsafe mode с примерами `GetValueUnsafe<int>` / `ref int RefHealth(...)` (не компилируются для value-типов) и описание `ATOMIC_OUTPUT_SOURCEGEN_FILES`.
- `Assets/Plugins/Atomic/SourceGenerators/Project~/Docs/EntityAPIAnalyzer.md` — таблица диагностик: `EAPI0001` (нет инициализатора) и `EAPI0002` (`new()`/`default` оставляет `Id = 0`), оба уровня Error, оба с code fix, с примерами плохого кода.
- `Assets/Plugins/Atomic/SourceGenerators/Project~/Docs/EntityDomainGenerator.md` — `[EntityDomainBehaviours]`: какие типизированные интерфейсы поведений генерируются. Здесь прямо сказано, что работу делает `EntityDomainGenerator.dll` — та самая DLL, которую забывает `Setup.md`.

### Исходники, к которым стоит идти первыми

- `Assets/Plugins/Atomic/Entities/Scripts/Entities/Interface/IEntity{,_Tags,_Values,_Behaviours}.cs` — настоящий контракт.
- `.../Scripts/Entities/Core/Entity{,_Tags,_Values,_Behaviours,_Lifecycle}.cs` — чистая реализация; `Entity_Values.cs` и `Entity_Tags.cs` стоит прочитать целиком, если вы собираетесь полагаться на производительность.
- `.../Scripts/Entities/Unity/MonoEntity_UnityLifecycle.cs` — 80 строк, которые определяют всё поведение компонента в сцене.
- `.../Scripts/Lifecycle/Sources/*.cs` и `.../Scripts/Lifecycle/Extensions.cs` — базовые интерфейсы и семейство `When*`.
- `.../Scripts/Registry/EntityRegistry.cs` — выдача и переиспользование `InstanceID`.
- `.../Scripts/Utils/InternalUtils.cs` — `PrimeTable`, `CeilToPrime`, `Add`/`AddIfAbsent`/`Remove`; объясняет реальную семантику ёмкостей и стоимость `Disable()`.
- `.../Tests/EditMode/Entity/EntityTests_Values.cs` и `EntityTests_Lifecycle.cs` — исполняемая спецификация предусловий и порядка фаз.
- `.../Tests/Performance/Entity/EntityPerformance_{Tags,Values,Behaviours}.cs` — место, где воспроизводятся опубликованные замеры.
