# UI — меню, пауза и экраны

**Статус:** часть 1 реализована (ветка `feature/ui`); часть 2 (главное меню и сцены) — реализована вместе с точкой
входа ([Bootstrap.md](Bootstrap.md), ветка `feature/bootstrap`); части 3–5 — наброски. Пауза через `IGamePause` и
`Time.timeScale` из части 1 заменена игровым циклом с честной паузой — [GameLoop.md](GameLoop.md); ниже она описана
как была. Исследование (UI-системы, устройство меню, ввод, пауза, пиксель-арт, тесты) — [Research_UI.md](Research_UI.md).
Пошаговый план части 1 — [UI_Plan.md](UI_Plan.md).

**Что поменяла часть 2:** первой в сборке стоит не меню, а `Boot.unity` — меню грузит ход игры `IGameFlow`.
`MainMenu.unity` — камера и `[MainMenuUI]` (`MainMenuUI` + `MainMenuScreen`: «Continue» только при сохранении,
«New Game» при сохранении спрашивает, «Quit» спрашивает). `[GameUI]` из сцен ушёл: он — слой уровня
(`AppConfig.LevelLayer`), ход игры ставит его в каждую сцену уровня. В паузе — «Main Menu» между «Restart» и «Quit»
(до «Quit» три шага вниз); «Restart» и «Main Menu» зовут `IGameFlow`. Общий вопрос «да/нет» — `ConfirmPrompt`.
У `UIScreen` — свойство `FirstSelected` (экран меню открывается на «New Game», когда «Continue» спрятан).

Архитектура интерфейса игры: меню, пауза и диалоги, дальше — главное меню, настройки, «сок» и HUD. UI — на uGUI.
Навигация — свой модуль `ScreenStack` (стек экранов). Игровая часть — экраны-префабы и оркестратор корня UI.
Глобальное (пауза, загрузка сцен, настройки) — сервисы в `[Services]`.

## 1. Цель и части

Работа идёт пятью частями. Каждая заканчивается зелёными тестами и рабочей игрой.

| Часть | Что входит |
|---|---|
| 1. Ядро и пауза | Модуль `ScreenStack`; карта `UI` в `PlayerControls`; `EventSystem` в `[Services]`; сервис `IGamePause`; `GameUI` с `PauseScreen` и `ConfirmDialog` |
| 2. Главное меню и сцены | Сервис `ISceneLoader` с затемнением; `MainMenu.unity` первой в Build Settings; `MainMenuUI`; «Новая игра», «Продолжить» (с SaveSystem), «Выйти в меню» |
| 3. Настройки | Сервис `IGameSettings` (громкость, экран) со своим файлом; `SettingsScreen` |
| 4. Сок | Пресеты переходов, отклик кнопок, частицы в UI, `SpriteAnimator` для `Image`, материалы `GUI SSU`, выбор масштаба (пиксельный или HD), шрифты |
| 5. HUD | После этапа урона, в слое `Hud` |

**Часть 1 готова, когда:**

- в `Sandbox_Enemies` и `Game.unity` Esc или Start открывает паузу (в `Sandbox_Locomotion` нет `[Services]` — меню
  туда ставим вместе с ними, когда закоммитят текущие правки сцены);
- на паузе стоит всё игровое время: игрок, враги, платформы, анимации на игровом времени, игровые звуки; меню при этом
  анимируется;
- меню работает с клавиатуры, мыши и геймпада; наведение мыши и выделение с клавиатуры или геймпада — одно состояние;
- Esc или B — шаг назад, Start — закрыть всё и вернуться в игру;
- «Resume» по A, Enter или Space возвращает в игру, и игрок не прыгает от той же кнопки;
- «Quit» спрашивает подтверждение: «Yes» — `Application.Quit()` (в эдиторе Unity его игнорирует), «No», Esc или B —
  назад, и выделена снова «Quit»;
- потеря фокуса окна ставит паузу (выключается галочкой на `GameUI`);
- все тесты зелёные.

## 2. Решения

| Тема | Решение |
|---|---|
| UI-система | uGUI (Canvas, `CanvasGroup`, TMP). UI Toolkit — вне рамок, контракт `IScreen` его не исключает |
| Навигация | Свой модуль `ScreenStack`: стек экранов, контракт `IScreen` не зависит от UI-системы; готовые библиотеки не берём |
| Экран | Префаб с компонентом-наследником `UIScreen`; кнопки — `[SerializeField]`; экран поднимает события и не знает ни стек, ни сервисы, ни другие экраны |
| Корень UI | `GameUI` в игровых сценах, `MainMenuUI` в главном меню (часть 2) — оркестратор: стек, ввод «назад» и «пауза», связь событий экранов со стеком и сервисами. Не игровая сущность — без `Controllers`/`Components`, как `[Services]` |
| Пауза | Сервис `IGamePause` в `[Services]`. Игра на паузе тогда и только тогда, когда стек `GameUI` не пуст. **Позже заменено** игровым циклом: `GameUI` держит паузу `IGameLoop`, см. [GameLoop.md](GameLoop.md) |
| Ввод | Карта `UI` в `PlayerControls` (с действием `Pause`); один `EventSystem` с `InputSystemUIInputModule` в `[Services]` |
| Время | Весь UI — на unscaled time. `Time.timeScale` больше не трогаем: пауза честная, через игровой цикл ([GameLoop.md](GameLoop.md)) |
| Анимации | Точка расширения `ScreenTransition`; в модуле — `FadeTransition`; DOTween и Animator — в игре (часть 4) |
| Масштаб | До части 4 — `CanvasScaler` Scale With Screen Size 1920×1080, match 0,5. Пиксельный (`IntegerCanvasScaler`) или HD — на первом макете |
| Данные в UI | Явно: оркестратор зовёт методы экрана, экран шлёт события. Data binding нет |
| Тексты части 1 | Английские заглушки шрифтом TMP по умолчанию; язык, шрифты и локализация — позже |
| Ветка | `feature/ui` от вершины `feature/damage`, когда этап урона закоммичен |

## 3. Устройство

```
UI-карта ввода ──► GameUI (оркестратор) ──► ScreenStack ──► PauseScreen, ConfirmDialog (UIScreen)
                      ▲   события экранов: OnResume, OnQuit, OnYes, OnNo, OnClosed
                      │
                      ▼   стек не пуст ⇔ пауза
                 IGamePause ([Services]) ──► Time.timeScale, IAudioSystem.Pause/Resume
                      │
                      └── OnPauseChanged ──► PlayerInputController (карта Player выключена)
```

- Модуль `ScreenStack` — навигация, ничего не знает об игре.
- `Scripts/UI` — экраны игры и оркестраторы корня UI.
- `Scripts/GameSystem/Services` — `GamePause` (дальше `SceneLoader`, `GameSettings`), регистрирует их `GameServices`.

## 4. Модуль `ScreenStack`

### 4.1. Состав

```
Assets/Game/Scripts/Modules/ScreenStack/
├── Runtime/          сборка ScreenStack (namespace ScreenNavigation), ссылается только на UnityEngine.UI
│   ├── ScreenStack.asmdef
│   ├── IScreen.cs
│   ├── ScreenStack.cs
│   ├── UIScreen.cs
│   ├── ScreenTransition.cs
│   ├── FadeTransition.cs
│   └── SelectOnPointerEnter.cs
├── Tests/Editor/     сборка ScreenStack.Tests (EditMode)
├── Tests/Runtime/    сборка ScreenStack.Tests.Runtime (PlayMode)
├── README.md
└── package.json      com.eugenem23.screen-stack
```

Как остальные модули: от `Game` не зависит, тесты внутри, `GameTestRunner` подхватывает их сам. Ввод модуль не
читает — «назад» ему передаёт оркестратор. `IntegerCanvasScaler` добавляется в модуль в части 4.

### 4.2. `IScreen`

```csharp
namespace ScreenNavigation
{
    public interface IScreen
    {
        bool IsOverlay { get; }   // screens below stay visible (dialog over settings)
        bool CanGoBack { get; }   // false: Back() keeps the screen (main menu root)

        void Open();              // entered the stack
        void Close();             // left the stack
        void Show();              // became visible
        void Hide();              // became invisible
        void Focus();             // got input
        void Blur();              // lost input
    }
}
```

Три пары, у каждой одно значение: `Open`/`Close` — вход и выход из стека, `Show`/`Hide` — видимость, `Focus`/`Blur` —
ввод. Экран, перекрытый непрозрачным, скрыт, но остаётся в стеке: так он отличает «перекрыли» от «закрыли», помнит
выделенную кнопку до возврата и забывает её при `Close`.

### 4.3. `ScreenStack`

```csharp
namespace ScreenNavigation
{
    public sealed class ScreenStack
    {
        public event Action Changed;          // after every operation that changed the stack

        public IScreen Top { get; }           // null when empty
        public int Count { get; }
        public bool IsEmpty { get; }
        public bool Contains(IScreen screen);

        public void Push(IScreen screen);
        public bool Pop();                    // false when empty
        public bool Back();                   // false when empty or Top.CanGoBack == false; otherwise Pop()
        public void Replace(IScreen screen);  // swaps the top; on an empty stack — Push
        public void Clear();
    }
}
```

**Видимость.** Экран виден, если он верхний или все экраны над ним — оверлеи. После каждой операции стек пересчитывает
видимые и зовёт `Show`/`Hide` только тем, у кого видимость поменялась. Ввод (`Focus`) — только у верхнего.

**Порядок вызовов внутри операции:** `Blur` старого верхнего → `Open` вошедшего → `Hide` (сверху вниз) → `Close`
вышедших → `Show` (снизу вверх) → `Focus` нового верхнего → `Changed`.

- `Push(s)`: s входит наверх.
- `Pop()`: верхний выходит; открывшийся под ним получает фокус обратно.
- `Replace(s)`: верхний выходит, s входит — за одну операцию, поэтому экран под ними не мигает (если s не оверлей,
  нижний экран не показывается вовсе).
- `Clear()`: выходят все сверху вниз; на пустом стеке ничего не делает и `Changed` не шлёт.
- `Back()` на корневом экране (`CanGoBack == false`) ничего не делает и возвращает `false`.

**Ошибки — исключения, а не тихие пропуски:** `Push`/`Replace` с `null` — `ArgumentNullException`; экран уже
в стеке — `InvalidOperationException`; операция стека из `Open`/`Close`/`Show`/`Hide`/`Focus`/`Blur` во время другой
операции — `InvalidOperationException`. Из обработчика `Changed` операции можно: он вызывается после завершения.

### 4.4. `UIScreen`

`UIScreen : MonoBehaviour, IScreen` — реализация на uGUI. На объекте экрана — `CanvasGroup` (`[RequireComponent]`):
экран берёт его сам при первом обращении, поэтому работает и до `Awake`. Экран, собранный в коде (тесты), настраивается
`Configure(...)` — как `MovingPlatform.Configure` и другие объекты стенда.

```csharp
[Header("Stack")]
[SerializeField] private bool _isOverlay;
[SerializeField] private bool _canGoBack = true;

[Header("Focus")]
[SerializeField] private Selectable _firstSelected;     // optional: a mouse-only screen has none

[Header("View")]
[SerializeField] private ScreenTransition _transition;   // optional: without it the screen appears at once

public event Action OnOpened;
public event Action OnClosed;

public bool IsShown { get; }          // between Show and Hide
public bool IsInteractable { get; }   // focused and fully shown
```

- **В префабе экран выключен.** Показывает его только стек. `Open` и `Show` работают и до первого `Awake`.
- **`Show`:** включает объект, ставит его последним среди соседей (рисуется поверх), играет появление. Кнопки
  нажимаются только после конца появления.
- **`Hide`:** сразу снимает интерактивность и блокировку лучей, снимает выделение, если оно внутри экрана (иначе
  Submit в игре ушёл бы в выключенную кнопку), играет исчезновение, в конце выключает объект. `Show` во время
  исчезновения прерывает его и играет появление с текущего состояния.
- **`Focus`:** экран становится интерактивным (когда появление закончено) и выделяет запомненную кнопку, а если её
  нет или она выключена — `_firstSelected`.
- **`Blur`:** запоминает выделенную кнопку, если она внутри экрана, и снимает интерактивность. Экран под оверлеем
  виден, но не интерактивен — навигация геймпада в него не попадает.
- **`Open`/`Close`:** сбрасывают запомненную кнопку и шлют `OnOpened`/`OnClosed`. Свежая пауза открывается с первой
  кнопки; после закрытия диалога выделена кнопка, которая его открыла.
- **Выделение не теряется.** Пока экран в фокусе и выделения нет (клик мышью по пустому месту, выделенная кнопка
  выключилась), он возвращает его в `Update`.
- **Время** — unscaled, пауза меню не замораживает.
- **Наследникам:** Unity-колбэки базы (`Awake`, `Update`) — `protected virtual`, наследник зовёт `base`. Свои кнопки
  наследник подписывает в `OnEnable`/`OnDisable` именованными методами.

Экраны игры наследуют `UIScreen` — Template Method: общая логика (видимость, фокус, переходы) — больше 80% кода,
отношение «является».

### 4.5. `ScreenTransition` и `FadeTransition`

```csharp
public abstract class ScreenTransition : MonoBehaviour
{
    // Plays showing (true) or hiding (false) from the current state and calls done at the end.
    // A new Play interrupts the running one; the interrupted done is never called.
    public abstract void Play(bool show, Action done);
}
```

`FadeTransition` — альфа `CanvasGroup` за `_duration` секунд (по умолчанию 0,15) на unscaled time, без DOTween.
`_duration = 0` — мгновенно. Выключенный экран становится прозрачным, поэтому каждое появление — с нуля. `Tick(dt)` —
шаг вручную: `Update` зовёт его с unscaled time, тесты — со своим шагом; `Configure(duration)` — для кода. Пресеты
на DOTween и Animator — в игре, часть 4.

### 4.6. `SelectOnPointerEnter`

Вешается на кнопку: наведение мыши выделяет её через `EventSystem`, если она интерактивна. Мышь и геймпад работают
с одним выделением, у кнопки одно визуальное состояние.

## 5. Игровая сторона (часть 1)

### 5.1. Файлы

```
Assets/Game/Scripts/UI/        — домен UI: GameUI.cs (оркестратор), PauseScreen.cs, ConfirmDialog.cs
Assets/Game/Scripts/GameSystem/Services/  — IGamePause.cs, GamePause.cs (+ правка GameServices.cs)
Assets/Game/Prefabs/UI/        — GameUI.prefab, PauseScreen.prefab, ConfirmDialog.prefab
```

Подпапку `Screens/` заводим, когда экранов станет больше трёх (часть 2). `Game.asmdef` получает ссылки на
`ScreenStack`, `UnityEngine.UI` и `Unity.TextMeshPro`.

### 5.2. `GameUI` — корень UI игровых сцен

```
GameUI           GameUI, Canvas (Screen Space – Overlay, Sort Order 20), CanvasScaler, GraphicRaycaster
└── Screens      RectTransform на весь экран
    ├── PauseScreen      вложенный префаб, выключен
    └── ConfirmDialog    вложенный префаб, выключен
```

HUD этапа урона живёт на префабе игрока (`HUD`, Canvas с Sort Order 10) — `GameUI` рисуется поверх него. Пустой слой
`Hud` в `GameUI` не заводим, судьба HUD — часть 5. В сценах префаб стоит объектом `[GameUI]` в `Game.unity`
и `Sandbox_Enemies` (в обеих есть `[Services]`). В `Sandbox_Locomotion` `[Services]` нет, а без сервиса паузы
`GameUI` не работает — меню туда добавим вместе с `[Services]`.

```csharp
[Header("Input")]
[SerializeField] private InputActionReference _pauseAction;    // UI/Pause: Esc, Start
[SerializeField] private InputActionReference _cancelAction;   // UI/Cancel: Esc, B
[SerializeField] private bool _pauseOnFocusLoss = true;

[Header("Screens")]
[SerializeField] private PauseScreen _pauseScreen;
[SerializeField] private ConfirmDialog _confirmDialog;

[Header("Texts")]
[SerializeField] private string _quitQuestion = "Quit the game?";

private readonly ScreenStack _stack = new();
private IGamePause _pause;
private Action _onConfirm;
```

**Ввод** (в `Update`, за кадр — не больше одного шага):

| Стек | Нажато | Действие |
|---|---|---|
| пуст | `Pause` | `Push(_pauseScreen)` |
| не пуст | `Cancel` | `Back()` |
| не пуст | `Pause` без `Cancel` (Start) | `Clear()` |

Esc попадает в оба действия: при пустом стеке открывает паузу, при открытом меню — шаг назад.

**События экранов:**

- `PauseScreen.OnResume` — `Clear()`;
- `PauseScreen.OnQuit` — `Ask(_quitQuestion, Application.Quit)`;
- `Ask(text, onYes)` — запоминает `onYes`, `ConfirmDialog.SetText(text)`, `Push(_confirmDialog)`;
- `ConfirmDialog.OnYes` — `Pop()`, затем запомненное действие; `OnNo` — `Pop()`;
- `ConfirmDialog.OnClosed` — забыть запомненное действие (диалог могли закрыть и через «назад»).

**Пауза.** `_stack.Changed`: стек пуст — `_pause.Resume()`, иначе — `_pause.Pause()`. Отдельного флага паузы
у `GameUI` нет.

**Фокус окна.** `OnApplicationFocus(false)` при пустом стеке и `_pauseOnFocusLoss` — `Push(_pauseScreen)`.

**Жизненный цикл.**

- `Awake`: неназначенные ссылки — `ArgumentNullException` с именем поля.
- `OnEnable`: `_pause = ServiceLocator.Get<IGamePause>()` — без сервиса паузы меню не работает, это баг сцены
  (`[Services]` просыпается раньше всех, тесты регистрируют сервис до создания `GameUI`); подписки на события экранов
  и стека; включает своё действие `UI/Pause`. `UI/Cancel` включает и выключает модуль ввода, `GameUI` его только читает.
- `OnDisable`: отписки, выключает `UI/Pause`.
- `OnDestroy` с непустым стеком — `_pause.Resume()`.

### 5.3. Экраны

| Экран | Оверлей | «Назад» | Кнопки | События и методы |
|---|---|---|---|---|
| `PauseScreen` | нет | да | «Resume» (`_firstSelected`), «Quit» | `OnResume`, `OnQuit` |
| `ConfirmDialog` | да | да (= «Нет») | «Yes», «No» (`_firstSelected` — безопасный «No») | `SetText(string)`, `OnYes`, `OnNo` |

Фон `PauseScreen` — полупрозрачное затемнение, игра видна за ним (это картинка, а не флаг оверлея: под паузой в стеке
ничего нет). На кнопках — `SelectOnPointerEnter`, у экранов — `FadeTransition`.

### 5.4. Сервис `IGamePause`

```csharp
public interface IGamePause
{
    event Action<bool> OnPauseChanged;   // true — paused
    bool IsPaused { get; }
    void Pause();
    void Resume();
}
```

`GamePause : IGamePause, IDisposable` — обычный C#-класс:

- `Pause()`: `Time.timeScale = 0`, `IAudioSystem.Pause()`, `OnPauseChanged(true)`. На паузе — ничего.
- `Resume()`: `Time.timeScale = 1`, `IAudioSystem.Resume()`, `OnPauseChanged(false)`. Без паузы — ничего.
- Загрузка любой сцены (`SceneManager.sceneLoaded`) снимает паузу — `timeScale` не залипает на 0.
- `Dispose()`: снимает паузу и отписывается от `sceneLoaded`.
- Звук — через конструктор, `null` допустим (тесты без звука).

`GameServices` создаёт его в `Awake` (`new GamePause(_audioSystem)`) и регистрирует как `IGamePause`, в `OnDestroy` —
снимает с регистрации и зовёт `Dispose`. `IAudioSystem.Pause()` выключает `AudioSystem` и ставит на паузу играющие
события. Ручная проверка части 1 замечаний к паузе не дала (игровые звуки встают); что именно происходит с музыкой
и новыми событиями на паузе, отдельно не разбирали — это вход для звуков UI в части 4 (UI-звуки должны играть на паузе).

### 5.5. Ввод

**Карта `UI` в `PlayerControls`:**

| Действие | Тип | Клавиатура и мышь | Геймпад |
|---|---|---|---|
| Navigate | Vector2 | WASD, стрелки | левый стик, D-pad |
| Submit | Button | Enter, Space | South (A) |
| Cancel | Button | Esc | East (B) |
| Point | Vector2 (pass-through) | позиция мыши | — |
| Click | Button (pass-through) | ЛКМ | — |
| ScrollWheel | Vector2 | колесо | — |
| Pause | Button | Esc | Start |

**`EventSystem` в `[Services]`:** дочерний объект префаба `Services` с `EventSystem` и `InputSystemUIInputModule`
(действия — `PlayerControls`, карта `UI`; `Deselect On Background Click` выключен). Живёт вместе с `[Services]`
в `DontDestroyOnLoad`; у копии `[Services]` в следующей сцене не просыпается — её выключает `GameServices`.
`EventSystem` из `Game.unity` удаляется.

**`PlayerInputController`:**

- в `OnEnable` берёт `IGamePause` через `TryGet` и подписывается на `OnPauseChanged`, в `OnDisable` отписывается —
  сервис необязателен, тесты без `[Services]` работают как раньше;
- `OnPauseChanged(true)` — выключает карту `Player` и сбрасывает ввод (как в `OnDisable`); `false` — включает карту
  (подписка есть только у включённого контроллера); `OnEnable` на паузе карту не включает;
- нажатие, пришедшее при выключенной карте, Input System не засчитывает, поэтому «Resume» по A не вызывает прыжок —
  это проверяет тест.

Враги, платформы, Cinemachine, `SpriteAnimator` на игровом времени и твины DOTween по умолчанию должны вставать от
`timeScale = 0` сами — проверяем в песочницах.

### 5.6. Правила связей

- Экран → оркестратор: события C# (`On…`), подписка в `OnEnable`, отписка в `OnDisable`, именованными методами.
- Оркестратор → экран: методы (`SetText`, дальше — `SetValues`).
- Оркестратор → игра: сервисы (`IGamePause`; дальше — `ISceneLoader`, `IGameSettings`, `ISaveLoadService`).
- Игра → UI: события сервисов и сущностей, на которые подписан оркестратор. Объект уровня, которому нужно открыть
  экран (магазин, скамейка), ссылается на `GameUI` своей сцены через `[SerializeField]`.
- UI и экраны в `ServiceLocator` не кладём. Экраны не знают ни стек, ни сервисы, ни друг друга.

## 6. Части 2–5 (наброски, уточняются перед своей частью)

**Часть 2 — главное меню и сцены.**

- `ISceneLoader` в `[Services]` с затемнением — **готов** (модуль `Loading`, спек — [Loading.md](Loading.md)): свой
  Canvas (Sort Order 100) поверх `GameUI`; `Load(scene)` блокирует ввод, затемняет на unscaled time, грузит сцену,
  проявляет (и при ошибке); пауза снимается загрузкой сцены. В паузе уже есть «Restart» через него.
- `MainMenu.unity` — первой в Build Settings, с `[Services]` и `MainMenuUI`. Корневой `MainMenuScreen`
  (`CanGoBack = false`): «Continue», «New Game», «Settings», «Quit». «Continue» — когда появится SaveSystem и есть
  сохранение.
- В `PauseScreen` — «Quit to Menu» через `ISceneLoader`; общую часть оркестраторов (`Ask`) выделяем, когда она
  повторится во втором.

**Часть 3 — настройки.**

- `IGameSettings`: громкость (общая, музыка, звуки), полноэкранный режим, разрешение, VSync. Применяются сразу,
  хранятся в своём файле, отдельно от сейвов; сохраняются по `OnClosed` экрана настроек.
- `SettingsScreen` — ползунки и переключатели «влево-вправо» вместо выпадающих списков: так удобнее с геймпада и нет
  конфликта с Cancel, когда одно нажатие закрывает и список, и экран.
- Как AudioEngine задаёт громкость (группы микшера, параметры), разбираем перед частью.

**Часть 4 — сок.**

- `TweenTransition` на базовом API DOTween (`DOTween.To`, твины `Transform`) с `SetUpdate(true)`: UI-модули DOTween
  в `Plugins` без asmdef, а `Plugins` не трогаем. `AnimatorTransition` — Animator с Update Mode Unscaled Time.
- `ButtonFeedback`: при выделении и нажатии — пружинящий масштаб, звук через `IAudioSystem`, по желанию — частицы.
- Частицы в UI: Canvas в Screen Space – Camera или ParticleEffectForUGUI — выбираем на тестовой сцене.
- Покадровая анимация в UI: адаптер `SpriteAnimator` под `Image` (режим реального времени у аниматора уже есть).
- Материалы `GUI SSU` — свечение, блик, растворение.
- Масштаб: HD — `CanvasScaler` Scale With Screen Size; пиксельный — `IntegerCanvasScaler` в модуле (опорное разрешение,
  только целый коэффициент). Шрифты TMP: пиксельный (Point, растровый атлас) или SDF; кириллица — если тексты на русском.

**Часть 5 — HUD.** Сейчас HUD — Canvas `HUD` на префабе игрока с `HealthBarView` (этап урона). Оставить его там или
перенести в `GameUI` (подсказки кнопок, валюта, уведомления) — решаем перед частью; виджеты подписаны на события
сущностей, ссылки — внутри сцены.

## 7. Ошибки

| Ситуация | Поведение |
|---|---|
| `Push`/`Replace` с `null` | `ArgumentNullException` |
| Экран уже в стеке | `InvalidOperationException` с именем экрана |
| Операция стека из колбэка экрана во время другой операции | `InvalidOperationException` |
| Неназначенные ссылки `GameUI` | `ArgumentNullException` с именем поля в `Awake` |
| Неназначенные ссылки экранов | ловит EditMode-тест префабов (экран просыпается только при первом показе) |
| `GameUI` в сцене без `[Services]` | `InvalidOperationException` из `ServiceLocator.Get` |
| `PlayerInputController` без сервиса паузы | работает как раньше |
| Повторные `Pause()`/`Resume()` | ничего не делают |
| `GameUI` уничтожен на паузе | пауза снимается |

## 8. Тесты

**Модуль, EditMode** (`ScreenStack.Tests`, фейковые экраны пишут журнал вызовов):

- `Push`/`Pop`/`Replace`/`Clear` — порядок вызовов из раздела 4.3;
- видимость с оверлеями: непрозрачный экран скрывает нижние, оверлей — нет, цепочка оверлеев;
- `Back` на корневом экране и на пустом стеке;
- `Replace` не показывает экран под заменяемым;
- `Changed` — один раз на операцию, `Clear` пустого — ни разу;
- ошибки: `null`, повтор, операция из колбэка; операция из `Changed` разрешена.

**Модуль, PlayMode** (`ScreenStack.Tests.Runtime`, настоящий `EventSystem`):

- `Focus` выделяет `_firstSelected`; после возврата из-под оверлея выделена запомненная кнопка; после `Close`
  и нового `Open` — снова первая;
- экран под оверлеем не интерактивен; скрытый экран выключен; кнопки не нажимаются до конца появления;
- выделение возвращается после сброса;
- `FadeTransition` доигрывает при `timeScale = 0`, прерывание не зовёт старый `done`;
- `SelectOnPointerEnter` выделяет кнопку при наведении.

**Игра** (`Game.Tests.EditMode` и `Game.Tests.PlayMode`; стенд UI `UITestBase` — `EventSystem` из префаба `Services`,
`GamePause` с фейковым `IAudioSystem` в `ServiceLocator`, префаб `GameUI`, виртуальные клавиатура и геймпад
из `InputTestFixture`; тесты — `[UnityTest]`, шаг — реальный кадр: модуль UI принимает Submit/Cancel раз за
`Time.frameCount`; общий ассет `PlayerControls` ограничен виртуальными устройствами — сброс фикстуры его привязки
не переразрешает; `CharacterTestRig` — только для игрока; переходы доигрываются сразу; `ServiceLocator.Clear()`
в `SetUp` и `TearDown`):

- префабы (EditMode): ссылки экранов и `GameUI` назначены, экраны хранятся выключенными, кнопки выделяются наведением,
  `GameUI` читает `UI/Pause` и `UI/Cancel` и рисуется поверх HUD игрока;

- `GamePause`: `timeScale` и звук на паузе и после неё; повторные вызовы; `OnPauseChanged`; снятие паузы при загрузке
  сцены и в `Dispose`;
- Esc — пауза (`timeScale` 0, звук на паузе), снова Esc — игра идёт;
- Start в диалоге закрывает всё и снимает паузу;
- «Quit» — диалог; B — назад на паузу, выделена «Quit»; «No» — так же;
- стик вниз переводит выделение «Resume» → «Quit»;
- «Resume» по A снимает паузу, игрок не прыгает;
- `PlayerInputController` на паузе не двигает игрока, после паузы управление работает;
- потеря фокуса окна ставит паузу, при выключенной галочке — нет;
- `GameUI`, уничтоженный на паузе, снимает её.

## 9. Что меняется в правилах проекта (при реализации части 1)

- `CLAUDE.md`: модуль `ScreenStack` в списке модулей и сборок; домен `Scripts/UI`; `Prefabs/UI`; в `[Services]` —
  `EventSystem` и `GamePause`; карта `UI` и действие `Pause`; правило «пауза ⇔ стек `GameUI` не пуст»; `timeScale`
  меняет только `GamePause`; новые тестовые сборки.
- Роли: компоненты экранов — суффикс `Screen` или `Dialog`, висят на корне префаба экрана; корень UI (`GameUI`,
  `MainMenuUI`) — оркестратор, не игровая сущность, без `Controllers`/`Components`. Попадает в `CLAUDE.md`
  и в [CompositionGuide.md](CompositionGuide.md).

## 10. Вне рамок и когда пересматривать

**Вне рамок:** экран слотов (после SaveSystem); переназначение клавиш; локализация (тексты в компонентах TMP,
Unity Localization подключится позже); UI Toolkit; UI в мире — числа урона и подсказки делаем объектами мира,
как `YouWin`; сенсорный ввод; несколько игроков; асинхронная загрузка экранов.

**Пересмотреть, когда:**

- появились сложные экраны (инвентарь, карта, дерево умений) — пробуем отдельный экран на UI Toolkit, `IScreen` это
  позволяет;
- экраны нужно открывать из многих несвязанных мест — нужен сервис навигации вместо ссылок внутри сцены;
- экранов стало много и они тяжёлые — ленивое создание или Addressables вместо заранее вложенных префабов;
- на паузе должны работать отдельные системы (живой фон, мини-игра) — своё игровое время вместо `timeScale`.
