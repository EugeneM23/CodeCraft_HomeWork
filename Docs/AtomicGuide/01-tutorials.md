# 01. Подход Atomic: ядро модели

Эта часть фреймворка отвечает на один вопрос: как устроен игровой объект и кто им управляет. Atomic заменяет привычную пару «MonoBehaviour с полями + методы на нём» парой «контейнер данных + набор бесстатусных контроллеров», где данные — отдельные объекты ссылочного типа, а контроллеры подписаны на фазы жизненного цикла. Из этого вырастает всё остальное: кодогенерация ключей, установщики, миры, пулы, фильтры, системы и визуализация. Материал ниже описывает именно это ядро — то, что нужно понять до того, как трогать API, и то, что придётся переписывать в проекте в первую очередь.

Все подписи типов и все примеры в этой главе сверены с исходниками в `C:/!!!Work/_AtomicUpstream/Assets/Plugins/Atomic`. Там, где документация фреймворка расходится с исходником, в тексте стоит явная пометка — верен исходник. Фрагменты кода из `Docs/Tutorials`, которые не компилируются, приведены в разделе «Грабли» с исправлениями, а не в разделе «Идиомы».

---

## Модель

**Паттерн ESB (Entity–State–Behaviour).** `Docs/Tutorials/1. What is Atomic Approach.md` формулирует его так: сущность (`Entity`) — динамический контейнер; состояние (State) — набор *атомарных элементов*, каждый из которых отдельный объект ссылочного типа (константа, переменная, событие, действие, функция); поведение (Behaviour) — упорядоченный набор контроллеров, которые реализуют интерфейсы жизненного цикла и **не хранят состояния сами**. Данные и логика разделены жёстко. Отсюда переиспользуемость: один и тот же `MoveBehaviour` работает и на персонаже, и на пуле, потому что он не знает, на чём он стоит — он знает только имена элементов, которые ему нужны.

**Сущность — это не класс предметной области, а хэш-таблица.** Внутри `Entity` три независимых хранилища: теги (множество `int`), значения (`int -> object`) и поведения (`IEntityBehaviour[]`). Ни одно из них не является `Dictionary<,>`: это самодельные открыто-адресуемые таблицы над массивами структур-слотов. Практическое следствие: добавление поля «в сущность» — это рантайм-операция, а не правка класса; и наоборот, никакой компилятор не скажет вам, что элемент, от которого зависят пять поведений, кто-то перестал добавлять. `Docs/Tutorials/7. Summary.md` называет это прямо своим ограничением («Centralized Data»: «Removing such an element from an entity can break the entire project»).

**Ключи — это `int`, а не строки.** Имя превращается в идентификатор ровно один раз, в инициализаторе `static readonly` поля, через `EntityKeyStore.NameToId`. Все ключевые типы (`TagKey`, `TagKey<E>`, `ValueKey<T>`, `ValueKey<E,T>`, все четыре арности `EventKey`) — `readonly struct` с единственным полем `int Id` и `GetHashCode() => Id`. В рантайме строк нет.

**Значения хранятся как `object`.** Это центральный факт, который определяет всю конвенцию типизации ключей. Интерфейс сущности объявляет `void AddValue(int key, object value)` и `T GetValue<T>(int key)`, а реализация заканчивается `return (T) slot.value`. Значит, `ValueKey<int>` или `ValueKey<Vector3>` боксит при каждом `Add`/`Set` и распаковывает при каждом `Get`. Поэтому идиоматичный аргумент ключа — *интерфейс атомарного элемента* (`IValue<float>`, `IVariable<Vector3>`), то есть ссылочный тип: это не только способ закодировать право на чтение/запись в типе, но и способ не боксить. Замеры в `Docs/Entities/Entities/Manual.md#-performance` показывают цену: `Add` для ссылочного значения — 62.15 μs, для значимого — 178.45 μs на 1000 элементов.

**Элементы — это ссылки, то есть живые двусторонние ручки.** Поведение разрешает элемент один раз в `Init` и дальше пишет прямо в него: `_moveDirection.Value = ...`. Никакого обратного `SetMoveDirection` на сущности не нужно — README про это говорит отдельной врезкой. Это и есть механизм, который убирает поиск по хэш-таблице из `Tick`.

**Жизненный цикл.** `Init -> Enable -> Tick/FixedTick/LateTick -> Disable -> Dispose`. Две вещи, которые надо знать точно:

- `Entity.Enable()` сам начинается с `this.Init()`, а `Init()` идемпотентен (ранний выход по `_initialized`). То есть **отдельный вызов `Init()` перед `Enable()` не обязателен**; туториалы пишут его явно, но это не требование. `EntityWorld<E>.OnAdd` вызывает только `entity.Enable()` — свежеарендованная из пула сущность инициализируется именно там.
- `Enable`/`Disable` могут сработать много раз между одним `Init` и одним `Dispose` (`MonoEntity.OnEnable`/`OnDisable` их перевызывают). Одноразовую подписку или аллокацию нельзя класть в `IEntityEnable` — только в `IEntityInit`.

**`Dispose()` — это не «вернуть в пул», это разобрать сущность.** `Entity.Dispose()` вызывает `OnDispose()`, `Deinitialize()` (который делает `Disable()` и обходит все `IEntityDispose`), затем при `settings.disposeValues` диспозит значения, затем `ClearTags()`, `ClearValues()`, `ClearBehaviours()`, снимает все внутренние события и снимает регистрацию в `EntityRegistry.Instance`. Пулуемую сущность **нельзя** диспозить между использованиями: она останется пустой.

**Каждый архитектурный слой — тоже сущность.** `Docs/Tutorials/4. Architectural Consistency.md`: `public class GameContext : MonoEntity { }` — это полное объявление домена. Пустой наследник нужен только для того, чтобы к нему привязались типизированные ключи `ValueKey<GameContext, T>` и типизированный установщик `MonoEntityInstaller<GameContext>`. Туториал перечисляет полный набор домена прототипа: `GameEntity`, `GameContext`, `PlayerContext`, `AppContext`, `GameUI`, `MenuUI`. Старого модуля `Atomic.Contexts` не существует — контекст теперь буквально сущность.

**Взаимодействие между сущностями — процедурное.** `Docs/Tutorials/5. Interaction Between Entities.md` предписывает: один статический метод-UseCase, сущности передаются аргументами, никаких ссылок друг на друга и никакой иерархии. Врезка в конце туториала: «each behaviour should delegate all business logic to static methods placed inside static classes».

**Генерируемый API — это сахар над key-типизированными расширениями.** Генератор не добавляет нового механизма доступа: `entity.GetMoveSpeed()` разворачивается в `entity.GetValue<IValue<float>>(CharacterAPI.MoveSpeed.Id)`. Те же вызовы доступны руками через `Extensions_Values.cs`/`Extensions_Tags.cs` — это важно для миграции, потому что код можно начать переводить на ключи до того, как заработают генераторы.

**Unity 6 — жёсткое требование.** README: «The Atomic Framework requires **Unity 6+**». Это не косметика: все парные интерфейсы (`IEntityInit<in E>`, `IEntityTick<in E>`, `IEntityInstaller<in E>`, `IEntityGizmos<in E>`) мостят к неродовым формам через *default interface implementation*. Unity 2022.3 это не скомпилирует. Для проекта, который сейчас на 2022.3, это блокер плана миграции, а не пункт в чеклисте.

---

## API

### Ключи

```csharp
public readonly struct TagKey : IEquatable<TagKey>
{
    public readonly int Id;
    public TagKey(string name) => Id = EntityKeyStore.NameToId(name);
    public TagKey(int id) => this.Id = id;
    public override string ToString() => EntityKeyStore.IdToName(Id);
    public bool Equals(TagKey other) => Id == other.Id;
    public override bool Equals(object obj) => obj is TagKey other && Equals(other);
    public override int GetHashCode() => Id;
}

public readonly struct TagKey<E> : IEquatable<TagKey<E>> where E : IEntity
{
    public readonly int Id;
    public TagKey(string name) => Id = EntityKeyStore.NameToId(name);
    public TagKey(int id) => this.Id = id;
    public override string ToString() => EntityKeyStore.IdToName(Id);
    public bool Equals(TagKey<E> other) => Id == other.Id;
    public override bool Equals(object obj) => obj is TagKey<E> other && Equals(other);
    public override int GetHashCode() => Id;
}
```
Файлы: `Entities/Scripts/KeyStore/Keys/TagKey.cs`, `TagKey'1.cs`. Берите `TagKey<E>`, когда в проекте больше одного домена сущностей: тогда тег для `IGameEntity` нельзя случайно применить к `IGameContext`.

```csharp
public readonly struct ValueKey<T> : IEquatable<ValueKey<T>>
{
    public readonly int Id;
    public ValueKey(string name) => Id = EntityKeyStore.NameToId(name);
    public ValueKey(int id) => this.Id = id;
    public override string ToString() => EntityKeyStore.IdToName(Id);
    public bool Equals(ValueKey<T> other) => Id == other.Id;
    public override bool Equals(object obj) => obj is ValueKey<T> other && Equals(other);
    public override int GetHashCode() => Id;
}

public readonly struct ValueKey<E, T> : IEquatable<ValueKey<E, T>> where E : IEntity
{
    public readonly int Id;
    // тот же состав членов
}
```
Файлы: `Entities/Scripts/KeyStore/Keys/ValueKey.cs`, `ValueKey'1.cs`. Обратите внимание на асимметрию ограничений: на `T` ограничения нет нигде, на `E` — только `IEntity`. Односоставная форма `ValueKey<T>` используется в примерах README и туториалов; двусоставная `ValueKey<E, T>` — в `Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md`. Для проекта с несколькими доменами берите двусоставную.

```csharp
public readonly struct EventKey<TBus>                            : IEquatable<EventKey<TBus>>                            where TBus : IEventBus
public readonly struct EventKey<TBus, TArg>                      : IEquatable<EventKey<TBus, TArg>>                      where TBus : IEventBus
public readonly struct EventKey<TBus, TArg1, TArg2>              : IEquatable<EventKey<TBus, TArg1, TArg2>>              where TBus : IEventBus
public readonly struct EventKey<TBus, TArg1, TArg2, TArg3>       : IEquatable<EventKey<TBus, TArg1, TArg2, TArg3>>       where TBus : IEventBus
```
Файлы: `Events/Scripts/Keys/Keys/EventKey.cs`, `EventKey'1.cs`, `EventKey'2.cs`, `EventKey'3.cs`. **Первый** аргумент типа — всегда шина, не первая полезная нагрузка. Арности четыре (до трёх аргументов нагрузки), и это согласуется с `IEventBus`, у которого перегрузки `Subscribe`/`Invoke` тоже доходят до трёх. Имена событий живут в `EventKeyStore`, а не в `EntityKeyStore`.

### Кодогенерация

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityExtensionsAPIAttribute : Attribute
{
    public bool Unsafe { get; set; }
    public bool AggressiveInlining { get; set; } = true;
}
```
`Entities/Scripts/Codegen/EntityExtensionsAPIAttribute.cs`. Два переключателя, которых нет в туториалах и которые важны для рефакторинга под производительность. `AggressiveInlining` включён по умолчанию — выключайте только под профайлер/отладку. `Unsafe = true` заставляет генератор эмитить `GetValueUnsafe<T>` и `Ref{Name}()` вместо безопасного `GetValue<T>`.

```csharp
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class UnsafeAttribute : Attribute { }
```
`Entities/Scripts/Codegen/UnsafeAttribute.cs`. Поштучный opt-in на отдельное поле ключа. Конструктора с аргументом у него **нет** — пример `[Unsafe(false)]` из `Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md` не скомпилируется.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EntityDomainBehavioursAttribute : Attribute
{
    public Type EntityType { get; }
    public EntityDomainBehavioursAttribute(Type entityType) => this.EntityType = entityType;
}
```
`Entities/Scripts/Codegen/EntityDomainBehavioursAttribute.cs`. Привязывает целый класс поведений к одному домену сущностей — машинный аналог той дисциплины типизации, которую туториалы предлагают держать руками.

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class EditModeEntityBehaviourAttribute : Attribute { }
```
`Entities/Scripts/EditMode/EditModeEntityBehaviourAttribute.cs`. Маркер поведения, которое должно жить и в Edit Mode.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EventExtensionsAPIAttribute : Attribute
{
    public bool Unsafe { get; set; }
}
```
`Events/Scripts/CodeGen/EventExtensionsAPIAttribute.cs`. У событийного атрибута есть только `Unsafe`; `AggressiveInlining` тут нет.

### Сущность

```csharp
public partial interface IEntity : IInitSource, IEnableSource, ITickSource
{
    event Action<IEntity> OnStateChanged;
    int InstanceID { get; protected internal set; }
    string Name { get; set; }
    bool IsValid { get; }
}

public interface IInitSource : IDisposable
{
    event Action OnInitialized;
    event Action OnDisposed;
    bool Initialized { get; }
    void Init();
}

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
```
Файлы: `Entities/Scripts/Entities/Interface/IEntity.cs`, `Entities/Scripts/Lifecycle/Sources/{IInitSource,IEnableSource,ITickSource}.cs`. Узкие интерфейсы-источники стоит знать отдельно: именно на них, а не на `IEntity`, объявлены расширения `WhenInit`/`WhenTick`/… — поэтому те же расширения работают на мирах и коллекциях. `IEntity.Dispose()` — это `System.IDisposable.Dispose()`, унаследованный через `IInitSource`.

```csharp
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
`Entities/Scripts/Entities/Core/Entity.cs`. Основной конструктор делает `ConstructTags/ConstructValues/ConstructBehaviours` от переданных ёмкостей и затем `EntityRegistry.Instance.Register(this)`. Ёмкости по умолчанию нулевые; ёмкости тегов и значений округляются вверх до простого числа (`CeilToPrime`), а `behaviourCapacity` идёт прямо в `new IEntityBehaviour[capacity]` без округления — для пулуемых сущностей задавайте их явно. `Settings?` — nullable-структура; при `null` подставляется `new Settings { disposeValues = true }`. Коллекционные конструкторы вычисляют ёмкости сами (`tags?.Count() ?? 0`).

```csharp
public partial class MonoEntity : MonoBehaviour, IEntity

// сериализуемая конфигурация, MonoEntity_Inspector.cs:
[Header("Lifecycle")]
[SerializeField] internal bool useUnityLifecycle = true;
[SerializeField] internal bool disposeValues = true;
[Header("Installing")]
[SerializeField] internal bool installOnAwake = true;
[SerializeField] internal bool uninstallOnDestroy = true;
[SerializeField] internal List<ScriptableEntityInstaller> scriptableInstallers;
[SerializeField] [FormerlySerializedAs("installers")] internal List<MonoEntityInstaller> sceneInstallers;
[Header("Gizmos")]
[SerializeField] private bool onlySelectedGizmos;
[SerializeField] private bool onlyEditModeGizmos;
[Header("Editor")]
[SerializeField] private bool autoCompile;
[Header("Optimization")]
[Min(1)] [SerializeField] private int initialTagCapacity = 1;
[Min(1)] [SerializeField] private int initialValueCapacity = 1;
[Min(0)] [SerializeField] private int initialBehaviourCapacity;

// установка, MonoEntity_Installing.cs:
public bool Installed { get; }
public void Install();
public void WhenInstall(Action action);
public void Uninstall();
public void WhenUninstall(Action action);
public static void InstallAll(Scene scene, bool includeInactive = false);
public static void InstallAll<E>(Scene scene, bool includeInactive = false) where E : MonoEntity;
public static void InstallAll(Component component, bool includeInactive = false);
```
Поле для перетаскивания сценовых установщиков называется `sceneInstallers` (в инспекторе — **Scene Installers**); `[FormerlySerializedAs("installers")]` оставлен для совместимости. Два списка установщиков независимы: `scriptableInstallers` — общая конфигурация в ассетах, `sceneInstallers` — данные конкретного экземпляра.

Реальная раскладка по сообщениям Unity (`MonoEntity_UnityLifecycle.cs`) отличается от того, что обычно предполагают:

```csharp
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
```
То есть первый `OnEnable` (Unity вызывает его **до** `Start`) не делает ничего, а `Start` выполняет `Init()`, `Enable()` и регистрацию в тиковом менеджере. `OnDestroy` при `_started == false` пропускает `OnDispose/Deinitialize/DisposeValues`, но всё равно делает `Uninstall()` (если включён `uninstallOnDestroy`), `ClearTags/ClearValues/ClearBehaviours`, снимает события и `Unregister()`.

```csharp
public abstract class MonoEntitySingleton<E> : MonoEntity where E : MonoEntitySingleton<E>
{
    [SerializeField] private bool dontDestroyOnLoad;
    public static E Instance { get; }            // бросает Exception, если инстанса нет
    public static bool TryGetInstance(out E instance);
}
```
`Entities/Scripts/Entities/Unity/MonoEntitySingleton.cs`. `Instance` **нельзя проверять на null**: геттер делает `FindAnyObjectByType<E>(FindObjectsInactive.Exclude)` и при неудаче бросает `new Exception($"Singleton of type {typeof(E).Name} was not found in the scene.")`. В путях разрушения используйте `TryGetInstance`. `Awake` при обнаружении второго экземпляра логирует ошибку и делает `Destroy(gameObject)`.

```csharp
public abstract class MonoEntityProxy<E> : MonoBehaviour, IEntity where E : MonoEntity
{
    public E Source { get; set; }
    public string Name { get; set; }
    public bool IsValid { get; }
    public override bool Equals(object obj) => obj is IEntity entity && _source.InstanceID == entity.InstanceID;
}
```
`Entities/Scripts/Entities/Unity/MonoEntityProxy'1.cs`. Прокси на дочернем GameObject, который разрешается в родительскую сущность. Это поддерживаемая замена `TryGetComponent<IGameEntity>` на коллайдере: коллайдер на дочернем объекте можно сделать прокси, и `DealDamage(..., Collider victim, ...)` найдёт сущность без ручного прохода по иерархии.

### Значения, теги, поведения

```csharp
// IEntity_Values.cs
T GetValue<T>(int key);
ref T GetValueUnsafe<T>(int key) where T : class;
object GetValue(int key);
bool TryGetValue<T>(int key, out T value);
bool TryGetValueUnsafe<T>(int key, out T value) where T : class;
bool TryGetValue(int key, out object value);
void SetValue(int key, object value);
bool HasValue(int key);
void AddValue(int key, object value);
bool DelValue(int key);
void ClearValues();
KeyValuePair<int, object>[] GetValues();
int CopyValues(KeyValuePair<int, object>[] results);
IEnumerator<KeyValuePair<int, object>> GetValueEnumerator();
```
Обратите внимание: `object` в `AddValue`/`SetValue` — это и есть точка боксинга. `GetValueUnsafe<T>` ограничен `where T : class` и реализован через `Unsafe.As<object, T>(ref slot.value)` — то есть unsafe-режим применим только к ссылочным значениям.

```csharp
// IEntity_Behaviours.cs
event Action<IEntity, IEntityBehaviour> OnBehaviourAdded;
event Action<IEntity, IEntityBehaviour> OnBehaviourDeleted;
int BehaviourCount { get; }
void AddBehaviour(IEntityBehaviour behaviour);
T GetBehaviour<T>() where T : IEntityBehaviour;
IEntityBehaviour GetBehaviourAt(int index);
bool TryGetBehaviour<T>(out T behaviour) where T : IEntityBehaviour;
bool HasBehaviour(IEntityBehaviour behaviour);
bool HasBehaviour<T>() where T : IEntityBehaviour;
bool DelBehaviour(IEntityBehaviour behaviour);
bool DelBehaviour<T>() where T : IEntityBehaviour;
void DelBehaviours<T>() where T : IEntityBehaviour;
void ClearBehaviours();
IEntityBehaviour[] GetBehaviours();
T[] GetBehaviours<T>() where T : IEntityBehaviour;
int CopyBehaviours(IEntityBehaviour[] results);
int CopyBehaviours<T>(T[] results) where T : IEntityBehaviour;
IEnumerator<IEntityBehaviour> GetBehaviourEnumerator();
```
`BehaviourCount` + `GetBehaviourAt(int)` — безаллокационный индексный обход. `CopyBehaviours(buffer)` — безаллокационное массовое чтение в буфер вызывающего. `GetBehaviours()`/`GetBehaviours<T>()` каждый раз создают новый массив, `GetBehaviourEnumerator()` возвращает боксированный интерфейс. `DelBehaviours<T>()` (во множественном числе) — то, что должен вызывать `Uninstall`, добавивший N поведений одного типа.

```csharp
// Extensions_Values.cs — доступ по ключу без генератора
public static void AddValue(this IEntity entity, string key, object value);
public static void AddValue<T>(this IEntity entity, ValueKey<T> key, T value);
public static void AddValue<E, T>(this E entity, ValueKey<E, T> key, T value) where E : IEntity;
public static void AddValue(this IEntity entity, string key, object value, out int id);
public static void AddValues(this IEntity entity, IEnumerable<KeyValuePair<int, object>> values);
public static void AddValues(this IEntity entity, IEnumerable<KeyValuePair<string, object>> values);
public static T GetValue<T>(this IEntity entity, ValueKey<T> key);
public static T GetValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static bool TryGetValue<T>(this IEntity entity, ValueKey<T> key, out T value);
public static ref T GetValueUnsafe<T>(this IEntity entity, ValueKey<T> key) where T : class;
public static void SetValue<T>(this IEntity entity, ValueKey<T> key, T value);
public static bool HasValue<T>(this IEntity entity, ValueKey<T> key);
public static bool DelValue<T>(this IEntity entity, ValueKey<T> key);
```
Это важный факт для миграции: типобезопасный доступ по ключу есть **без** кодогенерации. Пока генераторы не настроены, можно писать `entity.AddValue(CharacterAPI.MoveSpeed, speed)` и получать ту же проверку типов.

### Интерфейсы жизненного цикла поведений

```csharp
public interface IEntityInit : IEntityBehaviour { void Init(IEntity entity); }
public interface IEntityInit<in E> : IEntityInit where E : IEntity
{
    void Init(E entity);
    void IEntityInit.Init(IEntity entity) { /* проверка типа + диспетч */ }
}

public interface IEntityTick      : IEntityBehaviour { void Tick(IEntity entity, float deltaTime); }
public interface IEntityFixedTick : IEntityBehaviour { void FixedTick(IEntity entity, float deltaTime); }
public interface IEntityLateTick  : IEntityBehaviour { void LateTick(IEntity entity, float deltaTime); }

public interface IEntityEnable  : IEntityBehaviour { void Enable(IEntity entity); }
public interface IEntityDisable : IEntityBehaviour { void Disable(IEntity entity); }
public interface IEntityDispose : IEntityBehaviour { void Dispose(IEntity entity); }

#if UNITY_5_3_OR_NEWER
public interface IEntityGizmos : IEntityBehaviour { void DrawGizmos(IEntity entity); }
#endif
```
Файлы: `Entities/Scripts/Behaviours/` — восемь интерфейсов, не семь. `IEntityGizmos` часто пропускают; он Unity-only (весь файл под `#if UNITY_5_3_OR_NEWER`) и у него тоже есть контравариантная форма `IEntityGizmos<in E>`. Каждый из восьми имеет парную `<in E> where E : IEntity` форму с default-реализацией, которая кастует и бросает `InvalidCastException` с подробным сообщением при несовпадении.

`IEntityDispose.Dispose(IEntity entity)` — **не** `System.IDisposable.Dispose()`: у него есть параметр. Класс, реализующий оба, потребует явной дизамбигуации.

`deltaTime` приходит аргументом, а не читается из `Time.deltaTime` внутри — это то, что делает поведения тестируемыми в Edit Mode и детерминированными.

### Установщики

```csharp
public interface IEntityInstaller
{
    void Install(IEntity entity);
}

public interface IEntityInstaller<in E> : IEntityInstaller where E : IEntity
{
    void Install(E entity);
    void IEntityInstaller.Install(IEntity entity) { /* проверка типа + диспетч */ }
}
```
`Entities/Scripts/Installers/IEntityInstaller.cs`, `IEntityInstaller'1.cs`. У интерфейсов `Uninstall` нет — только `Install`. Контравариантность `in E` означает, что `IEntityInstaller<IEntity>` присваивается в `IEntityInstaller<IGameEntity>`.

```csharp
public abstract class MonoEntityInstaller : MonoBehaviour, IEntityInstaller
{
#if UNITY_EDITOR
    internal Action onValidateCallback;
#endif
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
    protected virtual void OnValidate() { /* вызывает onValidateCallback вне Play/Compile */ }
}

public abstract class MonoEntityInstaller<E> : MonoEntityInstaller, IEntityInstaller<E> where E : class, IEntity
{
    public sealed override void Install(IEntity entity);      // кастует или бросает InvalidCastException
    public sealed override void Uninstall(IEntity entity);    // то же
    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}
```
Ограничение у типизированной формы строже, чем у интерфейса: `where E : class, IEntity`. Неродовые `Install`/`Uninstall` — `sealed override`, вернуть нетипизированную установку нельзя. Если переопределяете `OnValidate` в своём установщике, вызывайте `base.OnValidate()`, иначе перестанет работать живое обновление в редакторе.

```csharp
public abstract class ScriptableEntityInstaller : ScriptableObject, IEntityInstaller
{
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
}

public abstract class ScriptableEntityInstaller<E> : ScriptableEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
{
    public sealed override void Install(IEntity entity);
    public abstract void Install(E entity);
    public sealed override void Uninstall(IEntity entity);
    public virtual void Uninstall(E entity) { }
}
```
У ScriptableObject-установщиков `Uninstall` тоже есть (это расходится с распространённым утверждением, что он только у Mono-варианта). Чего у них действительно нет — это `OnValidate`/`onValidateCallback`, поэтому живого предпросмотра в редакторе они не дают.

```csharp
public sealed class MonoEntityInstallerConfigurable : MonoEntityInstaller
{
    [SerializeReference] private IEntityInstaller[] _installers;
    public override void Install(IEntity entity)
    {
        if (_installers == null) return;
        foreach (IEntityInstaller installer in _installers)
            if (installer != null)
                installer.Install(entity);
    }
}
```
`Entities/Scripts/Installers/MonoEntityInstallerConfigurable.cs`. Это поддерживаемый способ вкладывать обычные `[Serializable]` C#-установщики в инспектор — то, ради чего обычно пишут свои композитные компоненты. Заметьте: он не переопределяет `Uninstall`, так что вложенные установщики не разворачиваются обратно.

### Атомарные элементы

```csharp
public interface IValue<out T> : IFunction<T>, IValue
public interface ISetter<in T> : IAction<T>, ISetter
public interface IVariable<T> : IValue<T>, ISetter<T>
public interface IReactiveValue<out T> : IValue<T>, ISignal<T>
public interface IReactiveVariable<T> : IVariable<T>, IReactiveValue<T>
```
Файлы: `Elements/Scripts/Values/IValue.cs`, `Setters/ISetter.cs`, `Variables/Base/IVariable.cs`, `Values/IReactiveValue.cs`, `Variables/Reactive/IReactiveVariable.cs`. Этот набор — и есть тот язык, которым кодируется право доступа: поведение, которое только пишет, объявляет поле `ISetter<Vector3>`; которое только читает — `IValue<float>`.

```csharp
[Serializable]
public class Const<T> : IValue<T>
{
    public T Invoke() => this.value;
    public T Value => this.value;
    [SerializeField] private T value;
    public Const() { }
    public Const(T value) => this.value = value;
    public static implicit operator Const<T>(T value) => new(value);
    public static implicit operator T(Const<T> value) => value.value;
}

[Serializable]
public class Variable<T> : IVariable<T>
{
    public T Value { get => this.value; set => this.value = value; }
    [SerializeField] private T value;
    public T Invoke() => this.value;
    public Variable() => this.value = default;
    public Variable(T value) => this.value = value;
    public static implicit operator Variable<T>(T value) => new(value);
    public static implicit operator T(Variable<T> variable) => variable.value;
}

[Serializable]
public class ReactiveVariable<T> : IReactiveVariable<T>, IDisposable
{
    public event Action<T> OnEvent;
    public T Value { get; set; }   // сеттер сравнивает через EqualityComparer и шлёт OnEvent только на изменение
    public static implicit operator ReactiveVariable<T>(T value) => new(value);
}
```
Файлы: `Elements/Scripts/Values/Const.cs`, `Variables/Base/Variable.cs`, `Variables/Reactive/ReactiveVariable.cs`. Это **классы**, не структуры: один объект в куче на элемент на сущность. Неявные операторы — причина, по которой `[SerializeField] private Const<float> _moveSpeed = 5.0f;` компилируется и выглядит в инспекторе как обычный float. Реактивность платная (список подписчиков на элемент) — берите `Variable<T>` по умолчанию и `ReactiveVariable<T>` только там, где кто-то должен наблюдать.

```csharp
[Serializable] public class Event              : IEvent,                  IDisposable
[Serializable] public class Event<T>           : IEvent<T>,               IDisposable
[Serializable] public class Event<T1, T2>      : IEvent<T1, T2>,          IDisposable
[Serializable] public class Event<T1, T2, T3>  : IEvent<T1, T2, T3>,      IDisposable
[Serializable] public class Event<T1, T2, T3, T4>  : IEvent<T1, T2, T3, T4>,  IDisposable

public interface ISignal                                { event Action OnEvent; }
public interface ISignal<out T>                          { event Action<T> OnEvent; }
public interface ISignal<out T1, out T2>                 { event Action<T1, T2> OnEvent; }
public interface ISignal<out T1, out T2, out T3>         { event Action<T1, T2, T3> OnEvent; }
public interface ISignal<out T1, out T2, out T3, out T4> { event Action<T1, T2, T3, T4> OnEvent; }
```
`Elements/Scripts/Events/Event.cs`, `ISignal.cs`. Класс события называется `Event`, а не `BaseEvent` — последнего в исходниках нет вообще. `ISignal<T>` — узкий интерфейс только для подписки, он объявляет именно `event Action<T> OnEvent`, а `Subscribe`/`Unsubscribe` — расширения:

```csharp
// Elements/Scripts/Events/Extensions.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Subscription<T> Subscribe<T>(this ISignal<T> it, Action<T> action) => new(it, action);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Unsubscribe<T>(this ISignal<T> it, Action<T> action) => it.OnEvent -= action;

// Elements/Scripts/Values/Extensions.cs
public static Subscription<T> Observe<T>(this IReactiveValue<T> it, Action<T> action)
{
    action.Invoke(it.Value);
    return new Subscription<T>(it, action);
}
```
`Observe` **сразу вызывает** обработчик текущим значением, а потом подписывает — поэтому вид корректен на первом же кадре без дублирующей записи в `Init`. Все `Subscription`/`Subscription<T>`/… — `public readonly struct ... : IDisposable`.

### Коллекции и мир

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

public class EntityCollection<E> : IEntityCollection<E> where E : IEntity
{
    private protected const int DEFAULT_CAPACITY = 3;
    private protected static readonly ArrayPool<E> s_arrayPool = ArrayPool<E>.Shared;

    public EntityCollection() : this(DEFAULT_CAPACITY);
    public EntityCollection(int capacity);       // наследник EntityWorld<E> его НЕ пробрасывает
    public EntityCollection(IEnumerable<E> elements);
    public EntityCollection(params E[] entities);
    public EntityCollection(IReadOnlyCollection<E> elements);

    public E this[int index] { get; }
    public bool TryGetAt(int index, out E entity);
    public void CopyTo(E[] array, int arrayIndex);
    public void CopyTo(ICollection<E> results);
    public Enumerator GetEnumerator() => new(this);
    public struct Enumerator : IEnumerator<E> { /* Dispose() пустой */ }
}
```
`Entities/Scripts/Collections/EntityCollection'1.cs`, `IReadOnlyEntityCollection'1.cs`. Коллекция упорядочена по вставке, и у неё есть **индексатор** — обход по индексу вообще без энумератора строго лучше, чем `foreach`. Ёмкость по умолчанию — 3, с округлением до простого: мир на тысячи сущностей конструируйте с явной ёмкостью.

```csharp
public class EntityWorld<E> : EntityCollection<E>, IEntityWorld<E> where E : IEntity
{
    public event Action OnEnabled;
    public event Action OnDisabled;
    public bool Enabled { get; }
    public void Enable();
    public void Disable();
    public void Tick(float deltaTime);
    public void FixedTick(float deltaTime);
    public void LateTick(float deltaTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnAdd(E entity)    { if (_enabled) entity.Enable(); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnRemove(E entity) { if (_enabled) entity.Disable(); }

    public override void Dispose();
}

public class EntityWorld : EntityWorld<IEntity>, IEntityWorld
{
    public EntityWorld();
    public EntityWorld(params IEntity[] entities);
    public EntityWorld(string name = null, params IEntity[] entities);
    public EntityWorld(string name, IEnumerable<IEntity> entities);
}

// конструкторы EntityWorld<E> — те же четыре формы, ни одной с ёмкостью:
public EntityWorld();
public EntityWorld(params E[] entities);
public EntityWorld(string name = null, params E[] entities);
public EntityWorld(string name, IEnumerable<E> entities);
```
`Entities/Scripts/World/Core/EntityWorld'1.cs`, `EntityWorld.cs`. `OnAdd` вызывает именно `Enable()` — и поскольку `Entity.Enable()` сам делает `Init()`, добавление в активный мир полностью активирует сущность. Все три тиковых метода идентичны по форме: ранний выход по `!_enabled`, затем `for (int i = 0; i < _count; i++) _slots[_order[i]].value.Tick(deltaTime);`.

```csharp
// Entities/Scripts/World/Extensions.cs
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
```
Один вызов вместо восьми ручных подписок, которые выписывает `Docs/Tutorials/6. Minimizing Unity.md`. Порядок двух `WhenDispose` значим: сначала диспозятся сущности, потом сам мир.

```csharp
// Entities/Scripts/Lifecycle/Extensions.cs
public static InitSubscription      WhenInit(this IInitSource source, Action action);
public static DisposeSubscription   WhenDispose(this IInitSource source, Action action);
public static EnableSubscription    WhenEnable(this IEnableSource source, Action action);
public static DisableSubscription   WhenDisable(this IEnableSource source, Action action);
public static TickSubscription      WhenTick(this ITickSource source, Action<float> action);
public static FixedTickSubscription WhenFixedTick(this ITickSource source, Action<float> action);
public static LateTickSubscription  WhenLateTick(this ITickSource source, Action<float> action);
#if UNITY_EDITOR
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void WhenGizmos(this IGizmosSource source, Action action) => source.OnGizmosDraw += action;
#endif
```
Каждое возвращает типизированный `readonly struct`-хэндл (`InitSubscription`, `TickSubscription`, …), который туториалы молча выбрасывают. `WhenGizmos` — исключение: он не возвращает ничего и доступен только в редакторе (`GizmosSubscription` при этом в коде есть).

```csharp
// Entities/Scripts/Collections/Extensions.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void InitEntities<E>(this IEntityCollection<E> it) where E : IEntity;
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void DisposeEntities<E>(this IEntityCollection<E> it) where E : IEntity;
public static void CollectAllEntities<E>(this IEntityCollection<E> collection, bool includeInactive = false);

// Entities/Scripts/Entities/Extensions_Behaviours.cs
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static T AddBehaviour<T>(this IEntity entity) where T : IEntityBehaviour, new();
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void AddBehaviours(this IEntity entity, IEntityBehaviour[] behaviours, int startIndex, int count);
```
`AddBehaviour<T>()` требует `new()` — для поведений с аргументами конструктора или `[SerializeField]`-данными используйте неродовой `AddBehaviour(IEntityBehaviour)`. `InitEntities`/`DisposeEntities` объявлены на интерфейсе и потому боксят энумератор, но вызываются раз за жизнь мира — там это неважно. Не копируйте эту форму подписи для покадрового хелпера.

### Фильтры и триггеры

```csharp
public class EntityFilter<E> : IReadOnlyEntityCollection<E>, IDisposable where E : IEntity
{
    public EntityFilter(
        IReadOnlyEntityCollection<E> source,
        Predicate<E> predicate,
        params IEntityTrigger<E>[] triggers
    );

    public int Count { get; }
    public E this[int index] { get; }
    public bool TryGetAt(int index, out E entity);
    public bool Contains(E entity);
    public EntityCollection<E>.Enumerator GetEnumerator() => this.state.GetEnumerator();
    public void Dispose();

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
```
`Entities/Scripts/Filters/EntityFilter'1.cs`, `EntityFilter.cs`. Рядом лежат `DerivedEntityFilter` / `DerivedEntityFilter<E>`.

```csharp
public interface IEntityTrigger<E> where E : IEntity
{
    void SetAction(Action<E> action);
    void Track(E entity);
    void Untrack(E entity);
}
```
Готовые триггеры лежат в `Entities/Scripts/Triggers/`. Пять из них — обычные классы в неродовой и `<E>` формах: `TagEntityTrigger(bool added = true, bool deleted = true)`, `ValueEntityTrigger(bool added = true, bool deleted = true, bool changed = true)`, `BehaviourEntityTrigger`, `StateChangedEntityTrigger`, `InlineEntityTrigger`. Шестой — исключение: `SubscriptionEntityTrigger<S> : SubscriptionEntityTrigger<IEntity, S> where S : IDisposable` и `SubscriptionEntityTrigger<E, S>`, оба **абстрактные**, и неродовой формы без параметра типа у них нет.

Фильтр поддерживает результат инкрементально и **не лениво**: в конструкторе он подписывается на `source.OnAdded`/`OnRemoved`, прогоняет предикат по всем текущим сущностям, а каждому триггеру передаёт `SetAction(this.Synchronize)`. При срабатывании триггера пересчитывается предикат **только для той сущности**, которая изменилась:

```csharp
internal void Synchronize(E entity)
{
    bool matches = this.predicate(entity);
    if (!matches && this.state.Remove(entity)) { /* OnRemoved */ }
    else if (matches && this.state.Add(entity)) { /* OnAdded */ }
}
```
Это важно, потому что `Docs/Entities/Filters/Manual.md` в разделе Performance Considerations утверждает обратное — «Lazy Evaluation – Filter only evaluates when accessed» и «Re-evaluation Cost – Full re-filter when triggers fire». Исходник не ленив и не делает полный пересчёт. Верен исходник.

### Системы

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
    protected virtual void OnEnable();
    protected virtual void OnDisable();
    protected virtual void OnAddEntity(E entity);
    protected virtual void OnRemoveEntity(E entity);
    public abstract void Dispose();
}

[Serializable]
public abstract class EntitySystem<E> : EntitySystemBase<E>, IDisposable where E : IEntity
{
    protected EntitySystem(IReadOnlyEntityCollection<E> source, Settings settings);
    protected abstract void Update(E entity, float deltaTime);
}

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

    protected abstract void Update(E entity, float deltaTime);
    protected abstract EntityUpdatePriority EvaluatePriority(E entity);
}

public enum EntityUpdatePriority : byte { Low = 0, Medium = 1, High = 2 }
```
Файлы: `Entities/Scripts/Systems/{EntitySystemBase,EntitySystem,PriorityEntitySystem,EntityUpdatePriority}.cs`. Это ответ фреймворка на «10k сущностей, все тикать каждый кадр нельзя»: круговой обход с курсором, бюджет кадра и адаптивный размер батча; в приоритетном варианте — три корзины high/mid/low с кулдауном переоценки. Для рефакторинга под производительность это главная часть API, которой нет в туториалах.

```csharp
// Entities/Scripts/Systems/Extensions.cs
public static void AddTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
    where TContext : IEntity where TEntity : IEntity
{
    entity.WhenEnable(system.Enable);
    entity.WhenTick(system.Update);
    entity.WhenDisable(system.Disable);
    entity.WhenDispose(system.Dispose);
}
// плюс AddFixedTickSystem и AddLateTickSystem с той же формой
```
То же, что `BindTo` делает для мира: привязка системы к жизненному циклу контекста одним вызовом.

### Фабрики, аргументы, пулы

```csharp
public interface IArgs { }
public struct NoArgs : IArgs { public static readonly NoArgs Default = new(); }
public struct Args<T> : IArgs
{
    public readonly T value;
    public static implicit operator Args<T>(T value) => new(value);
}
// плюс Args<T1,T2>, Args<T1,T2,T3>, Args<T1,T2,T3,T4>

public interface IEntityFactory<out TEntity, in TArgs>
    where TEntity : IEntity where TArgs : IArgs
{
    TEntity Create(TArgs args);
}

public interface IEntityFactory<in TArgs> : IEntityFactory<IEntity, TArgs> where TArgs : IArgs { }

public interface IMultiEntityFactory<in TKey, TEntity, in TArgs>
    where TArgs : IArgs where TEntity : IEntity
{
    TEntity Create(TKey key, TArgs args);
    bool TryCreate(TKey key, TArgs args, out TEntity entity);
    bool Contains(TKey key);
}

public interface IMultiEntityFactory<in TArgs> : IMultiEntityFactory<string, IEntity, TArgs> where TArgs : IArgs { }
```
Файлы: `Entities/Scripts/Utils/Args.cs`, `Entities/Scripts/Factories/Interfaces/`. Без фабрики и полезной нагрузки `IArgs` пул не сконструировать — в туториале этот шаг пропущен, и `pool.Rent(name)` там висит в воздухе.

```csharp
public abstract class ScriptableEntityFactory<TEntity, TArgs> : ScriptableObject, IEntityFactory<TEntity, TArgs>
    where TArgs : IArgs where TEntity : IEntity
{
    [SerializeField] private int tagCapacity;
    [SerializeField] private int valueCapacity;
    [SerializeField] private int behaviourCapacity;
    [SerializeField] private Entity.Settings settings;
    [SerializeField] private bool autoCompile = true;
    public TEntity Create(TArgs args);
    protected abstract TEntity Create(
        int tagCapacity, int valueCapacity, int behaviourCapacity, Entity.Settings settings, TArgs args);
}

public abstract class ScriptableEntityFactory<TArgs> : ScriptableEntityFactory<IEntity, TArgs>, IEntityFactory<TArgs>
    where TArgs : IArgs
{
    [field: SerializeField] public virtual string Name { get; private set; }
    protected abstract void Install(IEntity entity);
}

[CreateAssetMenu(fileName = "MultiEntityFactory", menuName = "Atomic/Entities/MultiEntityFactory")]
public abstract class ScriptableEntityCatalog<TArgs> :
    ScriptableEntityCatalog<string, IEntity, ScriptableEntityFactory<TArgs>, TArgs>,
    IMultiEntityFactory<TArgs>
    where TArgs : IArgs
{
    protected override string GetKey(ScriptableEntityFactory<TArgs> factory) => factory.Name;
}
```
`Entities/Scripts/Factories/Unity/`. Вот где настраиваются те самые ёмкости из конструктора `Entity` — прямо в ассете фабрики. Каталог — это `IMultiEntityFactory`, ключ берётся из `factory.Name`; именно он передаётся в мультипул.

```csharp
public enum ExpandMode { ExpandByOne, ExpandByDoubling, NoExpand }

public interface IEntityPool<TEntity> : IDisposable where TEntity : IEntity
{
    TEntity Rent();
    void Return(TEntity entity);
    void Init(int initialCount);
}

public interface IMultiEntityPool<in TKey, TEntity> : IDisposable where TEntity : IEntity
{
    void Init(TKey key, int count);
    TEntity Rent(TKey key);
    void Return(TEntity entity);
}

public class EntityPool<TEntity, TArgs> : IEntityPool<TEntity>
{
    public EntityPool(IEntityFactory<TEntity, TArgs> factory, TArgs args,
                      ExpandMode expandMode = ExpandMode.ExpandByOne);
}

public class EntityPool<TArgs> : EntityPool<IEntity, TArgs>, IEntityPool where TArgs : IArgs

public class MultiEntityPool<TKey, TEntity, TArgs> : IMultiEntityPool<TKey, TEntity>
{
    public MultiEntityPool(IMultiEntityFactory<TKey, TEntity, TArgs> factory, TArgs args,
                           ExpandMode expandMode = ExpandMode.ExpandByOne);
}

public class MultiEntityPool<TArgs> : MultiEntityPool<string, IEntity, TArgs>, IMultiEntityPool
    where TArgs : IArgs

// Unity-варианты, которые действительно можно перетащить в [SerializeField]:
public abstract class MonoEntityPool<E, P>   : MonoBehaviour, IEntityPool<E>
public          class MonoEntityPool         : MonoEntityPool<IEntity, MonoEntity>, IEntityPool
public abstract class PrefabEntityPool<E, P> : MonoBehaviour, IPrefabEntityPool<E, P>
public          class PrefabEntityPool       : PrefabEntityPool<IEntity, MonoEntity>, IPrefabEntityPool
```
Файлы: `Entities/Scripts/Pooling/`. Важно для миграции: **неродового класса `EntityPool` не существует**, и ни `EntityPool<TArgs>`, ни `EntityPool<TEntity,TArgs>` не являются `MonoBehaviour` — перетащить их в поле инспектора нельзя. Сериализуемые пулы — это `MonoEntityPool` и `PrefabEntityPool`. Двухпараметрической формы `MultiEntityPool<string, IGameEntity>`, которую печатает туториал 6, тоже нет.

`ExpandMode.NoExpand` бросает `InvalidOperationException` при опустошении — это то, что нужно в релизной сборке, чтобы ловить недоразмеренные пулы. `ExpandByDoubling` — GC-дружелюбный режим роста. `Init(initialCount)` / `Init(key, count)` нужно вызывать для прогрева.

### Аспекты

```csharp
public interface IEntityAspect<in E> where E : IEntity
{
    void Apply(E entity);
    void Discard(E entity);
}

public interface IEntityAspect : IEntityAspect<IEntity> { }

public abstract class MonoEntityAspect<E> : MonoBehaviour, IEntityAspect<E> where E : IEntity
{
    public abstract void Apply(E entity);
    public abstract void Discard(E entity);
}
public abstract class MonoEntityAspect : MonoEntityAspect<IEntity>, IEntityAspect

// Entities/Scripts/Aspects/Extensions.cs
public static void Apply(this IEntity e, IEntityAspect entityAspect) => entityAspect.Apply(e);
public static void Apply<E>(this E e, IEntityAspect<E> entityAspect) where E : IEntity => entityAspect.Apply(e);
public static void Discard(this IEntity e, IEntityAspect entityAspect) => entityAspect.Discard(e);
public static void Discard<E>(this E e, IEntityAspect<E> entityAspect) where E : IEntity;
```
`Entities/Scripts/Aspects/`. Есть и `ScriptableEntityAspect` / `ScriptableEntityAspect<E>`. Аспект — это обратимый набор «состояние + поведения», то есть ровно тот паттерн «вид как временный аспект поверх модели», который туториал 6 предлагает делать руками через симметричные `Install`/`Uninstall`. Здесь симметрия задана типом: `Apply` абстрактен, `Discard` абстрактен, забыть второй нельзя.

### Бейкинг

```csharp
public abstract partial class MonoEntityBaker<TEntity, TArgs> : MonoBehaviour
    where TEntity : IEntity where TArgs : IArgs
{
    public TEntity Bake(TArgs args);
    protected abstract TEntity Create(/* ... */);
}

public abstract class MonoEntityBaker<TArgs> : MonoEntityBaker<IEntity, TArgs> where TArgs : IArgs
{
    protected abstract void Install(IEntity entity, TArgs args);
}

public abstract class MonoEntityBakerOptimized<TKey, TEntity, TView, TArgs> : MonoEntityBaker<TEntity, TArgs>
    where TEntity : class, IEntity
    where TView : EntityView<TEntity>
    where TArgs : IArgs
{
    [SerializeField] private TView _view;
    [SerializeField] private ScriptableEntityFactory<TEntity, TArgs> _factory;
    protected override TEntity Create(/* ... */);
    protected abstract void Override(TEntity entity, TArgs args);
    protected override void Release();
}
```
`Entities/Scripts/Baking/`. Это механизм, которым реализуется обещание «Unity — тонкий слой визуализации»: сущности авторятся в сцене MonoBehaviour-ами, а потом «запекаются» в обычные `Entity` плюс отдельный `EntityView`, и MonoBehaviour-ная стоимость на объект исчезает.

### Представления

```csharp
public abstract partial class EntityView<E> : MonoBehaviour where E : class, IEntity
{
    public E Entity { get; }
    public bool IsActive => _entity != null;
    public void Activate(E entity);
    public void Deactivate();
    protected virtual string FormateName(E entity);
    protected virtual void OnActivate(E entity);
    protected virtual void OnDeactivate(E entity);
}

public class EntityView : EntityView<IEntity>
{
    public string Name { get; private set; }
}

public abstract class EntityViewCatalog<E, V> : ScriptableObject
{
    public int Count { get; }
    public V GetPrefab(int index);
}

public abstract class EntityViewPool<K, E, V> : MonoBehaviour
{
    public void Register(K key, V prefab);
    public void Unregister(K key);
    public void Register(EntityViewCatalog<E, V> catalog);
    public void Unregister(EntityViewCatalog<E, V> catalog);
    public void Clear();
    public async ValueTask InitAsync(K key, int count);
    public void Init(K key, int count);
}

public class EntityViewPool : EntityViewPool<string, IEntity, EntityView>
{
    protected override string GetKey(EntityView view) => view.Name;
}

public abstract class EntityCollectionView<K, E, V> : MonoBehaviour, IReadOnlyCollection<KeyValuePair<E, V>>
{
    public event Action<E, V> OnAdded;
    public event Action<E, V> OnRemoved;
    public int Count { get; }
    public V Get(E entity);
    public bool TryGet(E entity, out V view);
    public bool Contains(E entity);
    public V Add(E entity);
    public void Remove(E entity);
    public void Remove(V view);
    public Dictionary<E, V>.Enumerator GetEnumerator();
    public void Clear();
}
```
`Entities/Scripts/UI/`. Рядом: `EntityWorldView`, `EntityWorldView<E>`, `EntityWorldViewSingleton`, `EntityWorldViewSingleton<T>`. Ключ вида по умолчанию — `EntityView.Name`, по нему `EntityViewPool` достаёт префаб из каталога.

### Точки входа

```csharp
[CreateAssetMenu(fileName = "EntityBootstrapper", menuName = "Atomic/Entities/EntityBootstrapper")]
public class ScriptableEntityBootstrapper : ScriptableObject
{
    public enum Mode { BeforeSceneLoad = 0, AfterSceneLoad = 1 }

    [SerializeField] private bool isEnabled = true;
    [SerializeField] private string _sceneRegex;
    [SerializeField] private Mode _mode = Mode.BeforeSceneLoad;
    [SerializeField] private MonoEntity[] _entityPrefabs;

    protected virtual bool IsAvailable(Scene scene) =>
        string.IsNullOrEmpty(_sceneRegex) || Regex.IsMatch(scene.name, _sceneRegex);

    protected virtual async void BootstrapEntities(Scene scene);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        ScriptableEntityBootstrapper[] bootstraps = Resources.LoadAll<ScriptableEntityBootstrapper>(string.Empty);
        // ...
    }
}
```
`Entities/Scripts/Bootstrap/ScriptableEntityBootstrapper.cs`. Самое важное здесь не в туториалах: обнаружение идёт через `Resources.LoadAll` — **ассет обязан лежать в папке `Resources/`**, иначе он просто не запустится. Фильтр по сцене — регулярное выражение по имени; пустая строка значит «все сцены». `BootstrapEntities` объявлен `async void`, поэтому исключение внутри переопределения не будет наблюдаемым.

Рядом есть статический сканер сцены — `MonoEntity.InstallAll(Scene, bool)`, `MonoEntity.InstallAll<E>(Scene, bool)`, `MonoEntity.InstallAll(Component, bool)` — и обширный набор фабричных методов `MonoEntity.Create(...)` / `Create<E>(...)` / `CreateAsync<E>(...)` (`MonoEntity_Creation.cs`).

### Шина событий

```csharp
public interface IEventBus : IDisposable
{
    Subscription Subscribe(int key, Action action);
    Subscription<T> Subscribe<T>(int key, Action<T> action);
    Subscription<T1, T2> Subscribe<T1, T2>(int key, Action<T1, T2> action);
    Subscription<T1, T2, T3> Subscribe<T1, T2, T3>(int key, Action<T1, T2, T3> action);
    // + SubscribeUnsafe, Unsubscribe, UnsubscribeUnsafe, Invoke, InvokeUnsafe в тех же четырёх арностях
    bool IsSubscribed(int key);
    bool Dispose(int key);
}
```
`Events/Scripts/Bus/Runtime/IEventBus.cs`. Реализации: `EventBus`, `ThreadSafeEventBus`, `MonoEventBus`, `MonoEventBusSingleton`. Максимум три аргумента нагрузки — это совпадает с максимальной арностью `EventKey`.

---

## Идиомы

### 1. Кэшировать ссылки на элементы в `Init`, не разрешать их в `Tick`

```csharp
// Controller that moves entity by its direction
public sealed class MoveBehaviour : IEntityInit, IEntityFixedTick
{
    private Transform _transform;
    private IValue<float> _moveSpeed;
    private IVariable<Vector3> _moveDirection;

    public void Init(IEntity entity)
    {
        _transform = entity.GetTransform();
        _moveSpeed = entity.GetMoveSpeed();
        _moveDirection = entity.GetMoveDirection();
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        Vector3 direction = _moveDirection.Value;
        if (direction != Vector3.zero)
            _transform.position += _moveSpeed.Value * deltaTime * direction;
    }
}
```

Самая важная форма во фреймворке (`README.md`, Quick Start, Step 4). `IEntityInit` всегда идёт в паре с тиковым интерфейсом, чтобы поиск по хэш-таблице (`entity.GetTransform()`) произошёл один раз за жизнь сущности, а не раз в кадр. Класс `sealed` — интерфейсные вызовы девиртуализуются. Поля — только кэшированные ссылки, никакого игрового состояния. `deltaTime` берётся из аргумента, а не из `Time.deltaTime`, — именно это делает поведение проверяемым в Edit Mode. Ранний выход по `Vector3.zero` экономит запись, а если элемент реактивный — ещё и рассылку подписчикам. Типы полей отражают права доступа: `IValue<float>` на скорость (только чтение), `IVariable<Vector3>` на направление.

Антипаттерн:

```csharp
public sealed class MoveBehaviour : IEntityTick
{
    public void Tick(IEntity entity, float deltaTime)
    {
        // три хэшированных поиска на сущность КАЖДЫЙ кадр
        entity.GetTransform().position +=
            entity.GetMoveSpeed().Value * deltaTime * entity.GetMoveDirection().Value;
    }
}
```

### 2. Объявлять ключи декларативно и дать генератору выпустить API

```csharp
using Atomic.Entities;
using UnityEngine;

namespace SampleGame
{
    [EntityExtensionsAPI]
    public static partial class CharacterAPI
    {
        public static readonly ValueKey<Transform> Transform = new(nameof(Transform));
        public static readonly ValueKey<IValue<float>> MoveSpeed = new(nameof(MoveSpeed));
        public static readonly ValueKey<IVariable<Vector3>> MoveDirection = new(nameof(MoveDirection));
    }
}
```

Здесь четыре решения, и каждое несёт нагрузку. (1) `static partial` — генератор дописывает вторую половину класса; без `partial` сборка падает. (2) `static readonly` с инициализатором — хэширование имени через `EntityKeyStore.NameToId` происходит один раз при инициализации типа, а не в точках вызова; анализатор делает отсутствие инициализатора ошибкой (`EAPI0001`). (3) `nameof(...)` привязывает рантайм-идентификатор к имени поля, так что переименование не может рассинхронизировать id с именем сгенерированного метода. (4) Аргумент типа — **интерфейс атомарного элемента**, а не сырое значение: `ValueKey<IValue<float>>` против `ValueKey<IVariable<Vector3>>` кодирует право на запись в системе типов, и одновременно избегает боксинга, потому что в слоте лежит ссылочный тип.

Имена генерируемых методов (`Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md`): для тегов — `Has{Name}Tag`, `Add{Name}Tag`, `Del{Name}Tag`; для значений — `Get{Name}`, `Set{Name}`, `Add{Name}`, `Has{Name}`, `Del{Name}`, `TryGet{Name}`; в unsafe-режиме дополнительно `Ref{Name}`.

Антипаттерн — строковый доступ, к которому туториал 1 неожиданно возвращается в разделе «Reusing Mechanics»:

```csharp
// перехэширует имя при каждом вызове, никакой проверки типов на компиляции
entity.AddValue("Position", new Variable<Vector3>());
entity.AddValue("MoveSpeed", new Variable<float>(3.5f));
entity.AddValue("Damage", 10);                          // ещё и боксированный int вместо элемента
var pos = (IVariable<Vector3>) entity.GetValue("Position");  // каст может упасть в рантайме
```

### 3. Пока генераторы не настроены — key-типизированные расширения

```csharp
// то же самое, что entity.AddMoveSpeed(speed) / entity.GetMoveSpeed(),
// но без кодогенерации — работает сразу
entity.AddValue(CharacterAPI.MoveSpeed, speed);
IValue<float> cached = entity.GetValue(CharacterAPI.MoveSpeed);

if (entity.TryGetValue(CharacterAPI.MoveDirection, out IVariable<Vector3> dir))
    dir.Value = Vector3.forward;
```

`Extensions_Values.cs` объявляет перегрузки `AddValue<T>(ValueKey<T>, T)`, `GetValue<T>(ValueKey<T>)`, `TryGetValue<T>`, `SetValue<T>`, `HasValue<T>`, `DelValue<T>`, и те же в форме `<E, T>` для `ValueKey<E, T>`. Форма правильная для миграции: типобезопасность появляется сразу, до того как заработает DLL-пайплайн генераторов, и переход на `entity.GetMoveSpeed()` потом чисто косметический.

### 4. Писать через ссылку элемента, а не через сеттер на сущности

```csharp
public class InputBehaviour : IEntityInit, IEntityTick
{
    private const string HORIZONTAL = "Horizontal";
    private const string VERTICAL = "Vertical";

    private ISetter<Vector3> _moveDirection;

    public void Init(IEntity entity)
    {
        _moveDirection = entity.GetMoveDirection();
    }

    public void Tick(IEntity entity, float deltaTime)
    {
        float dx = Input.GetAxis(HORIZONTAL);
        float dz = Input.GetAxis(VERTICAL);
        _moveDirection.Value = new Vector3(dx, 0, dz);
    }
}
```

README говорит это прямым текстом: «To change the entity's data, modify the value through its reference. No `SetMoveDirection` on the `IEntity` is required». Элементы — ссылочные типы, поэтому кэшированная ссылка это живая двусторонняя ручка: нет шага обратной записи и нет второго поиска. Намеренное сужение до `ISetter<Vector3>` важно: это поведение пишет и никогда не читает, и тип поля об этом сообщает — ревьюер видит с первого взгляда, что ввод не читает состояние симуляции. Имена осей — `const string` поля, а не литералы в вызове.

Антипаттерн:

```csharp
public void Tick(IEntity entity, float deltaTime)
{
    // второй поиск по хэш-таблице плюс круговой путь значения через сущность
    entity.SetMoveDirection(new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")));
}
```

### 5. Установщик держит авторские данные, поведение не держит ничего

```csharp
// Populates entity with tags, values and behaviours
public sealed class CharacterInstaller : MonoEntityInstaller
{
    [SerializeField] private Transform _transform;
    [SerializeField] private Const<float> _moveSpeed = 5.0f;
    [SerializeField] private Variable<Vector3> _moveDirection = Vector3.zero;

    public override void Install(IEntity entity)
    {
        entity.AddTransform(_transform);
        entity.AddMoveSpeed(_moveSpeed);
        entity.AddMoveDirection(_moveDirection);

        entity.AddBehaviour<MoveBehaviour>();
        entity.AddBehaviour<InputBehaviour>();
    }
}
```

Разделение обязанностей без остатка: установщик — единственное место, которое знает про инспектор; поведение — единственное место, которое знает про логику; сущность — единственное место, которое держит состояние. `Const<float> _moveSpeed = 5.0f` опирается на `implicit operator Const<T>(T)`, поэтому дизайнер видит обычное поле float. `entity.AddBehaviour<MoveBehaviour>()` использует перегрузку с ограничением `new()` — поведению не нужны данные, значит не нужно и `new MoveBehaviour()`, и нечего сохранять. Добавление управления с клавиатуры — правка в одну строку, `MoveBehaviour` не меняется.

Антипаттерн:

```csharp
public sealed class CharacterInstaller : MonoEntityInstaller
{
    [SerializeField] private float _moveSpeed = 5.0f;     // сырое значение, не атомарный элемент
    public override void Install(IEntity entity)
    {
        // аллокация на каждую установку, и поведение теперь владеет состоянием
        entity.AddBehaviour(new MoveBehaviour(_transform, _moveSpeed));
    }
}
```

### 6. Предпочитать типизированную базу установщика

```csharp
// Game Context
public sealed class GameContextInstaller : MonoEntityInstaller<GameContext>
{
    [SerializeField] private Transform _worldTransform;
    [SerializeField] private TeamCatalog _teamCatalog;
    [SerializeField] private MonoEntityPool _bulletPool;

    public override void Install(GameContext context)
    {
        context.AddPlayers(new Dictionary<TeamType, IPlayerContext>());
        context.AddWorldTransform(_worldTransform);
        context.AddTeamCatalog(_teamCatalog);
        context.AddBulletPool(_bulletPool);
        context.AddGameOverEvent(new Event());
    }
}
```

`MonoEntityInstaller<GameContext>` делает неродовой `Install(IEntity)` `sealed` и выполняет каст один раз, поэтому тело без кастов, а генерируемые `AddXxx` могут быть вариантами на `ValueKey<GameContext, T>`. Установщик сгруппирован по фичам, и каждый элемент добавляется как атомарный элемент (`new Event()`), а не как C#-`event`.

Два отличия от версии в `Docs/Tutorials/4. Architectural Consistency.md`, и оба обязательны: класса `BaseEvent` в исходниках не существует (есть `Event`), а поле `[SerializeField] private EntityPool _bulletPool;` не скомпилируется как drag-target — неродового `EntityPool` нет, а родовые формы не `MonoBehaviour`. Сериализуемый пул — `MonoEntityPool` или `PrefabEntityPool`.

Антипаттерн:

```csharp
public sealed class GameContextInstaller : MonoEntityInstaller
{
    public override void Install(IEntity entity)
    {
        var context = (GameContext) entity;   // ручной каст в каждом установщике
        context.AddTeamCatalog(_teamCatalog);
    }
}
```

### 7. Группировать регистрации по фиче — `[Header]` плюс комментарий

```csharp
// Application Context
public sealed class AppContextInstaller : MonoEntityInstaller<AppContext>
{
    [Header("Quit")]
    [SerializeField] private KeyCode _exitKey = KeyCode.Escape;

    [Header("Levels")]
    [SerializeField] private Const<int> _startLevel;
    [SerializeField] private Const<int> _maxLevel;
    [SerializeField] private ReactiveVariable<int> _currentLevel = 1;

    public override void Install(AppContext context)
    {
        // Quit
        context.AddExitKeyCode(new Const<KeyCode>(_exitKey));
        context.AddBehaviour<QuitController>();

        // Level System
        context.AddStartLevel(_startLevel);
        context.AddMaxLevel(_maxLevel);
        context.AddCurrentLevel(_currentLevel);
        context.AddBehaviour<LevelSaveLoadController>();

        // Menu
        context.AddBehaviour<MenuLoadController>();
    }
}
```

Каждая фича — непрерывный блок «её данные, потом её поведение», и `[Header]` в инспекторе повторяет комментарий в `Install`. Так фича добавляется и удаляется как одна единица, что на практике единственный способ жить с ограничением «Centralized Data» из туториала 7. Обратите внимание на выбор типов элементов: `Const<int>` для неизменных границ уровня, `ReactiveVariable<int>` для `_currentLevel`, потому что UI обязан за ним наблюдать. Реактивность оплачена только там, где она нужна.

### 8. Симметричный `Uninstall`, если установщик целит в вид или в пулуемую сущность

```csharp
public sealed class CharacterViewInstaller : MonoEntityInstaller<GameEntity>
{
    [SerializeField] private TakeDamageViewBehaviour _takeDamageBehaviour;
    [SerializeField] private PositionViewBehaviour _positionBehaviour;
    [SerializeField] private RotationViewBehaviour _rotationBehaviour;
    [SerializeField] private TeamColorViewBehaviour _teamColorBehaviour;
    [SerializeField] private WeaponRecoilViewBehaviour _weaponRecoilBehaviour;

    public override void Install(GameEntity entity)
    {
        entity.AddBehaviour(_takeDamageBehaviour);
        entity.AddBehaviour(_positionBehaviour);
        entity.AddBehaviour(_rotationBehaviour);
        entity.AddBehaviour(_teamColorBehaviour);
        entity.AddBehaviour(_weaponRecoilBehaviour);
    }

    public override void Uninstall(GameEntity entity)
    {
        entity.DelBehaviour(_takeDamageBehaviour);
        entity.DelBehaviour(_positionBehaviour);
        entity.DelBehaviour(_rotationBehaviour);
        entity.DelBehaviour(_teamColorBehaviour);
        entity.DelBehaviour(_weaponRecoilBehaviour);
    }
}
```

Вид — «временный аспект», навешенный на долгоживущую модельную сущность, поэтому `Install` должен быть в точности обратим. `Uninstall` удаляет те же экземпляры по ссылке — и именно поэтому поведения здесь `[SerializeField]`-экземпляры, а не конструкции `AddBehaviour<T>()`: нельзя `DelBehaviour` то, чего не сохранил. Если `Uninstall` не переопределить, при каждом прогоне сущности через пул на ней будут накапливаться видовые поведения.

Если установщик добавил N поведений одного типа, в `Uninstall` используйте `DelBehaviours<T>()` (множественное число), а не N вызовов `DelBehaviour<T>()`: неродовой `DelBehaviour<T>()` удаляет только первое совпадение.

Альтернатива, которую стоит рассмотреть на новом коде вместо ручной пары: `IEntityAspect<E>` с его `Apply`/`Discard` — там симметрия не забывается, потому что оба метода абстрактны.

### 9. Всю межсущностную логику — в статические UseCase

```csharp
public static class CombatUseCase
{
    public static bool DealDamage(
        IGameEntity instigator,
        IGameEntity victim,
        IGameContext gameContext,
        int damage
    )
    {
        IVariable<int> health = victim.GetHealth();
        if (health.Value <= 0)
            return false;

        health.Value = Math.Max(0, health.Value - damage);
        victim.GetTakeDamageEvent().Invoke(instigator, damage);

        if (health.Value == 0)
            gameContext.GetKillEvent().Invoke(instigator, victim);

        return true;
    }
}
```

Взаимодействие живёт в одном алгоритме; сущности — аргументы, поэтому они не держат ссылок друг на друга и никакой иерархии наследования не нужно. `static` на бесстатусном классе означает отсутствие аллокаций и отсутствие инстанс-диспетча. Возврат `bool` вместо исключения превращает охранное условие (`health.Value <= 0`) в ветку вызывающего. `Math.Max(0, ...)` клампит, так что в элементе здоровья никогда не окажется отрицательного значения. Локальная `IVariable<int> health` разрешает элемент один раз на три использования.

Антипаттерн:

```csharp
// логика зашита в поведение: непереиспользуема и нетестируема без графа сущностей
public sealed class BulletCollisionBehaviour : IEntityTick
{
    public void Tick(IEntity entity, float dt)
    {
        var victimHealth = _victim.GetHealth();
        victimHealth.Value -= _damage;   // нет клампа, нет события, дублируется в каждой точке вызова
    }
}
```

### 10. Расширять UseCase перегрузкой, а не правкой

```csharp
public static class CombatUseCase
{
    // Overloaded method
    public static bool DealDamage(
       IGameEntity instigator,
       Collider victim,
       IGameContext gameContext,
       int damage
    )
    {
       return victim.TryGetComponent(out IGameEntity target) &&
             DealDamage(instigator, target, gameContext, damage);
    }

    // Original method, written earlier
    public static bool DealDamage(
        IGameEntity instigator,
        IGameEntity victim,
        IGameContext gameContext,
        int damage
    )
    {
        // ...
    }
}
```

Unity-адаптер (`Collider`) — тонкая перегрузка, которая сводится к чистому C#-ядру и возвращает `false`, если коллайдер не сущность. `TryGetComponent` — безаллокационный доступ (в отличие от `GetComponent` + проверка на null), а `&&` коротко замыкается, поэтому на промахе никакой работы по урону не происходит. Чистая перегрузка остаётся Unity-независимой и, следовательно, юнит-тестируемой.

Если коллайдеры висят на дочерних объектах, вместо расширения этой перегрузки поставьте на них `MonoEntityProxy` — тогда `TryGetComponent<IGameEntity>` найдёт родительскую сущность штатно.

### 11. Подписаться в `Init`, отписаться в `Dispose`, держать ссылку на сигнал

```csharp
public sealed class LeaderboardController :
    IEntityInit<IGameContext>,
    IEntityDispose
{
    private ISignal<KillArgs> _killEvent;
    private IGameContext _gameContext;

    public void Init(IGameContext context)
    {
        _gameContext = context;
        _killEvent = context.GetKillEvent();
        _killEvent.Subscribe(this.OnKill);
    }

    public void Dispose(IEntity entity)
    {
        _killEvent.Unsubscribe(this.OnKill);
    }

    private void OnKill(KillArgs args)
    {
        LeaderboardUseCase.AddScore(_gameContext, args);
    }
}
```

Ссылка на элемент события кэшируется в `_killEvent`, поэтому `Dispose` отписывается именно от того элемента, на который подписался — это корректно даже если набор значений сущности между этим поменялся, и избегает второго поиска при разрушении. Обработчик — именованный приватный метод (`this.OnKill`), а не лямбда, поэтому идентичность делегата стабильна и `Unsubscribe` действительно находит его. Тело обработчика не делает ничего, кроме делегирования в статический UseCase. `ISignal<T>` — узкий, только-подписочный вид на элемент события.

Асимметрия `IEntityInit<IGameContext>` / `IEntityDispose` (а не `IEntityDispose<IGameContext>`) — из поставляемого примера; в новом коде берите `IEntityDispose<E>` для симметрии.

Антипаттерн:

```csharp
public void Init(IGameContext context)
{
    // лямбда: новый экземпляр делегата, отписаться невозможно, замыкание утекает
    context.GetKillEvent().Subscribe(args => LeaderboardUseCase.AddScore(context, args));
}
```

### 12. Реактивный видовой контроллер: наблюдать один раз, писать в Transform только на изменение

```csharp
[Serializable]
public sealed class PositionViewBehaviour :
    IEntityInit<GameEntity>,
    IEntityDispose
{
    [SerializeField]
    private Transform _transform;

    private IReactiveValue<Vector3> _position;

    public PositionViewBehaviour(Transform transform)
    {
        _transform = transform;
    }

    public void Init(GameEntity entity)
    {
        _position = entity.GetPosition();
        _position.Observe(this.OnPositionChanged);
    }

    public void Dispose(IEntity entity)
    {
        _position.Unsubscribe(this.OnPositionChanged);
    }

    private void OnPositionChanged(Vector3 position)
    {
        _transform.position = position;
    }
}
```

Событийный вместо опрашивающего: здесь нет `Tick`, поэтому неподвижная сущность стоит нуль в кадре — в противоположность `LateTick`, который писал бы `_transform.position` каждый кадр независимо ни от чего. `Observe` (а не `Subscribe`) и подписывается, и сразу один раз вызывается с текущим значением, так что Transform корректен на первом кадре без дублирующей инициализирующей записи. `[Serializable]` плюс `[SerializeField] Transform` позволяют авторить видовое поведение как вложенный объект инспектора, но при этом конструировать его из кода. Врезка туториала добавляет правило: «A good practice is to avoid using shared data if it's only needed within a single visual behaviour» — видовые данные держите в `[SerializeField]`, а не в значениях сущности: это экономит слот значения и поиск.

Антипаттерн:

```csharp
public sealed class PositionViewBehaviour : IEntityLateTick
{
    public void LateTick(IEntity entity, float dt)
        => _transform.position = entity.GetPosition().Value;  // поиск и запись каждый кадр, всегда
}
```

### 13. Точка входа владеет жизненным циклом Unity и больше ничем

```csharp
public sealed class EntryPoint : MonoBehaviour
{
    private GameContext _gameContext;

    [SerializeField]
    private GameContextInstaller _gameInstaller;

    private void Awake()
    {
        _gameContext = new GameContext();
        _gameInstaller.Install(_gameContext);
    }

    private void Start()
    {
        _gameContext.Init();
        _gameContext.Enable();
    }

    private void Update()
    {
        _gameContext.Tick(Time.deltaTime);
    }

    private void FixedUpdate()
    {
       _gameContext.FixedTick(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        _gameContext.LateTick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        _gameContext.Disable();
        _gameContext.Dispose();
    }
}
```

Один MonoBehaviour на всю симуляцию. Каждое сообщение Unity — один пересылающий вызов, то есть вся игра приводится в движение одним `Update` вместо N колбэков `MonoBehaviour.Update`; именно это туториал 7 называет выигрышем по памяти и по накладным расходам Unity. Конструирование в `Awake`, `Init + Enable` в `Start` (чтобы все чужие `Awake` уже прошли), разрушение в точно обратном порядке. `Time.fixedDeltaTime` в `FixedUpdate` и `Time.deltaTime` в `LateUpdate` — это не копипаста, а правильные значения. Контекст — обычный `new GameContext()`, не MonoBehaviour, поэтому тестируется headless.

Мелкое уточнение по исходнику: `_gameContext.Init()` перед `Enable()` избыточен, потому что `Enable()` сам вызывает `Init()`. Строку можно оставить как документацию намерения — она идемпотентна — но полагаться на неё как на необходимый шаг не надо.

### 14. Привязывать жизненный цикл мира к хозяину одним вызовом

```csharp
[Serializable]
public sealed class GameContextInstaller : IEntityInstaller<IGameContext>
{
    public void Install(IGameContext context)
    {
        var entityWorld = new EntityWorld<IGameEntity>();
        context.AddEntityWorld(entityWorld);

        entityWorld.BindTo(context);
    }
}
```

`world.BindTo(context)` — это побайтово тот же набор восьми подписок, который `Docs/Tutorials/6. Minimizing Unity.md` выписывает вручную, но помеченный `[MethodImpl(MethodImplOptions.AggressiveInlining)]` и с гарантированно правильным порядком двух `WhenDispose`. `[Serializable]` плюс `IEntityInstaller<IGameContext>` дают не-MonoBehaviour установщик, который всё равно сериализуется в инспекторе (через `MonoEntityInstallerConfigurable` и его `[SerializeReference]`).

Предзадать ёмкость мира, к сожалению, нельзя: все четыре конструктора `EntityWorld<E>` (`()`, `(params E[])`, `(string, params E[])`, `(string, IEnumerable<E>)`) идут в неявный `base()`, то есть в `EntityCollection() : this(DEFAULT_CAPACITY)` с ёмкостью 3. Перегрузки `EntityWorld<E>(int capacity)` в исходнике нет. Мир на тысячи сущностей будет перехэшироваться при наполнении, и обойти это из API мира нечем — только своим наследником `EntityCollection<E>`.

Антипаттерн — восемь ручных подписок:

```csharp
// то, что печатает туториал; заменяется одной строкой entityWorld.BindTo(context);
context.WhenInit(entityWorld.InitEntities);
context.WhenEnable(entityWorld.Enable);
context.WhenTick(entityWorld.Tick);
context.WhenFixedTick(entityWorld.FixedTick);
context.WhenLateTick(entityWorld.LateTick);
context.WhenDisable(entityWorld.Disable);
context.WhenDispose(entityWorld.DisposeEntities);
context.WhenDispose(entityWorld.Dispose);
```

### 15. Спавн и despawn как пара «пул плюс мир» в статическом UseCase

```csharp
public static class GameEntitiesUseCase
{
    public static IGameEntity Spawn(
        IGameContext gameContext,
        string name,
        Vector3 position,
        Quaternion rotation,
        TeamType team
    )
    {
        // Rent a game object from the pool
        IMultiEntityPool<string, IGameEntity> pool = gameContext.GetEntityPool();
        IGameEntity entity = pool.Rent(name);

        // Update its position and team
        entity.GetPosition().Value = position;
        entity.GetRotation().Value = rotation;
        entity.GetTeam().Value = team;

        // Add the game object to the world
        EntityWorld<IGameEntity> world = gameContext.GetEntityWorld();
        world.Add(entity);   // автоматическая активация
        return entity;
    }

    public static bool Despawn(IGameContext gameContext, IGameEntity entity)
    {
        EntityWorld<IGameEntity> world = gameContext.GetEntityWorld();
        if (!world.Remove(entity))   // автоматическая деактивация
            return false;

        IMultiEntityPool<string, IGameEntity> pool = gameContext.GetEntityPool();
        pool.Return(entity);
        return true;
    }
}
```

Смысл в порядке операций: `Rent` -> выставить состояние -> `world.Add` (который активирует), и на выходе `world.Remove` (который деактивирует) **до** `pool.Return`. Возврат в пул сущности, которая всё ещё в мире, оставил бы живой тикающий переиспользуемый объект. `Despawn` возвращает `false` и пропускает возврат в пул, если сущности в мире не было, так что двойной despawn не испортит пул. Никаких `Instantiate`/`Destroy` — спавн без аллокаций.

Два отличия от версии в туториале, оба необходимые. Во-первых, тип: `MultiEntityPool<string, IGameEntity>` с двумя аргументами не существует — реальные объявления `MultiEntityPool<TKey, TEntity, TArgs>` и `MultiEntityPool<TArgs> : MultiEntityPool<string, IEntity, TArgs>`; в ключе сущности лучше хранить интерфейс `IMultiEntityPool<string, IGameEntity>`, он как раз двухпараметрический. Во-вторых, в туториале `Despawn` присваивает локальную `pool` и тут же её игнорирует, вызывая `gameContext.GetEntityPool().Return(entity)` — то есть разрешает пул дважды. Используйте локальную.

### 16. Собрать пул: фабрика, каталог, прогрев

```csharp
// Фабрика одного вида сущности — ScriptableObject с ёмкостями в инспекторе
[CreateAssetMenu(menuName = "Game/Factories/Bullet")]
public sealed class BulletFactory : ScriptableEntityFactory<NoArgs>
{
    [SerializeField] private Const<float> _speed = 30f;
    [SerializeField] private Const<int> _damage = 10;

    protected override void Install(IEntity entity)
    {
        entity.AddPosition(new Variable<Vector3>());
        entity.AddRotation(new Variable<Quaternion>());
        entity.AddMoveSpeed(_speed);
        entity.AddDamage(_damage);
        entity.AddBehaviour<MoveBehaviour>();
    }
}

// Каталог = IMultiEntityFactory: ключ берётся из factory.Name
[CreateAssetMenu(menuName = "Game/Factories/EntityCatalog")]
public sealed class GameEntityCatalog : ScriptableEntityCatalog<NoArgs>
{
}

// Сборка пула в установщике контекста
public void Install(IGameContext context)
{
    var pool = new MultiEntityPool<string, IEntity, NoArgs>(
        _entityCatalog,
        NoArgs.Default,
        ExpandMode.NoExpand
    );

    pool.Init("Bullet", 256);
    pool.Init("Enemy", 64);

    context.AddEntityPool(pool);
}
```

Это то, чего нет в туториале 6, из-за чего его пример спавна нельзя довести до рабочего состояния. Форма правильна по трём причинам. Ёмкости (`tagCapacity`, `valueCapacity`, `behaviourCapacity`) задаются в ассете фабрики — они попадают прямо в конструктор `Entity` и убирают переаллокацию на первом `Add` каждого вида. `ExpandMode.NoExpand` бросает `InvalidOperationException` при опустошении — в релизной сборке это именно то, что нужно, чтобы недоразмеренный пул упал громко, а не начал аллоцировать в кадре; если расширение нужно, берите `ExpandByDoubling`, он делает один всплеск вместо N мелких. `Init(key, count)` обязателен для прогрева — без него первый `Rent` пойдёт в фабрику.

Одно ограничение сокращённых форм: `ScriptableEntityCatalog<TArgs>` жёстко зафиксирован как `ScriptableEntityCatalog<string, IEntity, ScriptableEntityFactory<TArgs>, TArgs>`, а `ScriptableEntityFactory<TArgs>` — как `ScriptableEntityFactory<IEntity, TArgs>`. Поэтому пул, типизированный доменом (`IMultiEntityPool<string, IGameEntity>`, как в идиоме 15), на сокращённых формах не собрать — нужны полные четырёх- и двухпараметрические базы `ScriptableEntityCatalog<K, E, F, TArgs>` и `ScriptableEntityFactory<TEntity, TArgs>`, и тогда `GetKey` придётся брать не из `factory.Name` (его объявляет только `ScriptableEntityFactory<TArgs>`), а из собственного поля.

Чистые C#-пулы и фабрики-каталоги **не покрыты действующими тестами**: `Entities/Tests/EditMode/Pool/EntityPoolTests.cs`, `Pool/MultiEntityPoolTests.cs`, `Factory/MultiEntityFactoryTests.cs` и `Entities/Tests/PlayMode/ScriptableEntityFactoryCatalog/ScriptableEntityFactoryCatalogTests.cs` закомментированы целиком (ноль незакомментированных строк). Unity-пулы тесты имеют: `Entities/Tests/PlayMode/SceneEntityPool/SceneEntityPoolTests.cs` (тестирует `MonoEntityPool`) и `PrefabEntityPoolTests.cs` живые. Относитесь к `EntityPool<>`/`MultiEntityPool<>` и каталогам как к непокрытым.

### 17. Запрос вместо покадрового скана: `EntityFilter` плюс `EntitySystem`

```csharp
// Реактивный запрос: предикат пересчитывается только когда сработал триггер
_aliveEnemies = EntityFilter<IGameEntity>.StartBuild()
    .WithSource(world)
    .WithPredicate(e => e.HasEnemyTag() && e.GetHealth().Value > 0)
    .WithTriggers(
        new TagEntityTrigger<IGameEntity>(),
        new ValueEntityTrigger<IGameEntity>()
    )
    .Build();

// Система с бюджетом кадра над этим запросом
public sealed class EnemyAiSystem : EntitySystem<IGameEntity>
{
    public EnemyAiSystem(IReadOnlyEntityCollection<IGameEntity> source, Settings settings)
        : base(source, settings) { }

    protected override void Update(IGameEntity entity, float deltaTime)
    {
        EnemyAiUseCase.Think(entity, deltaTime);
    }
}

// Привязка к контексту
public void Install(IGameContext context)
{
    var system = new EnemyAiSystem(_aliveEnemies, new EntitySystemBase<IGameEntity>.Settings());
    context.AddTickSystem(system);
}
```

Правильная форма по двум причинам. Фильтр поддерживается инкрементально — предикат вызывается при добавлении/удалении из источника и при срабатывании объявленного триггера, то есть «все живые враги» стоит нуль в кадре, а не один проход по миру; и `GetEnumerator()` фильтра — это переэкспортированный `EntityCollection<E>.Enumerator`, то есть структура, обход без аллокаций. Система поверх фильтра даёт фиксированную стоимость кадра независимо от числа сущностей: `OnUpdate(batchSize, deltaTime)` обрабатывает `batchSize` штук от сохранённого курсора с заворотом, а `batchSize` подстраивается под `Settings.frameBudget`.

Антипаттерн, который типично пишут на Atomic:

```csharp
public void Tick(IEntity entity, float deltaTime)
{
    // полный проход по миру каждый кадр, плюс два поиска на сущность
    foreach (IGameEntity e in _world)
        if (e.HasEnemyTag() && e.GetHealth().Value > 0)
            EnemyAiUseCase.Think(e, deltaTime);
}
```

### 18. Каждый архитектурный слой — просто сущность

```csharp
public class GameContext : MonoEntity
{
}
```

Пустой наследник `MonoEntity` — полное объявление домена. Тип существует только для того, чтобы генерируемый API `ValueKey<GameContext, T>` и `MonoEntityInstaller<GameContext>` имели к чему привязаться, то есть чтобы ключ, предназначенный игровому контексту, нельзя было применить к пуле. Так игровые сущности, контексты, корни UI и контекст приложения оказываются одним механизмом. Полный набор домена из туториала 4: `GameEntity`, `GameContext`, `PlayerContext`, `AppContext`, `GameUI`, `MenuUI`.

Для синглтонного контекста форма с рекурсивным ограничением:

```csharp
public sealed class GameContext : MonoEntitySingleton<GameContext>
{
}
```

### 19. Презентеры UI — это поведения на UI-сущности

```csharp
// Game UI
public sealed class GameUIInstaller : MonoEntityInstaller<GameUI>
{
    [SerializeField] private CountdownView _countdown;
    [SerializeField] private ScoreView _score;

    public override void Install(GameUI ui)
    {
        // Countdown
        ui.AddCountdownView(_countdown);
        ui.AddBehaviour<CountdownPresenter>();

        // Score
        ui.AddScoreView(_score);
        ui.AddBehaviour<ScorePresenter>();
    }
}
```

MVP без дополнительной машинерии: View — атомарный элемент на UI-сущности, Presenter — поведение, которое разрешает его в `Init`, а жизненный цикл UI-сущности сам снимает подписки. Та же парность «данные плюс поведение на фичу», что и в геймплейных установщиках.

В туториале 4 база этого класса написана как `EntityInstaller<GameUI>`. Такого типа в исходниках нет (есть `IEntityInstaller`, `IEntityInstaller<E>`, `MonoEntityInstaller`, `MonoEntityInstaller<E>`, `MonoEntityInstallerConfigurable`, `ScriptableEntityInstaller`, `ScriptableEntityInstaller<E>` — семь типов), читайте как `MonoEntityInstaller<GameUI>`.

### 20. Шина событий: ключи в своём API-классе, явная парность подписки

```csharp
using Atomic.Events;

[EventExtensionsAPI]
public static partial class GameEventAPI
{
    public static readonly EventKey<IEventBus> PlayerTurnStarted = new(nameof(PlayerTurnStarted));
    public static readonly EventKey<IEventBus, IEntity, int> EntityDamaged = new(nameof(EntityDamaged));
}

public sealed class DamagePresenter : IDisposable
{
    private readonly IEventBus _eventBus;

    public DamagePresenter(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _eventBus.SubscribeEntityDamaged(OnEntityDamaged);
    }

    public void Dispose()
    {
        _eventBus.UnsubscribeEntityDamaged(OnEntityDamaged);
    }

    private void OnEntityDamaged(IEntity entity, int damage)
    {
        Debug.Log($"Entity damaged: {entity} {damage}");
    }
}
```

Та же дисциплина, что у `[EntityExtensionsAPI]`: `static partial`, `static readonly`, `nameof`. Типы нагрузки живут в ключе, поэтому `InvokeEntityDamaged(target, damage)` проверяется на компиляции и никаких `object[]` или боксированных аргументов не возникает. Держать событийные ключи в отдельном API-классе от значений полезно потому, что выходы двух генераторов остаются независимыми. Поле шины `readonly`, обработчик — именованный метод (иначе `Unsubscribe` не найдёт делегат), пара подписка/отписка видимо симметрична в конструкторе и `Dispose`.

### 21. UseCase мутирует, затем объявляет

```csharp
public static class DealDamageUseCase
{
    public static void Execute(IEntity target, int damage, IEventBus eventBus)
    {
        IVariable<int> health = target.GetHealth();
        health.Value = Mathf.Max(0, health.Value - damage);
        eventBus.InvokeEntityDamaged(target, damage);
    }
}
```

Форма: разрешить элемент один раз, заклампить мутацию, затем выпустить типизированное событие, чтобы слушатели отреагировали — изменение состояния и уведомление в одном месте, а не разбросаны.

Это исправленная версия. Фрагмент в `Docs/Tutorials/2. Entity API Generation.md` не компилируется: класс объявлен `static`, а `Execute` — инстансный метод; написано `health - damage`, то есть вычитание `int` из `IVariable<int>` (должно быть `health.Value - damage`); и используется необъявленное поле `_eventBus` вместо параметра `eventBus`. Последняя ошибка, кстати, возможна именно из-за неявного оператора `implicit operator T(Variable<T>)`: он молча распаковывает элемент в арифметике и прячет поиск.

### 22. Тестировать механику headless в Edit Mode на обычном `Entity`

```csharp
public sealed class MoveBehaviourTests
{
    private Entity entity;

    [SetUp]
    public void SetUp()
    {
        entity = new Entity();

        // Add data
        entity.AddPosition(new Variable<Vector3>(Vector3.zero));
        entity.AddMoveSpeed(new Const<float>(2f));
        entity.AddMoveDirection(new Variable<Vector3>());

        // Add behaviours
        entity.AddBehaviour(new MoveBehaviour());

        // Activate the entity
        entity.Enable();   // Enable() сам вызывает Init()
    }

    [Test]
    public void Tick_WithZeroDirection_DoesNotMove()
    {
        // Arrange
        entity.GetMoveDirection().Value = Vector3.zero;

        // Act
        const float deltaTime = 1;
        entity.Tick(deltaTime);

        // Assert
        Assert.AreEqual(Vector3.zero, entity.GetPosition().Value);
    }

    [Test]
    public void Tick_WithNonZeroDirection_MovesCorrectly()
    {
        // Arrange
        entity.GetMoveDirection().Value = Vector3.forward;

        // Act
        const float deltaTime = 0.5f;
        entity.Tick(deltaTime);

        // Assert
        Assert.AreEqual(new Vector3(0, 0, 1), entity.GetPosition().Value);
    }
}
```

Ни Play Mode, ни MonoBehaviour, ни сцены: поскольку `MoveBehaviour` принимает `deltaTime` параметром и читает состояние только через атомарные элементы, вся механика конструируется в `[SetUp]`. Нотация `Method_Condition_Expectation`, комментарии Arrange/Act/Assert, `const float deltaTime`, чтобы ожидаемое значение выводилось в голове. Это и есть выплата по всем остальным правилам фреймворка.

В поставляемом туториале здесь опечатка — `entity.GeMoveDirection()` без `t` — и избыточный `entity.Init()` перед `entity.Enable()`.

### 23. Новое требование — новое поведение, старое не трогаем

```csharp
public sealed class KinematicMoveBehaviour : IEntityInit, IEntityFixedTick
{
    private Rigidbody _rigidbody;
    private IVariable<float> _moveSpeed;
    private IVariable<Vector3> _moveDirection;

    public void Init(IEntity entity)
    {
        _rigidbody = entity.GetRigidbody();
        _moveSpeed = entity.GetMoveSpeed();
        _moveDirection = entity.GetMoveDirection();
    }

    public void FixedTick(IEntity entity, float deltaTime)
    {
        Vector3 direction = _moveDirection.Value;
        if (direction == Vector3.zero)
            return;

        float moveStep = _moveSpeed.Value * deltaTime;
        if (_rigidbody.SweepTest(direction, out _, moveStep))
            return;

        Vector3 newPosition = _rigidbody.position + direction * moveStep;
        _rigidbody.MovePosition(newPosition);
    }
}
```

Открыт для расширения через композицию: кинематическое требование приходит как новое поведение плюс подмена данных (`Position` -> `Rigidbody`), а `MoveBehaviour` не тронут, поэтому пуля, которая его переиспользовала, продолжает работать. Стиль охранных условий: два ранних возврата вместо вложенности. `out _` отбрасывает неиспользуемый хит, так что локальной `RaycastHit` не возникает. `_rigidbody.position` читается напрямую, без круга через `transform`; `MovePosition` даёт интерполированное кинематическое движение; `moveStep` вычисляется один раз и переиспользуется и как дистанция свипа, и как смещение.

Две правки против версии в туториале 1, иначе оно не компилируется: класс объявлен `IEntityInit, IEntityTick`, но реализует `FixedTick` — нужен `IEntityFixedTick`; и `entity.Rigidbody()` не соответствует форме генерируемого аксессора — должно быть `entity.GetRigidbody()`.

---

## Производительность

Разделяю то, что **замерено** авторами, то, что **прочитано в исходнике**, и то, что является **выводом**.

### Замерено (`Docs/Entities/Entities/Manual.md#-performance`)

Замеры на MacBook с Apple M1, 1000 элементов на контейнер, медианное время в микросекундах.

Теги против `HashSet<int>`: `Contains` 3.80 против 47.85; `Add` 8.30 против 57.40; `Remove` 5.40 против 50.45; `Enumerator` 2.30 против 29.75. `Clear` медленнее (2.80 против 1.10).

Значения против `Dictionary`: `Get` 4.10 (object) против 7.40. `Get + Cast` — 12.00 для ссылочного, 4.70 для значимого. `Get + Unsafe Cast` — 4.20 / 4.50. **`Add` — 62.15 для ссылочного значения и 178.45 для значимого** при 34.10 у `Dictionary`; `Set` — 62.50 / 187.35 при 37.50. `Enumerator` — 56.80 / 171.75 при 56.60.

Это прямое измерение цены боксинга: значимое значение в три раза дороже на запись и в три раза дороже на обход. Конвенция «аргумент ключа — интерфейс атомарного элемента» оплачивается здесь.

Поведения против `List`: `Contains` 650.60 против 1825.95; `Remove` 243.91 против 312.63; `Get At` 2.30 против 1.60; `Add` 34.30 против 29.30. Врезка в документе: «some operations are **O(n)** in the worst case».

`EntityCollection` против `HashSet` (`Docs/Entities/Collections/Manual.md#-performance`, та же методика): `Add` 18.91 против 77.53; `Contains` 6.00 против 76.96; `Remove` 11.50 против 64.53; `Enumerator` 7.57 против 26.44. `Clear` хуже — 13.24 против 1.09.

Бенчмарки живут в `Entities/Tests/Performance/`: `Collection/EntityCollectionPerformance.cs`, `Entity/EntityPerformance_{Common,Tags,Values,Behaviours}.cs`, `Common/GameObject_Performance.cs`.

### Заявлено авторами

`Docs/Tutorials/7. Summary.md`, таблица сравнения: производительность Atomic — «Above average — optimized entity and world management», против ECS «Very high — especially with a large number of entities» и OOP «Moderate». То есть сами авторы не позиционируют это как SoA/кэш-линейную архитектуру. Пункт «6. High Performance» того же туториала объясняет выигрыш двумя вещами: оптимизированным доступом к данным и тем, что «avoiding heavy MonoBehaviours saves memory and reduces Unity's object management overhead».

### Прочитано в исходнике

**Хранилища — ручные хэш-таблицы над массивами структур, не `Dictionary<,>`.** `Entity_Values.cs`: `internal struct ValueSlot { public int key; public object value; public bool exists; public int next; }` плюс `ValueSlot[] _valueSlots` и `int[] _valueBuckets`. `Entity_Tags.cs`: `internal struct TagSlot { public int key; public int next; public bool exists; }`. `EntityCollection'1.cs`: `private protected struct Slot` плюс `Slot[] _slots`, `int[] _buckets`, `int[] _order`. Поиск читает слот через `ref readonly ValueSlot slot = ref _valueSlots[index]` (без копии) и хэширует как `key & 0x7FFFFFFF`, затем `% _valueCapacity` — одно целочисленное взятие остатка, без вызова компарера и без виртуального `GetHashCode`.

**Тиковые поведения разложены по корзинам в момент добавления.** `Entity_Lifecycle.cs` держит три отдельных массива — `IEntityTick[] _updates`, `IEntityFixedTick[] _fixedUpdates`, `IEntityLateTick[] _lateUpdates` — со своими счётчиками. `Tick()` обходит только `_updates[0.._updateCount]`. Следствие: поведение, которое не реализует `IEntityTick`, стоит в кадре ровно нуль, и никакой проверки `is` в кадре нет. Это вторая половина выигрыша; кэширование в `Init` — первая.

Проверки `is IEntityInit` / `is IEntityDispose` всё же платятся — но по одному проходу по всем поведениям за жизнь сущности, в `Init()` и `Deinitialize()`. Компареры подняты в `static readonly` поля (`s_updateComparer`, `s_fixedUpdateComparer`, `s_lateUpdateComparer`), чтобы не перерешать `EqualityComparer<T>.Default`.

**Поведения — обычный массив с удвоением.** `Array.Resize(ref _behaviours, capacity == 0 ? 1 : capacity * 2)`. `GetBehaviour<T>()`, `HasBehaviour<T>()`, `TryGetBehaviour<T>()`, `DelBehaviour<T>()` — линейные проходы с `is T` на элемент, а `DelBehaviour` ещё делает `Array.Copy` для компактизации. Никогда не разрешайте поведение по типу в горячем пути; разрешайте один раз в `Init`.

**Структурный энумератор есть, и интерфейсные реализации его боксят.**
```csharp
public Enumerator GetEnumerator() => new(this);
IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);
IEnumerator<E> IEnumerable<E>.GetEnumerator() => new Enumerator(this);
public struct Enumerator : IEnumerator<E> { /* Dispose() пустой */ }
```
Обход конкретного типа — без аллокаций; типизация мира как `IEntityCollection<E>` или `IEnumerable<E>` боксит структуру на каждом кадре. Но: **сам фреймворк в горячем пути не перечисляет.** `EntityWorld'1.cs` в `Enable/Disable/Tick/FixedTick/LateTick` индексирует напрямую: `for (int i = 0; i < _count; i++) _slots[_order[i]].value.Tick(deltaTime);`. Риск боксинга существует только в коде, который пишете вы.

**Индексатор лучше энумератора.** `EntityCollection<E>` даёт `public E this[int index]` и `TryGetAt(int, out E)` плюс `CopyTo(ICollection<E>)` / `CopyTo(E[], int)`. Обход по индексу не создаёт энумератора вообще, даже структурного.

**Ёмкости по умолчанию маленькие.** `EntityCollection.DEFAULT_CAPACITY = 3`. Конструктор `Entity` ставит все три ёмкости в 0. `MonoEntity` в инспекторе ставит `initialTagCapacity = 1` и `initialValueCapacity = 1` (`[Min(1)]`), `initialBehaviourCapacity = 0` (`[Min(0)]`). Ёмкости тегов и значений прогоняются через `CeilToPrime`; ёмкость поведений — нет, это длина обычного массива. Первый `Add` каждого вида при таких ёмкостях переаллоцирует (а для тегов и значений ещё и перехэширует). Задавайте явно для пулуемых сущностей и в ассетах `ScriptableEntityFactory` (поля `tagCapacity`/`valueCapacity`/`behaviourCapacity`).

Для **мира** так сделать нельзя: у `EntityWorld<E>` нет конструктора с ёмкостью — все четыре перегрузки уходят в неявный `base()`, то есть в `EntityCollection() : this(DEFAULT_CAPACITY)` с ёмкостью 3. `EntityCollection<E>` сам конструктор `(int capacity)` имеет, но мир его не пробрасывает. Если это станет узким местом, единственный путь — свой наследник.

**`ArrayPool<T>.Shared` для временных буферов.** `private protected static readonly ArrayPool<E> s_arrayPool = ArrayPool<E>.Shared` в `EntityCollection'1.cs` (используется, например, при массовом удалении: `s_arrayPool.Rent(_count)` и `Return`), и `ArrayPool<KeyValuePair<int, object>>.Shared` внутри массовых операций `Entity_Values.cs`.

**`AggressiveInlining` применяется систематически.** Все семь `WhenXxx` плюс `WhenGizmos`, `BindTo`, `InitEntities`/`DisposeEntities`, `AddBehaviour<T>`/`AddBehaviours`, все перегрузки в `Extensions_Values.cs`, `Subscribe`/`Unsubscribe` в `Elements/Scripts/Events/Extensions.cs`, `EntityWorld.OnAdd`/`OnRemove`, `Entity.OnDispose`/`Deinitialize`/`UnsubscribeEvents`, `EntitySystem.OnUpdate`/`OnAddEntity`/`OnRemoveEntity`. Плюс `[EntityExtensionsAPI].AggressiveInlining = true` по умолчанию — генерируемые аксессоры получают атрибут автоматически.

**Хэндлы подписок бесплатны, делегаты — нет.** Все восемь — `public readonly struct ... : IDisposable` (`InitSubscription`, `DisposeSubscription`, `EnableSubscription`, `DisableSubscription`, `TickSubscription`, `FixedTickSubscription`, `LateTickSubscription`, `GizmosSubscription`), держат только источник и делегат. Единственная аллокация — сам `Action`/`Action<float>`. Структура боксится, если сохранить её как `IDisposable`.

**Все ключи — `readonly struct` с одним `int Id`, `GetHashCode() => Id`, типизированным `Equals`.** Но у каждого есть и `public override bool Equals(object obj)`, который **боксит**. Это касается и типизированных вариантов (`TagKey<E>`, `ValueKey<E,T>`) — перегрузка там есть, несмотря на то, что в некоторых описаниях её опускают. Не сравнивайте ключи через неродовую перегрузку в горячем цикле.

**Пул: `Stack` для свободных, `HashSet` для арендованных.** `EntityPool<TEntity,TArgs>`: `private protected readonly Stack<TEntity> _pooledEntities = new();` и `private protected readonly HashSet<TEntity> _rentEntities = new();`. То есть `Rent` стоит одну вставку в хэш-множество, а `Return` сущности, которой пул не выдавал, ничего не делает и пишет `Debug.LogWarning("[EntityPool] Attempted to return untracked entity: …")` — то есть молча это не проходит, но и исключения не бросает. `ExpandMode.ExpandByDoubling` создаёт `_rentEntities.Count` новых сущностей сразу (один всплеск вместо N мелких); `NoExpand` бросает вместо аллокации.

**`EntitySystem<E>` даёт фиксированную стоимость кадра.** Внутри `Dictionary<E,int> _lookup`, плотный `E[] _entities`, `_entityCount` и постоянный `_cursor`. `OnUpdate(batchSize, deltaTime)` обрабатывает `batchSize` сущностей от курсора с заворотом. `Update(float)` после вызова измеряет время и подстраивает батч:
```csharp
_batchSize = frameSize > _settings.frameBudget
    ? Math.Max(_batchSize / _settings.batching.scaleDown, _settings.batching.minSize)
    : Math.Min(_batchSize + _settings.batching.stepUp, _settings.batching.maxSize);
```
`OnRemoveEntity` делает swap-remove по `_lookup`, то есть удаление O(1).

**`EntityFilter<E>` поддерживается инкрементально и переэкспортирует структурный энумератор.** `triggers[i].SetAction(this.Synchronize)` в конструкторе; `Synchronize(E entity)` пересчитывает предикат только для изменившейся сущности; `public EntityCollection<E>.Enumerator GetEnumerator() => this.state.GetEnumerator();`. Обход фильтра — без аллокаций.

**`Entity.LateTick` проверяет `_enabled` на каждой итерации**, в отличие от `Tick` и `FixedTick`:
```csharp
for (int i = 0; i < _lateUpdateCount && _enabled; i++)
    _lateUpdates[i].LateTick(this, deltaTime);
```
Это намеренная асимметрия в `Entity_Lifecycle.cs` (в `EntityWorld'1.cs` все три метода одинаковы). Знать это надо, если поведение отключает сущность посреди тика.

**Мир мутируется под циклом.** `Tick/FixedTick/LateTick` у `EntityWorld` сравнивают `i < _count` с живым полем, а не с закэшированной копией. Идиома Spawn/Despawn из статического UseCase делает ровно это — добавляет и удаляет сущности изнутри тика. Поведение определено (цикл увидит новое `_count`), но порядок обхода при этом меняется; на это нельзя полагаться.

### Выводы (не замерено и не заявлено)

- Косвенность по ссылке на каждый элемент — это цена дизайна «данные как абстракции ссылочного типа». Один `Const<T>`/`Variable<T>` на элемент на сущность означает отдельный объект в куче. Отсюда отдельная рекомендация фреймворка `Docs/BestPractices/SharedConstants.md` — flyweight-константы: одну разделяемую `Const<T>` на много сущностей вместо `new Const<float>(...)` на спавн. Это не SoA и не кэш-линейная раскладка; на «тысячах сущностей» работает не раскладка данных, а `EntitySystem` с бюджетом кадра и `EntityFilter` вместо сканов.
- Событийные виды против опрашивающих: неподвижная сущность с `Observe`-видом стоит нуль, а `LateTick`-вид стоит поиск плюс запись в Transform каждый кадр. Экономия пропорциональна доле неподвижных объектов, и численно её никто не мерил.
- `TryGetComponent(out IGameEntity target)` в Unity-перегрузке UseCase не аллоцирует (в отличие от `GetComponent` плюс проверка на null), и `&&` коротко замыкается до любой работы по урону.

---

## Грабли

- **Unity 6+ — жёсткое требование** (README: «The Atomic Framework requires **Unity 6+**»), а не рекомендация. Все парные интерфейсы (`IEntityInit<in E>`, `IEntityTick<in E>`, `IEntityInstaller<in E>`, `IEntityGizmos<in E>`) мостят к неродовым формам через default interface implementation. Unity 2022.3 это не компилирует. Проверьте целевую версию до начала работ.
- **Настройка DLL генераторов ручная и легко делается неправильно.** Файлы `EntityAPIGenerator.dll`, `EntityAPIAnalyzer.dll`, `EventAPIGenerator.dll`, `EventAPIAnalyzer.dll` должны лежать в `Assets/Plugins/Atomic/SourceGenerators/` (README перечисляет именно эти четыре; в самой папке лежит ещё пятая — `EntityDomainGenerator.dll`); каждой DLL нужен ассет-лейбл `RoslynAnalyzer`; затем снять **Any Platform** и все отдельные платформы, Apply, и перезапустить Unity (или `Assets -> Reimport All`). README специально отмечает, что снятые галочки — это правильно: генераторы и анализаторы работают только во время компиляции и не должны попадать в плеерные сборки.
- **API-класс не `static partial` — сборка падает.** Генератор дописывает вторую половину partial-класса.
- **`EAPI0001`: поле ключа без инициализатора — ошибка сборки**, не предупреждение. Быстрая правка (Ctrl+. / Alt+Enter) вставляет `= new(nameof(Health))`. `EAPI0002` покрывает случай конструктора без параметров. То же для событийных ключей.
- **`[Unsafe(false)]` не скомпилируется.** `Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md` показывает такой вызов, но `UnsafeAttribute` — `sealed class` без конструктора с аргументами и без свойств. Поштучное «обратно в safe» на поле не предусмотрено.
- **`Unsafe = true` применим только к ссылочным значениям.** `GetValueUnsafe<T>` и в интерфейсе, и во всех расширениях ограничен `where T : class` и реализован через `Unsafe.As<object, T>`. Пример из той же документации (`ValueKey<IEntity, int> Health` плюс `ref int healthRef = ref entity.RefHealth()`) под этим ограничением невозможен.
- **`Dispose()` разбирает сущность, а не возвращает её в пул.** Пулуемую сущность нельзя диспозить между использованиями: она потеряет теги, значения, поведения и регистрацию в `EntityRegistry`.
- **`Enable`/`Disable` срабатывают много раз между одним `Init` и одним `Dispose`.** Одноразовую подписку или аллокацию в `IEntityEnable` класть нельзя.
- **`IEntityDispose.Dispose(IEntity entity)` — это не `System.IDisposable.Dispose()`.** У него есть параметр. Класс, реализующий оба, потребует явной дизамбигуации. При этом сам `IEntity` наследует `IDisposable` через `IInitSource`.
- **`Entity.Settings.disposeValues` по умолчанию `true`.** Сущность продиспозит любое своё `IDisposable`-значение. Если она держит flyweight-константы или заимствованные объекты, передавайте `new Settings { disposeValues = false }` — иначе вы продиспозите состояние, на которое ссылаются другие. Тот же переключатель есть как сериализуемое поле `disposeValues` на `MonoEntity`.
- **Каждый `new Entity(...)` регистрируется в глобальном `EntityRegistry.Instance` в конструкторе.** Сущности достижимы (не собираемы) до явного освобождения. Актуально, если вы создаёте и бросаете сущности вместо пулинга.
- **Первый `OnEnable` у `MonoEntity` — no-op.** Он огорожен флагом `_started`, который ставится в конце `Start`, а Unity вызывает `OnEnable` до `Start`. Инициализация и активация происходят в `Start`. `OnDestroy` при `_started == false` пропускает `OnDispose/Deinitialize/DisposeValues`, но всё равно делает `Uninstall()` (под флагом `uninstallOnDestroy`), `ClearTags/ClearValues/ClearBehaviours` и `Unregister()`.
- **`MonoEntitySingleton<E>.Instance` нельзя проверять на null — он бросает.** `new Exception($"Singleton of type {typeof(E).Name} was not found in the scene.")`. В путях разрушения используйте `TryGetInstance(out E)`. Второй экземпляр в сцене: `Awake` логирует ошибку и делает `Destroy(gameObject)`.
- **`MonoEntity.autoCompile` перезапускает `Install` и полный жизненный цикл на каждом `OnValidate` в Edit Mode.** InfoBox самого фреймворка предупреждает: если `Install()` создаёт объекты Unity или другие тяжёлые объекты, флаг обязательно надо выключить.
- **Несовпадение типа у `MonoEntityInstaller<E>` — рантайм-`InvalidCastException`, а не ошибка компиляции.** Неправильный установщик, перетащенный на не ту сущность, падает на `Awake` с длинным диагностическим сообщением, а не в IDE.
- **Если переопределяете `OnValidate` в `MonoEntityInstaller`, вызывайте `base.OnValidate()`** — иначе живой предпросмотр в редакторе перестанет обновляться. У `ScriptableEntityInstaller` этого хука нет вообще.
- **`ScriptableEntityBootstrapper` обязан лежать в папке `Resources/`.** `Initialize()` помечен `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` и находит бутстрапперы через `Resources.LoadAll<ScriptableEntityBootstrapper>(string.Empty)`. Ассет вне `Resources` просто не запустится, без диагностики. Плюс `BootstrapEntities` — `protected virtual async void`: исключение внутри переопределения не наблюдаемо и всплывёт как unhandled-ошибка Unity, а не как падение вызывающего.
- **Порядок в паре спавн/despawn несущий.** `world.Remove(entity)` должен идти до `pool.Return(entity)`. Возврат в пул сущности, которая всё ещё в мире, оставит переиспользуемый объект под тиком.
- **`ExpandMode.ExpandByOne` — значение по умолчанию у всех конструкторов пулов.** То есть по умолчанию пул тихо аллоцирует по одной сущности в кадре при опустошении. Для релиза ставьте `NoExpand`, чтобы недоразмеренный пул падал громко.
- **`EntityWorld<E>` нельзя предразмерить.** Ни один из четырёх его конструкторов не принимает ёмкость и не пробрасывает её в `EntityCollection<E>(int capacity)` — мир всегда стартует с `DEFAULT_CAPACITY = 3`. Для мира на тысячи сущностей это серия перехэширований при наполнении, и из публичного API мира её не избежать.
- **`_batchSize` у `EntitySystemBase<E>` нигде не инициализируется и начинается с 0.** Первый вызов `Update(deltaTime)` обработает ноль сущностей, а дальше батч растёт на `stepUp` (256 по умолчанию) в кадр, пока не дойдёт до `minSize`/`maxSize`. То есть система «разогревается» несколько кадров. Если вам нужна полная пропускная способность с первого кадра, это надо учитывать.
- **Документация по фильтрам противоречит исходнику.** `Docs/Entities/Filters/Manual.md` обещает «Lazy Evaluation» и «Full re-filter when triggers fire». В исходнике фильтр строит состояние немедленно в конструкторе, а `Synchronize` пересчитывает предикат только для одной изменившейся сущности. Полагайтесь на исходник.
- **Чистые C#-пулы и каталоги фабрик не покрыты тестами.** `Entities/Tests/EditMode/Pool/EntityPoolTests.cs`, `Pool/MultiEntityPoolTests.cs`, `Factory/MultiEntityFactoryTests.cs` и `PlayMode/ScriptableEntityFactoryCatalog/ScriptableEntityFactoryCatalogTests.cs` закомментированы целиком. Unity-пулы (`MonoEntityPool`, `PrefabEntityPool`) тесты в Play Mode имеют. Остальное (`Entity`, `EntityCollection`, `EntityWorld`, `EntityFilter`, `EntitySystem`, `KeyStore`, `Registry`) тесты имеют — всего 216 файлов `*.cs` под путями `*Tests*` во всём плагине.
- **`AsConst` не разрешается.** `Elements/Scripts/Values/Extensions.cs`, строка 18: `public static Const<T> AsСonst<T>(this T it) => new(it);` — где «С» это U+0421 CYRILLIC CAPITAL LETTER ES (байты `0xD0 0xA1`), а не ASCII «C». Набрав `AsConst`, вы получите ошибку компиляции без видимой причины. `Observe<T>` в том же файле корректен.
- **`GetBehaviours()`, `GetBehaviours<T>()` и `GetBehaviourEnumerator()` в тике недопустимы.** Первые два аллоцируют новый массив на каждый вызов, третий возвращает боксированный `IEnumerator<IEntityBehaviour>`. Безаллокационные варианты — `BehaviourCount` + `GetBehaviourAt(int)` или `CopyBehaviours(buffer)`.
- **Расширения `WhenInit`/`WhenTick`/… аллоцируют делегат и возвращают хэндл, который туториалы выбрасывают.** Нормально для разовой проводки «один на контекст», неправильно для покадровой логики на сущность (там нужно настоящее `IEntityTick`-поведение), и утечка, если проводка была временной. Для группового освобождения смотрите `Docs/BestPractices/UsingSubscriptionsWithDisposeComposite.md`.
- **`MonoEntityInstallerConfigurable` не разворачивается.** Он переопределяет только `Install`, не `Uninstall`; вложенные `[SerializeReference]` установщики не откатываются.
- **Удаление ключа из API-класса — ломающее изменение без сигнала компилятора.** Это ограничение «Centralized Data» из туториала 7: много поведений ссылаются на один и тот же атомарный элемент, и «Removing such an element from an entity can break the entire project». Ничего не перестанет компилироваться.
- **Код в самих туториалах не компилируется в девяти местах.** Не копируйте их буквально: (1) `KinematicMoveBehaviour : IEntityInit, IEntityTick` реализует `FixedTick` — нужен `IEntityFixedTick`; (2) `entity.Rigidbody()` вместо `entity.GetRigidbody()` в том же фрагменте; (3) `entity.GeMoveDirection()` в `MoveBehaviourTests` — пропущена `t`; (4) `new BaseEvent()` — такого типа нет, есть `Event`; (5) `[SerializeField] private EntityPool _bulletPool;` — неродового `EntityPool` нет, а родовые не `MonoBehaviour`; (6) `MultiEntityPool<string, IGameEntity>` с двумя аргументами не существует; (7) `GameUIInstaller : EntityInstaller<GameUI>` — такой базы нет, нужна `MonoEntityInstaller<GameUI>`; (8) `DealDamageUseCase` — `static class` с инстансным `Execute`, `health - damage` вместо `health.Value - damage`, необъявленное `_eventBus` вместо параметра; (9) `GameEntitiesUseCase.Despawn` присваивает локальную `pool` и игнорирует её, разрешая пул дважды. Плюс туториал 5 ссылается в прозе на «line 37 / line 18 / line 22», которых в файле нет, и обещанный фрагмент поведения пули отсутствует целиком.
- **Туториал 1 после обучения генерируемому API возвращается к строковой форме** (`entity.AddValue("Position", new Variable<Vector3>())`, и даже `entity.AddValue("Damage", 10)` с боксированным `int` вместо элемента) в разделах «Reusing Mechanics» и «Adapting to New Requirements». Строковые перегрузки реально существуют в `Extensions_Values.cs`, но перехэшируют имя при каждом вызове и не дают проверки типов. Читайте эти два фрагмента как иллюстративную прозу.
- **Название поля в инспекторе дрейфовало.** Поле `MonoEntity` называется `sceneInstallers` и несёт `[FormerlySerializedAs("installers")]`. Подпись README «Scene Installers» актуальна; «Mono Installers» из туториала 3 — устаревшая документация на то же поле.
- **«Useful Links» в README устарели.** Стрим, статья на Хабре и статья на Medium все помечены «(прошлая версия)». Не используйте их как руководство по текущему API.
- **`TickableManager` — `internal sealed class`.** Это централизованный `Update`-насос, в который регистрируется `MonoEntity`, но он не часть публичного API: вызвать `TickableManager.Instance` из кода проекта нельзя. Свой аналог для чистых `Entity` придётся написать (это и есть `EntryPoint` из туториала 6).

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Первичные источники этой главы

- **`README.md`** — требования (Unity 6+), три способа установки (исходники / `Atomic.unitypackage` / UPM по Git URL `https://github.com/StarKRE22/Atomic.git?path=Assets/Plugins/Atomic`), полная инструкция по настройке DLL генераторов (лейбл `RoslynAnalyzer`, снять все платформы, перезапуск), и Quick Start из одиннадцати шагов: создание GameObject, добавление компонента Entity, объявление `CharacterAPI`, `MoveBehaviour` на `IEntityInit, IEntityFixedTick`, `CharacterInstaller`, подключение в поле **Scene Installers**, затем `InputBehaviour`. Дальше — полные индексы: API Reference по трём модулям, четыре сэмпл-проекта, Best Practices (архитектура / оптимизация / фичи / расширения), Performance со ссылками на бенчмарки, Useful Links (все помечены «прошлая версия»), MIT-лицензия. **Самый актуальный и самый корректный код во всей документации — именно здесь**, а не в туториалах.
- **`Docs/Tutorials/1. What is Atomic Approach.md`** — постановка задачи, описание паттерна ESB (Entity / State / Behaviour) и затем механика движения в четырёх шагах: данные, логика (`MoveBehaviour` на `IEntityInit, IEntityTick` с `IVariable<Vector3> _position` — вариант, отличный от README), создание сущности (`new Entity("Character")`), управление жизненным циклом (ручные `Init`/`Enable`/цикл с `Tick(0.016f)`/`Disable`/`Dispose`). Далее «Reusing Mechanics» (пуля на том же поведении — и регресс к строковому `AddValue`), «Testing Mechanics» (полный NUnit-класс в Edit Mode) и «Adapting to New Requirements» (`KinematicMoveBehaviour`). Три из девяти некомпилируемых фрагментов живут в этом файле.
- **`Docs/Tutorials/2. Entity API Generation.md`** — пошаговая настройка кодогенерации: проверка DLL, объявление ключей сущности, использование сгенерированного API, объявление событийных ключей, использование сгенерированного событийного API, и отдельный шаг «Fix Missing Initializers» с точным текстом диагностик `EAPI0001` (поле без инициализатора) и `EAPI0002` (конструктор без параметров) и описанием быстрой правки. Здесь же находится некомпилируемый `DealDamageUseCase`.
- **`Docs/Tutorials/3. Creating the Entity in Unity.md`** — тот же сценарий «персонаж», что в README Quick Start, но в восьми шагах с картинками инспектора, плюс раздел «Adding Keyboard Input» в два шага. Единственное содержательное отличие от README — устаревшая подпись поля «Mono Installers» вместо «Scene Installers». Если README уже прочитан, этот файл можно пропустить.
- **`Docs/Tutorials/4. Architectural Consistency.md`** — самый короткий и самый важный для планирования миграции файл. Тезис: контекст, UI и контекст приложения — это тоже сущности, `public class GameContext : MonoEntity { }`. Перечисляет полный набор домена прототипа (`GameEntity`, `GameContext`, `PlayerContext`, `AppContext`, `GameUI`, `MenuUI`) и даёт три установщика: `GameContextInstaller` (с несуществующими `EntityPool` и `BaseEvent`), `GameUIInstaller` (с несуществующей базой `EntityInstaller<GameUI>`) и `AppContextInstaller` (единственный полностью корректный, и лучший пример группировки по фичам).
- **`Docs/Tutorials/5. Interaction Between Entities.md`** — процедурное взаимодействие: `CombatUseCase.DealDamage` как единый алгоритм с сущностями в аргументах, его перегрузка под `Collider` через `TryGetComponent`, и `LeaderboardController` как пример пары подписка-в-`Init` / отписка-в-`Dispose` с кэшированным `ISignal<KillArgs>`. Завершается правилом: «each behaviour should delegate all business logic to static methods placed inside static classes». Проза ссылается на номера строк, которых в файле нет, и обещанный фрагмент поведения пули отсутствует.
- **`Docs/Tutorials/6. Minimizing Unity.md`** — самый объёмный туториал, две половины. «Simulation in C#»: `EntryPoint : MonoBehaviour` целиком, подключение `EntityWorld<IGameEntity>` в установщик контекста, восемь ручных подписок `WhenXxx` (которые заменяются одним `BindTo`), мультипул из каталога фабрик, и пара `Spawn`/`Despawn`. «Visualization in Unity»: `CharacterViewInstaller` с симметричным `Uninstall`, `PositionViewBehaviour` на `Observe` как эталон событийного вида, и описание `EntityWorldView` плюс `EntityViewPool`. Два из девяти некомпилируемых фрагментов здесь — `MultiEntityPool<string, IGameEntity>` (в установщике и в обоих методах UseCase) и двойное разрешение пула в `Despawn`; и нигде не упоминается ни `BindTo`, ни то, как на самом деле конструируется пул.
- **`Docs/Tutorials/7. Summary.md`** — восемь заявленных преимуществ, три честно названных ограничения («Lack of Encapsulation», «Centralized Data», «Hybrid Approach») и таблица сравнения с ECS и OOP по восьми осям. Для решения «идём или не идём» читать нужно именно раздел ограничений: он прямо говорит, что удаление элемента из сущности может сломать проект без сигнала компилятора, и что без дисциплины ESB проект превращается в спагетти. Там же самооценка производительности — «Above average», не «Very high».

### Куда идти после этой главы

Это уже не туториалы, а справочники по модулям. Структура у всех одинаковая: Examples of Usage, API Reference, Notes, Best Practices, иногда Performance.

- **`Docs/Entities/Manual.md`** — корневой справочник модуля `Atomic.Entities` и точка входа в описание кодогенерации (анкер `#-entity-api-source-generation`).
- **`Docs/Entities/Entities/Manual.md`** — четыре примера использования (`Entity`, `MonoEntity`, `MonoEntityProxy`, `MonoEntitySingleton`), полный API Reference и **единственный раздел с настоящими замерами**: три таблицы по тегам, значениям и поведениям против `HashSet`/`Dictionary`/`List`.
- **`Docs/Entities/Behaviours/Manual.md`** — все восемь интерфейсов жизненного цикла, раздел «Lifecycle Events» с порядком фаз.
- **`Docs/Entities/Lifecycle/Manual.md`** — контракты-источники (`IInitSource`, `IEnableSource`, `ITickSource`, `IGizmosSource`), структуры подписок и расширения `WhenXxx`; `Docs/Entities/Lifecycle/Extensions.md` — отдельная страница только по расширениям.
- **`Docs/Entities/Installers/Manual.md`** — все семь типов установщиков, Notes и ссылки на пять best practices по установщикам (модульность, `Uninstall`, `Optional`, `DisposeComposite`, PlayMode/EditMode).
- **`Docs/Entities/Collections/Manual.md`** — `EntityCollection` и `EntityCollection<E>`, интерфейсы, и раздел Performance со таблицей против `HashSet`.
- **`Docs/Entities/Worlds/Manual.md`** — пять примеров: родовой мир, неродовой, Unity-специфичный (`MonoEntityWorld`), авто-сканирование сущностей сцены, синглтонный мир.
- **`Docs/Entities/Filters/Manual.md`** — пять примеров построения фильтров (базовый, с тег-триггером, с value-триггером, цепочка фильтров, с событиями). Раздел Performance Considerations содержит два утверждения, противоречащих исходнику; `Docs/Entities/Filters/EntityTriggers.md` — справочник по шести типам триггеров.
- **`Docs/Entities/Systems/Manual.md`** — три примера: round-robin система, приоритетная система, проводка к контексту. Это самая важная страница для рефакторинга под производительность, потому что в туториалах `EntitySystem` не упоминается вообще.
- **`Docs/Entities/Pooling/Manual.md`** — три примера: `EntityPool`, `MultiEntityPool`, `PrefabEntityPool`. Читать вместе с `Docs/Entities/Factories/Manual.md` (три примера: `InlineEntityFactory`, `ScriptableEntityFactory<E>`, `ScriptableEntityCatalog`) — по отдельности ни одна из двух страниц не даёт рабочей связки.
- **`Docs/Entities/Aspects/Manual.md`** — `Apply`/`Discard` и выбор между `MonoEntityAspect` (в сцене) и `ScriptableEntityAspect` (переиспользуемый ассет).
- **`Docs/Entities/Baking/Manual.md`** — `MonoEntityBaker`, `MonoEntityBaker<E>`, «Bake All», и раздел «What is Baking?» с объяснением, зачем превращать сценовые MonoBehaviour-сущности в чистые `Entity` плюс `EntityView`.
- **`Docs/Entities/UI/Manual.md`** — пять примеров по всему слою визуализации: `EntityView`, `EntityViewCatalog`, `EntityViewPool`, `EntityCollectionView`, `EntityWorldView`.
- **`Docs/Entities/KeyStore/Manual.md`** — как имя превращается в `int`: `SequentialEntityKeyAlgorithm`, использование `TagKey` и `ValueKey`, справочник по `EntityKeyStore` и алгоритмам.
- **`Docs/Entities/Bootstrap/Manual.md`** — создание ассета бутстраппера и настройка фильтра сцен по регулярному выражению.
- **`Docs/Entities/Registry/EntityRegistry.md`** — глобальный реестр, в который регистрируется каждая созданная сущность.
- **`Docs/Entities/Inspector/Manual.md`** — редакторные атрибуты для визуализации данных сущности.
- **`Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md`** — точные имена генерируемых методов (`Has{Name}Tag`, `Add{Name}Tag`, `Del{Name}Tag`, `Get{Name}`, `Set{Name}`, `Add{Name}`, `Has{Name}`, `Del{Name}`, `TryGet{Name}`, `Ref{Name}`), описание `Unsafe` и `AggressiveInlining`. Два примера в этом файле не соответствуют исходнику (`[Unsafe(false)]` и `RefHealth()` на `ValueKey<IEntity,int>`).
- **`Docs/Entities/CodeGen/EntityAPIAnalyzer.md`** — диагностики анализатора; **`Docs/Entities/CodeGen/UnsafeAttribute.md`** — поштучный opt-in; **`Docs/Entities/CodeGen/EntityDomainBehavioursAttribute.md`** — привязка класса поведений к домену.
- **`Docs/Elements/Manual.md`** — корень модуля `Atomic.Elements` с индексом по Values, Variables, Actions, Commands, Functions, Setters, Requests, Events, Time, Collections, Expressions, Utilities.
- **`Docs/Events/Manual.md`**, **`Docs/Events/Keys/Manual.md`** — типизированная шина событий и ключи к ней.
- **Best practices, самые релевантные миграции:** `Docs/BestPractices/ProjectFolderOrganization.md` (раскладка файлов), `PreferAbstractInterfaces.md` (почему аргумент ключа — интерфейс), `SharedConstants.md` (flyweight-константы — прямой ответ на «один `Const<T>` на сущность»), `ModularEntityInstallers.md`, `UninstallEntityInstaller.md`, `UsingSubscriptionsWithDisposeComposite.md` (групповое освобождение хэндлов `WhenXxx`), `EntitySystem.md` (разделение модели и вида), `UsingEntityPoolWithFactories.md` и `OverrideEntityFactoriesWithBakers.md` (связка пул-фабрика-бейкер), и три страницы по оптимизации: `IteratingOverEntity.md`, `IteratingOverEntityCollections.md`, `IteratingReactiveCollections.md`.
- **Бенчмарки в коде:** `Assets/Plugins/Atomic/Entities/Tests/Performance/` — `Collection/EntityCollectionPerformance.cs`, `Entity/EntityPerformance_{Common,Tags,Values,Behaviours}.cs`, `Common/GameObject_Performance.cs`. Последний сравнивает подход с обычными GameObject и полезен для обоснования миграции числами на своём проекте, а не на MacBook автора.
