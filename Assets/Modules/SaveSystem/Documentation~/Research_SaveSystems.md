# Исследование: системы сохранений

Сводка поиска 2026-10-23 по трём направлениям — архитектура, данные и хранилище, геймдизайн сохранений в
платформерах — и сравнение с импортированным кодом домашки (`_Import/HomeWork06_SaveSystem`) и решениями из
[SaveSystem_Plan.md](SaveSystem_Plan.md), раздел 5. Источники — в конце. Выводы, которых нет в источниках
напрямую, помечены «(вывод)». Страницы Steamworks напрямую не открылись — пункты о Steam по выдержкам поиска.

**Как использовать.** Раздел 1 — выжимка. Разделы 2–4 — что нашли. Раздел 5 — сравнение с нашим. Раздел 6 —
вопросы, после ответов на которые переписываем план.

## 1. Выжимка

1. Единого «лучшего» устройства нет, но у зрелых систем общее: данные — обычные C#-классы (DTO), у каждой системы
   своя секция со своей версией, хранилище заменяемое, восстановление идёт фазами в фиксированном порядке.
2. Наша форма — типизированный DTO и маппер на ключ (`GameSerializer<TService, TData>`) — одна из признанных.
   Ей не хватает версий, миграций и порядка восстановления.
3. Гибридное восстановление мира (сценовые на месте, удалённые удаляются, заспавненные пересоздаются) — так
   работают Pixel Crushers, Opsive, Celezt. Решение 1 подтверждается.
4. ID сценового объекта — GUID, назначенный в эдиторе, никогда не на ассете префаба; дубликаты ловят реестр и тест;
   ID живут в пределах сцены. Ключ префаба в каталоге — GUID ассета, а не имя.
5. Платформеры (Hollow Knight, Celeste, Blasphemous, Shovel Knight, Dead Cells) **не сохраняют** фазу платформ,
   ловушек и обычных врагов. Они хранят флаги разовых событий (рычаг, дверь, предмет, босс) и **ID чекпоинта
   вместо координат** игрока. Это расходится с нашим решением 2.
6. Запись — только через временный файл и замену с бэкапом; битый файл не роняет игру, а откатывается на `.bak`.
7. Слот — самостоятельная единица; метаданные слота (время, время игры, прогресс) — отдельный маленький файл для
   меню; настройки — отдельно от прогресса.
8. Основные сохранения — по событиям (чекпоинт, выход). Таймер — страховка: только когда есть изменения и не в
   небезопасный момент (смерть, переход, загрузка).
9. Newtonsoft подходит: сейв платформера — единицы КБ, скорость не важна, важна читаемость. Но с DTO вместо
   «JSON в строке», с `ObjectCreationHandling.Replace`, `TypeNameHandling.None` и `link.xml` перед сборкой на IL2CPP.
   BinaryFormatter — нельзя.
10. Шифрование — опция в части библиотек (Easy Save, IntoTheDev), но для отладки и баг-репортов ценят читаемый
    JSON. Решение 4 (убрать AES) не противоречит практике.

## 2. Архитектура

### 2.1. Как устроены известные системы

| Система | Данные | ID объектов сцены | Заспавненные и удалённые | Заметки |
|---|---|---|---|---|
| Easy Save 3 | Ключ → значение в файле; `Save` дописывает или заменяет ключ | `long` из менеджера ссылок сцены; удалили менеджер — ID пересоздались, сейвы сломаны | `ES3Prefab` создаётся при загрузке | Ссылки на поздно загруженные экземпляры — совет «загрузить дважды» |
| Pixel Crushers Save System | `Saver`: `RecordData() → string`, `ApplyData(string)`; запись = ключ + индекс сцены + данные; версия | Ключ, по умолчанию имя объекта; инструмент «назначить уникальные ключи» | `SpawnedObjectManager` (по имени префаба), `DestructibleSaver` | Ближе всех к нашему; сериализатор и хранилище — заменяемые компоненты; `order` восстановления |
| Lowscope Component Save System (open source) | `ISaveable`: `OnSave → string`, `OnLoad`, `OnSaveCondition` (флаг изменений) | Сцена + имя + GUID объекта + имя и GUID скрипта | — | Не обновляется с 2020 |
| GameDevTV (основа большинства туториалов `SaveableEntity`) | `CaptureState/RestoreState`, ключ — имя типа компонента | UUID в edit mode (`[ExecuteAlways]` + статический реестр) | Отдельным дополнением | Два компонента одного типа — коллизия ключей |
| Unity: блог и сэмпл UniteNow | Одна типизированная `SaveData`, `PopulateSaveData/LoadFromSaveData` | UUID врагов | — | PlayerPrefs — только для настроек; BinaryFormatter — нет |
| Unity Open Project 1 «Chop Chop» | Сейв хранит GUID ассетов (локация, квесты), не координаты | GUID ассета кэшируется в `OnValidate` | Предметы — через Addressables по GUID | Перед записью старый файл → `.bak` |
| Odin Serializer | — | Резолверы ссылок по строке, GUID, индексу | — | Unity-объекты в рантайме не сохраняет; у нас Odin вне AudioEngine запрещён |
| Unity Platform Toolkit (6.3+) | Сейв = именованный набор файлов (данные, метаданные, картинка), атомарный `Commit()` | — | — | `EnumerateSaves()`, типизированные ошибки; ядро и локальные сейвы бесплатны, платформенные пакеты — Pro |
| UGS Cloud Save | Ключ-значение (2000 ключей / 5 МБ) или файлы (до 1 ГБ) | — | — | Оптимистичные блокировки записи |

### 2.2. Форма API

| Форма | Примеры | Плюсы | Минусы |
|---|---|---|---|
| Строка на компонент | Pixel Crushers, Lowscope, GameDevTV, Opsive | Децентрализовано, круг «сохранить → загрузить» легко тестировать | Версии размазаны, коллизии ключей |
| Одна типизированная модель сейва | Unity UniteNow, `GameData` + `IBind<TData>` (git-amend) | Одна схема, миграция всей модели | Всё завязано на центральную модель |
| **DTO + маппер на ключ (наша)** | Zajceq (`MigrateData(oldJson, oldVersion)` на компонент) | Типизировано и децентрализовано, миграция на ключ | Шаблонный код |
| Атрибуты и рефлексия | Easy Save Auto Save, Celezt `[Save]` + генерация | Меньше всего кода | Схема спрятана: переименовали поле — сейв сломан; нет гарантий порядка |
| Генерация кода | MemoryPack | Быстро, работает в IL2CPP | По умолчанию нельзя удалять и переставлять поля; сложная настройка генератора в Unity |

Horneman (Watch Dogs: Legion): у каждой системы своя секция; версии на всём; проверить версии до загрузки; грузить
системы в фиксированном порядке; запрещать сохранение, пока не закончилась загрузка. Тестируемость даёт
заменяемое хранилище (заглушка `DummySaveLoader` в Safekeeper).

### 2.3. Стабильные ID

- Имя объекта ломается при переименовании и дублировании. `InstanceID` меняется между запусками. ID из позиции
  ломается при перемещении.
- `GlobalObjectId` — только в эдиторе, пуст до сохранения сцены, меняется при переносе в другую сцену и при
  распаковке префаба. Его можно «запечь» в поле и заморозить после первого назначения (Safekeeper).
- Сериализованный GUID (Unity `guid-based-reference`) — основной подход:
  - назначается в эдиторе;
  - у ассета префаба GUID нет, иначе все экземпляры его скопируют;
  - дубликат при регистрации в реестре получает новый GUID;
  - правка экземпляра префаба фиксируется `PrefabUtility.RecordPrefabInstancePropertyModifications`.

  Дыра: выключенные объекты не регистрируются. Ловить дубликаты по сохранённому `InstanceID` нельзя — после
  перезапуска эдитора ложные срабатывания.
- Fluid Unique ID: окно починки и тест в CI на пустые и повторные ID.
- **Практика:** назначать в эдиторе, не на ассете префаба; дубликаты — реестр и тест; ID в пределах сцены.

### 2.4. Заспавненные и удалённые объекты, порядок, ссылки

- Ключ префаба, от хрупкого к надёжному: индекс в массиве → имя префаба → явный ID в каталоге → GUID ассета или
  ключ Addressables. GUID лежит в `.meta` и переживает перенос и переименование.
- Удалённые сценовые объекты записывать в игровом пути уничтожения (мир снимает сущность с причиной) или считать
  при сохранении как «сценовые ID минус живые». Не в `OnDestroy`: он срабатывает и при выгрузке сцены, и при выходе.
- Порядок: сначала создать все объекты, потом раздать данные, потом связать ссылки. Easy Save грузит дважды,
  Pixel Crushers — `order` и задержка в кадрах, Opsive — приоритеты (предметы раньше инвентарей).
- Ссылки между объектами — только ID. `$id/$ref` у Newtonsoft работает в пределах одного документа и не подходит.

### 2.5. Области данных

- Глобальное (живёт между сценами) отдельно от сценового. Pixel Crushers и Safekeeper держат состояние всех
  посещённых сцен в памяти и пишут на диск целым снимком (Safekeeper — на чекпоинтах).
- Сцена записывает свою секцию **перед выгрузкой** и применяет её после загрузки. Поздно зарегистрированные берут
  данные из кэша (Opsive). Ждать сигнала готовности, а не «один кадр».
- Внутри снимка — слияние по ключам; устаревшее удаляется явно (`DeleteObsoleteSaveData` в Pixel Crushers).
- Celeste делит данные на три уровня: Settings (глобально), SaveData (файл сохранения), Session (текущее
  прохождение главы: флаги, ключи, `DoNotLoad` — ID сущностей, которые больше не появляются).

## 3. Данные и хранилище

### 3.1. Формат и сериализатор

| Сериализатор | Плюсы | Минусы |
|---|---|---|
| BinaryFormatter | — | Небезопасен, Microsoft прямо запрещает; в .NET 9+ бросает исключение. Unity 7 переходит на CoreCLR и .NET 10 — такие сейвы перестанут читаться (вывод) |
| JsonUtility | Быстрый, мало мусора, можно в фоновом потоке | Только поля; словари не поддерживает (до 6.5), верхнеуровневые массивы тоже |
| **Newtonsoft** (наш пакет 3.2.1 = Json.NET 13.0.2, AOT-сборка) | Словари, `JObject` для миграций, полиморфизм | IL2CPP вырезает конструкторы, нужные рефлексии → `link.xml` в `Assets/`. `TypeNameHandling` не `None` опасен на чужих данных. `ObjectCreationHandling.Auto` дописывает загруженное в списки с инициализатором → нужен `Replace` |
| MessagePack-CSharp v3 | Бинарный, генерация кода, AOT, режим недоверенных данных | NuGetForUnity и git-пакет; файл нечитаем |
| MemoryPack | Самый быстрый | По умолчанию нельзя удалять и переставлять поля; README: только для доверенных данных |

Сейв платформера — единицы КБ: скорость не важна, важна читаемость — сравнение в тестах, сейв во вложении
баг-репорта.

### 3.2. Версии и миграции

- Номер версии формата у каждого файла, отдельно от версии игры. Читать текущую и старые, новее — отказ.
- Своя версия у каждой системы (секции). Цепочка шагов N→N+1 над `JObject` до типизированной десериализации.
- Добавить поле — безопасно (в старых файлах будет значение по умолчанию). Удалить — безопасно, если имя больше
  не используется. Переименовать — шаг миграции или `[JsonProperty("старое имя")]`.
- Поле `version` — без инициализатора: если его нет в файле, читается 0, а не текущая версия.
- `[JsonExtensionData]` сохраняет незнакомые поля при перезаписи.
- Сертификация Xbox (XR-003): обновлённая игра обязана читать старые сейвы.

### 3.3. Надёжность

- Никогда не писать поверх. У самого Unity Platform Toolkit был баг: короткий сейв оставлял хвост старого, JSON
  ломался.
- Атомарная запись:
  1. записать `.tmp` в той же папке и вызвать `Flush(true)`;
  2. `File.Replace(tmp, main, bak)`;
  3. при первой записи — `File.Move`: `Replace` требует существующий файл, а `File.Move` с перезаписью в
     .NET Standard 2.1 нет.

  Атомарность `File.Replace` на Mono и IL2CPP проверять на каждой платформе.
- Обнаружение порчи: заголовок с магическим числом, версией, длиной и CRC — для бинарных форматов. У JSON порча
  обычно ломает разбор.
- Откат: основной файл → `.bak` → автосейвы → новая игра, и сообщить игроку. Битый файл переименовать в
  `.corrupt` для баг-репорта, не удалять.
- `persistentDataPath` на Windows — `AppData\LocalLow\<company>\<product>`: смена имени компании или продукта
  «теряет» сейвы. **У нас сейчас `DefaultCompany` / `Lesson`.**
- Жизненный цикл: на iOS и в вебе `OnApplicationQuit` не вызывается; мобильная ОС убивает приложение в фоне —
  любую потерю фокуса считать возможным выходом. Асинхронная работа при выходе может не завершиться — последнее
  сохранение синхронное.
- Веб: запись в IndexedDB асинхронная — нужен `FS.syncfs` или `autoSyncPersistentDataPath`.

### 3.4. Слоты и метаданные

- Метаданные отдельно от сейва — меню загрузки читает только их (Stardew Valley `SaveGameInfo`, Opsive `.metadata`).
- Слот — самостоятельная единица. Xbox: запись атомарна в пределах контейнера, на связи между контейнерами
  не полагаться.
- В метаданных — время (UTC), время игры, прогресс (уровень, чекпоинт). По ним же решают облачные конфликты.
- Миниатюра — `ScreenCapture.CaptureScreenshotAsTexture` после рендера; PNG можно кодировать в фоне.
- Настройки — отдельно от прогресса; машинные (качество графики) — не в облако.
- Автосейвы — по кругу (`autosave_0..N` у Factorio) или отстающая копия (`_old` у Stardew).

### 3.5. Асинхронность

- Снимок на главном потоке (копия в DTO) → запись в фоне (`Awaitable.BackgroundThreadAsync`) → возврат
  (`MainThreadAsync`). В вебе потоков нет — фоновая запись должна быть необязательной.
- Одна очередь записи: автосейв и ручное сохранение не пишут одновременно, повторные запросы автосейва сливаются.

### 3.6. Облако (на будущее)

- Steam Auto-Cloud синхронизирует папку `LocalLow/<company>/<product>` при запуске и выходе; конфликт решает
  игрок; подпапка `{SteamID}` — для общих ПК.
- UGS Cloud Save: секции ложатся на ключи, весь файл — на Player File; номер ревизии в сейве — для конфликтов.

### 3.7. Тестирование

- Фейковое хранилище в памяти и несколько тестов во временной папке на настоящую замену файла.
- «Золотые» файлы: образец сейва каждой версии лежит в тестах и обязан мигрировать в ожидаемый DTO.
  Отсутствующий образец автоматически не создавать — иначе тест молча превращается в обычный круг
  «сохранить → загрузить».
- Внедрение сбоев: обрезанный файл, битый `.bak`, нет места, `IOException` во время замены — обёртка над
  хранилищем, которая падает по заказу.

## 4. Что сохраняют платформеры

### 4.1. Игры-образцы

| Игра | Когда сохраняет | Позиция игрока | Состояние мира |
|---|---|---|---|
| Hollow Knight / Silksong | Лавка; автосейв при выходе в меню, смерти, событиях прогресса | Всегда последняя лавка, координаты не сохраняются | bool по `(id, сцена)` + признак «полупостоянный» (сброс на лавке и при смерти); у Silksong на весь мир ~1300 bool и ~300 int; `.bak` профилей, откат к автосейвам |
| Celeste | Save & Quit; уровни Settings / SaveData / Session | Начало маленькой комнаты | Флаги, ягоды, ключи, `DoNotLoad`; ягода засчитывается только после приземления |
| Ori | Blind Forest: Soul Link, нельзя в воздухе, в воде, на нестабильной земле, в погонях. Will of the Wisps: автосейв + Spirit Wells | Точка связи или автосейв | На старте Will of the Wisps автосейв терял прогресс рядом со смертями |
| Dead Cells | Вход в уровень, смерть, выход в меню | Начало уровня | Забег отдельно от мета-прогресса |
| Super Meat Boy | Только между уровнями | Начало уровня | Внутри уровня — ничего |
| Blasphemous | Алтари; автосейва нет | Последний алтарь | Враги, кроме боссов, возрождаются |
| Shovel Knight | Только при выходе на карту мира | Чекпоинты — только в пределах сессии | Разбитые ради золота чекпоинты |
| Shadow Gambit (контрпример) | Сохраняет всё: корутины, часы, зерно случайных чисел | Точно | Своя сериализация и проверки зависимостей при сборке — ради жанра «быстрое сохранение и загрузка» |

**Ни один из платформеров не сохраняет фазу платформ, ловушек и обычных врагов.**

### 4.2. Чекпоинт и сохранение

- Три срока жизни состояния: **комната** (сброс при входе и смерти), **до отдыха** (сброс на чекпоинте и при
  смерти — враги в Hollow Knight и Blasphemous), **навсегда** (флаги).
- Два уровня возрождения: шипы или лава → последняя устойчивая земля с небольшим уроном (Hollow Knight);
  смерть → лавка или чекпоинт.
- Смерть не отменяет постоянные флаги: в Hollow Knight смерть сама вызывает сохранение. Celeste забирает только
  ягоду, которую не донесли до земли.

### 4.3. Состояние уровня

- Сохраняют: предметы, способности, рычаги, двери, разрушаемые стены, срезки, боссов.
- Сбрасывают: обычных врагов, платформы, рушащиеся блоки, ловушки.
- Устойчивость к правкам уровня: именованные bool, стабильные ID, сверка в обе стороны — запись без объекта
  игнорируется, объект без записи получает значение по умолчанию. В Hollow Knight нет ключа — значит 0, и объекты
  из патча начинают «нетронутыми».
- Цикличное движение — от одних часов уровня: фаза — функция времени, сохранять по платформе нечего.

### 4.4. Для игрока

- Значок сохранения хотя бы на 1–2 секунды и отдельное состояние «не удалось».
- Не сохранять в небезопасный момент: смерть, переход, катсцена, загрузка. В Far Cry 6 автосейв перед смертью
  запирал игрока в петле смертей.
- Save & Quit доступен всегда, продолжение — от точки привязки.

## 5. Сравнение с нашим

### 5.1. Код домашки и практика

| Аспект | Домашка | Практика | Итог |
|---|---|---|---|
| Форма API | `GameSerializer<TService, TData>`: DTO + маппер на ключ | Одна из признанных форм | **Оставляем** |
| Данные секции | JSON в строке внутри JSON (L12) | DTO, дерево JSON | **Меняем**: секция хранит `JToken` |
| Версии | Нет | Версия формата и версия секции, миграции N→N+1, новее — отказ | **Добавляем** |
| Ключи | Имя типа данных (L2) | Явный ключ системы; у компонентов — ключ и проверка дубликатов | **Меняем** |
| Слияние | `Save` пересобирает словарь и затирает файл (L1) | Снимок в памяти, слияние по ключам, явная чистка | **Меняем** (уже в плане) |
| Порядок восстановления | Нет; «снести всё → заспавнить → раздать» одной функцией | Фазы: удалить → заспавнить → раздать данные по `Order` → связать ссылки → событие «загружено» | **Меняем** |
| Рефлексия `[SaveField]` | Копирование по имени, `Alias` не работает (L13) | Прячет схему (переименовали поле — сейв сломан); IL2CPP может вырезать члены | **Предлагаем убрать** (вопрос Б) |
| ID сценовых сущностей | `int` в порядке `FindObjectsOfType` (L10) | GUID из эдитора в пределах сцены, реестр и тест дубликатов | **Меняем**; в плане был `int` из сцены — пересмотреть (вопрос В) |
| ID заспавненных | `int` из очереди переиспользования (L7) | Своё пространство ID, счётчик в секции сцены | **Меняем** |
| Ключ сцены | — (одна сцена) | GUID сцены, переименование не ломает | GUID сцены запекается в `EntityWorld` в эдиторе |
| Ключ префаба | Имя префаба (L19) | GUID ассета, запечённый в `OnValidate` | **Меняем** |
| Удалённые сценовые | — (сносится всё) | Путь уничтожения мира или «сценовые минус живые», не `OnDestroy` | По плану, уточнить способ |
| Ссылки между объектами | Объекты и ID вперемешку (`TargetObjectSerializer` из RTS) | Только ID | Правило для DTO |
| Хранилище | `WriteAllBytes` поверх, `streamingAssets`, исключения наружу (L3–L5) | `.tmp` + `Replace` + `.bak`, `persistentDataPath`, откат на `.bak`, `.corrupt` | **Меняем**; в плане не было отката на `.bak` |
| Слоты | Один файл | Папка слота и маленький `meta.json` | **Уточняем** (вопрос Г) |
| Сериализатор | Newtonsoft с настройками по умолчанию | Newtonsoft + `Replace` + `TypeNameHandling.None` + `link.xml` | **Уточняем** |
| Шифрование | AES (выключен), пароль в ассете (L17) | Опция в части библиотек; для отладки ценят читаемый JSON | Убрали (решение 4) — подтверждается |
| Автосейв | Таймер, фокус, выход; `Debug.Log` на каждый сейв | Основное — события; таймер — страховка при изменениях; пауза приложения = возможный выход; не в небезопасный момент и не до конца загрузки | **Уточняем** |
| Сервер `DedicatedServer(WIP)` | UniTask + HTTP, без защиты от отсутствия ключа | Облако — потом, через секции или файлы | Не переносим — подтверждается |
| Тесты | Нет | Память, временная папка, золотые файлы, внедрение сбоев | **Добавляем** (часть уже в плане) |

### 5.2. Наши решения и исследование

| # | Решение (план, раздел 5) | Что говорит исследование | Предложение |
|---|---|---|---|
| 1 | Гибрид | Подтверждается (Pixel Crushers, Opsive, Celezt) | **Оставить**; уточнить: GUID в пределах сцены, ключ префаба — GUID ассета, фазы восстановления, своё пространство ID у заспавненных. В игре пока нет заспавненных сущностей, которые надо сохранять (эффекты — из пула, врагов сбрасываем), — пересоздание проверяется тестами модуля на тестовых префабах |
| 2 | Позиция игрока + чекпоинты + состояние объектов уровня | Платформеры не сохраняют фазу платформ и обычных врагов; позиция = ID чекпоинта | **Пересмотреть** (вопрос А) |
| 3 | Несколько слотов, файл на слот | Слот — самостоятельная единица, метаданные отдельно | **Уточнить** (вопрос Г) |
| 4 | Без шифрования | Не противоречит; читаемость ценится | **Оставить** |
| 5 | Убрать `EntityType` | Модуль не знает типов игры | **Оставить** |
| 6 | Ветка `feature/save-system` | — | **Оставить** |
| 7 | F5/F9 и меню | Плюс проверка ID: пункт меню и тест сцен из Build Settings на пустые и повторные ID | **Дополнить** |
| 8 | Удалить `_Import` после переноса | — | **Оставить** |
| 9 | Автосейв по игровому времени | Таймер — страховка, основное — события; только при изменениях; не в небезопасный момент | **Уточнить**: таймер по игровому времени срабатывает, только если есть изменения и игра разрешает (не смерть, не переход, не загрузка); основные сохранения — активация чекпоинта и выход |

### 5.3. Новое, чего не было в плане

1. Конверт файла: `formatVersion`, версия игры, время сохранения (UTC), секции `{ key: { v, data } }`.
2. Версия и миграция у каждого сериализатора; все секции проверяются до применения; файл новее текущей версии —
   отказ; перед миграцией — копия исходного файла.
3. Фазы восстановления и `Order` у сериализатора; сохранение запрещено, пока идёт загрузка.
4. Секции сцен в снимке: сцена записывает свою секцию перед выгрузкой и применяет после загрузки; поздно
   зарегистрированные берут данные из снимка. Это снимает вопрос «кто и когда зовёт `Load`».
5. Сроки жизни данных: «навсегда» и «комната» (не сохраняется) — сейчас; «до отдыха» — вместе с врагами (этап 4).
6. Откат на `.bak`, если основной файл битый.
7. Общие настройки Newtonsoft в модуле; `link.xml` — перед первой сборкой на IL2CPP.
8. Имя компании и продукта (`DefaultCompany` / `Lesson`) задать, пока у игроков нет сейвов: от них зависит путь.
9. Тесты: золотой файл версии 1 (ловит случайную смену формата) и обёртка хранилища со сбоями по заказу.
10. Интерфейс хранилища в духе Platform Toolkit (перечислить слоты, прочитать, атомарно записать набор файлов
    слота) — позже Steam или консоли подключаются адаптером.
11. Снимок отдельно от записи: конверт собирается на главном потоке, запись — отдельный шаг. Сейчас синхронно,
    в фон — когда понадобится.

### 5.4. Что сознательно не берём сейчас

- Заголовок с CRC: у JSON порча ломает разбор, обрыв записи закрывает атомарная замена, а CRC мешает править сейв
  руками при отладке.
- MessagePack и MemoryPack: выигрыш в скорости не нужен, теряется читаемость.
- Platform Toolkit как зависимость: молодой пакет (был баг с хвостом файла), платформенные пакеты — Pro. Берём
  только форму интерфейса.
- Фоновую запись: сейв в КБ пишется за миллисекунды, а структура «снимок → запись» позволит перейти позже.
- Миниатюры слотов, значок сохранения, облако: нет UI и целевых платформ. Поля в метаданных под них оставляем.
- Генерацию кода и атрибуты: схема данных должна быть видна в коде.
- Сохранение фазы платформ — если вопрос А решим по рекомендации.

## 6. Решения по итогам исследования (2026-10-23)

> **Обновление того же дня.** Направление сменилось: пишем не систему под платформер, а общий модульный плагин
> с полным сохранением состояния (раздел 7 и [SaveSystem.md](SaveSystem.md)). Решения А и Б ниже отменены
> (сохраняем всё состояние; поля своих скриптов — через `[Save]`), В пересмотрено (GUID на `Saveable` без
> `EntityWorld`), Г остаётся.

Все четыре — по рекомендации.

| # | Вопрос | Решение |
|---|---|---|
| А | Что сохраняет игра | Позиция игрока — ID последнего активированного чекпоинта; флаги разовых объектов (сейчас — чекпоинты; рычаги, двери, предметы — когда появятся). Фазу платформ, пил и обычных врагов не сохраняем, падающие платформы сбрасываются. Точное продолжение «с места» — позже, через последний безопасный снимок |
| Б | `[SaveField]` и рефлексия | Убрать: перенос данных пишется явно в сериализаторе |
| В | ID сущностей | Один строковый ID: у сценовых — GUID, назначенный в эдиторе; у заспавненных — `spawn-N` со счётчиком из секции сцены; целые ID и очередь переиспользования уходят |
| Г | Слоты | Папка `Saves/slot_N/` с `save.json`, `meta.json` и `save.json.bak` |

## 7. Easy Save 3 — подробный разбор

Версия 3.5.27 (июль 2026), $59, около 1000 отзывов. Форум Moodkie во время поиска почти не открывался —
часть пунктов по выдержкам поиска.

**Устройство.** Файл хранит ключи, как словарь: `ES3.Save(key, value)`, `Load<T>(key)`, `LoadInto` (заполнить
существующий объект), `KeyExists`, `DeleteKey`, `GetKeys`. Настройки (`ES3Settings`) — на вызов или глобально
(ассет в `Resources`): место (файл, PlayerPrefs, память, кэш), шифрование AES, сжатие Gzip. Формат — JSON.
Без кэша каждый вызов открывает файл: 22 объекта под отдельными ключами сохранялись 6,6 с — производительность
даёт только запись одним пакетом.

**Auto Save (без кода).** Окно *Easy Save 3 → Auto Save*: флажки компонентов на объектах, поля выбираются на тип
компонента целиком. По умолчанию сохраняет при выходе, загружает в `Awake`. Ограничения: загрузка только в той же
сцене; порядок загрузки не гарантирован — ссылки на экземпляры префабов чинятся повторной загрузкой; уничтоженные
объекты не отслеживаются; один менеджер на сцену; в вебе `OnApplicationQuit` не вызывается, и сохранение по
умолчанию там не работает.

**Типы.** Из коробки: Transform, коллайдеры (не все 2D), рендереры, Mesh, Camera, Light, Material, Sprite,
Texture2D и AudioClip по значению. **Нет:** Rigidbody, Rigidbody2D, Animator, AudioSource, CharacterController.
ParticleSystem — только настройки, не частицы; загрузка в играющую систему бросает исключение. Свои классы —
рефлексией: `public`, `[SerializeField]`, `[ES3Serializable]`; свойства — только по атрибуту. Особые типы —
сгенерированные классы `ES3Type` (`Write` по именам свойств, `Read` — цикл по свойствам со `Skip` неизвестных).

**Ссылки и ID — главная боль.** `ES3ReferenceMgr` в каждой сцене собирает ID всех объектов, компонентов и их
зависимостей. Удалил или пересобрал менеджер — сломаны все сейвы. На больших проектах открытие сцены выросло с
1–2 до 15–20 минут, эдитор занимал гигабайты памяти. `ES3Prefab` на префабе даёт ID для спавна, но копии и варианты
префаба делят один ID. Уничтоженные объекты — самодельным скриптом с GUID.

**Версии, бэкапы, слоты.** Имена полей хранятся — добавлять и удалять поля безопасно; версий и миграций нет,
сменили тип поля — `FormatException`. Бэкапы — вручную (`CreateBackup`/`RestoreBackup`), внутри запись идёт через
временный файл. Слоты есть (UI на UGUI + TMP), метаданных у слотов нет.

**JsonUtility на MonoBehaviour** (проверено для нас): `ToJson`/`FromJsonOverwrite` сериализуют ровно те поля, что
видит Inspector, но ссылки на Unity-объекты пишутся как `instanceID` — валидны только в текущей сессии. Годится
лишь как запасной путь, и ссылки надо переписывать на свои ID.

**Берём:** чек-лист компонентов на объекте; каталог сериализаторов типов + рефлексия для остального; заполнение
существующих объектов (недостающие ключи не трогают значения, неизвестные пропускаются); запись одним пакетом;
временный файл → бэкап → замена — с автоматическим откатом, которого у ES3 нет.

**Не повторяем:** реестр всех зависимостей в каждой сцене; общие ID у копий префабов; «загрузить дважды» —
вместо этого фазы: сначала объекты (спавн, уничтожение), потом данные, потом колбэки; оставлять удаления
пользователю; полные имена типов сборок в данных; отсутствие версий; сохранение только при выходе по умолчанию.

Источники: [документация Easy Save 3](https://docs.moodkie.com/easy-save-3/),
[Auto Save](https://docs.moodkie.com/easy-save-3/es3-guides/auto-save-saving-without-code/),
[поддерживаемые типы](https://docs.moodkie.com/easy-save-3/es3-supported-types/),
[ссылки](https://docs.moodkie.com/easy-save-3/es3-guides/saving-and-loading-references/),
[объекты и префабы](https://docs.moodkie.com/easy-save-3/es3-guides/saving-loading-gameobjects-prefabs/),
[ES3Types](https://docs.moodkie.com/easy-save-3/es3-guides/controlling-serialization-using-es3types/),
[кэш](https://docs.moodkie.com/easy-save-3/es3-guides/caching/),
[бэкапы](https://docs.moodkie.com/easy-save-3/es3-guides/backups/),
[слоты](https://docs.moodkie.com/easy-save-3/es3-guides/creating-save-slots/),
[тема на Unity Discussions](https://discussions.unity.com/t/easy-save-the-complete-save-game-data-serializer-asset/446212),
[Asset Store](https://assetstore.unity.com/packages/tools/utilities/easy-save-the-complete-save-game-data-serializer-system-768),
[JsonUtility.ToJson](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.ToJson.html),
[FromJsonOverwrite](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.FromJsonOverwrite.html).

## Источники

**Архитектура**

- Easy Save 3 — [getting started](https://docs.moodkie.com/easy-save-3/getting-started/),
  [references](https://docs.moodkie.com/easy-save-3/es3-guides/saving-and-loading-references/)
- Pixel Crushers Save System — [manual](https://www.pixelcrushers.com/dialogue_system/manual2x/html/save_system.html),
  [Saver](https://www.pixelcrushers.com/dialogue_system/manual2x/html/class_pixel_crushers_1_1_saver.html),
  [SavedGameData](https://www.pixelcrushers.com/dialogue_system/manual2x/html/class_pixel_crushers_1_1_saved_game_data.html),
  [DestructibleSaver](https://www.pixelcrushers.com/dialogue_system/manual2x/html/class_pixel_crushers_1_1_destructible_saver.html)
- Lowscope Component Save System — [README](https://github.com/AlexMeesters/ComponentSaveSystem)
- GameDevTV — [ID в edit mode](https://community.gamedev.tv/t/can-someone-explain-in-depth-why-we-execute-saveableentity-cs-in-edit-mode/145881),
  [заспавненные](https://community.gamedev.tv/t/procedurally-spawned-characters/227493)
- Unity — [блог о сохранениях](https://unity.com/blog/games/persistent-data-how-to-save-your-game-states-and-settings),
  [UniteNow20-Persistent-Data](https://github.com/UnityTechnologies/UniteNow20-Persistent-Data),
  [Open Project 1: SaveSystem.cs](https://github.com/UnityTechnologies/open-project-1/blob/main/UOP1_Project/Assets/Scripts/SaveSystem/SaveSystem.cs),
  [SerializableScriptableObject.cs](https://github.com/UnityTechnologies/open-project-1/blob/main/UOP1_Project/Assets/Scripts/SaveSystem/SerializableScriptableObject.cs)
- ID — [guid-based-reference](https://github.com/Unity-Technologies/guid-based-reference),
  [GlobalObjectId](https://docs.unity3d.com/ScriptReference/GlobalObjectId.html),
  [Uninomicon: GlobalObjectId](https://uninomicon.com/globalobjectid),
  [Fluid Unique ID](https://github.com/ashblue/fluid-unique-id),
  [Safekeeper](https://github.com/aarthificial-unity/safekeeper)
- Каталоги и ключи — [Zajceq UnitySaveSystem](https://github.com/Zajceq/UnitySaveSystem),
  [Celezt UnitySaveSystem](https://github.com/Celezt/UnitySaveSystem),
  [AssetMetadata](https://docs.unity3d.com/Manual/AssetMetadata.html)
- Opsive — [save system](https://opsive.com/support/documentation/ultimate-inventory-system/save-system/),
  [переходы между сценами](https://opsive.com/support/documentation/ultimate-inventory-system/scene-transitions/)
- [Xarbrough о форме сейва](https://discussions.unity.com/t/save-system/870022/6),
  [Horneman: презентация о сохранениях](https://www.intelligent-artifice.com/2024/a-presentation-on-save-games/)
- Odin Serializer — [сохранения](https://odininspector.com/tutorials/serialize-anything/making-save-games-with-the-odin-serializer)

**Данные и хранилище**

- BinaryFormatter — [Microsoft](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide);
  Unity и CoreCLR — [обсуждение, июнь 2026](https://discussions.unity.com/t/coreclr-scripting-and-serialization-update-june-2026/1723299)
- JsonUtility — [manual](https://docs.unity.com/en-us/engine/6000.0/manual/programming-environment/code-reload-serialization/script-serialization/json-serialization.md)
- Newtonsoft — [пакет Unity](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/changelog/CHANGELOG.html),
  [TypeNameHandling](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_TypeNameHandling.htm),
  [ObjectCreationHandling](https://www.newtonsoft.com/json/help/html/P_Newtonsoft_Json_JsonSerializerSettings_ObjectCreationHandling.htm),
  [JsonExtensionData](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_JsonExtensionDataAttribute.htm),
  [stripping в Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/managed-code-stripping-preserving.html)
- [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp), [MemoryPack](https://github.com/Cysharp/MemoryPack)
- Версии — [Fadden: форматы файлов](https://fadden.com/tech/file-formats.html),
  [protobuf](https://protobuf.dev/programming-guides/proto3/),
  [версии и миграции в Unity](https://uhiyama-lab.com/en/notes/unity/unity-save-data-versioning-migration/),
  [Xbox XR-003](https://devdocs.xbox.com/publishing/certification/xr/xr-003)
- Запись — [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace),
  [атомарность Replace](https://zenn.dev/arika/articles/20251010-csharp-replace-is-atomic),
  [Dan Luu: согласованность файлов](https://danluu.com/file-consistency/),
  [баг Platform Toolkit](https://discussions.unity.com/t/platformtoolkit-save-data-overwrite-issue-on-editor-and-pc-with-local-saving/1706329)
- Unity — [persistentDataPath](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-persistentDataPath.html),
  [OnApplicationQuit](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnApplicationQuit.html),
  [wantsToQuit](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-wantsToQuit.html),
  [Awaitable](https://docs.unity3d.com/6000.0/Documentation/Manual/async-awaitable-continuations.html),
  [Platform Toolkit: сейвы](https://docs.unity3d.com/Packages/com.unity.platformtoolkit@1.0/manual/savedata/manage-save-files.html)
- Платформы — [Android lifecycle](https://developer.android.com/guide/components/activities/activity-lifecycle),
  [Xbox XGameSave](https://devdocs.xbox.com/build/core-features/common/game-save/xgamesave),
  [Steam Cloud](https://partner.steamgames.com/doc/features/cloud),
  [Steam Cloud кратко](https://www.gamedeveloper.com/game-platforms/quick-guide-to-steam-cloud-saves),
  [UGS Cloud Save](https://docs.unity.com/ugs/manual/cloud-save/manual/concepts/player-data)
- Слоты — [Stardew Valley: сейвы](https://stardewvalley.net/?p=1547),
  [Factorio: автосейвы](https://nodecraft.com/support/games/factorio/factorio-autosaves-and-rolling-back-to-an-autosave)
- Тесты — [System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions)

**Платформеры**

- Hollow Knight — [лавки](https://hollowknight.wiki/w/Bench_(Hollow_Knight)),
  [данные сохранения](https://www.hollowknight.wiki/w/Save_Data_(Hollow_Knight)),
  [ItemChanger: SDBool](https://homothetyhk.github.io/HollowKnight.ItemChanger/api/ItemChanger.SDBool.html),
  [Persistence](https://homothetyhk.github.io/HollowKnight.ItemChanger/api/ItemChanger.Persistence.html),
  [объём данных Silksong](https://github.com/betorzdev/pharloom-calculator/pull/30)
- Celeste — [Settings, SaveData и Session](https://github.com/EverestAPI/Resources/wiki/Settings,-SaveData-and-Session),
  [Session.cs](https://raw.githubusercontent.com/EverestAPI/Everest/dev/Celeste.Mod.mm/Patches/Session.cs)
- Ori — [интервью Mahler](https://stevivor.com/features/interviews/soul-link-ori-will-wisps-thomas-mahler)
- Dead Cells — [обсуждение сохранений](https://steamcommunity.com/app/588650/discussions/0/1741135677345062342)
- Super Meat Boy — [постмортем](https://www.gamedeveloper.com/audio/postmortem-team-meat-s-i-super-meat-boy-i-)
- Blasphemous — [Wikipedia](https://en.wikipedia.org/wiki/Blasphemous_(video_game))
- Shovel Knight — [Yacht Club: чекпоинты](https://www.yachtclubgames.com/blog/check-point-design)
- Shadow Gambit — [разбор системы сохранений](https://www.gamedeveloper.com/programming/deep-dive-creating-and-fine-tuning-the-save-system-of-_shadow-gambit_)
- [Sirlin: сохранения в играх](https://www.gamedeveloper.com/design/saving-the-day-save-systems-in-games),
  [Unity: Echo Weaver](https://unity.com/blog/echo-weaver-time-loop-unity),
  [часы для движущихся платформ](https://spindlebink.itch.io/yollicosmic/devlog/831128/moving-platforms)
- Автосейв — [дизайн автосейва](https://abratabia.com/game-saves/auto-save-design.php),
  [QA автосейва](https://bugnet.io/blog/qa-testing-for-autosave-reliability),
  [Far Cry 6](https://kotaku.com/far-cry-6s-autosave-bug-keeps-killing-players-repeatedl-1847855277)
