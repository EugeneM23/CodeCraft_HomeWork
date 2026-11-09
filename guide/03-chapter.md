# Глава 03. Кинематика и физика объектов

> **TL;DR для агента**
> - Делай движение сетевого объекта **одним из двух способов** и никогда не смешивай: либо кинематический шаг `transform.position += dir * speed * Runner.DeltaTime` внутри `FixedUpdateNetwork()`, либо настоящий `Rigidbody`, которому **Fusion** (а не Unity) вызывает `Physics.Simulate()` внутри тика.
> - Физику отдают Fusion одним сценным компонентом `RunnerSimulatePhysics3D` на объекте раннера: `_physicsAuthority = Fusion`, `_physicsTiming = FixedUpdateNetwork`, `ClientPhysicsSimulation = SimulateAlways`. Без этого Rigidbody нельзя откатить и переиграть.
> - Каждый `Rigidbody`-объект, который должен предсказываться на клиенте, обязан в `Spawned()` позвать `Runner.SetIsSimulated(Object, true)` — иначе аддон принудительно сделает прокси-Rigidbody кинематическим.
> - Попадания считай **активным запросом** `Runner.GetPhysicsScene().OverlapSphere(...)` из `FixedUpdateNetwork()`, а не `OnCollisionEnter`/`OnTriggerEnter`: только запрос воспроизводится при ресимуляции и работает в физической сцене конкретного раннера.
> - Любой факт, накопленный в FUN (попал/не попал, предыдущие кнопки, таймер), держи в `[Networked]`. Обычное поле не откатывается — и ресимуляция либо потеряет событие, либо задублирует его.

---

## Какую проблему решает

Host/Client-топология Fusion устроена так: клиент не ждёт сервера, а **предсказывает** результат своего ввода локально, а когда приходит авторитетный снапшот сервера — откатывает состояние назад и **переигрывает** (resimulate) все тики от снапшота до текущего. Это значит, что `FixedUpdateNetwork()` одного и того же тика исполняется несколько раз, и каждый повтор обязан дать **ровно тот же результат**.

Отсюда два требования к любому движению:

1. **Детерминизм.** Шаг движения должен быть чистой функцией от сетевого состояния и `Runner.DeltaTime`. `Time.deltaTime`, `Time.fixedDeltaTime`, `UnityEngine.Random` и локальные поля внутри FUN ломают это требование.
2. **Откатываемость.** Всё, что движение «помнит», должно лежать в снапшоте, который Fusion умеет откатить.

Кинематическое движение удовлетворяет оба требования почти бесплатно: это арифметика, а позицию синхронизирует `NetworkTransform`. Unity-физика — не удовлетворяет **ни одному**, пока она крутится в своём `FixedUpdate`: PhysX ничего не знает про тики Fusion, не умеет откатывать внутреннее состояние солвера и шагает в своём темпе (`Fixed Timestep: 0.02` против тикрейта Fusion `30`). Результат без вмешательства — физический снаряд на клиенте разъезжается с сервером и дёргается.

Раздел решает эту проблему ровно одним структурным приёмом: **шаг физики становится частью тика**. Для доказательства в проекте сделаны два снаряда с одинаковым контрактом урона и разным механизмом движения: `Bullet (Kinematic)` и `Bullet (Physics)`.

Проект: Unity 2022.3 + Photon Fusion **2.0.5 Stable, build 920** (`Assets/Photon/Fusion/release_history.txt`), `"PeerMode": 0` (Single Peer), запуск `GameMode.AutoHostOrClient`. Все решения главы справедливы для **Host/Client**, а не для Shared.

---

## Модель мира: кто чем владеет

| Что | Где исполняется | Чем подтверждается |
|---|---|---|
| Сбор ввода (`NetworkEvents.OnInput`) | только на пире, где `Runner.ProvideInput = true` | `EntryPoint.cs`, `InputAccumulator.cs` |
| `FixedUpdateNetwork()` объекта | на StateAuthority **и** на пире с InputAuthority (на прокси — нет, если объект не включён в симуляцию) | комментарии `//State Authority & Input Authority` в коде пуль |
| Спавн снаряда | **только сервер**: `if (!this.Runner.IsServer) return;` | `FireComponent.Fire()` |
| Спавн/despawn персонажа | **только сервер**: `if (!runner.IsServer) return;` | `PlayerJoinController`, `PlayerLeftController` |
| `Physics.Simulate()` | сервер — всегда; клиент — если `ClientPhysicsSimulation == SimulateAlways` (и в forward-, и в resim-тиках) | `RunnerSimulatePhysicsBase.CanSimulatePhysics()` |
| Предсказание Rigidbody на клиенте | только если `Object.IsInSimulation == true` **и** `ClientPhysicsSimulation` ∈ {SimulateAlways, SimulateForward} | `NetworkRigidbody.Spawned()` |
| Урон (`HealthComponent.TakeDamage`) | там, где исполняется FUN пули, т.е. и на предсказывающем клиенте | `KinematicBullet/PhysicsBullet.ProcessCollision` |

Главный вывод модели: **InputAuthority-клиент тоже считает попадания и урон**. Его результат — предсказание, которое затрётся снапшотом сервера. Поэтому весь код попаданий обязан быть идемпотентным по тику, а не «однократным».

Вот как это видно глазами (`Assets/Game/Scripts/GameObject/Core/HealthComponent.cs`):

```csharp
Debug.Log($"TAKE DAMAGE {Object.Name} {Health} {Runner.Tick} {this.Runner.IsForward}");
```

Один и тот же `Runner.Tick` печатается несколько раз, и в повторах `IsForward == false`. Это рабочий диагностический приём: **любой побочный эффект (звук, VFX, инкремент счётчика), не защищённый проверкой `Runner.IsForward`, задублируется столько раз, сколько раз переиграли тик.**

---

## Архитектура раздела

```
 [Unity Input]                                  ┌──────────── только на пире с ProvideInput
      │                                         │
      ▼                                         │
 InputAccumulator (MonoBehaviour) ──NetworkEvents.OnInput──▶ NetworkInput.Set(InputData)
                                                             │
                                               ═══ граница сети / тик Fusion ═══
                                                             ▼
 ┌─────────────────────────── FixedUpdateNetwork тика N ──────────────────────────┐
 │                                                                                │
 │  RunnerSimulatePhysics3D.FUN  ──▶ Physics.Simulate(Runner.DeltaTime)           │
 │        (сценный SimulationBehaviour на объекте раннера, НЕ на пуле)            │
 │                                                                                │
 │  Character:                                                                    │
 │    InputReceiver.FUN ──GetInput(out InputData)──▶ OnMove / OnFire              │
 │        │                       │                                               │
 │        ▼                       ▼                                               │
 │    MoveController        FireController                                        │
 │        │                       │                                               │
 │        ▼                       ▼                                               │
 │    MovementComponent     FireComponent.Fire() ──IsServer──▶ Runner.Spawn(...)  │
 │    RotationComponent                             onBeforeSpawned: Init()       │
 │        │                                                                       │
 │        ▼                                                                       │
 │    transform ──▶ NetworkTransform (захват в снапшот)                           │
 │                                                                                │
 │    Character.FUN ──▶ _collider.enabled = _healthComponent.Exists()             │
 │                                                                                │
 │  Bullet (Kinematic):            │  Bullet (Physics):                           │
 │    LifetimeComponent.FUN        │    LifetimeComponent.FUN                     │
 │    CollisionComponent.FUN ──┐   │    CollisionComponent.FUN ──┐                │
 │    KinematicBullet.FUN      │   │    NetworkRigidbody3D       │                │
 │      MoveStep(forward)      │   │    PhysicsBullet.FUN        │                │
 │      if(_collided) Despawn ◀┘   │      if(_collided) Despawn ◀┘                │
 └────────────────────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼  Render / OnChangedRender
                     CharacterView, ColorComponent, InterpolationTarget <Visual>
```

Четыре слоя, и само разбиение — приём:

1. **Core** (`Assets/Game/Scripts/GameObject/Core`) — мелкие кирпичики без знания про игру: `MovementComponent`, `RotationComponent`, `CollisionComponent`, `LifetimeComponent`, `HealthComponent`, `FireComponent`.
2. **Content** (`.../Content`) — сборщики: `Character`, `CharacterView`, `KinematicBullet`, `PhysicsBullet`. Здесь живёт развилка кинематика/физика.
3. **Player** (`Assets/Game/Scripts/Player`) — мост «ввод → компоненты»: `InputReceiver` плюс обычные `MonoBehaviour`-подписчики `MoveController` и `FireController`.
4. **System** (`Assets/Game/Scripts/System`) — сцена и инфраструктура: `EntryPoint`, `InputAccumulator`, `PlayerJoinController`/`PlayerLeftController`, `SpawnPointService`, `WorldTransform`, пул объектов.

**Выбор базового класса — осознанный, а не случайный.** `MovementComponent` и `RotationComponent` — обычные `MonoBehaviour`, потому что у них **нет собственного сетевого состояния**: их результат (позиция/поворот) уже синхронизируется внешним `NetworkTransform`. `NetworkBehaviour` только там, где есть `[Networked]` или нужны `FixedUpdateNetwork`/`Spawned`: `HealthComponent`, `LifetimeComponent`, `FireComponent`, `InputReceiver`, `CollisionComponent`, пули, `ColorComponent`, `CharacterView`. Каждый лишний `NetworkBehaviour` — это вес в weaving'е, в снапшоте и в обходе симуляции.

Отдельная деталь, которая подтверждает выбор: `RotationComponent._speedRate: 2000` на `Character.prefab` (строка 139). 2000 град/с в `Quaternion.RotateTowards` при шаге 1/30 с — это 66 град за тик, то есть поворот в большинстве тиков «добивает» до цели и практически не зависит от истории. Поэтому его безопасно держать в несетевом `MonoBehaviour` без собственного `[Networked]`-состояния.

---

## Реализация по шагам

### Шаг 1. Отдать `Physics.Simulate()` Fusion'у

`Assets/Game/Scenes/Game.unity`, GameObject `[System]` (компонент `RunnerSimulatePhysics3D`, guid `7b5a1ff9dee264bfd829a39e6542dcbd`):

```yaml
  m_Script: {fileID: 11500000, guid: 7b5a1ff9dee264bfd829a39e6542dcbd, type: 3}
  _physicsAuthority: 2        # PhysicsAuthorities.Fusion
  _physicsTiming: 3           # PhysicsTimings.FixedUpdateNetwork
  ClientPhysicsSimulation: 3  # SimulateAlways
  DeltaTimeMultiplier: 1
  SetUnityFixedTimestep: 0
```

**Почему именно так.** Это «physics simulation mode» урока, и он задаётся **не** в `NetworkProjectConfig`, а компонентом аддона на объекте раннера. Расшифровка по `RunnerSimulatePhysicsEnums.cs`: `PhysicsAuthorities` = Auto(0)/Unity(1)/Fusion(2); `PhysicsTimings` = FixedUpdate(0)/Update(1)/Script(2)/FixedUpdateNetwork(3); `ClientPhysicsSimulation` = Disabled(0)/SyncTransforms(1)/SimulateForward(2)/SimulateAlways(3).

Механика подтверждается кодом аддона. `Startup()` зовёт `OverrideAutoSimulate(_physicsAuthority == PhysicsAuthorities.Unity)`, т.е. с `false`, а `RunnerSimulatePhysics3D.OverrideAutoSimulate(false)` ставит `UnityEngine.Physics.simulationMode = SimulationMode.Script` — Unity перестаёт симулировать сама. Дальше:

```csharp
// Assets/Photon/FusionAddons/Physics/RunnerSimulatePhysics/RunnerSimulatePhysicsBase.cs
    public override void FixedUpdateNetwork() {
      // ...
      // If selected timing is not FixedUpdateNetwork, this FixedUpdateNetwork callback should be ignored
      if (_physicsTiming != PhysicsTimings.FixedUpdateNetwork) { return; }

      // if Unity is currently auto-simulating - Fusion should not.
      if (UnityPhysicsMode != PhysicsTimings.Script) { return; }

      var  deltaTime = PhysicsSimulationDeltaTime * DeltaTimeMultiplier;

      SimulationExecute(deltaTime, true);
    }

    protected bool CanSimulatePhysics(ClientPhysicsSimulation clientPhysicsSimulation) {
      if (Runner.IsServer) {
        return true;
      }

      return clientPhysicsSimulation == ClientPhysicsSimulation.SimulateAlways || (clientPhysicsSimulation == ClientPhysicsSimulation.SimulateForward && Runner.IsForward);
    }
```

`PhysicsSimulationDeltaTime => Runner.DeltaTime`. То есть шаг физики равен шагу тика, и при `SimulateAlways` клиент гоняет `Simulate` **и в forward-, и в resim-тиках** — физика предсказывается и переигрывается вместе с остальным состоянием. Если бы стояло `SimulateForward`, в resim-тиках вместо `Simulate` вызывался бы `Physics.SyncTransforms()`, и переигранная траектория не совпала бы с исходной.

Важно, чего аддон **не** сделает за вас. Фикс конфигурации в `Startup()` (переключение на `SimulateAlways`) срабатывает **только** при `Runner.GameMode == GameMode.Shared` — здесь режим `AutoHostOrClient`, значит ветка не исполняется никогда. А предупреждение «Unity cannot auto-simulate FixedUpdateNetwork()» живёт в `OnValidate()` под `#if UNITY_EDITOR` — это валидация инспектора, а не рантайм-защита. **Ставьте значения руками и проверяйте их глазами.**

Зачем вообще явный компонент в сцене: если его не положить, `NetworkRigidbody.EnsureHasRunnerSimulatePhysics()` добавит его сам «с дефолтными настройками» (и залогирует предупреждение) — а дефолт `ClientPhysicsSimulation = Disabled` убьёт клиентское предсказание физики.

`SetUnityFixedTimestep: 0` оставляет `Time.fixedDeltaTime = 0.02` (`ProjectSettings/TimeManager.asset`) при тикрейте Fusion 30 Hz. Расхождение допустимо **именно потому, что физика больше не живёт в `FixedUpdate`**.

### Шаг 2. Кинематический снаряд

`Assets/Game/Scripts/GameObject/Content/KinematicBullet.cs`:

```csharp
    public sealed class KinematicBullet : NetworkBehaviour
    {
        [SerializeField]
        private MovementComponent _movementComponent;

        [SerializeField]
        private CollisionComponent _collisionComponent;

        [SerializeField]
        private int _damage;

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
    }
```

**Почему именно так.** Движение — `_movementComponent.MoveStep(transform.forward, Runner.DeltaTime)`, где `MoveStep` это `transform.position += direction * (_speed * deltaTime)`. Чистая арифметика от шага тика: при ресимуляции Fusion откатывает `NetworkTransform` и `[Networked]`-поля, и те же N тиков дают ту же траекторию бит-в-бит. Подставьте сюда `Time.deltaTime` — и на повторном тике шаг будет другим, потому что `Time.deltaTime` это длительность кадра, а не тика.

Факт попадания лежит в `[Networked] bool _collided`, а не в обычном `bool`. Обычное поле не откатывается: после откатa состояния снаряд «уже попал», и либо урон не нанесётся повторно (сервер его не подтвердит), либо нанесётся дважды. `[Networked]` делает флаг частью снапшота.

`if (_collided) return;` первой строкой `ProcessCollision` делает обработку **идемпотентной**: повторный проход того же тика не наносит урон снова.

Подписка в `Spawned()` и отписка в `Despawned()` обязательны: объект пулится (см. шаг 8) и переживает несколько жизней. Без отписки второй экземпляр жизни получит два обработчика.

**Внимание на порядок внутри тика.** В `NetworkObject.NetworkedBehaviours` префаба (`Bullet (Kinematic).prefab:156-161`) `CollisionComponent` стоит **раньше** `KinematicBullet`. Вывод (о порядке исполнения Fusion, не факт из кода): `ProcessCollision` выставляет `_collided`, и FUN пули видит его **в том же тике**. При этом `MoveStep` стоит **до** проверки, значит пуля делает ещё один шаг после зафиксированного попадания. Это не баг — но если вам нужен despawn строго в тик попадания и без лишнего шага, проверку надо поднять выше `MoveStep`.

Префаб `Bullet (Kinematic).prefab`: `NetworkObject` + `NetworkTransform` (`SyncScale: 0`, `SyncParent: 0`, `_autoAOIOverride: 1`) + `MovementComponent (_speed: 5)` + `LifetimeComponent (_lifetime: 1)` + `CollisionComponent` + `KinematicBullet (_damage: 1)` + дочерний `<Visual>` с `ColorComponent`. **Ни `Rigidbody`, ни коллайдера на нём нет вообще.** Отсюда прямое следствие: `OnCollisionEnter`/`OnTriggerEnter` ему физически не придут, и единственный путь к попаданиям — активный запрос.

### Шаг 3. Активный запрос коллизий вместо колбэков Unity

`Assets/Game/Scripts/GameObject/Core/CollisionComponent.cs`:

```csharp
    public sealed class CollisionComponent : NetworkBehaviour
    {
        public delegate void Processor(Collider[] buffer, int count);

        public event Processor ProcessCollision;

        [SerializeField]
        private Transform _center;

        [SerializeField]
        private float _radius = 1;

        [SerializeField]
        private LayerMask _layerMask;

        [SerializeField]
        private QueryTriggerInteraction _triggerInteraction;

        [SerializeField]
        private Collider[] _buffer = new Collider[8];

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

**Почему именно так.** Четыре причины отказаться от `OnCollisionEnter`:

1. Колбэк приходит из цикла Unity-физики и **не воспроизводится при ресимуляции** — переигранный тик его не получит.
2. Он вообще не придёт объекту без `Rigidbody`/`Collider` — то есть кинематической пуле.
3. Статический `Physics.OverlapSphere` читает `Physics.defaultPhysicsScene`. `Runner.GetPhysicsScene()` отдаёт физическую сцену **этого** раннера — при multi-peer у каждого раннера своя, и статический вызов брал бы чужую.
4. Запрос из FUN — чистая функция от текущего состояния: одинаковая на всех пирах и в любом повторе тика.

`OverlapSphere` взят в **non-alloc** перегрузке с заранее выделенным `Collider[8]`: в каждом тике, включая десятки resim-тиков, не должно быть аллокаций. Делегат отдаёт `(buffer, count)`, а не копию массива — по той же причине.

`OnDrawGizmos` здесь не украшение: у кинематической пули нет ни коллайдера, ни хитбокса, и `DrawWireSphere` — **единственный** способ увидеть её зону попадания в редакторе.

⚠️ **Ловушка сериализованного буфера.** `= new Collider[8]` — это лишь дефолт для инспектора. Реальный размер берётся из префаба; в `Bullet (Kinematic).prefab:224-232` и `Bullet (Physics).prefab:196-204` видно ровно 8 записей `- {fileID: 0}`. Если в префабе окажется **0** элементов, non-alloc `OverlapSphere` вернёт 0, и попаданий не будет никогда при полностью «правильном» коде. Проверяйте размер буфера в префабе, а не в исходнике.

Для сравнения — учительская версия `Assets/[Teacher]/CollisionComponent.cs` (закомментирована) содержала дополнительный гейт `if (!this.Runner.IsServer) return;` в FUN. В финальной версии его нет: клиент тоже должен считать попадания, иначе предсказания урона не будет вовсе.

### Шаг 4. Физический снаряд

`Assets/Game/Scripts/GameObject/Content/PhysicsBullet.cs`:

```csharp
    public sealed class PhysicsBullet : NetworkBehaviour
    {
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private float _speed = 8;
        
        [SerializeField]
        private CollisionComponent _collisionComponent;

        [SerializeField]
        private int _damage;

        [Networked]
        private bool _collided { get; set; }

        public void Init()
        {
            _rigidbody.velocity = this.transform.forward * _speed;
        }
        
        public override void Spawned()
        {
            _collisionComponent.ProcessCollision += this.ProcessCollision;
            this.Runner.SetIsSimulated(this.Object, true);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _collisionComponent.ProcessCollision -= ProcessCollision;
        }

        //State Authority & Input Authority
        public override void FixedUpdateNetwork()
        {
            if (_collided)
                this.Runner.Despawn(this.Object);
        }

        // ... ProcessCollision и DealDamage дословно совпадают с KinematicBullet
    }
```

**Почему именно так.** FUN пустой, кроме despawn'а: движение целиком на `Rigidbody`. Смысл главы несут две строки.

`Init()` выставляет `velocity` **один раз** и вызывается из `onBeforeSpawned` (шаг 5) — до первого синхронизируемого снапшота.

`Runner.SetIsSimulated(this.Object, true)` включает объект в локальную симуляцию клиентов. Вот почему без неё не работает — вендорный код `Assets/Photon/FusionAddons/Physics/NetworkRigidbody/NetworkRigidbodyBase/NetworkRigidbody.cs` (`Spawned()` начинается на строке 50, `AfterSpawned()` — на 75):

```csharp
    public override void Spawned() {
      base.Spawned();

      // Force Proxies to be kinematic.
      // This can be removed if you specifically instruct proxies to simulate with Runner.SetIsSimulated()
      // and want proxies to predict (not applicable to Shared Mode).
      if (IsProxy) {
        SetRBIsKinematic(_rigidbody, true);
      }

      EnsureHasRunnerSimulatePhysics();
      _clientPrediction = Runner.Topology != Topologies.Shared && (Runner.IsServer || _physicsSimulator.ClientPhysicsSimulation == ClientPhysicsSimulation.SimulateAlways || _physicsSimulator.ClientPhysicsSimulation == ClientPhysicsSimulation.SimulateForward);

      if (HasStateAuthority) {
        CopyToBuffer(false);
      } else {
        // Mark the root as dirty to force CopyToEngine to update the transform.
        _rootIsDirtyFromInterpolation = true;
        CopyToEngine(true);
        // This has to be here after CopyToEngine, or it will set Kinematic right back.
        if (Object.IsInSimulation == false) {
          SetRBIsKinematic(_rigidbody, true);
        }
      }
    }
```

Прокси по умолчанию принудительно кинематические; динамическим Rigidbody остаётся только если `Object.IsInSimulation == true`. То есть `ClientPhysicsSimulation = SimulateAlways` и `SetIsSimulated(true)` — **согласованная пара**, два конца одной настройки. Рассогласование ловит `AfterSpawned()` и пишет варнинг с готовым рецептом в обе стороны («Add the NetworkObject on the simulation calling `Runner.SetIsSimulated(Object, true);` in Spawned()»). Без `SetIsSimulated` пуля на клиенте замрёт или задёргается.

Префаб `Bullet (Physics).prefab`: `SphereCollider` (`m_IsTrigger: 0`, `m_Radius: 0.125`), `Rigidbody` (`m_Mass: 1`, `m_Drag: 0`, `m_UseGravity: 1`, `m_IsKinematic: 0`, `m_Interpolate: 0`, `m_CollisionDetection: 0`), `NetworkRigidbody3D`, `LifetimeComponent (_lifetime: 5)`, `CollisionComponent`, `PhysicsBullet (_speed: 8)`, `ParentTransformConstraint (_parentName: '[World]')`. `NetworkTransform` на нём **отсутствует** — и не нужен: `NetworkRigidbodyBase : NetworkTRSP, INetworkTRSPTeleport`, то есть сам является носителем TRSP-состояния.

```yaml
# Assets/Game/Prefabs/Bullet (Physics).prefab — NetworkRigidbody3D
  SyncScale: 0
  SyncParent: 1
  _interpolationTarget: {fileID: 7002937625849939667}   # дочерний <Visual>
  UsePreciseRotation: 0
  UseRenderSleepThresholds: 1
```

`m_Interpolate: 0` на `Rigidbody` — сглаживание делает **не** Unity, а `NetworkRigidbody3D` через `_interpolationTarget`, и цель — дочерний `<Visual>`, а не корень. Это принципиально: корень носит `SphereCollider`, `Rigidbody` и служит `_center` для `OverlapSphere`, и обязан оставаться в «тиковой» позиции. Интерполируй корень — и физические запросы начнут читать сглаженную, то есть неверную позицию.

**Разница между снарядами шире, чем «способ движения».** У физической пули нетриггерный коллайдер, масса и гравитация: она реально летит по параболе и **отбивается** от пола и кубов. У кинематической нет коллайдера вообще — она не существует для физики и для чужих запросов. Побочно: собственный коллайдер физической пули попадает в её же `_buffer`, и отфильтровывается он только по тегу внутри `DealDamage`; окружение (Plane, Cube'ы) тоже лежит на слое Default при маске `_layerMask.m_Bits: 1`, то есть делит с целями те же 8 слотов буфера.

### Шаг 5. Инициализация сетевого состояния строго в `onBeforeSpawned`

`Assets/Game/Scripts/GameObject/Core/FireComponent.cs`:

```csharp
        [SerializeField]
        private NetworkPrefabRef _bulletPrefab;

        [SerializeField]
        private float _cooldown = 1;
        
        [Networked]
        private TickTimer timer { get; set; }

        public void Fire()
        {
            if (!this.Runner.IsServer)
                return;

            if (!this.timer.ExpiredOrNotRunning(this.Runner)) 
                return;
            
            PlayerRef player = this.Object.InputAuthority;
            Vector3 position = _firePoint.position;
            Quaternion rotation = _firePoint.rotation;
            this.Runner.Spawn(_bulletPrefab, position, rotation, player, onBeforeSpawned: (_, o) =>
            {
                o.GetComponent<LifetimeComponent>().Init();
                o.GetComponent<PhysicsBullet>().Init();
            });

            this.timer = TickTimer.CreateFromSeconds(this.Runner, _cooldown);
        }
```

**Почему именно так.** Порядок гейтов не случаен. Сначала `IsServer`: спавн — прерогатива сервера, клиент не должен тратить на него тик. Затем `ExpiredOrNotRunning`, а **не** `Expired`: `default`-таймер не считается «истёкшим», и с `Expired()` первый выстрел был бы запрещён навсегда. Контраст — `LifetimeComponent`, где намеренно `Expired()`, чтобы неинициализированный таймер не удалил объект в первый же тик. Оба поля `[Networked]`, иначе таймер «скакал» бы при ресимуляции.

`onBeforeSpawned` — **единственное корректное место** для записи `[Networked]`-полей и физического `velocity`: только там они попадут в первый же синхронизируемый снапшот. Сделай это после `Runner.Spawn` — и клиенты минимум один тик увидят объект с нулевым таймером и нулевой скоростью.

`InputAuthority` стрелка передаётся снаряду при спавне — на этом строится фильтр «не бей своего» в `DealDamage`.

⚠️ **Цена этой простоты.** Колбэк жёстко зовёт `GetComponent<PhysicsBullet>()`. Переключение кинематика/физика в уроке делается подменой `_bulletPrefab` в инспекторе (`Character.prefab`: `RawGuidValue: 7e564fc72506c4313a09a62c3ef49ede` = `Bullet (Physics)`), код не меняется — но на кинематическом префабе этого компонента нет, и будет `NullReferenceException`. В своём проекте пишите `if (o.TryGetComponent(out PhysicsBullet pb)) pb.Init();` или заведите общий интерфейс инициализации.

### Шаг 6. Ввод: один шлюз и `[Networked] previousButtons`

`Assets/Game/Scripts/Player/InputReceiver.cs`:

```csharp
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

        private void ProcessFire(InputData input)
        {
            if (input.buttons.WasPressed(this.previousButtons, InputKeys.Fire))
                this.OnFire?.Invoke();
        }
```

**Почему именно так.** `GetInput` имеет смысл только в FUN и только там, где есть InputAuthority. Один шлюз на объект — и остальные компоненты (`MoveController`, `FireController`) получают готовые события и остаются обычными `MonoBehaviour`. Разбросанный по компонентам `GetInput` дал бы разное поведение на разных пирах.

`previousButtons` помечен `[Networked]` — это ключевая деталь. Локальная переменная при ресимуляции уже содержала бы «будущее» значение, и фронт кнопки (`WasPressed`) либо потерялся бы, либо сработал дважды: выстрел пропал бы или задублировался.

Сбор ввода, `Assets/Game/Scripts/System/Input/InputAccumulator.cs`:

```csharp
        private void AccumulateFire(ref NetworkButtons buttons)
        {
            buttons.Set(InputKeys.Fire, Input.GetKey(KeyCode.Space));
        }
```

`Input.GetKey`, а **не** `GetKeyDown` — и это осознанно: фронт детектится позже, уже в тике, через `WasPressed(previousButtons, ...)`. С `GetKeyDown` нажатие, пришедшееся на кадр без колбэка `OnInput`, потерялось бы целиком. (Побочно: класс назван `Accumulator`, но ничего не аккумулирует между кадрами — `new NetworkButtons()` на каждый вызов; честнее было бы `InputProvider`.)

Теперь главная ловушка Fusion в этом разделе — `Assets/Game/Scripts/System/Input/InputKeys.cs`:

```csharp
    public enum InputKeys
    {
        None = 0,
        Fire = 1,
        Reload = 2,
        Jump = 3,
        Interact = 4
    }
```

Без `[Flags]`, со **последовательными** значениями. Учительская версия (`Assets/[Teacher]/InputKeys.cs`, закомментирована) — наоборот, с `[Flags]` и степенями двойки (`Fire = 1, Jump = 2, Reload = 4`). Вывод (о поведении Fusion, не факт из кода): `NetworkButtons.Set/WasPressed` трактует значение enum как **индекс бита**, а не как битовую маску. Поэтому последовательная нумерация правильна, а `[Flags]`-вариант тратит биты впустую и на 5-й кнопке начнёт вести себя неожиданно. **Нумеруйте кнопки подряд и не ставьте `[Flags]`.**

### Шаг 7. Состояние против представления

`Assets/Game/Scripts/GameObject/Core/HealthComponent.cs`:

```csharp
        //View
        public event Action<int> OnHealthChanged;

        [Networked]
        [OnChangedRender(nameof(InvokeHealthChanged))]
        [UnityNonSerialized]
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
            // Debug.Log($"DAMAGE TAKEN {Object.Name} {Health} {Runner.Tick} {this.Runner.IsForward}");
            return true;
        }
```

**Почему именно так.** `[OnChangedRender]` даёт реакцию вьюшки на изменение сетевого поля без polling'а в `Render()`. `[UnityNonSerialized]` запрещает Unity сохранять backing-поле networked-свойства в префаб — это проверяемо: в снапшоте раздела 02 в `Character.prefab` лежала строка `_Health: 0`, здесь её нет. Сериализованное в префаб networked-поле — мусор, который путает инспектор, дифф и читателя.

`TakeDamage` возвращает `bool`, а не `void`, и именно по этому `bool` снаряд решает, считать ли попадание состоявшимся. Так урон и уничтожение снаряда не расходятся.

⚠️ `Spawned()` пишет `Health = MaxHealth;` **без проверки авторитета**. На прокси это локальная запись в `[Networked]`-свойство, которую затрёт первый же снапшот. Корректнее — гейт `if (HasStateAuthority)` либо инициализация в `onBeforeSpawned`, как уже сделано для `LifetimeComponent.Init()`.

`Assets/Game/Scripts/GameObject/Content/CharacterView.cs`:

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

Явный вызов `this.OnHealthChanged(_healthComponent.Health)` сразу после подписки **обязателен**: `[OnChangedRender]` стреляет только на **изменении**, а не на начальном значении. Прокси, вошедший в AOI уже мёртвого персонажа, иначе увидел бы `_visual` включённым до первого изменения `Health`.

`Assets/Game/Scripts/GameObject/Content/Character.cs` (отступы в файле действительно сломаны — артефакт правки прямо на уроке):

```csharp
        public override void Spawned()
        {
            _movementComponent.SetCondition(_healthComponent.Exists);
            _rotationComponent.SetCondition(_healthComponent.Exists);
        }
        
                public override void FixedUpdateNetwork()
                {
               _collider.enabled = _healthComponent.Exists();
                }
```

**Почему именно так.** `SetCondition(Func<bool>)` — точка, где «правило игры» подключается к «кирпичику»: `MovementComponent` не знает про здоровье, а `Character` не переписывает движение. Если условие не задано (как у пули), `CanMove()` возвращает `true`. Альтернатива из `Assets/[Teacher]/Content/Character.cs` — интерфейс `ICondition`, который `Character` реализует сам; делегат убирает лишний интерфейс.

Гашение коллайдера стоит **в FUN**, а не в `Render()` и не в колбэке смерти — потому что это **часть игрового состояния**: оно детерминированно, переигрывается вместе с тиком и одинаково на всех пирах. Погаси коллайдер в `Render()` — и `OverlapSphere` пули продолжит попадать в труп, а на ресимуляции состояние коллайдера разойдётся между пирами. Это же объясняет, зачем `Character` держит `[SerializeField] Collider _collider`.

### Шаг 8. Пул сетевых объектов и локальный парентинг

`Assets/Game/Scripts/System/ObjectProvider/PoolableNetworkObjectProvider.cs`:

```csharp
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
```

**Почему именно так.** Это правильная точка расширения: подменяется только физическое создание/уничтожение `GameObject`, а владение жизненным циклом `NetworkObject` (Id, снапшоты, `Spawned`/`Despawned`) остаётся у Fusion. `instance.name = prefab.name` неочевидна и обязательна: без неё Unity допишет `" (Clone)"`, `FindPrefab(instance.name, ...)` при возврате не найдёт очередь, объект уйдёт в `base.DestroyPrefabInstance`, и пул начнёт течь. Белый список — ScriptableObject-каталог (`PoolableNetworkObjectCatalog.asset`: `Bullet (Physics)` и `Character`) с fallback на `base`, чтобы непулящиеся префабы работали как раньше. Именно из-за пулинга все подписки в снарядах обязаны сниматься в `Despawned`.

Парентинг — `ParentTransformConstraint`: `GameObject.Find(_parentName)` в `OnEnable` и локальная перепривязка под `[World]`. Он стоит на `Character.prefab` (строка 232) и `Bullet (Physics).prefab` (строка 304) — на обоих `SyncParent: 1`. На `Bullet (Kinematic).prefab` его **нет** вообще (полный список компонентов корня: Transform, NetworkObject, NetworkTransform, MovementComponent, LifetimeComponent, CollisionComponent, KinematicBullet), и там `SyncParent: 0`. Учительский вариант `Assets/[Teacher]/Utils/WorldTransformChild.cs` — единственный незакомментированный файл в `[Teacher]` — делает то же через `FindObjectOfType<WorldTransform>()`, т.е. через маркер-компонент, а не строковое имя: устойчивее к переименованию. **Предпочитайте маркер строке.**

---

## Fusion API этой главы

| API | Что делает | Где вызывается | Подводный камень |
|---|---|---|---|
| `RunnerSimulatePhysics3D` (сценный компонент) | Отбирает `Physics.Simulate` у Unity и зовёт его внутри тика | `Game.unity`, GO `[System]` рядом с `NetworkRunner` | Рантайм-защит нет: `OnValidate` — только редактор, а фикс в `Startup()` работает лишь при `GameMode.Shared`. Не положите компонент — `NetworkRigidbody` добавит свой с `ClientPhysicsSimulation = Disabled` |
| `Runner.SetIsSimulated(Object, bool)` | Включает объект в локальную симуляцию прокси | `PhysicsBullet.Spawned()` | Без неё прокси-Rigidbody форсится в `isKinematic = true`. Должна быть согласована с `ClientPhysicsSimulation`; рассогласование — варнинг в `AfterSpawned()`. В снапшоте 02 этот вызов стоял и на `Character` — в 03 убран как ненужный |
| `Runner.GetPhysicsScene()` → `PhysicsScene.OverlapSphere(center, radius, buffer, mask, triggerInteraction)` | Запрос пересечений в физической сцене **этого** раннера | `CollisionComponent.FixedUpdateNetwork()` | Член `NetworkRunner` (в `Fusion.Runtime.dll`), не extension-метод. Статический `Physics.OverlapSphere` при multi-peer взял бы дефолтную сцену. Берите non-alloc перегрузку |
| `FixedUpdateNetwork()` | Единственное место, где меняется игровое состояние | пули, `CollisionComponent`, `LifetimeComponent`, `InputReceiver`, `Character`, аддон физики | Вызывается многократно для одного тика. Всё накапливаемое — либо `[Networked]`, либо чистая функция от состояния |
| `Runner.DeltaTime` | Шаг интегрирования движения и физики | `MovementComponent.MoveStep`, `InputReceiver`, `PhysicsSimulationDeltaTime` | `Time.deltaTime`/`Time.fixedDeltaTime` в FUN ломают детерминизм: на ресимуляции они не равны шагу тика |
| `Runner.IsForward` | «Этот тик считается впервые» | диагностический `Debug.Log` в `HealthComponent` | Побочный эффект (звук, VFX, счётчик) без этой проверки задублируется столько раз, сколько было повторов тика |
| `[Networked] bool` / `TickTimer` / `NetworkButtons` | Состояние в снапшоте, которое Fusion откатывает | `_collided`, `timer`, `previousButtons` | Обычное поле не откатывается → потерянное или задвоенное событие |
| `TickTimer.CreateFromSeconds` / `.Expired` / `.ExpiredOrNotRunning` | Время в тиках, не в секундах реального времени | `FireComponent`, `LifetimeComponent` | `Expired()` у `default`-таймера = false (нужно `LifetimeComponent`), `ExpiredOrNotRunning()` = true (нужно `FireComponent`). Перепутаете — либо вечный снаряд, либо запрещённый первый выстрел |
| `Runner.Spawn(prefabRef, pos, rot, inputAuthority, onBeforeSpawned:)` | Серверный спавн с передачей InputAuthority и инициализацией до `Spawned()` | `FireComponent.Fire()` | `onBeforeSpawned` — единственное место для `[Networked]` и `velocity`. `GetComponent<T>()` там падает, если префаб подменили |
| `NetworkPrefabRef` | Сериализуемая ссылка на сетевой префаб | `FireComponent._bulletPrefab`, `PlayerJoinController._characterPrefab` | Переключение кинематика/физика делается подменой этого поля в инспекторе, но `onBeforeSpawned` зашит под `PhysicsBullet` |
| `GetInput(out InputData)` + `NetworkButtons.WasPressed(previous, key)` | Чтение ввода тика и детекция фронта кнопки | `InputReceiver.FixedUpdateNetwork()` | `previous` обязан быть `[Networked]`. Значения enum кнопок — индексы битов, подряд, без `[Flags]` |
| `[OnChangedRender(nameof(...))]`, `[UnityNonSerialized]` | Реакция вьюшки на изменение; запрет сериализации backing-поля в префаб | `HealthComponent.Health` | Стреляет только на **изменении** — начальное значение надо применить руками (`CharacterView.Spawned`) |
| `NetworkRigidbody3D` (`NetworkRigidbodyBase : NetworkTRSP`) | Синхронизирует Rigidbody как часть снапшота + сглаживает рендер | `Bullet (Physics).prefab` | `NetworkTransform` рядом не нужен и не ставится. `InterpolationTarget` — на дочерний визуал, иначе физические запросы читают сглаженную позицию |
| `NetworkTransform` | Синхронизирует кинематическое движение | `Character.prefab`, `Bullet (Kinematic).prefab` | Объект без `Rigidbody`/`Collider` не получает физических колбэков вообще |
| `NetworkObjectProviderDefault.InstantiatePrefab` / `DestroyPrefabInstance` | Пул: Fusion владеет `NetworkObject`, вы — созданием `GameObject` | `PoolableNetworkObjectProvider` | Опознание по `instance.name` — обязательно перезаписывать его, иначе `" (Clone)"` и утечка пула |
| `NetworkEvents` (`OnInput`, `PlayerJoined`, `PlayerLeft`) + `SetPlayerObject`/`GetPlayerObject` | Подписка обычных `MonoBehaviour` на события раннера | `InputAccumulator`, `PlayerJoinController`, `PlayerLeftController` | `FindObjectOfType<NetworkEvents>()` в `Awake` **зависит** от того, что `NetworkEvents` уже лежит в сцене (здесь — на `[System]`). Создадите раннер в рантайме — NRE в `OnEnable` |

---

## Настройки, без которых не работает

**`Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion`**
- `"PeerMode": 0` — Single Peer.
- `Simulation.TickRateSelection`: `Client: 30`, `ServerIndex: 0`, `ClientSendIndex: 1`, `ServerSendIndex: 1`. Тикрейт 30 Hz задаёт `Runner.DeltaTime`, а значит и шаг `Physics.Simulate`.
- `Simulation.PlayerCount: 10`, `InputDataWordCount: 0` (размер `InputData` считает ткач), `ReplicationFeatures: 1`, `InputTransferMode: 0`, `SimulationUpdateTimeMode: 0`.
- `LagCompensation.Enabled: false` (+ `HitboxBufferLengthInMs: 200`, `HitboxDefaultCapacity: 512`). `Hitbox`/`HitboxRoot` в учебном коде не используются вовсе — попадания идут через обычную физику и `OverlapSphere`. Лаг-компенсация — тема главы 06.
- `AssembliesToWeave`: `Fusion.Unity`, `Assembly-CSharp`, `Assembly-CSharp-firstpass`, **`Fusion.Addons.Physics`**, `Fusion.Addons.FSM`. Выпадет `Fusion.Addons.Physics` — `[Networked]` внутри `NetworkRigidbody` не соткётся, и синхронизация Rigidbody отвалится.
- `NetworkConditions.Enabled: false`, но заготовлено: `DelayMin/Max 0.15`, `AdditionalJitter 0.05`, `LossChanceMin/Max 0.05`. Включайте, чтобы **увидеть** ресимуляцию.
- `NullChecksForNetworkedProperties: true`, `InvokeRenderInBatchMode: true`, `HostMigration.EnableAutoUpdate: false`, `EncryptionConfig.EnableEncryption: false`.

**`ProjectSettings/`**
- `DynamicsManager.asset`: `m_Gravity: (0, -9.81, 0)` — физическая пуля летит по параболе, это и отличает её от кинематической визуально. `m_SimulationMode: 0` (состояние редактора; в рантайме аддон переводит его в `Script`). `m_AutoSyncTransforms: 0`, `m_DefaultSolverIterations: 6`, матрица слоёв — все со всеми.
- `TimeManager.asset`: `Fixed Timestep: 0.02` при тикрейте Fusion 30 Hz — расхождение осознанно, потому что физика больше не в `FixedUpdate`.

**Порядок компонентов в `NetworkObject.NetworkedBehaviours`** — это не косметика, это порядок исполнения FUN:
- `Bullet (Kinematic).prefab:156-161`: NetworkTransform → LifetimeComponent → CollisionComponent → KinematicBullet → ColorComponent.
- `Bullet (Physics).prefab:158-163`: LifetimeComponent → CollisionComponent → NetworkRigidbody3D → PhysicsBullet → ColorComponent.
- `Character.prefab:64-71`: NetworkTransform → InputReceiver → HealthComponent → Character → FireComponent → ColorComponent → CharacterView.

**Сцена `Game.unity`**: на `[System]` — `EntryPoint`, `NetworkRunner`, `NetworkEvents` (все 19 UnityEvent'ов с пустыми `m_Calls`, подписка идёт из кода), `NetworkSceneManagerDefault` (`IsSceneTakeOverEnabled: 1`), `RunnerSimulatePhysics3D`, `PoolableNetworkObjectProvider`; `InputAccumulator` на `Input`; `PlayerJoinController`/`PlayerLeftController` на `Players`; `WorldTransform` на `[World]`; `SpawnPointService` с 4 точками на `[SpawnPoints]`; контейнер `Pool`.

⚠️ **В этом снапшоте сцена и префабы сломаны, и это надо знать.**
- `EntryPoint._objectProvider: {fileID: 0}` (`Game.unity:928`) — не назначен, значит `StartGameArgs.ObjectProvider` уходит `null` и пул не используется. При этом сам `PoolableNetworkObjectProvider` висит на `[System]` с назначенными `_catalog` и `_container`: его `Awake()` построит словарь пула, который никто не спросит. Классический «работающий, но неподключённый» компонент.
- Слот `CollisionComponent` на обеих пулях указывает на guid `b1e46e3111fd42ca8a844b88a454b40b` = `Assets/[Teacher]/CollisionComponent.cs`, который **закомментирован целиком**. Это missing script: в рантайме `_collisionComponent == null`, и `Spawned()` обеих пуль упадёт на `_collisionComponent.ProcessCollision += ...`.
- Слот `Character` на `Character.prefab` указывает на guid `3d5ed43715a74977977a864e39acdd9a` = `Assets/[Archive1]/GameObjects/Character/Character.cs`, тоже закомментирован. Следствие: `SetCondition` не вызывается вовсе, `MovementComponent.CanMove()` всегда `true` (мёртвый персонаж продолжит ездить), коллайдер не гасится.
- Все три missing-компонента **остались** в списках `NetworkedBehaviours` (`Character.prefab:64-71`, `Bullet (Kinematic).prefab:156-161`, `Bullet (Physics).prefab:158-163`) — то есть пострадала и запечённая раскладка сетевых поведений, а не только одна ссылка.

**Перед запуском: перепривяжите эти три слота на классы из `Assets/Game/Scripts`.**

---

## Антипаттерны и как их обходит этот код

**`Time.deltaTime` внутри FUN.** На ресимуляции он не равен шагу тика, и переигранная траектория разойдётся с исходной. Обход: `Runner.DeltaTime` везде — и в `MoveStep`, и в `Physics.Simulate`.

**`OnCollisionEnter`/`OnTriggerEnter` как источник игровых событий.** Не воспроизводится при ресимуляции, не приходит объекту без коллайдера, читает дефолтную физическую сцену. Обход: активный `OverlapSphere` в FUN. Учительская версия `PhysicsBullet` (`Assets/[Teacher]/Content/PhysicsBullet.cs`) делала именно через `OnCollisionEnter` — от этого ушли.

**Обычное поле как «я уже попал».** Не откатывается → потерянный или двойной урон. Обход: `[Networked] bool _collided` плюс `if (_collided) return;` в начале обработки.

**Удаление объекта из обхода буфера.** Ломает итерацию и может случиться в не-forward тике. Обход: флаг намерения + `Runner.Despawn` отдельной строкой в FUN. (Из порядка компонентов следует, что despawn произойдёт в том же тике — но уже за пределами цикла по буферу, а это и есть цель.)

**Запись `[Networked]`-полей после `Runner.Spawn`.** Клиенты увидят нулевое состояние минимум один тик. Обход: всё в `onBeforeSpawned`.

**`Render()`-polling вместо реакции на изменение.** `CharacterView` в `[Teacher]` каждый кадр делал `_visual.SetActive(_healthComponent.Exists())`; `ColorComponent` в снапшоте 02 дублировал логику `Spawned()` в `Render()`. Обход: `[OnChangedRender]` + событие, и материал один раз в `Spawned()`.

**Логика в `Render()`, которая на самом деле состояние.** Гашение коллайдера живёт в FUN `Character`, потому что от него зависят попадания.

**Интерполяция корня физического объекта.** Обход: `_interpolationTarget` на дочерний `<Visual>`, корень — в тиковой позиции.

**`Random` в детерминированном коде.** Поучительный контраст: `SpawnPointService.NextPoint()` использует `UnityEngine.Random.Range` и мутирует `List` — но зовётся **только** из `PlayerJoinController.OnPlayerJoined` под гейтом `if (!runner.IsServer) return;`, то есть не из FUN. Поэтому здесь безопасно. Тот же `Random` внутри `FixedUpdateNetwork` сломал бы ресимуляцию: на переигранном тике выпало бы другое число. **Правило: `Random` — только вне FUN и только на сервере.**

**⚠️ Чего этот код не обходит (учитывайте в своём проекте):**

1. **`Despawn` без проверки авторитета.** `LifetimeComponent.FixedUpdateNetwork`, `KinematicBullet.FixedUpdateNetwork` и `PhysicsBullet.FixedUpdateNetwork` зовут `Runner.Despawn(this.Object)` без `HasStateAuthority`/`IsServer`, хотя комментарий `//State Authority & Input Authority` прямо фиксирует, что FUN идёт и на InputAuthority-клиенте (а у пули InputAuthority — стрелок). Контраст тем резче, что `FireComponent.Fire()` начинается с `if (!this.Runner.IsServer) return;`. Плюс `LifetimeComponent` стоит в `NetworkedBehaviours` **раньше** скрипта пули, значит в одном тике `Despawn` может быть вызван дважды — по таймеру и по `_collided`. Вывод, требующий проверки в редакторе: Fusion 2 на `Despawn` без StateAuthority логирует ошибку или игнорирует вызов. **Рекомендация: гейт `if (!HasStateAuthority) return;` перед любым `Despawn`.**
2. **`HealthComponent.Spawned()` пишет `Health = MaxHealth;` без гейта по авторитету** (см. шаг 7).
3. **`m_AutoSyncTransforms: 0` без единого `Physics.SyncTransforms()`.** При выключенном авто-синке позиции коллайдеров в PhysX обновляются только внутри `Physics.Simulate`/`Physics.SyncTransforms`. Сам аддон в тексте атрибута `[WarnIf]` (`RunnerSimulatePhysicsBase.cs:61-65`) советует «manually call `SyncTransforms()` before Raycast/Overlap queries». В учебном коде этого вызова нет **нигде**, при том что `Character` двигает `transform.position` напрямую в FUN и носит `CapsuleCollider`, а `CollisionComponent.OverlapSphere` — это ровно overlap query. Это настоящая мина раздела. (Уточнение: `WarnAutoSyncTransforms` — это свойство для inspector-атрибута `[WarnIf]`, а не рантайм-лог.)
4. **Порядок `Physics.Simulate` vs `OverlapSphere` внутри тика не зафиксирован.** `Simulate` зовётся из `FixedUpdateNetwork()` сценного `RunnerSimulatePhysics3D` (он `SimulationBehaviour, IBeforeTick` — отдельная точка в порядке симуляции, а не `NetworkBehaviour` на пуле), `OverlapSphere` — из FUN `CollisionComponent` на пуле. Ни `[OrderBefore]`, ни `[OrderAfter]` в аддоне нет (grep по `Assets/Photon/FusionAddons/Physics` даёт ноль совпадений). Значит вопрос «видит ли запрос позиции до или после `Simulate` этого тика» в коде раздела никак не решён. **Для главы про детерминизм это главный незаданный вопрос — в своём проекте расставьте порядок явно.**
5. **Туннелирование. Сведите числа сами:** тик 30 Hz → шаг 1/30 с. Кинематическая пуля `_speed: 5` → **0.167 м/тик** при радиусе запроса `_radius: 0.125`. Физическая `_speed: 8` → **0.267 м/тик** при `m_CollisionDetection: 0` (Discrete) и `SphereCollider m_Radius: 0.125`. Шаг за тик **вдвое больше радиуса** — и собственный `OverlapSphere`, и Unity-физика способны проскочить тонкую цель. Это и есть мотивация глав 04 (экстраполяция) и 06 (лаг-компенсация). **Рекомендация: либо radius ≥ шаг за тик, либо `Continuous` collision detection, либо sweep/raycast по отрезку между тиками.**
6. **`DealDamage`/`ProcessCollision` продублированы копипастой** по ~30 строк в `KinematicBullet.cs` и `PhysicsBullet.cs`; общего базового класса или хелпера нет — ничего никуда не «вынесено». Для урока это удобно (переменная эксперимента ровно одна — способ движения), для продакшена выносите в `abstract class BulletBase` или статический хелпер.
7. **`[SerializeField]` вместо `GetComponent` применён не везде.** `Character`, `KinematicBullet`, `PhysicsBullet`, `CharacterView` держат явные ссылки (и это правильно: разрешаются один раз на импорте, а не на каждый спавн пулящегося объекта). Но `MoveController` и `FireController` по-прежнему делают `GetComponent` в `Awake()`, а `InputAccumulator`, `PlayerJoinController`, `PlayerLeftController` — `FindObjectOfType` в `Awake()`. Утверждение «ушли от GetComponent/FindObjectOfType» верно только для четырёх классов.
8. **`EntryPoint`: результат `StartGame` (Task) не ожидается и не проверяется** — нет `await`, нет разбора `ShutdownReason`. Fire-and-forget `StartGame` в проде прячет ошибки подключения.

---

## Чеклист: как применить в своём проекте

1. Поставьте `NetworkProjectConfig`: `PeerMode`, тикрейт (здесь 30), и убедитесь, что `Fusion.Addons.Physics` есть в `AssembliesToWeave`.
2. На GameObject с `NetworkRunner` положите `RunnerSimulatePhysics3D` **руками** и выставьте `PhysicsAuthority = Fusion`, `PhysicsTiming = FixedUpdateNetwork`, `ClientPhysicsSimulation = SimulateAlways`, `DeltaTimeMultiplier = 1`. Не надейтесь ни на `OnValidate`, ни на фикс в `Startup()`: первый только редакторный, второй только для Shared.
3. Решите по каждому движущемуся объекту: кинематика или Rigidbody. **Смешивать нельзя.** Кинематика → `NetworkTransform` + шаг в FUN. Rigidbody → `NetworkRigidbody3D` (без `NetworkTransform`), `m_IsKinematic: 0`, `m_Interpolate: 0`.
4. У каждого `NetworkRigidbody`-объекта, который должен предсказываться на клиенте, в `Spawned()` вызовите `Runner.SetIsSimulated(Object, true)`. Проверьте согласованность с `ClientPhysicsSimulation` и прочитайте варнинги из `AfterSpawned()`.
5. `InterpolationTarget` у `NetworkRigidbody` назначьте на **дочерний** визуальный объект. Корень оставьте носителем коллайдера, Rigidbody и центра запросов.
6. Весь шаг кинематического движения пишите как `position += dir * (speed * Runner.DeltaTime)` в `FixedUpdateNetwork()`. `Time.deltaTime` в FUN запрещён.
7. Попадания делайте через `Runner.GetPhysicsScene().OverlapSphere(...)` в FUN, в non-alloc перегрузке с заранее выделенным буфером. **Проверьте размер буфера в префабе, а не в исходнике.** Добавьте `OnDrawGizmos`, чтобы видеть зону.
8. Любой факт, накопленный в FUN, объявите `[Networked]`: флаг попадания, предыдущие кнопки, таймеры. Первой строкой обработчика ставьте guard по этому флагу — обработка должна быть идемпотентной.
9. Удаление объекта делайте флагом + отдельным `Runner.Despawn` в FUN, никогда внутри обхода буфера или колбэка физики. Добавьте гейт `if (!HasStateAuthority) return;` (в этом проекте его нет — не копируйте пробел).
10. Всю инициализацию `[Networked]`-полей и `velocity` делайте в `onBeforeSpawned` у `Runner.Spawn`. Используйте `TryGetComponent`, чтобы подмена префаба не роняла колбэк.
11. Таймеры — `TickTimer`. `ExpiredOrNotRunning()` там, где `default` должен считаться «готов» (кулдаун); `Expired()` там, где `default` не должен срабатывать (время жизни).
12. Ввод: один `NetworkBehaviour`-шлюз с `GetInput(out T)` на объект, раскладывающий ввод в C#-события. `previousButtons` — `[Networked]`. Enum кнопок — **подряд от нуля, без `[Flags]`** (значение = индекс бита). В сборе ввода — `Input.GetKey`, фронт детектируйте в тике через `WasPressed`.
13. Базовый класс выбирайте по наличию состояния: нет `[Networked]`, нет FUN/Spawned → обычный `MonoBehaviour`.
14. Инициализируйте `[Networked]`-поля только на StateAuthority либо в `onBeforeSpawned`; на прокси такая запись будет затёрта снапшотом.
15. Визуал — только `Render()`/`[OnChangedRender]`, и **применяйте начальное значение руками** сразу после подписки. Никакой визуальной работы в FUN. Побочные эффекты (звук, VFX) гейтьте по `Runner.IsForward`.
16. Состояние, от которого зависят попадания (включённость коллайдера, живость), меняйте **в FUN**, а не в Render.
17. Условия («можно двигаться?») подключайте к core-компонентам снаружи через `Func<bool>`/`SetCondition`, а не зашивайте в компонент.
18. Пул делайте через `NetworkObjectProviderDefault`, не через свой спавнер. Обязательно `instance.name = prefab.name`. Снимайте все подписки в `Despawned()`. Передайте провайдер в `StartGameArgs.ObjectProvider` — иначе он просто не используется (как здесь).
19. Иерархию сцены держите локальной: перепривязка под контейнер через маркер-компонент (`FindObjectOfType<WorldTransform>()`), а не через строковое имя; `SyncParent` включайте только если родитель реально меняется в сети.
20. `Random` — только вне FUN и только на сервере.
21. Сведите числа на туннелирование: `speed / tickRate` против радиуса запроса и размера коллайдера. Если шаг больше радиуса — меняйте радиус, включайте `Continuous` или переходите на sweep.
22. При `m_AutoSyncTransforms: 0` добавьте `Physics.SyncTransforms()` перед overlap/raycast-запросами, если двигаете трансформы напрямую.
23. Расставьте `[OrderBefore]`/`[OrderAfter]` между симуляцией физики и вашими физическими запросами. Порядок, который «просто работает», — это не контракт.
24. Включите `NetworkConditions` (`DelayMin/Max 0.15`, `LossChance 0.05`) и залогируйте `Runner.Tick` вместе с `Runner.IsForward`, чтобы **увидеть** ресимуляцию и поймать задвоенные эффекты.

---

## Что изменилось относительно главы 02

**Оговорка о направлении диффа.** Папка «предыдущего» раздела 02 внутри называется `Lessons-kinematic-physics-bullet-31.03.25`, то есть архив 02 — это снапшот **того же проекта и той же темы**, снятый **позже** (mtime файлов 02 — 2025-04-02, файлов 03 — 2025-04-01). Вывод (из имён и времён, не из кода): по курсовому порядку 02 → 03 диффы читаются «назад по времени». 03 — рабочее состояние урока про кинематику/физику; 02 — его более поздняя редакция с уже сделанным рефакторингом спавна и пулинга.

**Что появилось в 03:**
- Референсные папки `Assets/[Teacher]` (15 файлов .cs, незакомментирован только `Utils/WorldTransformChild.cs`; в `Configs` лежит `PoolObjectCatalog.asset`, чей скрипт тоже закомментирован — ещё один missing-script asset; папки `Bullet` и `Camera` пустые) и `Assets/[Archive1]` (20 файлов .cs, почти все закомментированы). Папки `[Archive]` в разделе нет. `[Teacher]` — предыдущие редакции тех же классов (`GetComponent` вместо `[SerializeField]`, `ICondition`/`ConditionableComponent` вместо `Func<bool>`, `OnCollisionEnter` вместо `OverlapSphere`, `LifetimeComponent.SetCallback(Action)`, отдельный `BulletSpawner : SimulationBehaviour`, `CameraFollower` на `Render()`, свой `PoolObjectProvider`, `[Flags] InputKeys`). `[Archive1]` — досетевой Zenject-прототип (`TeamConfig`, `TeamType`, `Leaderboard`, `GameCycle`, `CharacterRespawnController`, `PlayerInstaller`), к Fusion отношения не имеющий; полезен только как «вот от чего ушли».
- `FireComponent` спавнит пулю **напрямую**: `[SerializeField] NetworkPrefabRef _bulletPrefab` + `Runner.Spawn(..., onBeforeSpawned:)` и серверный гейт `if (!this.Runner.IsServer) return;` первой строкой. В 02 была косвенность через `NetworkObjectSpawner` (`FindObjectOfType` в `Spawned()`, затем `TrySpawn(position, rotation, InputAuthority, out NetworkObject bullet)`), а гейта `IsServer` в `Fire()` не было. Папка `Assets/Game/Scripts/System/NetworkObject` в 02 содержит **четыре** файла (`NetworkObjectBuffer`, `NetworkObjectSpawner`, `PoolableNetworkObjectCatalog`, `PoolableNetworkObjectProvider`); в 03 каталог и провайдер переехали в `System/ObjectProvider` (переименование папки), а буфер и спавнер отсутствуют. В сцене 02 был отдельный NetworkObject «Bullets» с `NetworkObjectBuffer` + `NetworkObjectSpawner` (`_prefab` = Bullet (Physics), `_bufferSize: 8`, `_outsidePosition: (0,-1000,0)`) — в 03 его нет вовсе.
- `Character` в 03 **не** зовёт `Runner.SetIsSimulated(Object, true)` в `Spawned()` — в 02 эта строка там была. Содержательная правка: кинематического `NetworkTransform`-персонажа в локальную симуляцию прокси включать не нужно, а Rigidbody-снаряд без этого не предсказывается.
- `PhysicsBullet` в 03 проще: нет `[SerializeField] GameObject _visual` и нет `Render() { _visual.SetActive(!_collided); }`, в `Init()` нет `_rigidbody.isKinematic = false;`. Вывод: оба добавления в 02 нужны именно при пулинге/предсозданном буфере — переиспользованный Rigidbody может вернуться из пула кинематическим (`NetworkRigidbody.SimulationExit()` ставит `isKinematic = true`).
- `ColorComponent` в 03 красит материал только в `Spawned()`; в 02 та же логика дублировалась ещё и в `Render()` каждый кадр.
- `HealthComponent` в 03 получил `[UnityNonSerialized]` — и в `Character.prefab` исчезла строка `_Health: 0`, которая есть в версии 02.
- `LifetimeComponent` в 03 без закомментированного `Spawned()`-артефакта из 02 (там `timer` создавался в `Spawned`, а не в `Init()` из `onBeforeSpawned`).
- `_triggerInteraction` у `CollisionComponent` на пулях в 03 = `1` (`QueryTriggerInteraction.Ignore`), в 02 = `0` (`UseGlobal`).
- Префабы в 03 ссылаются на закомментированные классы в `[Teacher]`/`[Archive1]` (missing scripts, см. раздел настроек); в 02, на день позже, привязка уже исправлена на финальные классы в `Game/Scripts`.

**Что не изменилось вообще:** `ProjectSettings/**` побайтово идентичны между 02 и 03 (`diff -rq` не дал ни одной строки), `NetworkProjectConfig.fusion` тоже идентичен, аддон `Assets/Photon/FusionAddons/Physics` присутствует в обоих. То есть ключевые настройки этой главы (`RunnerSimulatePhysics3D` с Fusion/FixedUpdateNetwork/SimulateAlways в сцене, тикрейт 30, `m_SimulationMode: 0` + `m_AutoSyncTransforms: 0`, `Fusion.Addons.Physics` в `AssembliesToWeave`) к моменту этого урока уже стояли. Их «новизна» в главе 03 не в том, что их добавили, а в том, что урок их **объясняет и демонстрирует** на паре `KinematicBullet`/`PhysicsBullet`.
