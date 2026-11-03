# 13. Entities: миры, фильтры, коллекции

Это слой хранения, жизненного цикла и запросов над `IEntity`. Единственный примитив хранения здесь — `EntityCollection<E>`: гибрид хеш-таблицы и массива порядка вставки, ключом в котором служит `IEntity.InstanceID`. Из него вырастает всё остальное: `EntityWorld<E>` добавляет к коллекции enable/disable и три фазы тика, `MonoEntityWorld<E>` оборачивает мир в `MonoBehaviour` и отдаёт прокачку одному скрытому `TickableManager`, `EntityFilter<E>` и `DerivedEntityFilter<T,E>` дают живые наблюдаемые срезы по предикату, а `EntityRegistry` раздаёт и переиспользует `InstanceID` и даёт поиск сущности по id за O(1). Именно этот слой закрывает роль «контекста сцены»: то, что было контекстом-богом, становится обычной сущностью с привязанным к ней миром (`Extensions.BindTo`).

Все подписи типов и все примеры в этой главе сверены с исходниками в `Assets/Plugins/Atomic/Entities/Scripts`. Там, где документация расходится с кодом, в тексте указано и то, и другое — в проекте вы будете компилироваться против кода, а не против `.md`.

## Модель

Четыре уровня, снизу вверх.

**1. Идентичность — это `InstanceID`, а не ссылка.** `EntityCollection<E>` берёт `int hashCode = item.InstanceID` и в `Contains`/`Add`/`Remove` сравнивает **только** `slot.hashCode == hashCode`; само значение слота никогда не сравнивается. Это согласовано с самими сущностями: и `Entity`, и `MonoEntity` реализуют `GetHashCode() => _instanceId` и `Equals(IEntity other) => other != null && _instanceId == other.InstanceID`, а `IsValid => _instanceId > 0`.

Id выдаёт `EntityRegistry`. Регистрация происходит сама:
* `Entity` — в конструкторе (`EntityRegistry.Instance.Register(this)` в `Entity.cs`);
* `MonoEntity` — в `Awake()` и в `ISerializationCallbackReceiver.OnAfterDeserialize()` (`MonoEntity_UnityLifecycle.cs`), повторный вызов идемпотентен.

Отсюда следствие: сущность с `InstanceID == 0` (не зарегистрированная, или уже разрегистрированная — `Unregister` ставит `InstanceID = 0`) в коллекции неотличима от любой другой такой же. Вторую вы не добавите (`Add` вернёт `false`), а `Contains(другая_незарегистрированная)` вернёт `true`.

**2. `EntityCollection<E>` — хранилище.** Внутри: `Slot[] _slots` (значение, хеш, `next` для цепочки коллизий, `orderIndex`), `int[] _buckets` (головы цепочек) и отдельный `int[] _order` — индексы слотов в порядке вставки. Перебор идёт по `_order`, а не по цепочке `next`: это линейный проход по массиву с одной индирекцией, а не блуждание по указателям. Документация называет это «linked list semantics» — это не так, `next` обслуживает только коллизии хеша.

`Remove` переставляет последний элемент `_order` в освободившуюся позицию, то есть порядок сохраняется для всех, кроме переставленного.

**3. `EntityWorld<E>` — коллекция плюс жизненный цикл.** Реальные базовые интерфейсы — `IEnableSource` и `ITickSource` (документация в `Worlds/IEntityWorld`1.md` пишет `IEnableLifecycle`/`ITickLifecycle` и ссылается на несуществующие файлы; таких типов в дереве нет).

Ключевая механика: **жизненный цикл следует за членством**. `EntityWorld<E>` переопределяет хуки коллекции:

```csharp
protected override void OnAdd(E entity)    { if (_enabled) entity.Enable();  }
protected override void OnRemove(E entity) { if (_enabled) entity.Disable(); }
```

Добавили сущность во включённый мир — она включилась. Убрали — выключилась. Добавили в выключенный мир — она останется выключенной до `world.Enable()`. Хук вызывается внутри `Add`/`Remove` **до** событий `OnAdded`/`OnRemoved`, поэтому подписчики всегда видят уже согласованное состояние.

Тики — ручные индексные циклы прямо по `private protected` хранилищу, без энумератора:

```csharp
for (int i = 0; i < _count; i++)
    _slots[_order[i]].value.Tick(deltaTime);
```

`_count` и `_order` читаются живыми на каждой итерации, версионирования нет — мутировать мир из тика нельзя.

**4. `MonoEntityWorld<E>` — адаптер к Unity.** Он **композирует** `EntityWorld<E>` (приватное поле `_world`), а не наследует его, и переадресует каждый член. Он не реализует `Update`: в `Start`/`OnEnable` он регистрируется в `internal sealed class TickableManager`, который и прокачивает все миры из одного `Update`/`FixedUpdate`/`LateUpdate`.

**5. `EntityFilter<E>` — живой материализованный срез.** Фильтр хранит собственную `EntityCollection<E> state` с совпадениями. В конструкторе он проходит весь источник и вызывает `Observe` на каждой сущности; дальше синхронность держат `IEntityTrigger<E>`: фильтр вызывает `SetAction(this.Synchronize)` один раз на каждый триггер, а `Track`/`Untrack` вызываются на каждую сущность при входе/выходе из источника.

Два факта, противоречащих `Filters/Manual.md`:
* «Lazy Evaluation — Filter only evaluates when accessed» — **нет**. Вычисление жадное: и в конструкторе, и в момент мутации.
* «Re-evaluation Cost — Full re-filter when triggers fire» — **нет**. `Synchronize(E entity)` пересчитывает предикат ровно для одной сущности.

Что действительно важно: `Observe` ставит триггеры **до** проверки предиката, то есть подписки получают *все* сущности источника, а не только совпавшие. Стоимость триггера масштабируется размером источника, а не размером среза.

**6. `EntityRegistry` — глобальный авторитет по id.** Синглтон с приватным конструктором; `Register`/`Unregister`/`Clear` — `internal`, вы их не вызываете. Id начинаются с 1 (`++_maxId`) и переиспользуются из стека `int[] _recycledIds`, поэтому любой поиск сразу отсекает `id <= 0`.

## API

### Интерфейсы коллекций

```csharp
public interface IReadOnlyEntityCollection<E> : IReadOnlyCollection<E> where E : IEntity
{
    event Action OnStateChanged;
    event Action<E> OnAdded;
    event Action<E> OnRemoved;

    E this[int index] { get; }
    bool TryGetAt(int index, out E entity);

    bool Contains(E entity);
    void CopyTo(ICollection<E> results);
    void CopyTo(E[] array, int arrayIndex);
}
```

Читающая сторона всего модуля: её реализуют коллекции, миры, фильтры и реестр. Принимайте этот тип в API, которому нужно только наблюдать и перебирать. Индексатор и `TryGetAt` — настоящие члены интерфейса, но в `.md` их нет вообще (документация перечисляет `Count`/`IsReadOnly`/`Contains`/`CopyTo`/`GetEnumerator`). `IsReadOnly`, наоборот, задокументирован здесь, но в исходнике интерфейса отсутствует.

```csharp
public interface IReadOnlyEntityCollection : IReadOnlyEntityCollection<IEntity> { }
```

Псевдоним без своих членов — чтобы объявить параметр без аргумента типа.

```csharp
public interface IEntityCollection<E> : IReadOnlyEntityCollection<E>, ICollection<E>, IDisposable
    where E : IEntity
{
    new int Count { get; }
    new bool Contains(E entity);
    new bool Add(E entity);
    new void CopyTo(E[] array, int arrayIndex);
}
```

Мутабельная наблюдаемая коллекция; именно против этого типа написаны все расширения. `Add` переобъявлен через `new` и возвращает `bool`: `false` — «уже есть», исключения не будет. Из-за `new`-членов вызов через `ICollection<E>` попадёт в void-реализацию — держите entity-специфичный интерфейс.

```csharp
public interface IEntityCollection : IEntityCollection<IEntity>, IReadOnlyEntityCollection { }
```

Неgeneric-псевдоним; реализует оба интерфейса, чтобы его можно было передать и туда, и туда.

### `EntityCollection<E>`

```csharp
public class EntityCollection<E> : IEntityCollection<E> where E : IEntity
{
    private protected const int UNDEFINED_INDEX = -1;
    private protected const int DEFAULT_CAPACITY = 3;
    private protected static readonly ArrayPool<E> s_arrayPool = ArrayPool<E>.Shared;

    private protected struct Slot
    {
        public E value;
        public int hashCode;
        public int next;        // цепочка коллизий хеша
        public int orderIndex;
    }

    public event Action OnStateChanged;
    public event Action<E> OnAdded;
    public event Action<E> OnRemoved;

    public int Count => _count;
    public bool IsReadOnly => false;

    private protected int _capacity;
    private protected int _count;
    private int _primeIndex;
    private protected Slot[] _slots;
    private protected int[] _buckets;
    private protected int _freeList;
    private protected int _lastIndex;
    private protected int[] _order;

    public EntityCollection() : this(DEFAULT_CAPACITY) { }
    public EntityCollection(int capacity);
    public EntityCollection(IEnumerable<E> elements) : this(elements.Count());
    public EntityCollection(params E[] entities) : this(entities.Length);
    public EntityCollection(IReadOnlyCollection<E> elements) : this(elements.Count);

    public E this[int index] { get; }
    public bool TryGetAt(int index, out E entity);

    public bool Contains(E item);
    public bool Add(E item);
    void ICollection<E>.Add(E item);
    protected virtual void OnAdd(E entity) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Remove(E item);
    protected virtual void OnRemove(E entity) { }

    public void Clear();
    public virtual void Dispose();

    public void CopyTo(E[] array, int arrayIndex);
    public void CopyTo(ICollection<E> results);

    public Enumerator GetEnumerator() => new(this);
    IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);
    IEnumerator<E> IEnumerable<E>.GetEnumerator() => new Enumerator(this);

    public struct Enumerator : IEnumerator<E>
    {
        public E Current => _current;
        object IEnumerator.Current => _current;
        public Enumerator(EntityCollection<E> collection);
        public bool MoveNext();
        public void Reset();
        public void Dispose();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected void NotifyAboutStateChanged() => this.OnStateChanged?.Invoke();
}
```

Поведенческие детали, которые придётся знать наизусть:
* `EntityCollection(int capacity)` бросает `ArgumentOutOfRangeException` при отрицательном значении и делает `_capacity = CeilToPrime(capacity, out _primeIndex)` — запрошенная ёмкость это **нижняя граница**, фактическая округляется вверх до простого из `InternalUtils.PrimeTable` (`2, 3, 7, 17, 29, 53, 97, 193, …`).
* `Add(null)` бросает `ArgumentNullException`; `Remove(null)` и `Contains(null)` возвращают `false`.
* Порядок вызовов в `Add`: `OnAdd(item)` → `OnAdded?.Invoke(item)` → `OnStateChanged?.Invoke()`. Так же в `Remove`.
* `CopyTo(E[], int)` **проверяет** размер назначения (`array.Length - arrayIndex < _count` → `ArgumentException`) и копирует по `_order`, то есть в порядке вставки.
* `Dispose()` — `virtual`, вызывает `Clear()` (а тот поднимает `OnRemoved` на каждую сущность) и затем зануляет все три события.
* `_primeIndex` — единственное поле уровня `private`; все остальные `private protected`, то есть доступны подклассу в той же сборке.

```csharp
public class EntityCollection : EntityCollection<IEntity>, IEntityCollection
{
    public EntityCollection();
    public EntityCollection(int capacity);
    public EntityCollection(params IEntity[] entities);
    public EntityCollection(IReadOnlyCollection<IEntity> elements);
    public EntityCollection(IEnumerable<IEntity> elements);
}
```

Только конструкторы. Struct-энумератор наследуется, так что `foreach` остаётся безаллокационным.

### Расширения коллекций (`Collections/Extensions.cs`)

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddRange<E>(this IEntityCollection<E> it, params E[] entities) where E : IEntity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddRange<E>(this IEntityCollection<E> it, IEnumerable<E> entities) where E : IEntity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static E CreateEntity<E>(
        this IEntityCollection<E> it,
        E prefab,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null
    ) where E : MonoEntity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DestroyEntity<E>(this IEntityCollection<E> it, E entity, float delay = 0)
        where E : MonoEntity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InitEntities<E>(this IEntityCollection<E> it) where E : IEntity;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DisposeEntities<E>(this IEntityCollection<E> it) where E : IEntity;

    public static void CollectAllEntities<E>(this IEntityCollection<E> collection, bool includeInactive = false)
        where E : IEntity;
}
```

Unity-члены закрыты `#if UNITY_5_3_OR_NEWER`. `DestroyEntity` уничтожает объект только `if (it.Remove(entity))` — чужую сущность эта коллекция не убьёт. `CollectAllEntities` всегда опрашивает `GameObject.FindObjectsByType<MonoEntity>(...)` независимо от `E`, затем делает `if (entity is E target) { entity.Install(); collection.Add(target); }` — то есть не-`MonoBehaviour` сущности он найти не может в принципе, и `Install()` вызывается **до** `Add`. Документация `Collections/Extensions.md` опускает ограничение `where E : IEntity`.

### Миры: интерфейсы и жизненный цикл

```csharp
public interface IEntityWorld<E> : IEntityCollection<E>, IEnableSource, ITickSource where E : IEntity
{
    string Name { get; set; }
}

public interface IEntityWorld : IEntityWorld<IEntity> { }
```

Полные контракты баз (их `.md` не приводит, но они нужны любому ручному дублёру мира в тестах):

```csharp
public interface IEnableSource
{
    event Action OnEnabled;
    event Action OnDisabled;
    bool Enabled { get; }
    void Enable();
    void Disable();
}

public interface ITickSource
{
    event Action<float> OnTicked;
    event Action<float> OnFixedTicked;
    event Action<float> OnLateTicked;
    void Tick(float deltaTime);
    void FixedTick(float deltaTime);
    void LateTick(float deltaTime);
}

public interface IInitSource : IDisposable
{
    event Action OnInitialized;
    event Action OnDisposed;
    bool Initialized { get; }
    void Init();
}
```

Обратите внимание: `IEntityWorld<E>` **не** наследует `IInitSource`. Поэтому `BindTo` вешает на `WhenInit`/`WhenDispose` не методы мира, а расширения `InitEntities`/`DisposeEntities`. `IEntity` же наследует все три: `public partial interface IEntity : IInitSource, IEnableSource, ITickSource`.

### `EntityWorld<E>`

```csharp
public class EntityWorld<E> : EntityCollection<E>, IEntityWorld<E> where E : IEntity
{
    public event Action OnEnabled;
    public event Action OnDisabled;
    public event Action<float> OnTicked;
    public event Action<float> OnFixedTicked;
    public event Action<float> OnLateTicked;

    public string Name { get => _name; set => _name = value; }
    public bool Enabled => _enabled;

    public EntityWorld();
    public EntityWorld(params E[] entities);
    public EntityWorld(string name = null, params E[] entities);
    public EntityWorld(string name, IEnumerable<E> entities);

    public void Enable();
    public void Disable();
    public void Tick(float deltaTime);
    public void FixedTick(float deltaTime);
    public void LateTick(float deltaTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnAdd(E entity);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnRemove(E entity);

    public override void Dispose();
}

public class EntityWorld : EntityWorld<IEntity>, IEntityWorld
{
    public EntityWorld();
    public EntityWorld(params IEntity[] entities);
    public EntityWorld(string name = null, params IEntity[] entities);
    public EntityWorld(string name, IEnumerable<IEntity> entities);
}
```

`Enable`/`Disable` идемпотентны (ранний выход по `_enabled`), поднимают `NotifyAboutStateChanged()` **до** `OnEnabled`/`OnDisabled`, и проходят по всем сущностям. Все три тика делают ранний выход при `!_enabled`. `Dispose()` сначала `Disable()` (если включён), затем `base.Dispose()` (Clear + зануление трёх событий коллекции), затем зануляет пять событий жизненного цикла. `world.Dispose()` **не** вызывает `Dispose()` у сущностей — для этого есть `DisposeEntities()`.

### Привязка мира к владеющей сущности

```csharp
public partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BindTo<E>(this IEntityWorld<E> world, IEntity source) where E : IEntity
    {
        source.WhenInit(world.InitEntities);
        source.WhenEnable(world.Enable);
        source.WhenTick(world.Tick);
        source.WhenFixedTick(world.FixedTick);
        source.WhenLateTick(world.LateTick);
        source.WhenDisable(world.Disable);
        source.WhenDispose(world.DisposeEntities);
        source.WhenDispose(world.Dispose);
    }
}
```

Лежит в `World/Extensions.cs` и в документации Worlds отсутствует. Это главный приём миграции с контекста-бога: «контекст» становится обычной `IEntity`, а его подмир — одной строкой привязывается к её жизненному циклу. Мир при этом остаётся тестируемым обычным NUnit без сцены и без Unity-лупа.

Каждый `When*` возвращает дескриптор подписки, и `BindTo` отбрасывает все восемь:

```csharp
public static InitSubscription       WhenInit(this IInitSource source, Action action);
public static DisposeSubscription    WhenDispose(this IInitSource source, Action action);
public static EnableSubscription     WhenEnable(this IEnableSource source, Action action);
public static DisableSubscription    WhenDisable(this IEnableSource source, Action action);
public static TickSubscription       WhenTick(this ITickSource source, Action<float> action);
public static FixedTickSubscription  WhenFixedTick(this ITickSource source, Action<float> action);
public static LateTickSubscription   WhenLateTick(this ITickSource source, Action<float> action);
```

Все дескрипторы — `readonly struct … : IDisposable`, например:

```csharp
public readonly struct TickSubscription : IDisposable
{
    public TickSubscription(ITickSource source, Action<float> callback);   // делает source.OnTicked += callback
    public void Dispose();                                                 // делает source.OnTicked -= callback
}
```

Следствие: мир, привязанный через `BindTo`, отвязать уже нельзя. Если подмир должен отцепляться раньше смерти владельца — вызывайте `When*` сами и храните дескрипторы.

### Unity-миры

```csharp
public abstract partial class MonoEntityWorld<E> : MonoBehaviour, IEntityWorld<E> where E : IEntity
{
    private readonly EntityWorld<E> _world = new();

    public event Action OnStateChanged { add; remove; }
    public event Action<E> OnAdded     { add; remove; }
    public event Action<E> OnRemoved   { add; remove; }
    public event Action OnEnabled      { add; remove; }
    public event Action OnDisabled     { add; remove; }
    public event Action<float> OnTicked      { add; remove; }
    public event Action<float> OnFixedTicked { add; remove; }
    public event Action<float> OnLateTicked  { add; remove; }

    public string Name { get => this.name; set => this.name = value; }
    public E this[int index] => _world[index];
    public bool TryGetAt(int index, out E entity);

    public bool IsReadOnly => _world.IsReadOnly;
    int IEntityCollection<E>.Count    => _world.Count;
    int ICollection<E>.Count          => _world.Count;
    int IReadOnlyCollection<E>.Count  => _world.Count;

    public bool Contains(E entity);
    public bool Add(E entity);
    public bool Remove(E entity);
    public void Clear();
    public void CopyTo(ICollection<E> results);
    void ICollection<E>.Add(E item);
    void ICollection<E>.CopyTo(E[] array, int arrayIndex);
    void IEntityCollection<E>.CopyTo(E[] array, int arrayIndex);
    void IReadOnlyEntityCollection<E>.CopyTo(E[] array, int arrayIndex);
    public IEnumerator<E> GetEnumerator();

    public bool Enabled => _world.Enabled;
    public void Enable();
    public void Disable();
    public void Tick(float deltaTime);
    public void FixedTick(float deltaTime);
    public void LateTick(float deltaTime);
    public void Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Create<T>(string name = null, bool scanEntities = true, bool useUnityLifecycle = true)
        where T : MonoEntityWorld<E>;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Destroy(MonoEntityWorld<E> world, float t = 0);

    private protected virtual void Awake();
    private protected virtual void OnEnable();
    private protected virtual void Start();
    private protected virtual void OnDisable();
    private protected virtual void OnDestroy();
}
```

Реальное ограничение — `where E : IEntity`, а не `where E : MonoEntity`, как пишут `MonoEntityWorld`1.md` и `Collections/Extensions.md`. Именно поэтому в сценовый мир можно положить и plain-C# сущности, и `MonoEntity` одновременно — это и делает возможной пошаговую миграцию проекта по мирам, а не по сценам.

Три вещи, которые ломают наивный код:
1. **`Count` реализован только явно** — все три перегрузки. На конкретном типе `world.Count` не компилируется (CS1061), нужен каст к интерфейсу. Документация при этом перечисляет `Count` как свойство.
2. **`CopyTo(E[], int)` тоже только явно**, все три раза. Публичного массивного `CopyTo` на конкретном типе нет, в отличие от `EntityCollection<E>`.
3. **`GetEnumerator()` возвращает `IEnumerator<E>`** — struct-энумератор упаковывается. Это единственный контейнер модуля, где `foreach` аллоцирует.

Unity-коллбеки — `private protected virtual`, так что переопределять их можно только внутри сборки Atomic; в своём коде расширяйте мир не через них.

Поля инспектора (`MonoEntityWorld_Inspector.cs`): `useUnityLifecycle` (по умолчанию `true`), `dontDestroyOnLoad`, `collectOnAwake` (`true`), `includeInactiveOnCollect` (`true`). Таблица в `MonoEntityWorld.md` всё ещё называет их `registerOnAwake`/`includeInactiveOnRegister`.

Порядок коллбеков: `Awake` — `CollectAllEntities`, если включён `collectOnAwake`, и `DontDestroyOnLoad`, если включён флаг. `Start` — `Enable()` + регистрация в `TickableManager` + `isStarted = true`. `OnEnable` — то же, но только если `isStarted`. `OnDisable` — снятие с регистрации + `Disable()`. `OnDestroy` — `Dispose()`. Под `if (useUnityLifecycle)` стоят только `Start`/`OnEnable`/`OnDisable`/`OnDestroy`; тело `Awake` не гейтится этим флагом вообще, поэтому сбор сущностей и `DontDestroyOnLoad` срабатывают и при `useUnityLifecycle == false`.

```csharp
[AddComponentMenu("Atomic/Entities/Entity World")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
[HelpURL("…/Docs/Entities/Worlds/MonoEntityWorld.md")]
public class MonoEntityWorld : MonoEntityWorld<IEntity>
{
    public static MonoEntityWorld Create(
        string name = null,
        bool scanEntities = true,
        bool useUnityLifecycle = true
    ) => Create<MonoEntityWorld>(name, scanEntities, useUnityLifecycle);
}
```

Неabstract, вешается на `GameObject` напрямую. База — `MonoEntityWorld<IEntity>`, а не `<MonoEntity>`. `[DefaultExecutionOrder(-1000)]` гарантирует, что его `Awake`/`Start` идут раньше обычных скриптов — на этом держится надёжность `collectOnAwake`. Это единственный тип с неgeneric-обёрткой `Create`, которую можно вызвать без явного аргумента типа.

```csharp
public abstract class MonoEntityWorldSingleton<E> : MonoEntityWorld<E> where E : IEntity
{
    [Tooltip("If enabled, this object will persist across scene loads.")]
    [SerializeField] private bool dontDestroyOnLoad;

    public static MonoEntityWorldSingleton<E> Instance { get; }            // бросает Exception, если нет
    public static bool TryGetInstance(out MonoEntityWorldSingleton<E> instance);

    private protected override void Awake();
    private protected override void OnDestroy();
}

public abstract class MonoEntityWorldSingleton : MonoEntityWorld
{
    [Tooltip("If enabled, this object will persist across scene loads.")]
    [SerializeField] private bool dontDestroyOnLoad;

    public static MonoEntityWorldSingleton Instance { get; }
    public static bool TryGetInstance(out MonoEntityWorldSingleton instance);

    private protected override void Awake();
    private protected override void OnDestroy();
}
```

Это единственный в модуле штатный способ получить глобально доступный мир сцены. Детали:
* Ленивый поиск — `FindAnyObjectByType<…>(FindObjectsInactive.Exclude)`. Неактивный синглтон не виден, и `Instance` бросит исключение.
* Дубликат в `Awake` логирует ошибку, делает `Destroy(gameObject)` и `return` — **до** `base.Awake()`, поэтому дубликат никогда не собирает сущности, а живой оригинал сохраняет статическое поле.
* Статика — на закрытый generic-тип. При этом неgeneric `MonoEntityWorldSingleton` наследует `MonoEntityWorld`, а не `MonoEntityWorldSingleton<IEntity>`: это два независимых реестра, и `MonoEntityWorldSingleton<IEntity>.Instance` не найдёт неgeneric-синглтон.
* Оба класса объявляют собственное `[SerializeField] private bool dontDestroyOnLoad`, затеняющее одноимённое поле `MonoEntityWorld<E>`. Соблюдаются оба — в инспекторе будет два независимых чекбокса на одно понятие.

```csharp
[AddComponentMenu("")]
[DisallowMultipleComponent]
internal sealed class TickableManager : MonoBehaviour
{
    private static readonly IEqualityComparer<ITickSource> s_comparer = EqualityComparer<ITickSource>.Default;
    internal ITickSource[] _sources;

    internal static TickableManager Instance { get; }
    internal void Register(ITickSource tickSource);
    internal void Unregister(ITickSource tickSource);

    private void Update();
    private void FixedUpdate();
    private void LateUpdate();
}
```

`internal` — свои источники вы сюда не зарегистрируете. Прокачивает `Tick(Time.deltaTime)`, `FixedTick(Time.fixedDeltaTime)`, `LateTick(Time.deltaTime)`. Создаёт скрытый `DontDestroyOnLoad` объект с `HideFlags.HideAndDontSave`. `Register`/`Unregister` — no-op при `!EditorApplication.isPlaying`; `[InitializeOnEnterPlayMode]` сбрасывает флаг `_spawned` для режима без domain reload.

### Фильтры

```csharp
public class EntityFilter<E> : IReadOnlyEntityCollection<E>, IDisposable where E : IEntity
{
    public event Action OnStateChanged;
    public event Action<E> OnAdded;
    public event Action<E> OnRemoved;

    public int Count => this.state.Count;

    private readonly EntityCollection<E> state;
    private readonly IReadOnlyEntityCollection<E> source;
    private readonly Predicate<E> predicate;
    private readonly IEntityTrigger<E>[] triggers;

    public EntityFilter(
        IReadOnlyEntityCollection<E> source,
        Predicate<E> predicate,
        params IEntityTrigger<E>[] triggers
    );

    public void Dispose();
    public void CopyTo(ICollection<E> results);
    public void CopyTo(E[] array, int arrayIndex);
    public E this[int index] => state[index];
    public bool TryGetAt(int index, out E entity);
    public bool Contains(E entity);
    public EntityCollection<E>.Enumerator GetEnumerator() => this.state.GetEnumerator();

    internal void Synchronize(E entity);

    public static Builder StartBuild() => new();

    public struct Builder
    {
        public Builder WithSource(IReadOnlyEntityCollection<E> source);
        public Builder WithPredicate(Predicate<E> predicate);
        public Builder WithTrigger(IEntityTrigger<E> trigger);
        public Builder WithTriggers(params IEntityTrigger<E>[] triggers);
        public EntityFilter<E> Build();
    }
}

public class EntityFilter : EntityFilter<IEntity>, IReadOnlyEntityCollection
{
    public EntityFilter(
        IReadOnlyEntityCollection<IEntity> source,
        Predicate<IEntity> predicate,
        params IEntityTrigger<IEntity>[] triggers
    );
}
```

`source` и `predicate` проверяются на `null` и бросают `ArgumentNullException`; массив `triggers` — **нет**, литеральный `null` даст NRE на `triggers.Length` в конструкторе. Массив сохраняется по ссылке: мутируя переданный массив, вы мутируете набор триггеров фильтра.

`GetEnumerator()` отдаёт struct-энумератор внутреннего состояния — `foreach` по фильтру безаллокационен, но вы ходите по живому хранилищу, поэтому мутация во время обхода небезопасна.

`Dispose()` делает ровно три вещи: `Unobserve` на каждой сущности, которая сейчас в источнике, и отцепляет два своих обработчика от `source.OnAdded`/`source.OnRemoved`. Он **не** чистит `state` явно и **не** зануляет собственные `OnAdded`/`OnRemoved`/`OnStateChanged`. Но каждый `Unobserve` делает `state.Remove(entity)`, поэтому для источника, который всё ещё держит свои сущности, состояние по факту становится пустым — `EntityFilter_Dispose_Tests.Dispose_Should_Unobserve_AllEntities` утверждает `Assert.AreEqual(0, filter.Count)` после `Dispose`. Остаточный случай один, и он же — утечка: сущности, удалённые из источника **до** `Dispose`, навсегда сохраняют подписки триггеров.

`StartBuild()`/`Builder` в документации отсутствуют полностью.

```csharp
public class DerivedEntityFilter<T, E> : IReadOnlyEntityCollection<T>, IDisposable
    where E : IEntity
    where T : E
{
    public event Action OnStateChanged;
    public event Action<T> OnAdded;
    public event Action<T> OnRemoved;

    public int Count => this.state.Count;

    public DerivedEntityFilter(
        IReadOnlyEntityCollection<E> source,
        Predicate<T> predicate,
        params IEntityTrigger<T>[] triggers
    );

    public void Dispose();
    public void CopyTo(ICollection<T> results);
    public void CopyTo(T[] array, int arrayIndex);
    public T this[int index] => state[index];
    public bool TryGetAt(int index, out T entity);
    public bool Contains(T entity);
    public EntityCollection<T>.Enumerator GetEnumerator();

    internal void Synchronize(T entity);
}

public class DerivedEntityFilter<T> : DerivedEntityFilter<T, IEntity> where T : IEntity
{
    public DerivedEntityFilter(
        IReadOnlyEntityCollection<IEntity> source,
        Predicate<T> predicate,
        params IEntityTrigger<T>[] triggers
    );
}
```

Сужение по типу. Ограничение — именно `where T : E`: `T` должен наследовать тип элемента источника, а не просто `IEntity`. `Observe`/`Unobserve` начинаются с `if (baseEntity is not T entity) return;` — одна проверка типа на мутацию источника, и предикат остаётся строго типизированным.

Баг в `Dispose()`:

```csharp
public void Dispose()
{
    foreach (T entity in this.source)       // source — IReadOnlyEntityCollection<E>
        this.Unobserve(entity);
    …
}
```

`foreach` с переменной типа `T` над `IEnumerable<E>` вставляет каст. Если в источнике ещё есть не-`T` сущности — `InvalidCastException`. А это ровно тот случай, для которого тип и создан. `Unobserve(E)` сам по себе безопасен — у него есть `is not T`-защита; проблема исключительно в типе переменной цикла.

### Триггеры

```csharp
public interface IEntityTrigger<E> where E : IEntity
{
    void SetAction(Action<E> action);
    void Track(E entity);
    void Untrack(E entity);
}

public interface IEntityTrigger : IEntityTrigger<IEntity> { }
```

Главная точка расширения системы фильтров. Триггер держит ровно одно поле `Action<E> _action`, которое `SetAction` перезаписывает — один экземпляр триггера нельзя делить между двумя фильтрами.

```csharp
public class TagEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public TagEntityTrigger(bool added = true, bool deleted = true);
    public void SetAction(Action<E> action);
    public void Track(E entity)
    {
        if (_added) entity.OnTagAdded += this.OnTagAdded;
        if (_deleted) entity.OnTagDeleted += this.OnTagDeleted;
    }
    public void Untrack(E entity);
}

public class TagEntityTrigger : TagEntityTrigger<IEntity>
{
    public TagEntityTrigger(bool added = true, bool deleted = true);
}
```

Пересчёт при любом изменении тегов. Теги — `int`-ключи. Триггер неразборчив: *любое* изменение тега перезапускает предикат.

```csharp
public class ValueEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public ValueEntityTrigger(bool added = true, bool deleted = true, bool changed = true);
    public void SetAction(Action<E> action);
    public void Track(E entity)
    {
        if (_added) entity.OnValueAdded += this.OnValueAdded;
        if (_deleted) entity.OnValueDeleted += this.OnValueDeleted;
        if (_changed) entity.OnValueChanged += this.OnValueChanged;
    }
    public void Untrack(E entity);
}

public class ValueEntityTrigger : ValueEntityTrigger<IEntity>
{
    public ValueEntityTrigger(bool added = true, bool deleted = true, bool changed = true);
}
```

Конструктор принимает **только три bool** — никакого ключа. Пример `new ValueEntityTrigger<IUnitEntity>("Health")` из `DerivedEntityFilter.md` не существует; триггер, привязанный к одному ключу, пишется как `SubscriptionEntityTrigger`.

```csharp
public class BehaviourEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public BehaviourEntityTrigger(bool added = true, bool removed = true);
    public void SetAction(Action<E> action);
    public void Track(E entity)
    {
        if (_added) entity.OnBehaviourAdded += this.OnBehaviourAdded;
        if (_removed) entity.OnBehaviourDeleted += this.OnBehaviourRemoved;
    }
    public void Untrack(E entity);
}

public class BehaviourEntityTrigger : BehaviourEntityTrigger<IEntity>
{
    public BehaviourEntityTrigger(bool added = true, bool removed = true);
}
```

Параметр называется `removed`, хотя событие — `OnBehaviourDeleted`. Используйте для предикатов вида «есть такое поведение».

```csharp
public class StateChangedEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public void SetAction(Action<E> action);
    public void Track(E entity)   => entity.OnStateChanged += this.OnStateChanged;
    public void Untrack(E entity) => entity.OnStateChanged -= this.OnStateChanged;
}

public class StateChangedEntityTrigger : StateChangedEntityTrigger<IEntity> { }
```

Ловит всё: теги, значения, поведения. Нет ни конструктора, ни флагов — самый тупой и самый дорогой триггер. Для прототипа и маленьких наборов, не для горячих путей.

```csharp
public abstract class SubscriptionEntityTrigger<E, S> : IEntityTrigger<E>
    where E : IEntity
    where S : IDisposable
{
    private Action<E> _action;
    private readonly Dictionary<IEntity, S> _subscriptions = new();

    public void SetAction(Action<E> action);

    public void Track(E entity)
    {
        if (!_subscriptions.ContainsKey(entity))
        {
            S subscription = this.Track(entity, _action);
            _subscriptions.Add(entity, subscription);
        }
    }

    public void Untrack(E entity)
    {
        if (_subscriptions.Remove(entity, out S subscription))
            subscription.Dispose();
    }

    protected abstract S Track(E entity, Action<E> callback);
}

public abstract class SubscriptionEntityTrigger<S> : SubscriptionEntityTrigger<IEntity, S>
    where S : IDisposable { }
```

Рекомендованная база для точного триггера: вы подписываетесь ровно на одно реактивное значение и возвращаете дескриптор, база владеет словарём и гарантирует `Dispose()` на `Untrack`. `where S : IDisposable` (а не `S : class`) — осознанный выбор: struct-подписка вроде `Subscription<int>` лежит в словаре значением, без упаковки, а `Dispose()` вызывается через ограничение. Двойной `Track` идемпотентен. Словарь ключуется по `IEntity` со ссылочным равенством по умолчанию. `Track(E)` захватывает `_action` в момент вызова — для фильтра это гарантировано, для ручного использования `SetAction` должен быть вызван первым.

```csharp
public class InlineEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public InlineEntityTrigger(Action<E, Action<E>> track, Action<E, Action<E>> untrack);
    public void SetAction(Action<E> action);
    public void Track(E entity)   => _track.Invoke(entity, _action);
    public void Untrack(E entity) => _untrack.Invoke(entity, _action);
}

public class InlineEntityTrigger : InlineEntityTrigger<IEntity>
{
    public InlineEntityTrigger(Action<IEntity, Action<IEntity>> track, Action<IEntity, Action<IEntity>> untrack);
}
```

Триггер из двух лямбд. Опасен по конструкции: отписка требует от вас руками предъявить тот же экземпляр делегата, что был при подписке, — и оба примера в документации с этим не справляются (см. «Идиомы» и «Грабли»).

### `EntityRegistry`

```csharp
public sealed class EntityRegistry : IReadOnlyEntityCollection<IEntity>
{
    public static EntityRegistry Instance => _instance ??= new EntityRegistry();

    public event Action OnStateChanged;
    public event Action<IEntity> OnAdded;
    public event Action<IEntity> OnRemoved;

    public int Count => _count;

    public IEntity this[int index] { get; }
    public bool TryGetAt(int index, out IEntity entity);

    public bool Contains(IEntity entity);
    public bool Contains(int id);
    public void CopyTo(ICollection<IEntity> results);
    public void CopyTo(IEntity[] array, int arrayIndex);

    public IEntity Get(int id);                                       // KeyNotFoundException
    public bool TryGet(int id, out IEntity entity);
    public T GetUnsafe<T>(int id) where T : class, IEntity;            // Unsafe.As, без проверки типа
    public bool TryGetUnsafe<T>(int id, out T entity) where T : class, IEntity;

    public IEnumerator<IEntity> GetEnumerator() => new Enumerator(this);
    public struct Enumerator : IEnumerator<IEntity> { … }

    internal void Register(IEntity entity);
    internal void Unregister(IEntity entity);
    internal void Clear();

#if UNITY_EDITOR
    [InitializeOnEnterPlayMode]
#endif
    internal static void ResetAll();
}
```

Четыре уровня поиска с разной ценой и разной семантикой:
* `Get(int)` — бросает `KeyNotFoundException`, проверяет `slot.id == id && slot.entity != null`.
* `TryGet(int, out IEntity)` — проверяет **только** `slot.id == id` и затем присваивает `entity = slot.entity`. Может вернуть `true` с `null` в `out` там, где `GetUnsafe`/`TryGetUnsafe` вернут `false`.
* `GetUnsafe<T>(int)` — `Unsafe.As<IEntity, T>(ref slot.entity)` (под Unity это `UnsafeUtility.As`): ноль проверок типа, ноль `castclass`, ноль копирования. Неверный `T` — не `InvalidCastException`, а неопределённое поведение. Возвращает `null` при `_count == 0 || id <= 0`, но бросает `KeyNotFoundException`, если дошёл до конца цепочки.
* `TryGetUnsafe<T>(int, out T)` — то же без исключений.

`CopyTo(ICollection<IEntity>)`, индексатор и `Enumerator` ходят по `_order`, то есть в порядке регистрации. `CopyTo(IEntity[], int)` — **нет**: он идёт по сырым слотам `for (i < _lastIndex)`, пропуская `slot.id == 0`, и проверяет только `arrayIndex`, но не вместимость массива.

`Clear()` ставит `entity.InstanceID = UNDEFINED_INDEX` (-1), а `Unregister` — `0`. Разные сентинелы для одного состояния.

### Атрибуты кодогенерации

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityExtensionsAPIAttribute : Attribute
{
    public bool Unsafe { get; set; }
    public bool AggressiveInlining { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityDomainBehavioursAttribute : Attribute
{
    public Type EntityType { get; }
    public EntityDomainBehavioursAttribute(Type entityType);
}

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class UnsafeAttribute : Attribute { }
```

В `Scripts/Codegen/` ровно эти три типа, yaml-дескрипторов в дереве нет. Два уровня opt-in в unchecked-аксессоры: свойство `Unsafe` на классе и `[Unsafe]` на отдельном поле. `AggressiveInlining` по умолчанию `true`, то есть сгенерированные аксессоры инлайнятся.

## Идиомы

### 1. Перебирать конкретный тип, не интерфейс

```csharp
// ❌ упаковка: GetEnumerator() через интерфейс отдаёт IEnumerator<IEntity>
IEntityCollection collection = ...;
foreach (IEntity entity in collection) { … }

IEntityWorld world = ...;
foreach (IEntity entity in world) { … }
```

```csharp
// ✅ без упаковки: public Enumerator GetEnumerator() — структура по значению
EntityCollection collection = ...;
foreach (IEntity entity in collection) { … }

EntityWorld world = ...;
foreach (IEntity entity in world) { … }

EntityFilter filter = ...;
foreach (IEntity entity in filter) { … }
```

Почему именно так: `EntityCollection<E>` объявляет и `public Enumerator GetEnumerator()`, и явные `IEnumerator<E> IEnumerable<E>.GetEnumerator() => new Enumerator(this)`. `foreach` привязывается к публичному struct-возвращающему методу только тогда, когда **статический** тип выражения — конкретный класс. То есть тип поля, в котором вы храните контейнер, решает, аллоцирует ли каждый обход. `EntityWorld<E>` наследует этот метод, `EntityFilter<E>`/`DerivedEntityFilter<T,E>` переадресуют его как `EntityCollection<T>.Enumerator GetEnumerator()`.

Исключения, где правило не спасает: `MonoEntityWorld<E>` и `EntityRegistry` (см. «Грабли»).

### 2. Предикат — именованный метод

```csharp
private enum _TeamType { BLUE = 0, RED = 1 }

private bool IsBlueWarrior(IEntity e) =>
    e.HasTag("Warrior") &&
    e.TryGetValue("Team", out _TeamType teamType) &&
    teamType == _TeamType.BLUE;

// …

var alex = new Entity("Alex");
alex.AddTag("Warrior");
alex.AddValue("Team", _TeamType.BLUE);

var george = new Entity("George");
george.AddTag("Warrior");
george.AddValue("Team", _TeamType.RED);

var ivan = new Entity("Ivan");
ivan.AddTag("Warrior");                      // ключа "Team" нет вообще

var mike = new Entity("Mike");
mike.AddTag("Warrior");
mike.AddValue("Team", _TeamType.BLUE);

var world = new EntityWorld("Entity World", alex, george, ivan, mike);
var filter = new EntityFilter(world, this.IsBlueWarrior);

Assert.AreEqual(2, filter.Count);
Assert.IsTrue(filter.Contains(alex));
```

Это дословно собственный интеграционный тест фреймворка (`EntityFilter_IntergrationTests.cs`) — единственный полностью компилирующийся эталон во всей области. Три вещи, которые он показывает правильно:
* Предикат — именованный метод: его можно назвать, протестировать отдельно и переиспользовать.
* Предикат **не бросает** на сущностях без ключа: `TryGetValue`, а не `GetValue`. Конструктор фильтра прогоняет предикат по *каждой* сущности источника, поэтому `GetValue<int>("Health") > 0` упадёт `KeyNotFoundException` на первой сущности без «Health» (внутри это хелпер `Entity.ValueNotFoundException(int)`, возвращающий именно `KeyNotFoundException`). В тесте такой сущностью был бы `ivan`.
* Идиоматичная конструкция мира: `new EntityWorld("Entity World", a, b, c, d)` — имя плюс `params`, без последовательности `Add`.

### 3. Фильтр из мира, с самым узким триггером

```csharp
var entity1 = new Entity();
entity1.AddTag("Player");

var entity3 = new Entity();
entity3.AddTag("Enemy");

var entityCollection = new EntityCollection();
entityCollection.Add(entity1);
entityCollection.Add(entity3);

var playerFilter = new EntityFilter(
    entityCollection,
    e => e.HasTag("Player"),
    new TagEntityTrigger()
);

Assert.IsTrue(playerFilter.Contains(entity1));
Assert.IsFalse(playerFilter.Contains(entity3));

entity3.AddTag("Player");
Assert.IsTrue(playerFilter.Contains(entity3));
```

Предикат и триггер согласованы: тегу — `TagEntityTrigger`, и фильтр пересчитывается только на мутациях тегов, а не на любом изменении состояния. Экземпляр триггера создан на месте и принадлежит этому фильтру — это обязательно.

```csharp
// ❌ оба антипаттерна в двух строках
var trigger = new StateChangedEntityTrigger();                              // пересчёт на ЛЮБУЮ мутацию
var players = new EntityFilter(coll, e => e.HasTag("Player"), trigger);
var enemies = new EntityFilter(coll, e => e.HasTag("Enemy"),  trigger);     // второй SetAction украл
                                                                            // синхронизацию у players
```

Второй `SetAction` затирает единственное поле `_action`, и `players` молча перестаёт обновляться — без исключения, без лога.

### 4. Сужать флаги триггера

```csharp
// Только добавление тега может изменить предикат — снимаем вторую подписку
var additionTrigger = new TagEntityTrigger(added: true, deleted: false);

// Значение только меняется, не появляется и не исчезает
var changeOnly = new ValueEntityTrigger(added: false, deleted: false, changed: true);
```

`Track` подписывается условно (`if (_added) entity.OnTagAdded += …`), поэтому каждый `false` — это на один делегат меньше в списке вызова **каждой** отслеживаемой сущности, а не просто проверка внутри колбэка. Это самый дешёвый доступный рычаг настройки в сценах с большим числом фильтров: экономия линейна по числу сущностей.

### 5. Цепочка фильтров вместо составного предиката

```csharp
var playerUnitsFilter = new EntityFilter(
    sourceCollection,
    entity => entity.HasTag("Player")
);

var playerBuildingsFilter = new EntityFilter(
    playerUnitsFilter,                       // другой фильтр как источник
    entity => entity.HasTag("Building")
);
```

`EntityFilter<E>` реализует `IReadOnlyEntityCollection<E>` и поднимает `OnAdded`/`OnRemoved`, поэтому он сам — законный источник. Предикат второго фильтра прогоняется только по совпадениям первого, и оба остаются живыми. (В `Filters/Manual.md` в этом примере лишняя точка с запятой внутри списка аргументов — он не компилируется как напечатан; идиома в форме, а не в пунктуации.)

```csharp
// ❌ один толстый предикат
var filter = new EntityFilter(source,
    e => e.HasTag("Player") && e.HasTag("Building") && e.GetValue<int>("Health") > 0);
// пересчитывается целиком на каждое срабатывание триггера, не переиспользуется,
// и падает на сущностях без "Health"
```

### 6. Сужение по типу через `DerivedEntityFilter`, а не каст в цикле

```csharp
// IGameEntity : IEntity — тип проекта
IEntityWorld world = ...;

var aliveGameEntities = new DerivedEntityFilter<IGameEntity>(
    world,
    e => e.TryGetValue("IsAlive", out bool alive) && alive,
    new ValueEntityTrigger<IGameEntity>()
);

foreach (IGameEntity entity in aliveGameEntities)
    Debug.Log($"Alive game entity {entity.Name}");
```

Проверка `is not T` происходит один раз на мутацию источника внутри `Observe`/`Unobserve`, а не на каждую сущность каждый кадр. И предикат, и переменная цикла строго типизированы, кастов в пользовательском коде нет. Хранилище — `EntityCollection<T>`, поэтому обход идёт по `T` со struct-энумератором.

Версия из `DerivedEntityFilter.md` — `gameEntity.GetValue<bool>("IsAlive").Value` — не компилируется: расширение `public static T GetValue<T>(this IEntity entity, string key)` возвращает `T`, а у `bool` нет члена `.Value` (CS1061). Либо `GetValue<bool>("IsAlive")`, либо `GetValue<IReactiveValue<bool>>("IsAlive").Value`.

```csharp
// ❌ проверка типа и предикат на каждую сущность каждый кадр
foreach (IEntity e in world)
    if (e is IGameEntity g && g.TryGetValue("IsAlive", out bool alive) && alive)
        Debug.Log(g.Name);
```

### 7. Двухпараметрическая форма с явным базовым типом

```csharp
// IUnitEntity : IGameEntity : IEntity
IEntityWorld<IGameEntity> world = ...;

var aliveUnitFilter = new DerivedEntityFilter<IUnitEntity, IGameEntity>(
    world,
    unit => unit.TryGetValue("Health", out int health) && health > 0,
    new ValueEntityTrigger<IUnitEntity>(added: true, deleted: true, changed: true)
);

aliveUnitFilter.OnRemoved += unit => Debug.Log($"Unit died: {unit.Name}");
```

Форма `T : E` позволяет сузить `IGameEntity` до `IUnitEntity`, оставив и предикат, и триггер в терминах `IUnitEntity`. Подписка на `OnRemoved` — идиоматичный хук «сущность вышла из набора»: без опроса и без диффов.

Пример из документации здесь сломан дважды: `new ValueEntityTrigger<IUnitEntity>("Health")` — такого конструктора нет, а подписка ниже обращается к `aliveFilter`, хотя переменная объявлена как `aliveUnitFilter`.

### 8. `SubscriptionEntityTrigger` как канонический кастомный триггер

```csharp
using Atomic.Elements;

public sealed class HealthTrigger : SubscriptionEntityTrigger<IUnitEntity, Subscription<int>>
{
    private const string KEY = "Health";

    protected override Subscription<int> Track(IUnitEntity entity, Action<IUnitEntity> callback)
    {
        IReactiveValue<int> health = entity.GetValue<IReactiveValue<int>>(KEY);
        return health.Subscribe(_ => callback.Invoke(entity));   // Subscription<int> : IDisposable
    }
}
```

Правильная форма по трём причинам. Первая: вы пишете один метод, а база владеет `Dictionary<IEntity, S>` и гарантирует `Dispose()` на `Untrack` — подписка не может утечь. Вторая: объявив `S` конкретным struct-типом `Subscription<int>`, а не `IDisposable`, вы храните его в словаре без упаковки и вызываете `Dispose()` через ограничение; документация формулирует это прямо: «use struct-based subscriptions, avoid boxing when working with value-type subscriptions, ensure efficient memory management in high-performance systems». Третья, и на практике главная: триггер смотрит ровно на **одно** значение вместо всех value-событий сущности, и значение типизировано — путь уведомления не проходит через `object` и ничего не упаковывает. `sealed` на конкретном триггере разрешает девиртуализацию.

Честная оговорка: лямбда `_ => callback.Invoke(entity)` захватывает `entity` и `callback`, значит на каждый `Track` аллоцируются display-class и делегат, плюс запись в словарь. Выигрыш здесь — узость и гарантированная отписка, а не нулевые аллокации.

```csharp
// ❌ рукописный аналог из IEntityTrigger.md и EntityTriggers.md
public class ActiveStateTrigger : IEntityTrigger
{
    private readonly Dictionary<IEntity, Subscription<bool>> _subscriptions = new();
    private Action<IEntity> _onChanged;

    public void SetAction(Action<IEntity> action) => _onChanged = action;

    public void Track(IEntity entity)
    {
        Subscription<bool> subscription = entity
            .GetValue<IReactiveValue<bool>>("IsActive")
            .Subscribe(_ => _onChanged?.Invoke(entity));
        _subscriptions.Add(entity, subscription);          // двойной Track -> ArgumentException
    }

    public void Untrack(IEntity entity)
    {
        if (_subscriptions.Remove(entity, out Subscription<int> subscription))   // CS1503: словарь
            subscription.Dispose();                                              // держит Subscription<bool>
    }
}
```

Этот код приведён в документации дважды (`IEntityTrigger.md` и `EntityTriggers.md`) и не компилируется. Он переписывает, причём хуже, то, что `SubscriptionEntityTrigger<E,S>` уже даёт.

```csharp
// ❌ InlineEntityTrigger в том виде, в каком он в документации
var inlineTrigger = new InlineEntityTrigger(
    track:   (e, callback) => e.OnTagAdded += _ => callback(e),
    untrack: (e, callback) => e.OnTagAdded -= _ => callback(e)
);
```

Здесь два независимых дефекта. Арность: `OnTagAdded` — это `Action<IEntity, int>`, лямбда `_ => …` принимает один параметр, компилятор отвергает обе строки. И даже после исправления на `(_, _) => callback(e)` в `untrack` создаётся **новый** экземпляр делегата, поэтому `-=` не снимает ничего, и каждая отслеженная сущность утекает обработчиком. Для чего угодно сложнее выброшенного теста используйте `SubscriptionEntityTrigger<E,S>`, который возвращает вам дескриптор, вместо того чтобы требовать совпадения делегатов.

### 9. Привязать жизненный цикл мира к владеющей сущности

```csharp
IEntity root = new Entity("Battle");
var units = new EntityWorld<IEntity>("Units");

units.BindTo(root);

root.Init();        // -> units.InitEntities()
root.Enable();      // -> units.Enable()
root.Tick(dt);      // -> units.Tick(dt)
root.Disable();     // -> units.Disable()
root.Dispose();     // -> units.DisposeEntities(), затем units.Dispose()
```

Корень композиции — обычная сущность, а не `MonoBehaviour` и не статический контекст. Мир остаётся тестируемым в обычном NUnit: ни сцены, ни Unity-лупа, ни синглтона.

Если подмир должен отцепляться раньше владельца — `BindTo` не подойдёт, он отбрасывает все восемь дескрипторов. Держите их сами:

```csharp
public sealed class SubWorldBinding : IDisposable
{
    private readonly EnableSubscription _enable;
    private readonly DisableSubscription _disable;
    private readonly TickSubscription _tick;

    public SubWorldBinding(IEntityWorld<IEntity> world, IEntity source)
    {
        _enable  = source.WhenEnable(world.Enable);
        _disable = source.WhenDisable(world.Disable);
        _tick    = source.WhenTick(world.Tick);
    }

    public void Dispose()
    {
        _tick.Dispose();
        _disable.Dispose();
        _enable.Dispose();
    }
}
```

### 10. Типизированный сценовый мир через пустой подкласс

```csharp
public class GameEntity : MonoEntity
{
}

public class GameEntityWorld : MonoEntityWorld<GameEntity>
{
}

// T не выводится ни из одного параметра — задаём явно
GameEntityWorld world = GameEntityWorld.Create<GameEntityWorld>(
    "GameEntityWorld",
    scanEntities: true
);

GameEntityWorld.Destroy(world);
```

`MonoEntityWorld<E>` объявлен `abstract`, поэтому пустой подкласс — предусмотренный способ закрыть generic и получить компонент, видимый в инспекторе. `Create<T>` строит `GameObject` **неактивным**, выставляет `Name`, `collectOnAwake`, `useUnityLifecycle` и только потом делает `go.SetActive(true)` — к моменту `Awake` сериализованные настройки уже верны. Именованные аргументы держат подпись из трёх `bool` читаемой.

```csharp
// ❌ так написано в Worlds/Manual.md и MonoEntityWorld`1.md
GameEntityWorld world = GameEntityWorld.Create("BattleWorld", scanEntities: true);
// CS0411: тип T не может быть выведен — ни один параметр Create<T> не имеет типа T
```

Неgeneric-обёртку `Create` объявляет только `MonoEntityWorld`, и именно эту форму используют play-mode тесты:

```csharp
var entityWorld = MonoEntityWorld.Create("Test", scanEntities: false);
var entity = MonoEntity.Create("Test Entity");
entityWorld.Add(entity);
```

Там же — обход явной реализации `Count` и массивного `CopyTo`:

```csharp
GameEntityWorld world = ...;

// int n = world.Count;                                              // CS1061
int n = ((IReadOnlyEntityCollection<GameEntity>) world).Count;        // ✅

var buffer = new GameEntity[n];
((IReadOnlyEntityCollection<GameEntity>) world).CopyTo(buffer, 0);    // ✅
```

### 11. Синглтон-мир: `Instance` против `TryGetInstance`

```csharp
public sealed class GameWorld : MonoEntityWorldSingleton
{
}

// Жёсткое требование: бросает Exception, если синглтона в сцене нет
MonoEntityWorldSingleton world = MonoEntityWorldSingleton.Instance;
world.Enable();

// Необязательная зависимость
if (MonoEntityWorldSingleton.TryGetInstance(out MonoEntityWorldSingleton optional))
    optional.Enable();
```

Два явных контракта вместо одного nullable: `Instance` бросает (используйте там, где мир — обязательное условие), `TryGetInstance` возвращает `false` (опциональные системы, аддитивные сцены, тесты). Оба кешируют в одно и то же статическое поле, так что скан `FindAnyObjectByType` не повторяется.

Заметьте: `MonoEntityWorldSingleton.Instance` типизирован абстрактной базой — именно поэтому пример документации с объявленным `GameWorld` и присваиванием в `MonoEntityWorldSingleton` работает.

### 12. Поиск по id через реестр

```csharp
EntityRegistry registry = EntityRegistry.Instance;

IEntity byId = registry.Get(id);                     // KeyNotFoundException, если нет

if (registry.TryGet(id, out IEntity found))          // внимание: true возможен с found == null
    Debug.Log($"Found entity: {found}");

// Горячий путь: сеть, загрузка сейва — тип id является инвариантом ваших данных
if (registry.TryGetUnsafe(id, out PlayerEntity player))
    player.Enable();
```

Четыре уровня с явной ценой. `*Unsafe<T>`-пара полностью пропускает проверку типа через `Unsafe.As<IEntity, T>` — применяйте только там, где тип по id гарантирован вашей собственной схемой данных. Строка `registry.Clear()` из `Registry/EntityRegistry.md` из игрового кода не компилируется: `Clear` — `internal`.

### 13. Пакетные операции через расширения

```csharp
// EnemyEntity : MonoEntity
IEntityCollection<EnemyEntity> collection = new EntityCollection<EnemyEntity>();

collection.AddRange(enemyA, enemyB);
collection.AddRange(moreEnemies);              // IEnumerable<EnemyEntity>

collection.InitEntities();
// …
collection.DisposeEntities();

// Unity: время жизни GameObject и членство в коллекции идут вместе
EnemyEntity spawned = collection.CreateEntity(prefab, Vector3.zero, Quaternion.identity);
collection.DestroyEntity(spawned, 1.0f);       // уничтожит через секунду, и только если Remove удался
```

`CreateEntity`/`DestroyEntity` держат эти две вещи синхронно: `DestroyEntity` уничтожает объект только `if (it.Remove(entity))`, поэтому сущность, которой в этой коллекции не было, она не тронет. Все перечисленные расширения помечены `[MethodImpl(MethodImplOptions.AggressiveInlining)]`.

```csharp
// ❌ GameObject уничтожен, коллекция держит мёртвую ссылку
MonoEntity.Destroy(entity);
// дальше Contains/Tick ходят по разрушенному объекту
```

### 14. Задавать ёмкость, когда количество известно

```csharp
// ❌ рост от 3: 3 -> 7 -> 17 -> 29 … с полным рехешем на каждом шаге
var collection = new EntityCollection<GameEntity>();
foreach (GameEntity e in spawned)
    collection.Add(e);

// ✅ ёмкость округляется вверх до простого
var collection = new EntityCollection<GameEntity>(spawned.Count);
collection.AddRange(spawned);

// ✅ лучше: источник с известным Count
var collection = new EntityCollection<GameEntity>(spawned);   // List<E> -> перегрузка IReadOnlyCollection<E>
```

`DEFAULT_CAPACITY = 3`, а рост берёт следующее простое из `PrimeTable`, пересоздаёт `_slots`/`_buckets`, ресайзит `_order` и рехеширует каждую живую запись. Из четырёх конструкторов предпочитайте `IReadOnlyCollection<E>` или `params E[]`: перегрузка `IEnumerable<E>` вызывает LINQ-`Count()` и проходит последовательность второй раз. Для `List<E>` разрешение перегрузок само выберет `IReadOnlyCollection<E>` — он более конкретен, чем `IEnumerable<E>`.

### 15. Хуки `OnAdd`/`OnRemove`, а не подписка на события

```csharp
public sealed class CombatWorld : EntityWorld
{
    private readonly EntityCollection<IEntity> _targetable = new();

    public IReadOnlyEntityCollection<IEntity> Targetable => _targetable;

    protected override void OnAdd(IEntity entity)
    {
        base.OnAdd(entity);                 // обязательно: здесь сущность включается
        if (entity.HasTag("Targetable"))
            _targetable.Add(entity);
    }

    protected override void OnRemove(IEntity entity)
    {
        base.OnRemove(entity);              // обязательно: здесь сущность выключается
        _targetable.Remove(entity);
    }
}
```

Хук выполняется внутри `Add`/`Remove` **до** рассылки `OnAdded`/`OnRemoved`, поэтому побочные эффекты от членства синхронны и упорядочены, а подписчики видят уже согласованное состояние. `EntityWorld<E>` использует этот же хук, чтобы жизненный цикл следовал за членством, — забыв `base.OnAdd(entity)`, вы молча выключите жизненный цикл сущностей для всего мира. Документация `EntityWorld`1.md` здесь противоречит себе: в описании `OnAdd(E)` стоит «Enables entity when an entity is **added** to the collection», а строкой ниже — «Default behavior: Does nothing».

```csharp
// ❌ выполняется ПОСЛЕ рассылки события; другие подписчики могли уже увидеть
//    выключенную сущность, и порядок зависит от порядка подписки
world.OnAdded += e => e.Enable();
```

### 16. Наружу — read-only, внутри — конкретный тип

```csharp
public sealed class UnitRoster
{
    private readonly EntityCollection<IUnitEntity> _units = new(32);   // конкретный тип для foreach

    public IReadOnlyEntityCollection<IUnitEntity> Units => _units;     // наружу — только чтение

    public void Tick(float deltaTime)
    {
        foreach (IUnitEntity unit in _units)      // struct-энумератор, ноль аллокаций
            unit.Tick(deltaTime);
    }
}
```

`IReadOnlyEntityCollection<E>` несёт все три события изменения, так что наблюдатель получает полную реактивность без возможности `Add`/`Remove`/`Clear`/`Dispose`. Комбинируйте с правилом перебора: публикуйте read-only-интерфейс на границе API, но в поле, по которому вы сами делаете `foreach`, держите конкретный тип.

### 17. Мутация набора — через буфер, а не во время обхода

```csharp
private readonly List<IEntity> _buffer = new();

private void RemoveDead(EntityWorld world)
{
    _buffer.Clear();
    world.CopyTo(_buffer);                       // CopyTo(ICollection<IEntity>)

    for (int i = 0, count = _buffer.Count; i < count; i++)
        if (_buffer[i].HasTag("Dead"))
            world.Remove(_buffer[i]);
}
```

```csharp
// ❌ энумератор читает _count и _order живыми, а Remove переставляет
//    последний элемент _order в освободившуюся позицию — сущность будет пропущена.
//    Версионирования нет, исключения вы не получите.
foreach (IEntity entity in world)
    if (entity.HasTag("Dead"))
        world.Remove(entity);
```

Тот же запрет распространяется на мутацию мира из `Tick` и на мутацию фильтра во время `foreach` по нему: фильтр отдаёт энумератор своего живого хранилища.

### 18. `Builder`, если набор триггеров собирается условно

```csharp
EntityFilter<GameEntity> filter = EntityFilter<GameEntity>.StartBuild()
    .WithSource(world)
    .WithPredicate(IsAliveUnit)
    .WithTrigger(new TagEntityTrigger<GameEntity>(added: true, deleted: true))
    .WithTrigger(new ValueEntityTrigger<GameEntity>(added: false, deleted: false, changed: true))
    .Build();
```

`Build()` проверяет `_source` и `_predicate` и бросает `InvalidOperationException` с внятным текстом, если чего-то нет, — в отличие от прямого конструктора, который на `null`-массиве триггеров даёт NRE. Цена — `List<IEntityTrigger<E>>` плюс `ToArray()`, то есть две лишние аллокации по сравнению с конструктором.

```csharp
// ❌ Builder — мутабельная структура, возвращаемая ПО ЗНАЧЕНИЮ
var builder = EntityFilter<GameEntity>.StartBuild();
builder.WithSource(world);          // результат отброшен
builder.WithPredicate(IsAliveUnit); // результат отброшен
var filter = builder.Build();       // InvalidOperationException: "Source must be provided."
```

Цепочка работает только как одно выражение.

## Производительность

Отмечено, что именно является измерением/утверждением документации, что — прямым чтением исходника, а что — выводом.

**Замеры (документация).** `Collections/Manual.md`: MacBook Apple M1, 1000 элементов, медианное время в μs.

| Операция | `EntityCollection` | `HashSet` |
|---|---|---|
| Add | 18.91 | 77.53 |
| Clear | 13.24 | 1.09 |
| Contains | 6.00 | 76.96 |
| Enumerator | 7.57 | 26.44 |
| Remove | 11.50 | 64.53 |

Вывод документации: «While `HashSet` excels at bulk operations like `Clear`, `EntityCollection` offers significantly faster Add, Contains, and Remove operations on average». Соответствующий набор — `Tests/Performance/Collection/EntityCollectionPerformance.cs` (`Unity.PerformanceTesting`, `N = 1000`, `WarmupCount(5)`, `MeasurementCount(20)`), целиком под `#if UNITY_6000`.

**Почему быстрее (исходник + вывод).** Факт: хеш — это `item.InstanceID`, а сравнение совпадения — ровно `slot.hashCode == hashCode`. Ни вызова `GetHashCode()`, ни обращения к компаратору, ни сравнения значений. Вывод: преимущество над `Dictionary`/`HashSet` именно в отсутствии этих двух вызовов, а не в более умной структуре. Факт в обратную сторону: индекс бакета — `(hashCode & 0x7FFFFFFF) % _capacity`, где `_capacity` — непостоянное простое, то есть одно аппаратное целочисленное деление на каждый `Contains`/`Add`/`Remove`.

**Заявленная сложность (документация).** `Collections/Manual.md`: «Insertion & Removal — Near constant-time operations… Lookup — O(1) average complexity for `Contains`. Enumeration — Iterates in insertion order with minimal overhead».

**`ref`-доступ к слотам (исходник).** Читающие пути используют `ref readonly Slot slot = ref _slots[current]` — так устроены `Contains` и обход цепочки в начале `Add`. Пишущий путь `Remove` берёт `ref int cur = ref buckets[bucket]` и `ref Slot slot = ref slots[idx]`, а `Add` — `ref int head = ref _buckets[bucket]`. Четырёхполевая структура `Slot` не копируется, и хирургия над цепочкой идёт на месте.

**`AggressiveInlining` (исходник, точный список).** `EntityCollection<E>.Remove`, `EntityCollection<E>.NotifyAboutStateChanged`, `EntityWorld<E>.OnAdd`, `EntityWorld<E>.OnRemove`, все расширения коллекций (`AddRange` ×2, `CreateEntity`, `DestroyEntity`, `InitEntities`, `DisposeEntities`), все семь `When*`, `Extensions.BindTo`, `MonoEntityWorld<E>.Create<T>`, `MonoEntityWorld<E>.Destroy`, `EntityRegistry.Resize`, а также `InternalUtils.CeilToPrime`/`Add`/`AddIfAbsent`/`Contains`/`Expand`/`Remove`. `EntityCollection<E>.Add` атрибута **не** несёт, хотя `Remove` несёт. `EntityExtensionsAPIAttribute.AggressiveInlining` по умолчанию `true`, то есть сгенерированные аксессоры тоже инлайнятся.

Два места, где атрибут выглядит сомнительно (вывод): `EntityRegistry.Resize` — это холодный полный рехеш, инлайнить его в горячий `Register` значит раздувать вызывающего; `MonoEntityWorld<E>.Create<T>` создаёт `GameObject` и вызывает `AddComponent` — выигрыш от инлайна здесь неизмерим.

**Безаллокационный обход (документация + исходник).** `BestPractices/IteratingOverEntityCollections.md`: конкретные типы — «No boxing, zero allocations», интерфейсы — «Causes boxing and GC allocations», «Working with large collections of entities → Always prefer concrete types». В исходнике это ровно та разница между `public Enumerator GetEnumerator()` и явными `IEnumerable<E>.GetEnumerator() => new Enumerator(this)`.

Два исключения из правила, оба подтверждены исходником:
* `MonoEntityWorld<E>.GetEnumerator()` объявлен как `public IEnumerator<E> GetEnumerator() => _world.GetEnumerator();` — struct упаковывается даже на конкретном типе.
* `EntityRegistry.GetEnumerator()` возвращает `IEnumerator<IEntity>`, хотя публичная структура `Enumerator` существует. Пример `foreach (IEntity entity in registry)` из `Registry/EntityRegistry.md` аллоцирует.

Фильтры — наоборот: и `EntityFilter<E>`, и `DerivedEntityFilter<T,E>` объявляют `public EntityCollection<T>.Enumerator GetEnumerator()`, то есть обход фильтра безаллокационен. Если вам нужен обход сценового мира без аллокаций — ходите по индексу через `this[int]`/`TryGetAt` или перебирайте фильтр, построенный над этим миром.

**Фреймворк сам нарушает своё правило (исходник).** `Collections/Extensions.cs`: `InitEntities` и `DisposeEntities` делают `foreach (E entity in it)` по `IEntityCollection<E>`. `EntityFilter`1.cs` и `DerivedEntityFilter`1.cs` делают `foreach (… in this.source)` по `IReadOnlyEntityCollection<E>` и в конструкторе, и в `Dispose`. Вывод: каждый такой вызов упаковывает энумератор. Для конструктора и `Dispose` это разовая цена, для `InitEntities`/`DisposeEntities` — тоже разовая; просто не вызывайте их в цикле.

**Циклы тика (исходник).** `Enable`/`Disable`/`Tick`/`FixedTick`/`LateTick` — это `for (int i = 0; i < _count; i++) _slots[_order[i]].value.<Op>(deltaTime);`. Ни энумератора, ни интерфейсного `IEnumerable`, ноль аллокаций на кадр. Цена — две зависимые загрузки из массивов (индирекция через `_order`) на сущность на фазу, плюс интерфейсный вызов `IEntity.Tick`.

**`TickableManager` (исходник).** Источники лежат в голом `ITickSource[] _sources` с ручным `_count`, добавление/удаление — через `InternalUtils.AddIfAbsent`/`Remove` с закешированным в `static readonly` компаратором `EqualityComparer<ITickSource>.Default`. Ни `List`, ни энумератора, ни аллокаций на кадр. Много миров стоят один коллбек Unity, а не по одному на мир.

**`Clear` берёт буфер из пула (исходник).** `private protected static readonly ArrayPool<E> s_arrayPool = ArrayPool<E>.Shared;`, затем `E[] removedEntities = s_arrayPool.Rent(_count);` и `s_arrayPool.Return(removedEntities)` в `finally`. Массовая очистка поднимает `OnRemoved` на каждую сущность без аллокаций. Но в том же цикле сидит ошибка корректности — см. «Грабли».

**Аллокации в триггерах (исходник; поправка к документации).** Утверждение «handlers are cached instance method groups, so there are no per-entity closures» верно только наполовину. Факт: `Track` делает `entity.OnTagAdded += this.OnTagAdded;`. Преобразование группы методов **экземплярного** метода в делегат Roslyn не кеширует (кешируются только статические группы методов и незахватывающие лямбды). Значит каждый `Track` аллоцирует новый делегат на каждую подписку, и каждый `Untrack` аллоцирует ещё один — только чтобы сравнить и снять. Конкретно: `ValueEntityTrigger` со всеми тремя флагами — 3 делегата на `Track` и 3 на `Untrack`, на каждую сущность, входящую в источник или выходящую из него; `BehaviourEntityTrigger` — 2 + 2; `TagEntityTrigger` — 2 + 2; `StateChangedEntityTrigger` — 1 + 1. Избегается здесь только per-entity **замыкание**, но не делегат.

Прямое следствие: сужение флагов убирает и подписку, и аллокацию. И помните, что `Observe` ставит триггеры до проверки предиката — платите вы за размер источника, не за размер среза.

`SubscriptionEntityTrigger<E,S>` этой пульсации на каждую мутацию не имеет: он держит одну запись `Dictionary<IEntity, S>`, где `S` ограничен `IDisposable` (не `class`), поэтому struct-подписка лежит неупакованной, а `Dispose()` — constrained-вызов. Но и он платит аллокацией на `Track`, если ваш `Track(E, Action<E>)` возвращает подписку с захватывающей лямбдой (а он почти всегда её возвращает).

**Упаковка на пути value-событий (исходник).** `IEntity_Values.cs`: события объявлены как `event Action<IEntity, int, object> OnValueAdded/OnValueDeleted/OnValueChanged`. Значит любое изменение struct-значения (int health, enum team, Vector3) упаковывается на пути поднятия события, которое доходит до `ValueEntityTrigger`. Расход пропорционален **трафику записи значений**, а не размеру фильтра. Типизированный `IReactiveValue<T>` под `SubscriptionEntityTrigger` остаётся generic и не упаковывает. Сюда же: `AddValue(int key, object value)` упаковывает при записи, а `GetValue<T>` делает `(T) slot.value`, то есть распаковку без аллокации при чтении.

**Интерфейсный вызов в `CopyTo` (исходник).** `EntityCollection<E>.CopyTo(ICollection<E>)` и `EntityRegistry.CopyTo(ICollection<IEntity>)` делают `results.Add(...)` через интерфейс на каждый элемент — интерфейсный вызов на сущность. Массивная перегрузка — прямое индексное копирование. Если буфер переиспользуется каждый кадр, берите массив.

**Реестр (исходник).** Id переиспользуются из голого стека `int[] _recycledIds`, удваиваемого через `Array.Resize`; поиск — `id % _entityCapacity`, затем проход по цепочке со сравнением `int` и `ref readonly Slot`, чтобы не копировать слот. `GetUnsafe`/`TryGetUnsafe` используют `Unsafe.As<IEntity, T>(ref slot.entity)` — под Unity это алиас на `Unity.Collections.LowLevel.Unsafe.UnsafeUtility` — переинтерпретация поля на месте: ни `castclass`, ни копирования.

Одна находка в `Unregister` (исходник + вывод): после успешного удаления цикл **не прерывается** — он ставит `slot.next = _freeList`, а затем выполняет `last = index; index = slot.next;` и уходит гулять по списку свободных слотов, повторно сравнивая `id == 0` против освобождённых записей. Сегодня это выглядит безвредно, потому что `slot.id` уже обнулён, но это O(длина free-list) на каждое снятие с регистрации, и при двух слотах с одним id в одной цепочке `OnRemoved` поднимется дважды.

**Стоимость фильтра (документация).** `Filters/Manual.md`: «Predicate Cost — Called for each entity on evaluation. Trigger Overhead — Each trigger adds event subscriptions. Memory Efficient — Doesn't duplicate entity references. Re-evaluation Cost — Full re-filter when triggers fire», и в «Notes»: «Cache Results — Store filter results if used multiple times per frame», «Simple Predicates — Keep predicate logic fast and simple». Две поправки по исходнику: фильтр всё же держит собственную `EntityCollection<E> state` с совпадениями (ссылки, не копии сущностей), а «full re-filter» неверен — `Synchronize` пересчитывает одну сущность.

**`DerivedEntityFilter<T,E>` (исходник).** Проверка `is not T` платится один раз на мутацию источника в `Observe`/`Unobserve`, а не на кадр, и совпадения хранятся в `EntityCollection<T>`, поэтому обход идёт по `T` без кастов.

**Пулы.** Отдельного пула сущностей в этой области нет; пулится только буфер удаления в `Clear` (через `ArrayPool<E>.Shared`). Пулы сущностей живут в `Assets/Plugins/Atomic/Entities/Scripts/Pooling` и `Docs/Entities/Pooling` и к этой главе не относятся.

## Грабли

* **`Clear()` оставляет мёртвые головы в бакетах — и это может повесить процесс.** В `EntityCollection<E>.Clear()` цикл сбрасывает только `for (int i = 0; i < _lastIndex; i++) { _buckets[i] = UNDEFINED_INDEX; … }`, то есть первые `_lastIndex` бакетов, а не все `_capacity`. Любой бакет с индексом `>= _lastIndex` сохраняет устаревшую голову, и следующий `Add` в этот бакет запишет `_slots[index].next = head`, где `head` уже равен переиспользованному индексу слота. Воспроизведение: ёмкость 3, одна сущность с `InstanceID == 2` (бакет `2 % 3 == 2`, `_lastIndex == 1`) → `Clear()` (`_buckets[2]` остаётся равным 0) → `Add` сущности с `InstanceID == 5` (тот же бакет 2) → `_slots[0].next == 0`, самоссылающаяся цепочка → `Contains` сущности с `InstanceID == 11` (снова бакет 2) крутится вечно. То же касается `Dispose()`, который вызывает `Clear()`. Обход: не переиспользуйте коллекцию после `Clear` — создайте новую, либо снимайте сущности по одной через `Remove`, который цепочку расцепляет корректно. `EntityRegistry.Clear` эту ошибку не повторяет — там `Array.Fill(_buckets, UNDEFINED_INDEX)`.
* **Идентичность — `InstanceID`, а не ссылка.** Незарегистрированная сущность (`InstanceID` 0 или -1) в `EntityCollection<E>` равна любой другой незарегистрированной: вторую вы не добавите, а `Contains(другая)` вернёт `true`. `Entity` регистрируется в конструкторе, `MonoEntity` — в `Awake`/`OnAfterDeserialize`, так что на практике это бьёт по сущностям, уже снятым с регистрации (`Unregister` ставит `InstanceID = 0`), и по `MonoEntity` на префабе, чей `Awake` ещё не прошёл.
* **Один экземпляр триггера нельзя делить между фильтрами.** `SetAction` перезаписывает единственное поле `_action`; второй фильтр молча угоняет синхронизацию у первого. Создавайте триггер на фильтр.
* **`DerivedEntityFilter<T,E>.Dispose()` бросает `InvalidCastException` на гетерогенном источнике.** `foreach (T entity in this.source)` над `IReadOnlyEntityCollection<E>` вставляет каст. Это и есть типовой случай применения этого фильтра. Либо диспозьте его, когда источник однороден или уже очищен, либо отпускайте ссылку, смирившись с утечкой подписок.
* **`EntityFilter<E>.Dispose()` не зануляет собственные события** (`OnAdded`/`OnRemoved`/`OnStateChanged`), в отличие от `EntityCollection<E>.Dispose()`. Подписчики продолжают держать фильтр.
* **Сущности, удалённые из источника до `Dispose` фильтра, навсегда сохраняют подписки триггеров.** `Dispose` проходит только по текущему содержимому источника.
* **`EntityFilter`/`DerivedEntityFilter` не проверяют массив `triggers` на `null`.** Литеральный `null` даст NRE на `triggers.Length` в конструкторе. И массив хранится по ссылке — изменив его снаружи, вы изменили набор триггеров фильтра.
* **`EntityFilter<E>.Builder` — мутабельная структура, возвращаемая по значению.** Работает только как единая цепочка; присвоив её в локальную переменную и вызывая методы по отдельности, вы выбрасываете результаты.
* **Предикат фильтра прогоняется по каждой сущности источника в конструкторе.** Предикат на `GetValue<T>` упадёт `KeyNotFoundException` на первой сущности без этого ключа. Используйте `TryGetValue`/`HasValue`.
* **`foreach` по `MonoEntityWorld` аллоцирует**, несмотря на правило «prefer concrete types»: `GetEnumerator()` там возвращает `IEnumerator<E>`. Ходите по `this[int]`/`TryGetAt` или по фильтру над миром.
* **`foreach` по `EntityRegistry.Instance` тоже аллоцирует** — публичная структура `Enumerator` есть, но публичный метод упаковывает её.
* **У `MonoEntityWorld<E>` нет доступного `Count`.** Все три перегрузки реализованы явно: `world.Count` на конкретном типе — CS1061. То же с `CopyTo(E[], int)` — все три реализации явные. Нужен каст к интерфейсу. Документация при этом перечисляет оба как публичные члены.
* **`MonoEntityWorld<E>.Create<T>` не выводит `T`.** Ни один параметр не имеет тип `T` → CS0411. Пишите `GameEntityWorld.Create<GameEntityWorld>(…)`. Неgeneric-обёртка `Create` есть только у `MonoEntityWorld`.
* **`EntityRegistry.TryGet` может вернуть `true` с `null`.** Он проверяет только `slot.id == id`, тогда как `Get`, `GetUnsafe<T>` и `TryGetUnsafe<T>` проверяют ещё и `slot.entity != null`.
* **`EntityRegistry.GetUnsafe<T>` не проверяет тип вообще.** `Unsafe.As<IEntity, T>` при неверном `T` — неопределённое поведение, а не `InvalidCastException`. Плюс он возвращает `null` (а не бросает) при пустом реестре или `id <= 0`, что противоречит задокументированному контракту `KeyNotFoundException`.
* **`EntityRegistry.CopyTo(IEntity[], int)` не соблюдает порядок вставки и не проверяет вместимость.** Он идёт по сырым слотам вместо `_order` и валидирует только `arrayIndex`. Короткий массив даст `IndexOutOfRangeException` или тихо недозаполнится. Массивный `CopyTo` у `EntityCollection<E>`, наоборот, и порядок держит, и размер проверяет.
* **`EntityRegistry.Register`/`Unregister`/`Clear` — `internal`.** Строка `registry.Clear()` из документации из игрового кода не компилируется.
* **`Unregister` ставит `InstanceID = 0`, а `Clear` — `-1`.** Разные сентинелы: код, проверяющий «сущность снята», должен смотреть на `IsValid` (`_instanceId > 0`), а не на конкретное значение.
* **Переопределив `OnAdd`/`OnRemove` на подклассе мира, не забудьте `base`.** Иначе сущности перестанут включаться и выключаться вместе с членством — без исключения и без лога.
* **Добавление в выключенный мир оставляет сущность выключенной**, а удаление из включённого мира выключает её как побочный эффект `Remove`. Это по дизайну, но ломает код, который ожидал, что `Remove` только отвязывает.
* **Мутация во время обхода небезопасна и тиха.** Энумератор читает `_count`/`_order` живыми, версионирования нет, `Remove` переставляет последний элемент порядка в освободившуюся позицию — сущность будет пропущена, а исключения вы не увидите. Циклы тика мира индексируют те же массивы напрямую. Копируйте через `CopyTo` перед изменением.
* **`CollectAllEntities<E>` никогда не найдёт не-`MonoBehaviour` сущности**: он всегда опрашивает `FindObjectsByType<MonoEntity>(...)` независимо от `E`. Плюс он вызывает `entity.Install()` до `Add`. Это полный скан сцены — он отрабатывает один раз в `Awake` при включённом `collectOnAwake`, и вызывать его на кадр нельзя.
* **`MonoEntityWorldSingleton.Instance` не видит неактивный объект** (`FindObjectsInactive.Exclude`) и бросает. Дубликат в `Awake` делает `Destroy(gameObject)` и `return` **до** `base.Awake()`, поэтому дубликат никогда не собирает сущности, а живой оригинал остаётся в статическом поле.
* **Неgeneric `MonoEntityWorldSingleton` и `MonoEntityWorldSingleton<IEntity>` — два независимых синглтона.** Первый наследует `MonoEntityWorld`, а не `MonoEntityWorldSingleton<IEntity>`, поэтому generic-`Instance` его не найдёт.
* **Два чекбокса `dontDestroyOnLoad` в инспекторе.** Оба класса синглтона объявляют собственное `[SerializeField] private bool dontDestroyOnLoad`, затеняющее поле `MonoEntityWorld<E>`; соблюдаются оба.
* **`BindTo` отбрасывает все восемь дескрипторов подписки** — привязанный мир нельзя отвязать. Если нужно, вызывайте `When*` сами и храните результаты.
* **`new EntityCollection<E>(IEnumerable<E>)` проходит последовательность дважды** (LINQ-`Count()` в вызове базового конструктора плюс `AddRange`). Берите `IReadOnlyCollection<E>` или `params E[]`.
* **Запрошенная ёмкость — не точный размер.** `CeilToPrime` округляет вверх до простого из `PrimeTable`; `new EntityCollection<E>(100)` даст 193.
* **`InlineEntityTrigger` в задокументированном виде не компилируется и утекает.** Арность лямбд не совпадает с `Action<IEntity, int>`, а после исправления `-=` создаёт другой экземпляр делегата и не снимает ничего. Используйте `SubscriptionEntityTrigger<E,S>`.
* **Код в документации надо читать как форму, а не копировать.** Подтверждённые дефекты: `new ValueEntityTrigger<IUnitEntity>("Health")` (нет такого конструктора), `GetValue<bool>("IsAlive").Value` (CS1061), `GameEntityWorld.Create("…", scanEntities: true)` (CS0411), лишняя `;` внутри списка аргументов `new EntityFilter(source, entity => entity.HasTag("Player");)`, рукописный триггер со словарём `Subscription<bool>` и `out Subscription<int>`, переменная `aliveFilter` при объявленной `aliveUnitFilter`, `e.Id` вместо `e.InstanceID` в `EntityFilter.md`, неgeneric `IReactiveValue` в `IEntityTrigger`1.md`, `entity2.AddTag("Building")` вместо `entity3` в примере 1 `Filters/Manual.md` (ассерты примера при этом случайно остаются верными, но `entity3` так и не получает свой тег), `RemoveTag`/`RemoveValue` в `TagEntityTrigger.md` и `ValueEntityTrigger.md` — таких расширений нет вовсе, в исходнике только `DelTag`/`DelValue`.
* **Расхождения «документация против исходника», которые стоит знать до первого компиляционного прогона:** `MonoEntityWorld<E>` — `where E : IEntity`, а не `MonoEntity`; `IEntityWorld<E>` наследует `IEnableSource`/`ITickSource`, а не `IEnableLifecycle`/`ITickLifecycle` (их `.md`-ссылки ведут в несуществующие файлы, тогда как `Lifecycle/Sources/IEnableSource.md` и `ITickSource.md` существуют); поля инспектора — `collectOnAwake`/`includeInactiveOnCollect`, а не `registerOnAwake`/`includeInactiveOnRegister`; `IsReadOnly` задокументирован на read-only-интерфейсах, но в исходнике там не объявлен; индексатор, `TryGetAt`, `Extensions.BindTo` и `EntityFilter<E>.StartBuild`/`Builder` в документации отсутствуют вовсе; XML-комментарий в `DerivedEntityFilter.cs` ссылается на `TypedEntityFilter<PlayerEntity>`, которого больше нет.

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Коллекции — `Docs/Entities/Collections/`

* `Manual.md` — обзор, три примера использования (неgeneric, generic, расширения) и **единственная в области таблица замеров**: `EntityCollection` против `HashSet` на 1000 элементов, M1, μs. Плюс раздел «Notes» с заявленной сложностью операций. Читать ради таблицы и ради списка «Notes».
* `IReadOnlyEntityCollection`1.md` — члены read-only-интерфейса с описаниями. Внимание: перечисляет `IsReadOnly`, которого в исходнике интерфейса нет, и не упоминает индексатор и `TryGetAt`, которые есть.
* `IReadOnlyEntityCollection.md` — то же для неgeneric-псевдонима.
* `IEntityCollection`1.md` — мутабельный интерфейс; здесь объяснена семантика `new bool Add(E)` («уже есть» вместо исключения).
* `IEntityCollection.md` — неgeneric-версия того же.
* `EntityCollection`1.md` — полная справка по реализации: конструкторы, события, свойства, методы. Описание структуры называет её «hash table + linked list semantics» — по исходнику `next` обслуживает только коллизии, а порядок держит отдельный `int[] _order`.
* `EntityCollection.md` — то же для неgeneric-подкласса: пример использования плюс полная справка (конструкторы, события, свойства, методы) в терминах `IEntity`.
* `Extensions.md` — `AddRange`, `CreateEntity`, `DestroyEntity`, `InitEntities`, `DisposeEntities`, `CollectAllEntities`. У `CollectAllEntities` опущено ограничение `where E : IEntity` и не сказано, что метод всегда ищет только `MonoEntity`.

### Миры — `Docs/Entities/Worlds/`

* `Manual.md` — пять примеров: generic-мир, неgeneric-мир, Unity-мир, автосканирование сцены, синглтон-мир. Самый быстрый способ увидеть все четыре варианта рядом. Пример 4 (`GameEntityWorld.Create("BattleWorld", …)`) не компилируется — CS0411.
* `IEntityWorld`1.md` — справка по интерфейсу мира. Содержит главное расхождение области: объявляет базы как `IEnableLifecycle, ITickLifecycle` и ссылается на `../Lifecycle/Sources/IEnableLifecycle.md` / `ITickLifecycle.md`, которых не существует. В исходнике — `IEnableSource, ITickSource`.
* `IEntityWorld.md` — то же для неgeneric-версии, плюс раздел «Notes».
* `EntityWorld`1.md` — полная справка по plain-C# миру: четыре конструктора, восемь событий, `Enable`/`Disable`/три тика, `OnAdd`/`OnRemove`. Описания `OnAdd`/`OnRemove` противоречат сами себе («Enables entity when an entity is added to the collection» и тут же «Default behavior: Does nothing»); по исходнику они включают и выключают сущность.
* `EntityWorld.md` — то же для неgeneric-мира.
* `MonoEntityWorld`1.md` — сценовый мир: пошаговый пример (объявить `GameEntity`, объявить `GameEntityWorld`, навесить компонент, использовать), таблица настроек инспектора с **правильными** именами полей (`collectOnAwake`/`includeInactiveOnCollect`) и справка по API. Ограничение указано как `where E : MonoEntity` — в исходнике `where E : IEntity`.
* `MonoEntityWorld.md` — неgeneric-компонент. Здесь же единственное описание неgeneric-обёртки `Create(string, bool, bool)`. Таблица настроек устарела: `registerOnAwake`/`includeInactiveOnRegister`. API описан в терминах `MonoEntity` (`Add(MonoEntity)`, `CopyTo(ICollection<MonoEntity>)`), хотя база — `MonoEntityWorld<IEntity>`.
* `MonoEntityWorldSingleton`1.md` — типизированный синглтон-мир: как объявить, как достать, настройки инспектора (с правильными именами `collectOnAwake`/`includeInactiveOnCollect`), `Instance` против `TryGetInstance`, «Inherited APIs». Читать при миграции синглтон-контекста.
* `MonoEntityWorldSingleton.md` — то же для неgeneric-синглтона; тот же набор разделов.

### Фильтры и триггеры — `Docs/Entities/Filters/`

* `Manual.md` — обзор фильтров и пять примеров (базовый фильтр, фильтр с тег-триггером, фильтр с value-триггером, цепочка фильтров, события). Единственный источник по «Performance Considerations» и «Notes» для фильтров. Два утверждения ложны: «Lazy Evaluation» и «Re-evaluation Cost — Full re-filter when triggers fire». Пример 4 не компилируется (лишняя `;` внутри списка аргументов), в примере 1 `AddTag("Building")` применён к `entity2` вместо `entity3`.
* `EntityFilter`1.md` — справка по generic-фильтру: конструктор, три события, `Count`, методы. `StartBuild()`/`Builder` не упомянуты.
* `EntityFilter.md` — то же для неgeneric-фильтра. Источником в примере выступает `IReadOnlyEntityCollection<IEntity>`, а не мир, и сам пример сломан дважды: `GetValue<bool>("IsVisible").Value` (CS1061) и `e.Id` — у `IEntity` такого члена нет, есть `InstanceID`.
* `DerivedEntityFilter.md` — **один файл на оба типа**, `DerivedEntityFilter<T,E>` и `DerivedEntityFilter<T>`: оба объявления с ограничениями, конструкторы, `Count`, методы, и три сниппета в разделе «Example of Usage». Два из трёх содержат ошибки (в первом — `.Value` на `bool` и пропущенный `$` в `Debug.Log("Alive game entity {entity.Name}")`; во втором — несуществующий конструктор `new ValueEntityTrigger<IUnitEntity>("Health")` и обращение к `aliveFilter` при объявленной `aliveUnitFilter`), но это единственное место, где показана двухпараметрическая форма.
* `EntityTriggers.md` — обзорная страница по триггерам: тег-триггер, value-триггер, кастомный триггер, плюс «Notes» о колбэках, отслеживании и типобезопасности. Пример кастомного триггера здесь не компилируется.
* `IEntityTrigger`1.md` / `IEntityTrigger.md` — контракт из трёх методов. В `IEntityTrigger.md` воспроизведён некомпилирующийся рукописный триггер (словарь на `Subscription<bool>`, а `out Subscription<int>`); в `IEntityTrigger`1.md` типы подписки согласованы, зато соседний сниппет фильтра обращается к неgeneric `IReactiveValue`, которого нет — есть только `IReactiveValue<T>`.
* `TagEntityTriggers.md` — обзор тег-триггера (generic и неgeneric) с «Notes» про селективный мониторинг и автоматическое управление подписками. Короткая входная точка.
* `TagEntityTrigger`1.md` / `TagEntityTrigger.md` — конструктор `(bool added, bool deleted)` и методы. Неgeneric-файл даёт четыре сценария, включая «только добавление» и «только удаление», — это основной источник по сужению флагов. В сценариях перемешаны `RemoveTag` и `DelTag`; компилируется только `DelTag`.
* `ValueEntityTriggers.md` — обзор value-триггера с теми же «Notes».
* `ValueEntityTrigger`1.md` / `ValueEntityTrigger.md` — конструктор `(bool added, bool deleted, bool changed)` и пять сценариев в неgeneric-файле. Читать, чтобы увидеть все три флага; в примере «Changing-Only» целиком скопирован код и текст лога из «Deletion-Only» (переменная так и называется `deletionTrigger`, а лог пишет «Value removed»).
* `BehaviourEntityTriggers.md` / `BehaviourEntityTrigger`1.md` / `BehaviourEntityTrigger.md` — триггер на подключение/отключение поведений, конструктор `(bool added, bool removed)`. В примерах есть висячая запятая `new BehaviourEntityTrigger(added: true, removed: true,)` — невалидный C#.
* `StateChangedEntityTriggers.md` / `StateChangedEntityTrigger`1.md` / `StateChangedEntityTrigger.md` — триггер-ловец всего. Ни конструктора, ни флагов; файлы показывают использование в фильтре и вручную.
* `SubscriptionEntityTriggers.md` — **самый полезный файл по триггерам.** Два компилирующихся примера (`HealthTrigger` над `Subscription<int>` и `TeamEntityTrigger` над `Subscription<TeamType>`) и раздел «About the Generic Parameter `<S>`», где прямо сформулировано, зачем нужно `where S : IDisposable`: struct-подписки, отсутствие упаковки, управление памятью. Это эталон кастомного триггера.
* `SubscriptionEntityTrigger`1.md` / `SubscriptionEntityTrigger.md` — справка по абстрактной базе и её неgeneric-варианту.
* `InlineEntityTriggers.md` / `InlineEntityTrigger`1.md` / `InlineEntityTrigger.md` — триггер из двух лямбд. Читать, чтобы узнать тип, и **не** копировать: все примеры и не компилируются (арность), и утекают (`-=` другого делегата).

### Реестр — `Docs/Entities/Registry/`

* `EntityRegistry.md` — единственный файл: `Instance`, `Count`, три события, `Contains(int)`/`Contains(IEntity)`, оба `CopyTo`, `TryGet`, `Get`, `GetUnsafe<T>`, `TryGetUnsafe<T>`, плюс «Notes» про переиспользование id. Пример содержит `registry.Clear()` — метод `internal`, из игрового кода не компилируется; `foreach` по реестру в примере аллоцирует.

### Сопутствующее

* `Docs/BestPractices/IteratingOverEntityCollections.md` — правило «prefer concrete types» с парами ❌/✅ для `IEntityCollection`, `IEntityWorld`, `EntityCollection`, `EntityWorld`, `EntityFilter` и сводной таблицей «No boxing, zero allocations» против «Causes boxing and GC allocations». 182 строки, причём вторая половина файла дублирует первую. Обязательное чтение перед первым `foreach`. Про `MonoEntityWorld` и `EntityRegistry`, где правило не работает, здесь не сказано — эти типы в файле не упоминаются вообще.
* `Docs/Entities/Lifecycle/Sources/IEnableSource.md` и `ITickSource.md` — настоящие базы `IEntityWorld<E>` (те, на которые `Worlds/IEntityWorld`1.md` ссылается неверными именами). `IInitSource.md` рядом — то, что расширяют `WhenInit`/`WhenDispose`.
* `Docs/Entities/Lifecycle/Extensions.md` — семейство `When*`; единственное место в документации, где видно, что они возвращают дескрипторы подписок.
* `Docs/Entities/Lifecycle/Subscriptions/*.md` — по файлу на каждый дескриптор (`InitSubscription`, `DisposeSubscription`, `EnableSubscription`, `DisableSubscription`, `TickSubscription`, `FixedTickSubscription`, `LateTickSubscription`).
* `Docs/BestPractices/EntitySystem.md` — самый большой файл в `BestPractices` (≈22 КБ), разделение модели и представления на сущностях; на него ссылается `Worlds/Manual.md`.

### Исходники, которые стоит открыть вместо документации

Там, где `.md` расходится с кодом, смотреть сюда (всё относительно `Assets/Plugins/Atomic/Entities/`):

* `Scripts/Collections/EntityCollection`1.cs` — хранилище целиком: `Slot`, `_order`, `Add`/`Remove`/`Clear`, struct `Enumerator`. 463 строки вместе с XML-комментариями, читается за один присест и снимает большинство вопросов.
* `Scripts/Collections/Extensions.cs` — реальные подписи расширений и реализация `CollectAllEntities`.
* `Scripts/World/Core/EntityWorld`1.cs` — циклы тика и хуки `OnAdd`/`OnRemove`.
* `Scripts/World/Extensions.cs` — `BindTo` (весь файл — 19 строк, в документации отсутствует).
* `Scripts/World/Unity/MonoEntityWorld`1_Collection.cs` — явные реализации `Count` и `CopyTo`; открыть до того, как писать `world.Count`.
* `Scripts/World/Unity/MonoEntityWorld`1_Static.cs` — подпись `Create<T>` и порядок «неактивный `GameObject` → настройки → `SetActive(true)`».
* `Scripts/World/Unity/MonoEntityWorld`1_UnityCallbacks.cs` — точный порядок `Awake`/`OnEnable`/`Start`/`OnDisable`/`OnDestroy`.
* `Scripts/World/Unity/MonoEntityWorld_Inspector.cs` — настоящие имена сериализованных полей.
* `Scripts/Lifecycle/TickableManager.cs` — единственный пул обновления; объясняет, почему миры не реализуют `Update`.
* `Scripts/Filters/EntityFilter`1.cs` — `Observe`/`Unobserve`/`Synchronize`, порядок событий, `Builder`.
* `Scripts/Filters/DerivedEntityFilter`1.cs` — `is not T`-защита и баг `Dispose`.
* `Scripts/Triggers/*.cs` — 14 файлов (интерфейс плюс шесть типов триггеров в generic- и неgeneric-варианте), от 9 до 83 строк; быстрее прочитать их все, чем их документацию.
* `Scripts/Registry/EntityRegistry.cs` — четыре уровня поиска, переиспользование id, `Unsafe.As`.
* `Scripts/Utils/InternalUtils.cs` — `PrimeTable`, `CeilToPrime`, `AddIfAbsent`/`Remove`: отсюда берутся правила роста ёмкости.
* `Scripts/Entities/Interface/IEntity_Values.cs` — объявления `Action<IEntity, int, object>`, то есть источник упаковки на value-пути.
* `Scripts/Entities/Extensions_Values.cs` / `Extensions_Tags.cs` — строковые перегрузки `GetValue`/`TryGetValue`/`HasTag`; `GetValue<T>(this IEntity, string)` возвращает `T` (строка 92).

### Тесты как эталон кода

* `Tests/EditMode/Filter/Integration/EntityFilter_IntergrationTests.cs` — **лучший компилирующийся пример во всей области**: мир, фильтр, именованный предикат на `TryGetValue`, реакция на теги и значения.
* `Tests/EditMode/Filter/Unit/EntityFilter_Dispose_Tests.cs` — что `Dispose` фильтра реально гарантирует (`Count == 0`, отписка от источника, повторный `Dispose` не бросает).
* `Tests/EditMode/Collection/*` — 13 файлов по одному на член коллекции; точная семантика `Add`/`Remove`/`Clear`/`CopyTo`/событий.
* `Tests/EditMode/World/*` — 17 файлов по жизненному циклу мира.
* `Tests/EditMode/Registry/EntityRegistry_Tests.cs` — поведение реестра.
* `Tests/PlayMode/MonoEntityWorld/*` — четыре файла; здесь же идиоматичный `MonoEntityWorld.Create("Test", scanEntities: false)`.
* `Tests/Performance/Collection/EntityCollectionPerformance.cs` — набор, из которого получена таблица в `Collections/Manual.md`. Весь файл под `#if UNITY_6000` — на более ранней версии редактора он не соберётся.
