# Флеш-граната — дизайн

Дата: 2026-11-05. Статус: дизайн утверждён. Строится на гранатах (`2026-11-05-grenades-design.md`).

## Цель

Второй тип гранаты. Флешка летит и тормозит как осколочная, через 1.5 с вспыхивает: ослепляет врагов
в радиусе 8 м, а если в зоне вспышки игрок — его экран становится белым и плавно возвращается в норму.
Урона, разрушений и толчка нет. Стены от вспышки закрывают.

## 1. Типы гранат и переключение

- `enum GrenadeType { Frag, Flash }`.
- Ключи персонажа (`EntityAPI`, раздел `Grenades`):
  - `GrenadeAmmo : Dictionary<GrenadeType, Ammo>` — запас по типам (раньше один `Ammo`);
  - `GrenadePrefabs : Dictionary<GrenadeType, MonoEntity>`;
  - `GrenadeSelected : IReactiveVariable<GrenadeType>` — текущий тип;
  - `GrenadeSwitchAction : IAction` — следующий тип по кругу (из тех, что есть у персонажа),
    на пустой тоже можно.
- `CharacterGrenadeInstaller`: список слотов `{тип, префаб, стартовое количество}` — Frag 3, Flash 2.
  Условие броска: жив, запас выбранного типа > 0, кулдаун истёк.
- `CharacterGrenadeController`: Q — бросить выбранный тип, Tab — переключить.
- Индикатор: строка в левом нижнем углу `Граната: Флеш ×2  [Tab]` (`GrenadeHudBehaviour`, `UI.Text`),
  обновляется при переключении, броске и подборе.
- Пикапы: `GrenadePickUpInstaller._type`. `FlashGrenadeItem.prefab` — копия `GrenadeItem` с моделью флешки.
  На Playground — 2 рядом с осколочными.

## 2. Флешка и враги

- `Grenade_Flash_01.prefab` (собирает `GrenadeBuilder`): `GrenadeInstaller` (урон 0, запал 1.5 с,
  торможение с первого касания), модель — светло-серый корпус с синей полосой, `FlashInstaller` вместо
  `ExplosionInstaller`: радиус 8 м, `BlindBuffConfig`, эффект `FX_Flashbang` (белый свет ~0.3 с и
  белая вспышка частиц), лёгкая тряска камеры.
- `GrenadeFuseBehaviour`: по запалу `ExplosionUseCase.Explode` или `FlashUseCase.Flash` — что стоит на гранате.
- `FlashUseCase.Flash(grenade, point)`:
  1. `OverlapSphere(point, radius)` → уникальные сущности с `Damageable`.
  2. Прямая видимость: луч от точки (+0.3 м вверх) к центру коллайдера цели. Не перекрывают: пикапы,
     манекены, персонажи, враги, брошенное, пули, оружие, обломки, Ignore Raycast. Перекрыт — цель пропускается.
  3. Сила: `1 − 0.7·clamp01(d / radius)`, `d` — до ближайшей точки коллайдера.
  4. `Enemy` → `BuffUseCase.Apply(enemy, BlindBuff)`; `Player` → `FlashedEvent.Invoke(сила)`.
- `BlindBuff : TemporaryBuff` (`BlindBuffConfig`: Name "Blind", Duration 4, `StumbleSpeed` 0.35):
  - наложение: запомнить и сбросить `Target`; снять `EnemyChaseBehaviour`, `EnemyAttackBehaviour`,
    `EnemyPatrolBehaviour`; добавить `BlindStumbleBehaviour` (каждые 0.6–1.2 с случайное направление,
    `MoveDirection = dir · StumbleSpeed`);
  - снятие: убрать блуждание, обнулить `MoveDirection`, вернуть поведения, вернуть цель, если жива.
- Повторная флешка время не продлевает (`BuffUseCase` не накладывает бафф с тем же именем).

## 3. Белый экран игрока

- `CharacterFlashInstaller`: `FlashedEvent : IEvent<float>` и `FlashScreenBehaviour` с белым `Image`.
- `<FlashScreen>` — дочерний объект `[Character]`: Canvas Screen Space Overlay (sorting 100,
  `CanvasScaler` 1920×1080), белый `Image` на весь экран (alpha 0, без raycast) и строка индикатора.
- По событию: alpha = max(текущая, сила); держит `0.3 + 0.7·сила` с, затем гаснет за `1 + 2.5·сила` с
  (квадратичное затухание). Новая вспышка во время затухания начинает заново, если она сильнее.

## Поиск целей (исправление по ходу)

В радиусе 8 м — больше тысячи коллайдеров, почти все — плиты разрушаемого пола. `OverlapSphereNonAlloc`
по всем слоям заполнял буфер на 128 плитами, и персонажи в выборку не попадали: флешка не действовала
ни на кого. Та же ошибка была у урона осколочной (`ExplosionUseCase.DamageInRadius`). Оба запроса теперь
идут по маске `PhysicsLayers.Targets` (без Ground, Bullet, Destructible, Debris, Rubble), буферы — 256.

## Граничные случаи

- Флешка не наносит урон и не трогает RayFire.
- Игрок за стеной от вспышки — экран не белеет; враг за стеной — не слепнет.
- Мёртвые враги и сущности без баффов пропускаются.

## Проверка (Play mode)

Tab переключает тип, индикатор меняется; флешка у врага — враг теряет цель и блуждает 4 с, потом
снова преследует; флешка рядом с игроком — экран белеет и гаснет; за стеной — нет; консоль без ошибок.
