# 11. Entities: инсталлеры, аспекты, бейкинг

Эта часть фреймворка отвечает за composition layer: как `IEntity` — контейнер тегов, значений и поведений — наполняется данными, и как авторинг в Unity (GameObject, компоненты, ассеты) переходит в чистую C#-модель сущности. Инсталлеры декларативно собирают сущность при инициализации (`Install`) и снимают подписки при разрушении (`Uninstall`). Аспекты делают ту же работу, но как обратимую дельту во время игры (`Apply` / `Discard`) — баффы, триггерные зоны, снаряжение. Бейкеры превращают авторинговые GameObject в нативные `Entity` и затем уничтожают или пулят GameObject, используя посчитанные в инспекторе capacity, чтобы в рантайме не было роста массивов. Bootstrap (`ScriptableEntityBootstrapper`) автоматически спавнит префабы `MonoEntity` в сцены, подходящие по regex, через `[RuntimeInitializeOnLoadMethod]` — это замена рукописному бутстрапу сцен.

Всё, что ниже, сверено с исходниками в `Assets/Plugins/Atomic`. Там, где upstream-документация расходится с кодом, в тексте стоит явная пометка; в документации этой области достаточно много устаревших и некомпилируемых примеров (см. «Грабли» и «Где читать»).

---

## Модель

**Сущность — это мешок.** `IEntity` хранит три набора: теги (`int`), значения (`int -> object`) и поведения (`IEntityBehaviour`). Базовый интерфейс целочисленный: в `IEntity_Tags.cs` объявлены `bool AddTag(int key)`, `bool HasTag(int key)`, `bool DelTag(int key)`; в `IEntity_Values.cs` — `void AddValue(int key, object value)`, `T GetValue<T>(int key)`, `void SetValue(int key, object value)`. Строковые перегрузки — это extension-методы, которые прогоняют имя через `EntityKeyStore.NameToId(string)`. То есть строки в примерах не «настоящий» API, а сахар над id.

**Четыре механизма, четыре разных жизненных цикла.**

| Механизм | Когда работает | Обратимость | Что делает с GameObject |
|---|---|---|---|
| Installer | один раз при инициализации (`Install`), `Uninstall` при разрушении | `Uninstall` — `virtual`, по умолчанию пустой | ничего |
| Aspect | в любой момент рантайма | `Discard` — `abstract`, забыть нельзя | ничего |
| Baker | один раз при загрузке уровня (`Bake`) | нет | по умолчанию `Destroy(gameObject)` |
| Bootstrapper | один раз на старте приложения | нет | инстанцирует префабы |

Разница в обратимости — осознанное решение фреймворка. У `MonoEntityInstaller.Uninstall(IEntity)` тело пустое и метод `virtual`: про него можно забыть, и компилятор не возразит. У `MonoEntityAspect<E>.Discard(E)` и `ScriptableEntityAspect<E>.Discard(E)` метод `abstract`: аспект без `Discard` просто не скомпилируется.

**Порядок установки — несущая конструкция.** `MonoEntity.Install()` (файл `Entities/Unity/MonoEntity_Installing.cs`) идёт так: все `scriptableInstallers`, потом все `sceneInstallers`, потом `OnInstall()`, потом событие `_onInstalled`. Именно поэтому работает двухслойный паттерн «ассет задаёт дефолты через `Add*`, сцена переопределяет через `Set*`»: ассет исполняется раньше.

`Uninstall()` — **не точное зеркало**. Он защищён `if (!_installed) return;`, сбрасывает флаг, вызывает `OnUninstall()` **до** циклов, затем `sceneInstallers`, затем `scriptableInstallers`, и только потом поднимает `_onUninstalled`. То есть порядок инсталлеров обратный, а позиция хука — нет: в `Install()` хук после циклов, в `Uninstall()` — до. Если `OnInstall`/`OnUninstall` переопределены в наследнике `MonoEntity`, это нужно держать в голове.

**`Add*` на этапе сборки, `Set*` на этапе override.** Инсталлер определяет форму сущности и только добавляет; baker-override мутирует уже существующие значения и только присваивает. Дисциплина важна механически, а не стилистически: capacity precompute считает именно количество `Add*`-вызовов, и повторный `Add` после precompute либо ломает посчитанную ёмкость, либо тихо перезаписывает значение.

**Инсталлеры исполняются в Edit Mode.** `MonoEntity.Compile()` (`Entities/Unity/MonoEntity_Editor.cs`) делает `Uninstall(); Construct(); Install(); PrecomputeCapacities();` и симулирует lifecycle. Он вызывается из `OnValidate()`, если включён флаг `autoCompile`, и из контекстного меню компонента. У бейкера свой `Compile()` (`Baking/MonoEntityBaker`1_Editor.cs`), который создаёт одноразовую preview-сущность. Любой побочный эффект в `Install` (Instantiate, разрешение синглтона, регистрация колбэка) обязан быть за `AtomicUtils.IsPlayMode()`.

**Типизация проверяется в рантайме.** `IEntityInstaller<E>`, `MonoEntityInstaller<E>`, `ScriptableEntityInstaller<E>` бросают `InvalidCastException` с многострочной диагностикой, если сущность не того типа. Неправильно подключённый в инспекторе инсталлер — это исключение в Play Mode, а не ошибка компиляции.

**Capacity precompute — смысл бейкинга.** `Entity`'s ctor принимает `tagCapacity/valueCapacity/behaviourCapacity`; бейкер хранит их в сериализованных полях и передаёт в `Create`. `Compile()` в Edit Mode создаёт preview-сущность и записывает обратно наблюдённые `TagCount/ValueCount/BehaviourCount`. В рантайме массивы выделяются один раз, при установке не растут. Но «ровно нужного размера» — не буквально: `ConstructTags` и `ConstructValues` (`Entities/Core/Entity_Tags.cs`, `Entity_Values.cs`) округляют ёмкость вверх — `_tagCapacity = CeilToPrime(capacity, out _tagPrimeIndex);` — потому что теги и значения живут в slot/bucket-массивах хеш-таблицы. Ровно по запросу выделяется только массив поведений: `ConstructBehaviours` — это `_behaviours = new IEntityBehaviour[capacity];`.

---

## API

### Инсталлеры: интерфейсы

```csharp
namespace Atomic.Entities
{
    public interface IEntityInstaller
    {
        void Install(IEntity entity);
    }
}
```
`Installers/IEntityInstaller.cs`. Единственный член — `Install`. **На интерфейсе нет `Uninstall`.** Это главное практическое следствие: вложенный `[Serializable] class Foo : IEntityInstaller` не получает колбэка на снятие. Если он подписывается — владеть `DisposableComposite` и вызывать `Dispose()` обязан внешний `MonoEntityInstaller`.

```csharp
public interface IEntityInstaller<in E> : IEntityInstaller where E : IEntity
{
    void Install(E entity);

    /// <inheritdoc />
    void IEntityInstaller.Install(IEntity entity)
    {
        if (entity is not E e)
            throw new InvalidCastException(
                $"[IEntityInstaller<{typeof(E).Name}>] Invalid entity type for {this.GetType().Name}.\n" +
                $"Expected: {typeof(E).FullName}\n" +
                $"Received: {entity?.GetType().FullName ?? "null"}\n" +
                "Please make sure the correct IEntityInstaller is used for this entity type."
            );

        this.Install(e);
    }
}
```
`Installers/IEntityInstaller`1.cs`. Контравариантный по `E`. Мост к нетипизированному `Install` сделан через **default interface implementation** (C# 8), поэтому каст писать не нужно — реализуется только `Install(E)`. Ограничение здесь `where E : IEntity`, **без** `class`, поэтому `E` может быть интерфейсом (`IGameEntity`) или структурой.

### Инсталлеры: базовые классы Unity

```csharp
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Entities/Installers/MonoEntityInstaller.md")]
public abstract class MonoEntityInstaller : MonoBehaviour, IEntityInstaller
{
#if UNITY_EDITOR
    internal Action onValidateCallback;
#endif

    public abstract void Install(IEntity entity);

    public virtual void Uninstall(IEntity entity)
    {
    }

    protected virtual void OnValidate()
    {
#if UNITY_EDITOR
        try
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
                this.onValidateCallback?.Invoke();
        }
        catch (Exception)
        {
            // ignored
        }
#endif
    }
}
```
`Installers/MonoEntityInstaller.cs`, весь файл внутри `#if UNITY_5_3_OR_NEWER`. Брать только когда есть зависимости от сцены или когда экземпляры в сцене должны различаться — так прямо написано в `Docs/Entities/Installers/MonoEntityInstaller.md`. `onValidateCallback` ставит `MonoEntity.SetRefreshCallbackToInstallers()`, он равен `MonoEntity.Compile`. Если переопределяете `OnValidate` — вызывайте `base.OnValidate()`, иначе редакторный refresh сущности перестанет работать.

```csharp
public abstract class MonoEntityInstaller<E> : MonoEntityInstaller, IEntityInstaller<E> where E : class, IEntity
{
    public sealed override void Install(IEntity entity) { /* каст или InvalidCastException */ }
    public sealed override void Uninstall(IEntity entity) { /* каст или InvalidCastException */ }

    public abstract void Install(E entity);

    public virtual void Uninstall(E entity)
    {
    }
}
```
`Installers/MonoEntityInstaller`1.cs`. Ограничение **строже**, чем у интерфейса: `where E : class, IEntity`. Нетипизированные `Install`/`Uninstall` — `sealed`, обойти каст нельзя. Оба типизированных метода **публичные**; в наследнике пишется `public override void Install(E entity)` (документация `MonoEntityInstaller`1.md` показывает `protected override` — это ошибка документации).

```csharp
public sealed class MonoEntityInstallerConfigurable : MonoEntityInstaller
{
    [SerializeReference]
    private IEntityInstaller[] _installers;

    public override void Install(IEntity entity)
    {
        if (_installers == null)
            return;

        foreach (IEntityInstaller installer in _installers)
            if (installer != null)
                installer.Install(entity);
    }
}
```
`Installers/MonoEntityInstallerConfigurable.cs`. Готовый компонент: собирает сущность из массива разнородных `[Serializable]`-инсталлеров, заданных в инспекторе, без написания собственного MonoBehaviour. `[SerializeReference]` даёт полиморфную сериализацию. Null-безопасен и по массиву, и по элементам. **`Uninstall` не переопределён** — вложенные инсталлеры teardown не получают. Обратите внимание: этот файл, в отличие от базового класса, **не** обёрнут в `#if UNITY_5_3_OR_NEWER`.

```csharp
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Entities/Installers/ScriptableEntityInstaller.md")]
public abstract class ScriptableEntityInstaller : ScriptableObject, IEntityInstaller
{
    public abstract void Install(IEntity entity);

    public virtual void Uninstall(IEntity entity)
    {
    }
}

public abstract class ScriptableEntityInstaller<E> : ScriptableEntityInstaller, IEntityInstaller<E>
    where E : class, IEntity
{
    public sealed override void Install(IEntity entity) { /* каст */ }
    public abstract void Install(E entity);

    public sealed override void Uninstall(IEntity entity) { /* каст */ }
    public virtual void Uninstall(E entity) { }
}
```
`Installers/ScriptableEntityInstaller.cs`, `Installers/ScriptableEntityInstaller`1.cs`. Это **дефолтный выбор**: один ассет шарится всеми сущностями, которые на него ссылаются. Flyweight — значит никакого изменяемого per-entity состояния в полях; свежее реактивное состояние создаётся внутри `Install` (`new ReactiveVariable<Vector3>()`).

### Точки входа установки

```csharp
public partial class MonoEntity
{
    public bool Installed => _installed;

    public void Install();                       // не [AggressiveInlining]
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Uninstall();

    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnInstall();
    [MethodImpl(MethodImplOptions.AggressiveInlining)] protected virtual void OnUninstall();

    public void WhenInstall(Action action);      // бросает ArgumentNullException; вызывает сразу, если уже установлено
    public void WhenUninstall(Action action);    // бросает ArgumentNullException; «позднего входа» нет

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void InstallAll(Scene scene, bool includeInactive = false);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void InstallAll<E>(Scene scene, bool includeInactive = false) where E : MonoEntity;
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public static void InstallAll(Component component, bool includeInactive = false);
}
```
`Entities/Unity/MonoEntity_Installing.cs`. `Install()` идемпотентен, `Uninstall()` сбрасывает флаг — сущность можно переустановить, это нужно для пулинга. Отсутствующий (null) слот инсталлера даёт `Debug.LogWarning` и пропускается, исключения не будет: битая ссылка в инспекторе тихо даёт недонастроенную сущность. `InstallAll` пропускает уже установленные (`!entity.Installed`).

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEntity Install(this IEntity entity, IEntityInstaller installer)
    {
        installer.Install(entity);
        return entity;
    }

    public static void Install(this IEntity entity, IEnumerable<IEntityInstaller> installers);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InstallFromScene(this IEntity entity, Scene scene, bool includeInactive = true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InstallFromScene<T>(this T entity, Scene scene, bool includeInactive = true)
        where T : class, IEntity;

    public static void Clear(this IEntity entity);   // ClearTags + ClearValues + ClearBehaviours
}
```
`Entities/Extensions.cs`. `entity.Install(installer)` — канонический call site композиции, возвращает сущность для цепочек. Перегрузка с `IEnumerable` инлайна **не** имеет и не бросает на null-коллекции (просто выходит).

Важная асимметрия двух `InstallFromScene`: нетипизированная ищет `GetComponentsInChildren<MonoEntityInstaller>(includeInactive)`, а **генерик-версия ищет `GetComponentsInChildren<MonoEntityInstaller<T>>(includeInactive)`** — только инсталлеры, замкнутые ровно на `T`. Для обычных `MonoEntityInstaller`-компонентов она молча не найдёт ничего, и для `MonoEntityInstaller<IBase>`, когда `T` — производный интерфейс, тоже.

### Создание MonoEntity в коде

```csharp
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

[MethodImpl(MethodImplOptions.AggressiveInlining)] public static MonoEntity Create(in CreateArgs args);
[MethodImpl(MethodImplOptions.AggressiveInlining)] public static E Create<E>(in CreateArgs args) where E : MonoEntity;
```
`Entities/Unity/MonoEntity_Creation.cs`. Передаётся по `in`, копий структуры нет. Теги и значения здесь **целочисленные** (`IEnumerable<int>` / `IReadOnlyDictionary<int, object>`), не строковые.

Что реально делает `Create<E>(in CreateArgs)`: создаёт `new GameObject()`, сразу `SetActive(false)`, `AddComponent<E>()`, присваивает списки инсталлеров и четыре флага, **клампит ёмкости через `Mathf.Max(1, ...)`**, вызывает `Construct()`, затем `AddTags/AddValues/AddBehaviours`, затем `Register()`, и только потом `SetActive(true)`. Он **не** вызывает `Install()` и не проходит через `SetupAfterCreation`. Установка здесь случается исключительно потому, что `SetActive(true)` поднимает `Awake()`, а `Awake()` зовёт `Install()`, если `installOnAwake == true`. При `default(CreateArgs)` все флаги `false` и ёмкости `0` — сущность не будет установлена вообще.

```csharp
// Эргономичный путь: именованные параметры, другие дефолты
[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

// плюс нетипизированный двойник `public static MonoEntity Create(...)` с теми же параметрами
```
Здесь все четыре флага по умолчанию `true`, а ёмкости — `1`. Параметры называются `initialTagCount`, но присваиваются в `initialTagCapacity` — имена не совпадают.

```csharp
// Префабные перегрузки — единственные, что Register + Install
[MethodImpl(MethodImplOptions.AggressiveInlining)] public static MonoEntity Create(MonoEntity prefab, Transform parent = null);
[MethodImpl(MethodImplOptions.AggressiveInlining)] public static E Create<E>(E prefab, Transform parent = null) where E : MonoEntity;
[MethodImpl(MethodImplOptions.AggressiveInlining)] public static MonoEntity Create(MonoEntity prefab, Vector3 position, Quaternion rotation, Transform parent = null);
[MethodImpl(MethodImplOptions.AggressiveInlining)] public static E Create<E>(E prefab, Vector3 position, Quaternion rotation, Transform parent = null) where E : MonoEntity;
[MethodImpl(MethodImplOptions.AggressiveInlining)] public static E Create<E>(E prefab, Transform point, Transform parent) where E : MonoEntity;
[MethodImpl(MethodImplOptions.AggressiveInlining)] public static E Create<E>(E prefab, Scene scene) where E : MonoEntity;

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static async ValueTask<E[]> CreateAsync<E>(E prefab, int count, Transform parent = null) where E : MonoEntity;

private static void SetupAfterCreation<E>(E prefab, E entity) where E : MonoEntity
{
    entity.Register();
    entity.Install();
    entity.name = prefab.name;
}
```
Все префабные перегрузки проходят через `SetupAfterCreation` и возвращают уже зарегистрированную и установленную сущность с именем префаба. `CreateAsync` использует `InstantiateAsync` и применяет `SetupAfterCreation` к каждому экземпляру — единственный неблокирующий путь массового спавна.

Сериализованные поля, которые это всё читают, лежат в `Entities/Unity/MonoEntity_Inspector.cs`. Доступность у них разная: `internal` — только `useUnityLifecycle`, `disposeValues`, `installOnAwake`, `uninstallOnDestroy` и два списка инсталлеров (`scriptableInstallers`, `sceneInstallers`); `initialTagCapacity`, `initialValueCapacity`, `initialBehaviourCapacity`, `autoCompile`, `onlySelectedGizmos`, `onlyEditModeGizmos` объявлены `private`. Дефолты: `useUnityLifecycle = true`, `disposeValues = true`, `installOnAwake = true`, `uninstallOnDestroy = true`, `initialTagCapacity = 1` (с `[Min(1)]`), `initialValueCapacity = 1` (с `[Min(1)]`), `initialBehaviourCapacity = 0` (с `[Min(0)]`), `autoCompile` — `false`. Поле `sceneInstallers` помечено `[FormerlySerializedAs("installers")]` — старые префабы с полем `installers` переедут сами.

Для справки, `Entities/Unity/MonoEntity_UnityLifecycle.cs`:

```csharp
private protected virtual void Awake()
{
    this.Register();

    if (this.installOnAwake)
        this.Install();
}

private protected virtual void OnDestroy()
{
    if (this.useUnityLifecycle && _started) { /* OnDispose, Deinitialize, DisposeValues */ }

    if (this.uninstallOnDestroy)
        this.Uninstall();

    this.ClearTags();
    this.ClearValues();
    this.ClearBehaviours();
    // ...
}
```

### Аспекты

```csharp
public interface IEntityAspect<in E> where E : IEntity
{
    void Apply(E entity);
    void Discard(E entity);
}

public interface IEntityAspect : IEntityAspect<IEntity>
{
}
```
`Aspects/IEntityAspect`1.cs`, `Aspects/IEntityAspect.cs`. Структурно это **не то же самое**, что инсталлеры: `IEntityInstaller` — самостоятельный интерфейс, который `IEntityInstaller<E>` расширяет и к которому мостит кастом; `IEntityAspect` — замкнутая специализация `IEntityAspect<IEntity>` без собственных членов. Следствие: `IEntityAspect<IGameEntity>` **не приводится** к `IEntityAspect` и не ляжет в `IEntityAspect[]`. Коллекции типизированных аспектов держите как `IEntityAspect<E>[]`.

```csharp
public abstract class MonoEntityAspect<E> : MonoBehaviour, IEntityAspect<E> where E : IEntity
{
    public abstract void Apply(E entity);
    public abstract void Discard(E entity);
}

public abstract class MonoEntityAspect : MonoEntityAspect<IEntity>, IEntityAspect { }

public abstract class ScriptableEntityAspect<E> : ScriptableObject, IEntityAspect<E> where E : IEntity
{
    public abstract void Apply(E entity);
    public abstract void Discard(E entity);
}

public abstract class ScriptableEntityAspect : ScriptableEntityAspect<IEntity>, IEntityAspect { }
```
`Aspects/MonoEntityAspect`1.cs`, `Aspects/MonoEntityAspect.cs`, `Aspects/ScriptableEntityAspect`1.cs`, `Aspects/ScriptableEntityAspect.cs`. Нетипизированные варианты — пустые тела, чистые алиасы. Ограничение `where E : IEntity` без `class`, в отличие от инсталлерных базовых классов. `ScriptableEntityAspect` — рекомендуемая форма для баффов/дебаффов: один ассет на все сущности.

```csharp
public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Apply(this IEntity e, IEntityAspect entityAspect) => entityAspect.Apply(e);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Apply<E>(this E e, IEntityAspect<E> entityAspect) where E : IEntity => entityAspect.Apply(e);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Discard(this IEntity e, IEntityAspect entityAspect) => entityAspect.Discard(e);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Discard<E>(this E e, IEntityAspect<E> entityAspect) where E : IEntity =>
        entityAspect.Discard(e);
}
```
`Aspects/Extensions.cs`. Четыре expression-bodied форвардера с агрессивным инлайном. Генерик-перегрузки без `class`-ограничения: если `E` — структура, `this E e` принимается по значению, то есть копируется.

### Бейкеры: аргументы конструирования

```csharp
public interface IArgs
{
}

public struct NoArgs : IArgs
{
    public static readonly NoArgs Default = new();
}

public struct Args<T> : IArgs
{
    public readonly T value;
    public Args(T value) => this.value = value;
    public static implicit operator Args<T>(T value) => new(value);
}

public struct Args<T1, T2> : IArgs
{
    public T1 value1;
    public T2 value2;
    public Args(T1 value1, T2 value2) { this.value1 = value1; this.value2 = value2; }
}

public struct Args<T1, T2, T3> : IArgs { /* value1..value3, изменяемые */ }
public struct Args<T1, T2, T3, T4> : IArgs { /* value1..value4, изменяемые */ }
```
`Utils/Args.cs`. `IArgs` — маркер, всё остальное — структуры. У `Args<T>` поле `readonly`, у многоаргументных вариантов поля изменяемые — асимметрия, и изменяемые структуры провоцируют баги с defensive copy. `Args<T>` имеет неявное приведение из `T`, поэтому можно передавать «сырое» значение.

### Бейкеры: базовые классы

```csharp
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Entities/Baking/MonoEntityBaker%601.md")]
public abstract partial class MonoEntityBaker<TEntity, TArgs> : MonoBehaviour
    where TEntity : IEntity
    where TArgs : IArgs
{
    [Header("Optimization")]
    [Tooltip("Initial number of tags to assign to the entity")]
    [SerializeField] private int tagCapacity;

    [Tooltip("Initial number of values to assign to the entity")]
    [SerializeField] private int valueCapacity;

    [Tooltip("Initial number of behaviours to assign to the entity")]
    [SerializeField] private int behaviourCapacity;

    [SerializeField] private Entity.Settings settings;

    public TEntity Bake(TArgs args)
    {
        TEntity entity = this.Create(
            this.tagCapacity,
            this.valueCapacity,
            this.behaviourCapacity,
            this.settings,
            args
        );
        this.Release();
        return entity;
    }

    protected virtual void Release()
    {
        Destroy(this.gameObject);
    }

    protected abstract TEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        TArgs args
    );
}
```
`Baking/MonoEntityBaker`1.cs`, внутри `#if UNITY_5_3_OR_NEWER`, с Odin-атрибутами за `#if ODIN_INSPECTOR`. **Два типовых параметра, не один.** Поля ёмкостей `private` — они доступны только как параметры `Create`. Документация говорит про `protected int initialTagCount` — этого нет ни под таким именем, ни с таким модификатором.

```csharp
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Entities/Baking/MonoEntityBaker.md")]
public abstract class MonoEntityBaker<TArgs> : MonoEntityBaker<IEntity, TArgs> where TArgs : IArgs
{
    protected override IEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        TArgs args
    )
    {
        Entity entity = new Entity(
            this.name,
            tagCapacity,
            valueCapacity,
            behaviourCapacity,
            settings
        );
        this.Install(entity, args);
        return entity;
    }

    protected abstract void Install(IEntity entity, TArgs args);
}
```
`Baking/MonoEntityBaker.cs`. То, что документация называет «нонженериком `MonoEntityBaker`», — вот это: он всё равно параметризован `TArgs`. Пишите `MonoEntityBaker<NoArgs>`. `Create` здесь `protected override`, **не** `sealed` (xml-doc в файле утверждает, что sealed — это неправда), так что наследник может заменить конструирование.

```csharp
public partial class MonoEntityBaker<TEntity, TArgs>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity[] BakeAll(TArgs args, bool includeInactive = true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BakeAll(TArgs args, ICollection<TEntity> destination, bool includeInactive = true);

    public static List<TEntity> Bake(TArgs args, Scene scene, bool includeInactive = true);
    public static void Bake(TArgs args, Scene scene, ICollection<TEntity> results, bool includeInactive = true);
    public static TEntity[] Bake(TArgs args, GameObject gameObject, bool includeInactive = true);
    public static void Bake(TArgs args, GameObject gameObject, ICollection<TEntity> results, bool includeInactive = true);
}
```
`Baking/MonoEntityBaker`1_Static.cs`. **`args` идёт первым параметром во всех шести перегрузках.** `BakeAll` ищет через `FindObjectsByType<MonoEntityBaker<TEntity, TArgs>>(include, FindObjectsSortMode.None)`; `Bake(Scene)` — через `scene.GetRootGameObjects()` + `GetComponentsInChildren` на каждый корень. Перегрузки с `ICollection` бросают `ArgumentNullException` на null; массивные — не валидируют.

```csharp
public partial class MonoEntityBaker<TEntity, TArgs>
{
    [Header("Editor")]
    [Tooltip("Should precompute capacities when OnValidate happens?")]
    [SerializeField] private bool autoCompile;

    private protected IEntity _previewEntity;

    protected virtual void OnValidate()
    {
        if (this.autoCompile)
            this.Compile();
    }

    [ContextMenu(nameof(Compile))]
    private protected virtual void Compile()
    {
#if UNITY_EDITOR
        if (EditorApplication.isPlaying)
            return;

        try
        {
            _previewEntity = this.Create(this.tagCapacity, this.valueCapacity, this.behaviourCapacity, this.settings, default);

            if (_previewEntity == null)
                Debug.LogWarning($"{nameof(MonoEntityBaker<TEntity, TArgs>)}: Create() returned null.", this);
            else
            {
                this.tagCapacity = _previewEntity.TagCount;
                this.valueCapacity = _previewEntity.ValueCount;
                this.behaviourCapacity = _previewEntity.BehaviourCount;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"<color=#FF3C3C>{this.name} Compilation failed: {ex.Message}</color>\n{ex.StackTrace}", this);
        }
#endif
    }

    protected virtual void Reset()
    {
#if UNITY_EDITOR
        this.tagCapacity = 0;
        this.valueCapacity = 0;
        this.behaviourCapacity = 0;
#endif
    }
}
```
`Baking/MonoEntityBaker`1_Editor.cs`. Метод называется `Compile` (документация зовёт его `Precompile`) и объявлен `private protected virtual` — бейкер из другого asmdef переопределить его **не может**. `Compile` передаёт `default` в качестве `TArgs`.

```csharp
#if UNITY_EDITOR
public partial class MonoEntityBaker<TEntity, TArgs>
{
    [Header("Gizmos")]
    [SerializeField] private bool onlySelectedGizmos;
    [SerializeField] private bool onlyEditModeGizmos;

    private void OnDrawGizmos()
    {
        if (!this.onlySelectedGizmos)
            this.OnDrawGizmosSelected();
    }

    private void OnDrawGizmosSelected()
    {
        if (_previewEntity == null || EditorApplication.isPlaying && this.onlyEditModeGizmos)
            return;

        try
        {
            for (int i = 0; i < _previewEntity.BehaviourCount; i++)
                if (_previewEntity.GetBehaviourAt(i) is IEntityGizmos gizmos)
                    gizmos.DrawGizmos(_previewEntity);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"MonoEntity: Ops! Detected exception in OnDrawGizmos: {e.StackTrace}", this);
        }
    }
}
#endif
```
`Baking/MonoEntityBaker`1_Gizmos.cs` — **весь файл** под `#if UNITY_EDITOR`, поэтому два сериализованных bool существуют только в редакторе: это различие в раскладке сериализации между редактором и плеером. Гизмы рисуются только если `_previewEntity` создан, то есть после `Compile()`.

```csharp
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Entities/Baking/MonoEntityBakerOptimized%602.md")]
public abstract class MonoEntityBakerOptimized<TKey, TEntity, TView, TArgs> : MonoEntityBaker<TEntity, TArgs>
    where TEntity : class, IEntity
    where TView : EntityView<TEntity>
    where TArgs : IArgs
{
    [SerializeField] private TView _view;

    [Space]
    [SerializeField] private ScriptableEntityFactory<TEntity, TArgs> _factory;

    protected override TEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        TArgs args
    )
    {
        TEntity entity = _factory.Create(args);
        this.Override(entity, args);
        return entity;
    }

    protected override void Reset()
    {
        base.Reset();
        _view = this.GetComponent<TView>();
    }

    protected abstract void Override(TEntity entity, TArgs args);

    protected override void Release()
    {
        EntityWorldViewSingleton<TKey, TEntity, TView>.Instance.Pool.Return(_view);
        Destroy(this);
    }
}

public abstract class MonoEntityBakerOptimized<TArgs> :
    MonoEntityBakerOptimized<string, IEntity, EntityView, TArgs>
    where TArgs : IArgs
{
}
```
`Baking/MonoEntityBakerOptimized`1.cs`, `Baking/MonoEntityBakerOptimized.cs`. Конструирование делегировано фабрике, бейкер применяет только scene-override. `Release()` уничтожает **только компонент** (`Destroy(this)`), GameObject остаётся жить, потому что его view уехал в пул. Ёмкости и `settings` здесь игнорируются — ими владеет фабрика, оставлять нули правильно. `TKey` используется только для разрешения `EntityWorldViewSingleton<TKey, TEntity, TView>`.

Оба файла `MonoEntityBakerOptimized*` **не** обёрнуты в `#if UNITY_5_3_OR_NEWER`, хотя их базовый класс обёрнут.

Важные оговорки по `Release()`: `EntityWorldViewSingleton<K, E, V>` сам `abstract`, а его `static Instance` ищет `FindAnyObjectByType<EntityWorldViewSingleton<K, E, V>>(FindObjectsInactive.Exclude)` и бросает `Exception`, если не нашёл. Нетипизированный `EntityWorldViewSingleton` (`UI/World/EntityWorldViewSingleton.cs`) **этим наследником не является**: он объявлен как `class EntityWorldViewSingleton : EntityWorldView<string, IEntity, EntityView>`, то есть наследуется от `EntityWorldView`, а не от `EntityWorldViewSingleton<string, IEntity, EntityView>`, и несёт собственный, независимый статик `Instance`. Значит `MonoEntityBakerOptimized<TArgs>` (замыкание `<string, IEntity, EntityView, TArgs>`) готовым шорткатом не обслуживается — в сцене должен лежать свой наследник `EntityWorldViewSingleton<string, IEntity, EntityView>`. Свойство `Pool` на `EntityCollectionView<K, E, V>` объявлено `internal`, как и `EntityViewPool<K, E, V>.Rent/Return` — работает это только потому, что код внутри той же сборки.

### Фабрики, нужные бейкеру

```csharp
[HelpURL("https://github.com/StarKRE22/Atomic/blob/main/Docs/Entities/Factories/ScriptableEntityFactory%601.md")]
public abstract class ScriptableEntityFactory<TEntity, TArgs> : ScriptableObject, IEntityFactory<TEntity, TArgs>
    where TArgs : IArgs
    where TEntity : IEntity
{
    public TEntity Create(TArgs args) => this.Create(
        this.tagCapacity,
        this.valueCapacity,
        this.behaviourCapacity,
        this.settings,
        args
    );

    protected abstract TEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        TArgs args
    );
}

public abstract class ScriptableEntityFactory<TArgs> :
    ScriptableEntityFactory<IEntity, TArgs>, IEntityFactory<TArgs>
    where TArgs : IArgs
{
    [field: SerializeField]
    public virtual string Name { get; private set; }

    protected override IEntity Create(int tagCapacity, int valueCapacity, int behaviourCapacity,
        Entity.Settings settings, TArgs args)
    {
        Entity entity = new Entity(this.Name, tagCapacity, valueCapacity, behaviourCapacity, settings);
        this.Install(entity);
        return entity;
    }

    protected abstract void Install(IEntity entity);
}
```
`Factories/Unity/ScriptableEntityFactory`1.cs`, `Factories/Unity/ScriptableEntityFactory.cs`. Без этой формы `_factory.Create(args)` в `MonoEntityBakerOptimized` не имеет смысла. У фабрики свой `Compile()` с `_previewEntity = this.Create(default)`, и её `autoCompile` в отличие от бейкера **по умолчанию `true`** — значит `Create(default)` будет дёргаться при любом `OnValidate` ассета. Обратите внимание, что `Install(IEntity)` у `ScriptableEntityFactory<TArgs>` аргументов не получает: `TArgs` до него не доходит.

### Entity.Settings и конструктор Entity

```csharp
[Serializable]
public struct Settings
{
    /// <summary>
    /// Indicates whether stored values should be disposed automatically when Dispose is called.
    /// Default is true.
    /// </summary>
    public bool disposeValues;
}

public Entity(
    string name = null,
    int tagCapacity = 0,
    int valueCapacity = 0,
    int behaviourCapacity = 0,
    Settings? settings = null
)
{
    _name = name ?? string.Empty;
    _settings = settings ?? new Settings {disposeValues = true};

    this.ConstructTags(tagCapacity);
    this.ConstructValues(valueCapacity);
    this.ConstructBehaviours(behaviourCapacity);

    EntityRegistry.Instance.Register(this);
}
```
`Entities/Core/Entity.cs`. Два критичных факта. Первый: `settings` принимается как `Settings?`, и `null` означает `disposeValues == true` — но сериализованное поле бейкера это **структура**, которая по умолчанию `disposeValues == false`, и бейкер передаёт её не-null. Значит сущность, созданная бейкером, свои `IDisposable`-значения **не** диспоузит, пока галочка не поставлена. Второй: конструктор сам регистрирует сущность в глобальном `EntityRegistry`.

### Bootstrap и синглтон-сущность

```csharp
[CreateAssetMenu(
    fileName = "EntityBootstrapper",
    menuName = "Atomic/Entities/EntityBootstrapper"
)]
public class ScriptableEntityBootstrapper : ScriptableObject
{
    public enum Mode
    {
        BeforeSceneLoad = 0,
        AfterSceneLoad = 1
    }

    [SerializeField] private bool isEnabled = true;
    [SerializeField] private string _sceneRegex;
    [SerializeField] private Mode _mode = Mode.BeforeSceneLoad;
    [Space]
    [SerializeField] private MonoEntity[] _entityPrefabs;

    protected virtual bool IsAvailable(Scene scene)
    {
        return string.IsNullOrEmpty(_sceneRegex) || Regex.IsMatch(scene.name, _sceneRegex);
    }

    protected virtual async void BootstrapEntities(Scene scene)
    {
        if (_mode == Mode.AfterSceneLoad)
            while (!scene.isLoaded)
                await Task.Yield();

        for (int i = 0, count = _entityPrefabs.Length; i < count; i++)
        {
            MonoEntity prefab = _entityPrefabs[i];
            MonoEntity.Create(prefab, scene);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        ScriptableEntityBootstrapper[] bootstraps = Resources.LoadAll<ScriptableEntityBootstrapper>(string.Empty);
        int count = bootstraps.Length;
        if (count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            ScriptableEntityBootstrapper bootstrapper = bootstraps[i];
            if (bootstrapper.isEnabled && bootstrapper.IsAvailable(activeScene))
                bootstrapper.BootstrapEntities(activeScene);
        }
    }
}
```
`Bootstrap/ScriptableEntityBootstrapper.cs`. Класс не `abstract` и не `sealed`: можно использовать как есть или наследовать, переопределив `IsAvailable`. Спавн идёт через `MonoEntity.Create(prefab, scene)`, то есть префабы приходят зарегистрированными и установленными.

```csharp
public abstract class MonoEntitySingleton<E> : MonoEntity where E : MonoEntitySingleton<E>
{
    [Tooltip("If enabled, this object will persist across scene loads.")]
    [SerializeField] private bool dontDestroyOnLoad;

    private static E _instance;

    public static E Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<E>(FindObjectsInactive.Exclude);

            if (_instance == null)
                throw new Exception($"Singleton of type {typeof(E).Name} was not found in the scene.");

            return _instance;
        }
    }

    public static bool TryGetInstance(out E instance);

    private protected override void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogError(
                $"Duplicate singleton of type {typeof(E).Name} detected on '{name}'. Destroying this instance.",
                this);

            Destroy(gameObject);
            return;
        }

        _instance = (E) this;

        base.Awake();

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);
    }

    private protected override void OnDestroy()
    {
        base.OnDestroy();

        if (_instance == this)
            _instance = null;
    }

#if UNITY_EDITOR
    private protected override void Reset()
    {
        base.Reset();
        dontDestroyOnLoad = false;
    }
#endif
}
```
`Entities/Unity/MonoEntitySingleton.cs`. CRTP-синглтон, структурная замена старому `SingletonSceneContext`: глобально достижимая сущность, которая при этом собирается инсталлерами как любая другая `MonoEntity`. Статик **очищается** в `OnDestroy`, дубликат уничтожается в `Awake` с `Debug.LogError`. Важно: `Awake` возвращается **до** `base.Awake()`, если обнаружил дубликат, — значит дубликат не регистрируется и не устанавливается, что правильно. `Instance` ищет через `FindAnyObjectByType<E>(FindObjectsInactive.Exclude)` и **бросает**, если не нашёл; неактивный синглтон для него не существует.

### Мост Unity → Entity

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool TryGetEntity(this GameObject gameObject, out IEntity entity) =>
    gameObject.TryGetComponent(out entity);

// плюс перегрузки для Component, Collision2D, Collision

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool FindEntityInParent(this GameObject gameObject, out IEntity entity)
{
    entity = gameObject.GetComponentInParent<IEntity>();
    return entity != null;
}

// плюс перегрузки для Component, Collision2D, Collision

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void DrawGizmos(this IEntity entity)
{
    for (int i = 0; i < entity.BehaviourCount; i++)
        if (entity.GetBehaviourAt(i) is IEntityGizmos gizmos)
            gizmos.DrawGizmos(entity);
}
```
`Entities/Extensions.cs`. Это штатный мост фреймворка из коллизий/триггеров в модель сущностей. `TryGetEntity` — когда `IEntity` лежит на том же GameObject, `FindEntityInParent` — когда коллайдер висит на дочернем объекте (обычная авторинговая раскладка). `DrawGizmos(this IEntity)` — тот самый цикл, который partial бейкера дублирует у себя; в своём бейкере или `MonoEntity` зовите его одной строкой.

### Типизированные ключи (для справки)

```csharp
public readonly struct TagKey : IEquatable<TagKey>              // Id, ctor(string), ctor(int)
public readonly struct TagKey<E> : IEquatable<TagKey<E>> where E : IEntity
public readonly struct ValueKey<T> : IEquatable<ValueKey<T>>
public readonly struct ValueKey<E, T> : IEquatable<ValueKey<E, T>> where E : IEntity
```
`KeyStore/Keys/`. Под них есть перегрузки расширений: `AddTag(this IEntity, TagKey)`, `AddTag<E>(this E, TagKey<E>)`, `AddValue<T>(this IEntity, ValueKey<T>, T)`, `AddValue<E, T>(this E, ValueKey<E, T>, T)` и так далее. Это промежуточный вариант между строками и сгенерированным API — детали в главе про CodeGen/KeyStore.

---

## Идиомы

### 1. Канонический scene-инсталлер: типы полей кодируют намерение

```csharp
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

public sealed class CharacterInstaller : MonoEntityInstaller
{
    [SerializeField] private Transform _transform;
    [SerializeField] private Const<float> _moveSpeed = 5.0f;              // неизменяемое
    [SerializeField] private ReactiveVariable<Vector3> _moveDirection;    // изменяемое, с подписками

    public override void Install(IEntity entity)
    {
        entity.AddTag("Character");
        entity.AddTag("Moveable");

        entity.AddValue("Transform", _transform);
        entity.AddValue("MoveSpeed", _moveSpeed);
        entity.AddValue("MoveDirection", _moveDirection);
    }
}
```

Почему так правильно: инсталлер `sealed` (девиртуализация вызова `Install` и явный сигнал «лист иерархии»). Он описывает **из чего состоит** сущность и больше ничего — ни `Update`, ни ветвлений, ни игровой логики. Тип поля несёт смысл: `Const<float>` — значение, которое никто никогда не запишет (у него есть `implicit operator Const<T>(T)`, поэтому инициализатор `= 5.0f` компилируется); `ReactiveVariable<Vector3>` — состояние, на которое подписываются поведения. Укладывая в сущность обёртку, а не голый `float`, вы даёте потребителям единообразный доступ через `IValue<float>` / `IVariable<Vector3>`.

**Анти-паттерн.** MonoBehaviour, который одновременно сериализует данные и исполняет механику (поля + `Update()`, который двигает трансформ). В Atomic данные уезжают в сущность через инсталлер, а механика — в `IEntityBehaviour`, который этот же инсталлер и добавляет.

### 2. Типизированный инсталлер на интерфейс, а не на конкретный класс

```csharp
using System;
using Atomic.Entities;
using UnityEngine;

public interface ICharacterEntity : IEntity
{
}

[Serializable]
public sealed class CharacterInstaller : IEntityInstaller<ICharacterEntity>
{
    [SerializeField] private Transform _transform;
    [SerializeField] private float _moveSpeed = 5.0f;

    public void Install(ICharacterEntity entity)
    {
        entity.AddTag("Character");
        entity.AddTag("Moveable");

        entity.AddValue("Transform", _transform);
        entity.AddValue("MoveSpeed", _moveSpeed);

        entity.AddBehaviour<MoveBehaviour>();   // требует public ctor без параметров
        entity.AddBehaviour<LookBehaviour>();
    }
}
```

Почему так правильно: `[Serializable] sealed class` + чистый интерфейс — самый дешёвый инсталлер, который существует: ни MonoBehaviour, ни ассета, ни GameObject, ни оверхеда Unity-объекта; он сериализуется inline внутрь того, кто им владеет. Целиться в интерфейс `ICharacterEntity` можно именно потому, что `IEntityInstaller<E>` ограничен `where E : IEntity` без `class` — и это держит механику переносимой между всеми конкретными классами сущностей, которые интерфейс реализуют.

Про `AddBehaviour<T>()`: его настоящая подпись — `public static T AddBehaviour<T>(this IEntity entity) where T : IEntityBehaviour, new()`, он возвращает созданный экземпляр. Ограничение `new()` означает публичный конструктор без параметров. Поведению с зависимостями нужен `entity.AddBehaviour(new LifeBehaviour(gameContext))` — член интерфейса `void AddBehaviour(IEntityBehaviour)`.

**Анти-паттерн.** `IEntityInstaller<UnitEntity>`, привязанный к конкретному классу: механика залипает на один тип сущности, и на каждый вариант сущности приходится писать новый инсталлер.

### 3. Модульные инсталлеры через `entity.Install(...)`

Документация (`Docs/BestPractices/ModularEntityInstallers.md`) прямо ранжирует три способа организации. Ниже — худший и лучший, как в источнике.

**Плохо — Method 1 «Split by Comments»** (годится только для маленьких уникальных сущностей):

```csharp
public sealed class CharacterInstaller : MonoEntityInstaller<IGameEntity>
{
    [SerializeField] private Const<float> _moveSpeed = 3;
    [SerializeField] private Const<float> _rotationSpeed = 360 * 4;

    public override void Install(IGameEntity entity)
    {
        // Base
        entity.AddTag("Character");
        entity.AddValue("Transform", this.transform);

        // Movement
        entity.AddValue("MoveSpeed", _moveSpeed);
        entity.AddValue("MoveDirection", new ReactiveVariable<Vector3>());
        entity.AddBehaviour<MovementBehaviour>();

        // Rotation
        entity.AddValue("RotationSpeed", _rotationSpeed);
        entity.AddValue("RotationDirection", new ReactiveVariable<Vector3>());
        entity.AddBehaviour<RotationBehaviour>();
    }
}
```

Нулевое переиспользование: `EnemyInstaller` с той же механикой придётся скопировать. Промежуточный «Method 2 Split by Methods» (приватные `InstallMovementFeature` и т. п.) улучшает читаемость, но переиспользования не добавляет; документация отдельно отмечает, что он «slightly less performant due to additional method calls».

**Хорошо — Method 3 «Modular Installers»:**

```csharp
using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

[Serializable]
public sealed class MovementEntityInstaller : IEntityInstaller<IGameEntity>
{
    [SerializeField] private Const<float> _moveSpeed = 3;

    public void Install(IGameEntity entity)
    {
        entity.AddValue("MoveSpeed", _moveSpeed);
        entity.AddValue("MoveDirection", new ReactiveVariable<Vector3>());
        entity.AddBehaviour<MovementBehaviour>();
    }
}

[Serializable]
public sealed class RotationEntityInstaller : IEntityInstaller<IGameEntity>
{
    [SerializeField] private Const<float> _rotationSpeed = 360 * 4;

    public void Install(IGameEntity entity)
    {
        entity.AddValue("RotationSpeed", _rotationSpeed);
        entity.AddValue("RotationDirection", new ReactiveVariable<Vector3>());
        entity.AddBehaviour<RotationBehaviour>();
    }
}

public sealed class CharacterInstaller : MonoEntityInstaller<IGameEntity>
{
    [SerializeField] private TransformEntityInstaller _transformInstaller;
    [SerializeField] private MovementEntityInstaller _movementInstaller;
    [SerializeField] private RotationEntityInstaller _rotationInstaller;
    [SerializeField] private TeamEntityInstaller _teamInstaller;

    public override void Install(IGameEntity entity)
    {
        entity.AddTag("Character");

        entity.Install(_transformInstaller);
        entity.Install(_movementInstaller);
        entity.Install(_rotationInstaller);
        entity.Install(_teamInstaller);
    }
}
```

Почему так правильно: одна фича — один маленький `[Serializable]`-инсталлер, который владеет ровно своими сериализованными данными и своим поведением. `Character` и `Enemy` переиспользуют идентичную механику, просто держа поле того же типа — без наследования, без копипасты, без god-инсталлера. Данные фичи и её поведение лежат в одном файле, поэтому «добавить механику» = один новый класс и одно новое поле. Call site написан как `entity.Install(x)`, а не `x.Install(entity)`: эта перегрузка `[MethodImpl(AggressiveInlining)]` и возвращает сущность.

Важно: `MovementEntityInstaller` **обязан** иметь `[Serializable]`, иначе Unity не сериализует его как поле.

В реальном проекте с включённым кодогенератором `entity.AddValue("MoveSpeed", _moveSpeed)` заменяется на сгенерированное `entity.AddMoveSpeed(_moveSpeed)`, объявленное на `IGameEntity`. Выше намеренно использован ручной API, потому что это то, что гарантированно есть в фреймворке.

### 4. `Uninstall` + `DisposableComposite` на каждую подписку

```csharp
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

public sealed class WeaponViewInstaller : MonoEntityInstaller
{
    [SerializeField] private ParticleSystem _fireVFX;
    [SerializeField] private AudioSource _fireSFX;
    [SerializeField] private Animator _animator;

    private readonly DisposableComposite _disposables = new();

    public override void Install(IEntity entity)
    {
        ISignal fireEvent = entity.GetValue<ISignal>("FireEvent");

        fireEvent.Subscribe(_fireVFX.Play).AddTo(_disposables);
        fireEvent.Subscribe(_fireSFX.Play).AddTo(_disposables);
        fireEvent.Subscribe(() => _animator.SetTrigger("Fire")).AddTo(_disposables);
    }

    public override void Uninstall(IEntity entity)
    {
        _disposables.Dispose();
    }
}
```

Почему так правильно: каждый `Subscribe` возвращает `IDisposable`, `.AddTo(_disposables)` приписывает его одному владельцу, и снятие — это один `Dispose()`, про который нельзя забыть при добавлении четвёртой подписки. Композит `private readonly ... = new()` создаётся один раз на инсталлер. Подписки через группу методов (`_fireVFX.Play`, `_fireSFX.Play`) аллоцируют по одному делегату и не требуют замыкания; замыкание нужно только строке с аниматором, и оно захватывает поле, а не локальную переменную. Правило из `Docs/Entities/Installers/Manual.md`: «Always override `Uninstall` when working with subscriptions or `IDisposable` objects to ensure proper cleanup.»

Две поправки к документации, которые нужны, чтобы это компилировалось:
* В документации (`UninstallEntityInstaller.md`, `UsingSubscriptionsWithDisposeComposite.md`) написано `public override void Uninstall()` без параметра. Настоящая подпись — `Uninstall(IEntity entity)`.
* `Subscribe` — не член `ISignal`. У `ISignal` единственный член `event Action OnEvent`; `Subscribe` — расширение из `Elements/Scripts/Events/Extensions.cs`: `public static Subscription Subscribe(this ISignal it, Action action) => new(it, action);`.

**Безаллокационный вариант того же.** `Subscription` — `readonly struct`, а `AddTo(this IDisposable it, DisposableComposite composite)` принимает интерфейс, поэтому каждая строка выше **боксит** структуру. Если подписок много и они на горячем пути создания сущностей, используйте типизированный композит:

```csharp
private readonly DisposableComposite<Subscription> _subs = new();

public override void Install(IEntity entity)
{
    ISignal fireEvent = entity.GetValue<ISignal>("FireEvent");

    fireEvent.Subscribe(_fireVFX.Play).AddTo(_subs);   // AddTo<T>(this T, DisposableComposite<T>) — без боксинга
    fireEvent.Subscribe(_fireSFX.Play).AddTo(_subs);
}

public override void Uninstall(IEntity entity) => _subs.Dispose();
```

Это работает, потому что рядом объявлена вторая перегрузка: `public static void AddTo<T>(this T it, DisposableComposite<T> composite) where T : IDisposable`. Ограничение: в один `DisposableComposite<Subscription>` ложатся только подписки без аргументов — `Subscription<T>` это другой тип.

**Анти-паттерн.** Подписаться в `Install` и никогда не отписаться (для пулящихся и респавнящихся сущностей инсталлер живёт дольше сущности, обработчики накапливаются и стреляют по мёртвому состоянию). Или хранить три отдельных `IDisposable`-поля и диспоузить каждое руками — именно это и рассинхронизируется, когда добавляют четвёртое.

### 5. `Optional<T>` для условной установки и для scene-override общего ассета

```csharp
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

public sealed class WeaponInstaller : MonoEntityInstaller<IWeaponEntity>
{
    [SerializeField] private Optional<ReactiveInt> _ammo;
    [SerializeField] private Optional<Cooldown> _cooldown;

    public override void Install(IWeaponEntity entity)
    {
        if (_ammo)
            entity.AddValue("Ammo", _ammo.Value);        // .Value обязателен, см. ниже

        if (_cooldown)
            entity.AddValue("Cooldown", _cooldown.Value);

        entity.AddValue("FireRequest", new Request());
        entity.AddValue("FireEvent", new Event());
        entity.AddBehaviour<FireProjectileBehaviour>();
    }
}
```

Два слоя — дефолты в ассете, дельты в сцене:

```csharp
// Общий ассет задаёт дефолты для всех врагов
public sealed class EnemyInstaller : ScriptableEntityInstaller<IEnemyEntity>
{
    [SerializeField] private ReactiveInt _health;
    [SerializeField] private ReactiveInt _damage;
    [SerializeField] private ReactiveFloat _speed;

    public override void Install(IEnemyEntity entity)
    {
        entity.AddValue("Health", _health);
        entity.AddValue("Damage", _damage);
        entity.AddValue("Speed", _speed);
    }
}

// Компонент сцены переопределяет только то, что отметил дизайнер
public sealed class OverrideEnemyInstaller : MonoEntityInstaller<IEnemyEntity>
{
    [SerializeField] private Optional<ReactiveInt> _health;
    [SerializeField] private Optional<ReactiveInt> _damage;
    [SerializeField] private Optional<ReactiveFloat> _speed;

    public override void Install(IEnemyEntity entity)
    {
        if (_health) entity.SetValue("Health", _health.Value);
        if (_damage) entity.SetValue("Damage", _damage.Value);
        if (_speed)  entity.SetValue("Speed",  _speed.Value);
    }
}
```

Почему так правильно: у `Optional<T>` есть `operator true` / `operator false`, поэтому `if (_ammo)` читается как флаг фичи. Отсутствующая фича вообще не попадает в сущность: массив значений остаётся коротким, а поведения, которые ищут это значение, его просто не найдут — это дешевле и честнее, чем ставить «выключенный» sentinel. Двухслойная схема опирается на документированный порядок `MonoEntity`: `scriptableInstallers` исполняются **до** `sceneInstallers`, поэтому `Add*` в ассете и `Set*` в сцене складываются корректно, не зная друг о друге.

Про `.Value`: `Optional<T>` — **структура** с `implicit operator T(Optional<T>)`. Когда целевой параметр типизирован (как в сгенерированном `entity.AddAmmo(_ammo)`), неявное приведение сработает. Но `AddValue(string, object)` принимает `object`, а приведение `Optional<ReactiveInt> -> object` — это стандартный боксинг, который по правилам C# выигрывает у пользовательского приведения. То есть `entity.AddValue("Ammo", _ammo)` положит в сущность **боксированный `Optional<ReactiveInt>`**, а не `ReactiveInt`, и ближайший `GetValue<ReactiveInt>("Ammo")` упадёт. С нетипизированным API всегда пишите `.Value`. (Факт про типы и операторы проверен по `Elements/Scripts/Utils/Optional.cs`; вывод про выбор приведения — следствие правил перегрузки C#.)

**Анти-паттерн.** `bool _hasAmmo` рядом с `int _ammo` — два поля, которые могут разойтись. Или установка всех значений безусловно и проверка «магического» нуля/-1 в поведениях.

### 6. `AtomicUtils.IsPlayMode()` вокруг всего, что имеет побочный эффект

```csharp
using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

[Serializable]
public sealed class CharacterSystemInstaller : IEntityInstaller<IPlayerContext>
{
    [SerializeField] private GameEntity _characterPrefab;

    public void Install(IPlayerContext context)
    {
        if (AtomicUtils.IsPlayMode())
        {
            GameContext gameContext = GameContext.Instance;
            GameEntity character = CharacterUseCase.Spawn(context, gameContext, _characterPrefab);
            context.AddValue("Character", character);
            gameContext.WhenDisable(character.Disable);
        }

        context.AddBehaviour<CharacterMoveController>();
        context.AddBehaviour<CharacterFireController>();
        context.AddBehaviour<CharacterRespawnController>();
    }
}
```

Почему так правильно: `Install` исполняется и в Edit Mode — его зовут `MonoEntity.Compile()` (из `OnValidate`, если включён `autoCompile`, или из контекстного меню) и `Compile()` бейкера при precompute. Всё с побочными эффектами — `Instantiate`, разрешение синглтона, регистрация колбэка — должно сидеть за `IsPlayMode()`, а чистая композиция (`AddBehaviour`) остаётся снаружи, чтобы редакторный превью всё ещё считал правильные counts и рисовал гизмы. Реализация (`Elements/Scripts/Utils/AtomicUtils.cs`) — `[AggressiveInlining]`, в билде это `return true`, то есть ветка схлопывается:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static bool IsPlayMode()
{
#if UNITY_EDITOR
    return EditorApplication.isPlaying;
#else
    return true;
#endif
}
```
Есть и парный `IsEditMode()` — `!isPlaying && !isCompiling`.

**Анти-паттерн.** Незащищённый `Instantiate` или `Singleton.Instance` внутри `Install`: он сработает при `OnValidate`-driven precompile и либо намусорит в редакторной сцене, либо бросит — а `Compile()` проглотит исключение в красный `Debug.LogError`.

### 7. Обратимый аспект: `Apply` и `Discard` построчно обратны

```csharp
using Atomic.Entities;
using UnityEngine;

public sealed class JumpAspect : MonoEntityAspect<IGameEntity>
{
    [SerializeField] private float _jumpForce = 3f;

    public override void Apply(IGameEntity entity)
    {
        entity.AddTag("Jumpable");
        entity.AddValue("JumpForce", _jumpForce);
        entity.AddBehaviour<JumpBehaviour>();
    }

    public override void Discard(IGameEntity entity)
    {
        entity.DelTag("Jumpable");
        entity.DelValue("JumpForce");
        entity.DelBehaviour<JumpBehaviour>();
    }
}
```

Почему так правильно: `Discard` зеркалит `Apply` строка за строкой, в том же порядке, с `Del*`-парой на каждый `Add*`. Эта симметрия — весь аргумент корректности аспектов, и она проверяется глазами на ревью: три `Add`, три `Del`. Фреймворк помогает тем, что `Discard` на `MonoEntityAspect<E>` / `ScriptableEntityAspect<E>` объявлен `abstract` (в отличие от инсталлерного `Uninstall`, который `virtual` no-op) — забыть нельзя. Типизация на `IGameEntity`, а не на `IEntity`, даёт доступ к сгенерированным свойствам без касты.

Обратите внимание: `DelBehaviour<T>()` — член `IEntity` (`bool DelBehaviour<T>() where T : IEntityBehaviour`), без ограничения `new()`, в отличие от `AddBehaviour<T>()`.

### 8. `ScriptableEntityAspect` как разделяемый бесстатусный бафф + триггер

```csharp
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SpeedBuff",
    menuName = "SampleGame/SpeedBuff"
)]
public sealed class SpeedBuff : ScriptableEntityAspect
{
    [SerializeField] private float _factor = 2f;

    public override void Apply(IEntity entity)
    {
        IVariable<float> speed = entity.GetValue<IVariable<float>>("Speed");
        speed.Value *= _factor;
    }

    public override void Discard(IEntity entity)
    {
        IVariable<float> speed = entity.GetValue<IVariable<float>>("Speed");
        speed.Value /= _factor;
    }
}

public sealed class EntityAspectTrigger : MonoBehaviour
{
    [SerializeField] private ScriptableEntityAspect _aspect;

    private void OnTriggerEnter(Collider other)
    {
        if (other.FindEntityInParent(out IEntity entity))
            _aspect.Apply(entity);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.FindEntityInParent(out IEntity entity))
            _aspect.Discard(entity);
    }
}
```

Почему так правильно: аспект хранит только свою конфигурацию (`_factor`) и никакого per-entity состояния, поэтому один ассет обслуживает все сущности в игре. Документация называет это прямо: «Use flyweight pattern — the same aspect instance can be applied to multiple entities for reusability» (`Docs/Entities/Aspects/Manual.md`). Триггер знает только абстрактное поле `ScriptableEntityAspect`, так что дизайнеры меняют баффы в инспекторе, не трогая код. Вызов `_aspect.Apply(entity)` попадает в `[AggressiveInlining]`-расширение из `Aspects/Extensions.cs`.

Поправка к документации: её пример триггера написан как `collider.TryGetComponent(out IEntity entity)`. Во фреймворке для этого есть собственные расширения — `other.TryGetEntity(out var entity)`, если `IEntity` на том же объекте, и `other.FindEntityInParent(out var entity)`, если коллайдер висит на дочернем объекте сущности (обычная раскладка: коллайдеры отдельными детьми). Выше взят второй вариант как более часто правильный.

**Анти-паттерн.** `GetComponent<MyConcreteEntity>()` + null-проверка (приколачивает триггер к одному классу сущности). Или хранение «скорости до баффа» в поле аспекта — ломается в момент, когда в триггер входит вторая сущность.

Предупреждение про сам множитель: `speed *= f` / `speed /= f` терпимо для одного применения, но не реентерантно — два пересекающихся применения одного ассета плюс округление float дадут дрейф. Если баффы могут накладываться, храните учёт на сущности (например стек модификаторов в значении), а не в арифметике аспекта.

### 9. Бейкер поверх общей фабрики (настоящие подписи)

Документация (`Docs/BestPractices/OverrideEntityFactoriesWithBakers.md`) показывает этот паттерн на `MonoEntityBaker<IUnitEntity>` с `protected sealed override IUnitEntity Create()` — **это не компилируется**: единственный типовой параметр одноаргументного бейкера это `TArgs : IArgs`, а `Create` имеет пять параметров. Корректная форма:

```csharp
using Atomic.Entities;
using UnityEngine;

public interface IUnitEntity : IEntity
{
}

// Фабрика-шаблон: один ассет на тип юнита
public abstract class UnitFactory : ScriptableEntityFactory<IUnitEntity, NoArgs>
{
    protected sealed override IUnitEntity Create(
        int tagCapacity,
        int valueCapacity,
        int behaviourCapacity,
        Entity.Settings settings,
        NoArgs args
    )
    {
        UnitEntity entity = new UnitEntity(this.name, tagCapacity, valueCapacity, behaviourCapacity, settings);
        this.Install(entity);
        return entity;
    }

    protected abstract void Install(IUnitEntity entity);
}

// Бейкер сцены: только per-instance override
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
        IUnitEntity entity = _factory.Create(args);
        this.Override(entity);
        return entity;
    }

    protected abstract void Override(IUnitEntity entity);
}
```

Почему так правильно: двухфазная композиция. Фабрика владеет конструированием и дефолтами (один шаблон на тип юнита, ёмкости уже посчитаны её собственным `Compile`), бейкер сцены владеет только override. `sealed override Create` в обоих фиксирует последовательность конструирования, чтобы наследник не смог проскочить мимо фабрики, а `protected abstract Install`/`Override` остаются единственной точкой расширения — это template method применённый к созданию сущностей. Дизайнер перестраивает конкретный танк в сцене, не трогая фабрику, и все танки сохраняют одинаковую структуру.

Здесь `UnitEntity` — проектный класс, наследник `Entity`; конструктор `Entity(string, int, int, int, Settings?)` существует и принимает именно эти пять аргументов.

Простой случай без фабрики — тот, с которого стоит начинать:

```csharp
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

public sealed class EnemyBaker : MonoEntityBaker<NoArgs>
{
    [SerializeField] private int _health = 100;
    [SerializeField] private Const<float> _speed = 5f;

    protected override void Install(IEntity entity, NoArgs args)
    {
        entity.AddTag("Enemy");
        entity.AddValue("Health", new ReactiveInt(_health));
        entity.AddValue("Speed", _speed);
        entity.AddBehaviour<PatrolBehaviour>();
    }
}
```

**Анти-паттерн.** Дублировать полную композицию сущности в бейкере, чтобы объекты сцены и объекты фабрики со временем разошлись. Или собирать сущность в бейкере вообще без capacity-подсказок (ёмкости нулями и `autoCompile` выключен), чтобы каждый `AddValue`/`AddBehaviour` растил массив.

### 10. Пакетный бейкинг прямо в коллекцию вызывающего

```csharp
using System.Collections.Generic;
using Atomic.Entities;

// Удобный путь: массив, который потом копируется в мир
IEntity[] enemies = MonoEntityBaker<IEntity, NoArgs>.BakeAll(NoArgs.Default);

EntityWorld world = new EntityWorld();
world.AddRange(enemies);
```

```csharp
// Пропускной путь: печём сразу в мир, промежуточного массива нет
EntityWorld world = new EntityWorld();
MonoEntityBaker<IEntity, NoArgs>.BakeAll(NoArgs.Default, world);

// или в заранее выделенный буфер
List<IEntity> buffer = new List<IEntity>(capacity: 256);
MonoEntityBaker<IEntity, NoArgs>.BakeAll(NoArgs.Default, buffer, includeInactive: false);
```

Почему так правильно: цепочка такая — `EntityWorld : EntityWorld<IEntity>, IEntityWorld`, а `EntityWorld<E> : EntityCollection<E>`, который реализует `IEntityCollection<E> : IReadOnlyEntityCollection<E>, ICollection<E>, IDisposable`. То есть мир — это `ICollection<IEntity>`, и его можно передать прямо как `destination`. Перегрузки с `ICollection<TEntity>` существуют именно для этого: одноразовая конвертация уровня не аллоцирует промежуточный массив, который сразу же копируется в мир. `AddRange` — расширение на `IEntityCollection<E>` из `Entities/Scripts/Collections/Extensions.cs`:

```csharp
public static void AddRange<E>(this IEntityCollection<E> it, params E[] entities) where E : IEntity;
public static void AddRange<E>(this IEntityCollection<E> it, IEnumerable<E> entities) where E : IEntity;
```

Два предупреждения.

Первое, и его стоит воспринимать как баг: `BakeAll(TArgs args, bool includeInactive = true)` создаёт массив размером `bakers.Length`, но пропускает бейкеры с неактивным GameObject при `includeInactive == false` — в возвращённом массиве остаются `default`/`null`-дырки. Тот же дефект в `Bake(TArgs args, GameObject gameObject, bool includeInactive = true)`. При `includeInactive: false` пользуйтесь только `ICollection`-перегрузками.

Второе: `EnemyBaker.BakeAll(NoArgs.Default)` синтаксически работает (статик унаследован), но это **тот же** `MonoEntityBaker<IEntity, NoArgs>.BakeAll` — он найдёт все бейкеры этого замыкания в сцене, не только `EnemyBaker`. Если нужна выборка по конкретному типу бейкера, фильтруйте сами.

**Анти-паттерн.** `BakeAll()` в массив, затем `world.AddRange(array)`, затем выброс массива: два прохода и мусорный массив на каждую загрузку уровня. Хуже — вызывать `Bake()` в кадровом цикле: бейкинг уничтожает GameObject, это операция времени загрузки.

### 11. `MonoEntityInstallerConfigurable`: фичи без MonoBehaviour на каждую

```csharp
using System;
using Atomic.Entities;
using UnityEngine;

[Serializable]
public sealed class HealthInstaller : IEntityInstaller
{
    [SerializeField] private int _health = 100;

    public void Install(IEntity entity) => entity.AddValue("Health", _health);
}

[Serializable]
public sealed class TeamInstaller : IEntityInstaller
{
    [SerializeField] private int _teamId;

    public void Install(IEntity entity) => entity.AddValue("TeamId", _teamId);
}
```

Почему так правильно: фича — это одно expression-bodied выражение с одним сериализованным полем; тестировать нечего, ломать нечего. Классы складываются в `[SerializeReference] IEntityInstaller[] _installers` компонента `MonoEntityInstallerConfigurable`, так что сущность собирается из разнородных типов инсталлеров **одним** компонентом на GameObject вместо N. Это строго дешевле N MonoBehaviour: нет Unity-объекта на фичу, нет `Awake`/`OnValidate`-трафика на фичу, а цикл в `Install` null-безопасен и не аллоцирует.

**`[Serializable]` обязателен** — без него Unity не создаст и не сериализует экземпляр в `[SerializeReference]`-массив. Документация (`Docs/Entities/Installers/MonoEntityInstallerConfigurable.md`) показывает этот пример **без** атрибута; это ошибка.

Второе ограничение, о котором нужно договориться в команде: у `IEntityInstaller` нет `Uninstall`, а `MonoEntityInstallerConfigurable` его не переопределяет. Вложенные инсталлеры **никогда** не получат teardown. Либо не подписывайтесь из них, либо не используйте этот компонент для фич с подписками — пишите свой `MonoEntityInstaller`, который владеет композитом.

**Анти-паттерн.** По одному наследнику `MonoEntityInstaller` на каждое поле, каждому нужен свой компонент на GameObject и своя ссылка на `Transform`.

### 12. Подкласс `ScriptableEntityBootstrapper` под проект

```csharp
using Atomic.Entities;
using UnityEngine;

[CreateAssetMenu(fileName = "GameBootstrapper", menuName = "Game/Bootstrapper")]
public sealed class GameBootstrapper : ScriptableEntityBootstrapper { }
```

Почему так правильно: пустой наследник — вся реализация. Вы получаете отдельный тип ассета со своим пунктом в Create-меню (проект может нести несколько независимых бутстрапперов), а вся логика сопоставления и спавна остаётся во фреймворке. Вместе со `Scene Regex` = `^Gameplay.*$` и `Mode = AfterSceneLoad` глобальные менеджеры, камеры и game-context сущности появляются в каждой подходящей сцене без кода бутстрапа. Обнаружение через `Resources.LoadAll` означает, что «добавить глобальную сущность» = добавить ассет, а не править загрузчик.

Если нужно другое сопоставление сцен — переопределяйте `IsAvailable(Scene)`, он `protected virtual`:

```csharp
public sealed class GameBootstrapper : ScriptableEntityBootstrapper
{
    [SerializeField] private string[] _sceneNames;

    protected override bool IsAvailable(Scene scene) =>
        System.Array.IndexOf(_sceneNames, scene.name) >= 0;
}
```
Точное сравнение имён вместо незаякоренного regex снимает целый класс ошибок (см. «Грабли»).

### 13. Спавн префаба `MonoEntity` в рантайме

```csharp
using Atomic.Entities;
using UnityEngine;

public sealed class EnemySpawner : MonoBehaviour
{
    [SerializeField] private MonoEntity _enemyPrefab;
    [SerializeField] private Transform _spawnPoint;

    public MonoEntity Spawn() =>
        MonoEntity.Create(_enemyPrefab, _spawnPoint.position, _spawnPoint.rotation, this.transform);
}
```

Почему так правильно: эта перегрузка проходит через `SetupAfterCreation`, то есть возвращает уже `Register()`-нутую и `Install()`-нутую сущность с именем префаба — ничего доустанавливать вручную не нужно. Для массового спавна есть `CreateAsync`:

```csharp
public async ValueTask<MonoEntity[]> SpawnWave(int count) =>
    await MonoEntity.CreateAsync(_enemyPrefab, count, this.transform);
```

Контраст, который важно знать: `MonoEntity.Create<E>(in CreateArgs)` и именованные перегрузки `Create<E>(name: ..., tags: ..., ...)` создают сущность с нуля и **не** вызывают `Install()` сами. Там установка происходит только через `installOnAwake` (который у именованных перегрузок по умолчанию `true`, а у `default(CreateArgs)` — `false`) или явным вызовом:

```csharp
// default(CreateArgs): installOnAwake == false, ёмкости == 0 -> клампятся в 1
MonoEntity e = MonoEntity.Create(new MonoEntity.CreateArgs
{
    name = "Runtime Entity",
    installOnAwake = true,
    uninstallOnDestroy = true,
    disposeValues = true,
    useUnityLifecycle = true,
    initialTagCapacity = 4,
    initialValueCapacity = 8,
    initialBehaviourCapacity = 2
});
```
Заполняйте `CreateArgs` целиком: это структура, и забытое поле — это `false`/`0`, а не «как в инспекторе».

---

## Производительность

Ниже разделено на то, что **заявлено** в документации, то, что **видно в коде** (атрибуты, типы, формы циклов), и то, что является **выводом**. Измерений (бенчмарков) в репозитории по этой области нет — цифр здесь не будет.

**Инлайн — видно в коде.**
* `Aspects/Extensions.cs`: все четыре `Apply`/`Discard` — expression-bodied форвардеры с `[MethodImpl(MethodImplOptions.AggressiveInlining)]`. Документация (`Docs/Entities/Aspects/Extensions.md`) заявляет, что они «optimized with aggressive inlining» чтобы «minimize call overhead».
* `Entities/Extensions.cs`: `Install(this IEntity, IEntityInstaller)` — `AggressiveInlining` и возвращает сущность; `Install(this IEntity, IEnumerable<IEntityInstaller>)` — **без** инлайна и с `foreach` по интерфейсу. Для не-массивного источника это аллокация (и для структурного энумератора — боксинг). В коде композиции предпочитайте одноинсталлерную перегрузку.
* `MonoEntity_Installing.cs`: `Uninstall()`, `OnInstall()`, `OnUninstall()`, `InstallAll(Scene, bool)`, `InstallAll<E>(Scene, bool)`, `InstallAll(Component, bool)` — все `AggressiveInlining`. `Install()` — **нет**.
* `MonoEntity_Creation.cs`: все `Create`/`CreateAsync` — `AggressiveInlining`.
* `AtomicUtils.IsPlayMode()` / `IsEditMode()` — `AggressiveInlining`; в билде первое компилируется в `return true`.
* `Extensions.TryGetEntity` (4 перегрузки), `FindEntityInParent` (4 перегрузки), `DrawGizmos(this IEntity)` — все `AggressiveInlining`.

**Итерация без аллокаций — видно в коде.**
* `MonoEntity.Install/Uninstall` обходят `List<MonoEntityInstaller>` / `List<ScriptableEntityInstaller>` индексным `for (int i = 0, count = list.Count; i < count; i++)` с поднятым count. Никаких `foreach` по `List<T>` (который аллоцировал бы, будь он по интерфейсу). Единственная потенциальная аллокация — интерполяция строки в `Debug.LogWarning`, и она происходит только на пустом слоте.
* `MonoEntityInstallerConfigurable.Install` делает `foreach (IEntityInstaller installer in _installers)` по полю, объявленному как `IEntityInstaller[]`. Компилятор для массива выдаёт индексный цикл, энумератор не создаётся. Плюс один компонент вместо N — это N несозданных Unity-объектов и N не вызванных `Awake`/`OnValidate`.
* Все статические методы бейкера поднимают count (`for (int i = 0, count = bakers.Length; ...)`), а сценовые перегрузки используют вложенный двойной цикл с поднятыми границами.
* Гизмы бейкера: `_previewEntity.GetBehaviourAt(i)` + паттерн-тест `is IEntityGizmos`. Ни LINQ, ни итераторов, ни аллокаций. Но `BehaviourCount` перечитывается на каждой итерации, и проверка типа на интерфейс бежит по каждому поведению на каждый кадр перерисовки — а `OnDrawGizmos` вызывается на каждый repaint, если `onlySelectedGizmos == false`. Редактор-онли.
* `ScriptableEntityBootstrapper.Initialize()` поднимает count и рано выходит при `count == 0`.

**Что аллоцирует — видно в коде.**
* `InstallFromScene` / `InstallFromScene<T>`: `scene.GetRootGameObjects()` (один массив) + `GetComponentsInChildren<...>(includeInactive)` **на каждый корневой объект** (массив на корень). Это операция времени загрузки, не кадра.
* `BakeAll` использует `FindObjectsByType<MonoEntityBaker<TEntity, TArgs>>(include, FindObjectsSortMode.None)` — один массив, `SortMode.None` пропускает сортировку Unity. Это самый дешёвый из поисковых путей.
* `Bake(TArgs, Scene, ...)` аллоцирует `GetRootGameObjects()` плюс массив компонентов на каждый корень; `List`-возвращающая перегрузка дополнительно создаёт `new List<TEntity>()` без capacity, который растёт. `ICollection`-перегрузки убирают только результирующий массив/список, но **не** массивы компонентов на корень.
* `Bake(TArgs args)` принимает `args` **по значению**, не по `in`. Статические batch-методы передают ту же `TArgs` вниз на каждый бейкер — `Args<T1, T2, T3, T4>` копируется один раз на бейкер. Держите args маленькими.

**Боксинг.**
* `IArgs` — пустой маркер, а `NoArgs` / `Args<...>` — структуры; типовой параметр ограничен `where TArgs : IArgs`, и пока вызов остаётся generic, боксинга нет. **Риск:** в момент, когда вы кладёте `TArgs` в поле, локальную переменную или коллекцию типа `IArgs`, он боксится.
* `Subscription`, `Subscription<T>`, `Subscription<T1, T2>`, `Subscription<T1, T2, T3>`, `Subscription<T1, T2, T3, T4>` — все `public readonly struct : IDisposable` (`Elements/Scripts/Events/Subscription.cs`). `AddTo(this IDisposable it, DisposableComposite composite)` принимает интерфейс, поэтому канонический `signal.Subscribe(x).AddTo(_disposables)` **боксит по одному объекту на подписку** сверх делегата. Три подписки = три делегата **плюс** три бокса на каждую установку. Безбоксинговый путь — generic `AddTo<T>(this T it, DisposableComposite<T> composite) where T : IDisposable` с `DisposableComposite<Subscription>`.
* `Optional<T>` — структура с `implicit operator T`; передача её в `object`-параметр (`AddValue(string, object)`) боксит саму `Optional<T>` вместо приведения к `T`. Это и аллокация, и логическая ошибка. Пишите `.Value`.
* `Const<T>` — **класс**, не структура (`Elements/Scripts/Values/Const.cs`: `public class Const<T> : IValue<T>`), с неявными приведениями в обе стороны. Значит каждое поле `[SerializeField] private Const<float> _moveSpeed = 5.0f;` — это объект в куче, и неявное приведение в инициализаторе создаёт ещё один. Для сериализованных полей это нормально (один раз на инсталлер), но не создавайте `Const<T>` в горячем коде.
* Аспектные расширения `Apply<E>` / `Discard<E>` ограничены только `where E : IEntity`, без `class`. Если `E` — структурная сущность, она копируется в расширение по значению и ещё раз в вызов через интерфейс. Для структурных сущностей берите интерфейсную перегрузку, либо держите сущности классами.

**Capacity precompute — заявлено и видно.**
`Docs/Entities/Baking/Manual.md`: «Parameters like `initialTagCapacity` and `initialValueCapacity` help optimize entity creation and reduce runtime overhead.» `Docs/Entities/Baking/MonoEntityBaker`1.md`: «These parameters help optimize entity creation by reducing dynamic resizing during runtime.» В коде: `Compile()` в Edit Mode создаёт preview-сущность и записывает обратно её `TagCount`/`ValueCount`/`BehaviourCount`, после чего рантайм-сущность выделяет массивы один раз и при установке их не растит. Оговорка: ёмкости тегов и значений округляются вверх до простого числа (`CeilToPrime`), потому что это slot/bucket-массивы хеш-таблицы; ровно по запросу выделяется только массив поведений. Это основная причина, по которой бейкинг существует.

**Пулинг — заявлено и видно.**
`MonoEntityBakerOptimized` делегирует конструирование общей `ScriptableEntityFactory` (один шаблон, ёмкости уже разрешены) и в `Release()` возвращает `EntityView` в `EntityWorldViewSingleton<TKey, TEntity, TView>.Instance.Pool`, вместо уничтожения. Его собственный xml-doc: «Used for efficient pooling and binding», «a higher-performance workflow». Оговорка уже названа в разделе API: `EntityWorldViewSingleton<K, E, V>` абстрактен, и `Instance` бросает, если в сцене нет конкретного наследника этого замыкания.

**Утечка в редакторе — вывод из кода, не из документации.**
Конструктор `Entity` заканчивается `EntityRegistry.Instance.Register(this)`. `Compile()` бейкера создаёт одноразовую preview-сущность и кладёт её в `_previewEntity` **без** `Dispose`/`Unregister`. Значит каждый `OnValidate` при включённом `autoCompile` добавляет одну зарегистрированную сущность в глобальный `EntityRegistry` на время сессии редактирования. Тот же паттерн у `ScriptableEntityFactory` (`_previewEntity = this.Create(default)`), и у фабрики `autoCompile` по умолчанию **включён**. На билд это не влияет, на длинную сессию в редакторе — влияет.

**Доступность для оптимизации — видно в коде.**
`Compile()` бейкера — `private protected virtual`, `_previewEntity` — `private protected`, три поля ёмкостей и `settings` — `private`. Бейкер проекта в другом asmdef действительно **не может** переопределить `Compile` или прочитать ёмкости: они доходят только как параметры `Create`. Если вам нужен свой precompute, придётся повторять его в `Create`.

**Чем ранжирует документация.**
`Docs/BestPractices/ModularEntityInstallers.md` отдельно отмечает, что «Split by Methods» «slightly less performant due to additional method calls» относительно одного плоского `Install`, и всё равно рекомендует модульные инсталлеры за переиспользование. На практике `entity.Install(x)` — `AggressiveInlining`, а per-feature инсталлер — обычный `[Serializable]`-класс без Unity-объекта за ним, так что модульная форма почти ничего не стоит на загрузке.

---

## Грабли

1. **`IEntityInstaller` не имеет `Uninstall`.** `Uninstall` есть только на базовых классах `MonoEntityInstaller` / `ScriptableEntityInstaller`. Вложенный `[Serializable] class Foo : IEntityInstaller` внутри `MonoEntityInstallerConfigurable` (или как поле другого инсталлера) teardown не получит никогда. Подписки должны принадлежать внешнему `MonoEntityInstaller`.
2. **`MonoEntityInstallerConfigurable` не переопределяет `Uninstall`.** Это не оплошность, которую можно обойти: класс `sealed`. Не используйте его для фич с подписками.
3. **`Uninstall` — `virtual` no-op.** Фреймворк не заставит вас его написать (в отличие от аспектного `Discard`, который `abstract`). Правило из документации: всегда переопределяйте `Uninstall`, если работаете с подписками или `IDisposable`.
4. **Порядок `Uninstall` — не точное зеркало `Install`.** Инсталлеры идут в обратном порядке, но `OnUninstall()` вызывается **до** циклов, тогда как `OnInstall()` — **после**. Плюс `Uninstall` защищён `if (!_installed) return;`.
5. **Отсутствующий слот инсталлера не бросает.** Null в `sceneInstallers`/`scriptableInstallers` даёт `Debug.LogWarning` и пропускается. Битая ссылка в инспекторе тихо даёт недонастроенную сущность, которая упадёт позже и в другом месте.
6. **Неправильный типизированный инсталлер — ошибка Play Mode, не компиляции.** `IEntityInstaller<E>`, `MonoEntityInstaller<E>`, `ScriptableEntityInstaller<E>` бросают `InvalidCastException` в рантайме.
7. **Асимметрия ограничений.** Интерфейс `IEntityInstaller<in E>` — `where E : IEntity`; базовые классы `MonoEntityInstaller<E>` / `ScriptableEntityInstaller<E>` — `where E : class, IEntity`. Инсталлер для структурной сущности может реализовать интерфейс, но не может наследоваться от базовых классов.
8. **`InstallFromScene<T>` ищет только `MonoEntityInstaller<T>`.** Не `MonoEntityInstaller`, и не `MonoEntityInstaller<IBase>`, когда `T` — производный интерфейс. Для обычных нетипизированных инсталлеров она молча ничего не найдёт. Нетипизированная перегрузка ищет `MonoEntityInstaller`.
9. **`WhenUninstall` не имеет «позднего входа».** `WhenInstall(action)` вызывает `action` немедленно, если сущность уже установлена; `WhenUninstall` просто подписывается. Оба бросают `ArgumentNullException` на null.
10. **`MonoEntity.Create<E>(in CreateArgs)` не вызывает `Install()`.** Он создаёт неактивный GameObject, настраивает его и активирует; установка случается только потому, что `Awake()` зовёт `Install()`, если `installOnAwake`. У `default(CreateArgs)` все флаги `false` — сущность не будет установлена, не будет диспоузить значения и не будет жить по Unity-lifecycle.
11. **У `CreateArgs` и у именованных перегрузок разные дефолты.** В `Create<E>(string name = null, ...)` все четыре флага по умолчанию `true`, а ёмкости `1`. В `default(CreateArgs)` — `false` и `0`. Плюс параметры называются `initialTagCount`, а присваиваются в `initialTagCapacity`.
12. **Ёмкости в `Create<E>(in CreateArgs)` клампятся через `Mathf.Max(1, ...)`.** Передать 0, чтобы «не выделять», не получится — будет 1.
13. **Только префабные перегрузки `Create` регистрируют и устанавливают.** `Create<E>(E prefab, ...)`, `Create<E>(E prefab, Scene scene)` и `CreateAsync<E>` проходят через `SetupAfterCreation`. Остальные — нет.
14. **Инсталлеры исполняются в Edit Mode.** `MonoEntity.Compile()` делает `Uninstall(); Construct(); Install(); PrecomputeCapacities();` и его зовёт `OnValidate()` при включённом `autoCompile`. Инспектор `MonoEntity` даже несёт предупреждение: «WARNING: If you create Unity objects or another heavy objects in the Install() method, be sure to turn off!». Любой побочный эффект — за `AtomicUtils.IsPlayMode()`.
15. **Если переопределяете `MonoEntityInstaller.OnValidate`, вызывайте `base.OnValidate()`.** Иначе `onValidateCallback` (он равен `MonoEntity.Compile`) перестанет вызываться и редакторный превью замрёт. Там же try/catch с пустым `// ignored` — ошибки обновления редактора глотаются молча.
16. **`Compile()` бейкера передаёт `default` в `TArgs`.** Любой `Create`/`Install`, который разыменовывает args, даст `NullReferenceException` при edit-mode precompute, а `Compile` превратит это в красный `Debug.LogError`. Защищайтесь от default-args.
17. **`Compile()` бейкера нельзя переопределить из другого asmdef.** Он `private protected virtual`; поля ёмкостей `private`. То же у `ScriptableEntityFactory`.
18. **`Entity.Settings.disposeValues` у бейкера по умолчанию `false`.** Конструктор `Entity` трактует `null` как `disposeValues = true`, но бейкер передаёт сериализованную структуру не-null, а `default(bool)` это `false`. Сущность, созданная бейкером, свои `IDisposable`-значения не диспоузит, пока галочку не поставят. Это реальный путь утечки для инсталлеров, которые кладут подписки в значения.
19. **`Bake()` уничтожает GameObject.** Дефолтный `Release()` — `Destroy(this.gameObject)`. Авторинговый объект после бейкинга **исчезает**. Чтобы сохранить — переопределяйте `Release()`. `MonoEntityBakerOptimized.Release()` делает `Destroy(this)` — только компонент.
20. **Дефект в `BakeAll(TArgs, bool)` и `Bake(TArgs, GameObject, bool)`.** Массив результата размером `bakers.Length`, но неактивные бейкеры пропускаются при `includeInactive == false` — в массиве остаются `default`/`null` дырки. При `includeInactive: false` используйте только `ICollection`-перегрузки. (`ICollection`-перегрузки к тому же бросают `ArgumentNullException` на null-назначение; массивные ничего не валидируют.)
21. **`MyBaker.BakeAll(...)` не ограничивается `MyBaker`.** Статик унаследован от `MonoEntityBaker<TEntity, TArgs>`, и поиск идёт по этому закрытому типу — найдутся все бейкеры того же замыкания.
22. **Файлы гизмо-партиала бейкера целиком под `#if UNITY_EDITOR`.** Поля `onlySelectedGizmos` и `onlyEditModeGizmos` существуют только в редакторе — различие в раскладке сериализации между редактором и плеером. Гизмы требуют `_previewEntity`, то есть `Compile()` должен был отработать.
23. **Три файла этой области не обёрнуты в `#if UNITY_5_3_OR_NEWER`:** `Installers/MonoEntityInstallerConfigurable.cs`, `Baking/MonoEntityBakerOptimized`1.cs` и `Baking/MonoEntityBakerOptimized.cs` — хотя их базовые классы (`MonoEntityInstaller`, `MonoEntityBaker<TEntity, TArgs>`) целиком под этой директивой. У первых двух к тому же `using UnityEngine;` на верхнем уровне без защиты; у `MonoEntityBakerOptimized.cs` своих using нет вообще, но он наследуется от защищённого типа. Не-Unity сборка `Atomic.Entities` сломается на всех трёх.
24. **`MonoEntityBakerOptimized.Release()` зависит от сцены и от internal-API.** `EntityWorldViewSingleton<K, E, V>` абстрактен, и его `Instance` бросает `Exception`, если в сцене нет конкретного наследника именно этого замыкания. Готового наследника для шортката в фреймворке нет: нетипизированный `EntityWorldViewSingleton` наследуется от `EntityWorldView<string, IEntity, EntityView>`, а не от `EntityWorldViewSingleton<string, IEntity, EntityView>`, и `FindAnyObjectByType` его не найдёт. Свойство `Pool` и методы `EntityViewPool.Rent/Return` объявлены `internal` — своя реализация пула вне сборки фреймворка так не напишется.
25. **`ScriptableEntityBootstrapper` работает только из `Resources`.** `Resources.LoadAll<ScriptableEntityBootstrapper>(string.Empty)` — ассет вне `Resources` не будет найден никогда. Этот вызов загружает и удерживает в памяти **все** бутстрапперы проекта.
26. **Regex бутстраппера не заякорен.** `Regex.IsMatch(scene.name, _sceneRegex)` — паттерн `Level_` совпадёт и с `MyLevel_Boss`. Совет из `Docs/Entities/Bootstrap/Manual.md`: «Keep regex patterns simple and specific». Якорьте: `^Level_.*$`. Паттерн не кешируется и интерпретируется на каждый вызов (один раз на старте).
27. **`BootstrapEntities` — `async void`.** Исключение из него уходит в лог Unity и не ловится вызывающим; дождаться завершения нельзя. Для `Mode.AfterSceneLoad` он крутит `while (!scene.isLoaded) await Task.Yield();`. Не рассчитывайте, что бутстрапнутые сущности существуют в `Awake` другого объекта.
28. **Бутстраппер смотрит только на `SceneManager.GetActiveScene()`, один раз.** Это `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`. На последующие аддитивные загрузки сцен он не реагирует. И встроенной защиты от дублей нет — документация сама предупреждает: «Avoid duplicating entities that are already placed in the scene».
29. **`MonoEntitySingleton<E>.Instance` бросает.** Поиск через `FindAnyObjectByType<E>(FindObjectsInactive.Exclude)` — **неактивный** синглтон для него не существует, и вы получите `Exception`. Где отсутствие легально, используйте `TryGetInstance(out E)`. Статик, в отличие от того, что можно подумать, **очищается** в `OnDestroy` (`if (_instance == this) _instance = null;`), а дубликат уничтожается в `Awake` с `Debug.LogError`.
30. **`IEntityAspect<IGameEntity>` — не `IEntityAspect`.** `IEntityAspect` это замкнутая специализация `IEntityAspect<IEntity>`, а не базовый интерфейс с кастующим мостом. Смешанные типизированные аспекты в `IEntityAspect[]` не положить; держите коллекции как `IEntityAspect<E>[]`.
31. **`Optional<T>` в `object`-параметре боксится вместо приведения.** `entity.AddValue("Ammo", _ammo)` положит боксированную `Optional<ReactiveInt>`. Пишите `.Value`. Отдельно: `implicit operator Optional<T>(T it) => new() { value = it, active = it != null }` — для значимого `T` выражение `it != null` всегда истинно, то есть неявное приведение из структуры всегда даёт `active == true`; неактивной `Optional` может быть только заполненная в инспекторе.
32. **Аспект-ассет не должен хранить состояние.** Flyweight — требование, а не рекомендация: один ассет применяется к множеству сущностей. Мультипликативный бафф (`speed *= f` / `speed /= f`) терпим для одного применения, но не реентерантен — два пересекающихся применения плюс округление float дают дрейф.
33. **Документация этой области содержит ошибки компиляции — не копируйте её примеры дословно.** Проверенный список:
    * `UninstallEntityInstaller.md` и `UsingSubscriptionsWithDisposeComposite.md` пишут `public override void Uninstall()` без параметра; настоящая подпись `Uninstall(IEntity entity)`.
    * `UsingOptionalWithInstallers.md` объявляет `public void Install(...)` там, где нужен `public override void Install(...)`, и даёт `EnemyInstaller : ScriptableEntityInstaller<IEnemyEntity>` метод `Install(IWeaponEntity entity)` — не тот тип сущности.
    * `MonoEntityInstaller`1.md` показывает `protected override void Install(UnitEntity entity)`; член `public abstract`.
    * `ScriptableEntityInstaller`1.md` указывает наследование как «MonoEntityInstaller».
    * `MonoEntityInstallerConfigurable.md` даёт примеры вложенных инсталлеров **без** `[Serializable]`, без которого Unity их не сериализует.
    * Baking-документация описывает `MonoEntityBaker<E> : MonoBehaviour where E : IEntity` с `protected abstract E Create()`, `public E Bake()`, non-generic `MonoEntityBaker`, `protected int initialTagCount`, контекстное меню `Precompile` и `static E[] BakeAll(bool includeInactive = true)`. В исходниках: `MonoEntityBaker<TEntity, TArgs>`, `Bake(TArgs args)`, пятипараметровый `Create`, `private int tagCapacity/valueCapacity/behaviourCapacity`, меню `Compile`, и статики с `TArgs` **первым** аргументом.
    * `OverrideEntityFactoriesWithBakers.md` строит весь паттерн на несуществующих `ScriptableEntityFactory<IUnitEntity>.Create()` и `MonoEntityBaker<IUnitEntity>`.
    * Многие примеры используют типы, которых во фреймворке **нет вообще**: `BaseVariable<T>`, `BaseRequest`, `BaseEvent`, `BaseEvent<int>`, `Health`. Реальные: `Variable<T>` (`Elements/Scripts/Variables/Base/Variable.cs`), `ReactiveVariable<T>` (`Elements/Scripts/Variables/Reactive/ReactiveVariable.cs`), `Request`/`IRequest` (`Elements/Scripts/Requests/Request.cs`), `Event`/`IEvent`/`ISignal` (`Elements/Scripts/Events/Event.cs`, `ISignal.cs`). `Health` — проектный тип, не фреймворковый.
    * `MonoEntityBakerOptimized` в папке `Docs/Entities/Baking` не документирован совсем: его `HelpURL` указывает на `MonoEntityBakerOptimized%602.md`, которого там нет.
34. **Тесты по бейкингу не подтверждают ничего.** `Entities/Tests/PlayMode/SceneEntityBaker/SceneEntityBaker_BakeAll_Tests.cs`, `_Bake_GameObject_Tests.cs`, `_Bake_Scene_Tests.cs` и `Entities/Tests/Substitutions/Baker/SceneEntityBakerTestDouble.cs` **полностью закомментированы** и нацелены на мёртвый API (`EntityBaker<IEntity>` с `Create(in int tagCapacity, ...)` без `TArgs`). В `Entities/Tests/Substitutions/Aspects/SceneEntityAspectSpy.cs` объявлен класс с именем `MonoEntityAspectSpy` — имя файла и имя класса расходятся. Живой код по этой области: `PlayMode/SceneEntity/SceneEntityTests_Install.cs`, `SceneEntityTests_InstallEntities.cs` и `Substitutions/Installers/MonoEntityInstallerSpy.cs`. Поведение `Install` (идемпотентность, пропуск null, `installOnAwake`, вызов `OnInstall`) этими тестами подтверждено; поведение бейкеров — нет.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`. Напоминание: ниже исходники — истина, документация — подсказка. Там, где они расходятся, см. грабли №33.

### Документация: инсталлеры

* `Docs/Entities/Installers/Manual.md` — входная точка. Пример `CharacterInstaller : MonoEntityInstaller` с `Const<float>` / `ReactiveVariable<Vector3>`, скриншоты привязки инсталлера к полю `installers` сущности, список всех семи типов инсталлеров и раздел Notes с двумя правилами, которые стоит считать нормативными: «Keep installers focused on entity configuration only» и «Always override `Uninstall` when working with subscriptions or `IDisposable` objects». Внизу — ссылки на пять Best Practices.
* `Docs/Entities/Installers/IEntityInstaller.md` — один метод `Install(IEntity)`, пример `[Serializable] sealed class CharacterInstaller : IEntityInstaller`. Короткий, корректный.
* `Docs/Entities/Installers/IEntityInstaller`1.md` — типизированный вариант; объясняет, что `Install(IEntity)` реализован автоматически кастом к `E`. Пример с `ICharacterEntity : IEntity`. Корректный.
* `Docs/Entities/Installers/MonoEntityInstaller.md` — важен одной фразой из вступления: брать `MonoEntityInstaller` **только** если есть зависимости от сцены или если экземпляры в сцене должны различаться, иначе — `ScriptableEntityInstaller`. Документирует `Install`, `Uninstall(IEntity)` и `OnValidate()`.
* `Docs/Entities/Installers/MonoEntityInstaller`1.md` — тот же набор для типизированной версии. Содержит ошибку: показывает `protected override void Install(UnitEntity entity)`, хотя член публичный.
* `Docs/Entities/Installers/MonoEntityInstallerConfigurable.md` — единственное описание готового компонента с `[SerializeReference] IEntityInstaller[]`. Показывает композицию из `HealthInstaller` и `TeamInstaller`, но **без** `[Serializable]`.
* `Docs/Entities/Installers/ScriptableEntityInstaller.md` — ассетная форма. Пример с `[CreateAssetMenu(fileName = "MoveInstaller", ...)]`. Это дефолтный выбор по документации.
* `Docs/Entities/Installers/ScriptableEntityInstaller`1.md` — типизированная ассетная форма, пример с `UnitEntity : Entity`. В разделе Type ошибочно указано наследование от «MonoEntityInstaller».

### Документация: аспекты

* `Docs/Entities/Aspects/Manual.md` — определение аспекта, полный пример `SpeedBuff : ScriptableEntityAspect` с множителем и пример `EntityAspectTrigger` на `OnTriggerEnter`/`OnTriggerExit`. Раздел Notes несёт два обязательных правила: всегда реализуйте `Discard` и используйте flyweight (один ассет на много сущностей). Триггер в примере использует `collider.TryGetComponent(out IEntity entity)` — у фреймворка есть лучше (`TryGetEntity` / `FindEntityInParent`).
* `Docs/Entities/Aspects/IEntityAspect.md` — нетипизированный интерфейс, `Apply(IEntity)` / `Discard(IEntity)`. Не объясняет, что это замкнутая специализация `IEntityAspect<IEntity>` и что из этого следует.
* `Docs/Entities/Aspects/IEntityAspect`1.md` — типизированный интерфейс, `Apply(E)` / `Discard(E)`.
* `Docs/Entities/Aspects/MonoEntityAspect.md` и `Docs/Entities/Aspects/MonoEntityAspect`1.md` — scene-аспекты как MonoBehaviour. Оба метода `abstract`.
* `Docs/Entities/Aspects/ScriptableEntityAspect.md` и `Docs/Entities/Aspects/ScriptableEntityAspect`1.md` — аспекты-ассеты, рекомендованная форма для баффов и дебаффов.
* `Docs/Entities/Aspects/Extensions.md` — четыре расширения `entity.Apply(aspect)` / `entity.Discard(aspect)`. Единственное место, где заявлено, что они «optimized with aggressive inlining» для «minimize call overhead».

### Документация: бейкинг и bootstrap

* `Docs/Entities/Baking/Manual.md` — концепция бейкинга, три примера (`MonoEntityBaker`, `MonoEntityBaker<E>`, Bake All) и раздел Notes про `initialTagCapacity`/`initialValueCapacity`. **Все три примера написаны против мёртвого API.** В конце файла есть большой HTML-комментарий с развёрнутым описанием идеи («Baking can be thought of as overriding factory defaults», список Key Features, «Bake directly into an existing `ICollection<E>` to reuse memory or append results») — это самое внятное объяснение замысла во всей документации, и оно скрыто.
* `Docs/Entities/Baking/MonoEntityBaker.md` (483 строки) — самый подробный файл области и самый устаревший. Таблицы Inspector Settings (`autoCompile`, три `initial*Capacity`), Context Menu (`Precompile`, `Reset`), Gizmos, и полный API-reference `Bake()` / `Create()` / `Release()` / `Install(IEntity)` / `OnValidate()` / `Reset()` — всё без `TArgs` и с неправильными именами полей. Читать как описание **замысла**, подписи брать из исходников.
* `Docs/Entities/Baking/MonoEntityBaker`1.md` (381 строка) — то же для генерик-версии, с таким же расхождением (один типовой параметр вместо двух).
* `Docs/Entities/Bootstrap/Manual.md` — короткий и точный. Путь создания ассета (`Assets > Create > Atomic > Entities > EntityBootstrapper`), пример пустого наследника `GameBootstrapper`, таблица полей инспектора (`Is Enabled`, `Scene Regex` = `^Gameplay.*$`, `Mode`, `Entity Prefabs`) и четыре правила Best Practices, включая «Keep regex patterns simple and specific», «Use `AfterSceneLoad` mode when spawned entities depend on scene objects» и «Avoid duplicating entities that are already placed in the scene».
* `Docs/Entities/Bootstrap/ScriptableEntityBootstrapper.md` — API: вложенный `enum Mode`, `IsAvailable(Scene)`, `BootstrapEntities(Scene)`. Подписи приведены честно, включая `protected virtual async void BootstrapEntities(Scene scene)`, и есть примечание, что ассет должен лежать в `Resources`, иначе `Resources.LoadAll` его не найдёт. Чего в файле нет — последствий: что `async void` нельзя дождаться и его исключение не поймать, что `Resources.LoadAll(string.Empty)` загружает и удерживает все бутстрапперы проекта, и что `Initialize()` смотрит только на `SceneManager.GetActiveScene()`, один раз, до загрузки первой сцены.

### Документация: Best Practices (это главное по идиомам)

* `Docs/BestPractices/ModularEntityInstallers.md` — **самый полезный файл области.** Три способа организации инсталлеров (Split by Comments, Split by Methods, Modular Installers) с полным кодом каждого и итоговой таблицей, какой для чего. Это источник канонического `entity.Install(_movementInstaller)` и примеров `CharacterInstaller`/`EnemyInstaller`, переиспользующих одни и те же модули. Код использует несуществующий `BaseVariable<Vector3>` — замените на `ReactiveVariable<Vector3>`.
* `Docs/BestPractices/UninstallEntityInstaller.md` — пример `WeaponViewInstaller` с `DisposableComposite` и объяснение, когда вызывается `Uninstall`. Подпись `Uninstall()` без параметра — ошибка.
* `Docs/BestPractices/UsingSubscriptionsWithDisposeComposite.md` — тот же пример подробнее, с объяснением `AddTo` и списком выгод. Та же ошибка в подписи. Ни один из двух файлов не упоминает, что `AddTo(IDisposable, DisposableComposite)` боксит структуру `Subscription`.
* `Docs/BestPractices/UsingOptionalWithInstallers.md` — три сценария `Optional<T>`: опциональное состояние (`Optional<ReactiveInt> _ammo`, `Optional<Cooldown> _cooldown`), опциональная визуализация (`Optional<ParticleSystem>` и подписки через `.Value`) и override общего ассета (`EnemyInstaller` с `Add*` плюс `OverrideEnemyInstaller` с `Optional<T>` и `Set*`). Идиома здесь ценная, код — нет: пропущенные `override`, неверные типы сущностей в подписях, `BaseRequest`/`BaseEvent`.
* `Docs/BestPractices/UsingUtilsForEntityInstallers.md` — единственный файл про `AtomicUtils.IsPlayMode()` в инсталлерах. Объясняет, почему спавн префаба должен быть под guard, а `AddBehaviour` — нет. В примере `CharacterSystemInstaller` строка `context.AddBehaviour<CharacterMoveController>()` продублирована (опечатка в документации).
* `Docs/BestPractices/OverrideEntityFactoriesWithBakers.md` — объясняет разделение «фабрика создаёт, бейкер переопределяет», даёт раздел «Installer vs Baker» с контрастом `LifeEntityInstaller` (только `Add*`) против `LifeEntityBaker`/`MoveEntityBaker` (только `Set*`, через `Optional<T>`), и итоговую таблицу «где используется / зачем». Концептуально это ключевой файл для бейкинга; весь его код построен на несуществующих подписях.

### Исходники (здесь истина)

* `Assets/Plugins/Atomic/Entities/Scripts/Installers/` — семь файлов, все короткие: `IEntityInstaller.cs`, `IEntityInstaller`1.cs` (default interface implementation с кастом), `MonoEntityInstaller.cs` (+`onValidateCallback`, `OnValidate`), `MonoEntityInstaller`1.cs` (sealed-мост), `MonoEntityInstallerConfigurable.cs` (`[SerializeReference]`-массив), `ScriptableEntityInstaller.cs`, `ScriptableEntityInstaller`1.cs`.
* `Assets/Plugins/Atomic/Entities/Scripts/Aspects/` — `IEntityAspect.cs`, `IEntityAspect`1.cs`, `MonoEntityAspect.cs`, `MonoEntityAspect`1.cs`, `ScriptableEntityAspect.cs`, `ScriptableEntityAspect`1.cs`, `Extensions.cs`. Все тривиальные; смотреть сюда стоит ровно за ограничениями типов и за тем, что `Discard` `abstract`.
* `Assets/Plugins/Atomic/Entities/Scripts/Baking/MonoEntityBaker`1.cs` — настоящий базовый класс: два типовых параметра, три приватных поля ёмкостей, `Entity.Settings settings`, `Bake(TArgs)`, `Release()`, пятипараметровый `Create`.
* `.../Baking/MonoEntityBaker.cs` — `MonoEntityBaker<TArgs> : MonoEntityBaker<IEntity, TArgs>`: создаёт `new Entity(...)` и даёт `Install(IEntity, TArgs)` как единственную точку расширения. То, от чего должно наследоваться большинство проектных бейкеров.
* `.../Baking/MonoEntityBaker`1_Static.cs` — шесть статических batch-методов. Здесь видно и `FindObjectsSortMode.None`, и дефект с дырками в массиве, и что `args` идёт первым.
* `.../Baking/MonoEntityBaker`1_Editor.cs` — механизм precompute: `autoCompile`, `_previewEntity`, `Compile()` (`private protected virtual`, передаёт `default` в `TArgs`), `Reset()`.
* `.../Baking/MonoEntityBaker`1_Gizmos.cs` — весь файл под `#if UNITY_EDITOR`; цикл отрисовки гизмов preview-сущности.
* `.../Baking/MonoEntityBakerOptimized`1.cs` и `.../Baking/MonoEntityBakerOptimized.cs` — фабрично-пуловый бейкер и его шорткат `<string, IEntity, EntityView, TArgs>`. В документации отсутствуют полностью.
* `Assets/Plugins/Atomic/Entities/Scripts/Bootstrap/ScriptableEntityBootstrapper.cs` — ~130 строк, читается целиком за минуту; содержит все грабли bootstrap-а (`Resources.LoadAll`, незаякоренный regex, `async void`, единственная активная сцена).
* `Assets/Plugins/Atomic/Entities/Scripts/Entities/Extensions.cs` — `Clear`, два `Install`, два `InstallFromScene` (с разной логикой поиска!), четыре `TryGetEntity`, четыре `FindEntityInParent`, `DrawGizmos(this IEntity)`.
* `.../Entities/Unity/MonoEntity_Installing.cs` — драйвер установки: порядок, идемпотентность, предупреждения на null, `WhenInstall`/`WhenUninstall`, три `InstallAll`.
* `.../Entities/Unity/MonoEntity_Creation.cs` — `CreateArgs` и все перегрузки `Create`/`CreateAsync` плюс `SetupAfterCreation`. Единственное место, где видно, какие пути устанавливают сущность, а какие нет.
* `.../Entities/Unity/MonoEntity_Inspector.cs` — все сериализованные поля `MonoEntity` с их дефолтами и `[FormerlySerializedAs("installers")]` на `sceneInstallers`. Смотреть перед миграцией префабов.
* `.../Entities/Unity/MonoEntity_UnityLifecycle.cs` — `Awake` (`Register` + `Install` при `installOnAwake`), `OnDestroy` (`Uninstall` при `uninstallOnDestroy`, затем `Clear*`), `ISerializationCallbackReceiver.OnAfterDeserialize` (`Construct` + `Register`).
* `.../Entities/Unity/MonoEntity_Editor.cs` — `Compile()` в Edit Mode (`Uninstall`/`Construct`/`Install`/`PrecomputeCapacities` + симуляция lifecycle), `SetRefreshCallbackToInstallers()`, `OnValidate`, `Reset()` (там видно, что `Reset` собирает `sceneInstallers` через `GetComponentsInChildren` и ставит `autoCompile = false`).
* `.../Entities/Unity/MonoEntitySingleton.cs` — CRTP-синглтон целиком, включая `Awake`/`OnDestroy`/`Reset`, которых нет в документации.
* `.../Entities/Core/Entity.cs` — `struct Settings`, три конструктора (строковый, целочисленный, основной с ёмкостями) и `EntityRegistry.Instance.Register(this)` в конце основного.
* `.../Entities/Interface/IEntity_Tags.cs`, `IEntity_Values.cs`, `IEntity_Behaviours.cs` — настоящий, целочисленный контракт сущности. Полезно открыть, чтобы увидеть, что строковых методов там нет.
* `.../Entities/Extensions_Tags.cs`, `Extensions_Values.cs`, `Extensions_Behaviours.cs` — строковые и ключевые (`TagKey`, `ValueKey<T>`, `ValueKey<E, T>`) перегрузки, `AddBehaviour<T>() where T : IEntityBehaviour, new()`, `DisposeValues`.
* `.../Utils/Args.cs` — `IArgs`, `NoArgs`, `Args<T>`..`Args<T1..T4>`. Двадцать строк, но определяют всю ergonomics бейкеров.
* `.../Factories/Unity/ScriptableEntityFactory`1.cs` и `.../Factories/Unity/ScriptableEntityFactory.cs` — настоящие подписи фабрики, нужные `MonoEntityBakerOptimized`; плюс её собственный `Compile()` с `autoCompile = true` по умолчанию.
* `.../Collections/Extensions.cs` — `AddRange<E>(this IEntityCollection<E>, ...)`, который все примеры вызывают как «метод `EntityWorld`».
* `Assets/Plugins/Atomic/Elements/Scripts/Utils/Optional.cs`, `.../Utils/Extensions.cs` (`AddTo` ×2), `.../Utils/DisposableComposite.cs`, `.../Utils/DisposableComposite`1.cs`, `.../Utils/AtomicUtils.cs`, `.../Values/Const.cs`, `.../Events/Subscription.cs`, `.../Events/ISignal.cs`, `.../Events/Extensions.cs` (`Subscribe`) — все типы, которыми инсталлеры фактически пользуются. Короткие файлы, и именно в них видно, что `Const<T>` — класс, `Optional<T>` и `Subscription` — структуры, а `Subscribe` и `AddTo` — расширения.
* `Assets/Plugins/Atomic/Entities/Tests/PlayMode/SceneEntity/SceneEntityTests_Install.cs` и `SceneEntityTests_InstallEntities.cs`, `Assets/Plugins/Atomic/Entities/Tests/Substitutions/Installers/MonoEntityInstallerSpy.cs` — живые тесты установки. Хорошая отправная точка для своих тестов и единственное исполняемое подтверждение семантики `Install`. Папку `Tests/PlayMode/SceneEntityBaker/` открывать бессмысленно: она закомментирована целиком.
