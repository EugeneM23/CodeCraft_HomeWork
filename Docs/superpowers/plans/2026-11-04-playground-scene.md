# Playground Scene Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить тяжёлую сцену `Playground.unity` на graybox-полигон, собираемый скриптом, с оружием, баффами, бессмертными манекенами, зомби и триггер-кнопкой спавна.

**Architecture:** Сцена описывается кодом в editor-скрипте `PlaygroundBuilder` и пересобирается одним вызовом. Два новых рантайм-компонента (манекен и кнопка спавна) следуют существующей идиоме проекта: `MonoEntityInstaller` ставит значения и поведения, поведение реализует `IEntityInit`/`IEntityDispose`. Существующий игровой код не меняется.

**Tech Stack:** Unity 6000.0.38f1, фреймворк Atomic (`Atomic.Entities`, `Atomic.Elements`), `UnityEditor.SceneManagement`, `SerializedObject` для записи приватных сериализованных полей из кода.

## Global Constraints

- Спек: `Docs/superpowers/specs/2026-11-04-playground-scene-design.md`.
- Все комментарии, имена типов, методов и переменных — на английском.
- Приватные поля `_camelCase`, `[SerializeField] private`, константы `UPPER_CASE`, скобки Allman, 4 пробела.
- Новый рантайм-код — в `Assets/Game/Scripts/Gameplay/`, editor-код — в `Assets/Game/Scripts/Editor/`.
- Перемещение и переименование ассетов — только `AssetDatabase.MoveAsset`/`RenameAsset`, никогда файловой системой.
- Сцену сохранять явно через `EditorSceneManager.SaveScene`.
- `-executeMethod` работает только при закрытом редакторе; при открытом — сборка из меню.
- Не коммитить без явной просьбы пользователя.
- В рабочем дереве может лежать чужая незакоммиченная работа: `git add` только по конкретным путям, никогда `git add -A`.

## Отступление от TDD и почему

В проекте нет тестовой инфраструктуры для игрового кода: сборок `Game.Tests.*` не существует, только тесты самого фреймворка в `Assets/Plugins/Atomic`. Заводить asmdef с тестами ради сцены-полигона — расширение задачи за пределы запроса.

Тестовый цикл здесь другой и он реальный: **компиляция в batchmode** (ловит все ошибки типов и сигнатур) плюс **проверочный `-executeMethod`**, который открывает собранную сцену и утверждает её состав. Это исполняемая проверка с бинарным результатом, она и играет роль теста. Каждая задача заканчивается таким прогоном.

---

### Task 1: Бессмертный манекен

**Files:**
- Create: `Assets/Game/Scripts/Gameplay/GameEntities/Content/Dummy/DummyInstaller.cs`
- Create: `Assets/Game/Scripts/Gameplay/GameEntities/Content/Dummy/RestoreHealthOnDamageBehaviour.cs`

**Interfaces:**
- Consumes: `Modules.Gameplay.Health(int health, int max)`, `health.SetCurrent(int)`, `health.GetMax()`; `entity.GetDamageTakenEvent()` → `IEvent<TakeDamageArgs>`; `TakeDamageBloodBehaviour(ParticleSystem bulletBlood, ParticleSystem meleeBlood, Transform originTransform)`.
- Produces: `DummyInstaller : MonoEntityInstaller` с сериализованными полями `_health`, `_bulletBlood`, `_meleeBlood`, `_originTransform`; `RestoreHealthOnDamageBehaviour : IEntityInit, IEntityDispose`.

Манекен не получает `DeathBehaviour`, поэтому не умирает в принципе: `DamageUseCase.TakeDamage` только снижает здоровье и дёргает `DamageTakenEvent`, а смерть обрабатывает отдельное поведение. Восстановление нужно, чтобы здоровье не осталось на нуле и урон продолжал регистрироваться.

- [ ] **Step 1: Написать `RestoreHealthOnDamageBehaviour`**

```csharp
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;

namespace Game
{
    /// <summary>
    /// Refills the entity's health whenever it takes damage, so a target never runs out.
    /// Restoring rather than giving it a huge pool keeps the whole damage path exercised:
    /// health really is reduced and DamageTakenEvent really fires before the refill.
    /// </summary>
    public sealed class RestoreHealthOnDamageBehaviour : IEntityInit, IEntityDispose
    {
        private Health _health;
        private ISignal<TakeDamageArgs> _damageEvent;

        public void Init(IEntity entity)
        {
            _health = entity.GetHealth();
            _damageEvent = entity.GetDamageTakenEvent();
            _damageEvent.Subscribe(this.OnDamageTaken);
        }

        public void Dispose(IEntity entity) => _damageEvent.Unsubscribe(this.OnDamageTaken);

        private void OnDamageTaken(TakeDamageArgs args) => _health.SetCurrent(_health.GetMax());
    }
}
```

Отписка в `Dispose` обязательна: забытая отписка — самый частый дефект в этом проекте.

- [ ] **Step 2: Написать `DummyInstaller`**

```csharp
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A shooting target: takes damage and shows it, never dies, never fights back.
    /// No DeathBehaviour and no AI, so it is not an enemy — it sits on the Dummy layer and
    /// stays out of the enemy filters.
    /// </summary>
    public sealed class DummyInstaller : MonoEntityInstaller
    {
        [SerializeField] private int _health = 100;
        [SerializeField] private Transform _originTransform;
        [SerializeField] private ParticleSystem _bulletBlood;
        [SerializeField] private ParticleSystem _meleeBlood;

        public override void Install(IEntity entity)
        {
            entity.AddDamageableTag();
            entity.AddGameObject(this.gameObject);
            entity.AddTransform(_originTransform != null ? _originTransform : this.transform);

            entity.AddHealth(new Health(_health, _health));
            entity.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            entity.AddDeathTakenEvent(new Event<TakeDamageArgs>());

            entity.AddBehaviour<RestoreHealthOnDamageBehaviour>();

            if (_bulletBlood != null && _meleeBlood != null)
            {
                entity.AddBehaviour(new TakeDamageBloodBehaviour(
                    _bulletBlood,
                    _meleeBlood,
                    _originTransform != null ? _originTransform : this.transform));
            }
        }
    }
}
```

`DeathTakenEvent` добавляется потому, что `TakeDamageBloodBehaviour` подписывается на оба события и без второго упадёт на `GetDeathTakenEvent()`.

- [ ] **Step 3: Собрать и убедиться в 0 ошибок**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.0.38f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath "C:\!!!Work\CodeCraft_HomeWork_ATOMIC" -logFile "<log>"
```

Затем `grep -c "error CS" <log>` → ожидается `0`.

---

### Task 2: Кнопка спавна

**Files:**
- Create: `Assets/Game/Scripts/Gameplay/GameEntities/Content/SpawnButton/SpawnButtonInstaller.cs`
- Create: `Assets/Game/Scripts/Gameplay/GameEntities/Content/SpawnButton/SpawnButtonBehaviour.cs`

**Interfaces:**
- Consumes: `Game.TriggerEventReceiver` с `event Action<Collider> OnEntered` / `OnExited`; `SpawnUseCase.SpawnCharacter(MonoEntity characterPrefab, Transform spawnPoint, IEntityWorld<IEntity> entityWorld, GameFactory gameFactory)`; `GameContext.Instance.GetEntityWorld()`, `GetGameFactory()`; `collider.TryGetEntity(out IEntity)` и `entity.HasPlayerTag()`.
- Produces: `SpawnButtonInstaller : MonoEntityInstaller` с полями `_enemyPrefab`, `_spawnPoint`, `_trigger`, `_cooldown`; `SpawnButtonBehaviour : IEntityInit, IEntityDispose, IEntityTick`.

- [ ] **Step 1: Написать `SpawnButtonBehaviour`**

Состояние «внутри» хранится явно, а не выводится из числа событий: триггер репортит событие на каждый коллайдер, а персонаж их несёт несколько. Ровно на этом в проекте уже падал `EffectAreaBehaviour`, и счётчик коллайдеров там не помог, потому что состояние сущности живёт дольше счётчика.

```csharp
using System.Collections.Generic;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Spawns one enemy per entry into the trigger, then refuses until the player has left and
    /// the cooldown has elapsed. Standing inside does nothing.
    /// </summary>
    public sealed class SpawnButtonBehaviour : IEntityInit, IEntityDispose, IEntityTick
    {
        private readonly TriggerEventReceiver _trigger;
        private readonly MonoEntity _enemyPrefab;
        private readonly Transform _spawnPoint;
        private readonly float _cooldown;

        private readonly HashSet<Collider> _occupants = new();

        private float _remainingCooldown;
        private bool _armed = true;

        public SpawnButtonBehaviour(
            TriggerEventReceiver trigger,
            MonoEntity enemyPrefab,
            Transform spawnPoint,
            float cooldown)
        {
            _trigger = trigger;
            _enemyPrefab = enemyPrefab;
            _spawnPoint = spawnPoint;
            _cooldown = cooldown;
        }

        public void Init(IEntity entity)
        {
            _trigger.OnEntered += this.OnEntered;
            _trigger.OnExited += this.OnExited;
        }

        public void Dispose(IEntity entity)
        {
            _trigger.OnEntered -= this.OnEntered;
            _trigger.OnExited -= this.OnExited;
            _occupants.Clear();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (_remainingCooldown <= 0f) return;

            _remainingCooldown -= deltaTime;
        }

        private void OnEntered(Collider other)
        {
            if (!this.IsPlayer(other)) return;

            // One event per collider, and a character carries several: only the first one counts.
            bool wasEmpty = _occupants.Count == 0;
            _occupants.Add(other);

            if (!wasEmpty) return;
            if (!_armed || _remainingCooldown > 0f) return;

            _armed = false;
            _remainingCooldown = _cooldown;
            this.Spawn();
        }

        private void OnExited(Collider other)
        {
            if (!_occupants.Remove(other)) return;
            if (_occupants.Count > 0) return;

            _armed = true;
        }

        private bool IsPlayer(Collider other) =>
            other.TryGetEntity(out IEntity entity) && entity.HasPlayerTag();

        private void Spawn()
        {
            GameContext context = GameContext.Instance;

            SpawnUseCase.SpawnCharacter(
                _enemyPrefab,
                _spawnPoint,
                context.GetEntityWorld(),
                context.GetGameFactory());
        }
    }
}
```

- [ ] **Step 2: Написать `SpawnButtonInstaller`**

```csharp
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public sealed class SpawnButtonInstaller : MonoEntityInstaller
    {
        [SerializeField] private TriggerEventReceiver _trigger;
        [SerializeField] private MonoEntity _enemyPrefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _cooldown = 1f;

        public override void Install(IEntity entity)
        {
            entity.AddGameObject(this.gameObject);
            entity.AddTransform(this.transform);

            if (_trigger == null || _enemyPrefab == null || _spawnPoint == null)
            {
                Debug.LogWarning($"{nameof(SpawnButtonInstaller)} on {this.name} is not wired.", this);
                return;
            }

            entity.AddBehaviour(new SpawnButtonBehaviour(_trigger, _enemyPrefab, _spawnPoint, _cooldown));
        }
    }
}
```

Проверка на незаданные ссылки стоит раньше любого `AddValue`: новый `AddValue` кидает `ArgumentNullException` на `null`, а исключение из инсталлера обрывает всю цепочку `Install` без try/catch — именно так зомби терял здоровье и ИИ.

- [ ] **Step 3: Проверить, что `TryGetEntity` существует с такой сигнатурой**

```bash
grep -rn "TryGetEntity" Assets/Plugins/Atomic --include=*.cs | head -5
```

Если расширения `Collider.TryGetEntity(out IEntity)` нет — заменить на `other.GetComponentInParent<MonoEntity>()` и проверку `HasPlayerTag()` на нём.

- [ ] **Step 4: Собрать, 0 ошибок**

Та же команда batchmode, `grep -c "error CS"` → `0`.

---

### Task 3: Билдер сцены

**Files:**
- Create: `Assets/Game/Scripts/Editor/PlaygroundBuilder.cs`

**Interfaces:**
- Consumes: всё из Task 1 и Task 2; `EditorSceneManager.NewScene`/`SaveScene`, `PrefabUtility.InstantiatePrefab`, `SerializedObject`.
- Produces: `public static void Build()` и `[MenuItem("Tools/Playground/Build")]`.

- [ ] **Step 1: Сохранить копию старой сцены**

Сцена неотслеживаемая, удаление необратимо.

```bash
cp "Assets/Game/Scenes/Playground.unity" "<scratchpad>/Playground.unity.old"
```

- [ ] **Step 2: Написать билдер**

Структура метода `Build()`:

1. `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`.
2. `BuildBox()` — пол куб `(24, 1, 24)` с верхом на `y = 0`, четыре стены высотой 4 и толщиной 1 по периметру, слой `Default`, `BoxCollider` от примитива.
3. `BuildRig()` — `[Context]` с `GameContext`, `PlayerContext`, `MonoEntityWorld` (`useUnityLifecycle = false`, `collectOnAwake = true`), их инсталлеры; `[CameraRoot]`/`[Camera]`; направленный свет.
4. `BuildCharacter()` — инстанс `Characters/[Character].prefab` в `y = 0.05`, затем принудительно: `interpolation = Interpolate`, `collisionDetectionMode = ContinuousSpeculative`, `excludeLayers = 1 << 6`, `constraints = FreezeRotation`, `useGravity = true`, `drag = 0`, `CapsuleCollider.center.y = 1.08`.
5. `BuildWeapons()` — ряд из `AR_01_PickUp`, `AR_02_PickUp`, `PT_01_PickUp`, `Knife_01_PickUp`, `AmmoItem`, `HealthItem`.
6. `BuildBuffPickups()` — четыре объекта, собираемых с нуля: `MonoEntity` + `BuffPickUpInstaller`, приватное поле `buffConfig` пишется через `SerializedObject`.
7. `BuildZones()` — три объекта: триггерный `BoxCollider` + `TriggerEventReceiver` + `MonoEntity` + `EffectAreaInstaller`, приватные `_triggerEventReceiver` и `_entityAspect` через `SerializedObject`.
8. `BuildDummies()` — три объекта на слое `Dummy` (7) с `MonoEntity` + `DummyInstaller`.
9. `BuildEnemies()` — инстанс `Characters/Zombie.prefab`, точка спавна (пустышка), кнопка: триггерный `BoxCollider` + `TriggerEventReceiver` + `MonoEntity` + `SpawnButtonInstaller` со связанными `_trigger`, `_enemyPrefab`, `_spawnPoint`.
10. `EditorSceneManager.SaveScene(scene, "Assets/Game/Scenes/Playground.unity")`.

Запись приватного сериализованного поля из кода:

```csharp
private static void SetPrivateField(Object target, string fieldName, Object value)
{
    SerializedObject serialized = new(target);
    SerializedProperty property = serialized.FindProperty(fieldName);

    if (property == null)
    {
        Debug.LogError($"No serialized field '{fieldName}' on {target.GetType().Name}.");
        return;
    }

    property.objectReferenceValue = value;
    serialized.ApplyModifiedPropertiesWithoutUndo();
}
```

Это единственный корректный способ: поля в `BuffPickUpInstaller` и `EffectAreaInstaller` приватные, а рефлексия не помечает объект грязным и значение не попадёт в сцену.

Билдер идемпотентен по построению: он начинает с пустой сцены и перезаписывает файл, поэтому повторный запуск даёт тот же состав, а не удвоенный.

- [ ] **Step 3: Удалить старую сцену и собрать новую**

```bash
"C:/Program Files/Unity/Hub/Editor/6000.0.38f1/Editor/Unity.exe" -batchmode -quit -nographics -projectPath "C:\!!!Work\CodeCraft_HomeWork_ATOMIC" -executeMethod Game.EditorTools.PlaygroundBuilder.Build -logFile "<log>"
```

- [ ] **Step 4: Проверить лог**

`grep -c "error CS" <log>` → `0`; `grep -iE "exception|error" <log>` без находок по существу.

---

### Task 4: Проверочный проход

**Files:**
- Create: `Assets/Game/Scripts/Editor/PlaygroundVerifier.cs` (временный, удаляется после прогона)

- [ ] **Step 1: Написать верификатор**

`public static void Verify()` открывает `Assets/Game/Scenes/Playground.unity` и печатает состав: число детей в `<Weapons>`, `<Buffs>`, `<Zones>`, `<Dummies>`, `<Enemies>`, наличие `<Spawn>` и `<Triggers>`, и для кнопки — что `_trigger`, `_enemyPrefab`, `_spawnPoint` непустые (через `SerializedObject`). Печать с префиксом `[PGVERIFY]`.

- [ ] **Step 2: Прогнать и сверить ожидания**

Ожидается: `Weapons=6`, `Buffs=4`, `Zones=3`, `Dummies=3`, `Enemies=1`, кнопка связана тремя непустыми ссылками.

- [ ] **Step 3: Проверить целостность сцены файлом**

```bash
grep -oE "^--- !u![0-9]+ &[0-9]+" Assets/Game/Scenes/Playground.unity | awk '{print $3}' | sort | uniq -d
```

Ожидается пусто — дублирующихся `fileID` нет.

Затем сверить, что все GUID сцены резолвятся: собрать индекс GUID из `.meta` по `Assets` и `Packages` и вычесть из списка GUID сцены; нерешённых, кроме встроенных сентинелов Unity `0000000000000000e000000000000000` и `...f000...`, быть не должно.

- [ ] **Step 4: Пересобрать второй раз и сверить состав**

Повторный `Build` и повторный `Verify` должны дать те же числа. Это проверка идемпотентности.

- [ ] **Step 5: Удалить верификатор**

Удалить `PlaygroundVerifier.cs` вместе с `.meta`.

---

## Что проверяет пользователь

Логи и состав сцены проверяю я. В игре проверяется то, что логом не видно: по манекену стреляется и он не умирает, оружие подбирается, бафф-пикап даёт визуал и истекает, зона держит эффект пока стоишь, кнопка спавнит ровно одного зомби на вход.
