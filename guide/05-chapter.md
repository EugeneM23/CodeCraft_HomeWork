# Глава 05. Интерполяция и снапшоты

> **TL;DR для агента**
> - Рисовать сетевое состояние можно двумя принципиально разными способами, и выбор делается **по природе траектории**, а не по вкусу: детерминированную траекторию (пуля, ракета) **экстраполируй формулой** от стартового тика; траекторию, зависящую от внешнего движущегося состояния (самонаведение), **интерполируй между двумя снапшотами**.
> - `NetworkBehaviour.TryGetSnapshotsBuffers(out from, out to, out alpha)` + `GetArrayReader<T>(nameof(поле))` + `ArrayReader<T>.Read(buffer)` дают доступ к *любому* `[Networked]`-полю на начало и конец render-кадра. Это ручная замена `NetworkTransform`-интерполяции для данных, которые Fusion интерполировать не умеет.
> - Расширяй `INetworkStruct` не новыми полями под каждый тип сущности, а блоком `fixed byte customData[N]` + типизированным `ref T CustomData<T>()`: цена трафика остаётся постоянной при росте числа типов.
> - Одноразовую инициализацию сетевой сущности (захват цели) делай **до** записи структуры в `NetworkArray`, и храни ссылку как `NetworkId`, а не как `NetworkObject`.
> - `SimulateStep(ref ProjectileState …)` + `NetworkArray<T>.GetRef(i)` = мутация прямо в сетевой памяти, без копии и без обратного `Set`.

---

## Какую проблему решает

Симуляция в этом проекте идёт на **30 Гц** (`NetworkProjectConfig.fusion`: `TickRateSelection.Client: 30`), а рендер — на частоте кадров монитора. Между двумя тиками сетевое состояние не меняется вообще, поэтому «рисовать то, что лежит в `[Networked]`-поле прямо сейчас» = показывать картинку с шагом 33 мс. Это заметно всегда, а на быстром снаряде — особенно.

В главе 04 проблема решалась **экстраполяцией**: в сетевое состояние писались только `tick`/`position`/`rotation` момента выстрела, а позиция на произвольное render-время считалась чистой функцией `GetPosition(state, time)`. Это идеальное решение, но у него есть жёсткое предусловие: **траектория должна полностью выводиться из стартового состояния**. Пуля (прямая) и ракета (парабола с гравитацией) это условие выполняют. Побочный выигрыш огромен: состояние снаряда после спавна **вообще не меняется**, то есть трафика на полёт нет, и экстраполировать можно сколько угодно вперёд.

Глава 05 вводит снаряд, который это предусловие ломает, — самонаводящийся. Его путь зависит от позиции *движущейся цели*, которая на момент выстрела неизвестна. Формулы нет. Состояние приходится мутировать каждый тик. И дальше встаёт вопрос: как такое рисовать плавно?

- Если экстраполировать (как Bullet/Rocket) — снаряд будет улетать по направлению, актуальному на последний известный тик, а при приходе нового снапшота рывком возвращаться. Чем сильнее доворот, тем сильнее рывок.
- Если рисовать «последнее известное состояние» — получим ступеньки 30 Гц.
- Единственный корректный вариант — **интерполяция назад**: взять два уже известных состояния (прошлый и следующий тик render-таймфрейма), смешать их по `alpha`. Картинка отстаёт от симуляции меньше чем на тик, зато гладкая и никогда не «врёт» о будущем.

Проблема главы, таким образом, формулируется так: **расширить рендер-контракт так, чтобы экстраполяторы и интерполяторы жили за одной абстракцией**, и получить низкоуровневый доступ к снапшотам произвольного `[Networked]`-поля.

---

## Модель мира: кто чем владеет

Здесь важно не повторить ошибку, которую легко сделать по аналогии с «серверным» кодом.

**Где живёт состояние.** `ProjectileWorld : NetworkBehaviour` висит на корне префаба `Player`. `Player` — это `NetworkObject`, заспавненный хостом с `inputAuthority = player` (`PlayerJoinController`), с вложенным `NetworkObject` персонажа. Значит:

- `this.Object.InputAuthority` внутри `ProjectileWorld` — это **стреляющий игрок**. Отсюда и фильтр «не стреляй в себя», и выбор цвета вью.
- `this.Object.IsProxy` внутри `ProjectileWorldView` — это «чужой игрок», что и даёт развилку таймфреймов.
- `StateAuthority` всех `Player`/`Character` в host-режиме — один и тот же хост. **Поэтому проверка «свой/чужой» делается по `InputAuthority`, а не по `StateAuthority`** — по `StateAuthority` все персонажи выглядели бы одинаково.

**Кто исполняет код.** `EntryPoint` стартует `GameMode.AutoHostOrClient` и ставит `ProvideInput = true`. Это значит, что `InputReceiver.FixedUpdateNetwork` → `GetInput(out InputData)` отдаёт ввод и хосту (у него и StateAuthority, и InputAuthority своего Player), и клиенту с InputAuthority. Дальше:

```
Assets/Game/Scripts/System/Players/PlayerJoinController.cs
```
```csharp
private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
{
    if (!runner.IsServer)
        return;
    // ...
}
```

Это **единственная** проверка authority во всём учебном коде раздела. В цепочке выстрела — `InputReceiver.FixedUpdateNetwork` → `FireController` → `FireComponent.Fire` → `ProjectileWorld.Spawn` → `HomingProjectileConfig.IniitalizeStep`, и далее `ProjectileWorld.FixedUpdateNetwork` → `SimulateStep` — **нет ни одной** проверки `HasStateAuthority` / `Runner.IsServer` / `HasInputAuthority`.

Практический вывод, который надо держать в голове: `Spawn` и `IniitalizeStep` выполняются на **каждом пире, который симулирует этот тик** — то есть и предсказательно на клиенте-владельце ввода, и повторно при ресимуляции того же тика. Говорить «цель выбирается один раз и одинаково у всех» как о факте из кода **нельзя**. Корректная формулировка (это вывод, а не факт из кода): *детерминизм выбора цели обеспечивается не кодом конфига, а тем, что реплицированное значение хоста перетирает клиентское предсказание*. Фиксация цели в сетевом состоянии при этом остаётся правильным приёмом — она убирает перецеливание между тиками, но не заменяет authority-гейт.

> ⚠️ **в проекте не реализовано, рекомендация:** в продакшене закрой спавн снаряда authority-гейтом — `if (!Object.HasStateAuthority) return;` в `Spawn` (host-авторитет) либо `if (!Runner.IsForward) return;` там, где одноразовый побочный эффект не должен повторяться при ресимуляции. Здесь это сознательное упрощение урока.

**Где исполняется рендер.** `Spawned()` вызывается на **всех** пирах, включая прокси, — и это обязательное условие работы главы: `ArrayReader` нужен именно на render-пути прокси. Если бы его инициализация стояла под проверкой authority, интерполяция снапшотов у чужих игроков не работала бы вообще.

**Порядок внутри объекта.** В `Player.prefab` у `NetworkObject` корня:

```
Assets/Game/Prefabs/Player.prefab
```
```yaml
  NetworkedBehaviours:
  - {fileID: 8032379552497074581}   # InputReceiver
  - {fileID: 1219346628194265535}   # ProjectileWorld
  - {fileID: 7963562908096527971}   # ProjectileWorldView
  ForceRemoteRenderTimeframe: 0
```

`ProjectileWorld` стоит **раньше** `ProjectileWorldView`, поэтому `ProjectileWorld.Spawned()` (создание ридера) гарантированно проходит до первого `ProjectileWorldView.Render()` на том же объекте. Это не косметика — это то, почему достаточно создать ридер в `Spawned()` и не проверять его на `null` в `Render()`.

---

## Архитектура раздела

```
                 ВВОД (локальный, каждый кадр)
  InputAccumulator (MonoBehaviour, NetworkEvents.OnInput)
        │ buttons.Set(InputKeys.Fire, …) ; input.Set(inputData)
        ▼
  ═══════════════ СИМУЛЯЦИЯ — FixedUpdateNetwork, 30 Гц ═══════════════
  InputReceiver (NetworkBehaviour, корень Player)
        │ GetInput(out InputData) ; buttons.WasPressed([Networked] previousButtons, Fire)
        ▼ OnFire
  FireController ──► FireComponent (Character)
        │ [Networked] TickTimer timer — кулдаун в ТИКАХ
        ▼ _projectileWorld.Spawn(type, firePoint.position, firePoint.rotation)
  ProjectileWorld (корень Player)
        │ FindFreeSlot → собрать ProjectileState{ tick = Runner.Tick }
        │ config.IniitalizeStep(ref state, Runner, Object.InputAuthority)
        │        └─► HomingProjectileConfig
        │                └─► runner.GetSingleton<NetworkObjectService>()
        │                        └─► Runner.GetAllNetworkObjects(ListPool-буфер)
        │                            фильтр: тег + InputAuthority != player + Health > 0
        │                └─► state.CustomData<NetworkId>() = target.Id
        │ _projectiles.Set(index, state)          ← атомарно, цель уже внутри
        │
        │ каждый тик: ref state = ref _projectiles.GetRef(i)
        │             config.SimulateStep(ref state, …)
        │               homing: TryFindObject(id) → LookRotation → position += …
        │               false → state.SetInactive()  (tick = 0, слот свободен)
        ▼
  [Networked, Capacity(8)] NetworkArray<ProjectileState>   ← РЕПЛИКАЦИЯ
        │
  ═══════════════ РЕНДЕР — Render(), частота кадров ═══════════════
  ProjectileWorldView (дочерний GO "View", зарегистрирован в NetworkedBehaviours)
        │ renderTime = Object.IsProxy ? RemoteRenderTime : LocalRenderTime + DeltaTime
        │ _world.TryGetProjectileSnapshots(out prev, out cur, out alpha)
        │        └─► TryGetSnapshotsBuffers(out NetworkBehaviourBuffer from,
        │                                   out NetworkBehaviourBuffer to, out alpha)
        │        └─► _projectileReader.Read(buffer) ×2 → NetworkArrayReadOnly<T> ×2
        │ if (!prev[i].IsActive) prev[i] = cur[i];        ← защита кадра появления
        │ диффер: слот активен ↔ вью есть  → Rent / Return в ProjectileViewPool
        ▼
  ProjectileView (обычный MonoBehaviour, БЕЗ NetworkObject, пулится)
        │ _config.GetRenderPosition(in from, in to, in alpha, runner, player, renderTime)
        ├── Bullet/Rocket:  time = renderTime - to.tick * DeltaTime → ЭКСТРАПОЛЯЦИЯ
        └── Homing:         Vector3.Lerp(from.position, to.position, alpha) → ИНТЕРПОЛЯЦИЯ
        ▼ transform.SetPositionAndRotation(...)
```

Слои снизу вверх: **(1)** сетевое состояние — `ProjectileState` с union-блоком; **(2)** поведение как данные — `ProjectileConfig : ScriptableObject` с тремя контрактами (инициализация / симуляция / рендер); **(3)** мир — `ProjectileWorld` как единственный владелец `NetworkArray` и фасад над снапшот-API; **(4)** рендер — `ProjectileWorldView` (диффер слотов) + `ProjectileView` (транспорт); **(5)** сервис запросов к миру — `NetworkObjectService : SimulationBehaviour`.

---

## Реализация по шагам

### Шаг 1. Расширяемое сетевое состояние: union вместо полей под каждый тип

```
Assets/Game/Scripts/Projectiles/Core/ProjectileState.cs
```
```csharp
[Serializable]
public unsafe struct ProjectileState : INetworkStruct
{
    private const int CUSTOM_DATA_SIZE = 16;

    public ProjectileType type;
    public int tick;
    public Vector3 position;
    public Quaternion rotation;
    public fixed byte customData[CUSTOM_DATA_SIZE];

    public bool IsActive => this.tick > 0;

    public void SetInactive() => this.tick = 0;

    public readonly ref T CustomData<T>() where T : unmanaged
    {
        if (sizeof(T) > sizeof(byte) * CUSTOM_DATA_SIZE)
            throw new ArgumentOutOfRangeException(nameof(T), $"Struct {typeof(T).Name} too large for custom data.");

        fixed (byte* ptr = this.customData)
            return ref *(T*) ptr;
    }
}
```

**Почему именно так.** Элемент `NetworkArray` реплицируется для всех слотов одного размера. Если под каждый новый тип снаряда добавлять своё поле (`targetId` для homing, что-то ещё для следующего), структура и трафик растут линейно по числу типов, хотя в каждом конкретном слоте используется только одно из этих полей. Общий блок фиксированного размера держит цену постоянной: 16 байт, которые каждый тип трактует по-своему (homing пишет туда `NetworkId` — 4 байта).

Три тонкости, которые надо понимать правильно:

- `readonly` на методе, возвращающем **mutable** `ref` в собственную память, — не защита от потери записи в текущем коде. Оба места, где `CustomData` используется (`IniitalizeStep`, `SimulateStep`), принимают `ref ProjectileState`, значит defensive copy там не возникает ни при каком модификаторе. `readonly` нужен для другого: чтобы `CustomData<T>()` *вообще можно было вызвать* в `in`/readonly-контексте (`DrawGizmos(in ProjectileState …)`, `GetRenderPosition(in ProjectileState from, …)`) без того, чтобы компилятор отдал ref внутрь временной копии. В главе 05 ни один render/gizmo-метод `CustomData` не вызывает — это задел на будущее.
- Проверка размера **рантаймовая**. Компилятор её не обеспечивает. Это лучше, чем молча затереть соседние поля, но хуже, чем ошибка компиляции.
- `IsActive => tick > 0` + `SetInactive() => tick = 0` означают, что **тик 0 зарезервирован как «слот пуст»**. Выигрыш: обнулённая память `NetworkArray` = пустые слоты, отдельный флаг не нужен и не ест байт. Цена: снаряд, выстреленный ровно на тике 0, будет невидим. На практике тик 0 недостижим, но приём надо применять осознанно.

### Шаг 2. Сузить enum до byte

```
Assets/Game/Scripts/Projectiles/Core/ProjectileType.cs
```
```csharp
public enum ProjectileType : byte
{
    Bullet = 0,
    Rocket = 1,
    Homing = 2
}
```

**Почему именно так.** Этот enum лежит в каждом из 8 элементов реплицируемого массива. `int` по умолчанию — 4 байта там, где достаточно одного. Насколько реально сэкономится трафик — **вывод, а не факт из кода**: Fusion упаковывает `INetworkStruct` по словам, так что выигрыш зависит от общего размера структуры и может оказаться нулевым. Но как привычка для полей внутри `INetworkStruct` это правильно и бесплатно.

### Шаг 3. Три контракта в одном конфиге

```
Assets/Game/Scripts/Projectiles/Core/ProjectileConfig.cs
```
```csharp
public abstract class ProjectileConfig : ScriptableObject
{
    [field: SerializeField]
    public ProjectileType Type { get; private set; }

    [field: SerializeField]
    public ProjectileView Prefab { get; private set; }

    public virtual void IniitalizeStep(ref ProjectileState projectile, in NetworkRunner runner, PlayerRef player)
    {
    }

    public abstract bool SimulateStep(ref ProjectileState projectile, in NetworkRunner runner, in PlayerRef player);

    public abstract void DrawGizmos(in ProjectileState projectile, in NetworkRunner runner, in PlayerRef player);

    public abstract Vector3 GetPosition(in ProjectileState projectile, in float time);

    public abstract Quaternion GetRotation(in ProjectileState projectile, in float time);

    public abstract Vector3 GetRenderPosition(
        in ProjectileState @from,
        in ProjectileState to,
        in float alpha,
        in NetworkRunner runner,
        in PlayerRef player,
        in float renderTime
    );

    public abstract Quaternion GetRenderRotation(
        in ProjectileState @from,
        in ProjectileState to,
        in float alpha,
        in NetworkRunner runner,
        in PlayerRef player,
        in float renderTime
    );
}
```

**Почему именно так.** Это ключевое проектное решение главы — **разделение контракта симуляции и контракта рендера**.

- Симуляция обязана быть детерминированной и работать по тикам (её переигрывают при ресимуляции). Её вход — `ref ProjectileState` + `Runner` + `PlayerRef`. Никакого render-времени здесь быть не должно: занеси его в сетевую логику — получишь рассинхрон, потому что render-время у каждого пира своё.
- Рендер обязан быть гладким и вызываться с любой частотой. Его вход — `from`, `to`, `alpha`, `renderTime`. Он вправе делать что угодно, **кроме записи в состояние**.
- Единая сигнатура рендер-методов покрывает оба случая. `BulletConfig`/`RocketConfig` используют из неё только `to.tick` и `renderTime`; `HomingProjectileConfig` — только `from`/`to`/`alpha`. Вызывающий код (`ProjectileView`) о разнице не знает вообще.

Старые `GetPosition(state, time)` / `GetRotation(state, time)` остались как чистые функции траектории — ими пользуются симуляция и гизмо. Homing реализует их тривиально (возвращает текущие `position`/`rotation`), потому что «формулы траектории» для него не существует, — и это честно описывает его природу.

### Шаг 4. Фасад над снапшот-API внутри владельца состояния

```
Assets/Game/Scripts/Projectiles/Core/ProjectileWorld.cs
```
```csharp
[Networked, Capacity(MAX_PROJECTILES)]
private NetworkArray<ProjectileState> _projectiles { get; }

private ArrayReader<ProjectileState> _projectileReader;

public int Length => MAX_PROJECTILES;

[ShowInInspector, Sirenix.OdinInspector.ReadOnly]
public IEnumerable<ProjectileState> Projectiles => _projectiles;

public override void Spawned()
{
    _projectileReader = this.GetArrayReader<ProjectileState>(nameof(_projectiles));
}

// ...

public bool TryGetProjectileSnapshots(
    out NetworkArrayReadOnly<ProjectileState> previous,
    out NetworkArrayReadOnly<ProjectileState> current,
    out float alpha //0..1
)
{
    if (this.TryGetSnapshotsBuffers(
            out NetworkBehaviourBuffer previousSnapshot,
            out NetworkBehaviourBuffer currentSnapshot,
            out alpha))
    {
        previous = _projectileReader.Read(previousSnapshot);
        current = _projectileReader.Read(currentSnapshot);
        return true;
    }

    previous = default;
    current = default;
    return false;
}
```

**Почему именно так.** Это ядро урока, и здесь работают три сущности Fusion одновременно:

- `NetworkBehaviourBuffer` — **сырая память** состояния этого behaviour'а на один тик. Сам по себе бесполезен.
- `ArrayReader<T>` — типизированный разбор буфера в `NetworkArrayReadOnly<T>`. Получается из `GetArrayReader<T>(имя свойства)`. Имя передаётся **строкой**, поэтому единственно верный способ — `nameof(_projectiles)`: переименование поля ломает компиляцию, а не рантайм.
- `alpha` — где внутри интервала `[from; to]` находится текущий кадр.

Важно про имена: `from` — состояние **на начало** render-кадра, `to` — **на конец** (дословно из `Fusion.Runtime.xml`: `The buffer representing the state of the network behaviour at the start of the render frame` / `at the end of the render frame`). Поэтому `to` в коде считается «актуальным» снапшотом (`projectiles = current`), и именно от `to.tick` экстраполяторы отсчитывают время.

Ридер создаётся **в `Spawned()`, а не в `Awake`**. Привязка behaviour'а к сетевому состоянию до спавна ещё не готова — это **вывод из места вызова, а не факт из кода**, но правило безопасное: любые метаданные `[Networked]`-полей трогай начиная со `Spawned()`.

Фасад нужен, чтобы детали памяти не утекали во view-слой: иначе `ProjectileWorldView` зависел бы от имени приватного поля `_projectiles`. Инкапсуляция здесь не стилистика — только владелец состояния знает, как оно лежит.

Два попутных замечания по этому фрагменту. `ArrayReader<T>` — это **вложенный** тип `Fusion.NetworkBehaviour.ArrayReader<T>`; неквалифицированное имя компилируется только потому, что класс наследует `NetworkBehaviour`. И `[ShowInInspector] IEnumerable<ProjectileState> Projectiles` — жёсткая компиляционная зависимость учебного кода на Odin Inspector (`using Sirenix.OdinInspector;`); как **приём отладки** смотреть содержимое `NetworkArray` в инспекторе в рантайме — отлично, но `IEnumerable` по `NetworkArray` аллоцирует энумератор, поэтому такое свойство годится только для инспектора и никогда для игрового кода.

### Шаг 5. Одноразовая инициализация попадает в состояние тем же присваиванием

```
Assets/Game/Scripts/Projectiles/Core/ProjectileWorld.cs
```
```csharp
public bool Spawn(ProjectileType type, Vector3 position, Quaternion rotation)
{
    if (!this.FindFreeSlot(out int index))
        return false;

    ProjectileState projectile = new ProjectileState
    {
        type = type,
        tick = this.Runner.Tick,
        position = position,
        rotation = rotation
    };

    ProjectileConfig config = _catalog.GetConfig(type);
    config.IniitalizeStep(ref projectile, this.Runner, this.Object.InputAuthority);

    _projectiles.Set(index, projectile);
    return true;
}

public override void FixedUpdateNetwork()
{
    NetworkRunner runner = this.Runner;
    PlayerRef player = this.Object.InputAuthority;

    for (int i = 0; i < _projectiles.Length; i++)
    {
        ref ProjectileState projectile = ref _projectiles.GetRef(i);
        if (!projectile.IsActive)
            continue;

        ProjectileType type = projectile.type;
        ProjectileConfig config = _catalog.GetConfig(type);

        if (!config.SimulateStep(ref projectile, in runner, in player))
            projectile.SetInactive();
    }
}
```

**Почему именно так.** Два разных приёма в одном месте.

Первое: `IniitalizeStep` вызывается на **локальной** структуре, **до** `_projectiles.Set`. Значит нет ни одного тика, в котором снаряд уже существует в сетевом состоянии, но цель ещё не захвачена. Поиск цели — операция O(всех сетевых объектов) и зависит от текущих позиций; делать её каждый тик означало бы и дорого, и «перескоки» цели между врагами. Фиксация `NetworkId` внутри реплицируемого стейта убирает перецеливание полностью.

Второе: `_projectiles.GetRef(i)` отдаёт **ссылку прямо в сетевую память**, а `SimulateStep` принимает `ref ProjectileState`. Пока контракт был `in ProjectileState` (так было в главе 04), самонаводящийся снаряд было **невозможно** реализовать — ему нужно мутировать свои `position`/`rotation` каждый тик. Альтернатива «скопировать структуру, изменить, записать `Set`» — лишний проход и очень легко потерять изменение, забыв обратную запись. С `GetRef` + `ref` мутация внутри конфига **сразу является** изменением сетевого состояния.

### Шаг 6. Stateless-сервис запросов к миру

```
Assets/Game/Scripts/System/ObjectProvider/NetworkObjectService.cs
```
```csharp
//Simulation Behaviour: — для сетевой логики без привязки к сетевому объекту

//Network Behaviour: — для сетевой логики с привязкой к сетевому объекту + сетевые свойства

public sealed class NetworkObjectService : SimulationBehaviour
{
    public bool FindClosestInRadius(
        in Vector3 center,
        in float radius,
        out NetworkObject target,
        in Predicate<NetworkObject> conditon
    )
    {
        List<NetworkObject> buffer = ListPool<NetworkObject>.Get();
        this.Runner.GetAllNetworkObjects(buffer);

        float minValue = float.MaxValue;
        target = default;

        for (int i = 0, count = buffer.Count; i < count; i++)
        {
            NetworkObject obj = buffer[i];
            if (!conditon.Invoke(obj))
                continue;

            Vector3 position = obj.transform.position;
            Vector3 vector = position - center;

            float distance = vector.magnitude;
            if (distance > radius)
                continue;

            if (distance < minValue)
            {
                target = obj;
                minValue = distance;
            }
        }

        ListPool<NetworkObject>.Release(buffer);
        return target != default;
    }
}
```

**Почему именно так.** `ProjectileConfig` — это `ScriptableObject`: один инстанс на все снаряды, живёт в ассетах и **не может** держать ссылки на объекты сцены. Ему нужен способ спросить у мира «кто рядом». `FindObjectOfType` — медленно и вне сетевого контекста; `NetworkBehaviour` потребовал бы собственный `NetworkObject` и собственное сетевое состояние. `SimulationBehaviour` — ровно то, что нужно: сетевой контекст (`Runner`) **без** сетевого состояния. У него нет `[Networked]`-свойств и нет `Object`/`StateAuthority`, поэтому класс **нечем рассинхронизировать** — он обязан быть stateless, и это ограничение здесь выполнено буквально.

`ListPool<NetworkObject>` вместо `new List` — потому что поиск происходит в тике симуляции, который может переигрываться многократно; аллокация на каждый выстрел копилась бы в GC. Буфер обязательно возвращается перед выходом. Фильтрация отдана наружу предикатом — сервис остаётся переиспользуемым.

### Шаг 7. Самонаводящийся снаряд: захват цели и шаг симуляции

```
Assets/Game/Scripts/Projectiles/Content/HomingProjectileConfig.cs
```
```csharp
public override void IniitalizeStep(ref ProjectileState projectile, in NetworkRunner runner, PlayerRef player)
{
    NetworkObjectService objectService = runner.GetSingleton<NetworkObjectService>();
    bool success = objectService.FindClosestInRadius(projectile.position, _searchRadius,
        out NetworkObject target, obj =>
            obj.CompareTag(GameObjectTags.Character) &&
            obj.InputAuthority != player &&
            obj.GetBehaviour<HealthComponent>().Exists());

    if (success)
        projectile.CustomData<NetworkId>() = target.Id;
}

public override bool SimulateStep(ref ProjectileState projectile, in NetworkRunner runner, in PlayerRef player)
{
    ref Vector3 position = ref projectile.position;
    ref Quaternion rotation = ref projectile.rotation;

    float deltaSpeed = _speed * runner.DeltaTime;
    NetworkId targetId = projectile.CustomData<NetworkId>();

    if (this.TryGetTarget(targetId, runner, out NetworkObject target))
    {
        Vector3 destination = target.transform.position + _targetOffset;
        Vector3 distance = destination - position;
        if (distance.sqrMagnitude <= deltaSpeed * deltaSpeed)
        {
            target.GetComponent<HealthComponent>().TakeDamage(_damage);
            return false;
        }

        rotation = Quaternion.LookRotation(distance.normalized);
    }

    position += rotation * Vector3.forward * deltaSpeed;
    return true;
}

private bool TryGetTarget(NetworkId targetId, in NetworkRunner runner, out NetworkObject target)
{
    if (!targetId.IsValid)
    {
        target = default;
        return false;
    }

    return runner.TryFindObject(targetId, out target);
}
```

**Почему именно так.** Разберём по пунктам, каждый из которых закрывает отдельный класс багов.

- **`NetworkId`, а не `NetworkObject`.** Ссылка на объект не реплицируется, невалидна после despawn/пулинга, и у разных пиров это разные инстансы. `NetworkId` — 4 байта, валиден у всех, разыменовывается через `runner.TryFindObject` каждый тик. Проверка `targetId.IsValid` отличает «цель не найдена при спавне» (`default(NetworkId)`) от валидного id.
- **Потеря цели не убивает снаряд.** `TryFindObject` вернёт `false`, если цель умерла и была удалена, — снаряд просто продолжает лететь прямо (`position += rotation * forward * deltaSpeed` стоит **вне** блока `if`). Это сознательно: иначе снаряды исчезали бы в воздухе.
- **Предикат — это явный набор сетевых инвариантов.** `CompareTag(Character)` отсеивает и корневой `Player` (он `Untagged`), и дочерние объекты — тег `Character` стоит только на корне `Character.prefab`. `InputAuthority != player` — **именно InputAuthority**, потому что в host-режиме `StateAuthority` у всех персонажей один и проверка по нему не отличила бы своего от чужого (иначе — самоубийственный снаряд). `GetBehaviour<HealthComponent>().Exists()` отсекает трупы, иначе снаряд летел бы к мёртвому персонажу и **навсегда занимал один из 8 слотов**. Порядок условий важен: тег проверяется **до** `GetBehaviour<HealthComponent>()`, иначе на объекте без `HealthComponent` был бы NRE.
- **`sqrMagnitude <= deltaSpeed * deltaSpeed` — защита от tunneling.** При 30 Гц и скорости 4 шаг равен ≈0.13 м. Условие «до цели осталось меньше одного шага» срабатывает всегда, тогда как сравнение с фиксированным радиусом попадания могло бы промахнуться — снаряд кружил бы вокруг цели, перелетая её каждый тик. `sqrMagnitude` вместо `magnitude` — без корня.
- **`ref`-алиасы `position`/`rotation`** — просто читаемость: запись идёт прямо в сетевую память, полученную из `GetRef`.

Две честные дыры этого кода:

> ⚠️ `target.transform.position` — это **Unity-трансформ**, который на прокси двигает `NetworkTransform` в интерполированном render-таймфрейме, а не состояние цели на текущий симулируемый тик. При ресимуляции позиция цели не переигрывается вместе с тиком — источник недетерминизма. **в проекте не реализовано, рекомендация:** читать сетевое состояние цели (её `NetworkTRSP`/снапшот) на нужный тик, если траектория должна быть строго детерминированной.

> ⚠️ У homing **нет никакого лимита времени жизни** — в отличие от Bullet (`_lifetime`) и Rocket (проверка `position.y <= _radius`). Слот освобождается только попаданием. Потерявший цель снаряд летит прямо вечно и занимает один из 8 слотов. **в проекте не реализовано, рекомендация:** добавить проверку по тику (`projectile.tick + lifetime < runner.Tick`) или границу мира.

### Шаг 8. Две стратегии времени за одним интерфейсом

```
Assets/Game/Scripts/Projectiles/Content/HomingProjectileConfig.cs
```
```csharp
public override Vector3 GetRenderPosition(
    in ProjectileState from,
    in ProjectileState to,
    in float alpha,
    in NetworkRunner runner,
    in PlayerRef player,
    in float renderTime
)
{
    return Vector3.Lerp(from.position, to.position, alpha);
}

public override Quaternion GetRenderRotation(
    in ProjectileState from,
    in ProjectileState to,
    in float alpha,
    in NetworkRunner runner,
    in PlayerRef player,
    in float renderTime
)
{
    return Quaternion.Lerp(from.rotation, to.rotation, alpha);
}
```

```
Assets/Game/Scripts/Projectiles/Content/RocketConfig.cs
```
```csharp
public override Vector3 GetRenderPosition(
    in ProjectileState from,
    in ProjectileState to,
    in float alpha,
    in NetworkRunner runner,
    in PlayerRef player,
    in float renderTime
)
{
    float time = renderTime - to.tick * runner.DeltaTime;
    return this.GetPosition(to, time);
}

public override Quaternion GetRenderRotation(in ProjectileState @from, in ProjectileState to, in float alpha,
    in NetworkRunner runner,
    in PlayerRef player, in float renderTime)
{
    float time = renderTime - to.tick * runner.DeltaTime;
    return this.GetRotation(to, time);
}
```

**Почему именно так.** Это контраст, на котором держится вся глава. Homing **полностью игнорирует** `renderTime`: ему достаточно `alpha`, потому что `alpha` уже выдана Fusion в правильном для этого объекта таймфрейме. Rocket **полностью игнорирует** `from` и `alpha`: он берёт стартовый тик актуального снапшота (`to.tick`) и считает аналитическую формулу. Один интерфейс, две стратегии времени.

И вот почему это безопасно, а не хрупко: состояние Bullet/Rocket **после спавна вообще не мутируется** (их `SimulateStep` сменил `in` на `ref`, но тела не изменились — проверено диффом 04↔05). Значит для них `from` и `to` — одна и та же структура, и `alpha` просто нечему испортить. Безопасность достигнута **по построению**, а не аккуратностью вызывающего кода.

Мелкая, но настоящая деталь: `Quaternion.Lerp`, а не `Slerp`. Lerp — линейная интерполяция с нормализацией, у неё непостоянная угловая скорость. На шаге 1/30 с и плавном довороте разница незаметна, но при резком перецеливании (цель ушла за спину) доворот будет неравномерным. Если нужна корректная угловая скорость — `Quaternion.Slerp`.

### Шаг 9. Render: чтение снапшотов и диффер слотов

```
Assets/Game/Scripts/Projectiles/View/ProjectileWoldView.cs
```
```csharp
public override void Render()
{
    NetworkRunner runner = this.Runner;
    float renderTime = this.Object.IsProxy
        ? this.Runner.RemoteRenderTime
        : this.Runner.LocalRenderTime + this.Runner.DeltaTime;

    _world.TryGetProjectileSnapshots(
        out NetworkArrayReadOnly<ProjectileState> prevProjectiles,
        out NetworkArrayReadOnly<ProjectileState> projectiles,
        out float alpha
    );

    PlayerRef player = this.Object.InputAuthority;

    for (int i = 0; i < _world.Length; i++)
    {
        ProjectileState projectile = projectiles[i];
        ProjectileState prevProjectile = prevProjectiles[i];
        if (!prevProjectile.IsActive) prevProjectile = projectile;

        bool hasProjectile = projectile.IsActive;

        ProjectileView projectileView = _projectileViews[i];
        bool hasView = projectileView != null;

        if (hasProjectile && !hasView)
        {
            //Spawn view
            projectileView = _viewPool.Rent(projectile.type, _container);
            projectileView.OnSpawn(in prevProjectile, in projectile, in alpha, in runner, in player, in renderTime);
            _projectileViews[i] = projectileView;
        }
        else if (hasView && !hasProjectile)
        {
            //Unspawn view:
            projectileView.OnUnspawn(in runner, in player);
            _viewPool.Return(projectileView);
            _projectileViews[i] = null;
        }
        else if (hasProjectile)
        {
            //Update view:
            projectileView.OnUpdate(in prevProjectile, in projectile, in alpha, in runner, in player, in renderTime);
        }
    }
}
```

**Почему именно так.**

- **Снапшоты берутся один раз за кадр**, до цикла. Это один вызов `TryGetSnapshotsBuffers` + два `Read` на весь массив, а не на каждый слот.
- **Развилка таймфреймов сохранена из главы 04** — она всё ещё нужна экстраполяторам: прокси рисуем в удалённом (интерполированном, отстающем) времени, свой объект — в локальном предсказанном `+ DeltaTime`. Homing её игнорирует, и это правильно: **нельзя** применять и `renderTime`, и `alpha` в одном типе — получишь двойной учёт времени.
- **`if (!prevProjectile.IsActive) prevProjectile = projectile;`** — одна строка, закрывающая визуальный баг, который иначе неизбежен. Слоты массива переиспользуются; на первом кадре жизни снаряда предыдущий снапшот слота либо неактивен, либо содержит остатки **предыдущего жильца слота** с совершенно другой позицией. `Lerp(from, to, alpha)` нарисовал бы снаряд где-то между чужой позицией (или нулём координат) и настоящей — «телепорт из прошлого». Подмена делает отрезок нулевым, и снаряд сразу стоит на месте появления.
- **Диффер «слот активен ↔ вью есть»** управляет `Rent`/`Return` в пуле. Вью появляется и исчезает по **локальному** решению рендера, без всякой сетевой синхронизации.
- `Render()` ничего не пишет в `[Networked]` — только читает буферы и ставит transform у не-сетевых вью. Запись в сетевое состояние из `Render` = рассинхрон.

Слабое место, которое надо знать: **возвращаемый `bool` у `TryGetProjectileSnapshots` здесь проигнорирован.** При `false` оба out-массива — `default(NetworkArrayReadOnly<ProjectileState>)`, и следующая же строка индексирует `projectiles[i]`.

> ⚠️ **в проекте не реализовано, рекомендация:** оборачивай вызов в `if (!_world.TryGetProjectileSnapshots(out …)) return;`. Гарантирует ли Fusion валидность буферов на любом `Render` после `Spawned()` — из кода неизвестно; XML-дока прямо говорит `returns: True if the snapshot buffers are valid`, то есть false предусмотрен. Не полагайся на удачу.

### Шаг 10. Вью как чистый транспорт

```
Assets/Game/Scripts/Projectiles/View/ProjectileView.cs
```
```csharp
private void UpdateTransform(
    in ProjectileState from,
    in ProjectileState to,
    in float alpha,
    in NetworkRunner runner,
    in PlayerRef player,
    in float renderTime
)
{
    Vector3 position = _config.GetRenderPosition(in from, in to, in alpha, in runner, in player, in renderTime);
    Quaternion rotation = _config.GetRenderRotation(in from, in to, in alpha, in runner, in player, in renderTime);
    this.transform.SetPositionAndRotation(position, rotation);
}
```

**Почему именно так.** В главе 04 здесь была строка `float time = renderTime - projectile.tick * runner.DeltaTime;`. Её **удалили**, и это принципиально: пока арифметика времени жила в вью, вью предполагал, что *все* типы снарядов экстраполируются от стартового тика. Теперь вью не знает ни про время, ни про alpha — он только пересылает параметры в конфиг и ставит transform. Это и есть то, что позволило добавить интерполятор, не трогая ни одной строки экстраполяторов.

Полезно знать педагогическую линию: арифметика времени в этом курсе переезжала **трижды** — базовый класс (`Assets/[Teacher]/ProjectileConfig.txt`: `float t = time - projectile.startTick * deltaTime;` внутри невиртуальной перегрузки) → `ProjectileView` (глава 04) → конкретный конфиг (глава 05). Каждый переезд был вынужден: как только появляется тип, которому эта арифметика не нужна, её нельзя держать выше него по иерархии.

### Шаг 11. Гизмо и жизненный цикл эдитора

```
Assets/Game/Scripts/Projectiles/Core/ProjectileWorld.cs
```
```csharp
private void OnDrawGizmos()
{
    if (!this.StateBufferIsValid)
        return;
    // ...
}
```

**Почему именно так.** `OnDrawGizmos` вызывается редактором вне жизненного цикла Fusion — до спавна и после despawn. Без этого гарда обращение к `_projectiles` даёт исключения в эдиторе. Правило общее: **любое чтение `[Networked]`-полей из Unity-колбэков (не из `FixedUpdateNetwork`/`Render`) гарди `StateBufferIsValid`.**

---

## Fusion API этой главы

| API | Что делает | Где вызывается | Подводный камень |
|---|---|---|---|
| `NetworkBehaviour.TryGetSnapshotsBuffers(out NetworkBehaviourBuffer from, out NetworkBehaviourBuffer to, out float alpha) : bool` | Отдаёт сырые буферы состояния этого behaviour'а на начало и конец render-кадра + коэффициент `alpha` (0..1). Позволяет интерполировать **любое** `[Networked]`-поле, а не только то, что умеет `NetworkTransform` | `ProjectileWorld.TryGetProjectileSnapshots` | Возвращает `bool`, и в `ProjectileWorldView.Render` он **не проверяется** — при `false` оба массива `default`, а код сразу индексирует. По XML-доке: `from` = «state at the start of the render frame», `to` = «at the end» — то есть `to` это тик, к которому идём, и он же «актуальный» |
| `NetworkBehaviour.GetArrayReader<T>(string property) : ArrayReader<T>` | Типизированный ридер конкретного `[Networked] NetworkArray` по имени свойства | `ProjectileWorld.Spawned()` | Имя — строка; передавай только `nameof(_projectiles)`. Это **вложенный** тип `NetworkBehaviour.ArrayReader<T>` — вне `NetworkBehaviour` придётся квалифицировать. Создавай в `Spawned()`, не в `Awake` (вывод, не факт из кода) |
| `ArrayReader<T>.Read(NetworkBehaviourBuffer) : NetworkArrayReadOnly<T>` | Превращает сырой буфер в типизированный read-only вид | `ProjectileWorld.TryGetProjectileSnapshots` | Это **не копия**, а вид на буфер снапшота. Писать нельзя — рендер обязан быть read-only по отношению к сетевому состоянию |
| `NetworkArray<T>.GetRef(int) : ref T` | Прямая ссылка в сетевую память элемента — мутация через неё сразу попадает в состояние без `Set()` | `ProjectileWorld.FixedUpdateNetwork`, `OnDrawGizmos` | Именно из-за `GetRef` переход `SimulateStep` с `in` на `ref` осмыслен. При `in` мутация невозможна; при копировании+`Set` — лишний проход и риск потерять изменения |
| `NetworkRunner.LocalRenderTime` / `RemoteRenderTime` | Render-время предсказанных / не-предсказанных объектов (по доке: `State.Time + Simulation.DeltaTime`) | `ProjectileWorldView.Render` | Нужно **только экстраполяторам**. Интерполятору достаточно `alpha` — она уже в правильном таймфрейме. Применишь оба в одном типе — двойной учёт времени |
| `NetworkRunner.GetSingleton<T>() where T : SimulationBehaviour` | По XML-доке «Ensures that a specific SimulationBehaviour component exists on this NetworkRunner gameobject» — то есть **обеспечивает наличие**, а не просто ищет | `HomingProjectileConfig.IniitalizeStep` | Работает относительно GameObject'а раннера. В сцене `NetworkObjectService` добавлен именно на `[System]`, где `NetworkRunner`. Положишь на другой GO — получишь не тот инстанс (или свежесозданный пустой) |
| `SimulationBehaviour` (vs `NetworkBehaviour`) | Сетевая логика (есть `Runner`) без собственного сетевого состояния и без привязки к `NetworkObject` | `NetworkObjectService` | Нет `[Networked]`-свойств, нет `Object`/`StateAuthority`. Класс **обязан** быть stateless, иначе появится несинхронизированное состояние, расходящееся при ресимуляции |
| `NetworkRunner.GetAllNetworkObjects(List<NetworkObject>)` | Перебор всех сетевых объектов симуляции | `NetworkObjectService.FindClosestInRadius` | O(n) и вызывается в тике, который может переигрываться. Буфер — только из пула (`ListPool<T>`), с обязательным `Release` |
| `NetworkRunner.TryFindObject(NetworkId, out NetworkObject)` | Разыменование сохранённого `NetworkId` в объект | `HomingProjectileConfig.TryGetTarget` | Корректно вернёт `false`, если цель умерла/удалена. Проверяй `targetId.IsValid` отдельно: `default(NetworkId)` означает «цель не найдена при спавне» |
| `INetworkStruct` + `fixed byte buffer` (unsafe) | Нетипизированный блок, реплицируемый как часть структуры; `CustomData<T>()` даёт типизированный `ref` | `ProjectileState` | Нужен `unsafe`-контекст и `allowUnsafeCode: 1`. Метод `readonly`, но возвращает mutable `ref` — `readonly` здесь нужен для вызова из `in`-контекста, а не для защиты записи. Проверка размера рантаймовая |
| `NetworkBehaviour.Spawned()` / `Render()` | `Spawned` — инициализация ридера (вызывается на **всех** пирах, включая прокси). `Render` — единственное место чтения снапшотов и движения вью | `ProjectileWorld`, `ProjectileWorldView` | `Render` вызывается раз на кадр, `FixedUpdateNetwork` — раз на тик и **возможно многократно** при ресимуляции. Из `Render` нельзя писать в `[Networked]` |
| `NetworkBehaviour.StateBufferIsValid` | Признак существования сетевого состояния | `ProjectileWorld.OnDrawGizmos` | Без гарда — исключения в эдиторе, где `OnDrawGizmos` вызывается вне жизненного цикла Fusion |
| `[Networked]` + `[OnChangedRender(nameof(...))]` + `[UnityNonSerialized]` | Декларативный change detection, колбэк вызывается на рендере | `HealthComponent.Health` | Второй, декларативный механизм чтения изменений — альтернатива ручному `TryGetSnapshotsBuffers` для простых скалярных полей. `[UnityNonSerialized]` нужен, чтобы weaving-свойство не пыталось сериализоваться Unity |
| `TickTimer.CreateFromSeconds` / `ExpiredOrNotRunning` | Кулдаун выстрела как `[Networked] TickTimer` — время в **тиках**, не в локальных секундах | `FireComponent.Fire` | Благодаря этому тик выстрела, попадающий в `ProjectileState.tick`, детерминирован и одинаков у всех пиров |

---

## Настройки, без которых не работает

**`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`** — **побайтово идентичен главе 04**. Это важный вывод: интерполяция снапшотов — чисто клиентский приём отображения и **не требует никаких настроек проекта**.

- `PeerMode: 0` (Single), `Simulation.ReplicationFeatures: 1`, `InputTransferMode: 0`, `PlayerCount: 10`.
- `TickRateSelection: { Client: 30, ServerIndex: 0, ClientSendIndex: 1, ServerSendIndex: 1 }` — симуляция 30 Гц. Именно разрыв между 30 тиками и FPS и создаёт нужду в интерполяции/экстраполяции.
- `LagCompensation.Enabled: false` (`HitboxBufferLengthInMs: 200` задан, но не используется). Глава 05 lag compensation не касается: хит-детект у Bullet/Rocket — обычные `PhysicsScene.Raycast`/`OverlapSphere` в текущем тике, у Homing — проверка дистанции.
- `NetworkConditions.Enabled: false`, но предзаданы `DelayMin/Max: 0.15`, `AdditionalJitter: 0.05`, `LossChanceMin/Max: 0.05` — готовый тумблер, чтобы **увидеть** разницу между экстраполяцией и интерполяцией. Включай его при отладке.
- `AssembliesToWeave` включает `Assembly-CSharp` — без этого `[Networked]`/`INetworkStruct` не weav'ятся. Асмдефов у учебного кода нет (все 8 `.asmdef` в проекте — вендорные).
- `InvokeRenderInBatchMode: true`, `NullChecksForNetworkedProperties: true`, `Heap: { PageShift: 15, PageCount: 256 }`.

**`ProjectSettings/ProjectSettings.asset`, строка 763: `allowUnsafeCode: 1`** — обязательно, иначе `fixed byte customData[16]` в `ProjectileState` не компилируется.

**Версии:** Unity `2022.3.42f1` (`ProjectSettings/ProjectVersion.txt`), Fusion `2.0.5 Stable 920` (`Assets/Photon/Fusion/build_info.txt`). Fusion-1 API в учебном коде отсутствует полностью: ни `ChangeDetector`, ни `[Rpc]`, ни `NetworkTRSP`, ни `HitboxRoot`, ни `InterpolationDataSource`.

**Сцена `Assets/Game/Scenes/Game.unity`, GameObject `[System]` (fileID 1271488996)** несёт 7 компонентов: `NetworkRunner`, `NetworkEvents`, `NetworkSceneManagerDefault`, `RunnerSimulatePhysics3D`, `EntryPoint`, … и **новый седьмой — `NetworkObjectService`** (fileID 1271489003, guid `4fa4d565cd174b6baa04ed35855a455d`). Совместное размещение с `NetworkRunner` **обязательно** — `GetSingleton<T>()` работает относительно GameObject'а раннера. Диффом 04↔05 это единственное изменение сцены.

`EntryPoint` в сцене: `_objectProvider: {fileID: 0}` — `PoolableNetworkObjectProvider` не назначен. Пулинг сетевых объектов фактически не активен, и это нормально: снаряды и так не являются `NetworkObject`.

**`Assets/Game/Prefabs/Player.prefab`** (он же `_playerPrefab` у `PlayerJoinController`):
- Корень: `NetworkObject` (`ObjectInterest: 1`, `Flags: 262145`, `NestedObjects: [NetworkObject персонажа]`, `ForceRemoteRenderTimeframe: 0`), `NetworkedBehaviours = [InputReceiver, ProjectileWorld, ProjectileWorldView]` — **в этом порядке**. `NetworkTransform` на корне Player **нет**.
- `ProjectileWorld._catalog` → `ProjectileCatalog.asset` (guid `7459d46122c244d1bae1aea4e45fb215`).
- Дочерний GO `View`: `ProjectileWorldView` с `_world` → `ProjectileWorld` корня и `_container: {fileID: 2505253765202100194}`. Этот stripped Transform — `m_CorrespondingSourceObject: {fileID: 8478411985332340610, guid: eb3686ca…}`, то есть **Transform дочернего GO `<Visual>` вложенного префаба Character**. Пулимые вью снарядов паренятся под визуальный узел персонажа, а не в отдельный контейнер. Отдельный контейнер есть только у `ProjectileViewPool` в сцене (`_container: {fileID: 2122250561}`), куда вью возвращаются в `Return()`.
- Вложенный инстанс Character: оверрайд `_projectileWorld` → `ProjectileWorld` корня Player. В самом `Character.prefab` это поле пустое — связь делается только на уровне Player.

**`Assets/Game/Prefabs/Character.prefab`:** `NetworkObject` + `NetworkedBehaviours` из 6 записей, где **первая — `NetworkTransform`**: `[NetworkTransform, HealthComponent, FireComponent, Character, ColorComponent, CharacterView]`. `NetworkTransform` (fileID `158639473`, guid `e725a070cec140c4caffb81624c8c787` = `Fusion.Runtime.dll`) настроен: `SyncScale: 0`, `SyncParent: 1`, `DisableSharedModeInterpolation: 0`, `_stateAuthorityChangeErrorCorrectionDelta: 0`. Именно эту интерполированную позицию и читает `HomingProjectileConfig` через `target.transform.position`.

`MovementComponent` и `RotationComponent` — **обычные `MonoBehaviour`**, не `NetworkBehaviour`, и в `NetworkedBehaviours` их нет. Движение/поворот — прямые записи в `transform`, которые реплицируются **только** через `NetworkTransform`. Тег `Character` стоит **только на корне** префаба (все дочерние `Untagged`, корень Player тоже `Untagged`) — на этом и держится фильтр цели. `FireComponent`: `_cooldown: 1`, `_type: 2` (Homing).

**Конфиги (`Assets/Game/Configs/Projectiles/`):** `ProjectileCatalog.asset` с тремя записями, третья — `HomingProjectile.asset` (guid `06e8187f563f54aca9f455346b9be879`). `HomingProjectile.asset`: `Type: 2`, `_searchRadius: 20`, `_speed: 4`, `_damage: 1`, `Prefab` → `Homing (View)`. Поле `_targetOffset` в ассете **отсутствует** — значит берётся дефолт из кода `new(0, 1, 0)`.

**`Assets/Game/Prefabs/Homing (View).prefab`:** `NetworkObject` **отсутствует** (намеренно — вью локальное и пулимое). Корень: тег `Bullet`, `localPosition (0, 3, 0)`, `ProjectileView` с `_renderer` → MeshRenderer дочернего `<Visual>`, `_blue`/`_red` материалы, `_config` → `HomingProjectile.asset`. Дочерний `<Visual>`: `m_Mesh: {fileID: 10208, …}` — встроенная **капсула** (тот же меш, что у `Rocket (View)` и у тела Character; сфера `10207` только у `Bullet (View)`), повёрнута на 90° по X, `scale 0.25`. Плюс `ColorComponent` — но это `NetworkBehaviour` на объекте без `NetworkObject`, поэтому его `Spawned()` никогда не вызывается; материал реально ставит `ProjectileView.OnSpawn`.

`ProjectileViewPool` в сцене (ищется через `FindObjectOfType` в `ProjectileWorldView.Awake`) обязан иметь `_catalog` = тот же `ProjectileCatalog`, иначе `Rent(Homing)` упадёт `KeyNotFoundException`: пулы создаются по `AllConfigs`.

---

## Антипаттерны и как их обходит этот код

1. **Экстраполяция недетерминированной траектории.** Если бы homing рендерился формулой от стартового тика, он улетал бы по изначальному направлению и рывком возвращался при каждом снапшоте. **Обход:** отдельный контракт `GetRenderPosition/GetRenderRotation`, где homing использует только `from/to/alpha` и полностью игнорирует `renderTime`.
2. **Двойной учёт времени.** Если бы в `ProjectileView` осталось `time = renderTime - projectile.tick * DeltaTime`, а конфиг дополнительно применял `alpha`, время считалось бы дважды. **Обход:** арифметика времени целиком вынесена из вью в конкретные конфиги; вью стал транспортным слоем, не знающим ни про время, ни про alpha.
3. **Интерполяция «из ниоткуда» на кадре появления.** Слоты переиспользуются; previous-снапшот слота содержит мусор предыдущего жильца. **Обход:** `if (!prevProjectile.IsActive) prevProjectile = projectile;` — alpha применяется к нулевому отрезку.
4. **Хранение ссылки `NetworkObject` в сетевой структуре.** Не реплицируется, невалидна после despawn/пулинга, у разных пиров разные инстансы. **Обход:** в `customData` пишется `NetworkId`, каждый тик разыменовывается через `TryFindObject`; `targetId.IsValid` отличает «цель не найдена».
5. **Поиск цели каждый тик.** Дорого, даёт перескоки цели между врагами. **Обход:** хук `IniitalizeStep` в тике спавна, результат попадает в состояние **до** `_projectiles.Set`.
6. **Проверка «свой/чужой» по `StateAuthority`.** В host-режиме StateAuthority всех персонажей один — проверка не различала бы своего и чужого, и снаряд захватывал бы собственного персонажа. **Обход:** `obj.InputAuthority != player`.
7. **Стрельба по трупам.** Снаряд летел бы к мёртвому персонажу и навсегда занимал слот. **Обход:** `GetBehaviour<HealthComponent>().Exists()` в предикате, **после** проверки тега (иначе NRE на объекте без `HealthComponent`).
8. **Проскок цели за тик (tunneling).** **Обход:** `distance.sqrMagnitude <= deltaSpeed * deltaSpeed` — «осталось меньше одного шага», вместо сравнения с фиксированным радиусом попадания.
9. **Аллокация списка на каждый запрос.** Поиск идёт в тике, который может переигрываться. **Обход:** `ListPool<NetworkObject>.Get()`/`.Release(buffer)`.
10. **Мутация состояния при `in`-параметре.** **Обход:** `ref` во всём контракте симуляции + `_projectiles.GetRef(i)`.
11. **Переполнение `customData`.** **Обход:** рантайм-проверка `sizeof(T) > 16` с `ArgumentOutOfRangeException` вместо молчаливой порчи соседних полей.
12. **Строковое имя сетевого свойства.** **Обход:** `nameof(_projectiles)` — переименование ломает компиляцию, а не рантайм.
13. **Чтение сетевого массива вне жизненного цикла.** **Обход:** `if (!this.StateBufferIsValid) return;` в `OnDrawGizmos`.
14. **Создание `ArrayReader` слишком рано.** **Обход:** в `Spawned()`, а не в `Awake` (вывод из места вызова).
15. **8 отдельных `NetworkObject` на снаряды.** Сохранён ключевой приём главы 04: сетевая сущность — один `ProjectileWorld` с массивом. Это убирает стоимость спавна/despawn сетевых объектов на каждый выстрел и позволяет вью появляться/исчезать по локальному решению рендера.

---

## Чеклист: как применить в своём проекте

1. **Классифицируй каждую сетевую сущность по природе траектории.** Выводится из стартового состояния чистой функцией → экстраполятор (ничего не реплицируй после спавна). Зависит от внешнего движущегося состояния → интерполятор снапшотов.
2. **Разведи контракты.** Один метод для симуляции (`ref State`, `Runner`, `PlayerRef` — без render-времени) и отдельная пара для рендера (`in from`, `in to`, `in alpha`, `renderTime` — без записи в состояние). Единая сигнатура рендера должна покрывать и экстраполяцию, и интерполяцию.
3. **Включи `allowUnsafeCode: 1`**, если собираешься использовать `fixed`-буфер в `INetworkStruct`. Проверь, что сборка с учебным кодом перечислена в `AssembliesToWeave`.
4. **Спроектируй `INetworkStruct` с union-блоком:** `private const int CUSTOM_DATA_SIZE = N; public fixed byte customData[N];` + `public readonly ref T CustomData<T>() where T : unmanaged` с рантайм-проверкой `sizeof(T)`. Сузь enum'ы до `: byte`. Зарезервируй «нулевое» значение одного поля под «слот пуст», если так дешевле, чем отдельный флаг.
5. **В `Spawned()` создай ридер:** `_reader = this.GetArrayReader<T>(nameof(_field));`. Только через `nameof`. Убедись, что владелец состояния стоит в `NetworkedBehaviours` **раньше** view-компонента — тогда ридер гарантированно готов к первому `Render()`.
6. **Заверни снапшот-API в фасад** на владельце состояния: `TryGetSnapshots(out NetworkArrayReadOnly<T> prev, out cur, out float alpha)` поверх `TryGetSnapshotsBuffers` + двух `Read`. Не пускай `NetworkBehaviourBuffer` и имена приватных полей во view-слой.
7. **Проверяй возвращаемый `bool`** фасада в `Render()` и выходи при `false`. Код этой главы этого не делает — не копируй эту часть.
8. **Добавь хук одноразовой инициализации**, вызываемый **до** записи структуры в сетевое состояние. Внутри храни ссылки на другие сетевые объекты только как `NetworkId`, и проверяй `IsValid` + `TryFindObject` на каждом чтении.
9. **Закрой спавн authority-гейтом**, если побочный эффект не должен повторяться: `Object.HasStateAuthority` для host-авторитета или `Runner.IsForward` против ресимуляции. (В этом проекте гейта нет — это упрощение урока.)
10. **Передавай элемент симуляции по `ref`** и получай его через `NetworkArray<T>.GetRef(i)`, если шаг мутирует состояние. Не копируй структуру с обратным `Set`.
11. **Stateless-запросы к миру оформи как `SimulationBehaviour`** на GameObject'е раннера, получай через `Runner.GetSingleton<T>()`. Буферы — только из `ListPool<T>` с обязательным `Release`. Фильтрацию отдавай предикатом наружу.
12. **Фильтр цели собери из явных инвариантов:** тег/слой → `InputAuthority != player` (не `StateAuthority`) → живость. Порядок важен — сначала то, что отсекает объекты без нужных компонентов.
13. **На кадре появления подменяй отсутствующий предыдущий снапшот текущим:** `if (!prev.IsActive) prev = cur;`.
14. **Выбирай таймфрейм один раз на кадр:** `Object.IsProxy ? Runner.RemoteRenderTime : Runner.LocalRenderTime + Runner.DeltaTime` — и используй его **только** в экстраполяторах. Интерполятор берёт только `alpha`.
15. **Для поворотов с резкими доворотами используй `Slerp`, не `Lerp`** (в этом проекте `Quaternion.Lerp` приемлем для плавного доворота за 1/30 с, но угловая скорость непостоянна).
16. **Добавь каждой сетевой сущности выход из жизни по времени** (lifetime в тиках или граница мира). У homing здесь его нет — потерявший цель снаряд занимает слот вечно.
17. **Гарди `StateBufferIsValid`** во всех Unity-колбэках, читающих `[Networked]`-поля (`OnDrawGizmos` в первую очередь).
18. **Включи `NetworkConditions` при отладке** (в конфиге уже предзаданы `Delay 0.15`, `Jitter 0.05`, `Loss 0.05`) — без лага разница между экстраполяцией и интерполяцией не видна.

---

## Что изменилось относительно главы 04

Проекты лежат прямо в `_src` без вложенной папки. Изменений немного, и все они бьют в одну цель: добавить снаряд, который нельзя экстраполировать, и научить рендер интерполировать снапшоты.

**Новые файлы (2 учебных `.cs` + 2 ассета):**

1. `Assets/Game/Scripts/Projectiles/Content/HomingProjectileConfig.cs` — единственная реализация, использующая интерполяцию снапшотов на рендере и единственная, которая мутирует состояние в `SimulateStep`.
2. `Assets/Game/Scripts/System/ObjectProvider/NetworkObjectService.cs` — новый `SimulationBehaviour` с `FindClosestInRadius`; в файле оставлен учебный комментарий, противопоставляющий `SimulationBehaviour` и `NetworkBehaviour`.
3. `Assets/Game/Configs/Projectiles/HomingProjectile.asset` — `Type: 2`, `_searchRadius: 20`, `_speed: 4`, `_damage: 1`.
4. `Assets/Game/Prefabs/Homing (View).prefab` — вью без `NetworkObject`; капсульный визуал, повёрнутый на 90° по X, `scale 0.25`.

**Изменённые учебные `.cs` — ровно 9 файлов** (по `diff -rq`):

5. `Projectiles/Core/ProjectileState.cs` — структура стала `unsafe`, добавлены `CUSTOM_DATA_SIZE = 16`, `fixed byte customData[16]` и `readonly ref T CustomData<T>()`. Главное изменение модели данных: per-type payload внутри общего сетевого элемента.
6. `Projectiles/Core/ProjectileType.cs` — enum сужен до `: byte`, добавлен `Homing = 2`.
7. `Projectiles/Core/ProjectileConfig.cs` — три правки контракта: новый `virtual IniitalizeStep` (пустой по умолчанию), `SimulateStep` сменил `in` на `ref`, добавлены абстрактные `GetRenderPosition/GetRenderRotation`. Старые `GetPosition/GetRotation(state, time)` не тронуты.
8. `Projectiles/Core/ProjectileWorld.cs` — самый содержательный дифф: поле `ArrayReader<ProjectileState> _projectileReader`; новый `Spawned()` с `GetArrayReader<T>(nameof(_projectiles))`; новый публичный `TryGetProjectileSnapshots` поверх `TryGetSnapshotsBuffers`; в `Spawn()` две строки — получение конфига и `config.IniitalizeStep(...)` перед `_projectiles.Set`; в `FixedUpdateNetwork` вызов стал `SimulateStep(ref …)`. Метод `GetProjectile(int)` остался, но рендером больше не используется.
9. `Projectiles/View/ProjectileView.cs` — сигнатуры `OnSpawn`/`OnUpdate`/`UpdateTransform` переехали с `(in projectile, …)` на `(in from, in to, in alpha, …)`; из `UpdateTransform` **удалено** `float time = renderTime - projectile.tick * runner.DeltaTime;`; `GetPosition/GetRotation` → `GetRenderPosition/GetRenderRotation`. Побочно: у `OnSpawn`/`OnUpdate` **снят** модификатор `virtual` (остался только у `OnUnspawn`).
10. `Projectiles/View/ProjectileWoldView.cs` — в `Render()` добавлен вызов `TryGetProjectileSnapshots`; `_world.GetProjectile(i)` заменено на `projectiles[i]` / `prevProjectiles[i]`; добавлена строка подмены `prevProjectile`; в `OnSpawn/OnUpdate` передаётся тройка. Выбор `renderTime` **не менялся** — это наследие главы 04.
11. `Projectiles/Content/BulletConfig.cs` и `Projectiles/Content/RocketConfig.cs` — `SimulateStep` сменил `in` на `ref` (**тела не изменились, мутаций не появилось**) и добавлены `GetRenderPosition/GetRenderRotation`, воспроизводящие ровно ту экстраполяцию, что раньше жила в `ProjectileView`. Bullet возвращает `to.rotation` без изменений, Rocket — `GetRotation(to, time)`.
12. `GameObject/Core/HealthComponent.cs` — дифф чисто косметический (один символ: отступ в пустой строке).

**Изменённые ассеты/настройки:**

13. `Configs/Projectiles/ProjectileCatalog.asset` — третья запись в `_configs`.
14. `Prefabs/Character.prefab` — изменился ровно один байт полезной нагрузки: `FireComponent._type: 1` → `2`. По умолчанию игрок стреляет Homing, чтобы урок было видно сразу.
15. `Scenes/Game.unity` — на `[System]` добавлен седьмой компонент `NetworkObjectService`. Единственное изменение сцены, и обязательное условие работы `GetSingleton<T>()`.

**Чего не изменилось (границы урока):**

- `NetworkProjectConfig.fusion` — **побайтово идентичен**. Интерполяция снапшотов не требует настроек проекта.
- `Assets/[Teacher]` — побайтово идентична главе 04. Там 8 txt-файлов (`BulletConfig`, `ProjectileCatalog`, `ProjectileConfig`, `ProjectileViewPool`, `ProjectileWorld`, `ProjectileWorldView`, `ProjetileView` — именно с опечаткой, `RocketConfig`) с более ранним API: `Simulate(in ProjectileState, PhysicsScene, in Tick, in float deltaTime, …)`, `CAPACITY = 32`, индексатор `this[in int index]`, поле `startTick` и флаг `IsAlive` (вместо `tick`/`IsActive`), единый `float renderTime = Object.RenderTime` **без** развилки Local/Remote, прямое чтение `_projectileWorld[i]`. Интерполяции там нет вовсе — это полезное «до» для сравнения. И именно в `[Teacher]/ProjectileConfig.txt` арифметика времени жила в **базовом классе**: `float t = time - projectile.startTick * deltaTime;`. Три места размещения этой арифметики по курсу: база (Teacher) → `ProjectileView` (04) → конкретный конфиг (05). При этом `ProjectileViewPool.txt` и `ProjectileCatalog.txt` идентичны финальным — пул и каталог в курсе не переписывались ни разу.
- `Assets/Archive` (папка называется именно `Archive`) содержит только `Bullet (Kinematic).prefab` и `Bullet (Physics).prefab`, кода нет. Папок `[Archive]`/`[Archive1]` в разделе нет.
- Не тронуты и содержательно для главы 05 не работают: `Character`, `CharacterView`, `ColorComponent`, `InputReceiver`, `InputAccumulator`, `InputData`, `InputKeys`, `FireComponent`, `MovementComponent`, `RotationComponent`, `MoveController`, `FireController`, `ProjectileCatalog`, `ProjectileViewPool`, `PlayerJoinController`, `PlayerLeftController`, `SpawnPointService`, `EntryPoint`, `GameObjectTags`, `WorldTransform`, `PoolableNetworkObjectCatalog`, `PoolableNetworkObjectProvider`, а также наследие глав 02–03: `KinematicBullet`, `PhysicsBullet`, `CollisionComponent`, `LifetimeComponent`, `ParentTransformConstraint`.
