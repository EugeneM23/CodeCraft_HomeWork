# Звук игры на AudioEngine

**Дата:** 2026-11-17
**Статус:** черновик для тимлида
**Команда:** звук, ветка `game/audio`, пункт 5 общего плана

## 1. Задача

Слова пользователя: «Аудиосистема — берём AudioEngine-плагин и делаем наше аудиосопровождение на нём. Ты берёшь на себя настройку всех звуков, а я просто подставляю звуки в ключи и банки».

Готово, когда:
- все события игры звучат через AudioEngine;
- сборка плеера не падает;
- пользователь кладёт клип в папку ключа и больше ничего не настраивает;
- инструкция `Docs/Audio/README.md` записана.

## 2. Что есть сейчас (аудит 2026-11-17)

**Звучат** только оружие танка, Regax и пеший код. Каждый звук — свой `AudioSource` на префабе, все 2D или 3D без правил, все идут в группу `Sound` микшера `Assets/Game/Audio/AudioMixer.mixer` (Master, Music, Sound). Из кода микшер никто не трогает.

| Что | Код | Клипы |
|---|---|---|
| Пушка и пулемёт танка | `Features/Weapons/WeaponFireSoundInstaller.cs:47,68` | `etfx_shoot_grenade.wav`, `Weapons/Fire (MachineGun).wav`, 2D |
| Regax: ранение, смерть, атака, шаги | `Character/Common/CharacterAudioInstaller.cs:31-37` → `DamageSoundBehaviour`, `DeathSoundBehaviour`, `AttackSoundBehaviour`, `MoveStepSoundBehaviour` | `Zombie/*`, `Character/MoveStep1-4`, 3D |
| Пеший игрок, ручное оружие, подбор | `PlayerCharacterAudioInstaller`, `WeaponVisualInstaller`, `ItemVisualInstaller` | `Character/*`, `PickableItems/*`, `Gunshot 5-5.wav` — уходит с чисткой, не подключаю |
| Пул одноразовых источников | `Shared/Audio/AudioSourceInstaller.cs`, `GameContextInstaller.cs:32` | без клипа |

**Молчат:** двигатель, гусеницы, башня, таран, давка, попадания по танку, гибель танка, взрывы, авиаудар, разрушение вокселей, обломки, грязь, дождь, ветер, орда, интерфейс, музыка (лежащий `Music/Music.wav`, 23 МБ, никем не используется).

**Данных для параметров нет готовых:** оборотов и коробки передач у танка нет. Есть `TankEngine.State` (выключен, запуск, работает), `DriveInput` (газ и поворот), `DriveBoost` (Shift), скорость тела, `TankSuspension` (колёса на земле, сцепление), `MudSurface` (доля грязи, пробуксовка), скорость башни. Обороты считаем сами из скорости, газа и буста.

**Рывок давки, 29 мс** (`Docs/Reports/2026-11-17-perf-baseline.md`): клипы `Zombie/ZombieTakeDamage*` и `ZombieDeath*` на ПК не предзагружены (`preloadAudioData: 0`) и грузятся с диска при первом проигрывании.

## 3. Решение

### 3.1. Модуль

`Assets/Modules/AudioEngine` — копия `module/AudioEngine@6e1cb8de` со своими сборками. Ядро правим только ради ошибок (сделано, список в `Assets/Modules/AudioEngine/README.md`):
- `using UnityEditor` без `#if` — блокер сборки плеера;
- событие с `duration = 0` обрывалось на первом кадре, теперь длина берётся из клипа;
- `Dispose(fade)` гасил чужое событие;
- `PlayEvent` терял поворот;
- утечка `ListPool`;
- случайный интервал не работал;
- `FindObjectOfType`.

Тесты модуля — `Tests/AudioEngine.Tests.asmdef`.

Из модуля берём три части, как он задуман:

| Часть модуля | Для чего у нас | Где живёт |
|---|---|---|
| `AudioSystem` + `AudioBank` + `AudioSourceEvent` | звуки мира: танк, оружие, взрывы, монстры, орда, окружение | объект `GameAudio` уровня. Пауза боя замораживает его целиком |
| `UISoundPlayer` + `UISoundCatalog` | кнопки и экраны | объект `AppAudio` на всю игру. Пауза его не трогает |
| `MusicPlayer` + `MusicCatalog` | музыка меню и боя | там же, в `AppAudio` |

### 3.2. Наш код и ассеты

Всё звуковое — в одной папке, у неё один владелец:

```
Assets/Game/Audio/
  Banks/                     AudioBank: tank, weapons, destruction, monsters, horde, world
  Events/<bank>/             AudioSourceEvent на каждый ключ: <bank>_<key>.asset
  Clips/<bank>/<key>/        сюда пользователь кладёт клипы ключа
  Music/                     MusicCatalog: MenuMusic, BattleMusic (+ лежащий Music.wav)
  UI/                        UISoundCatalog
  AudioMixer.mixer           существующий микшер, перестроенный под каналы
  Prefabs/                   GameAudio, AppAudio
  Scripts/                   сборка Game.AudioEngine.Adapters (ссылается на Game, Game.Voxels, Atomic, AudioEngine)
  Scripts/Editor/            Game.AudioEngine.Adapters.Editor: сборщик банков, правила импорта, окно прослушивания
  Tests/                     Game.AudioEngine.Adapters.Tests
```

Старые папки клипов (`Zombie/`, `Weapons/`, `Common/`…) остаются на месте: события ссылаются на них, пока пользователь не положит свои клипы. Переносить их не буду, часть из них удаляет чистка.

### 3.3. «Положил файл — звучит»

- **Папка ключа.** Сборщик (`Tools/Audio/Sync Banks` и автоматически после импорта звука) смотрит папку `Clips/<bank>/<key>/`. Если в ней есть клипы, они становятся клипами события: один клип — `ClipConst`, несколько — `ClipRandomNonRepeat`. Громкость, высоту, 3D и группу микшера сборщик не трогает: их настраиваю я в ассете события. Пустая папка — остаётся нынешний клип или тишина.
- **Правила импорта** (`AssetPostprocessor` на `Assets/Game/Audio/**`) ставятся сами:
  - короткие звуки (до 5 с) — Decompress On Load, Vorbis, **Preload Audio Data**, Load In Background;
  - длинные петли и окружение (5–30 с) — Compressed In Memory, Preload;
  - музыка (папка `Music/` или длиннее 30 с) — Streaming.
- **Нет клипа.** Ключ без клипа молчит без ошибок. В редакторе — одно предупреждение на ключ за сессию: «звук `tank.engine_loop` без клипа».
- **Прослушать.** Окно `Tools/Audio/Sound Board`: все банки и ключи, рядом клип, кнопка ▶ (работает и без Play), отметка «нет клипа». В Play у `AudioSystem` есть свои кнопки `PlayEvent`.
- **Константы ключей.** Сборщик пишет `<Bank>BankAPI.cs` генератором модуля, код зовёт `TankBankAPI.EngineLoopEvent`, а не строки.

### 3.4. Каналы громкости

Микшер `AudioMixer.mixer` перестраиваю. Группа `Sound` переименовывается в `Effects`, ссылки старых источников на неё сохраняются.

| Канал (меню) | Группа микшера | Открытый параметр | Что в нём |
|---|---|---|---|
| Общая | `Master` | `MasterVolume` | всё |
| Музыка | `Master/Music` | `MusicVolume` | музыка, победа и поражение |
| Эффекты | `Master/Effects` | `EffectsVolume` | танк, оружие, разрушение, монстры, орда |
| Интерфейс | `Master/UI` | `UIVolume` | кнопки и экраны |
| Окружение | `Master/Ambient` | `AmbientVolume` | ветер, дождь, гром |

`IAudioVolumes` (из спецификации меню): `Get/Set(channel, 0..1)`. Значение 0..1 переводится в децибелы `20·log10(v)`, ноль — −80 дБ. Храню только в микшере, сохраняет меню через `ISettingsStore`.

### 3.5. Пауза

Когда бой на паузе, `GameAudio.Pause()` замораживает звуки мира: петли встают и продолжаются с того же места. Интерфейс и музыка играют дальше.

Источник паузы — `IAudioPauseSource` в адаптере. Пока ядра нет, реализация смотрит на `Time.timeScale == 0`: так ставит паузу заглушка `IPauseService` меню. Потом ядро даст события GameLoop, и меняется одна реализация.

### 3.6. Как звук узнаёт о событиях игры

**Сущности (танк, Regax).** `GameAudio` подписывается на мир сущностей `GameContext` и сам добавляет звуковые поведения сущностям по тегам и ключам. Это делается при появлении сущности, префабы танка и Regax не трогаю.

| Сущность | Поведение | Слушает |
|---|---|---|
| Танк игрока | `TankEngineAudioBehaviour` | `TankEngine.State`, `DriveInput`, `DriveBoost`, скорость тела; ставит параметры `tank.rpm`, `tank.throttle`, `tank.speed` |
| Танк игрока | `TankTracksAudioBehaviour` | `Suspension`, `MudSurface`, `TankWheelSpin`; параметры `tank.mud`, `tank.slip` |
| Танк игрока | `TankTurretAudioBehaviour` | скорость башни; параметр `tank.turret` |
| Танк игрока | `TankHitAudioBehaviour` | `DamageTakenEvent`, `DeathTakenEvent` |
| Оружие танка | `WeaponFireAudioBehaviour` | `FireEvent`, конец перезарядки пушки (`WeaponCooldown`) |
| Regax | `MonsterAudioBehaviour` | `DamageTakenEvent`, `DeathTakenEvent`, выстрел оружия-руки, шаг по скорости, рык с случайным интервалом |

Все параметры глобальные в `AudioSystem`: танк игрока один. У монстров параметров нет.

**События без сущности** — взрыв, удар по вокселям, обломки, давка, грязь, авиаудар, дождь. Сейчас у них нет события, только методы. Предлагаю два маленьких статических класса сигналов, по строке вызова в месте события:
- `Game.Voxels/VoxelSignals.cs`: `Blast`, `Impact`, `Fracture`, `DebrisLanded`, `RainChanged`;
- `Game/Shared/Events/GameSignals.cs`: `Explosion`, `MudHit`, `Crushed`, `AirStrikeCalled`, `AirStrikeBombFalling`.

Сигнал — `static event Action<in struct>` без аллокаций. На него подписывается только `GameAudio`, и его могут слушать камера (тряска) и HUD.

Места вызова:
- `VoxelDamage.cs:25,76,130`;
- `VoxelImpactFracture.cs:60`;
- `VoxelDebris.cs:263`;
- `VoxelRain.cs:86,116`;
- `ExplosionUseCase.cs:32`;
- `MudImpactUseCase.cs:41,64`;
- `TankCrushBehaviour.cs:206`;
- `VoxelAirStrike.cs:139,152`.

Это чужие файлы, вопрос 1.

**Как `GameAudio` появляется.** Пока ядро не собирает уровень из сцен, `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` создаёт префаб `GameAudio`, если на сцене есть `GameContext`. Префаб `AppAudio` — один на игру, `DontDestroyOnLoad`. Сцену VoxelSandbox не правлю. Когда появится AppContext, `AppAudio` и оба интерфейса станут его значениями, а `GameAudio` переедет в сцену уровня.

### 3.7. Банки и ключи

Полный ключ — `<bank>.<key>`.
- **3D** — звук в точке или едет за объектом.
- **Петля** — играет, пока условие верно; гаснет за 0.2–0.5 с.
- **Лимит** — `maxFrequency`, повтор не чаще, с.
- **Клип сейчас** — что звучит, пока пользователь не положил свой; «—» — тишина.

**`tank`** — канал Эффекты, 3D, едет за танком.

| Ключ | Когда | Параметры и поведение | Клип сейчас |
|---|---|---|---|
| `engine_start` | `TankEngine` → Starting | — | — |
| `engine_loop` | двигатель работает, петля | высота 0.8→1.6 и громкость 0.6→1 по `rpm` | — |
| `engine_stop` | двигатель заглох | — | — |
| `boost` | нажат Shift | — | — |
| `tracks_loop` | танк едет, петля | громкость и высота по `speed` | — |
| `tracks_mud_loop` | едет по грязи, петля | громкость `speed × mud` | — |
| `wheel_spin_loop` | буксует, петля | громкость по `slip` | — |
| `turret_loop` | башня вращается, петля | громкость по `turret` | — |
| `ram` | таран, удар корпусом | лимит 0.15 | `Common/MeleeDamage` |
| `landing` | подвеска упала на землю после прыжка | лимит 0.3 | `Common/BodyFall2` |
| `hit` | попадание по танку | лимит 0.1 | `Common/BulletDamage` |
| `low_health` | здоровье ниже 25%, один раз | 2D | `Common/Alarm4` |
| `destroyed` | гибель танка | — | `etfx_explosion_magic` |

Параметры `tank`: `rpm`, `throttle`, `speed`, `mud`, `slip`, `turret` — все 0..1.

**`weapons`** — Эффекты, 3D.

| Ключ | Когда | Клип сейчас |
|---|---|---|
| `cannon_shot` | выстрел пушки | `etfx_shoot_grenade` |
| `cannon_reload` | пушка снова готова | — |
| `mg_shot` | выстрел пулемёта, лимит 0.05 | `Weapons/Fire (MachineGun)` |
| `impact_ground` | пуля или снаряд в землю | — |
| `impact_voxel` | в воксельную постройку | — |
| `impact_flesh` | в монстра | `Common/BulletDamage` |
| `airstrike_call` | вызов авиаудара, 2D | `Common/Alarm` |
| `airstrike_whistle` | бомба летит, едет за бомбой | — |

**`destruction`** — Эффекты, 3D.

| Ключ | Когда | Клип сейчас |
|---|---|---|
| `explosion_small` | снаряд пушки | `etfx_explosion_magic` |
| `explosion_large` | бомба авиаудара | `etfx_explosion_laser` |
| `voxel_break` | взрыв или удар выбил воксели; громкость по числу | — |
| `voxel_fracture` | постройка раскололась при ударе | — |
| `debris_land` | обломки упали; лимит 0.08 | — |
| `mud_splash` | попадание в грязь | — |

**`monsters`** (Regax) — Эффекты, 3D.

| Ключ | Когда | Клип сейчас |
|---|---|---|
| `growl` | случайно раз в 4–10 с, пока жив | — |
| `roar` | заметил танк или появился | `Zombie/ZombieAttack1` |
| `step` | шаг, по скорости | `Character/MoveStep1-4` |
| `attack` | удар | `Zombie/ZombieAttack*` |
| `hit` | ранен; лимит 0.05 | `Zombie/ZombieTakeDamage*` |
| `death` | погиб; лимит 0.05 | `Zombie/ZombieDeath*` |
| `crushed` | раздавлен танком | `Common/MeleeDamage` |
| `gibs` | разлетелся на куски | `Common/BodyFall1` |

**`horde`** — Эффекты, 2D. Заглушки: орду делает команда AI. Они зовут `GameAudio.Horde.SetDensity(0..1)` и `WaveStarted()`.

| Ключ | Когда | Клип сейчас |
|---|---|---|
| `crowd_loop` | гул толпы, громкость по `horde.density` | — |
| `wave_start` | новая волна | `Common/Alarm3` |

**`world`** — канал Окружение, 2D.

| Ключ | Когда | Клип сейчас |
|---|---|---|
| `wind_loop` | всегда в бою | — |
| `rain_loop` | идёт дождь, громкость по `world.rain` (плавно 0↔1) | — |
| `thunder` | в дождь раз в 15–40 с | — |

**Интерфейс** (`UISoundCatalog`, канал Интерфейс). Ключи — как в `IUiSounds` меню: `hover`, `click`, `back`, `open`, `purchase`, `error`, плюс `victory`, `defeat` для итогов боя (канал Музыка). Клипов нет.

**Музыка** (`MusicCatalog`, канал Музыка):
- `MenuMusic` — по кругу в меню и ангаре;
- `BattleMusic` — в бою, случайный порядок.

Сейчас в обоих — `Music/Music.wav`. Переходы между ними — затухание 1 с.

### 3.8. Старый звук

Когда события из 3.6 зазвучат через AudioEngine, старые источники надо снять: `WeaponFireSoundInstaller` с `Tank_01`, `CharacterAudioInstaller` с `Regax`. Иначе звук двойной. До этого новые поведения их не дублируют: оружие и Regax подключаются, только если на сущности нет старого установщика. Снимать их и удалять скрипты — вопрос 2.

### 3.9. Производительность

- Предзагрузка всех клипов банков уровня: правила импорта из 3.3 плюс `AudioClip.LoadAudioData()` для клипов загруженных банков при старте уровня. Рывка 29 мс при давке не будет.
- Ноль аллокаций на проигрывание: пулы модуля, строки ключей — константы, сигналы — структуры.
- Лимиты повторов на массовых событиях (давка, обломки, пулемёт). Не больше 32 одновременных источников — `Real Voices`.
- Свой `Update` только у `AudioSystem` (ядро модуля) и у `AppAudio` (`// perf-ok: ticks during pause (UI, music)`). Остальное тикает через Atomic.
- Проверка — `/perf-check`, сценарий crush: кадр без `LoadFMODSound`.

## 4. Тесты

- **Модуль, EditMode:** банк, ключи, длительность по клипу, петля, ручки, пауза.
- **Адаптер, EditMode:**
  - обороты из скорости и газа;
  - громкость 0..1 ↔ дБ;
  - сборщик: папка ключа → клипы события;
  - правила импорта;
  - у каждого ключа из таблиц есть событие.
- **Адаптер, PlayMode в сцене:** танк едет → `engine_loop` играет и его высота растёт; давка → `monsters.crushed` без загрузки с диска; пауза → события стоят.
- **Сборка плеера Windows:** собирается без ошибок.

## 5. Вопросы тимлиду

1. **Сигналы в чужих файлах** (3.6). По строке вызова в `VoxelDamage`, `VoxelImpactFracture`, `VoxelDebris`, `VoxelRain`, `ExplosionUseCase`, `MudImpactUseCase`, `TankCrushBehaviour`, `VoxelAirStrike`. Плюс два новых файла сигналов: `Game.Voxels/VoxelSignals.cs`, `Shared/Events/GameSignals.cs`. Можно делать сейчас в `game/audio`, а конфликты с чисткой решу при слиянии? Или ждать влития чистки?
2. **Снятие старого звука** (3.8). Снимаю `WeaponFireSoundInstaller` и `CharacterAudioInstaller` с префабов `Tank_01` и `Regax` и удаляю их скрипты (`DamageSound`, `DeathSound`, `AttackSound`, `MoveStepSound`, `AudioSourceInstaller` и пул в `GameContextInstaller`) после влития чистки? Или это делает чистка?
3. **Интерфейсы `IAudioVolumes` и `IUiSounds`.** Объявляет меню в `Game`, реализую я в адаптере, в AppContext кладёт ядро. Пока AppContext нет, доступ через `AppAudio.Volumes` и `AppAudio.UiSounds`. Подходит?
4. **Канал «Окружение»** беру из меню как есть. Пять каналов — окончательный список.
5. **Звуки орды** — заглушки и два метода для команды AI (3.7). Передать им?
