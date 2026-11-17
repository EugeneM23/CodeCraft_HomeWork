# Ракетница RPG_01 — дизайн

## Цель
Новое оружие: ракетница. Ракета летит, при касании чего угодно взрывается: урон по области
с затуханием к краю и разрушение RayFire-объектов в радиусе.

## Решения
- Подбирается с уровня (пикап, как `AR_01_PickUp`), 5 ракет, перезарядка ~1.5 с, без гильз.
- Оружие — существующий `RangeWeaponInstaller`, новый код для оружия не нужен.
- Ракета — вариант пули (`BulletInstaller`) + новый `ExplosionInstaller`.

## Компоненты
- `WeaponID.RPG_01` — в конец enum (сериализованные значения не съезжают).
- `EntityAPI`: `ExplosionRadius` (float), `ExplosionMinDamageFactor` (float),
  `ExplosionBomb` (RayfireBomb), `ExplosionVfxPrefab` (GameObject).
- `TakeDamageArgs.damageFactor` (по умолчанию 1) — `DamageUseCase` умножает урон на него.
- `ExplosionInstaller` — добавляет ключи выше.
- `ExplosionUseCase.Explode(rocket, point)`:
  1. `OverlapSphere(point, radius)` → уникальные `IEntity` с `Damageable`; урон × lerp(1, min, d/r).
  2. `RayfireBomb` в точку удара → `Explode(0)` (ломает и раскидывает шарды).
  3. VFX в точке, тряска камеры.
- `BulletCollisionBehaviour`: если у сущности есть `ExplosionRadius` — взрыв при любом касании
  вместо точечного урона/импакта. Защита от двойного срабатывания сохраняется.

## Префабы
- `Rocket_01.prefab` — скорость ~15 м/с, время жизни 3 с, радиус 4 м, урон 100, мин. 25%.
- `RPG_01.prefab`, `RPG_01_PickUp.prefab` на модели `Human_Bazooka` (Kevin Iglesias).
- Ссылки в `WeaponCatalog`, пикап на Playground.

## Граничные случаи
- Игрок в радиусе получает урон.
- Ракета без попадания исчезает по времени жизни без взрыва.

## Проверка
Play mode: выстрел по зомби и по разрушаемой стене; консоль без ошибок.
