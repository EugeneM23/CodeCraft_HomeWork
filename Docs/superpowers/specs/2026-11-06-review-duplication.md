# Ревью: дублирование

**Дата:** 2026-11-06
**Статус:** сделано (первая часть)
**Продолжает:** [2026-11-06-model-and-cameras-by-content.md](2026-11-06-model-and-cameras-by-content.md).

## Сделано

1. **`HeldWeaponFireListener`** (`Features/Weapons/`) — «следить за слотом оружия и слышать его
   выстрел». Было 4 ручные копии, уже разошедшиеся (защиту `IsValid` при выгрузке сцены имели не все):
   `CharacterAttackAnimBehaviour`, `CreatureAttackAnimBehaviour`, `AttackSoundBehaviour`,
   `PlayerShakeController`. Танковые `GunRecoil`/`TankRecoil`/`TankMuzzleSmoke` не трогали: у танка
   одна пушка, подписка в три строки без слежения за слотом.
2. **Время жизни из пула.** `LifeTimeBehaviour` сбрасывает таймер в `Enable` — `MonoEntity` вызывает
   его на первом старте и на каждом включении, а пул включает объект при выдаче. Удалены 9 ручных
   `GetLifeTime().Reset()` (SpawnUseCase ×3, ExplosionUseCase ×2, FlashUseCase, TankDestroyBehaviour,
   TankMuzzleSmokeBehaviour, ItemVisualInstaller) и сброс в `UnSpawnEntity`. Забытый сброс больше не
   уничтожает объект на первом тике. Запал гранаты — отдельный таймер, не трогали.
3. **Слои по именам.** В `PhysicsLayers` — номера слоёв из TagManager; маски в 6 файлах
   (JumpUseCase, RigidbodyMoveBehaviour, ControllableUseCase, TankSuspensionBehaviour, FlashUseCase,
   MudImpactUseCase) собраны из имён, значения те же.
4. **`CursorUseCase`** (`PlayerCharacter/`, бывший `RayCastUseCase`) — точка под курсором.
   `TryProjectOnPlane` заменил две копии проекции на плоскость в `PlayerAimController` и
   `PlayerCharacterGrenadeController`.

## Проверка

- Тесты: 138 `Game.Tests` проходят, из них 4 новых `HeldWeaponFireListenerTests`.
- Play: эффект из пула живёт свои 7.1 с (через ~200 кадров активен, прогресс 0.53), затем
  возвращается в пул; повторно выданный экземпляр не истёкший. Выстрел оружия игрока (RPG_01) —
  слой 1 в `Fire`, одна тряска камеры. Ошибок в консоли нет.

## Осталось (по ревью)

- Три core-инсталлера персонажей повторяют «тело» (здоровье ×6, движение ×3, атака ×3), три
  инсталлера техники — посадку/выход.
- `ExplosionUseCase` и `FlashUseCase` — один и тот же обход радиуса.
- Звук получения урона тремя классами (`HitSound`, `TakeDamageSound`, закомментированный `PainSound`).
- Шесть пикапов повторяют один протокол.
- Баффы и аспекты — двойники одних эффектов; баг `DamageAspect`.
