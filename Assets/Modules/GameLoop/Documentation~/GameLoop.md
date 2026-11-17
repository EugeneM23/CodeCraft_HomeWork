# GameLoop — игровой цикл и честная пауза

**Статус:** реализовано (24.10.2026), все шаги плана; тесты зелёные — EditMode 516/516, PlayMode 696/696 (batch-режим).
Ветка `feature/pause` (от `main`), worktree `C:\!!!Work\Platformer-Pause`, влита в `main` (24.10.2026, проверена пользователем в редакторе).
Исследование — [Research_Pause.md](Research_Pause.md).

Игра больше не ставит `Time.timeScale = 0`. Весь игровой код тикает от **игрового цикла** (`GameLoop`): цикл получает
`Update`/`FixedUpdate`/`LateUpdate` от Unity и раздаёт их подписчикам. Пауза — цикл перестаёт раздавать тики, а
«морозилки» останавливают то, что Unity двигает сама (физика, анимации, частицы, звук, камера). Снятие паузы
продолжает мир с того же места.

## 1. Решения

| Тема | Решение |
|---|---|
| Что такое пауза | Цикл не раздаёт `OnUpdate`/`OnFixedUpdate`/`OnLateUpdate`, игровое время цикла (`Time`) стоит. `Time.timeScale` не трогаем — он 1 |
| Кто в цикле | Весь геймплей: механики персонажа, ИИ, уровень, эффекты, ввод игрока. UI, загрузка сцен, сохранения, отладка — на обычном `Update` и `unscaledDeltaTime`, им пауза не нужна |
| Подписка | Интерфейсы по фазам: `IUpdateListener`, `IFixedUpdateListener`, `ILateUpdateListener`, `IPauseListener` — объект реализует только нужные. `Add(listener)` / `Remove(listener)`; повторный `Add` ничего не делает; объект без интерфейсов — исключение |
| Где подписываться | `MonoBehaviour` — в `OnEnable` / `OnDisable` (`this.JoinGameLoop()` / `this.LeaveGameLoop()`), как в примере update manager из руководства Unity. Обычный C# — `loop.Add` у владельца |
| Порядок | `[DefaultExecutionOrder]` класса слушателя, как у Unity (без атрибута — 0); при равенстве — порядок добавления |
| Изменения во время обхода | Добавленный во время прохода тикает со следующего прохода; удалённый перестаёт тикать сразу |
| Выключенный и уничтоженный | `Behaviour` с `isActiveAndEnabled == false` не тикает; уничтоженный Unity-объект выбрасывается из цикла |
| Исключения | Ловятся на каждом слушателе, `Debug.LogException` — остальные тикают дальше, как в Unity |
| Держатели паузы | `Pause(source)` / `Resume(source)`: каждый держатель ставит и снимает свою паузу; игра стоит, пока есть хоть один. Повторный `Pause` того же держателя ничего не делает. Уничтоженный Unity-объект-держатель отпускает паузу сам (проверка раз в кадр) |
| Слушатели паузы | `IPauseListener.OnPause()` / `OnResume()` — при переходах. Добавленный в уже стоящую игру сразу получает `OnPause` — включённый в паузе контроллер не оживёт до снятия |
| Когда пауза вступает | Тики проверяют паузу в начале прохода: пауза, поставленная во время прохода, остановит следующий |
| Физика | Unity шагает физику как раньше (`FixedUpdate`-режим, интерполяция работает). На паузе морозилка переводит `Physics2D.simulationMode` в `Script` — мир стоит целиком, контакты и скорости целы; на снятии и при уничтожении цикла — прежний режим |
| Что морозит модуль | Физика 2D (и 3D), `Animator` и legacy `Animation` (скорость 0), `ParticleSystem` (`Pause`/`Play`), по выбору — `AudioListener.pause`. Включаются галками на `GameLoopRunner`. Кто на unscaled-времени (`AnimatorUpdateMode.UnscaledTime`, `useUnscaledTime`) — это меню, его не трогаем |
| Что морозит игра | Звук AudioEngine (`IAudioSystem.Pause`), `SpriteAnimator` (Normal → Manual на время паузы), Cinemachine (демпфирование — `UniformDeltaTimeOverride = 0`, тряска и шум — `CurrentTimeOverride` = игровое время) |
| Время игры и автосейв | `SaveLoadSystem` тикает от цикла (`TickManually`): пауза останавливает счёт времени игры и таймер автосейва |
| Загрузка сцены | `SceneLoader` держит паузу всё время перехода (затемнение, загрузка, проявление) — мир стоит, пока экран не виден; меню паузы отпускает свою, когда уничтожается со сценой. Старое правило «загрузка снимает паузу» больше не нужно |
| Сервис | `IGameLoop` в `ServiceLocator`, регистрирует `GameServices`; сам цикл и морозилки движка — компонент `GameLoopRunner` на `[Services]`. `IGamePause`/`GamePause` удалены |
| Сцены без `[Services]` | Игровые объекты не тикают (одно предупреждение в консоль). `[Services]` и `GameUI` добавлены в `Sandbox_Locomotion` и `Sandbox_MotionStretch`; их `[Services]` пишет в свой слот (Locomotion — 8, MotionStretch — 7) и при старте ничего не грузит — песочница не трогает игровой слот 0. `Sandbox_MotionStretch` добавлена в Build Settings — без этого «Restart» в меню паузы не загрузит сцену |
| Тестовый стенд | Свой `GameLoop` в `ServiceLocator`; шаг = `Loop.FixedUpdate` + `Physics2D.Simulate`, с `InvokeUpdate` — ещё `Update`/`LateUpdate` и `SpriteAnimator`. На паузе стенд не шагает физику и анимацию — как морозилки. Рефлексии больше нет. UI-тесты ведут `Update`/`LateUpdate` цикла стенда на реальных кадрах (`LoopFrameDriver`) |
| Чего нет | Локальных часов и замедления отдельных объектов, хит-стопа, своего `timeScale` у цикла — добавятся, когда понадобятся (раздел 7) |

## 2. Модуль `Modules/GameLoop`

Namespace `Ticking`, сборки `GameLoop` (рантайм, без ссылок), `GameLoop.Editor` (инспектор), `GameLoop.Tests`
(EditMode), `GameLoop.Tests.Runtime` (PlayMode). Модуль ничего не знает об игре.

```
Modules/GameLoop/
├── README.md
├── Runtime/   GameLoop.asmdef
│   ├── IGameLoop.cs              — сервис: подписка, пауза с держателями, игровое время
│   ├── GameLoop.cs               — цикл: списки слушателей, держатели паузы, Update/FixedUpdate/LateUpdate
│   ├── ListenerList.cs           — упорядоченный список, безопасный к изменениям во время обхода (internal)
│   ├── Listeners.cs              — IUpdateListener, IFixedUpdateListener, ILateUpdateListener, IPauseListener
│   ├── GameLoopRunner.cs         — MonoBehaviour: Unity → цикл, морозилки движка галками
│   └── Freezers/                 — Physics2DFreezer, PhysicsFreezer, AnimatorFreezer, LegacyAnimationFreezer,
│                                   ParticleSystemFreezer, AudioListenerFreezer
├── Editor/    GameLoop.Editor.asmdef — инспектор GameLoopRunner: состояние, держатели, счётчики, кнопки
└── Tests/
    ├── Editor/   GameLoop.Tests.asmdef          — ядро на фейковых слушателях
    └── Runtime/  GameLoop.Tests.Runtime.asmdef  — раннер на реальных кадрах, морозилки
```

### 2.1. Контракт

```csharp
public interface IUpdateListener      { void OnUpdate(float deltaTime); }       // каждый кадр
public interface IFixedUpdateListener { void OnFixedUpdate(float deltaTime); }  // фиксированный шаг, до физики
public interface ILateUpdateListener  { void OnLateUpdate(float deltaTime); }   // каждый кадр, после всех OnUpdate
public interface IPauseListener       { void OnPause(); void OnResume(); }

public interface IGameLoop
{
    bool IsPaused { get; }
    float Time { get; }                         // игровые секунды: идут только без паузы
    IReadOnlyList<object> PauseSources { get; } // кто держит паузу (для отладки)
    event Action<bool> PauseChanged;            // true — встала, false — пошла

    void Add(object listener);
    void Remove(object listener);
    bool Contains(object listener);

    void Pause(object source);
    void Resume(object source);
    bool IsPausedBy(object source);
}
```

`GameLoop` (обычный C#) сверх контракта — то, что зовёт владелец: `Update(dt)`, `FixedUpdate(dt)`, `LateUpdate(dt)`,
`ResumeAll()` и счётчики слушателей для инспектора. Тест шагает цикл сам, игра — через `GameLoopRunner`.

В профайлере каждый слушатель — свой образец с именем своего класса (`SplatterLoopComponent`, `CharacterLoopComponent`…)
под фазой раннера: без этого весь игровой код — один кусок `GameLoopRunner.Update`, и не видно, кто дорогой. Образцы
есть только при `ENABLE_PROFILER` (эдитор, development-сборка), в релизе вызов слушателя голый (27.10.2026).

### 2.2. `GameLoopRunner`

Компонент на постоянном объекте (у нас `[Services]`). Создаёт цикл (`Loop` — при первом обращении), из своих
`FixedUpdate`/`Update`/`LateUpdate` зовёт `Loop.FixedUpdate(Time.fixedDeltaTime)` и т. д., добавляет выбранные в
инспекторе морозилки движка. При уничтожении снимает все паузы — морозилки возвращают движок (режим физики не
останется в Project Settings). Кто зарегистрирует `Loop` как сервис — решает игра.

### 2.3. Морозилки

Морозилка — `IPauseListener`: на `OnPause` останавливает подсистему и запоминает, что тронула; на `OnResume`
возвращает ровно это. Сканирующие (Animator, Animation, частицы, `SpriteAnimator`) — наследники `ComponentFreezer<T>`
с двумя методами `Freeze`/`Thaw`: ищут объекты загруженных сцен (`FindObjectsByType`) на паузу, а сцену, загруженную
во время паузы (смена сцены за затемнением, аддитивная загрузка), морозят на `sceneLoaded` — до первого кадра её
объектов. Морозилки физики в редакторе запоминают прежний режим и в `SessionState`: если Play Mode кончится без их
`OnResume` (перекомпиляция в Play Mode теряет цикл), режим вернётся на выходе в Edit Mode. Морозилки модуля под
`versionDefines` встроенных модулей Unity (`physics2d`, `physics`, `animation`, `particlesystem`, `audio`).

## 3. Игра

- **`[Services]`**: компонент `GameLoopRunner`; `GameServices` регистрирует `IGameLoop`, добавляет игровые
  морозилки (`AudioEngineFreezer`, `SpriteAnimatorFreezer`, `CinemachineFreezer`) и тикает `SaveLoadSystem` от цикла.
- **`GameUI`**: держит паузу, пока стек экранов не пуст (`Pause(this)` / `Resume(this)`), отпускает в `OnDestroy`.
- **`PlayerInputController`**: `IUpdateListener` + `IPauseListener` (на паузе карта `Player` выключена и ввод сброшен).
- **`SceneLoader`**: держит паузу на время перехода.
- **Компоненты** (`Character`, уровень, эффекты, ИИ): `FixedUpdate` → `OnFixedUpdate(dt)`, `Update` → `OnUpdate(dt)`,
  `LateUpdate` → `OnLateUpdate(dt)`; подписка в `OnEnable`/`OnDisable`. Публичные `Tick(dt)` остаются (тесты).
- **`PatrolMover`** (пила, старая платформа): вместо DOTween и корутины — движение и ожидание от цикла.
- **Сцены**: `[Services]` (свой слот: 8 и 7, без загрузки на старте) и `GameUI` — в `Sandbox_Locomotion` и
  `Sandbox_MotionStretch`; `Sandbox_MotionStretch` — в Build Settings (для «Restart»).

## 4. Правила для нового кода

1. Игровой код **не пишет** `Update`/`FixedUpdate`/`LateUpdate`, не читает `Time.deltaTime`/`Time.time`, не запускает
   корутины по времени, `Invoke` и твины: реализует `IUpdateListener`/`IFixedUpdateListener`/`ILateUpdateListener`,
   время — из параметра `dt`, таймеры — полями.
2. Подписка — в `OnEnable` (`this.JoinGameLoop()`), отписка — в `OnDisable` (`this.LeaveGameLoop()`).
3. Нужен порядок — `[DefaultExecutionOrder]` на классе, как раньше.
4. Что-то должно стоять на паузе, но тикает само (новый тип анимации, видео, шейдер со временем, сторонний пак) —
   морозилка `IPauseListener` в `GameServices`.
5. Что-то должно работать в паузе — это UI: обычный `Update` и `unscaledDeltaTime`; Animator/частицы меню — на
   unscaled-времени, морозилки их не трогают.
6. Поставить игру на паузу — `ServiceLocator.Get<IGameLoop>().Pause(this)`, снять — `Resume(this)`.
   `Time.timeScale` не трогать.

## 5. Тесты

- **Модуль, EditMode** (`GameLoop.Tests`): фазы и `dt`, порядок по атрибуту и добавлению, повторный `Add`,
  добавление/удаление во время прохода, исключение не рвёт проход, держатели паузы, `PauseChanged`, `OnPause` при
  добавлении в паузу, уничтоженный держатель, выключенный `Behaviour`, игровое время.
- **Модуль, PlayMode** (`GameLoop.Tests.Runtime`): раннер на реальных кадрах, падающее тело стоит в паузе и
  продолжает с той же скоростью, режим физики возвращается, Animator/Animation/частицы стоят, unscaled — идут.
- **Игра, PlayMode**: честная пауза на стенде (игрок, рывок, платформа, враг — ни шага в паузе, всё продолжается
  после), ввод в паузе, `GameUI` держит паузу, `GameServices` регистрирует цикл и морозилки, звук, `SpriteAnimator`,
  Cinemachine, время игры не идёт, загрузка держит паузу, `PatrolMover` (пила) стоит в паузе.
- **Сквозной** (`SandboxPauseTests`): настоящая сцена `Sandbox_Locomotion` на реальных кадрах — падающий игрок
  встаёт в воздухе с той же скоростью и падает дальше после паузы, меню паузы держит паузу.

## Как проверить руками

`Sandbox_Locomotion` (или `Sandbox_Enemies`, `Game.unity`), Play: разбежаться, прыгнуть, сделать рывок под тряску
камеры — Esc. Мир стоит целиком: игрок висит в воздухе, пила и враги не двигаются, анимация и тряска замерли, звук на
паузе, меню анимируется. Esc ещё раз — всё продолжается с того же места. В инспекторе `[Services]` (компонент
`GameLoopRunner`) видно, кто держит паузу, игровое время и слушателей; кнопка «Pause (Inspector)» ставит свою паузу,
чтобы рассмотреть кадр без меню.

## Миграция кода

`Update`/`FixedUpdate`/`LateUpdate` → `void IUpdateListener.OnUpdate(float deltaTime)` / `IFixedUpdateListener` /
`ILateUpdateListener` + `this.JoinGameLoop()` в `OnEnable`, `this.LeaveGameLoop()` в `OnDisable`; `Time.deltaTime`
и `Time.fixedDeltaTime` в теле → `deltaTime`. `IGamePause` → `IGameLoop` (`Pause()` → `Pause(this)`,
`OnPauseChanged` → `PauseChanged` или `IPauseListener`). Тест: `Rig.Loop.Pause(this)` вместо `GamePause`.

## 6. План

1. Модуль: ядро `GameLoop` по TDD (EditMode).
2. Модуль: `GameLoopRunner` и морозилки движка (PlayMode), инспектор.
3. Игра: сервис в `[Services]`, игровые морозилки, `SaveLoadSystem.TickManually`, `GameUI`, ввод, загрузка;
   `GamePause` удалён.
4. Игра: все компоненты в цикл, `PatrolMover` без DOTween; стенд тестов на цикле.
5. Сцены: `[Services]` + `GameUI` в песочницах локомоушена.
6. Полный прогон тестов, документация (README модуля, этот файл, `CLAUDE.md`).

## 7. Известные ограничения

- **Сохранение во время паузы** (потеря фокуса, выход из меню паузы) пишет то, что видит: у `Animator` — `speed = 0`,
  у частиц — `Paused`, которые поставила морозилка; после загрузки они так и останутся стоять. Сейчас это никого не
  задевает: у `Saveable` игрока, врагов и ящика нет ни `Animator`, ни частиц, а `SpriteAnimator` морозится сменой
  режима, который не сохраняется. Появится `Saveable` с `Animator` — перед сохранением на паузе надо оттаивать
  морозилки (событие «перед сохранением» в `SaveSystem`) или не сохранять `speed`.
- **Перекомпиляция в Play Mode** теряет цикл и `ServiceLocator` (статика): до перезапуска Play ничего не тикает.
  Режим физики после этого всё равно вернётся (страховка морозилок в редакторе).
- **Вспомогательные методы механик** ещё читают `Time.fixedDeltaTime` (мотор, бег, рывок, лестница, толчок, склон):
  сейчас это тот же шаг, что даёт цикл. Для замедления через цикл их надо перевести на `deltaTime` из параметра.
- **Частицы, `playOnAwake` в сцене, загруженной на паузе**, морозятся, только если к `sceneLoaded` уже играют.

## 8. Дальше (не сделано)

- **Хит-стоп**: держатель паузы на 50–100 мс с таймером на реальном времени (`Pause(this)`, через `unscaledDeltaTime`
  — `Resume(this)`). Ядро готово, нужен только компонент.
- **Замедление**: `TimeScale` у цикла (множитель `dt`) — с физикой в `Script`-режиме, иначе она не замедлится.
- **Локальные часы** (заморозить только врагов): дочерний цикл как слушатель родительского.
