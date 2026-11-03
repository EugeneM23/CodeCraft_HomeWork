# 91. Аудит существующего кода

Аудит выполнен четырьмя независимыми агентами по всему игровому коду `Assets/Game/Scripts` (140 файлов) со сверкой по исходникам вендоренного фреймворка и по 29 документам `Docs/BestPractices` нового апстрима. Каждая находка привязана к файлу и строке.

## Сводка

| Область | Всего | high | medium | low |
|---|---|---|---|---|
| Контексты: GameContext, PlayerContext, контроллеры | 24 | 9 | 14 | 1 |
| Entity/Common: механики, поведения, use-cases | 30 | 11 | 12 | 7 |
| Entity/Content: инсталлеры, фабрика, пулы, оружие | 30 | 14 | 13 | 3 |
| Способности, аспекты, баффы, UI | 32 | 15 | 15 | 2 |
| **Итого** | **116** | **49** | **54** | **13** |

По категориям:

- `correctness` — 56
- `performance` — 25
- `idiom-violation` — 12
- `architecture` — 12
- `dead-code` — 7
- `unity-misuse` — 4

---

## Контексты: GameContext, PlayerContext, контроллеры

Read all 29 .cs files under Gameplay/GameContext and Gameplay/PlayerContext, plus the framework sources they bind to (Assets/Plugins/Atomic/Context, Entities, Elements), the use cases and installers they call, GameScene.unity to see what is actually wired, and the upstream BestPractices docs to judge idiom. 24 findings, all pointed at source.

The load-bearing ones:

1. **EntryPoint.cs is dead and dangerous.** Its GUID appears nowhere in GameScene.unity — the scene pumps lifecycle through `SceneContextRunner` (GameScene.unity:18057) with `GameContext.installOnAwake: 1`. If anyone ever adds the component, `_gameContextInstaller.Install(...)` bypasses `SceneContext`'s `installed` guard and the installer runs twice, producing a second `EnemyController` (double spawns), a second orphan `EntityFilter`, and an unreferenced `Pool_AudioSource` GameObject, because `AddValue` is `TryAdd` and silently discards the duplicate.

2. **The reactive character is snapshotted at Init in eight places.** `_character = context.GetCharacter().Value` freezes the entity out of an `IReactiveVariable<IEntity>` in all six controllers plus `CameraFollowController` and `DashAbilityConfig`. After any respawn every one of them drives the dead entity, with no exception to trace. `TeleportAbilityConfig` reads it lazily and is the proof of intent.

3. **`Debug.Log(_enemyFilter.Count)` per frame** in a shipped controller (GameContextInstaller.cs:58), boxing an int and formatting a string 60 times a second.

4. **The enemy `EntityFilter` is never disposed** (GameContextInstaller.cs:27). `IEntityFilter : IDisposable`, and its ctor hooks the world plus every entity's tag/value events; nothing in the context lifecycle disposes values, so each re-install leaves another live filter re-running the predicate on every entity mutation.

5. **Free stuns.** StunAbilityConfig.cs:36 applies the stun buff outside the mana condition that `AbilityUseCase.Use` checks, so the ability works at zero mana.

6. **The mana economy does not exist in the current build.** `new Cooldown(period)` starts already expired, and the scene's period is 0 (GameScene.unity:7649), so `RestoreManaBehaviour` grants mana every FixedUpdate. The `==` guard at RestoreManaBehaviour.cs:30 also lets mana overshoot `MaxMana` and then grow without bound.

7. **`GameFactory.Create` allocates a string per spawn** (`name.Replace("(Clone)", "")`) plus a double dictionary lookup, on the bullet/shell/hit-effect path.

8. **Duplicate `AddCharacter`** (PlayerContextInstaller.cs:27 vs CharacterSystemInstaller.cs:16) silently no-ops via `TryAdd`; the scene currently wires both inspector fields to the same object, so the trap is armed but not sprung.

Also: `CharacterShakeController` leaks both of its subscriptions (no `IContextDispose`); `CharacterJumpController` and `CharacterFireController` evaluate expensive conditions before checking input, and jump re-evaluates its condition twice per frame then applies a Rigidbody impulse from LateUpdate; `GameContext.Instance` is reached from 16 files including entity installers; abilities are keyed by ScriptableObject *file* name diverging from the `Name` used for the ability entity; `AbilityRunner.Init` appends without clearing so a re-init double-ticks every ability; `WeaponCatalog.Get*` actually instantiates, at the catalog's own transform, through an unguarded dictionary index, bypassing the pool; `_hitEffectPrefab` and `_playerID` are serialized, scene-wired and never read; camera follow is frame-rate dependent with an `int` speed; `CharacterRotateController` pins rotate direction to zero on desktop.

### Высокая важность (9)

#### HIGH-1. `Assets/Game/Scripts/Gameplay/GameContext/GameContextInstaller.cs:58` — performance

**Проблема.** `Debug.Log(_enemyFilter.Count)` runs in `EnemyController.OnUpdate`, i.e. every single frame, in a shipped controller.

**Следствие.** Each call boxes the `int` into `object` for `Debug.Log(object)`, formats a string, and pushes a managed stack trace into Unity's log system. That is ~200+ bytes of garbage per frame plus a synchronous write to the editor log and, in a development build, to the player log file. At 60fps this alone is ~12 KB/s of GC pressure and a measurable frame spike whenever the console flushes.

**Как исправить.**

```csharp
Delete the line. If the count is genuinely needed for diagnostics, gate it behind the filter's own event so it only fires on change, not per frame:

```csharp
public void Init(IGameContext context)
{
    _entityWorld = context.GetEntityWorld();
    _enemyFilter = context.GetEnemyFilter();
    _gameFactory = context.GetGameFactory();
#if UNITY_EDITOR
    _enemyFilter.OnAdded += _ => Debug.Log($"Enemies: {_enemyFilter.Count}");
    _enemyFilter.OnDeleted += _ => Debug.Log($"Enemies: {_enemyFilter.Count}");
#endif
}

public void OnUpdate(IContext context, float deltaTime)
{
    if (Input.GetKeyDown(KeyCode.M))
        SpawnUseCase.SpawnCharacter(_enemyPrefab, _spawnPoint, _entityWorld, _gameFactory);
}
```
```

#### HIGH-2. `Assets/Game/Scripts/Gameplay/GameContext/GameContextInstaller.cs:27` — correctness

**Проблема.** `new EntityFilter(_entityWorld, entity => entity.HasEnemyTag())` is stored as a plain context value and never disposed. `IEntityFilter` is `IDisposable` (Assets/Plugins/Atomic/Entities/Scripts/Filter/IEntityFilter.cs:9) and its constructor calls `Initialize()`, which hooks `world.OnAdded`/`OnDeleted` and then `OnTagAdded`, `OnTagDeleted`, `OnValueAdded`, `OnValueDeleted`, `OnValueChanged` on *every* entity in the world (EntityFilter.cs:36-52, 91-105).

**Следствие.** Nothing in the context lifecycle disposes values: `Context.Dispose()` only walks controllers (Context_Lifecycle.cs:87-98), and `SceneContext.InstallInternal` calls `context.Clear()` → `ClearValues()` (Context.cs:28-32), which drops the dictionary entry while the filter stays subscribed to the live `SceneEntityWorld` and to every entity. Every re-install (scene reload, or the `installInEditMode` path) leaves another orphan filter alive, each one re-running `predicate(entity)` on every tag/value mutation of every entity in the world. That is an unbounded per-mutation cost plus the filter keeping dead entities reachable.

**Как исправить.**

```csharp
Own the filter in a controller that implements `IContextDispose`, instead of leaking it as a bare value:

```csharp
public sealed class EnemyFilterController : IContextInit<IGameContext>, IContextDispose
{
    private EntityFilter _filter;

    public void Init(IGameContext context)
    {
        _filter = new EntityFilter(context.GetEntityWorld(), static e => e.HasEnemyTag());
        context.AddEnemyFilter(_filter);
    }

    public void Dispose(IContext context)
    {
        context.DelEnemyFilter();
        _filter?.Dispose();
        _filter = null;
    }
}
```
and in `Install`: `context.AddController(new EnemyFilterController());` instead of line 27. Note `static` on the predicate so no closure is allocated.
```

#### HIGH-3. `Assets/Game/Scripts/Gameplay/GameContext/EntryPoint.cs:15` — architecture

**Проблема.** `EntryPoint` is dead code that would corrupt the context if it were ever used. Its GUID (2deb0ece51924d45af7231d420078342) appears nowhere in Assets/Scenes/GameScene.unity — the scene drives lifecycle through `SceneContextRunner` (GameScene.unity:18057, contexts = GameContext + PlayerContext) with `GameContext.installOnAwake: 1`. Meanwhile `EntryPoint.Start` calls `_gameContextInstaller.Install(_gameContext)` directly, bypassing `SceneContext.Install`'s `installed` guard (SceneContext.cs:95-102).

**Следствие.** If someone drops this component into the scene (the obvious reading of a class named `EntryPoint`), `GameContextInstaller.Install` runs twice: once from `GameContext.Awake` and once from `Start`. `AddValue` uses `TryAdd` (Context_Values.cs:50-59) so the second `AddGameFactory` silently fails — but `new GameFactory(_poolsParent)` and its `RegisterNewPrefab` have already created a live `Pool_AudioSource` GameObject that nothing will ever reference, `AddController` uses a `HashSet` of instances so a *second* `EnemyController` is registered and ticks every frame (double enemy spawns per keypress), and a *second* `EntityFilter` subscribes to the world and is unreachable. `OnDestroy` also dereferences `_gameContext` unconditionally, so it throws if `Start` never ran.

**Как исправить.**

```csharp
Delete `EntryPoint.cs`. `SceneContextRunner` already performs install/init/enable/update/dispose and guards every callback with `started`. If a hand-written entry point is wanted, it must go through the context rather than the installer, and must be null-safe:

```csharp
public sealed class EntryPoint : MonoBehaviour
{
    [SerializeField] private GameContext _gameContext;

    private void Start()
    {
        _gameContext.Install();   // idempotent: guarded by SceneContext.installed
        _gameContext.Init();
        _gameContext.Enable();
    }

    private void Update() => _gameContext.OnUpdate(Time.deltaTime);
    private void FixedUpdate() => _gameContext.OnFixedUpdate(Time.fixedDeltaTime);
    private void LateUpdate() => _gameContext.OnLateUpdate(Time.deltaTime);
    private void OnDestroy() { if (_gameContext != null && _gameContext.Initialized) _gameContext.Dispose(); }
}
```
and remove the `SceneContextRunner` from the scene so exactly one pump exists.
```

#### HIGH-4. `Assets/Game/Scripts/Gameplay/PlayerContext/PlayerContextInstaller.cs:27` — correctness

**Проблема.** `context.AddCharacter(new ReactiveVariable<IEntity>(_playerEntity))` is a second `AddCharacter` for the same key — `CharacterSystemInstaller.Install` already did it at CharacterSystemInstaller.cs:16, and line 22 runs it before line 27.

**Следствие.** `AddValue` is `TryAdd` (Context_Values.cs:50-59) and returns `bool`, which is discarded. So line 27 silently does nothing and allocates a throwaway `ReactiveVariable<IEntity>`. Worse, it is a live trap: the scene wires two separate inspector fields to the same object (`_playerEntity` and `_characterSystem._character` are both fileID 6040861125521742544 in GameScene.unity:7632 and :7637). The moment an artist points one of them at a different entity, the game keeps using whichever installer ran first, with no warning anywhere — the symptom is "controllers drive the wrong character" with no error to trace.

**Как исправить.**

```csharp
Delete line 27. The character belongs to `CharacterSystemInstaller`, which already owns the `_character` field. Also delete the now-duplicate `_playerEntity` field at line 12 and repoint the scene reference to `_characterSystem._character`. If keeping belt-and-braces, make the duplicate loud rather than silent:

```csharp
protected override void Install(IPlayerContext context)
{
    context.AddMoveJoystick(_movementJoystick);
    context.AddRotateJoystick(_rotationJoystick);

    _characterSystem.Install(context);   // owns AddCharacter
    _cameraSystem.Install(context);
    _abilitySystem.Install(context);
    _manaSystem.Install(context);
}
```
Note the joysticks now install *before* the systems, so a system that needs them during `Install` (not just `Init`) also works.
```

#### HIGH-5. `Assets/Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterMoveController.cs:15` — correctness

**Проблема.** `_character = context.GetCharacter().Value` snapshots the entity out of a reactive variable at `Init` and never updates. The same line appears in CharacterRotateController.cs:15, CharacterFireController.cs:15, CharacterJumpController.cs:14, CharacterInteractController.cs:14, CharacterDropWeaponController.cs:14, CharacterShakeController.cs:16, CameraFollowController.cs:17, and inside an installer at Ability/Content/DashAbilityConfig.cs:20.

**Следствие.** `Character` is declared as `IReactiveVariable<IEntity>` (CodeGen/PlayerContextAPI.cs:21) — the whole point of the reactive wrapper is that the player entity is replaceable (respawn, possession, vehicle). After any `GetCharacter().Value = newEntity`, all eight sites keep driving the old entity: input writes `MoveDirection` on a corpse, the camera follows the dead body's `CameraPoint`, the dash ability buffs the dead entity, and the reference keeps the destroyed `SceneEntity`'s managed object alive. Nothing throws, so this presents as "input stops working after respawn". Note the one place that got it right (TeleportAbilityConfig.cs:22, 27 reads `context.GetCharacter().Value` lazily inside the closure) proves the variable is expected to change.

**Как исправить.**

```csharp
Hold the variable, not its value, and cache the per-entity dependencies on change. Applied to the move controller (and the same shape for the other seven):

```csharp
public sealed class CharacterMoveController : IContextInit<IPlayerContext>, IContextDispose, IContextLateUpdate
{
    private IReactiveVariable<IEntity> _character;
    private IVariable<Vector3> _moveDirection;
    private Joystick _joystick;

    public void Init(IPlayerContext context)
    {
        _joystick = context.GetMoveJoystick();
        _character = context.GetCharacter();
        _character.Observe(OnCharacterChanged);   // subscribes AND fires for the current value
    }

    public void Dispose(IContext context) => _character.Unsubscribe(OnCharacterChanged);

    private void OnCharacterChanged(IEntity character) =>
        _moveDirection = character?.GetMoveDirection();

    public void OnLateUpdate(IContext context, float deltaTime)
    {
        if (_moveDirection == null) return;
        ...
        _moveDirection.Value = direction;   // no per-frame dictionary lookup
    }
}
```
```

#### HIGH-6. `Assets/Game/Scripts/Gameplay/PlayerContext/Camera/CharacterShakeController.cs:20` — correctness

**Проблема.** `_weapon.Observe(OnWeaponChanged)` is subscribed in `Init`, and `OnWeaponChanged` subscribes `Shake` to the weapon's fire event — but the class implements only `IContextInit<IPlayerContext>`, so neither subscription is ever released. `OnWeaponChanged` also calls `_currentWeapon.GetFireEvent().Subscribe(Shake)` (line 29) with no null guard.

**Следствие.** `Observe` subscribes and immediately invokes (Assets/Plugins/Atomic/Elements/Scripts/Values/Extensions.cs:12-16), so the first weapon's `FireEvent` gets `Shake` at init. On context dispose, `Context.Dispose()` only calls `IContextDispose.Dispose` on controllers — this controller has none, so `Shake` stays on the weapon entity's `BaseEvent` and the controller (and through it the camera entity) stays reachable from the weapon for the rest of the process. In a scene-reload loop every reload adds another live `Shake` handler to the pooled weapon entity, so one shot fires N camera shakes on N different dead camera transforms. `Shake` (line 34) also re-reads `_weapon.Value.GetCameraShakeArgs()` — a dictionary lookup plus cast on every shot — instead of the args it already resolved when the weapon changed, and uses `_weapon.Value` rather than the `weapon` it actually subscribed to.

**Как исправить.**

```csharp
```csharp
public sealed class CharacterShakeController : IContextInit<IPlayerContext>, IContextDispose
{
    private IReactiveVariable<IEntity> _weapon;
    private IEntity _currentWeapon;
    private IEvent<CameraShakeArgs> _shakeEvent;
    private CameraShakeArgs _args;

    public void Init(IPlayerContext context)
    {
        _shakeEvent = context.GetCamera().GetCameraShakeEvent();
        _weapon = context.GetCharacter().Value.GetWeapon();
        _weapon.Observe(OnWeaponChanged);
    }

    public void Dispose(IContext context)
    {
        _weapon.Unsubscribe(OnWeaponChanged);
        if (_currentWeapon != null) _currentWeapon.GetFireEvent().Unsubscribe(Shake);
        _currentWeapon = null;
    }

    private void OnWeaponChanged(IEntity weapon)
    {
        if (_currentWeapon != null) _currentWeapon.GetFireEvent().Unsubscribe(Shake);

        _currentWeapon = weapon;
        if (weapon == null) { _args = null; return; }

        weapon.TryGetCameraShakeArgs(out _args);   // resolve once, not per shot
        weapon.GetFireEvent().Subscribe(Shake);
    }

    private void Shake()
    {
        if (_args != null) _shakeEvent.Invoke(_args);
    }
}
```
```

#### HIGH-7. `Assets/Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterJumpController.cs:19` — performance

**Проблема.** The expensive checks run before the cheap one, and the condition is then evaluated a second time. Lines 19-21 do `HasJumpableTag()` + `GetJumpCondition().Invoke()` every frame; only line 23 checks input; `JumpUseCase.Jump` (Jump/JumpUseCase.cs:10-13) immediately re-checks the tag and re-invokes the condition.

**Следствие.** Per frame, with no input at all: one tag hash-set probe, one `GetValue<BaseFunction<bool>>` dictionary lookup plus cast, one delegate invoke, and inside that closure (JumpAspect.cs:18 / JumpBuff.cs:25) another `GetHealth()` dictionary lookup. On a frame where V *is* held, all of it runs twice. `Input.GetKey` is the cheapest predicate here and it is checked last. Separately, `Input.GetKey` + `Rigidbody.AddForce(ForceMode.Impulse)` from `OnLateUpdate` means the impulse is applied after the physics step and `GetKey` (held, not pressed) fires an impulse on every frame the key is down, so holding V stacks impulses.

**Как исправить.**

```csharp
Check input first, let the use case own the validation (don't duplicate it), and drive physics from fixed update:

```csharp
public sealed class CharacterJumpController : IContextInit<IPlayerContext>, IContextFixedUpdate
{
    private IReactiveVariable<IEntity> _character;

    public void Init(IPlayerContext context) => _character = context.GetCharacter();

    public void OnFixedUpdate(IContext context, float deltaTime)
    {
        if (!Input.GetKeyDown(KeyCode.V)) return;   // cheapest gate first, GetKeyDown not GetKey

        IEntity character = _character.Value;
        if (character != null) JumpUseCase.Jump(character);   // Jump() already checks tag + condition
    }
}
```
Idiomatically this should be a request on the entity rather than a context controller poking it (Docs/BestPractices/RequestConditionActionEvent.md): `entity.AddJumpRequest(new BaseRequest())`, the controller sets the request, and a `JumpBehaviour` on the entity consumes it in `FixedTick`.
```

#### HIGH-8. `Assets/Game/Scripts/Gameplay/Entity/Content/GameFactory/GameFactory.cs:19` — performance

**Проблема.** `string id = prefab.gameObject.name.Replace("(Clone)", "")` allocates a string on every `Create(SceneEntity)` call, then lines 21 and 24 do two separate dictionary lookups (`ContainsKey` then `_pools[id]` inside `Create(string)`).

**Следствие.** This is the hot spawn path: every bullet (SpawnUseCase.cs:24), every shell (:36), every collision hit effect (:15). `GameObject.name` is a managed-string marshal out of native Unity each call, and `String.Replace` allocates a second string even though a prefab name never contains "(Clone)" — so it is pure garbage. A 600 RPM automatic weapon with a shell and a hit effect per shot generates 30 string allocations per second from a path whose whole purpose is to avoid allocation. The double lookup then hashes that fresh string twice.

**Как исправить.**

```csharp
Key the pools by the prefab reference, which is stable and needs no string at all:

```csharp
public sealed class GameFactory
{
    private readonly Dictionary<SceneEntity, SceneEntityPool> _poolsByPrefab = new();
    private readonly Dictionary<string, SceneEntityPool> _poolsById = new();
    private readonly Transform _poolsParent;

    public IEntity Create(SceneEntity prefab)
    {
        if (!_poolsByPrefab.TryGetValue(prefab, out SceneEntityPool pool))
            pool = RegisterNewPrefab(prefab, prefab.name);

        return pool.Rent();
    }

    public IEntity Create(string id) => _poolsById[id].Rent();

    public SceneEntityPool RegisterNewPrefab(SceneEntity prefab, string id)
    {
        var container = new GameObject($"Pool_{id}");
        container.transform.SetParent(_poolsParent);

        var pool = new SceneEntityPool(prefab, container.transform);
        _poolsByPrefab[prefab] = pool;
        _poolsById[id] = pool;
        return pool;
    }
}
```
`ReactiveDictionary` was also the wrong container here — nothing subscribes to pool add/remove, so it only added indirection.
```

#### HIGH-9. `Assets/Game/Scripts/Gameplay/Ability/Content/StunAbilityConfig.cs:36` — correctness

**Проблема.** `BuffUseCase.Apply(target, _stunBuff.CreateBuff())` sits outside the ability's own gate. `AbilityUseCase.Use(ability, target)` (PlayerContext/Ability/AbilityUseCase.cs:32-42) checks the target condition and returns silently if mana is insufficient, but line 36 then applies the stun unconditionally.

**Следствие.** The mana cost is free. `AbilityUseCase.Use` returns `void`, so the caller cannot tell whether the condition passed; with 0 mana the condition at line 21 fails, `ManaUseCase.Spend` is never reached, and the stun is still applied to the raycast target. Press R repeatedly with empty mana and you stun everything in the scene for nothing. The effect also lives in the wrong place: the ability's `TargetAction` (line 24) only spends mana, so the actual gameplay effect bypasses the Request-Condition-Action-Event flow the framework is built around (Docs/BestPractices/RequestConditionActionEvent.md).

**Как исправить.**

```csharp
Move the effect into the ability's action, where the condition already guards it, and have the condition use its own parameter:

```csharp
protected override void Install(Ability ability, IPlayerContext context)
{
    ability.AddTargetAbilityTag();
    ability.AddManaCost(new Const<int>(_manaCost));
    ability.AddTargetEvent(new BaseEvent<IEntity>());

    ability.AddTargetCondition(new BaseFunction<IEntity, bool>(target =>
        target != null &&
        ManaUseCase.Enough(context, _manaCost) &&
        BuffUseCase.CanApply(target, _stunBuff)));

    ability.AddTargetAction(new BaseAction<IEntity>(target =>
    {
        if (!ManaUseCase.Spend(context, _manaCost)) return;
        BuffUseCase.Apply(target, _stunBuff.CreateBuff());
    }));

    ability.WhenUpdate(_ =>
    {
        if (Input.GetKeyDown(_abilityKey) && RayCastUseCase.RayCastTarget(out var target, _layerMask))
            AbilityUseCase.Use(ability, target);
    });
}
```
```

### Средняя важность (14)

#### MEDIUM-1. `Assets/Game/Scripts/Gameplay/PlayerContext/Ability/AbillityRunner.cs:20` — correctness

**Проблема.** `Init` appends to `_ability` without clearing it, and `Dispose` disposes the abilities but leaves the list populated. The list is a `readonly` field on a controller that outlives both calls.

**Следствие.** `Context.Dispose()` sets `initialized = false` but never removes controllers (Context_Lifecycle.cs:87-98; `ClearControllers` is only reached through `Context.Clear()`), so the same `AbilityRunner` instance can receive a second `Init`. After that, `_ability` holds each ability twice: `item.Init()` runs twice per ability, and `OnUpdate`/`OnFixedUpdate`/`OnLateUpdate` tick each one twice per frame — a dash that spends one charge now spends two, and every `WhenUpdate` input closure polls `Input.GetKeyDown` twice, which can double-trigger on the same frame. Each disposed ability is also re-ticked.

**Как исправить.**

```csharp
```csharp
public void Init(IPlayerContext context)
{
    _abilities.Clear();

    foreach (KeyValuePair<string, Ability> pair in context.GetAbilities())
        _abilities.Add(pair.Value);

    for (int i = 0, count = _abilities.Count; i < count; i++)
        _abilities[i].Init();
}

public void Dispose(IContext context)
{
    for (int i = 0, count = _abilities.Count; i < count; i++)
        _abilities[i].Dispose();

    _abilities.Clear();
}
```
Also rename the field from `_ability` to `_abilities` (it is a collection) and index the `List<Ability>` with `for` rather than `foreach` in the three per-frame pumps, so there is no enumerator at all on the hot path. Fix the filename too: `AbillityRunner.cs` holds `AbilityRunner`.
```

#### MEDIUM-2. `Assets/Game/Scripts/Gameplay/PlayerContext/Ability/AbilitySystemInstaller.cs:19` — idiom-violation

**Проблема.** Abilities are keyed by `_dashAbility.name` — the Unity asset *file* name — in a `ReactiveDictionary<string, Ability>`, while the `Ability` entity itself is constructed with the unrelated serialized `AbilityConfig.Name` (Ability/AbilityConfig.cs:7, 13). `context.GetAbilities()` is also re-resolved from the context dictionary on each of lines 19, 20, 21.

**Следствие.** The dictionary key and the entity's own identity diverge: the scene's assets are `DashAbilityConfig`, `TeleportAbilityConfig`, `StunAbilityConfig` (GameScene.unity:7642-7644), so a lookup by the designer-facing `Name` ("Dash") misses, and renaming the asset file silently breaks every call site. Stringly-typed keys are exactly what the framework's generated `int` id constants exist to replace (CodeGen/PlayerContextAPI.cs:21-30 and Docs/BestPractices/SharedConstants.md). Nothing subscribes to the dictionary either, so `ReactiveDictionary` is pure overhead, and `AbilityRunner.Init` copies it to a list anyway.

**Как исправить.**

```csharp
Key by a serialized enum, build the dictionary once, and add it to the context in a single call:

```csharp
public void Install(IPlayerContext context)
{
    var abilities = new Dictionary<AbilityID, Ability>(_configs.Length);

    foreach (AbilityConfig config in _configs)
    {
        if (config == null) continue;
        abilities.Add(config.ID, config.Create(context));
    }

    context.AddAbilities(abilities);
    context.AddController<AbilityRunner>();
}
```
with `[SerializeField] private AbilityConfig[] _configs;` replacing the three typed fields (which also removes the god-installer shape — adding a fourth ability currently means editing this class) and `public AbilityID ID` replacing the `string Name` key on `AbilityConfig`. Regenerate `PlayerContextAPI` so `Abilities` is `IReadOnlyDictionary<AbilityID, Ability>`.
```

#### MEDIUM-3. `Assets/Game/Scripts/Gameplay/PlayerContext/Mana/RestoreManaBehaviour.cs:30` — correctness

**Проблема.** The only guard against over-restoring is `if (_currentMana.Value == _maxMana.Value) return;`, and line 37 does an unclamped `_currentMana.Value += _restore.Value`.

**Следствие.** Equality, not a bound. With `_restoreAmount` > 1 and a max that is not a multiple of it — e.g. max 10, current 9, restore 2 — the sum is 11, the `==` guard never matches again, and mana grows without limit: 13, 15, 17… every period, forever. Every ability's `ManaUseCase.Enough` then passes unconditionally, so the entire mana economy stops existing. The scene's current values (max 10, restore 1, GameScene.unity:7646-7651) happen to divide evenly, which is why it has not been noticed.

**Как исправить.**

```csharp
```csharp
public void OnFixedUpdate(IContext context, float deltaTime)
{
    int max = _maxMana.Value;
    if (_currentMana.Value >= max)
        return;

    _period.Tick(deltaTime);
    if (!_period.IsExpired())
        return;

    _currentMana.Value = Mathf.Min(_currentMana.Value + _restore.Value, max);
    _period.Reset();
}
```
```

#### MEDIUM-4. `Assets/Game/Scripts/Gameplay/PlayerContext/Mana/ManaSystemInstaller.cs:25` — correctness

**Проблема.** `new Cooldown(_restoreManaPeriod)` uses the one-argument constructor, which leaves `_current = 0` (Assets/Modules/Cooldown/Cooldown.cs:13-17), i.e. the cooldown starts already expired. `_restoreManaPeriod` is also serialized as `0` in the scene (GameScene.unity:7649).

**Следствие.** `Cooldown.IsExpired()` is `_current <= 0`, so the very first `FixedUpdate` after init grants mana before any time has passed. Worse, with the scene's period of 0, `Reset()` sets `_current = 0` and the cooldown is expired again on the next fixed step — so `RestoreManaBehaviour` grants `_restoreAmount` mana 50 times per second and the pool is permanently full. Every mana cost in the ability system is therefore free in the current build.

**Как исправить.**

```csharp
Start the cooldown full, and reject a non-positive period at install time where it is cheap to catch:

```csharp
[SerializeField, Min(0.01f)] private float _restoreManaPeriod = 1f;

public void Install(IPlayerContext context)
{
    context.AddMaxMana(_maxMana);
    context.AddCurrentMana(new ReactiveInt(_maxMana.Value));
    context.AddController(new RestoreManaBehaviour(
        new Cooldown(_restoreManaPeriod, _restoreManaPeriod),   // current = duration: starts unexpired
        _restoreAmount));
}
```
and set the period to a real value on the PlayerContextInstaller in GameScene.
```

#### MEDIUM-5. `Assets/Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterDropWeaponController.cs:15` — idiom-violation

**Проблема.** `_gameContext = GameContext.Instance;` — a controller belonging to the *player* context reaches the game context through a static singleton, then hands the concrete `GameContext` type to `DropWeaponUseCase.DropWeapon(IEntity, GameContext)` (Entity/Common/Interact/DropWeaponUseCase.cs:9).

**Следствие.** `SingletonSceneContext<T>.Instance` does `FindObjectOfType<T>()` on first access and throws `NullReferenceException` if the context is not in the scene yet (Assets/Plugins/Atomic/Context/Scripts/Contexts/Unity/SingletonSceneContext.cs:14-18) — and the `_instance` field is `static`, so in the editor it survives scene reloads and can hand out a destroyed `GameContext` after the second Play. The parent link already exists and is already wired: `PlayerContext.initialParent` is the `GameContext` (GameScene.unity:7673), and the game context even holds the player context as a value (GameContextInstaller.cs:23). Taking the concrete class also makes `DropWeaponUseCase` untestable and un-mockable, against Docs/BestPractices/PreferAbstractInterfaces.md. This is the same singleton reach found in 15 other files (CharacterCoreInstaller.cs:38, BulletInstaller.cs:21, RangeWeaponInstaller.cs:35, …).

**Как исправить.**

```csharp
Resolve the parent context once, through the interface:

```csharp
public sealed class CharacterDropWeaponController : IContextInit<IPlayerContext>, IContextUpdate
{
    private IReactiveVariable<IEntity> _character;
    private IGameContext _gameContext;

    public void Init(IPlayerContext context)
    {
        _character = context.GetCharacter();
        _gameContext = (IGameContext) context.Parent;
    }

    public void OnUpdate(IContext context, float deltaTime)
    {
        if (!Input.GetKeyDown(KeyCode.G)) return;

        IEntity character = _character.Value;
        if (character != null) DropWeaponUseCase.DropWeapon(character, _gameContext);
    }
}
```
and change `DropWeaponUseCase.DropWeapon(IEntity character, IGameContext gameContext)` to take the interface. Note the input check moved to the top so the `_character.Value` read does not happen 59 frames out of 60.
```

#### MEDIUM-6. `Assets/Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterMoveController.cs:24` — performance

**Проблема.** Per frame in `Move()`: two `new Vector3(...)` constructions (lines 21, 24), two string-keyed `Input.GetAxis("Horizontal")`/`"Vertical"` calls, a `direction.magnitude` square root, and a `_character.GetMoveDirection()` context-dictionary lookup plus cast on line 29.

**Следствие.** `Input.GetAxis(string)` hashes the axis name and walks Unity's input-axis table on every call — it is the documented slow path versus a cached axis id, and the literals are stringly-typed so a renamed axis fails at runtime, not compile time. `magnitude` is an unnecessary `Sqrt` when only the comparison against 1 matters. `GetMoveDirection()` re-resolves the same `IReactiveVariable<Vector3>` 60 times a second when it could be cached once per character (see the stale-character finding). The second `new Vector3` runs on every frame the joystick is idle, which is most frames on desktop.

**Как исправить.**

```csharp
```csharp
public void OnLateUpdate(IContext context, float deltaTime)
{
    if (_moveDirection == null) return;

    Vector3 direction;
    float h = _joystick.Horizontal, v = _joystick.Vertical;

    if (h != 0f || v != 0f)
        direction = new Vector3(h, 0f, v);
    else
        direction = new Vector3(Input.GetAxis(HORIZONTAL), 0f, Input.GetAxis(VERTICAL));

    if (direction.sqrMagnitude > 1f)        // no Sqrt
        direction.Normalize();

    _moveDirection.Value = direction;       // cached variable, no dictionary lookup
}

private const string HORIZONTAL = "Horizontal";
private const string VERTICAL = "Vertical";
```
`Vector3` is a struct, so the two constructions are stack-only — the real wins here are the cached variable and dropping `GetAxis` on joystick frames.
```

#### MEDIUM-7. `Assets/Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterRotateController.cs:21` — correctness

**Проблема.** `Rotate()` writes `_character.GetRotateDirection().Value = direction` every frame, where `direction` is `Vector3.zero` whenever the joystick is idle — and unlike `CharacterMoveController` there is no keyboard/mouse fallback (compare CharacterMoveController.cs:24). The class is otherwise a copy of the move controller.

**Следствие.** On desktop, where `_rotationJoystick.Horizontal/Vertical` are always 0, the character's rotate direction is pinned to zero forever, so `CharacterRotateBehaviour` has nothing to aim at — aiming simply does not work outside of mobile. The write is also unconditional: it depends on `ReactiveVariable`'s equality check (Assets/Plugins/Atomic/Elements/Scripts/Variables/ReactiveVariable.cs:29) to avoid re-raising `OnValueChanged` to every subscriber each frame, which is a silent dependency on an implementation detail rather than intent. The duplicated `new Vector3` / normalize / assign body across the two controllers means any fix has to be made twice.

**Как исправить.**

```csharp
Fold both controllers into one parameterised mechanic so the two input paths cannot drift:

```csharp
public sealed class CharacterDirectionController : IContextInit<IPlayerContext>, IContextLateUpdate
{
    private readonly Func<IPlayerContext, Joystick> _joystickSelector;
    private readonly Func<IEntity, IVariable<Vector3>> _targetSelector;
    private readonly bool _useKeyboardFallback;

    private Joystick _joystick;
    private IVariable<Vector3> _target;
    ...
    public void OnLateUpdate(IContext context, float deltaTime)
    {
        if (_target == null) return;

        var direction = new Vector3(_joystick.Horizontal, 0f, _joystick.Vertical);

        if (_useKeyboardFallback && direction == Vector3.zero)
            direction = CameraRelativeAimUseCase.FromMouse();   // real desktop aim, not zero

        if (direction.sqrMagnitude > 1f) direction.Normalize();
        _target.Value = direction;
    }
}
```
registered twice in `CharacterSystemInstaller` — once for `GetMoveDirection` with the keyboard fallback, once for `GetRotateDirection` with a mouse-aim fallback. At minimum, give the rotate controller a desktop fallback instead of writing zero.
```

#### MEDIUM-8. `Assets/Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterFireController.cs:21` — performance

**Проблема.** `_character.GetFireCondition().Invoke()` is evaluated every frame before the input check on line 23, and both `GetFireCondition()` and `GetFireAction()` are resolved from the entity dictionary on every frame they run. The `_weapon` field (line 11) is assigned in `Init` (line 16) and never read.

**Следствие.** The fire condition is an `AndExpression` chain (CharacterCoreInstaller.cs:72, MeleeWeaponInstaller.cs:50) whose terms each do their own dictionary lookups — evaluating it on frames where Space is not held is wasted work plus a `GetValue<IExpression<bool>>` lookup and cast. `Input.GetKey` is the cheap predicate and should gate the rest. `Input.GetKey` (held) rather than `GetKeyDown` also means the action fires every frame Space is down and relies entirely on the weapon cooldown inside `FireCondition` for rate limiting, so any weapon installed without a cooldown term becomes a hitscan beam. The dead `_weapon` field makes the class look like it reacts to weapon changes when it does not.

**Как исправить.**

```csharp
```csharp
public sealed class CharacterFireController : IContextInit<IPlayerContext>, IContextLateUpdate
{
    private IReactiveVariable<IEntity> _character;

    public void Init(IPlayerContext context) => _character = context.GetCharacter();

    public void OnLateUpdate(IContext context, float deltaTime)
    {
        if (!Input.GetKey(KeyCode.Space)) return;   // cheapest gate first

        IEntity character = _character.Value;
        if (character == null) return;

        if (character.GetFireCondition().Invoke())
            character.GetFireAction().Invoke();
    }
}
```
The `_weapon` field and its `Init` assignment are deleted. Better still, per Docs/BestPractices/RequestConditionActionEvent.md this controller should only set `character.GetFireRequest()`, leaving the condition/action/event flow to a `FireBehaviour` on the entity — the controller then holds no entity-specific state at all.
```

#### MEDIUM-9. `Assets/Game/Scripts/Gameplay/GameContext/GameContextInstaller.cs:31` — architecture

**Проблема.** `EnemyController` is declared in the installer's own file, implements `IContextInit<GameContext>` (the concrete MonoBehaviour context, not `IGameContext`), and mixes three unrelated jobs: debug keyboard spawning, holding the prefab/spawn point, and logging.

**Следствие.** `IContextInit<T>.Init(IContext)` casts to `T` (Assets/Plugins/Atomic/Context/Scripts/Behaviours/IContextInit.cs:11). It works when the context's `Init()` runs, because `Context` passes `this.owner` — the `GameContext` MonoBehaviour. But `Context.AddController` passes `this`, the plain inner `Context`, when the controller is added after init (Context_Controllers.cs:64), so adding this controller at runtime throws `InvalidCastException: Atomic.Contexts.Context -> Game.GameContext`. Every other controller in the project correctly targets an interface (`IContextInit<IPlayerContext>`), so this one also blocks ever swapping the game context for a test double or a networked implementation (Docs/BestPractices/PreferAbstractInterfaces.md). Hiding a tickable controller inside the installer file also means nobody finds it when hunting for per-frame work — which is how the `Debug.Log` survived.

**Как исправить.**

```csharp
Move it to its own file as `Gameplay/GameContext/EnemySpawnController.cs`, target the interface, and keep it to one job:

```csharp
public sealed class EnemySpawnController : IContextInit<IGameContext>, IContextUpdate
{
    private readonly SceneEntity _prefab;
    private readonly Transform _spawnPoint;
    private readonly KeyCode _debugSpawnKey;

    private IEntityWorld _world;
    private GameFactory _factory;

    public EnemySpawnController(SceneEntity prefab, Transform spawnPoint, KeyCode debugSpawnKey)
    {
        _prefab = prefab;
        _spawnPoint = spawnPoint;
        _debugSpawnKey = debugSpawnKey;
    }

    public void Init(IGameContext context)
    {
        _world = context.GetEntityWorld();
        _factory = context.GetGameFactory();
    }

    public void OnUpdate(IContext context, float deltaTime)
    {
        if (Input.GetKeyDown(_debugSpawnKey))
            SpawnUseCase.SpawnCharacter(_prefab, _spawnPoint, _world, _factory);
    }
}
```
It no longer needs `GetEnemyFilter` at all once the `Debug.Log` is gone.
```

#### MEDIUM-10. `Assets/Game/Scripts/Gameplay/GameContext/GameContextInstaller.cs:13` — dead-code

**Проблема.** `[SerializeField] private SceneEntity _hitEffectPrefab;` is wired in the scene (GameScene.unity:18022) but never read anywhere in the project — `Install` registers only `_audioPrefab` (line 25). Grep for `_hitEffectPrefab` returns exactly one hit: the declaration.

**Следствие.** The clear intent was `RegisterNewPrefab(_hitEffectPrefab, ...)` alongside the audio prefab, so the hit-effect pool exists before gameplay. Because it is missing, the pool is created lazily on the *first bullet impact* by `GameFactory.Create(prefab)` (GameFactory.cs:21-22) — which at that moment does `new GameObject($"Pool_{id}")` plus a string concat plus the first `SceneEntity.Create` instantiation, inside a collision callback. That is a guaranteed hitch on the first shot that lands, exactly the spike pooling exists to prevent. The dangling field also implies pre-warming is handled when it is not.

**Как исправить.**

```csharp
Register it next to the audio prefab, and pre-warm both:

```csharp
protected override void Install(IGameContext context)
{
    context.AddEntityWorld(_entityWorld);
    context.AddWeaponCatalog(_weaponsCatalog);
    context.AddPlayerContext(_playerContext);

    var factory = new GameFactory(_poolsParent);
    factory.RegisterNewPrefab(_audioPrefab, GameFactoryID.AudioSource.ToString()).Init(_audioPoolSize);
    factory.RegisterNewPrefab(_hitEffectPrefab, GameFactoryID.HitEffect.ToString()).Init(_hitPoolSize);
    context.AddGameFactory(factory);

    context.AddController(new EnemySpawnController(_enemyPrefab, _spawnPoint, KeyCode.M));
    context.AddController(new EnemyFilterController());
}
```
(`RegisterNewPrefab` returning the pool is part of the `GameFactory` fix above; `SceneEntityPool.Init(count)` already exists.) If the hit effect really is unused, delete the field and the scene reference instead.
```

#### MEDIUM-11. `Assets/Game/Scripts/Gameplay/PlayerContext/PlayerContextInstaller.cs:11` — dead-code

**Проблема.** `[SerializeField] private PlayerID _playerID;` is never used — `Install` (lines 20-30) never puts it in the context, and there is no `AddPlayerID` in the generated `PlayerContextAPI`.

**Следствие.** The id exists precisely so consumers can find *their* player context, and the UI is written as if it did: `AmmoPresenter.cs:14` and `HealthPresenter.cs:14` both serialize their own `PlayerID ID` field, which is likewise never compared to anything, and then fall back to `GameContext.Instance.GetPlayerContext().GetCharacter().Value` (AmmoPresenter.cs:21, HealthPresenter.cs:20) — a global reach that hard-codes single-player. So the project carries the data model for multiple players and none of the wiring, and every UI element silently binds to whichever player context the singleton happens to return.

**Как исправить.**

```csharp
Install the id and resolve the context by it instead of through the singleton. In `PlayerContextInstaller.Install`:

```csharp
context.AddPlayerID(_playerID);
```
after regenerating `PlayerContextAPI` with a `PlayerID` value. Then in the game context, index the player contexts:

```csharp
// GameContextInstaller
context.AddPlayerContexts(_playerContexts.ToDictionary(c => c.GetPlayerID()));
```
and have the presenters take an `IPlayerContext` resolved from their serialized `ID` rather than calling `GameContext.Instance`. If multiplayer is genuinely out of scope, delete `_playerID` here and the `ID` fields in both presenters so the code stops implying support that does not exist.
```

#### MEDIUM-12. `Assets/Game/Scripts/Gameplay/GameContext/WeaponCatalog.cs:18` — correctness

**Проблема.** `GetPickUpWeapon`/`GetWeapon` are named as lookups but each one instantiates a new `SceneEntity`, at the *catalog's own* transform, via an unguarded dictionary index `_pickUpWeapons[id]` / `_weapons[id]`. The class is also a `SerializedMonoBehaviour` decorated with a pointless `[Serializable]` (line 10).

**Следствие.** Three distinct problems from one method. (1) `_weapons[id]` throws `KeyNotFoundException` for any `WeaponID` an artist has not filled in — and the enum has five members (WeaponID.cs), so a half-populated catalog crashes at the moment a weapon is dropped rather than at validation time. (2) The `Get` prefix means callers assume a cheap lookup; `DropWeaponUseCase.SpawnPickUpWeapon` (Entity/Common/Interact/DropWeaponUseCase.cs:30-31) in fact instantiates at the catalog's position and then immediately teleports the object, so every drop spawns at the wrong place for one frame — visible as a pop if the catalog sits anywhere near the camera. (3) It bypasses `GameFactory` entirely, so pick-up weapons are `Instantiate`d and `Destroy`ed (DropWeaponUseCase.cs:19) while everything else in the project is pooled.

**Как исправить.**

```csharp
```csharp
public sealed class WeaponCatalog : SerializedMonoBehaviour
{
    [OdinSerialize] private Dictionary<WeaponID, SceneEntity> _weapons = new();
    [OdinSerialize] private Dictionary<WeaponID, SceneEntity> _pickUpWeapons = new();

    public bool TryGetWeaponPrefab(WeaponID id, out SceneEntity prefab) =>
        _weapons.TryGetValue(id, out prefab);

    public bool TryGetPickUpPrefab(WeaponID id, out SceneEntity prefab) =>
        _pickUpWeapons.TryGetValue(id, out prefab);

#if UNITY_EDITOR
    private void OnValidate()
    {
        foreach (WeaponID id in Enum.GetValues(typeof(WeaponID)))
        {
            if (!_weapons.ContainsKey(id)) Debug.LogError($"WeaponCatalog: missing weapon for {id}", this);
            if (!_pickUpWeapons.ContainsKey(id)) Debug.LogError($"WeaponCatalog: missing pick-up for {id}", this);
        }
    }
#endif
}
```
The catalog now only looks things up. Instantiation moves to the caller, through the pool and at the right position:

```csharp
// DropWeaponUseCase.SpawnPickUpWeapon
if (!gameContext.GetWeaponCatalog().TryGetPickUpPrefab(weapon.GetWeaponId(), out SceneEntity prefab))
    return;

IEntity pickUp = gameContext.GetGameFactory().Create(prefab);
pickUp.GetTransform().SetPositionAndRotation(weaponTransform.position, Quaternion.identity);
```
Drop the `[Serializable]` attribute — it does nothing on a MonoBehaviour.
```

#### MEDIUM-13. `Assets/Game/Scripts/Gameplay/PlayerContext/Camera/CameraSystemInstaller.cs:26` — architecture

**Проблема.** The camera installer registers `CharacterShakeController` — a controller that subscribes to the *character's weapon* fire event — and owns a `[SerializeField] private Transform _character` (line 16) used only to compute a camera offset at install time (line 23). Line 23 also writes `_cameraRoot.transform`, where `_cameraRoot` is already a `Transform`.

**Следствие.** Two installers now both claim the character: `CharacterSystemInstaller` owns `AddCharacter` and the character controllers, while the camera installer silently adds a seventh character controller. Deleting the camera system therefore also removes weapon shake, and the scene wires `_cameraSystem._character` to fileID 5160812569022390376 — a *different* object from the character entity at 6040861125521742544 (GameScene.unity:7637 vs :7641) — so the offset is computed from a transform that is not the player, with nothing to flag the mismatch. The offset is captured once during `Awake` into a `Const<Vector3>`, baking the editor-time layout into runtime behaviour: if the camera rig or that stand-in transform moves before `Awake`, the camera's resting offset is wrong for the whole session. `_cameraRoot.transform` is a redundant native property call that returns the component's own transform.

**Как исправить.**

```csharp
Move the character-facing controller to the installer that owns the character, and make the offset explicit data instead of a scene measurement:

```csharp
[Serializable]
public sealed class CameraSystemInstaller : IContextInstaller<IPlayerContext>
{
    [SerializeField] private SceneEntity _camera;
    [SerializeField] private Transform _cameraRoot;
    [SerializeField] private Const<float> _cameraSpeed = 5f;
    [SerializeField] private Const<Vector3> _cameraOffset = new Vector3(0f, 12f, -8f);

    public void Install(IPlayerContext context)
    {
        context.AddCamera(_camera);
        context.AddCameraRoot(_cameraRoot);
        context.AddCameraSpeed(_cameraSpeed);
        context.AddCameraOffset(_cameraOffset);
        context.AddController<CameraFollowController>();
    }
}
```
`context.AddController<CharacterShakeController>()` moves into `CharacterSystemInstaller.Install`. `_character` and the `UnityEngine.Serialization` using are deleted. Speed becomes `Const<float>` rather than `BaseVariable<int>`: it is a read-only tuning value, which is what `Const<T>` is for (Docs/BestPractices/SharedConstants.md), and an `int` cannot express a sub-unit smoothing rate.
```

#### MEDIUM-14. `Assets/Game/Scripts/Gameplay/Entity/Common/Camera/CameraFollowUseCase.cs:15` — correctness

**Проблема.** `Vector3.Lerp(source.position, target.position + offset, speed * deltaTime)` is a frame-rate-dependent smoothing step, driven every frame from `CameraFollowController.OnLateUpdate` (Camera/CameraFollowController.cs:25) with `speed` typed as `int`.

**Следствие.** Lerping toward a moving target by `speed * deltaTime` each frame is the classic frame-rate-dependent damp: the fraction of remaining distance closed per second is `1 - (1 - speed*dt)^(1/dt)`, which is not constant in `dt`. At the scene's speed of 5 (GameScene.unity:7639), a 30fps machine closes ~15% per frame and a 144fps machine ~3.5%, so the camera visibly lags further behind the player on low-end hardware and the follow feel changes with V-Sync or a frame spike. Because `speed` is an `int`, the only slower setting below 5 is 4 — there is no way to express gentle smoothing at all.

**Как исправить.**

```csharp
Use an exponential damp that is exact under any `deltaTime`:

```csharp
public static void Follow(
    Transform source,
    Transform target,
    float deltaTime,
    float speed,
    Vector3 offset
)
{
    Vector3 desired = target.position + offset;
    float t = 1f - Mathf.Exp(-speed * deltaTime);   // frame-rate independent
    source.position = Vector3.LerpUnclamped(source.position, desired, t);
}
```
With `speed` as `float` (see the `CameraSystemInstaller` fix) the same numeric value now means the same motion at any frame rate. Drop the `in` modifiers on the reference-type parameters — `in Transform` adds a pointer indirection for no benefit — and drop the default arguments, since the only caller passes everything.
```

### Низкая важность (1)

#### LOW-1. `Assets/Game/Scripts/Gameplay/PlayerContext/PlayerUseCase.cs:7` — dead-code

**Проблема.** `PlayerUseCase.GetPlayerContext(GameContext gameContext, IEntity character)` ignores its `character` parameter entirely and returns `gameContext.GetPlayerContext()`. It has exactly one caller (Entity/Content/PickUps/DashPickUpInstaller.cs:21).

**Следствие.** The signature promises per-character resolution — which is exactly what `_playerID` was meant to enable — and delivers a single global context, so a second player's pick-up would silently buff player one. The wrapper also pins the concrete `GameContext`, so the only call site is forced through `GameContext.Instance` (DashPickUpInstaller.cs:12) to obtain an argument that is then thrown away. One indirection, zero behaviour, one misleading contract.

**Как исправить.**

```csharp
Either delete the file and call `gameContext.GetPlayerContext()` directly at DashPickUpInstaller.cs:21, or make it honour its signature once `PlayerID` is installed:

```csharp
public static class PlayerUseCase
{
    public static bool TryGetPlayerContext(IGameContext gameContext, IEntity character, out IPlayerContext result)
    {
        result = null;

        return character.TryGetPlayerID(out PlayerID id) &&
               gameContext.GetPlayerContexts().TryGetValue(id, out result);
    }
}
```
Note `IGameContext` rather than the concrete class, so the pick-up installer can take the context from the entity's world instead of the static singleton.
```

---

## Entity/Common: механики, поведения, use-cases

Audited all 53 .cs files under Assets/Game/Scripts/Gameplay/Entity/Common, cross-checked against the installed Atomic source (Assets/Plugins/Atomic) and the upstream BestPractices docs. The mechanics layer has four classes of real defect, not style problems.

(1) Lifecycle/subscription correctness is the worst area. Five behaviours subscribe in `Init` and declare no `IEntityDispose` at all (AttackSoundBehaviour, HitSoundBehaviour, DeathAnimBehaviour, DiscardAllBuffBehaviour, CameraShakeBehaviour), and TakeDamageBloodBehaviour's `Dispose` calls `Subscribe` twice instead of `Unsubscribe` — so every dispose/re-init cycle doubles the handler count. I verified against `Entity_Behaviours.cs:40-57` that `DelBehaviour` calls `Dispose(entity)` and `AddBehaviour` calls `Init(entity)` on an already-initialized entity, which means StunBuff's remove/re-add cycle drives these paths for real, not hypothetically. That same mechanism makes `EnemyPatrolBehaviour`'s never-cleared `_patrolPoints` list grow on every re-init.

(2) Pooled state is not reset. `GameFactory` is a pool (`SceneEntityPool.Rent/Return`), and `Entity.AddValue` (`Entity_Values.cs:79-88`) returns false without overwriting when the key exists. So `SpawnUseCase.cs:26`'s `bullet.AddDamage(...)` silently no-ops for every recycled bullet: a bullet keeps the damage of the first weapon that ever fired it.

(3) Hard crash and resource-leak paths: `FindTargetUseCase` never returns its rented `ArrayPool` buffer on the success path, and `DamageCastBehaviour` calls `GetDamageRadius()`/`GetDamageLayer()` unconditionally on the current weapon — values that `RangeWeaponInstaller` never adds — so `Entity.GetValue` throws `KeyNotFoundException` from inside `OnUpdate`, killing the whole world tick.

(4) Per-frame cost and gameplay bugs: `EnemyChaseBehaviour` feeds an unnormalized direction into `MoveUseCase.Move`, making chase speed proportional to distance; `CameraShakeBehaviour` re-captures `_originalPosition` from an already-displaced transform, so overlapping shakes permanently drift the camera; `InteractMarkerUIBehaviour` zeroes `.y`/`.z` on a *Quaternion*; `MoveUseCase.Move` re-evaluates the move condition expression that the caller just evaluated; `CharacterMoveAnimBehaviour` uses string animator parameters next to a cached hash and runs twice per frame because one handler is `Observe`d on two variables; `BuffUseCase` allocates LINQ closures and boxed enumerators on paths driven by health changes.

Idiom-wise, the docs are violated in specific, citable ways: `GameContext.Instance` is reached from inside entity behaviours (BulletCollisionBehaviour, InteractMarkerHighlightBehaviour) against Tutorials/6; concrete `BaseEvent<T>`/`Cooldown` types are held where `IReactive<T>`/`IValue` belong, against BestPractices/PreferAbstractInterfaces; no behaviour or installer uses `DisposableComposite`+`AddTo` from BestPractices/UsingSubscriptionsWithDisposeComposite; and `InteractInstaller._triggerInteraction` is an unserialized field that can never be configured.

### Высокая важность (11)

#### HIGH-1. `Assets/Game/Scripts/Gameplay/Entity/Common/TakeDamage/TakeDamageBloodBehaviour.cs:38` — correctness

**Проблема.** `Dispose` calls `Subscribe` on both events instead of `Unsubscribe`, so disposing the behaviour adds a second copy of `OnDamageTaken` to each event rather than removing the first.

**Следствие.** Every dispose/re-init cycle doubles the handler count. `Entity_Behaviours.cs:53` calls `Dispose(entity)` from `DelBehaviour` and `Entity_Behaviours.cs:26` calls `Init(entity)` from `AddBehaviour` on an initialized entity, and `StunBuff.OnApply/OnDiscard` removes and re-adds behaviours on a live character — so after two stuns this entity plays 4 blood bursts per hit, and the handler keeps firing after the behaviour is logically dead, holding the ParticleSystem and Transform alive.

**Как исправить.**

```csharp
public void Dispose(in IEntity entity)
{
    _damageEvent.Unsubscribe(this.OnDamageTaken);
    _deathEvent.Unsubscribe(this.OnDamageTaken);
}
```

#### HIGH-2. `Assets/Game/Scripts/Gameplay/Entity/Common/Sensors/FindTargetUseCase.cs:28` — performance

**Проблема.** `return true` inside the loop (line 28) skips `ArrayPool<Collider>.Shared.Return(colliders)` on line 32. The rented buffer is only returned when no target is found.

**Следствие.** Every successful melee cast permanently leaks a 32-element `Collider[]` out of the shared ArrayPool. `DamageCastBehaviour.OnUpdate` calls this once per frame for the whole ~0.1s cast window, so a few seconds of melee combat drains the pool bucket and `Rent` starts allocating fresh arrays on the GC heap every frame — exactly the allocation the ArrayPool was added to avoid.

**Как исправить.**

```csharp
public static bool TryGetTarget(IEntity weapon, out IEntity target)
{
    target = null;

    Vector3 position = weapon.GetTransform().position;
    float radius = weapon.GetDamageRadius().Value;
    LayerMask damageLayer = weapon.GetDamageLayer();

    Collider[] colliders = ArrayPool<Collider>.Shared.Rent(COLLIDER_BUFFER_SIZE);
    try
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, colliders, damageLayer);

        for (int i = 0; i < count; i++)
        {
            if (colliders[i].TryGetEntity(out IEntity entity) && entity.HasDamageableTag())
            {
                target = entity;
                return true;
            }
        }

        return false;
    }
    finally
    {
        ArrayPool<Collider>.Shared.Return(colliders);
    }
}
```

#### HIGH-3. `Assets/Game/Scripts/Gameplay/Entity/Common/SpawnEntity/SpawnUseCase.cs:26` — correctness

**Проблема.** `bullet.AddDamage(weapon.GetDamage())` uses `Add`, which does not overwrite an existing value. Bullets come from a pool, so the second and every later use of the same bullet instance keeps the damage reference of the first weapon that ever fired it.

**Следствие.** Confirmed in `Assets/Plugins/Atomic/Entities/Scripts/Entity/Plain/Entity_Values.cs:79-88`: `AddValue` does `values.TryAdd(id, value)` and returns false when the key is present — no overwrite, no exception, silent. `GameFactory.Create` rents from `SceneEntityPool` and `GameFactory.Destroy` returns the same instance. So: fire the 10-damage pistol, the bullet returns to the pool, pick up the 50-damage rifle, and the recycled bullet still deals 10. Worse, it holds the *pistol's* `IReactiveVariable<int>`, so an ExtraDamageBuff on the pistol silently buffs rifle bullets.

**Как исправить.**

```csharp
public static IEntity SpawnBullet(IEntity weapon, GameFactory gameFactory, Transform firePoint)
{
    IEntity bullet = gameFactory.Create(weapon.GetBulletPrefab());

    bullet.SetDamage(weapon.GetDamage());   // Set, not Add: pooled instances already hold a value

    Transform bulletTransform = bullet.GetTransform();
    bulletTransform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
    bullet.GetMoveDirection().Value = bulletTransform.forward;
    bullet.GetLifeTime().Reset();

    return bullet;
}
```

#### HIGH-4. `Assets/Game/Scripts/Gameplay/Entity/Common/MeleeAttack/DamageCastBehaviour.cs:39` — correctness

**Проблема.** `DamageCastBehaviour` is installed on every character (`CharacterCoreInstaller.cs:74`) and passes the currently-held weapon to `FindTargetUseCase.TryGetTarget`, which unconditionally calls `weapon.GetDamageRadius()` and `weapon.GetDamageLayer()`. Only melee weapons have those values.

**Следствие.** `MeleeWeaponInstaller.cs:33-34` adds `DamageLayer`/`DamageRadius`; `RangeWeaponInstaller.cs` (lines 33-72) adds neither. `Entity_Values.cs:17-22` makes `GetValue<T>` throw `KeyNotFoundException` for a missing key. The cast window is 0.1s wide and the weapon can change inside it (`PickUpWeaponUseCase` sets `GetWeapon().Value`), so swapping to a firearm mid-swing throws from inside `OnUpdate`. `Entity.OnUpdate` (Entity_Lifecycle.cs:98-117) does not catch, so the exception unwinds the world tick and every entity ordered after this one stops updating for that frame.

**Как исправить.**

```csharp
public void OnUpdate(in IEntity entity, in float deltaTime)
{
    if (_castTimer <= 0f) return;

    _castTimer -= deltaTime;

    IEntity weapon = _weapon.Value;
    if (weapon == null || !weapon.HasDamageRadius() || !weapon.HasDamageLayer())
    {
        _castTimer = 0f;   // not a melee weapon any more: abandon the cast
        return;
    }

    if (FindTargetUseCase.TryGetTarget(weapon, out IEntity target))
    {
        DamageUseCase.TakeDamage(target, new TakeDamageArgs(weapon));
        _castTimer = 0f;
    }
}
```

#### HIGH-5. `Assets/Game/Scripts/Gameplay/Entity/Common/Enemy/EnemyPatrolBehaviour.cs:16` — correctness

**Проблема.** `_patrolPoints` is a readonly instance list that `Init` only ever appends to (line 27). It is never cleared, and the behaviour declares no `IEntityDispose`.

**Следствие.** `Entity_Behaviours.cs:26` runs `Init(entity)` again whenever `AddBehaviour` is called on an already-initialized entity. `StunBuff.CollectAndRemove<EnemyPatrolBehaviour>` removes the behaviour and `StunBuff.OnDiscard` re-adds the same instance, so one stun on a patrolling enemy doubles the point list. After N stuns the enemy walks the same loop N+1 times and `_patrolPoints.Count` no longer matches `_patrolTransfroms.Length`, so the modulo wrap in `PatrolUseCase` stops lining up with the authored route. The same duplication hits any pooled enemy that is re-initialized.

**Как исправить.**

```csharp
public void Init(in IEntity entity)
{
    _moveDirection = entity.GetMoveDirection();
    _rotateDirection = entity.GetRotateDirection();
    _target = entity.GetTarget();
    _patrolTransfroms = entity.GetPatrolPoints();

    _currentPointIndex = 0;
    _patrolPoints.Clear();

    for (int i = 0; i < _patrolTransfroms.Length; i++)
        _patrolPoints.Add(_patrolTransfroms[i].position);
}
```

#### HIGH-6. `Assets/Game/Scripts/Gameplay/Entity/Common/Camera/CameraShakeBehaviour.cs:31` — correctness

**Проблема.** `OnShake` re-reads `_originalPosition = _cameraTransform.localPosition` every time a shake starts, including while a shake is already in progress and the transform is displaced.

**Следствие.** The transform is offset by up to `_shakeStrength` when a shake is running (`ShakeTransformUseCase.ShakePosition` writes `originalPosition + random offset`). A second shot fired inside the shake window captures that displaced position as the new rest position, and the next shake captures that one, and so on. Automatic fire re-triggers the event many times per second, so the camera random-walks away from the rig and never returns — there is no code path that restores the true rest pose.

**Как исправить.**

```csharp
private Vector3 _restPosition;
private bool _restCaptured;

public void Init(in IEntity entity)
{
    _cameraTransform = entity.GetTransform();
    _cameraShakeEvent = entity.GetCameraShakeEvent();
    _cameraShakeEvent.Subscribe(OnShake);

    _restPosition = _cameraTransform.localPosition;
    _restCaptured = true;
}

public void Dispose(in IEntity entity)
{
    _cameraShakeEvent.Unsubscribe(OnShake);

    if (_restCaptured)
        _cameraTransform.localPosition = _restPosition;
}

private void OnShake(CameraShakeArgs args)
{
    // Do NOT re-capture the rest pose: it is already displaced mid-shake.
    _shakeDuration = Mathf.Max(args.ShakeDuration, 0.0001f);
    _shakeStrength = args.ShakeStrength;
    _shakeTimeRemaining = _shakeDuration;
    _isShaking = true;
}

public void OnUpdate(in IEntity entity, in float deltaTime)
{
    if (!_isShaking) return;

    _shakeTimeRemaining -= deltaTime;

    if (_shakeTimeRemaining <= 0f)
    {
        _cameraTransform.localPosition = _restPosition;
        _isShaking = false;
        return;
    }

    ShakeTransformUseCase.ShakePosition(
        _cameraTransform, _restPosition, _shakeStrength, _shakeTimeRemaining / _shakeDuration);
}
```

#### HIGH-7. `Assets/Game/Scripts/Gameplay/Entity/Common/Camera/CameraShakeBehaviour.cs:7` — correctness

**Проблема.** The class declares `IEntityUpdate, IEntityInit` only. It subscribes to the shake event on line 21 and never unsubscribes, because there is no `IEntityDispose`.

**Следствие.** `Entity_Lifecycle.cs:52-54` only calls `Dispose` on behaviours that implement `IEntityDispose`, so the handler stays attached to the camera entity's `BaseEvent<CameraShakeArgs>` forever. The behaviour's captured `_cameraTransform` keeps a destroyed camera alive across a scene reload, and on the next load a second handler is added — each fired shot then runs `OnShake` twice, overwriting the shake state of the live camera from a dead one.

**Как исправить.**

```csharp
public class CameraShakeBehaviour : IEntityInit, IEntityUpdate, IEntityDispose
{
    // ... fields ...

    public void Dispose(in IEntity entity)
    {
        _cameraShakeEvent.Unsubscribe(OnShake);
        _cameraShakeEvent = null;
    }
}
```

#### HIGH-8. `Assets/Game/Scripts/Gameplay/Entity/Common/Enemy/EnemyChaseBehaviour.cs:26` — correctness

**Проблема.** `DirectionUseCase.Get` returns an unnormalized `target.position - source.position`, and that raw vector is written straight into `_moveVector.Value`. `MoveUseCase.Move` then does `direction * speed * deltaTime`.

**Следствие.** Chase speed becomes proportional to distance: an enemy 20m away moves 20x its configured `MoveSpeed`, teleporting through geometry, while an enemy 0.3m away crawls. `EnemyPatrolBehaviour.cs:38-39` normalizes for exactly this reason, so the two movement sources are inconsistent — the bug only shows once a target is acquired. The duplicate `DirectionUseCase.Get(...)` call on line 27 also pays for `_target.Value.GetTransform()` (a dictionary lookup) twice per frame and triggers the reactive animation chain twice.

**Как исправить.**

```csharp
public void OnUpdate(in IEntity entity, in float deltaTime)
{
    IEntity target = _target.Value;
    if (target == null)
        return;

    Vector3 direction = DirectionUseCase.Get(_enemy, target.GetTransform());
    direction.y = 0f;

    Vector3 normalized = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;

    _moveVector.Value = normalized;
    _rotateVector.Value = normalized;
}
```

#### HIGH-9. `Assets/Game/Scripts/Gameplay/Entity/Common/Interact/InteractMarkerUIBehaviour.cs:51` — correctness

**Проблема.** `transformRotation.z = 0; transformRotation.y = 0;` zeroes raw components of a *Quaternion*, not Euler angles, producing a non-unit quaternion that is then assigned to `transform.rotation`.

**Следствие.** Quaternion components are not independent axis rotations — (x, 0, 0, w) with the original x and w is not 'the same rotation with yaw and roll removed', and its magnitude is no longer 1. The marker ends up at an arbitrary pitch that changes nonlinearly as the camera orbits, and Unity renormalizes the garbage silently so there is no error to chase. The intent is clearly a yaw-only billboard.

**Как исправить.**

```csharp
public void OnLateUpdate(in IEntity entity, in float deltaTime)
{
    if (!_view.activeSelf)
        return;

    Vector3 toCamera = _view.transform.position - _cameraTransform.position;
    toCamera.y = 0f;

    if (toCamera.sqrMagnitude < 1e-6f)
        return;

    _view.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
}

// and in Init, cache the transform instead of a dictionary lookup per frame:
// _cameraTransform = _gameContext.GetPlayerContext().GetCamera().GetTransform();
```

#### HIGH-10. `Assets/Game/Scripts/Gameplay/Entity/Common/Buffs/DiscardAllBuffBehaviour.cs:17` — correctness

**Проблема.** Subscribes to `Health.OnHealthChanged` in `Init` with no `IEntityDispose` to unsubscribe, and `OnHealthChanged` is the wrong signal: it fires on every heal and every point of damage, not on death.

**Следствие.** Two defects in four lines. (a) The handler is never removed, and it captures `_character`, so a dead or pooled entity stays reachable from the live `Health` instance and keeps running `DiscardAll` on it. (b) `Health.Reduce` (Modules/Health/Health.cs:90) raises `OnHealthChanged` for *any* nonzero damage and `Health.Add` (line 71) raises it on heal — so a single point of chip damage, or picking up a health pack, strips every buff the player paid for. `Health` exposes `OnHealthEmpty` (line 13), which is what 'discard all buffs' means.

**Как исправить.**

```csharp
public sealed class DiscardAllBuffBehaviour : IEntityInit, IEntityDispose
{
    private IEntity _character;
    private Health _health;

    public void Init(in IEntity entity)
    {
        _character = entity;
        _health = entity.GetHealth();
        _health.OnHealthEmpty += DiscardAllBuffs;
    }

    public void Dispose(in IEntity entity)
    {
        _health.OnHealthEmpty -= DiscardAllBuffs;
        _health = null;
        _character = null;
    }

    private void DiscardAllBuffs() => BuffUseCase.DiscardAll(_character);
}
```

#### HIGH-11. `Assets/Game/Scripts/Gameplay/Entity/Common/Death/DeathBehaviour.cs:31` — correctness

**Проблема.** `OnHealthEmpty` calls `_entity.Disable()` and `_gameContext.GetEntityWorld().Del(_entity)` but never `Dispose()`, so no `IEntityDispose` behaviour on the dying entity ever runs. The GameObject is also never returned to the pool.

**Следствие.** Verified in `Assets/Plugins/Atomic/Entities/Scripts/World/Plain/EntityWorld.cs:92-106`: `Del` removes the entity from the world dictionary and strips its tags/values but does not call `Dispose()`. So every `IEntityDispose` in this audit stays subscribed on a corpse: `DeathBehaviour` itself keeps its own `OnHealthEmpty` handler on the `Health` object, `DamageCastBehaviour` keeps its `AnimationEventReceiver.OnEvent` handler, `SwitchAnimatorBehaviour` keeps its weapon observer. Every enemy killed leaks its whole behaviour graph plus its GameObject — nothing calls `GameFactory.Destroy` on it. Lines 39-40 also look up `GetEntityWorld()` twice, and `Has` then `Del` is two dictionary probes where `Del`'s own bool return already says whether it was there.

**Как исправить.**

```csharp
private void OnHealthEmpty()
{
    if (_entity.TryGetDeathTakenEvent(out var deathEvent))
        deathEvent.Invoke(new TakeDamageArgs(_entity));

    if (_entity.GetGameObject().TryGetComponent(out CapsuleCollider collider))
        collider.enabled = false;

    _entity.Disable();
    _entity.Dispose();                       // run every IEntityDispose before letting go

    _gameContext.GetEntityWorld().Del(_entity);   // Del already no-ops when absent
}
```

### Средняя важность (12)

#### MEDIUM-1. `Assets/Game/Scripts/Gameplay/Entity/Common/Fire/AttackSoundBehaviour.cs:22` — correctness

**Проблема.** `_fireEvent.Subscribe(PlaySound)` in `Init` with no `IEntityDispose`. Same defect in `Hit/HitSoundBehaviour.cs:22` (`_health.OnHealthChanged += PlayHitSound`) and `Death/DeathAnimBehaviour.cs:18` (`_deathTakenEvent.Subscribe(OnDeath)`).

**Следствие.** These behaviours live on weapons, which are swapped at runtime: `PickUpWeaponUseCase.cs:22` reassigns `GetWeapon().Value` and `DropWeaponUseCase.cs:19` destroys the old weapon GameObject. The behaviour's handler stays on the weapon entity's `BaseEvent`, holding the `AudioSource` and `AudioClip[]` of a destroyed object. When the same weapon prefab is re-installed (or re-added by `StunBuff.OnDiscard`, which calls `AddBehaviour` -> `Init` per Entity_Behaviours.cs:26) a second handler is added and every shot plays two overlapping clips at double volume.

**Как исправить.**

```csharp
public sealed class AttackSoundBehaviour : IEntityInit, IEntityDispose
{
    private readonly AudioClip[] _attackClips;
    private readonly AudioSource _audioSource;
    private IReactive _fireEvent;

    public AttackSoundBehaviour(AudioClip[] attackClips, AudioSource audioSource)
    {
        _attackClips = attackClips;
        _audioSource = audioSource;
    }

    public void Init(in IEntity entity)
    {
        _fireEvent = entity.GetFireEvent();
        _fireEvent.Subscribe(PlaySound);
    }

    public void Dispose(in IEntity entity)
    {
        _fireEvent.Unsubscribe(PlaySound);
        _fireEvent = null;
    }

    private void PlaySound()
    {
        if (_attackClips.Length == 0) return;
        _audioSource.PlayOneShot(_attackClips[Random.Range(0, _attackClips.Length)]);
    }
}
```

#### MEDIUM-2. `Assets/Game/Scripts/Gameplay/Entity/Common/TakeDamage/DamageUseCase.cs:10` — correctness

**Проблема.** `Reduce` is called before `DamageTakenEvent.Invoke`, so on a killing blow the whole death sequence runs first and the damage event fires afterwards, on an entity that is already disabled and removed from the world.

**Следствие.** `Health.Reduce` (Modules/Health/Health.cs:88-93) raises `OnHealthChanged` and then `OnHealthEmpty` synchronously. `OnHealthEmpty` reaches `DeathBehaviour.OnHealthEmpty`, which invokes `DeathTakenEvent` and calls `Disable()` + `Del()`. Only then does line 11 here fire `DamageTakenEvent`. Observable result: death blood and death sound play, then the ordinary hit blood and hit sound play on top of the corpse. It also means `TakeDamageBloodBehaviour`/`TakeDamageSoundBehaviour` run on an entity the world no longer owns. There is no reason for damage feedback to trail death feedback.

**Как исправить.**

```csharp
public static void TakeDamage(IEntity target, in TakeDamageArgs args)
{
    IEntity source = args.Source;
    if (source == null || !target.TryGetHealth(out Health health))
        return;

    int damage = source.GetDamage().Value;

    // Raise the damage event BEFORE health can reach zero and start the death sequence.
    if (target.TryGetDamageTakenEvent(out var damageEvent))
        damageEvent.Invoke(args);

    health.Reduce(damage);
}
```

#### MEDIUM-3. `Assets/Game/Scripts/Gameplay/Entity/Common/Animator/SwitchAnimatorUseCase.cs:11` — correctness

**Проблема.** `animator.Play("Idle")` is called on line 11 and `animator.Rebind()` on line 12. `Rebind` resets the animator to its default state, discarding the `Play` that was just issued.

**Следствие.** The ordering makes line 11 dead: `Rebind` re-binds all properties and resets the state machine, so the explicit jump to Idle never takes effect and the new controller starts in whatever its default state is. On a weapon swap mid-animation that shows as a one-frame pose pop. The `"Idle"` string literal is also hashed on every swap and silently does nothing if the new controller has no state by that name.

**Как исправить.**

```csharp
private static readonly int IDLE = Animator.StringToHash("Idle");

public static void Switch(Animator animator, IEntity weapon)
{
    if (animator == null || weapon == null)
        return;

    if (!weapon.TryGetAnimationController(out RuntimeAnimatorController controller))
        return;

    animator.runtimeAnimatorController = controller;
    animator.Rebind();          // reset first
    animator.Play(IDLE);        // then choose the state
    animator.Update(0f);        // and flush it this frame
}
```

#### MEDIUM-4. `Assets/Game/Scripts/Gameplay/Entity/Common/Animator/SwitchAnimatorBehaviour.cs:17` — correctness

**Проблема.** `_weapon.Observe(OnWeaponChanged)` invokes the handler immediately with the current value, and `OnWeaponChanged` passes it to `SwitchAnimatorUseCase.Switch` with no null check.

**Следствие.** Confirmed in `Assets/Plugins/Atomic/Elements/Scripts/Values/Extensions.cs:12-16`: `Observe` does `Subscribe(action)` then `action.Invoke(it.Value)`. `GetWeapon()` is an `IReactiveVariable<IEntity>` that `DropWeaponUseCase.cs:22` and `PickUpWeaponUseCase.cs:22` reassign at runtime, and any entity installed without a starting weapon has `Value == null` at `Init`. The immediate invoke then dereferences null inside `weapon.GetAnimationController()`, throwing out of `Entity.Init` and aborting the remaining behaviours' `Init` (Entity_Lifecycle.cs:36-39) — the entity comes up half-installed with no error pointing here. `Observe` also returns void in this framework version, so there is no subscription handle to dispose; BestPractices/UsingObserveWithReactiveValues wants the handle stored.

**Как исправить.**

```csharp
public sealed class SwitchAnimatorBehaviour : IEntityInit, IEntityDispose
{
    private Animator _animator;
    private IReactiveVariable<IEntity> _weapon;

    public void Init(in IEntity entity)
    {
        _animator = entity.GetAnimator();
        _weapon = entity.GetWeapon();

        _weapon.Subscribe(OnWeaponChanged);
        OnWeaponChanged(_weapon.Value);     // explicit priming, guarded below
    }

    public void Dispose(in IEntity entity)
    {
        _weapon.Unsubscribe(OnWeaponChanged);
        _weapon = null;
        _animator = null;
    }

    private void OnWeaponChanged(IEntity weapon)
    {
        if (weapon == null) return;
        SwitchAnimatorUseCase.Switch(_animator, weapon);
    }
}
```

#### MEDIUM-5. `Assets/Game/Scripts/Gameplay/Entity/Common/Buffs/BuffUseCase.cs:14` — performance

**Проблема.** `buffsEffects.Any(b => b.Name == buff.Name)` on line 14 (and `.Any` on line 27, `.FirstOrDefault` on line 37) runs LINQ over an `IList<BuffBase>` interface, allocating a closure object, a delegate and a boxed enumerator per call. Line 53's `foreach` over the interface boxes the enumerator too.

**Следствие.** `ReactiveList.GetEnumerator()` (Assets/Plugins/Atomic/Elements/Scripts/Collections/Implementations/ReactiveList.cs:165-168) returns `IEnumerator<T>`, so every enumeration heap-allocates even without LINQ. These paths are driven per-frame and per-hit: `TemporaryBuff.OnFixedUpdate` calls `Discard` (line 37, `FirstOrDefault`) every fixed frame once a buff expires, and `DiscardAllBuffBehaviour` calls `DiscardAll` (line 53, boxed `foreach`) on every health change. With a handful of buffs on a player under sustained fire that is steady garbage in FixedUpdate for work that is a 4-element linear scan. Comparing by `string` name rather than an id also means a string compare per element.

**Как исправить.**

```csharp
public static bool Apply(IEntity character, BuffBase buff)
{
    IReactiveList<BuffBase> buffs = character.GetBuffsEffects();

    if (IndexOf(buffs, buff.Name) >= 0)
        return false;

    buffs.Add(buff);
    buff.Apply(character);
    return true;
}

public static bool CanApply(IEntity character, BuffConfig buff) =>
    IndexOf(character.GetBuffsEffects(), buff.Name) < 0;

public static bool Discard(IEntity character, string buffName)
{
    IReactiveList<BuffBase> buffs = character.GetBuffsEffects();

    int index = IndexOf(buffs, buffName);
    if (index < 0)
        return false;

    BuffBase buff = buffs[index];
    buffs.RemoveAt(index);
    buff.Discard(character);
    return true;
}

public static void DiscardAll(IEntity character)
{
    IReactiveList<BuffBase> buffs = character.GetBuffsEffects();

    // Reverse indexed walk: no enumerator allocation, and safe if Discard mutates the list.
    for (int i = buffs.Count - 1; i >= 0; i--)
    {
        BuffBase buff = buffs[i];
        buffs.RemoveAt(i);
        buff.Discard(character);
    }
}

private static int IndexOf(IReactiveList<BuffBase> buffs, string name)
{
    for (int i = 0, count = buffs.Count; i < count; i++)
        if (string.Equals(buffs[i].Name, name, System.StringComparison.Ordinal))
            return i;

    return -1;
}
```

#### MEDIUM-6. `Assets/Game/Scripts/Gameplay/Entity/Common/Move/MoveUsecase.cs:17` — performance

**Проблема.** `Move` re-evaluates the move condition (`entity.TryGetMoveCondition(out var condition) && !condition.Value`) that its only real caller, `CharacterMoveBehaviour.OnUpdate` (line 20), has already evaluated one line earlier.

**Следствие.** `MoveCondition` is an `AndExpression`, and `CharacterCoreInstaller.cs:58-61` builds it as `health.Exists` plus a closure `() => entity.GetWeapon().Value.GetMoveCondition().Invoke()`. Evaluating it costs a delegate invoke per clause plus two dictionary lookups plus a nested expression evaluation. Doing it twice per entity per frame, plus the extra `TryGetMoveCondition` dictionary probe, doubles that for nothing. The condition can also change between the two checks if a clause has side effects, so the duplicate is not even harmless.

**Как исправить.**

```csharp
public static class MoveUseCase
{
    /// <summary>Moves the entity. The CALLER is responsible for the move condition.</summary>
    public static void Move(this IEntity entity, in Vector3 direction, in float deltaTime)
    {
        if (direction == Vector3.zero)
            return;

        Transform transform = entity.GetTransform();
        float speed = entity.GetMoveSpeed().Value;

        transform.position += direction * (speed * deltaTime);
    }
}

// CharacterMoveBehaviour keeps the single authoritative check it already has:
// if (!_moveCondition.Value) return;
// MoveUseCase.Move(entity, _moveDirection.Value, deltaTime);
```

#### MEDIUM-7. `Assets/Game/Scripts/Gameplay/Entity/Common/Move/CharacterMoveAnimBehaviour.cs:60` — performance

**Проблема.** `_animator.SetFloat("MoveX", ...)` and `SetFloat("MoveY", ...)` on lines 60-61 and 68-69 use string parameter names, in a class that already caches `IS_MOVING` as a hash on line 9. The same handler is also `Observe`d on two different variables (lines 25-26), so it runs twice per change batch.

**Следствие.** Each string overload hashes the name on every call. `EnemyChaseBehaviour.OnUpdate` writes both `_moveVector.Value` and `_rotateVector.Value` every frame, and both are observed by `OnDirectionChanged`, so `UpdateAnimation` runs twice per frame per enemy — that is 4 string hashes plus two `_character.GetMoveCondition().Invoke()` expression evaluations (line 50) plus a `Quaternion.Inverse` and a quaternion-vector multiply, all duplicated. The duplicated branches on lines 59-61 and 67-69 are also identical apart from how `localDirection` is computed.

**Как исправить.**

```csharp
private static readonly int IS_MOVING = Animator.StringToHash("IsMoving");
private static readonly int MOVE_X = Animator.StringToHash("MoveX");
private static readonly int MOVE_Y = Animator.StringToHash("MoveY");

private void UpdateAnimation()
{
    Vector3 direction = _moveDirection.Value;

    if (direction == Vector3.zero || !_moveCondition.Value)
    {
        _animator.SetBool(IS_MOVING, false);
        return;
    }

    Vector3 rotateDir = _rotateDirection.Value;

    Vector3 localDirection = rotateDir != Vector3.zero
        ? Quaternion.Inverse(Quaternion.LookRotation(rotateDir)) * direction
        : _characterTransform.InverseTransformDirection(direction);

    _animator.SetBool(IS_MOVING, true);
    _animator.SetFloat(MOVE_X, localDirection.x);
    _animator.SetFloat(MOVE_Y, localDirection.z);
}

// Cache the expression in Init instead of re-resolving it, and prime once:
// _moveCondition = entity.GetMoveCondition();
// _moveDirection.Subscribe(OnDirectionChanged);
// _rotateDirection.Subscribe(OnDirectionChanged);
// UpdateAnimation();
```

#### MEDIUM-8. `Assets/Game/Scripts/Gameplay/Entity/Common/Enemy/EnemyAttackBehaviour.cs:28` — performance

**Проблема.** `Init` caches `_target` and `_enemy` but `OnUpdate` still resolves `entity.GetWeapon()`, `.GetFireCondition()` and `entity.GetFireAction()` through the value dictionary on every frame (lines 28-29), and line 24 uses `Vector3.Distance` (a square root) where a squared comparison would do.

**Следствие.** Three dictionary lookups plus an unboxing cast per enemy per frame, for references that never change after install — the class already has an `Init` for exactly this. `_target.Value.GetTransform()` on line 24 is a fourth. With a crowd of enemies this is the hot loop. Line 28 also dereferences `entity.GetWeapon().Value` with no null guard, and the hardcoded `ATTACK_DISTANCE = 1` (line 10) means attack range cannot be tuned per enemy — the docs want that as entity data.

**Как исправить.**

```csharp
public sealed class EnemyAttackBehaviour : IEntityInit, IEntityUpdate
{
    private IReactiveVariable<IEntity> _target;
    private IReactiveVariable<IEntity> _weapon;
    private IAction _fireAction;
    private Transform _enemy;
    private float _sqrAttackDistance;

    public void Init(in IEntity entity)
    {
        _target = entity.GetTarget();
        _weapon = entity.GetWeapon();
        _fireAction = entity.GetFireAction();
        _enemy = entity.GetTransform();

        float range = entity.TryGetAttackDistance(out IValue<float> d) ? d.Value : 1f;
        _sqrAttackDistance = range * range;
    }

    public void OnUpdate(in IEntity entity, in float deltaTime)
    {
        IEntity target = _target.Value;
        if (target == null) return;

        IEntity weapon = _weapon.Value;
        if (weapon == null) return;

        if ((target.GetTransform().position - _enemy.position).sqrMagnitude >= _sqrAttackDistance)
            return;

        if (weapon.GetFireCondition().Invoke())
            _fireAction.Invoke();
    }
}
```

#### MEDIUM-9. `Assets/Game/Scripts/Gameplay/Entity/Common/Bullet/BulletCollisionBehaviour.cs:15` — idiom-violation

**Проблема.** `GameContext.Instance.GetGameFactory()` is reached from inside a behaviour's `Init`. The same pattern is in `Interact/InteractMarkerHighlightBehaviour.cs:15`, which additionally caches `GetCharacter().Value.GetTargetInteractable()` once at `Init`.

**Следствие.** Docs/Tutorials/6 'Minimizing Unity' makes the context an explicitly constructed object handed down from the entry point, not a static singleton — and the rest of this codebase already does it properly (`DeathBehaviour` and `InteractMarkerUIBehaviour` both take `GameContext` by constructor). Reaching the singleton inside a behaviour makes the behaviour untestable without a live scene and breaks under additive scene loading or a second context. In `InteractMarkerHighlightBehaviour` it is worse than style: `GetCharacter().Value` is captured once, so after the player respawns the behaviour is still subscribed to the dead character's `TargetInteractable` and highlights nothing.

**Как исправить.**

```csharp
public sealed class BulletCollisionBehaviour : IEntityInit, IEntityDispose
{
    private readonly GameFactory _gameFactory;

    private CollisionEventReceiver _collisionEventReceiver;
    private IEntity _bullet;

    public BulletCollisionBehaviour(GameFactory gameFactory) => _gameFactory = gameFactory;

    public void Init(in IEntity entity)
    {
        _bullet = entity;
        _collisionEventReceiver = entity.GetCollisionReceiver();
        _collisionEventReceiver.OnEntered += OnCollision;
    }

    public void Dispose(in IEntity entity)
    {
        _collisionEventReceiver.OnEntered -= OnCollision;
        _collisionEventReceiver = null;
        _bullet = null;
    }
    // ...
}

// BulletInstaller already holds the factory (BulletInstaller.cs:21), so pass it:
// entity.AddBehaviour(new BulletCollisionBehaviour(_factory));
```

#### MEDIUM-10. `Assets/Game/Scripts/Gameplay/Entity/Common/Bullet/BulletCollisionBehaviour.cs:23` — correctness

**Проблема.** `Destroy` has no re-entry guard. It is a raw `OnEntered` handler that applies damage, spawns a hit effect and returns the bullet to the pool, and nothing stops it running twice for one bullet.

**Следствие.** Unity delivers every `OnCollisionEnter` of a physics step, so a bullet that touches two colliders in the same step runs this twice: two hit VFX spawned, and if both are damageable, full damage applied twice from one projectile. `SceneEntityPool.Return` (Assets/Plugins/Atomic/Entities/Scripts/Pooling/SceneEntityPool.cs:42) only prevents the duplicate *enqueue*, via an O(n) `_queue.Contains` linear scan — it does not stop the damage or the VFX. The bullet is also never `Dispose()`d before going back to the pool, so this handler stays attached while the instance sits in the queue.

**Как исправить.**

```csharp
private bool _consumed;

public void Init(in IEntity entity)
{
    _bullet = entity;
    _consumed = false;                      // reset pooled state on every rent
    _collisionEventReceiver = entity.GetCollisionReceiver();
    _collisionEventReceiver.OnEntered += OnCollision;
}

private void OnCollision(Collision collision)
{
    if (_consumed) return;
    _consumed = true;

    if (collision.gameObject.TryGetComponent(out IEntity target) && target.HasDamageableTag())
        DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));
    else
        SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);

    SpawnUseCase.UnSpawnEntity(_gameFactory, _bullet);
}
```

#### MEDIUM-11. `Assets/Game/Scripts/Gameplay/Entity/Common/Interact/InteractInstaller.cs:15` — dead-code

**Проблема.** `private QueryTriggerInteraction _triggerInteraction;` has no `[SerializeField]`, unlike the three fields above it. It is never assigned, so it is always `QueryTriggerInteraction.UseGlobal` and is passed straight into `Physics.OverlapSphereNonAlloc`.

**Следствие.** The field exists only to be configured, and it cannot be — the Inspector never shows it and no code writes it. Interactable pickups are normally trigger colliders, so the query's trigger behaviour silently falls back to the project-wide `Physics.queriesHitTriggers` setting. If a designer turns that off globally for some unrelated reason, interaction detection stops working project-wide with nothing in this installer to explain why.

**Как исправить.**

```csharp
[Serializable]
public sealed class InteractInstaller : IEntityInstaller
{
    [SerializeField] private Transform _center;
    [SerializeField] private float _radius = 1.5f;
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

    public void Install(IEntity entity)
    {
        entity.AddTargetInteractable(new ReactiveVariable<IEntity>());
        entity.AddBehaviour(new DetectInteractableBehaviour(_center, _layerMask, _radius, _triggerInteraction));
    }
}
```

#### MEDIUM-12. `Assets/Game/Scripts/Gameplay/Entity/Common/TakeDamage/TakeDamageArgs.cs:5` — performance

**Проблема.** `TakeDamageArgs` is a mutable reference type with a public field, and a fresh instance is heap-allocated at every damage site: `DamageCastBehaviour.cs:41`, `BulletCollisionBehaviour.cs:26`, `DeathBehaviour.cs:34`.

**Следствие.** It carries one reference. As a class it costs a 24-byte allocation per hit, and `DamageCastBehaviour` can allocate one per frame while a melee cast is live and a target is in range. The public mutable `source` field also means any subscriber of `DamageTakenEvent` can rewrite the damage source for every later subscriber in the same dispatch — the events are plain `BaseEvent<T>` multicast delegates, so handler order decides what the rest of the chain sees.

**Как исправить.**

```csharp
public readonly struct TakeDamageArgs
{
    public readonly IEntity Source;

    public TakeDamageArgs(IEntity source) => Source = source;
}

// Call sites stay source-compatible; update the one field reference in
// DamageUseCase (args.source -> args.Source) and TakeDamageBloodBehaviour.cs:44.
```

### Низкая важность (7)

#### LOW-1. `Assets/Game/Scripts/Gameplay/Entity/Common/Interact/InteractUseCase.cs:49` — performance

**Проблема.** `Vector3.Distance(center, position)` computes a square root for each of up to 32 candidate colliders, purely to find the minimum. `DetectInteractableBehaviour` calls this every FixedUpdate.

**Следствие.** Square root per candidate, 50 times a second, to rank values whose ordering is identical under squaring. `other.GetTransform()` is also a dictionary lookup plus unboxing cast per candidate. On a dense pickup pile that is 32 sqrts plus 32 dictionary probes per physics step, for a ranking that needs neither.

**Как исправить.**

```csharp
float minSqrDistance = float.MaxValue;
target = null;

for (int i = 0; i < count; i++)
{
    if (!colliders[i].TryGetEntity(out IEntity other) || !other.HasInteractableTag())
        continue;

    float sqrDistance = (other.GetTransform().position - center).sqrMagnitude;

    if (sqrDistance >= minSqrDistance)
        continue;

    target = other;
    minSqrDistance = sqrDistance;
}
```

#### LOW-2. `Assets/Game/Scripts/Gameplay/Entity/Common/Fire/CharacterFireAction.cs:22` — performance

**Проблема.** The constructor caches `_weapon = _character.GetWeapon()` (line 17), but `Invoke` still calls `_character.GetWeapon().Value` on line 22 and then uses `_weapon.Value` on lines 24, 28 and 31 — four reads of the same variable, one of them through a redundant dictionary lookup. `_character.GetAnimator()` is looked up again on lines 29 and 32. The `_fire`/`_melee` hashes on lines 9-10 are per-instance rather than static.

**Следствие.** This runs on every shot, including full-auto fire from every character and enemy. Per shot: one wasted `GetWeapon` dictionary probe plus unboxing cast, and up to two `GetAnimator` probes. The `Animator.StringToHash` pair is also recomputed for every character instance created, when the result is a compile-time constant of the string.

**Как исправить.**

```csharp
public sealed class CharacterFireAction : IAction
{
    private static readonly int FIRE = Animator.StringToHash("Fire");
    private static readonly int MELEE = Animator.StringToHash("Melee");

    private readonly IReactiveVariable<IEntity> _weapon;
    private readonly Animator _animator;

    public CharacterFireAction(IEntity character)
    {
        _weapon = character.GetWeapon();
        _animator = character.GetAnimator();
    }

    public void Invoke()
    {
        IEntity weapon = _weapon.Value;
        if (weapon == null) return;

        if (!weapon.GetFireCondition().Invoke()) return;

        weapon.GetFireAction().Invoke();

        if (weapon.HasRangeWeaponTag())
            _animator.Play(FIRE, 1);
        else if (weapon.HasMeleeWeaponTag())
            _animator.Play(MELEE, 2);
    }
}
```

#### LOW-3. `Assets/Game/Scripts/Gameplay/Entity/Common/MeleeAttack/DamageCastBehaviour.cs:29` — idiom-violation

**Проблема.** The animation event name `"damage_cast_event"` is a bare inline string literal compared on every animation event. The equivalent strings elsewhere are at least named constants (`BodyFallSoundBehaviour`'s `FALL_ANIM_EVENT`, `AttackSoundBehaviour`'s `ATTACK_EVENT`).

**Следствие.** `AnimationEventReceiver.OnEvent` fires for *every* animation event on the entity, so this string compare runs on all of them, and a typo in either the animation clip or this literal fails silently — the melee attack simply never lands, with nothing to debug. Docs/BestPractices/SharedConstants exists precisely to centralise these; the inconsistency with the two sibling behaviours means a rename cannot be done by searching for a symbol.

**Как исправить.**

```csharp
// In a shared constants type (per BestPractices/SharedConstants.md):
public static class AnimEvents
{
    public const string DamageCast = "damage_cast_event";
    public const string BodyFall   = "body_fall_event";
    public const string AttackStart = "start_attack_event";
}

// Then:
private void OnDamageCast(string eventName)
{
    if (string.Equals(eventName, AnimEvents.DamageCast, System.StringComparison.Ordinal))
        _castTimer = CAST_TIME;
}
```

#### LOW-4. `Assets/Game/Scripts/Gameplay/Entity/Common/LifeTime/LifeTimeBehaviour.cs:9` — idiom-violation

**Проблема.** `private Cooldown _lifeTime;` holds the concrete `Modules.Gameplay.Cooldown` class. `CameraShakeBehaviour.cs:10` and `DeathAnimBehaviour.cs:10` likewise hold concrete `BaseEvent<T>` where `IReactive<T>`/`IEvent<T>` is available.

**Следствие.** Docs/BestPractices/PreferAbstractInterfaces states the rule directly and gives this exact incorrect/correct pair. The concrete types pin these mechanics to one implementation: the behaviour cannot be unit-tested with a stub clock or a stub event, and the upstream framework being migrated to replaces this `Cooldown` and `BaseEvent` outright (`IEntityUpdate` is now `IEntityTick`, events are `ISignal<T>`), so every concrete reference is a port site that an interface reference would not be.

**Как исправить.**

```csharp
public sealed class LifeTimeBehaviour : IEntityInit, IEntityUpdate
{
    private ICooldown _lifeTime;      // or IValue<float>/ITimer in the new framework
    private IAction _destroyAction;

    public void Init(in IEntity entity)
    {
        _lifeTime = entity.GetLifeTime();
        _destroyAction = entity.GetDestroyAction();
    }

    public void OnUpdate(in IEntity entity, in float deltaTime)
    {
        _lifeTime.Tick(deltaTime);

        if (_lifeTime.IsExpired())
            _destroyAction.Invoke();
    }
}

// CameraShakeBehaviour: private IReactive<CameraShakeArgs> _cameraShakeEvent;
// DeathAnimBehaviour:   private IReactive<TakeDamageArgs>  _deathTakenEvent;
```

#### LOW-5. `Assets/Game/Scripts/Gameplay/Entity/Common/Move/CharacterVelocityBehaviour.cs:22` — performance

**Проблема.** `OnUpdate` reads `_characterTransform.position` twice (lines 22 and 24) and uses `Vector3.Distance`, which is a square root, to produce a scalar that is only ever compared against a threshold.

**Следствие.** `Transform.position` is a native property call that walks the transform hierarchy — doing it twice per character per frame is a measurable managed-to-native round trip for a value that cannot change mid-method. The `Velocity` this writes is consumed by `MoveStepSoundBehaviour.cs:31` as `_velocity.Value < TOLERANCE`, a pure threshold test, so the square root is thrown away. Writing `_velocity.Value` unconditionally every frame also fires the reactive change notification every frame even when the character is standing still.

**Как исправить.**

```csharp
public void OnUpdate(in IEntity entity, in float deltaTime)
{
    if (deltaTime <= 0f)
        return;

    Vector3 position = _characterTransform.position;     // read once
    float speed = (position - _lastFramePosition).magnitude / deltaTime;
    _lastFramePosition = position;

    // Avoid re-notifying every subscriber on imperceptible changes.
    if (!Mathf.Approximately(_velocity.Value, speed))
        _velocity.Value = speed;
}
```

#### LOW-6. `Assets/Game/Scripts/Gameplay/Entity/Common/Interact/InteractMarkerUIBehaviour.cs:36` — correctness

**Проблема.** `OnTargetChanged(IEntity item)` branches on `_targetInteractable.Value != null` but then dereferences the `item` parameter on line 39.

**Следствие.** The guard tests a different object from the one it protects. `DetectInteractableBehaviour.OnFixedUpdate` writes `_target.Value` every physics step, so a write that happens re-entrantly during this dispatch (or any future second writer) leaves `Value` non-null while `item` is null, and line 39's `item.GetTransform()` throws from inside the reactive callback. The parameter is the value the event delivered and is the only thing this handler should read.

**Как исправить.**

```csharp
private void OnTargetChanged(IEntity item)
{
    if (item == null)
    {
        _view.SetActive(false);
        return;
    }

    _view.SetActive(true);
    _view.transform.position = item.GetTransform().position + _positionOffset;
}
```

#### LOW-7. `Assets/Game/Scripts/Gameplay/Entity/Common/Camera/CameraFollowUseCase.cs:15` — correctness

**Проблема.** `Vector3.Lerp(source.position, target.position + offset, speed * deltaTime)` uses an unclamped frame-rate-dependent factor as the interpolant, and `speed` is typed `int` so follow smoothing cannot be tuned finer than whole units.

**Следствие.** `speed * deltaTime` is not a valid Lerp parameter: at the default `speed = 5` and 30fps it is 0.167, at 144fps it is 0.035 — the camera lags noticeably more on fast hardware, which is the opposite of the intent. Above `speed = 60` the product exceeds 1 on a hitching frame and `Vector3.Lerp` clamps, snapping the camera. `CameraFollowController.OnLateUpdate` calls this every LateUpdate via `IValue<int> _speed`. The `in` modifiers on `Transform` parameters are also pointless — `Transform` is already a reference — and `in float deltaTime` pessimizes a 4-byte copy into an indirection.

**Как исправить.**

```csharp
public static void Follow(
    Transform source,
    Transform target,
    float deltaTime,
    float sharpness = 5f,
    Vector3 offset = default
)
{
    Vector3 desired = target.position + offset;

    // Exponential smoothing: identical motion at any frame rate, never overshoots.
    float t = 1f - Mathf.Exp(-sharpness * deltaTime);

    source.position = Vector3.LerpUnclamped(source.position, desired, t);
}

// Change IPlayerContext's CameraSpeed to IValue<float> so it can be tuned below 1.
```

---

## Entity/Content: инсталлеры, фабрика, пулы, оружие

Audited all 26 .cs files under Gameplay/Entity/Content plus every collaborator they construct (behaviours, use-cases, GameFactory, WeaponCatalog, Health/Ammo/Cooldown modules) and cross-checked against the old bundled Atomic plugin source and the 29 upstream BestPractices docs.

The Content layer has three structural problems that produce most of the concrete defects:

1. **Every installer is an entry point into global state.** `GameContext.Instance` is reached from 12 sites inside entity installers (AudioSourceInstaller:19, BulletInstaller:21, HitInstaller:15, ShellInstaller:17, CharacterCoreInstaller:38, EnemyCoreInstaller:35, MarkerInstaller:16, MeleeWeaponInstaller:22, RangeWeaponInstaller:35, WeaponPickUpInstaller:17, BuffPickUpInstaller:24, DashPickUpInstaller:12). Nothing under Content is testable or reusable, and `BulletCollisionBehaviour.Init` resolves the singleton per bullet instance.

2. **The pooling layer is incorrect, not just slow.** `GameFactory` keys pools by `gameObject.name.Replace("(Clone)","")` (a fresh string marshalled from native per spawn), does `ContainsKey`+indexer double lookups, stores pools in a `ReactiveDictionary` nobody observes, and — worst — registers a *live, already-mutated instance* as a pool prefab when an id is unknown, via `entity.GetTransform().GetComponent<SceneEntity>()`, which is `null` for every character/enemy because those install `_characterRoot` as their Transform. `EnemyCoreInstaller` never calls `AddEntityID` at all, so returning a pooled enemy throws `KeyNotFoundException`. Pooled bullets keep the first shooter's damage forever because `Entity.AddValue` returns `false` on an existing key (Entity_Values.cs:79) and `SpawnUseCase.SpawnBullet` uses `AddDamage`, not `SetDamage`.

3. **Character and Enemy core installers are the same file twice.** ~40 lines are duplicated verbatim (health, move/rotate/velocity, weapon, fire condition, DamageCast), while the docs' prescribed answer — `[Serializable] IEntityInstaller` modules composed with `entity.Install(module)` (BestPractices/ModularEntityInstallers.md, Method 3) — is already present in the codebase as `InteractInstaller` and is used exactly once, called wrongly (`_interactInstaller.Install(entity)`), with its `_triggerInteraction` field non-serialized and therefore permanently `UseGlobal`.

On top of that: a `Dispose` that calls `Subscribe` instead of `Unsubscribe`, an `ArrayPool` array leaked on the melee hit path, four behaviours that subscribe in `Init` with no `Dispose`, `Debug.Log` on the context's per-frame update, closures + per-frame dictionary lookups for weapon cooldown ticking, destructive stat buffs that drift permanently on weapon swap while the `ExtraDamage` sum-expression built for exactly that purpose is written once and never read, and `Ammo.SpendAll()` silently skipping its change event so the HUD goes stale after every pickup.

Also worth knowing before you adopt `Consts`: the bundled plugin ships `Consts.True = new(false)` (Plugins/Atomic/Elements/Scripts/Values/Consts.cs:5). The new framework's `Constants.True` is correct — use that one.

### Высокая важность (14)

#### HIGH-1. `Assets/Game/Scripts/Gameplay/Entity/Common/TakeDamage/TakeDamageBloodBehaviour.cs:38` — correctness

**Проблема.** `Dispose` calls `Subscribe` twice instead of `Unsubscribe`. The behaviour is constructed by CharacterVfxInstaller.cs:20 and EnemyVfxInstaller.cs:22, so it is live on every character and enemy.

**Следствие.** The handler is never removed, and each dispose/re-init cycle *adds* another subscription. After a pooled enemy has died and been reused N times, one damage event plays N blood bursts and `ParticleSystem.Play` is called N times; the behaviour (and through it the `_rootTransform`/`ParticleSystem` references) is pinned alive forever. Because `Entity.Dispose()` only runs on destroy (Entity_Lifecycle.cs:43) the leak is permanent for the session.

**Как исправить.**

```csharp
public void Dispose(in IEntity entity)
{
    _damageEvent.Unsubscribe(this.OnDamageTaken);
    _deathEvent.Unsubscribe(this.OnDamageTaken);
}
```

#### HIGH-2. `Assets/Game/Scripts/Gameplay/Entity/Common/Sensors/FindTargetUseCase.cs:26` — performance

**Проблема.** The `ArrayPool<Collider>.Shared.Rent(32)` array is only returned on the failure path (line 32). The `return true` inside the loop leaks it.

**Следствие.** `DamageCastBehaviour.OnUpdate` (DamageCastBehaviour.cs:39) calls this every frame while `_castTimer > 0`, for every character and enemy holding a melee weapon. Every *successful* hit permanently removes a 32-element Collider[] from the shared pool, so the pool refills by allocating: steady-state 256-byte GC garbage per landed melee hit, plus it starves every other `ArrayPool<Collider>` consumer in the process (InteractUseCase.cs:34 rents from the same shared pool every FixedUpdate).

**Как исправить.**

```csharp
public static bool TryGetTarget(IEntity weapon, out IEntity target)
{
    target = null;

    Vector3 position = weapon.GetTransform().position;
    float radius = weapon.GetDamageRadius().Value;
    LayerMask damageLayer = weapon.GetDamageLayer();

    Collider[] colliders = ArrayPool<Collider>.Shared.Rent(COLLIDER_BUFFER_SIZE);
    try
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, colliders, damageLayer);
        for (int i = 0; i < count; i++)
        {
            if (colliders[i].TryGetEntity(out IEntity entity) && entity.HasDamageableTag())
            {
                target = entity;
                return true;
            }
        }
        return false;
    }
    finally
    {
        ArrayPool<Collider>.Shared.Return(colliders, clearArray: true);
    }
}
```

#### HIGH-3. `Assets/Game/Scripts/Gameplay/Entity/Common/SpawnEntity/SpawnUseCase.cs:26` — correctness

**Проблема.** `bullet.AddDamage(weapon.GetDamage())` is called on a *pooled* bullet. `Entity.AddValue` is add-if-absent and returns `false` when the key already exists (Plugins/Atomic/Entities/Scripts/Entity/Plain/Entity_Values.cs:79-88); the return value is discarded. `BulletInstaller` never declares Damage, so the first spawn wins permanently.

**Следствие.** Bullet #1 fired by the pistol stores the pistol's `ReactiveInt`. That bullet returns to the pool. Bullet #1 is then rented by the rifle: `AddDamage` silently no-ops, the bullet still points at the *pistol's* damage variable, and `DamageUseCase.TakeDamage` (DamageUseCase.cs:10) reads `source.GetDamage().Value` → rifle rounds deal pistol damage. It also aliases the weapon's live variable, so an ExtraDamage buff applied later retroactively changes bullets already in flight.

**Как исправить.**

```csharp
// BulletInstaller.Install — declare the slot once, at install time:
entity.AddDamage(new ReactiveInt());

// SpawnUseCase.SpawnBullet — write the value on every rent:
public static IEntity SpawnBullet(IEntity weapon, GameFactory gameFactory, Transform firePoint)
{
    IEntity bullet = gameFactory.Create(weapon.GetBulletPrefab());

    bullet.GetDamage().Value = weapon.GetDamage().Value;   // copy, do not alias
    bullet.GetTransform().SetPositionAndRotation(firePoint.position, firePoint.rotation);
    bullet.GetMoveDirection().Value = firePoint.forward;
    bullet.GetLifeTime().Reset();

    return bullet;
}
```

#### HIGH-4. `Assets/Game/Scripts/Gameplay/Entity/Content/GameFactory/GameFactory.cs:46` — correctness

**Проблема.** `Destroy` recovers from an unknown id by registering the **live instance** as the pool's prefab: `RegisterNewPrefab(entity.GetTransform().GetComponent<SceneEntity>(), entityID)`. For every character and enemy `GetTransform()` is `_characterRoot` (CharacterCoreInstaller.cs:45, EnemyCoreInstaller.cs:46), not the SceneEntity root, so `GetComponent<SceneEntity>()` returns `null`.

**Следствие.** Two distinct failures. (a) Null prefab: `SceneEntityPool` stores it and the next `Rent()` on an empty queue calls `SceneEntity.Create(null, container)` → `Instantiate` throws ArgumentException, killing the spawn path. (b) When the component *is* found (bullets, shells), the pool's prefab becomes a deactivated, mutated, pool-parented runtime object; every later `Rent()` clones that corpse — entity values already populated, `AddValue` therefore no-ops on install, and state from the dead instance is inherited by every future spawn. The fallback silently converts a missing-registration bug into data corruption.

**Как исправить.**

```csharp
private readonly Dictionary<string, SceneEntityPool> _pools = new();

public void Destroy(IEntity entity)
{
    string entityID = entity.GetEntityID();

    if (!_pools.TryGetValue(entityID, out SceneEntityPool pool))
        throw new InvalidOperationException(
            $"GameFactory: no pool registered for id '{entityID}'. " +
            $"Register the prefab in GameContextInstaller before spawning it.");

    pool.Return(entity);
}
```

#### HIGH-5. `Assets/Game/Scripts/Gameplay/Entity/Content/Enemy/EnemyCoreInstaller.cs:37` — correctness

**Проблема.** `EnemyCoreInstaller` never calls `entity.AddEntityID(...)`, yet enemies are created through the pool: GameContextInstaller.cs:26 → `SpawnUseCase.SpawnCharacter` (SpawnUseCase.cs:53) → `gameFactory.Create(characterPrefab)`.

**Следствие.** `GameFactory.Destroy` starts with `entity.GetEntityID()`, and `Entity.GetValue` throws `KeyNotFoundException` for a missing key (Entity_Values.cs:17-21). So a pooled enemy can never be returned — every enemy that dies is leaked as an active GameObject while its pool grows by one `Instantiate` per spawn. `DeathBehaviour.OnHealthEmpty` only `Disable()`s it and removes it from the world (DeathBehaviour.cs:31-40), so the corpse stays in the scene with its colliders and renderers resident.

**Как исправить.**

```csharp
// EnemyCoreInstaller.Install, in the // Core block:
entity.AddEntityID(GameFactoryID.Enemy_01.ToString());   // add Enemy_01 to the GameFactoryID enum

// and give DeathBehaviour a return path instead of leaking the corpse:
entity.AddDestroyAction(new BaseAction(() => SpawnUseCase.UnSpawnEntity(_factory, entity)));
```

#### HIGH-6. `Assets/Game/Scripts/Gameplay/Entity/Content/Bullet/HitInstaller.cs:18` — correctness

**Проблема.** `entity.AddLifeTime(new Cooldown(3f))` hardcodes 3 seconds and ignores the serialized `_lifeTime` field declared on line 10.

**Следствие.** The inspector value is inert: designers change Hit Effect lifetime and nothing happens. Because `GameFactory` pools these and the destroy action returns them (line 19), the hardcoded 3s also fixes the pool's turnover rate — a short VFX occupies a pooled slot for 3s, forcing extra `Instantiate` calls under rapid fire.

**Как исправить.**

```csharp
entity.AddLifeTime(new Cooldown(_lifeTime));
```

#### HIGH-7. `Assets/Game/Scripts/Gameplay/Entity/Content/Enemy/EnemyAudioInstaller.cs:29` — correctness

**Проблема.** `new AttackSoundBehaviour(_damageClips, _audioSource)` passes the damage clip array to the attack behaviour. `_attackClips` (declared line 12) is never used anywhere.

**Следствие.** Enemies play a pain grunt when they swing. Worse, `AttackSoundBehaviour.PlaySound` indexes `_audionClips[Random.Range(0, _audionClips.Length)]` with no empty check (Fire/AttackSoundBehaviour.cs:27-28), so any prefab that filled Attack Clips but left Damage Clips empty throws IndexOutOfRangeException on its first attack.

**Как исправить.**

```csharp
entity.AddBehaviour(new HitSoundBehaviour(_damageClips, _audioSource));
entity.AddBehaviour(new AttackSoundBehaviour(_attackClips, _audioSource));
```

#### HIGH-8. `Assets/Game/Scripts/Gameplay/Entity/Common/Buffs/DiscardAllBuffBehaviour.cs:17` — correctness

**Проблема.** Subscribes to `Health.OnHealthChanged` to drop all buffs, and has no `IEntityDispose`. `OnHealthChanged` fires on healing as well as damage (Modules/Health/Health.cs:71 in `Add`, :90 in `Reduce`, :108 in `SetCurrent`). Installed on every character by CharacterBuffInstaller.cs:17.

**Следствие.** Two bugs in four lines. (a) `HealthPickUpInstaller.cs:21` calls `character.GetHealth().Add(_healthAmount)` → `OnHealthChanged` → `BuffUseCase.DiscardAll` → picking up a health pack strips the player's speed/damage/jump buffs. Taking 1 point of chip damage does the same. (b) No `Dispose`, so the handler on the shared `Health` instance is never removed; combined with pooling this accumulates one `DiscardAllBuffs` call per reuse.

**Как исправить.**

```csharp
public class DiscardAllBuffBehaviour : IEntityInit, IEntityDispose
{
    private IEntity _character;
    private Health _health;

    public void Init(in IEntity entity)
    {
        _character = entity;
        _health = entity.GetHealth();
        _health.OnHealthEmpty += DiscardAllBuffs;   // on death only, not on every change
    }

    public void Dispose(in IEntity entity) => _health.OnHealthEmpty -= DiscardAllBuffs;

    private void DiscardAllBuffs() => BuffUseCase.DiscardAll(_character);
}
```

#### HIGH-9. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/ExtraDamageBuff.cs:22` — architecture

**Проблема.** Stat buffs mutate the target's base value destructively (`weapon.GetDamage().Value += _extraDamage` here, `*= _multiplier` / `/= _multiplier` in MoveSpeedBuff.cs:18,23, `+= / -= _extraDamage` in Aspects/DamageAspect.cs:15,21) and re-resolve `entity.GetWeapon().Value` separately in Apply and Discard. Meanwhile `RangeWeaponInstaller.cs:53` adds an `ExtraDamage` `IntSumExpression` that is never read by any code in the project.

**Следствие.** `OnApply` adds to weapon A; the player swaps weapons (PickUpWeaponUseCase.cs:22 reassigns `GetWeapon().Value`); the timer in TemporaryBuff.cs:45 expires and `OnDiscard` subtracts from weapon **B**. Weapon A keeps the bonus permanently and weapon B is permanently debuffed. MoveSpeedBuff drifts the same way, plus float `*=` / `/=` never round-trips exactly. `DamageAspect` has the identical hole on EffectArea enter/exit (EffectAreaBehaviour.cs:34,40). The `IntSumExpression` already built at RangeWeaponInstaller:53 is the non-destructive fix the code was reaching for and then forgot to wire up.

**Как исправить.**

```csharp
// Read the sum where damage is consumed (DamageUseCase.cs:10):
public static void TakeDamage(IEntity target, TakeDamageArgs args)
{
    IEntity source = args.source;
    int damage = source.GetDamage().Value;
    if (source.TryGetExtraDamage(out IExpression<int> extra))
        damage += extra.Value;

    target.GetHealth().Reduce(damage);
    target.GetDamageTakenEvent().Invoke(args);
}

// ExtraDamageBuff: contribute a term, remove the same term, and remember the weapon.
public class ExtraDamageBuff : TemporaryBuff
{
    private readonly int _extraDamage;
    private readonly Func<int> _term;
    private IEntity _weapon;

    public ExtraDamageBuff(ExtraDamageBuffConfig config) : base(config)
    {
        _extraDamage = config.Damage;
        _term = () => _extraDamage;          // allocated once, identity-stable for Remove
    }

    protected override bool IsValid(IEntity entity) => entity.HasWeapon();

    protected override void OnApply(IEntity entity)
    {
        _weapon = entity.GetWeapon().Value;   // bind to the weapon we actually buffed
        _weapon.GetExtraDamage().Append(_term);
    }

    protected override void OnDiscard(IEntity entity)
    {
        _weapon?.GetExtraDamage().Remove(_term);
        _weapon = null;
    }
}
```

#### HIGH-10. `Assets/Modules/Ammo/Ammo.cs:44` — correctness

**Проблема.** `public void SpendAll() => count = 0;` is the only mutator that does not raise `OnStateChanged`. Compare `Add` (line 32) and `Spend` (line 40).

**Следствие.** `PickUpWeaponUseCase.cs:17` does `ammo.SpendAll(); ammo.Add(pickUpValue.GetCount());` — the `Add` fires, so this one hides. But `DropWeaponUseCase.cs:38` does `ammo.SpendAll()` on a pickup whose count is then set from the dropped weapon, and any path that zeroes ammo without a following `Add` leaves `AmmoPresenter.UpdateAmmo` (UI/AmmoPresenter.cs:42) unsubscribed from the change: the HUD keeps showing the old round count until the next shot.

**Как исправить.**

```csharp
public void SpendAll()
{
    if (this.count == 0)
        return;

    this.count = 0;
    this.OnStateChanged?.Invoke();
}
```

#### HIGH-11. `Assets/Game/Scripts/Gameplay/GameContext/GameContextInstaller.cs:58` — performance

**Проблема.** `Debug.Log(_enemyFilter.Count)` runs in `EnemyController.OnUpdate`, i.e. once per frame for the whole game.

**Следствие.** `Debug.Log(object)` boxes the `int`, formats a string, and — the expensive part — captures a managed stack trace on every call. In the editor that is ~0.1-0.3 ms/frame and an unbounded console buffer; in a development build it writes a line to Player.log every frame. It is pure debug residue on the hottest callback in the project.

**Как исправить.**

```csharp
public void OnUpdate(IContext context, float deltaTime)
{
    // debug spawn + per-frame log removed; see the Input finding for the spawn path
}
```

#### HIGH-12. `Assets/Game/Scripts/Gameplay/Entity/Content/Weapons/RangeWeaponInstaller.cs:71` — performance

**Проблема.** `entity.OnUpdated += deltaTime => entity.GetWeaponCooldown().Tick(deltaTime);` — same code at MeleeWeaponInstaller.cs:55. Cooldown ticking is done with a raw event subscription and a capturing closure instead of an `IEntityUpdate` behaviour.

**Следствие.** Per weapon, per frame: a delegate invocation through `Entity.OnUpdated`, a `Dictionary<int,object>` lookup in `GetWeaponCooldown()`, and an unbox-cast of the stored `object`. The closure captures `entity`, allocates a display class plus an `Action<float>` at install, and is never unsubscribed — and since `WeaponCatalog.GetWeapon` Instantiates a fresh weapon entity on every pickup (WeaponCatalog.cs:23), each swap adds another live subscription. A behaviour caches the `Cooldown` reference once in `Init` and removes the lookup entirely.

**Как исправить.**

```csharp
// Game/Scripts/Gameplay/Entity/Common/Cooldown/TickCooldownBehaviour.cs
public sealed class TickCooldownBehaviour : IEntityInit, IEntityUpdate
{
    private Cooldown _cooldown;

    public void Init(in IEntity entity) => _cooldown = entity.GetWeaponCooldown();

    public void OnUpdate(in IEntity entity, in float deltaTime) => _cooldown.Tick(deltaTime);
}

// RangeWeaponInstaller / MeleeWeaponInstaller — replace the OnUpdated line with:
entity.AddBehaviour<TickCooldownBehaviour>();
```

#### HIGH-13. `Assets/Game/Scripts/Gameplay/Entity/Content/GameFactory/GameFactory.cs:19` — performance

**Проблема.** `string id = prefab.gameObject.name.Replace("(Clone)", "")` on the spawn path, plus `ContainsKey` followed by an indexer (lines 21-24, 29, 46-51) — two hashes and two probes per operation — and pools stored in a `ReactiveDictionary` (line 9) that nothing ever observes.

**Следствие.** `UnityEngine.Object.name` is a native property: every read marshals a brand-new managed string. `GameFactory.Create(SceneEntity)` is called for every bullet (SpawnUseCase.cs:24), every shell (:36) and every impact effect (:15) — so a full-auto weapon allocates 2-3 strings per shot purely to compute a dictionary key that is fixed at author time. `ReactiveDictionary` adds an event-dispatch wrapper and per-write delegate null-checks around a lookup table, for zero subscribers. BestPractices/UsingEntityPoolWithFactories.md keys pools by type/factory, not by name.

**Как исправить.**

```csharp
public class GameFactory
{
    private readonly Dictionary<string, SceneEntityPool> _pools = new();
    private readonly Dictionary<SceneEntity, string> _idByPrefab = new();   // name read once per prefab
    private readonly Transform _poolsParent;

    public GameFactory(Transform poolsParent) => _poolsParent = poolsParent;

    public IEntity Create(SceneEntity prefab)
    {
        if (!_idByPrefab.TryGetValue(prefab, out string id))
        {
            id = prefab.name;                      // prefab names never carry "(Clone)"
            _idByPrefab[prefab] = id;
            RegisterNewPrefab(prefab, id);
        }
        return _pools[id].Rent();
    }

    public IEntity Create(string id)
    {
        if (!_pools.TryGetValue(id, out SceneEntityPool pool))
            throw new InvalidOperationException($"GameFactory: no pool registered for id '{id}'.");
        return pool.Rent();
    }

    public void RegisterNewPrefab(SceneEntity prefab, string id, int prewarm = 16)
    {
        if (_pools.ContainsKey(id))
            return;

        GameObject container = new GameObject($"Pool_{id}");
        container.transform.SetParent(_poolsParent);

        SceneEntityPool pool = new SceneEntityPool(prefab, container.transform);
        pool.Init(prewarm);                        // no Instantiate during gameplay
        _pools[id] = pool;
    }
}
```

#### HIGH-14. `Assets/Game/Scripts/Gameplay/Entity/Content/GameFactory/GameFactory.cs:33` — performance

**Проблема.** `RegisterNewPrefab` creates the pool but never calls `SceneEntityPool.Init(count)`, so no instances are pre-warmed. GameContextInstaller.cs:25 registers only the audio prefab, and that one is not pre-warmed either.

**Следствие.** `SceneEntityPool.Rent()` falls through to `SceneEntity.Create(prefab, container)` → `Instantiate` + `Install()` whenever the queue is empty (Plugins/Atomic/Entities/Scripts/Pooling/SceneEntityPool.cs:30-34). The pool therefore grows by a synchronous `Instantiate` **during** the first burst of fire — the exact frame-spike the pool exists to prevent. Every bullet, shell, impact VFX and one-shot audio entity pays an Instantiate on first use, and the enemy pool pays one per spawn forever (see the EntityID finding). BestPractices/UsingEntityPoolWithFactories.md: "pre-instantiates 20 bullets to avoid runtime allocations during gameplay".

**Как исправить.**

```csharp
// GameContextInstaller.Install — pre-warm every pooled prefab up front:
GameFactory factory = new GameFactory(_poolsParent);
context.AddGameFactory(factory);

factory.RegisterNewPrefab(_audioPrefab,     GameFactoryID.AudioSource.ToString(), prewarm: 8);
factory.RegisterNewPrefab(_hitEffectPrefab, GameFactoryID.HitEffect_01.ToString(), prewarm: 32);
factory.RegisterNewPrefab(_bulletPrefab,    GameFactoryID.Bullet_01.ToString(),    prewarm: 64);
factory.RegisterNewPrefab(_shellPrefab,     GameFactoryID.Shell_01.ToString(),     prewarm: 32);
factory.RegisterNewPrefab(_enemyPrefab,     GameFactoryID.Enemy_01.ToString(),     prewarm: 16);
```

### Средняя важность (13)

#### MEDIUM-1. `Assets/Game/Scripts/Gameplay/Entity/Content/Character/CharacterCoreInstaller.cs:58` — performance

**Проблема.** `entity.AddMoveCondition(new AndExpression(entity.GetHealth().Exists, () => entity.GetWeapon().Value.GetMoveCondition().Invoke()))` — identical code at EnemyCoreInstaller.cs:59. The lambda does two entity-value lookups and dereferences `.Value` without a null check.

**Следствие.** `CharacterMoveBehaviour.OnUpdate` evaluates `_moveCondition.Value` every frame (CharacterMoveBehaviour.cs:20), which walks the expression and invokes this closure: two `Dictionary<int,object>` lookups plus two unbox-casts per character per frame, where a cached `IReactiveVariable<IEntity>` captured once costs nothing. And `GetWeapon().Value` is nullable — `AmmoPresenter.cs:28` and `AmmoPickUpInstaller.cs:21` both guard it — so the frame after any code sets `Weapon.Value = null` the player's movement throws NullReferenceException inside the expression.

**Как исправить.**

```csharp
IReactiveVariable<IEntity> weapon = new ReactiveVariable<IEntity>(_weapon);
entity.AddWeapon(weapon);

entity.AddMoveCondition(new AndExpression(
    entity.GetHealth().Exists,
    () => weapon.Value == null || weapon.Value.GetMoveCondition().Invoke()
));
```

#### MEDIUM-2. `Assets/Game/Scripts/Gameplay/Entity/Content/PickUps/DashPickUpInstaller.cs:24` — performance

**Проблема.** `foreach (var (key, ability) in playerContext.GetAbilities())` over an `IReactiveDictionary<string, Ability>`, comparing `key == nameof(DashAbilityConfig)` — the author marked it `//Костыль!` on line 23.

**Следствие.** `ReactiveDictionary.GetEnumerator()` returns `IEnumerator<KeyValuePair<K,V>>` (Plugins/Atomic/Elements/Scripts/Collections/Implementations/ReactiveDictionary.cs:56), so iterating through the interface boxes the enumerator on the heap, and `var (key, ability)` deconstructs every pair — a full linear scan with a string comparison per entry, where the dictionary's own `TryGetValue` is O(1). BestPractices/IteratingReactiveCollections.md flags exactly this: "When iterating via interfaces using IEnumerator<T>, the iterator may box, causing heap allocations and GC pressure."

**Как исправить.**

```csharp
private static readonly string DashAbilityKey = nameof(DashAbilityConfig);

entity.AddInteractAction(new BaseAction<IEntity>(character =>
{
    IPlayerContext playerContext = gameContext.GetPlayerContext();

    if (playerContext.GetAbilities().TryGetValue(DashAbilityKey, out Ability dash))
        dash.GetCharges().Value++;

    entity.GetPickUpEvent().Invoke();
    gameObject.SetActive(false);
}));
```

#### MEDIUM-3. `Assets/Game/Scripts/Gameplay/Entity/Common/Buffs/BuffUseCase.cs:14` — performance

**Проблема.** LINQ over an `IList<BuffBase>` on every buff operation: `Any(b => b.Name == buff.Name)` (line 14), again at line 27, and `FirstOrDefault(b => b.Name == buffName)` (line 37).

**Следствие.** Each call allocates a closure plus a boxed `IEnumerator<BuffBase>` through the `IList` interface, and compares by string. `TemporaryBuff.OnFixedUpdate` (TemporaryBuff.cs:45) calls `BuffUseCase.Discard` **every FixedUpdate for every active buff** once the duration is reached, not once — the behaviour is only removed inside `Discard`, so until the list scan finds it, each 50 Hz tick allocates a closure and scans the list by string. Buffs are also stored in a `ReactiveList` whose concrete type exposes a struct enumerator.

**Как исправить.**

```csharp
public static bool Apply(IEntity character, BuffBase buff)
{
    ReactiveList<BuffBase> buffs = character.GetBuffsEffects();   // concrete type: struct enumerator

    for (int i = 0, count = buffs.Count; i < count; i++)
        if (ReferenceEquals(buffs[i].Name, buff.Name) || buffs[i].Name == buff.Name)
            return false;

    buffs.Add(buff);
    buff.Apply(character);
    return true;
}

public static bool Discard(IEntity character, string buffName)
{
    ReactiveList<BuffBase> buffs = character.GetBuffsEffects();

    for (int i = 0, count = buffs.Count; i < count; i++)
    {
        BuffBase buff = buffs[i];
        if (buff.Name != buffName)
            continue;

        buffs.RemoveAt(i);
        buff.Discard(character);
        return true;
    }
    return false;
}
```

#### MEDIUM-4. `Assets/Game/Scripts/Gameplay/Entity/Content/Weapons/RangeWeaponInstaller.cs:49` — idiom-violation

**Проблема.** `entity.AddMoveCondition(new AndExpression((() => true)))` — a heap-allocated lambda whose only job is to return a constant. MeleeWeaponInstaller.cs:50 has the mirror-image problem: `new AndExpression(() => entity.GetWeaponCooldown().IsExpired())` allocates a closure that re-looks-up the cooldown, two lines after line 40 proved the direct method group `_cooldown.IsExpired` works.

**Следствие.** BestPractices/UsingConstantsWithAndExpressions.md and SharedConstants.md: constants belong in a shared `Const<bool>`, not a per-instance closure. Each weapon instance allocates a display class + `Func<bool>` for a value known at compile time, and the melee variant pays a dictionary lookup plus unbox on every fire-condition evaluation. Caution when you fix this: the plugin bundled in this project ships `Consts.True = new(false)` (Plugins/Atomic/Elements/Scripts/Values/Consts.cs:5) — the constant named True evaluates to false. Use the upstream `Constants.True`, which is correct.

**Как исправить.**

```csharp
// RangeWeaponInstaller — no closure, no allocation:
entity.AddMoveCondition(new AndExpression(Constants.True.AsFunc()));
// (or simply: entity.AddMoveCondition(new AndExpression());  an empty AndExpression
//  already returns true — see AndExpression.Invoke, which returns true when count == 0)

// MeleeWeaponInstaller — reuse the field you already hold, as line 40 does:
entity.AddFireCondition(new AndExpression(_cooldown.IsExpired));
```

#### MEDIUM-5. `Assets/Game/Scripts/Gameplay/Entity/Content/Items/ItemVisualInstaller.cs:22` — idiom-violation

**Проблема.** `entity.GetPickUpEvent().OnEvent += () => {...}` subscribes a capturing lambda straight from `Install`, with no matching unsubscribe; the drop handler on line 30 does the same. `GameFactoryID.AudioSource.ToString()` is called inside the handler, and the `_audioPool` field (line 17) is dead.

**Следствие.** Three costs. (a) The lambda captures `this` (a MonoBehaviour) and `_pickUpSound`, and the subscription outlives the installer — there is no `Uninstall`, which the new `MonoEntityInstaller` provides specifically for this. (b) `Enum.ToString()` allocates a string via reflection-backed name lookup on **every pickup**, to look up a key that is constant. (c) The rented audio entity is configured by poking at it from outside (`GetLifeTime().Reset()`, `GetAudioSource().PlayOneShot`), so the pickup installer owns audio-entity internals. BestPractices/UsingSubscriptionsWithDisposeComposite.md prescribes `Subscribe(...).AddTo(_disposables)` plus `Uninstall()`.

**Как исправить.**

```csharp
public sealed class ItemVisualInstaller : MonoEntityInstaller
{
    private static readonly string AudioSourceID = GameFactoryID.AudioSource.ToString();

    [SerializeField] private HighlightEffect _highlight;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pickUpSound;
    [SerializeField] private AudioClip _dropSound;

    private readonly DisposableComposite _disposables = new();

    public override void Install(IEntity entity)
    {
        entity.AddHighlight(_highlight);

        entity.GetPickUpEvent().Subscribe(this.OnPickUp).AddTo(_disposables);

        if (entity.TryGetDropEvent(out ISignal dropEvent))
            dropEvent.Subscribe(this.OnDrop).AddTo(_disposables);
    }

    public override void Uninstall(IEntity entity) => _disposables.Dispose();

    private void OnPickUp() =>
        AudioUseCase.PlayOneShot(GameContext.Instance.GetGameFactory(), AudioSourceID, _pickUpSound);

    private void OnDrop() => _audioSource.PlayOneShot(_dropSound);
}
```

#### MEDIUM-6. `Assets/Game/Scripts/Gameplay/Entity/Content/Character/CharacterCoreInstaller.cs:38` — architecture

**Проблема.** `_gameContext = GameContext.Instance` inside `Install`. The same line appears in EnemyCoreInstaller.cs:35, ShellInstaller.cs:17, BulletInstaller.cs:21, HitInstaller.cs:15, MeleeWeaponInstaller.cs:22, RangeWeaponInstaller.cs:35, WeaponPickUpInstaller.cs:17, DashPickUpInstaller.cs:12, and inline at AudioSourceInstaller.cs:19, MarkerInstaller.cs:16, BuffPickUpInstaller.cs:24 — 12 sites in a 26-file folder. `BulletCollisionBehaviour.cs:15` reaches the singleton again from inside `Init`, once per bullet.

**Следствие.** The entity layer cannot be instantiated without a live scene singleton: no unit test can install a character, no prefab can be previewed in isolation, and installer execution order silently depends on `GameContext.Awake` having already run (`SingletonSceneContext` + `SceneEntity` both use `DefaultExecutionOrder(-1000)`, so this is ordering-by-luck, not by contract). BestPractices/UsingUtilsForEntityInstallers.md shows the context being *passed in* (`IEntityInstaller<IPlayerContext>`), with `GameContext.Instance` touched only at the system-installer boundary, guarded by `AtomicUtils.IsPlayMode()`.

**Как исправить.**

```csharp
// Resolve the context once, at the system boundary, and hand dependencies down.
// CharacterSystemInstaller (PlayerContext) already sits at that boundary:
[Serializable]
public sealed class CharacterSystemInstaller : IEntityInstaller<IPlayerContext>
{
    [SerializeField] private SceneEntity _characterPrefab;

    public void Install(IPlayerContext context)
    {
        if (!AtomicUtils.IsPlayMode())
            return;

        GameContext gameContext = GameContext.Instance;
        IEntity character = gameContext.GetGameFactory().Create(_characterPrefab);
        character.AddGameFactory(gameContext.GetGameFactory());   // injected, not fetched
        character.AddEntityWorld(gameContext.GetEntityWorld());
        context.AddCharacter(character);
    }
}

// Content installers then read what was injected:
public sealed class CharacterCoreInstaller : MonoEntityInstaller<IGameEntity>
{
    public override void Install(IGameEntity entity)
    {
        // no GameContext.Instance anywhere below this line
        entity.AddBehaviour(new DeathBehaviour());   // pulls the world off the entity in Init
    }
}
```

#### MEDIUM-7. `Assets/Game/Scripts/Gameplay/Entity/Content/Enemy/EnemyCoreInstaller.cs:33` — architecture

**Проблема.** `EnemyCoreInstaller.Install` (lines 33-86) and `CharacterCoreInstaller.Install` (lines 36-81) are the same installer twice: health block, rotation speed, move/rotate direction, velocity, the three movement behaviours, weapon + weapon root, fire condition, `CharacterFireAction`, `DamageCastBehaviour`, trigger receiver, GameObject, Transform — ~40 duplicated lines including the identical `AndExpression` move condition. The same duplication runs through CharacterAnimInstaller/EnemyAnimInstaller (identical but for behaviour order), CharacterVfxInstaller/EnemyVfxInstaller (byte-identical `Install` bodies), and CharacterAudioInstaller/EnemyAudioInstaller.

**Следствие.** Any movement or combat change has to be made twice and stays correct only by vigilance — the Anim installers have already drifted (`CharacterMoveAnimBehaviour` and `SwitchAnimatorBehaviour` are added in opposite order), and the Audio pair drifted into the `_attackClips` bug reported above. The codebase already contains the prescribed answer, `InteractInstaller` (`[Serializable] class : IEntityInstaller`), and uses it in exactly one place. BestPractices/ModularEntityInstallers.md Method 3 is written for precisely this case: "For entities that share mechanics (e.g., characters and enemies), create modular installers."

**Как исправить.**

```csharp
[Serializable]
public sealed class HealthInstaller : IEntityInstaller<IGameEntity>
{
    [SerializeField] private int _health = 100;

    public void Install(IGameEntity entity)
    {
        entity.AddDamageableTag();
        entity.AddHealth(new Health(_health, _health));
        entity.AddDamageTakenEvent(new BaseEvent<TakeDamageArgs>());
        entity.AddDeathTakenEvent(new BaseEvent<TakeDamageArgs>());
        entity.AddBehaviour<DeathBehaviour>();
    }
}

[Serializable]
public sealed class LocomotionInstaller : IEntityInstaller<IGameEntity>
{
    [SerializeField] private Const<float> _moveSpeed = 15f;
    [SerializeField] private Const<float> _rotationSpeed = 15f;

    public void Install(IGameEntity entity)
    {
        entity.AddMoveSpeed(_moveSpeed);
        entity.AddRotationSpeed(_rotationSpeed);
        entity.AddMoveDirection(new ReactiveVariable<Vector3>());
        entity.AddRotateDirection(new ReactiveVariable<Vector3>());
        entity.AddVelocity(new ReactiveFloat());
        entity.AddBehaviour<CharacterMoveBehaviour>();
        entity.AddBehaviour<CharacterRotateBehaviour>();
        entity.AddBehaviour<CharacterVelocityBehaviour>();
    }
}

[Serializable]
public sealed class CombatInstaller : IEntityInstaller<IGameEntity>
{
    [SerializeField] private SceneEntity _weapon;
    [SerializeField] private Transform _weaponRoot;

    public void Install(IGameEntity entity)
    {
        entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
        entity.AddWeaponRoot(_weaponRoot);
        entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));
        entity.AddFireAction(new CharacterFireAction(entity));
        entity.AddBehaviour<DamageCastBehaviour>();
    }
}

// Both concrete entities then declare only what makes them different:
public sealed class CharacterCoreInstaller : MonoEntityInstaller<IGameEntity>
{
    [SerializeField] private HealthInstaller _health;
    [SerializeField] private LocomotionInstaller _locomotion;
    [SerializeField] private CombatInstaller _combat;
    [SerializeField] private InteractInstaller _interact;

    public override void Install(IGameEntity entity)
    {
        entity.AddPlayerTag();
        entity.Install(_health);
        entity.Install(_locomotion);
        entity.Install(_combat);
        entity.Install(_interact);
    }
}

public sealed class EnemyCoreInstaller : MonoEntityInstaller<IGameEntity>
{
    [SerializeField] private HealthInstaller _health;
    [SerializeField] private LocomotionInstaller _locomotion;
    [SerializeField] private CombatInstaller _combat;
    [SerializeField] private Transform[] _patrolPoints;

    public override void Install(IGameEntity entity)
    {
        entity.AddEnemyTag();
        entity.AddEntityID(GameFactoryID.Enemy_01.ToString());
        entity.Install(_health);
        entity.Install(_locomotion);
        entity.Install(_combat);

        entity.AddPatrolPoints(_patrolPoints);
        entity.AddTarget(new ReactiveVariable<IEntity>());
        entity.AddBehaviour<EnemyPatrolBehaviour>();
        entity.AddBehaviour<EnemyChaseBehaviour>();
        entity.AddBehaviour<EnemyAttackBehaviour>();
    }
}
```

#### MEDIUM-8. `Assets/Game/Scripts/Gameplay/Entity/Common/Interact/InteractInstaller.cs:15` — correctness

**Проблема.** `private QueryTriggerInteraction _triggerInteraction;` has no `[SerializeField]`, so it can never be configured and is always `default` = `QueryTriggerInteraction.UseGlobal` (0). It is passed to the overlap query at line 19. Separately, CharacterCoreInstaller.cs:80 invokes it as `_interactInstaller.Install(entity)` rather than through the framework's `entity.Install(...)`.

**Следствие.** Interaction detection silently falls back to the project-wide `Physics.queriesHitTriggers` setting, so whether pickups can be detected at all depends on a global checkbox rather than on this installer — and since pickup colliders are triggers (BuffPickUpInstaller/HealthPickUpInstaller add `InteractableTag` to trigger volumes), flipping that global off makes every pickup in the game undetectable with nothing in this file to explain why. Calling `.Install(entity)` directly also bypasses the framework hook, so `Uninstall` will never be dispatched for this module when you move to `MonoEntityInstaller`.

**Как исправить.**

```csharp
[Serializable]
public sealed class InteractInstaller : IEntityInstaller<IGameEntity>
{
    [SerializeField] private Transform _center;
    [SerializeField] private float _radius = 1.5f;
    [SerializeField] private LayerMask _layerMask;
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

    public void Install(IGameEntity entity)
    {
        entity.AddTargetInteractable(new ReactiveVariable<IEntity>());
        entity.AddBehaviour(new DetectInteractableBehaviour(_center, _layerMask, _radius, _triggerInteraction));
    }
}

// CharacterCoreInstaller.cs:80
entity.Install(_interactInstaller);
```

#### MEDIUM-9. `Assets/Game/Scripts/Gameplay/Entity/Content/Character/CharacterCoreInstaller.cs:69` — architecture

**Проблема.** `entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));` and `entity.AddHandWeapon(new ReactiveVariable<IEntity>(_weapon));` — two separate reactive variables seeded from the same serialized reference. `HandWeapon` is never reassigned anywhere in the project; it is only read, to toggle a GameObject (PickUpWeaponUseCase.cs:30, DropWeaponUseCase.cs:21-22).

**Следствие.** `HandWeapon` is not a slot, it is a constant: the default weapon this character was authored with. Modelling it as a second `ReactiveVariable<IEntity>` creates two sources of truth for 'which weapon' — a reader that picks the wrong one gets the default weapon instead of the equipped one, and nothing in the type system distinguishes them. It also allocates a second reactive wrapper plus a second entity-value slot per character for a value that never changes. A `Const<IEntity>` states the invariant and costs one immutable object.

**Как исправить.**

```csharp
// Weapon is the mutable slot; the hand weapon is the authored default and never changes.
entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
entity.AddHandWeapon(new Const<IEntity>(_weapon));

// EntityAPI codegen signature becomes:
//   public static IValue<IEntity> GetHandWeapon(this IEntity obj)
// so DropWeaponUseCase.cs:22 reads, and can no longer accidentally write:
character.GetWeapon().Value = character.GetHandWeapon().Value;
```

#### MEDIUM-10. `Assets/Game/Scripts/Gameplay/GameContext/WeaponCatalog.cs:23` — performance

**Проблема.** `GetWeapon` and `GetPickUpWeapon` are named as accessors but each call runs `SceneEntity.Create(...)` → `Instantiate` + `Install()`. Their callers then destroy the old object: PickUpWeaponUseCase.cs:28 `SceneEntity.Destroy(sceneWeapon)`, DropWeaponUseCase.cs:19 `GameObject.Destroy(weaponTransform.gameObject)`.

**Следствие.** One weapon swap costs two `Instantiate` calls (the equipped weapon plus the dropped pickup) and two `Destroy` calls, with full `Install()` — reactive variables, expressions, behaviours — re-allocated each time, while a `GameFactory` with pools sits right next to it in the same context (GameContextInstaller.cs:24). The naming hides it: `GetWeapon` reads as a dictionary lookup at every call site. Each new weapon entity also re-subscribes the never-unsubscribed cooldown closure from RangeWeaponInstaller.cs:71.

**Как исправить.**

```csharp
public class WeaponCatalog : SerializedMonoBehaviour
{
    [OdinSerialize] private Dictionary<WeaponID, SceneEntity> _weapons = new();
    [OdinSerialize] private Dictionary<WeaponID, SceneEntity> _pickUpWeapons = new();

    // Prefab lookups: no side effects, name matches behaviour.
    public SceneEntity GetWeaponPrefab(WeaponID id) => _weapons[id];
    public SceneEntity GetPickUpPrefab(WeaponID id) => _pickUpWeapons[id];
}

// Register both tables with the factory in GameContextInstaller, then rent/return:
IEntity weapon = factory.Create(catalog.GetWeaponPrefab(id));      // pooled
factory.Destroy(previousWeapon);                                    // returned, not destroyed
```

#### MEDIUM-11. `Assets/Game/Scripts/Gameplay/GameContext/GameContextInstaller.cs:55` — architecture

**Проблема.** `if (Input.GetKeyDown(KeyCode.M)) SpawnUseCase.SpawnCharacter(...)` inside `EnemyController.OnUpdate` — a debug keybinding wired into the game context's per-frame controller, reading `UnityEngine.Input` directly from context logic.

**Следствие.** Pressing M in a shipped build spawns enemies. It also puts a Unity input dependency in the one layer the framework wants Unity-free: every other input path in this project goes through the PlayerContext controllers (PlayerContext/Controllers/*), so this controller cannot be unit-tested or driven from a replay/AI source, and it is the only reason `EnemyController` needs `OnUpdate` at all.

**Как исправить.**

```csharp
// Spawning becomes a request on the context; who triggers it is the caller's business.
public sealed class EnemySpawnController : IContextInit<GameContext>
{
    private readonly SceneEntity _enemyPrefab;
    private readonly Transform _spawnPoint;

    private IEntityWorld _world;
    private GameFactory _factory;

    public EnemySpawnController(SceneEntity enemyPrefab, Transform spawnPoint)
    {
        _enemyPrefab = enemyPrefab;
        _spawnPoint = spawnPoint;
    }

    public void Init(GameContext context)
    {
        _world = context.GetEntityWorld();
        _factory = context.GetGameFactory();
        context.GetSpawnEnemyRequest().Subscribe(this.Spawn);
    }

    private void Spawn() => SpawnUseCase.SpawnCharacter(_enemyPrefab, _spawnPoint, _world, _factory);
}

// Debug key moved to an editor-only driver, out of the shipped controller:
#if UNITY_EDITOR
public sealed class EnemySpawnDebugInput : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
            GameContext.Instance.GetSpawnEnemyRequest().Invoke();
    }
}
#endif
```

#### MEDIUM-12. `Assets/Game/Scripts/Gameplay/Entity/Common/Hit/HitSoundBehaviour.cs:22` — correctness

**Проблема.** `HitSoundBehaviour` implements `IEntityInit` only and subscribes `_health.OnHealthChanged += PlayHitSound` with no `IEntityDispose`. `Fire/AttackSoundBehaviour.cs:22` has the identical shape on the fire event, and `LifeTimeBehaviour.cs:7` caches `_lifeTime`/`_destroyAction` with no dispose either. All three are installed from Content: EnemyAudioInstaller.cs:28-29, AudioSourceInstaller.cs:17, BulletInstaller.cs:32, HitInstaller.cs:20, ShellInstaller.cs:28.

**Следствие.** `Entity.Dispose()` is the only place `IEntityDispose` runs (Entity_Lifecycle.cs:50-53) and pooled entities are only `Disable()`d on return (SceneEntityPool.cs:74-78), never disposed — so these handlers stay attached to long-lived `Health`/`BaseEvent` objects for the whole session, and each keeps its `AudioSource` and `AudioClip[]` alive. `PlayHitSound` also indexes `_damageClips[Random.Range(0, _damageClips.Length)]` with no empty guard, so an enemy prefab with no damage clips throws IndexOutOfRangeException the first time it is hit.

**Как исправить.**

```csharp
public sealed class HitSoundBehaviour : IEntityInit, IEntityDispose
{
    private readonly AudioClip[] _damageClips;
    private readonly AudioSource _audioSource;
    private Health _health;

    public HitSoundBehaviour(AudioClip[] damageClips, AudioSource audioSource)
    {
        _damageClips = damageClips;
        _audioSource = audioSource;
    }

    public void Init(in IEntity entity)
    {
        _health = entity.GetHealth();
        _health.OnHealthChanged += PlayHitSound;
    }

    public void Dispose(in IEntity entity) => _health.OnHealthChanged -= PlayHitSound;

    private void PlayHitSound(int _)
    {
        if (_damageClips.Length == 0)
            return;

        _audioSource.PlayOneShot(_damageClips[Random.Range(0, _damageClips.Length)]);
    }
}
```

#### MEDIUM-13. `Assets/Game/Scripts/UI/AmmoPresenter.cs:30` — correctness

**Проблема.** `OnWeaponChanged` subscribes `ammo.OnStateChanged += UpdateAmmo` for the new weapon but never unsubscribes from the previous one. `OnHide` (line 50) only unsubscribes the current weapon, and dereferences `_weapon.Value` without a null check.

**Следствие.** `_weapon.Observe(OnWeaponChanged)` fires on every pickup and drop. After N weapon swaps the handler is attached to N `Ammo` instances; each one pins its destroyed weapon entity alive, and `UpdateAmmo` is invoked N times per shot — each call doing a `GetAmmo()` dictionary lookup, an unbox and an `int.ToString()` allocation, then writing the same text to TMP. `UpdateAmmo` also reads `_weapon.Value.GetAmmo()` unconditionally, so a stale handler firing while the player holds the ammo-less hand weapon throws KeyNotFoundException (Entity_Values.cs:17).

**Как исправить.**

```csharp
private Ammo _ammo;

private void OnWeaponChanged(IEntity weapon)
{
    if (_ammo != null)
    {
        _ammo.OnStateChanged -= UpdateAmmo;   // release the previous weapon first
        _ammo = null;
    }

    if (weapon != null && weapon.TryGetAmmo(out Ammo ammo))
    {
        _ammo = ammo;
        _ammo.OnStateChanged += UpdateAmmo;
        _statView.gameObject.SetActive(true);
        _infinityIcon.SetActive(false);
        UpdateAmmo();
    }
    else
    {
        _infinityIcon.SetActive(true);
        _statView.gameObject.SetActive(false);
    }
}

private void UpdateAmmo() => _statView.SetText(_ammo.GetCount().ToString());

protected override void OnHide()
{
    if (_ammo != null)
    {
        _ammo.OnStateChanged -= UpdateAmmo;
        _ammo = null;
    }
}
```

### Низкая важность (3)

#### LOW-1. `Assets/Game/Scripts/Gameplay/Entity/Content/Character/CharacterBuffInstaller.cs:11` — idiom-violation

**Проблема.** `[OdinSerialize] private Dictionary<string, ParticleSystem> _buffsParticles` — buff VFX keyed by a hand-typed string, matched against `buff.Name` in BuffVFXBehaviour.cs:35,41. Buff identity is stringly-typed throughout: `BuffUseCase.Discard(character, string buffName)`, `TemporaryBuff._name`, and `nameof(DashAbilityConfig)` as an ability key in DashPickUpInstaller.cs:27.

**Следствие.** A typo in the inspector dictionary silently disables that buff's VFX with no error, and renaming a `BuffConfig.Name` breaks the mapping in every prefab that references it, with nothing to find at compile time. Every lookup also hashes a string instead of an int. The project already has the right pattern next door — `GameFactoryID` and `WeaponID` enums, and the codegen `EntityAPI` that compiles names to `int` constants.

**Как исправить.**

```csharp
public enum BuffID { MoveSpeed, Jump, ExtraDamage, Stun }

// BuffConfig exposes the id instead of a free-text name:
public abstract class BuffConfig : ScriptableObject
{
    [SerializeField] private BuffID _id;
    public BuffID ID => _id;
}

// CharacterBuffInstaller:
[OdinSerialize] private Dictionary<BuffID, ParticleSystem> _buffsParticles = new();

// BuffVFXBehaviour:
private void OnItemAdded(int index, BuffBase buff)
{
    if (_particles.TryGetValue(buff.ID, out ParticleSystem vfx))
        vfx.gameObject.SetActive(true);
}
```

#### LOW-2. `Assets/Game/Scripts/Gameplay/Entity/Content/Character/CharacterVfxInstaller.cs:12` — dead-code

**Проблема.** Serialized fields that no code reads: `_deadBlood` (line 12) and `_groundPoint` (line 14) here; `_deadBlood`, `_groundPoint`, `_deathBloodVfx` in EnemyVfxInstaller.cs:12,14,18; `_painLevels`, `_bodyFallClip`, `_bulletDamageClip` in CharacterAudioInstaller.cs:12,18,22; `_attackClips`, `_bodyFallClip`, `_bulletDamageClip` in EnemyAudioInstaller.cs:12,16,18; `_audioPool` in ItemVisualInstaller.cs:17; `_damage` in EnemyCoreInstaller.cs:12 is read but only to build `AddDamage`, while `BulletInstaller._damage` (line 12) is never read at all.

**Следствие.** Serialized references force their assets into the build and into memory whenever the prefab loads — `_painLevels` is a `PainSoundBehaviour.Level[]` holding whole `AudioClip[]` arrays per level, all resident for a behaviour whose body is commented out (PainSoundBehaviour.cs:12-55). They also actively mislead: `_bulletDamageClip` is populated in the inspector, but CharacterAudioInstaller.cs:28 passes `_meleeDamageClip` to `TakeDamageSoundBehaviour`, so bullet hits play the melee sound and the field that would have fixed it sits unused two lines above.

**Как исправить.**

```csharp
// CharacterVfxInstaller — keep only what Install consumes:
public sealed class CharacterVfxInstaller : MonoEntityInstaller
{
    [SerializeField] private ParticleSystem _bulletBlood;
    [SerializeField] private ParticleSystem _meleeBlood;
    [SerializeField] private Transform _rootTransform;

    public override void Install(IEntity entity) =>
        entity.AddBehaviour(new TakeDamageBloodBehaviour(_bulletBlood, _meleeBlood, _rootTransform));
}

// CharacterAudioInstaller — pass both damage clips, so the serialized data is actually used:
entity.AddBehaviour(new TakeDamageSoundBehaviour(_meleeDamageClip, _bulletDamageClip));

// and in TakeDamageSoundBehaviour, pick by the source's weapon tag:
private void OnDamageTaken(TakeDamageArgs args)
{
    IEntity source = args.source;
    AudioClip clip = source != null && source.HasRangeWeaponTag() ? _bulletClip : _meleeClip;
    if (clip != null)
        _audioSource.PlayOneShot(clip);
}
```

#### LOW-3. `Assets/Game/Scripts/Gameplay/Entity/Content/GravityScaler.cs:9` — unity-misuse

**Проблема.** A `[RequireComponent(typeof(Rigidbody))]` MonoBehaviour whose only member is an empty `private void Start() {}`. Alongside it, ProjectileCoreInstaller.cs:9 is an installer whose entire body is `//TODO`, and PlayerUseCase.cs:7 takes an `IEntity character` parameter it never reads before returning `gameContext.GetPlayerContext()`.

**Следствие.** An empty `Start` is not free: Unity registers the component in its message list and calls it, so every instance pays a managed-to-native callback at spawn and the component shows in the inspector as if it did something. The empty `ProjectileCoreInstaller` is worse — `SceneEntity.InstallInternal` iterates its installer list and calls it (SceneEntity.cs:90), so any projectile prefab using it silently gets an entity with no values and fails later at the first `GetValue` with KeyNotFoundException rather than at install. `PlayerUseCase.GetPlayerContext(gameContext, character)` invites callers (DashPickUpInstaller.cs:21) to believe the context is resolved per character; in a two-player build every pickup would credit player 1.

**Как исправить.**

```csharp
// Delete GravityScaler.cs and ProjectileCoreInstaller.cs (neither has a body).
// If projectiles are coming, write the installer when it has content rather than
// leaving an empty one wired into prefabs.

// PlayerUseCase: either resolve per character, or drop the lie in the signature.
public static class PlayerUseCase
{
    public static IPlayerContext GetPlayerContext(GameContext gameContext, IEntity character) =>
        gameContext.GetPlayerContexts().TryGetValue(character.GetPlayerID(), out IPlayerContext ctx)
            ? ctx
            : throw new InvalidOperationException($"No PlayerContext owns entity '{character.Name}'.");
}
```

---

## Способности, аспекты, баффы, UI

Audited 26 files across Ability, Aspects, BuffEffects, Triggers and UI, plus the infrastructure they call into (BuffUseCase, AbilityUseCase, EffectAreaBehaviour, Presenter, Entity_Values/Behaviours, PlayerContextInstaller) to confirm semantics rather than guess. 27 findings, all pinned to a line.

The defects cluster into four real mechanisms, not style:

1. Nothing unsubscribes. `Extensions.Observe` in this framework returns `void` (Elements/Values/Extensions.cs:12), so `AmmoPresenter`'s weapon subscription is literally unreleasable; `DamageScreenPresenter` never detaches from `DamageTakenEvent`; `AmmoPresenter` leaks one `Ammo.OnStateChanged` handler per weapon swap and its `OnHide` is asymmetric with `OnInit` (unsubscribes a thing it never re-subscribes, and NREs on an unarmed character). The upstream BestPractices doc `UsingObserveWithReactiveValues.md` requires storing the handle; `UsingSubscriptionsWithDisposeComposite.md` gives the `DisposableComposite` form.

2. Apply/Discard is not a matched pair anywhere in the buff/aspect system. `TemporaryBuff.Discard` is gated on `IsValid`, so a buff whose precondition lapses is removed from the list but never from the entity — the behaviour then ticks forever, running a LINQ+closure `FirstOrDefault` every FixedUpdate. `BuffUseCase.Apply` inserts into the list *before* `Apply` can refuse, leaving a phantom buff that blocks reapplication and pins the VFX on. `EffectAreaBehaviour` calls `Apply`/`Discard` per trigger event with no refcount, and `Entity.AddValue` is `TryAdd` while `DelValue` is unconditional — so a second Apply is a silent no-op and the first Discard strips the mechanic. `MoveSpeedAspect._multiplier` defaults to 0: speed → 0, then `/= 0` → Infinity.

3. Stale entity references. `ExtraDamageBuff`/`DamageAspect` re-resolve `GetWeapon().Value` at discard time; `PickUpWeaponUseCase.cs:22` swaps that reference to a shared catalog weapon mid-buff, so the wrong weapon is un-buffed and the original keeps the bonus permanently. The three presenters and `DashAbilityConfig` snapshot `GetCharacter().Value` once at install.

4. Silent installer failures and logic in installers. `CharacterSystemInstaller.cs:16` and `PlayerContextInstaller.cs:27` both call `AddCharacter`; the second is dropped by `TryAdd` and `_playerEntity` is dead serialized state. Every ability config polls `Input` inside a `WhenUpdate` closure registered from a ScriptableObject's `Install`, discarding the returned `Action` so it can never be detached. `StunAbilityConfig` applies its buff outside the condition/action flow entirely — the stun lands and the mana is never spent when the condition fails.

Also worth flagging: buff identity is a free-text serialized `string` (default empty) compared with `==` in three LINQ queries and used as a `Dictionary<string, ParticleSystem>` key; `StunBuff` hard-codes six concrete behaviour types and abuses `DelBehaviour`/`AddBehaviour` (which fire `Dispose`/`Init`) as pause/resume; and `HealthScreen.ChangePercent` — the entire five-level injury overlay with its looping critical tween — has no caller anywhere in the project.

### Высокая важность (15)

#### HIGH-1. `Assets/Game/Scripts/UI/AmmoPresenter.cs:23` — correctness

**Проблема.** `_weapon.Observe(OnWeaponChanged)` is called in OnInit and never released. In this framework `Extensions.Observe` (Plugins/Atomic/Elements/Scripts/Values/Extensions.cs:12) is declared `public static void Observe<T>(...)` — it returns no handle, so this subscription is unreleasable by construction. The character entity's weapon variable therefore holds a strong reference to a destroyed MonoBehaviour forever. Separately, every call to OnWeaponChanged does `ammo.OnStateChanged += UpdateAmmo` on the new weapon without ever detaching the previous weapon's handler, so handlers accumulate one per swap and each one re-runs UpdateAmmo against the *current* weapon.

**Следствие.** After the UI is destroyed (scene reload, HUD teardown) the next weapon change invokes OnWeaponChanged on a dead presenter, and `_statView.gameObject.SetActive` / `_infinityIcon.SetActive` throw MissingReferenceException. Before that, N weapon pickups mean N redundant UpdateAmmo calls plus N string allocations on every single shot fired.

**Как исправить.**

```csharp
Subscribe/Unsubscribe explicitly and track the ammo you are attached to:

```csharp
public class AmmoPresenter : Presenter
{
    [SerializeField] private GameObject _infinityIcon;
    [SerializeField] private StatView _statView;

    private IReactiveVariable<IEntity> _weapon;
    private Ammo _ammo;            // what we are currently attached to

    protected override void OnInit()
    {
        _weapon = GameContext.Instance.GetPlayerContext().GetCharacter().Value.GetWeapon();
    }

    protected override void OnShow()
    {
        _weapon.Subscribe(OnWeaponChanged);
        OnWeaponChanged(_weapon.Value);
    }

    protected override void OnHide()
    {
        _weapon.Unsubscribe(OnWeaponChanged);
        DetachAmmo();
    }

    private void OnWeaponChanged(IEntity weapon)
    {
        DetachAmmo();

        if (weapon != null && weapon.TryGetAmmo(out Ammo ammo))
        {
            _ammo = ammo;
            _ammo.OnStateChanged += UpdateAmmo;
            _statView.SetVisible(true);
            _infinityIcon.SetActive(false);
            UpdateAmmo();
        }
        else
        {
            _infinityIcon.SetActive(true);
            _statView.SetVisible(false);
        }
    }

    private void DetachAmmo()
    {
        if (_ammo == null) return;
        _ammo.OnStateChanged -= UpdateAmmo;
        _ammo = null;
    }

    private void UpdateAmmo() => _statView.SetText(_ammo.GetCount().ToString());
}
```

On the new framework `Observe` returns a `Subscription<T>` — store it and `AddTo(_disposables)` per Docs/BestPractices/UsingSubscriptionsWithDisposeComposite.md instead of the manual Unsubscribe.
```

#### HIGH-2. `Assets/Game/Scripts/UI/AmmoPresenter.cs:50` — correctness

**Проблема.** `OnHide` does `if (_weapon.Value.TryGetAmmo(out var ammo))` — unguarded dereference of `_weapon.Value`, which OnWeaponChanged itself proves can be null (line 28 explicitly tests `_weapon.Value != null`). Worse, the subscribe/unsubscribe pair is asymmetric with the Presenter lifecycle: `Presenter.OnInit` runs once from Start (Plugins/Atomic/Presenters/Scripts/Presenter.cs:59-64) while `OnHide` runs on *every* OnDisable (line 48-51). So the ammo handler is detached on the first hide and nothing ever re-attaches it.

**Следствие.** Two concrete failures. (a) Hide or destroy the HUD while the character is unarmed and OnHide throws NullReferenceException, which in Unity aborts the rest of OnDisable. (b) Toggle the HUD off and on once and the ammo counter is frozen for the remainder of the session — it keeps showing the count from before the hide while the player fires.

**Как исправить.**

```csharp
Move subscription into OnShow so it pairs with OnHide, and null-guard. See the full rewrite in the previous finding: `OnShow` does `_weapon.Subscribe(OnWeaponChanged); OnWeaponChanged(_weapon.Value);`, `OnHide` does `_weapon.Unsubscribe(OnWeaponChanged); DetachAmmo();`, and `DetachAmmo` is null-safe:

```csharp
private void DetachAmmo()
{
    if (_ammo == null) return;
    _ammo.OnStateChanged -= UpdateAmmo;
    _ammo = null;
}
```

Also note OnWeaponChanged ignores its own `weapon` parameter and re-reads `_weapon.Value` (lines 26-28) — use the parameter.
```

#### HIGH-3. `Assets/Game/Scripts/UI/DamageScreen/DamageScreenPresenter.cs:16` — correctness

**Проблема.** `_character.GetDamageTakenEvent().Subscribe(OnTakeDamage)` in OnInit, and the class overrides neither OnHide nor OnDispose. `BaseEvent<T>.Subscribe` is `this.OnEvent += action` (Plugins/Atomic/Elements/Scripts/Events/BaseEvent.cs:41) and an `Unsubscribe` exists at line 43 — it is simply never called. The character entity outlives the HUD (DeathBehaviour only disables the entity, it does not destroy it), so the event keeps the presenter alive.

**Следствие.** After the HUD GameObject is destroyed, the next hit on the player invokes OnTakeDamage, which calls `_healthScreen.TakeDamage(...)` on a destroyed MonoBehaviour → MissingReferenceException thrown from inside DamageUseCase.TakeDamage (Gameplay/Entity/Common/TakeDamage/DamageUseCase.cs:11), which aborts damage processing for that hit for every other listener registered after this one.

**Как исправить.**

```csharp
```csharp
public class DamageScreenPresenter : Presenter
{
    [SerializeField] private HealthScreen _healthScreen;

    private IEntity _character;
    private Health _health;

    protected override void OnInit() =>
        _character = GameContext.Instance.GetPlayerContext().GetCharacter().Value;

    protected override void OnShow()
    {
        _character.GetDamageTakenEvent().Subscribe(OnTakeDamage);

        _health = _character.GetHealth();
        _health.OnStateChanged += OnHealthStateChanged;
        OnHealthStateChanged();
    }

    protected override void OnHide()
    {
        _character.GetDamageTakenEvent().Unsubscribe(OnTakeDamage);
        _health.OnStateChanged -= OnHealthStateChanged;
    }

    private void OnTakeDamage(TakeDamageArgs args) =>
        _healthScreen.TakeDamage(args.source.GetDamage().Value);

    private void OnHealthStateChanged() =>
        _healthScreen.ChangePercent(_health.GetPercent());
}
```

This also wires up `HealthScreen.ChangePercent`, which currently has no caller anywhere in the project (see the separate HealthScreen finding).
```

#### HIGH-4. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/TemporaryBuff.cs:27` — correctness

**Проблема.** `Discard` gates both the behaviour removal and OnDiscard behind `IsValid(entity)`, mirroring `Apply` at line 18. But IsValid is a *precondition on current entity state*, not an identity check: `ExtraDamageBuff.IsValid` is `entity.HasWeapon()` and `MoveSpeedBuff.IsValid` is `entity.HasMoveSpeed()`. If that state lapses after Apply, Discard silently no-ops and `entity.DelBehaviour(this)` is never reached, so the buff stays registered as an IEntityFixedUpdate forever.

**Следствие.** Concrete sequence: ExtraDamageBuff is applied to an armed character; the character drops its weapon (DropWeaponUseCase deletes the weapon value); the buff expires. OnFixedUpdate (line 44) calls `BuffUseCase.Discard(entity, _name)`, which removes it from the list and calls `buff.Discard` → IsValid is now false → DelBehaviour never runs. The behaviour is now immortal: every FixedUpdate it calls BuffUseCase.Discard, which runs `buffsEffects.FirstOrDefault(b => b.Name == buffName)` — a boxed IEnumerator plus a display-class closure allocation plus N string comparisons, 50 times a second, for the rest of the session, per orphaned buff.

**Как исправить.**

```csharp
Make Discard unconditional and idempotent; Apply decides, Discard always cleans up:

```csharp
public sealed override void Apply(IEntity entity)
{
    if (!IsValid(entity))
        return;

    _applied = true;
    _currentTime = 0f;
    OnApply(entity);
    entity.AddBehaviour(this);   // add last: OnApply must not be ticked mid-setup
}

public sealed override void Discard(IEntity entity)
{
    if (!_applied)
        return;

    _applied = false;
    entity.DelBehaviour(this);   // always runs, regardless of current entity state
    OnDiscard(entity);
}
```

with `private bool _applied;` added next to `_currentTime`. Each concrete OnDiscard must then tolerate the state it depended on being gone — see the ExtraDamageBuff finding for the captured-reference fix that makes this safe.
```

#### HIGH-5. `Assets/Game/Scripts/Gameplay/Entity/Common/Buffs/BuffUseCase.cs:17` — correctness

**Проблема.** `buffsEffects.Add(buff)` runs before `buff.Apply(character)`, and Apply is allowed to refuse (TemporaryBuff.cs:18 `if (IsValid(entity))`). There is no rollback, and Apply returns void so the caller cannot tell. Result: a buff that declined to apply is permanently recorded as active.

**Следствие.** Apply ExtraDamageBuff to an entity with no weapon (an enemy spawned unarmed, or a character between drop and pickup): `IsValid` is false, so no behaviour is attached and nothing will ever tick it to expiry, but it sits in the ReactiveList forever. Two consequences: `CanApply` (line 27) returns false for that buff name for the rest of the session, so the ability silently stops working; and `BuffVFXBehaviour.OnItemAdded` (Gameplay/Entity/Common/Buffs/BuffVFXBehaviour.cs:33) already fired, so the particle system is switched on and never switched off.

**Как исправить.**

```csharp
Let Apply report success and only commit on success:

```csharp
public static bool Apply(IEntity character, BuffBase buff)
{
    IList<BuffBase> buffs = character.GetBuffsEffects();

    for (int i = 0, count = buffs.Count; i < count; i++)
        if (buffs[i].Id == buff.Id)
            return false;

    if (!buff.Apply(character))   // Apply returns bool now
        return false;

    buffs.Add(buff);              // commit only after Apply succeeded
    return true;
}
```

and in TemporaryBuff:

```csharp
public sealed override bool Apply(IEntity entity)
{
    if (!IsValid(entity)) return false;
    _applied = true; _currentTime = 0f;
    OnApply(entity);
    entity.AddBehaviour(this);
    return true;
}
```

`BuffBase.Apply`/`Discard` change to `public abstract bool Apply(IEntity entity);`. The indexed `for` also removes the LINQ allocation — see the separate BuffUseCase LINQ finding.
```

#### HIGH-6. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/ExtraDamageBuff.cs:28` — correctness

**Проблема.** OnDiscard re-resolves `entity.GetWeapon().Value` instead of using the weapon OnApply actually modified (line 21). `Weapon` is `IReactiveVariable<IEntity>` (CodeGen/EntityAPI.cs:50) and `PickUpWeaponUseCase.cs:22` reassigns it mid-game: `character.GetWeapon().Value = characterWeapon`, where `characterWeapon` comes from `gameContext.GetWeaponCatalog().GetWeapon(...)` — a shared, long-lived catalog instance. DamageAspect.cs:20 has the identical bug.

**Следствие.** Pick up a new weapon while ExtraDamageBuff is active and on expiry the bonus is subtracted from the *new* weapon and its cooldown is doubled, while the old weapon keeps `+_extraDamage` and its halved cooldown permanently. Because both weapons are catalog singletons shared across every pickup, the corruption is permanent for the session and compounds with each buff cycle: the old weapon becomes progressively stronger and faster, the new one progressively weaker and slower.

**Как исправить.**

```csharp
Capture the exact weapon (and its original cooldown) at Apply time and operate on that:

```csharp
public sealed class ExtraDamageBuff : TemporaryBuff
{
    private readonly int _extraDamage;

    private IEntity _weapon;          // the weapon we actually modified
    private float _originalCooldown;  // restore by value, not by arithmetic

    public ExtraDamageBuff(ExtraDamageBuffConfig config) : base(config) =>
        _extraDamage = config.Damage;

    protected override bool IsValid(IEntity entity) => entity.HasWeapon();

    protected override void OnApply(IEntity entity)
    {
        _weapon = entity.GetWeapon().Value;
        if (_weapon == null) return;

        _weapon.GetDamage().Value += _extraDamage;

        Cooldown cooldown = _weapon.GetWeaponCooldown();
        _originalCooldown = cooldown.Duration;
        cooldown.SetDuration(_originalCooldown * 0.5f);
    }

    protected override void OnDiscard(IEntity entity)
    {
        if (_weapon == null) return;

        _weapon.GetDamage().Value -= _extraDamage;
        _weapon.GetWeaponCooldown().SetDuration(_originalCooldown);
        _weapon = null;
    }
}
```

Restoring `_originalCooldown` by value rather than `* 2f` also removes the drift when two buffs touch the same cooldown. Apply the same captured-reference pattern to DamageAspect.
```

#### HIGH-7. `Assets/Game/Scripts/Gameplay/Entity/Aspects/MoveSpeedAspect.cs:11` — correctness

**Проблема.** `[SerializeField] private float _multiplier;` has no initializer, so a freshly created MoveSpeedAspect asset has `_multiplier == 0`. Apply (line 15) then does `moveSpeed.Value *= 0` and Discard (line 20) does `moveSpeed.Value /= 0`. The multiply/divide pair is also not idempotent and EffectAreaBehaviour.cs:34/40 calls Apply and Discard once per raw trigger event with no refcount.

**Следствие.** Two distinct failures from one line. (a) Default asset: walking into the area sets move speed to exactly 0 (character frozen), walking out sets it to `0f / 0f` = NaN — every subsequent `transform.position += direction * speed * dt` produces NaN and the character's transform is corrupted beyond recovery. (b) Even with a valid multiplier, a character whose collider fires OnTriggerEnter twice (multi-collider rigs, or two overlapping effect areas sharing this asset) gets speed multiplied twice and divided once, keeping a permanent speed boost after leaving.

**Как исправить.**

```csharp
Default the multiplier, reject degenerate values, and make the modifier additive-and-refcounted rather than multiplicative. Minimal correctness fix:

```csharp
[SerializeField, Min(0.01f)] private float _multiplier = 1.5f;

public override void Apply(IEntity entity)
{
    if (_multiplier <= 0f) return;
    if (entity.TryGetMoveSpeed(out var moveSpeed))
        moveSpeed.Value *= _multiplier;
}

public override void Discard(IEntity entity)
{
    if (_multiplier <= 0f) return;
    if (entity.TryGetMoveSpeed(out var moveSpeed))
        moveSpeed.Value /= _multiplier;
}
```

The structural fix, and what the framework docs intend (Docs/BestPractices/UsingExpressions.md), is to stop mutating the base stat at all. Give the entity a base speed plus a multiplier collection and expose the product as an expression, so stacking and unstacking are order-independent and exact:

```csharp
// installer
entity.AddBaseMoveSpeed(new Const<float>(_moveSpeed));
entity.AddMoveSpeedMultipliers(new ReactiveList<float>());
entity.AddMoveSpeed(new FloatMulExpression(
    entity.GetBaseMoveSpeed(),
    entity.GetMoveSpeedMultipliers()));

// aspect
public override void Apply(IEntity entity) =>
    entity.GetMoveSpeedMultipliers().Add(_multiplier);

public override void Discard(IEntity entity) =>
    entity.GetMoveSpeedMultipliers().Remove(_multiplier);
```
```

#### HIGH-8. `Assets/Game/Scripts/Gameplay/Ability/Content/StunAbilityConfig.cs:33` — correctness

**Проблема.** The input handler calls `AbilityUseCase.Use(ability, target)` and then, as a separate unconditional statement, `BuffUseCase.Apply(target, _stunBuff.CreateBuff())`. `AbilityUseCase.Use` (PlayerContext/Ability/AbilityUseCase.cs:32-42) returns void and bails out early when `GetTargetCondition().Invoke(target)` is false — which here means insufficient mana (line 21-22). The buff application sits outside that flow, and the ability's own TargetAction (line 24) does nothing but spend mana.

**Следствие.** With zero mana, pressing R and hitting a target still applies the full stun — mana is never spent and the condition is bypassed. The ability's cost is entirely optional. It also double-dispatches: the stun lands via the raw call, not via the registered TargetAction, so anything that listens to TargetEvent to drive VFX/SFX/analytics never fires on a free stun, and anything that reuses this ability through AbilityUseCase (AI, scripted events) gets an ability that spends mana and does nothing.

**Как исправить.**

```csharp
Put the effect in the action, where the condition guards it, and keep input out of the config (see the WhenUpdate finding):

```csharp
protected override void Install(Ability ability, IPlayerContext context)
{
    ability.AddTargetAbilityTag();
    ability.AddManaCost(new Const<int>(_manaCost));
    ability.AddTargetEvent(new BaseEvent<IEntity>());

    ability.AddTargetCondition(new BaseFunction<IEntity, bool>(target =>
        target != null &&
        ManaUseCase.Enough(context, _manaCost) &&
        BuffUseCase.CanApply(target, _stunBuff)));

    ability.AddTargetAction(new BaseAction<IEntity>(target =>
    {
        ManaUseCase.Spend(context, _manaCost);
        BuffUseCase.Apply(target, _stunBuff.CreateBuff());
    }));
}
```

The caller then only raises intent: `if (RayCastUseCase.RayCastTarget(out var target, _layerMask)) AbilityUseCase.Use(ability, target);` — and the condition now also pre-checks CanApply, so a target that is already stunned no longer burns mana.
```

#### HIGH-9. `Assets/Game/Scripts/Gameplay/Entity/Aspects/JumpAspect.cs:13` — correctness

**Проблема.** Apply uses `AddJumpableTag`/`AddJumpForce`/`AddJumpEvent`/`AddJumpCondition` and Discard uses the matching `Del*`. But `Entity.AddTag` and `Entity.AddValue` are `TryAdd`-based and return false when the key already exists (Plugins/Atomic/Entities/Scripts/Entity/Plain/Entity_Values.cs:79-88, Entity_Tags.cs:21-29), while `DelTag`/`DelValue` remove unconditionally. All four bool returns are discarded. JumpBuff.cs:20-34 duplicates this code verbatim, so the aspect and the buff can both think they own the jump mechanic.

**Следствие.** Non-idempotent in the one direction that matters. Two overlapping effect areas sharing this aspect (or a JumpBuff active while the character stands in a jump area): the second Apply is a complete no-op, and the first Discard deletes the tag, force, event and condition. The character loses jump while still standing in the area that grants it, and the second Discard then deletes nothing — so JumpUseCase.Jump (Gameplay/Entity/Common/Jump/JumpUseCase.cs:10) fails the HasJumpableTag check and jump is dead until the player leaves and re-enters. Anything subscribed to the deleted BaseEvent instance is also silently orphaned.

**Как исправить.**

```csharp
Refcount the grant on the entity so the last Discard wins, and share one implementation between the aspect and the buff:

```csharp
public static class JumpUseCase
{
    public static void AddJumpSource(IEntity entity, float jumpForce)
    {
        int count = entity.TryGetJumpSourceCount(out var c) ? c.Value : 0;

        if (count == 0)
        {
            entity.AddJumpableTag();
            entity.AddJumpForce(jumpForce);
            entity.AddJumpEvent(new BaseEvent());
            entity.AddJumpCondition(new BaseFunction<bool>(entity.GetHealth().Exists));
        }

        entity.SetJumpSourceCount(count + 1);
    }

    public static void DelJumpSource(IEntity entity)
    {
        if (!entity.TryGetJumpSourceCount(out var c) || c.Value <= 0)
            return;

        int count = c.Value - 1;
        entity.SetJumpSourceCount(count);

        if (count > 0) return;

        entity.DelJumpableTag();
        entity.DelJumpForce();
        entity.DelJumpEvent();
        entity.DelJumpCondition();
    }
}
```

Then `JumpAspect.Apply` is `JumpUseCase.AddJumpSource(entity, _jumpForce)` and `Discard` is `JumpUseCase.DelJumpSource(entity)`; JumpBuff.OnApply/OnDiscard call the same two methods and the duplicated block disappears. Note `entity.GetHealth().Exists` is a method group — it replaces the `() => entity.GetHealth().GetCurrent() > 0` closure, removing both the capture and a dictionary lookup from the per-frame condition evaluation (CharacterJumpController.cs:21 invokes this every LateUpdate).
```

#### HIGH-10. `Assets/Game/Scripts/Gameplay/PlayerContext/PlayerContextInstaller.cs:27` — correctness

**Проблема.** `context.AddCharacter(new ReactiveVariable<IEntity>(_playerEntity))` at line 27, but `CharacterSystemInstaller.Install` already did `context.AddCharacter(new ReactiveVariable<IEntity>(_character))` at line 16, invoked from line 22 of this same method. `AddCharacter` is `AddValue`, which is `TryAdd` (Plugins/Atomic/Context/Scripts/Contexts/Plain/Context_Values.cs:50-59): it returns false and changes nothing. The bool return is discarded at both call sites.

**Следствие.** `PlayerContextInstaller._playerEntity` is dead serialized state that looks authoritative in the inspector. A designer who assigns the player prefab there and leaves `CharacterSystemInstaller._character` pointing at something else (or empty) gets the wrong character — or a null one — with no warning, and the freshly allocated ReactiveVariable is immediately garbage. There are now two inspector fields claiming to define the player entity and the losing one fails silently.

**Как исправить.**

```csharp
Delete the duplicate. `CharacterSystemInstaller` owns the character:

```csharp
protected override void Install(IPlayerContext context)
{
    _characterSystem.Install(context);   // adds Character
    _cameraSystem.Install(context);
    _abilitySystem.Install(context);     // reads Character — must come after
    _manaSystem.Install(context);

    context.AddMoveJoystick(_movementJoystick);
    context.AddRotateJoystick(_rotationJoystick);
}
```

and remove `[SerializeField] private SceneEntity _playerEntity;` (line 12). More generally, stop discarding these bools in installers — a failed Add is a configuration error, not a no-op:

```csharp
if (!context.AddCharacter(new ReactiveVariable<IEntity>(_character)))
    Debug.LogError($"{nameof(CharacterSystemInstaller)}: Character already installed", _character);
```
```

#### HIGH-11. `Assets/Game/Scripts/Gameplay/Ability/Content/DashAbilityConfig.cs:34` — idiom-violation

**Проблема.** `ability.WhenUpdate(dt => { if (Input.GetKeyDown(_dashKey)) AbilityUseCase.Use(ability); })` — a per-frame input poll registered from inside a ScriptableObject's Install, with the returned handle thrown away. `BehaviourExtensions.WhenUpdate` returns the `Action<float>` precisely so it can be detached later (`entity.OnUpdated -= handle`). TeleportAbilityConfig.cs:34 and StunAbilityConfig.cs:29 do the same. These closures are live: AbilityRunner.OnUpdate (PlayerContext/Ability/AbillityRunner.cs:27-31) ticks every ability every frame.

**Следствие.** Three costs. (1) The subscription cannot be released — removing or swapping an ability leaves its input closure polling `Input.GetKeyDown` forever against a disposed Ability entity, and `AbilityUseCase.Use` will then hit the entity's cleared value dictionary. (2) Input handling lives in a ScriptableObject, so the ability is unusable by AI or by a replay/network path — there is no way to trigger a dash except a physical keypress, which is the opposite of what Docs/Tutorials/'6. Minimizing Unity.md' and the RCAE flow prescribe. (3) Three abilities mean three delegate invocations plus three legacy `Input` calls per frame for logic that is identical except for a KeyCode.

**Как исправить.**

```csharp
Install only data and behaviour; move input to one reusable behaviour whose handle the entity owns. Config:

```csharp
protected override void Install(Ability ability, IPlayerContext context)
{
    ability.AddBaseAbilityTag();
    ability.AddBaseEvent(new BaseEvent());
    ability.AddCharges(new ReactiveInt(_initialCharges));

    // resolve the character lazily - do not snapshot .Value at install time
    IReactiveVariable<IEntity> character = context.GetCharacter();

    ability.AddBaseCondition(new BaseFunction<bool>(() =>
        ability.GetCharges().Value > 0 &&
        character.Value != null &&
        BuffUseCase.CanApply(character.Value, _dashBuff)));

    ability.AddBaseAction(new BaseAction(() =>
    {
        ability.GetCharges().Value--;
        BuffUseCase.Apply(character.Value, _dashBuff.CreateBuff());
    }));

    ability.AddBehaviour(new AbilityInputBehaviour(_dashKey));
}
```

and one behaviour shared by all three configs:

```csharp
public sealed class AbilityInputBehaviour : IEntityInit, IEntityUpdate
{
    private readonly KeyCode _key;
    private Ability _ability;

    public AbilityInputBehaviour(KeyCode key) => _key = key;

    public void Init(in IEntity entity) => _ability = (Ability) entity;

    public void OnUpdate(in IEntity entity, in float deltaTime)
    {
        if (Input.GetKeyDown(_key))
            AbilityUseCase.Use(_ability);
    }
}
```

Behaviours are removed with `DelBehaviour`, which the framework already tracks — no dangling delegate. Better still, have the behaviour set a `BaseRequest` and let a separate consumer run the RCAE flow (Docs/BestPractices/RequestConditionActionEvent.md), which makes the ability driveable by AI.
```

#### HIGH-12. `Assets/Game/Scripts/Gameplay/Ability/Content/DashAbilityConfig.cs:20` — correctness

**Проблема.** `IEntity character = context.GetCharacter().Value;` resolves the character eagerly at install time and captures the bare `IEntity` into both closures (lines 23 and 28). `IPlayerContext.GetValue<T>` returns `default` for a missing key (Plugins/Atomic/Context/Scripts/Contexts/Plain/Context_Values.cs:22-25), so this depends entirely on `_characterSystem.Install` running before `_abilitySystem.Install` in PlayerContextInstaller.cs:22-24. TeleportAbilityConfig.cs:22 resolves the same thing lazily inside its lambda, so the two configs disagree.

**Следствие.** Two failures. (a) Reorder those two installer lines — or install the ability system from anywhere else — and `context.GetCharacter()` returns null, so line 20 throws NullReferenceException during scene startup, before any gameplay. (b) The captured reference is a snapshot: `Character` is an `IReactiveVariable<IEntity>` precisely because it changes (respawn, possession). After it changes, dash keeps buffing the old entity — on a dead or pooled character it writes to a disposed value dictionary while the live character gets nothing.

**Как исправить.**

```csharp
Capture the reactive variable, not its value, and read `.Value` at invocation time:

```csharp
IReactiveVariable<IEntity> character = context.GetCharacter();

ability.AddBaseCondition(new BaseFunction<bool>(() =>
    ability.GetCharges().Value > 0 &&
    character.Value != null &&
    BuffUseCase.CanApply(character.Value, _dashBuff)));

ability.AddBaseAction(new BaseAction(() =>
{
    ability.GetCharges().Value--;
    BuffUseCase.Apply(character.Value, _dashBuff.CreateBuff());
}));
```

This is also what makes the ordering safe: `GetCharacter()` is still resolved at install, but if you want full independence from installer order, defer that too with `context.GetCharacter()` inside the lambda as TeleportAbilityConfig already does.
```

#### HIGH-13. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/StunBuff.cs:20` — architecture

**Проблема.** OnApply hard-codes six concrete behaviour types — CharacterMoveBehaviour, CharacterRotateBehaviour, CharacterMoveAnimBehaviour, EnemyPatrolBehaviour, EnemyChaseBehaviour, EnemyAttackBehaviour — pulls them off the entity with `DelBehaviour<T>` and stashes them in a `Dictionary<Type, IEntityBehaviour>`, then re-adds them in OnDiscard. `DelBehaviour` invokes `IEntityDisable.Disable` and `IEntityDispose.Dispose` on each, and `AddBehaviour` invokes `Init` then `Enable` (Plugins/Atomic/Entities/Scripts/Entity/Plain/Entity_Behaviours.cs:20-56) — so the full dispose/init lifecycle is being used as pause/resume.

**Следствие.** Four concrete consequences. (1) Any new locomotion or attack behaviour is silently not stunned — a stunned entity keeps acting, with no compile error to catch it. (2) Behaviours that acquire state in Init and release it in Dispose (subscriptions, cached components) are torn down and rebuilt on every stun; any behaviour that registers an event handler in Init and unregisters in Dispose loses and re-adds it, and any that does work in Init pays it again. (3) Restore order is the Dictionary's, not the installer's, so behaviours re-Init in an arbitrary order relative to each other and to the behaviours that were never removed. (4) A Dictionary plus up to six boxed Type keys allocated per stun application.

**Как исправить.**

```csharp
Stun is a state, not surgery on the behaviour list. Add a tag and let the mechanics read it through their existing conditions:

```csharp
public sealed class StunBuff : TemporaryBuff
{
    private readonly float _scale;

    public StunBuff(StunBuffConfig config) : base(config) => _scale = config.ScaleFactor;

    protected override bool IsValid(IEntity entity) => !entity.HasStunnedTag();

    protected override void OnApply(IEntity entity)
    {
        entity.AddStunnedTag();
        entity.GetTransform().localScale = entity.GetBaseScale() * _scale;
        entity.GetAnimator().SetBool(AnimatorParams.IsMoving, false);
    }

    protected override void OnDiscard(IEntity entity)
    {
        entity.DelStunnedTag();
        entity.GetTransform().localScale = entity.GetBaseScale();
    }
}
```

Each affected mechanic then gates itself once, in its installer, with no knowledge of stun's implementation:

```csharp
entity.AddMoveCondition(new AndExpression(
    entity.GetHealth().Exists,
    () => !entity.HasStunnedTag()));
```

That is the composition the docs intend (Docs/BestPractices/UsingExpressions.md, UsingConstantsWithAndExpressions.md): adding a seventh movement behaviour requires no change to StunBuff, nothing is disposed or re-initialised, and the scale is restored from a stored base rather than by inverse arithmetic.
```

#### HIGH-14. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/BuffBase.cs:10` — architecture

**Проблема.** Buff identity is the free-text serialized string `BuffConfig.Name` (BuffConfig.cs:7), exposed as `BuffBase.Name` and compared with `==` in three places in BuffUseCase (lines 14, 27, 37), used as the discard key in TemporaryBuff.cs:45, and used as a `Dictionary<string, ParticleSystem>` key in BuffVFXBehaviour.cs:12/35/41. The field has no initializer and no validation, so it defaults to the empty string on every new asset.

**Следствие.** Create two buff configs and forget to type Names and both are `""`: `CanApply` reports the second as already active, so it silently never applies, and `Discard(entity, "")` removes whichever happens to be first in the list — the wrong buff is torn down while the right one ticks forever. Renaming a Name string in the inspector silently desynchronises it from the BuffVFXBehaviour dictionary keys configured in a different asset, so the VFX stops appearing with no error. Every identity check is also a string comparison inside a LINQ predicate rather than an integer compare.

**Как исправить.**

```csharp
Key on the config asset itself — it is already a unique reference and cannot be mistyped:

```csharp
public abstract class BuffBase : IEntityAspect
{
    private readonly BuffConfig _config;

    public BuffConfig Id => _config;          // identity
    public string Name => _config.Name;       // display only
    public Sprite Icon => _config.Icon;

    protected BuffBase(BuffConfig config) => _config = config;

    public abstract bool Apply(IEntity entity);
    public abstract bool Discard(IEntity entity);
}
```

BuffUseCase then compares references, TemporaryBuff holds `_config` instead of `_name` and calls `BuffUseCase.Discard(entity, Id)`, and BuffVFXBehaviour becomes `Dictionary<BuffConfig, ParticleSystem>` — all three string-keyed lookups become reference compares, and the inspector cannot produce a collision. If the buff type must also be matched by category, add `[SerializeField] private BuffType _type;` (an enum) to BuffConfig rather than reusing the display name.
```

#### HIGH-15. `Assets/Game/Scripts/Gameplay/Entity/Common/Buffs/DiscardAllBuffBehaviour.cs:17` — correctness

**Проблема.** Subscribes `_health.OnHealthChanged += DiscardAllBuffs` in Init and implements only `IEntityInit` — there is no `IEntityDispose`, so the handler is never detached. And the trigger is wrong: `OnHealthChanged` fires on *any* health delta, including healing (Modules/Health/Health.cs:57 `Add`).

**Следствие.** Every buff on an entity is wiped the moment it takes one point of damage or picks up one medkit — a MoveSpeedBuff or ExtraDamageBuff cannot survive a single scratch, which makes the entire buff system nearly unobservable in play. The missing unsubscribe additionally keeps the behaviour (and through `_character`, the entity) reachable from the Health object after the entity is disposed, and a pooled/respawned entity that reuses the same Health instance accumulates one handler per life, so DiscardAll runs N times per health change.

**Как исправить.**

```csharp
Subscribe to death, not to every change, and release the handler:

```csharp
public sealed class DiscardAllBuffBehaviour : IEntityInit, IEntityDispose
{
    private IEntity _entity;
    private Health _health;

    public void Init(in IEntity entity)
    {
        _entity = entity;
        _health = entity.GetHealth();
        _health.OnHealthEmpty += OnHealthEmpty;
    }

    public void Dispose(in IEntity entity)
    {
        _health.OnHealthEmpty -= OnHealthEmpty;
        _health = null;
        _entity = null;
    }

    private void OnHealthEmpty() => BuffUseCase.DiscardAll(_entity);
}
```

`Health.OnHealthEmpty` is the signal DeathBehaviour already uses (Gameplay/Entity/Common/Death/DeathBehaviour.cs:24), so the two stay consistent.
```

### Средняя важность (15)

#### MEDIUM-1. `Assets/Game/Scripts/Gameplay/Entity/Common/Buffs/BuffUseCase.cs:14` — performance

**Проблема.** All four methods use LINQ over `IList<BuffBase>` obtained from `GetBuffsEffects()`, which is an `IReactiveList<BuffBase>` (CodeGen/EntityAPI.cs:90). `Any(b => b.Name == buff.Name)` at lines 14 and 27 and `FirstOrDefault(b => b.Name == buffName)` at line 37 each allocate a closure display class plus a boxed `IEnumerator<BuffBase>` per call, then do N string comparisons through a property indirection. `DiscardAll` (line 53) foreaches the interface, boxing the enumerator again.

**Следствие.** Docs/BestPractices/IteratingReactiveCollections.md is explicit that iterating a reactive collection through its interface boxes the enumerator and that a `for` loop should be preferred over `foreach` even on the concrete type. `Discard` is called from TemporaryBuff.OnFixedUpdate (TemporaryBuff.cs:45) — so in the orphaned-buff case described above this is two heap allocations plus a string scan 50 times a second, indefinitely. `DiscardAllBuffBehaviour` calls DiscardAll on every single health change, healing included.

**Как исправить.**

```csharp
Indexed loops, reference identity, no LINQ (pair this with the BuffConfig-as-identity fix):

```csharp
using System.Collections.Generic;
using Atomic.Entities;

public static class BuffUseCase
{
    public static bool CanApply(IEntity character, BuffConfig config) =>
        IndexOf(character.GetBuffsEffects(), config) < 0;

    public static bool Apply(IEntity character, BuffBase buff)
    {
        IList<BuffBase> buffs = character.GetBuffsEffects();
        if (IndexOf(buffs, buff.Id) >= 0) return false;
        if (!buff.Apply(character)) return false;
        buffs.Add(buff);
        return true;
    }

    public static bool Discard(IEntity character, BuffConfig config)
    {
        IList<BuffBase> buffs = character.GetBuffsEffects();
        int index = IndexOf(buffs, config);
        if (index < 0) return false;

        BuffBase buff = buffs[index];
        buffs.RemoveAt(index);
        buff.Discard(character);
        return true;
    }

    public static void DiscardAll(IEntity character)
    {
        IList<BuffBase> buffs = character.GetBuffsEffects();

        for (int i = buffs.Count - 1; i >= 0; i--)   // reverse: Discard may touch the list
        {
            BuffBase buff = buffs[i];
            buffs.RemoveAt(i);
            buff.Discard(character);
        }
    }

    private static int IndexOf(IList<BuffBase> buffs, BuffConfig config)
    {
        for (int i = 0, count = buffs.Count; i < count; i++)
            if (ReferenceEquals(buffs[i].Id, config))
                return i;
        return -1;
    }
}
```

Zero allocations, integer/reference compares only. Note `DiscardAll` now removes each entry before discarding it, which also fixes the original's hazard of calling Discard on entries while the list is still being enumerated and clearing afterwards. `IList<BuffBase>` rather than `IReactiveList<BuffBase>` is also required here: `IReactiveList<T> : IList<T>, IReadOnlyList<T>` makes `.Count` and the indexer ambiguous when accessed through the reactive interface.
```

#### MEDIUM-2. `Assets/Game/Scripts/UI/Health/HealthPresenter.cs:20` — correctness

**Проблема.** `_character = GameContext.Instance.GetPlayerContext().GetCharacter().Value;` snapshots the character once in OnInit and never observes the `IReactiveVariable<IEntity>` it came from. OnShow (line 26-27) then calls `_character.GetHealth()` twice — two dictionary lookups — and `[SerializeField] private PlayerID ID;` (line 14) is never read, because `GetPlayerContext()` takes no arguments (CodeGen/GameContextAPI.cs:50). AmmoPresenter.cs:14 and :21 and DamageScreenPresenter.cs:15 have the same two problems.

**Следствие.** After a respawn or possession change the HUD displays the health of a dead or pooled entity and never updates again, while `OnHide` unsubscribes from the stale Health object — leaving the live character's Health permanently subscribed via the previous handler if the entity swapped while the presenter was shown. The dead `PlayerID` field is worse than useless: it tells every reader that this presenter is player-scoped when it is hardwired to the one global context, so the first attempt at splitscreen silently binds both HUDs to player one.

**Как исправить.**

```csharp
Observe the character variable and cache Health once per character:

```csharp
public class HealthPresenter : Presenter
{
    [SerializeField] private StatView _statView;

    private IReactiveVariable<IEntity> _character;
    private Health _health;

    protected override void OnInit() =>
        _character = GameContext.Instance.GetPlayerContext().GetCharacter();

    protected override void OnShow()
    {
        _character.Subscribe(OnCharacterChanged);
        OnCharacterChanged(_character.Value);
    }

    protected override void OnHide()
    {
        _character.Unsubscribe(OnCharacterChanged);
        Detach();
    }

    private void OnCharacterChanged(IEntity character)
    {
        Detach();
        if (character == null) return;

        _health = character.GetHealth();          // one lookup
        _health.OnHealthChanged += OnHealthChanged;
        OnHealthChanged(_health.GetCurrent());
    }

    private void Detach()
    {
        if (_health == null) return;
        _health.OnHealthChanged -= OnHealthChanged;
        _health = null;
    }

    private void OnHealthChanged(int health) => _statView.SetText(health.ToString());
}
```

Delete the `ID` field, or make it real by giving PlayerContext a per-ID lookup — do not leave it as decoration.
```

#### MEDIUM-3. `Assets/Game/Scripts/UI/Health/HealthPresenter.cs:20` — idiom-violation

**Проблема.** `GameContext.Instance` is reached directly from inside the presenter. AmmoPresenter.cs:21 and DamageScreenPresenter.cs:15 do the same, as do InteractMarkerHighlightBehaviour.cs:15 and InteractMarkerUIBehaviour.cs:23. Nothing is injected; the dependency is invisible at the type level.

**Следствие.** The presenter cannot be instantiated or tested without a live scene containing a GameContext singleton, cannot be bound to a second context, and its Start-time ordering silently depends on the singleton having already installed `PlayerContext` and `Character`. Because `Context.GetValue<T>` returns `default` for a missing key rather than throwing (Plugins/Atomic/Context/Scripts/Contexts/Plain/Context_Values.cs:22-25), a HUD that initialises one frame too early gets a null context and NREs on `.GetCharacter()` with no indication of the real cause.

**Как исправить.**

```csharp
Inject the context from the scene boundary and fail loudly if it is absent:

```csharp
public class HealthPresenter : Presenter
{
    [SerializeField] private SceneContext _contextSource;   // assigned in the scene
    [SerializeField] private StatView _statView;

    private IReactiveVariable<IEntity> _character;

    protected override void OnInit()
    {
        IPlayerContext player = _contextSource.GetPlayerContext();

        if (player == null)
        {
            Debug.LogError($"{nameof(HealthPresenter)}: PlayerContext is not installed", this);
            return;
        }

        _character = player.GetCharacter();
    }
}
```

For presenters created from code, pass `IPlayerContext` to a constructor or an `Install(IPlayerContext)` method instead. The one place a singleton lookup is acceptable is the scene-composition root that wires these fields — not the leaves.
```

#### MEDIUM-4. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/StunBuff.cs:27` — unity-misuse

**Проблема.** `entity.GetAnimator().SetBool("IsMoving", false)` uses a string literal, while the project already caches exactly this parameter: `CharacterMoveAnimBehaviour.cs:9` has `private static readonly int IS_MOVING = Animator.StringToHash("IsMoving");`. The two are independent copies of the same magic string.

**Следствие.** `Animator.SetBool(string, bool)` hashes the string on every call, and more importantly the two spellings can drift: rename the parameter in the Animator controller, fix CharacterMoveAnimBehaviour, and StunBuff keeps writing to a parameter that no longer exists — Animator silently ignores unknown names, so the stunned character keeps playing its run animation with no error anywhere. Docs/BestPractices/SharedConstants.md covers exactly this.

**Как исправить.**

```csharp
Hoist the hashes into one shared constants type and use the int overload everywhere:

```csharp
public static class AnimatorParams
{
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");
    public static readonly int Fire     = Animator.StringToHash("Fire");
    public static readonly int Death    = Animator.StringToHash("Death");
}
```

StunBuff becomes `entity.GetAnimator().SetBool(AnimatorParams.IsMoving, false);` and `CharacterMoveAnimBehaviour`'s private `IS_MOVING` is deleted in favour of `AnimatorParams.IsMoving`. One declaration, one place to rename.
```

#### MEDIUM-5. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/StunBuff.cs:18` — correctness

**Проблема.** `entity.GetTransform().localScale *= 1.5f` in OnApply and `/= 1.5f` in OnDiscard. The magic number is duplicated across the two methods, the base scale is never recorded, and the restore is an inverse float operation rather than an assignment.

**Следствие.** Any path that applies the visual twice before discarding — or that discards without a matching apply, which TemporaryBuff.Discard's `IsValid` gate makes possible (see that finding) — leaves the character permanently at 1.5x or 0.67x scale. Even in the happy path, `(v * 1.5f) / 1.5f` is not bit-identical to `v` in IEEE-754, so repeated stuns drift the scale away from its authored value. Changing the factor also requires editing two lines that are 19 lines apart.

**Как исправить.**

```csharp
Store the base scale and restore by assignment:

```csharp
[SerializeField] private float _scaleFactor = 1.5f;   // on StunBuffConfig
```

```csharp
private Vector3 _baseScale;

protected override void OnApply(IEntity entity)
{
    Transform transform = entity.GetTransform();
    _baseScale = transform.localScale;
    transform.localScale = _baseScale * _scaleFactor;
    ...
}

protected override void OnDiscard(IEntity entity) =>
    entity.GetTransform().localScale = _baseScale;
```

Better still, keep the authored scale on the entity (`entity.AddBaseScale(transform.localScale)` in the installer) so every visual effect restores from one source of truth rather than from whatever the scale happened to be when it started.
```

#### MEDIUM-6. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/ExtraDamageBuff.cs:7` — unity-misuse

**Проблема.** `[CreateAssetMenu(fileName = "ExtraDamageBuffConfig", menuName = "Gameplay/ExtraDamageBuffConfig")]` is applied to `ExtraDamageBuff`, which derives from `TemporaryBuff` → `BuffBase`, a plain class — not ScriptableObject. The attribute also duplicates the fileName and near-duplicates the menuName of the real asset type, `ExtraDamageBuffConfig` (ExtraDamageBuffConfig.cs:5).

**Следствие.** Unity emits a console error on every domain reload: CreateAssetMenuAttribute on ExtraDamageBuff will be ignored as ExtraDamageBuff is not derived from ScriptableObject. It also puts a second, misleading 'ExtraDamageBuffConfig' entry in the Assets/Create menu tree under a different path, so a designer can pick the wrong one and get nothing. It is the only buff implementation carrying the attribute — JumpBuff, MoveSpeedBuff and StunBuff correctly have none — which is how it was missed.

**Как исправить.**

```csharp
Delete lines 7 and the now-unneeded `using UnityEngine;` (line 3) from ExtraDamageBuff.cs. The class becomes:

```csharp
using Atomic.Entities;
using Modules.Gameplay;

namespace Game
{
    public sealed class ExtraDamageBuff : TemporaryBuff
    {
        ...
    }
}
```

The asset-creation entry already exists where it belongs, on ExtraDamageBuffConfig. Mark all four buff implementations `sealed` while you are there — none is subclassed and it lets the JIT devirtualise the OnApply/OnDiscard calls.
```

#### MEDIUM-7. `Assets/Game/Scripts/UI/Health/HealthScreen.cs:34` — dead-code

**Проблема.** `ChangePercent` — and with it the entire five-level injury state machine, `SetLowInjure` through `SetCriticalInjure`, `DisableInjure`, `StartCriticalTween`, `StopCriticalTween`, `staticIndication`, `dynamicIndication` and `criticalTween`, roughly 90 of this file's 127 lines — has no caller. A project-wide grep for `ChangePercent` returns only this declaration. The only live path is `TakeDamage` (line 57), called from DamageScreenPresenter.cs:21.

**Следствие.** The injury overlay never renders: `staticIndication` keeps the alpha Awake left it at and the looping critical tween never starts, so the player gets no low-health feedback at all. The `[Button]` attributes mean it works when poked by hand in the inspector, which is how dead UI code like this survives. Two serialized Image references also sit on the prefab looking wired up.

**Как исправить.**

```csharp
Drive it from health, in the presenter that already has the character (see the DamageScreenPresenter fix above):

```csharp
protected override void OnShow()
{
    _health = _character.GetHealth();
    _health.OnStateChanged += OnHealthStateChanged;
    OnHealthStateChanged();
}

protected override void OnHide() => _health.OnStateChanged -= OnHealthStateChanged;

private void OnHealthStateChanged() => _healthScreen.ChangePercent(_health.GetPercent());
```

`Health.GetPercent()` and `OnStateChanged` already exist (Modules/Health/Health.cs:10, :51). If the overlay is genuinely not wanted, delete the 90 dead lines and the two Image fields instead — leaving it in place as inspector-only code guarantees the next person re-implements it.
```

#### MEDIUM-8. `Assets/Game/Scripts/UI/Health/HealthScreen.cs:77` — unity-misuse

**Проблема.** `criticalTween` is created with `SetLoops(-1, LoopType.Yoyo)` — an infinite tween — and `takeDamageTween` is a Sequence created on every hit. Neither is killed in OnDestroy; the only Kill calls are StopCriticalTween (line 91, itself unreachable since ChangePercent has no caller) and the re-entrancy guard at line 61.

**Следствие.** DOTween holds the tween and its target Image in its global update loop. When the HUD is destroyed — scene reload, HUD teardown — the infinite tween keeps running against a destroyed Image: with safe mode on, DOTween logs an error every frame and kills the tween; with safe mode off (common in release settings) it is a NullReferenceException inside DOTween's update, which takes down every other tween in the same frame. The per-hit Sequence has the same exposure if the player dies mid-fade.

**Как исправить.**

```csharp
Link the tweens to the GameObject so DOTween kills them automatically:

```csharp
this.takeDamageTween = DOTween
    .Sequence()
    .Append(this.takeDamageView.DOFade(Mathf.Clamp(damage / 10.0f, 0.2f, 1.0f), 0.05f))
    .Append(this.takeDamageView.DOFade(0, 0.2f))
    .SetLink(this.gameObject);

this.criticalTween = this.dynamicIndication
    .DOFade(0.85f, 0.75f)
    .SetEase(Ease.InOutSine)
    .SetLoops(-1, LoopType.Yoyo)
    .SetLink(this.gameObject);
```

and add the explicit teardown as well, since SetLink depends on DOTween's component being present:

```csharp
private void OnDestroy()
{
    this.takeDamageTween?.Kill();
    this.criticalTween?.Kill();
    this.takeDamageTween = null;
    this.criticalTween = null;
}
```
```

#### MEDIUM-9. `Assets/Game/Scripts/Gameplay/Triggers/EnemyTrigger.cs:21` — correctness

**Проблема.** `OnTriggerExit` sets `_enemies[i].GetTarget().Value = null` for every enemy in the array whenever any player-tagged entity leaves, without checking whether that entity is the enemy's current target and without tracking how many players are still inside the volume.

**Следствие.** Two concrete failures. (a) Two player-tagged entities inside the volume: when the first leaves, every enemy's target is cleared even though the second is still standing there, so the enemies go idle with a player in front of them until someone re-crosses the boundary. (b) An enemy that acquired a different target by another route (a chase behaviour, another trigger) has it wiped by an unrelated player leaving this volume. Unity also fires OnTriggerExit when the *collider* is disabled or destroyed, which DeathBehaviour.cs:36-37 does on death — so a player dying inside the volume clears all targets through this path too, which may or may not be intended but is not expressed anywhere.

**Как исправить.**

```csharp
Only clear the target you set, and refcount occupancy:

```csharp
[RequireComponent(typeof(Collider))]
public sealed class EnemyTrigger : MonoBehaviour
{
    [SerializeField] private SceneEntity[] _enemies;

    private readonly HashSet<IEntity> _inside = new();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetEntity(out IEntity entity) || !entity.HasPlayerTag()) return;
        if (!_inside.Add(entity)) return;

        SetTarget(entity);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetEntity(out IEntity entity) || !entity.HasPlayerTag()) return;
        if (!_inside.Remove(entity)) return;

        // hand over to whoever is still inside, otherwise clear only our own target
        IEntity next = null;
        foreach (IEntity candidate in _inside) { next = candidate; break; }

        for (int i = 0; i < _enemies.Length; i++)
        {
            IReactiveVariable<IEntity> target = _enemies[i].GetTarget();
            if (ReferenceEquals(target.Value, entity))
                target.Value = next;
        }
    }

    private void SetTarget(IEntity entity)
    {
        for (int i = 0; i < _enemies.Length; i++)
            _enemies[i].GetTarget().Value = entity;
    }
}
```

The `HashSet.Add`/`Remove` guards also make this robust against multi-collider player rigs, which fire Enter and Exit once per collider. Longer term this belongs on the entity as a TriggerEvents value plus a behaviour rather than a MonoBehaviour reaching into an array of SceneEntities (Docs/Tutorials/'6. Minimizing Unity.md', '5. Interaction Between Entities.md') — as written it cannot be spawned, pooled or reused, and `using System;` on line 1 is unused.
```

#### MEDIUM-10. `Assets/Game/Scripts/Gameplay/Ability/Ability.cs:5` — architecture

**Проблема.** `public class Ability : Entity` is an empty subclass — a constructor that forwards to base and nothing else. Its only effect is that the generated AbilityAPI extension methods are typed `this Ability obj` (AbilityAPI.cs:42-285) rather than `this IEntity obj`, and that `AbilityUseCase` (PlayerContext/Ability/AbilityUseCase.cs) takes `Ability` instead of `IEntity`.

**Следствие.** Abilities are now a closed world. An Ability cannot be passed to any IEntity-based mechanic, cannot be put in the EntityWorld alongside other entities, and cannot carry a behaviour written against `IEntity` without a cast — which is exactly what AbilityInputBehaviour has to do. Conversely no other entity can hold the BaseAction/BaseCondition/BaseEvent trio, so the RCAE flow implemented in AbilityUseCase is unreusable: a weapon or a trap that wants the same condition-action-event structure has to duplicate it. Docs/BestPractices/PreferAbstractInterfaces.md makes the opposite call.

**Как исправить.**

```csharp
Delete the class and regenerate the API against `IEntity`, distinguishing abilities by tag rather than by type — the tags already exist (AbilityAPI.cs:19-21: BaseAbility, PointAbility, TargetAbility):

```csharp
// AbilityConfig
public IEntity Create(IPlayerContext context)
{
    IEntity ability = new Entity(Name);
    Install(ability, context);
    return ability;
}

protected abstract void Install(IEntity ability, IPlayerContext context);
```

```csharp
// AbilityUseCase
public static bool Use(IEntity ability)
{
    if (!ability.HasBaseAbilityTag()) return false;
    if (!ability.GetBaseCondition().Invoke()) return false;

    ability.GetBaseAction().Invoke();
    ability.GetBaseEvent().Invoke();
    return true;
}
```

Abilities then live in the same world as everything else, share behaviours, and the generated extensions apply to any entity that carries the tag. If a distinct generated API surface is genuinely wanted, express it with a marker interface `IAbilityEntity : IEntity` rather than a concrete subclass, so implementations stay substitutable.
```

#### MEDIUM-11. `Assets/Game/Scripts/Gameplay/PlayerContext/Ability/AbilityUseCase.cs:16` — idiom-violation

**Проблема.** All three overloads invoke the event before the action: `ability.GetBaseEvent().Invoke(); ability.GetBaseAction().Invoke();` (lines 16-17, and the same inversion at 28-29 and 40-41). The RCAE flow the framework documents is Request → Condition → Action → Event, where the event is the post-hoc notification: 'Action — executes the main effect if the condition passes. Event — triggers notifications or side effects after the action' (Docs/BestPractices/RequestConditionActionEvent.md).

**Следствие.** Every event listener observes the world as it was *before* the ability resolved. A dash VFX listener reading the charge count sees the pre-decrement value; a teleport listener reading the character transform sees the old position, so a trail or camera-shake effect anchors to where the player was standing rather than where they landed. The ability also returns void, so callers cannot tell a refused use from a successful one — StunAbilityConfig.cs:33-37 works around exactly that by applying its effect outside the flow.

**Как исправить.**

```csharp
Swap the order and report the outcome:

```csharp
public static bool Use(IEntity ability)
{
    if (!ability.HasBaseAbilityTag()) return false;
    if (!ability.GetBaseCondition().Invoke()) return false;

    ability.GetBaseAction().Invoke();
    ability.GetBaseEvent().Invoke();
    return true;
}

public static bool Use(IEntity ability, Vector3 position)
{
    if (!ability.HasPointAbilityTag()) return false;
    if (!ability.GetPointCondition().Invoke(position)) return false;

    ability.GetPointAction().Invoke(position);
    ability.GetPointEvent().Invoke(position);
    return true;
}

public static bool Use(IEntity ability, IEntity target)
{
    if (!ability.HasTargetAbilityTag()) return false;
    if (!ability.GetTargetCondition().Invoke(target)) return false;

    ability.GetTargetAction().Invoke(target);
    ability.GetTargetEvent().Invoke(target);
    return true;
}
```

The bool return is what lets StunAbilityConfig move its buff into the action and lets callers play a 'denied' cue.
```

#### MEDIUM-12. `Assets/Game/Scripts/Gameplay/PlayerContext/Ability/AbilitySystemInstaller.cs:19` — idiom-violation

**Проблема.** `context.GetAbilities().Add(_dashAbility.name, _dashAbility.Create(context))` keys the ability dictionary on `ScriptableObject.name` — the asset's filename — while `AbilityConfig.Name` (AbilityConfig.cs:7) is a separate serialized string used for the Ability entity's name. Two competing identities for the same object, one of them a filename.

**Следствие.** Renaming the DashAbilityConfig asset in the Project window silently changes the dictionary key, breaking any lookup by name with no compile error and no runtime warning. `ScriptableObject.name` is also not guaranteed to be loaded in all build/addressable paths. And `ReactiveDictionary.Add` throws on a duplicate key, so two ability assets with the same filename in different folders crash scene startup. Meanwhile `AbilityConfig.Name` — the field that looks like the identity — is used only for the entity's display name.

**Как исправить.**

```csharp
Key on the config asset reference, which cannot be mistyped or renamed out from under you:

```csharp
public void Install(IPlayerContext context)
{
    var abilities = new ReactiveDictionary<AbilityConfig, IEntity>();
    context.AddAbilities(abilities);

    for (int i = 0; i < _configs.Length; i++)
    {
        AbilityConfig config = _configs[i];
        if (config == null) continue;

        if (!abilities.TryAdd(config, config.Create(context)))
            Debug.LogError($"Duplicate ability config: {config.name}", config);
    }

    context.AddController<AbilityRunner>();
}
```

with `[SerializeField] private AbilityConfig[] _configs;` replacing the three typed fields (lines 12-14) — which also removes the hardcoded three-ability limit, so adding a fourth ability is a content change rather than a code change. If a string key is genuinely required by a save format, put an explicit `[SerializeField] private string _id;` on AbilityConfig and validate it in OnValidate; do not reuse the filename.
```

#### MEDIUM-13. `Assets/Game/Scripts/Gameplay/PlayerContext/Ability/AbillityRunner.cs:20` — correctness

**Проблема.** `Init` copies `context.GetAbilities()` into the private `List<Ability> _ability` once and nothing ever refreshes it. The source is a `ReactiveDictionary<string, Ability>` (AbilitySystemInstaller.cs:18) whose entire purpose is to notify on change — `OnItemAdded`/`OnItemRemoved` are never subscribed. Abilities are also never removed from `_ability`, so Dispose (line 57-61) disposes entities the context may no longer own.

**Следствие.** Any ability granted at runtime — and DashPickUpInstaller.cs:21 reaches for the player context to do exactly this kind of thing — is added to the ReactiveDictionary but never to `_ability`, so it is never Init'd, never Enabled and never updated. Its input closure never runs and the ability is inert, with nothing logged. An ability removed at runtime keeps being ticked against a disposed value dictionary.

**Как исправить.**

```csharp
Track the collection reactively:

```csharp
public sealed class AbilityRunner : IContextInit<IPlayerContext>, IContextDispose,
    IContextUpdate, IContextFixedUpdate, IContextLateUpdate, IContextEnable, IContextDisable
{
    private readonly List<IEntity> _abilities = new();
    private IReactiveDictionary<AbilityConfig, IEntity> _source;

    public void Init(IPlayerContext context)
    {
        _source = context.GetAbilities();
        _source.OnItemAdded += OnAbilityAdded;
        _source.OnItemRemoved += OnAbilityRemoved;

        foreach (var pair in _source)
            OnAbilityAdded(pair.Key, pair.Value);
    }

    public void Dispose(IContext context)
    {
        _source.OnItemAdded -= OnAbilityAdded;
        _source.OnItemRemoved -= OnAbilityRemoved;

        for (int i = 0; i < _abilities.Count; i++)
            _abilities[i].Dispose();

        _abilities.Clear();
    }

    private void OnAbilityAdded(AbilityConfig key, IEntity ability)
    {
        _abilities.Add(ability);
        ability.Init();
        ability.Enable();
    }

    private void OnAbilityRemoved(AbilityConfig key, IEntity ability)
    {
        if (!_abilities.Remove(ability)) return;
        ability.Disable();
        ability.Dispose();
    }

    public void OnUpdate(IContext context, float deltaTime)
    {
        for (int i = 0, count = _abilities.Count; i < count; i++)
            _abilities[i].OnUpdate(deltaTime);
    }
    // OnFixedUpdate / OnLateUpdate identical, indexed
}
```

The indexed `for` loops also replace the six `foreach` passes over the list, per Docs/BestPractices/IteratingReactiveCollections.md. Note the file is named AbillityRunner.cs (double L) while the class is AbilityRunner — rename the file.
```

#### MEDIUM-14. `Assets/Game/Scripts/Gameplay/PlayerContext/Controllers/CharacterJumpController.cs:19` — performance

**Проблема.** OnLateUpdate evaluates `_character.HasJumpableTag()` and then `_character.GetJumpCondition().Invoke()` on every single frame, and only then checks `Input.GetKey(KeyCode.V)`. The condition itself is the closure installed by JumpAspect.cs:18 / JumpBuff.cs:25, `() => entity.GetHealth().GetCurrent() > 0`, so each evaluation is a dictionary lookup for JumpCondition, a delegate invoke, and a second dictionary lookup for Health. JumpUseCase.Jump (Gameplay/Entity/Common/Jump/JumpUseCase.cs:10-13) then repeats both checks.

**Следствие.** Three dictionary lookups, two delegate invocations and a duplicated tag check every frame, for a result that is discarded unless V is held — and then the whole thing is recomputed inside JumpUseCase. `KeyCode.V` is also hardcoded here while every ability takes its key from a serialized field, so this one binding cannot be remapped. `GetJumpForce` compounds it: `JumpForce` is declared as a plain `float` (CodeGen/EntityAPI.cs:42) stored in a `Dictionary<int, object>` (Entity_Values.cs:15), so every read unboxes and every JumpAspect.Apply boxes.

**Как исправить.**

```csharp
Check input first and let JumpUseCase own the validation it already performs:

```csharp
public sealed class CharacterJumpController : IContextInit<IPlayerContext>, IContextLateUpdate
{
    [SerializeField] private KeyCode _jumpKey = KeyCode.V;

    private IReactiveVariable<IEntity> _character;

    public void Init(IPlayerContext context) => _character = context.GetCharacter();

    public void OnLateUpdate(IContext context, float deltaTime)
    {
        if (!Input.GetKeyDown(_jumpKey))   // cheapest test first, and GetKeyDown not GetKey
            return;

        IEntity character = _character.Value;
        if (character != null)
            JumpUseCase.Jump(character);   // performs tag + condition itself
    }
}
```

This also fixes the stale-character snapshot at line 14 (same pattern as the presenters) and the `GetKey` vs `GetKeyDown` bug — as written, holding V applies an impulse every frame. For the boxing, change JumpForce's generated type from `float` to `IValue<float>` (as MoveSpeed already is, `IReactiveVariable<float>`) so the stored object is a reference and reads stop unboxing.
```

#### MEDIUM-15. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/JumpBuff.cs:20` — architecture

**Проблема.** `JumpBuff.OnApply`/`OnDiscard` (lines 20-34) and `JumpAspect.Apply`/`Discard` (JumpAspect.cs:13-27) are byte-for-byte the same four statements each, including the identical `new BaseFunction<bool>(() => entity.GetHealth().GetCurrent() > 0)` closure. Two parallel hierarchies — `ScriptableEntityAspect` and `BuffBase` — implement the same `IEntityAspect` contract (Plugins/Atomic/Extensions/Scripts/Entity/Aspects/IEntityAspect.cs:9-11) over copy-pasted bodies. MoveSpeedAspect/MoveSpeedBuff and DamageAspect/ExtraDamageBuff are the same pairing.

**Следствие.** Every fix has to be made twice and the two copies have already diverged: `MoveSpeedAspect` guards with `TryGetMoveSpeed` inside Apply while `MoveSpeedBuff` uses an `IsValid` override; `DamageAspect` adds a flat `_extraDamage` while `ExtraDamageBuff` also halves the weapon cooldown. The refcounting fix for the jump Add/Del asymmetry would have to be applied in both files, and whichever copy is missed keeps the bug. Three mechanics, six implementations, no shared code.

**Как исправить.**

```csharp
Put the mechanic in one use-case and let both the aspect and the buff delegate to it. Using the JumpUseCase.AddJumpSource/DelJumpSource from the earlier finding:

```csharp
// JumpBuff
public sealed class JumpBuff : TemporaryBuff
{
    private readonly float _jumpForce;

    public JumpBuff(JumpBuffConfig config) : base(config) => _jumpForce = config.JumpForce;

    protected override bool IsValid(IEntity entity) => entity.HasTransform();
    protected override void OnApply(IEntity entity) => JumpUseCase.AddJumpSource(entity, _jumpForce);
    protected override void OnDiscard(IEntity entity) => JumpUseCase.DelJumpSource(entity);
}
```

```csharp
// JumpAspect
[CreateAssetMenu(fileName = "JumpAspect", menuName = "BuffSystem/Aspect/JumpAspect")]
public sealed class JumpAspect : ScriptableEntityAspect
{
    [SerializeField] private float _jumpForce = 2f;

    public override void Apply(IEntity entity) => JumpUseCase.AddJumpSource(entity, _jumpForce);
    public override void Discard(IEntity entity) => JumpUseCase.DelJumpSource(entity);
}
```

Same for move speed (`MoveSpeedUseCase.AddMultiplier`/`DelMultiplier`) and damage (`WeaponUseCase.AddDamageModifier`/`DelDamageModifier`). The aspect/buff classes shrink to a serialized value plus two one-line delegations, and the mechanic — including the refcounting that makes it idempotent — exists exactly once. This is the structure Docs/BestPractices/UsingUtilsForEntityInstallers.md describes.
```

### Низкая важность (2)

#### LOW-1. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/StunBuffConfig.cs:8` — dead-code

**Проблема.** `[field: SerializeField] public float Time { get; private set; }` is never read anywhere; the stun's lifetime comes from the inherited `BuffConfig.Duration` (BuffConfig.cs:9), which is what TemporaryBuff.cs:14 consumes. Two serialized duration fields on the same asset, one of them inert. The same pattern recurs across the ability configs: `StunAbilityConfig._initialCharges` (line 11) and `TeleportAbilityConfig._initialCharges` (line 10) are never passed to `AddCharges` — only DashAbilityConfig does that (line 32) — so those two abilities have no Charges value at all and their conditions silently ignore charges; and `DashAbilityConfig._multiplier` (line 12) is never read. `AmmoPresenter.ID` (line 14) and `HealthPresenter.ID` (line 14) round it out.

**Следствие.** Six serialized fields that a designer can set with no effect and no warning. `StunBuffConfig.Time` is the dangerous one: it sits directly above the field that does work, so setting Time to 3 and leaving Duration at its default 0 produces a stun that expires on the first FixedUpdate — `_currentTime += deltaTime; if (_currentTime >= 0f)` is true immediately. The `_initialCharges` fields mean the stun and teleport abilities appear to be charge-limited in the inspector but are not, so a designer balancing them is tuning a number that does nothing.

**Как исправить.**

```csharp
Delete `Time` from StunBuffConfig — Duration already carries it:

```csharp
[CreateAssetMenu(fileName = "StunBuffConfig", menuName = "BuffSystem/Buff/StunBuffConfig")]
public sealed class StunBuffConfig : BuffConfig
{
    [field: SerializeField] public float ScaleFactor { get; private set; } = 1.5f;

    public override BuffBase CreateBuff() => new StunBuff(this);
}
```

Guard the inherited field so a zero duration cannot ship:

```csharp
public abstract class BuffConfig : ScriptableObject
{
    [field: SerializeField] public string Name { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
    [field: SerializeField, Min(0.01f)] public float Duration { get; private set; } = 1f;

    public abstract BuffBase CreateBuff();
}
```

Then either wire `_initialCharges` up in StunAbilityConfig and TeleportAbilityConfig (`ability.AddCharges(new ReactiveInt(_initialCharges));` plus `ability.GetCharges().Value > 0` in the condition and a decrement in the action, as DashAbilityConfig does) or delete both fields — and delete `DashAbilityConfig._multiplier` and both presenters' `ID` fields.
```

#### LOW-2. `Assets/Game/Scripts/Gameplay/Entity/BuffEffects/MoveSpeedBuffConfig.cs:8` — correctness

**Проблема.** `[field: SerializeField] public int Speed { get; private set; }` is an int, but MoveSpeedBuff.cs:11 reads it into `private readonly float _multiplier` and applies it as `entity.GetMoveSpeed().Value *= _multiplier` (line 18). The name says speed, the use is a multiplier, and the type is integral with no initializer — so it defaults to 0.

**Следствие.** A default MoveSpeedBuffConfig asset multiplies move speed by 0 and then divides by 0 on discard, producing Infinity — the same failure as MoveSpeedAspect, reached from the buff path instead of the aspect path. And because the field is an int, a designer cannot author the obvious value: a 1.5x speed buff is unrepresentable, the nearest choices being 1 (no effect) and 2.

**Как исправить.**

```csharp
```csharp
[CreateAssetMenu(fileName = "MoveSpeedBuffConfig", menuName = "BuffSystem/Buff/MoveSpeedBuffConfig")]
public sealed class MoveSpeedBuffConfig : BuffConfig
{
    [field: SerializeField, Min(0.01f)] public float SpeedMultiplier { get; private set; } = 1.5f;

    public override BuffBase CreateBuff() => new MoveSpeedBuff(this);
}
```

and in MoveSpeedBuff, capture the entity's speed variable at Apply so Discard restores by value rather than by inverse arithmetic — matching the ExtraDamageBuff fix:

```csharp
private readonly float _multiplier;
private IReactiveVariable<float> _moveSpeed;
private float _original;

public MoveSpeedBuff(MoveSpeedBuffConfig config) : base(config) =>
    _multiplier = config.SpeedMultiplier;

protected override bool IsValid(IEntity entity) => entity.HasMoveSpeed();

protected override void OnApply(IEntity entity)
{
    _moveSpeed = entity.GetMoveSpeed();
    _original = _moveSpeed.Value;
    _moveSpeed.Value = _original * _multiplier;
}

protected override void OnDiscard(IEntity entity)
{
    if (_moveSpeed == null) return;
    _moveSpeed.Value = _original;
    _moveSpeed = null;
}
```

Note this restore-by-value form is only correct for one buff at a time; if speeds must stack, use the multiplier-collection plus expression shown in the MoveSpeedAspect finding, and have both the aspect and the buff feed that one collection.
```

