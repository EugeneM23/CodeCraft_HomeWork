# Глава 02. Сетевые компоненты и механики

> **TL;DR для агента**
> - Разбивай игровую логику на мелкие компоненты и выбирай базовый класс по одному критерию: **есть `[Networked]`-поле или нужен `FixedUpdateNetwork`/`Render` → `NetworkBehaviour`, иначе `MonoBehaviour`**. Компоненты связывай C#-событиями и `Func<bool>`, а не взаимными ссылками.
> - Читай ввод **ровно в одном месте** через `GetInput`, храни `previousButtons` как `[Networked]` и транслируй наружу намерения (`OnMove`, `OnFire`), а не кнопки.
> - Детектируй попадания `Runner.GetPhysicsScene().OverlapSphere(...)` внутри `FixedUpdateNetwork`, а не через `OnTriggerEnter`. Результат записывай в `[Networked] bool`, а `Despawn` делай отдельно, в следующем проходе FUN.
> - Чтобы у клиента не было задержки выстрела на RTT, держи `[Networked] NetworkArray<NetworkObject>` предспавненных объектов, снятых с симуляции, и выдавай их через `AssignInputAuthority`. Не-сетевые следствия (`SetActive`) восстанавливай на прокси диффом в `Render()`.
> - Пулинг включай не в игровом коде, а в `NetworkObjectProviderDefault.InstantiatePrefab/DestroyPrefabInstance` — тогда `Runner.Spawn`/`Runner.Despawn` остаются единственным API игровой логики.

Проект главы: `C:/Users/MY PC/Downloads/Модуль V. Мультиплеер/02 - Сетевые компоненты и механики/_src/Lessons-kinematic-physics-bullet-31.03.25`.
Unity 2022.3.42f1, Fusion 2.0.5 Stable 920 (2025-02-12). Учебный код — 28 файлов в `Assets/Game/Scripts/**`. Папок `[Teacher]`/`[Archive]` в этом разделе нет.

---

## Какую проблему решает

Раздел закрывает четыре разные инженерные задачи, и важно не смешивать их:

1. **Декомпозиция.** Типовой «скрипт игрока» на 500 строк в мультиплеере хуже, чем в сингле: каждое поле такого класса — кандидат в сетевое состояние, и веавер Fusion платит за весь класс целиком. Раздел показывает, как резать логику на атомарные механики и как при этом **не** превращать их все в `NetworkBehaviour`.
2. **Детерминированный детект попаданий.** Unity-колбэки физики (`OnTriggerEnter`, `OnCollisionEnter`) привязаны к `FixedUpdate` Unity, а не к тикам Fusion, и при ресимуляции не воспроизводятся в нужный тик. Урон, построенный на них, в сети становится то двойным, то потерянным. Решение — явный запрос к физической сцене раннера внутри `FixedUpdateNetwork`.
3. **Задержка спавна у клиента.** В Host-режиме клиент не имеет State Authority и не может создать сетевой объект сам: `Runner.Spawn` на нём — это просьба к хосту, и снаряд появится через RTT (на 150 мс канале это заметно физически). Решение — буфер уже существующих во всех копиях мира `NetworkObject`.
4. **Цена `Instantiate`/`Destroy`.** Снаряды живут 1–5 секунд, значит создаются и уничтожаются постоянно. Решение — пулинг в штатной точке расширения Fusion, прозрачный для игрового кода.

---

## Модель мира: кто чем владеет

Конфигурация (`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` + `EntryPoint.cs`): `PeerMode: 0` (Single-Peer), `GameMode.AutoHostOrClient`. То есть первый пир становится хостом (State Authority на всё), остальные — клиентами. Shared-режим в этом разделе не используется.

**Кто чем владеет:**

| Объект | State Authority | Input Authority |
|---|---|---|
| `Character` (спавнится в `PlayerJoinController`) | хост | игрок, которому он принадлежит (`runner.Spawn(..., player)`) |
| `Bullets` (**сценный** NetworkObject, `SortKey: 2750755712`, `Flags: 262145`) | хост всегда | нет |
| Снаряд из буфера | хост | присваивается клиенту через `AssignInputAuthority` в момент выстрела |

То, что `Bullets` — сценный объект, а не спавненный префаб, важно архитектурно: State Authority над буфером принадлежит хосту **по построению сцены**, а не по договорённости в коде. Поэтому ветки «наполнить буфер» и «фолбэк на `Runner.Spawn`» гарантированно хостовые. Переносимое правило: инфраструктурные `[Networked]`-данные уровня матча кладите на сценный NetworkObject.

**Где исполняется код:**

- `FixedUpdateNetwork` идёт на пире, который *симулирует* объект. По умолчанию это State Authority и Input Authority. Чтобы FUN шёл и на прокси, нужен `Runner.SetIsSimulated(Object, true)` — именно это делают `Character.Spawned()` и `PhysicsBullet.Spawned()`.
- `GetInput` возвращает `true` только там, где ввод доступен (State Authority + Input Authority). На прокси — `false`, поэтому ранний `return` в `InputReceiver` обязателен.
- `Render()` идёт на всех пирах каждый кадр. В конфиге `InvokeRenderInBatchMode: true`.
- **`Runner.Despawn` на не-StateAuthority пире — тихий no-op.** Прямая цитата из `Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml` (`M:Fusion.NetworkRunner.Despawn`): «This method checks if the local simulation has state authority over the NetworkObject.» Отсюда следует центральный вывод главы: клиент не может «предсказанно удалить» снаряд, он может только записать в сетевое состояние факт попадания и дождаться, пока хост удалит объект и реплицирует это.

**Авторитет в пути нанесения урона: его нет.** Проверено `grep` по `GameObject/Core/*.cs`, `GameObject/Content/*.cs`: ни `HasStateAuthority`, ни `IsServer` в `CollisionComponent`, `KinematicBullet`, `PhysicsBullet`, `HealthComponent` не встречаются ни разу (единственное упоминание авторитета в этих папках — `HasInputAuthority` в `ColorComponent`). Значит цепочка `CollisionComponent.FixedUpdateNetwork → ProcessCollision → HealthComponent.TakeDamage` пишет `[Networked] Health` на **каждом** пире, который симулирует снаряд: на хосте — авторитетно, на клиенте-стрелке — как предсказание, которое следующий снапшот хоста либо подтвердит, либо перезапишет. То же про `[Networked] bool _collided` и `[Networked] TickTimer timer` в `FireComponent`. Именно поэтому в `TakeDamage` стоит диагностический лог с `Runner.Tick` и `Runner.IsForward`: он даёт увидеть, как один и тот же урон проигрывается в форварде и затем ещё раз в ресимуляции.

**Время.** `TickRateSelection.Client: 30` в конфиге, `Fixed Timestep: 0.02` в `ProjectSettings/TimeManager.asset`. Это **разные** частоты, и они не обязаны совпадать. Отсюда железное правило раздела: шаг механики получает `Runner.DeltaTime` параметром, `Time.deltaTime` в сетевой логике не читается нигде.

---

## Архитектура раздела

```
         клавиатура (Update)
                │
                ▼
   ┌─ InputAccumulator : MonoBehaviour ────────────┐   подписан на NetworkEvents.OnInput
   │  buttons.Set(InputKeys.Fire, GetKey(Space))   │   (компонент Fusion на [System])
   │  input.Set(new InputData{ dir, buttons })     │
   └───────────────────────┬───────────────────────┘
                           │  Fusion доставляет кадр ввода на симуляцию
                           ▼
   ┌─ InputReceiver : NetworkBehaviour ────────────┐   ЕДИНСТВЕННЫЙ GetInput в разделе
   │  if (!GetInput(out InputData)) return;        │   [Networked] previousButtons
   │  OnMove(dir, Runner.DeltaTime) / OnFire()     │   → наружу уходят НАМЕРЕНИЯ
   └─────────┬───────────────────────────┬─────────┘
             │ C#-event                  │ C#-event
             ▼                           ▼
   MoveController (Mono)          FireController (Mono)        ← адаптеры «ввод → механика»
             │                           │
             ▼                           ▼
   MovementComponent (Mono)       FireComponent (Network)
   RotationComponent (Mono)         [Networked] TickTimer
     Func<bool> _condition                │ TrySpawn
             ▲                            ▼
             │ SetCondition      ┌─ NetworkObjectSpawner (сценный объект Bullets) ─┐
   Character (сборщик) ──────────┤ [Networked, Capacity(8)] NetworkArray<NetworkObject>
     HealthComponent (Network)   │ [Networked] int _bufferIndex                    │
       [Networked] Health        │ Render(): diff против _localBuffer на прокси    │
       [OnChangedRender]         └──────────────────┬──────────────────────────────┘
             │                                     ▼ выданный снаряд
             ▼                      PhysicsBullet / KinematicBullet
   CharacterView (View)              [Networked] bool _collided
     _visual.SetActive(hp>0)                 ▲
                                             │ ProcessCollision(buffer,count)
                                  CollisionComponent : NetworkBehaviour
                                    FUN: Runner.GetPhysicsScene().OverlapSphere(...)

   уровень ниже и ортогонально:  PoolableNetworkObjectProvider : NetworkObjectProviderDefault
                                 (Instantiate/Destroy ↔ Queue, каталог-ScriptableObject)
```

**Слои и их границы:**

- `Scripts/System/**` — инфраструктура сцены. Почти всё — обычные `MonoBehaviour`, подписанные на `NetworkEvents` (`OnInput`, `PlayerJoined`, `PlayerLeft`). Сюда же `NetworkObjectSpawner`/`NetworkObjectBuffer` и провайдер пула.
- `Scripts/GameObject/Core/**` — атомарные механики: `MovementComponent`, `RotationComponent` (Mono), `HealthComponent`, `FireComponent`, `LifetimeComponent`, `CollisionComponent` (Network). Ни одна механика не знает про другую.
- `Scripts/GameObject/Content/**` — «сборщики»: `Character`, `CharacterView`, `KinematicBullet`, `PhysicsBullet`. Только здесь механики узнают друг о друге, и только через `[SerializeField]`.
- `Scripts/Player/**` — адаптеры ввода: `InputReceiver`, `MoveController`, `FireController`.
- `Scripts/GameObject/View/**` — `ColorComponent`, чистый вид.

**Два буфера на одном объекте.** В сцене на `Bullets` висят **оба** компонента, и оба включены (`m_Enabled: 1`, оба зарегистрированы в `NetworkObject.NetworkedBehaviours`: `2072367389` → `NetworkObjectBuffer`, guid `dd841b92…`; `2072367390` → `NetworkObjectSpawner`, guid `a53b464d…`). У обоих `_prefab` = `Bullet (Physics)`, `_bufferSize: 8`. То есть `NetworkObjectBuffer` — не «спящий эталон»: его `Spawned() → FillBuffer()` реально выполняется, в рантайме предспавнивается **16** снарядов, и его `Render()`-реконсиляция крутится каждый кадр на прокси. Игровой код (`FireComponent`) зовёт только `NetworkObjectSpawner`. Учебно это полезно: два независимых `[Networked]`-массива на одном NetworkObject наглядно показывают, что сетевое состояние принадлежит **компоненту**, а не объекту. `NetworkObjectSpawner` — единственный учебный класс без `sealed`, и его XML-`<summary>` слово в слово совпадает с `NetworkObjectBuffer`: это авторская производная от референсной версии Photon.

---

## Реализация по шагам

### Шаг 1. Точка входа

`Assets/Game/Scripts/EntryPoint.cs`

```csharp
private void Start()
{
    _networkRunner.ProvideInput = true;

    _networkRunner.StartGame(new StartGameArgs
    {
        GameMode = GameMode.AutoHostOrClient,
        SessionName = "SampleSession",
        Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
        SceneManager = _sceneManager,
        ObjectProvider = _objectProvider
    });
}
```

**Почему именно так.** `ProvideInput = true` ставится **до** `StartGame`: без него `NetworkEvents.OnInput` не вызовется и `GetInput` всегда вернёт `false` — игра запустится, но персонаж не сдвинется. `SceneRef.FromIndex(активная сцена)` вместе с `NetworkSceneManagerDefault.IsSceneTakeOverEnabled: 1` позволяет Fusion «присвоить» уже загруженную сцену, не перезагружая её: нет отдельной лобби-сцены, нажал Play — играешь. `ObjectProvider` — штатная подмена фабрики инстансов.

⚠️ **Дефект сцены, который важно не скопировать.** В `Game.unity` у `EntryPoint` стоит `_objectProvider: {fileID: 0}`, хотя `PoolableNetworkObjectProvider` (`fileID: 1271489003`) физически висит на `[System]` с настроенными `_catalog` и `_container: {fileID: 2122250561}` (объект `Pool`). То есть в текущем состоянии сцены **провайдерный пулинг в `StartGame` не передаётся** (вывод по YAML, не по коду). Проверяйте такие ссылки: `null` в `[SerializeField]` не даёт ошибки компиляции и ломается молча. Автор, кстати, держит для этого отдельный инструмент — `Assets/Tools/ReferenceCheck/` (вендорная папка, но упомянуть стоит: весь раздел построен на ссылках из инспектора).

### Шаг 2. Кадр ввода

`Assets/Game/Scripts/System/Input/InputData.cs` и `InputKeys.cs`

```csharp
public struct InputData : INetworkInput
{
    public Vector3 moveDirection;
    public NetworkButtons buttons;
}

public enum InputKeys
{
    None = 0,
    Fire = 1,
    Reload = 2,
    Jump = 3,
    Interact = 4
}
```

**Почему именно так.** `InputDataWordCount: 0` в конфиге — размер структуры выводится веавером, вручную его задавать не нужно. А вот `InputKeys` требует осторожности: по XML-докам Fusion 2.0.5 `NetworkButtons.Set(System.Int32 button, ...)` и `WasPressed(NetworkButtons previous, System.Int32 button)` трактуют аргумент как **индекс бита** («The index of the button»), а дженерик-перегрузки просто кастуют enum к int. Это **не** `[Flags]`-маска: значения enum — номера битов, и `None = 0` занимает нулевой бит, а не означает «ничего нажато». Пока используется только `Fire`, бага нет. Переносимое правило: такой enum нумеруйте с 1, никогда не вводите в него комбинированные значения и не заводите элемент `None`.

### Шаг 3. Сбор ввода на обычном MonoBehaviour

`Assets/Game/Scripts/System/Input/InputAccumulator.cs`

```csharp
private void Awake()
{
    _networkEvents = FindObjectOfType<NetworkEvents>();
}

private void OnEnable()  { _networkEvents.OnInput.AddListener(this.OnInput); }
private void OnDisable() { _networkEvents.OnInput.RemoveListener(this.OnInput); }

private void OnInput(NetworkRunner runner, NetworkInput input)
{
    var buttons = new NetworkButtons();
    this.AccumulateFire(ref buttons);

    var inputData = new InputData
    {
        moveDirection = this.AccumulateMove(),
        buttons = buttons
    };

    input.Set(inputData);
}

private void AccumulateFire(ref NetworkButtons buttons)
{
    buttons.Set(InputKeys.Fire, Input.GetKey(KeyCode.Space));
}
```

**Почему именно так.** Альтернатива — реализовать `INetworkRunnerCallbacks` и получить один класс с десятком несвязанных методов. `NetworkEvents` превращает колбэки раннера в `UnityEvent`, и каждая ответственность уезжает в отдельный маленький компонент с нормальным жизненным циклом Unity. Подписка строго в `OnEnable`, отписка в `OnDisable` — иначе выключенный объект продолжит получать ввод. В сцене у `NetworkEvents` все `m_PersistentCalls` пустые: вся подписка идёт из кода, в инспекторе ничего не проставлено.

Обратите внимание: здесь `Input.GetKey` (удержание), а не `GetKeyDown`. Фронт нажатия детектируется **на стороне симуляции**, а не на стороне сбора — потому что только симуляция знает, какой тик она сейчас проигрывает.

### Шаг 4. Единственная точка чтения ввода

`Assets/Game/Scripts/Player/InputReceiver.cs`

```csharp
public sealed class InputReceiver : NetworkBehaviour
{
    public event Action<Vector3, float> OnMove;
    public event Action OnFire;

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

    private void ProcessMove(InputData input, float deltaTime)
    {
        Vector3 moveDirection = input.moveDirection.normalized;
        if (moveDirection != Vector3.zero)
            this.OnMove?.Invoke(moveDirection, deltaTime);
    }

    private void ProcessFire(InputData input)
    {
        if (input.buttons.WasPressed(this.previousButtons, InputKeys.Fire))
            this.OnFire?.Invoke();
    }
}
```

**Почему именно так.** Три решения, каждое закрывает свой класс багов.

1. **Ранний `return` при `!GetInput`.** Без него прокси начнут двигать объект локально по мусорным данным и разъедутся с сервером. С ним на прокси не стреляет ни одно событие — вообще ни одна механика не выполняется.
2. **`previousButtons` обязано быть `[Networked]`.** Fusion переигрывает тики при ресимуляции. Если держать предыдущее состояние кнопок в обычном поле, при повторном проигрывании тика N сравнение пойдёт с состоянием из тика N+3 — и выстрел либо продублируется, либо исчезнет. `[Networked]` откатывается вместе с остальным состоянием. Заметьте: свойство **приватное** — `[Networked]` работает и с приватными авто-свойствами.
3. **Наружу уходят намерения, не кнопки.** `OnMove(Vector3, float)` и `OnFire()` — механики не знают ни про `InputData`, ни про `NetworkButtons`, ни про сеть вообще. И `deltaTime` берётся из `Runner.DeltaTime` **здесь** и передаётся параметром, чтобы механика физически не могла прочитать `Time.deltaTime`.

Проверено `grep`: `GetInput` в разделе встречается ровно один раз — в этом файле.

### Шаг 5. Адаптеры «ввод → механика»

`Assets/Game/Scripts/Player/MoveController.cs`

```csharp
private void Awake()
{
    _inputReceiver = this.GetComponent<InputReceiver>();
    _movementComponent = this.GetComponent<MovementComponent>();
    _rotationComponent = this.GetComponent<RotationComponent>();
}

private void OnEnable()  { _inputReceiver.OnMove += this.OnMove; }
private void OnDisable() { _inputReceiver.OnMove -= this.OnMove; }

private void OnMove(Vector3 direction, float deltaTime)
{
    _movementComponent.MoveStep(direction, deltaTime);
    _rotationComponent.RotateStep(direction, deltaTime);
}
```

`Assets/Game/Scripts/Player/FireController.cs`

```csharp
private void OnEnable()  { _inputReceiver.OnFire += _fireComponent.Fire; }
private void OnDisable() { _inputReceiver.OnFire -= _fireComponent.Fire; }
```

**Почему именно так.** Это чистые `MonoBehaviour` — у них нет сетевого состояния, они лишь склеивают событие с вызовом. Два разных стиля: `MoveController` подписывает **свой** приватный метод, потому что одно событие надо развести на два вызова; `FireController` подписывает метод механики напрямую, потому что вызов один. Оба в списке `NetworkedBehaviours` префаба отсутствуют — проверено по `Character.prefab` (`MoveController` = `3067079622937131539`, `FireController` = `540261866940632050`, в списке их нет).

### Шаг 6. Механика без сети: критерий выбора базового класса

`Assets/Game/Scripts/GameObject/Core/MovementComponent.cs`

```csharp
public sealed class MovementComponent : MonoBehaviour
{
    [SerializeField]
    private float _speed = 5;

    private Func<bool> _condition;

    public void SetCondition(Func<bool> condition) { _condition = condition; }

    public bool CanMove() { return _condition == null || _condition.Invoke(); }

    public void MoveStep(Vector3 direction, float deltaTime)
    {
        if (this.CanMove()) 
            this.transform.position += direction * (_speed * deltaTime);
    }
}
```

**Почему именно так.** `NetworkBehaviour` стоит денег: веавер, слот в сетевом состоянии, участие в ресимуляции, запись в `NetworkObject.NetworkedBehaviours`. У `MovementComponent` нет `[Networked]`-полей и он не нуждается в собственном FUN — позицию реплицирует `NetworkTransform`. Значит он `MonoBehaviour`. В `Character.prefab` это видно напрямую: `NetworkedBehaviours` содержит 7 элементов (`NetworkTransform`, `InputReceiver`, `HealthComponent`, `FireComponent`, `Character`, `ColorComponent`, `CharacterView`), а `MovementComponent` (`4809922655746850113`) и `RotationComponent` (`7786442775252456377`) в него не попали.

Второе решение — **внешнее условие как `Func<bool>`, а не ссылка на `HealthComponent`**. Если бы `MovementComponent` знал про здоровье, то каждая новая причина «нельзя двигаться» (стан, перезарядка, катсцена) правила бы сам `MovementComponent`. С `Func<bool>` тот же компонент работает на персонаже (условие = жив) и на кинематическом снаряде (условия нет вообще — `CanMove()` вернёт `true`). `RotationComponent` устроен так же (`CanRotate()`, `RotateStep` через `Quaternion.RotateTowards`, `_speedRate: 2000` в префабе).

### Шаг 7. Состояние и мост к виду

`Assets/Game/Scripts/GameObject/Core/HealthComponent.cs`

```csharp
public sealed class HealthComponent : NetworkBehaviour
{
    //View
    public event Action<int> OnHealthChanged;

    [Networked]
    [OnChangedRender(nameof(InvokeHealthChanged))]
    public int Health { get; private set; }

    public int MaxHealth;

    public override void Spawned()
    {
        Health = MaxHealth;
    }

    public bool TakeDamage(int damage)
    {
        if (this.Health == 0)
            return false;

        Debug.Log($"TAKE DAMAGE {Object.Name} {Health} {Runner.Tick} {this.Runner.IsForward}");
        this.Health = Mathf.Max(0, this.Health - damage);
        return true;
    }

    public bool Exists() { return this.Health > 0; }

    private void InvokeHealthChanged() { this.OnHealthChanged?.Invoke(this.Health); }
}
```

**Почему именно так.** Четыре приёма в одном файле.

1. **`[OnChangedRender]` как мост модель→вид.** Визуал нельзя обновлять в FUN: в ресимуляции тик проигрывается несколько раз, и UI/партиклы будут мигать и срабатывать повторно. Атрибут переносит колбэк на фазу рендера. ⚠️ Точная семантика (сравнение отрендеренных значений, а не каждой записи в FUN) **кодом и доками раздела не подтверждается** — в `Fusion.Runtime.xml` про `OnChangedRenderAttribute` сказано только «This attribute is used to specify a method that should be called when the property changes». Считайте это выводом о поведении Fusion, а не фактом из кода; практический вывод — обновляйте вид отсюда, а не из FUN.
2. **`TakeDamage` возвращает `bool` «урон реально нанесён».** Снаряд использует это как критерий «я попал», так что состояние цели и решение о despawn снаряда согласованы одной проверкой, без второго источника истины.
3. **`if (Health == 0) return false`** не даёт двум снарядам в одном тике «добить» уже мёртвого и некорректно схлопнуть логику.
4. **`Health = MaxHealth` именно в `Spawned()`, без проверки авторитета.** Это сознательно: объект приходит из пула/буфера, `Awake` на нём уже отработал когда-то давно, и единственный момент «новая жизнь началась» — это `Spawned`. В `Character.prefab` рядом с `MaxHealth: 5` сериализовано `_Health: 0` — это бэкинг-поле, которое веавер создал для `[Networked] Health`. Прямое доказательство в YAML, что `[Networked]`-свойство превращается в обычное сериализуемое поле, и заодно объяснение, почему без строки в `Spawned()` объект стартовал бы с нулём здоровья. `MaxHealth` при этом — обычное публичное поле, не `[Networked]`: это конфиг из префаба, он не меняется в рантайме.

Сравните с соседним файлом `LifetimeComponent.cs`, где вариант со `Spawned()` **специально закомментирован**:

```csharp
// public override void Spawned()
// {
//     this.timer = TickTimer.CreateFromSeconds(this.Runner, _lifetime);
// }

//Server side...
public void Init()
{
    this.timer = TickTimer.CreateFromSeconds(this.Runner, _lifetime);
}

public override void FixedUpdateNetwork()
{
    if (this.timer.Expired(this.Runner)) 
        this.Runner.Despawn(this.Object);
}
```

Два противоположных решения в двух соседних файлах — и это и есть учебный смысл. Здоровье сбрасывается в `Spawned`, потому что «жизнь объекта» = «его существование». Время жизни снаряда стартует в `Init()`, потому что `Spawned` у буферизованного снаряда прошёл задолго до выстрела, и таймер, запущенный там, убил бы снаряд прямо в буфере. **Правило: если момент «реального начала» не совпадает со `Spawned`, заводите явный `Init()`.**

### Шаг 8. Вид, подписанный на модель

`Assets/Game/Scripts/GameObject/Content/CharacterView.cs`

```csharp
public override void Spawned()
{
    _healthComponent.OnHealthChanged += this.OnHealthChanged;
    this.OnHealthChanged(_healthComponent.Health);
}

public override void Despawned(NetworkRunner runner, bool hasState)
{
    _healthComponent.OnHealthChanged -= this.OnHealthChanged;
}

private void OnHealthChanged(int health)
{
    _visual.SetActive(health > 0);
}
```

**Почему именно так.** Модель о виде не знает ничего (в `HealthComponent` над событием стоит только комментарий `//View`). Критическая деталь — **явный вызов `this.OnHealthChanged(_healthComponent.Health)` сразу после подписки**: change-событие срабатывает только на *изменение*, поэтому присоединившийся позже клиент (или объект, пришедший из пула) без этой строки отрисовал бы вид по состоянию префаба, а не по актуальному сетевому `Health`. Переносимое правило: **подписка на change-событие всегда идёт в паре с синхронизацией по текущему значению.**

Отписка в `Despawned` — не вежливость, а необходимость: при пулинге GameObject не уничтожается, тот же инстанс вернётся из `Queue` и снова получит `Spawned()`. Без `-=` подписка удвоится, и обработчик сработает дважды.

### Шаг 9. Сборщик

`Assets/Game/Scripts/GameObject/Content/Character.cs`

```csharp
public override void Spawned()
{
    _movementComponent.SetCondition(_healthComponent.Exists);
    _rotationComponent.SetCondition(_healthComponent.Exists);
    this.Runner.SetIsSimulated(this.Object, true);
}

public override void FixedUpdateNetwork()
{
    _collider.enabled = _healthComponent.Exists();
}
```

**Почему именно так.** `Character` — не «класс персонажа», а единственное место, где механики узнают друг о друге. `SetCondition(_healthComponent.Exists)` — передача метода как делегата: без аллокации лямбды и без компиляционной связи `MovementComponent → HealthComponent`.

`SetIsSimulated(Object, true)` расширяет исполнение FUN на прокси. Без него коллайдер мёртвого персонажа не выключился бы на пирах, которые им не владеют, и попадания считались бы «по трупу». Выключение коллайдера сделано именно в FUN, а не в `Render`/`Update`, чтобы физическое состояние было детерминированным по тикам — иначе `OverlapSphere` в чужом FUN увидит разную картину на разных пирах.

### Шаг 10. Детект коллизий без Unity-колбэков

`Assets/Game/Scripts/GameObject/Core/CollisionComponent.cs`

```csharp
public sealed class CollisionComponent : NetworkBehaviour
{
    public delegate void Processor(Collider[] buffer, int count);

    public event Processor ProcessCollision;

    [SerializeField] private Transform _center;
    [SerializeField] private float _radius = 1;
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private QueryTriggerInteraction _triggerInteraction;
    [SerializeField] private Collider[] _buffer = new Collider[8];

    public override void FixedUpdateNetwork()
    {
        PhysicsScene physicsScene = this.Runner.GetPhysicsScene();
        int count = physicsScene.OverlapSphere(_center.position, _radius, _buffer, _layerMask, _triggerInteraction);
        this.ProcessCollision?.Invoke(_buffer, count);
    }

    private void OnDrawGizmos()
    {
        if (_center)
            Gizmos.DrawWireSphere(_center.position, _radius);
    }
}
```

**Почему именно так.** Это замена `OnTriggerEnter` на детерминированный запрос внутри сетевого тика. `Runner.GetPhysicsScene()` отдаёт физическую сцену **именно этого раннера** (критично для мультипирного режима и для ParrelSync-тестов), а вызов из FUN гарантирует, что детект произойдёт в том же тике и при ресимуляции. Собственный `delegate` с `(buffer, count)` вместо `List`/`IEnumerable` плюс предвыделенный `Collider[8]` убирают аллокации в самом горячем месте проекта: запрос идёт каждый тик на каждом снаряде. Потребитель итерирует `for (int i = 0; i < count; i++)` — буфер отдаётся как есть, валидная длина передаётся отдельно.

Все параметры — сериализуемые поля, поэтому один компонент настраивается и под кинематический, и под физический снаряд (в обоих префабах `_radius: 0.125`, `_layerMask.m_Bits: 1` — только `Default`, `_triggerInteraction: 0`).

Деталь из префабов: у обоих снарядов `_center` указывает на **корневой** `Transform` (`5428821966894332721`), а не на дочерний `<Visual>` (`7002937625849939667`). Корень живёт в тиковом пространстве, визуал интерполируется отдельно — логика обязана опираться на корень.

### Шаг 11. Снаряд: флаг вместо немедленного despawn

`Assets/Game/Scripts/GameObject/Content/KinematicBullet.cs`

```csharp
[Networked]
private bool _collided { get; set; }

public override void Spawned()
{
    _collisionComponent.ProcessCollision += this.ProcessCollision;
}

public override void Despawned(NetworkRunner runner, bool hasState)
{
    _collisionComponent.ProcessCollision -= ProcessCollision;
}

//State Authority & Input Authority
public override void FixedUpdateNetwork()
{
    _movementComponent.MoveStep(this.transform.forward, this.Runner.DeltaTime);
    if (_collided)
        this.Runner.Despawn(this.Object);
}

//Fixed Update Network
private void ProcessCollision(Collider[] buffer, int count)
{
    if (_collided)
        return;

    for (int i = 0; i < count; i++)
    {
        Collider collider = buffer[i];
        if (this.DealDamage(collider))
        {
            _collided = true;
            return;
        }
    }
}

private bool DealDamage(Collider collider)
{
    if (!collider.CompareTag(GameObjectTags.Character))
        return false;

    NetworkObject other = collider.GetComponent<NetworkObject>();
    if (other.InputAuthority == this.Object.InputAuthority)
        return false;

    HealthComponent healthComponent = other.GetComponent<HealthComponent>();
    return healthComponent.TakeDamage(_damage);
}
```

**Почему именно так.**

- **`[Networked] bool _collided` вместо `Despawn` прямо из обработчика.** Решение «я попал» становится частью сетевого состояния: оно переигрывается при ресимуляции и, главное, **доживает до хоста**. Напомню цитату из доков: клиентский `Runner.Despawn` — no-op, он ничего не удалит. Так что флаг тут не «предсказанное удаление», а единственный способ сообщить решение. Плюс `if (_collided) return` на входе защищает от повторного урона и в этом тике, и в последующих, пока объект ещё жив.
- **Фильтр по возрастанию стоимости:** тег → `InputAuthority` → `TakeDamage`. Friendly fire решается сравнением `other.InputAuthority == this.Object.InputAuthority` — **без собственного `[Networked] PlayerRef _owner`**. `InputAuthority` уже реплицируется Fusion и уже присваивается снаряду при выдаче, так что это и дешевле по байтам, и на один источник расхождения меньше. Теги вынесены в константы: `GameObjectTags.Character = nameof(Character)` — никаких магических строк.
- **Подписка/отписка строго симметричны** — обязательно из-за буфера и пула.

⚠️ **Слабое место раздела, не копируйте его.** Вопреки заявленному «композиция через `[SerializeField]`», **межобъектные** связи решаются рантайм-поиском: `collider.GetComponent<NetworkObject>()` и `other.GetComponent<HealthComponent>()` вызываются на **каждый** коллайдер из `OverlapSphere` на **каждом** тике. Внутри одного объекта ссылки действительно проставлены в YAML (проверено для `Character`, `KinematicBullet`, `PhysicsBullet`, `CollisionComponent`), но путь «снаряд → цель» — это два `GetComponent` в самом горячем коде. Рекомендация: кешировать карту `Collider → HealthComponent` или положить на цель один компонент-фасад, который достаётся одним `TryGetComponent`.

`PhysicsBullet` отличается от кинематического тремя вещами: он не двигает себя сам (`Rigidbody` + `NetworkRigidbody3D`), гасит визуал в `Render()` (`_visual.SetActive(!_collided)` — не-сетевое следствие сетевого флага, на фазе рендера) и имеет `Init()`:

```csharp
public void Init()
{
    _rigidbody.isKinematic = false;
    _rigidbody.velocity = this.transform.forward * _speed;
}
```

**Порядок строк обязателен:** `velocity` на kinematic-Rigidbody Unity молча проигнорирует. В префабе `m_IsKinematic: 0`, и никто в учебном коде не ставит его в `true`, значит первая строка защитная — объект приходит из буфера снятым с симуляции, и `NetworkRigidbody3D` аддона сам управляет kinematic-состоянием несимулируемого тела (вывод о поведении аддона, не факт из учебного кода).

**Разница префабов снарядов (по YAML), из которой вырастает следующий раздел:**

| | `Bullet (Kinematic)` | `Bullet (Physics)` |
|---|---|---|
| Коллайдер | **нет вообще** | `SphereCollider`, `m_Radius: 0.125`, `m_IsTrigger: 0` |
| Rigidbody | нет | есть, `m_UseGravity: 1`, `m_Drag: 0`, `m_Interpolate: 0`, `m_CollisionDetection: 0` |
| Траектория | прямая (`MoveStep(transform.forward)`) | **баллистическая дуга** |
| `_lifetime` | `1` | `5` |
| Репликация | `NetworkTransform` (`SyncParent: 0`, `_autoAOIOverride: 1`) | `NetworkRigidbody3D`, `NetworkTransform` отсутствует |
| `ParentTransformConstraint` | нет | есть, `_parentName: '[World]'` |
| `NetworkedBehaviours` (5) | NetworkTransform, LifetimeComponent, KinematicBullet, CollisionComponent, ColorComponent | LifetimeComponent, NetworkRigidbody3D, PhysicsBullet, CollisionComponent, ColorComponent |

Кинематический снаряд **нечем ударить** (коллайдера нет) — он только испускает запросы. `_lifetime: 5` у физического компенсирует дугу: он летит дольше и дальше по времени. `MovementComponent` кинематического снаряда в `NetworkedBehaviours` не попал — тот же критерий, что у `Character`.

Ещё деталь про компоновку: `ColorComponent` на всех трёх префабах висит на **дочернем** GameObject `<Visual>`, но зарегистрирован в `NetworkedBehaviours` **корневого** NetworkObject (`Character`: `767492506650752275`; оба снаряда: `7266777733760057856`). `NetworkBehaviour`-ы одного NetworkObject могут жить на детях — это и объясняет, почему `_renderer` указывает на рендерер визуала, а `_center` коллизии — на корень.

### Шаг 12. Выстрел

`Assets/Game/Scripts/GameObject/Core/FireComponent.cs`

```csharp
[Networked]
private TickTimer timer { get; set; }

private NetworkObjectSpawner _bulletSpawner;

public override void Spawned()
{
    _bulletSpawner = FindObjectOfType<NetworkObjectSpawner>();
}

public void Fire()
{
    if (!this.timer.ExpiredOrNotRunning(this.Runner))
        return;

    if (_bulletSpawner.TrySpawn(
            _firePoint.position,
            _firePoint.rotation,
            this.Object.InputAuthority,
            out NetworkObject bullet))
    {
        bullet.GetComponent<LifetimeComponent>().Init();
        bullet.GetComponent<PhysicsBullet>().Init();
    }

    this.timer = TickTimer.CreateFromSeconds(this.Runner, _cooldown);
}
```

**Почему именно так.** Кулдаун — `[Networked] TickTimer`, а не `float` + `Time.time`: таймер живёт в тиках, ресимуляция даёт тот же результат, и клиент может предсказать свой выстрел без расхождения с хостом. `ExpiredOrNotRunning` выбран вместо `Expired` именно ради начального состояния: default-таймер «не запущен» тоже означает «можно стрелять». Для `LifetimeComponent`, наоборот, нужен `Expired` — там таймер гарантированно запущен в `Init()`, и `ExpiredOrNotRunning` despawn-ил бы снаряд в первом же тике.

⚠️ **Два реальных дефекта в этих 20 строках. Исправляйте при переносе.**

1. **Перезарядка стоит ВНЕ блока `if (TrySpawn(...))`.** Клиент с пустым буфером (`TrySpawn` вернул `false`, фолбэк на `Runner.Spawn` доступен только State Authority) теряет и выстрел, **и** кулдаун: нажатие съедено, снаряда нет, следующая попытка — только через секунду. Правильно — перезаряжать внутри успешной ветки.
2. **`FindObjectOfType<NetworkObjectSpawner>()` в `Spawned()`** — поиск по всей сцене на каждого персонажа при каждом `Spawned`, включая каждое извлечение `Character` из пула. И тип в поиске жёстко зашит — именно поэтому `NetworkObjectBuffer.Get<T>()` в разделе не вызывается никем. Переносимый вывод здесь **обратный** тому, что подаёт код: механика сама ищет инфраструктуру вместо того, чтобы получить её инъекцией от сборщика `Character`, как получает условие `Func<bool>`. Это нарушение собственной декомпозиции раздела.
3. **`GetComponent<PhysicsBullet>()` вызывается безусловно.** На `Bullet (Kinematic)` этого компонента нет, так что переключение `_prefab` на кинематический уронит выстрел `NullReferenceException`.

### Шаг 13. Предспавненный буфер — ядро главы

`Assets/Game/Scripts/System/NetworkObject/NetworkObjectSpawner.cs`

```csharp
[Networked, Capacity(CAPACITY)]
private NetworkArray<NetworkObject> _buffer { get; }

[Networked]
private int _bufferIndex { get; set; }

private NetworkObject[] _localBuffer;

public override void Spawned()
{
    _localBuffer = new NetworkObject[_bufferSize];

    if (this.HasStateAuthority)
        for (int i = 0; i < _bufferSize; i++)
            _buffer.Set(i, this.CreateInstance());
}

public bool TrySpawn(Vector3 position, Quaternion rotation, PlayerRef player, out NetworkObject instance)
{
    instance = _buffer[_bufferIndex];
    if (instance != null)
    {
        instance.AssignInputAuthority(player);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);

        this.Runner.SetIsSimulated(instance, true);

        _buffer.Set(_bufferIndex, this.HasStateAuthority ? this.CreateInstance() : null);
        _bufferIndex = (_bufferIndex + 1) % _bufferSize;
        
        return true;
    }
    
    if (!this.HasStateAuthority)
        return false;

    instance = this.Runner.Spawn(_prefab, position, rotation, player);
    return true;
}

private NetworkObject CreateInstance()
{
    NetworkObject instance = this.Runner.Spawn(_prefab, _outsidePosition);
    instance.gameObject.SetActive(false);
    this.Runner.SetIsSimulated(instance, false);
    return instance;
}
```

**Почему именно так.** Объект уже существует во всех копиях мира, поэтому Input Authority получает снаряд **мгновенно**, без RTT: `AssignInputAuthority` + `SetPositionAndRotation` + `SetActive(true)` + `SetIsSimulated(true)`.

- `NetworkArray` — read-only свойство без сеттера, запись только через `.Set(index, value)`. `NetworkObject` в нём сериализуется как `NetworkId`.
- **Тернарник в `_buffer.Set` — суть предсказания.** State Authority подставляет в освободившуюся ячейку свежий инстанс; клиент пишет туда `null` как локальное предсказание, которое следующий снапшот либо подтвердит, либо откатит.
- `_bufferIndex` тоже `[Networked]` — иначе клиент и хост разошлись бы в том, какую ячейку считать текущей. (В референсном `NetworkObjectBuffer` то же поле называется `_bufferHead` — это два разных класса с разными именами полей.)
- `CreateInstance` спавнит объект в `_outsidePosition` (в сцене `{x: 0, y: -1000, z: 0}`), гасит GameObject и **обязательно** снимает с симуляции. Без `SetIsSimulated(false)` `LifetimeComponent` отсчитал бы время жизни и despawn-ил снаряд прямо в буфере, а `CollisionComponent` жёг бы `OverlapSphere` каждый тик на восьми невидимых объектах.
- **Фолбэк доступен только State Authority.** Клиент при пустом буфере честно возвращает `false` и не стреляет, вместо того чтобы предсказать выстрел, которого хост не подтвердит. Расхождение хуже пропущенного выстрела.
- `_localBuffer` аллоцируется в `Spawned()` **на всех пирах** (прокси теневая копия нужна не меньше), а наполнение `_buffer` — только под `HasStateAuthority`.

Обязательная вторая половина приёма — разбор:

```csharp
public override void Despawned(NetworkRunner runner, bool hasState)
{
    if (this.HasStateAuthority)
    {
        for (int i = 0; i < _bufferSize; i++)
        {
            NetworkObject instance = _buffer[i];
            if (instance != null)
                this.Runner.Despawn(instance);
        }

        _buffer.Clear();
    }
    else
    {
        Array.Clear(_localBuffer, 0, _localBuffer.Length);
    }
}
```

Без этого 8 (а в этой сцене 16) невидимых снарядов переживут despawn спавнера и останутся висеть на `(0, -1000, 0)` — утечка сетевых объектов, которая на ресетах матча накапливается.

### Шаг 14. Реконсиляция не-сетевых следствий в Render()

`Assets/Game/Scripts/System/NetworkObject/NetworkObjectSpawner.cs`

```csharp
public override void Render()
{
    if (this.HasStateAuthority)
        return;

    for (int i = 0; i < _bufferSize; i++)
    {
        NetworkObject networkInstance = _buffer[i];
        NetworkObject localInstance = _localBuffer[i];

        if (localInstance == networkInstance)
            continue;

        if (localInstance != null && localInstance.IsValid)
        {
            // Network instance was released so we need to activate
            // object on all clients (including proxies) not only
            // on those where Get method was called
            localInstance.gameObject.SetActive(true);
#if UNITY_EDITOR
            localInstance.name = _prefab.name;
#endif
        }

        _localBuffer[i] = networkInstance;

        if (networkInstance != null)
        {
            // New instance was added to the buffer, we need to make sure
            // that the object is inactive on all clients (including proxies)
            networkInstance.gameObject.SetActive(false);
#if UNITY_EDITOR
            networkInstance.name = $"(Buffered) {networkInstance.name}";
#endif
        }
    }
}
```

**Почему именно так.** Это решает проблему, которую легко не заметить: **`GameObject.activeSelf` — не сетевое состояние.** Прокси (третий игрок, который не стрелял) никогда не вызывает `TrySpawn`, значит у него выданный снаряд так и остался выключенным и невидимым. И наоборот: когда хост положил в ячейку новый инстанс, его надо спрятать на всех пирах.

Приём универсален: **держите теневую локальную копию `[Networked]`-данных и в `Render()` диффьте её против сетевого состояния, применяя не-сетевые следствия.** Схема переносится на любое не-сетевое следствие сетевого состояния — активация, звук, партиклы, слой.

Детали, без которых не работает: `localInstance.IsValid` — объект мог быть уже despawned; `if (HasStateAuthority) return` — на хосте состояние и так истинное, диффить нечего; переименование в `(Buffered) ...` только под `UNITY_EDITOR` — это не просто отладка, см. следующий шаг.

### Шаг 15. Пулинг в точке расширения Fusion

`Assets/Game/Scripts/System/NetworkObject/PoolableNetworkObjectProvider.cs`

```csharp
public sealed class PoolableNetworkObjectProvider : NetworkObjectProviderDefault
{
    [SerializeField] private PoolableNetworkObjectCatalog _catalog;
    [SerializeField] private Transform _container;

    private Dictionary<NetworkObject, Queue<NetworkObject>> _pool;

    private void Awake()
    {
        IReadOnlyList<NetworkObject> prefabs = _catalog.Prefabs;
        int count = prefabs.Count;

        _pool = new Dictionary<NetworkObject, Queue<NetworkObject>>(count);
        for (int i = 0; i < count; i++)
        {
            NetworkObject prefab = prefabs[i];
            _pool.Add(prefab, new Queue<NetworkObject>());
        }
    }

    protected override NetworkObject InstantiatePrefab(NetworkRunner runner, NetworkObject prefab)
    {
        if (!_catalog.ContainsPrefab(prefab))
            return base.InstantiatePrefab(runner, prefab);

        Queue<NetworkObject> queue = _pool[prefab];
        if (queue.TryDequeue(out NetworkObject instance))
            instance.gameObject.SetActive(true);
        else
            instance = Instantiate(prefab);

        instance.name = prefab.name;
        return instance;
    }

    protected override void DestroyPrefabInstance(
        NetworkRunner runner,
        NetworkPrefabId prefabId,
        NetworkObject instance
    )
    {
        if (!_catalog.FindPrefab(instance.name, out NetworkObject prefab))
        {
            base.DestroyPrefabInstance(runner, prefabId, instance);
            return;
        }

        Queue<NetworkObject> queue = _pool[prefab];
        queue.Enqueue(instance);

        instance.gameObject.SetActive(false);
        instance.transform.parent = _container;
    }
}
```

**Почему именно так.** Пулинг вставлен в штатную точку расширения (`NetworkObjectProviderDefault.InstantiatePrefab` / `DestroyPrefabInstance` — `protected virtual`, `Assets/Photon/Fusion/Runtime/NetworkObjectProviderDefault.cs`), поэтому игровой код продолжает звать `Runner.Spawn`/`Runner.Despawn` и о пуле не знает вообще. Это **два ортогональных уровня**: провайдер экономит `Instantiate`/`Destroy`, буфер экономит сетевой round-trip. Они независимы.

- **`Awake` несущий.** Без него словаря не существует, и индексатор `_pool[prefab]` бросил бы исключение. `Awake` заранее создаёт по одной `Queue` на каждый префаб каталога с точной ёмкостью словаря. Приём: пул **не растёт по требованию**, набор ключей фиксируется каталогом на старте, поэтому `_pool[prefab]` можно писать индексатором без `TryGetValue` — отказоустойчивость заменена белым списком.
- **Белый список — опт-ин, не «пулим всё».** Каталог (`PoolableNetworkObjectCatalog.asset`) содержит ровно два префаба: `Bullet (Physics)` и `Character`. `Bullet (Kinematic)` в каталог не входит и честно уходит в `base`.
- ⚠️ **Ключом обратного поиска служит ИМЯ инстанса**, не `NetworkPrefabId`: `FindPrefab(instance.name, ...)`. Отсюда жёсткая связка с `#if UNITY_EDITOR`-переименованием в буфере: `(Buffered) Bullet (Physics)` не найдётся в каталоге, и объект уйдёт в `Destroy` вместо пула. Поэтому `Render()` обязан вернуть имя назад при выдаче. Хрупко; при переносе лучше ключевать по `NetworkPrefabId` или по компоненту-метке на инстансе.

### Шаг 16. Системная логика на UnityEvent

`Assets/Game/Scripts/System/Players/PlayerJoinController.cs`

```csharp
private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
{
    if (!runner.IsServer)
        return;

    Transform spawnPoint = _spawnPointService.NextPoint();
    NetworkObject character = runner.Spawn(_characterPrefab, spawnPoint.position, spawnPoint.rotation, player);
    runner.SetPlayerObject(player, character);
}
```

`Assets/Game/Scripts/System/Players/PlayerLeftController.cs`

```csharp
private void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
{
    if (runner.IsServer)
    {
        NetworkObject character = runner.GetPlayerObject(player);
        runner.Despawn(character);
    }
}
```

**Почему именно так.** `SetPlayerObject`/`GetPlayerObject` — штатный реестр «игрок → его персонаж», который ведёт и реплицирует сам Fusion. Свой `Dictionary<PlayerRef, NetworkObject>` не нужен и, в отличие от штатного, умеет рассинхронизироваться. Обе ветки ограничены сервером, но стиль разный: guard-clause против положительной обёртки — приём один, оформление два.

`SpawnPointService.NextPoint()` — bag-random: точка удаляется из `_freePoints` и список перезаполняется только при опустошении. Чистый `Random.Range` по массиву дал бы повторы и спавн двух игроков в одной точке. Несетевой `Random` здесь безопасен **потому что вызывается только на сервере**: решение принимает один пир и реплицирует результат как позицию спавна.

`ParentTransformConstraint` в `OnEnable` делает `GameObject.Find(_parentName)` и уезжает под `[World]`. Чтобы перепарентивание не разъехалось, у `Character` `NetworkTransform.SyncParent: 1`, у `Bullet (Physics)` `NetworkRigidbody3D.SyncParent: 1`. `SyncScale: 0` выключен везде — лишние байты на то, что не меняется.

---

## Fusion API этой главы

| API | Что делает | Где вызывается | Подводный камень |
|---|---|---|---|
| `NetworkRunner.ProvideInput` | разрешает пиру поставлять ввод | `EntryPoint.Start` | ставить **до** `StartGame`; без него `OnInput` не зовётся и `GetInput` всегда `false` |
| `StartGame(new StartGameArgs{ GameMode, SessionName, Scene, SceneManager, ObjectProvider })` | единая точка старта | `EntryPoint` | `ObjectProvider` можно забыть проставить в инспекторе — именно это и произошло в `Game.unity` |
| `NetworkEvents.OnInput / PlayerJoined / PlayerLeft` | колбэки раннера как `UnityEvent` | `InputAccumulator`, `PlayerJoinController`, `PlayerLeftController` | находится через `FindObjectOfType` в `Awake`; подписка строго `OnEnable`/`OnDisable` |
| `NetworkInput.Set(inputData)` / `INetworkInput` | упаковка кадра ввода | `InputAccumulator.OnInput` | `InputDataWordCount: 0` — размер выводит веавер |
| `NetworkBehaviour.GetInput(out InputData)` | единственное чтение ввода | `InputReceiver.FixedUpdateNetwork` | на прокси `false`; без раннего `return` прокси начнут симулировать локально |
| `NetworkButtons.Set(key, bool)` / `WasPressed(prev, key)` | фронт нажатия поверх удержания | `InputAccumulator`, `InputReceiver` | аргумент — **индекс бита**, не флаг; `prev` обязано быть `[Networked]` |
| `[Networked]` на приватном авто-свойстве | сетевое состояние | `previousButtons`, `timer`, `_collided`, `_bufferIndex` | работает и с `private`; в YAML появляется бэкинг-поле (`_Health: 0`) |
| `[OnChangedRender(nameof(M))]` | колбэк на изменение, на фазе рендера | `HealthComponent.Health` | срабатывает только на **изменение** — после подписки синхронизируйте вид вручную; точная семантика в доках не описана (вывод) |
| `TickTimer.CreateFromSeconds` / `ExpiredOrNotRunning` / `Expired` | время в тиках | `FireComponent`, `LifetimeComponent` | `ExpiredOrNotRunning` для «ещё не запускался», `Expired` для «запущен и истёк»; на `Time.time` ресимуляция ломается |
| `[Networked, Capacity(N)] NetworkArray<T>` | сетевой массив | `NetworkObjectSpawner`, `NetworkObjectBuffer` | read-only свойство, запись только `.Set(i, v)`; `NetworkObject` сериализуется как `NetworkId` |
| `NetworkRunner.SetIsSimulated(obj, bool)` | прогон FUN на этом пире | `Character`, `PhysicsBullet`, оба буфера | `false` для лежащих в буфере — иначе `LifetimeComponent` despawn-ит снаряд в пуле |
| `NetworkObject.AssignInputAuthority(player)` | передача владения уже существующему объекту | `TrySpawn`, `Get` | авторитет меняется **после** `Spawned` — поэтому `ColorComponent` красит и в `Render()` |
| `NetworkRunner.Spawn(prefab, pos, rot, player)` | создание объекта | `PlayerJoinController`, `CreateInstance`, фолбэк | только State Authority; на клиенте — это RTT-запрос к хосту |
| `NetworkRunner.Despawn(obj)` | удаление | `LifetimeComponent`, оба снаряда, `PlayerLeftController`, `Despawned` буфера | **no-op на не-StateAuthority** (XML-док); зовите из FUN, не из `Render`/колбэка физики |
| `SetPlayerObject` / `GetPlayerObject` | реестр «игрок → объект» | `PlayerJoin/LeftController` | ведёт и реплицирует сам Fusion — свой словарь не нужен |
| `NetworkRunner.GetPhysicsScene().OverlapSphere(...)` | детерминированный запрос коллизий | `CollisionComponent.FixedUpdateNetwork` | физическая сцена **этого** раннера; нужен `RunnerSimulatePhysics3D`, иначе позиции неактуальны |
| `HasStateAuthority` / `HasInputAuthority` / `Object.InputAuthority` | развилки прав и friendly-fire | буферы, снаряды, `ColorComponent` | сравнение `InputAuthority` — самый дешёвый фильтр «не бить своего» |
| `Runner.DeltaTime` / `Runner.Tick` / `Runner.IsForward` | тиковое время и фаза | `InputReceiver`, `KinematicBullet`, `HealthComponent` | `Time.deltaTime` даёт разную дистанцию на пирах; `IsForward` — диагностика ресимуляции |
| `Spawned()` / `Despawned(runner, hasState)` | парная инициализация и отписка | все Content-классы, буферы | при пулинге GameObject не умирает — без `-=` подписка удвоится |
| `Render()` | фаза визуала и реконсиляции | `ColorComponent`, `PhysicsBullet`, буферы | в буферах активен только при `!HasStateAuthority` |
| `NetworkObjectProviderDefault.InstantiatePrefab/DestroyPrefabInstance` | точка расширения под пул | `PoolableNetworkObjectProvider` | обратный поиск идёт по **имени** инстанса |
| `NetworkTransform` (`SyncParent`, `SyncScale`, `_autoAOIOverride`) | репликация трансформа | `Character`, `Bullet (Kinematic)` | `SyncParent: 1` обязателен, если объект перепарентивается |
| `NetworkRigidbody3D` + `_interpolationTarget` | сетевое физическое тело | `Bullet (Physics)` | `_interpolationTarget` — дочерний `<Visual>`, не корень |
| `RunnerSimulatePhysics3D` | отдаёт `Physics.Simulate()` под Fusion | объект `[System]` в сцене | без него `OverlapSphere` в FUN видит устаревшие позиции |

---

## Настройки, без которых не работает

**`NetworkProjectConfig.fusion`:**
- `PeerMode: 0` (Single-Peer), `Simulation.PlayerCount: 10`.
- `AssembliesToWeave` содержит `Fusion.Addons.Physics` и `Fusion.Addons.FSM` — без первого не соткётся `NetworkRigidbody3D`.
- `Simulation.TickRateSelection: { Client: 30, ServerIndex: 0, ClientSendIndex: 1, ServerSendIndex: 1 }`. Тикрейт Fusion живёт **отдельно** от `Fixed Timestep: 0.02` — ровно поэтому механики обязаны получать `Runner.DeltaTime` параметром.
- `Simulation.ReplicationFeatures: 1`. По `Fusion.Runtime.xml` это `Scheduling`, а `SchedulingAndInterestManagement` = 2. Значит **Interest Management (AOI / Explicit Interest) на уровне проекта выключен**, и `ObjectInterest: 1` на префабах вместе с `_autoAOIOverride: 1` у `Bullet (Kinematic)` в этом разделе ни на что не влияют — это задел на будущее, а не действующая настройка.
- `LagCompensation.Enabled: false`. Хитбоксов и `HitboxRoot` в разделе нет вообще (`grep` по префабам — ноль), попадания считаются обычными физическими оверлапами.
- `Simulation.InputDataWordCount: 0`, `InvokeRenderInBatchMode: true`, `NullChecksForNetworkedProperties: true`, `EnqueueIncompleteSynchronousSpawns: false`, `HostMigration.EnableAutoUpdate: false`, `Heap: { PageShift: 15, PageCount: 256 }`.
- **`NetworkConditions` настроен, но `Enabled: false`**: `DelayMin/DelayMax: 0.15`, `AdditionalJitter: 0.05`, `LossChanceMin/Max: 0.05`. Это штатный способ включить 150 мс задержки и 5% потерь и **своими глазами увидеть**, зачем нужен предспавненный буфер. Без него весь раздел на localhost выглядит бессмысленным: разницы между `Runner.Spawn` и буфером не видно. **Включайте это при разработке предсказания.**

**Сцена `Game.unity`, объект `[System]`:** `NetworkRunner`, `NetworkEvents`, `NetworkSceneManagerDefault` (`IsSceneTakeOverEnabled: 1`), `EntryPoint`, `PoolableNetworkObjectProvider`, и `RunnerSimulatePhysics3D` с `_physicsAuthority: 2` (`PhysicsAuthorities.Fusion`), `_physicsTiming: 3` (`PhysicsTimings.FixedUpdateNetwork`), `ClientPhysicsSimulation: 3` (`SimulateAlways` — «Physics.Simulate() is called every tick»), `SetUnityFixedTimestep: 0`, `DeltaTimeMultiplier: 1`. Декодировка — по `RunnerSimulatePhysicsEnums.cs`.

Важное уточнение про `ProjectSettings/DynamicsManager.asset`: `m_SimulationMode: 0` — это **дефолт Unity** (`FixedUpdate`), он ничего не говорит о Fusion. Переключение делает аддон в рантайме: `RunnerSimulatePhysics3D.OverrideAutoSimulate` ставит `UnityEngine.Physics.simulationMode = SimulationMode.Script`, когда авторитет — Fusion. А вот `m_AutoSyncTransforms: 0` значим — `RunnerSimulatePhysicsBase.WarnAutoSyncTransforms` его проверяет и ругается, если он включён. Также значимо `m_QueriesHitTriggers: 1`.

**`TagManager`:** ровно два тега — `Character` и `Bullet` (на них опираются `GameObjectTags` и `CompareTag`), слои `Player1`/`Player2` на индексах 6 и 7, матрица коллизий целиком открыта.

**Сцена, объект `Bullets`:** сценный NetworkObject (`Flags: 262145`) с **двумя** включёнными буферами, у обоих `_prefab` = `Bullet (Physics)`, `_bufferSize: 8`.

**Порядок компонентов и ссылки.** Весь раздел держится на `[SerializeField]`-ссылках, проставленных в инспекторе. `null` здесь не даёт ошибки компиляции — он падает в рантайме, иногда через минуту игры. Заводите проверку ссылок (в проекте это `Assets/Tools/ReferenceCheck/`) или валидацию в `OnValidate`.

---

## Антипаттерны и как их обходит этот код

| Антипаттерн | Чем плох в сети | Как сделано здесь |
|---|---|---|
| Всё игровое — `NetworkBehaviour` | веавер, слот состояния, участие в ресимуляции на компонентах, которым это не нужно | базовый класс выбирается по наличию `[Networked]`/нужде в FUN; `MovementComponent`/`RotationComponent`/контроллеры — `MonoBehaviour` и в `NetworkedBehaviours` не попадают |
| `Time.deltaTime` / `Time.time` в сетевой логике | разная дистанция на пирах, кулдауны ломаются при ресимуляции | `Runner.DeltaTime` берётся в `InputReceiver` и передаётся параметром; кулдауны — `[Networked] TickTimer` |
| `OnTriggerEnter` / `OnCollisionEnter` для урона | привязаны к `FixedUpdate` Unity, при ресимуляции срабатывают не в том тике; урон то двойной, то потерянный | `Runner.GetPhysicsScene().OverlapSphere(...)` прямо в FUN, буфер предвыделен |
| `previousButtons` в обычном поле | при ресимуляции сравнение идёт с «будущим» состоянием — выстрел дублируется или теряется | `[Networked] NetworkButtons previousButtons` |
| `Despawn` прямо из обработчика коллизии | на клиенте это no-op; на хосте — изменение набора объектов посреди обхода, плюс второй удар до удаления | `[Networked] bool _collided` + `Despawn` отдельным проходом в FUN, `if (_collided) return` на входе |
| Обновление вида из `FixedUpdateNetwork` | ресимуляция проигрывает тик несколько раз — мигание UI, повторные партиклы | `[OnChangedRender]` + отдельный View-компонент; `PhysicsBullet._visual` гасится в `Render()` |
| Подписка в `Spawned` без отписки | при пулинге тот же инстанс получит `Spawned` снова → двойной урон, двойная реакция вида | строго зеркальные `Spawned`/`Despawned`, для Mono-адаптеров `OnEnable`/`OnDisable` |
| Подписка на change-событие без начальной синхронизации | поздний клиент и объект из пула рисуют состояние префаба | `CharacterView.Spawned` явно зовёт `OnHealthChanged(_healthComponent.Health)` |
| Собственное `[Networked] PlayerRef _owner` на снаряде | лишние байты и второй источник истины | фильтр `other.InputAuthority == Object.InputAuthority` |
| Свой `Dictionary<PlayerRef, NetworkObject>` | может рассинхронизироваться | `SetPlayerObject`/`GetPlayerObject` |
| `List`/`IEnumerable` в детекте коллизий | аллокация каждый тик на каждом снаряде | `delegate void Processor(Collider[] buffer, int count)` + `Collider[8]` в `[SerializeField]` |
| Клиент ждёт хоста, чтобы увидеть свой выстрел | 50–150 мс визуального лага на каждое нажатие | предспавненный `[Networked] NetworkArray<NetworkObject>`, снятый с симуляции |
| Полагаться, что изменение `[Networked]` само включит объект | `activeSelf` — не сетевое состояние, прокси останется с выключенным снарядом | теневой `_localBuffer` + диффинг в `Render()` при `!HasStateAuthority` |
| Пулить вручную в игровом коде | каждый `Spawn`/`Despawn` обрастает обвязкой | override `InstantiatePrefab`/`DestroyPrefabInstance` + каталог-белый список |
| Реализовать `INetworkRunnerCallbacks` одним классом | один класс на десяток несвязанных колбэков | `NetworkEvents` + по одному `MonoBehaviour` на ответственность |
| `Random.Range` по массиву спавн-точек | повторы, два игрока в одной точке | bag-random в `SpawnPointService`, вызывается только на сервере |
| **Антипаттерны, которые этот код НЕ обходит** | | |
| `GetComponent` в горячем коде | два `GetComponent` на каждый коллайдер каждый тик | ⚠️ так и сделано в `DealDamage` обоих снарядов; при переносе кешируйте |
| `FindObjectOfType` в `Spawned` | поиск по сцене на каждый спавн, жёстко зашитый тип | ⚠️ так и сделано в `FireComponent`; инжектируйте спавнер из `Character` |
| Побочный эффект вне успешной ветки | кулдаун тратится на неудавшийся выстрел | ⚠️ `TickTimer` в `FireComponent.Fire` стоит вне `if (TrySpawn(...))` |
| Ключ пула по строке-имени | переименование объекта уводит его в `Destroy` | ⚠️ `FindPrefab(instance.name, ...)` + `#if UNITY_EDITOR`-переименование |

---

## Чеклист: как применить в своём проекте

1. **Включи `ProvideInput = true` до `StartGame`.** Проверь, что все `[SerializeField]` в точке входа действительно проставлены в сцене (особенно `ObjectProvider` — в учебном проекте он забыт).
2. **Положи инфраструктурное сетевое состояние на сценный NetworkObject**, а не на спавненный префаб: тогда State Authority над ним принадлежит хосту по построению.
3. **Раздели ввод на три уровня:** сборщик (`MonoBehaviour` на `NetworkEvents.OnInput`) → приёмник (единственный `GetInput`, `[Networked] previousButtons`, ранний `return`) → адаптеры (`MonoBehaviour`, `OnEnable`/`OnDisable`). Наружу из приёмника выпускай намерения и `Runner.DeltaTime`, не кнопки.
4. **Нумеруй enum кнопок с 1**, без `None` и без комбинированных значений: `NetworkButtons` трактует значение как индекс бита.
5. **Для каждого компонента задай вопрос «есть ли `[Networked]`-поле или нужен ли FUN/Render».** Нет — делай `MonoBehaviour`. После написания сверься со списком `NetworkedBehaviours` в YAML префаба: там должны быть только настоящие сетевые компоненты.
6. **Внешние условия механик передавай как `Func<bool>`** (метод как делегат, без лямбды), а склейку механик держи в одном сборщике на объект.
7. **Кулдауны, время жизни, любые задержки — `[Networked] TickTimer`.** `ExpiredOrNotRunning` для «ещё не запускался», `Expired` для «запущен и истёк». Никогда `Time.time`.
8. **Если «начало жизни» объекта не совпадает со `Spawned` (пул, буфер), заведи явный `Init()`** и зови его из того, кто выдал объект. Сброс состояния, одинаковый для любого рождения (здоровье), оставляй в `Spawned`.
9. **Коллизии считай `Runner.GetPhysicsScene().OverlapSphere(...)` в `FixedUpdateNetwork`** с предвыделенным `Collider[N]` и отдавай результат как `(buffer, count)`. Никаких Unity-колбэков физики в сетевой логике.
10. **Результат попадания пиши в `[Networked] bool`, а `Despawn` делай отдельно по этому флагу в FUN.** Помни: `Runner.Despawn` на не-StateAuthority — тихий no-op, флаг нужен именно чтобы решение дожило до хоста.
11. **Friendly fire решай сравнением `InputAuthority`**, не собственным сетевым полем «владелец». Фильтры выстраивай по возрастанию стоимости: тег → авторитет → обращение к компоненту.
12. **Вид обновляй только на фазе рендера** — через `[OnChangedRender]` или в `Render()`. Никогда из FUN. И сразу после подписки на change-событие синхронизируй вид текущим значением.
13. **Все подписки симметричны:** `Spawned`/`Despawned` для `NetworkBehaviour`, `OnEnable`/`OnDisable` для `MonoBehaviour`. При пулинге это не стиль, а защита от двойного срабатывания.
14. **Чтобы убрать задержку выстрела у клиента:** `[Networked, Capacity(N)] NetworkArray<NetworkObject>` + `[Networked] int` индекс; State Authority наполняет в `Spawned`, прячет объекты далеко (`0, -1000, 0`), гасит `SetActive(false)` и **обязательно** снимает с симуляции `SetIsSimulated(false)`; выдача — `AssignInputAuthority` + позиция + `SetActive(true)` + `SetIsSimulated(true)`; клиент пишет в ячейку `null` как предсказание, хост — свежий инстанс.
15. **В `Despawned` буфера обязательно разбери его:** State Authority despawn-ит каждый непустой элемент и зовёт `.Clear()`, остальные пиры чистят теневую копию. Иначе невидимые объекты утекут.
16. **Любое не-сетевое следствие сетевого состояния восстанавливай диффом в `Render()`** против теневой локальной копии, под `if (HasStateAuthority) return`. `activeSelf`, имена, материалы, звук — всё это не реплицируется.
17. **Фолбэк на `Runner.Spawn` разрешай только State Authority.** Клиент при пустом буфере должен вернуть `false` и не стрелять — расхождение хуже пропущенного выстрела. И перезаряжай кулдаун **внутри** успешной ветки.
18. **Пулинг включай через `NetworkObjectProviderDefault`**, с белым списком в `ScriptableObject` и предсозданными очередями в `Awake`. Всё, что не в списке, делегируй в `base`. Ключуй по `NetworkPrefabId` или компоненту-метке, а не по имени объекта.
19. **Для физических объектов поставь `RunnerSimulatePhysics3D`** с `_physicsAuthority: Fusion`, `_physicsTiming: FixedUpdateNetwork`, `ClientPhysicsSimulation: SimulateAlways`, выключи `m_AutoSyncTransforms`, а `_interpolationTarget` у `NetworkRigidbody3D` направь на дочерний визуал, не на корень. Логику привязывай к корню.
20. **Если объект перепарентивается в рантайме — включи `SyncParent: 1`.** `SyncScale` выключай, если масштаб не меняется.
21. **Включи `NetworkConditions` (150 мс + 5% потерь) на время разработки предсказания.** Без этого на localhost ты не увидишь ни проблему, ни эффект буфера.
22. **Не оставляй `GetComponent`/`FindObjectOfType` в горячем пути.** Кешируй цели попадания и инжектируй инфраструктуру в механики от сборщика — ровно так же, как инжектируется `Func<bool>`.
