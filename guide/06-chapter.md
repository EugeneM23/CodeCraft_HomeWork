# Глава 06. Компенсация задержек: хитскан-оружие на Runner.LagCompensation

> **TL;DR для агента**
> - Мгновенное (хитскан) оружие в Fusion 2 считается **только** через `Runner.LagCompensation.Raycast(...)` с передачей `player: Object.InputAuthority` — это команда перемотать историю снапшотов Hitbox'ов в тайм-фрейм стрелка. Руками историю позиций не веди.
> - Хитбоксы (`Hitbox` + `HitboxRoot`) — отдельная геометрия на отдельном слое, независимая от PhysX-коллайдеров. Лаг-компенсированный запрос по умолчанию **вообще не опрашивает PhysX** (нужен флаг `HitOptions.IncludePhysX`).
> - Фильтруй цели в `preProcessRoots`, а не после запроса: `Raycast` возвращает одно ближайшее попадание, и труп перед целью «съест» луч.
> - Разброс/любая случайность в симуляции обязана быть чистой функцией от тика: `Random.InitState(Tick.Raw * unchecked((int) Object.Id.Raw))`.
> - Визуал мгновенного события восстанавливается из состояния — `[Networked] int FireCount` + `[Networked, Capacity(N)] NetworkArray<HitScanState>` — и проигрывается **только в `Render()`**. RPC не нужен и вреден.
> - Для включения лаг-компенсации достаточно компонента `HitboxManager` в сцене рядом с `NetworkRunner`; флаг `LagCompensation.Enabled` в `NetworkProjectConfig` управляет лишь его автодобавлением.

---

## Какую проблему решает

Стрелок видит мир в прошлом. Его рендер отстаёт от серверной симуляции на (RTT/2 + интерполяционный буфер прокси). При `TickRateSelection.Client = 30` один тик это 33 мс, а интерполяция чужих объектов добавляет сверху ещё минимум тик. Снаряд из главы 05 это терпит: он летит несколько десятков тиков, и ошибка в полметра на старте размывается. Хитскан не терпит ничего — он попадает в тот самый кадр, который игрок видел на экране.

Если считать попадание по **текущим** позициям на хосте, получается классический «надо стрелять на опережение»: при 100 мс RTT и скорости персонажа 5 м/с (`MovementComponent._speed = 5` на префабе) цель смещается на ~0.5 м — ровно диаметр хитбокса-капсулы (`CapsuleRadius: 0.5`). То есть попадание в центр видимой модели стабильно даёт промах. Любое точное оружие в такой схеме ощущается сломанным, и никакой подкруткой урона это не лечится.

Решение в Fusion 2 системное: раннер ведёт кольцевой буфер снапшотов всех зарегистрированных хитбоксов (глубина задана `HitboxBufferLengthInMs: 200`), а запрос с указанием `PlayerRef` стрелка выполняется против состояния мира **в тайм-фрейме этого стрелка**. Геймплейный код не хранит историю вообще — в разделе 06 нет ни одного буфера позиций, ни одного `List<Snapshot>`.

Вторая половина задачи — представление. Хитскан это событие длиной в один тик. Его нельзя интерполировать, его легко потерять между кадрами рендера, и его нельзя передать RPC (RPC не откатывается при rollback, дублируется при ресимуляции и не доходит до late joiner'а). Раздел решает это превращением события в состояние: монотонный счётчик + кольцевой буфер последних трасс.

---

## Модель мира: кто чем владеет

Режим — `GameMode.AutoHostOrClient` (`EntryPoint.cs`), то есть Host/Client. `PeerMode: 0` (Single). Разложение по авторитету:

**Кто вообще вызывает `Fire()`.** В разделе **нет ни одной проверки** `HasStateAuthority` или `Runner.IsServer` на пути выстрела. Единственный гейт — первая строка `InputReceiver.FixedUpdateNetwork`:

```csharp
if (!this.GetInput(out InputData input)) 
    return;
```

`GetInput` отдаёт данные только тому, кто имеет на объект StateAuthority (хост) или InputAuthority (владелец). Значит `Fire()` крутится на хосте за всех игроков и на клиенте — только за себя. Это и есть вся «авторизация» выстрела. Запомни приём: **гейт по вводу заменяет гейт по авторитету**, если весь геймплей — функция от ввода.

**Кто владеет хитбоксами.** Никто из геймплейного кода. `HitboxRoot` и `Hitbox` — системные компоненты Fusion; `HitboxRoot` внесён в `NetworkedBehaviours` NetworkObject'а Character, то есть это полноценный сетевой behaviour, а историю его снапшотов ведёт одиночный `HitboxManager` на сценном объекте `[System]`.

**Кто владеет урон-состоянием.** `HealthComponent` висит на NetworkObject **цели**, а `TakeDamage` вызывается из симуляции **стрелка**. Проверки авторитета там нет:

```csharp
public bool TakeDamage(int damage)
{
    if (this.Health == 0)
        return false;

    Debug.Log($"TAKE DAMAGE {Object.Name} {Health} {Runner.Tick} {this.Runner.IsForward}");
    this.Health = Mathf.Max(0, this.Health - damage);
    return true;
}
```

⚠️ **Открытый вопрос, а не факт из кода.** XML-док параметра `player` у `Raycast` говорит: «Player who "owns" this raycast. **Used by the server** to find the exact hitbox snapshots to check against». То есть перемотка истории документирована как серверная операция. Клиент в этом коде тоже выполнит `Fire()` за себя и тоже вызовет `TakeDamage` на чужом объекте, где у него нет StateAuthority. Что именно происходит с такой локальной записью (отбрасывается ли она, живёт ли до следующего снапшота как предсказание) кодом раздела не подтверждается. **Директива:** в своём проекте не повторяй это как образец — ставь урон под `if (HasStateAuthority)` или под `if (Runner.IsServer)`, а клиентский визуал держи на `[Networked]`-состоянии, которое и так сойдётся.

Тот же изъян в инициализации здоровья — `Spawned()` выполняется на **всех** пирах, включая прокси:

```csharp
public override void Spawned()
{
    Health = MaxHealth;
}
```

⚠️ Это запись `[Networked]`-свойства не владельцем состояния. В Host-режиме локальная запись прокси будет перезатёрта ближайшим снапшотом, так что видимой поломки нет, но приём неверный. Правильно: `if (HasStateAuthority) Health = MaxHealth;`.

**Кто делает Spawn/Despawn.** Единственные два места во всём разделе, и оба с явной проверкой:

```csharp
// Assets/Game/Scripts/System/Players/PlayerJoinController.cs
private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
{
    if (!runner.IsServer)
        return;

    Transform spawnPoint = _spawnPointService.NextPoint();
    NetworkObject playerObject = runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity, player);
    runner.SetPlayerObject(player, playerObject);

    Character character = playerObject.GetComponentInChildren<Character>();
    character.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
}
```

```csharp
// Assets/Game/Scripts/System/Players/PlayerLeftController.cs
private void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
{
    if (runner.IsServer)
    {
        NetworkObject character = runner.GetPlayerObject(player);
        runner.Despawn(character);
    }
}
```

Оба — обычные `MonoBehaviour`, подписанные на `NetworkEvents` (компонент Fusion на `[System]`), найденный через `FindObjectOfType<NetworkEvents>()`. Трассы и VFX выстрела при этом **не** спавнятся через `Runner.Spawn` вообще — обычный `Instantiate`.

---

## Архитектура раздела

```mermaid
flowchart TD
    A["Input.GetKey(Space)<br/>InputAccumulator (MonoBehaviour)<br/>подписан на NetworkEvents.OnInput"] -->|input.Set(InputData)| B["InputReceiver : NetworkBehaviour<br/>FixedUpdateNetwork<br/>GetInput → WasPressed(previousButtons, Fire)"]
    B -->|event OnFire| C["FireController : MonoBehaviour<br/>IFireComponent _fireComponent"]
    C -->|Fire()| D["HitScanFireComponent : NetworkBehaviour<br/>Random.InitState(Tick × ObjectId)<br/>× _shotCount"]
    D -->|Shot()| E["Runner.LagCompensation.Raycast<br/>player = Object.InputAuthority<br/>SubtickAccuracy | IgnoreInputAuthority<br/>preProcessRoots = FilterHitboxRoots"]
    E -->|broadphase candidates| F["FilterHitboxRoots<br/>RemoveWhere(IsNotValidTarget)"]
    F --> E
    E -->|hit.Hitbox.Root| G["HealthComponent.TakeDamage<br/>[Networked] Health"]
    E -->|start / end| H["[Networked] LastHit<br/>[Networked, Capacity(4)] _lastHits<br/>[Networked] FireCount++"]
    G -->|OnChangedRender| I["CharacterView<br/>_visual.SetActive(health > 0)"]
    H -->|репликация| J["*HitScanFirePresenter.Render()<br/>fireCount > _visualFireCount ?"]
    J --> K["ParticleSystem.Play()<br/>+ InstantHitScanView (LineRenderer)<br/>или Instantiate(ProjectileHitscanView)"]
```

Четыре слоя, те же что в 04–05, плюс один новый.

**1. Ввод.** `InputAccumulator` — обычный `MonoBehaviour`, собирает `InputData { Vector3 moveDirection; NetworkButtons buttons }` в колбэке `NetworkEvents.OnInput`. `InputReceiver : NetworkBehaviour` переводит сырой ввод в события C# внутри `FixedUpdateNetwork`. Детект фронта нажатия — `WasPressed` относительно `[Networked] previousButtons`.

**2. Кто стреляет.** Главное архитектурное изменение раздела: `FireController` больше не знает конкретный класс оружия, он держит `IFireComponent`. Благодаря этому в проекте одновременно живут `ProjectileFireComponent` (бывший `FireComponent` из 05) и `HitScanFireComponent`, а выбор оружия — решение уровня префаба.

**3. Симуляция попадания.** `HitScanFireComponent` — ядро урока. Детерминированный сид, N лаг-компенсированных рейкастов, отсечение кандидатов, урон, запись состояния.

**4. Сетевое состояние выстрела.** `HitScanState : INetworkStruct` (две сжатые точки), `LastHit`, кольцо `_lastHits`, счётчик `FireCount`.

**5. Представление.** Два `NetworkBehaviour`-презентера, оба работают только в `Render()`, оба в `Spawned()` синхронизируют локальный `_visualFireCount`. Вся косметика — несетевые `MonoBehaviour`.

---

## Реализация по шагам

### Шаг 1. Разведи хитбоксы и физические коллайдеры по разным объектам и слоям

`Assets/Game/Prefabs/Character.prefab` (фрагменты, сведённые из разных мест YAML):

```yaml
# на КОРНЕ Character (тег Character, слой 0), рядом с HealthComponent:
  m_Script: {fileID: 1039544458, guid: e725a070cec140c4caffb81624c8c787, type: 3}  # Fusion.HitboxRoot
  Config: 3
  BroadRadius: 1
  Offset: {x: 0, y: 1, z: 0}
  Hitboxes:
  - {fileID: 7068549961817914289}
---
# дочерний объект <Hitbox>
  m_Layer: 10
  m_Name: <Hitbox>
  m_Script: {fileID: 1014831617, guid: e725a070cec140c4caffb81624c8c787, type: 3}  # Fusion.Hitbox
  Type: 3                 # Capsule
  CapsuleRadius: 0.5
  CapsuleExtents: 2
  Offset: {x: 0, y: 1, z: 0}
  Root: {fileID: 5646907907258848231}
---
# дочерний объект <Collider> — ОТДЕЛЬНО, для PhysX
  m_Layer: 0
  m_Name: <Collider>
```

**Почему именно так.** Это две разные подсистемы с двумя разными моделями времени. PhysX-коллайдер живёт в **настоящем** времени, никакой истории у него нет, и он нужен снарядам из 04–05 (`BulletProjectileConfig` делает `physicsScene.Raycast`) и обычной физике. Хитбокс нужен запросам «как видел стрелок» и регистрируется в буфере снапшотов. Если смешать их на одном объекте, теряется возможность независимо гасить одно и оставлять другое, и `LayerMask` перестаёт разделять подсистемы.

`HitboxRoot` обязан лежать на том же GameObject, что тег `Character` и `HealthComponent` — потому что код достаёт здоровье именно с рута: `hit.Hitbox.Root.GetComponent<HealthComponent>()`. Если позже разнесёшь хитбоксы по костям, `Root` всё равно останется корневым — в этом и смысл конструкции Root/Hitbox.

Два уточнения по YAML, которые легко прочитать неправильно:
- `Hitboxes` — **не** настройка. Док: «All Hitbox instances in hierarchy. **Auto-filled at Spawned**». В инспекторе этот список можно не трогать.
- `Config: 3` — числовых значений перечисления `HitboxRoot.ConfigFlags` в поставке нет (Fusion идёт DLL), а в XML после `ReinitializeHitboxesBeforeRegistration` и `IncludeInactiveHitboxes` объявлены ещё `Legacy` и `Default` («Set of configuration flags with the default behaviour, suitable for most use-cases»). Скорее всего `3` это просто `Default`. Не выдавай расшифровку «Reinit | IncludeInactive» за факт.

### Шаг 2. Выстрел: детерминированный сид + N рейкастов

`Assets/Game/Scripts/GameObject/Core/HitScanFireComponent.cs`

```csharp
[Networked, Capacity(4)]
private NetworkArray<HitScanState> _lastHits { get; } 

[Networked]
public HitScanState LastHit { get; private set; }

[Networked, UnityNonSerialized]
public int FireCount { get; private set; }

public HitScanState GetLastHit(int index)
{
    return _lastHits[index % _lastHits.Length];
}

public void Fire()
{
    int seed = this.Runner.Tick.Raw * unchecked((int) this.Object.Id.Raw);
    Random.InitState(seed);
    
    for (int i = 0; i < _shotCount; i++) 
        this.Shot();
}
```

**Почему именно так.** `UnityEngine.Random` — глобальное изменяемое состояние процесса. Разброс — часть симуляции, а симуляцию Fusion прогоняет много раз: клиент предсказывает тик локально, хост считает его же, и при расхождении клиент **ресимулирует** уже посчитанные тики. Без сида направления будут разными на каждом пире и на каждой ресимуляции: трассы задёргаются, записанный в `[Networked] HitScanState` станет нестабильным, попадания у стрелка и у хоста разойдутся. Сид от `(Tick, ObjectId)` делает разброс чистой функцией от состояния. Умножение на `Object.Id.Raw` нужно, чтобы в одном тике два стрелка не получили **идентичный** разброс.

Про `unchecked` — здесь важна точность. Ключевое слово обёрнуто вокруг **каста** `(int) this.Object.Id.Raw`, а не вокруг умножения: `NetworkId.Raw` — `uint`, и конверсия `uint → int` для значений выше `int.MaxValue` в `checked`-контексте бросила бы `OverflowException`. Само умножение в C# по умолчанию и так unchecked, и `Tick.Raw` — это `int`, а не `uint`. Не пересказывай это как «чтобы умножение не переполнилось».

⚠️ Два изъяна самого разброса, которые стоит знать и не копировать:
1. `Random.insideUnitSphere * _dispersion` подаётся в `Quaternion.Euler(...)`, то есть задаётся и **roll** (ось Z), который на направление `startRotation * Vector3.forward` не влияет вообще — треть «энергии» разброса уходит впустую.
2. `insideUnitSphere` даёт точку **внутри** шара (переменная длина вектора), поэтому углы распределены неравномерно. Для честного конуса бери `Random.insideUnitCircle` по двум осям или `Random.onUnitSphere` с нужным углом.

### Шаг 3. Сам лаг-компенсированный запрос

`Assets/Game/Scripts/GameObject/Core/HitScanFireComponent.cs`

```csharp
private void Shot()
{
    Vector3 startPosition = _firePoint.position;
    Quaternion startRotation = _firePoint.rotation * Quaternion.Euler(Random.insideUnitSphere * _dispersion);
    Vector3 fireDirection = startRotation * Vector3.forward;
    Vector3 endPosition;

    HitboxManager hitboxManager = this.Runner.LagCompensation;
    PlayerRef player = this.Object.InputAuthority;
    bool success = hitboxManager.Raycast(
        startPosition,
        fireDirection,
        _maxFireDistance,
        player,
        out LagCompensatedHit hit,
        _layerMask,
        HitOptions.SubtickAccuracy | HitOptions.IgnoreInputAuthority,
        preProcessRoots: this.FilterHitboxRoots
    );

    if (success)
    {
        Hitbox hitbox = hit.Hitbox;
        HitboxRoot root = hitbox.Root;
        root.GetComponent<HealthComponent>().TakeDamage(_damage);

        endPosition = hit.Point;
    }
    else
    {
        endPosition = startPosition + fireDirection * _maxFireDistance;
    }

    HitScanState hitScan = new HitScanState
    {
        start = startPosition,
        end = endPosition
    };
    
    this.LastHit = hitScan;
    
    _lastHits.Set(this.FireCount % _lastHits.Length, hitScan);
    this.FireCount++;
}
```

**Почему именно так.** Четыре независимых решения в одном вызове.

`player = Object.InputAuthority` — это **и есть** лаг-компенсация. Больше нигде в коде её нет. Передача `PlayerRef` стрелка говорит Fusion: подбери снапшоты хитбоксов того момента, в котором этот игрок видел мир.

`HitOptions.SubtickAccuracy` — док: «Subtick accuracy query (exactly like seen by player)». Рендер клиента всегда **между** тиками. Без этого флага запрос решается против целого тика, и на быстром страфе цель на границе хитбокса даёт ложные промахи/попадания до полтика рассинхрона — при 30 Hz это ~17 мс движения, около 8 см при скорости 5 м/с, и при радиусе капсулы 0.5 это уже ощутимая доля габарита.

`HitOptions.IgnoreInputAuthority` — док: хитбоксы, над которыми запрашивающий игрок имеет InputAuthority, игнорируются. В коде **нет ни одной явной проверки «не я»** — ни по тегу, ни по `InputAuthority`. Её целиком заменяет этот флаг, и он обязателен: `<FirePoint>` находится локально в (0, 1, 0.5), то есть **внутри** собственного хитбокса-капсулы (radius 0.5, offset (0,1,0)), а `_dispersion: 25` градусов разворачивает часть лучей внутрь себя. Уберёшь флаг — игрок убьёт себя первой же очередью.

**Почему хитскан не видит ни землю, ни свою физическую капсулу.** Главная причина — **не** `_layerMask`. Док `Raycast`: «If the `HitOptions.IncludePhysX` or `HitOptions.IncludeBox2D` flag is indicated, query will also include static colliders». Этих флагов в вызове нет, значит PhysX вообще не опрашивается — при любом LayerMask. `_layerMask.m_Bits = 1024` (= 2^10, ровно слой 10 «Hitbox») фильтрует **сами хитбоксы** (у `Hitbox` есть `SetLayer(int)`), то есть это второй, независимый уровень отсечения.

### Шаг 4. Отсечение кандидатов в preProcessRoots

`Assets/Game/Scripts/GameObject/Core/HitScanFireComponent.cs`

```csharp
private void FilterHitboxRoots(
    Query query,
    HashSet<HitboxRoot> candidates, //
    HashSet<int> colliderIndices //-
) => candidates.RemoveWhere(this.IsNotValidTarget);

private bool IsNotValidTarget(HitboxRoot root) => 
    !root.CompareTag(GameObjectTags.Character) || !root.GetComponent<HealthComponent>().Exists();
```

**Почему именно так, а не после запроса.** `Raycast` возвращает **одно** попадание — ближайшее. Если отбросить «невалидную» цель уже после возврата, труп или декорация, лежащие перед живым противником, заберут луч на себя, и выстрел уйдёт в молоко. Постфильтрация результата в хитскан-оружии принципиально неверна.

Делегат вызывается после broadphase и до narrow-phase: «Roles removed from the list will not be processed any further». Мутируется переданный `HashSet` — это сайд-эффект на структуре Fusion, аллокаций нет. Предикат здесь — **метод-группа**, а не лямбда с захватом: меньше мусора на каждый из 4 выстрелов за тик (в `[Teacher]`-варианте стоит лямбда с захватом — так делать не надо).

Фильтр по `Exists()` обязателен, потому что при смерти код гасит **только физический** коллайдер:

```csharp
// Assets/Game/Scripts/GameObject/Content/Character.cs
public override void FixedUpdateNetwork()
{
    _collider.enabled = _healthComponent.Exists();
}
```

`Hitbox`/`HitboxRoot` продолжают регистрироваться в буфере.

⚠️ **Штатная альтернатива, которой в проекте нет.** Fusion 2 даёт `Hitbox.HitboxActive` и `HitboxRoot.HitboxRootActive` («For a hitbox to be hit by lag-compensated queries, both it and its HitboxRoot must be active») плюс `HitboxRoot.SetHitboxActive(Hitbox, bool)`. Рекомендация: гаси хитбокс при смерти один раз вместо фильтрации его на каждом выстреле. Фильтр в `preProcessRoots` тогда оставь только для правил, которые нельзя выразить флагом (команды, неуязвимость, фракции).

⚠️ **Третий параметр делегата не используется, а он мощный.** Док: «Hitbox collider indices added to the processed set will be evaluated in the narrow-phase **regardless of further root processing steps (e.g. layer mask match)**». То есть через `processedColliderIndices` можно принудительно протестировать конкретный коллайдер **в обход LayerMask**. Студенческий код его игнорирует (`HashSet<int> colliderIndices //-`).

### Шаг 5. Сетевое представление события: счётчик + кольцо

`Assets/Game/Scripts/HitScan/HitScanState.cs`

```csharp
[Serializable]
public struct HitScanState : INetworkStruct
{
    public Vector3Compressed start;
    public Vector3Compressed end;

    public Quaternion Rotation => Quaternion.LookRotation(this.Direction, Vector3.up);
    public Vector3 Direction => ((Vector3) this.end - this.start).normalized;
}
```

**Почему именно так.** Реплицируется только то, что нельзя вывести: две сжатые точки. Поворот трассёра вычисляется из направления. Экономия умножается на `Capacity` кольца — каждый слот реплицируется целиком (в `[Teacher]`-варианте в структуре лежит ещё `QuaternionCompressed startRotation`, то есть платится 4–8 раз за то, что считается одной строкой).

Явный каст `(Vector3) this.end` **обязателен**, и причина точная: у `Fusion.Vector3Compressed` есть implicit-операторы и в/из `Vector3`, **и** в/из `Vector2` (`op_Implicit(...)~UnityEngine.Vector2` и `~UnityEngine.Vector3` оба объявлены). Без каста `this.end - this.start` неоднозначно между Vector2- и Vector3-арифметикой.

`INetworkStruct` + только unmanaged-поля — обязательное условие, чтобы структуру можно было положить в `NetworkArray<T>` и в `[Networked]`-свойство.

### Шаг 6. Воспроизведение пропущенных событий в Render()

`Assets/Game/Scripts/GameObject/View/ProjectileHitScanFirePresenter.cs`

```csharp
private int _visualFireCount;

public override void Spawned()
{
    _visualFireCount = _fireComponent.FireCount;
}

public override void Render()
{
    int fireCount = _fireComponent.FireCount;
    if (fireCount <= _visualFireCount)
        return;

    _fireVfx.Play();

    for (int i = _visualFireCount; i < fireCount; i++)
    {
        HitScanState hitscan = _fireComponent.GetLastHit(i);
        ProjectileHitscanView projectile = Instantiate(_projectilePrefab, hitscan.start, hitscan.Rotation, null);
        projectile.Init(hitscan.end);
    }

    _visualFireCount = fireCount;
}
```

**Почему именно так.** Это канонический «воспроизводитель пропущенных сетевых событий», разбери его по частям:

- **`Spawned()` сбрасывает счётчик в текущее сетевое значение.** Когда прокси или поздно подключившийся клиент спавнит чужого персонажа, `FireCount` уже большой. Без этой строки `_visualFireCount` стартует с 0, и первый же `Render()` выдаст залп из десятков вспышек и трассёров из ниоткуда, читая по модулю мусор из кольца на 4 элемента.
- **Цикл, а не одно `LastHit`.** `Render()` идёт по кадрам, а один тик даёт `_shotCount = 4` выстрела. Одного `LastHit` хватило бы только на одну трассу из четырёх — поэтому введено кольцо.
- **Всё в `Render()`, а не в `FixedUpdateNetwork`.** `Render()` вызывается один раз за кадр **после** всей симуляции, включая ресимуляции. Положи ту же логику в FUN — и rollback прогонит тик с выстрелом повторно, выдав несколько вспышек и несколько трассёров на один выстрел.
- **`parent: null` в `Instantiate`.** Трасса не должна ездить за стрелком.

Instant-вариант (`InstantHitScanFirePresenter`) устроен так же, но рисует один `LastHit` в `LineRenderer`:

```csharp
// Assets/Game/Scripts/GameObject/View/InstantHitScanFirePresenter.cs
HitScanState lastHit = _fireComponent.LastHit;
_fireRender.Play(lastHit.start, lastHit.end);
```

⚠️ Заметь жёсткую связку, которая легко ломается: `Capacity(4)` зашита в **код**, а `_shotCount: 4` — редакторское поле префаба. Поднять темп стрельбы в инспекторе выше 4 нельзя без правки исходника: цикл начнёт по модулю читать уже перезаписанные свежие слоты и нарисует неверные трассы. И дефолты в коде отличаются от префаба (`_dispersion = 5` в коде против `25` на префабе, `_maxFireDistance = 10` совпадает) — классическая ловушка «читал код, а работает префаб». Директива: держи `Capacity` с запасом минимум в 2 тика (`2 × shotCount`) и валидируй в `OnValidate`, что `_shotCount <= Capacity / 2`.

### Шаг 7. Косметика — несетевые MonoBehaviour

`Assets/Game/Scripts/HitScan/ProjectileHitScanFireView.cs`

```csharp
public sealed class ProjectileHitscanView : MonoBehaviour
{
    [SerializeField]
    private float _speed = 30;
    
    private float _endTime;

    public void Init(Vector3 destination)
    {
        float distance = (destination - this.transform.position).magnitude;
        float lifetime = distance / _speed;
        _endTime = Time.time + lifetime;
    }

    private void Update()
    {
        if (_endTime == 0)
            return;

        if (Time.time >= _endTime)
            Destroy(this.gameObject);
        else
            this.transform.position += this.transform.forward * _speed * Time.deltaTime;
    }
}
```

**Почему именно так.** Трасса и летящий трассёр ни на что не влияют: урон уже посчитан симуляцией. Делать их `NetworkObject`'ами значит платить трафиком, занимать слоты и втягивать их в rollback, где спавн/деспавн будет мигать. Плюс каждому клиенту нужен свой тайминг — он видит выстрел в своём render-time. Префаб `Assets/Game/Prefabs/Projectiles/HitScanProjectile.prefab` не содержит компонента `NetworkObject` вообще.

Будь точен в описании: трассёр самоуничтожается **по таймеру**, а не «доехав до `end`». `destination` после `Init` не сохраняется, направление берётся только из `transform.forward`. Практическое следствие: при просадке кадров или изменении `timeScale` трассёр **перелетает** точку попадания — сверки с геометрией нет. На префабе `_speed: 3` (при дефолте в коде 30) и `_maxFireDistance: 10`, то есть трассёр живёт до ~3.3 с, и несколько залпов видны одновременно.

⚠️ **Баг в этом же префабе, который стоит поймать.** На дочернем `<Visual>` у `HitScanProjectile.prefab` висит `ColorComponent`, а это `NetworkBehaviour`:

```csharp
// Assets/Game/Scripts/GameObject/View/ColorComponent.cs
public override void Spawned()
{
    Material material = this.HasInputAuthority ? _blue : _red;
    _renderer.material = material;
}
```

`NetworkObject` в префабе отсутствует, значит `Spawned()` **никогда не вызовется** и материал blue/red никогда не выставится. Директива: никогда не вешай `NetworkBehaviour` на префаб, который создаётся через `Instantiate`, а не через `Runner.Spawn`.

⚠️ И ещё один живой баг — в `InstantHitScanFireView.LerpGradient`, в цикле по alpha-ключам:

```csharp
key.time = Mathf.Lerp(fromAlphaKeys[i].time, toColorKeys[i].time, alpha);
```

Берётся время **color**-ключа вместо alpha-ключа. Ошибка продублирована в `[Teacher]/HitscanLineView.cs` — единственном незакомментированном файле этой папки.

### Шаг 8. Подмена оружия на уровне префаба

`Assets/Game/Scripts/Player/FireController.cs`

```csharp
public sealed class FireController : MonoBehaviour
{
    private InputReceiver _inputReceiver;
    
    [SerializeField]
    private GameObject _character;
    
    private IFireComponent _fireComponent;

    private void Awake()
    {
        _inputReceiver = this.GetComponent<InputReceiver>();
        _fireComponent = _character.GetComponent<IFireComponent>();
    }

    private void OnEnable()
    {
        _inputReceiver.OnFire += _fireComponent.Fire;
    }

    private void OnDisable()
    {
        _inputReceiver.OnFire -= _fireComponent.Fire;
    }
}
```

**Почему именно так.** Единственное изменение относительно 05 — тип поля стал `IFireComponent` (`public interface IFireComponent { void Fire(); }`), и этого достаточно, чтобы хитскан и симулируемый снаряд стали взаимозаменяемыми реализациями на уровне префаба. `GetComponent<IFireComponent>()` по интерфейсу Unity поддерживает. Тот же приём на уровне вью: на `<Visual>` висят оба презентера, Instant отключён галочкой (`m_Enabled: 0`), Projectile включён.

Контроллер остаётся обычным `MonoBehaviour` без сетевого состояния: он только связывает событие ввода (поднятое **внутри** `FixedUpdateNetwork`) с сетевым компонентом оружия, поэтому `Fire()` исполняется внутри симуляционного тика и корректно ресимулируется. Если бы `FireController` слушал Unity-`Update`, выстрел попал бы вне тика и перестал быть функцией от ввода.

### Шаг 9. Устойчивый к ресимуляции детект нажатия

`Assets/Game/Scripts/Player/InputReceiver.cs`

```csharp
[Networked]
private NetworkButtons previousButtons { get; set; }

public override void FixedUpdateNetwork()
{
    if (!this.GetInput(out InputData input)) 
        return;
    
    float deltaTime = this.Runner.DeltaTime;
    
    this.ProcessMove(input, deltaTime);
    this.ProcessFire(input);
    
    this.previousButtons = input.buttons;
}

private void ProcessFire(InputData input)
{
    if (input.buttons.WasPressed(this.previousButtons, InputKeys.Fire))
        this.OnFire?.Invoke();
}
```

**Почему именно так.** `previousButtons` **обязан** быть `[Networked]`. Rollback перезапускает уже посчитанные тики; локальная переменная к этому моменту уже затёрта более новым кадром, и фронт нажатия либо потеряется, либо сработает дважды. Сетевое поле откатывается вместе со снапшотом, поэтому ресимуляция видит ровно то «предыдущее» состояние, которое было тогда.

### Шаг 10. Идемпотентность урона

`HealthComponent.TakeDamage` (код приведён выше) начинается с `if (this.Health == 0) return false;` и использует `Mathf.Max(0, Health - damage)`.

**Почему именно так.** Один и тот же тик с уроном Fusion прогоняет повторно при ресимуляции. Поскольку `Health` — `[Networked]`, его значение откатывается вместе со снапшотом и повторное применение даёт тот же результат; но ограничители нужны на случай, когда в одном тике по цели приходит несколько выстрелов (здесь их 4) и когда смерть должна сработать один раз. Логирование `Runner.IsForward` оставлено специально — это учебный инструмент, показывающий разницу между форвард-тиком и ресимом на одном и том же уроне. `Exists()` используется и фильтром хитбоксов, и `CharacterView` — одно условие «жив» на все подсистемы.

---

## Fusion API этой главы

| API | Что делает | Где вызывается | Подводный камень |
|---|---|---|---|
| `NetworkRunner.LagCompensation` → `Fusion.HitboxManager` | «Returns the global instance of a lag compensation buffer HitboxManager» | `HitScanFireComponent.Shot()` | Это глобальный объект раннера, не компонент NetworkObject. Нужен компонент `HitboxManager` в сцене (или `LagCompensation.Enabled` в конфиге, который его автодобавляет). |
| `HitboxManager.Raycast(origin, direction, length, player, out hit, layerMask, options, queryTriggerInteraction, preProcessRoots)` | Лаг-компенсированный рейкаст по истории хитбоксов | `HitScanFireComponent.Shot()` | Возвращает **одно** (ближайшее) попадание. PhysX не опрашивается без `IncludePhysX`. `player` по доке «Used by the server». |
| `HitOptions.SubtickAccuracy` | «Subtick accuracy query (exactly like seen by player)» | там же | Без него до полтика рассинхрона (~17 мс при 30 Hz) на границе хитбокса. |
| `HitOptions.IgnoreInputAuthority` | Исключает хитбоксы, над которыми запрашивающий имеет InputAuthority | там же | Единственная защита от самопопадания в этом коде — явных проверок «не я» нет. |
| `HitOptions.IncludePhysX` / `IncludeBox2D` | Добавляет в запрос статические коллайдеры | ⚠️ в проекте не используется | Именно их отсутствие делает луч «слепым» к земле. Нужны, если хитскан должен упираться в геометрию уровня. |
| `LagCompensation.PreProcessingDelegate(Query, HashSet<HitboxRoot>, HashSet<int>)` | Отсечение кандидатов между broad- и narrow-phase | `FilterHitboxRoots` | Работает по `HitboxRoot`, не по `Hitbox` — отдельную часть тела так не отфильтруешь. Третий параметр позволяет протестировать коллайдер **в обход LayerMask**. |
| `LagCompensatedHit.Hitbox` / `.Point`, `Hitbox.Root` | Путь от попадания к геймплейному объекту | `HitScanFireComponent.Shot()` | `GetComponent` вызывается на `Root`: тег и `HealthComponent` обязаны лежать на том же GameObject. |
| `HitboxRoot` (`Config`, `BroadRadius`, `Offset`, `Hitboxes`) | Регистрация объекта в истории | `Character.prefab` | `Hitboxes` «Auto-filled at Spawned». `BroadRadius` — радиус broadphase-сферы для BVH; слишком маленький обрежет кандидатов до точных тестов (`SetMinBoundingRadius()` подбирает его автоматически). |
| `Hitbox` (`Type`, `CapsuleRadius`, `CapsuleExtents`, `Offset`, `Root`), `Hitbox.SetLayer(int)` | Геометрия хитбокса | `Character.prefab` | `Type: 3` = Capsule (порядок в `HitboxTypes`: None, Box, Sphere, Capsule). |
| `Hitbox.HitboxActive` / `HitboxRoot.HitboxRootActive` / `SetHitboxActive` | Включение/выключение хитбокса для запросов | ⚠️ в проекте не используется | Штатная замена фильтру мёртвых в `preProcessRoots`. |
| `HitboxManager.RaycastAll`, `OverlapSphere`, `OverlapBox` (+ query-перегрузки через `RaycastQuery` / `RaycastAllQuery` / `SphereOverlapQuery` / `BoxOverlapQuery`), `GetClosestHit(List<LagCompensatedHit>)`, `PositionRotation(Hitbox, PlayerRef, out Vector3, out Quaternion, bool)`, `GetPlayerTickAndAlpha(PlayerRef, …)` | Остальной API лаг-компенсации: несколько попаданий, объёмные запросы, позиция хитбокса в тайм-фрейме игрока | ⚠️ в проекте не используется | `RaycastAll` нужен для пробивающего оружия; `OverlapSphere` — для взрывов; `PositionRotation` — для серверной отладки «где был хитбокс». |
| `[Networked, Capacity(N)] NetworkArray<T>` + `Set(index, value)` | Кольцевой журнал событий | `_lastHits` | `Capacity` — это сколько событий ещё можно восстановить. Здесь 4 = `_shotCount`, запаса на второй тик нет. |
| `[Networked, UnityNonSerialized] int FireCount` | Сетевое событие вместо RPC | `HitScanFireComponent` | `UnityNonSerialized` держит публичное свойство вне инспектора, чтобы сериализация префаба его не перезатёрла. При rollback счётчик у клиента может **уменьшиться** — поэтому в презентере стоит `if (fireCount <= _visualFireCount) return;`, а не `!=`. |
| `NetworkBehaviour.Render()` / `Spawned()` | Вся визуализация выстрела | оба презентера | `Render()` — один раз за кадр после симуляции и ресимуляций. `Spawned()` обязан инициализировать `_visualFireCount`. |
| `NetworkButtons.WasPressed(previous, key)` + `[Networked] NetworkButtons` | Фронт нажатия, устойчивый к ресимуляции | `InputReceiver` | `previousButtons` обязан быть `[Networked]`. |
| `NetworkBehaviour.GetInput<T>(out T)` | Фактический гейт авторитета в этом разделе | `InputReceiver.FixedUpdateNetwork` | Отдаёт данные только StateAuthority и InputAuthority. Это единственная «проверка прав» на пути выстрела. |
| `Vector3Compressed` / `QuaternionCompressed` | Сжатые поля в сетевых структурах | `HitScanState`, `ProjectileState` | Implicit-операторы есть и в `Vector3`, **и** в `Vector2` → арифметика без явного каста неоднозначна. |

---

## Настройки, без которых не работает

**NetworkProjectConfig** (`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`, байт в байт совпадает с разделом 05):

```json
"LagCompensation": {
    "Enabled": false,
    "HitboxBufferLengthInMs": 200,
    "HitboxDefaultCapacity": 512,
    "CachedStaticCollidersSize": 64
},
```

Читай этот флаг правильно. XML-док: `F:Fusion.LagCompensationSettings.Enabled` — «Indicates if a `HitboxManager` instance should be added when the `NetworkRunner` is initialized». Флаг управляет **только автодобавлением компонента**. В разделе 06 `HitboxManager` поставлен в сцену **вручную**, и это единственное содержательное изменение `Game.unity` относительно 05. То есть «`Enabled: false` → оружие не стреляет» — ложная тревога: ручная установка компонента заменяет галочку.

Остальное, что влияет на главу:
- `TickRateSelection.Client: 30` — полтика это ~17 мс, поэтому `SubtickAccuracy` имеет смысл.
- `HitboxBufferLengthInMs: 200` — максимальная глубина отката. Игрок с задержкой, выходящей за этот буфер, компенсироваться уже не будет.
- `AssembliesToWeave` содержит `Assembly-CSharp` — без этого `[Networked]` в учебных классах не заработает.
- `NetworkConditions: { "Enabled": false, "DelayMin": 0.15, "DelayMax": 0.15, "AdditionalJitter": 0.05, "LossChanceMin": 0.05 }` — заготовленный симулятор лагов. **Его включение и есть способ увидеть эффект лаг-компенсации**; по умолчанию он выключен.
- `InvokeRenderInBatchMode: true`.

**ProjectSettings.** Единственное изменение во всём `ProjectSettings` относительно 05 — `TagManager.asset`: слой 10 получил имя `Hitbox` (раньше был пустым). Индексация: `Default=0, TransparentFX=1, Ignore Raycast=2, (3), Water=4, UI=5, Player1=6, Player2=7, (8), (9), Hitbox=10` → `m_Bits: 1024` = ровно слой Hitbox. Теги: `Character`, `Bullet`. В `DynamicsManager.asset` матрица коллизий вся в `ff` — слой Hitbox **не** изолирован на уровне PhysX; изоляция обеспечена только тем, что на `<Hitbox>` нет Collider'а вообще.

**Сцена** `Assets/Game/Scenes/Game.unity`, объект `[System]` (fileID 1271488996) — шесть компонентов плюс Transform:
1. `EntryPoint`
2. `NetworkRunner` (fileID -1199893898)
3. `NetworkEvents` (fileID -232609262) — **несущий компонент**: `InputAccumulator`, `PlayerJoinController`, `PlayerLeftController` все ищут его через `FindObjectOfType<NetworkEvents>()`. Без него ни ввод, ни спавн игроков не работают.
4. `NetworkSceneManagerDefault`
5. `RunnerSimulatePhysics3D` (`_physicsAuthority: 2`, `_physicsTiming: 3`, `ClientPhysicsSimulation: 3`, `DeltaTimeMultiplier: 1`, `SetUnityFixedTimestep: 0`) — обслуживает `Runner.GetPhysicsScene()` для снарядов из 04/05.
6. `HitboxManager` (fileID -49887739, поля `BVHDepth`/`BVHNodes`/`TotalHitboxes`) — **добавлен именно в 06**.

`NetworkObjectService` из сцены при этом **удалён**. Механизм, кстати, не только ручной: GUID файла `NetworkObjectService.cs` сменился между разделами (05 — `4fa4d565cd174b6baa04ed35855a455d`, 06 — `c06279cc26cc414981da59a5b4726139`), то есть при переносе папки `System/ObjectProvider/` → `System/NetworkObject/` файл был **пересоздан**, а не перемещён, и ссылка оборвалась бы и без ручной замены. Следствие: `HomingProjectileConfig`, который ищет сервис через `runner.GetSingleton<NetworkObjectService>()`, в этом разделе нерабочий.

**Префаб Character** (тег `Character`, слой 0). Корень: `NetworkObject` (9 `NetworkedBehaviours`), `NetworkTransform`, `MovementComponent` (`_speed 5`), `RotationComponent`, `HealthComponent` (`MaxHealth 5`), `Character`, `HitScanFireComponent`, `HitboxRoot`. Поля оружия: `_firePoint` → `<FirePoint>`, `_maxFireDistance: 10`, `_layerMask.m_Bits: 1024`, `_damage: 1`, `_shotCount: 4`, `_dispersion: 25`. Дети: `<Hitbox>` (слой 10), `<Collider>` (слой 0, `CapsuleCollider` 0.5/2, центр (0,1,0)), `<FirePoint>` (локально (0,1,0.5)), `<Visual>` с `CharacterView`, `ColorComponent` и двумя презентерами (Instant `m_Enabled: 0`, Projectile `m_Enabled: 1`).

⚠️ **Кулдауна у `HitScanFireComponent` нет вообще** — полей `_cooldown`/`TickTimer` в классе не существует, в отличие от `ProjectileFireComponent` (`_cooldown = 1` + `[Networked] TickTimer timer`) и от закомментированного `[Teacher]`-варианта. Темп стрельбы ограничен только тикрейтом: удержание Space даёт выстрел на каждое новое нажатие, но `WasPressed` защищает только от автоогня, не от спама. В своём проекте добавь `TickTimer` по образцу `ProjectileFireComponent`.

**Префаб Player**: `NetworkObject`, `InputReceiver`, `MoveController`, `FireController`, `ProjectileWorld` + вложенный инстанс Character. То есть `ProjectileWorld` — пер-игроковая, живёт на объекте Player, а не в сцене. В `m_Modifications` есть висячий override:

```yaml
- target: {fileID: 408801443769707503, guid: eb3686ca2092740f296aaaca3ccbc891, type: 3}
  propertyPath: _projectileWorld
  value: 
  objectReference: {fileID: 1219346628194265535}
```

Будь точен: висячая здесь **цель** override'а (fileID 408801443769707503 — удалённый `FireComponent` внутри Character.prefab, guid `eb3686ca…` = Character.prefab), а **значение** `objectReference: 1219346628194265535` ссылается на вполне существующий `ProjectileWorld` на самом Player. Мусорная строка, не обрыв данных.

Подсистема снарядов, кстати, **не отключена, а просто не используется**: `ProjectileWorldView` стоит на дочернем `View` в Player.prefab с заполненным `_world`, так что `ProjectileWorld.FixedUpdateNetwork` крутится каждый тик по 8 пустым слотам, а `Render()` вью опрашивает снапшоты каждый кадр. Никто просто не вызывает `Spawn`.

**Регистрация провайдера объектов.** ⚠️ `EntryPoint` передаёт `ObjectProvider = _objectProvider`, но в сцене `_objectProvider: {fileID: 0}` — то есть **null**, и Fusion берёт дефолтный провайдер. `PoolableNetworkObjectProvider` (который корректно переопределяет `InstantiatePrefab`/`DestroyPrefabInstance`) в сцену не подключён — пулинг сетевых объектов в этом разделе фактически не работает.

---

## Антипаттерны и как их обходит этот код

| Антипаттерн | Чем ломается | Как обойдено здесь |
|---|---|---|
| Считать хитскан по текущим позициям на хосте | Надо «стрелять на опережение»; промах в полметра при 100 мс RTT | `Raycast(..., player: Object.InputAuthority, ...)` |
| Вести историю позиций вручную | Своя реализация не совпадёт с тем, что видел клиент, и не учтёт subtick | Историю ведёт системный `HitboxManager`; геймплейный код её не трогает |
| Хитбокс = физический коллайдер | Луч бьёт в свою капсулу, в землю, в декорации; `LayerMask` перестаёт разделять подсистемы | Два отдельных дочерних объекта на разных слоях |
| Проверка «не я» по тегу/`InputAuthority` после попадания | `Raycast` вернул одно попадание — своя капсула его и съела | `HitOptions.IgnoreInputAuthority` |
| Постфильтрация результата запроса | Труп перед целью забирает луч → выстрел в молоко | `preProcessRoots: FilterHitboxRoots` с `RemoveWhere` |
| Лямбда с захватом в `preProcessRoots` | Аллокация на каждый выстрел (здесь 4 за тик) | Метод-группа `this.IsNotValidTarget` |
| `Random` без сида в симуляции | Разброс различается на пирах и на каждой ресимуляции: дрожание трасс, расхождение попаданий, «фантомный» урон | `Random.InitState(Tick.Raw * unchecked((int) Object.Id.Raw))` |
| RPC о выстреле | Не откатывается при rollback, дублируется при ресимуляции, не доходит до late joiner'а, требует надёжной доставки | `[Networked] FireCount` + `NetworkArray<HitScanState>` |
| Визуал выстрела в `FixedUpdateNetwork` | Rollback прогоняет тик повторно → несколько вспышек на один выстрел | Вся косметика в `Render()` |
| Презентер без инициализации в `Spawned()` | Прокси и late joiner выплёвывают всю историю выстрелов на первом кадре | `_visualFireCount = _fireComponent.FireCount` |
| Одно `LastHit` на очередь из N выстрелов | 3 из 4 трасс потеряются между кадрами рендера | Кольцо + цикл `for (i = _visualFireCount; i < fireCount; i++)` |
| Трасса как `NetworkObject` | Трафик, слоты, мигание при rollback, общий тайминг вместо локального | Несетевые `MonoBehaviour` + `Instantiate(..., parent: null)` |
| Репликация выводимых данных | `Capacity` множит цену каждого лишнего поля | В `HitScanState` только `start`/`end`; `Rotation`/`Direction` — вычисляемые свойства |
| Неидемпотентный урон | Ресимуляция уводит здоровье в минус, смерть срабатывает дважды | `if (Health == 0) return false;` + `Mathf.Max(0, …)` |
| Жёсткая зависимость контроллера от класса оружия | Смена оружия = правка кода; два варианта в одном проекте невозможны | `IFireComponent` + `GetComponent<IFireComponent>()` |

Для контраста посмотри `GameObject/Core/CollisionComponent.cs` — он делает `physicsScene.OverlapSphere` прямо в `FixedUpdateNetwork`. Это **не** лаг-компенсированный детект: запрос идёт по текущему состоянию PhysX, без истории и без тайм-фрейма игрока. Подходит для снаряда, который летит много тиков, и не подходит для хитскана. Два способа детекта в одном проекте — ровно то противопоставление, на котором построен раздел.

---

## Чеклист: как применить в своём проекте

1. Поставь компонент `HitboxManager` на сценный объект рядом с `NetworkRunner` **или** включи `LagCompensation.Enabled` в `NetworkProjectConfig` (флаг лишь автодобавляет этот компонент при инициализации раннера). Одного из двух достаточно.
2. Подбери `HitboxBufferLengthInMs` под целевой пинг аудитории (в поставке 200 мс). Игроки за пределами буфера компенсироваться не будут.
3. Завези отдельный слой для хитбоксов (здесь — слой 10 «Hitbox») и держи его только под них.
4. На префаб цели: `HitboxRoot` — на тот же GameObject, где лежат тег и `HealthComponent`; `Hitbox` — на дочерние объекты на слое хитбоксов. `Hitboxes` на руте не заполняй, он автозаполняется в `Spawned`. `BroadRadius` подбирай так, чтобы broadphase-сфера покрывала все хитбоксы (есть `SetMinBoundingRadius()`).
5. Физический `Collider` оставь отдельным дочерним объектом на обычном слое — он нужен PhysX и снарядам, а не хитскану.
6. В компоненте оружия вызывай `Runner.LagCompensation.Raycast(...)` и **всегда** передавай `player: Object.InputAuthority`.
7. Всегда ставь `HitOptions.SubtickAccuracy` — иначе до полтика рассинхрона на границе хитбокса.
8. Всегда ставь `HitOptions.IgnoreInputAuthority`, если точка выстрела лежит внутри собственного хитбокса (почти всегда лежит).
9. Нужно упираться в геометрию уровня — добавь `HitOptions.IncludePhysX`; без него PhysX не опрашивается ни при каком `LayerMask`.
10. `LayerMask` задавай ровно по слою хитбоксов, но не считай его защитой от попадания в землю.
11. Всю фильтрацию целей делай в `preProcessRoots` через `candidates.RemoveWhere(...)` и только методом-группой или статическим предикатом, без лямбд с захватом.
12. Мёртвых и неуязвимых гаси через `HitboxRoot.SetHitboxActive` / `HitboxActive`, а не фильтром на каждом выстреле; фильтр оставь под правила, которые флагом не выразишь.
13. Нужно несколько попаданий (пробивающее оружие, взрыв) — бери `RaycastAll` / `OverlapSphere` / `OverlapBox` с `List<LagCompensatedHit>` и `GetClosestHit`, а не один `Raycast`.
14. Любую случайность в симуляции сей от тика и идентификатора: `Random.InitState(Runner.Tick.Raw * unchecked((int) Object.Id.Raw))`. `unchecked` — вокруг каста `uint → int`.
15. Разброс строй на равномерном распределении по конусу и не задавай roll — `Quaternion.Euler(Random.insideUnitSphere * d)` тратит треть разброса впустую и распределяет углы неравномерно.
16. Урон бери с `hit.Hitbox.Root`, а не с объекта хитбокса, и делай метод идемпотентным (ранний выход при нулевом здоровье + `Mathf.Max(0, …)`).
17. Урон ставь под `if (HasStateAuthority)` — в этом разделе проверки нет, и это не образец для копирования.
18. Событие выстрела публикуй как состояние: `[Networked] int Count` + `[Networked, Capacity(N)] NetworkArray<TState>` с записью `Set(Count % Length, …)`. Никаких RPC.
19. `Capacity` бери с запасом минимум в 2 тика (`2 × shotCount`) и проверяй связку в `OnValidate` — `Capacity` зашита в код, а темп стрельбы настраивается в инспекторе.
20. В сетевой структуре события храни только то, что нельзя вывести; повороты и направления считай свойствами. Арифметику над `Vector3Compressed` пиши с явным кастом в `Vector3`.
21. Весь визуал события — в `Render()`, никогда в `FixedUpdateNetwork`.
22. В `Spawned()` презентера обязательно `_visualCount = component.Count`.
23. Сравнивай счётчики через `if (count <= _visualCount) return;` — при rollback сетевой счётчик может уменьшиться.
24. Пропущенные события доигрывай циклом от `_visualCount` до `count`, а не одним «последним».
25. Косметику делай несетевыми `MonoBehaviour` через `Instantiate(..., parent: null)` и **не** вешай на такие префабы `NetworkBehaviour` — его `Spawned()` никогда не вызовется.
26. `[Networked]` previous-состояние кнопок обязательно, иначе ресимуляция потеряет или продублирует фронт нажатия.
27. Включи `NetworkConditions` (здесь заготовлено 150 мс + 5% потерь) и проверь оружие с лагом — без этого эффект компенсации не увидишь.
28. Проверь, что `StartGameArgs.ObjectProvider` действительно заполнен в инспекторе: в этом разделе он `fileID: 0`, и пулинг молча не работает.

---

## Что изменилось относительно главы 05

Диффом (`diff -rq`) структура подтверждена как идентичная; ниже только учебно значимое.

**Появилось — это и есть урок:**
- `GameObject/Core/HitScanFireComponent.cs` — новый. Лаг-компенсированное оружие целиком.
- `GameObject/Core/IFireComponent.cs` — новый интерфейс с единственным `Fire()`.
- `GameObject/Core/ProjectileFireComponent.cs` — это переименованный `FireComponent.cs` из 05 (в 05 удалён). Тело идентично, добавлена только реализация `IFireComponent`.
- `HitScan/` — новая папка: `HitScanState.cs`, `InstantHitScanFireView.cs` (класс `InstantHitScanView`), `ProjectileHitScanFireView.cs` (класс `ProjectileHitscanView`).
- `GameObject/View/InstantHitScanFirePresenter.cs` и `ProjectileHitScanFirePresenter.cs` — новые презентеры.
- `Assets/Game/Prefabs/Projectiles/` — новая папка: `HitScanProjectile.prefab` (несетевой трассёр) и `ProjectileView (Bullet|Ballistic|Homing).prefab` вместо корневых `Bullet (View)` / `Rocket (View)` / `Homing (View)`.
- `Assets/[Teacher]/HitscanFireComponent.cs`. ⚠️ Важная оговорка: почти вся папка `[Teacher]` закомментирована **целиком** — `HitscanFireComponent.cs`, `HitscanState.cs`, `HitscanProjectileView.cs`, `InstantHitscanFirePresenter.cs`, `ProjectileHitscanFirePresenter.cs`, `NetworkObjectStack.cs`. Живой код там только в `HitscanLineView.cs`. Так что сравнения «в `[Teacher]` стоит `Capacity(8)`», «там лямбда с захватом», «там есть `QuaternionCompressed startRotation`» — это сравнение с мёртвым референсом, а не с альтернативной рабочей реализацией. Полезное из него: `Capacity(8)` (запас на 2 тика), `TickTimer`-кулдаун и `OnDrawGizmos` по кольцу трасс.

**Изменилось в учебном коде:**
- `Player/FireController.cs` — ровно две строки: тип поля и `GetComponent<IFireComponent>()`. Минимальное изменение, открывающее подмену оружия на уровне префаба.
- `GameObject/Core/HealthComponent.cs` — убран один пробел, функционально идентичен 05. Важно: лаг-компенсированное попадание использует **тот же** `TakeDamage`, никакого отдельного «серверного» пути урона не вводится.
- `Projectiles/Core/ProjectileState.cs` — `Vector3`/`Quaternion` → `Vector3Compressed`/`QuaternionCompressed` (+ свойства-обёртки), `CUSTOM_DATA_SIZE` 16 → 32, проверка размера вынесена в `AssertSize<T>()`.
- `Projectiles/Core/ProjectileConfig.cs` — опечатка `IniitalizeStep` → `InitializeStep`, `PlayerRef player` → `in PlayerRef player`; из контракта удалены `GetPosition`/`GetRotation`; из `GetRenderPosition`/`GetRenderRotation` удалён параметр `in float renderTime`.
- `Projectiles/Core/ProjectileType.cs` — `Rocket = 1` → `Ballistic = 1`; `BulletConfig.cs` → `BulletProjectileConfig.cs`, `RocketConfig.cs` → `BallisticProjectileConfig.cs`.
- `Projectiles/Content/HomingProjectileConfig.cs` — вместо сырого `CustomData<NetworkId>()` явная вложенная `[Serializable] public struct CustomData : INetworkStruct { public NetworkId target; }`; добавлены `_lifetime`, `_stoppingDistance`, выводимый `searchRadius => _speed * _lifetime`, `IsExpired`.
- `Projectiles/Core/ProjectileWorld.cs` — убран Odin, `Capacity(MAX_PROJECTILES)` → `Capacity(8)`, `TryGetProjectileSnapshots(previous/current)` → `TryGetSnapshots(from/to)`.
- `Projectiles/View/ProjectileWoldView.cs` — расчёт `renderTime` **удалён из вью** и перенесён в каждый config. Теперь каждый конфиг сам выбирает тайм-фрейм: `runner.IsServer || runner.LocalPlayer == player ? runner.LocalRenderTime + deltaTime : runner.RemoteRenderTime`, затем `time = renderTime - to.tick * deltaTime`. Зачем: баллистика считает позицию аналитической формулой от времени, а не лерпом между снапшотами, так что общий `renderTime` навязывал всем одну модель времени. `+ deltaTime` — экстраполяция своих/серверных снарядов на тик вперёд, чтобы они не отставали от собственного ввода.
- `System/ObjectProvider/` → `System/NetworkObject/` (папка переименована, файлы пересозданы — GUID'ы сменились), `FindClosestInRadius` → `FindClosest`. ⚠️ **Регрессия:** из `NetworkObjectService.FindClosest` удалён `ListPool<NetworkObject>.Release(buffer)`, который был в 05 — буфер пула теперь утекает на каждый поиск цели. Для контраста `ProjectileViewPool.Pool.Return` страхуется `_stack.Contains(view)` перед `Push` с `Debug.LogWarning` — хорошая пара «как надо / как получилось».

**Изменилось в конфигурации:**
- `ProjectSettings/TagManager.asset` — слой 10 назван `Hitbox`. Единственное изменение во всём `ProjectSettings`. `NetworkProjectConfig.fusion` идентичен 05 байт в байт.
- `Assets/Game/Scenes/Game.unity` — единственное изменение: на `[System]` компонент `NetworkObjectService` заменён на Fusion `HitboxManager`.
- `Assets/Game/Prefabs/Character.prefab` — удалены `FireComponent` и корневой `CapsuleCollider`; добавлены `HitScanFireComponent`, `HitboxRoot`, дочерние `<Collider>` и `<Hitbox>`; на `<Visual>` добавлены `FireRender` (LineRenderer) и оба презентера. ⚠️ Компонент `Character` к новшествам раздела **не относится** — он уже стоял на префабе в 05, и `Character.cs` в дифф скриптов не попал.
- `Assets/Game/Prefabs/Player.prefab` — `NetworkedBehaviours` вырос с 3 до 9 элементов; появился висячий **target** override'а `_projectileWorld`.

**Смысл перехода 05 → 06.** Глава 05 учила «как показать чужой снаряд в прошлом» (интерполяция, снапшоты, render time). Глава 06 учит обратному: «как посчитать мгновенное попадание в прошлом» (откат истории хитбоксов в тайм-фрейм стрелка) и «как восстановить мгновенное событие в представлении без RPC» (счётчик + кольцевой буфер). Подсистема снарядов из 04–05 остаётся в коде, рефакторится (сжатие состояния, упрощение контракта) и остаётся смонтированной в префабе — просто никто не вызывает `Spawn`. Активный путь урона в разделе один: хитскан.
