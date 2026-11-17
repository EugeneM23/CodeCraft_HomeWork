# План: звук игры на AudioEngine

**Дата:** 2026-11-17
**Спецификация:** `Docs/superpowers/specs/2026-11-17-game-audio.md`
**Ветка:** `game/audio`

## Готово

- [x] Модуль `Assets/Modules/AudioEngine`: перенос, исправления ядра, тесты (9/9), README — `9fc7b05c6`.
- [x] Каркас адаптера:
  - сборки `Game.AudioEngine.Adapters` (`Assets/Game/Audio/Scripts`), `.Editor`, `.Tests`;
  - `AudioKeys` — ключи;
  - `AudioLayout` — таблица звуков: канал, 3D, петля, лимит, клипы по умолчанию;
  - `AudioChannel`.

## Задачи

### A. Рантайм (`Assets/Game/Audio/Scripts`), субагент 1
1. `GameAudioVolumes : IAudioVolumes` — каналы на микшере, 0..1 ↔ дБ.
2. `GameUiSounds : IUiSounds` — поверх `UISoundPlayer`.
3. `AppAudio` — один на игру, `DontDestroyOnLoad`: UI-звуки, музыка (`MusicPlayer`, каталоги меню и боя, затухание), громкость.
4. `GameAudio` — на уровень:
   - `AudioSystem` с банками;
   - предзагрузка клипов;
   - пауза через `IAudioPauseSource`, сейчас `TimeScalePauseSource`;
   - запуск через `RuntimeInitializeOnLoadMethod`, если есть `GameContext`.
5. `EntityAudioDirector` — вешает звуковые поведения на сущности мира `GameContext`:
   - танк: двигатель, гусеницы, башня, попадания;
   - оружие: выстрел, перезарядка;
   - Regax: рык, шаги, атака, ранение, смерть.

   Старые звуковые установщики на сущности не дублирует.
6. Поведения событий AudioEngine: высота и громкость по параметру через кривую.
7. Окружение: ветер в бою, дождь по `VoxelRain.enabled` с плавным `world.rain`, гром.
8. API орды: `SetDensity`, `WaveStarted`.

### B. Редактор (`Assets/Game/Audio/Scripts/Editor`), субагент 2
1. Микшер: группы `Music`, `Effects` (бывшая `Sound`), `UI`, `Ambient`, открытые параметры `<Канал>Volume`.
2. `Tools/Audio/Sync Banks`: по `AudioLayout` создаёт и обновляет:
   - папки `Clips/<bank>/<key>/`;
   - события `Events/<bank>/<bank>_<key>.asset`;
   - банки `Banks/<bank>.asset`;
   - каталог UI и каталоги музыки.

   Клипы папки ключа идут в событие, а если папка пуста — клипы по умолчанию. Ручные настройки события не трогает.
3. Правила импорта (`AssetPostprocessor`), запуск синхронизации после импорта клипа в `Clips/`.
4. Окно `Tools/Audio/Sound Board`: прослушивание без Play, отметка «нет клипа».

### C. После A и B
1. Префабы `GameAudio` и `AppAudio`.
2. Тесты адаптера.
3. Компиляция.
4. Сборка плеера Windows.

### D. После ответа тимлида
1. Сигналы взрывов, разрушения, давки, грязи, авиаудара.
2. Снятие старого звука.

### E. Документы и проверка
1. `Docs/Audio/README.md`.
2. Ревью субагентом.
3. `/perf-check`.
4. Отчёт тимлиду.
