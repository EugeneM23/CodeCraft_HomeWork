# Рикошет пуль: план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Пуля, задевшая поверхность по касательной, отскакивает и летит дальше без потери урона и скорости.

**Architecture:** Решение об отскоке принимается в обработчике касания `BulletCollisionBehaviour`. Математика и изменение состояния пули вынесены в статический `RicochetUseCase` (как `DamageUseCase`, `ExplosionUseCase`). Настройки рикошета живут на сущности пули как значения `EntityAPI` и задаются в `BulletInstaller`.

**Tech Stack:** Unity 6000.0.38f1, Atomic.Entities (генератор `EntityAPI`), RayFire, NUnit (Unity Test Framework), Unity Editor MCP для компиляции, тестов и Play Mode.

**Спека:** `Docs/superpowers/specs/2026-11-05-bullet-ricochet-design.md`

## Global Constraints

- Рикошет только при угле к поверхности `<= RicochetMaxAngle` (по умолчанию 30°) и `RicochetCount < RicochetMaxCount`.
- `RicochetMaxCount = 0` — рикошет выключен, поведение пули как раньше.
- От `Damageable` и у взрывающихся снарядов рикошета нет никогда.
- Без потерь: урон, скорость и `LifeTime` после отскока не меняются.
- Разрушаемая поверхность при рикошете получает удар RayFire с множителем `RicochetImpactFactor` (по умолчанию 0.3); knockout только при прямом попадании.
- Нормаль контакта направлена от поверхности к пуле.
- Комментарии в коде на английском, в стиле проекта: объясняют «почему».
- В рабочем дереве много чужих незакоммиченных правок в тех же файлах (`BulletInstaller.cs`, `BulletCollisionBehaviour.cs`, префабы). Коммитить только по команде пользователя.
- В редакторе выключен Auto Refresh: после правки файлов на диске выполнять `Assets/Refresh` (`mcp__unity-editor-mcp__editor_invoke_menu`), затем ждать конца компиляции (`mcp__unity-editor-mcp__editor_status`, `compiling: false`) и проверять консоль на ошибки (`mcp__unity-editor-mcp__console_get`, `level: error`).

---

## Файлы

| Файл | Ответственность |
|---|---|
| `Assets/Game/Scripts/Declarations/EntityAPI.cs` | Ключи рикошета (раздел Physics) |
| `Assets/Game/Scripts/Features/Combat/Model/RicochetUseCase.cs` (новый) | Угол, «летит от поверхности», можно ли отскочить, сам отскок |
| `Assets/Game/Tests/RicochetUseCaseTests.cs` (новый) | EditMode-тесты `RicochetUseCase` |
| `Assets/Game/Tests/Game.Tests.asmdef` | Сделать сборку EditMode (`includePlatforms: ["Editor"]`) |
| `Assets/Game/Scripts/Features/Destruction/DestructionUseCase.cs` | Параметр `impactFactor` у `TryImpact` |
| `Assets/Game/Scripts/Features/Combat/Model/BulletCollisionBehaviour.cs` | Порядок обработки касания |
| `Assets/Game/Scripts/Entities/Projectile/BulletInstaller.cs` | Сериализуемые настройки рикошета |
| `Assets/Game/Scripts/Features/Spawning/SpawnUseCase.cs` | Сброс `RicochetCount` при выдаче пули из пула |
| `Assets/Game/Prefabs/Projectiles/Bullet_01.prefab`, `Bullet_01_AR.prefab`, `Bullet_02.prefab` | Включить рикошет |

---

### Task 1: Данные и логика рикошета (`RicochetUseCase`)

**Files:**
- Modify: `Assets/Game/Scripts/Declarations/EntityAPI.cs` (раздел `#region Physics`, после `ExplosionCrumbsPrefab`)
- Modify: `Assets/Game/Tests/Game.Tests.asmdef`
- Create: `Assets/Game/Scripts/Features/Combat/Model/RicochetUseCase.cs`
- Test: `Assets/Game/Tests/RicochetUseCaseTests.cs`

**Interfaces:**
- Produces:
  - `EntityAPI.RicochetMaxCount : ValueKey<IEntity, int>`, `RicochetMaxAngle : ValueKey<IEntity, float>`, `RicochetImpactFactor : ValueKey<IEntity, float>`, `RicochetCount : ValueKey<IEntity, int>` и сгенерированные `Add/Get/TryGet/Has/Set/Del<Name>`.
  - `static float RicochetUseCase.GetSurfaceAngle(Vector3 direction, Vector3 normal)`
  - `static bool RicochetUseCase.IsMovingAway(Vector3 direction, Vector3 normal)`
  - `static bool RicochetUseCase.IsEnabled(IEntity bullet)`
  - `static bool RicochetUseCase.CanRicochet(IEntity bullet, Vector3 normal)`
  - `static void RicochetUseCase.Ricochet(IEntity bullet, Vector3 normal)`

- [ ] **Step 1: Добавить ключи в `EntityAPI`**

В `EntityAPI.cs` сразу после строки
`public static readonly ValueKey<IEntity, MonoEntity> ExplosionCrumbsPrefab = new(nameof(ExplosionCrumbsPrefab));` вставить:

```csharp

        // Ricochet: a bullet that grazes a surface bounces off it and flies on instead of stopping.
        // A bullet without these values (or with RicochetMaxCount 0) never ricochets.
        public static readonly ValueKey<IEntity, int> RicochetMaxCount = new(nameof(RicochetMaxCount));
        // Largest angle (degrees) between the flight direction and the surface that still bounces.
        public static readonly ValueKey<IEntity, float> RicochetMaxAngle = new(nameof(RicochetMaxAngle));
        // Share of the RayFire impact a destructible surface takes from a bullet bouncing off it.
        public static readonly ValueKey<IEntity, float> RicochetImpactFactor = new(nameof(RicochetImpactFactor));
        // How many times the bullet has bounced since it left the barrel.
        public static readonly ValueKey<IEntity, int> RicochetCount = new(nameof(RicochetCount));
```

- [ ] **Step 2: Сделать `Game.Tests` сборкой EditMode**

В `Assets/Game/Tests/Game.Tests.asmdef` заменить `"includePlatforms": [],` на:

```json
    "includePlatforms": [
        "Editor"
    ],
```

Тестов в сборке пока нет, так что ничего не ломается. Тесты логики не требуют Play Mode.

- [ ] **Step 3: Написать падающие тесты**

Создать `Assets/Game/Tests/RicochetUseCaseTests.cs`:

```csharp
using Atomic.Elements;
using Atomic.Entities;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class RicochetUseCaseTests
    {
        // The surface is a floor: its normal points up, towards a bullet coming down onto it.
        private static readonly Vector3 Normal = Vector3.up;

        private GameObject _gameObject;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
        }

        // Flies along +X and down, at the given angle to the floor.
        private static Vector3 Incoming(float angleToSurface) =>
            Quaternion.AngleAxis(-angleToSurface, Vector3.forward) * Vector3.right;

        private IEntity CreateBullet(Vector3 direction, int maxCount = 2, float maxAngle = 30f, int count = 0)
        {
            _gameObject = new GameObject("Bullet");
            _gameObject.AddComponent<Rigidbody>().useGravity = false;

            IEntity bullet = new Entity();
            bullet.AddTransform(_gameObject.transform);
            bullet.AddMoveDirection(new ReactiveVector3(direction));
            bullet.AddRiggedBody(_gameObject.GetComponent<Rigidbody>());
            bullet.AddRicochetMaxCount(maxCount);
            bullet.AddRicochetMaxAngle(maxAngle);
            bullet.AddRicochetImpactFactor(0.3f);
            bullet.AddRicochetCount(count);
            return bullet;
        }

        [Test]
        public void GetSurfaceAngle_GrazingHit_ReturnsAngleToPlane()
        {
            Assert.That(RicochetUseCase.GetSurfaceAngle(Incoming(20f), Normal), Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void GetSurfaceAngle_HeadOnHit_Returns90()
        {
            Assert.That(RicochetUseCase.GetSurfaceAngle(Vector3.down, Normal), Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void IsMovingAway_TowardsSurface_False()
        {
            Assert.That(RicochetUseCase.IsMovingAway(Incoming(20f), Normal), Is.False);
        }

        [Test]
        public void IsMovingAway_AfterBounce_True()
        {
            Vector3 bounced = Vector3.Reflect(Incoming(20f), Normal);
            Assert.That(RicochetUseCase.IsMovingAway(bounced, Normal), Is.True);
        }

        [Test]
        public void IsEnabled_WithoutRicochetValues_False()
        {
            Assert.That(RicochetUseCase.IsEnabled(new Entity()), Is.False);
        }

        [Test]
        public void IsEnabled_ZeroMaxCount_False()
        {
            Assert.That(RicochetUseCase.IsEnabled(CreateBullet(Incoming(20f), maxCount: 0)), Is.False);
        }

        [Test]
        public void IsEnabled_PositiveMaxCount_True()
        {
            Assert.That(RicochetUseCase.IsEnabled(CreateBullet(Incoming(20f))), Is.True);
        }

        [Test]
        public void CanRicochet_GrazingHit_True()
        {
            Assert.That(RicochetUseCase.CanRicochet(CreateBullet(Incoming(20f)), Normal), Is.True);
        }

        [Test]
        public void CanRicochet_SteepHit_False()
        {
            Assert.That(RicochetUseCase.CanRicochet(CreateBullet(Incoming(45f)), Normal), Is.False);
        }

        [Test]
        public void CanRicochet_LimitReached_False()
        {
            Assert.That(RicochetUseCase.CanRicochet(CreateBullet(Incoming(20f), count: 2), Normal), Is.False);
        }

        [Test]
        public void CanRicochet_Disabled_False()
        {
            Assert.That(RicochetUseCase.CanRicochet(new Entity(), Normal), Is.False);
        }

        [Test]
        public void Ricochet_ReflectsDirectionAndTurnsBullet()
        {
            IEntity bullet = CreateBullet(Incoming(20f));
            Vector3 expected = Vector3.Reflect(Incoming(20f), Normal).normalized;

            RicochetUseCase.Ricochet(bullet, Normal);

            Assert.That(Vector3.Distance(bullet.GetMoveDirection().Value, expected), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(bullet.GetTransform().forward, expected), Is.LessThan(1e-4f));
        }

        [Test]
        public void Ricochet_CountsBounce()
        {
            IEntity bullet = CreateBullet(Incoming(20f), count: 1);

            RicochetUseCase.Ricochet(bullet, Normal);

            Assert.That(bullet.GetRicochetCount(), Is.EqualTo(2));
        }

        [Test]
        public void Ricochet_StopsRigidbody()
        {
            IEntity bullet = CreateBullet(Incoming(20f));
            Rigidbody body = bullet.GetRiggedBody();
            body.linearVelocity = new Vector3(3f, 1f, 0f);
            body.angularVelocity = new Vector3(0f, 2f, 0f);

            RicochetUseCase.Ricochet(bullet, Normal);

            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
        }
    }
}
```

- [ ] **Step 4: Убедиться, что тесты не компилируются**

`mcp__unity-editor-mcp__editor_invoke_menu` с `Assets/Refresh`, дождаться `compiling: false`.
`mcp__unity-editor-mcp__console_get` (`level: error`).
Ожидается: ошибка `CS0103: The name 'RicochetUseCase' does not exist in the current context` в `RicochetUseCaseTests.cs`. Других ошибок быть не должно: если ругается на ключи `EntityAPI`, исправить Step 1.

- [ ] **Step 5: Написать `RicochetUseCase`**

Создать `Assets/Game/Scripts/Features/Combat/Model/RicochetUseCase.cs`:

```csharp
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A bullet that grazes a surface bounces off it and flies on with its full damage and speed;
    /// a steep hit still stops it. <c>normal</c> is the contact normal, pointing from the surface
    /// towards the bullet.
    /// </summary>
    public static class RicochetUseCase
    {
        /// <summary>
        /// Angle between the flight direction and the surface plane: 0 when skimming along it,
        /// 90 when hitting it head-on, negative when flying away from it.
        /// </summary>
        public static float GetSurfaceAngle(Vector3 direction, Vector3 normal) =>
            90f - Vector3.Angle(direction, -normal);

        /// <summary>
        /// True when the bullet already flies away from the surface. One physics step reports a
        /// contact per collider touched, so right after a bounce the bullet still gets the contact
        /// of a neighbouring brick or of the same wall; reflecting it again would send it back.
        /// </summary>
        public static bool IsMovingAway(Vector3 direction, Vector3 normal) =>
            Vector3.Dot(direction, normal) >= 0f;

        public static bool IsEnabled(IEntity bullet) =>
            bullet.TryGetRicochetMaxCount(out int maxCount) && maxCount > 0;

        public static bool CanRicochet(IEntity bullet, Vector3 normal)
        {
            if (!IsEnabled(bullet) || bullet.GetRicochetCount() >= bullet.GetRicochetMaxCount())
                return false;

            Vector3 direction = bullet.GetMoveDirection().Value;
            return GetSurfaceAngle(direction, normal) <= bullet.GetRicochetMaxAngle();
        }

        public static void Ricochet(IEntity bullet, Vector3 normal)
        {
            IReactiveVariable<Vector3> moveDirection = bullet.GetMoveDirection();
            Vector3 direction = Vector3.Reflect(moveDirection.Value, normal).normalized;
            moveDirection.Value = direction;

            // The trail and the model follow the transform, so turn it to where the bullet now flies.
            bullet.GetTransform().rotation = Quaternion.LookRotation(direction);

            // The bullet flies by transform, but its Rigidbody is dynamic: the solver has just
            // bounced it off the wall, and that velocity would drift it from now on. It never
            // mattered before, because the bullet went back to the pool on its first contact.
            if (bullet.TryGetRiggedBody(out Rigidbody body))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            bullet.SetRicochetCount(bullet.GetRicochetCount() + 1);
        }
    }
}
```

- [ ] **Step 6: Прогнать тесты**

`Assets/Refresh`, дождаться `compiling: false`, `console_get` (`level: error`) — ошибок нет.
`mcp__unity-editor-mcp__test_run` с `mode: editor`, `filter: RicochetUseCaseTests`.
Ожидается: 14 тестов, все PASS.

Если падает только `Ricochet_StopsRigidbody` из-за того, что в EditMode `Rigidbody` не принимает скорость, проверить это `script_eval`-ом; если так и есть, убрать этот тест и проверить обнуление скорости в Task 4 (Play Mode).

---

### Task 2: Слабый удар RayFire (`DestructionUseCase.TryImpact`)

**Files:**
- Modify: `Assets/Game/Scripts/Features/Destruction/DestructionUseCase.cs` (метод `TryImpact`)

**Interfaces:**
- Produces: `static bool DestructionUseCase.TryImpact(IEntity bullet, Collision collision, float impactFactor = 1f)`. Существующий вызов `TryImpact(_bullet, collision)` не меняется.

Юнит-теста нет: метод работает только с RayFire в сцене. Проверка — компиляция здесь и Play Mode в Task 4.

- [ ] **Step 1: Добавить параметр и документацию**

Заменить сигнатуру

```csharp
        public static bool TryImpact(IEntity bullet, Collision collision)
```

на

```csharp
        /// <param name="impactFactor">
        /// Share of the gun's damage and push this contact applies: 1 for a direct hit, less for a
        /// bullet that only grazes the surface and bounces off. The lucky knockout is a direct-hit thing.
        /// </param>
        public static bool TryImpact(IEntity bullet, Collision collision, float impactFactor = 1f)
```

- [ ] **Step 2: Масштабировать силу и урон**

Заменить блок

```csharp
            float strength = gun.strength;
            if (body != null && body.mass > HEAVY_MASS)
                gun.strength = strength / body.mass;

            // Lucky hit: top the damage up to exactly what the piece still holds, so this shot
            // knocks it out however little damage it took before.
            float damage = gun.damage;
            if (hitRigid != null && hitRigid.damage.en
                && bullet.TryGetDestructionKnockoutChance(out float knockoutChance)
                && Random.value < knockoutChance)
                gun.damage = Mathf.Max(damage, hitRigid.damage.max - hitRigid.damage.cur);
```

на

```csharp
            float strength = gun.strength;
            float push = strength * impactFactor;
            gun.strength = body != null && body.mass > HEAVY_MASS ? push / body.mass : push;

            // Lucky hit: top the damage up to exactly what the piece still holds, so this shot
            // knocks it out however little damage it took before.
            float damage = gun.damage;
            gun.damage = damage * impactFactor;
            bool directHit = impactFactor >= 1f;
            if (directHit && hitRigid != null && hitRigid.damage.en
                && bullet.TryGetDestructionKnockoutChance(out float knockoutChance)
                && Random.value < knockoutChance)
                gun.damage = Mathf.Max(damage, hitRigid.damage.max - hitRigid.damage.cur);
```

Строки `gun.Shoot(origin, direction); gun.strength = strength; gun.damage = damage;` остаются как есть: они восстанавливают исходные значения пушки.

- [ ] **Step 3: Масштабировать разлёт осколков**

Заменить

```csharp
                ScatterShards(hitRigid, contact.point + direction * SHARD_BURST_DEPTH, strength);
```

на

```csharp
                ScatterShards(hitRigid, contact.point + direction * SHARD_BURST_DEPTH, push);
```

- [ ] **Step 4: Проверить компиляцию и тесты**

`Assets/Refresh`, дождаться `compiling: false`, `console_get` (`level: error`) — ошибок нет.
`test_run` с `mode: editor`, `filter: RicochetUseCaseTests` — 14 PASS.

---

### Task 3: Подключить рикошет к пулям

**Files:**
- Modify: `Assets/Game/Scripts/Entities/Projectile/BulletInstaller.cs`
- Modify: `Assets/Game/Scripts/Features/Spawning/SpawnUseCase.cs` (метод `SpawnBullet`)
- Modify: `Assets/Game/Scripts/Features/Combat/Model/BulletCollisionBehaviour.cs`
- Modify: `Assets/Game/Prefabs/Projectiles/Bullet_01.prefab`, `Bullet_01_AR.prefab`, `Bullet_02.prefab`

**Interfaces:**
- Consumes: всё из Task 1, `DestructionUseCase.TryImpact(bullet, collision, impactFactor)` из Task 2.

- [ ] **Step 1: Настройки в `BulletInstaller`**

После поля `_knockoutChance` (строка `[SerializeField] private float _knockoutChance;`) добавить:

```csharp
        [Header("Ricochet")]
        [Tooltip("How many times the bullet may bounce off surfaces it grazes. 0 turns ricochet off.")]
        [Min(0)]
        [SerializeField] private int _ricochetMaxCount;
        [Tooltip("Largest angle (degrees) between the flight direction and the surface that still bounces the bullet off.")]
        [Range(0f, 90f)]
        [SerializeField] private float _ricochetMaxAngle = 30f;
        [Tooltip("Share (0..1) of the RayFire impact a destructible surface takes when the bullet bounces off it.")]
        [Range(0f, 1f)]
        [SerializeField] private float _ricochetImpactFactor = 0.3f;
```

В `Install` после строки `entity.WhenFixedTick(deltaTime => entity.MoveSelfSwept(body, deltaTime));` добавить:

```csharp

            //Ricochet
            if (_ricochetMaxCount > 0)
            {
                entity.AddRicochetMaxCount(_ricochetMaxCount);
                entity.AddRicochetMaxAngle(_ricochetMaxAngle);
                entity.AddRicochetImpactFactor(_ricochetImpactFactor);
                entity.AddRicochetCount(0);
                // The bounce zeroes the velocity the solver gives the body when it hits the wall.
                if (body != null)
                    entity.AddRiggedBody(body);
            }
```

- [ ] **Step 2: Сброс счётчика при выдаче из пула**

В `SpawnUseCase.SpawnBullet` после строки `bullet.GetLifeTime().Reset();` добавить:

```csharp

            // A pooled bullet keeps the bounces of its previous flight.
            if (bullet.HasRicochetCount())
                bullet.SetRicochetCount(0);
```

- [ ] **Step 3: Порядок обработки касания**

Заменить метод `Destroy` в `BulletCollisionBehaviour` и подписки на него целиком. Новый файл:

```csharp
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public class BulletCollisionBehaviour : IEntityInit, IEntityDispose
    {
        private CollisionEventReceiver _collisionEventReceiver;
        private IEntity _bullet;
        private GameFactory _gameFactory;

        public void Init(IEntity entity)
        {
            _gameFactory = GameContext.Instance.GetGameFactory();
            _collisionEventReceiver = entity.GetCollisionReceiver();
            _bullet = entity;
            _collisionEventReceiver.OnEntered += OnCollision;
        }

        public void Dispose(IEntity entity) => _collisionEventReceiver.OnEntered -= OnCollision;

        private void OnCollision(Collision collision)
        {
            // A bullet touching several colliders gets one OnCollisionEnter per collider in the same
            // physics step, even after the first one returned it to the pool. Only the first counts:
            // otherwise one bullet deals damage several times and is returned to the pool twice.
            if (!_bullet.GetGameObject().activeInHierarchy)
                return;

            if (ExplosionUseCase.IsExplosive(_bullet))
                ExplosionUseCase.Explode(_bullet, collision.GetContact(0).point, collision.collider, _gameFactory);
            else if (collision.gameObject.TryGetComponent(out IEntity target) && target.HasDamageableTag())
                DamageUseCase.TakeDamage(target, new TakeDamageArgs(_bullet));
            else if (RicochetUseCase.IsEnabled(_bullet) && TryRicochet(collision))
                return;
            else
            {
                DestructionUseCase.TryImpact(_bullet, collision);
                SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
            }

            SpawnUseCase.UnSpawnEntity(_gameFactory, _bullet);
        }

        /// <summary>True when the bullet flies on: it bounced off the surface, or already flies away from it.</summary>
        private bool TryRicochet(Collision collision)
        {
            Vector3 normal = collision.GetContact(0).normal;

            if (RicochetUseCase.IsMovingAway(_bullet.GetMoveDirection().Value, normal))
                return true;

            if (!RicochetUseCase.CanRicochet(_bullet, normal))
                return false;

            // Before the bounce: the impact ray follows the direction the bullet came from.
            DestructionUseCase.TryImpact(_bullet, collision, _bullet.GetRicochetImpactFactor());
            SpawnUseCase.SpawnCollisionHit(collision, _gameFactory, _bullet);
            RicochetUseCase.Ricochet(_bullet, normal);
            return true;
        }
    }
}
```

- [ ] **Step 4: Включить рикошет на префабах**

В каждом из `Bullet_01.prefab`, `Bullet_01_AR.prefab`, `Bullet_02.prefab` в блоке `BulletInstaller` после строки `  _destructionImpact: {fileID: ...}` добавить три строки (два пробела отступа, как у соседних полей):

```yaml
  _ricochetMaxCount: 2
  _ricochetMaxAngle: 30
  _ricochetImpactFactor: 0.3
```

Строки `_destructionImpact` в файлах: `Bullet_01.prefab:158`, `Bullet_01_AR.prefab:158`, `Bullet_02.prefab:159`. `Rocket_01.prefab` не трогать.

- [ ] **Step 5: Проверить компиляцию, импорт и тесты**

`Assets/Refresh`, дождаться `compiling: false`, `console_get` (`level: error`) — ошибок нет.
`mcp__unity-editor-mcp__component_get_fields` для `BulletInstaller` на каждом из трёх префабов: `_ricochetMaxCount = 2`, `_ricochetMaxAngle = 30`, `_ricochetImpactFactor = 0.3`. На `Rocket_01`: `_ricochetMaxCount = 0`.
`test_run` с `mode: editor`, `filter: RicochetUseCaseTests` — 14 PASS.

---

### Task 4: Проверка в Play Mode

**Files:** нет изменений, только проверка.

Сценарии запускаются на сцене `Assets/Game/Scenes/Playground.unity` через `mcp__unity-editor-mcp__script_eval`. Код проб рассчитан на `using Game; using UnityEngine;`: расширения `EntityAPI` лежат в namespace `Game`. Если `script_eval` не принимает using-директивы, вызывать их явно (`Game.EntityAPI.GetTransform(bullet)`). Пробная стена ставится высоко над сценой (y = 50), чтобы ничего не задеть: у пуль выключена гравитация.

- [ ] **Step 1: Запустить Play Mode**

`mcp__unity-editor-mcp__scene_open` на `Assets/Game/Scenes/Playground.unity` (если не открыта), `mcp__unity-editor-mcp__editor_playmode_start`, затем `mcp__unity-editor-mcp__runtime_set_time_scale` = 0.1. Так время жизни пули (1 с) растягивается до ~10 с реального времени и между вызовами MCP хватает запаса.

- [ ] **Step 2: Отскок по касательной**

`script_eval`:

```csharp
var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
wall.name = "RicochetProbeWall";
wall.transform.position = new Vector3(0f, 50f, 0f);
wall.transform.localScale = new Vector3(0.2f, 4f, 20f); // a wall in the YZ plane, normal along -X/+X

var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Atomic.Entities.MonoEntity>("Assets/Game/Prefabs/Projectiles/Bullet_01.prefab");
var bullet = Game.GameContext.Instance.GetGameFactory().Create(prefab);
// 20 degrees to the wall, flying towards it from the -X side.
Vector3 direction = Quaternion.AngleAxis(20f, Vector3.up) * Vector3.forward;
bullet.GetTransform().SetPositionAndRotation(new Vector3(-1.5f, 50f, -4f), Quaternion.LookRotation(direction));
bullet.GetMoveDirection().Value = direction;
bullet.GetLifeTime().Reset();
bullet.SetRicochetCount(0);
bullet.GetGameObject().name = "RicochetProbe";
return direction;
```

Подождать ~3 с реального времени, затем `script_eval`:

```csharp
var go = GameObject.Find("RicochetProbe");
if (go == null) return "bullet gone";
var entity = go.GetComponent<Atomic.Entities.IEntity>();
return $"count={entity.GetRicochetCount()} dir={entity.GetMoveDirection().Value} vel={go.GetComponent<Rigidbody>().linearVelocity}";
```

Ожидается: `count=1`, у `dir` компонента X отрицательная (пуля отскочила обратно на сторону -X), `vel` близка к нулю.

Если `bullet gone` или `count=0`: проверить знак нормали контакта (лог `collision.GetContact(0).normal` и `MoveDirection` в `TryRicochet`), а не подгонять константы. Начинать с superpowers:systematic-debugging.

- [ ] **Step 3: Прямое попадание**

Тот же код, что в Step 2, но `direction = Vector3.right` и позиция `new Vector3(-3f, 50f, 0f)`. Через ~3 с `GameObject.Find("RicochetProbe")` должен вернуть `null`: пуля вернулась в пул.

- [ ] **Step 4: Убрать пробную стену и остановить Play Mode**

`script_eval`: `Object.Destroy(GameObject.Find("RicochetProbeWall"));`, затем `runtime_set_time_scale` = 1 и `editor_playmode_stop`.

- [ ] **Step 5: Ручная проверка пользователем**

Попросить пользователя проверить в игре то, что не покрывается пробами:
- стрельба вдоль разрушаемой стены: отскок, кирпичи получают лишь слабый удар;
- стрельба во врага по касательной: урон, без отскока;
- RPG: взрыв, без отскока.
