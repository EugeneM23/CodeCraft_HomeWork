# Проверка производительности и порядка: game/audio-m1

**Дата:** 2026-11-18 **Коммит:** `d1f9eb7a5` **База:** `dc272cb08` (`game/develop` в точке ответвления) **Редактор:** замер не снимался (см. «Замер»): версии, рендера, качества, экрана и Scene view нет

Проверялось: `Assets/Game/Audio`, `Assets/Game/Scripts/Shared/Events/GameSignals.cs`, `Docs/`. Взято у других команд как есть и не проверялось по поручению тимлида: `Assets/Game/Scripts/App/Contracts/Run`, `App/Catalog`, `App/Run`, `App/AppContextAPI.cs` (ядро, `claude/core-run`), `Features/Character/EnemyCharacter/EliteStatus.cs` (AI). Эти файлы проверят ветки своих команд.

## Итог

**Можно вливать.** Новых нарушений нет, долг не вырос, бенчмарк не снимался (правило команды: окна редактора не открывать; сцены вехи с ордой ещё нет).

## Замер

Не снимался: `measure: no`, `play: none`.
- Правило команды запрещает открывать окна редактора.
- Сцены арены с ордой (сценарий `horde`, Performance.md раздел 3) ещё нет, а в VoxelSandbox почти весь новый код вехи молчит: нет `IRunService`, `IEnemyDeaths`, орды и элиты.

Таблицы сценариев и сравнения с историей поэтому нет. Прошлой строки ветки `game/audio-m1` в истории нет. Есть строки `game/audio` от 2026-11-17, но это другая ветка и другая база, с ними не сравнивалось.

Оценка по коду (не замер):
- Новый покадровый код — `AppAudio.Update` → `MusicIntensity.Tick` (одна `Mathf.Exp`) и `LayeredMusic.Tick` (три `AudioSource.volume`). Порядок — микросекунды, выделений нет.
- Остальное — события. Одно `GameAudio.Play` на событие, повторы ограничены `maxFrequency`. На всю стаю Ползунов одна петля `crawler.pack_loop`, а не источник на каждого монстра.

Когда появится сцена с ордой, ветке, в которой она появится, нужно снять замер «до и после» по сценарию `horde` с живым звуком (Performance.md раздел 5, «Замер своими силами»).

## Статическая проверка

`bash Tools/PerfCheck/static_check.sh --repo <worktree> --base dc272cb08`.

**Новые нарушения:** 0.

**Ложные срабатывания:** нет (новых срабатываний не было).

**Пометки `// perf-ok`:**
- `AppAudio.cs:209` — `Update() // perf-ok: ticks during pause (music fades, track changes, battle layers)`. Пометка была и раньше, ветка расширила только причину.
- Причина правдоподобна: музыка и её слои должны идти и плавно меняться в паузе (выбор навыка ставит бой на паузу). Правило раздела 5 прямо разрешает `Update` для «звука UI» и того, что работает в паузе.

**Долг по правилам** — по сравнению с прошлой строкой `static` в истории (`game/debris` `f07b35a6f`, база `619ca415d`):

| Правило | было | стало |
|---|---|---|
| camera-main | 9 | 10 |
| debug-log | 0 | 1 |
| file-type-name | 2 | 8 |
| asset-prefix | 29 | 30 |
| unused-assets | 12 | 14 |
| остальные | без изменений | без изменений |
| **всего** | **118** | **129** |

- Ветка ничего из этого роста не добавила: по всем правилам `new 0` относительно `dc272cb08`, а выборочно проверенные пункты долга есть уже в базе.
  - Пункты: `Audio/Scripts/Editor/AssemblyInfo.cs`, `Voxels/Runtime/VoxelBody.Structure.cs`, `Shaders/Outline (Base).shadergraph`, `Materials/MT_Rocket_01.mat`.
- Рост пришёл в `game/develop` между `619ca415d` и `dc272cb08` вместе с влитием веток.
  - Например, новые `file-type-name` у `game/debris`: partial-файлы `VoxelDebris.Cells.cs`, `VoxelDebris.Earth.cs`.
  - Строки `static` для `game/develop` на `dc272cb08` в истории нет. Поэтому для ветки звука это не ВНИМАНИЕ, а отметка для тимлида: долг `game/develop` вырос на 11.

## Ручной обзор изменений

Смотрелись все изменённые `.cs` в `Assets/Game/Audio/Scripts` (рантайм и Editor), `GameSignals.cs`, префабы `AppAudio.prefab` и `GameAudio.prefab`, новые ассеты банков, событий и музыки, тесты.

Без замечаний:
- **Аллокации в тике.** Новых нет.
  - `LayeredMusic`: массивы на 3 слоя делаются один раз, `Tick` ничего не выделяет.
  - `MusicIntensity`, `AudioStreak`: только значения.
- **Аллокации на событие.** Новых нет.
  - Делегаты кэшируются в конструкторах: `GameSignalAudio` (`_onXpPicked`, `_onEliteAppeared`, `_onEliteGone`) и `RunEventAudio` (все шесть подписок).
  - `EnemyDeath` — `readonly struct`, в `PlayDeath` передаётся через `in`.
  - Отписка симметрична подписке (`Unsubscribe`, `Dispose`).
- **GetComponent, Find, Camera.main, Instantiate, Destroy** — новых нет.
- **Звук стаи.** Одна петля `crawler.pack_loop` на всех Ползунов: `HordeAudio.SetAlive`, вызывается не чаще 4 раз в секунду, по спецификации AI.
- **Повторы.** Ограничены `maxFrequency` в `AudioLayout`: укус 0.05 с, смерть 0.03 с, опыт 0.04 с, серия давки 0.05 с.
- **Двойные звуки.** Убраны.
  - Где уровень сообщает о смертях (`GameAudio.DeathsReported`), смерть и давка звучат один раз, из `RunEventAudio`.
  - `MonsterAudioBehaviour.OnDeath` и `GameSignalAudio.OnCrushed` в этом случае молчат.
  - Элита не получает `MonsterAudioBehaviour`: работает через `EntityAudioDirector.HasOwnSounds`, вызов один раз при подключении сущности.
- **Предзагрузка.** Четыре новых банка добавлены в список `GameAudio.prefab`, клипы предзагружаются `Preload()` при старте уровня.
- **Синхронный старт слоёв.** `PlayScheduled` на один `dspTime` с запасом 0.3 с, для незагруженных клипов вызывается `LoadAudioData`. Источники слоёв стоят на паузе: `volume 0`, `Stop` после затухания. Лишнего микширования нет.
- **Один тип верхнего уровня на файл.** Соблюдено, `enum Fade` вложен в `LayeredMusic`.
- **Папки.** `Audio/Clips/<банк>/<ключ>/README.txt`, `Audio/Events/<банк>/`, `Audio/Banks/`, `Audio/Music/`, `Audio/Prefabs/`: все папки уже были в базе и соответствуют Project.md раздел 1.
- **asmdef.** Не менялись.
- **Чужие файлы.** Ветка правит только `GameSignals.cs` (одно событие `XpPicked`) — как и оговорено в спецификации вехи.

Мелочи (не блокируют):
1. `Assets/Game/Audio/Scripts/App/LayeredMusic.cs:21-22` — два `<summary>` подряд у `START_DELAY`. Остался старый вариант комментария, первый нужно удалить.
2. **Тесты.** Покрыты `AudioStreak`, `RunAudio` (серии без уровня) и `MusicIntensity`. Без тестов осталась логика, которую правило «одна смерть — один звук» из коммита `d1f9eb7a5` и делает важной:
   - `RunEventAudio.PlayDeath` — маршрут по виду, давке и флагу элиты;
   - `EliteAudio.Appeared`/`Gone` — повторный вызов ничего не делает;
   - `HordeAudio.SetAlive` — деление на `FullHorde`, ноль;
   - `LayeredMusic` — затухание. Требует `AudioSource`, это PlayMode, поэтому может подождать.

   Первые три — чистые EditMode-тесты без уровня: `GameAudio` без экземпляра молчит.
3. **Расхождение со спецификацией вехи.** В спецификации `SetAlive(crawlers01, total01)`, в коде `SetAlive(int alive)`, а громкость стаи равна общей доле живых (`_crawlers = _alive`). Это решение по звуку, а не по производительности; стоит поправить спецификацию или код, чтобы AI вызывал то, что написано.

## Что сделать

1. (Звук, `game/audio`) Дописать EditMode-тесты на `RunEventAudio.PlayDeath`, `EliteAudio.Appeared`/`Gone`, `HordeAudio.SetAlive` — до влития или первым коммитом после.
2. (Звук) Убрать лишний `<summary>` в `LayeredMusic.cs:21`. Свести `SetAlive` в спецификации вехи и в коде к одному виду.
3. (Звук вместе с AI, когда появится сцена арены с ордой) Добавить сценарий `horde` в `Tools/PerfCheck/Bench/` и снять замер «до и после» со звуком вехи: 60 живых, волны, элита, давка серией.
4. (Тимлид / «Производительность и порядок») Записать строку `static` для `game/develop` на текущем коммите. Долг `game/develop` между `619ca415d` и `dc272cb08` вырос с 118 до 129: `file-type-name` +6, `unused-assets` +2, `camera-main`, `debug-log`, `asset-prefix` по +1. Это нужно разобрать с командами, которые его внесли.
