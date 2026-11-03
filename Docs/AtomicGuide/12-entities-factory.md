# 12. Entities: фабрики и пулы

Фабрика в Atomic — это одно место, где знают, как собрать сущность одного архетипа: с какими тегами, значениями, поведениями и с какой ёмкостью внутренних массивов. Пул — это место, где знают, сколько экземпляров этого архетипа держать живыми и когда их переиспользовать вместо `new` и `Destroy`. Эти две роли разделены намеренно: фабрика отвечает на «как», пул — на «сколько», и код спавна не знает ни того, ни другого, он только берёт (`Rent`) и возвращает (`Return`).

Важное предупреждение, с которого придётся начать: в этом клоне **документация в `Docs/Entities/Factories` и `Docs/Entities/Pooling` описывает более старый API, чем код в `Assets/Plugins/Atomic/Entities/Scripts`**. Каждый тип фабрики и каталога в исходниках несёт дополнительный параметр `TArgs : IArgs`, которого в документации нет вообще; часть задокументированных типов (`InlineEntityFactory`, `MultiEntityFactory`) в исходниках отсутствует. Все подписи в этой главе сверены с файлами исходников, пути и номера строк указаны; документация в разделе «Где читать» помечена по степени расхождения.

Второе предупреждение: **почти весь тест-сьют этой области закомментирован построчно**. Живых файлов три. Это первое, что нужно знать перед рефакторингом против этого кода, поэтому подробности — в «Граблях».

## Модель

Пять вещей, которые нужно уложить в голову до первого обращения к API.

**1. Два ортогональных деления.** Первое — «один архетип» против «много архетипов по ключу»: `IEntityPool<TEntity>` против `IMultiEntityPool<TKey, TEntity>`, `IEntityFactory<TEntity, TArgs>` против `IMultiEntityFactory<TKey, TEntity, TArgs>`. Второе — «чистый C#» против «Unity»: `EntityPool`/`MultiEntityPool` работают с `Entity` и не требуют сцены, `MonoEntityPool`/`PrefabEntityPool` — это `MonoBehaviour`, которые инстанцируют префабы и у которых механизм аренды — это `SetActive`.

**2. Семейств пулов на самом деле четыре, а не три.** Три в `Pooling/` (`EntityPool`, `MultiEntityPool`, и два Unity-пула), и четвёртое — `EntityViewPool<K, E, V>` в `UI/Pool/`. Четвёртое не реализует `IEntityPool`, его `Rent`/`Return` помечены `internal`, но это единственный пул в дереве, у которого есть рантайм-регистрация (`Register(K, V)` / `Unregister(K)`), единственный, который заранее задаёт ёмкость `Stack`, и единственный, обёрнутый в `ProfilerMarker`. Если вам нужна регистрация архетипов в рантайме — смотрите на него как на образец, потому что в `Factories/` её нет вообще.

**3. У пулов четыре виртуальных хука, и их семантика различается по классам.** `OnCreate` вызывается один раз на экземпляр при создании, `OnRent` — при каждой выдаче, `OnReturn` — при каждом возврате, `OnDispose` — при разрушении пула. В `EntityPool`/`MultiEntityPool` все четыре — пустые `protected virtual`. В `MonoEntityPool<E, P>` они переопределены на `SetActive(false)` / `SetActive(true)` / `SetActive(false) + SetParent(_container)`, **без** `MethodImpl`. В `PrefabEntityPool<E, P>` они переопределены на `SetActive` и помечены `AggressiveInlining`, а репарентинг при возврате сделан не в хуке, а инлайном в `Return`. Это не придирка: если вы копируете «реализацию фреймворка», нужно знать, из какого именно класса вы копируете.

**4. Пулированная `MonoEntity` регистрируется и устанавливается ровно один раз.** `MonoEntity.Create(prefab, …)` вызывает приватный `SetupAfterCreation`, который делает `entity.Register(); entity.Install(); entity.name = prefab.name;` (`Entities/Unity/MonoEntity_Creation.cs:245`). Дальше аренда — это только `SetActive`. Отсюда два следствия. Первое: **никогда не переустанавливайте пулированную сущность на `Rent`** — установка уже произошла. Второе, которое обычно не замечают: `SetActive` дёргает `OnEnable`/`OnDisable` сущности, а они при `useUnityLifecycle && _started` вызывают `Enable()`/`Disable()` и регистрируют/снимают сущность в `TickableManager.Instance` (`Entities/Unity/MonoEntity_UnityLifecycle.cs:22-48`). То есть аренда и возврат — это ещё и подписка/отписка от тика. Инициализация-на-спавн принадлежит хуку `OnRent`, а не интерфейсу инициализации.

**5. `TArgs` — это аргументы конструирования, а не состояния.** `Create(TArgs args)` вызывается при создании экземпляра, то есть при прогреве пула и при его расширении, а не при аренде. Пул хранит ровно один `TArgs`, переданный в конструктор, и использует его для всех создаваемых сущностей. Если вам нужно передать параметры конкретного спавна (владелец, команда, точка), `TArgs` для этого не подойдёт при использовании через пул — он подойдёт только при прямом вызове фабрики. Через пул параметры спавна ставятся после `Rent`.

## API

### Аргументы конструирования

`Utils/Args.cs`:

```csharp
public interface IArgs { }

public struct NoArgs : IArgs
{
    public static readonly NoArgs Default = new();
}

public struct Args<T> : IArgs
{
    public readonly T value;
    public Args(T value) { this.value = value; }
    public static implicit operator Args<T>(T value) => new(value);
}

public struct Args<T1, T2> : IArgs       { public T1 value1; public T2 value2; /* + ctor */ }
public struct Args<T1, T2, T3> : IArgs   { public T1 value1; public T2 value2; public T3 value3; }
public struct Args<T1, T2, T3, T4> : IArgs { /* value1..value4 */ }
```

Берите `NoArgs` и `NoArgs.Default`, когда архетипу ничего не нужно (это большинство случаев). Весь набор — структуры; других реализаций `IArgs` в дереве нет. Обратите внимание на несогласованность: у `Args<T>` единственное поле `readonly` и есть неявное приведение из `T`, у `Args<T1, T2>` и дальше поля публичные мутабельные и приведения нет. Отдельного типа `Args` без параметров **не существует** — `new Args()` не компилируется.

### Фабрики: интерфейсы

```csharp
// Factories/Interfaces/IEntityFactory`1.cs
public interface IEntityFactory<out TEntity, in TArgs>
    where TEntity : IEntity
    where TArgs : IArgs
{
    TEntity Create(TArgs args);
}
```

Единственный примитив создания. Ковариантен по сущности, контравариантен по аргументам, поэтому `IEntityFactory<UnitEntity, NoArgs>` напрямую годится там, где ждут `IEntityFactory<IEntity, NoArgs>` — адаптеры-обёртки писать не нужно.

```csharp
// Factories/Interfaces/IEntityFactory.cs
public interface IEntityFactory<in TArgs> : IEntityFactory<IEntity, TArgs>
    where TArgs : IArgs { }
```

Псевдоним для гетерогенных реестров. Заметьте: «неgeneric» версия сама генерик по `TArgs`. Типа `IEntityFactory` вообще без аргументов типа **нет**.

```csharp
// Factories/Interfaces/IMultiEntityFactory`1.cs
public interface IMultiEntityFactory<in TKey, TEntity, in TArgs>
    where TArgs : IArgs
    where TEntity : IEntity
{
    TEntity Create(TKey key, TArgs args);
    bool TryCreate(TKey key, TArgs args, out TEntity entity);
    bool Contains(TKey key);
}

// Factories/Interfaces/IMultiEntityFactory.cs
public interface IMultiEntityFactory<in TArgs> : IMultiEntityFactory<string, IEntity, TArgs>
    where TArgs : IArgs { }
```

Три метода, и всё. `Register`/`Unregister` в этом интерфейсе **нет**, и ни одного класса с такими методами в `Factories/` тоже нет. `TEntity` здесь инвариантен, потому что появляется в `out`-параметре.

### Фабрики: реализации

```csharp
// Factories/Unity/ScriptableEntityFactory`1.cs:26
public abstract class ScriptableEntityFactory<TEntity, TArgs> : ScriptableObject,
    IEntityFactory<TEntity, TArgs>
    where TArgs : IArgs
    where TEntity : IEntity
{
    [SerializeField] private int tagCapacity;        // Header("Optimization")
    [SerializeField] private int valueCapacity;
    [SerializeField] private int behaviourCapacity;
    [SerializeField] private Entity.Settings settings;
    [SerializeField] private bool autoCompile = true;

    private protected IEntity _previewEntity;

    public TEntity Create(TArgs args) =>
        this.Create(this.tagCapacity, this.valueCapacity, this.behaviourCapacity, this.settings, args);

    protected abstract TEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        TArgs args
    );

    protected virtual void OnValidate();        // вызывает Compile(), если autoCompile
    private protected virtual void Compile();   // только редактор, [ContextMenu(nameof(Compile))]
    protected virtual void Reset();             // обнуляет три ёмкости
}
```

Базовый класс фабрики-ассета. Точка расширения — **protected-перегрузка `Create` с пятью параметрами**; публичный `Create(TArgs)` её вызывает, подставляя сериализованные поля. Поля ёмкостей приватные (`tagCapacity`, не `initialTagCount`, как в документации) — вы получаете их как параметры и обязаны протащить в конструктор сущности. `Compile()` — `private protected`, так что переопределить его можно только внутри сборки `Atomic.Entities`; он вызывает `this.Create(default)`, читает `TagCount`/`ValueCount`/`BehaviourCount` у превью-сущности и пишет обратно в сериализованные поля.

```csharp
// Factories/Unity/ScriptableEntityFactory.cs:14
public abstract class ScriptableEntityFactory<TArgs> :
    ScriptableEntityFactory<IEntity, TArgs>, IEntityFactory<TArgs>
    where TArgs : IArgs
{
    [field: SerializeField]
    public virtual string Name { get; private set; }

    protected override IEntity Create(
        int tagCapacity, int valueCapacity, int behaviourCapacity,
        Entity.Settings settings, TArgs args)
    {
        Entity entity = new Entity(this.Name, tagCapacity, valueCapacity, behaviourCapacity, settings);
        this.Install(entity);
        return entity;
    }

    protected abstract void Install(IEntity entity);
}
```

Случай на 95%: наследуйтесь и реализуйте только `Install(IEntity)`. Конструирование, имя, ёмкости и `Entity.Settings` уже сделаны правильно. `Name` — сериализованное авто-свойство с приватным сеттером, и оно служит одновременно именем сущности и строковым ключом каталога. Дыра: **`Install` не получает `args`**, поэтому арг-зависимый архетип обязан переопределять пятипараметровый `Create`, а не `Install`.

```csharp
// Factories/Unity/ScriptableEntityCatalog`1.cs:21
public abstract class ScriptableEntityCatalog<K, E, F, TArgs> : ScriptableObject,
    IMultiEntityFactory<K, E, TArgs>,
    IReadOnlyDictionary<K, F>
    where E : IEntity
    where F : ScriptableObject, IEntityFactory<E, TArgs>
    where TArgs : IArgs
{
    [SerializeField] private F[] _factories;
    private Dictionary<K, F> _factoryMap;

    public E Create(K key, TArgs args);                     // KeyNotFoundException, если ключа нет
    public bool TryCreate(K key, TArgs args, out E entity);
    public bool Contains(K key);

    protected abstract K GetKey(F factory);

    public int Count { get; }
    public IEnumerable<K> Keys { get; }
    public IEnumerable<F> Values { get; }
    public F this[K key] { get; }
    public bool ContainsKey(K key);
    public bool TryGetValue(K key, out F value);
    public IEnumerator<KeyValuePair<K, F>> GetEnumerator();
}
```

Четыре параметра типа (документация показывает три). Ограничение на `F` — `ScriptableObject, IEntityFactory<E, TArgs>`, а **не** `ScriptableEntityFactory<E, TArgs>`: подойдёт любой `ScriptableObject`, реализующий интерфейс фабрики. Параметр `GetKey` имеет тип `F`, то есть ваш аргумент типа, а не какой-то базовый класс фабрики. Массив `_factories` лениво сплющивается в словарь внутри `EnsureInitialized()`, который вызывает **каждый** публичный член.

При наличии Odin в инспекторе работает валидация ещё на этапе авторинга (``ScriptableEntityCatalog`1.cs:28-34``, `109-126`):

```csharp
#if ODIN_INSPECTOR
        [AssetsOnly]
        [ValidateInput(nameof(ValidateFactories), "Factories contain duplicates or nulls")]
        [ListDrawerSettings(ShowFoldout = true)]
#endif
        [SerializeField]
        private F[] _factories;
```

`ValidateFactories` возвращает `false` при любом `null` и при любом дубликате ключа. Без Odin вы получите только `Debug.LogWarning` в рантайме из `EnsureInitialized`. Это меняет рекомендацию: включайте путь Odin или напишите свой редакторный валидатор, а не надейтесь на логи при прогреве.

```csharp
// Factories/Unity/ScriptableEntityCatalog.cs:17
[CreateAssetMenu(fileName = "MultiEntityFactory", menuName = "Atomic/Entities/MultiEntityFactory")]
public abstract class ScriptableEntityCatalog<TArgs> :
    ScriptableEntityCatalog<string, IEntity, ScriptableEntityFactory<TArgs>, TArgs>,
    IMultiEntityFactory<TArgs>
    where TArgs : IArgs
{
    protected override string GetKey(ScriptableEntityFactory<TArgs> factory) => factory.Name;
}
```

Строково-ключевой каталог. Ключ — сериализованное свойство `Name` фабрики, а **не** `factory.name` (имя файла ассета), хотя `<returns>` прямо над этой строкой утверждает «The name of the factory asset». Класс абстрактный, несмотря на `[CreateAssetMenu]`: пункт меню не может создать экземпляр, нужен конкретный закрытый подкласс.

### Пулы: интерфейсы

```csharp
// Pooling/Interfaces/IEntityPool`1.cs
public interface IEntityPool<TEntity> : IDisposable where TEntity : IEntity
{
    TEntity Rent();
    void Return(TEntity entity);
    void Init(int initialCount);
}

// Pooling/Interfaces/IEntityPool.cs
public interface IEntityPool : IEntityPool<IEntity> { }
```

```csharp
// Pooling/Interfaces/IMultiEntityPool`1.cs
public interface IMultiEntityPool<in TKey, TEntity> : IDisposable where TEntity : IEntity
{
    void Init(TKey key, int count);
    TEntity Rent(TKey key);
    void Return(TEntity entity);
}

// Pooling/Interfaces/IMultiEntityPool.cs
public interface IMultiEntityPool : IMultiEntityPool<string, IEntity> { }
```

`Return(entity)` **не берёт ключ** — пул сам помнит, из какого ключа вышел экземпляр (`Dictionary<TEntity, TKey> _rentEntities`). Это главная деталь, которую стоит скопировать в свои пулы: вызывающему не приходится носить ключ вместе со ссылкой.

```csharp
// Pooling/Interfaces/IPrefabEntityPool`1.cs  (#if UNITY_5_3_OR_NEWER)
public interface IPrefabEntityPool<E, in P> : IDisposable
    where E : IEntity
    where P : MonoEntity, E
{
    void Init(P prefab, int count);
    ValueTask InitAsync(P prefab, int initialCount);
    E Rent(P prefab);
    E Rent(P prefab, Transform parent);
    E Rent(P prefab, Vector3 position, Quaternion rotation, Transform parent = null);
    void Return(E entity);
    void Dispose(P prefab);
}

// Pooling/Interfaces/IPrefabEntityPool.cs
public interface IPrefabEntityPool : IPrefabEntityPool<IEntity, MonoEntity> { }
```

Два параметра типа: `E` — то, что пул выдаёт наружу, `P` — конкретный тип префаба. Ограничение `where P : MonoEntity, E` — самая полезная форма во всём разделе: пул может выдавать `IEnemyEntity`, инстанцируя префабы `EnemyEntity`, без приведений на вызывающей стороне. Пулевые интерфейсы, в отличие от фабричных, **не получили** `TArgs`: аргументы живут в реализации, потребители только арендуют.

### Пулы: реализации

```csharp
// Pooling/ExpandMode.cs
public enum ExpandMode
{
    ExpandByOne,       // по умолчанию во всех конструкторах и сериализованных полях
    ExpandByDoubling,
    NoExpand
}
```

| Значение | Что делает на самом деле | Когда брать |
|---|---|---|
| `ExpandByOne` | создаёт одну сущность за пустой `Rent` | разумный дефолт для редких спавнов |
| `ExpandByDoubling` | создаёт `rentedCount` сущностей (минимум 1), пушит их и снимает одну | пик нагрузки неизвестен |
| `NoExpand` | бросает `InvalidOperationException` с текстом, называющим лечение | жёсткий лимит; единственный режим с гарантией нулевых аллокаций в рантайме |

Явных числовых значений у членов перечисления в исходнике **нет** (в `Docs/Entities/Pooling/ExpandMode.md` они проставлены как `0``/`1``/`2`). Порядок совпадает, но не персистите эти значения как `int` через апгрейд фреймворка.

```csharp
// Pooling/Core/EntityPool`1.cs:25
public class EntityPool<TEntity, TArgs> : IEntityPool<TEntity>
    where TEntity : IEntity
    where TArgs : IArgs
{
    private protected readonly Stack<TEntity> _pooledEntities = new();
    private protected readonly HashSet<TEntity> _rentEntities = new();

    public EntityPool(
        IEntityFactory<TEntity, TArgs> factory,
        TArgs args,
        ExpandMode expandMode = ExpandMode.ExpandByOne);

    public void Init(int initialCount);
    public TEntity Rent();
    public void Return(TEntity entity);
    public void Dispose();

    protected virtual void OnCreate(TEntity entity);
    protected virtual void OnDispose(TEntity entity);
    protected virtual void OnRent(TEntity entity);
    protected virtual void OnReturn(TEntity entity);
}

// Pooling/Core/EntityPool.cs:9
public class EntityPool<TArgs> : EntityPool<IEntity, TArgs>, IEntityPool
    where TArgs : IArgs
{
    public EntityPool(IEntityFactory<IEntity, TArgs> factory, TArgs args,
        ExpandMode expandMode = ExpandMode.ExpandByOne);
}
```

Самый чистый из пулов: `Stack` для свободных, `HashSet` для арендованных, `Return` проверяет только `_rentEntities.Remove(entity)` — никакого `Stack.Contains`. `new EntityPool(factory)` из документации не компилируется; минимум — `new EntityPool<NoArgs>(factory, NoArgs.Default)`.

```csharp
// Pooling/Core/MultiEntityPool`1.cs:17
public class MultiEntityPool<TKey, TEntity, TArgs> : IMultiEntityPool<TKey, TEntity>
    where TEntity : IEntity
    where TArgs : IArgs
{
    private readonly Dictionary<TKey, Stack<TEntity>> _pooledEntities = new();
    private readonly Dictionary<TEntity, TKey> _rentEntities = new();
    private readonly Dictionary<TKey, int> _rentedCounts = new();

    public MultiEntityPool(
        IMultiEntityFactory<TKey, TEntity, TArgs> factory,
        TArgs args,
        ExpandMode expandMode = ExpandMode.ExpandByOne);

    public void Init(TKey key, int count);
    public TEntity Rent(TKey key);
    public void Return(TEntity entity);
    public void Dispose();
    // те же четыре protected virtual хука
}

// Pooling/Core/MultiEntityPool.cs:6
public class MultiEntityPool<TArgs> : MultiEntityPool<string, IEntity, TArgs>, IMultiEntityPool
    where TArgs : IArgs
{
    public MultiEntityPool(IMultiEntityFactory<string, IEntity, TArgs> factory, TArgs args,
        ExpandMode expandMode = ExpandMode.ExpandByOne);
}
```

Каноническая рантайм-пара: `ScriptableEntityCatalog<TArgs>` на вход, `MultiEntityPool<TArgs>` на выход — у них совпадают ключ (`string`) и тип сущности (`IEntity`). Осторожно: ``MultiEntityPool`1.cs:3`` содержит безусловный `using UnityEngine;` без `#if UNITY_5_3_OR_NEWER` (и при этом ничего из `UnityEngine` не использует), в отличие от ``EntityPool`1.cs``, где этот using закрыт директивой. В не-Unity сборке `MultiEntityPool` не скомпилируется.

```csharp
// Pooling/Unity/MonoEntityPool`1.cs:24
public abstract class MonoEntityPool<E, P> : MonoBehaviour, IEntityPool<E>
    where E : IEntity
    where P : MonoEntity, E
{
    [SerializeField] private bool _initOnAwake = true;
    [SerializeField] internal bool _acceptExternalReturns = true;
    [SerializeField] private int _initialCount;
    [SerializeField] private P _prefab;
    [SerializeField] private Transform _container;
    [SerializeField] private bool _dontDestroyOnLoad;
    [SerializeField] private ExpandMode _expandMode = ExpandMode.ExpandByOne;

    internal readonly Stack<P> _pooledEntities = new();
    internal readonly HashSet<P> _rentEntities = new();

    protected virtual void Awake();
    protected virtual void Reset();

    public void Init(int initialCount);
    public async ValueTask InitAsync(int initialCount);
    public E Rent();
    public void Return(E entity);
    public virtual void Dispose();

    protected virtual void OnCreate(P entity) => entity.gameObject.SetActive(false);
    protected virtual void OnDispose(P entity) { }
    protected virtual void OnRent(P entity) => entity.gameObject.SetActive(true);
    protected virtual void OnReturn(P entity)
    {
        entity.gameObject.SetActive(false);
        entity.transform.SetParent(_container);
    }

    [Serializable]
    public struct CreateArgs
    {
        public string name;
        public P prefab;
        public Transform container;
        public bool initOnAwake;
        public int initialCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TPool Create<TPool>(in CreateArgs args) where TPool : MonoEntityPool<E, P>;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Destroy(MonoEntityPool<E, P> pool, float t = 0);
}

// Pooling/Unity/MonoEntityPool.cs:23
[AddComponentMenu("Atomic/Entities/Entity Pool")]
[DisallowMultipleComponent]
public class MonoEntityPool : MonoEntityPool<IEntity, MonoEntity>, IEntityPool
{
    public static MonoEntityPool Create(in CreateArgs args) => Create<MonoEntityPool>(in args);
}
```

`InitAsync` **не объявлен** в `IEntityPool<TEntity>`, так что асинхронный прогрев недостижим через интерфейс — для него нужно держать конкретный тип. Поле `_acceptExternalReturns` — `internal`, то есть из игровой сборки его невозможно выставить кодом, только галочкой в инспекторе (`InternalsVisibleTo` в `AssemblyInfo.cs` выдан только тестовым сборкам и `NetworkEngine.Fusion`). `[DisallowMultipleComponent]` означает один пул на `GameObject`: несколько префабов — это либо дочерние объекты, либо `PrefabEntityPool`.

```csharp
// Pooling/Unity/PrefabEntityPool`1.cs:25
public abstract class PrefabEntityPool<E, P> : MonoBehaviour, IPrefabEntityPool<E, P>
    where E : IEntity
    where P : MonoEntity, E
{
    private const string NUMBER_PATTERN = @"\s*\(\d+\)$";

    internal struct Pool
    {
        public Stack<P> stack;
        public Transform container;
        public GameObject go;
    }

    private readonly Dictionary<string, Pool> _pools = new();
    private readonly Dictionary<string, int> _rentedCounts = new();

    [SerializeField] private Transform _container;
    [SerializeField] private bool _dontDestroyOnLoad;
    [SerializeField] private ExpandMode _expandMode = ExpandMode.ExpandByOne;

    public void Init(P prefab, int count);
    public async ValueTask InitAsync(P prefab, int initialCount);
    public E Rent(P prefab);
    public E Rent(P prefab, Transform parent);
    public E Rent(P prefab, Vector3 position, Quaternion rotation, Transform parent = null);
    public void Return(E entity);
    public void Dispose(P prefab);
    public void Dispose();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnCreate(P entity) => entity.gameObject.SetActive(false);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnRent(P entity) => entity.gameObject.SetActive(true);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnReturn(P entity) => entity.gameObject.SetActive(false);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual void OnDispose(P entity) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual string GetEntityName(P entity) =>
        Regex.Replace(entity.name, NUMBER_PATTERN, string.Empty).Trim();
}

// Pooling/Unity/PrefabEntityPool.cs:16
[AddComponentMenu("Atomic/Entities/Prefab Entity Pool")]
[DisallowMultipleComponent]
public class PrefabEntityPool : PrefabEntityPool<IEntity, MonoEntity>, IPrefabEntityPool { }
```

Ключ пула — **строка**, полученная регексом из имени `GameObject`, а не ссылка на префаб. Пулы создаются лениво на первом `Init`/`Rent`/`Return`, каждый со своим дочерним контейнером-`GameObject` с именем `<Names>`. `GetEntityName` — единственный `protected virtual`, который стоит переопределять ради производительности (см. «Производительность»).

### Четвёртое семейство: EntityViewPool

```csharp
// UI/Pool/EntityViewPool`1.cs:13
public abstract class EntityViewPool<K, E, V> : MonoBehaviour
    where E : class, IEntity
    where V : EntityView<E>
{
    private static readonly ProfilerMarker s_rentMarker = new($"EntityViewPool<{typeof(E).Name}>.Rent");
    private static readonly ProfilerMarker s_returnMarker = new($"EntityViewPool<{typeof(E).Name}>.Return");

    [SerializeField] private Transform container;
    [SerializeField] private int initialCapacity = 32;
    [SerializeField] private EntityViewCatalog<E, V>[] catalogs;

    public void Register(K key, V prefab);
    public void Unregister(K key);
    public void Register(EntityViewCatalog<E, V> catalog);
    public void Unregister(EntityViewCatalog<E, V> catalog);
    public void Clear();
    public void Init(K key, int count);
    public async ValueTask InitAsync(K key, int count);

    internal V Rent(K key, Transform parent);
    internal void Return(V view);

    protected abstract K GetKey(V view);
}

// UI/Pool/EntityViewPool.cs
public class EntityViewPool : EntityViewPool<string, IEntity, EntityView>
{
    protected override string GetKey(EntityView view) => view.Name;
}

// UI/Catalog/EntityViewCatalog`1.cs:14
public abstract class EntityViewCatalog<E, V> : ScriptableObject
    where E : class, IEntity
    where V : EntityView<E>
{
    [SerializeField] private List<V> prefabs;
    public int Count => this.prefabs.Count;
    public V GetPrefab(int index) => prefabs[index];
}
```

Это пул представлений, не сущностей, и `Rent`/`Return` у него `internal` — заменой `IEntityPool` он не является. Но это in-tree образец для двух вещей, которых в `Pooling/` нет: рантайм-регистрация архетипов по ключу и предварительный размер `Stack` (`new Stack<V>(initialCapacity)` в `GetOrCreatePool`).

### Вспомогательное из MonoEntity

```csharp
// Entities/Unity/MonoEntity_Creation.cs
[Serializable]
public struct CreateArgs
{
    public string name;
    public IEnumerable<int> tags;
    public IReadOnlyDictionary<int, object> values;
    public IEnumerable<IEntityBehaviour> behaviours;
    public List<MonoEntityInstaller> sceneInstallers;
    public List<ScriptableEntityInstaller> scriptableInstallers;
    public int initialTagCapacity, initialValueCapacity, initialBehaviourCapacity;
    public bool installOnAwake, uninstallOnDestroy, disposeValues, useUnityLifecycle;
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static MonoEntity Create(in CreateArgs args);
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static E Create<E>(in CreateArgs args) where E : MonoEntity;
```

Единственный путь создать `MonoEntity` без префаба с заданными ёмкостями, инсталлерами и флагами жизненного цикла. Его используют живые тесты (`MonoEntity.Create<MonoEntity>()` в `PrefabEntityPoolTests`, `MonoEntity.Create()` в `MonoEntityPoolTests`) чтобы собрать «префаб» в коде. Две ловушки: все три ёмкости зажимаются через `Mathf.Max(1, …)` (строки 63-65), поэтому запечённый нуль молча становится единицей; и сам `Create<E>` делает `Construct()` + `Register()`, но **не** `Install()` — установка произойдёт только в `Awake` и только если `args.installOnAwake == true`, а `CreateArgs` — структура, так что инициализатор объекта, где это поле пропущено, оставит его `false`.

```csharp
public static MonoEntity Create(MonoEntity prefab, Transform parent = null);
public static E Create<E>(E prefab, Transform parent = null) where E : MonoEntity;
public static MonoEntity Create(MonoEntity prefab, Vector3 position, Quaternion rotation, Transform parent = null);
public static E Create<E>(E prefab, Vector3 position, Quaternion rotation, Transform parent = null) where E : MonoEntity;
public static E Create<E>(E prefab, Transform point, Transform parent) where E : MonoEntity;
public static E Create<E>(E prefab, Scene scene) where E : MonoEntity;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static async ValueTask<E[]> CreateAsync<E>(E prefab, int count, Transform parent = null)
    where E : MonoEntity;
```

Все помечены `AggressiveInlining` и все проходят через `SetupAfterCreation` (`Register` + `Install` + `name = prefab.name`). Пулы используют только `Create<E>(prefab, parent)`. Если вы пишете свой пул — берите перегрузку с позицией и поворотом: она инстанцирует сразу на месте, вместо «инстанцировать, потом записать трансформ». Перегрузка со `Scene` — единственный способ заспавнить сущность прямо в неактивную сцену (стриминг аддитивных сцен).

```csharp
// Entities/Unity/MonoEntity_Casting.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static MonoEntity Cast(IEntity entity) => Cast<MonoEntity>(entity);
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static E Cast<E>(IEntity entity) where E : MonoEntity => entity switch
{
    null => null,
    E sceneEntity => sceneEntity,
    MonoEntityProxy<E> proxy => proxy.Source,
    _ => throw new InvalidCastException($"Can't cast {entity.Name} to {typeof(E).Name}")
};
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool TryCast(IEntity entity, out MonoEntity result) => TryCast<MonoEntity>(entity, out result);
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool TryCast<E>(IEntity entity, out E result) where E : MonoEntity;
```

Канонический способ вернуться от `IEntity` к `GameObject` после аренды через `IEntityPool`/`IPrefabEntityPool` — именно `MonoEntity.Cast(...)`, так делают живые тесты. В своём коде предпочитайте `TryCast`: `Cast` бросает.

```csharp
// Entities/Unity/MonoEntity_Destruction.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Destroy(IEntity entity, float t = 0) => Destroy(Cast(entity), t);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Destroy(MonoEntity entity, float t = 0)
{
    if (entity)
    {
        entity.Uninstall();
        Destroy(entity.gameObject, t);
    }
}
```

`Destroy` сначала вызывает `Uninstall()` — это парный к `Install` из `SetupAfterCreation`. Именно поэтому `MonoEntityPool.Dispose()` и `PrefabEntityPool.Dispose()` безопасны: ваш `OnDispose` вызывается **до** `Uninstall`, поэтому он ещё видит установленное состояние. Перегрузка с `IEntity` проходит через `Cast`, то есть бросает `InvalidCastException` на не-`MonoEntity`.

### Конструкторы Entity, которые видит автор фабрики

```csharp
// Entities/Core/Entity.cs:79, :109, :138
public Entity(string name,
    IEnumerable<string> tags,
    IEnumerable<KeyValuePair<string, object>> values,
    IEnumerable<IEntityBehaviour> behaviours,
    Settings? settings = null);

public Entity(string name,
    IEnumerable<int> tags,
    IEnumerable<KeyValuePair<int, object>> values,
    IEnumerable<IEntityBehaviour> behaviours,
    Settings? settings = null);

public Entity(string name = null,
    int tagCapacity = 0,
    int valueCapacity = 0,
    int behaviourCapacity = 0,
    Settings? settings = null);
```

Первые два — **неправильный выбор для фабрики**: они вычисляют ёмкости через `tags?.Count() ?? 0`, то есть LINQ-`Count()` по `IEnumerable` — полный проход с интерфейсным вызовом на элемент, на каждую создаваемую сущность. Третий — тот, который использует `ScriptableEntityFactory<TArgs>`. Ещё два факта: каждый конструктор `Entity` заканчивается `EntityRegistry.Instance.Register(this)` (строка 153), так что прогрев пула чистых `Entity` навсегда увеличивает глобальный реестр; и `settings == null` даёт `new Settings { disposeValues = true }`.

### Тестовые двойники

```csharp
// Tests/Substitutions/Pools/EntityPoolTestDouble.cs
public sealed class EntityPoolTestDouble : IEntityPool
{
    public Action DisposeMethod;
    public Func<IEntity> RentMethod;
    public Action<IEntity> ReturnMethod;
    public Action<int> InitMethod;
    // каждый член просто Invoke'ает соответствующий делегат
}

// Tests/Substitutions/Pools/EntityPoolTestDouble`1.cs
public sealed class EntityPoolTestDouble<T> : IEntityPool<T> where T : IEntity { /* то же */ }
```

Единственная живая, не закомментированная тестовая инфраструктура в этой области. Это поддерживаемый шов для юнит-тестов вашего спавн-кода против `IEntityPool`/`IEntityPool<T>` без сцены.

## Идиомы

### 1. Фабрика-архетип: только Install

```csharp
[CreateAssetMenu(fileName = "PlayerFactory", menuName = "Game/Factories/Player")]
public sealed class PlayerFactory : ScriptableEntityFactory<NoArgs>
{
    protected override void Install(IEntity entity)
    {
        entity.AddTag("Player");
        entity.AddValue("Health", 200);
        entity.AddValue("MoveSpeed", 10f);
        entity.AddBehaviour<MoveBehaviour>();
    }
}
```

Правильная форма: в наследнике нет ничего, кроме композиции архетипа. Конструирование, имя, ёмкости и `Entity.Settings` — забота базового класса, поэтому ни один наследник не может их испортить, и редакторный `Compile()` может запечь точные ёмкости именно потому, что базовый класс владеет вызовом конструктора. `[CreateAssetMenu]` ставится на конкретный закрытый класс, а не на базовый.

**Антипаттерн** (эту форму показывает `Docs/Entities/Factories/Manual.md`, и именно так делать нельзя):

```csharp
// НЕ ДЕЛАТЬ: ёмкости не заданы, внутренние массивы растут и перехешируются
public override IEntity Create()
{
    var e = new Entity("Player");
    e.AddTag("Player");
    return e;
}
```

Кроме отсутствия ёмкостей, у этого кода нет шансов скомпилироваться: беспараметрового `Create()` в текущих исходниках нет.

### 2. Фабрика с аргументами: переопределять пятипараметровый Create

`Install(IEntity)` не получает `args`. Если архетип зависит от параметров конструирования, точка расширения другая:

```csharp
public sealed class SpawnedUnitFactory
    : ScriptableEntityFactory<Entity, Args<int, Vector3>>
{
    protected override Entity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        Args<int, Vector3> args)
    {
        var entity = new Entity("Unit", tagCapacity, valueCapacity, behaviourCapacity, settings);

        entity.AddTag("Unit");
        entity.AddValue("TeamId", args.value1);
        entity.AddValue("SpawnPoint", args.value2);
        entity.AddBehaviour<MoveBehaviour>();

        return entity;
    }
}
```

Правильная форма по трём причинам: запечённые ёмкости приходят параметрами и уходят прямо в конструктор сущности (прочитать приватные поля вы всё равно не можете); `settings` протаскивается, а не придумывается заново; и аргументы доступны там, где они нужны — в момент конструирования.

Цена: `autoCompile` по умолчанию `true`, и `Compile()` вызывает `this.Create(default)`, то есть `default(Args<int, Vector3>)`. Для этой фабрики это безвредно (структура с нулями), но если ваш путь конструирования разыменовывает что-то из `args`, редакторный авто-компайл будет падать на каждом `OnValidate`, а `catch` только напишет красное в консоль. Вывод: либо держите конструирование устойчивым к `default(TArgs)`, либо снимайте `autoCompile` в ассете.

### 3. Семейная база фабрик: sealed override + protected abstract Install

```csharp
public class EnemyEntity : Entity
{
    public EnemyEntity(
        string name,
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Settings? settings = null)
        : base(name, tagCapacity, valueCapacity, behaviourCapacity, settings) { }
}

public abstract class EnemyFactoryBase : ScriptableEntityFactory<EnemyEntity, NoArgs>
{
    [SerializeField] private EnemyType _type;
    public EnemyType Type => _type;

    protected sealed override EnemyEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        NoArgs args)
    {
        var entity = new EnemyEntity(
            _type.ToString(), tagCapacity, valueCapacity, behaviourCapacity, settings);

        this.Install(entity);
        return entity;
    }

    protected abstract void Install(EnemyEntity entity);
}

[CreateAssetMenu(fileName = "OrcFactory", menuName = "Game/Factories/Enemies/Orc")]
public sealed class OrcFactory : EnemyFactoryBase
{
    protected override void Install(EnemyEntity entity)
    {
        entity.AddTag("Enemy");
        entity.AddValue("Health", 100);
        entity.AddValue("Damage", 15);
        entity.AddBehaviour<AttackBehaviour>();
    }
}
```

Это шаблонный метод, тот же, что использует сам фреймворк в `ScriptableEntityFactory<TArgs>`: конструирование решается один раз на семейство, композиция — на ассет, и ни один листовой класс не может «забыть» ёмкости. Дополнительно база выдаёт ключ каталога (`Type`) типизированным свойством, а не магической строкой.

Проверьте ограничение: `EnemyFactoryBase` наследует `ScriptableEntityFactory<EnemyEntity, NoArgs>`, значит он `ScriptableObject` и реализует `IEntityFactory<EnemyEntity, NoArgs>` — ровно то, что требует `where F : ScriptableObject, IEntityFactory<E, TArgs>` у каталога. Если вместо этого попробовать унаследовать `EnemyFactoryBase` от какого-нибудь своего не-`ScriptableObject` базового класса, каталог его не примет.

### 4. Каталог по типизированному ключу

```csharp
[CreateAssetMenu(fileName = "EnemyCatalog", menuName = "Game/Catalogs/Enemies")]
public sealed class EnemyCatalog
    : ScriptableEntityCatalog<EnemyType, EnemyEntity, EnemyFactoryBase, NoArgs>
{
    protected override EnemyType GetKey(EnemyFactoryBase factory) => factory.Type;
}
```

Две строки превращают массив ассетов-фабрик в типизированный реестр. Тип параметра `GetKey` — **именно `F`**, то есть `EnemyFactoryBase`; если написать там производный `OrcFactory`, это не будет переопределением и класс не скомпилируется как неабстрактный. Enum-ключ вместо строки убирает весь класс опечаточных багов, а `GetKey` держит ключ рядом с ассетом, так что переименование файла ничего не ломает.

**Антипаттерн** (эту форму даёт `ScriptableEntityCatalog<TArgs>` в базовом варианте, и в документации она ещё и описана неверно):

```csharp
// НЕ ДЕЛАТЬ: имя файла ассета как ключ — переименование ассета молча ломает все Create("…")
protected override string GetKey(SomeFactory factory) => factory.name;
```

Для строкового каталога правильный ключ — сериализованное свойство `Name` фабрики (`factory.Name`), именно так и поступает `ScriptableEntityCatalog<TArgs>`, несмотря на то, что его собственный XML-комментарий утверждает обратное.

### 5. Каталог как flyweight: TryCreate вместо Contains + Create

```csharp
public sealed class EnemySpawner : MonoBehaviour
{
    [SerializeField] private EnemyCatalog _catalog;   // один ассет, инжектится/грузится один раз

    private void Awake()
    {
        // прогрев: любой публичный член строит словарь из массива
        _catalog.Contains(default);
    }

    public bool TrySpawn(EnemyType type, out EnemyEntity enemy) =>
        _catalog.TryCreate(type, NoArgs.Default, out enemy);
}
```

Правильная форма: `TryCreate` делает один поиск по словарю и не бросает, тогда как `Contains` + `Create` делает два поиска и всё равно может бросить `KeyNotFoundException` при гонке с изменением ассета. Вызов `Contains(default)` в `Awake` существует ровно ради побочного эффекта `EnsureInitialized()` — чтобы полная сборка словаря произошла на загрузке, а не на первом спавне после перезагрузки домена.

Типы возврата: `Create`/`TryCreate` отдают `E`, то есть `EnemyEntity`, а не `IEntity` — приводить не нужно.

### 6. Фабрика + пул, прогрев через Init

```csharp
IEntityFactory<BulletEntity, NoArgs> factory = bulletFactoryAsset;

var pool = new EntityPool<BulletEntity, NoArgs>(
    factory,
    NoArgs.Default,
    ExpandMode.ExpandByDoubling);

pool.Init(20);   // все 20 аллокаций — на загрузке
```

Разделение труда, вокруг которого построен весь раздел: фабрика знает, как собрать один экземпляр, пул — сколько держать. `Init(n)` переносит аллокации на время загрузки, и ради этого пулы вообще существуют. Обратите внимание на тип переменной: `IEntityFactory<BulletEntity, NoArgs>` — благодаря ковариантности `out TEntity` сюда подойдёт и фабрика, объявленная как `IEntityFactory<ArmorPiercingBulletEntity, NoArgs>`.

Для `IEntity`-типизированного варианта:

```csharp
var pool = new EntityPool<NoArgs>(factory, NoArgs.Default);
```

### 7. Rent → SetValue → Return

```csharp
public sealed class WeaponFire
{
    private static readonly ValueKey<int> DamageKey = new("Damage");   // хоистим ключ
    private readonly IEntityPool<BulletEntity> _pool;

    public WeaponFire(IEntityPool<BulletEntity> pool) => _pool = pool;

    public BulletEntity Fire(int damage)
    {
        BulletEntity bullet = _pool.Rent();
        bullet.SetValue(DamageKey, damage);   // слот уже существует — фабрика его добавила
        return bullet;
    }

    public void Recycle(BulletEntity bullet) => _pool.Return(bullet);
}
```

Правильная форма по двум причинам. Во-первых, `SetValue`, а не `AddValue`: слот значения уже создан фабрикой при конструировании, поэтому переиспользование мутирует на месте, без роста хранилища. Если место аренды делает `AddValue`, хранилище сущности пухнет и пулинг не даёт ничего. Во-вторых, `ValueKey<int>` вынесен в `static readonly`: его конструктор зовёт `EntityKeyStore.NameToId(string)`, а это поиск по `Dictionary<string, int>` (`KeyStore/EntityKeyStore.cs:40`) — на горячем пути его повторять незачем.

Чего в API **нет**: метода `Set`. `bullet.Set("Damage", 10)` из `Docs/BestPractices/UsingEntityPoolWithFactories.md` не компилируется; расширения называются `SetValue`. Не компилируется и `entity.AddValue<int>("Health", 100)` из `Docs/Entities/Factories/Manual.md`: строковая перегрузка — это негенерик `AddValue(this IEntity, string key, object value)`, явный аргумент типа к ней не прикладывается, а неявного приведения `string` → `ValueKey<T>` в `KeyStore/Keys/ValueKey.cs` не объявлено.

### 8. Политика роста выбирается явно

```csharp
var pool = new EntityPool<GameEntity, NoArgs>(
    factory, NoArgs.Default, ExpandMode.ExpandByDoubling);

pool.Init(10);
// … арендовали все 10 …
var e11 = pool.Rent();   // пул пуст: создаст rentedCount (= 10) штук, затолкнёт и снимет одну
```

Амортизированный рост: один всплеск создания вместо одного `Create` на кадр в течение следующих десяти кадров — именно то, что нужно, когда волновой спавнер превысил вашу оценку `Init`.

**Антипаттерн**:

```csharp
// НЕ ДЕЛАТЬ без точного верхнего предела: 6-я аренда бросит InvalidOperationException
var pool = new EntityPool<GameEntity, NoArgs>(
    factory, NoArgs.Default, ExpandMode.NoExpand);
pool.Init(5);
```

`NoExpand` — единственный режим с жёсткой гарантией нулевых аллокаций в рантайме, и поэтому единственный, который требует, чтобы `Init(n)` был доказуемо достаточен. Текст исключения прямо называет лечение, но лечение в рантайме — это уже упавший кадр.

Отдельно: удвоение считается от **арендованного** количества, а не от лежащего в пуле (``EntityPool`1.cs:112``: `_rentEntities.Count > 0 ? _rentEntities.Count : 1`). Если ничего ни разу не арендовали, режим вырождается в `ExpandByOne`.

### 9. Закрытый подкласс пула одной строкой

```csharp
public interface IEnemyEntity : IEntity { }

public class EnemyMonoEntity : MonoEntity, IEnemyEntity { }

public sealed class EnemyPool : MonoEntityPool<IEnemyEntity, EnemyMonoEntity> { }

public sealed class EnemyPrefabPool : PrefabEntityPool<IEnemyEntity, EnemyMonoEntity> { }
```

Unity не сериализует открытый генерик-`MonoBehaviour`, поэтому односторочный закрытый подкласс — это необходимое и достаточное количество кода: пулинг, расширение и хуки унаследованы. `sealed` здесь не косметика: он даёт JIT возможность девиртуализовать вызовы `OnCreate`/`OnRent`/`OnReturn`, а у `PrefabEntityPool` эти хуки ещё и помечены `AggressiveInlining`, так что девиртуализация — предусловие для того, чтобы атрибут вообще что-то значил.

Форма `MonoEntityPool<IEnemyEntity, EnemyMonoEntity>` — та, ради которой существует ограничение `where P : MonoEntity, E`: пул выдаёт интерфейс, инстанцирует конкретный тип, приведений на вызывающей стороне нет.

### 10. Создание пула в рантайме через CreateArgs

```csharp
var args = new MonoEntityPool.CreateArgs
{
    name = "EnemyPool",
    prefab = enemyPrefab,
    container = poolRoot,
    initOnAwake = true,
    initialCount = 10
};

MonoEntityPool pool = MonoEntityPool.Create(in args);

// … позже …
MonoEntityPool.Destroy(pool);
```

Именованная `[Serializable]`-структура, переданная по `in`, заменяет конструктор на пять аргументов: самодокументированный вызов, без защитной копии, без аллокации, и ту же структуру можно положить сериализованным полем в инспектор. Внутри `Create<TPool>` деактивирует хост-`GameObject` **до** `AddComponent` и активирует после (``MonoEntityPool`1.cs:303-311``) — поэтому `Awake` видит уже присвоенные поля. Этот приём стоит скопировать для любого компонента, который инициализируется в `Awake`.

Чего `Create<TPool>` **не** делает: не присваивает `_dontDestroyOnLoad` и `_expandMode`, потому что в `CreateArgs` для них нет полей. Пул, созданный в рантайме, всегда `ExpandByOne` и умирает при смене сцены. Если вам нужно иначе — наследуйте пул и переопределите `Awake`, либо выставьте поля через инспектор на префабе пула и инстанцируйте префаб вместо `Create`.

### 11. Мульти-префабный пул: только четырёхаргументный Rent

```csharp
public sealed class WaveSpawner : MonoBehaviour
{
    [SerializeField] private EnemyPrefabPool _pool;
    [SerializeField] private EnemyMonoEntity _orcPrefab;
    [SerializeField] private EnemyMonoEntity _goblinPrefab;
    [SerializeField] private Transform _enemiesRoot;

    private void Start()
    {
        _pool.Init(_orcPrefab, 5);
        _pool.Init(_goblinPrefab, 3);
    }

    public IEnemyEntity Spawn(EnemyMonoEntity prefab, Transform at) =>
        // ЕДИНСТВЕННАЯ перегрузка, которая действительно ставит родителя
        _pool.Rent(prefab, at.position, at.rotation, _enemiesRoot);

    public void Despawn(IEnemyEntity enemy) => _pool.Return(enemy);

    private void OnDestroy()
    {
        _pool.Dispose(_orcPrefab);   // один префаб
        _pool.Dispose();             // всё
    }
}
```

Правильная форма именно потому, что перегрузка `Rent(P prefab, Transform parent)` **сломана**:

```csharp
// PrefabEntityPool`1.cs:117 — как есть в исходнике
public E Rent(P prefab, Transform parent) => this.Rent(prefab, parent.position, parent.rotation);
```

Аргумент `parent` не передаётся дальше, поэтому у настоящей реализации `parent` остаётся `null`, и `tf.SetParent(null, false)` **отвязывает** сущность в корень сцены, скопировав при этом позицию и поворот предполагаемого родителя. Интерфейс ``IPrefabEntityPool`1.cs`` документирует эту перегрузку как «Rents an entity instance and parents it under the specified transform». Не используйте её; всегда вызывайте четырёхаргументную.

Про `Return(entity)`: ключа он не требует — пул восстановит строковый ключ из имени `GameObject`. Это же и его слабое место (см. «Грабли»).

### 12. Сброс состояния в OnReturn, активация в OnRent

Контракт один, реализации в двух Unity-пулах **разные**. Вот что действительно написано в исходниках.

`MonoEntityPool<E, P>` (``MonoEntityPool`1.cs:220-246``) — четыре хука **без** `MethodImpl`, репарентинг сделан в `OnReturn`:

```csharp
protected virtual void OnCreate(P entity) => entity.gameObject.SetActive(false);
protected virtual void OnDispose(P entity) { }
protected virtual void OnRent(P entity) => entity.gameObject.SetActive(true);
protected virtual void OnReturn(P entity)
{
    entity.gameObject.SetActive(false);
    entity.transform.SetParent(_container);
}
```

`PrefabEntityPool<E, P>` (``PrefabEntityPool`1.cs:255-282``) — четыре хука **с** `AggressiveInlining`, `OnReturn` только деактивирует, а репарентинг живёт инлайном в `Return` (`SetParent(pool.container, false)`, строка 201):

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
protected virtual void OnCreate(P entity) => entity.gameObject.SetActive(false);
[MethodImpl(MethodImplOptions.AggressiveInlining)]
protected virtual void OnRent(P entity) => entity.gameObject.SetActive(true);
[MethodImpl(MethodImplOptions.AggressiveInlining)]
protected virtual void OnReturn(P entity) => entity.gameObject.SetActive(false);
[MethodImpl(MethodImplOptions.AggressiveInlining)]
protected virtual void OnDispose(P entity) { }
```

Ваше расширение должно вызывать базовую реализацию и добавлять только обнуление игрового состояния:

```csharp
public sealed class EnemyPool : MonoEntityPool<IEnemyEntity, EnemyMonoEntity>
{
    private static readonly ValueKey<int> HealthKey = new("Health");

    protected override void OnReturn(EnemyMonoEntity entity)
    {
        base.OnReturn(entity);          // SetActive(false) + SetParent(_container)
        entity.SetValue(HealthKey, 0);  // обнуляем игровое состояние здесь, а не на вызывающей стороне
    }

    protected override void OnDispose(EnemyMonoEntity entity)
    {
        // вызывается ДО MonoEntity.Destroy → Uninstall, поэтому установленное состояние ещё доступно
        base.OnDispose(entity);
    }
}
```

Правильная форма потому, что состояние жизненного цикла принадлежит хукам пула: ни один вызывающий не может забыть деактивировать или вернуть в контейнер. Держите переопределения маленькими — для `PrefabEntityPool` от этого прямо зависит, останется ли хук инлайнируемым.

Чего делать **нельзя**: писать «реализацию фреймворка» как смесь двух классов — `AggressiveInlining` на `OnRent` вместе с репарентингом в `OnReturn`. Такой класс в исходниках не существует, и выбор базы определяет, какое из двух поведений вы наследуете.

### 13. Прогрев без спайка: InitAsync у MonoEntityPool

```csharp
public sealed class LoadingScreen : MonoBehaviour
{
    [SerializeField] private EnemyPool _pool;   // _initOnAwake снят в инспекторе

    private async void Start()
    {
        await _pool.InitAsync(512);
    }
}
```

`MonoEntityPool.InitAsync` (``MonoEntityPool`1.cs:112-129``) — правильный прогрев по пропускной способности: он идёт через `MonoEntity.CreateAsync` → `Object.InstantiateAsync(prefab, count, parent)`, который Unity размазывает по кадрам, в отличие от синхронного `Init(n)`, инстанцирующего n префабов в одном кадре. `ValueTask` избегает аллокации `Task`. Его тело содержит обе нужные проверки: бросает на `null`-префабе и выходит на `initialCount <= 0`.

Аналог у `PrefabEntityPool` (``PrefabEntityPool`1.cs:95-111``) **слабее и сломан**: ни проверки префаба на `null`, ни отсечки `initialCount <= 0` (то есть `Object.InstantiateAsync(prefab, 0, …)` действительно вызывается), и инстанцирует он в `_container` — общий корень пула, — а не в `pool.container`, который сам же только что создал, в отличие от `Init`/`CreateEntity`. Плюс он может выполниться до того, как `Awake` присвоит `_container`, и тогда там `null`. Если вам нужен асинхронный прогрев мульти-префабного пула — переопределяйте или пишите свой.

Напоминание: `InitAsync` нет ни в `IEntityPool<TEntity>`, ни в `IMultiEntityPool<TKey, TEntity>`. У `IPrefabEntityPool<E, in P>` он есть. Чтобы дотянуться до асинхронного прогрева одноархетипного пула, нужно держать конкретный тип.

### 14. Рантайм-реестр фабрик, которого нет в поставке

`IMultiEntityFactory<TKey, TEntity, TArgs>` имеет только `Create`/`TryCreate`/`Contains`, и единственная поставляемая реализация — `ScriptableEntityCatalog`, авторируемый перетаскиванием ассетов. Если проект зависел от регистрации фабрик в рантайме, пишите своё; интерфейс — стабильный контракт, реализация — тридцать строк:

```csharp
public sealed class EntityFactoryRegistry<TKey, TEntity, TArgs>
    : IMultiEntityFactory<TKey, TEntity, TArgs>
    where TEntity : IEntity
    where TArgs : IArgs
{
    private readonly Dictionary<TKey, IEntityFactory<TEntity, TArgs>> _factories;

    public EntityFactoryRegistry(int capacity = 0, IEqualityComparer<TKey> comparer = null) =>
        _factories = new Dictionary<TKey, IEntityFactory<TEntity, TArgs>>(capacity, comparer);

    public void Register(TKey key, IEntityFactory<TEntity, TArgs> factory) =>
        _factories.Add(key, factory);

    public bool Unregister(TKey key) => _factories.Remove(key);

    public TEntity Create(TKey key, TArgs args) => _factories[key].Create(args);

    public bool TryCreate(TKey key, TArgs args, out TEntity entity)
    {
        if (_factories.TryGetValue(key, out IEntityFactory<TEntity, TArgs> factory))
        {
            entity = factory.Create(args);
            return true;
        }

        entity = default;
        return false;
    }

    public bool Contains(TKey key) => _factories.ContainsKey(key);
}
```

Правильная форма по двум причинам. Первая: конструктор принимает `IEqualityComparer<TKey>`, чего `ScriptableEntityCatalog` и `MultiEntityPool` не дают — а именно это нужно, чтобы enum-ключ не уходил на боксирующий компаратор под IL2CPP (`new EntityFactoryRegistry<EnemyType, …>(8, EnemyTypeComparer.Instance)` с вашей структурой-компаратором). Вторая: тип реализует тот же интерфейс, что и каталог, поэтому `MultiEntityPool<TKey, TEntity, TArgs>` принимает его без изменений — пул ничего не знает про источник фабрик.

В дереве уже есть прецедент такой регистрации: `EntityViewPool<K, E, V>.Register(K, V)` / `Unregister(K)` / `Register(EntityViewCatalog<E, V>)` в ``UI/Pool/EntityViewPool`1.cs``. Если решите писать своё — посмотрите, как там устроена подача из каталога и `ProfilerMarker` вокруг `Rent`/`Return`.

### 15. Юнит-тест спавн-кода через тестовый двойник

```csharp
[Test]
public void Fire_RentsFromPool_AndSetsDamage()
{
    var bullet = new BulletEntity();
    var rented = new List<BulletEntity>();

    var pool = new EntityPoolTestDouble<BulletEntity>
    {
        RentMethod = () => { rented.Add(bullet); return bullet; },
        ReturnMethod = _ => { },
        InitMethod = _ => { },
        DisposeMethod = () => { }
    };

    var weapon = new WeaponFire(pool);
    weapon.Fire(42);

    Assert.AreEqual(1, rented.Count);
    Assert.AreEqual(42, bullet.GetValue<int>("Damage"));
}
```

Правильная форма потому, что `WeaponFire` зависит от `IEntityPool<BulletEntity>`, а не от конкретного пула, — и это единственный живой тестовый шов в области: все остальные фабричные и пулевые двойники в репозитории закомментированы. Назначайте все четыре делегата: каждый член двойника безусловно делает `Invoke`, незаданный делегат даст `NullReferenceException`.

## Производительность

Помечаю источник каждого утверждения: **[код]** — прочитано в указанном файле, **[измерено]** — измерено в ходе верификации против исходника, **[док]** — утверждение документации или XML-комментария, **[вывод]** — следствие, не измеренное.

**Запекание ёмкостей — главная оптимизация фабрик.** **[код]** `ScriptableEntityFactory<TEntity, TArgs>` сериализует `tagCapacity`/`valueCapacity`/`behaviourCapacity` под заголовком `Optimization` с тултипами «Initial number of … to assign to the entity»; `autoCompile = true` гонит `Compile()` из `OnValidate`; `Compile()` — `private protected virtual`, только под `#if UNITY_EDITOR`, выходит на `EditorApplication.isPlaying`, вызывает `this.Create(default)` и читает `TagCount`/`ValueCount`/`BehaviourCount` у превью-сущности. Атрибуты — `[ContextMenu(nameof(Compile))]` плюс Odin `[Button]`. **[код]** Эффект виден в `Entity`: `ConstructTags`/`ConstructValues` делают `_tagCapacity = CeilToPrime(capacity, …)`, `new TagSlot[_tagCapacity]`, `new int[_tagCapacity]` + `Array.Fill(buckets, -1)` (`Entity_Tags.cs:43`, `Entity_Values.cs:56`), а `ConstructBehaviours` — просто `new IEntityBehaviour[capacity]` (`Entity_Behaviours.cs:344`). **[вывод]** Запечённое точное число не даёт массив ровно такого размера для тегов и значений — оно округляется вверх до простого; выигрыш в том, что роста и перехеширования не происходит вовсе, а не в экономии байт.

**`TArgs` на структурах не боксируется на пути `Create`.** **[код]** `IEntityFactory<out TEntity, in TArgs>` объявляет `in TArgs`, реализации — структуры `NoArgs`/`Args<T…>`, вызов `factory.Create(_args)` идёт по обобщённому интерфейсу. **[измерено]** Единственное подозрительное место — `_args = args ?? throw new ArgumentNullException(nameof(args))` в конструкторах обоих core-пулов (``EntityPool`1.cs:53``, ``MultiEntityPool`1.cs:55``). Этот шаблон компилируется (`??` на параметре типа, ограниченном только интерфейсом, — валидный C#) и стоит ноль байт: замер `GC.GetAllocatedBytesForCurrentThread` на 1000 конструирований дал 24 байта на экземпляр и с проверкой, и без — `box`+`brtrue` элидируется на CoreCLR. **[вывод]** Под IL2CPP IL-боксирование может выжить, но это один раз на конструирование пула, никогда не на `Rent`. **[код]** Настоящий дефект тут другой: для структурного `TArgs` условие никогда не истинно, значит задокументированный в `<exception>` `ArgumentNullException` для `args` — мёртвый код, а сам XML-комментарий лжёт (``EntityPool`1.cs:49``: «Thrown if `factory` or `args` is null»; в ``MultiEntityPool`1.cs:51`` упомянут только `factory`).

**Горячие пути `EntityPool` — O(1) и без аллокаций.** **[код]** `Rent()` = `Stack<TEntity>.TryPop` + `HashSet<TEntity>.Add` + один невстраиваемый виртуальный `OnRent`. `Return()` = `HashSet.Remove` + `OnReturn` + `Stack.Push`. Никаких `in`/`ref` нигде нет, но `TEntity` — ссылочный тип, так что это неважно.

**Раскладка хранилищ, по файлам.** **[код]** ``EntityPool`1`` — `Stack<TEntity>` (на массиве, LIFO, дружественно к кэшу) плюс `HashSet<TEntity>`. ``MultiEntityPool`1`` — `Dictionary<TKey, Stack<TEntity>>` плюс обратный `Dictionary<TEntity, TKey>` плюс `Dictionary<TKey, int> _rentedCounts`. ``PrefabEntityPool`1`` — `Dictionary<string, Pool>` плюс `Dictionary<string, int>`. `ScriptableEntityCatalog` — авторируемый `F[] _factories`, один раз сплющиваемый в `new Dictionary<K, F>(_factories?.Length ?? 0)`, то есть предразмеренный, без перехеширования на сборке.

**Ни один пул в `Pooling/` не задаёт начальную ёмкость `Stack`.** **[код]** `new Stack<TEntity>()` стартует с нулевой ёмкости и растёт удвоением. **[вывод]** Прогрев на 1000 сущностей выполняет порядка десяти переаллокаций и копирований массива. **[код]** Передать ёмкость — бесплатный выигрыш, и это именно тот шаблон, который уже применяет UI-пул: `new Stack<V>(initialCapacity)` в ``EntityViewPool`1.GetOrCreatePool`` при `initialCapacity = 32` по умолчанию.

**Аудит `AggressiveInlining`, точно.** **[код]** ``PrefabEntityPool`1`` — на `OnCreate`/`OnRent`/`OnReturn`/`OnDispose`/`GetEntityName`/`CreatePool`/`CreateEntity`. ``MonoEntityPool`1`` — **только** на приватном `CreateEntity`, на `Create<TPool>` и на `Destroy`; четыре хука атрибута не имеют. ``EntityPool`1`` и ``MultiEntityPool`1`` — `MethodImpl` нет вообще. `MonoEntity.Create` (все перегрузки), `CreateAsync`, `Cast`, `TryCast`, `Destroy` — все несут атрибут. **[вывод]** Держите переопределения хуков маленькими и объявляйте свои пулы `sealed`, иначе виртуальный вызов не девиртуализуется и атрибут не значит ничего.

**`PrefabEntityPool.Pool` — структура, хранимая по значению.** **[код]** `internal struct Pool { Stack<P> stack; Transform container; GameObject go; }` в `Dictionary<string, Pool>`. Каждый `TryGetValue` копирует структуру из трёх ссылок. Мутация-копией безвредна только потому, что `stack`/`container` — ссылки, — но ровно поэтому поле `go` невозможно починить после факта: `CreatePool` собирает возвращаемую структуру **без** него (``PrefabEntityPool`1.cs:303``), а обе перегрузки `Dispose` зовут `Destroy(pool.go)` по этому всегда-`null` полю. Контейнерные `GameObject` с именами `<Names>` не уничтожаются никогда.

**Строковая цена каждой аренды в `PrefabEntityPool`.** **[код]** `GetEntityName` = геттер `GameObject.name` (аллокация строки из нативного кода) + `Regex.Replace` (поиск в статическом кэше Regex, `Match`, строка-результат) + `.Trim()` (ещё строка). Вызовов: 1 в `Rent` (строка 122), ещё 1 в `Expand` (строка 153), и по одному на каждую созданную сущность в `CreateEntities` (строка 179); плюс 1 в `Init`, 1 в `InitAsync`, 1 в `Return` (строка 188), 1 в `Dispose(prefab)` (строка 214). **[вывод]** Промах с `ExpandByDoubling`, создающий N сущностей, выполняет `2 + N` аллоцирующих вызовов в одном кадре. `GetEntityName` — `protected virtual`: переопределите его на кэшированный ключ (например, поле на вашем типе сущности или `prefab.GetInstanceID().ToString()`, посчитанный один раз) — это прямой выигрыш и единственная санкционированная точка вмешательства.

**`Stack<T>.Contains` — линейный проход, и в двух пулах по-разному.** **[код]** В `MultiEntityPool.Return` он идёт после `_rentEntities.Remove(entity, out key)`, который уже доказал факт аренды (``MultiEntityPool`1.cs:132-142``), — там он действительно избыточен. В `PrefabEntityPool.Return` (строка 196) он **несущий**: этот класс не ведёт множество арендованных вообще, и `pool.stack.Contains(sceneEntity)` — единственная защита от двойного возврата; на ней держится живой тест `Return_Twice_SecondIgnored` в `Tests/PlayMode/SceneEntityPool/PrefabEntityPoolTests.cs`. **[вывод]** Убрать его там нельзя; заменить можно только на `HashSet<P>` арендованных, а не на ничто — иначе вернётся тот самый баг с дубликатами.

**`MultiEntityPool.Rent` — лишние просмотры словарей.** **[код]** `_rentEntities.Add(entity, key)` — бросающий `Add`, не `TryAdd`; `_rentedCounts.TryAdd(key, 1)`, иначе `_rentedCounts[key]++` — два просмотра на пути инкремента. **[код]** Все словари построены с компаратором по умолчанию, и класс не предоставляет конструктора с компаратором. **[вывод]** Enum-ключ уйдёт на боксирующий `ObjectEqualityComparer` под IL2CPP/AOT. Предпочитайте `int` или `string` для ключей пулов в шиппинг-билде, либо свой подкласс/свою реализацию (идиома 14). **[код]** Для `Dictionary<TEntity, TKey>` ситуация мягче: `Entity.GetHashCode()` возвращает `_instanceId`, а `Equals(object)` — `obj is IEntity other && other.InstanceID == _instanceId` (`Entity.cs:160`, `:170`), так что не-generic путь `Equals` не боксирует, но делает интерфейсное приведение и интерфейсный вызов свойства на каждую пробу.

**Значения сущности боксируются всегда, когда они структуры.** **[код]** `IEntity` объявляет `void AddValue(int key, object value)` и `void SetValue(int key, object value)` (`Entities/Interface/IEntity_Values.cs:72`, `:62` соответственно), а `ValueSlot` хранит `public object value` (`Entity_Values.cs:41`). Все строковые и `ValueKey<T>`-перегрузки — расширения, которые в итоге зовут эти же два метода (`Extensions_Values.cs:16-26`, `:160-169`). **[вывод]** `entity.AddValue("Health", 200)` и `entity.SetValue(new ValueKey<int>("Health"), 200)` боксируют одинаково. Для пулинга это важно так: фабрика добавляет значения **один раз на экземпляр**, то есть боксирование происходит при прогреве, а не на спавне; но если место аренды делает `SetValue` структурой на каждый `Rent`, бокс будет на каждый спавн. Читается всё без бокса — `GetValue<T>(int key)` обобщённый.

**Асинхронный прогрев.** **[код]** `MonoEntity.CreateAsync` — `[AggressiveInlining] public static async ValueTask<E[]> CreateAsync<E>(E prefab, int count, Transform parent = null)`, оборачивает `InstantiateAsync(prefab, count, parent)`, ждёт `AsyncInstantiateOperation<E>`, затем прогоняет `SetupAfterCreation` по каждому экземпляру. **[док]** Пакетная, размазанная по кадрам инстанциация — это свойство `Object.InstantiateAsync`. **[вывод]** `ValueTask` вместо `Task` экономит аллокацию на синхронно завершённом пути.

**Трансформная работа минимизирована намеренно, но только на одном пути.** **[код]** На попадании в пул `Rent` делает `tf.SetParent(parent, false)` (без пересчёта мировой матрицы) и один `tf.SetPositionAndRotation(position, rotation)` — один переход в нативный код вместо двух записей свойств. **[код]** На промахе ветка `Expand` делает только `entity.name = name` и `entity.transform.SetPositionAndRotation(...)` — `SetParent` там **нет** совсем (``PrefabEntityPool`1.cs:136-141``). **[вывод]** Первая аренда префаба (созданного по требованию под `pool.container`) оказывается привязанной иначе, чем каждая последующая аренда того же префаба. Та же асимметрия на пути `ExpandByDoubling`.

**`in CreateArgs`.** **[код]** Используется и на `MonoEntityPool.Create(in CreateArgs)`, и на `MonoEntityPool<E, P>.Create<TPool>(in CreateArgs)` — защитной копии структуры из пяти полей нет. `Create<TPool>` деактивирует хост-`GameObject`, делает `AddComponent`, присваивает `_prefab`/`_container`/`_initOnAwake`/`_initialCount` и активирует обратно, так что `Awake` видит поля. **[код]** Подтверждено, что он не присваивает ни `_dontDestroyOnLoad`, ни `_expandMode`: в `CreateArgs` для них нет полей.

**Перебор коллекций.** **[код]** `EntityPool.Dispose` перебирает `Stack<TEntity>` и `HashSet<TEntity>` по конкретным типам — структурные перечислители, без аллокаций. `MultiEntityPool.Dispose` перебирает `Dictionary<TKey, Stack<TEntity>>` и `_rentEntities.Keys` — тоже структурные. `PrefabEntityPool.Dispose` перебирает `Dictionary<string, Pool>` структурным перечислителем, но копирует `Pool` на каждой итерации. **[код]** А вот `ScriptableEntityCatalog.GetEnumerator()` возвращает `_factoryMap.GetEnumerator()` как `IEnumerator<KeyValuePair<K, F>>` — структурный `Dictionary<K, F>.Enumerator` боксируется на каждом вызове; `Keys`/`Values` отдаются как `IEnumerable<…>`, так что `foreach` по ним тоже боксирует их структурные перечислители. **[вывод]** Каталог нельзя перебирать покадрово через `IReadOnlyDictionary`; если очень нужно — храните свой плоский массив, построенный один раз.

**Ленивая инициализация каталога.** **[код]** Каждый публичный член (`Count`, `Keys`, `Values`, индексатор, `ContainsKey`, `TryGetValue`, `GetEnumerator`, `Create`, `TryCreate`, `Contains`) первым делом зовёт `EnsureInitialized()` — одна проверка на `null` на тёплом пути и полная сборка словаря на первом касании после перезагрузки домена.

**``EntityViewPool`1`` — единственный инструментированный пул в дереве.** **[код]** Оборачивает `Rent` и `Return` в `using (s_rentMarker.Auto())` / `s_returnMarker.Auto()` со статическими `ProfilerMarker`, и предразмеряет каждый `Stack<V>`. Пулы в `Pooling/` не инструментированы никак.

**Тестов производительности для этой области нет.** **[код]** `Tests/Performance` содержит только `Collection/`, `Common/GameObject_Performance.cs` и `Entity/` — ни одного теста по фабрикам или пулам. Любая цифра по этим типам в вашем проекте — ваша собственная.

## Грабли

- **Документация описывает другой API.** Каждый тип фабрики и каталога в исходниках несёт `TArgs : IArgs`, которого нет в `Docs/Entities/Factories` и `Docs/Entities/Pooling`. Относитесь к документации как к концептуальному описанию, а к исходнику — как к API, и сверяйте каждую подпись перед написанием миграционного кода.

- **Типы, которые документация описывает, а исходник не содержит:** `InlineEntityFactory`, `InlineEntityFactory<E>`, `MultiEntityFactory`, `MultiEntityFactory<K, E>` (с `Register`/`Unregister`), а также версии `IEntityFactory`/`ScriptableEntityFactory`/`EntityPool`/`MultiEntityPool`/`ScriptableEntityCatalog` вообще без аргументов типа. `MultiEntityFactory` упоминается только внутри полностью закомментированных файлов `Tests/EditMode/Factory/MultiEntityFactoryTests.cs` и `Tests/Substitutions/Factory/MultiEntityFactoryStub.cs`, а также в столь же закомментированных `Tests/EditMode/Pool/MultiEntityPoolTests.cs` и `Tests/PlayMode/ScriptableEntityFactoryCatalog/ScriptableEntityFactoryCatalogTests.cs`.

- **Почти весь тест-сьют области закомментирован построчно.** Закомментированы: `Tests/EditMode/Pool/EntityPoolTests.cs` (304 строки), `Tests/EditMode/Pool/MultiEntityPoolTests.cs` (141), `Tests/EditMode/Factory/MultiEntityFactoryTests.cs` (144), `Tests/PlayMode/ScriptableEntityFactoryCatalog/ScriptableEntityFactoryCatalogTests.cs`, `Tests/PlayMode/EntityViewPool/EntityViewPoolTests.cs`, `Tests/PlayMode/EntityViewCatalog/EntityViewCatalogTests.cs` и все пять двойников `Tests/Substitutions/Factory/*.cs`. Живых файлов три: `Tests/PlayMode/SceneEntityPool/PrefabEntityPoolTests.cs`, `Tests/PlayMode/SceneEntityPool/SceneEntityPoolTests.cs` (класс фикстуры назван `MonoEntityPoolTests`) и `Tests/Substitutions/Pools/EntityPoolTestDouble{,`1}.cs`. То есть у `EntityPool`, `MultiEntityPool` и `ScriptableEntityCatalog` **нулевое исполняемое тестовое покрытие**.

- **`PrefabEntityPool.Rent(prefab, parent)` не привязывает к родителю.** `public E Rent(P prefab, Transform parent) => this.Rent(prefab, parent.position, parent.rotation);` — аргумент `parent` не передаётся, поэтому у настоящей реализации он `null`, и `SetParent(null, false)` отвязывает сущность в корень сцены, скопировав трансформ предполагаемого родителя. Пользуйтесь только четырёхаргументной перегрузкой.

- **Первая аренда префаба привязана иначе, чем все последующие.** Ветка попадания в пул делает `SetParent(parent, false)` + `SetPositionAndRotation`; ветка `Expand` — только `name` и `SetPositionAndRotation`, без `SetParent`.

- **`PrefabEntityPool` вообще не отслеживает аренду.** `Return` восстанавливает ключ из имени `GameObject` и, не найдя пула, создаёт его на лету и пушит туда сущность. Возврат чужой сущности или сущности, чей `GameObject` переименовали после аренды, молча кладёт её в неправильный (возможно, только что созданный) пул. `_rentedCounts` уменьшается только если запись уже есть.

- **Контейнерные `GameObject` в `PrefabEntityPool` утекают.** `CreatePool` никогда не присваивает `Pool.go`, а `Dispose(prefab)` и `Dispose()` зовут `Destroy(pool.go)` по этому `null`. Объекты `<Names>` не уничтожаются.

- **`PrefabEntityPool.InitAsync` небезопасен.** Нет проверки префаба на `null`, нет отсечки `initialCount <= 0`, инстанцирует в общий `_container` вместо `pool.container`, и может выполниться раньше, чем `Awake` присвоит `_container`.

- **`MonoEntityPool._acceptExternalReturns` по умолчанию `true` и недоступен из кода.** При `true` `Return` принимает и пулит сущности, которые пул никогда не выдавал (условие — `_rentEntities.Remove(sceneEntity) || _acceptExternalReturns`), так что предупреждение «Attempted to return untracked entity» при дефолтных настройках недостижимо. Двойной возврат одной сущности пушит её в стек дважды, и две последующие аренды выдадут один и тот же экземпляр. Поле объявлено `internal`, поэтому снять его из игровой сборки кодом нельзя — только галочкой в инспекторе.

- **`MonoEntityPool.Return` и `PrefabEntityPool.Return` идут через `MonoEntity.Cast<P>`, который бросает.** `InvalidCastException` на любой сущности, которая не `P` и не `MonoEntityProxy<P>`. В своём коде используйте `MonoEntity.TryCast`.

- **`Create<TPool>(in CreateArgs)` не копирует `_dontDestroyOnLoad` и `_expandMode`.** Пул, созданный в рантайме, всегда `ExpandByOne` и умирает при смене сцены; полей для этого в `CreateArgs` нет.

- **`Dispose()` — это снос, а не «очистить список свободных».** `MonoEntityPool.Dispose()` уничтожает и лежащие в пуле, и арендованные сущности; `EntityPool.Dispose()` прогоняет `OnDispose` по обоим множествам. Всё, что ещё держит ссылку на арендованную сущность, получит уничтоженный объект.

- **`EntityPool.Dispose()` не вызывает `IDisposable.Dispose()` у сущностей.** Он только дёргает хук `OnDispose` (пустой по умолчанию) и чистит коллекции. `IEntity` наследует `IDisposable` через `IInitSource`, а `Entity.Dispose()` снимает сущность с `EntityRegistry` и, при `Settings.disposeValues == true`, освобождает значения (`Entity_Lifecycle.cs:243-261`). Без переопределения `OnDispose` на `entity.Dispose()` ваши сущности останутся в глобальном реестре, а их значения не будут освобождены.

- **Прогрев пула чистых `Entity` навсегда увеличивает глобальный реестр.** Каждый конструктор `Entity` заканчивается `EntityRegistry.Instance.Register(this)`, а `Register`/`Unregister` помечены `internal` — снять сущность можно только через `Entity.Dispose()`.

- **`ExpandByDoubling` считается от арендованного количества, а не от лежащего в пуле.** В `EntityPool` — от `_rentEntities.Count`, в `MultiEntityPool`/`PrefabEntityPool` — от отдельно поддерживаемого `_rentedCounts[key]`. Если сущности арендовали и не вернули, оценка роста уползает; если не арендовали ни разу, режим падает до 1, то есть становится `ExpandByOne`. При этом XML-комментарий на `Pooling/ExpandMode.cs` утверждает «creates new entities equal to the current **pooled** count, effectively doubling the inventory (e.g. 10 → 20)», и то же написано в тултипах инспектора у обоих Unity-пулов. Это ровно то, что читатель видит при наведении курсора, и это неверно.

- **`Docs/Entities/Pooling/ExpandMode.md` проставляет членам явные значения `0``/`1``/`2`.** В исходнике явных значений нет. Порядок совпадает, но не персистите эти значения как `int` через апгрейд фреймворка.

- **`MultiEntityPool` не компилируется вне Unity.** ``MultiEntityPool`1.cs:3`` содержит безусловный `using UnityEngine;` без `#if UNITY_5_3_OR_NEWER` (и ничего из `UnityEngine` не использует), в отличие от ``EntityPool`1.cs``, где этот using закрыт директивой.

- **`ScriptableEntityCatalog<TArgs>` абстрактный, но помечен `[CreateAssetMenu]`.** Пункт меню `Atomic/Entities/MultiEntityFactory` не может создать пригодный ассет. Объявите конкретный закрытый подкласс и повесьте `CreateAssetMenu` на него.

- **Ленивая инициализация каталога прячет ошибки авторинга.** Дубликат ключа пишет `Debug.LogWarning` и **последняя** фабрика побеждает; `null`-элемент массива пропускается молча. С Odin обе проблемы ловятся на этапе авторинга через `[ValidateInput(nameof(ValidateFactories), …)]`; без Odin остаётся только рантайм-варнинг. Включайте путь Odin или пишите свой редакторный валидатор.

- **Поля ёмкостей `ScriptableEntityFactory` приватные.** Это `tagCapacity`/`valueCapacity`/`behaviourCapacity`, а не `protected int initialTagCount`, как в документации. Примеры из документации, обращающиеся к `this.initialTagCount` или `this.initialTagCapacity`, не компилируются. Ёмкости приходят параметрами пятипараметровой `protected` перегрузки `Create`.

- **`Compile()` вызывает `this.Create(default)` при каждом `OnValidate`.** `autoCompile` по умолчанию `true`. Для ссылочного `TArgs` это `null`; любая логика конструирования, разыменовывающая `args`, будет падать в редакторе, а `catch` только напишет красное в консоль. Держите арг-зависимую логику вне пути конструирования или снимайте `autoCompile`.

- **`Install(IEntity)` не получает `args`.** Арг-зависимый архетип обязан переопределять пятипараметровый `Create`, а не `Install`.

- **`public TEntity Create(TArgs args)` в базе не `sealed`.** ``ScriptableEntityFactory`1.cs:82`` объявляет его обычным не-виртуальным методом; ничто не мешает наследнику перекрыть его через `new` и обойти запекание ёмкостей. Практический совет — переопределять пятипараметровую перегрузку — остаётся верным, но язык его не навязывает.

- **Ключ строкового каталога — `factory.Name`, а не имя файла ассета.** `<returns>` прямо над этой строкой (`ScriptableEntityCatalog.cs:24-27`) утверждает «Uses the factory's asset name as the key» / «The name of the factory asset». Неверно и в документации, и в самом исходнике. Старый код, опиравшийся на имена файлов, должен заполнить сериализованное `Name`.

- **`MonoEntity.Create<E>(in CreateArgs)` зажимает ёмкости через `Mathf.Max(1, …)`** — запечённый нуль молча становится единицей, — и сам не вызывает `Install()`: установка произойдёт только в `Awake` и только при `args.installOnAwake == true`, а в инициализаторе структуры это поле по умолчанию `false`.

- **Аренда и возврат пулированной `MonoEntity` — это ещё и подписка/отписка от тика.** `SetActive` дёргает `OnEnable`/`OnDisable`, которые при `useUnityLifecycle && _started` вызывают `Enable()`/`Disable()` и регистрируют/снимают сущность в `TickableManager.Instance` (`MonoEntity_UnityLifecycle.cs:22-48`). Если ваш хук `OnRent` ожидает, что поведения уже тикают, учтите порядок: пул вызывает `OnRent` **после** того, как снял сущность со стека, то есть `SetActive(true)` происходит внутри самого хука.

- **Асинхронный прогрев недостижим через интерфейсы одноархетипных пулов.** `IEntityPool<TEntity>` не объявляет `InitAsync`, хотя `MonoEntityPool` его реализует. `IPrefabEntityPool<E, in P>` объявляет. Чтобы дотянуться до `MonoEntityPool.InitAsync`, нужно держать конкретный тип.

- **Код требует современного профиля .NET.** `Stack<T>.TryPop`, `Dictionary.TryAdd` и `Dictionary.Remove(key, out value)` используются по всему разделу — это .NET Standard 2.1 / Unity 2021+. На более старом рантайме скриптинга код не скомпилируется.

- **Метода `Set` не существует.** `bullet.Set("Damage", 10)` из `Docs/BestPractices/UsingEntityPoolWithFactories.md` не компилируется — расширения называются `SetValue`. Не компилируется и `entity.AddValue<int>("Health", 100)` из `Docs/Entities/Factories/Manual.md`: строковая перегрузка — негенерик `AddValue(this IEntity, string, object)`, а неявного приведения `string` → `ValueKey<T>` нет.

- **Разметка документации в разделе Pooling местами битая.** Лишние ограждения кода в ``EntityPool`1.md``, анкоры вида `destroyMonoEntitypool-float` для `#### Destroy(MonoEntityPool, float)`. Косметика, но признак того, что страницы не перегенерировали вместе с исходником.

- **Путь к исходникам фабрик — `Factories`, а не `Factory`.** Это `Assets/Plugins/Atomic/Entities/Scripts/Factories`; каталога `Factory` не существует.

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Документация: Docs/Entities/Factories

Весь каталог написан под API **без** `TArgs`. Читайте его ради намерения, а подписи берите из исходника.

- `Docs/Entities/Factories/Manual.md` — обзорная страница раздела. Три примера: `InlineEntityFactory` (тип отсутствует в исходнике), `ScriptableEntityFactory<E>` с беспараметровым `override Create()` (такой перегрузки нет), и секция «3️⃣ ScriptableEntityCatalog», которая на самом деле показывает `MultiEntityFactory` и заканчивается строкой `registry.Create("Orc")` против переменной с именем `multiFactory`. Ниже — оглавление всех типов раздела и ссылки на четыре страницы Best Practices. Снипеты не копировать.
- `Docs/Entities/Factories/IEntityFactory.md` — неgeneric интерфейс фабрики: `Create()` без аргументов. В исходнике это `IEntityFactory<in TArgs> : IEntityFactory<IEntity, TArgs>` с `Create(TArgs)`.
- ``Docs/Entities/Factories/IEntityFactory`1.md`` — то же для `IEntityFactory<out E>`. Полезна описанием ковариантности; подпись устарела.
- `Docs/Entities/Factories/IMultiEntityFactory.md` — строково-ключевой вариант: `Create(string)`, `TryCreate(string, out IEntity)`, `Contains(string)`. Три метода совпадают с исходником по смыслу, но в исходнике все принимают ещё `TArgs`.
- ``Docs/Entities/Factories/IMultiEntityFactory`1.md`` — то же для `<K, E>`: `Create(K)`, `TryCreate(K, out E)`, `Contains(K)`. Самая близкая к истине страница по этому интерфейсу.
- `Docs/Entities/Factories/InlineEntityFactory.md`, ``Docs/Entities/Factories/InlineEntityFactory`1.md`` — страницы лямбда-фабрики: конструктор, принимающий `Func<IEntity>`, и `Create()`. **Типа в исходниках нет.** Если он был в проекте — замените своей реализацией `IEntityFactory<TEntity, TArgs>` на делегате.
- `Docs/Entities/Factories/MultiEntityFactory.md`, ``Docs/Entities/Factories/MultiEntityFactory`1.md`` — единственное описание реестра фабрик с рантайм-регистрацией: четыре конструктора (пустой, из словаря, из `IEnumerable`, из `params`), `Register`, `Unregister`, `Create`, `TryCreate`, `Contains`. **Классов в исходниках нет.** Читайте как спецификацию для своей реализации (идиома 14) — набор конструкторов там разумный.
- `Docs/Entities/Factories/ScriptableEntityFactory.md` — неgeneric фабрика-ассет. Разделы «Inspector Settings» (три ёмкости), «Context Menu» (пункт назван `Precompile`; в исходнике атрибут — `[ContextMenu(nameof(Compile))]`), и описания `Create()`, `Install(IEntity)`, `OnValidate()`, `Reset()`. Поля описаны как `initialTagCapacity`/`initialValueCapacity`/`initialBehaviourCapacity` — в исходнике они приватные и называются иначе. Самая полезная часть — объяснение смысла `Install`.
- ``Docs/Entities/Factories/ScriptableEntityFactory`1.md`` — то же для `<E>`: `Create()`, `OnValidate()`, `Reset()`, те же три поля. Содержит прямое объяснение, зачем нужны запечённые ёмкости; это единственное место, где намерение сформулировано словами.
- `Docs/Entities/Factories/ScriptableEntityCatalog.md` — строковый каталог. Пошаговый сценарий авторинга: создать ассет, завести несколько фабрик, перетащить их в массив, пользоваться. Описывает `Create(string)`, `TryCreate`, `Contains`, `GetKey(ScriptableEntityFactory)` и настройки инспектора. Утверждает, что ключ — имя файла ассета; на самом деле `factory.Name`.
- ``Docs/Entities/Factories/ScriptableEntityCatalog`1.md`` — самая содержательная страница раздела: семь шагов полного примера с enum-типами врагов, базовой фабрикой с `sealed override Create()` и `protected abstract Install`, несколькими листовыми фабриками и каталогом с `GetKey(F)`. Структура примера верна и воспроизведена в идиомах 3-4 этой главы; подписи — на три параметра типа вместо четырёх.

### Документация: Docs/Entities/Pooling

- `Docs/Entities/Pooling/Manual.md` — обзор. Три примера («Entity Pool», «Multi-Entity Pool», «Prefab Entity Pool»), затем оглавление по трём группам (`SinglePools`, `MultiPools`, `PrefabPools`) и секция «Notes», которая коротко формулирует, когда брать какое семейство. Единственное место, где три семейства противопоставлены в одном абзаце. Четвёртое семейство (`EntityViewPool`) здесь не упомянуто.
- `Docs/Entities/Pooling/ExpandMode.md` — перечисление с явно проставленными значениями `0``/`1``/`2` (в исходнике значений нет) и по строке «Growth» на каждый член: Linear / Exponential / None. Описание `ExpandByDoubling` считает рост от **pooled** count — реализации считают от rented.
- `Docs/Entities/Pooling/IEntityPool.md`, ``Docs/Entities/Pooling/IEntityPool`1.md`` — контракт одноархетипного пула: `Rent()`, `Return()`, `Init(int)`, `Dispose()`. Единственное расхождение — `Dispose()` перечислен как член самого интерфейса, тогда как он приходит из `IDisposable`. По существу актуальны.
- `Docs/Entities/Pooling/EntityPool.md`, ``Docs/Entities/Pooling/EntityPool`1.md`` — чисто-C# пул: конструктор, `Init`, `Rent`, `Return`, `Dispose` и все четыре хука с описанием момента вызова. Описания хуков — лучшее, что есть по их семантике. Конструктор на страницах неgeneric-версии показан без `TArgs` (`EntityPool(IEntityFactory<IEntity>, ExpandMode)`); на ``EntityPool`1.md`` подпись уже с `TArgs`. В ``EntityPool`1.md`` есть лишние ограждения кода.
- `Docs/Entities/Pooling/IMultiEntityPool.md`, ``Docs/Entities/Pooling/IMultiEntityPool`1.md`` — контракт ключевого пула: `Init(K, int)`, `Rent(K)`, `Return(E)`. Актуальны: эти интерфейсы `TArgs` не получали. Объясняют, почему `Return` не требует ключа.
- `Docs/Entities/Pooling/MultiEntityPool.md`, ``Docs/Entities/Pooling/MultiEntityPool`1.md`` — реализация ключевого пула: конструктор, `Init`, `Rent`, `Return`, `Dispose`, четыре хука. Конструктор показан без `TArgs` (`MultiEntityPool(IMultiEntityFactory<K, E>, ExpandMode)`).
- `Docs/Entities/Pooling/IPrefabEntityPool.md`, ``Docs/Entities/Pooling/IPrefabEntityPool`1.md`` — контракт мульти-префабного пула. Важны по двум причинам: здесь описаны все три перегрузки `Rent` (включая ту, что сломана: «Rents an entity instance and parents it under the specified transform»), и здесь же видно, что `InitAsync` в документации **не описан**, хотя в интерфейсе он есть. В документации один параметр типа вместо двух.
- `Docs/Entities/Pooling/MonoEntityPool.md` — самая подробная страница по сценному пулу: два сценария («Basic Usage» через компонент в инспекторе и «Runtime Creation» через `CreateArgs`), таблица настроек инспектора, затем `Rent`, `Return`, `Init`, `Dispose`, четыре хука, `Awake`, `Reset`, `Create(CreateArgs)`, `Destroy(MonoEntityPool, float)` и вложенный тип `CreateArgs`. Единственное место, где описаны поля инспектора. `_acceptExternalReturns` и `InitAsync` не упомянуты. Анкоры вида `destroyMonoEntitypool-float` битые.
- ``Docs/Entities/Pooling/MonoEntityPool`1.md`` — то же для generic-версии, плюс описан приватный `CreateEntity()` и `Create<T>(CreateArgs)`. Пример на три шага: объявить `EnemyPool`, повесить компонент, пользоваться. Параметр типа один (`MonoEntityPool<E> where E : MonoEntity`) вместо двух.
- `Docs/Entities/Pooling/PrefabEntityPool.md` — мульти-префабный пул: как повесить компонент, таблица инспектора, затем `Awake`, `Init`, все три `Rent`, `Return`, `Dispose(prefab)`, `Dispose()`, четыре хука и **`GetEntityName(MonoEntity)`**. Последнее — ключ ко всему классу: именно эта страница объясняет, что ключ пула получается из имени `GameObject` стрижкой суффикса `" (1)"`. Заканчивается секцией «Notes». `InitAsync` не описан.
- ``Docs/Entities/Pooling/PrefabEntityPool`1.md`` — то же для generic-версии, с примером из четырёх шагов (`EnemyEntity`, `EnemyPrefabPool`, компонент, использование) и описанием `GetEntityName(E)`. Снипет объявления пула на один параметр типа вместо двух.

### Документация: прочее

- `Docs/BestPractices/UsingEntityPoolWithFactories.md` — короткая страница про связку фабрика + пул: `BulletEntity`, `BulletFactory`, `new EntityPool<BulletEntity>(factory)`, `pool.Init(20)`, затем `Rent` → `bullet.Set("Damage", 10)` → `Return`. Единственный документ, где идиома показана целиком от фабрики до возврата, и ровно поэтому его стоит прочитать — при том что `Set` не существует, а конструктор пула требует `TArgs`.
- `Docs/Entities/Manual.md` — корневая страница модуля Entities; оглавление по всем подразделам, включая `Factories`, `Pooling`, `Installers`, `Baking`, `UI`. Нужна для навигации между главами.
- `Docs/Entities/Baking/Manual.md`, `Docs/Entities/Baking/MonoEntityBaker.md`, ``Docs/Entities/Baking/MonoEntityBaker`1.md`` — соседняя область. `MonoEntityBaker` в исходниках **есть** (`Entities/Scripts/Baking/`), в отличие от `InlineEntityFactory`, и в `Docs/BestPractices/OverrideEntityFactoriesWithBakers.md` описан как альтернатива фабрике. Это не часть этой главы, но если ваш архетип сценный, а не кодовый, бейкер может оказаться правильнее фабрики.

### Исходники: фабрики

- `Assets/Plugins/Atomic/Entities/Scripts/Utils/Args.cs` — `IArgs`, `NoArgs`, `Args<T>` … `Args<T1..T4>`. Весь файл — 65 строк, прочитайте целиком: это единственное определение `TArgs`, и других реализаций `IArgs` в дереве нет.
- `.../Scripts/Factories/Interfaces/IEntityFactory.cs` — `IEntityFactory<in TArgs>`.
- ``.../Scripts/Factories/Interfaces/IEntityFactory`1.cs`` — `IEntityFactory<out TEntity, in TArgs>` с единственным `Create(TArgs)`. Именно здесь объявлена вариантность, на которую стоит опираться вместо адаптеров.
- `.../Scripts/Factories/Interfaces/IMultiEntityFactory.cs` — `IMultiEntityFactory<in TArgs> : IMultiEntityFactory<string, IEntity, TArgs>`.
- ``.../Scripts/Factories/Interfaces/IMultiEntityFactory`1.cs`` — `Create`/`TryCreate`/`Contains`, с полными XML-комментариями. Проверьте сами, что `Register`/`Unregister` здесь нет.
- ``.../Scripts/Factories/Unity/ScriptableEntityFactory`1.cs`` — база фабрики-ассета (166 строк). Строки 30-76 — сериализованные поля и Odin-атрибуты, 82-96 — публичный `Create` и абстрактная пятипараметровая перегрузка, 102-145 — `OnValidate` и `Compile`, 156-163 — `Reset`. Главный файл раздела.
- `.../Scripts/Factories/Unity/ScriptableEntityFactory.cs` — `ScriptableEntityFactory<TArgs>` с `Name` и `Install(IEntity)` (60 строк). Строки 29-38 показывают, как именно базовый класс собирает `Entity` из запечённых ёмкостей — это эталон для идиомы 3.
- ``.../Scripts/Factories/Unity/ScriptableEntityCatalog`1.cs`` — каталог (195 строк). Строки 21-36 — объявление и ограничения, 41-70 — фабричный API, 81-106 — `EnsureInitialized` с варнингами на пустой массив и дубликаты, 109-126 — Odin-валидатор, 133-190 — реализация `IReadOnlyDictionary` (здесь видно, что каждый член зовёт `EnsureInitialized`, и здесь же боксирование перечислителя).
- `.../Scripts/Factories/Unity/ScriptableEntityCatalog.cs` — строковый каталог, 31 строка, одна строка кода: `GetKey(factory) => factory.Name`. Прочитайте вместе с комментарием над ней, чтобы увидеть расхождение.

### Исходники: пулы

- `.../Scripts/Pooling/ExpandMode.cs` — перечисление и его XML-комментарии. Прочитайте, чтобы увидеть своими глазами, что комментарий описывает рост от pooled count.
- `.../Scripts/Pooling/Interfaces/IEntityPool.cs`, ``IEntityPool`1.cs``, `IMultiEntityPool.cs`, ``IMultiEntityPool`1.cs``, `IPrefabEntityPool.cs`, ``IPrefabEntityPool`1.cs`` — шесть небольших файлов, вместе дающих полную карту контрактов. ``IPrefabEntityPool`1.cs`` — единственный под `#if UNITY_5_3_OR_NEWER` и единственный, объявляющий `InitAsync`.
- ``.../Scripts/Pooling/Core/EntityPool`1.cs`` — чисто-C# пул (187 строк). Строки 32-41 — хранилища и поля, 50-55 — конструктор с `args ?? throw`, 61-69 — `Init`, 74-84 — `Dispose`, 92-123 — `Rent` и `Expand` (здесь видно, что удвоение считается от `_rentEntities.Count`), 140-153 — `Return`, 159-185 — четыре хука. Самый читаемый пул в дереве; начинайте с него.
- `.../Scripts/Pooling/Core/EntityPool.cs` — `EntityPool<TArgs>`, 23 строки, только конструктор.
- ``.../Scripts/Pooling/Core/MultiEntityPool`1.cs`` — ключевой пул (198 строк). Строки 27-43 — три словаря, 52-57 — конструктор, 60-74 — `Init`, 77-117 — `Rent` и `Expand`, 130-149 — `Return` (строка 141 — избыточный `Stack.Contains`), 152-164 — `Dispose`. Строка 3 — безусловный `using UnityEngine;`.
- `.../Scripts/Pooling/Core/MultiEntityPool.cs` — `MultiEntityPool<TArgs>`, 20 строк.
- ``.../Scripts/Pooling/Unity/MonoEntityPool`1.cs`` — сценный пул (338 строк). Строки 28-80 — все сериализованные поля с тултипами (включая `internal bool _acceptExternalReturns`), 85-100 — `Awake`/`Reset`, 106-129 — `Init` и правильный `InitAsync`, 137-175 — `Rent`/`Expand`, 181-193 — `Return` с условием `|| _acceptExternalReturns`, 198-214 — `Dispose`, 220-254 — хуки (без `MethodImpl`) и `CreateEntity`, 261-333 — `CreateArgs`, `Create<TPool>` и `Destroy`.
- `.../Scripts/Pooling/Unity/MonoEntityPool.cs` — закрытый компонент, 47 строк, один статический `Create`. Пример для вашего собственного закрытого пула.
- ``.../Scripts/Pooling/Unity/PrefabEntityPool`1.cs`` — мульти-префабный пул (323 строки) и место концентрации проблем раздела. Строки 29-62 — `NUMBER_PATTERN`, структура `Pool`, два словаря и поля инспектора; 78-111 — `Init` и небезопасный `InitAsync`; **114-149 — три перегрузки `Rent`, из которых строка 117 теряет `parent`, а ветки попадания и промаха парентят по-разному**; 151-182 — `Expand`/`CreateEntities`; 185-206 — `Return` с несущим `Stack.Contains` и репарентингом инлайном; 212-248 — обе `Dispose`, уничтожающие всегда-`null` `pool.go`; 255-294 — четыре хука с `AggressiveInlining` и `GetEntityName`; **296-308 — `CreatePool`, не присваивающий `go`**.
- `.../Scripts/Pooling/Unity/PrefabEntityPool.cs` — закрытый компонент, 20 строк, пустое тело.

### Исходники: смежное, без чего пулы не читаются

- `.../Scripts/Entities/Unity/MonoEntity_Creation.cs` — `CreateArgs`, все перегрузки `Create`, `CreateAsync` и приватный `SetupAfterCreation` (строка 245). Прочитайте строки 47-77, чтобы увидеть `Mathf.Max(1, …)` и отсутствие `Install`, и строку 245, чтобы понять, почему пулированная сущность устанавливается ровно один раз.
- `.../Scripts/Entities/Unity/MonoEntity_Casting.cs` — `Cast`/`Cast<E>`/`TryCast`/`TryCast<E>`, 76 строк. Через `Cast<P>` проходит каждый `Return` в обоих Unity-пулах.
- `.../Scripts/Entities/Unity/MonoEntity_Destruction.cs` — `Destroy(IEntity, float)` и `Destroy(MonoEntity, float)`, 32 строки. Здесь видно, что `Destroy` зовёт `Uninstall()` перед уничтожением объекта.
- `.../Scripts/Entities/Unity/MonoEntity_UnityLifecycle.cs` — `Awake`/`OnEnable`/`Start`/`OnDisable`/`OnDestroy`. Строки 22-48 объясняют, почему `SetActive` в хуках пула — это ещё и регистрация в `TickableManager`.
- `.../Scripts/Entities/Core/Entity.cs` — три конструктора (строки 79, 109, 138), вложенная `Settings` (строка 56), `Equals`/`GetHashCode` на `_instanceId` (строки 160-170) и `EntityRegistry.Instance.Register(this)` в конце конструктора с ёмкостями (строка 153).
- `.../Scripts/Entities/Core/Entity_Lifecycle.cs` — `public void Dispose()` (строка 243), который снимает сущность с реестра и освобождает значения. Это то, что `EntityPool.Dispose()` не вызывает.
- `.../Scripts/Entities/Core/Entity_Values.cs`, `Entity_Tags.cs`, `Entity_Behaviours.cs` — `ConstructValues`/`ConstructTags` с `CeilToPrime` и `ConstructBehaviours` с простым массивом. Нужны, чтобы понимать, что именно покупает запекание ёмкостей.
- `.../Scripts/Entities/Interface/IEntity_Values.cs` — `AddValue(int, object)` / `SetValue(int, object)` / `GetValue<T>(int)`. Источник утверждения про боксирование структурных значений.
- `.../Scripts/Entities/Extensions_Values.cs`, `Extensions_Tags.cs`, `Extensions_Behaviours.cs` — строковые и `ValueKey<T>`-расширения, которые вы будете вызывать в `Install`. Здесь видно, что метода `Set` нет.
- `.../Scripts/KeyStore/Keys/ValueKey.cs`, `.../KeyStore/EntityKeyStore.cs` — `ValueKey<T>` (без неявных приведений) и `NameToId`, который делает поиск по `Dictionary<string, int>`. Основание для хоистинга ключей в `static readonly`.

### Исходники: четвёртое семейство и тесты

- ``.../Scripts/UI/Pool/EntityViewPool`1.cs`` — 189 строк. Строки 17-18 — `ProfilerMarker`; 52-84 — `Register`/`Unregister` по ключу и по каталогу; 102-133 — `InitAsync`/`Init`; 142-173 — `internal Rent`/`Return` в маркерах; 177-186 — `GetOrCreatePool` с предразмеренным `Stack`. Единственный in-tree прецедент рантайм-регистрации и инструментирования.
- `.../Scripts/UI/Pool/EntityViewPool.cs` — закрытый `EntityViewPool : EntityViewPool<string, IEntity, EntityView>` с `GetKey(view) => view.Name`, 7 строк.
- ``.../Scripts/UI/Catalog/EntityViewCatalog`1.cs``, `EntityViewCatalog.cs` — `ScriptableObject` со `List<V> prefabs`, `Count` и `GetPrefab(int)`. Минимальный каталог, подаваемый в `EntityViewPool.Register(catalog)`.
- `.../Tests/PlayMode/SceneEntityPool/PrefabEntityPoolTests.cs` — **живой**, 172 строки, восемь `[UnityTest]`. Содержит `TestPrefabEntityPool` — подкласс, достающий приватный `_pools` через рефлексию, что показывает, как вообще можно наблюдать внутренности этого пула. Тест `Return_Twice_SecondIgnored` — тот, что держится на `Stack.Contains`.
- `.../Tests/PlayMode/SceneEntityPool/SceneEntityPoolTests.cs` — **живой**, 124 строки, класс фикстуры назван `MonoEntityPoolTests`. Семь тестов, включая `Awake_AutoInit_CreatesEntities` (показывает `MonoEntityPool.Create` с `initOnAwake = true`) и `Return_UntrackedEntity_ShowsWarning` (единственное место, где `_acceptExternalReturns` выставляется в `false` — и это возможно только потому, что тестовая сборка имеет `InternalsVisibleTo`).
- `.../Tests/Substitutions/Pools/EntityPoolTestDouble.cs`, ``EntityPoolTestDouble`1.cs`` — **живые**, по 19 строк. Ваш шов для юнит-тестов спавн-кода.
- `.../Tests/EditMode/Pool/EntityPoolTests.cs`, `MultiEntityPoolTests.cs`, `.../Tests/EditMode/Factory/MultiEntityFactoryTests.cs`, `.../Tests/PlayMode/ScriptableEntityFactoryCatalog/ScriptableEntityFactoryCatalogTests.cs`, `.../Tests/PlayMode/EntityViewPool/EntityViewPoolTests.cs`, `.../Tests/Substitutions/Factory/*.cs` — **все закомментированы построчно**. Открывать стоит ровно для одного: увидеть, какие сценарии когда-то проверялись, и переписать нужные под текущий API. Как спецификация они бесполезны (написаны под API без `TArgs`), как список кейсов — полезны.
- `.../Scripts/AssemblyInfo.cs` — `InternalsVisibleTo` для `Atomic.Entities.EditModeTests`, `Atomic.Entities.PlayModeTests` и `NetworkEngine.Fusion`. Объясняет, почему `internal`-поля пулов доступны тестам и недоступны вашей игровой сборке.
- `.../Tests/Performance/` — здесь **нет** тестов производительности по фабрикам и пулам: только `Collection/`, `Common/GameObject_Performance.cs` и `Entity/`. Любая цифра по пулам в вашем проекте — ваш собственный замер.
