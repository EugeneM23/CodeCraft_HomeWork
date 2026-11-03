# 02. Best Practices: как писать идиоматично

Раздел `Docs/BestPractices` (29 файлов) — это не справочник API, а свод стилевых решений автора фреймворка: как разбивать сборку сущности на установщики, как выражать механику цепочкой Request → Condition → Action → Event, как отделить чистую C#-модель от Unity-представления и как итерироваться по коллекциям без аллокаций. Главное, что нужно понять перед миграцией: эти документы написаны против **предыдущего поколения API**. Фабрики, пулы, каталоги и бейкеры в исходниках уже параметризованы типом аргументов `IArgs`, а половина типов, на которые ссылаются документы (`BaseRequest`, `BaseEvent`, `BaseVariable`, `Timer`, `PrintAction`, `GameContext`), в коде отсутствует вовсе. Поэтому ниже каждый идиом приведён в той форме, которая подтверждена исходником в `C:/!!!Work/_AtomicUpstream/Assets/Plugins/Atomic`, а расхождения с документацией помечены отдельно. Всё, что не удалось подтвердить объявлением в коде, из главы убрано и перечислено в `droppedClaims`.

---

## Модель

Пять утверждений, без которых идиомы не читаются.

**1. Сущность — это три таблицы и ничего больше.** `IEntity` складывается из трёх partial-интерфейсов: теги (`int → присутствие`), значения (`int → object`) и поведения (список `IEntityBehaviour`). Плюс жизненный цикл: `IEntity : IInitSource, IEnableSource, ITickSource`. Никаких полей, никакой схемы компонентов. Вся «типизация» сущности существует только на стороне чтения.

**2. Ключи — целые числа, а не строки.** Строковый ключ на каждом обращении проходит через `EntityKeyStore.NameToId(string)` — статический `Dictionary<string,int>`. `ValueKey<T>` / `ValueKey<E,T>` / `TagKey` / `TagKey<E>` — это `readonly struct` с единственным полем `int Id`, которое вычисляется один раз в конструкторе. Это единственный способ получить в этом фреймворке типобезопасность по значению и единственный способ убрать хеширование строки из горячего кода.

**3. Значения хранятся как `object`, поэтому примитивы оборачивают.** `void AddValue(int key, object value)` — любой value-type боксируется при добавлении. Отсюда вся «атомарная» обвязка `Atomic.Elements`: `Const<T>`, `Variable<T>`, `ReactiveVariable<T>`, `Event`, `Request`, `Cooldown` — это ссылочные объекты, которые кладут в таблицу один раз и дальше читают через узкий интерфейс. Это не стилистика, а следствие схемы хранения.

**4. «Узкий интерфейс» и «конкретный тип» — правила для разных мест.** `PreferAbstractInterfaces.md` требует зависеть от `IValue<T>`, `IVariable<T>`, `ISignal`, `IAction` — это про **поля поведений и презентеров**. Документы `IteratingOver*.md` требуют обратного: объявлять переменную конкретным типом (`Entity`, `EntityCollection<E>`, `ReactiveList<T>`) — это про **точку итерации**, потому что struct-энумератор доступен только через конкретный тип. Противоречия нет: узкие интерфейсы — для зависимостей, конкретные типы — для циклов.

**5. Композиция имеет два механизма, а не один.** `IEntityInstaller<E>` собирает сущность один раз и необратимо (`Install`, плюс опциональный `Uninstall`). `IEntityAspect<E>` (`Apply`/`Discard`) — обратимое наложение: баффы, состояния, экипировка. Документация Best Practices про аспекты не упоминает вовсе, из-за чего временные модификации естественно уезжают в установщики, где их нечем снять.

---

## API

### Композиция сущности

```csharp
public interface IEntityInstaller
{
    void Install(IEntity entity);
}

public interface IEntityInstaller<in E> : IEntityInstaller where E : IEntity
{
    void Install(E entity);

    void IEntityInstaller.Install(IEntity entity)   // default interface member: is-тест + throw
    {
        if (entity is not E e)
            throw new InvalidCastException(/* ... */);
        this.Install(e);
    }
}
```
`Entities/Scripts/Installers/IEntityInstaller.cs`, ``IEntityInstaller`1.cs``. Базовая единица сборки. Обобщённая версия реализует необобщённую **явно, через default-реализацию с runtime-проверкой типа**, поэтому дженерик не даёт статической гарантии в точке вызова `Install`.

```csharp
public abstract class MonoEntityInstaller : MonoBehaviour, IEntityInstaller
{
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
    protected virtual void OnValidate() { /* onValidateCallback */ }
}

public abstract class MonoEntityInstaller<E> : MonoEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
{
    public sealed override void Install(IEntity entity);     // каст + throw
    public sealed override void Uninstall(IEntity entity);   // каст + throw
    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}
```
`Installers/MonoEntityInstaller.cs`, ``MonoEntityInstaller`1.cs``. В наследнике переопределяют **только** `Install(E)` и `Uninstall(E)`; версии с `IEntity` помечены `sealed override`. Обратите внимание на `where E : class` — у `MonoEntityInstaller<E>` и `ScriptableEntityInstaller<E>` он есть, у `IEntityInstaller<E>` его нет.

```csharp
public abstract class ScriptableEntityInstaller : ScriptableObject, IEntityInstaller
public abstract class ScriptableEntityInstaller<E> : ScriptableEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
```
`Installers/ScriptableEntityInstaller.cs`, ``ScriptableEntityInstaller`1.cs``. Та же форма, но конфигурация живёт в ассете и шарится между сущностями.

```csharp
public interface IEntityAspect<in E> where E : IEntity
{
    void Apply(E entity);
    void Discard(E entity);
}
public interface IEntityAspect : IEntityAspect<IEntity> { }

// Entities/Scripts/Aspects/Extensions.cs
public static void Apply<E>(this E e, IEntityAspect<E> entityAspect) where E : IEntity;
public static void Discard<E>(this E e, IEntityAspect<E> entityAspect) where E : IEntity;
```
`Entities/Scripts/Aspects/`. Плюс `MonoEntityAspect`, `MonoEntityAspect<E>`, `ScriptableEntityAspect`, `ScriptableEntityAspect<E>`. Берите аспект, когда модификацию надо будет снять.

```csharp
// Entities/Scripts/Entities/Extensions.cs
public static IEntity Install(this IEntity entity, IEntityInstaller installer);     // возвращает entity
public static void Install(this IEntity entity, IEnumerable<IEntityInstaller> installers);
public static void InstallFromScene(this IEntity entity, Scene scene, bool includeInactive = true);
public static void Clear(this IEntity entity);
```

### Ключи и доступ к значениям

```csharp
public readonly struct ValueKey<T> : IEquatable<ValueKey<T>>
{
    public readonly int Id;
    public ValueKey(string name);   // Id = EntityKeyStore.NameToId(name)
    public ValueKey(int id);
}

public readonly struct ValueKey<E, T> : IEquatable<ValueKey<E, T>> where E : IEntity { /* то же */ }
public readonly struct TagKey    : IEquatable<TagKey>    { public readonly int Id; /* ... */ }
public readonly struct TagKey<E>  : IEquatable<TagKey<E>>  where E : IEntity { /* ... */ }
```
`Entities/Scripts/KeyStore/Keys/`. Используйте их везде, где ключ известен на этапе компиляции. Это самое важное, чего нет в Best Practices: все примеры в документации строковые.

```csharp
// Entities/Scripts/Entities/Extensions_Values.cs
public static void AddValue(this IEntity entity, string key, object value);
public static void AddValue<T>(this IEntity entity, ValueKey<T> key, T value);
public static void AddValue<E, T>(this E entity, ValueKey<E, T> key, T value) where E : IEntity;

public static T GetValue<T>(this IEntity entity, string key);
public static T GetValue<T>(this IEntity entity, ValueKey<T> key);
public static T GetValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;

public static bool TryGetValue<E, T>(this E entity, ValueKey<E, T> key, out T value) where E : IEntity;
public static ref T GetValueUnsafe<T>(this IEntity entity, ValueKey<T> key) where T : class;
public static T GetValueUnsafe<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity where T : class;
public static void DisposeValues(this IEntity entity);
```
Обобщённого строкового `AddValue<T>(string, T)` **не существует** — строковая перегрузка только одна, `(string, object)`. Обратите внимание на асимметрию: двухпараметрический `GetValueUnsafe<E,T>` возвращает `T`, а не `ref T`, в отличие от двух остальных перегрузок.

```csharp
// Entities/Scripts/Entities/Interface/IEntity_Values.cs — «сырой» слой
T GetValue<T>(int key);
ref T GetValueUnsafe<T>(int key) where T : class;   // без проверки типа — реальный hot path
bool TryGetValue<T>(int key, out T value);
void AddValue(int key, object value);
KeyValuePair<int, object>[] GetValues();            // аллоцирует
int CopyValues(KeyValuePair<int, object>[] results); // не аллоцирует
IEnumerator<KeyValuePair<int, object>> GetValueEnumerator();  // боксирует
```

```csharp
public static class EntityKeyStore
{
    public static void SetAlgorithm(IEntityKeyAlgorithm algorithm);  // сбрасывает кеш
    public static int NameToId(string name);
    public static string IdToName(int id);
    public static void Reset();
}
```
`Entities/Scripts/KeyStore/EntityKeyStore.cs`. Алгоритм по умолчанию — `SequentialEntityKeyAlgorithm`, у которого `public int NameToId(string name) => _nextId++`, то есть **имя игнорируется**. Если id нужны стабильные (сохранения, сеть), подставьте детерминированный `IEntityKeyAlgorithm` до создания первого ключа.

### Жизненный цикл

```csharp
public interface IInitSource : IDisposable
{ event Action OnInitialized; event Action OnDisposed; bool Initialized { get; } void Init(); }

public interface IEnableSource
{ event Action OnEnabled; event Action OnDisabled; bool Enabled { get; } void Enable(); void Disable(); }

public interface ITickSource
{
    event Action<float> OnTicked; event Action<float> OnFixedTicked; event Action<float> OnLateTicked;
    void Tick(float deltaTime); void FixedTick(float deltaTime); void LateTick(float deltaTime);
}
```
`Entities/Scripts/Lifecycle/Sources/`.

```csharp
// Entities/Scripts/Lifecycle/Extensions.cs — все с [MethodImpl(AggressiveInlining)]
public static InitSubscription       WhenInit(this IInitSource source, Action action);
public static DisposeSubscription    WhenDispose(this IInitSource source, Action action);
public static EnableSubscription     WhenEnable(this IEnableSource source, Action action);
public static DisableSubscription    WhenDisable(this IEnableSource source, Action action);
public static TickSubscription       WhenTick(this ITickSource source, Action<float> action);
public static FixedTickSubscription  WhenFixedTick(this ITickSource source, Action<float> action);
public static LateTickSubscription   WhenLateTick(this ITickSource source, Action<float> action);
public static void                   WhenGizmos(this IGizmosSource source, Action action);   // void!
```
Каждая, кроме `WhenGizmos`, возвращает `readonly struct … : IDisposable` (`Lifecycle/Subscriptions/`). Документация во всех примерах возвращаемое значение отбрасывает — для пулящихся сущностей это означает, что подписку невозможно снять.

```csharp
public interface IEntityBehaviour { }
public interface IEntityInit      : IEntityBehaviour { void Init(IEntity entity); }
public interface IEntityEnable    : IEntityBehaviour { /* Enable(IEntity) */ }
public interface IEntityDisable   : IEntityBehaviour { /* Disable(IEntity) */ }
public interface IEntityTick      : IEntityBehaviour { void Tick(IEntity entity, float deltaTime); }
public interface IEntityFixedTick : IEntityBehaviour { /* FixedTick(IEntity, float) */ }
public interface IEntityLateTick  : IEntityBehaviour { /* LateTick(IEntity, float) */ }
public interface IEntityDispose   : IEntityBehaviour { /* Dispose(IEntity) */ }
public interface IEntityGizmos    : IEntityBehaviour { /* DrawGizmos(IEntity) */ }
// + типизированный вариант каждого: IEntityTick<in E> : IEntityTick where E : IEntity
```
`Entities/Scripts/Behaviours/`. Типизированные версии устроены как `IEntityInstaller<E>`: default-реализация необобщённого метода делает `is not E` и бросает.

### Сущность и её реализации

```csharp
public partial class Entity : IEntity
{
    [Serializable] public struct Settings { public bool disposeValues; }

    public Entity(string name = null, int tagCapacity = 0, int valueCapacity = 0,
                  int behaviourCapacity = 0, Settings? settings = null);
    public Entity(string name, IEnumerable<string> tags,
                  IEnumerable<KeyValuePair<string, object>> values,
                  IEnumerable<IEntityBehaviour> behaviours, Settings? settings = null);
    public Entity(string name, IEnumerable<int> tags,
                  IEnumerable<KeyValuePair<int, object>> values,
                  IEnumerable<IEntityBehaviour> behaviours, Settings? settings = null);
}
```
`Entities/Scripts/Entities/Core/Entity.cs:138`. Пятый параметр `Settings?` по умолчанию даёт `disposeValues = true`. Конструктор ёмкостей **вызывает `EntityRegistry.Instance.Register(this)`** (строка 153) — создание сущности всегда имеет глобальный побочный эффект.

```csharp
public partial class MonoEntity : MonoBehaviour, IEntity
public abstract class MonoEntitySingleton<E> : MonoEntity where E : MonoEntitySingleton<E>
{
    public static E Instance { get; }                 // FindAnyObjectByType, throw если нет
    public static bool TryGetInstance(out E instance);
}
```
`Entities/Scripts/Entities/Unity/`. `MonoEntitySingleton<E>` — это **единственный** механизм синглтонов во фреймворке. `GameContext.Instance` / `GameContext.SetFactory(...)` из документации не существуют.

```csharp
public sealed class EntityRegistry : IReadOnlyEntityCollection<IEntity>
{
    public static EntityRegistry Instance { get; }
    public IEntity Get(int id);
    public bool TryGet(int id, out IEntity entity);
    public T GetUnsafe<T>(int id) where T : class, IEntity;
    public bool TryGetUnsafe<T>(int id, out T entity) where T : class, IEntity;
    public bool Contains(int id);
    public event Action<IEntity> OnAdded, OnRemoved;
}
```
`Entities/Scripts/Registry/EntityRegistry.cs`. Глобальный `InstanceID → IEntity`. Это то, на что ложатся сетевой код и сохранения.

### Коллекции, миры, фильтры, системы

```csharp
public interface IReadOnlyEntityCollection<E> : IReadOnlyCollection<E> where E : IEntity
{
    event Action OnStateChanged; event Action<E> OnAdded; event Action<E> OnRemoved;
    E this[int index] { get; }
    bool TryGetAt(int index, out E entity);
    bool Contains(E entity);
    void CopyTo(ICollection<E> results);
    void CopyTo(E[] array, int arrayIndex);
}

public class EntityCollection<E> : IEntityCollection<E> where E : IEntity
{
    public bool Add(E item);        // bool, не void
    public bool Remove(E item);     // bool
    public Enumerator GetEnumerator();          // struct, по значению
    public struct Enumerator : IEnumerator<E> { /* ... */ }
}
public class EntityCollection : EntityCollection<IEntity>, IEntityCollection { }
```
`Entities/Scripts/Collections/`. Внутри — `Slot[] _slots`, `int[] _buckets`, `int[] _order`: собственная хеш-таблица на массивах, а не `Dictionary`.

```csharp
public interface IEntityWorld<E> : IEntityCollection<E>, IEnableSource, ITickSource where E : IEntity
{ string Name { get; set; } }
public interface IEntityWorld : IEntityWorld<IEntity> { }

public class EntityWorld<E> : EntityCollection<E>, IEntityWorld<E> where E : IEntity
{
    public EntityWorld();
    public EntityWorld(params E[] entities);
    public EntityWorld(string name = null, params E[] entities);
    public EntityWorld(string name, IEnumerable<E> entities);
    public void Enable(); public void Disable();
    public void Tick(float dt); public void FixedTick(float dt); public void LateTick(float dt);
}
public class EntityWorld : EntityWorld<IEntity>, IEntityWorld { }
```
`Entities/Scripts/World/`. Есть ещё `MonoEntityWorld`, `MonoEntityWorld<E>`, `MonoEntityWorldSingleton`.

```csharp
// Entities/Scripts/Collections/Extensions.cs — это РАСШИРЕНИЯ, а не члены мира
public static void InitEntities<E>(this IEntityCollection<E> it) where E : IEntity;
public static void DisposeEntities<E>(this IEntityCollection<E> it) where E : IEntity;
public static void AddRange<E>(this IEntityCollection<E> it, params E[] entities) where E : IEntity;
public static void CollectAllEntities<E>(this IEntityCollection<E> collection, bool includeInactive = false)
    where E : IEntity;

// Entities/Scripts/World/Extensions.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void BindTo<E>(this IEntityWorld<E> world, IEntity source) where E : IEntity;
```
`BindTo` делает ровно восемь регистраций `WhenInit/WhenEnable/WhenTick/WhenFixedTick/WhenLateTick/WhenDisable/WhenDispose×2`, которые документация предлагает писать руками.

```csharp
public class EntityFilter<E> : IReadOnlyEntityCollection<E>, IDisposable where E : IEntity
{
    public EntityFilter(IReadOnlyEntityCollection<E> source, Predicate<E> predicate,
                        params IEntityTrigger<E>[] triggers);
    public EntityCollection<E>.Enumerator GetEnumerator();   // struct
    public static Builder StartBuild();

    public struct Builder
    {
        public Builder WithSource(IReadOnlyEntityCollection<E> source);
        public Builder WithPredicate(Predicate<E> predicate);
        public Builder WithTrigger(IEntityTrigger<E> trigger);
        public Builder WithTriggers(params IEntityTrigger<E>[] triggers);
        public EntityFilter<E> Build();
    }
}
public class EntityFilter : EntityFilter<IEntity>, IReadOnlyEntityCollection { }
```
`Entities/Scripts/Filters/`. Плюс `DerivedEntityFilter`, `DerivedEntityFilter<E>`.

```csharp
public interface IEntityTrigger<E> where E : IEntity
{
    void SetAction(Action<E> action);
    void Track(E entity);
    void Untrack(E entity);
}

public class TagEntityTrigger<E>   : IEntityTrigger<E> where E : IEntity { public TagEntityTrigger(bool added = true, bool deleted = true); }
public class ValueEntityTrigger<E> : IEntityTrigger<E> where E : IEntity { public ValueEntityTrigger(bool added = true, bool deleted = true, bool changed = true); }
public class InlineEntityTrigger<E>: IEntityTrigger<E> where E : IEntity { public InlineEntityTrigger(Action<E, Action<E>> track, Action<E, Action<E>> untrack); }
public class BehaviourEntityTrigger<E> : IEntityTrigger<E> where E : IEntity { public BehaviourEntityTrigger(bool added = true, bool removed = true); }
public class StateChangedEntityTrigger<E> : IEntityTrigger<E> where E : IEntity { /* подписка на entity.OnStateChanged */ }

public abstract class SubscriptionEntityTrigger<E, S> : IEntityTrigger<E>
    where E : IEntity where S : IDisposable { /* ... */ }
```
`Entities/Scripts/Triggers/`. Шесть видов триггеров, у каждого обобщённая и необобщённая версия. Триггер — это то, что превращает фильтр из опрашиваемого в реактивный.

```csharp
public abstract class EntitySystemBase<E> : IDisposable where E : IEntity
{
    [Serializable] public class Settings
    {
        public float frameBudget = 0.03f;
        public AdaptiveBatching batching = new();
        [Serializable] public sealed class AdaptiveBatching
        { public int minSize = 1024, maxSize = 2048, scaleDown = 2, stepUp = 256; }
    }

    protected EntitySystemBase(IReadOnlyEntityCollection<E> source, Settings settings);
    public void Enable(); public void Disable();
    public void Update(float deltaTime);                 // замеряет время, подкручивает batchSize
    protected abstract void OnUpdate(int batchSize, float deltaTime);
    protected virtual void OnEnable(); protected virtual void OnDisable();
    protected virtual void OnAddEntity(E entity); protected virtual void OnRemoveEntity(E entity);
    public abstract void Dispose();
}

[Serializable]
public abstract class EntitySystem<E> : EntitySystemBase<E>, IDisposable where E : IEntity
{
    protected EntitySystem(IReadOnlyEntityCollection<E> source, Settings settings);
    protected abstract void Update(E entity, float deltaTime);   // единственная точка расширения
}

public abstract class PriorityEntitySystem<E> : EntitySystemBase<E>, IDisposable where E : IEntity
{
    protected abstract void Update(E entity, float deltaTime);
    protected abstract EntityUpdatePriority EvaluatePriority(E entity);
}
public enum EntityUpdatePriority : byte { Low = 0, Medium = 1, High = 2 }
```
`Entities/Scripts/Systems/`. Это и есть ответ фреймворка на массовую обработку: массив сущностей, курсор, кольцевой проход батчами, размер батча подстраивается под `frameBudget`.

```csharp
// Entities/Scripts/Systems/Extensions.cs
public static void AddTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
    where TContext : IEntity where TEntity : IEntity;
public static void AddFixedTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
    where TContext : IEntity where TEntity : IEntity;
public static void AddLateTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
    where TContext : IEntity where TEntity : IEntity;
```

### Фабрики, каталоги, пулы, бейкеры

```csharp
public interface IArgs { }
public struct NoArgs : IArgs { public static readonly NoArgs Default; }
public struct Args<T> : IArgs { public readonly T value; public Args(T value); public static implicit operator Args<T>(T value); }
public struct Args<T1, T2> : IArgs { public T1 value1; public T2 value2; /* + ctor */ }
// + Args<T1,T2,T3>, Args<T1,T2,T3,T4>
```
`Entities/Scripts/Utils/Args.cs`. **Это ось всей новой модели создания.** Каждая фабрика, каталог, пул и бейкер теперь имеют лишний type-параметр `TArgs : IArgs`.

```csharp
public interface IEntityFactory<out TEntity, in TArgs>
    where TEntity : IEntity where TArgs : IArgs
{
    TEntity Create(TArgs args);
}
public interface IEntityFactory<in TArgs> : IEntityFactory<IEntity, TArgs> where TArgs : IArgs { }

public interface IMultiEntityFactory<in TKey, TEntity, in TArgs>
    where TEntity : IEntity where TArgs : IArgs
{
    TEntity Create(TKey key, TArgs args);
    bool TryCreate(TKey key, TArgs args, out TEntity entity);
    bool Contains(TKey key);
}
public interface IMultiEntityFactory<in TArgs> : IMultiEntityFactory<string, IEntity, TArgs> where TArgs : IArgs { }
```
`Entities/Scripts/Factories/Interfaces/`. Однопараметрический `IEntityFactory<T>` — это вариант **по аргументам**, а не по сущности: `IEntityFactory<NoArgs>` создаёт `IEntity`. `IEntityFactory<BulletEntity>` не скомпилируется.

```csharp
public abstract class ScriptableEntityFactory<TEntity, TArgs> : ScriptableObject, IEntityFactory<TEntity, TArgs>
    where TEntity : IEntity where TArgs : IArgs
{
    public TEntity Create(TArgs args);   // sealed по смыслу: пробрасывает сериализованные ёмкости
    protected abstract TEntity Create(int tagCapacity, int valueCapacity, int behaviourCapacity,
                                      Entity.Settings settings, TArgs args);
    protected virtual void OnValidate();   // autoCompile → Compile()
    protected virtual void Reset();
}

public abstract class ScriptableEntityFactory<TArgs> : ScriptableEntityFactory<IEntity, TArgs>, IEntityFactory<TArgs>
    where TArgs : IArgs
{
    [field: SerializeField] public virtual string Name { get; private set; }
    protected override IEntity Create(int, int, int, Entity.Settings, TArgs);   // уже реализован
    protected abstract void Install(IEntity entity);    // единственная точка расширения
}
```
Имена файлов здесь обратны ожидаемому: двухпараметрический `ScriptableEntityFactory<TEntity, TArgs>` лежит в ``Entities/Scripts/Factories/Unity/ScriptableEntityFactory`1.cs:26``, а однопараметрический `ScriptableEntityFactory<TArgs>` — в `Factories/Unity/ScriptableEntityFactory.cs:14`. Ёмкости — **параметры** `protected Create` (сериализованные `private` поля `tagCapacity`/`valueCapacity`/`behaviourCapacity` наследнику недоступны). `Name` уже объявлен в необобщённой версии, поэтому `public string Name => this.name;` в наследнике даст CS0108.

```csharp
public abstract class ScriptableEntityCatalog<K, E, F, TArgs> : ScriptableObject,
    IMultiEntityFactory<K, E, TArgs>, IReadOnlyDictionary<K, F>
    where E : IEntity
    where F : ScriptableObject, IEntityFactory<E, TArgs>
    where TArgs : IArgs
{
    public E Create(K key, TArgs args);
    public bool TryCreate(K key, TArgs args, out E entity);
    public bool Contains(K key);
    protected abstract K GetKey(F factory);
    public int Count { get; } public IEnumerable<K> Keys { get; } public IEnumerable<F> Values { get; }
    public F this[K key] { get; } public bool ContainsKey(K key); public bool TryGetValue(K key, out F value);
}

public abstract class ScriptableEntityCatalog<TArgs> :
    ScriptableEntityCatalog<string, IEntity, ScriptableEntityFactory<TArgs>, TArgs>, IMultiEntityFactory<TArgs>
{
    protected override string GetKey(ScriptableEntityFactory<TArgs> factory) => factory.Name;
}
```
``Factories/Unity/ScriptableEntityCatalog`1.cs:21``. Четыре type-параметра, и это полноценный `IReadOnlyDictionary<K,F>`, а не только хук ключа.

```csharp
public enum ExpandMode { ExpandByOne, ExpandByDoubling, NoExpand }

public interface IEntityPool<TEntity> : IDisposable where TEntity : IEntity
{ TEntity Rent(); void Return(TEntity entity); void Init(int initialCount); }

public class EntityPool<TEntity, TArgs> : IEntityPool<TEntity>
    where TEntity : IEntity where TArgs : IArgs
{
    public EntityPool(IEntityFactory<TEntity, TArgs> factory, TArgs args,
                      ExpandMode expandMode = ExpandMode.ExpandByOne);
    public void Init(int initialCount); public TEntity Rent(); public void Return(TEntity entity); public void Dispose();
    protected virtual void OnCreate(TEntity entity); protected virtual void OnDispose(TEntity entity);
    protected virtual void OnRent(TEntity entity);   protected virtual void OnReturn(TEntity entity);
}
public class EntityPool<TArgs> : EntityPool<IEntity, TArgs>, IEntityPool where TArgs : IArgs { /* ... */ }

public class MultiEntityPool<TKey, TEntity, TArgs> : IMultiEntityPool<TKey, TEntity>
    where TEntity : IEntity where TArgs : IArgs
{
    public MultiEntityPool(IMultiEntityFactory<TKey, TEntity, TArgs> factory, TArgs args,
                           ExpandMode expandMode = ExpandMode.ExpandByOne);
    public void Init(TKey key, int count); public TEntity Rent(TKey key); public void Return(TEntity entity);
}
```
`Entities/Scripts/Pooling/`. `ExpandMode` в документации не упомянут ни разу, а по умолчанию стоит `ExpandByOne` — худший выбор для залпового спавна. Плюс `MonoEntityPool`, `MonoEntityPool<E>`, `PrefabEntityPool`, `PrefabEntityPool<E>`.

```csharp
public abstract partial class MonoEntityBaker<TEntity, TArgs> : MonoBehaviour
    where TEntity : IEntity where TArgs : IArgs
{
    public TEntity Bake(TArgs args);              // Create(...) + Release()
    protected virtual void Release();             // по умолчанию Destroy(gameObject)
    protected abstract TEntity Create(int tagCapacity, int valueCapacity, int behaviourCapacity,
                                      Entity.Settings settings, TArgs args);
    protected virtual void OnValidate();          // MonoEntityBaker`1_Editor.cs:38
    protected virtual void Reset();

    public static TEntity[] BakeAll(TArgs args, bool includeInactive = true);
    public static void BakeAll(TArgs args, ICollection<TEntity> destination, bool includeInactive = true);
    public static List<TEntity> Bake(TArgs args, Scene scene, bool includeInactive = true);
    public static TEntity[] Bake(TArgs args, GameObject gameObject, bool includeInactive = true);
}

public abstract class MonoEntityBaker<TArgs> : MonoEntityBaker<IEntity, TArgs> where TArgs : IArgs
{
    protected override IEntity Create(int, int, int, Entity.Settings, TArgs);
    protected abstract void Install(IEntity entity, TArgs args);
}
```
`Entities/Scripts/Baking/`. Есть также `MonoEntityBakerOptimized<TKey, TEntity, TView, TArgs>` с точкой расширения `protected abstract void Override(TEntity entity, TArgs args)` — вариант, ориентированный на производительность, которого документация не упоминает.

### Представление

```csharp
public abstract partial class EntityView<E> : MonoBehaviour where E : class, IEntity
{
    internal List<MonoEntityInstaller> installers;   // internal — из пользовательской сборки недоступно
    public E Entity { get; }
    public bool IsActive { get; }
    public void Activate(E entity);     // Deactivate + FormateName + OnActivate + installers.Install
    public void Deactivate();           // installers.Uninstall + OnDeactivate
    protected virtual string FormateName(E entity);   // "{entity.Name}:{entity.InstanceID}"
    protected virtual void OnActivate(E entity);
    protected virtual void OnDeactivate(E entity);
}

public class EntityView : EntityView<IEntity>
{
    [field: SerializeField] public string Name { get; private set; }
}
```
`Entities/Scripts/UI/View/`. `Name` объявлен **только в необобщённом** `EntityView`; у `EntityView<E>` его нет. Именно `Deactivate()` вызывает `installer.Uninstall(...)` — вот зачем нужен `Uninstall`.

```csharp
public abstract class EntityViewCatalog<E, V> : ScriptableObject where E : class, IEntity where V : EntityView<E>
{ public int Count { get; } public V GetPrefab(int index); }

public abstract class EntityViewPool<K, E, V> : MonoBehaviour where E : class, IEntity where V : EntityView<E>
{
    public void Register(K key, V prefab); public void Unregister(K key);
    public void Register(EntityViewCatalog<E, V> catalog); public void Unregister(EntityViewCatalog<E, V> catalog);
    public void Init(K key, int count);
    public async ValueTask InitAsync(K key, int count);     // InstantiateAsync — прогрев без фризов
    public void Clear();
    protected abstract K GetKey(V view);
}

public abstract class EntityCollectionView<K, E, V> : MonoBehaviour, IReadOnlyCollection<KeyValuePair<E, V>>
{
    public event Action<E, V> OnAdded, OnRemoved;
    public int Count { get; }
    public V Get(E entity); public bool TryGet(E entity, out V view); public bool Contains(E entity);
    public V Add(E entity);          // возвращает арендованную view
    public void Remove(E entity); public void Remove(V view); public void Clear();
    public Dictionary<E, V>.Enumerator GetEnumerator();   // struct
    protected abstract K GetKey(E entity);
}

public abstract class EntityWorldView<K, E, V> : EntityCollectionView<K, E, V>
{
    public bool IsActive { get; }
    public void Activate(IReadOnlyEntityCollection<E> source);   // подписка на OnAdded/OnRemoved
    public void Deactivate();
}
```
`Entities/Scripts/UI/`. `Rent`/`Return` у `EntityViewPool` — `internal`: пул дёргает только `EntityCollectionView`.

### Atomic.Elements: значения, сигналы, действия

```csharp
public interface IValue<out T> : IFunction<T>, IValue { new T Value { get; } }
public interface IFunction<out R> { R Invoke(); }
public interface ISetter<in T> : IAction<T>, ISetter { new T Value { set; } }
public interface IVariable<T> : IValue<T>, ISetter<T> { new T Value { get; set; } }
public interface IReactiveValue<out T> : IValue<T>, ISignal<T> { }
public interface IReactiveVariable<T> : IVariable<T>, IReactiveValue<T> { }
public interface ISignal { event Action OnEvent; }
public interface ISignal<out T> { event Action<T> OnEvent; }
public interface IAction { void Invoke(); }
public interface IEvent : ISignal, IAction { }
public interface IPredicate : IFunction<bool> { }
```
`Elements/Scripts/{Values,Functions,Setters,Variables,Events,Actions}`. `ISetter<T>` реализует `IAction<T>.Invoke` через default-метод (`Value = arg`) с `AggressiveInlining`.

```csharp
[Serializable] public class Const<T> : IValue<T>
{
    public T Value { get; } public T Invoke();
    public Const(); public Const(T value);
    public static implicit operator Const<T>(T value);
    public static implicit operator T(Const<T> value);
}
[Serializable] public class Variable<T> : IVariable<T>              // неподписываемая
[Serializable] public class ReactiveVariable<T> : IReactiveVariable<T>, IDisposable
[Serializable] public class InlineFunction<R> : IFunction<R>        // ctor(Func<R>), implicit из Func<R>
[Serializable] public class InlineSetter<T>  : ISetter<T>           // ctor(Action<T>)
[Serializable] public class InlineValue<T> : IValue<T>
public abstract class ScriptableConst<T> : ScriptableObject, IValue<T>
```
`Const<T>` — **класс** (не struct) и **помечен `[Serializable]`** (`Elements/Scripts/Values/Const.cs:22-24`). Из неперечисленных в документации: `InlineVariable<T>`, `ThreadSafeVariable<T>`, `InlineReactiveVariable<T>`, `ThreadSafeReactiveVariable<T>`, а также готовые `IntVariable`/`FloatVariable`/`BoolVariable`, `ReactiveInt`/`ReactiveFloat`/`ReactiveBool`, `Vector3Variable`/`ReactiveVector3` и т.д.

```csharp
public static class Constants
{
    public static readonly Const<bool> True, False;
    public static readonly Const<float> PI, TwoPI, HalfPI, Deg2Rad, Rad2Deg, E, GoldenRatio;
    public static readonly Const<float> Second, Minute, Hour, FrameTime60FPS;
    public static readonly Const<int> ZeroInt, OneInt;
    public static readonly Const<float> Zero, One, NegativeOne, Half, Gravity;
    public static readonly Const<Vector3> Up, Down, Left, Right, Forward, Back, ZeroVector, OneVector;
    public static readonly Const<Color> White, Black, Red, Green, Blue, Transparent;
}
```
`Elements/Scripts/Values/Constants.cs`. Тип называется `Constants`; `DefaultConstants` не существует.

```csharp
[Serializable] public class Event : IEvent, IDisposable { public event Action OnEvent; public void Invoke(); public void Dispose(); }
[Serializable] public class Event<T> : IEvent<T>, IDisposable { /* ... до Event<T1,T2,T3,T4> */ }
public class ThreadSafeEvent  // + InlineSignal

public readonly struct Subscription : IDisposable { public Subscription(ISignal signal, Action action); public void Dispose(); }
public readonly struct Subscription<T> : IDisposable { /* ... до Subscription<T1,T2,T3,T4> */ }
```
`Elements/Scripts/Events/`. Тип называется `Event`, не `BaseEvent`.

```csharp
// Elements/Scripts/Events/Extensions.cs — все с AggressiveInlining
public static Subscription    Subscribe(this ISignal it, Action action);
public static Subscription    Subscribe(this ISignal it, IAction action);
public static Subscription<T> Subscribe<T>(this ISignal<T> it, Action<T> action);
public static void Unsubscribe(this ISignal it, Action action);

// Elements/Scripts/Values/Extensions.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T> Observe<T>(this IReactiveValue<T> it, Action<T> action);  // сначала Invoke, потом подписка
```
`Subscribe`/`Unsubscribe`/`Observe` — **расширения**, не члены интерфейсов: без `using Atomic.Elements;` они не видны. `ISignal` объявляет только `event Action OnEvent`.

```csharp
[Serializable] public class Request : IRequest
{
    public bool Required { get; }
    public void Invoke();     // _required = true
    public bool Consume();    // true + сброс, если был Required
}
[Serializable] public class Request<T> : IRequest<T>
{
    public bool Required { get; } public T Arg { get; }
    public void Invoke(T arg);
    public bool TryGet(out T arg);     // без сброса
    public bool Consume(out T arg);    // со сбросом
}
// + Request<T1,T2> .. Request<T1,T2,T3,T4>

public interface IRequest : IAction { bool Required { get; } bool Consume(); }
public interface IRequest<T> : IAction<T> { bool Required { get; } T Arg { get; } bool TryGet(out T); bool Consume(out T); }
```
`Elements/Scripts/Requests/`. Тип называется `Request`, не `BaseRequest`.

```csharp
[Serializable] public sealed class LogAction : IAction
{
    public LogAction();
    public LogAction(string message, LogType logType = LogType.Log);
    public LogAction(string message);
    public void Invoke();
}
public class InlineAction : IAction { public InlineAction(Action action); public static implicit operator InlineAction(Action value); }
[Serializable] public class CompositeAction : ReactiveLinkedList<Action>, ICompositeAction
{ public CompositeAction(); public CompositeAction(params Action[] actions); public void Invoke(); }

public abstract class MonoAction : MonoBehaviour, IAction { public abstract void Invoke(); }
public class MonoActionConfigurable : MonoAction { [SerializeReference] public IAction[] actions; public override void Invoke(); }
public class MonoActionComposite : MonoAction   // [SerializeField] MonoAction[]
public sealed class MonoActionReference : IAction

// Elements/Scripts/Actions/Extensions.cs
public static void InvokeRange(this IAction[] actions);
public static void InvokeRange<T>(this IAction<T>[] actions, T arg);
```
`PrintAction` не существует — тип называется `LogAction`. `CompositeAction` держит `System.Action`-делегаты, которые Unity не сериализует, поэтому собрать его в инспекторе нельзя; для этого есть `MonoActionConfigurable` с `[SerializeReference] IAction[]`.

```csharp
public interface IExpression<R> : IReactiveList<ExpressionMember<R>>, IValue<R> { }

[Serializable] public struct ExpressionMember<R>
{
    public object Source { get; }
    public ExpressionMember(Func<R> func);
    public ExpressionMember(IFunction<R> func);
    public ExpressionMember(object source, Func<R> func);
    public ExpressionMember(object source, IFunction<R> func);
    public readonly bool EqualsFunction(Func<R> func);
    public readonly R Invoke();
}

public abstract class ExpressionBase<R> : ReactiveLinkedList<ExpressionMember<R>>, IExpression<R>
{ public R Value { get; } public R Invoke(); }

[Serializable] public class AndExpression : ExpressionBase<bool>, IPredicate
{
    public AndExpression(int capacity = INITIAL_CAPACITY);
    public AndExpression(params ExpressionMember<bool>[] members);
    public AndExpression(IEnumerable<ExpressionMember<bool>> members);
}
[Serializable] public class OrExpression : ExpressionBase<bool>, IPredicate { /* те же три ctor */ }
// + AndExpression<T>, AndExpression<T1,T2>, IntSumExpression, IntMulExpression, FloatSumExpression, FloatMulExpression, InlineExpression<R>

// Elements/Scripts/Expressions/Extensions.cs — AggressiveInlining
public static IExpression<R> Add<R>(this IExpression<R> it, Func<R> func);
public static IExpression<R> Add<R>(this IExpression<R> it, IFunction<R> func);
public static IExpression<R> Add<R>(this IExpression<R> it, object source, Func<R> func);
public static bool Remove<R>(this IExpression<R> it, Func<R> value);
public static bool Remove<R>(this IExpression<R> it, IFunction<R> value);
public static bool Remove<R>(this IExpression<R> it, object source);
```
`Elements/Scripts/Expressions/`. У `params`-конструктора **нет** неявного преобразования из `Func<bool>` — лямбду надо обернуть в `ExpressionMember<bool>` вручную.

```csharp
[Serializable] public sealed class AndPredicate : IPredicate
{
    [SerializeReference] private IPredicate[] conditions;
    public AndPredicate(); public AndPredicate(IPredicate[] conditions);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool Invoke();
}
[Serializable] public sealed class OrPredicate  : IPredicate { /* та же форма */ }
[Serializable] public sealed class NotPredicate : IPredicate { [SerializeReference] private IPredicate condition; public bool Invoke(); }
public class InlinePredicate : InlineFunction<bool>, IPredicate { public InlinePredicate(Func<bool> func); }

// Elements/Scripts/Functions/Extensions.cs
public static bool InvokeNot(this IFunction<bool> it);
public static InlineFunction<bool> Negate(this IFunction<bool> it);
```
`Elements/Scripts/Functions/`. Предикаты — на массиве, с циклом по `Length`; `[MethodImpl(AggressiveInlining)]` при этом стоит только на `AndPredicate.Invoke()` — у `OrPredicate.Invoke()` и `NotPredicate.Invoke()` его нет. Для фиксированного набора условий это дешевле выражения, а `[SerializeReference]` делает их собираемыми в инспекторе.

### Atomic.Elements: время

```csharp
public interface ICompleteSource { event Action OnCompleted; bool IsCompleted(); }
public interface IDurationSource { event Action<float> OnDurationChanged; float GetDuration(); void SetDuration(float d); }
public interface IProgressSource { event Action<float> OnProgressChanged; float GetProgress(); void SetProgress(float p); }
public interface ITimeSource     { event Action<float> OnTimeChanged; float GetTime(); void SetTime(float t); void ResetTime(); }
public interface ITickSource     { void Tick(float deltaTime); }
public interface IStartSource    { event Action OnStarted, OnStopped; bool IsStarted(); void Start(float time); void Start(); void Stop(); }
public interface IPauseSource    { event Action OnPaused, OnResumed; bool IsPaused(); void Pause(); void Resume(); }
public interface IStateSource<out T> where T : Enum { event Action<T> OnStateChanged; T GetState(); }

public interface ICooldown : IDurationSource, ITimeSource, IProgressSource, ICompleteSource, ITickSource
{ bool IsPlaying(); }

public interface ITimer : IStartSource, IPauseSource, ICompleteSource, IStateSource<TimerState>,
                          ITimeSource, IDurationSource, IProgressSource, ITickSource { }
public enum TimerState { IDLE = 0, PLAYING = 1, PAUSED = 2, COMPLETED = 3 }
```
`Elements/Scripts/Time/Sources/`, `Time/Cooldown/ICooldown.cs`, `Time/Timer/ITimer.cs`. Принцип узких интерфейсов применим и здесь: UI-презентеру достаточно `IProgressSource`, тикеру — `ITickSource`, гейту механики — `ICompleteSource`.

```csharp
[Serializable] public class Cooldown : ICooldown
{
    public event Action<float> OnDurationChanged, OnTimeChanged, OnProgressChanged;
    public event Action OnCompleted;
    public float Duration { get; set; } public float CurrentTime { get; set; } public float Progress { get; set; }
    public Cooldown(float duration); public Cooldown(float duration, float current);
    public static implicit operator Cooldown(float duration);
    public static implicit operator Cooldown(int duration);
    public bool IsCompleted();   // МЕТОД, не свойство
    public bool IsPlaying();
    public float GetProgress(); public void SetProgress(float p);
    public float GetDuration();  public void SetDuration(float d);
    public float GetTime();      public void SetTime(float t);  public void ResetTime();
    public void Tick(float deltaTime);
}
```
`Elements/Scripts/Time/Cooldown/Cooldown.cs:17`. События — `OnDurationChanged`, `OnTimeChanged`, `OnProgressChanged`, `OnCompleted`; `OnStateChanged` у `Cooldown` **нет** (он живёт в `IStateSource<T>`, то есть у `ITimer`/`IStopwatch`).

Реализации времени целиком: `Cooldown`, `RandomCooldown`, `ThreadSafeCooldown`; `DownTimer`, `UpTimer` (оба `[Serializable] : ITimer`); `Period`/`IPeriod`/`PeriodState`; `Stopwatch`/`IStopwatch`/`StopwatchState`; `FixedTimestamp`, `DurationTimestamp`, `ITimestamp`, `IDurationTimestamp`. Типа с именем `Timer` в исходниках нет.

### Atomic.Elements: прочее

```csharp
[Serializable] public struct Optional<T>
{
    public T Value { get; set; }       // сеттер ставит active = value != null
    public bool Active { get; set; }
    public bool TryGetValue(out T value);
    public T GetValueOrDefault(T defaultValue);
    public static implicit operator Optional<T>(T it);   // active = it != null
    public static implicit operator T(Optional<T> it);
    public static bool operator true(Optional<T> it);    // НЕ operator bool
    public static bool operator false(Optional<T> it);
}
```
`Elements/Scripts/Utils/Optional.cs:21`. `operator true`/`operator false`, а не `operator bool` — поэтому `if (_x)` работает, а `!_x`, `_x && y`, `bool b = _x` — нет.

```csharp
public class DisposableComposite<T> : IDisposable where T : IDisposable
{
    public int Count { get; }
    public DisposableComposite(IEnumerable<T> disposables);
    public DisposableComposite(params T[] disposables);   // => new DisposableComposite<T>() валиден
    public DisposableComposite<T> Add(T disposable);      // возвращает себя
    public void Dispose();                                 // Dispose всех + Clear
}
public class DisposableComposite : DisposableComposite<IDisposable> { }

// Elements/Scripts/Utils/Extensions.cs
public static void AddTo(this IDisposable it, DisposableComposite composite);              // БОКСИРУЕТ struct-подписки
public static void AddTo<T>(this T it, DisposableComposite<T> composite) where T : IDisposable;  // не боксирует
```
`AddTo` возвращает `void` — цепочку дальше не продолжить.

```csharp
public static class AtomicUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsPlayMode();  // в билде всегда true
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool IsEditMode();  // в билде всегда false
}
```
`Elements/Scripts/Utils/AtomicUtils.cs`. `IsEditMode()` дополнительно проверяет `!EditorApplication.isCompiling`.

### Кодогенерация доступа (связь с главой про codegen)

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class EntityExtensionsAPIAttribute : Attribute
{
    public bool Unsafe { get; set; }                       // GetValueUnsafe<T> + RefXxx() вместо GetValue<T>
    public bool AggressiveInlining { get; set; } = true;
}
[AttributeUsage(AttributeTargets.Field)]
public sealed class UnsafeAttribute : Attribute { }
```
`Entities/Scripts/Codegen/`. Генератор (`SourceGenerators/Project~/EntityAPIGenerator/`) читает **статические** поля типов `ValueKey<…>`/`TagKey<…>` из класса с этим атрибутом и выпускает `Has{Name}Tag`/`Add{Name}Tag`/`Del{Name}Tag` и `Get{Name}`/`TryGet{Name}`/`Add{Name}`/`Has{Name}`/`Del{Name}`/`Set{Name}` (+ `Ref{Name}` для unsafe), каждый — обёртка над `entity.HasTag(Keys.X.Id)` c `[MethodImpl(AggressiveInlining)]`. Именно отсюда берутся `AddHealth`, `GetFireEvent`, `AddCharacterTag` из всех примеров Best Practices: это **ваш** сгенерированный код, а не API фреймворка.

---

## Идиомы

Во всех примерах ниже `IUnitEntity`/`IWeaponEntity` и ключи `UnitAPI`/`WeaponAPI` — проектные типы; всё остальное — проверенное API фреймворка.

### 1. Класс ключей как единственный источник имён

```csharp
using Atomic.Entities;
using Atomic.Elements;
using UnityEngine;

public interface IUnitEntity : IEntity { }

public sealed class UnitEntity : Entity, IUnitEntity
{
    public UnitEntity(
        string name = null,
        int tagCapacity = 0,
        int valueCapacity = 0,
        int behaviourCapacity = 0,
        Settings? settings = null
    ) : base(name, tagCapacity, valueCapacity, behaviourCapacity, settings)
    {
    }
}

[EntityExtensionsAPI]
public static partial class UnitAPI            // partial — ОБЯЗАТЕЛЬНО: генератор дописывает этот же класс
{
    public static readonly TagKey<IUnitEntity> Damageable = new TagKey<IUnitEntity>("Damageable");

    public static readonly ValueKey<IUnitEntity, IValue<float>> MoveSpeed =
        new ValueKey<IUnitEntity, IValue<float>>("MoveSpeed");

    public static readonly ValueKey<IUnitEntity, IReactiveVariable<Vector3>> MoveDirection =
        new ValueKey<IUnitEntity, IReactiveVariable<Vector3>>("MoveDirection");

    public static readonly ValueKey<IUnitEntity, IReactiveVariable<Vector3>> Position =
        new ValueKey<IUnitEntity, IReactiveVariable<Vector3>>("Position");

    public static readonly ValueKey<IUnitEntity, IReactiveValue<int>> Health =
        new ValueKey<IUnitEntity, IReactiveValue<int>>("Health");
}
```

Почему так. `ValueKey<E,T>` считает `Id` один раз в конструкторе статического поля; дальше каждое обращение — `entity.GetValue<T>(int)`, без прохода через `EntityKeyStore.NameToId`. Второй type-параметр закрепляет тип значения на стороне чтения, а первый не даёт случайно применить ключ юнита к сущности другого типа. Пятый параметр `Settings?` в конструкторе `UnitEntity` пробрасывать стоит сразу: без него нельзя будет отключить `disposeValues` для пулящихся сущностей.

**Антипаттерн из документации** — строковые ключи во всех примерах:

```csharp
// ❌ хеширование строки на каждом обращении, тип значения не проверяется
entity.AddValue("MoveDirection", new ReactiveVariable<Vector3>());
ISetter<Vector3> dir = entity.GetValue<ISetter<Vector3>>("MoveDirection");

// ✅
entity.AddValue(UnitAPI.MoveDirection, new ReactiveVariable<Vector3>());
ISetter<Vector3> dir = entity.GetValue(UnitAPI.MoveDirection);
```

Строковый `AddValue<ISetter<Vector3>>("MoveDirection", …)` из `UsingSetters.md` вообще не компилируется: обобщённой строковой перегрузки нет.

### 2. Модульные установщики вместо одного толстого

```csharp
[Serializable]
public sealed class MovementInstaller : IEntityInstaller<IUnitEntity>
{
    [SerializeField] private Const<float> _moveSpeed = 3f;

    public void Install(IUnitEntity entity)
    {
        entity.AddValue(UnitAPI.MoveSpeed, _moveSpeed);
        entity.AddValue(UnitAPI.MoveDirection, new ReactiveVariable<Vector3>());
        entity.AddBehaviour<MovementBehaviour>();
    }
}

[Serializable]
public sealed class TransformInstaller : IEntityInstaller<IUnitEntity>
{
    [SerializeField] private Transform _transform;

    public void Install(IUnitEntity entity)
    {
        entity.AddValue(UnitAPI.Position, new ReactiveVariable<Vector3>(_transform.position));
    }
}

public sealed class CharacterInstaller : MonoEntityInstaller<IUnitEntity>
{
    [SerializeField] private TransformInstaller _transformInstaller;
    [SerializeField] private MovementInstaller _movementInstaller;

    public override void Install(IUnitEntity entity)      // override — Install(E) объявлен abstract
    {
        entity.AddTag(UnitAPI.Damageable);
        entity.Install(_transformInstaller);
        entity.Install(_movementInstaller);
    }
}
```

Почему так. `[Serializable]`-класс, реализующий `IEntityInstaller<E>`, рисуется в инспекторе как вложенное поле, поэтому один и тот же модуль переиспользуется в установщике персонажа, фабрике и бейкере без наследования. `MonoEntityInstaller<E>` — только точка входа со сцены.

Три ошибки в документации на этом месте: `public void Install(…)` без `override` (метод `abstract` в базе — нужен `override`); параметр, типизированный не тем интерфейсом (`public void Install(IWeaponEntity entity)` в `MonoEntityInstaller<IEnemyEntity>`); `public override void Install(IEntity entity)` в обобщённом установщике (в базе это `sealed override`).

### 3. Узкие интерфейсы в поведениях, конкретика — только в сборке

```csharp
public sealed class MovementBehaviour : IEntityInit<IUnitEntity>, IEntityFixedTick<IUnitEntity>
{
    private IValue<float> _speed;
    private IValue<Vector3> _direction;
    private IVariable<Vector3> _position;

    public void Init(IUnitEntity entity)
    {
        _speed = entity.GetValue(UnitAPI.MoveSpeed);
        _direction = entity.GetValue(UnitAPI.MoveDirection);
        _position = entity.GetValue(UnitAPI.Position);
    }

    public void FixedTick(IUnitEntity entity, float deltaTime)
    {
        _position.Value += _direction.Value * (_speed.Value * deltaTime);
    }
}
```

Почему так. Поведение не знает, лежит ли под `IValue<float>` сериализованный `Const<float>`, `ScriptableConst<float>` или `InlineFunction<float>`, читающий из конфига — подменяется без правок логики (`PreferAbstractInterfaces.md`). Второе: все разрешения ключей вынесены в `Init`, в `FixedTick` остаётся только арифметика. Третье: типизированные `IEntityInit<E>`/`IEntityFixedTick<E>` избавляют от касту `(IUnitEntity) entity` в каждом тике.

**Антипаттерн:**

```csharp
// ❌ конкретные типы + разрешение ключей в тике
public sealed class MovementBehaviour : IEntityTick
{
    public void Tick(IEntity entity, float deltaTime)
    {
        ReactiveVariable<Vector3> position = entity.GetValue<ReactiveVariable<Vector3>>("Position");
        Const<float> speed = entity.GetValue<Const<float>>("MoveSpeed");
        position.Value += ... ;
    }
}
```

### 4. Один объект — два узких интерфейса: `ISignal` слушать, `IAction` вызывать

```csharp
// сборка
entity.AddValue(WeaponAPI.FireEvent, new Event());     // Event : IEvent (= ISignal + IAction)

// потребитель-слушатель
ISignal fire = entity.GetValue(WeaponAPI.FireEvent);
Subscription subscription = fire.Subscribe(OnFire);    // храним! struct, но Dispose нужен

// потребитель-инициатор
IAction fire = entity.GetValue(WeaponAPI.FireEvent);
fire.Invoke();
```

Почему так. `IEvent : ISignal, IAction` — один и тот же объект отдаётся читателю и писателю под двумя разными правами. Ключ `WeaponAPI.FireEvent` при этом должен быть объявлен как `ValueKey<IWeaponEntity, IEvent>`; присваивания к `ISignal`/`IAction` — обычные неявные преобразования ссылок.

### 5. `ISetter<T>` для производителей, которые только пишут

```csharp
public sealed class MoveInputController : IEntityInit<IUnitEntity>, IEntityTick<IUnitEntity>
{
    private ISetter<Vector3> _moveDirection;

    public void Init(IUnitEntity entity) =>
        _moveDirection = entity.GetValue(UnitAPI.MoveDirection);

    public void Tick(IUnitEntity entity, float deltaTime)
    {
        float dx = Input.GetAxis("Horizontal");
        float dz = Input.GetAxis("Vertical");
        _moveDirection.Value = new Vector3(dx, 0f, dz);
    }
}
```

Почему так. Контроллер ввода по типу поля не может прочитать направление — только записать. Это ловит классическую ошибку «ввод читает своё же состояние и залипает». `ISetter<T> : IAction<T>`, поэтому тот же объект годится и как обработчик сигнала.

### 6. Request в `Tick`, Consume в `FixedTick`

```csharp
// сборка
entity.AddValue(UnitAPI.MoveRequest, new Request<Vector3>());

// производитель — кадровый поток
public sealed class MoveInputController : IEntityInit<IUnitEntity>, IEntityTick<IUnitEntity>
{
    private IRequest<Vector3> _moveRequest;

    public void Init(IUnitEntity entity) => _moveRequest = entity.GetValue(UnitAPI.MoveRequest);

    public void Tick(IUnitEntity entity, float deltaTime)
    {
        float dx = Input.GetAxis("Horizontal");
        float dz = Input.GetAxis("Vertical");
        _moveRequest.Invoke(new Vector3(dx, 0f, dz));
    }
}

// потребитель — физический поток
public sealed class MoveBehaviour : IEntityInit<IUnitEntity>, IEntityFixedTick<IUnitEntity>
{
    private IRequest<Vector3> _moveRequest;
    private IValue<float> _speed;
    private IVariable<Vector3> _position;

    public void Init(IUnitEntity entity)
    {
        _moveRequest = entity.GetValue(UnitAPI.MoveRequest);
        _speed = entity.GetValue(UnitAPI.MoveSpeed);
        _position = entity.GetValue(UnitAPI.Position);
    }

    public void FixedTick(IUnitEntity entity, float deltaTime)
    {
        if (_moveRequest.Consume(out Vector3 direction))
            _position.Value += direction * (_speed.Value * deltaTime);
    }
}
```

Почему так. `Request<T>` — это один флаг `_required` плюс один слот аргумента; повторные `Invoke` в одном кадре перезаписывают аргумент, а не накапливают очередь. Это ровно то, что нужно для ввода: `Update` может вызваться дважды между `FixedUpdate`, но движение применится один раз. `RequestsVsActions.md` отдельно предупреждает: в сетевых играх предпочтительны `IAction`, потому что флаг запроса пришлось бы синхронизировать.

### 7. Request → Condition → Action → Event одной сборкой

```csharp
// сборка
IReactiveValue<int> health = entity.GetValue(UnitAPI.Health);
IReactiveVariable<bool> grounded = entity.GetValue(UnitAPI.Grounded);

entity.AddValue(UnitAPI.JumpRequest, new Request());
entity.AddValue(UnitAPI.JumpCondition, new AndExpression(
    new ExpressionMember<bool>(() => health.Value > 0),
    new ExpressionMember<bool>(() => grounded.Value)
));
entity.AddValue(UnitAPI.JumpAction, new InlineAction(() => rigidbody.AddForce(Vector3.up, ForceMode.Impulse)));
entity.AddValue(UnitAPI.JumpEvent, new Event());
entity.AddBehaviour<JumpBehaviour>();
```

```csharp
public sealed class JumpBehaviour : IEntityInit<IUnitEntity>, IEntityFixedTick<IUnitEntity>
{
    private IRequest _request;
    private IValue<bool> _condition;
    private IAction _action;
    private IEvent _event;

    public void Init(IUnitEntity entity)
    {
        _request = entity.GetValue(UnitAPI.JumpRequest);
        _condition = entity.GetValue(UnitAPI.JumpCondition);
        _action = entity.GetValue(UnitAPI.JumpAction);
        _event = entity.GetValue(UnitAPI.JumpEvent);
    }

    public void FixedTick(IUnitEntity entity, float deltaTime)
    {
        if (!_condition.Value)
            return;                 // запрос остаётся висеть до выполнения условия

        if (_request.Consume())
        {
            _action.Invoke();
            _event.Invoke();
        }
    }
}
```

Почему так. Четыре звена лежат в таблице значений независимо, поэтому условие можно подкрутить из бафф-аспекта, действие подменить, а на событие подписаться из VFX-установщика, ничего не зная о прыжке. Ключ `JumpCondition` объявлен как `ValueKey<IUnitEntity, IValue<bool>>`: `AndExpression → ExpressionBase<bool> → IExpression<bool> → IValue<bool>`.

**Антипаттерн из `RequestConditionActionEvent.md`** — и он не компилируется, и семантика другая:

```csharp
// ❌ 1) new AndExpression(lambda, lambda) — нет преобразования Func<bool> → ExpressionMember<bool>
// ❌ 2) поле объявлено IFunction<bool>, а читается .Value — у IFunction<bool> есть только Invoke()
// ❌ 3) Consume() стоит первым: при ложном условии запрос съедается молча
if (_jumpRequest.Consume() && _jumpCondition.Value) { ... }
```

Порядок — это решение, а не деталь. `Consume()` первым = «запрос живёт один кадр»; условие первым = «запрос ждёт разрешения». Ошибка — не выбрать осознанно. `UsingCooldownInGameMechanics.md` в примере рукопашного боя, кстати, ставит проверки перед `Consume` — правильно.

### 8. Фиксированный набор условий — предикат, изменяемый — выражение

```csharp
// фиксированный: массив, цикл по Length, AggressiveInlining, собирается в инспекторе
[SerializeReference] private IPredicate _fireCondition;   // в инспекторе: AndPredicate{ ... }

// или в коде
IPredicate fireCondition = new AndPredicate(new IPredicate[]
{
    new InlinePredicate(() => health.Value > 0),
    new InlinePredicate(() => ammo.Value > 0)
});
```

```csharp
// изменяемый в рантайме: AndExpression — это ReactiveLinkedList<ExpressionMember<bool>>
IExpression<bool> fireCondition = entity.GetValue(UnitAPI.FireCondition);

fireCondition.Add(Constants.False);      // полностью блокируем (разоружили)
fireCondition.Remove(Constants.False);   // снимаем блокировку
```

Почему так. `AndExpression` платит за изменяемость структурой: связный список, по узлу на член, один вызов делегата на член при каждом чтении `Value`. Если набор условий не меняется — `AndPredicate` с массивом дешевле и к тому же сериализуется через `[SerializeReference]`. `Constants.False` — это `static readonly Const<bool>`, то есть общий на весь процесс экземпляр, и `Remove(Constants.False)` находит его снова, потому что сравнение делегатов идёт по цели и методу.

**Антипаттерн из `UsingExpressions.md`:**

```csharp
// ❌ лямбда — новый объект, и снять её потом нечем: нужен тот же экземпляр делегата
condition.Add(() => false);
```

### 9. `InlineFunction<T>` абстрагирует источник данных — но проксирующая переменная лучше

```csharp
// документация: лямбда-замыкание на каждую сущность
entity.AddValue(UnitAPI.PositionSource, new InlineFunction<Vector3>(() => _rigidbody.position));

// без замыкания: готовый прокси из Elements/Scripts/Variables/Proxy
entity.AddValue(UnitAPI.PositionVar, new TransformPositionVariable(_transform));
```

Почему так. `InlineFunction<R>` хранит `Func<R>`; лямбда, захватывающая `this`/`_rigidbody`, — это ещё один объект класса замыкания на сущность. `TransformPositionVariable` (а также `TransformRotationVariable`, `TransformScaleVariable`, `TransformParentVariable`, `GameObjectLayerVariable`, `IntPrefsVariable`/`FloatPrefsVariable`/`BoolPrefsVariable`/`StringPrefsVariable`) делает то же самое одним объектом с полем `Transform` и даёт `IVariable<Vector3>` — то есть ещё и запись. Там, где прокси нет, `InlineFunction` остаётся правильным ответом.

### 10. Cooldown тикается жизненным циклом сущности, а не `MonoBehaviour.Update`

```csharp
[Serializable]
public sealed class FireCooldownInstaller : IEntityInstaller<IWeaponEntity>
{
    [SerializeField] private Cooldown _cooldown = 0.5f;   // implicit operator Cooldown(float)

    public void Install(IWeaponEntity entity)
    {
        entity.AddValue(WeaponAPI.FireCooldown, _cooldown);
        entity.WhenFixedTick(_cooldown.Tick);             // FixedTickSubscription — см. грабли
    }
}
```

```csharp
// гейт механики: достаточно ICompleteSource
public sealed class FireBehaviour : IEntityInit<IWeaponEntity>, IEntityFixedTick<IWeaponEntity>
{
    private ICompleteSource _cooldown;
    private IRequest _fireRequest;

    public void Init(IWeaponEntity entity)
    {
        _cooldown = entity.GetValue(WeaponAPI.FireCooldown);
        _fireRequest = entity.GetValue(WeaponAPI.FireRequest);
    }

    public void FixedTick(IWeaponEntity entity, float deltaTime)
    {
        if (_cooldown.IsCompleted() && _fireRequest.Consume())
        {
            // выстрел
            entity.GetValue(WeaponAPI.FireCooldown).ResetTime();
        }
    }
}
```

Почему так. Сущность — сама источник тика (`IEntity : ITickSource`), поэтому отключение сущности автоматически замораживает её кулдауны; с `MonoBehaviour.Update` пришлось бы отдельно гасить компонент. `IsCompleted()` — метод, не свойство. Для UI берите `IProgressSource` — презентеру не нужны `SetDuration`/`Tick`:

```csharp
public sealed class CooldownBarPresenter
{
    public CooldownBarPresenter(IProgressSource cooldown, Image bar)
    {
        bar.fillAmount = cooldown.GetProgress();
        cooldown.OnProgressChanged += p => bar.fillAmount = p;
    }
}
```

`Cooldown` против `ITimer` (`ChosingBetweenTimerAndCooldown.md`): кулдаун — интервал между действиями, у него нет состояния «запущен/на паузе/остановлен»; `DownTimer`/`UpTimer` реализуют `ITimer` с `Start`/`Pause`/`Resume`/`Stop` и `IStateSource<TimerState>` — берите их, когда отсчёт сам является игровым объектом (раунд, бафф).

### 11. Презентер владеет struct-подпиской

```csharp
public sealed class HealthBarPresenter : IDisposable
{
    private readonly Subscription<int> _subscription;   // struct, readonly-поле
    private readonly Image _bar;
    private readonly int _max;

    public HealthBarPresenter(IReactiveValue<int> health, int max, Image bar)
    {
        _bar = bar;
        _max = max;
        _subscription = health.Observe(this.OnHealthChanged);   // подписка + немедленный вызов
    }

    private void OnHealthChanged(int value) => _bar.fillAmount = value / (float) _max;

    public void Dispose() => _subscription.Dispose();
}
```

Почему так. `Observe` сначала вызывает колбэк текущим значением, потом подписывается — UI не ждёт первого изменения, чтобы показать правду. Возвращённый `Subscription<T>` — `readonly struct`; в `readonly`-поле он лежит без аллокации. Важное свойство реализации: `Observe` вызывает `action` **до** конструирования подписки, поэтому исключение в колбэке оставит объект вообще без подписки.

### 12. Много подписок — типизированный композит, а не `DisposableComposite`

```csharp
public sealed class WeaponViewInstaller : MonoEntityInstaller<IWeaponEntity>
{
    [SerializeField] private ParticleSystem _fireVFX;
    [SerializeField] private AudioSource _fireSFX;

    private readonly DisposableComposite<Subscription> _subscriptions = new();

    public override void Install(IWeaponEntity entity)
    {
        ISignal fire = entity.GetValue(WeaponAPI.FireEvent);
        fire.Subscribe(_fireVFX.Play).AddTo(_subscriptions);
        fire.Subscribe(_fireSFX.Play).AddTo(_subscriptions);
    }

    public override void Uninstall(IWeaponEntity entity)   // параметр обязателен
    {
        _subscriptions.Dispose();
    }
}
```

Почему так. `Subscribe` возвращает `Subscription` — `readonly struct`. Перегрузка `AddTo(this IDisposable, DisposableComposite)` принимает интерфейс, то есть **боксирует каждую подписку**, а необобщённый `DisposableComposite : DisposableComposite<IDisposable>` этого избежать не позволяет. Обобщённая перегрузка `AddTo<T>(this T, DisposableComposite<T>) where T : IDisposable` с полем `DisposableComposite<Subscription>` боксирования не делает. Это ограничивает композит одним типом подписок — если нужны разнородные, боксирование неизбежно, и тогда просто решите это сознательно.

**Антипаттерн из `UsingSubscriptionsWithDisposeComposite.md` и `UninstallEntityInstaller.md`** — он не компилируется:

```csharp
// ❌ параметрless Uninstall() не существует ни в одном из установщиков
public override void Uninstall() { _disposables.Dispose(); }
```

В `MonoEntityInstaller` объявлено `public virtual void Uninstall(IEntity entity)`, в `MonoEntityInstaller<E>` — `sealed override` для `IEntity` плюс `public virtual void Uninstall(E entity)`. То же для `ScriptableEntityInstaller`. Подтверждается тестовым дублёром `Entities/Tests/Substitutions/Installers/MonoEntityInstallerSpy.cs`, который переопределяет `Uninstall(IEntity entity)`.

Отдельно: `Uninstall` вызывает не сущность, а `EntityView<E>.Deactivate()` — для каждого установщика из своего списка. Если ваша сущность живёт без view, `Uninstall` никто не позовёт; вешайте очистку на `entity.WhenDispose(...)`.

### 13. `Optional<T>` для фич, включаемых в инспекторе

```csharp
[Serializable]
public sealed class AmmoInstaller : IEntityInstaller<IWeaponEntity>
{
    [SerializeField] private Optional<ReactiveInt> _ammo;
    [SerializeField] private Optional<Cooldown> _cooldown;

    public void Install(IWeaponEntity entity)
    {
        if (_ammo.TryGetValue(out ReactiveInt ammo))
            entity.AddValue(WeaponAPI.Ammo, ammo);

        if (_cooldown.Active)
            entity.AddValue(WeaponAPI.FireCooldown, _cooldown.Value);
    }
}
```

Почему так. `Optional<T>` сериализуется как `bool active` + `T value` и рисуется одним свёрнутым полем — дизайнер включает механику галочкой, код остаётся один. Предпочитайте `TryGetValue`/`Active` сокращению `if (_ammo)`: у `Optional<T>` определены `operator true`/`operator false`, а не `operator bool`, поэтому `if (_x)` работает, а `!_x`, `_x && y` и `bool b = _x` — нет, и это обнаружится в самый неудобный момент.

Тот же тип закрывает оверрайд общей конфигурации в сцене:

```csharp
[Serializable]
public sealed class MoveOverrideBaker : IEntityInstaller<IUnitEntity>
{
    [SerializeField] private Optional<float> _moveSpeed;

    public void Install(IUnitEntity entity)
    {
        if (_moveSpeed.Active)
            entity.SetValue(UnitAPI.MoveSpeed, new Const<float>(_moveSpeed.Value));
    }
}
```

### 14. Общий `Const<T>` вместо скопированных float — и только `Const`

```csharp
public sealed class UnitConfig : ScriptableObject
{
    [SerializeField] public Const<float> moveSpeed = 5f;
    [SerializeField] public Const<float> rotationSpeed = 1440f;
}

[Serializable]
public sealed class ConfiguredMovementInstaller : IEntityInstaller<IUnitEntity>
{
    [SerializeField] private UnitConfig _config;

    public void Install(IUnitEntity entity) =>
        entity.AddValue(UnitAPI.MoveSpeed, _config.moveSpeed);   // один объект на всех юнитов
}
```

Почему так. `Const<T>` — `[Serializable]`-класс, реализующий `IValue<T>`: в таблицу значений он кладётся один раз как ссылка, а не боксируется на каждую сущность, и правка в ассете видна всем сразу (`SharedConstants.md`, flyweight). Критично: шарить так можно **только** не-`IDisposable` обёртки — `Const<T>`, `ScriptableConst<T>`, `InlineValue<T>`. `ReactiveVariable<T>`, `Event`, `ReactiveList<T>` реализуют `IDisposable`, а `Entity.Dispose()` при `disposeValues = true` (значение по умолчанию!) вызовет `Dispose()` у каждого такого значения — первая умершая сущность убьёт общий объект для всех остальных.

### 15. Фабрика создаёт, бейкер переопределяет в сцене

```csharp
[CreateAssetMenu(menuName = "Game/Unit Factory")]
public sealed class UnitFactory : ScriptableEntityFactory<IUnitEntity, NoArgs>
{
    [SerializeField] private TransformInstaller _transformInstaller;
    [SerializeField] private MovementInstaller _movementInstaller;

    protected override IUnitEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        NoArgs args
    )
    {
        UnitEntity entity = new UnitEntity(
            this.name, tagCapacity, valueCapacity, behaviourCapacity, settings);

        entity.Install(_transformInstaller);
        entity.Install(_movementInstaller);
        return entity;
    }
}
```

```csharp
public abstract class UnitBaker : MonoEntityBaker<IUnitEntity, NoArgs>
{
    [SerializeField] private UnitFactory _factory;

    protected sealed override IUnitEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        NoArgs args
    )
    {
        IUnitEntity entity = _factory.Create(args);   // ёмкости берутся из АССЕТА фабрики
        this.Override(entity);
        return entity;
    }

    protected abstract void Override(IUnitEntity entity);
}

public sealed class TankBaker : UnitBaker
{
    [SerializeField] private MoveOverrideBaker _moveOverride;

    protected override void Override(IUnitEntity entity) => entity.Install(_moveOverride);
}
```

```csharp
// запекание всей сцены
IUnitEntity[] units = UnitBaker.BakeAll(NoArgs.Default);
```

Почему так. Фабрика — ассет, определяющий тип юнита; бейкер — компонент на сцене, который правит конкретный экземпляр и затем уничтожает свой `GameObject` (`protected virtual void Release() => Destroy(this.gameObject);`). Разработчик владеет фабрикой, дизайнер — расстановкой и оверрайдами. Обратите внимание: сериализованные ёмкости самого бейкера в этой схеме не используются — они передаются в `Create`, а мы их игнорируем в пользу значений, пришедших из фабрики.

Оверрайд-установщик отличается от обычного тем, что только **правит** уже созданное: `entity.SetValue(...)`, а не `AddValue(...)`. `AddValue` на существующий ключ бросает `ArgumentException`.

### 16. Запечатать `Create`, открыть `Install`

```csharp
public sealed class BulletFactory : ScriptableEntityFactory<NoArgs>   // необобщённый по сущности
{
    [SerializeField] private Const<int> _damage = 10;

    protected override void Install(IEntity entity)      // единственное, что надо написать
    {
        entity.AddValue(BulletAPI.Damage, _damage);
        entity.AddBehaviour<BulletMoveBehaviour>();
    }
}
```

Почему так. `ScriptableEntityFactory<TArgs>` уже реализует `protected override IEntity Create(...)`: создаёт `new Entity(this.Name, ёмкости, settings)` и зовёт ваш `Install`. Создание едино для всех фабрик, различается только конфигурация. Цена — сущность всегда `Entity`, без своего типа; если нужен `UnitEntity`, наследуйтесь от двухпараметрического `ScriptableEntityFactory<TEntity, TArgs>`, как в идиоме 15.

`Name` в этой иерархии **уже объявлен** (`[field: SerializeField] public virtual string Name { get; private set; }`), поэтому `public string Name => this.name;` из документации даёт CS0108 — сокрытие базового члена.

### 17. Зависимости создания — в `TArgs`, а не в поля билдера

```csharp
public sealed class PlayerContextFactory : IEntityFactory<PlayerContext, Args<Camera, TeamType>>
{
    public PlayerContext Create(Args<Camera, TeamType> args)
    {
        PlayerContext context = new PlayerContext("PlayerContext", 4, 8, 4);
        context.AddValue(PlayerAPI.Camera, args.value1);
        context.AddValue(PlayerAPI.Team, new Const<TeamType>(args.value2));
        return context;
    }
}

// вызов
PlayerContext context = factory.Create(new Args<Camera, TeamType>(camera, TeamType.Blue));
```

Почему так. Это и есть смысл рефакторинга на `IArgs`: то, что меняется от вызова к вызову, идёт параметром `Create`, а то, что фиксировано — сериализованным состоянием фабрики. Пул умеет пробрасывать `TArgs` (`EntityPool(factory, args, expandMode)`), поэтому фабрика с аргументами подключается к пулу, а фабрика с мутабельными полями билдера — нет.

Если fluent-синтаксис всё же нужен (много опциональных параметров), оставьте его обёрткой, но реализуйте `IEntityFactory<,>` честно:

```csharp
public sealed class PlayerContextBuilder : IEntityFactory<PlayerContext, NoArgs>
{
    private Camera _camera;
    private TeamType _team;

    public PlayerContextBuilder SetCamera(Camera camera) { _camera = camera; return this; }
    public PlayerContextBuilder SetTeam(TeamType team)   { _team = team;     return this; }

    public PlayerContext Create(NoArgs args)
    {
        if (_camera == null)
            throw new InvalidOperationException("Camera must be set before creating PlayerContext.");

        PlayerContext context = new PlayerContext("PlayerContext", 4, 8, 4);
        context.AddValue(PlayerAPI.Camera, _camera);
        context.AddValue(PlayerAPI.Team, new Const<TeamType>(_team));
        return context;
    }

    public PlayerContext Create() => this.Create(NoArgs.Default);
}
```

Версия из `UpgradingEntityFactoryToBuilder.md` (`IEntityFactory<PlayerContext>` с `public override PlayerContext Create()`) не компилируется: однопараметрический `IEntityFactory<T>` параметризован аргументами, а не сущностью, и `override` переопределять нечего. Регистрация `GameContext.SetFactory(builder)` из того же документа тоже вымышлена — синглтонов такого вида во фреймворке нет, есть только `MonoEntitySingleton<E>`.

### 18. Фабрика + пул + мир: загрузка подсистемы

```csharp
[Serializable]
public sealed class UnitSystemInstaller : IEntityInstaller<IGameContext>
{
    [SerializeField] private UnitCatalog _catalog;

    public void Install(IGameContext context)
    {
        MultiEntityPool<string, IUnitEntity, NoArgs> pool =
            new MultiEntityPool<string, IUnitEntity, NoArgs>(
                _catalog, NoArgs.Default, ExpandMode.ExpandByDoubling);

        pool.Init("Tank", 64);
        pool.Init("Soldier", 256);
        context.AddValue(GameAPI.UnitPool, pool);

        EntityWorld<IUnitEntity> world = new EntityWorld<IUnitEntity>("Units");
        context.AddValue(GameAPI.UnitWorld, world);

        world.BindTo(context);     // вместо восьми WhenXxx руками
    }
}
```

```csharp
public sealed class UnitCatalog : ScriptableEntityCatalog<string, IUnitEntity, UnitFactory, NoArgs>
{
    protected override string GetKey(UnitFactory factory) => factory.name;
}
```

Почему так. `BindTo<E>(this IEntityWorld<E>, IEntity)` делает ровно те восемь регистраций, которые `EntitySystem.md` предлагает выписывать вручную, и помечен `AggressiveInlining`. `ExpandByDoubling` вместо дефолтного `ExpandByOne` — иначе залповый спавн даст по аллокации на юнит. `NoExpand` — если нужен жёсткий лимит: при исчерпании `Rent()` бросит `InvalidOperationException`.

Спавн/деспавн тогда сводится к:

```csharp
public static class UnitsUseCase
{
    public static IUnitEntity Spawn(IGameContext context, string key, Vector3 position)
    {
        MultiEntityPool<string, IUnitEntity, NoArgs> pool = context.GetValue(GameAPI.UnitPool);
        IUnitEntity entity = pool.Rent(key);

        entity.GetValue(UnitAPI.Position).Value = position;

        EntityWorld<IUnitEntity> world = context.GetValue(GameAPI.UnitWorld);
        world.Add(entity);        // bool
        return entity;
    }

    public static bool Despawn(IGameContext context, IUnitEntity entity)
    {
        EntityWorld<IUnitEntity> world = context.GetValue(GameAPI.UnitWorld);
        if (!world.Remove(entity))
            return false;

        context.GetValue(GameAPI.UnitPool).Return(entity);
        return true;
    }
}
```

Ключи `GameAPI.UnitPool`/`UnitWorld` объявляются конкретными типами (`ValueKey<IGameContext, MultiEntityPool<string, IUnitEntity, NoArgs>>`, `ValueKey<IGameContext, EntityWorld<IUnitEntity>>`) — это тот случай, когда конкретный тип нужен: `foreach` по миру через `IEntityWorld<E>` боксирует энумератор.

### 19. Реактивный фильтр вместо ручного перебора

```csharp
EntityWorld<IUnitEntity> world = context.GetValue(GameAPI.UnitWorld);

EntityFilter<IUnitEntity> aliveEnemies = EntityFilter<IUnitEntity>.StartBuild()
    .WithSource(world)
    .WithPredicate(e => e.HasTag(UnitAPI.Damageable) && e.GetValue(UnitAPI.Health).Value > 0)
    .WithTrigger(new TagEntityTrigger<IUnitEntity>())
    .WithTrigger(new ValueEntityTrigger<IUnitEntity>(added: false, deleted: false, changed: true))
    .Build();

foreach (IUnitEntity enemy in aliveEnemies)   // EntityCollection<E>.Enumerator — struct
{
    // ...
}
```

Почему так. Без триггеров фильтр пересчитает сущность только при добавлении/удалении из источника, и «живые враги» придётся пересканировать самому. `TagEntityTrigger` подписывается на `OnTagAdded`/`OnTagDeleted`, `ValueEntityTrigger` — на `OnValueAdded`/`OnValueDeleted`/`OnValueChanged` (флаги в конструкторе позволяют слушать только нужное). `Builder` — `struct`, поэтому сама сборка не аллоцирует; `GetEnumerator()` фильтра возвращает `EntityCollection<E>.Enumerator` по значению.

Осторожно: `Builder` — значимый тип и возвращает `this` **по значению**. Цепочку нельзя разрывать без переприсваивания:

```csharp
// ❌ _source потерян: WithSource отработал на копии
var builder = EntityFilter<IUnitEntity>.StartBuild();
builder.WithSource(world);
builder.WithPredicate(p);
EntityFilter<IUnitEntity> filter = builder.Build();   // InvalidOperationException: Source must be provided

// ✅ либо непрерывная цепочка, либо builder = builder.WithSource(world);
```

### 20. Массовая обработка — `EntitySystem<E>`, а не поведение на сущность

```csharp
public sealed class MovementSystem : EntitySystem<IUnitEntity>
{
    public MovementSystem(IReadOnlyEntityCollection<IUnitEntity> source, Settings settings)
        : base(source, settings)
    {
    }

    protected override void Update(IUnitEntity entity, float deltaTime)
    {
        IVariable<Vector3> position = entity.GetValue(UnitAPI.Position);
        IValue<Vector3> direction = entity.GetValue(UnitAPI.MoveDirection);
        IValue<float> speed = entity.GetValue(UnitAPI.MoveSpeed);

        position.Value += direction.Value * (speed.Value * deltaTime);
    }
}
```

```csharp
// регистрация в жизненном цикле контекста
MovementSystem movement = new MovementSystem(world, new EntitySystemBase<IUnitEntity>.Settings());
context.AddFixedTickSystem(movement);   // WhenEnable/WhenFixedTick/WhenDisable/WhenDispose
```

Почему так. `EntitySystem<E>` держит сущности в собственном `E[]` с `Dictionary<E,int>` для O(1) удаления swap-back, проходит их кольцевым курсором по `batchSize` штук за кадр и подстраивает `batchSize` под `Settings.frameBudget` (по умолчанию 0.03 с, границы батча 1024…2048). Для RTS-масштаба это то, чего поведения на каждой сущности не дают: там цена — виртуальный вызов на сущность каждый кадр без всякого бюджета. Если часть сущностей важнее — `PriorityEntitySystem<E>` с `protected abstract EntityUpdatePriority EvaluatePriority(E entity)`.

### 21. Модель и представление связываются через `EntityWorldView`

```csharp
public class UnitView : EntityView<IUnitEntity>
{
    [field: SerializeField]
    public string Name { get; private set; }    // ОБЪЯВЛЯЕМ САМИ: у EntityView<E> его нет
}

public sealed class UnitViewCatalog : EntityViewCatalog<IUnitEntity, UnitView> { }

public sealed class UnitViewPool : EntityViewPool<string, IUnitEntity, UnitView>
{
    protected override string GetKey(UnitView view) => view.Name;
}

public sealed class UnitWorldView : EntityWorldView<string, IUnitEntity, UnitView>
{
    protected override string GetKey(IUnitEntity entity) => entity.Name;
}
```

```csharp
public sealed class EntryPoint : MonoBehaviour
{
    [SerializeField] private UnitWorldView _worldView;
    [SerializeField] private UnitViewPool _viewPool;

    private async void Start()
    {
        await _viewPool.InitAsync("Tank", 128);     // прогрев без фриза: InstantiateAsync
        await _viewPool.InitAsync("Soldier", 512);

        EntityWorld<IUnitEntity> world = /* ... */;
        _worldView.Activate(world);
    }
}
```

Почему так. `Activate(IReadOnlyEntityCollection<E>)` подписывается на `OnAdded`/`OnRemoved` источника и прогоняет текущее содержимое: добавили сущность в мир — view арендована из пула и `Activate`-нута, убрали — `Deactivate`-нута и возвращена. Модель ничего не знает о Unity. `InitAsync` возвращает `ValueTask` и внутри использует `Object.InstantiateAsync` — синхронный `Init` на сотнях префабов даёт заметный спайк, и это ровно тот случай, про который `EntitySystem.md` обещает «hundreds of units efficiently».

Документация здесь даёт невалидный код: `protected override string GetKey(UnitView view) => view.Name;` при `UnitView : EntityView<IUnitEntity>` не компилируется — `Name` объявлен только в необобщённом `EntityView`. Либо объявите свойство сами (как выше), либо верните `view.name` (унаследованное от `Object`), понимая, что `EntityView<E>.Activate` перезаписывает `name` через `FormateName(entity)`.

### 22. `[SerializeReference]` для действий, собираемых дизайнером

```csharp
public sealed class PlayerActionTrigger : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [SerializeReference] private IAction[] _actions;   // в инспекторе: LogAction, свои IAction

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(PlayerTag))
            _actions.InvokeRange();
    }
}
```

Почему так. `[SerializeReference]` сериализует полиморфно по ссылке, поэтому дизайнер набирает массив из любых `[Serializable]`-реализаций `IAction`; `InvokeRange(this IAction[])` из `Elements/Scripts/Actions/Extensions.cs` прогоняет его циклом по `Length`. Готовая альтернатива без своего компонента — `MonoActionConfigurable` с публичным `[SerializeReference] public IAction[] actions`.

**Антипаттерн из `UsingSerializeReferenceForCompositeActions.md`:**

```csharp
// ❌ CompositeAction : ReactiveLinkedList<System.Action> — делегаты, Unity их не сериализует.
//    Поле всегда приедет пустым, сколько LogAction туда ни кладите.
[SerializeReference] private IAction _action;   // + CompositeAction в инспекторе
```

`CompositeAction` рассчитан на сборку из кода (`new CompositeAction(a1, a2)` / `Add(Action)`), не из инспектора. И тип для вывода в консоль называется `LogAction`, а не `PrintAction`.

### 23. Рантайм-работа в установщике под `AtomicUtils.IsPlayMode()`

```csharp
[Serializable]
public sealed class CharacterSystemInstaller : IEntityInstaller<IPlayerContext>
{
    [SerializeField] private UnitFactory _characterFactory;

    public void Install(IPlayerContext context)
    {
        context.AddBehaviour<CharacterMoveController>();
        context.AddBehaviour<CharacterFireController>();

        if (!AtomicUtils.IsPlayMode())
            return;

        IUnitEntity character = _characterFactory.Create(NoArgs.Default);
        context.AddValue(PlayerAPI.Character, character);
        context.WhenDisable(character.Disable);
    }
}
```

Почему так. Бейкеры и установщики вызываются из `OnValidate` для превью в редакторе (`ScriptableEntityFactory.Compile()`, ``MonoEntityBaker`1_Editor.cs``), поэтому спавн, подписки на сетевые события и прочее с побочными эффектами надо отсечь. В билде `IsPlayMode()` компилируется в `return true` и помечен `AggressiveInlining` — проверка бесплатна. Парная `IsEditMode()` — это `!isPlaying && !isCompiling`, то есть не просто отрицание.

### 24. Итерация: конкретный тип в объявлении

```csharp
// ✅ struct-энумератор, ноль аллокаций
EntityWorld<IUnitEntity> world = ...;
foreach (IUnitEntity unit in world) { }

EntityCollection<IUnitEntity> collection = ...;
foreach (IUnitEntity unit in collection) { }

EntityFilter<IUnitEntity> filter = ...;
foreach (IUnitEntity unit in filter) { }
```

```csharp
// ❌ IEnumerator<E> — энумератор боксируется на каждом foreach
IEntityWorld<IUnitEntity> world = ...;
foreach (IUnitEntity unit in world) { }

IReadOnlyEntityCollection<IUnitEntity> collection = ...;
foreach (IUnitEntity unit in collection) { }
```

```csharp
// теги/значения/поведения — то же правило
Entity entity = ...;
Entity.TagEnumerator tags = entity.GetTagEnumerator();
while (tags.MoveNext())
{
    int tagId = tags.Current;
}

// ❌ через IEntity — явная реализация интерфейса, бокс гарантирован
IEntity boxed = entity;
IEnumerator<int> boxedTags = boxed.GetTagEnumerator();
```

Почему так. `EntityCollection<E>` объявляет `public Enumerator GetEnumerator()` (struct) и отдельно `IEnumerator<E> IEnumerable<E>.GetEnumerator() => new Enumerator(this)`. Компилятор `foreach` выбирает перегрузку по **статическому** типу: конкретный тип → struct по значению, интерфейс → упаковка. У `Entity`/`MonoEntity` та же схема: `public TagEnumerator GetTagEnumerator()` рядом с явной `IEnumerator<int> IEntity.GetTagEnumerator()`. Здесь «узкие интерфейсы» не применяются: в цикле нужен конкретный тип.

### 25. Реактивные коллекции: `for` для массивов и списков, `foreach` для остального

```csharp
// ✅ ReactiveArray / ReactiveList — for с кешированным Count
ReactiveList<string> items = ...;
for (int i = 0, count = items.Count; i < count; i++)
{
    string item = items[i];
}

// ✅ ReactiveLinkedList / ReactiveDictionary / ReactiveHashSet — foreach
ReactiveLinkedList<string> links = ...;
foreach (string item in links) { }
```

```csharp
// ❌ foreach по ReactiveList на 1000 элементах: 28.80 мкс против 1.70 мкс у for
// ❌ for по ReactiveLinkedList: индексатор O(N) — 1273.55 мкс против 28.50 мкс у foreach
```

Почему так. Это не догадка: `Docs/Elements/Performance/ReactiveListPerformance.md` и `ReactiveLinkedListPerformance.md` дают замеры на Apple M1 для 1000 элементов типа `object`. У `ReactiveList` `For` — 1.70 мкс, `Enumerator` — 28.80 мкс. У `ReactiveLinkedList` наоборот: `Enumerator` — 28.50 мкс, `For` — 1273.55 мкс, `Get` — 1277.70 мкс (обход от головы). У `ReactiveArray` разрыв ещё заметнее: `For` 0.70 мкс против `Enumerator` 28.50 мкс.

---

## Производительность

Разделяю измеренное (есть числа в `Docs/Elements/Performance/`), заявленное в исходнике (атрибут, тип, структура данных — видно в коде) и выведенное (мой вывод из прочитанного кода).

### Измерено (Apple M1, 1000 элементов типа `object`)

| Операция | Baseline | Reactive-версия | Источник |
|---|---|---|---|
| `ReactiveList.For` / `Enumerator` | `List`: 1.70 / 29.35 | 1.70 / 28.80 | `ReactiveListPerformance.md` |
| `ReactiveList.Contains` | `List`: 1821.35 | **33455.75** | `ReactiveListPerformance.md` |
| `ReactiveList.Set` | `List`: 30.40 | 42.00 | `ReactiveListPerformance.md` |
| `ReactiveArray.For` / `Enumerator` | `Array`: 0.70 / 0.80 | 0.70 / **28.50** | `ReactiveArrayPerformance.md` |
| `ReactiveArray.Set` / `Clear` | `Array`: 28.00 / 0.40 | **76.90** / **41.50** | `ReactiveArrayPerformance.md` |
| `ReactiveLinkedList.For` / `Get` | `List`: 1.70 / 1.50 | **1273.55** / **1277.70** | `ReactiveLinkedListPerformance.md` |
| `ReactiveLinkedList.RemoveAt` | `List`: 29.55 | **2546.10** | `ReactiveLinkedListPerformance.md` |
| `ReactiveLinkedList.Remove` | `List`: 307.40 | 43.50 (быстрее) | `ReactiveLinkedListPerformance.md` |
| `ReactiveHashSet.Contains` | `HashSet`: 50.10 | 10.00 (быстрее) | `ReactiveHashSetPerformance.md` |
| `ReactiveHashSet.Clear` | `HashSet`: 1.10 | **32.00** | `ReactiveHashSetPerformance.md` |
| `ReactiveDictionary.Add` | `Dictionary`: 34.10 | 64.40 | `ReactiveDictionaryPerformance.md` |
| `ReactiveDictionary.Set` / `Get` | `Dictionary`: 35.50 / 7.40 | 10.10 / 5.80 (быстрее) | `ReactiveDictionaryPerformance.md` |

Практические выводы из этой таблицы: `Contains` на реактивных списках в 18 раз дороже обычного — не используйте их как множества, для этого есть `ReactiveHashSet`, где `Contains` наоборот в 5 раз быстрее `HashSet`. `ReactiveArray.Set` в 2.7 раза дороже обычной записи из-за `OnItemChanged`/`OnStateChanged` — не держите в реактивном массиве то, что перезаписывается каждый кадр.

### Заявлено в исходнике

**Struct-энумераторы с явными интерфейсными реализациями.** `Entity.TagEnumerator` (`Entity_Tags.cs:301`), `Entity.ValueEnumerator` (`Entity_Values.cs:608`), `Entity.BehaviourEnumerator` (`Entity_Behaviours.cs:305`); те же три у `MonoEntity` (`MonoEntity_Tags.cs:419`, `MonoEntity_Values.cs:604`, `MonoEntity_Behaviours.cs:289`); `EntityCollection<E>.Enumerator` (``EntityCollection`1.cs:417``); `ReactiveArray<T>.Enumerator`, `ReactiveList<T>.Enumerator`, `ReactiveLinkedList<T>.Enumerator`, `ReactiveHashSet<T>.Enumerator`, `ReactiveDictionary<K,V>.Enumerator`. Рядом с каждым публичным `GetEnumerator()` стоит **явная** интерфейсная реализация (`IEnumerator<int> IEntity.GetTagEnumerator() => new TagEnumerator(this)`), то есть бокс происходит в момент, когда статический тип становится интерфейсом, а не «иногда при foreach».

**`AggressiveInlining`.** `Entity_Values.cs` — 7 атрибутов, `Entity_Behaviours.cs` — 5, `Entity_Tags.cs` — 2, ``EntityCollection`1.cs`` — 2. Целиком помечены: все `When*` из `Lifecycle/Extensions.cs`, все `Subscribe`/`Unsubscribe` из `Events/Extensions.cs`, `Observe`/`AsСonst` из `Values/Extensions.cs`, все `Add`/`Remove` из `Expressions/Extensions.cs`, все `AddValue`/`GetValue`/`HasTag`/`AddTag`-расширения, `BindTo`, `AtomicUtils.IsPlayMode`/`IsEditMode`, `AndPredicate.Invoke`, `EntityKeyStore.NameToId`/`IdToName`, `ISetter<T>.IAction<T>.Invoke`, хуки `EntitySystemBase.OnEnable/OnDisable/OnAddEntity/OnRemoveEntity`. Генератор API по умолчанию (`AggressiveInlining = true`) ставит атрибут на каждый сгенерированный метод доступа.

**Хранилище на массивах, не на `Dictionary`.** Значения: `internal struct ValueSlot { int key; object value; bool exists; int next; }` плюс `ValueSlot[] _valueSlots`, `int[] _valueBuckets`, `_valueFreeList`, `_valueLastIndex` — открытая адресация с цепочкой (`Entity_Values.cs:38`). Теги — `internal struct TagSlot` по той же схеме (`Entity_Tags.cs:26`). `EntityCollection<E>` — `Slot[] _slots`, `int[] _buckets`, `int[] _order` с устойчивым порядком обхода (``EntityCollection`1.cs:24-56``). `EntityRegistry` — своя слот/бакет-структура.

**Значения лежат как `object`.** `void AddValue(int key, object value)` (`Entity_Values.cs:286`). Любой value-type боксируется один раз при добавлении. Отсюда обязательность обёрток `Const<T>`/`Variable<T>` для примитивов: иначе бокс происходит при каждой записи.

**`ref`-доступ без проверки типа.** `public ref T GetValueUnsafe<T>(int key) where T : class` (`Entity_Values.cs:196`) возвращает `ref Unsafe.As<object, T>(ref slot.value)` — ни каста, ни проверки на null. Проверенная версия — `TryGetValue<T>(int, out T)` (`Entity_Values.cs:133`). Кодогенератор умеет выпускать `Ref{Name}()` поверх этого при `[EntityExtensionsAPI(Unsafe = true)]` или `[Unsafe]` на поле.

**Снимок значений без аллокации.** `KeyValuePair<int,object>[] GetValues()` аллоцирует массив по `_valueCount` (`Entity_Values.cs:503`); `int CopyValues(KeyValuePair<int,object>[] results)` (`:516`) пишет в ваш буфер. Аналогично `GetTags()`/`CopyTags(int[])` и `GetBehaviours()`/`CopyBehaviours(...)`.

**`EntityCollection.Clear()` берёт массив из пула.** `private protected static readonly ArrayPool<E> s_arrayPool = ArrayPool<E>.Shared`, `Clear()` делает `s_arrayPool.Rent(_count)` и возвращает его в `finally` (``EntityCollection`1.cs:22,320,353``) — события `OnRemoved` раздаются без аллокации.

**`Subscription` и все подписки жизненного цикла — `readonly struct`.** `Subscription`, `Subscription<T>`…`Subscription<T1..T4>` (`Events/Subscription.cs:9,43,77,112,148`); `InitSubscription`, `DisposeSubscription`, `EnableSubscription`, `DisableSubscription`, `TickSubscription`, `FixedTickSubscription`, `LateTickSubscription`, `GizmosSubscription` (`Lifecycle/Subscriptions/`).

**Выражение — связный список.** `IExpression<R> : IReactiveList<ExpressionMember<R>>`, `ExpressionBase<R> : ReactiveLinkedList<ExpressionMember<R>>` (`IExpression.cs:13`, `ExpressionBase.cs:20`). Каждое чтение `Value` — проход по узлам плюс вызов делегата на член. `ExpressionMember<R>` — `struct` с `object Source` и `Func<R>`, `readonly R Invoke()`.

**Предикаты — массив.** `AndPredicate`/`OrPredicate` держат `[SerializeReference] IPredicate[] conditions`, `Invoke()` — `for` по `Length`; `[MethodImpl(AggressiveInlining)]` стоит только на `AndPredicate.Invoke()`. `NotPredicate` — одно поле, без атрибута.

**Пулы платят хешем за защиту от двойного возврата.** `EntityPool<TEntity,TArgs>`: `Stack<TEntity> _pooledEntities` + `HashSet<TEntity> _rentEntities` (``EntityPool`1.cs:32,37``) — `Return` делает `_rentEntities.Remove(entity)`. `MultiEntityPool`: `Dictionary<TKey,Stack<TEntity>>` + `Dictionary<TEntity,TKey>` + `Dictionary<TKey,int>` (``MultiEntityPool`1.cs:27-43``) — до трёх обращений к словарям на `Rent`/`Return`. У обоих есть виртуальные `OnCreate`/`OnDispose`/`OnRent`/`OnReturn` — виртуальный вызов на операцию.

**Батчинг с бюджетом кадра.** `EntitySystemBase<E>.Settings`: `frameBudget = 0.03f`, `AdaptiveBatching { minSize = 1024, maxSize = 2048, scaleDown = 2, stepUp = 256 }`. `Update(float)` измеряет время `OnUpdate`, делит `batchSize` на `scaleDown` при перерасходе и добавляет `stepUp` иначе (`EntitySystemBase.cs:17-100,172-190`). `EntitySystem<E>.OnUpdate` — `sealed override` с `AggressiveInlining`, обход кольцевым курсором по массиву.

**Профайлерные маркеры.** `EntityView<E>` держит `static readonly ProfilerMarker` для `Activate`/`Deactivate` под `#if ENABLE_PROFILER` (``EntityView`1.cs:29-30``); `EntityCollectionView` — для `Add`/`Remove`; `EntityViewPool` — для `Rent`/`Return`; `EntitySystemBase` — `new ProfilerMarker(this.GetType().Name + ".Update")`.

**Асинхронный прогрев view.** `public async ValueTask InitAsync(K key, int count)` использует `Object.InstantiateAsync(prefab, count, container)` (``UI/Pool/EntityViewPool`1.cs:102``), синхронный `Init` — цикл `Instantiate`.

**`AtomicUtils` бесплатен в билде.** Вне редактора `IsPlayMode()` компилируется в `return true`, `IsEditMode()` — в `return false`, оба с `AggressiveInlining`.

### Выведено (мой вывод, не утверждение исходника)

**Строковый ключ дороже `ValueKey` ровно на один `Dictionary<string,int>.TryGetValue` плюс хеш строки.** `NameToId` помечен `AggressiveInlining`, но словарный lookup никуда не денется; `ValueKey<T>.Id` — чтение поля struct. Числа не замерены — замерьте на своём профиле перед тем, как переписывать всё.

**`entity.Install(installer)` всегда платит интерфейсный диспатч плюс проверку типа.** Расширение объявлено как `Install(this IEntity, IEntityInstaller)` и принимает только необобщённый интерфейс; обобщённая реализация — default-метод с `is not E` и `throw`. То есть `IEntityInstaller<E>` не даёт статической гарантии в точке вызова `Install`, только читаемую ошибку в рантайме. Издержка разовая (время сборки сущности), но типовую безопасность она не покупает.

**Пулящаяся сущность остаётся в `EntityRegistry` на всё время жизни.** Регистрация происходит в конструкторе ёмкостей, снятие — в `Dispose()` при `_instanceId > 0`. `pool.Return(entity)` `Dispose` не вызывает, значит запись в реестре сохраняется — это и хорошо (id не мигрируют), и означает, что реестр растёт по пику, а не по текущему числу живых сущностей.

**`AndExpression` на горячем пути стоит дороже, чем кажется.** Проход по связному списку плюс делегат на член плюс struct-энумератор. Для 2-3 условий, читаемых раз в кадр на сущность при тысячах сущностей, это заметно. `AndPredicate` с массивом — тот же интерфейс `IFunction<bool>` при меньшей цене. Числа не замерены.

**`Add(IFunction<R>)`/`Remove(IFunction<R>)` аллоцируют делегат на каждый вызов.** Реализации — `it.Add(new ExpressionMember<R>(null, func.Invoke))` и `it.Remove(value.Invoke)`: преобразование группы методов в делегат создаёт новый объект. Round-trip `Add(Constants.False)` → `Remove(Constants.False)` при этом работает, потому что `Delegate.operator==` сравнивает цель и метод. То есть «allocation-free» из `UsingConstantsWithAndExpressions.md` относится к отсутствию нового объекта-условия, а не к нулю аллокаций на вызов.

**Состояние тестового покрытия.** Тесты пулов закомментированы целиком (`Entities/Tests/EditMode/Pool/EntityPoolTests.cs`, `MultiEntityPoolTests.cs`), и ещё ряд тестовых файлов в `Entities/Tests/EditMode`, `Tests/Substitutions` и `Elements/Tests/EditMode` отключён. Это согласуется с тем, что рефакторинг на `IArgs` свежий. Практический вывод: поведение фабрик, пулов, каталогов и бейкеров на миграции проверяйте своими тестами, а не полагайтесь на регрессию апстрима.

---

## Грабли

- **`EntityKeyStore` по умолчанию выдаёт id по порядку обращения, а не по имени.** `SequentialEntityKeyAlgorithm.NameToId(string name) => _nextId++` — аргумент не используется. Значит целочисленные ключи **не стабильны между запусками** и зависят от порядка статической инициализации. Любая сериализация int-ключей или передача их по сети сломается. Если это нужно — поставьте детерминированный `IEntityKeyAlgorithm` через `EntityKeyStore.SetAlgorithm(...)` до создания первого `ValueKey`/`TagKey`; обратите внимание, что `SetAlgorithm` чистит кеш, поэтому вызывать его после создания ключей нельзя.

- **`Entity.Settings.disposeValues` по умолчанию `true`, и `Dispose()` убивает общие объекты.** `Entity.Dispose()` вызывает `DisposeValues()`, который проходит все значения и делает `Dispose()` каждому `IDisposable`. `ReactiveVariable<T>`, `Event`, `Event<T>`, `ReactiveList<T>`, `ReactiveArray<T>`, `ReactiveDictionary<K,V>`, `ReactiveHashSet<T>`, `EntityCollection<E>` — все `IDisposable`. Один `ReactiveVariable`, разделённый между сущностями через ScriptableObject, умрёт вместе с первой из них. Шарьте только `Const<T>`/`ScriptableConst<T>`/`InlineValue<T>`, либо передавайте `new Entity.Settings { disposeValues = false }` и чистите сами.

- **`DisposeValues()` аллоцирует.** Он вызывает `GetValues()`, то есть создаёт массив `KeyValuePair<int,object>[]` по числу значений, — и делает это при каждом `Dispose()`. Для сущностей, которые часто создаются и уничтожаются, это плюс одна аллокация на смерть; пулинг её убирает, потому что возврат в пул `Dispose` не зовёт.

- **`Request.Consume()` имеет побочный эффект, и порядок в условии его определяет.** `if (_request.Consume() && _condition.Value)` съедает запрос при ложном условии; `if (_condition.Value && _request.Consume())` оставляет его висеть. Пример в `RequestConditionActionEvent.md` — первый вариант, пример в `UsingCooldownInGameMechanics.md` — второй. Выберите сами и напишите об этом в коде.

- **`foreach` по `ReactiveArray<T>` через интерфейс падает с `NullReferenceException`.** `IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator();` (`Collections/Array/ReactiveArray.cs:309`) — создаётся энумератор **по умолчанию**, у которого `_array == null`, а `MoveNext()` читает `_array.items.Length`. Небоксирующий `GetEnumerator()` и реализация `IEnumerable` неcгенерической версии передают `this` корректно, то есть проблема ровно в типизированной интерфейсной перегрузке. Практика: переменную `ReactiveArray<T>` всегда объявляйте конкретным типом, а по `IReadOnlyReactiveArray<T>`/`IReactiveArray<T>` ходите индексом (`for` по `Length`), а не `foreach`. Это одновременно и быстрее (0.70 против 28.50 мкс).

- **`EntityFilter<E>.Builder` — `struct` и возвращает `this` по значению.** `builder.WithSource(world);` отдельной строкой теряет источник, потому что `_source` присвоен копии; `Build()` затем бросает `InvalidOperationException("Source must be provided.")`. Либо непрерывная цепочка, либо `builder = builder.WithSource(world);`. Коварно то, что `WithTrigger` после первого вызова ведёт себя иначе: он создаёт `List` и дальше мутирует его по ссылке, то есть часть вызовов «работает» в разорванной цепочке, а часть — нет.

- **Параметрless `Uninstall()` не существует.** Переопределяйте `Uninstall(E entity)` (или `Uninstall(IEntity entity)` в необобщённом установщике). Код из `UsingSubscriptionsWithDisposeComposite.md` и `UninstallEntityInstaller.md` не компилируется.

- **`Uninstall` зовёт только `EntityView<E>.Deactivate()`.** Никакой автоматики «сущность удалили → установщики сняли» нет: `Entity.Dispose()` чистит таблицы, но `Uninstall` не вызывает. Если сущность не имеет view, вешайте очистку на `entity.WhenDispose(...)`.

- **`AddTo(this IDisposable, DisposableComposite)` боксирует каждую struct-подписку.** `Subscription` — `readonly struct : IDisposable`, и передача по интерфейсу создаёт бокс. Невозможно обойти с необобщённым `DisposableComposite`, потому что он есть `DisposableComposite<IDisposable>`. Используйте `DisposableComposite<Subscription>` плюс `AddTo<T>`. Плюс `AddTo` возвращает `void` — дальше по цепочке ничего не сделать.

- **Все `When*`, кроме `WhenGizmos`, возвращают подписку, и все примеры её выбрасывают.** (`WhenGizmos(this IGizmosSource, Action)` объявлен как `void` — снять его нечем вообще, хотя `GizmosSubscription` в `Lifecycle/Subscriptions/` существует.) `entity.WhenFixedTick(_cooldown.Tick);` регистрирует обработчик навсегда. Для сущности из пула, которую установят повторно, это означает дубликат обработчика на каждую аренду. Сохраняйте `FixedTickSubscription` и снимайте в `Uninstall`/`WhenDispose`, либо гарантируйте, что сущность устанавливается один раз за жизнь.

- **`Optional<T>` определяет `operator true`/`operator false`, но не `operator bool`.** `if (_x)` работает; `!_x`, `_x && y`, `_x || y`, `bool b = _x` — нет. Используйте `.Active` или `TryGetValue(out T)`.

- **Неявное преобразование `Optional<T>` из value-type всегда даёт `active = true`.** `implicit operator Optional<T>(T it) => new() { value = it, active = it != null }` — для `T = float` сравнение с `null` в обобщённом коде всегда ложно, то есть `Optional<float> x = 0f;` активен. Сеттер `Value` делает то же самое. Для value-типов ставьте `Active` явно; в инспекторе этой проблемы нет, потому что `active` сериализуется напрямую.

- **`AddValue` на уже существующий ключ бросает `ArgumentException`.** Оверрайд-установщики и бейкеры должны звать `SetValue`, а не `AddValue`. В `Entity_Values.cs` для этого есть отдельное сообщение «A value with the same key … already has been added».

- **`GetValueUnsafe<T>` не проверяет тип, и это `where T : class`.** Если под ключом лежит не `T`, вы получите `ref` на объект другого типа через `Unsafe.As` — порча памяти, а не исключение. И обратите внимание на асимметрию перегрузок: `GetValueUnsafe<T>(ValueKey<T>)` отдаёт `ref T`, а `GetValueUnsafe<E,T>(ValueKey<E,T>)` — просто `T`.

- **`EntityKeyStore.NameToId` не потокобезопасен.** Статические `Dictionary<string,int>`/`Dictionary<int,string>` без синхронизации. Создавайте все ключи из главного потока (статические поля класса с `[EntityExtensionsAPI]` это обеспечивают естественным образом). `ThreadSafeVariable<T>`, `ThreadSafeReactiveVariable<T>`, `ThreadSafeEvent`, `ThreadSafeCooldown` в `Atomic.Elements` есть, но ключи они не прикрывают.

- **Класс с `[EntityExtensionsAPI]` обязан быть `partial`.** Генератор выпускает `public static partial class {ClassName}` в том же пространстве имён; без `partial` в вашем файле получите конфликт определений. Поля должны быть `static` (любой доступности) и иметь тип `ValueKey`/`TagKey` **именно из `Atomic.Entities`** — парсер проверяет пространство имён.

- **`Name` у фабрик, view и бейкеров живёт в трёх разных местах.** `ScriptableEntityFactory<TArgs>` объявляет `public virtual string Name { get; private set; }` — переопределять его в наследнике нужно через `override`, объявление заново даёт CS0108. У `ScriptableEntityFactory<TEntity, TArgs>` (двухпараметрического) `Name` **нет** — используйте `this.name` из `ScriptableObject`. У `EntityView<E>` `Name` нет, у необобщённого `EntityView` есть. Проверяйте, от чего именно наследуетесь.

- **`EntityView<E>.installers` — `internal`.** Из своей сборки список установщиков view программно не наполнить; только через инспектор. Если нужна программная сборка — делайте это в `OnActivate(E entity)`.

- **`MonoEntityBaker.Bake` уничтожает `GameObject` по умолчанию.** `protected virtual void Release() => Destroy(this.gameObject);`. Если бейкер стоит на объекте с нужной графикой, переопределите `Release`.

- **`ExpandMode.ExpandByOne` — значение по умолчанию у обоих пулов.** При исчерпании создаётся ровно одна сущность, то есть залповый спавн 200 снарядов даст 200 отдельных созданий через фабрику. `ExpandByDoubling` для залпов, `NoExpand` — чтобы ошибка проявилась исключением, а не тихой деградацией.

- **`pool.Return(entity)` для не арендованной сущности только пишет warning.** `if (_rentEntities.Remove(entity)) { … } else Debug.LogWarning(...)` — двойной возврат не падает, а молча теряется. Не полагайтесь на пул как на средство обнаружить ошибку владения.

- **Расширение `AsConst` недостижимо.** В `Values/Extensions.cs:18` метод называется `AsСonst` с **кириллической** `С` (U+0421). `"value".AsConst()` латиницей не скомпилируется. Пишите `new Const<T>(value)`.

- **`Observe` вызывает колбэк до создания подписки.** Исключение в колбэке на первом вызове оставит объект без подписки вообще, без следов в логе подписок. И `Observe` объявлен только на `IReactiveValue<T>` — на `ISignal<T>` его нет.

- **`AndExpression` и `AndPredicate` на пустом наборе возвращают `true`.** `AndExpression.Invoke(Enumerator)` проходит энумератор и возвращает `true`, если ни один член не вернул `false`; `AndPredicate.Invoke()` начинается с `if (this.conditions == null) return true;`. Условие, которое ещё не настроили, разрешает действие. `OrExpression`, `OrPredicate` и `NotPredicate` в той же ситуации возвращают `false` — поведение зеркальное, не перепутайте при подстановке одного вместо другого.

- **`AddValue(int, object)` бросает `ArgumentNullException` на `null`.** Нельзя «зарезервировать» ключ пустым значением, а `Optional<T>`, который неактивен и несёт `null`, нельзя передать в `AddValue` напрямую — отсюда обязательная проверка `Active`/`TryGetValue` перед добавлением, а не после.

- **Типизированные `IEntityInstaller<E>`/`IEntityInit<E>`/`IEntityTick<E>` бросают `InvalidCastException` в рантайме, не на компиляции.** Default-реализация необобщённого метода делает `is not E e` и `throw`. Подключили установщик юнита к сущности оружия — узнаете при первом `Install`.

- **`EntityWorld` при `Dispose` не разрушает сущности сам.** `Dispose/DisposeEntities` — разные операции: `DisposeEntities` — расширение на `IEntityCollection<E>`, которое проходит сущности; `Dispose` — освобождение самой коллекции. `BindTo` регистрирует оба на `WhenDispose`, причём в этом порядке. Если вы пишете привязку руками, не перепутайте порядок: `Dispose()` коллекции очистит её раньше, чем вы успеете пройти сущности.

---

## Где читать

Все пути относительно `C:/!!!Work/_AtomicUpstream`.

### Docs/BestPractices — 29 файлов, тематический разбор

Стиль и архитектура:

- `Docs/BestPractices/PreferAbstractInterfaces.md` (66 строк) — одна страница с правилом «зависеть от `IValue<T>`/`IVariable<T>`/`ISignal`, а не от `ReactiveVariable<T>`/`Const<T>`», с мотивацией через DIP и переносимость между Mirror/Fusion. Код валиден, читать первым.
- `Docs/BestPractices/ProjectFolderOrganization.md` (197 строк) — раскладка файлов: `Assets/{3rdParty,Game,Modules,Plugins,Settings,Tools}`, внутри `Game/Scripts/{App,Gameplay,UI}`, внутри `Gameplay/GameEntities/{Core,View,Content}`, контексты по фичам. Кода нет, только дерево каталогов. Переносимо как есть.
- `Docs/BestPractices/ModularEntityInstallers.md` (233 строки) — три способа разбить сборку сущности (по комментариям, по методам, по модульным `[Serializable] IEntityInstaller<E>`) с полными примерами. Код содержит `BaseVariable<Vector3>` (не существует → `Variable<T>`) и `public void Install` без `override`.

Поток механики:

- `Docs/BestPractices/RequestConditionActionEvent.md` (103 строки) — определение цепочки RCAE плюс пример прыжка. Самый концептуально важный и самый сломанный из кодов: `BaseRequest`, `BaseEvent`, `new AndExpression(lambda, lambda)`, `entity.GetHealth().Exists`, `IFunction<bool>.Value`, `Consume()` перед условием. Читать за идеей, не за кодом.
- `Docs/BestPractices/RequestsVsActions.md` (160 строк) — когда `IAction` (немедленно), когда `IRequest` (отложенно), с примерами ввода и AI-следования, плюс раздел про мультиплеер: в сетевых играх предпочитать `IAction`, потому что флаг запроса надо синхронизировать. Код почти валиден.
- `Docs/BestPractices/UsingRequests.md` (169 строк) — те же два примера `IRequest<Vector3>` (ввод в `Tick` → `Consume` в `FixedTick`; AI-следование) подробнее. Пересекается с `RequestsVsActions.md` на 80%.
- `Docs/BestPractices/UsingEvents.md` (104 строки) — `ISignal` слушать / `IAction` вызывать на одном объекте события, с `FireSFXBehaviour`. Использует `BaseEvent` и строковые ключи.
- `Docs/BestPractices/UsingSetters.md` (73 строки) — `ISetter<T>` для контроллеров ввода. Короткий; содержит несуществующий `AddValue<ISetter<Vector3>>("MoveDirection", …)` и `BaseVariable<Vector3>`.

Условия:

- `Docs/BestPractices/UsingExpressions.md` (197 строк) — `OrExpression` для условия лечения, `AndExpression` для условия выстрела, динамическое отключение через `condition.Add(() => false)`. Конструкторы с лямбдами не компилируются, расширения `Add`/`Remove` — да.
- `Docs/BestPractices/UsingConstantsWithAndExpressions.md` (43 строки) — `Constants.False` как заглушка для блокировки выражения без аллокации нового условия, с парным `Remove`. Короткий и рабочий; единственная неточность — ссылка на `DefaultConstants` (класс называется `Constants`).
- `Docs/BestPractices/SharedConstants.md` (65 строк) — flyweight: общий `Const<T>` в ScriptableObject-конфиге вместо скопированных float. Код валиден; про `IDisposable`-ловушку `disposeValues` ничего не сказано.

Инспектор и авторинг:

- `Docs/BestPractices/UsingOptionalWithInstallers.md` (152 строки) — три сценария `Optional<T>`: включаемое состояние, включаемая визуализация, оверрайд общей конфигурации. Самый полезный из «авторинговых»; код содержит `public void Install` без `override` и параметр не того типа (`IWeaponEntity` в `MonoEntityInstaller<IEnemyEntity>`).
- `Docs/BestPractices/UsingSerializeReferenceForPrintActions.md` (70 строк) — `[SerializeReference] IAction` + `LogAction` из инспектора как способ для дизайнера проверить срабатывание триггера. Код компонента валиден, и тип в тексте назван правильно — `LogAction`; устаревшее имя осталось только в имени самого файла и в путях ссылок (`../Elements/Actions/PrintAction.md`).
- `Docs/BestPractices/UsingSerializeReferenceForCompositeActions.md` (78 строк) — тот же компонент, но предлагает положить в поле `CompositeAction` и набрать внутрь последовательность действий. Приём **нереализуем**: `CompositeAction` держит `System.Action`-делегаты. Нужен `MonoActionConfigurable` или свой `[SerializeReference] IAction[]`.
- `Docs/BestPractices/UsingInlineActions.md` (136 строк) — `InlineAction` с лямбдой, инкапсулирующей логику выстрела (патроны, кулдаун, спавн, событие) прямо в установщике. Хороший пример «когда лямбды достаточно»; внутри `BaseEvent` и `GameContext.Instance`.
- `Docs/BestPractices/UsingInlineFunctions.md` (142 строки) — `InlineFunction<Vector3>` как абстракция источника координат: `Rigidbody.position` против `Transform.position`, потребитель видит только `IFunction<Vector3>`. Код валиден. Готовые прокси (`TransformPositionVariable`) не упомянуты.
- `Docs/BestPractices/UsingUtilsForEntityInstallers.md` (69 строк) — `AtomicUtils.IsPlayMode()` как гейт для рантайм-работы в установщике. Приём валиден, пример опирается на несуществующий `GameContext.Instance`.

Подписки и жизненный цикл:

- `Docs/BestPractices/UsingObserveWithReactiveValues.md` (95 строк) — `Observe` (подписка + немедленный вызов) и хранение `Subscription<int>` в `readonly`-поле презентера. Один из немногих полностью валидных примеров.
- `Docs/BestPractices/UsingSubscriptionsWithDisposeComposite.md` (74 строки) — `DisposableComposite` + `AddTo` в `WeaponViewInstaller`. Две проблемы: `Uninstall()` без параметра не компилируется, и `AddTo` на необобщённом композите боксирует подписки.
- `Docs/BestPractices/UninstallEntityInstaller.md` (56 строк) — зачем вообще нужен `Uninstall`. Содержит ровно тот же пример, что и предыдущий файл, с той же ошибкой сигнатуры. Полезен только утверждением «Install при создании, Uninstall при удалении».

Время:

- `Docs/BestPractices/ChosingBetweenTimerAndCooldown.md` (85 строк) — таблицы сравнения: кулдаун для интервалов между действиями, таймер — когда отсчёт сам является игровым объектом. Кода нет. Тип `Timer` в исходниках отсутствует (есть `ITimer`, `DownTimer`, `UpTimer`), таблица событий кулдауна лишнее содержит `OnStateChanged`.
- `Docs/BestPractices/UsingCooldownInGameMechanics.md` (488 строк) — **самый объёмный прикладной документ**: четыре сквозных примера (стрельба, рукопашный бой, таймер сессии с UI-презентером, спавнер монет) с тиком кулдауна через `WhenFixedTick` и, в отличие от RCAE-документа, с правильным порядком проверок перед `Consume`. Внутри `BaseRequest<T>`, `BaseEvent<T>`.

Создание сущностей:

- `Docs/BestPractices/EntitySystem.md` (598 строк) — **сквозная сборка всей подсистемы**: `IUnitEntity`/`UnitEntity`, `UnitFactory`, конкретные `TankFactory`/`SoldierFactory`, каталог, пул + мир в установщике контекста, use-case спавна/деспавна, затем весь слой представления (`UnitView`, `UnitViewCatalog`, `UnitViewPool`, `UnitCollectionView`, `UnitWorldView`) со скриншотами инспектора. Главный документ для понимания целевой архитектуры и одновременно самый устаревший по API: все фабрики/пулы/каталоги без `IArgs`, `GameContext.Instance`, `view.Name` на `EntityView<E>`, восемь `WhenXxx` вместо `BindTo`.
- `Docs/BestPractices/OverrideEntityFactoriesWithBakers.md` (247 строк) — разделение «фабрика создаёт по шаблону, бейкер правит экземпляр в сцене», с таблицей Installer/Baker/Factory и примером `Optional<float>`-оверрайда. Концептуально важно; `MonoEntityBaker<IUnitEntity>` с `protected sealed override IUnitEntity Create()` (строки 86-91) не компилируется: у реального `MonoEntityBaker<TEntity, TArgs>` есть второй type-параметр, а `Create` принимает пять аргументов.
- `Docs/BestPractices/UsingEntityPoolWithFactories.md` (85 строк) — минимальный пример `EntityPool`: фабрика, `Init(20)`, `Rent`/`Return`. Короткий и целиком написан против старого API (`EntityPool<BulletEntity>`, `new BulletFactory()` на ScriptableObject, `bullet.Set("Damage", 10)`). `ExpandMode` не упомянут.
- `Docs/BestPractices/UpgradingEntityFactoryToBuilder.md` (284 строки) — fluent-билдер поверх фабрики, в том числе для синглтон-контекста. Файл содержит два слипшихся варианта текста (строки 1-166 и 167-284). Весь код невалиден: `IEntityFactory<PlayerContext>`, `public override PlayerContext Create()`, `GameContext.SetFactory(...)`.

Итерация и производительность:

- `Docs/BestPractices/IteratingOverEntity.md` (130 строк) — `IEntity.GetTagEnumerator()` боксирует, `Entity.TagEnumerator`/`MonoEntity.TagEnumerator` — нет. Подтверждено исходником полностью, включая то, что интерфейсные реализации явные.
- `Docs/BestPractices/IteratingOverEntityCollections.md` (182 строки) — то же для `IEntityCollection`/`IEntityWorld` против `EntityCollection`/`EntityWorld`/`EntityFilter`. Подтверждено.
- `Docs/BestPractices/IteratingReactiveCollections.md` (301 строка) — три правила: конкретный тип вместо интерфейса; `for` с кешированным `Count` для `ReactiveArray`/`ReactiveList`; `foreach` для `ReactiveLinkedList`/`ReactiveDictionary`/`ReactiveHashSet` (индексатор связного списка O(N)). Единственный документ раздела со ссылками на реальные замеры.

### Docs/Elements/Performance — замеры

- `Docs/Elements/Performance/ReactiveListPerformance.md` — `List` против `ReactiveList`, 11 операций. Главные числа: `For` 1.70 против `Enumerator` 28.80; `Contains` 33455.75 против 1821.35 у `List`.
- `Docs/Elements/Performance/ReactiveArrayPerformance.md` — `Array` против `ReactiveArray`, 6 операций. `Enumerator` 28.50 против `For` 0.70; `Set` 76.90 против 28.00; `Clear` 41.50 против 0.40.
- `Docs/Elements/Performance/ReactiveLinkedListPerformance.md` — трёхколоночное сравнение `List`/`ReactiveList`/`ReactiveLinkedList`, 11 операций, плюс явные выводы: хорош для вставок/удалений в любой позиции, индексный доступ медленный, всегда `foreach`.
- `Docs/Elements/Performance/ReactiveDictionaryPerformance.md` — `Dictionary` против `ReactiveDictionary`, 8 операций. Накладные расходы минимальны, `Set`/`Get`/`TryGetValue` местами быстрее.
- `Docs/Elements/Performance/ReactiveHashSetPerformance.md` — `HashSet` против `ReactiveHashSet`, 5 операций. `Contains` 10.00 против 50.10 (в 5 раз быстрее), `Clear` 32.00 против 1.10.

### Исходники, которые придётся открыть при миграции

- `Assets/Plugins/Atomic/Entities/Scripts/Utils/Args.cs` — `IArgs`, `NoArgs`, `Args<T>`…`Args<T1..T4>`. Прочитайте первым: от этого файла зависят сигнатуры всех фабрик, пулов, каталогов и бейкеров, и именно он делает весь код в `Docs/BestPractices` устаревшим.
- `Entities/Scripts/KeyStore/` — `EntityKeyStore.cs`, `Keys/ValueKey.cs`, `Keys/ValueKey'1.cs`, `Keys/TagKey.cs`, `Keys/TagKey'1.cs`, `Algorithms/SequentialEntityKeyAlgorithm.cs`. Типизированные ключи и то, как из имени получается `int`.
- `Entities/Scripts/Codegen/EntityExtensionsAPIAttribute.cs` и `SourceGenerators/Project~/EntityAPIGenerator/CodeEmitter.cs` — какие именно методы генерируются (`Add{Name}`, `Get{Name}`, `Ref{Name}`, `Has{Name}Tag`, …) и с какими атрибутами. Источник всех `AddHealth`/`GetFireEvent` из документации.
- `Entities/Scripts/Entities/Core/Entity.cs` + `Entity_Values.cs` + `Entity_Tags.cs` + `Entity_Behaviours.cs` + `Entity_Lifecycle.cs` — конструкторы с `Settings?`, регистрация в `EntityRegistry`, слот-хранилище, struct-энумераторы, порядок операций в `Dispose()`.
- `Entities/Scripts/Installers/` — семь файлов (`IEntityInstaller`, ``IEntityInstaller`1``, `MonoEntityInstaller`, ``MonoEntityInstaller`1``, `MonoEntityInstallerConfigurable`, `ScriptableEntityInstaller`, ``ScriptableEntityInstaller`1``), полные сигнатуры `Install`/`Uninstall` с `sealed override`. Открыть до того, как переписывать установщики по документации.
- `Entities/Scripts/Lifecycle/Extensions.cs` + `Lifecycle/Subscriptions/` — `When*` и типы возвращаемых подписок.
- `Entities/Scripts/World/Extensions.cs` — один метод `BindTo` и восемь регистраций внутри него. Заменяет весь блок ручной привязки из `EntitySystem.md`.
- ``Entities/Scripts/Filters/EntityFilter`1.cs`` + `Entities/Scripts/Triggers/` — построитель фильтра и шесть видов триггеров (`Tag`, `Value`, `Behaviour`, `StateChanged`, `Inline`, `Subscription`), каждый в обобщённом и необобщённом вариантах. Единственное место, где описан реактивный фильтр; в Best Practices его нет.
- `Entities/Scripts/Systems/EntitySystemBase.cs` + `EntitySystem.cs` + `PriorityEntitySystem.cs` + `Systems/Extensions.cs` — батчинг с бюджетом кадра. Обязательно, если целевой масштаб — RTS.
- `Entities/Scripts/Aspects/` — семь файлов обратимой композиции (`Apply`/`Discard`). В Best Practices отсутствуют полностью, хотя закрывают класс задач (баффы, экипировка), который иначе уедет в установщики.
- `Entities/Scripts/Pooling/` — `ExpandMode.cs` (три режима), ``Core/EntityPool`1.cs``, ``Core/MultiEntityPool`1.cs``, плюс `Unity/PrefabEntityPool*`. Реальные конструкторы с `TArgs` и `ExpandMode`.
- `Entities/Scripts/Factories/` — четыре интерфейса в `Interfaces/` и четыре Unity-класса в `Unity/`. `ScriptableEntityFactory.cs` (необобщённый по сущности) показывает идиому «sealed Create, abstract Install» в исполнении самого фреймворка.
- `Entities/Scripts/Baking/` — семь файлов, включая `MonoEntityBakerOptimized*` и ``MonoEntityBaker`1_Static.cs`` с `BakeAll`/`Bake(Scene)`/`Bake(GameObject)`.
- `Entities/Scripts/UI/` — ``View/EntityView`1.cs`` (где `installers` is `internal` и где зовётся `Uninstall`), ``Pool/EntityViewPool`1.cs`` (`InitAsync`), ``Collection/EntityCollectionView`1.cs``, ``World/EntityWorldView`1.cs``.
- `Entities/Scripts/Registry/EntityRegistry.cs` — глобальный `id → IEntity`. Точка опоры для сохранений и сетевого кода.
- `Entities/Scripts/Entities/Unity/MonoEntitySingleton.cs` — единственный механизм синглтонов. Нужен тем, кто ищет `GameContext.Instance`.
- `Entities/Scripts/Entities/Extensions.cs` — `Install`, `InstallFromScene`, и, что важно для обработчиков триггеров, `GameObject.TryGetEntity`, `Component.TryGetEntity`, `Collision.TryGetEntity`, `Collision2D.TryGetEntity`, `FindEntityInParent`. Это замена рукописным `GetComponent<X>()`.
- `Elements/Scripts/Values/Constants.cs` — полный список готовых `Const<...>`, которых в `SharedConstants.md` нет.
- `Elements/Scripts/Utils/Optional.cs` — 92 строки; прочитайте целиком, прежде чем полагаться на `if (_x)`.
- `Elements/Scripts/Utils/Extensions.cs` (24 строки) + `DisposableComposite.cs` + ``DisposableComposite`1.cs`` — две перегрузки `AddTo` и почему типизированный композит не боксирует.
- `Elements/Scripts/Expressions/ExpressionMember.cs` + `AndExpression.cs` + `Extensions.cs` — почему лямбда в конструкторе не работает, а в `Add` работает.
- `Elements/Scripts/Functions/AndPredicate.cs` + `OrPredicate.cs` + `NotPredicate.cs` — дешёвая альтернатива выражениям для фиксированного набора условий, сериализуемая через `[SerializeReference]`.
- `Elements/Scripts/Time/Sources/` — восемь мелких интерфейсов, из которых собраны `ICooldown`, `ITimer`, `IStopwatch`, `IPeriod`. Основа для узких зависимостей в UI и механике.
- `Elements/Scripts/Variables/Proxy/` — `Transform*Variable`, `GameObjectLayerVariable`, `*PrefsVariable`: `IVariable<T>` поверх Unity-объекта без лямбда-замыкания.
- `Elements/Scripts/Actions/Unity/MonoActionConfigurable.cs` — рабочая замена нереализуемой идее «CompositeAction в инспекторе».
- `Entities/Tests/Substitutions/` — тестовые дублёры (`MonoEntityInstallerSpy`, дублёры аспектов, бейкеров, фабрик, пулов, триггеров). Быстрый способ увидеть, как автор сам вызывает своё API; где тест закомментирован — там API недавно менялось.
