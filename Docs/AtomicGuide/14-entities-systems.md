# 14. Entities: системы, UI, инспектор

Этот срез фреймворка отвечает за две вещи, которые в старом коде обычно слиты в один `MonoBehaviour`: за **обработку множества сущностей по кадрам** (`EntitySystemBase<E>`, `EntitySystem<E>`, `PriorityEntitySystem<E>`) и за **одностороннюю привязку модели к сцене** (`EntityView<E>`, `EntityViewCatalog<E,V>`, `EntityViewPool<K,E,V>`, `EntityCollectionView<K,E,V>`, `EntityWorldView<K,E,V>`). Системы — обычные C#-классы, они не `MonoBehaviour`, не `IEntityBehaviour` и не живут ни в каком реестре: их `Enable`/`Update`/`Disable`/`Dispose` подписываются на жизненный цикл контекстной сущности тремя extension-методами. Виды — наоборот, целиком Unity-объекты, но без игровой логики: `Activate(entity)` навешивает на сущность набор view-поведений через сериализованные инсталляторы, `Deactivate()` снимает их. Сюда же примыкают два редакторных механизма: Odin-дропдауны (`[EntityInspectorAPI]`, `[EntityInspectorTag]`, `[EntityInspectorValue]`) вместо магических строк в инспекторе и `[EditModeEntityBehaviour]` — опт-ин на прогон `Init`/`Enable` вне Play Mode (`Disable`/`Dispose` до помеченных поведений не доходят никогда, см. «Грабли», пункт 40). Главное свойство всей конструкции: симуляция ни разу не ссылается на объекты сцены, Unity только вычитывает состояние сущностей.

Корень исходников — `C:/!!!Work/_AtomicUpstream/Assets/Plugins/Atomic/Entities/Scripts`; все пути ниже даны относительно него, если не сказано иначе.

---

## Модель

Шесть вещей, которые нужно держать в голове до первой строки кода.

**1. Система — это класс с одним методом, а не объект сцены.** Вся инфраструктура (массив сущностей, словарь индексов, курсор, замер времени, подбор размера батча) живёт в базовом классе. Ваш код — один `Update(E entity, float deltaTime)`. Конструктор принимает `IReadOnlyEntityCollection<E>` и `Settings`, и ничего больше; `EntitySystemBase<E>` реализует только `IDisposable` и не наследует ни `MonoBehaviour`, ни `IEntityBehaviour`.

**2. Система не обрабатывает всех за кадр — и это не баг.** `EntitySystemBase<E>.Update(float)` замеряет время своего `OnUpdate` через `Stopwatch` и после каждого кадра пересчитывает `_batchSize`:

```csharp
_batchSize = frameSize > _settings.frameBudget
    ? Math.Max(_batchSize / _settings.batching.scaleDown, _settings.batching.minSize)
    : Math.Min(_batchSize + _settings.batching.stepUp, _settings.batching.maxSize);
```
(`Systems/EntitySystemBase.cs:187-189`)

`_batchSize` стартует с нуля. Значит: первый `Update` не обрабатывает **ничего**, второй — `stepUp` сущностей, и так далее. С дефолтами (`minSize = 1024`, `stepUp = 256`) системе нужно 4 вызова `Update`, чтобы `_batchSize` дошёл до `minSize`. `minSize` — это пол **только после просадки**, вверх он не подтягивает. Это подтверждено хелпером в тестах фреймворка (`Tests/EditMode/System/EntitySystemTests.cs:86-94`): «The first Update call always starts with batchSize=0 (processes nothing) and ramps the batch size to stepUp».

Практический вывод: логика в `Update(entity, dt)` обязана быть устойчивой к тому, что её вызвали для этой сущности не в прошлом кадре, а три кадра назад, — поэтому `deltaTime` передаётся явно и его нельзя интерпретировать как «время с прошлого обновления этой сущности». Он равен `deltaTime` контекста.

**3. Источник для системы — любой `IReadOnlyEntityCollection<E>`.** Система не умеет фильтровать. Если нужно обрабатывать не весь мир, подайте ей не `EntityWorld<E>`, а `EntityFilter<E>` — он сам `IReadOnlyEntityCollection<E>` и сам поддерживает своё состояние по триггерам. Фильтрация — это выбор источника, а не `if` внутри `Update`.

**4. Приоритет в `PriorityEntitySystem<E>` — это доля бюджета, а не порядок.** XML-док на `EntityUpdatePriority` утверждает «Systems with higher priority are typically updated before lower-priority systems» (`Systems/EntityUpdatePriority.cs:3-5`) — это неверно. В коде `highPercent`/`midPercent` делят `batchSize` между тремя массивами сущностей, неиспользованная квота перетекает вниз (`Systems/PriorityEntitySystem.cs:341-394`). Никакого межсистемного упорядочивания здесь нет вообще.

**5. Вид — это односторонний мост, и у него ровно два глагола.** `Activate(E entity)` и `Deactivate()`. `Activate` сначала вызывает `Deactivate()`, потом присваивает сущность, потом переименовывает `GameObject`, потом `OnActivate`, потом прогоняет инсталляторы. `Deactivate` делает обратное и выходит сразу, если сущности нет. Оба идемпотентны — именно поэтому их безопасно дёргать из пула. Вид не содержит логики: всё, что он делает, — через `List<MonoEntityInstaller>` навешивает на сущность поведения, которые читают её значения и пишут в `Transform`/`Renderer`/`Animator`.

**6. Точка соединения двух половин — коллекция.** `IEntityWorld<E> : IEntityCollection<E>, IEnableSource, ITickSource`, а `IEntityCollection<E> : IReadOnlyEntityCollection<E>, ICollection<E>, IDisposable` — то есть мир годится и как источник для системы, и как объект, который можно подписать на жизненный цикл. Один и тот же мир отдаётся и в конструктор системы, и в `EntityWorldView.Activate(...)`. Код спавна трогает только модель; виды появляются и исчезают сами, потому что `EntityWorldView` подписан на `OnAdded`/`OnRemoved`.

Жизненный цикл в одном абзаце: инсталлятор контекста создаёт `EntityWorld<E>` → `world.BindTo(context)` вешает восемь методов мира на `When*`-хуки контекстной сущности → конструируются системы над этим миром → `context.AddTickSystem(system)` подписывает их `Enable/Update/Disable/Dispose` → на стороне сцены `EntityWorldView` активируется этим же миром и ведёт виды → каждый вид на `Activate` навешивает view-поведения на сущность и снимает их на `Deactivate`, а пул переиспользует `GameObject`-ы.

---

## API

### Системы

```csharp
public abstract class EntitySystemBase<E> : IDisposable
    where E : IEntity
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

    private protected readonly IReadOnlyEntityCollection<E> _source;

    protected EntitySystemBase(IReadOnlyEntityCollection<E> source, Settings settings);

    public void Enable();
    public void Disable();
    public void Update(float deltaTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnEnable() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnDisable() { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnAddEntity(E entity) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnRemoveEntity(E entity) { }

    protected abstract void OnUpdate(int batchSize, float deltaTime);
    public abstract void Dispose();
}
```
`Systems/EntitySystemBase.cs`. Берите, когда нужно своё хранилище или хуки на добавление/удаление: в `EntitySystem<E>` они `sealed`. Ограничение — `where E : IEntity` (не `class, IEntity`), struct-сущности формально допустимы; UI-слой их не примет. `_source` — `private protected readonly`, то есть доступен только коду внутри сборки `Atomic.Entities` (`Entities/Scripts/Atomic.Entities.asmdef`): `private protected` означает «своя сборка **и** наследник», а не «или». Наследник из игровой сборки `_source` **не видит** — ссылку на источник ему надо запомнить самому (идиома 10). Конструктор бросает `ArgumentNullException` на `null` любого из аргументов. `Enable()`/`Disable()` идемпотентны через флаг `_enabled`. Сам класс **не** `[Serializable]`.

Порядок внутри `Enable()` важен и неочевиден (`EntitySystemBase.cs:114-128`): сначала `OnAddEntity` для каждой сущности из `_source`, затем подписка на события коллекции, и **только потом** `OnEnable()`. `Disable()` — зеркально: `OnDisable()`, отписка, затем `OnRemoveEntity` для всех, кто ещё лежит в `_source`.

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
`Systems/EntitySystem.cs`. Это базовый класс для 95% систем. Round-robin по плотному `E[]` с курсором, который заворачивается; `batchSize` зажимается по `count`, так что сущность не обработается дважды за кадр. Внутри — `Dictionary<E,int> _lookup` (ёмкость `source.Count`) и `E[] _entities` (ёмкость `Math.Max(4, source.Count)`), рост — `Array.Resize` с удвоением, удаление — swap-with-last. Три хука `sealed`, расширять нечего — только `Update`.

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
    ) : base(source, settings);

    protected abstract EntityUpdatePriority EvaluatePriority(E entity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected abstract void Update(E entity, float deltaTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected void ChangePriority(E entity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected void RecalculatePriorities();

    protected override void OnEnable();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected sealed override void OnUpdate(int batchSize, float deltaTime);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected sealed override void OnAddEntity(E entity);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected sealed override void OnRemoveEntity(E entity);
    public override void Dispose();
}
```
`Systems/PriorityEntitySystem.cs`. **Единственный** конструктор — вот этот, трёхаргументный. Параметрless-конструктора в базе нет, поэтому наследник обязан объявить свой конструктор и пробросить аргументы, иначе класс не скомпилируется.

`Settings` объявлен как `public new class Settings : EntitySystemBase<E>.Settings` — это **скрывающий** вложенный тип. Внутри наследника `Settings` без квалификации разрешается именно в приоритетный; поле, объявленное как базовый `EntitySystemBase<E>.Settings`, в этот конструктор не пройдёт.

Три плотных массива (`internal E[] _highEntities/_midEntities/_lowEntities`, стартовая ёмкость `Math.Max(16, source.Count)`), три курсора, три счётчика (`internal int _highEntityCount/...`). Повторный вход обслуживается буфером команд: пока `_isUpdating == true`, мутации хранилища складываются в `List<Command>` (предразмер 64) и применяются в `UpdateCommands()` после цикла. `cooldown <= 0` полностью отключает периодический пересчёт.

```csharp
public enum EntityUpdatePriority : byte
{
    Low = 0,
    Medium = 1,
    High = 2
}
```
`Systems/EntityUpdatePriority.cs`. Возвращается из `EvaluatePriority`. Подложка `byte` держит `Entry` и `Command` маленькими. `default` — `Low`, и во всех `switch` стоит `case EntityUpdatePriority.Low: default:` — значение вне диапазона молча уедет в Low.

```csharp
public static partial class Extensions
{
    public static void AddTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
        where TContext : IEntity where TEntity : IEntity;

    public static void AddFixedTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
        where TContext : IEntity where TEntity : IEntity;

    public static void AddLateTickSystem<TContext, TEntity>(this TContext entity, EntitySystemBase<TEntity> system)
        where TContext : IEntity where TEntity : IEntity;
}
```
`Systems/Extensions.cs`. Весь механизм запуска систем — это он. Тело `AddTickSystem` целиком:

```csharp
entity.WhenEnable(system.Enable);
entity.WhenTick(system.Update);
entity.WhenDisable(system.Disable);
entity.WhenDispose(system.Dispose);
```

Фазу выбирает вызывающий (`WhenTick` / `WhenFixedTick` / `WhenLateTick`). Система **не** становится `IEntityBehaviour`: её не найти через `GetBehaviour<T>()` и не снять через `DelBehaviour`. Возвращаемые `When*`-подписки отбрасываются, так что отписаться через этот путь нельзя.

```csharp
public interface IEntityTrigger<E> where E : IEntity
{
    void SetAction(Action<E> action);
    void Track(E entity);
    void Untrack(E entity);
}
```
``Triggers/IEntityTrigger`1.cs``. Реактивный путь `PriorityEntitySystem<E>`: `OnEnable` зовёт `trigger.SetAction(this.ChangePriority)`, `OnAddEntity`/`OnRemoveEntity` — `trigger.Track`/`trigger.Untrack`. В комплекте шесть реализаций (`Triggers/`), у каждой есть generic- и не-generic-форма:

```csharp
public class ValueEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public ValueEntityTrigger(bool added = true, bool deleted = true, bool changed = true);
}

public class TagEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public TagEntityTrigger(bool added = true, bool deleted = true);
}

public class InlineEntityTrigger<E> : IEntityTrigger<E> where E : IEntity
{
    public InlineEntityTrigger(Action<E, Action<E>> track, Action<E, Action<E>> untrack);
}
```
Плюс `StateChangedEntityTrigger<E>`, `BehaviourEntityTrigger<E>`, `SubscriptionEntityTrigger<E>`. Выбор по делу: приоритет зависит от тега — `TagEntityTrigger`; от конкретного значения — `ValueEntityTrigger` с явно выключенными ненужными флагами; от чего-то своего — `InlineEntityTrigger` с двумя лямбдами подписки/отписки.

```csharp
public class EntityFilter<E> : IReadOnlyEntityCollection<E>, IDisposable where E : IEntity
{
    public EntityFilter(
        IReadOnlyEntityCollection<E> source,
        Predicate<E> predicate,
        params IEntityTrigger<E>[] triggers
    );

    public EntityCollection<E>.Enumerator GetEnumerator();
}
```
``Filters/EntityFilter`1.cs``. Формально не из этой главы, но это правильный `source` для системы, которой нужен подмножество мира.

### Представления

```csharp
[HelpURL("...")]
public abstract partial class EntityView<E> : MonoBehaviour where E : class, IEntity
{
    private const string NAME_FORMAT = "{0}:{1}";

    [Header("Installing")]
    [SerializeField] internal List<MonoEntityInstaller> installers;

    public E Entity => _entity;
    public bool IsActive => _entity != null;

    public void Activate(E entity);
    public void Deactivate();

    protected virtual string FormateName(E entity) =>
        string.Format(NAME_FORMAT, entity.Name, entity.InstanceID);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnActivate(E entity) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnDeactivate(E entity) { }
}
```
``UI/View/EntityView`1.cs``, весь файл под `#if UNITY_5_3_OR_NEWER`. Ограничение `where E : class, IEntity` — общее для **всего** UI-слоя. `installers` объявлено `internal`: из игровой сборки список недоступен из кода, он заполняется только в инспекторе. `FormateName` (опечатка в самом API) — `protected virtual`, переопределять можно откуда угодно; это единственная точка настройки имени `GameObject`.

Гизмо-часть вынесена в партиал под `#if UNITY_EDITOR` (``UI/View/EntityView`1_Gizmos.cs``):

```csharp
public partial class EntityView<E>
{
    [Header("Gizmos")]
    [SerializeField] private bool _onlySelectedGizmos;
    [SerializeField] private bool _onlyEditModeGizmos;

    private protected virtual void OnDrawGizmos();
    private protected virtual void OnDrawGizmosSelected();
    internal void OnGizmosDraw();
}
```
Оба метода — `private protected virtual`. `private protected` означает «своя сборка **и** наследник» (в отличие от `protected internal` — «или»). Ваш `UnitView : EntityView<IUnitEntity>` живёт в другой asmdef, поэтому **переопределить их из игрового кода нельзя**. Если объявить в наследнике свой `OnDrawGizmos`, он не переопределит базовый, а скроет его. Правильный путь — `IEntityGizmos<E>` на поведении либо собственный `OnDrawGizmos` + `entity.DrawGizmos()` (см. «Идиомы»).

```csharp
[AddComponentMenu("Atomic/Entities/Entity View")]
[DisallowMultipleComponent]
public class EntityView : EntityView<IEntity>
{
    [field: SerializeField]
    public string Name { get; private set; }
}
```
`UI/View/EntityView.cs`. **`Name` объявлен только здесь, в не-generic обёртке.** У `EntityView<E>` его нет. Это ровно та причина, по которой типизированный пул не получает `GetKey` бесплатно.

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GetValue<E, T>(this EntityView<E> view, ValueKey<E, T> key) where E : class, IEntity =>
        view.Entity.GetValue<T>(key.Id);
}
```
`UI/View/Extensions.cs`. Типизированное чтение значения прямо с вида. `ValueKey<E,T>` — `readonly struct` с `public readonly int Id` (`KeyStore/Keys/ValueKey'1.cs`), поэтому это поиск по `int` без хеширования строки и без каста на месте вызова. Именно эту форму должны использовать view-поведения.

```csharp
[HelpURL("...")]
public abstract class EntityViewCatalog<E, V> : ScriptableObject
    where E : class, IEntity
    where V : EntityView<E>
{
    [SerializeField] private List<V> prefabs;

    public int Count => this.prefabs.Count;
    public V GetPrefab(int index) => prefabs[index];
}
```
``UI/Catalog/EntityViewCatalog`1.cs``. Сознательно «глупый» ассет: только индексный доступ, никакого поиска по имени. Ключевание — забота пула (`GetKey(V)`), поэтому один и тот же каталог может ключеваться по-разному разными пулами. `GetPrefab` не проверяет границы — бросит `ArgumentOutOfRangeException` от `List<V>`, а не `KeyNotFoundException`. У generic-базы **нет** `[CreateAssetMenu]`; он есть только у не-generic `EntityViewCatalog` (`Atomic/Entities/EntityViewCatalog`), так что типизированному каталогу атрибут надо добавить самому.

```csharp
public abstract class EntityViewPool<K, E, V> : MonoBehaviour
    where E : class, IEntity
    where V : EntityView<E>
{
    private static readonly ProfilerMarker s_rentMarker = new($"EntityViewPool<{typeof(E).Name}>.Rent");
    private static readonly ProfilerMarker s_returnMarker = new($"EntityViewPool<{typeof(E).Name}>.Return");

    [SerializeField] private Transform container;
    [SerializeField] private int initialCapacity = 32;
    [SerializeField] private EntityViewCatalog<E, V>[] catalogs;

    private protected virtual void Awake();               // => RegisterCatalogs()

    public void Register(K key, V prefab);                // _prefabs.Add
    public void Unregister(K key);                        // _prefabs.Remove
    public void Register(EntityViewCatalog<E, V> catalog);
    public void Unregister(EntityViewCatalog<E, V> catalog);
    public void Clear();
    public async ValueTask InitAsync(K key, int count);
    public void Init(K key, int count);

    internal V Rent(K key, Transform parent);
    internal void Return(V view);

    protected abstract K GetKey(V view);
}
```
``UI/Pool/EntityViewPool`1.cs``. `Rent`/`Return` — `internal`: из вашей сборки их не вызвать, их дёргает `EntityCollectionView`. `Awake` — `private protected virtual`, то есть из игровой сборки **не переопределяется**; объявленный в наследнике `Awake` скроет базовый и `RegisterCatalogs()` молча не выполнится. `Register(K,V)` использует `Dictionary.Add` и бросает `ArgumentException` на дубль ключа, а `Unregister(K)` — `Remove`, который на промах молчит. `Init` и `InitAsync` оба бросают `KeyNotFoundException`, если ключ не зарегистрирован, и оба предразмеривают стек: `Init` — `GetOrCreatePool(key, Mathf.Max(count, this.initialCapacity))`, `InitAsync` — то же, но по фактической длине возвращённого массива: `GetOrCreatePool(key, Mathf.Max(viewCount, this.initialCapacity))`.

```csharp
public class EntityViewPool : EntityViewPool<string, IEntity, EntityView>
{
    protected override string GetKey(EntityView view) => view.Name;
}
```
`UI/Pool/EntityViewPool.cs`. Вся конкретная реализация — одно выражение. Это и есть предполагаемый размер конкретного пула. `[AddComponentMenu]`/`[HelpURL]` на нём нет.

```csharp
public abstract class EntityCollectionView<K, E, V> : MonoBehaviour, IReadOnlyCollection<KeyValuePair<E, V>>
    where E : class, IEntity
    where V : EntityView<E>
{
    public event Action<E, V> OnAdded;
    public event Action<E, V> OnRemoved;

    public int Count => _views.Count;
    internal EntityViewPool<K, E, V> Pool => _pool;

    [SerializeField] private Transform viewport;
    [Space, SerializeField] private EntityViewPool<K, E, V> _pool;

    public V Get(E entity) => _views[entity];
    public bool TryGet(E entity, out V view) => _views.TryGetValue(entity, out view);
    public bool Contains(E entity) => _views.ContainsKey(entity);

    public V Add(E entity);
    public void Remove(E entity);
    public void Remove(V view) => this.Remove(view.Entity);
    public void Clear();

    protected abstract K GetKey(E entity);

    public Dictionary<E, V>.Enumerator GetEnumerator() => _views.GetEnumerator();
    IEnumerator<KeyValuePair<E, V>> IEnumerable<KeyValuePair<E, V>>.GetEnumerator() => _views.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _views.GetEnumerator();
}
```
``UI/Collection/EntityCollectionView`1.cs``. Ручная карта «сущность → вид». `Add` идемпотентен (вернёт уже существующий вид). Порядок событий намеренно асимметричен: на добавлении `view.Activate(entity)` → `OnAdded`; на удалении `OnRemoved` → `view.Deactivate()` → `_pool.Return(view)`, то есть обработчик `OnRemoved` ещё видит живой вид с непустым `Entity`. `GetEnumerator()` возвращает конкретную `Dictionary<E,V>.Enumerator` (структура) из публичного не-интерфейсного метода — `foreach` по компоненту не аллоцирует.

```csharp
[HelpURL("...")]
public abstract class EntityWorldView<K, E, V> : EntityCollectionView<K, E, V>
    where E : class, IEntity
    where V : EntityView<E>
{
    public bool IsActive => _source != null;

    public void Activate(IReadOnlyEntityCollection<E> source);
    public void Deactivate();
}
```
``UI/World/EntityWorldView`1.cs``. Автосинхронная версия. Тело `Activate`: `Deactivate()`; `_source = source ?? throw new ArgumentNullException(...)`; `_source.OnAdded += this.OnAdd; _source.OnRemoved += this.Remove;`; затем `foreach (E entity in _source) this.Add(entity);`. `OnRemoved` подписывается прямо на перегрузку `Remove(E)` (она совпадает с `Action<E>`), а добавление идёт через приватную обёртку `OnAdd` только потому, что `Add` возвращает `V`. `Deactivate` сначала `Clear()`, затем отписка и обнуление источника — вызывать повторно безопасно. **Ни `OnDisable`, ни `OnDestroy` его не вызывают.**

```csharp
public abstract class EntityWorldViewSingleton<K, E, V> : EntityWorldView<K, E, V>
    where E : class, IEntity
    where V : EntityView<E>
{
    [SerializeField] private bool dontDestroyOnLoad;

    public static EntityWorldViewSingleton<K, E, V> Instance { get; }   // FindAnyObjectByType, иначе throw
    public static bool TryGetInstance(out EntityWorldViewSingleton<K, E, V> instance);

    private void Awake();                              // InitializeSingleton()
    private protected virtual void OnDestroy();
}
```
``UI/World/EntityWorldViewSingleton`1.cs``. Разрешение — `FindAnyObjectByType<...>(FindObjectsInactive.Exclude)`: неактивный `GameObject` не будет найден, а `Instance` на промахе бросает голый `Exception`. Статика лежит на **закрытом** generic-типе, и `Instance` типизирован как базовый закрытый generic — потребителю придётся кастовать к своему классу. `Awake` — `private void`, не виртуальный: собственный `Awake` в наследнике скроет его и регистрация синглтона не произойдёт, а исправить переопределением нельзя. Класс абстрактный и `GetKey` не реализует.

Не-generic `EntityWorldViewSingleton` (`UI/World/EntityWorldViewSingleton.cs`) наследуется **не** от generic-синглтона, а напрямую от `EntityWorldView<string, IEntity, EntityView>`; код синглтона скопирован целиком, `_instance` у них разные. Его `OnDestroy` — `protected virtual` (в отличие от `private protected` у generic-версии).

### Инсталляторы

```csharp
public interface IEntityInstaller
{
    void Install(IEntity entity);
}

public interface IEntityInstaller<in E> : IEntityInstaller where E : IEntity
{
    void Install(E entity);

    void IEntityInstaller.Install(IEntity entity) { /* is not E -> InvalidCastException */ }
}
```
`Installers/IEntityInstaller.cs`, ``Installers/IEntityInstaller`1.cs``. Обратите внимание: **в интерфейсе нет `Uninstall`.** Он появляется только на Unity-базах ниже. Не-generic член реализован как default interface method с одной проверкой `is not E e` и подробным сообщением об ошибке.

```csharp
[HelpURL("...")]
public abstract class MonoEntityInstaller : MonoBehaviour, IEntityInstaller
{
#if UNITY_EDITOR
    internal Action onValidateCallback;
#endif
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
    protected virtual void OnValidate();
}

[HelpURL("...")]
public abstract class MonoEntityInstaller<E> : MonoEntityInstaller, IEntityInstaller<E> where E : class, IEntity
{
    public sealed override void Install(IEntity entity);    // каст или InvalidCastException
    public sealed override void Uninstall(IEntity entity);  // каст или InvalidCastException

    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}
```
`Installers/MonoEntityInstaller.cs`, ``Installers/MonoEntityInstaller`1.cs``. Для видов используйте **generic-форму**: один `is`-тест на активацию, ноль кастов в вашем коде. `Uninstall` — виртуальный с пустым телом; вид зовёт его на каждом `Deactivate()`, так что для пулящегося вида переопределять обязательно. `OnValidate` дёргает `onValidateCallback` только когда не играем и не компилируемся, всё внутри `try/catch` со проглатыванием — это то, что обновляет edit-mode превью.

```csharp
public sealed class MonoEntityInstallerConfigurable : MonoEntityInstaller
{
    [SerializeReference] private IEntityInstaller[] _installers;

    public override void Install(IEntity entity);
}
```
`Installers/MonoEntityInstallerConfigurable.cs`. Единственный конкретный `MonoEntityInstaller` в комплекте: через `[SerializeReference]` позволяет собрать в инспекторе массив обычных `[Serializable]`-классов `IEntityInstaller` без отдельного `MonoBehaviour` на каждый. Цена: он **не переопределяет `Uninstall`** и класс `sealed`, так что починить наследованием нельзя, — и чинить нечем, потому что в `IEntityInstaller` метода `Uninstall` не существует. Для пулящегося вида он означает гарантированную утечку поведений на переиспользуемую сущность.

```csharp
[HelpURL("...")]
public abstract class ScriptableEntityInstaller : ScriptableObject, IEntityInstaller
{
    public abstract void Install(IEntity entity);
    public virtual void Uninstall(IEntity entity) { }
}

[HelpURL("...")]
public abstract class ScriptableEntityInstaller<E> : ScriptableEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
{
    public sealed override void Install(IEntity entity);
    public sealed override void Uninstall(IEntity entity);

    public abstract void Install(E entity);
    public virtual void Uninstall(E entity) { }
}
```
`Installers/ScriptableEntityInstaller.cs`, ``ScriptableEntityInstaller`1.cs``. Ассетная форма инсталлятора — разделяемая альтернатива инсталлятору-на-префаб. В `EntityView<E>.installers` она не поместится (там `List<MonoEntityInstaller>`), зато `MonoEntity` принимает отдельный `scriptableInstallers`.

### Гизмо

```csharp
public interface IEntityGizmos : IEntityBehaviour
{
    void DrawGizmos(IEntity entity);
}

public interface IEntityGizmos<in E> : IEntityGizmos where E : IEntity
{
    void DrawGizmos(E entity);

    void IEntityGizmos.DrawGizmos(IEntity entity) { /* is not E -> InvalidCastException */ }
}
```
`Behaviours/IEntityGizmos.cs`. Файл под `#if UNITY_5_3_OR_NEWER`, **не** под `#if UNITY_EDITOR`: интерфейс и его реализации попадают в плеерную сборку. Реализуйте generic-форму — не-generic член приходит default interface method'ом, адаптер не нужен. Диспетчер на стороне вида (`EntityView<E>.OnGizmosDraw`) проверяет именно не-generic `is IEntityGizmos`.

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DrawGizmos(this IEntity entity)
    {
        for (int i = 0; i < entity.BehaviourCount; i++)
            if (entity.GetBehaviourAt(i) is IEntityGizmos gizmos)
                gizmos.DrawGizmos(entity);
    }
}
```
`Entities/Extensions.cs:209-215`. Публичный и ничем не загорожен — это ваш способ вызвать гизмо-диспетчер из собственного `OnDrawGizmos`, когда `private protected` базы мешает.

```csharp
#if UNITY_EDITOR
public interface IGizmosSource
{
    event Action OnGizmosDraw;
}

public static void WhenGizmos(this IGizmosSource source, Action action) => source.OnGizmosDraw += action;
public readonly struct GizmosSubscription : IDisposable { /* ... */ }
#endif
```
`Lifecycle/Sources/IGizmosSource.cs`, `Lifecycle/Extensions.cs:84`, `Lifecycle/Subscriptions/GizmosSubscription.cs`. Подписочный аналог `When*`-хуков. Важная оговорка: `IGizmosSource` реализует **только `MonoEntity`** (`Entities/Unity/MonoEntity_Gizmos.cs:12`), а `EntityView<E>` — нет; у вида `OnGizmosDraw` это `internal void`-метод, а не событие. То есть `WhenGizmos` применим к сценовой сущности, а не к виду.

### Прокси

```csharp
[HelpURL("...")]
public abstract class MonoEntityProxy<E> : MonoBehaviour, IEntity where E : MonoEntity
{
    [SerializeField] private E _source;
    public E Source { get => _source; set => _source = value; }

    public string Name { get => _source.Name; set => _source.Name = value; }
    int IEntity.InstanceID { get; set; }
    public bool IsValid => _source.IsValid;

    public override bool Equals(object obj) => obj is IEntity entity && _source.InstanceID == entity.InstanceID;
    public override int GetHashCode() => _source.GetHashCode();
    private void Reset() => _source = this.GetComponentInParent<E>();

    // и далее — проброс ВСЕГО IEntity, включая:
    public void Init()   => _source.Init();
    public void Enable() => _source.Enable();
    public void Disable()=> _source.Disable();
    public void Dispose()=> _source.Dispose();
}
```
``Entities/Unity/MonoEntityProxy`1.cs`` (весь файл под `#if UNITY_5_3_OR_NEWER`). Файлы лежат в `Entities/Unity/`, папки `Scripts/Proxy` в этой версии нет. Ограничение — `where E : MonoEntity` (конкретный класс, не интерфейс). Проброшено буквально всё: теги, значения, поведения, все семь событий жизненного цикла как `add`/`remove`-форвардеры, `Initialized`/`Enabled`, `Tick`/`FixedTick`/`LateTick`, `GetTags/CopyTags/GetTagEnumerator`, `GetValues/CopyValues/GetValueEnumerator`, `CopyBehaviours`, `TryGetValueUnsafe<T>`. Самый опасный форвардер — `Dispose()`: вызов `Dispose` на прокси уничтожает реальную `MonoEntity`, и попасть в него легко случайно через `using` или через код, который обрабатывает `IDisposable` вообще. `Equals` сравнивает по `InstanceID`, поэтому прокси и его источник взаимозаменяемы как ключи словаря, — но `_source` разыменовывается без проверки на `null`.

```csharp
[AddComponentMenu("Atomic/Entities/Entity Proxy")]
[DisallowMultipleComponent]
public class MonoEntityProxy : MonoEntityProxy<MonoEntity> { }
```

```csharp
public static bool TryGetEntity(this GameObject gameObject, out IEntity entity);
public static bool TryGetEntity(this Component component, out IEntity entity);
public static bool TryGetEntity(this Collision2D collision2D, out IEntity entity);
public static bool TryGetEntity(this Collision collision, out IEntity entity);

public static bool FindEntityInParent(this GameObject gameObject, out IEntity entity);
public static bool FindEntityInParent(this Component component, out IEntity entity);
public static bool FindEntityInParent(this Collision2D collision2D, out IEntity entity);
public static bool FindEntityInParent(this Collision collision, out IEntity entity);
```
`Entities/Extensions.cs:134-205`, все под `#if UNITY_5_3_OR_NEWER`. Перегрузки для `Collider`/`Collider2D` отдельно не объявлены — они резолвятся через `Component`. `TryGetEntity` — это `TryGetComponent` на том же объекте (нужен прокси на каждом коллайдере); `FindEntityInParent` — `GetComponentInParent<IEntity>()`, то есть обход вверх по иерархии без прокси. Выбор между ними — это выбор «память на прокси» против «обход в момент попадания».

### Ключи и расширения, без которых идиомы не компилируются

`IEntity` сам по себе — **только `int`-API**. Строковые и ключевые формы — extension-методы:

```csharp
// Entities/Extensions_Tags.cs
public static bool AddTag(this IEntity entity, string tag);
public static bool AddTag(this IEntity entity, TagKey tag);
public static bool AddTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static bool HasTag(this IEntity entity, string tag);
public static bool HasTag<E>(this E entity, TagKey<E> tag) where E : IEntity;
public static bool HasAllTags<E>(this E entity, params TagKey<E>[] tags) where E : IEntity;
public static bool HasAnyTag<E>(this E entity, params TagKey<E>[] tags) where E : IEntity;

// Entities/Extensions_Values.cs
public static void AddValue(this IEntity entity, string key, object value);
public static void AddValue<E, T>(this E entity, ValueKey<E, T> key, T value) where E : IEntity;
public static T GetValue<T>(this IEntity entity, string key);
public static T GetValue<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity;
public static ref T GetValueUnsafe<T>(this IEntity entity, ValueKey<T> key) where T : class;
public static T GetValueUnsafe<E, T>(this E entity, ValueKey<E, T> key) where E : IEntity where T : class;
```
Каждая строковая форма проходит через `EntityKeyStore.NameToId(string)` — поиск в `Dictionary<string,int>`, то есть хеш строки на каждый вызов (`KeyStore/EntityKeyStore.cs:40`). Заметьте несимметричность: `GetValueUnsafe` в форме `<T>` возвращает `ref T`, а в типизированной форме `<E,T>` — просто `T`, теряя `ref`.

```csharp
public readonly struct ValueKey<E, T> : IEquatable<ValueKey<E, T>> where E : IEntity
{
    public readonly int Id;
    public ValueKey(string name) => Id = EntityKeyStore.NameToId(name);
    public ValueKey(int id) => this.Id = id;
}
```
Аналогично `ValueKey<T>`, `TagKey`, `TagKey<E>` (`KeyStore/Keys/`).

### Инспектор

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class EntityInspectorAPIAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class EntityInspectorTagAttribute : Attribute
{
    public readonly Type entityType;
    public EntityInspectorTagAttribute(Type entityType) => this.entityType = entityType;
    public EntityInspectorTagAttribute() => this.entityType = typeof(IEntity);
}

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class EntityInspectorValueAttribute : Attribute
{
    public readonly Type entityType;
    public readonly Type valueType;

    public EntityInspectorValueAttribute(Type valueType);                  // entityType = typeof(IEntity)
    public EntityInspectorValueAttribute(Type entityType, Type valueType);
}
```
`Inspector/`. Все три — пустые/почти пустые маркеры, вне `#if UNITY_EDITOR`, то есть сами типы попадают в сборку; работают только их потребители.

Потребители стоит знать точно, потому что от них зависит, что именно окажется в дропдауне. `Editor/Inspector/EntityInspectorCache.cs` — `#if UNITY_EDITOR`, `[InitializeOnLoad]`, строит кеш один раз в статическом конструкторе:

```csharp
var apiTypes = TypeCache.GetTypesWithAttribute<EntityInspectorAPIAttribute>();

foreach (var type in apiTypes)
{
    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
    foreach (var field in fields)
        AddTagOrValue(field);
}
```
Из этого следует четыре вещи. Сканируются **только публичные статические поля** — свойства и непубличные поля игнорируются. В дропдаун попадает **`field.Name`**, а не строка, переданная в конструктор ключа. `TagKey` регистрируется под `typeof(IEntity)`, `TagKey<E>` — под `E`, `ValueKey<E,T>` — под парой `(E, T)`, `ValueKey<T>` — под `(IEntity, T)`. Кеш строится при загрузке домена и сам не обновляется — новый API-класс виден после перекомпиляции.

Фильтрация расширяющая, а не сужающая: `GetTagKeys(entityType)` отдаёт имена всех записей, для которых `kvp.Key.IsAssignableFrom(entityType)`. Значит `[EntityInspectorTag(typeof(IGameEntity))]` покажет и теги `TagKey<IGameEntity>`, и теги не-generic `TagKey` (зарегистрированные под `IEntity`), а голый `[EntityInspectorTag]` запрашивает `typeof(IEntity)` и `TagKey<IGameEntity>` **не** покажет.

Для значений фильтрация идёт через `Matches(stored, requested)` по обеим осям — и по типу сущности, и по типу значения (`EntityInspectorCache.cs:72-73, 114-136`). Первым аргументом всегда стоит тип **из кеша**, вторым — запрошенный в атрибуте, и ветка с открытыми generic-определениями (`stored.IsGenericTypeDefinition`) проверяет именно кешированный тип. Типы в кеше всегда закрытые — объявить поле `ValueKey<IGameEntity, IVariable<>>` нельзя, — поэтому эта ветка недостижима, а открытое определение, переданное в атрибут (`[EntityInspectorValue(typeof(IVariable<>))]`), не совпадёт ни с чем: список выйдет пустым, и драйвер через `CallNextDrawer` молча нарисует обычное текстовое поле. Запрашивайте закрытый тип: `typeof(IVariable<int>)`.

Сами дропдауны рисуют `EntityInspectorTagAttributeDrawer` и `EntityInspectorValueAttributeDrawer` — оба под `#if UNITY_EDITOR && ODIN_INSPECTOR`. Без Odin атрибуты не делают ничего видимого, поле остаётся обычным текстовым.

### Edit Mode

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class EditModeEntityBehaviourAttribute : Attribute { }
```
`EditMode/EditModeEntityBehaviourAttribute.cs`. Единственный потребитель — редакторный партиал `MonoEntity` (`Entities/Unity/MonoEntity_Editor.cs`, весь файл под `#if UNITY_EDITOR`), через

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
internal static bool IsEditModeBehaviourDefined(object obj) =>
    obj.GetType().IsDefined(typeof(EditModeEntityBehaviourAttribute));
```
(`Utils/InternalUtils.cs:57-59`) — некешированный reflection-вызов на каждое поведение на каждую проверку. `[AttributeUsage]` без `Inherited` даёт `Inherited = true`, а `IsDefined` по умолчанию обходит цепочку базовых классов, так что атрибут на базовом классе поведения распространяется на всех наследников; проверьте, что это то, чего вы хотите, прежде чем ставить его на общую базу.

Движок edit-mode — `MonoEntity.Compile()`, доступный как Odin-кнопка `[Button("Compile")]` и как `[ContextMenu(nameof(Compile))]` (то есть и без Odin, через контекстное меню компонента):

```csharp
private void Compile()
{
    bool isPrefab = PrefabUtility.GetPrefabInstanceHandle(this.gameObject) == this.gameObject;

    try
    {
        if (!isPrefab) { this.DisableInEditMode(); this.DisposeInEditMode(); }

        this.Uninstall();
        this.Construct();
        this.Install();
        this.PrecomputeCapacities();

        if (!isPrefab) { this.InitInEditMode(); this.EnableInEditMode(); }
    }
    catch (Exception e)
    {
        Debug.LogError($"... Compilation failed: {e.Message}\n{e.StackTrace}", this);
    }
}
```
`MonoEntity_Editor.cs:31-60`. Всё тело под `try/catch`: упавший инсталлятор не прерывает редактор, но и не останавливает редактирование — вы увидите только `Debug.LogError`, причём сущность останется в полусобранном состоянии (часть шагов уже выполнена, часть — нет). Автоматический прогон включается сериализованным флагом `autoCompile`: тогда `OnValidate` сначала раздаёт `sceneInstallers` колбэк `installer.onValidateCallback = this.Compile`, потом зовёт `Compile()`, и сам `OnValidate` тоже обёрнут в `try/catch` с пустым обработчиком. `PrecomputeCapacities()` записывает обратно в сериализованные поля `initialTagCapacity`/`initialValueCapacity`/`initialBehaviourCapacity` — это и есть механизм предразмеривания хранилищ сущности из редактора.

### Внутреннее

```csharp
internal static class InternalUtils
{
    internal static readonly float DeltaTick = 1f / System.Diagnostics.Stopwatch.Frequency;

    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static long GetTimestamp();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool IsEditModeBehaviourDefined(object obj);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void Add<T>(ref T[] array, ref int count, T item);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool AddIfAbsent<T>(ref T[] array, ref int count, T item, IEqualityComparer<T> comparer, int initialCapacity = 1);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool Contains<T>(T[] array, T item, int count, IEqualityComparer<T> comparer);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void Expand<T>(ref T[] array);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool Remove<T>(ref T[] array, ref int count, T item, IEqualityComparer<T> comparer);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static int CeilToPrime(int value, out int index);

    internal static readonly int[] PrimeTable;
}
```
`Utils/InternalUtils.cs`. Класс `internal` — из игровых сборок недоступен. Приведён не как API, а как описание домашнего стиля, который команде стоит копировать для своих горячих коллекций: параметры `ref T[] array, ref int count`, явно передаваемый `IEqualityComparer<T>` (никогда `EqualityComparer<T>.Default` на месте вызова и никогда `object.Equals`, то есть нет упаковки struct-ключей), рост удвоением с клампом по `int.MaxValue`, `array[count] = default` при удалении, `AggressiveInlining` на каждом члене. Одна поправка: `Remove<T>` здесь **сохраняет порядок** через `Array.Copy` — это O(n)-сдвиг, а не swap-with-last; для горячих коллекций копировать нужно стиль, а не эту конкретную реализацию.

---

## Идиомы

### 1. Round-robin система: наследовать, переопределить один метод, хранилище отдать базе

```csharp
public sealed class MovementSystem : EntitySystem<IGameEntity>
{
    public MovementSystem(IReadOnlyEntityCollection<IGameEntity> source, Settings settings)
        : base(source, settings)
    {
    }

    protected override void Update(IGameEntity entity, float deltaTime)
    {
        MoveUseCase.MoveTowardsDirection(entity, deltaTime);
    }
}
```

`sealed`, один пробрасывающий конструктор, один `override`. Вся машинерия массива/словаря/курсора/батчинга остаётся в базе, так что файл системы не содержит ничего, кроме домена, — и даже домен делегирован статическому `*UseCase`, чтобы его можно было переиспользовать из поведения, из теста или из другой системы. `Settings` написан без квалификации и разрешается в унаследованный вложенный тип. `sealed` здесь не стилистика: он позволяет JIT девиртуализовать вызов `Update` на каждую сущность.

Антипаттерн:

```csharp
// ПЛОХО
public class MovementSystem : MonoBehaviour
{
    private readonly List<GameEntity> _entities = new();

    void Update()
    {
        foreach (var e in _entities)                      // нет бюджета кадра, нет батчинга
            e.Position += e.Velocity * Time.deltaTime;    // логика заперта в MonoBehaviour, не тестируется
    }
}
```

### 2. Приоритетная система: `EvaluatePriority` — чистый запрос, `Update` — работа

```csharp
public sealed class AIPrioritySystem : PriorityEntitySystem<IGameEntity>
{
    public AIPrioritySystem(
        IReadOnlyEntityCollection<IGameEntity> source,
        Settings settings,
        params IEntityTrigger<IGameEntity>[] triggers
    ) : base(source, settings, triggers)
    {
    }

    protected override EntityUpdatePriority EvaluatePriority(IGameEntity entity)
    {
        float distance = PlayerUseCase.GetDistanceToPlayer(entity);

        if (distance < 10f)
            return EntityUpdatePriority.High;
        if (distance < 40f)
            return EntityUpdatePriority.Medium;

        return EntityUpdatePriority.Low;
    }

    protected override void Update(IGameEntity entity, float deltaTime)
    {
        AIUseCase.UpdateAIBehaviour(entity, deltaTime);
    }
}
```

Важность (дешёвая проверка без побочных эффектов) отделена от работы, поэтому фреймворк может перераскладывать сущности по корзинам в своём ритме, не запуская ИИ. Дорогой запрос расстояния читается **один раз в локальную переменную**, дальше плоские упорядоченные guard'ы — никаких вложенных тернарников и повторных вызовов. Проброс `params IEntityTrigger<E>[]` оставляет вызывающему решение, пересчитывать приоритеты по кулдауну или реактивно, и система об этом не знает.

Антипаттерн здесь — это буквально пример из документации (`Docs/Entities/Systems/EntityUpdatePriority.md:23-36`), где `DistancePrioritySystem` объявлен с одним только `EvaluatePriority`:

```csharp
// ПЛОХО: не компилируется
public sealed class DistancePrioritySystem : PriorityEntitySystem<IGameEntity>
{
    protected override EntityUpdatePriority EvaluatePriority(IGameEntity entity) => /* ... */;
    // конструктора нет -> неявный конструктор по умолчанию не найдёт
    //                     base(source, settings, triggers)
    // Update не реализован -> абстрактный член в неабстрактном классе
}
```

### 3. Триггер вместо опроса каждый кадр

```csharp
// приоритет зависит от тега "IsVisible" -> триггер по тегам, не по значениям
var trigger = new TagEntityTrigger<IGameEntity>();

var settings = new AIPrioritySystem.Settings
{
    cooldown = 0f,              // периодический O(n)-пересчёт выключен полностью
    highPercent = 70,
    midPercent = 20,
    frameBudget = 0.008f,
    batching = { minSize = 128, maxSize = 1024 }
};

var system = new AIPrioritySystem(world, settings, trigger);
gameContext.AddTickSystem(system);
```

`OnEnable` вешает `trigger.SetAction(this.ChangePriority)`, а `OnAddEntity`/`OnRemoveEntity` трекают и растрекивают каждую сущность — изменение приоритета стоит один реактивный колбэк вместо прохода `RecalculatePriorities()` по всей коллекции. Это прямая рекомендация документации (`Docs/Entities/Systems/Manual.md:110`): «Use triggers with `PriorityEntitySystem<E>` to avoid recalculating priorities every frame». `cooldown = 0` гасит опрос совсем (`UpdateCooldown` выходит сразу: `if (_settings.cooldown <= 0) return;`).

Выбор типа триггера — не формальность. `ValueEntityTrigger<E>()` с дефолтами реагирует на **любое** добавление, удаление и изменение **любого** значения сущности, то есть на запись `Position` каждый кадр. Если приоритет зависит от одного значения, это хуже кулдауна. Нужен точный хук — берите `InlineEntityTrigger`:

```csharp
var trigger = new InlineEntityTrigger<IGameEntity>(
    track:   (entity, action) => entity.GetValue(GameEntityAPI.Team).OnValueChanged += _ => action(entity),
    untrack: (entity, action) => { /* симметричная отписка */ }
);
```

### 4. Привязка систем к жизненному циклу контекста

```csharp
IEntityWorld<IGameEntity> world = ...;

var settings = new MovementSystem.Settings
{
    frameBudget = 0.016f,
    batching = { minSize = 64, maxSize = 512 }
};

MovementSystem movement = new MovementSystem(world, settings);

IEntity gameContext = ...;
gameContext.AddTickSystem(movement);        // одна фаза, не три
```

Система строится над миром (`IReadOnlyEntityCollection<E>`), а не над объектом сцены, и один вызов подписывает `Enable`/`Update`/`Disable`/`Dispose` на владеющий контекст. Вложенный инициализатор `batching = { minSize = 64, maxSize = 512 }` **мутирует уже выделенный** `AdaptiveBatching`, а не заменяет его — второго объекта не создаётся. Фаза — решение места вызова, поэтому один и тот же класс системы может идти в `Tick`, `FixedTick` или `LateTick`.

В `Docs/Entities/Systems/Manual.md:87-89` все три вызова показаны подряд. Это перечисление опций, а не рецепт: подписав все три, вы получите три обновления системы за кадр и трижды подписанные `Enable`/`Disable`/`Dispose`.

Когда система должна жить короче контекста, `AddTickSystem` не подходит — он отбрасывает подписки. Разворачивайте вручную и держите хендлы:

```csharp
public sealed class CombatSystemsScope : IDisposable
{
    private readonly EnableSubscription _enable;
    private readonly TickSubscription _tick;
    private readonly DisableSubscription _disable;

    public CombatSystemsScope(IEntity context, EntitySystemBase<IGameEntity> system)
    {
        _enable  = context.WhenEnable(system.Enable);
        _tick    = context.WhenTick(system.Update);
        _disable = context.WhenDisable(system.Disable);
    }

    public void Dispose()
    {
        _disable.Dispose();
        _tick.Dispose();
        _enable.Dispose();
    }
}
```

`WhenEnable`/`WhenTick`/`WhenDisable` возвращают `readonly struct`-подписки, реализующие `IDisposable` (`Lifecycle/Subscriptions/`), так что ни одной аллокации на хранение хендлов не приходится — в отличие от варианта с `List<IDisposable>`, который бы их упаковал.

### 5. Мир и системы в инсталляторе контекста

```csharp
[Serializable]
public sealed class UnitSystemInstaller : IEntityInstaller<IGameContext>
{
    // UnitMovementSystem : EntitySystem<IUnitEntity> — generic-параметр системы
    // обязан совпадать с типом сущностей мира, иначе AddTickSystem не выведет TEntity
    [SerializeField] private UnitMovementSystem.Settings _movementSettings;

    public void Install(IGameContext context)
    {
        // 1. мир активных сущностей
        var world = new EntityWorld<IUnitEntity>();

        // 2. весь жизненный цикл мира — одним вызовом
        world.BindTo(context);

        // 3. мир доступен остальному коду как типизированное значение контекста
        context.AddValue(GameContextAPI.UnitWorld, (IEntityWorld<IUnitEntity>) world);

        // 4. системы над этим миром
        context.AddTickSystem(new UnitMovementSystem(world, _movementSettings));
    }
}
```

Контекст в этой версии — обычная сущность, а инсталлятор — обычный `[Serializable]`-класс, реализующий `IEntityInstaller<IGameContext>`: не `MonoBehaviour`, не базовый класс фреймворка. `world.BindTo(context)` — реальный метод (`World/Extensions.cs:8-18`), и его тело — ровно те восемь подписок, которые документация best practices выписывает руками:

```csharp
source.WhenInit(world.InitEntities);
source.WhenEnable(world.Enable);
source.WhenTick(world.Tick);
source.WhenFixedTick(world.FixedTick);
source.WhenLateTick(world.LateTick);
source.WhenDisable(world.Disable);
source.WhenDispose(world.DisposeEntities);
source.WhenDispose(world.Dispose);
```

Подписка группами методов (`world.Tick`), а не лямбдами, — её можно отписать и она не захватывает замыкание.

Антипаттерн здесь особенный: это **пример из самой документации**, `Docs/BestPractices/EntitySystem.md:259-285`.

```csharp
// ПЛОХО: не компилируется на этой версии фреймворка
var entityPool = new MultiEntityPool<string, IUnitEntity>(factoryCatalog);
context.AddEntityPool(entityPool);

var entityWorld = new EntityWorld<IUnitEntity>();
context.AddEntityWorld(entityWorld);
```

Двухаргументного `MultiEntityPool<K,E>` не существует: класс объявлен как `MultiEntityPool<TKey, TEntity, TArgs>` с `where TArgs : IArgs` и конструктором `(IMultiEntityFactory<TKey,TEntity,TArgs> factory, TArgs args, ExpandMode expandMode = ExpandMode.ExpandByOne)` (``Pooling/Core/MultiEntityPool`1.cs:17,52``); двухпараметрическая форма — это только *интерфейс* `IMultiEntityPool<in TKey, TEntity>`. А `AddEntityPool`, `AddEntityWorld` и `GetEntityWorld` не существуют нигде под `Assets/Plugins/Atomic` — `grep` не даёт ни одного совпадения. Это кодогенерация проекта-примера над `int`-хранилищем значений, а не API фреймворка. Читая best practices, держите это в голове: показанная там «каноническая форма контекста» компилируется только вместе с генератором.

### 6. Типизированное семейство: пять объявлений

```csharp
public interface IUnitEntity : IEntity
{
}

// база семейства — и единственное место, где появляется ключ пула
public class UnitView : EntityView<IUnitEntity>
{
    [field: SerializeField]
    public string Name { get; private set; }
}

[CreateAssetMenu(fileName = "UnitViewCatalog", menuName = "Example/New UnitViewCatalog")]
public sealed class UnitViewCatalog : EntityViewCatalog<IUnitEntity, UnitView>
{
}

public sealed class UnitViewPool : EntityViewPool<string, IUnitEntity, UnitView>
{
    protected override string GetKey(UnitView view) => view.Name;
}

public sealed class UnitCollectionView : EntityCollectionView<string, IUnitEntity, UnitView>
{
    protected override string GetKey(IUnitEntity entity) => entity.Name;
}

public sealed class UnitWorldView : EntityWorldView<string, IUnitEntity, UnitView>
{
    protected override string GetKey(IUnitEntity entity) => entity.Name;
}
```

Весь слой визуализации семейства — пять объявлений, в каждом максимум одно выражение. Закрыв generic-параметры один раз, вы получаете типобезопасность вниз по всему стеку: `collectionView.Get(entity)` возвращает `UnitView` без кастов и `as`, а `GetKey` — единственное место, где идентичность модели отображается в идентичность префаба.

Обратите внимание на `Name` в `UnitView`. **Это не косметика, а условие компиляции.** `EntityView<E>` не объявляет `Name` — его объявляет только не-generic `EntityView` (`UI/View/EntityView.cs`). Вариант `protected override string GetKey(UnitView view) => view.Name;` при пустом `UnitView : EntityView<IUnitEntity>` не скомпилируется. Поэтому ключ объявляется в базе вашего семейства, и `[field: SerializeField]` на авто-свойстве — принятый в Atomic способ сериализовать без ручного backing-поля.

Антипаттерн — «сэкономить» на поле и ключеваться по имени `GameObject`:

```csharp
// ПЛОХО
protected override string GetKey(UnitView view) => view.name;
```
`EntityView<E>.Activate` присваивает `this.name = this.FormateName(entity)` (``EntityView`1.cs:76``), то есть `"Knight:17"`, а `Deactivate()` имя не восстанавливает. На `Return` пул вызовет `GetKey(view)` и получит имя сущности — вид уедет в стек под случайным ключом, и `Rent("Knight")` его никогда не найдёт.

### 7. Инсталлятор вида: типизированный, с симметричным `Uninstall`

```csharp
public sealed class TankViewInstaller : MonoEntityInstaller<IGameEntity>
{
    [SerializeField] private TakeDamageViewBehaviour _takeDamageBehaviour;
    [SerializeField] private PositionViewBehaviour _positionBehaviour;
    [SerializeField] private RotationViewBehaviour _rotationBehaviour;
    [SerializeField] private TeamColorViewBehaviour _teamColorBehaviour;

    public override void Install(IGameEntity entity)
    {
        entity.AddBehaviour(_takeDamageBehaviour);
        entity.AddBehaviour(_positionBehaviour);
        entity.AddBehaviour(_rotationBehaviour);
        entity.AddBehaviour(_teamColorBehaviour);
    }

    public override void Uninstall(IGameEntity entity)
    {
        entity.DelBehaviour(_takeDamageBehaviour);
        entity.DelBehaviour(_positionBehaviour);
        entity.DelBehaviour(_rotationBehaviour);
        entity.DelBehaviour(_teamColorBehaviour);
    }
}
```

Generic-база означает ноль кастов внутри `Install`. Зеркальный `Uninstall` — это то, что делает вид **пулящимся**: всё, что навешено на `Activate`, снимается на `Deactivate`, и переиспользованный вид не оставляет на сущности мусора. Поведения — сериализованные ссылки (авторятся и настраиваются в префабе), а не `new` на каждую активацию, поэтому активация не аллоцирует. Сам класс вида при этом остаётся пустым: вся композиция — данные.

Антипаттерн:

```csharp
// ПЛОХО
public sealed class TankViewInstaller : MonoEntityInstaller
{
    public override void Install(IEntity entity)
    {
        var e = (IGameEntity) entity;                            // ручной каст
        e.AddBehaviour(new PositionViewBehaviour(transform));    // аллокация на каждую активацию
    }
    // Uninstall не переопределён -> поведения накапливаются на пулящихся сущностях
}
```

И тот же сбой в готовом виде: `MonoEntityInstallerConfigurable`, положенный в `EntityView<E>.installers`, выглядит как удобный способ собрать композицию из `[Serializable]`-классов без `MonoBehaviour` на каждый, но `Uninstall` он не переопределяет и `sealed`. Для вида, который возвращается в пул, он непригоден.

### 8. Привязать мировой вид один раз в точке входа

```csharp
public class EntryPoint : MonoBehaviour
{
    [SerializeField] private UnitWorldView _worldView;
    [SerializeField] private GameContextBootstrap _bootstrap;

    private IEntityWorld<IUnitEntity> _world;

    private void Start()
    {
        _world = _bootstrap.Context.GetValue(GameContextAPI.UnitWorld);
        _worldView.Activate(_world);
    }

    private void OnDestroy()
    {
        _worldView.Deactivate();     // фреймворк этого НЕ делает
    }
}
```

Один `Activate` заменяет всю бухгалтерию спавна/despawn'а на стороне видов: `EntityWorldView` подписывается на `OnAdded`/`OnRemoved` и арендует/возвращает виды на весь срок привязки. Код спавна трогает только модель — взять сущность из пула, выставить значения, `world.Add` — и ни разу не упоминает `GameObject`. Ссылка на вид сериализованная, а не через синглтон.

`OnDestroy` с `Deactivate()` здесь обязателен: ни `EntityWorldView`, ни `EntityCollectionView` не вызывают `Deactivate` сами (``UI/World/EntityWorldView`1.cs`` — ни `OnDisable`, ни `OnDestroy` в файле нет).

Антипаттерн:

```csharp
// ПЛОХО
var entity = pool.Rent("Tank");
var go = Instantiate(tankPrefab);                 // время жизни вида руками рядом с моделью
go.GetComponent<UnitView>().Activate(entity);
_spawnedViews[entity] = go;                       // дублирующая бухгалтерия, которую надо
                                                  // синхронизировать на despawn
```

### 9. Итерация по `EntityCollectionView` без аллокаций

```csharp
foreach (KeyValuePair<IUnitEntity, UnitView> pair in collectionView)
{
    IUnitEntity entity = pair.Key;
    UnitView view = pair.Value;
    view.transform.position = entity.GetValue(UnitEntityAPI.Position);
}
```

`GetEnumerator()` отдаёт `Dictionary<E,V>.Enumerator` как структуру из публичного не-интерфейсного метода, поэтому `foreach` привязывается к ней по шаблону и не аллоцирует. Объявление переменной цикла конкретным `KeyValuePair<,>` — часть идиомы: реализации `IEnumerator<KeyValuePair<E,V>>` и `IEnumerator` здесь **явные**, так что они упакуют энумератор ровно в тот момент, когда вы положите коллекцию в `IEnumerable<...>`-переменную.

```csharp
// ПЛОХО: энумератор упакован, одна аллокация на каждый проход
IEnumerable<KeyValuePair<IUnitEntity, UnitView>> pairs = collectionView;
foreach (var pair in pairs) { /* ... */ }
```

### 10. Итерация по `IReadOnlyEntityCollection<E>` без аллокаций

```csharp
public sealed class CrowdSystem : EntitySystemBase<IGameEntity>
{
    // база держит свой _source как private protected — из игровой сборки он недоступен,
    // поэтому ссылку запоминаем сами
    private readonly IReadOnlyEntityCollection<IGameEntity> _entities;

    public CrowdSystem(IReadOnlyEntityCollection<IGameEntity> source, Settings settings)
        : base(source, settings)
    {
        _entities = source;
    }

    protected override void OnUpdate(int batchSize, float deltaTime)
    {
        for (int i = 0, count = _entities.Count; i < count; i++)
        {
            IGameEntity entity = _entities[i];
            // ...
        }
    }

    public override void Dispose() { }
}
```

Две вещи сразу. Первая: `_source` базы — `private protected`, а фреймворк живёт в своей сборке `Atomic.Entities`, так что из игрового кода поле не читается — наследник обязан сохранить источник в собственное поле (или получать его аргументом). Вторая: `EntityCollection<E>` объявляет `public struct Enumerator` и отдаёт её из публичного `GetEnumerator()`, но **интерфейс** `IReadOnlyEntityCollection<E>` выставляет только формы `IEnumerator<E>` / `IEnumerator`. Поскольку поле статически типизировано интерфейсом, `foreach (E e in _entities)` упаковывает структуру-энумератор — одна аллокация на проход. Интерфейс зато объявляет `E this[int index]` и `bool TryGetAt(int index, out E entity)` (``Collections/IReadOnlyEntityCollection`1.cs``), и индексный цикл свободен от этой аллокации.

### 11. `TryGet` вместо `Contains` + `Get`

```csharp
if (collectionView.TryGet(entity, out UnitView view))
    view.PlayHitFlash();
```

Один проход по словарю без исключительного пути. `Get` — assert-форма, бросающая `KeyNotFoundException`. `Contains` с последующим `Get` удваивает поиск.

### 12. Прогреть пул до геймплея

```csharp
UnitViewPool pool = ...;

// на экране загрузки — батчевый асинхронный прогрев
await pool.InitAsync("Knight", 20);
await pool.InitAsync("Archer", 12);

// синхронный вариант, когда кадром можно пожертвовать
pool.Init("Knight", 20);
```

`InitAsync` идёт через Unity'ный батчевый `InstantiateAsync(prefab, count, this.container)` и ждёт **одну** `AsyncInstantiateOperation<V>` — это одна асинхронная операция на N объектов, а не цикл; возвращает `ValueTask`, то есть на синхронном завершении не аллоцирует `Task`. Оба метода кладут в per-key стек уже деактивированные экземпляры, так что первый `Rent` геймплея не инстанцирует. Оба, и `Init` тоже, бросают `KeyNotFoundException`, если ключ не зарегистрирован, — то есть прогрев надо делать после `Awake` пула (где регистрируются каталоги) либо после явного `Register`.

### 13. Гизмо как поведение сущности, а не редакторный код в виде

```csharp
#if UNITY_EDITOR
public sealed class TransformGizmos : IEntityGizmos<IGameEntity>
{
    public void DrawGizmos(IGameEntity entity)
    {
        Vector3 center = entity.GetValue(GameEntityAPI.Position);
        float scale = entity.GetValue(GameEntityAPI.Scale);
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, scale);
    }
}
#endif
```

Отладочная отрисовка собирается на сущность как любое другое поведение и находится видом через `is IEntityGizmos`, то есть устанавливается и снимается вместе с остальными. Реализуется **generic**-интерфейс: не-generic точка входа приходит default interface method'ом, без адаптера и без ручного каста.

`#if UNITY_EDITOR` вокруг класса здесь обязателен, и это правка к документации. `IEntityGizmos`/`IEntityGizmos<in E>` объявлены под `#if UNITY_5_3_OR_NEWER` (`Behaviours/IEntityGizmos.cs:3`), а не под `#if UNITY_EDITOR`: интерфейс и его реализации компилируются в плеерную сборку. `UnityEditor.Handles` в сборке не существует, поэтому пример из ``Docs/Entities/UI/EntityView`1.md:141-149``, где `TransformGizmos` объявлен без всякой защиты, ломает плеерный билд. Если гизмо-поведение нужно упоминать из кода, который компилируется всегда (например, держать его полем инсталлятора), прячьте под `#if UNITY_EDITOR` и поле, и его использование.

Антипаттерн:

```csharp
// ПЛОХО
public class UnitView : EntityView<IGameEntity>
{
    private void OnDrawGizmos()                             // не композируется, не per-entity,
    {                                                       // и скрывает базовый OnDrawGizmos
        Gizmos.DrawWireSphere(transform.position, 1f);
    }
}
```

### 14. Гизмо-поведение как поле инсталлятора

```csharp
public sealed class CharacterViewInstaller : MonoEntityInstaller<IGameEntity>
{
#if UNITY_EDITOR
    private readonly TransformGizmos _transformGizmos = new();
#endif

    public override void Install(IGameEntity entity)
    {
        // остальные привязки...
#if UNITY_EDITOR
        entity.AddBehaviour(_transformGizmos);
#endif
    }

    public override void Uninstall(IGameEntity entity)
    {
        // остальные привязки...
#if UNITY_EDITOR
        entity.DelBehaviour(_transformGizmos);
#endif
    }
}
```

`private readonly ... = new()` выделяет stateless-поведение один раз на инсталлятор, а не на активацию, и хранение той же самой ссылки — ровно то, что позволяет `DelBehaviour(_transformGizmos)` снять именно этот экземпляр. Target-typed `new()` без повторения имени типа.

### 15. Своя гизмо-точка на виде, когда база не даёт переопределиться

```csharp
public class UnitView : EntityView<IUnitEntity>
{
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!this.IsActive)
            return;

        this.Entity.DrawGizmos();                       // диспетчер по IEntityGizmos
        Handles.Label(transform.position, this.Entity.Name);
    }
#endif
}
```

`EntityView<E>.OnDrawGizmos`/`OnDrawGizmosSelected` объявлены `private protected virtual`, поэтому из вашей сборки их не переопределить, а собственный одноимённый метод их не переопределяет, а скрывает. Публичный `Extensions.DrawGizmos(this IEntity)` (`Entities/Extensions.cs:209`) даёт тот же проход по поведениям, что и внутренний `OnGizmosDraw`, так что вы можете собрать собственную гизмо-точку, не теряя дистпетчер. Используйте это только когда действительно нужен view-level рисунок; обычный случай — идиома 13.

### 16. Один `[EntityInspectorAPI]`-класс как единственный источник ключей

```csharp
[EntityInspectorAPI]
public static partial class GameEntityAPI
{
    public static readonly TagKey IsEnemy = new(nameof(IsEnemy));
    public static readonly TagKey<IGameEntity> IsSelectable = new(nameof(IsSelectable));

    public static readonly ValueKey<IGameEntity, int> Health = new(nameof(Health));
    public static readonly ValueKey<IGameEntity, Vector3> Position = new(nameof(Position));
}
```

`public static readonly` типизированные ключи, собранные через `nameof(...)`: имя физически не может разойтись с полем, а `ValueKey<IGameEntity, int>` делает `entity.GetValue(GameEntityAPI.Health)` возвращающим `int` без аргумента типа и без строки на месте вызова. `partial` — чтобы сгенерированная и рукописная половины сосуществовали.

Три требования, которые видно только из кода кеша (`Editor/Inspector/EntityInspectorCache.cs:149-156`), а не из документации. Поля должны быть **`public static`** — `GetFields(BindingFlags.Public | BindingFlags.Static)` не увидит ни `internal`, ни свойство. В дропдаун попадает **имя поля**, а не строка из конструктора, поэтому `nameof` обязателен:

```csharp
// ПЛОХО: в дропдауне будет "Health", а Id посчитан от "hp" —
// выбранное в инспекторе значение разрешится в другой ключ
public static readonly ValueKey<IGameEntity, int> Health = new("hp");
```

И выбор между `TagKey` и `TagKey<E>` определяет, где тег будет видно: не-generic `TagKey` регистрируется под `typeof(IEntity)` и попадает во все дропдауны, `TagKey<IGameEntity>` — только в те, что запрашивают `IGameEntity` или его наследника.

Антипаттерн на месте вызова:

```csharp
entity.GetValue<int>("Health");   // строковый литерал, хеш строки на каждый вызов,
                                  // молча неверно после переименования
```

### 17. Дропдауны вместо сырых строковых полей

```csharp
public class CharacterInstaller : MonoEntityInstaller<IGameEntity>
{
    [EntityInspectorTag(typeof(IGameEntity))]
    [SerializeField] private string _targetTag;

    [EntityInspectorValue(typeof(IGameEntity), typeof(IVariable<int>))]
    [SerializeField] private string _healthKey;

    public override void Install(IGameEntity entity)
    {
        entity.AddTag(_targetTag);
        entity.AddValue(_healthKey, new Variable<int>(100));
    }
}
```

Там, где имя действительно должно авторяться в инспекторе, эти атрибуты превращают текстовое поле в отфильтрованный попап из `[EntityInspectorAPI]`-классов, что убирает класс опечаточных багов целиком. Фильтр по типу значения берётся по хранимой обёртке, а не по «логическому» типу: `typeof(IVariable<int>)` оставит в списке только ключи, чьё значение действительно `IVariable<int>`.

Двухаргументная форма здесь выбрана осознанно, потому что односложная читается двусмысленно:

```csharp
[EntityInspectorValue(typeof(IGameEntity))]   // компилируется, но означает
                                              // «значения типа IGameEntity», а не фильтр по сущности
```

Оба конструктора принимают `Type` первым параметром, так что компилятор здесь не поможет. Если нужен фильтр только по типу значения — пишите `[EntityInspectorValue(typeof(int))]`; если по сущности тоже — обе формы явно.

Открытое определение передавать нельзя:

```csharp
// ПЛОХО: дропдаун окажется пустым и выродится в обычное текстовое поле
[EntityInspectorValue(typeof(IVariable<>))]
[SerializeField] private string _anyVariableKey;
```

`Matches` сверяет `IsGenericTypeDefinition` у **кешированного** типа, а не у запрошенного, и кешированные типы всегда закрытые, потому что поля вида `ValueKey<E, IVariable<>>` объявить невозможно. Совпадений не будет, `GetValueKeys` вернёт пустой список, а драйвер уйдёт в `CallNextDrawer` — то есть ошибка выглядит как «атрибут не сработал». Указывайте закрытый тип: `typeof(IVariable<int>)`.

Все эти значения остаются `string` в рантайме. Best practice самой документации (`Docs/Entities/Inspector/Manual.md:86`): «always validate string names at runtime if they can be supplied outside the Inspector».

### 18. Переопределить `FormateName` для пулов с высокой текучкой

```csharp
public class ProjectileView : EntityView<IProjectileEntity>
{
    protected override string FormateName(IProjectileEntity entity) => "Projectile";
}
```

`EntityView<E>.Activate` на каждой активации выполняет `this.name = this.FormateName(entity)`, а дефолтная реализация — `string.Format("{0}:{1}", entity.Name, entity.InstanceID)`: аллокация строки плюс сеттер `GameObject.name`, который сам по себе managed→native-маршалинг. Для пуль/искр/урон-попапов, которых сотни в секунду, это самая дорогая единичная строка во всём пути активации. Возврат константы убирает аллокацию, но не сеттер; имя в иерархии при этом перестаёт быть диагностическим — это осознанный обмен.

### 19. Edit-mode превью

```csharp
[EditModeEntityBehaviour]
public sealed class InitColorBehaviour : IEntityInit<IGameEntity>
{
    public void Init(IGameEntity entity)
    {
        var renderer = entity.GetValue(GameEntityAPI.Renderer);
        var color = entity.GetValue(GameEntityAPI.Color);
        renderer.material.color = color;
    }
}
```

Опт-ин на класс, а не глобальный переключатель «работать в редакторе»: помечаются только те поведения, которые безопасно и полезно запускать вне Play Mode. Поведение реализует узкий `IEntityInit<E>`, а не толстую базу жизненного цикла, и тот же путь кода работает и в редакторе, и в рантайме, — отдельной реализации превью, которую надо синхронизировать, не появляется.

Generic-форма здесь не стилистика. Пример документации (`Docs/Entities/EditMode/EditModeEntityBehaviourAttribute.md:19-30`) реализует не-generic `IEntityInit` и читает значения строками: `entity.GetValue<Renderer>("Renderer")`. Заменить строки на типизированные ключи, не меняя интерфейс, нельзя: `GetValue<E,T>(this E entity, ValueKey<E,T> key)` выводит `E` из обоих аргументов, а в не-generic `Init(IEntity entity)` аргумент имеет тип `IEntity`, тогда как `GameEntityAPI.Renderer` — это `ValueKey<IGameEntity, Renderer>`; вывод типов не сойдётся. Либо `IEntityInit<IGameEntity>` и типизированные ключи, либо `IEntityInit` и строки.

Границы применимости надо знать до того, как на это полагаться. Атрибут читает **только** редакторный партиал `MonoEntity` (`Entities/Unity/MonoEntity_Editor.cs`), значит: работает исключительно для сущностей, авторенных в сцене как `MonoEntity`; для чистых `Entity` и для сущностей, которые рисует `EntityView`, не работает вообще. Запуск — `Compile()`: Odin-кнопка, контекстное меню компонента или сериализованный `autoCompile` через `OnValidate`.

### 20. Разрешение попадания по нескольким коллайдерам

```csharp
public class DamageVolume : MonoBehaviour
{
    [EntityInspectorTag(typeof(IGameEntity))]
    [SerializeField] private string _targetTag;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetEntity(out IEntity entity) && entity.HasTag(_targetTag))
            DamageUseCase.TakeDamage(entity, 10);
    }
}
```

`TryGetEntity` — это `TryGetComponent(out IEntity)` на том же объекте (`Entities/Extensions.cs:142-143`, разрешается через перегрузку для `Component`; отдельной перегрузки для `Collider` нет). Каждый дочерний коллайдер несёт `MonoEntityProxy`, чей `_source` указывает на одну и ту же `MonoEntity` (автопроводка через `Reset()` при добавлении компонента), поэтому любой хитбокс или триггер разрешается в одну логическую сущность без обхода `GetComponentInParent` в момент попадания. Запрос идёт по **интерфейсу**, так что код столкновений не знает, попал он в саму сущность или в прокси. Тег — данные на компоненте, а не литерал, поэтому один скрипт обслуживает все объёмы урона в игре.

Для физики есть и перегрузки по коллизиям, которые обычно и нужны:

```csharp
private void OnCollisionEnter(Collision collision)
{
    if (collision.TryGetEntity(out IEntity entity))
        /* ... */;
}
```

Альтернатива без прокси — `FindEntityInParent`, то есть `GetComponentInParent<IEntity>()` в момент попадания. Это обмен «память и дисциплина сборки префаба» на «обход иерархии на каждый хит»; осознанный выбор, но делайте его явно.

---

## Производительность

Ниже разделено то, что написано в исходнике или измерено тестами, и то, что выводится из прочитанного кода.

**В исходнике: бюджет кадра на `Stopwatch`, не на `Time`.** `EntitySystemBase<E>.Update` обрамляет `OnUpdate` вызовами `InternalUtils.GetTimestamp()` (это `Stopwatch.GetTimestamp()`) и переводит в секунды предвычисленным `static readonly float DeltaTick = 1f / Stopwatch.Frequency` (`EntitySystemBase.cs:177-189`, `InternalUtils.cs:14-17`). Дефолты: `frameBudget = 0.03f`, `minSize = 1024`, `maxSize = 2048`, `scaleDown = 2`, `stepUp = 256`. Документация просит «Tune `frameBudget` and `batching` settings per platform» и «Keep per-entity update logic fast to allow adaptive batching to scale» (`Docs/Entities/Systems/Manual.md:106-110`).

**Измерено тестами: разогрев батча реален и зафиксирован в хелпере.** `Tests/EditMode/System/EntitySystemTests.cs:86-94` документирует, что первый `Update` не обрабатывает ничего, и вводит `WarmupBatchSize`, чтобы тесты вообще могли что-то проверить. Ветка «в бюджете» — `Math.Min(_batchSize + stepUp, maxSize)` и к `minSize` вверх не подтягивает, так что `minSize` работает только как пол после перебора бюджета.

**В исходнике: `EntitySystem<E>` — плотный массив плюс словарь индексов.** Непрерывный проход по `E[]` в `OnUpdate`, добавление амортизированно O(1) через `Array.Resize` с удвоением, удаление O(1) через swap-with-last (`EntitySystem.cs:104-147`). Нет индирекции `List<T>`, нет линейного `Remove(item)`, нет сдвигов.

**В исходнике: горячие циклы не читают поля.** `EntitySystem<E>.OnUpdate` поднимает `_entityCount` и `_cursor` в локали перед циклом и возвращает курсор один раз в конце; `batchSize` зажимается по `count`, так что сущность не обрабатывается дважды за кадр. `PriorityEntitySystem.UpdateEntities` в каждой корзине копирует в локали `cursor`, `count` **и саму ссылку на массив** (`E[] array = _highEntities;`) перед циклом (`PriorityEntitySystem.cs:349-412`).

**В исходнике: приоритетная система не аллоцирует на изменениях.** `Entry` и `Command` — приватные `struct`, лежащие в `Dictionary<E,Entry>` и `List<Command>` с предразмером 64; `Command.Type` — `enum : byte`, `EntityUpdatePriority` — тоже `byte`. `UpdateCommands` обходит список индексным `for` с явным комментарием `// ReSharper disable once ForCanBeConvertedToForeach`.

**Вывод: struct-сущности в системах будут упаковываться.** Ограничение у систем — только `where E : IEntity`, без `class`. `Dictionary<E,int>` в `EntitySystem<E>` и `Dictionary<E,Entry>` в `PriorityEntitySystem<E>` используют `EqualityComparer<E>.Default`; для struct-типа, реализующего интерфейс, это обернётся упаковкой на каждый поиск. UI-слой struct-сущности не примет вообще (`where E : class, IEntity`).

**Вывод: `AggressiveInlining` на виртуальных членах ничего не даёт.** Все четыре хука `EntitySystemBase` (`OnEnable`, `OnDisable`, `OnAddEntity`, `OnRemoveEntity`) помечены `[MethodImpl(AggressiveInlining)]` и при этом `virtual`; тот же атрибут стоит на `protected abstract void Update(E, float)` в `PriorityEntitySystem`. На точке вызова атрибут в этих случаях неприменим. Нагрузку он несёт на `sealed override`-ах (`EntitySystem.OnUpdate/OnAddEntity/OnRemoveEntity`) и на приватных хелперах (`AddInternal`, `RemoveInternal`, `ChangePriorityInternal`, `Swap`, `UpdateCooldown`, `UpdateEntities`, `UpdateCommands`). Сам `sealed override` при этом полезен сам по себе: он позволяет JIT девиртуализовать вызов `Update` на сущность.

**Вывод: `foreach` по `_source` внутри системы аллоцирует.** `EntityCollection<E>` отдаёт `public struct Enumerator` из публичного `GetEnumerator()`, но `_source` типизирован интерфейсом `IReadOnlyEntityCollection<E>`, который выставляет только `IEnumerator<E>`. Поэтому `foreach (E entity in _source)` в `EntitySystemBase.Enable()`, `Disable()` и в `PriorityEntitySystem.RecalculatePriorities()` упаковывает энумератор. Для `Enable`/`Disable` это один раз за жизнь системы — неважно. Для `RecalculatePriorities` это **одна аллокация каждые `cooldown` секунд**, плюс сам O(n)-проход с вызовом `EvaluatePriority` на каждую сущность коллекции. Это ещё одна причина ставить `cooldown = 0` и работать триггерами.

**Вывод: `highPercent`/`midPercent` не зажимаются, и Low можно заморить голодом.** `lowQuota = batchSize - highQuota - midQuota`; перетекание квот аддитивное: `midBudget = midQuota + max(0, highQuota - highProcessed)`, `lowBudget = lowQuota + max(0, midBudget - midProcessed)`. Сумма процентов больше 100 делает `lowQuota` отрицательным, и `if (_lowEntityCount > 0 && lowBudget > 0)` просто пропустит корзину навсегда. Свойство `Settings.lowPercent` в пути обновления вообще не используется — мёртвый код. При этом `Docs/Entities/Systems/PriorityEntitySystem.md:84` утверждает обратное: «`highPercent` and `midPercent`` are clamped so their sum does not exceed `100``». В коде такого зажима нет нигде.

**В исходнике: `ProfilerMarker` в UI-слое статические, в системах — на экземпляр.** `EntityView<E>` держит `static readonly ProfilerMarker s_activateMarker/s_deactivateMarker` на закрытый generic, и **и поля, и обрамляющие `using`-блоки** лежат внутри `#if ENABLE_PROFILER` — в непрофилируемой сборке стоимости нет вообще. `EntityCollectionView<K,E,V>` тоже держит статические `s_addMarker`/`s_removeMarker`, а `EntityViewPool<K,E,V>` — `s_rentMarker`/`s_returnMarker`, но у этих двух типов ни поля, ни `using`-блоки под `#if ENABLE_PROFILER` **не** спрятаны (``EntityViewPool`1.cs:17-18,144,162``). `EntitySystemBase<E>` создаёт маркер на **каждый экземпляр** в конструкторе — `new ProfilerMarker(this.GetType().Name + ".Update")` под `#if ENABLE_PROFILER` — то есть конкатенация строки и отдельный маркер на систему, а не статический на закрытый generic.

**В исходнике: ни одного `foreach` по `List<T>` и ни одного LINQ в этой области.** Проход по инсталляторам в `EntityView<E>.Activate`/`Deactivate`, по каталогам в `EntityViewPool.Register`/`Unregister`/`RegisterCatalogs` — всюду `for (int i = 0, count = X.Count; i < count; i++)` с поднятым `count` и Unity-null-проверкой `if (installer)`.

**В исходнике: гизмо-диспетчер не аллоцирует массив.** `EntityView<E>.OnGizmosDraw` обходит поведения по индексу — `for (int i = 0, count = _entity.BehaviourCount; i < count; i++) if (_entity.GetBehaviourAt(i) is IEntityGizmos gizmos)` — вместо `GetBehaviours<T>()`, который выделял бы массив на каждый редакторный кадр. У публичного `Extensions.DrawGizmos(this IEntity)` та же схема, но без поднятия `count` в локаль: `entity.BehaviourCount` читается каждую итерацию.

**В исходнике: `EntityCollectionView.Clear()` мутирует во время обхода через `ArrayPool`.** `private static readonly ArrayPool<E> s_arrayPool = ArrayPool<E>.Shared`; `Rent(viewCount)`, `_views.Keys.CopyTo(buffer, 0)`, удаление по копии в `try`, `s_arrayPool.Return(buffer)` в `finally`, плюс ранний выход при `Count == 0`. В стабильном режиме аллокаций нет.

**Вывод: `Return(buffer)` без `clearArray: true` удерживает ссылки.** `s_arrayPool.Return(buffer)` вызывается без второго аргумента (``EntityCollectionView`1.cs:151``), поэтому буфер общего пула сохраняет сильные ссылки на удалённые сущности до того момента, когда его снова арендуют и перезапишут. Для долгоживущих сущностей это задержка сборки, а не утечка, но при «очистил сцену и ждём освобождения памяти» это заметно.

**Вывод: `EntitySystem<E>` удерживает удалённую сущность в освободившемся слоте.** `OnRemoveEntity` делает swap и декремент счётчика, но **не** пишет `_entities[last] = default` (`EntitySystem.cs:124-135`). Слот держит живую ссылку, пока его не перезапишет следующее добавление; очищает массив только `Dispose()` через `Array.Clear`. `PriorityEntitySystem<E>.RemoveInternal` ведёт себя иначе — там `array[last] = default` есть (строки 252, 273, 295). Это реальная разница в поведении GC между двумя базами.

**Вывод: `PriorityEntitySystem` никогда не снимает `SetAction` с триггеров.** `OnEnable` ставит `trigger.SetAction(this.ChangePriority)`, а `OnDisable`-переопределения в классе нет и `Dispose()` триггеры не трогает. Делегат на `this.ChangePriority` остаётся в каждом триггере после `Disable()`/`Dispose()`. `Untrack` в этом смысле лучше: `Disable()` базового класса проходит по `_source` и вызывает `OnRemoveEntity`, который растрекивает каждую сущность, **ещё лежащую в коллекции**, — тех, кто успел уйти из коллекции раньше, не растрекает никто. Отдельно: `SetAction(null)` вызвать и нельзя — все реализации триггеров бросают на этом `ArgumentNullException`.

**Вывод: `EntityWorldView.Activate` аллоцирует энумератор, и это тот же случай.** `foreach (E entity in _source)` по интерфейсу. Один раз на привязку — несущественно, упоминаю для полноты.

**Вывод: `ValueKey<E,T>` против строки — это разница в хеше строки.** `view.Entity.GetValue<T>(key.Id)` — поиск по `int`. Строковая форма идёт через `EntityKeyStore.NameToId`, то есть `Dictionary<string,int>.TryGetValue` — хеш строки плюс сравнение на каждый вызов (`EntityKeyStore.cs:40-52`). Аллокаций нет, но в цикле по тысячам сущностей это считаемая разница. Отдельно: `EntityKeyStore` — глобальное изменяемое статическое состояние на двух несинхронизированных словарях.

**Вывод: значения сущности хранятся как `object`, и struct-значения упакованы уже на входе.** `ValueSlot.value` — `object` (`Entities/Core/Entity_Values.cs`), а `IEntity` выставляет только `AddValue(int, object)`/`SetValue(int, object)`. Собственного struct-перегруза `AddValue<T>(int, T) where T : struct` в `IEntity`/`MonoEntity` нет. Поэтому `MonoEntityProxy<E>.AddValue<T>(int key, T value) where T : struct => _source.AddValue(key, value)` (``MonoEntityProxy`1.cs:135``) **не** является перегрузкой без упаковки — она разрешается в `AddValue(int, object)` и упаковывает. Реальный путь без копии — `ref T GetValueUnsafe<T>(int key) where T : class`, и он только для ссылочных типов.

**Вывод: `[EditModeEntityBehaviour]` — reflection на каждую проверку.** `obj.GetType().IsDefined(...)` без кеша, по одному вызову на каждое поведение в каждом `InitInEditMode`/`EnableInEditMode`/`DisableInEditMode`/`DisposeInEditMode`. Это редакторный путь, в рантайме не исполняется, но при `autoCompile` на сцене с сотнями `MonoEntity` каждый `OnValidate` будет заметен.

**Измеримый факт про тесты: UI-слой не покрыт вообще.** `grep -c '^\s*\[Test\]'` даёт 0 по всем файлам `Tests/PlayMode/EntityView`, `EntityViewPool`, `EntityViewCatalog`, `EntityViewCollection` — больше пятидесяти тестовых методов, и все закомментированы. Живыми в этой области остались только `Tests/PlayMode/SceneEntityProxy` (4 теста) и `Tests/EditMode/System` (`EntitySystemTests` — 50, `PriorityEntitySystemTests` — 65). Хуже того, закомментированный код ссылается на **прошлую** версию API (`_pool.RegisterPrefab(key, prefab)`, `_pool.Return(key, view)`, однопараметрический `_pool.Rent(key)`, `view.overrideName`, `view.customName`, `catalog.GetPrefab(string)`, `catalog.GetEntityName(view)`, `List<SceneEntityInstaller>`). Если при миграции кто-то пойдёт за «предполагаемым использованием» в эти файлы, он получит API, которого больше нет.

---

## Грабли

1. **`_batchSize` стартует с 0.** Первый `Update` любой системы обрабатывает ноль сущностей, дальше рост по `stepUp` за кадр. С дефолтами до `minSize` — 4 кадра. С маленьким `minSize` разгон виден глазами. После одного перебора бюджета батч падает до `_batchSize / scaleDown` (но не ниже `minSize`).

2. **`highPercent`/`midPercent` не зажимаются, вопреки документации.** `Docs/Entities/Systems/PriorityEntitySystem.md:84` обещает зажим до 100; в коде зажима нет. Сумма больше 100 → `lowQuota` отрицательный → корзина Low не обновляется никогда. Валидируйте настройки сами, желательно в `OnValidate` сериализованного `Settings`.

3. **`PriorityEntitySystem.Settings` скрывает базовый тип.** Объявлен `public new class Settings : EntitySystemBase<E>.Settings`. Передать базовый `EntitySystemBase<E>.Settings` в конструктор приоритетной системы не получится, а сериализованное поле, объявленное базовым типом, не донесёт `cooldown`/`highPercent`/`midPercent`.

4. **В `EntitySystem<E>` хуков add/remove нет.** `OnUpdate`, `OnAddEntity`, `OnRemoveEntity` — `sealed override`. Нужен per-entity кеш на добавлении — наследуйтесь от `EntitySystemBase<E>` и пишите своё хранилище. То же в `PriorityEntitySystem<E>`.

5. **`Disable()` вызывает `OnRemoveEntity` по `_source`, а не по тому, что система действительно трекает.** Если сущность ушла из коллекции так, что система этого не увидела, её трекинг не вычистится. Симметрично `Enable()` вызывает `OnAddEntity` для всей коллекции, и от повторного входа защищает только флаг `_enabled`.

6. **`OnEnable()` вызывается ПОСЛЕ `OnAddEntity` для всех существующих сущностей.** `EntitySystemBase.Enable()`: сначала цикл `OnAddEntity`, потом подписка на события, и только затем `OnEnable()`. Для `PriorityEntitySystem` это значит, что `trigger.Track(entity)` для всех начальных сущностей происходит **до** `trigger.SetAction(this.ChangePriority)`. `ValueEntityTrigger.Track` подписывается на события и запомнит `_action == null` в момент срабатывания только если событие придёт до `SetAction` — тогда `_action.Invoke` даст `NullReferenceException`. С `InlineEntityTrigger` хуже: `Track(entity) => _track.Invoke(entity, _action)` передаст вашей лямбде `null` как действие, и подписка молча потеряется.

7. **Один экземпляр триггера нельзя делить между двумя системами.** `SetAction` просто перезаписывает поле: вторая система, вызвав `Enable()`, перенаправит колбэк на себя, и первая перестанет получать изменения приоритетов. Один триггер — одна система.

8. **`ValueEntityTrigger<E>()` с дефолтами реагирует на любое значение.** Три флага по умолчанию `true`, и подписка идёт на `OnValueAdded`/`OnValueDeleted`/`OnValueChanged` целиком, без фильтра по ключу. Любая запись `Position` вызовет `EvaluatePriority`. Выключайте лишние флаги или берите `TagEntityTrigger`/`InlineEntityTrigger`.

9. **`RecalculatePriorities()` идёт по `_source`, а не по трекаемым.** O(n) по всей коллекции каждые `cooldown` секунд, независимо от `batchSize`, плюс упакованный энумератор на проход. Для больших миров — `cooldown = 0` и триггеры.

10. **Триггеры не отвязываются на `Disable`/`Dispose`.** `SetAction` снимается нигде, `OnDisable`-переопределения нет, `Dispose()` триггеры не трогает. Переиспользуя триггер после пересоздания системы, вы получите делегат на мёртвый объект.

11. **`AddTickSystem` и родственники не делают систему поведением.** Это ровно четыре `When*`-подписки. Система не появится в `GetBehaviours()`, её не найти через `GetBehaviour<T>()` и не снять через `DelBehaviour`. Возвращаемые подписки отбрасываются — отписаться через этот путь невозможно, время жизни системы равно времени жизни контекста. Нужен другой срок — подписывайтесь вручную и держите `TickSubscription` и остальные (идиома 4).

12. **Вызвать два или три `Add*TickSystem` с одним экземпляром — ошибка.** `Enable`/`Disable`/`Dispose` подпишутся по разу на фазу, а `Update` будет вызван в каждой фазе кадра. Пример в `Docs/Entities/Systems/Manual.md:87-89` показывает варианты, а не рецепт.

13. **У `EntityView<E>` нет `Name`.** Он есть только у не-generic `EntityView`. `protected override string GetKey(UnitView view) => view.Name;` при пустом `UnitView : EntityView<IUnitEntity>` не скомпилируется — несмотря на то, что именно так написано в документации. Объявляйте `Name` в базе своего семейства.

14. **Ключеваться по `view.name` нельзя.** `Activate` переписывает `GameObject.name` на `FormateName(entity)`, `Deactivate` его не восстанавливает, а `Return` вызывает `GetKey(view)` после `Deactivate`. Вид уйдёт в стек под ключом вида «Knight:17».

15. **`EntityViewPool<K,E,V>.Awake` из вашей сборки не переопределяется.** Он `private protected virtual`, а `private protected` — это «своя сборка **и** наследник»: фреймворк лежит в `Atomic.Entities.asmdef`, поэтому наследник из игровой сборки этот метод ни видеть, ни переопределить не может. Объявив в наследнике собственный `Awake`, вы базовый **скроете**, и `RegisterCatalogs()` молча не выполнится. После этого любой `Rent` на незарегистрированном ключе бросит `KeyNotFoundException`. Инициализацию наследника кладите в `Start`/`OnEnable`.

16. **`Register(K, V)` бросает на дубль, `Unregister(K)` молчит на промах.** `_prefabs.Add` против `_prefabs.Remove`. Два каталога с префабами, дающими один `GetKey`, или каталог, зарегистрированный дважды, уронят `Awake` с `ArgumentException`.

17. **`Rent` на пустом стеке молча инстанцирует.** Пул никогда не «кончается» и не предупреждает — он просто аллоцирует. Размеряйте через `Init`/`InitAsync` и смотрите маркер `EntityViewPool<...>.Rent`; ровный поток `Instantiate` означает недостаточный прогрев. Отдельно: на этом пути `Instantiate(prefab, parent)` вызывается **без** последующего `SetActive(true)` — если префаб вида выключен, арендованный вид останется выключенным.

18. **`Return` кладёт вид в стек ДО проверки на `null`.** ``EntityViewPool`1.cs:160-173``: `pool.Push(view);` и только потом `if (view) { ... }`. Уничтоженный вид попадёт в пул, и следующий `Rent` выдаст мёртвую ссылку, на которой разыменуется `view.transform`. Никогда не `Destroy`-йте вид, который ещё трекает collection view; вызывайте `Remove(entity)`.

19. **`Clear()` не чистит регистрации префабов.** Он обходит `_pools.Values`, `Destroy`-ит все пулинутые `view.gameObject`, чистит стеки и `_pools`, но `_prefabs` не трогает. После `Clear()` следующий `Rent` не бросит, а заново заинстанцирует. И наоборот: `Clear()` уничтожает только **возвращённые** экземпляры — арендованные и активные пулу неизвестны. Чистите сначала collection/world view, потом пул.

20. **`Remove(V view)` на уже деактивированном виде падает.** `Remove(V view) => this.Remove(view.Entity)`, а после `Deactivate()` у вида `Entity == null`, и `Dictionary<E,V>.Remove(null)` бросает `ArgumentNullException`. Удаляйте по сущности.

21. **`OnRemoved` срабатывает ДО `Deactivate()` и возврата в пул, `OnAdded` — ПОСЛЕ `Activate`.** Обработчик `OnRemoved` ещё видит живой вид с непустой `Entity`, и это сделано намеренно. Но обработчик, который из события зайдёт обратно в `Add`/`Remove`, будет мутировать `_views` посреди операции.

22. **`EntityWorldView` никогда не вызывает `Deactivate()` сам.** Ни `OnDisable`, ни `OnDestroy` в файле нет. Если мир живёт дольше `GameObject`, его делегаты `OnAdded`/`OnRemoved` продолжают указывать на уничтоженный `MonoBehaviour`, и первое же добавление сущности упадёт на `_pool.Rent`. Вызывайте `Deactivate()` на `OnDestroy` или при выгрузке сцены.

23. **`EntityWorldView` и `EntityCollectionView` висят в одном пункте меню.** Оба объявляют `[AddComponentMenu("Atomic/Entities/Entity Collection View")]`, и документация `EntityWorldView` тоже отправляет вас в «Atomic/Entities/Entity Collection View». Легко добавить ручной collection view и потом удивляться, что виды не появляются. Мировой — тот, у которого есть `Activate(IReadOnlyEntityCollection<E>)`; выбирайте по имени скрипта в поиске Add Component.

24. **Серилизованное поле пула называется `_pool`, а в документации — `pool`.** В исходнике `[Space, SerializeField] private EntityViewPool<K,E,V> _pool;` рядом с `private Transform viewport;` без подчёркивания. В инспекторе Unity покажет «Pool», в документации (``Docs/Entities/UI/EntityCollectionView`3.md:105``) он назван `pool`. На поиск по проекту это влияет.

25. **Не-generic `EntityWorldViewSingleton` — не наследник generic-синглтона.** Он наследуется напрямую от `EntityWorldView<string, IEntity, EntityView>`, а код синглтона скопирован. Два `_instance` никак не связаны: `EntityWorldViewSingleton.Instance` никогда не найдёт наследника generic-версии и наоборот.

26. **Статика generic-синглтона живёт на закрытом generic-типе.** Два разных наследника, закрывающих один и тот же `<K,E,V>`, будут драться за один слот, а `Instance` типизирован базовым закрытым generic — потребителю придётся кастовать. `Awake` там `private void`, не виртуальный: собственный `Awake` в наследнике скроет его и синглтон не зарегистрируется, а исправить переопределением нельзя. Предпочитайте сериализованные ссылки.

27. **`Instance` не найдёт неактивный объект и бросает голый `Exception`.** `FindAnyObjectByType<...>(FindObjectsInactive.Exclude)`. Для опциональных поисков есть `TryGetInstance`.

28. **`MonoEntityInstaller.Uninstall` — пустая виртуалка.** Инсталлятор вида без переопределения `Uninstall` оставляет поведения на каждой сущности, которую этот пулящийся вид когда-либо рисовал. Фреймворк зовёт `Uninstall` на каждом `Deactivate`, так что пропуск абсолютно бесшумен. `MonoEntityInstallerConfigurable` — готовый пример этого сбоя: он `sealed` и `Uninstall` не переопределяет, а починить нельзя, потому что в `IEntityInstaller` метода `Uninstall` нет вовсе.

29. **Пустой слот в `installers` даёт `Debug.LogWarning` на каждую активацию.** Не fail-fast, а предупреждение на спавн. С пулящимся видом это предупреждение на каждую пулю. Держите список чистым.

30. **`FormateName` аллоцирует строку на каждый `Activate`.** `string.Format("{0}:{1}", entity.Name, entity.InstanceID)` плюс сеттер `GameObject.name`, который сам managed→native. Для пулов с высокой текучкой переопределяйте (идиома 18). Имя метода в API написано с опечаткой — `FormateName`, не `FormatName`.

31. **Гизмо-поведения компилируются в плеерный билд.** `Behaviours/IEntityGizmos.cs` под `#if UNITY_5_3_OR_NEWER`, не под `#if UNITY_EDITOR`. Пример из ``Docs/Entities/UI/EntityView`1.md:141-149`` вызывает `Handles.DrawWireDisc` из незащищённого класса — в сборке `UnityEditor.Handles` не существует, и билд ломается. Оборачивайте `#if UNITY_EDITOR`.

32. **Гизмо-диспетчер проглатывает исключения.** `EntityView<E>.OnGizmosDraw` ловит всё и пишет `Debug.LogWarning($"Ops: detected exception in gizmos: {e.Message}")` — без стектрейса. Сломанное гизмо выглядит как «ничего не рисуется».

33. **При `_onlySelectedGizmos == false` и выделенном объекте гизмо рисуются дважды.** `OnDrawGizmos()` вызывает `OnDrawGizmosSelected()`, когда флаг снят, и Unity отдельно вызывает `OnDrawGizmosSelected()` на выделении. Для идемпотентного рисования это незаметно, для `Handles` с прозрачностью или для накапливающих счётчиков — нет.

34. **`[EntityInspectorValue]` имеет два конструктора с `Type` первым параметром.** `(valueType)` и `(entityType, valueType)`. `[EntityInspectorValue(typeof(IGameEntity))]` компилируется и означает «значения типа `IGameEntity`» — не то, что задумано. Компилятор не поможет.

35. **В дропдаун попадает имя поля, а не строка из конструктора ключа.** Кеш берёт `field.Name`. `public static readonly ValueKey<IGameEntity,int> Health = new("hp")` даст в инспекторе «Health», а в рантайме этот выбор превратится в `NameToId("Health")` — другой ключ. Всегда `nameof`.

36. **Кеш инспектора видит только `public static` поля и обновляется на перезагрузке домена.** `GetFields(BindingFlags.Public | BindingFlags.Static)` в статическом конструкторе `[InitializeOnLoad]`-класса. Свойства и `internal`-поля в список не попадут, а новый `[EntityInspectorAPI]`-класс появится в дропдаунах только после перекомпиляции.

37. **Фильтр по типу сущности расширяющий, а не сужающий.** `GetTagKeys(entityType)` отдаёт всё, для чего `kvp.Key.IsAssignableFrom(entityType)`. `[EntityInspectorTag(typeof(IGameEntity))]` покажет и `TagKey<IGameEntity>`, и все не-generic `TagKey`; голый `[EntityInspectorTag]` запрашивает `IEntity` и `TagKey<IGameEntity>` **не** покажет. И в `Matches` ветка про открытые generic-определения проверяет кешированный тип, а не запрошенный, так что `[EntityInspectorValue(typeof(IVariable<>))]` не совпадёт ни с одним ключом: список окажется пустым, и Odin через `CallNextDrawer` нарисует обычное текстовое поле вместо дропдауна. Нужен закрытый тип.

38. **Дропдауны требуют Odin.** `EntityInspectorCache` под `#if UNITY_EDITOR`, но оба драйвера — под `#if UNITY_EDITOR && ODIN_INSPECTOR`. Без Odin атрибуты не делают ничего видимого. Значение в любом случае остаётся `string` в рантайме.

39. **`[EditModeEntityBehaviour]` работает только для `MonoEntity`.** Единственный потребитель — `MonoEntity_Editor.cs`. Для чистых `Entity` и для сущностей, которые рисует `EntityView`, атрибут не делает ничего.

40. **В Edit Mode `Disable`/`Dispose` помеченных поведений никогда не вызываются.** `InitInEditMode` и `EnableInEditMode` не выставляют `_initialized`/`_enabled` в `true` (они ставятся только в рантайм-`Init()`/`Enable()`), поэтому `DisableInEditMode` (`if (!_enabled) return;`) и `DisposeInEditMode` (`if (!_initialized) return;`) всегда выходят сразу. Следствие: каждый `Compile()` заново прогоняет `Init`/`Enable` без парного свёртывания, и всё, на что поведение подписалось или что выделило в `Init`, накапливается от перекомпиляции к перекомпиляции. Документация (`Docs/Entities/EditMode/EditModeEntityBehaviourAttribute.md:53`) утверждает «Applies to `Init`, `Enable`, `Disable`, and `Dispose` methods» — на практике применяются только первые два.

41. **`[EditModeEntityBehaviour]` наследуется.** `AttributeUsage` без `Inherited` даёт `Inherited = true`, а `IsDefined` по умолчанию идёт по цепочке базовых классов. Атрибут на общей базе поведений включит edit-mode для всех наследников.

42. **`[EditModeEntityBehaviour]` — не Unity'ный `[ExecuteAlways]`.** Это маркер фреймворка; с `[ExecuteAlways]`/`[RunInEditMode]` он не связан и их не заменяет.

43. **`MonoEntityProxy.Dispose()` уничтожает исходную сущность.** `Init`/`Enable`/`Disable`/`Dispose` пробрасываются в `_source` (``MonoEntityProxy`1.cs:239-242``). Прокси — это `IEntity`, а `IEntity : IDisposable`; любой код, который «на всякий случай» зовёт `Dispose` на `IDisposable`, или `using` над прокси убьёт реальную `MonoEntity`.

44. **`MonoEntityProxy.Equals` разыменовывает `_source` без проверки.** Непривязанный прокси упадёт на любой вставке в словарь или сравнении. `Reset()` подтягивает `_source` из родительской цепочки только при первом добавлении компонента в редакторе.

45. **`MonoEntityProxy<E>.AddValue<T>/SetValue<T> where T : struct` не избавляют от упаковки.** Они форвардят в `AddValue(int, object)` / `SetValue(int, object)`, потому что собственных struct-перегрузок у `IEntity`/`MonoEntity` нет. Значения сущности хранятся как `object`, и struct упакуется на входе.

46. **`GetValueUnsafe` теряет `ref` в типизированной форме.** `GetValueUnsafe<T>(this IEntity, ValueKey<T>)` возвращает `ref T`, а `GetValueUnsafe<E,T>(this E, ValueKey<E,T>)` — просто `T` (`Extensions_Values.cs:142,146`). Если нужен `ref`, берите не типизированную по сущности форму.

47. **Папки `Scripts/Proxy` не существует.** `MonoEntityProxy.cs` и ``MonoEntityProxy`1.cs`` лежат в `Entities/Scripts/Entities/Unity/`.

48. **Закомментированные UI-тесты описывают прошлое API.** Больше пятидесяти тестов в `Tests/PlayMode/EntityView*`/`EntityViewCollection` закомментированы и ссылаются на `RegisterPrefab`, `Return(key, view)`, однопараметрический `Rent(key)`, `view.overrideName`, `view.customName`, `catalog.GetPrefab(string)`, `catalog.GetEntityName(view)`, `List<SceneEntityInstaller>` — всего этого больше нет. Не используйте эти файлы как справку по предполагаемому использованию.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`. Документации по этой области — 24 markdown-файла плюс один best-practices; все прочитаны целиком.

### Системы

| Файл | Что внутри |
|---|---|
| `Docs/Entities/Systems/Manual.md` | Входная точка (110 строк). Три примера: round-robin `MovementSystem`, приоритетная `AIPrioritySystem` и раздел **Wiring to a Context** со вложенным инициализатором `batching = { minSize = 64, maxSize = 512 }` и всеми тремя `Add*TickSystem` подряд (строки 87-89 — именно то перечисление, которое легко прочитать как рецепт). Заканчивается навигационным индексом и пятью строками Best Practices (106-110), из которых одна про скорость per-entity логики, одна про тюнинг `frameBudget`/`batching` по платформам и одна — про триггеры вместо ежекадрового пересчёта. |
| `Docs/Entities/Systems/EntitySystemBase.md` | Самая полная справка по базе (193 строки): тип, конструктор, постатейный разбор `Enable`/`Disable`/`Update`/`OnUpdate`/`OnEnable`/`OnDisable`/`OnAddEntity`/`OnRemoveEntity`/`Dispose`. В примере (строки 29-52) `MySystem : EntitySystemBase<IGameEntity>` с пустыми `OnUpdate`/`Dispose` и конструирование через `new EntitySystemBase<IGameEntity>.Settings { frameBudget = 0.02f }` — полезно как шаблон, когда нужно своё хранилище. Про старт `_batchSize` с нуля здесь не сказано. |
| `Docs/Entities/Systems/EntitySystem.md` | 100 строк про round-robin-базу. Пример (строки 22-47) показывает `Update` через строковые ключи (`entity.GetValue<Vector3>("velocity")`) — именно та форма, которую стоит заменить на `ValueKey<E,T>`. Раздел Methods фиксирует `sealed override` на трёх хуках. |
| `Docs/Entities/Systems/PriorityEntitySystem.md` | 155 строк. Пример (строки 25-55) полный и корректный: конструктор с `params IEntityTrigger<IGameEntity>[]`, трёхуровневый `EvaluatePriority` с ранними возвратами, `Update`, и внизу — создание `ValueEntityTrigger<IGameEntity>` и `AddTickSystem`. Таблица `Settings` на строках 77-84 содержит ту самую неверную фразу про зажим `highPercent`/`midPercent` до 100. |
| `Docs/Entities/Systems/EntityUpdatePriority.md` | 86 строк, самый короткий файл раздела. Три подраздела полей (56-81) и пример использования (23-36) — тот самый `DistancePrioritySystem` с одним `EvaluatePriority`, без конструктора и без `Update`, который не компилируется (см. идиому 2). Ошибочную формулировку XML-дока про «updated before lower-priority systems» этот файл **не** повторяет: на строке 51 сказано корректное «Higher priority entities receive a larger share of the per-frame update budget». |
| `Docs/Entities/Systems/Extensions.md` | 155 строк и единственное место, где расписаны все три `Add*TickSystem` по отдельности с примерами для Tick и FixedTick. Раздел **Notes** (строки 151-155) — три фразы, которые стоит знать: методы не добавляют систему как поведение, владеющий контекст обязан быть прокачан соответствующим методом, система уничтожается вместе с контекстом. Про отсутствие отписки не сказано. |
| `Docs/BestPractices/EntitySystem.md` | 598 строк, сквозной пример «модель + вид» от интерфейса сущности до привязки collection view. Самый полезный и самый опасный файл области. Полезный: разделы **Entity Visualization** (строки 365-525) и **Binding the View Collection to the Entity World** (526-566) показывают именно тот набор пяти типизированных объявлений, который надо копировать. Опасный: раздел **Entity System Setup** (строки 254-300) построен на `context.AddEntityPool(...)`, `context.AddEntityWorld(...)`, `GameContext.Instance.GetEntityWorld()` и на `new MultiEntityPool<string, IUnitEntity>(factoryCatalog)` — ни одного из этих членов во фреймворке нет, это кодогенерация проекта-примера. Читайте за структурой, не за API. |

### Представления

| Файл | Что внутри |
|---|---|
| `Docs/Entities/UI/Manual.md` | 287 строк, обзор всего UI-слоя пятью нумерованными разделами: `EntityView` (27-95), `EntityViewCatalog` (96-121), `EntityViewPool` (122-168), `EntityCollectionView` (169-224), `EntityWorldView` (225-248). Разделы Notes (273-283) и Best Practices (285-287) короткие; Best Practices — одна ссылка на `BestPractices/EntitySystem.md`. Это файл, с которого стоит начинать знакомство со слоем. |
| ``Docs/Entities/UI/EntityView`1.md`` | 320 строк — самый объёмный файл области и основной справочник по виду. Три примера: **View Setup** (39-107), **Entity Rendering** с `Activate`/`Deactivate` (108-133) и **Gizmos Support** (134-179). Гизмо-пример (141-149) — тот, который ломает плеерный билд. Раздел **Inspector Settings** (180-198) объясняет `installers`, `_onlySelectedGizmos` и `_onlyEditModeGizmos`. Раздел **Extension Methods** (293-315) — единственное место, где документирован `GetValue<E,T>(this EntityView<E>, ValueKey<E,T>)`. |
| `Docs/Entities/UI/EntityView.md` | 105 строк про не-generic обёртку. Главное здесь — раздел Properties (41-70), где `Name` объявлен как `[field: SerializeField] public string Name { get; private set; }`: именно отсюда видно, что `Name` есть только у не-generic версии. Раздел Methods (71-101) полезно детальный: расписывает порядок шагов внутри `Activate` и `Deactivate`, включая формат имени `{entity.Name}:{entity.InstanceID}`. |
| ``Docs/Entities/UI/EntityViewCatalog`2.md`` | 114 строк. Два примера — **Custom Catalog** с `[CreateAssetMenu(fileName = "UnitViewCatalog", menuName = "Example/New UnitViewCatalog")]` (28-55) и **Prefab Lookup** по индексу (56-66). Сюда идти, когда надо вспомнить, что `[CreateAssetMenu]` типизированному каталогу нужно дописать самому. |
| `Docs/Entities/UI/EntityViewCatalog.md` | 62 строки, самый короткий файл UI-раздела. Как создать ассет (`Assets → Create → Atomic → Entities → EntityViewCatalog`) и как использовать его из кода. |
| ``Docs/Entities/UI/EntityViewPool`3.md`` | 198 строк, полная справка по пулу. Примеры **Custom Pool** (36-60) и **Preloading** (61-74) с `pool.Init("Knight", 20)` / `await pool.InitAsync("Knight", 20)`. Раздел **Inspector Settings** (75-84) — `container`, `initialCapacity`, `catalogs`. Раздел Notes (184-192) фиксирует три факта: `Rent`/`Return` internal и могут быть выставлены наружу своим пулом, `Stack<V>` создаётся на ключ по требованию, возвращённые виды репарентятся в `container` и деактивируются. Про `Push` до null-проверки, про асимметрию `Register`/`Unregister` и про то, что `Clear` не чистит `_prefabs`, не сказано. |
| `Docs/Entities/UI/EntityViewPool.md` | 93 строки про не-generic пул. Раздел Notes (81-87) прямо пишет, что ключом служит `Name` вида. Это стоит прочитать, прежде чем закрывать generic'и своим типом вида без `Name`. |
| ``Docs/Entities/UI/EntityCollectionView`3.md`` | 241 строка, справка по ручной карте «сущность → вид». Примеры **Custom Collection View** (40-64) и **Managing Views** (65-99). Раздел **Inspector Settings** (100-107) называет поля `viewport` и `pool` (в коде второе — `_pool`). Разделы Events (127-146) и Methods (159-236) — единственное место, где порядок `OnAdded`/`OnRemoved` относительно `Activate`/`Deactivate` описан прозой. |
| `Docs/Entities/UI/EntityCollectionView.md` | 91 строка. **Scene Setup** (24-36) и **Managing Views** (37-71) — тот самый пример с `Contains` + `TryGet` + `Get` и с `foreach (KeyValuePair<IEntity, EntityView> pair in collectionView)`. Копировать стоит именно `foreach`: объявление переменной цикла конкретным `KeyValuePair<,>` здесь не косметика. Остальное — не рецепт: на строках 47-50 пример зовёт `Remove(someEntity)`, а затем `Remove(createdView)` по уже деактивированному виду, то есть ровно то, что падает с `ArgumentNullException` (см. «Грабли», пункт 20). |
| ``Docs/Entities/UI/EntityWorldView`3.md`` | 134 строки. **Custom World View** (29-53) и **Activation and Deactivation** (54-68). Раздел Methods (99-128) описывает, что `Activate` подписывается на `OnAdded`/`OnRemoved` и сразу добавляет всё существующее, а `Deactivate` чистит и отписывается. Что ни `OnDisable`, ни `OnDestroy` этого не делают, — не сказано. |
| `Docs/Entities/UI/EntityWorldView.md` | 69 строк. **Scene Setup** (24-34) отправляет добавлять компонент через «Atomic/Entities/Entity Collection View» — ровно то совпадение пунктов меню, из-за которого легко поставить ручной collection view вместо мирового. |
| ``Docs/Entities/UI/EntityWorldViewSingleton`3.md`` | 131 строка. **Custom Singleton World View** (30-54) и **Accessing the Singleton** (55-70). Раздел **Inspector Settings** (71-82) — единственное место, где в одной таблице собраны `dontDestroyOnLoad` и унаследованные `viewport`/`pool`. Про `private void Awake`, про статику на закрытом generic'е и про голый `Exception` из `Instance` не сказано. |
| `Docs/Entities/UI/EntityWorldViewSingleton.md` | 98 строк про не-generic синглтон. Разделы Properties (68-80) и Methods (81-93) документируют `Instance` и `TryGetInstance`. Что этот класс наследуется **не** от generic-синглтона, в документации не отражено нигде — это видно только в исходнике. |

### Инспектор и Edit Mode

| Файл | Что внутри |
|---|---|
| `Docs/Entities/Inspector/Manual.md` | 86 строк и лучшая отправная точка по трём атрибутам. Два примера: **Mark an API Class** (22-38) с `[EntityInspectorAPI] public static partial class GameEntityAPI` и **Use Dropdowns in Behaviours** (39-72) с `[EntityInspectorTag] string targetTag` + `[EntityInspectorValue(typeof(IVariable<int>))] string healthKey` и установкой через `entity.AddTag(targetTag)` / `entity.AddValue(healthKey, new Variable<int>(100))`. Best Practices (81-86) содержат важнейшее требование валидировать строковые имена в рантайме. Что в дропдаун уходит имя поля, а не строка из конструктора, не сказано. |
| `Docs/Entities/Inspector/EntityInspectorAPIAttribute.md` | 48 строк: пустой маркер, пример с `TagKey`/`ValueKey` полями, раздел Type. Про `BindingFlags.Public | Static` и про перезагрузку домена — ничего. |
| `Docs/Entities/Inspector/EntityInspectorTagAttribute.md` | 99 строк. Единственное место, где **оба конструктора** (беспараметрический и с `Type`) расписаны отдельными подразделами (69-89), и где задокументировано `public readonly Type entityType` как поле, а не свойство (90-99). |
| `Docs/Entities/Inspector/EntityInspectorValueAttribute.md` | 112 строк. Разделы Constructors (70-93) и Properties (94-112) — сюда идти, чтобы не перепутать одноаргументную форму `(valueType)` с двухаргументной `(entityType, valueType)`. Что обе принимают `Type` первым параметром и поэтому `[EntityInspectorValue(typeof(IGameEntity))]` компилируется с другим смыслом, прямым текстом не сказано. |
| `Docs/Entities/EditMode/EditModeEntityBehaviourAttribute.md` | 57 строк, единственный файл в `Docs/Entities/EditMode` (`Manual.md` там нет). Пример (15-36) — `[EditModeEntityBehaviour] public class InitColorBehaviour : IEntityInit`. Раздел Notes (49-57) утверждает «Lifecycle Coverage – Applies to `Init`, `Enable`, `Disable`, and `Dispose` methods» — в коде `Disable`/`Dispose` в Edit Mode не доходят до поведений никогда (см. «Грабли», пункт 40). Про reflection без кеша и про то, что потребитель — только `MonoEntity`, не сказано. |

### Исходники, когда документации не хватает

| Файл | Зачем открывать |
|---|---|
| `Assets/Plugins/Atomic/Entities/Scripts/Systems/EntitySystemBase.cs` | Строки 172-190 — весь механизм бюджета кадра и пересчёта `_batchSize`. Строки 114-156 — порядок внутри `Enable`/`Disable`, из которого следует, что `OnEnable` вызывается последним. |
| `.../Scripts/Systems/EntitySystem.cs` | Строки 104-147 — хранилище, рост, swap-with-last и отсутствие `_entities[last] = default`. |
| `.../Scripts/Systems/PriorityEntitySystem.cs` | Строки 102-115 — единственный конструктор. 120-126 — `OnEnable` с `_priorityTime = _settings.cooldown`. 337-415 — `UpdateEntities` с квотами и перетеканием. 417-443 — буфер команд. |
| `.../Scripts/Triggers/` | 14 файлов, шесть реализаций в generic- и не-generic-формах. ``ValueEntityTrigger`1.cs:45-50`` — почему дефолтный конструктор реагирует на любое значение. ``InlineEntityTrigger`1.cs:46-52`` — почему порядок `Track` и `SetAction` важен. |
| ``.../Scripts/UI/View/EntityView`1.cs`` | Строки 67-127 — точный порядок шагов `Activate`/`Deactivate`, обработка `null`-инсталляторов, `#if ENABLE_PROFILER` вокруг и полей, и `using`-блоков. |
| ``.../Scripts/UI/View/EntityView`1_Gizmos.cs`` | Модификаторы `private protected` на обоих гизмо-хуках и двойной вызов при снятом `_onlySelectedGizmos`. |
| ``.../Scripts/UI/Pool/EntityViewPool`1.cs`` | Строки 142-173 — `Rent` без `SetActive(true)` на пути инстанцирования и `Return` с `Push` до null-проверки. 89-100 — `Clear`, не чистящий `_prefabs`. |
| ``.../Scripts/UI/Collection/EntityCollectionView`1.cs`` | Строки 80-117 — порядок событий. 135-153 — `Clear` через `ArrayPool<E>.Shared` без `clearArray`. 118-130 — struct-энумератор и явные интерфейсные реализации. |
| ``.../Scripts/UI/World/EntityWorldView`1.cs`` | 62 строки целиком, из них класс — 16-61; видно, что `OnDisable`/`OnDestroy` отсутствуют. |
| `.../Scripts/Installers/MonoEntityInstallerConfigurable.cs` | 20 строк. `sealed`, `[SerializeReference] IEntityInstaller[]`, нет `Uninstall`. |
| `.../Scripts/Entities/Unity/MonoEntity_Editor.cs` | Весь edit-mode механизм: `Compile()` (31-60), раздача `onValidateCallback` инсталляторам (65-73), `PrecomputeCapacities` (80-85), четыре `*InEditMode` (90-157), из которых видно, что `_initialized`/`_enabled` там не выставляются. |
| `.../Entities/Editor/Inspector/EntityInspectorCache.cs` | Строки 142-190 — что именно сканируется и под каким ключом регистрируется. 86-108 — направление фильтрации через `IsAssignableFrom`. |
| `.../Scripts/Entities/Extensions_Tags.cs`, `Extensions_Values.cs` | Полный список строковых и ключевых перегрузок, без которых идиомы с `AddTag(string)` и `GetValue(ValueKey<E,T>)` не компилируются. |
| `.../Scripts/Entities/Extensions.cs` | 134-205 — `TryGetEntity` и `FindEntityInParent` по четырём источникам каждый. 209-215 — публичный `DrawGizmos(this IEntity)`. |
| `.../Scripts/World/Extensions.cs` | 19 строк. `BindTo<E>(this IEntityWorld<E> world, IEntity source)` (8-18) и его тело — те самые восемь `When*`, которые best practices выписывает руками. |
| `.../Entities/Tests/EditMode/System/` | `EntitySystemTests.cs` (50 живых тестов) и `PriorityEntitySystemTests.cs` (65) — единственное живое покрытие области. `EntitySystemTests.cs:86-94` — хелпер `WarmupBatchSize` с комментарием про старт с нуля. |
