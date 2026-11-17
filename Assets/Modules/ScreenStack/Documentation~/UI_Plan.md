# UI, часть 1 — ядро и пауза: план реализации

> **Для исполнителя:** обязательный под-скилл — superpowers:executing-plans (инлайн: тесты гоняются через один
> Unity-эдитор по MCP, параллельные исполнители мешали бы друг другу). Шаги — чекбоксы (`- [ ]`).

**Цель:** меню паузы в игровых сценах: Esc / Start открывает паузу, игровое время встаёт, меню работает с клавиатуры,
мыши и геймпада, «назад» и «закрыть всё», подтверждение выхода, пауза при потере фокуса окна.

**Архитектура:** самостоятельный модуль `Assets/Game/Scripts/Modules/ScreenStack` — стек экранов (обычный C# за контрактом
`IScreen`) и экран на uGUI `UIScreen` с фокусом для геймпада и переходом `FadeTransition`. Игра: глобальный сервис
`IGamePause` (единственный владелец `Time.timeScale`) в `[Services]`, общий `EventSystem` в `[Services]` на новой карте
`UI` в `PlayerControls`, экраны `PauseScreen` и `ConfirmDialog`, оркестратор `GameUI` (пауза ⇔ стек не пуст).
`PlayerInputController` на паузе выключает карту `Player`.

**Стек:** Unity 6000.5.6f1, uGUI 2.x + TMP, Input System 1.20, C# 9, NUnit (Unity Test Framework), EditMode + PlayMode,
виртуальные устройства `InputTestFixture`, MCP `unity-mcp`.

Спек — [UI.md](UI.md) (часть 1), исследование — [Research_UI.md](Research_UI.md).

## Глобальные ограничения

- Модуль — `Assets/Game/Scripts/Modules/ScreenStack/`; сборки `ScreenStack` (Runtime, ссылка только на `UnityEngine.UI`),
  `ScreenStack.Tests` (EditMode), `ScreenStack.Tests.Runtime` (PlayMode); namespace `ScreenNavigation` /
  `ScreenNavigation.Tests`. Модуль не зависит от `Game`, Odin, DOTween и Input System.
- Игровой код — namespace `Gameplay`: `Scripts/UI/` (`GameUI`, `PauseScreen`, `ConfirmDialog`; рядом уже лежит
  `HealthBarView` этапа урона), `Scripts/GameSystem/Services/` (`IGamePause`, `GamePause`, `GameServices`). Префабы —
  `Assets/Game/Prefabs/UI/`. В сценах корень UI — объект `[GameUI]`.
- Не трогать: `Assets/ThirdParty/**`, `Assets/Plugins/**`, `Assets/TextMesh Pro/**`, `Assets/Game/Scripts/Modules/AudioEngine/**`.
- Стиль: Allman, 4 пробела, `_camelCase` приватные поля, `[SerializeField] private`, `UPPER_CASE` константы.
  Комментарии, `<summary>`, имена и сообщения — **по-английски**; правленый файл целиком на одном языке
  (`PlayerInputController.cs` переводится на английский целиком). Без `[Min]`, без `GetInstanceID()`. Обработчики
  событий — `Handle…`, события — `On…`, подписки — парой в `OnEnable`/`OnDisable`.
- TDD: тест → падает по правильной причине → минимальный код → зелёные тесты. Имена `Класс_Ситуация_Ожидание`.
  Чтобы тестовая сборка компилировалась, на шаге Red новый API — заглушками (тест падает на проверке, а не на сборке).
- `Time.timeScale` меняет только `GamePause`. Весь UI — на unscaled time.
- Тексты UI части 1 — английские заглушки: `PAUSED`, `Resume`, `Quit`, `Quit the game?`, `Yes`, `No`. Имя объекта кнопки
  в префабе = её текст: тесты ищут кнопки по имени.
- Коммиты — только по просьбе пользователя или при разрешённой автономной работе; перед коммитом — сводка. `git add` —
  только перечисленные в шаге пути, не `git add -A`. В рабочей копии чужие незакоммиченные правки (на 2026-10-23):
  `Assets/Plugins/Sirenix/**` (Odin), удалённый `DOTweenUpgradeManager`, `Stage1_Locomotion.md`,
  `PlayerMovementConfig.asset`, `CharacterMovementConfig.cs`, `LedgeComponent.cs`, `Sandbox_Locomotion.unity`,
  `Sandbox_PixelBaker.unity`, документы SaveSystem, `GanzSeTest`, `PixelTest`, `Synty`, `Scenes/1.unity`. Не трогать,
  в коммиты не брать.
- Один Unity-эдитор: пока идёт работа по плану, другая сессия в этом checkout с Unity не работает.
- `.meta` создаёт Unity при импорте (после `AssetDatabase.Refresh()`). `Unity_RunCommand` не умеет удалять ассеты —
  временных ассетов не создавать. Создание префабов не откатывается через Undo — откат только через git.

## Как запускать тесты (через MCP)

1. Выйти из Play Mode, сохранить открытые сцены (иначе Test Runner спросит и встанет).
2. После правки файлов с диска — `Unity_RunCommand` с `AssetDatabase.Refresh()`, затем `Unity_GetConsoleLogs`:
   ошибок компиляции быть не должно.
3. `Unity_RunCommand`: `EditorApplication.ExecuteMenuItem("Tools/Game/Run EditMode Tests")`
   (или `Run PlayMode Tests` / `Run All Tests`).
4. После доменной перезагрузки прочитать `Temp/GameTestResults.txt` (сводка и тексты падений). Мост отвалился
   («Unity not detected») — повторить вызов через ~10 с.

Шаблон `Unity_RunCommand` — `internal class CommandScript : IRunCommand` (см. `Assets/AI/CLAUDE.md`). Если команда не
видит типы игры или модуля (`GameServices`, `PauseScreen`, `FadeTransition`…), заменить `AddComponent<T>()` на
`AddComponent(Type.GetType("Gameplay.PauseScreen, Game"))` (модуль — `"ScreenNavigation.FadeTransition, ScreenStack"`),
а назначения полей оставить через `SerializedObject`.

## Файлы

```
Assets/Game/Scripts/Modules/ScreenStack/                                  (новое)
├── package.json, README.md
├── Runtime/ScreenStack.asmdef, IScreen.cs, ScreenStack.cs, UIScreen.cs, ScreenTransition.cs,
│           FadeTransition.cs, SelectOnPointerEnter.cs
├── Tests/Editor/ScreenStack.Tests.asmdef, ScreenStackTests.cs
└── Tests/Runtime/ScreenStack.Tests.Runtime.asmdef, UIScreenTests.cs, FadeTransitionTests.cs,
                  SelectOnPointerEnterTests.cs

Assets/Game/Game.asmdef                                           + ScreenStack, Unity.TextMeshPro
Assets/Game/Input/PlayerControls.inputactions                     + карта UI
Assets/Game/Scripts/GameSystem/Services/IGamePause.cs, GamePause.cs          (новое)
Assets/Game/Scripts/GameSystem/Services/GameServices.cs                      + GamePause
Assets/Game/Scripts/Gameplay/Character/Controllers/PlayerInputController.cs   пауза; комментарии на английском
Assets/Game/Scripts/UI/PauseScreen.cs, ConfirmDialog.cs, GameUI.cs    (новое)
Assets/Game/Prefabs/Services.prefab                               + дочерний EventSystem
Assets/Game/Prefabs/UI/PauseScreen.prefab, ConfirmDialog.prefab, GameUI.prefab   (новое, через Unity)
Assets/Game/Scenes/Game.unity                                     − EventSystem, + [GameUI]
Assets/Game/Scenes/Sandbox_Enemies.unity                          + [GameUI]
Assets/Game/Scenes/Sandbox_Locomotion.unity                       + [GameUI] (только после ответа пользователя)
Assets/Game/Tests/EditMode/Game.Tests.EditMode.asmdef             + ScreenStack, UnityEngine.UI, Unity.InputSystem
Assets/Game/Tests/EditMode/UIPrefabTests.cs                       (новое)
Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef             + UnityEngine.UI, ScreenStack
Assets/Game/Tests/PlayMode/FakeAudioSystem.cs                     пишет Pause/Resume
Assets/Game/Tests/PlayMode/GameServicesTests.cs                   + пауза, EventSystem
Assets/Game/Tests/PlayMode/GamePauseTests.cs, PauseInputTests.cs, ScreenPrefabTests.cs,
                           UITestBase.cs, GameUITests.cs          (новое)
Assets/Game/Tests/PlayMode/Scenes/EmptyTestScene.unity            (новое, через Unity)
Assets/AI/CLAUDE.md, Assets/AI/Docs/CompositionGuide.md, Assets/AI/Docs/UI.md
```

---

### Задача 1: ядро модуля ScreenStack

**Файлы:**
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/package.json`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Runtime/ScreenStack.asmdef`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Runtime/IScreen.cs`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Runtime/ScreenStack.cs`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Tests/Editor/ScreenStack.Tests.asmdef`
- Тест: `Assets/Game/Scripts/Modules/ScreenStack/Tests/Editor/ScreenStackTests.cs`

**Интерфейсы:**
- Использует: —
- Отдаёт (задачам 2–7): сборка `ScreenStack`, namespace `ScreenNavigation`.
  `interface IScreen { bool IsOverlay { get; } bool CanGoBack { get; } void Open(); void Close(); void Show(); void Hide(); void Focus(); void Blur(); }`;
  `sealed class ScreenStack { event Action Changed; IScreen Top { get; } int Count { get; } bool IsEmpty { get; } bool Contains(IScreen); void Push(IScreen); bool Pop(); bool Back(); void Replace(IScreen); void Clear(); }`.

- [ ] **Шаг 0: исходное состояние.**
  1. Этап урона закоммичен (ветка `feature/damage`, включая его документацию); других сессий с Unity в этом checkout
     нет. Если не так — остановиться и спросить пользователя.
  2. `git switch -c feature/ui` от вершины `feature/damage` (если урон уже влит в `main` — от `main`). Чужие
     незакоммиченные правки останутся в рабочей копии.
  3. `AssetDatabase.Refresh()` (Unity создаст `.meta` документов), затем с разрешения пользователя закоммитить
     документы UI:

```bash
git add Assets/AI/Docs/Research_UI.md Assets/AI/Docs/Research_UI.md.meta Assets/AI/Docs/UI.md Assets/AI/Docs/UI.md.meta Assets/AI/Docs/UI_Plan.md Assets/AI/Docs/UI_Plan.md.meta
git commit -m "Add UI research, spec and plan" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

  4. `Run All Tests` и записать, что красное ещё до начала работы (в рабочей копии чужие правки `LedgeComponent`
     и конфигов) — чтобы не принять чужое падение за своё.

- [ ] **Шаг 1: каркас модуля.** `package.json`:

```json
{
  "name": "com.eugenem23.screen-stack",
  "version": "1.0.0",
  "displayName": "Screen Stack",
  "description": "Menu navigation for Unity UI: a stack of screens where only the top one gets input, overlays keep the screens below visible and Back pops. The stack is plain C# behind the IScreen contract; UIScreen implements it for uGUI with focus for keyboard and gamepad. No third-party dependencies.",
  "unity": "2021.3",
  "dependencies": {
    "com.unity.ugui": "1.0.0"
  },
  "keywords": [
    "ui",
    "menu",
    "screen stack",
    "navigation",
    "ugui"
  ],
  "author": {
    "name": "EugeneM23"
  }
}
```

`Runtime/ScreenStack.asmdef`:

```json
{
    "name": "ScreenStack",
    "rootNamespace": "ScreenNavigation",
    "references": [
        "UnityEngine.UI"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Tests/Editor/ScreenStack.Tests.asmdef`:

```json
{
    "name": "ScreenStack.Tests",
    "rootNamespace": "ScreenNavigation.Tests",
    "references": [
        "ScreenStack",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Шаг 2: контракт и заглушка стека.** `Runtime/IScreen.cs` — сразу целиком:

```csharp
namespace ScreenNavigation
{
    /// <summary>
    /// A screen as ScreenStack sees it. Three pairs, one meaning each: Open/Close — the screen entered or left the
    /// stack, Show/Hide — it became visible or invisible, Focus/Blur — it got or lost input. A screen covered by an
    /// opaque one is hidden but stays in the stack, which is how it tells "covered" from "closed".
    /// Only ScreenStack calls these methods.
    /// </summary>
    public interface IScreen
    {
        /// <summary>Screens below an overlay stay visible (a dialog over settings).</summary>
        bool IsOverlay { get; }

        /// <summary>False: Back() keeps the screen in the stack (the main menu root).</summary>
        bool CanGoBack { get; }

        void Open();
        void Close();
        void Show();
        void Hide();
        void Focus();
        void Blur();
    }
}
```

`Runtime/ScreenStack.cs` — заглушка (тесты падают на `NotImplementedException`, а не на сборке):

```csharp
using System;

namespace ScreenNavigation
{
    public sealed class ScreenStack
    {
        public event Action Changed;

        public IScreen Top => throw new NotImplementedException();
        public int Count => throw new NotImplementedException();
        public bool IsEmpty => throw new NotImplementedException();

        public bool Contains(IScreen screen) => throw new NotImplementedException();
        public void Push(IScreen screen) => throw new NotImplementedException();
        public bool Pop() => throw new NotImplementedException();
        public bool Back() => throw new NotImplementedException();
        public void Replace(IScreen screen) => throw new NotImplementedException();
        public void Clear() => throw new NotImplementedException();
    }
}
```

- [ ] **Шаг 3: тесты** — `Tests/Editor/ScreenStackTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ScreenNavigation.Tests
{
    /// <summary>Stack rules on fake screens that write every call into one shared log.</summary>
    public class ScreenStackTests
    {
        private sealed class FakeScreen : IScreen
        {
            private readonly List<string> _log;
            private readonly string _name;

            public FakeScreen(string name, List<string> log, bool overlay, bool canGoBack)
            {
                _name = name;
                _log = log;
                IsOverlay = overlay;
                CanGoBack = canGoBack;
            }

            public bool IsOverlay { get; }
            public bool CanGoBack { get; }

            /// <summary>Runs inside Show — to call the stack from a screen callback.</summary>
            public Action OnShow { get; set; }

            public void Open() => _log.Add($"{_name}.Open");
            public void Close() => _log.Add($"{_name}.Close");

            public void Show()
            {
                _log.Add($"{_name}.Show");
                OnShow?.Invoke();
            }

            public void Hide() => _log.Add($"{_name}.Hide");
            public void Focus() => _log.Add($"{_name}.Focus");
            public void Blur() => _log.Add($"{_name}.Blur");
            public override string ToString() => _name;
        }

        private readonly List<string> _log = new();
        private ScreenStack _stack;
        private int _changed;

        [SetUp]
        public void SetUp()
        {
            _log.Clear();
            _changed = 0;
            _stack = new ScreenStack();
            _stack.Changed += () => _changed++;
        }

        private FakeScreen Screen(string name, bool overlay = false, bool canGoBack = true) =>
            new(name, _log, overlay, canGoBack);

        private void AssertLog(params string[] expected) => CollectionAssert.AreEqual(expected, _log);

        [Test]
        public void Push_OnEmpty_OpensShowsAndFocuses()
        {
            FakeScreen a = Screen("A");

            _stack.Push(a);

            AssertLog("A.Open", "A.Show", "A.Focus");
            Assert.AreSame(a, _stack.Top);
            Assert.AreEqual(1, _stack.Count);
            Assert.IsTrue(_stack.Contains(a));
        }

        [Test]
        public void Push_Opaque_BlursAndHidesPrevious()
        {
            _stack.Push(Screen("A"));
            _log.Clear();

            _stack.Push(Screen("B"));

            AssertLog("A.Blur", "B.Open", "A.Hide", "B.Show", "B.Focus");
        }

        [Test]
        public void Push_Overlay_KeepsPreviousVisible()
        {
            _stack.Push(Screen("A"));
            _log.Clear();

            _stack.Push(Screen("D", overlay: true));

            AssertLog("A.Blur", "D.Open", "D.Show", "D.Focus");
        }

        [Test]
        public void Pop_Opaque_ShowsAndFocusesPrevious()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);
            _stack.Push(Screen("B"));
            _log.Clear();

            bool popped = _stack.Pop();

            Assert.IsTrue(popped);
            AssertLog("B.Blur", "B.Hide", "B.Close", "A.Show", "A.Focus");
            Assert.AreSame(a, _stack.Top);
        }

        [Test]
        public void Pop_Overlay_RefocusesPreviousWithoutShowing()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("D", overlay: true));
            _log.Clear();

            _stack.Pop();

            AssertLog("D.Blur", "D.Hide", "D.Close", "A.Focus");
        }

        [Test]
        public void Pop_Empty_ReturnsFalseWithoutChanged()
        {
            Assert.IsFalse(_stack.Pop());
            Assert.AreEqual(0, _changed);
            Assert.IsNull(_stack.Top);
            Assert.IsTrue(_stack.IsEmpty);
        }

        [Test]
        public void Visibility_OverlayChain_ShowsDownToFirstOpaque()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _stack.Push(Screen("C", overlay: true));
            _stack.Push(Screen("D", overlay: true));
            _log.Clear();

            _stack.Clear();

            // A was hidden under B; B, C and D are visible and hide top-down
            AssertLog("D.Blur", "D.Hide", "C.Hide", "B.Hide", "D.Close", "C.Close", "B.Close", "A.Close");
            Assert.IsTrue(_stack.IsEmpty);
        }

        [Test]
        public void Back_RootCannotGoBack_KeepsScreen()
        {
            FakeScreen root = Screen("Root", canGoBack: false);
            _stack.Push(root);
            _log.Clear();
            _changed = 0;

            Assert.IsFalse(_stack.Back());

            AssertLog();
            Assert.AreSame(root, _stack.Top);
            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Back_CanGoBack_Pops()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);
            _stack.Push(Screen("B"));

            Assert.IsTrue(_stack.Back());

            Assert.AreSame(a, _stack.Top);
        }

        [Test]
        public void Back_Empty_ReturnsFalse()
        {
            Assert.IsFalse(_stack.Back());
            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Replace_OpaqueOverOpaque_DoesNotShowScreenBelow()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _log.Clear();

            _stack.Replace(Screen("C"));

            AssertLog("B.Blur", "C.Open", "B.Hide", "B.Close", "C.Show", "C.Focus");
            Assert.AreEqual(2, _stack.Count);
        }

        [Test]
        public void Replace_WithOverlay_ShowsScreenBelow()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _log.Clear();

            _stack.Replace(Screen("D", overlay: true));

            AssertLog("B.Blur", "D.Open", "B.Hide", "B.Close", "A.Show", "D.Show", "D.Focus");
        }

        [Test]
        public void Replace_Empty_Pushes()
        {
            _stack.Replace(Screen("A"));

            AssertLog("A.Open", "A.Show", "A.Focus");
        }

        [Test]
        public void Clear_OverlayOverScreen_HidesAndClosesTopDown()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B", overlay: true));
            _log.Clear();

            _stack.Clear();

            AssertLog("B.Blur", "B.Hide", "A.Hide", "B.Close", "A.Close");
        }

        [Test]
        public void Clear_Empty_DoesNotRaiseChanged()
        {
            _stack.Clear();

            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Changed_RaisedOncePerOperation()
        {
            _stack.Push(Screen("A"));
            _stack.Push(Screen("B"));
            _stack.Replace(Screen("C"));
            _stack.Back();
            _stack.Pop();

            Assert.AreEqual(5, _changed);
        }

        [Test]
        public void Push_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _stack.Push(null));
        }

        [Test]
        public void Push_SameScreenTwice_Throws()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);

            Assert.Throws<InvalidOperationException>(() => _stack.Push(a));
            Assert.AreEqual(1, _stack.Count);
        }

        [Test]
        public void Replace_WithScreenInStack_Throws()
        {
            FakeScreen a = Screen("A");
            _stack.Push(a);
            _stack.Push(Screen("B"));

            Assert.Throws<InvalidOperationException>(() => _stack.Replace(a));
        }

        [Test]
        public void Push_FromScreenCallback_Throws()
        {
            FakeScreen a = Screen("A");
            a.OnShow = () => _stack.Push(Screen("B"));

            Assert.Throws<InvalidOperationException>(() => _stack.Push(a));
        }

        [Test]
        public void Push_FromChangedHandler_Allowed()
        {
            FakeScreen b = Screen("B");
            bool pushed = false;
            _stack.Changed += () =>
            {
                if (pushed)
                    return;
                pushed = true;
                _stack.Push(b);
            };

            _stack.Push(Screen("A"));

            Assert.AreSame(b, _stack.Top);
            Assert.AreEqual(2, _stack.Count);
        }
    }
}
```

- [ ] **Шаг 4: Red.** `AssetDatabase.Refresh()`, консоль без ошибок, `Run EditMode Tests`. Ожидание: все тесты
  `ScreenStackTests` падают с `NotImplementedException`, остальные — как на шаге 0.

- [ ] **Шаг 5: реализация** — `Runtime/ScreenStack.cs` целиком:

```csharp
using System;
using System.Collections.Generic;

namespace ScreenNavigation
{
    /// <summary>
    /// Stack of menu screens. Only the top screen gets input; a screen is visible while it is the top one or every
    /// screen above it is an overlay. Inside one operation the calls go in a fixed order: Blur of the old top →
    /// Open of the entering screen → Hide (top-down) → Close of the leaving screens (top-down) → Show (bottom-up) →
    /// Focus of the new top → Changed. Plain C#: the owner keeps it as a field.
    /// </summary>
    public sealed class ScreenStack
    {
        /// <summary>After every operation that changed the stack; stack operations are allowed in handlers.</summary>
        public event Action Changed;

        private readonly List<IScreen> _screens = new();
        private readonly List<IScreen> _visible = new();
        private readonly List<IScreen> _nextVisible = new();
        private readonly List<IScreen> _leaving = new();
        private bool _busy;

        public IScreen Top => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;
        public int Count => _screens.Count;
        public bool IsEmpty => _screens.Count == 0;

        public bool Contains(IScreen screen) => _screens.Contains(screen);

        public void Push(IScreen screen)
        {
            EnsureIdle();
            EnsureCanEnter(screen);

            Begin();
            try
            {
                Top?.Blur();
                _screens.Add(screen);
                screen.Open();
                ApplyVisibility();
                screen.Focus();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
        }

        public bool Pop()
        {
            EnsureIdle();
            if (IsEmpty)
                return false;

            Begin();
            try
            {
                IScreen leaving = Top;
                leaving.Blur();
                _screens.RemoveAt(_screens.Count - 1);
                _leaving.Add(leaving);
                ApplyVisibility();
                Top?.Focus();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
            return true;
        }

        public bool Back()
        {
            EnsureIdle();
            if (IsEmpty || !Top.CanGoBack)
                return false;

            return Pop();
        }

        public void Replace(IScreen screen)
        {
            EnsureIdle();
            if (IsEmpty)
            {
                Push(screen);
                return;
            }

            EnsureCanEnter(screen);

            Begin();
            try
            {
                IScreen leaving = Top;
                leaving.Blur();
                _screens[_screens.Count - 1] = screen;
                _leaving.Add(leaving);
                screen.Open();
                ApplyVisibility();
                screen.Focus();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
        }

        public void Clear()
        {
            EnsureIdle();
            if (IsEmpty)
                return;

            Begin();
            try
            {
                Top.Blur();
                for (int i = _screens.Count - 1; i >= 0; i--)
                    _leaving.Add(_screens[i]);
                _screens.Clear();
                ApplyVisibility();
            }
            finally
            {
                End();
            }

            Changed?.Invoke();
        }

        // Hide what is no longer visible (top-down), close what left (top-down), show what became visible (bottom-up)
        private void ApplyVisibility()
        {
            CollectVisible(_nextVisible);

            for (int i = _visible.Count - 1; i >= 0; i--)
            {
                if (!_nextVisible.Contains(_visible[i]))
                    _visible[i].Hide();
            }

            for (int i = 0; i < _leaving.Count; i++)
                _leaving[i].Close();
            _leaving.Clear();

            for (int i = 0; i < _nextVisible.Count; i++)
            {
                if (!_visible.Contains(_nextVisible[i]))
                    _nextVisible[i].Show();
            }

            _visible.Clear();
            _visible.AddRange(_nextVisible);
        }

        // From the top down to the first screen that is not an overlay; the result goes bottom-up
        private void CollectVisible(List<IScreen> result)
        {
            result.Clear();
            for (int i = _screens.Count - 1; i >= 0; i--)
            {
                result.Insert(0, _screens[i]);
                if (!_screens[i].IsOverlay)
                    break;
            }
        }

        private void Begin() => _busy = true;

        private void End()
        {
            _busy = false;
            _leaving.Clear();
        }

        private void EnsureIdle()
        {
            if (_busy)
                throw new InvalidOperationException("ScreenStack operations can't be called from screen callbacks.");
        }

        private void EnsureCanEnter(IScreen screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));
            if (_screens.Contains(screen))
                throw new InvalidOperationException($"Screen {screen} is already in the stack.");
        }
    }
}
```

- [ ] **Шаг 6: Green.** `Refresh`, консоль, `Run EditMode Tests` — `ScreenStackTests` зелёные, остальное как на шаге 0.

- [ ] **Шаг 7: коммит.**

```bash
git add Assets/Game/Scripts/Modules/ScreenStack.meta Assets/Game/Scripts/Modules/ScreenStack
git commit -m "Add ScreenStack module core" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Задача 2: экраны uGUI в модуле

**Файлы:**
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Runtime/UIScreen.cs`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Runtime/ScreenTransition.cs`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Runtime/FadeTransition.cs`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Runtime/SelectOnPointerEnter.cs`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/Tests/Runtime/ScreenStack.Tests.Runtime.asmdef`
- Тест: `Assets/Game/Scripts/Modules/ScreenStack/Tests/Runtime/UIScreenTests.cs`, `FadeTransitionTests.cs`,
  `SelectOnPointerEnterTests.cs`
- Создать: `Assets/Game/Scripts/Modules/ScreenStack/README.md`

**Интерфейсы:**
- Использует: `IScreen`, `ScreenStack` (задача 1).
- Отдаёт (задачам 6–7):
  - `[RequireComponent(typeof(CanvasGroup))] class UIScreen : MonoBehaviour, IScreen` — события `OnOpened`, `OnClosed`;
    свойства `IsOverlay`, `CanGoBack`, `IsShown` (между `Show` и `Hide`), `IsInteractable` (в фокусе и появление
    закончено); `void Configure(Selectable firstSelected, bool isOverlay = false, bool canGoBack = true, ScreenTransition transition = null)`;
    `protected virtual void Update()`; сериализуемые поля `_isOverlay`, `_canGoBack`, `_firstSelected`, `_transition`.
  - `abstract class ScreenTransition : MonoBehaviour { abstract void Play(bool show, Action done); }`.
  - `[RequireComponent(typeof(CanvasGroup))] sealed class FadeTransition : ScreenTransition` — поле `_duration` (0,15),
    `float Duration`, `bool IsPlaying`, `void Tick(float dt)`, `void Configure(float duration)`.
  - `sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler`.

- [ ] **Шаг 1: сборка тестов** — `Tests/Runtime/ScreenStack.Tests.Runtime.asmdef`:

```json
{
    "name": "ScreenStack.Tests.Runtime",
    "rootNamespace": "ScreenNavigation.Tests",
    "references": [
        "ScreenStack",
        "UnityEngine.UI",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Шаг 2: заглушки.** `Runtime/ScreenTransition.cs` — сразу целиком:

```csharp
using System;
using UnityEngine;

namespace ScreenNavigation
{
    /// <summary>
    /// Animation of a UIScreen appearing and disappearing. Plays from the current state, runs on unscaled time (the game
    /// may be paused) and calls done at the end; a new Play interrupts the running one, whose done is never called.
    /// Derive for tweens or an Animator; FadeTransition is the built-in one.
    /// </summary>
    public abstract class ScreenTransition : MonoBehaviour
    {
        public abstract void Play(bool show, Action done);
    }
}
```

`Runtime/UIScreen.cs` — заглушка:

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ScreenNavigation
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour, IScreen
    {
        public event Action OnOpened;
        public event Action OnClosed;

        [SerializeField] private bool _isOverlay;
        [SerializeField] private bool _canGoBack = true;
        [SerializeField] private Selectable _firstSelected;
        [SerializeField] private ScreenTransition _transition;

        public bool IsOverlay => _isOverlay;
        public bool CanGoBack => _canGoBack;
        public bool IsShown => false;
        public bool IsInteractable => false;

        public void Configure(Selectable firstSelected, bool isOverlay = false, bool canGoBack = true,
            ScreenTransition transition = null)
        {
        }

        protected virtual void Update()
        {
        }

        public void Open()
        {
        }

        public void Close()
        {
        }

        public void Show()
        {
        }

        public void Hide()
        {
        }

        public void Focus()
        {
        }

        public void Blur()
        {
        }
    }
}
```

`Runtime/FadeTransition.cs` — заглушка:

```csharp
using System;
using UnityEngine;

namespace ScreenNavigation
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FadeTransition : ScreenTransition
    {
        [SerializeField] private float _duration = 0.15f;

        public float Duration => _duration;
        public bool IsPlaying => false;

        public void Configure(float duration)
        {
        }

        public override void Play(bool show, Action done)
        {
        }

        public void Tick(float dt)
        {
        }
    }
}
```

`Runtime/SelectOnPointerEnter.cs` — заглушка:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

namespace ScreenNavigation
{
    public sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
        }
    }
}
```

- [ ] **Шаг 3: тесты.** `Tests/Runtime/UIScreenTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ScreenNavigation.Tests
{
    /// <summary>UIScreen with a real EventSystem: visibility, input, selection memory, transitions.</summary>
    public class UIScreenTests
    {
        private EventSystem _events;
        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("EventSystem").AddComponent<EventSystem>();
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_events.gameObject);
        }

        private GameObject Selected => _events.currentSelectedGameObject;

        /// <summary>A switched-off screen with buttons First (selected on open) and Second.</summary>
        private UIScreen CreateScreen(string name, bool overlay = false, float fade = -1f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(_canvas.transform, false);
            var screen = go.AddComponent<UIScreen>();
            Button first = CreateButton(go.transform, "First");
            CreateButton(go.transform, "Second");

            FadeTransition transition = null;
            if (fade >= 0f)
            {
                transition = go.AddComponent<FadeTransition>();
                transition.Configure(fade);
            }

            screen.Configure(first, overlay, transition: transition);
            return screen;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }

        private static GameObject Child(UIScreen screen, string name) => screen.transform.Find(name).gameObject;

        private static void Enter(UIScreen screen)
        {
            screen.Open();
            screen.Show();
            screen.Focus();
        }

        [Test]
        public void UIScreen_Focus_SelectsFirstSelected()
        {
            UIScreen screen = CreateScreen("A");

            Enter(screen);

            Assert.AreSame(Child(screen, "First"), Selected);
            Assert.IsTrue(screen.IsShown);
            Assert.IsTrue(screen.IsInteractable);
            Assert.IsTrue(screen.GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void UIScreen_FocusAfterBlur_SelectsRememberedButton()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);
            _events.SetSelectedGameObject(Child(screen, "Second"));

            screen.Blur();
            _events.SetSelectedGameObject(null);
            screen.Focus();

            Assert.AreSame(Child(screen, "Second"), Selected);
        }

        [Test]
        public void UIScreen_ReopenedAfterClose_SelectsFirstAgain()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);
            _events.SetSelectedGameObject(Child(screen, "Second"));
            screen.Blur();
            screen.Hide();
            screen.Close();

            Enter(screen);

            Assert.AreSame(Child(screen, "First"), Selected);
        }

        [Test]
        public void UIScreen_Blur_StaysVisibleButNotInteractable()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);

            screen.Blur();

            Assert.IsTrue(screen.gameObject.activeSelf);
            Assert.IsTrue(screen.IsShown);
            Assert.IsFalse(screen.IsInteractable);
            Assert.IsFalse(screen.GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void UIScreen_Hide_SwitchesOffAndDropsItsSelection()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);
            screen.Blur();
            Assert.IsTrue(screen.gameObject.activeSelf, "precondition: the screen is shown");
            Assert.IsNotNull(Selected, "precondition: the screen holds the selection");

            screen.Hide();

            Assert.IsFalse(screen.gameObject.activeSelf);
            Assert.IsFalse(screen.IsShown);
            Assert.IsNull(Selected, "Submit in the game must not reach a switched-off button");
        }

        [Test]
        public void UIScreen_ShowWithFade_InteractableOnlyAfterFade()
        {
            UIScreen screen = CreateScreen("A", fade: 0.2f);
            var fade = screen.GetComponent<FadeTransition>();

            Enter(screen);
            Assert.IsFalse(screen.IsInteractable, "buttons wait for the fade");

            fade.Tick(0.1f);
            Assert.IsFalse(screen.IsInteractable);

            fade.Tick(0.1f);
            Assert.IsTrue(screen.IsInteractable);
            Assert.IsTrue(screen.GetComponent<CanvasGroup>().interactable);
        }

        [Test]
        public void UIScreen_HideWithFade_SwitchesOffAfterFade()
        {
            UIScreen screen = CreateScreen("A", fade: 0.2f);
            var fade = screen.GetComponent<FadeTransition>();
            Enter(screen);
            fade.Tick(1f);

            screen.Blur();
            screen.Hide();
            Assert.IsTrue(screen.gameObject.activeSelf, "still fading out");

            fade.Tick(1f);
            Assert.IsFalse(screen.gameObject.activeSelf);
        }

        [Test]
        public void UIScreen_Show_DrawsOverSiblings()
        {
            UIScreen first = CreateScreen("A");
            UIScreen second = CreateScreen("B");
            second.Show();

            first.Show();

            Assert.AreEqual(_canvas.transform.childCount - 1, first.transform.GetSiblingIndex());
        }

        [Test]
        public void UIScreen_OpenAndClose_RaiseEvents()
        {
            UIScreen screen = CreateScreen("A");
            int opened = 0;
            int closed = 0;
            screen.OnOpened += () => opened++;
            screen.OnClosed += () => closed++;

            screen.Open();
            screen.Close();

            Assert.AreEqual(1, opened);
            Assert.AreEqual(1, closed);
        }

        [UnityTest]
        public IEnumerator UIScreen_SelectionLost_SelectsAgain()
        {
            UIScreen screen = CreateScreen("A");
            Enter(screen);

            _events.SetSelectedGameObject(null);
            yield return null;

            Assert.AreSame(Child(screen, "First"), Selected);
        }

        [Test]
        public void ScreenStack_DialogOverScreen_ReturnsSelectionToOpener()
        {
            var stack = new ScreenStack();
            UIScreen menu = CreateScreen("Menu");
            UIScreen dialog = CreateScreen("Dialog", overlay: true);
            stack.Push(menu);
            _events.SetSelectedGameObject(Child(menu, "Second"));

            stack.Push(dialog);

            Assert.IsTrue(menu.IsShown, "an overlay keeps the menu visible");
            Assert.IsFalse(menu.IsInteractable);
            Assert.AreSame(Child(dialog, "First"), Selected);

            stack.Pop();

            Assert.AreSame(Child(menu, "Second"), Selected);
            Assert.IsTrue(menu.IsInteractable);
        }
    }
}
```

`Tests/Runtime/FadeTransitionTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ScreenNavigation.Tests
{
    /// <summary>FadeTransition: alpha over Duration of unscaled time, interruption, transparent when switched off.</summary>
    public class FadeTransitionTests
    {
        private GameObject _go;
        private FadeTransition _fade;
        private CanvasGroup _group;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Fade", typeof(RectTransform));
            _fade = _go.AddComponent<FadeTransition>();
            _group = _go.GetComponent<CanvasGroup>();
            _fade.Configure(0.2f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            Time.timeScale = 1f;
        }

        [Test]
        public void FadeTransition_Ticks_ReachesTargetAfterDuration()
        {
            bool done = false;

            _fade.Play(true, () => done = true);
            _fade.Tick(0.1f);

            Assert.AreEqual(0.5f, _group.alpha, 1e-4f);
            Assert.IsFalse(done);
            Assert.IsTrue(_fade.IsPlaying);

            _fade.Tick(0.1f);

            Assert.AreEqual(1f, _group.alpha);
            Assert.IsTrue(done);
            Assert.IsFalse(_fade.IsPlaying);
        }

        [Test]
        public void FadeTransition_ZeroDuration_FinishesAtOnce()
        {
            _fade.Configure(0f);
            bool done = false;

            _fade.Play(true, () => done = true);

            Assert.IsTrue(done);
            Assert.AreEqual(1f, _group.alpha);
        }

        [Test]
        public void FadeTransition_Interrupted_DropsOldCallback()
        {
            bool shown = false;
            bool hidden = false;

            _fade.Play(true, () => shown = true);
            _fade.Tick(0.1f);
            _fade.Play(false, () => hidden = true);
            _fade.Tick(1f);

            Assert.IsFalse(shown);
            Assert.IsTrue(hidden);
            Assert.AreEqual(0f, _group.alpha);
        }

        [Test]
        public void FadeTransition_SwitchedOff_BecomesTransparent()
        {
            _fade.Play(true, null);
            _fade.Tick(1f);

            _go.SetActive(false);

            Assert.AreEqual(0f, _group.alpha, "the next show fades in from zero");
        }

        [UnityTest]
        public IEnumerator FadeTransition_GamePaused_FinishesOnRealTime()
        {
            Time.timeScale = 0f;
            bool done = false;

            _fade.Play(true, () => done = true);
            float until = Time.realtimeSinceStartup + 1f;
            while (!done && Time.realtimeSinceStartup < until)
                yield return null;

            Assert.IsTrue(done);
        }
    }
}
```

`Tests/Runtime/SelectOnPointerEnterTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScreenNavigation.Tests
{
    /// <summary>Mouse hover selects an interactable button — mouse and gamepad share one selection.</summary>
    public class SelectOnPointerEnterTests
    {
        private EventSystem _events;
        private Button _button;

        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("EventSystem").AddComponent<EventSystem>();
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.AddComponent<SelectOnPointerEnter>();
            _button = go.GetComponent<Button>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_button.gameObject);
            Object.DestroyImmediate(_events.gameObject);
        }

        private void Hover() =>
            ExecuteEvents.Execute(_button.gameObject, new PointerEventData(_events), ExecuteEvents.pointerEnterHandler);

        [Test]
        public void SelectOnPointerEnter_Hover_SelectsButton()
        {
            Hover();

            Assert.AreSame(_button.gameObject, _events.currentSelectedGameObject);
        }

        [Test]
        public void SelectOnPointerEnter_NotInteractable_KeepsSelection()
        {
            _button.interactable = false;

            Hover();

            Assert.IsNull(_events.currentSelectedGameObject);
        }
    }
}
```

- [ ] **Шаг 4: Red.** `Refresh`, консоль, `Run PlayMode Tests`. Ожидание: тесты `UIScreenTests`, `FadeTransitionTests`,
  `SelectOnPointerEnterTests` падают на проверках (заглушки ничего не делают); зелёный уже сейчас только
  `SelectOnPointerEnter_NotInteractable_KeepsSelection` (заглушка и так ничего не выделяет); остальное — как на шаге 0
  задачи 1.

- [ ] **Шаг 5: реализация.** `Runtime/UIScreen.cs` целиком:

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScreenNavigation
{
    /// <summary>
    /// A uGUI screen for ScreenStack, stored switched off: only the stack shows it. Show turns the GameObject on,
    /// draws it over its siblings and plays the optional transition; Hide plays it back and turns the object off.
    /// Input goes through the CanvasGroup: buttons work only while the screen is focused and fully shown. On focus
    /// the screen selects the button remembered at its last Blur, or the first one; it forgets the button when it
    /// leaves the stack. Game screens derive from it and wire their buttons in OnEnable/OnDisable.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour, IScreen
    {
        public event Action OnOpened;
        public event Action OnClosed;

        [Header("Stack")]
        [Tooltip("Screens below stay visible (a dialog over a menu)")]
        [SerializeField] private bool _isOverlay;
        [Tooltip("Off: Back keeps the screen (the main menu root)")]
        [SerializeField] private bool _canGoBack = true;

        [Header("Focus")]
        [Tooltip("Selected when the screen opens; empty for a mouse-only screen")]
        [SerializeField] private Selectable _firstSelected;

        [Header("View")]
        [Tooltip("Optional: without it the screen appears and disappears at once")]
        [SerializeField] private ScreenTransition _transition;

        private CanvasGroup _canvasGroup;
        private GameObject _remembered;
        private bool _shown;
        private bool _showFinished;
        private bool _focused;

        public bool IsOverlay => _isOverlay;
        public bool CanGoBack => _canGoBack;

        /// <summary>Between Show and Hide, the show transition included.</summary>
        public bool IsShown => _shown;

        /// <summary>Focused and fully shown: its buttons react.</summary>
        public bool IsInteractable => _shown && _showFinished && _focused;

        // Works before Awake: the stack opens and shows screens that have never been active
        private CanvasGroup Group => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        /// <summary>Setup for screens built in code (tests); prefabs set the same fields in the Inspector.</summary>
        public void Configure(Selectable firstSelected, bool isOverlay = false, bool canGoBack = true,
            ScreenTransition transition = null)
        {
            _firstSelected = firstSelected;
            _isOverlay = isOverlay;
            _canGoBack = canGoBack;
            _transition = transition;
        }

        /// <summary>Gives the selection back when it is lost (a mouse click on empty space, a button switched off).</summary>
        protected virtual void Update()
        {
            if (!IsInteractable)
                return;

            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null)
                Select();
        }

        public void Open()
        {
            _remembered = null;
            OnOpened?.Invoke();
        }

        public void Close()
        {
            _remembered = null;
            OnClosed?.Invoke();
        }

        public void Show()
        {
            _shown = true;
            _showFinished = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Group.blocksRaycasts = true;
            Refresh();

            if (_transition != null)
                _transition.Play(true, FinishShow);
            else
                FinishShow();
        }

        public void Hide()
        {
            _shown = false;
            _showFinished = false;
            Group.blocksRaycasts = false;
            Refresh();
            DropSelection();

            if (_transition != null && gameObject.activeInHierarchy)
                _transition.Play(false, FinishHide);
            else
                FinishHide();
        }

        public void Focus()
        {
            _focused = true;
            Refresh();
            Select();
        }

        public void Blur()
        {
            GameObject selected = CurrentSelection();
            if (IsMine(selected))
                _remembered = selected;

            _focused = false;
            Refresh();
        }

        private void FinishShow()
        {
            _showFinished = true;
            Refresh();
        }

        private void FinishHide() => gameObject.SetActive(false);

        private void Refresh() => Group.interactable = IsInteractable;

        private void Select()
        {
            EventSystem events = EventSystem.current;
            if (events == null)
                return;

            GameObject target = _remembered != null && _remembered.activeInHierarchy ? _remembered
                : _firstSelected != null ? _firstSelected.gameObject : null;
            events.SetSelectedGameObject(target);
        }

        // A hidden screen keeps no selection: Submit in the game would land on a switched-off button
        private void DropSelection()
        {
            if (IsMine(CurrentSelection()))
                EventSystem.current.SetSelectedGameObject(null);
        }

        private static GameObject CurrentSelection()
        {
            EventSystem events = EventSystem.current;
            return events != null ? events.currentSelectedGameObject : null;
        }

        private bool IsMine(GameObject selected) => selected != null && selected.transform.IsChildOf(transform);
    }
}
```

`Runtime/FadeTransition.cs` целиком:

```csharp
using System;
using UnityEngine;

namespace ScreenNavigation
{
    /// <summary>
    /// Fades the CanvasGroup alpha in or out over Duration seconds of unscaled time. A screen switched off becomes
    /// transparent, so every show fades in from zero. Duration 0 — at once.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FadeTransition : ScreenTransition
    {
        [Tooltip("Seconds of unscaled time; 0 — at once")]
        [SerializeField] private float _duration = 0.15f;

        private CanvasGroup _canvasGroup;
        private float _target;
        private Action _done;
        private bool _playing;

        public float Duration => _duration;
        public bool IsPlaying => _playing;

        private CanvasGroup Group => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        private void Awake() => Group.alpha = 0f;

        private void OnDisable() => Group.alpha = 0f;

        private void OnValidate()
        {
            if (_duration < 0f)
                _duration = 0f;
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Setup for objects built in code (tests); prefabs set Duration in the Inspector.</summary>
        public void Configure(float duration) => _duration = Mathf.Max(0f, duration);

        public override void Play(bool show, Action done)
        {
            _target = show ? 1f : 0f;
            _done = done;
            _playing = true;
            Tick(0f);
        }

        /// <summary>Advances the fade by dt seconds: Update passes unscaled time, tests — their own step.</summary>
        public void Tick(float dt)
        {
            if (!_playing)
                return;

            float step = _duration > 0f ? dt / _duration : 1f;
            Group.alpha = Mathf.MoveTowards(Group.alpha, _target, step);
            if (Group.alpha != _target)
                return;

            _playing = false;
            Action done = _done;
            _done = null;
            done?.Invoke();
        }
    }
}
```

`Runtime/SelectOnPointerEnter.cs` целиком:

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScreenNavigation
{
    /// <summary>
    /// Mouse hover selects the button, so mouse, keyboard and gamepad share one selection and one look.
    /// </summary>
    public sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
    {
        private Selectable _selectable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable == null)
                _selectable = GetComponent<Selectable>();

            EventSystem events = EventSystem.current;
            if (_selectable != null && _selectable.IsInteractable() && events != null)
                events.SetSelectedGameObject(gameObject);
        }
    }
}
```

- [ ] **Шаг 6: Green.** `Refresh`, консоль, `Run All Tests` — тесты модуля зелёные, остальное как на шаге 0 задачи 1.

- [ ] **Шаг 7: README модуля** — `Assets/Game/Scripts/Modules/ScreenStack/README.md`:

````markdown
# Screen Stack

Навигация меню для Unity UI: стек экранов, ввод — только у верхнего, оверлей оставляет нижние видимыми, «назад» —
`Pop`. Стек — обычный C# за контрактом `IScreen`; `UIScreen` реализует его на uGUI с фокусом для клавиатуры и геймпада.

Модуль самостоятельный: не зависит ни от игры, ни от сторонних плагинов, только Unity и uGUI (`com.unity.ugui`).
Ввод не читает — кнопку «назад» передаёт владелец стека. Проверено на Unity 6000.5; нужен C# 9 (Unity 2021.3+).

## Состав

```
ScreenStack/
├── Runtime/        (сборка ScreenStack, namespace ScreenNavigation)
│   ├── IScreen              — контракт экрана для стека
│   ├── ScreenStack          — стек: Push, Pop, Back, Replace, Clear, Changed
│   ├── UIScreen             — экран на uGUI: CanvasGroup, фокус, запоминание выделения, переход
│   ├── ScreenTransition     — база анимации появления и исчезновения
│   ├── FadeTransition       — затухание альфы на unscaled time
│   └── SelectOnPointerEnter — наведение мыши выделяет кнопку
└── Tests/
    ├── Editor/   (сборка ScreenStack.Tests)          — правила стека на фейковых экранах
    └── Runtime/  (сборка ScreenStack.Tests.Runtime)  — UIScreen, FadeTransition, SelectOnPointerEnter (Play Mode)
```

## Быстрый старт

```csharp
using System;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.UI;

// Префаб экрана: CanvasGroup, кнопки, по желанию FadeTransition; в префабе выключен
public sealed class PauseScreen : UIScreen
{
    public event Action OnResume;

    [SerializeField] private Button _resume;

    private void OnEnable() => _resume.onClick.AddListener(HandleResume);
    private void OnDisable() => _resume.onClick.RemoveListener(HandleResume);
    private void HandleResume() => OnResume?.Invoke();
}

// Владелец стека — оркестратор: решает, что делают кнопки экранов
public sealed class Menus : MonoBehaviour
{
    [SerializeField] private PauseScreen _pause;

    private readonly ScreenStack _stack = new();

    private void OnEnable() => _pause.OnResume += HandleResume;
    private void OnDisable() => _pause.OnResume -= HandleResume;

    public void OpenPause() => _stack.Push(_pause);
    public void Back() => _stack.Back();             // по кнопке «назад» (Cancel)

    private void HandleResume() => _stack.Clear();
}
```

## Кто за что отвечает

- **Стек** — порядок экранов, видимость, кто получает ввод. Не знает UI-систему, ввод не читает.
- **Экран** — показ и скрытие, интерактивность, выделение. Не знает ни стек, ни игру: наружу — события.
- **Владелец стека** (оркестратор) — читает «назад», связывает события экранов со стеком и игрой.

## Контракт

**IScreen** — три пары: `Open`/`Close` — вход и выход из стека, `Show`/`Hide` — видимость, `Focus`/`Blur` — ввод.
`IsOverlay` — экраны под ним остаются видимыми. `CanGoBack = false` — `Back()` экран не закрывает.

**ScreenStack**

- Экран виден, если он верхний или все экраны над ним — оверлеи. Ввод — только у верхнего.
- Порядок вызовов в операции: `Blur` старого верхнего → `Open` вошедшего → `Hide` (сверху вниз) → `Close` вышедших →
  `Show` (снизу вверх) → `Focus` нового верхнего → `Changed`. `Show`/`Hide` — только тем, у кого видимость поменялась.
- `Push` — наверх; `Pop` — снять верхний (`false` на пустом); `Back` — `Pop`, если верхний `CanGoBack`;
  `Replace` — заменить верхний за одну операцию (нижний не мигает); `Clear` — закрыть всё.
- `Changed` — после каждой операции, которая изменила стек; из обработчика операции разрешены.
- Ошибки: `null` — `ArgumentNullException`; экран уже в стеке — `InvalidOperationException`; операция стека из колбэка
  экрана — `InvalidOperationException`.

**UIScreen**

- В префабе выключен; `Show` включает объект, ставит его последним среди соседей и играет переход; `Hide` снимает
  интерактивность и выделение внутри экрана, играет переход назад и выключает объект.
- Кнопки нажимаются, только когда экран в фокусе и появление закончено (`IsInteractable`).
- `Focus` выделяет кнопку, запомненную при `Blur`, иначе `_firstSelected`; `Open`/`Close` забывают её. Выделение
  потерялось при фокусе — экран возвращает его.
- `Configure(...)` — настройка экрана, собранного в коде. Unity-колбэк базы — `protected virtual Update`, наследник
  зовёт `base.Update()`.

**FadeTransition** — альфа `CanvasGroup` за `Duration` секунд unscaled time; выключенный объект прозрачен, поэтому
каждое появление — с нуля; `Tick(dt)` — шаг вручную; новый `Play` прерывает старый, его `done` не вызывается.

**SelectOnPointerEnter** — наведение мыши выделяет интерактивную кнопку.

## Чего нет (намеренно)

Загрузки экранов по пути и Addressables, асинхронного жизненного цикла, вкладок без истории, очереди окон, чтения ввода,
блокатора на время перехода (кнопки и так неактивны до конца появления), UI Toolkit (контракт `IScreen` его допускает).

## Тесты

`ScreenStack.Tests` (EditMode): порядок вызовов во всех операциях, видимость с оверлеями, `Back` и `CanGoBack`,
`Replace` без мигания, `Changed`, ошибки и вызовы из колбэков. `ScreenStack.Tests.Runtime` (PlayMode): выделение
при фокусе и после возврата, сброс при закрытии, интерактивность после перехода, выключение и снятие выделения при
скрытии, порядок отрисовки, возврат потерянного выделения, переход на паузе игры, наведение мыши. Запуск —
*Window → General → Test Runner*.
````

- [ ] **Шаг 8: коммит.**

```bash
git add Assets/Game/Scripts/Modules/ScreenStack
git commit -m "Add uGUI screens to ScreenStack" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Задача 3: сервис паузы

**Файлы:**
- Создать: `Assets/Game/Scripts/GameSystem/Services/IGamePause.cs`, `Assets/Game/Scripts/GameSystem/Services/GamePause.cs`
- Изменить: `Assets/Game/Scripts/GameSystem/Services/GameServices.cs`
- Изменить: `Assets/Game/Tests/PlayMode/FakeAudioSystem.cs`
- Создать (через Unity): `Assets/Game/Tests/PlayMode/Scenes/EmptyTestScene.unity`
- Тест: `Assets/Game/Tests/PlayMode/GamePauseTests.cs`; изменить `Assets/Game/Tests/PlayMode/GameServicesTests.cs`

**Интерфейсы:**
- Использует: `AudioEngine.IAudioSystem` (`Pause()`, `Resume()`: `AudioSystem` выключает себя и ставит на паузу все
  играющие события), `ServiceLocator`.
- Отдаёт (задачам 4, 7): `Gameplay.IGamePause { event Action<bool> OnPauseChanged; bool IsPaused { get; } void Pause(); void Resume(); }`;
  `Gameplay.GamePause : IGamePause, IDisposable` с конструктором `GamePause(IAudioSystem audio)` (`null` допустим);
  `GameServices` регистрирует его как `IGamePause`. `FakeAudioSystem`: `int PauseCalls`, `int ResumeCalls`,
  `bool IsPaused`.

- [ ] **Шаг 1: заглушки.** `Scripts/GameSystem/Services/IGamePause.cs` — сразу целиком:

```csharp
using System;

namespace Gameplay
{
    /// <summary>
    /// Game-wide pause: stops game time (Time.timeScale) and game sounds. Owned by GameServices; the UI pauses while
    /// a menu is open, input controllers go quiet while paused.
    /// </summary>
    public interface IGamePause
    {
        /// <summary>True — the game has just been paused, false — resumed.</summary>
        event Action<bool> OnPauseChanged;

        bool IsPaused { get; }

        /// <summary>Does nothing when already paused.</summary>
        void Pause();

        /// <summary>Does nothing when not paused.</summary>
        void Resume();
    }
}
```

`Scripts/GameSystem/Services/GamePause.cs` — заглушка:

```csharp
using System;
using AudioEngine;

namespace Gameplay
{
    public sealed class GamePause : IGamePause, IDisposable
    {
        public event Action<bool> OnPauseChanged;

        public GamePause(IAudioSystem audio)
        {
        }

        public bool IsPaused => false;

        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public void Dispose()
        {
        }
    }
}
```

В `Assets/Game/Tests/PlayMode/FakeAudioSystem.cs` `<summary>` заменить на
`/// <summary>Audio engine stand-in for tests: records every PlayEvent id and Pause/Resume calls, everything else is not supported.</summary>`,
в секцию `// ---------- Recorded ----------` (после последнего `PlayEvent`) добавить:

```csharp
        public int PauseCalls { get; private set; }

        public int ResumeCalls { get; private set; }

        public bool IsPaused => PauseCalls > ResumeCalls;

        public void Pause() => PauseCalls++;

        public void Resume() => ResumeCalls++;
```

а из секции `// ---------- Not supported ----------` удалить строки `public void Pause() => throw NotSupported();`
и `public void Resume() => throw NotSupported();`.

- [ ] **Шаг 2: пустая тестовая сцена** — `Unity_RunCommand` (открытые сцены сохранены):

```csharp
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    private const string FOLDER = "Assets/Game/Tests/PlayMode/Scenes";
    private const string SCENE_PATH = FOLDER + "/EmptyTestScene.unity";

    public void Execute(ExecutionResult result)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SCENE_PATH) != null)
        {
            result.Log("{0} already exists", SCENE_PATH);
            return;
        }

        if (!AssetDatabase.IsValidFolder(FOLDER))
            AssetDatabase.CreateFolder("Assets/Game/Tests/PlayMode", "Scenes");

        // Additive: the scenes open in the editor stay as they are
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        if (!EditorSceneManager.SaveScene(scene, SCENE_PATH))
            throw new InvalidOperationException($"Could not save {SCENE_PATH}");

        EditorSceneManager.CloseScene(scene, true);
        result.Log("Created {0}", SCENE_PATH);
    }
}
```

- [ ] **Шаг 3: тесты.** `Assets/Game/Tests/PlayMode/GamePauseTests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Gameplay.Tests
{
    /// <summary>Game pause: time scale, audio, events, resume on scene load and on dispose.</summary>
    public class GamePauseTests
    {
        private const string EMPTY_SCENE = "Assets/Game/Tests/PlayMode/Scenes/EmptyTestScene.unity";

        private FakeAudioSystem _audio;
        private GamePause _pause;

        [SetUp]
        public void SetUp()
        {
            _audio = new FakeAudioSystem();
            _pause = new GamePause(_audio);
        }

        [TearDown]
        public void TearDown()
        {
            _pause.Dispose();
            Time.timeScale = 1f;
        }

        [Test]
        public void GamePause_Pause_StopsTimeAndAudio()
        {
            _pause.Pause();

            Assert.IsTrue(_pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(_audio.IsPaused);
        }

        [Test]
        public void GamePause_Resume_RestoresTimeAndAudio()
        {
            _pause.Pause();
            Assert.IsTrue(_pause.IsPaused, "precondition: paused");

            _pause.Resume();

            Assert.IsFalse(_pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(_audio.IsPaused);
        }

        [Test]
        public void GamePause_PauseTwice_ActsOnce()
        {
            var changes = new List<bool>();
            _pause.OnPauseChanged += changes.Add;

            _pause.Pause();
            _pause.Pause();

            CollectionAssert.AreEqual(new[] { true }, changes);
            Assert.AreEqual(1, _audio.PauseCalls);
        }

        [Test]
        public void GamePause_ResumeWhilePlaying_DoesNothing()
        {
            var changes = new List<bool>();
            _pause.OnPauseChanged += changes.Add;

            _pause.Resume();

            CollectionAssert.IsEmpty(changes);
            Assert.AreEqual(0, _audio.ResumeCalls);
        }

        [Test]
        public void GamePause_Dispose_RestoresTimeWithoutTouchingAudio()
        {
            _pause.Pause();
            Assert.IsTrue(_pause.IsPaused, "precondition: paused");

            _pause.Dispose();

            Assert.IsFalse(_pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0, _audio.ResumeCalls, "the audio engine dies together with [Services]");
        }

        [Test]
        public void GamePause_WithoutAudio_PausesTime()
        {
            var pause = new GamePause(null);

            pause.Pause();

            Assert.AreEqual(0f, Time.timeScale);
            pause.Dispose();
        }

        [UnityTest]
        public IEnumerator GamePause_SceneLoaded_Resumes()
        {
#if UNITY_EDITOR
            _pause.Pause();
            Assert.IsTrue(_pause.IsPaused, "precondition: paused");

            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(EMPTY_SCENE,
                new LoadSceneParameters(LoadSceneMode.Additive));

            Assert.IsFalse(_pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(EMPTY_SCENE));
#else
            Assert.Ignore("Loads a scene asset by path: editor only");
            yield break;
#endif
        }
    }
}
```

В `GameServicesTests.cs`: в `TearDown` последней строкой добавить `Time.timeScale = 1f;`, в конец класса — тесты:

```csharp
        [Test]
        public void GameServices_Awake_RegistersGamePause()
        {
            Spawn();

            Assert.IsFalse(ServiceLocator.Get<IGamePause>().IsPaused);
        }

        [Test]
        public void GameServices_Destroyed_UnregistersGamePause()
        {
            GameObject services = Spawn();
            Assert.IsTrue(ServiceLocator.TryGet(out IGamePause _), "precondition: the pause is registered");

            Object.DestroyImmediate(services);

            Assert.IsFalse(ServiceLocator.TryGet(out IGamePause _));
        }

        [Test]
        public void GameServices_DestroyedWhilePaused_RestoresTime()
        {
            GameObject services = Spawn();
            ServiceLocator.Get<IGamePause>().Pause();

            Object.DestroyImmediate(services);

            Assert.AreEqual(1f, Time.timeScale);
        }
```

- [ ] **Шаг 4: Red.** `Refresh`, консоль, `Run PlayMode Tests`. Ожидание: падают `GamePauseTests` (кроме
  `ResumeWhilePlaying` — заглушка ничего не делает, он зелёный уже сейчас) и три новых `GameServicesTests`
  («Service IGamePause is not registered» / `timeScale` не 0).

- [ ] **Шаг 5: реализация.** `Scripts/GameSystem/Services/GamePause.cs` целиком:

```csharp
using System;
using AudioEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay
{
    /// <summary>
    /// The only owner of Time.timeScale: 0 while paused, 1 otherwise. Pauses and resumes the audio engine (optional —
    /// tests run without it). Loading any scene resumes, so a pause never outlives the scene that opened it.
    /// </summary>
    public sealed class GamePause : IGamePause, IDisposable
    {
        public event Action<bool> OnPauseChanged;

        private readonly IAudioSystem _audio;
        private bool _disposed;

        public GamePause(IAudioSystem audio)
        {
            _audio = audio;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            Time.timeScale = 0f;
            _audio?.Pause();
            OnPauseChanged?.Invoke(true);
        }

        public void Resume() => Resume(true);

        /// <summary>Restores time without touching the audio engine: it is destroyed together with [Services].</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Resume(false);
        }

        private void Resume(bool resumeAudio)
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            Time.timeScale = 1f;
            if (resumeAudio)
                _audio?.Resume();
            OnPauseChanged?.Invoke(false);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Resume();
    }
}
```

`Scripts/GameSystem/Services/GameServices.cs` целиком:

```csharp
using AudioEngine;
using ServiceLocation;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Root of the [Services] prefab and the only place that registers game-wide services in the ServiceLocator.
    /// The prefab sits in every playable scene: the first instance survives scene loads, a copy in the next scene
    /// finds it registered and removes itself before its services wake up. A new service is a field here plus its
    /// lines in Awake and OnDestroy; plain C# services (GamePause) are created here and disposed in OnDestroy.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class GameServices : MonoBehaviour
    {
        [SerializeField] private AudioSystem _audioSystem;

        private GamePause _gamePause;

        private void Awake()
        {
            if (ServiceLocator.TryGet(out GameServices _))
            {
                // Switched off first: the copy's AudioSystem must not load banks and build pools just to die
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            ServiceLocator.Register(this);
            ServiceLocator.Register<IAudioSystem>(_audioSystem);
            _gamePause = new GamePause(_audioSystem);
            ServiceLocator.Register<IGamePause>(_gamePause);
        }

        private void OnDestroy()
        {
            // Removes only what this instance registered: a dying copy leaves the live services alone
            if (_gamePause != null)
            {
                ServiceLocator.Unregister<IGamePause>(_gamePause);
                _gamePause.Dispose();
            }

            ServiceLocator.Unregister<IAudioSystem>(_audioSystem);
            ServiceLocator.Unregister(this);
        }
    }
}
```

- [ ] **Шаг 6: Green.** `Refresh`, консоль, `Run All Tests` — новые тесты зелёные, старые не сломались
  (`AudioServiceTests` не трогают `Pause`/`Resume` фейка).

- [ ] **Шаг 7: коммит.**

```bash
git add Assets/Game/Scripts/GameSystem/Services Assets/Game/Tests/PlayMode/FakeAudioSystem.cs Assets/Game/Tests/PlayMode/GamePauseTests.cs Assets/Game/Tests/PlayMode/GamePauseTests.cs.meta Assets/Game/Tests/PlayMode/GameServicesTests.cs Assets/Game/Tests/PlayMode/Scenes.meta Assets/Game/Tests/PlayMode/Scenes
git commit -m "Add game pause service" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Задача 4: ввод игрока на паузе

**Файлы:**
- Изменить: `Assets/Game/Scripts/Gameplay/Character/Controllers/PlayerInputController.cs` (целиком, комментарии — на английский)
- Тест: `Assets/Game/Tests/PlayMode/PauseInputTests.cs`

**Интерфейсы:**
- Использует: `IGamePause`, `GamePause` (задача 3); `CharacterTestRig` (`SpawnGrounded`, `Player`, `Run.MoveInput`,
  `InvokeUpdate`, `Step`); `InputTestFixture`.
- Отдаёт (задаче 7): `PlayerInputController` выключает карту `Player` на паузе и включает после неё; пока карта
  выключена, нажатия не засчитываются.

- [ ] **Шаг 1: тесты** — `Assets/Game/Tests/PlayMode/PauseInputTests.cs`:

```csharp
using NUnit.Framework;
using ServiceLocation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Tests
{
    /// <summary>Player input while the game is paused: silent during the pause, back after it.</summary>
    public class PauseInputTests : CharacterTestBase
    {
        private InputTestFixture _input;
        private Keyboard _keyboard;
        private GamePause _pause;

        [SetUp]
        public void SetUpPause()
        {
            ServiceLocator.Clear();
            _input = new InputTestFixture();
            _input.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _pause = new GamePause(null);
            ServiceLocator.Register<IGamePause>(_pause);
            Rig.InvokeUpdate = true;
        }

        [TearDown]
        public void TearDownPause()
        {
            _pause.Dispose();
            ServiceLocator.Clear();
            _input.TearDown();
        }

        private void SpawnWithController()
        {
            Rig.AddFloor();
            Rig.SpawnGrounded(Vector2.zero);
            Rig.Player.transform.Find("Controllers").gameObject.SetActive(true);
        }

        private void Step(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Rig.Step();
                InputSystem.Update();
            }
        }

        [Test]
        public void PauseInput_Paused_KeyDDoesNotRun()
        {
            SpawnWithController();
            _pause.Pause();

            _input.Press(_keyboard.dKey);
            Step();

            Assert.AreEqual(0f, Rig.Run.MoveInput);
        }

        [Test]
        public void PauseInput_PausedWhileRunning_ReleasesRun()
        {
            SpawnWithController();
            _input.Press(_keyboard.dKey);
            Step();
            Assume.That(Rig.Run.MoveInput, Is.EqualTo(1f));

            _pause.Pause();

            Assert.AreEqual(0f, Rig.Run.MoveInput);
        }

        [Test]
        public void PauseInput_Resumed_RunsAgain()
        {
            SpawnWithController();
            _pause.Pause();
            _pause.Resume();

            _input.Press(_keyboard.dKey);
            Step();

            Assert.AreEqual(1f, Rig.Run.MoveInput);
        }

        [Test]
        public void PauseInput_EnabledDuringPause_WaitsForResume()
        {
            _pause.Pause();
            SpawnWithController();

            _input.Press(_keyboard.dKey);
            Step();
            Assert.AreEqual(0f, Rig.Run.MoveInput, "switched on during the pause");

            _pause.Resume();
            Step(2);

            Assert.AreEqual(1f, Rig.Run.MoveInput, "a key held through the pause counts after it");
        }
    }
}
```

- [ ] **Шаг 2: Red.** `Refresh`, консоль, `Run PlayMode Tests`. Ожидание: падают `Paused_KeyDDoesNotRun`,
  `PausedWhileRunning_ReleasesRun`, `EnabledDuringPause_WaitsForResume` (первая проверка); `Resumed_RunsAgain` зелёный.

- [ ] **Шаг 3: реализация** — `PlayerInputController.cs` целиком:

```csharp
using ServiceLocation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    /// <summary>
    /// Player input (Input System, keyboard + gamepad) → calls to the movement components. Buffers and timings live
    /// in the components; the controller only turns presses into calls. Actions are added as mechanics appear.
    /// While the game is paused (IGamePause, optional) the Player map is off: a press made during the pause is not
    /// counted after it, so the button that closes the menu does not jump.
    /// </summary>
    public class PlayerInputController : MonoBehaviour
    {
        private const string MAP = "Player";
        private const float DOWN_THRESHOLD = -0.5f;

        [Header("Input")]
        [SerializeField] private InputActionAsset _actions;

        [Header("Components")]
        [SerializeField] private RunComponent _run;
        [SerializeField] private JumpComponent _jump;
        [SerializeField] private DashComponent _dash;
        [SerializeField] private SmashComponent _smash;
        [SerializeField] private WallComponent _wall;
        [SerializeField] private CrouchComponent _crouch;
        [SerializeField] private LedgeComponent _ledge;
        [SerializeField] private LadderComponent _ladder;
        [SerializeField] private AttackComponent _attack;

        private InputActionAsset _runtimeActions;
        private InputActionMap _map;
        private InputAction _move;
        private InputAction _jumpAction;
        private InputAction _dashAction;
        private InputAction _smashAction;
        private InputAction _attackAction;
        private IGamePause _pause;

        private void Awake()
        {
            // Own copy of the asset: a shared asset switched on and off by several instances
            // (respawn, a second player) breaks the state of the action map
            _runtimeActions = Instantiate(_actions);
            _map = _runtimeActions.FindActionMap(MAP, true);
            _move = _map.FindAction("Move", true);
            _jumpAction = _map.FindAction("Jump", true);
            _dashAction = _map.FindAction("Dash", true);
            _smashAction = _map.FindAction("Smash", true);
            _attackAction = _map.FindAction("Attack", true);
        }

        private void OnEnable()
        {
            if (ServiceLocator.TryGet(out _pause))
                _pause.OnPauseChanged += HandlePauseChanged;

            if (_pause == null || !_pause.IsPaused)
                _map.Enable();
        }

        private void OnDisable()
        {
            if (_pause != null)
            {
                _pause.OnPauseChanged -= HandlePauseChanged;
                _pause = null;
            }

            ReleaseInput();
        }

        private void OnDestroy() => Destroy(_runtimeActions);

        private void Update()
        {
            Vector2 move = _move.ReadValue<Vector2>();
            bool down = move.y < DOWN_THRESHOLD;
            _run.SetInput(move.x);
            _jump.SetFastFall(down);
            _crouch.SetCrouchInput(down);
            _wall.SetVerticalInput(move.y);
            _ledge.SetVerticalInput(move.y);
            _ladder.SetVerticalInput(move.y);

            if (_jumpAction.WasPressedThisFrame())
            {
                _jump.Press();
                _wall.PressJump();
                _ledge.PressJump();
                _ladder.PressJump();
            }

            if (_jumpAction.WasReleasedThisFrame())
                _jump.Release();

            if (_dashAction.WasPressedThisFrame())
                _dash.TryDash(move.x);

            // Smash on the keyboard is a double S (MultiTap): it counts as a press once the interaction is performed
            if (_smashAction.WasPerformedThisFrame())
                _smash.TrySmash();

            if (_attackAction.WasPressedThisFrame())
                _attack.Press();
        }

        private void HandlePauseChanged(bool paused)
        {
            if (paused)
                ReleaseInput();
            else
                _map.Enable();
        }

        private void ReleaseInput()
        {
            _map.Disable();
            _run.SetInput(0f);
            _jump.SetFastFall(false);
            _crouch.SetCrouchInput(false);
        }
    }
}
```

  Перед заменой сверить с текущим файлом: если этап урона или кто-то другой добавил действия или поля, перенести их
  в новый текст без изменений.

- [ ] **Шаг 4: Green.** `Refresh`, консоль, `Run All Tests` — `PauseInputTests` и `PlayerInputControllerTests`
  зелёные (без сервиса паузы контроллер работает как раньше).

- [ ] **Шаг 5: коммит.**

```bash
git add Assets/Game/Scripts/Gameplay/Character/Controllers/PlayerInputController.cs Assets/Game/Tests/PlayMode/PauseInputTests.cs Assets/Game/Tests/PlayMode/PauseInputTests.cs.meta
git commit -m "Silence player input while paused" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Задача 5: карта UI и общий EventSystem

**Файлы:**
- Изменить: `Assets/Game/Input/PlayerControls.inputactions` — карта `UI`
- Изменить (через Unity): `Assets/Game/Prefabs/Services.prefab` — дочерний `EventSystem`
- Изменить (через Unity): `Assets/Game/Scenes/Game.unity` — удалить свой `EventSystem`
- Изменить: `Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef` — `+ UnityEngine.UI`
- Тест: `Assets/Game/Tests/PlayMode/GameServicesTests.cs` (+2)

**Интерфейсы:**
- Использует: `Services.prefab` и `GameServices` (задача 3).
- Отдаёт (задачам 6–8): карта `UI` в `PlayerControls`: `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `ScrollWheel`,
  `Pause` (у каждого действия импортёр создаёт под-ассет `InputActionReference`); дочерний объект `EventSystem`
  префаба `Services` с `EventSystem` и `InputSystemUIInputModule` (действия `PlayerControls`/`UI`,
  `deselectOnBackgroundClick = false`).

- [ ] **Шаг 1: тесты.** В `Game.Tests.PlayMode.asmdef` массив `references` — целиком:

```json
    "references": [
        "Game",
        "SpriteAnimator",
        "AudioEngine",
        "ServiceLocator",
        "UnityEngine.UI",
        "Unity.InputSystem",
        "Unity.InputSystem.TestFramework",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

В `GameServicesTests.cs` добавить `using UnityEngine.EventSystems;`, `using UnityEngine.InputSystem;`,
`using UnityEngine.InputSystem.UI;` и в конец класса:

```csharp
        [Test]
        public void Services_EventSystem_UsesUIMapOfPlayerControls()
        {
            GameObject services = Spawn();
            var module = services.GetComponentInChildren<InputSystemUIInputModule>();

            Assert.IsNotNull(module, "[Services] must carry the only EventSystem");
            Assert.AreEqual("PlayerControls", module.actionsAsset.name);
            AssertUIAction(module.move, "Navigate");
            AssertUIAction(module.submit, "Submit");
            AssertUIAction(module.cancel, "Cancel");
            AssertUIAction(module.point, "Point");
            AssertUIAction(module.leftClick, "Click");
            AssertUIAction(module.scrollWheel, "ScrollWheel");
            Assert.IsFalse(module.deselectOnBackgroundClick, "a mouse click on empty space keeps the gamepad selection");
        }

        [UnityTest]
        public IEnumerator GameServices_SecondCopy_LeavesOneEventSystem()
        {
            Spawn();
            Spawn();

            yield return null;

            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
        }

        private static void AssertUIAction(InputActionReference reference, string name)
        {
            Assert.IsNotNull(reference, $"UI/{name} is not assigned");
            Assert.IsNotNull(reference.action, $"UI/{name} does not resolve");
            Assert.AreEqual("UI", reference.action.actionMap.name);
            Assert.AreEqual(name, reference.action.name);
        }
```

- [ ] **Шаг 2: Red.** `Refresh`, консоль, `Run PlayMode Tests`: `Services_EventSystem_UsesUIMapOfPlayerControls`
  падает («[Services] must carry the only EventSystem»), `SecondCopy_LeavesOneEventSystem` — тоже: `EventSystem`
  пока нет ни одного (ожидалось 1, было 0).

- [ ] **Шаг 3: карта `UI`.** В `Assets/Game/Input/PlayerControls.inputactions` в массиве `maps` после объекта карты
  `Player` (его закрывающая `}` стоит перед `],` массива `maps`) поставить запятую и добавить объект:

```json
        {
            "name": "UI",
            "id": "bea0a9f0-73d2-4e8c-be3d-6b307ab0aaba",
            "actions": [
                {
                    "name": "Navigate",
                    "type": "PassThrough",
                    "id": "100d3f93-9233-49bd-8900-a5e170f2c254",
                    "expectedControlType": "Vector2",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "Submit",
                    "type": "Button",
                    "id": "ccbb2658-6607-4ccd-bfa7-cecebb13594e",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "Cancel",
                    "type": "Button",
                    "id": "a5dc13c5-fb81-4b06-b1bf-688d545efda0",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "Point",
                    "type": "PassThrough",
                    "id": "33272ecc-1ad9-4162-8a4c-5fd3d1a1f37c",
                    "expectedControlType": "Vector2",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": true
                },
                {
                    "name": "Click",
                    "type": "PassThrough",
                    "id": "f13b339a-285a-4970-a1fd-094eacddbda1",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": true
                },
                {
                    "name": "ScrollWheel",
                    "type": "PassThrough",
                    "id": "a865d47f-cf5c-4885-97ef-da37a2c48ae7",
                    "expectedControlType": "Vector2",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "Pause",
                    "type": "Button",
                    "id": "889eb56b-ba9e-4539-8fd1-6b2891b8b928",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                }
            ],
            "bindings": [
                {
                    "name": "WASD",
                    "id": "24c0dd5a-0a4f-414c-94ee-5bb2d8a9803f",
                    "path": "2DVector",
                    "interactions": "",
                    "processors": "",
                    "groups": "",
                    "action": "Navigate",
                    "isComposite": true,
                    "isPartOfComposite": false
                },
                {
                    "name": "up",
                    "id": "757267f5-d4c7-45e3-91ef-8978067cf0c1",
                    "path": "<Keyboard>/w",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "down",
                    "id": "3a88480d-0964-46c7-9c8f-0a86b955e7e2",
                    "path": "<Keyboard>/s",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "left",
                    "id": "43c62258-f08f-445e-9c3f-e126188cb70c",
                    "path": "<Keyboard>/a",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "right",
                    "id": "a982685e-3c22-483d-bd7d-d1f00a02e29c",
                    "path": "<Keyboard>/d",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "Arrows",
                    "id": "701e5d72-d260-488e-94c4-7936f07b8b04",
                    "path": "2DVector",
                    "interactions": "",
                    "processors": "",
                    "groups": "",
                    "action": "Navigate",
                    "isComposite": true,
                    "isPartOfComposite": false
                },
                {
                    "name": "up",
                    "id": "35790a69-fb2d-48b0-a74b-abb8ca6c3b3c",
                    "path": "<Keyboard>/upArrow",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "down",
                    "id": "56807774-27b7-4d32-bb2a-a0c3b881521d",
                    "path": "<Keyboard>/downArrow",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "left",
                    "id": "9d756c33-07ca-4e17-84b0-ce18ef455538",
                    "path": "<Keyboard>/leftArrow",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "right",
                    "id": "30879ddf-9a96-4b1c-9568-cfa7605815ed",
                    "path": "<Keyboard>/rightArrow",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "",
                    "id": "15ea5a01-2a32-434a-8c19-4e8cd433edbb",
                    "path": "<Gamepad>/leftStick",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "76c4882f-4f3e-4177-850b-4934059c3ef1",
                    "path": "<Gamepad>/dpad",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Navigate",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "f37ea215-63a0-42fc-9e67-78766e26096b",
                    "path": "<Keyboard>/enter",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Submit",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "a4c47f4e-2266-45d5-9ee3-96b02d79c390",
                    "path": "<Keyboard>/numpadEnter",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Submit",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "1c6801bf-e540-487b-a870-ff140a8c20b2",
                    "path": "<Keyboard>/space",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Submit",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "3e6fa579-8a62-4a3e-b1d4-fc1119868815",
                    "path": "<Gamepad>/buttonSouth",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Submit",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "937babcd-22b2-4486-80b0-3e8d5e0362a9",
                    "path": "<Keyboard>/escape",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Cancel",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "aa67c5f5-6659-4394-b5bc-74ef3999ac83",
                    "path": "<Gamepad>/buttonEast",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Cancel",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "4faf77a6-c0ec-4acf-a087-ee7aa4b7e757",
                    "path": "<Mouse>/position",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Point",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "82707e55-e70e-498e-a3ad-0bffa85e13b4",
                    "path": "<Mouse>/leftButton",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Click",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "adb22377-316f-46f0-a555-24cd55ca0caa",
                    "path": "<Mouse>/scroll",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "ScrollWheel",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "eef8fc40-5922-47e4-92d5-144d7911a41b",
                    "path": "<Keyboard>/escape",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Pause",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "1484de5b-24f4-4b27-82f7-5b6736210066",
                    "path": "<Gamepad>/start",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Pause",
                    "isComposite": false,
                    "isPartOfComposite": false
                }
            ]
        }
```

  `Refresh`, консоль без ошибок импорта; в инспекторе ассета видны карты `Player` и `UI`.

- [ ] **Шаг 4: `EventSystem` в `[Services]`** — `Unity_RunCommand`:

```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

internal class CommandScript : IRunCommand
{
    private const string PREFAB_PATH = "Assets/Game/Prefabs/Services.prefab";
    private const string ACTIONS_PATH = "Assets/Game/Input/PlayerControls.inputactions";

    public void Execute(ExecutionResult result)
    {
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ACTIONS_PATH);
        if (asset == null)
            throw new InvalidOperationException($"No {ACTIONS_PATH}");

        // Serialized references must be the importer's visible sub-assets (the importer also makes hidden copies),
        // not runtime InputActionReference.Create
        InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(ACTIONS_PATH)
            .OfType<InputActionReference>()
            .Where(r => AssetDatabase.IsSubAsset(r) && (r.hideFlags & HideFlags.HideInHierarchy) == 0)
            .ToArray();

        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB_PATH);
        try
        {
            if (root.GetComponentInChildren<EventSystem>(true) != null)
                throw new InvalidOperationException("Services already has an EventSystem");

            var go = new GameObject("EventSystem");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();

            // Straight into the serialized fields: after actionsAsset the property setters keep a runtime reference
            // to the same action (SwapAction skips an equal action) and the prefab would save {fileID: 0}
            var serialized = new SerializedObject(module);
            Set(serialized, "m_ActionsAsset", asset);
            Set(serialized, "m_PointAction", Find(references, "Point"));
            Set(serialized, "m_LeftClickAction", Find(references, "Click"));
            Set(serialized, "m_ScrollWheelAction", Find(references, "ScrollWheel"));
            Set(serialized, "m_MoveAction", Find(references, "Navigate"));
            Set(serialized, "m_SubmitAction", Find(references, "Submit"));
            Set(serialized, "m_CancelAction", Find(references, "Cancel"));
            Set(serialized, "m_MiddleClickAction", null);
            Set(serialized, "m_RightClickAction", null);
            Set(serialized, "m_TrackedDevicePositionAction", null);
            Set(serialized, "m_TrackedDeviceOrientationAction", null);
            serialized.FindProperty("m_DeselectOnBackgroundClick").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
            result.Log("EventSystem added to {0}", PREFAB_PATH);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Set(SerializedObject serialized, string field, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"InputSystemUIInputModule has no field {field}");
        property.objectReferenceValue = value;
    }

    private static InputActionReference Find(InputActionReference[] references, string action)
    {
        InputActionReference found = references.FirstOrDefault(r =>
            r.action != null && r.action.actionMap.name == "UI" && r.action.name == action);
        if (found == null)
            throw new InvalidOperationException($"No UI/{action} reference in PlayerControls");
        return found;
    }
}
```

  Проверить: в `Services.prefab` дочерний `EventSystem`; в YAML префаба у `m_PointAction`, `m_MoveAction`,
  `m_SubmitAction`, `m_CancelAction`, `m_LeftClickAction`, `m_ScrollWheelAction` — `fileID` под-ассетов
  `PlayerControls` (не `{fileID: 0}`), `m_DeselectOnBackgroundClick: 0`.

- [ ] **Шаг 5: убрать `EventSystem` из `Game.unity`** — `Unity_RunCommand`:

```csharp
using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    private const string SCENE_PATH = "Assets/Game/Scenes/Game.unity";

    public void Execute(ExecutionResult result)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty)
                throw new InvalidOperationException($"Save or discard changes in {open.path} first");
        }

        string previous = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // The scene's own EventSystem is a root object; the one inside [Services] stays
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(r => r.GetComponent<EventSystem>() != null);
        if (old == null)
        {
            result.Log("{0} has no EventSystem of its own", SCENE_PATH);
        }
        else
        {
            result.DestroyObject(old);
            EditorSceneManager.SaveScene(scene);
            result.Log("Removed the EventSystem of {0}", SCENE_PATH);
        }

        if (!string.IsNullOrEmpty(previous) && previous != SCENE_PATH)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }
}
```

  Проверить поиском по `Assets/Game/Scenes/*.unity` строки `m_Name: EventSystem`: совпадений нет (в сценах
  `EventSystem` приходит только с префабом `[Services]`).

- [ ] **Шаг 6: Green.** `Run All Tests` — зелёные, включая два новых `GameServicesTests`.

- [ ] **Шаг 7: коммит.**

```bash
git add Assets/Game/Input/PlayerControls.inputactions Assets/Game/Prefabs/Services.prefab Assets/Game/Scenes/Game.unity Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef Assets/Game/Tests/PlayMode/GameServicesTests.cs
git commit -m "Add UI action map and shared EventSystem" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Задача 6: экраны паузы и подтверждения

**Файлы:**
- Изменить: `Assets/Game/Game.asmdef` — `+ ScreenStack`, `+ Unity.TextMeshPro`
- Изменить: `Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef` — `+ ScreenStack`
- Изменить: `Assets/Game/Tests/EditMode/Game.Tests.EditMode.asmdef` — `+ ScreenStack`, `UnityEngine.UI`, `Unity.InputSystem`
- Создать: `Assets/Game/Scripts/UI/PauseScreen.cs`, `Assets/Game/Scripts/UI/ConfirmDialog.cs`
- Создать (через Unity): `Assets/Game/Prefabs/UI/PauseScreen.prefab`, `Assets/Game/Prefabs/UI/ConfirmDialog.prefab`
- Тест: `Assets/Game/Tests/EditMode/UIPrefabTests.cs`, `Assets/Game/Tests/PlayMode/ScreenPrefabTests.cs`

**Интерфейсы:**
- Использует: `UIScreen`, `FadeTransition`, `SelectOnPointerEnter` (задача 2).
- Отдаёт (задаче 7): `Gameplay.PauseScreen : UIScreen` — события `OnResume`, `OnQuit`, поля `_resume`, `_quit`;
  `Gameplay.ConfirmDialog : UIScreen` — события `OnYes`, `OnNo`, `string Text`, `void SetText(string)`, поля
  `_message` (`TMP_Text`), `_yes`, `_no`. Префабы: `PauseScreen` (выключен; `Panel` → `Title`, `Resume`, `Quit`;
  `_firstSelected = Resume`, не оверлей), `ConfirmDialog` (выключен; `Panel` → `Message`, `Buttons` → `Yes`, `No`;
  `_firstSelected = No`, оверлей). У обоих — `FadeTransition`, у кнопок — `SelectOnPointerEnter`.

- [ ] **Шаг 1: ссылки сборок.** В `Assets/Game/Game.asmdef` к текущему списку `references` добавить `"ScreenStack"`
  и `"Unity.TextMeshPro"` (`UnityEngine.UI` там уже есть — его добавил этап урона). Ожидаемый вид:

```json
{
  "name": "Game",
  "references":[ "SpriteAnimator", "AudioEngine", "PrefabPool", "ServiceLocator", "ScreenStack", "Unity.InputSystem", "Unity.Cinemachine", "UnityEngine.UI", "Unity.TextMeshPro" ]
}
```

В `Game.Tests.PlayMode.asmdef` в `references` после `"ServiceLocator"` добавить `"ScreenStack"`. В
`Game.Tests.EditMode.asmdef` массив `references` — целиком:

```json
    "references": [
        "Game",
        "SpriteAnimator",
        "ScreenStack",
        "UnityEngine.UI",
        "Unity.InputSystem",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

- [ ] **Шаг 2: заглушки экранов** (поля есть — префабы собираются уже с ними, тесты падают на проверках).
  `Assets/Game/Scripts/UI/PauseScreen.cs`:

```csharp
using System;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public sealed class PauseScreen : UIScreen
    {
        public event Action OnResume;
        public event Action OnQuit;

        [Header("Buttons")]
        [SerializeField] private Button _resume;
        [SerializeField] private Button _quit;
    }
}
```

`Assets/Game/Scripts/UI/ConfirmDialog.cs`:

```csharp
using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public sealed class ConfirmDialog : UIScreen
    {
        public event Action OnYes;
        public event Action OnNo;

        [Header("Content")]
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button _yes;
        [SerializeField] private Button _no;

        public string Text => string.Empty;

        public void SetText(string text)
        {
        }
    }
}
```

- [ ] **Шаг 3: тесты.** `Assets/Game/Tests/EditMode/UIPrefabTests.cs`:

```csharp
using NUnit.Framework;
using ScreenNavigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Tests
{
    /// <summary>
    /// UI prefabs are wired. Screens wake up only when first shown, so a missing reference shows up here and not at
    /// the first pause in the game.
    /// </summary>
    public class UIPrefabTests
    {
        private const string PAUSE_PREFAB = "Assets/Game/Prefabs/UI/PauseScreen.prefab";
        private const string DIALOG_PREFAB = "Assets/Game/Prefabs/UI/ConfirmDialog.prefab";

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"No prefab at {path}");
            return prefab;
        }

        private static SerializedProperty Property(Object target, string field)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            Assert.IsNotNull(property, $"{target.GetType().Name} has no field {field}");
            return property;
        }

        private static Object Reference(Object target, string field) => Property(target, field).objectReferenceValue;

        private static bool Flag(Object target, string field) => Property(target, field).boolValue;

        [Test]
        public void PauseScreenPrefab_ButtonsAndFocusAssigned()
        {
            var screen = Load(PAUSE_PREFAB).GetComponent<PauseScreen>();

            Assert.IsNotNull(screen);
            Assert.IsNotNull(Reference(screen, "_resume"));
            Assert.IsNotNull(Reference(screen, "_quit"));
            Assert.AreSame(Reference(screen, "_resume"), Reference(screen, "_firstSelected"));
            Assert.IsNotNull(Reference(screen, "_transition"));
            Assert.IsFalse(Flag(screen, "_isOverlay"));
            Assert.IsTrue(Flag(screen, "_canGoBack"));
        }

        [Test]
        public void ConfirmDialogPrefab_OverlayWithNoSelected()
        {
            var dialog = Load(DIALOG_PREFAB).GetComponent<ConfirmDialog>();

            Assert.IsNotNull(dialog);
            Assert.IsNotNull(Reference(dialog, "_message"));
            Assert.IsNotNull(Reference(dialog, "_yes"));
            Assert.IsNotNull(Reference(dialog, "_no"));
            Assert.AreSame(Reference(dialog, "_no"), Reference(dialog, "_firstSelected"), "the safe answer first");
            Assert.IsNotNull(Reference(dialog, "_transition"));
            Assert.IsTrue(Flag(dialog, "_isOverlay"));
            Assert.IsTrue(Flag(dialog, "_canGoBack"));
        }

        [Test]
        public void ScreenPrefabs_StoredSwitchedOff()
        {
            Assert.IsFalse(Load(PAUSE_PREFAB).activeSelf);
            Assert.IsFalse(Load(DIALOG_PREFAB).activeSelf);
        }

        [Test]
        public void ScreenPrefabs_ButtonsSelectOnHover()
        {
            foreach (string path in new[] { PAUSE_PREFAB, DIALOG_PREFAB })
            {
                foreach (Button button in Load(path).GetComponentsInChildren<Button>(true))
                    Assert.IsNotNull(button.GetComponent<SelectOnPointerEnter>(), $"{path}: {button.name}");
            }
        }
    }
}
```

`Assets/Game/Tests/PlayMode/ScreenPrefabTests.cs`:

```csharp
using NUnit.Framework;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Tests
{
    /// <summary>The pause screen and confirm dialog prefabs: buttons raise their events, the dialog shows its question.</summary>
    public class ScreenPrefabTests
    {
        private const string PAUSE_PREFAB = "Assets/Game/Prefabs/UI/PauseScreen.prefab";
        private const string DIALOG_PREFAB = "Assets/Game/Prefabs/UI/ConfirmDialog.prefab";

        private EventSystem _events;
        private GameObject _canvas;

        [SetUp]
        public void SetUp()
        {
            _events = new GameObject("EventSystem").AddComponent<EventSystem>();
            _canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
            Object.DestroyImmediate(_events.gameObject);
        }

        private T Spawn<T>(string path) where T : Component
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"No prefab at {path}");
            return Object.Instantiate(prefab, _canvas.transform, false).GetComponent<T>();
#else
            Assert.Ignore("Loads prefabs by path: editor only");
            return null;
#endif
        }

        private static Button ButtonOf(Component screen, string name)
        {
            foreach (Button button in screen.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                    return button;
            }

            Assert.Fail($"{screen.name} has no button {name}");
            return null;
        }

        private static void Enter(UIScreen screen)
        {
            screen.Open();
            screen.Show();
            screen.Focus();
        }

        [Test]
        public void PauseScreen_Resume_RaisesOnResume()
        {
            var screen = Spawn<PauseScreen>(PAUSE_PREFAB);
            bool resumed = false;
            screen.OnResume += () => resumed = true;
            Enter(screen);

            ButtonOf(screen, "Resume").onClick.Invoke();

            Assert.IsTrue(resumed);
        }

        [Test]
        public void PauseScreen_Quit_RaisesOnQuit()
        {
            var screen = Spawn<PauseScreen>(PAUSE_PREFAB);
            bool quit = false;
            screen.OnQuit += () => quit = true;
            Enter(screen);

            ButtonOf(screen, "Quit").onClick.Invoke();

            Assert.IsTrue(quit);
        }

        [Test]
        public void PauseScreen_Opened_SelectsResume()
        {
            var screen = Spawn<PauseScreen>(PAUSE_PREFAB);

            Enter(screen);

            Assert.AreSame(ButtonOf(screen, "Resume").gameObject, _events.currentSelectedGameObject);
        }

        [Test]
        public void ConfirmDialog_SetText_ShowsQuestion()
        {
            var dialog = Spawn<ConfirmDialog>(DIALOG_PREFAB);

            dialog.SetText("Quit the game?");

            Assert.AreEqual("Quit the game?", dialog.Text);
        }

        [Test]
        public void ConfirmDialog_Opened_SelectsNo()
        {
            var dialog = Spawn<ConfirmDialog>(DIALOG_PREFAB);

            Enter(dialog);

            Assert.AreSame(ButtonOf(dialog, "No").gameObject, _events.currentSelectedGameObject);
        }

        [Test]
        public void ConfirmDialog_YesAndNo_RaiseTheirEvents()
        {
            var dialog = Spawn<ConfirmDialog>(DIALOG_PREFAB);
            int yes = 0;
            int no = 0;
            dialog.OnYes += () => yes++;
            dialog.OnNo += () => no++;
            Enter(dialog);

            ButtonOf(dialog, "Yes").onClick.Invoke();
            ButtonOf(dialog, "No").onClick.Invoke();

            Assert.AreEqual(1, yes);
            Assert.AreEqual(1, no);
        }
    }
}
```

- [ ] **Шаг 4: Red.** `Refresh`, консоль, `Run All Tests`: `UIPrefabTests` и `ScreenPrefabTests` падают «No prefab at …».

- [ ] **Шаг 5: реализация экранов.** `Assets/Game/Scripts/UI/PauseScreen.cs` целиком:

```csharp
using System;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    /// <summary>Pause menu: Resume and Quit. Only raises events — GameUI decides what they do.</summary>
    public sealed class PauseScreen : UIScreen
    {
        public event Action OnResume;
        public event Action OnQuit;

        [Header("Buttons")]
        [SerializeField] private Button _resume;
        [SerializeField] private Button _quit;

        private void OnEnable()
        {
            _resume.onClick.AddListener(HandleResume);
            _quit.onClick.AddListener(HandleQuit);
        }

        private void OnDisable()
        {
            _resume.onClick.RemoveListener(HandleResume);
            _quit.onClick.RemoveListener(HandleQuit);
        }

        private void HandleResume() => OnResume?.Invoke();

        private void HandleQuit() => OnQuit?.Invoke();
    }
}
```

`Assets/Game/Scripts/UI/ConfirmDialog.cs` целиком:

```csharp
using System;
using ScreenNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    /// <summary>
    /// A yes/no question over another screen (overlay). Back closes it like No; the question is set by the orchestrator
    /// before showing. Only raises events — the orchestrator decides what Yes does.
    /// </summary>
    public sealed class ConfirmDialog : UIScreen
    {
        public event Action OnYes;
        public event Action OnNo;

        [Header("Content")]
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button _yes;
        [SerializeField] private Button _no;

        public string Text => _message.text;

        public void SetText(string text) => _message.text = text;

        private void OnEnable()
        {
            _yes.onClick.AddListener(HandleYes);
            _no.onClick.AddListener(HandleNo);
        }

        private void OnDisable()
        {
            _yes.onClick.RemoveListener(HandleYes);
            _no.onClick.RemoveListener(HandleNo);
        }

        private void HandleYes() => OnYes?.Invoke();

        private void HandleNo() => OnNo?.Invoke();
    }
}
```

- [ ] **Шаг 6: префабы экранов** — `Refresh`, консоль, затем `Unity_RunCommand`. Объекты собираются в preview-сцене:
  открытые сцены не трогаются.

```csharp
using System;
using Gameplay;
using ScreenNavigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal class CommandScript : IRunCommand
{
    private const string FOLDER = "Assets/Game/Prefabs/UI";
    private const string PAUSE_PATH = FOLDER + "/PauseScreen.prefab";
    private const string DIALOG_PATH = FOLDER + "/ConfirmDialog.prefab";

    private static readonly Color Normal = new(0.22f, 0.22f, 0.25f, 1f);
    private static readonly Color Selected = new(0.95f, 0.62f, 0.12f, 1f);
    private static readonly Color Pressed = new(0.75f, 0.45f, 0.05f, 1f);

    public void Execute(ExecutionResult result)
    {
        if (!AssetDatabase.IsValidFolder(FOLDER))
            AssetDatabase.CreateFolder("Assets/Game/Prefabs", "UI");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PAUSE_PATH) != null
            || AssetDatabase.LoadAssetAtPath<GameObject>(DIALOG_PATH) != null)
            throw new InvalidOperationException("Screen prefabs already exist");

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            Save(BuildPauseScreen(preview), PAUSE_PATH, result);
            Save(BuildConfirmDialog(preview), DIALOG_PATH, result);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static GameObject BuildPauseScreen(Scene preview)
    {
        GameObject root = CreateScreenRoot("PauseScreen", new Color(0f, 0f, 0f, 0.6f), preview);
        var screen = root.AddComponent<PauseScreen>();
        var fade = root.AddComponent<FadeTransition>();

        RectTransform panel = CreatePanel(root.transform, "Panel", new Vector2(480f, 360f), null);
        AddColumn(panel.gameObject, 0, 24f);
        CreateLabel(panel, "Title", "PAUSED", 64f, 90f);
        Button resume = CreateButton(panel, "Resume", 80f);
        Button quit = CreateButton(panel, "Quit", 80f);

        SetReference(screen, "_resume", resume);
        SetReference(screen, "_quit", quit);
        SetReference(screen, "_firstSelected", resume);
        SetReference(screen, "_transition", fade);
        SetFlag(screen, "_isOverlay", false);
        SetFlag(screen, "_canGoBack", true);
        return root;
    }

    private static GameObject BuildConfirmDialog(Scene preview)
    {
        GameObject root = CreateScreenRoot("ConfirmDialog", new Color(0f, 0f, 0f, 0.5f), preview);
        var dialog = root.AddComponent<ConfirmDialog>();
        var fade = root.AddComponent<FadeTransition>();

        RectTransform panel = CreatePanel(root.transform, "Panel", new Vector2(760f, 320f),
            new Color(0.12f, 0.12f, 0.14f, 0.95f));
        AddColumn(panel.gameObject, 40, 32f);
        TextMeshProUGUI message = CreateLabel(panel, "Message", "Are you sure?", 40f, 120f);

        var row = new GameObject("Buttons", typeof(RectTransform));
        row.transform.SetParent(panel, false);
        ((RectTransform)row.transform).sizeDelta = new Vector2(0f, 80f);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 32f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;
        Button yes = CreateButton(row.transform, "Yes", 0f);
        Button no = CreateButton(row.transform, "No", 0f);

        SetReference(dialog, "_message", message);
        SetReference(dialog, "_yes", yes);
        SetReference(dialog, "_no", no);
        SetReference(dialog, "_firstSelected", no);
        SetReference(dialog, "_transition", fade);
        SetFlag(dialog, "_isOverlay", true);
        SetFlag(dialog, "_canGoBack", true);
        return root;
    }

    private static void Save(GameObject root, string path, ExecutionResult result)
    {
        root.SetActive(false);   // screens are stored switched off: only the stack shows them
        PrefabUtility.SaveAsPrefabAsset(root, path);
        result.Log("Created {0}", path);
    }

    private static GameObject CreateScreenRoot(string name, Color dim, Scene preview)
    {
        var root = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, preview);
        Stretch((RectTransform)root.transform);
        // Full name: MCP wraps the command in a namespace where Image is a namespace
        var background = root.AddComponent<UnityEngine.UI.Image>();
        background.color = dim;
        background.raycastTarget = true;   // the menu takes the clicks, nothing behind it reacts
        return root;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Color? color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        if (color.HasValue)
            go.AddComponent<UnityEngine.UI.Image>().color = color.Value;
        return rect;
    }

    private static void AddColumn(GameObject go, int padding, float spacing)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(0f, height);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    // The object's name is its text: tests find buttons by name
    private static Button CreateButton(Transform parent, string text, float height)
    {
        var resources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
        };
        GameObject go = TMP_DefaultControls.CreateButton(resources);
        go.name = text;
        go.transform.SetParent(parent, false);
        ((RectTransform)go.transform).sizeDelta = new Vector2(0f, height);

        var button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Normal;
        colors.highlightedColor = Selected;
        colors.selectedColor = Selected;
        colors.pressedColor = Pressed;
        colors.disabledColor = new Color(Normal.r, Normal.g, Normal.b, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        var label = go.GetComponentInChildren<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 36f;
        label.color = Color.white;

        go.AddComponent<SelectOnPointerEnter>();
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name} has no field {field}");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFlag(Object target, string field, bool value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name} has no field {field}");
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
```

  Проверить: префабы на диске, оба выключены; в `PauseScreen.prefab` кнопки `Resume`, `Quit` с подписями; в инспекторе
  `PauseScreen` и `ConfirmDialog` назначены все ссылки. `new GameObject` и `TMP_DefaultControls` создают объекты сначала
  в активной сцене, поэтому она становится dirty, хотя всё переезжает в preview-сцену: проверить, что в её корне нет
  лишних объектов, и перезагрузить её с диска (`EditorSceneManager.OpenScene(path, OpenSceneMode.Single)`), иначе
  Test Runner встанет на вопросе о сохранении.

- [ ] **Шаг 7: Green.** `Run All Tests` — `UIPrefabTests` и `ScreenPrefabTests` зелёные.

- [ ] **Шаг 8: коммит.**

```bash
git add Assets/Game/Game.asmdef Assets/Game/Tests/PlayMode/Game.Tests.PlayMode.asmdef Assets/Game/Tests/EditMode/Game.Tests.EditMode.asmdef Assets/Game/Scripts/UI/PauseScreen.cs Assets/Game/Scripts/UI/PauseScreen.cs.meta Assets/Game/Scripts/UI/ConfirmDialog.cs Assets/Game/Scripts/UI/ConfirmDialog.cs.meta Assets/Game/Prefabs/UI.meta Assets/Game/Prefabs/UI Assets/Game/Tests/EditMode/UIPrefabTests.cs Assets/Game/Tests/EditMode/UIPrefabTests.cs.meta Assets/Game/Tests/PlayMode/ScreenPrefabTests.cs Assets/Game/Tests/PlayMode/ScreenPrefabTests.cs.meta
git commit -m "Add pause screen and confirm dialog" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Задача 7: GameUI

**Файлы:**
- Создать: `Assets/Game/Scripts/UI/GameUI.cs`
- Создать (через Unity): `Assets/Game/Prefabs/UI/GameUI.prefab`
- Изменить: `Assets/Game/Tests/EditMode/UIPrefabTests.cs` (+4)
- Тест: `Assets/Game/Tests/PlayMode/UITestBase.cs`, `Assets/Game/Tests/PlayMode/GameUITests.cs`

**Интерфейсы:**
- Использует: `ScreenStack` (задача 1), `UIScreen`, `FadeTransition` (задача 2), `IGamePause`, `GamePause`,
  `FakeAudioSystem` (задача 3), паузу в `PlayerInputController` (задача 4), карту `UI` и `EventSystem` префаба
  `Services` (задача 5), `PauseScreen`, `ConfirmDialog` и их префабы (задача 6).
- Отдаёт (задаче 8): `Gameplay.GameUI : MonoBehaviour` с полями `_pauseAction`, `_cancelAction`, `_pauseOnFocusLoss`,
  `_pauseScreen`, `_confirmDialog`, `_quitQuestion`; префаб `Assets/Game/Prefabs/UI/GameUI.prefab` (корень `GameUI`:
  `Canvas` Screen Space – Overlay, Sort Order 20, `CanvasScaler` 1920×1080, `GraphicRaycaster`; `Screens` → вложенные
  `PauseScreen`, `ConfirmDialog`).

- [ ] **Шаг 1: заглушка** — `Assets/Game/Scripts/UI/GameUI.cs` (поля есть — префаб собирается уже с ними):

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    public sealed class GameUI : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference _pauseAction;
        [SerializeField] private InputActionReference _cancelAction;
        [SerializeField] private bool _pauseOnFocusLoss = true;

        [Header("Screens")]
        [SerializeField] private PauseScreen _pauseScreen;
        [SerializeField] private ConfirmDialog _confirmDialog;

        [Header("Texts")]
        [SerializeField] private string _quitQuestion = "Quit the game?";
    }
}
```

- [ ] **Шаг 2: префаб `GameUI`** — `Refresh`, консоль, затем `Unity_RunCommand`:

```csharp
using System;
using System.Linq;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal class CommandScript : IRunCommand
{
    private const string GAME_UI_PATH = "Assets/Game/Prefabs/UI/GameUI.prefab";
    private const string PAUSE_PATH = "Assets/Game/Prefabs/UI/PauseScreen.prefab";
    private const string DIALOG_PATH = "Assets/Game/Prefabs/UI/ConfirmDialog.prefab";
    private const string ACTIONS_PATH = "Assets/Game/Input/PlayerControls.inputactions";
    private const int SORT_ORDER = 20;   // over the player's HUD (10)

    public void Execute(ExecutionResult result)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GAME_UI_PATH) != null)
            throw new InvalidOperationException($"{GAME_UI_PATH} already exists");

        var pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PAUSE_PATH);
        var dialogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DIALOG_PATH);
        if (pausePrefab == null || dialogPrefab == null)
            throw new InvalidOperationException("Build the screen prefabs first (task 6)");

        // Visible sub-assets only: the importer also makes hidden copies of every reference
        InputActionReference[] references = AssetDatabase.LoadAllAssetsAtPath(ACTIONS_PATH)
            .OfType<InputActionReference>()
            .Where(r => AssetDatabase.IsSubAsset(r) && (r.hideFlags & HideFlags.HideInHierarchy) == 0)
            .ToArray();

        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("GameUI", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, preview);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORT_ORDER;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var gameUI = root.AddComponent<GameUI>();

            var screens = new GameObject("Screens", typeof(RectTransform));
            screens.transform.SetParent(root.transform, false);
            var rect = (RectTransform)screens.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var pause = (GameObject)PrefabUtility.InstantiatePrefab(pausePrefab, screens.transform);
            var dialog = (GameObject)PrefabUtility.InstantiatePrefab(dialogPrefab, screens.transform);

            SetReference(gameUI, "_pauseAction", Find(references, "Pause"));
            SetReference(gameUI, "_cancelAction", Find(references, "Cancel"));
            SetReference(gameUI, "_pauseScreen", pause.GetComponent<PauseScreen>());
            SetReference(gameUI, "_confirmDialog", dialog.GetComponent<ConfirmDialog>());

            PrefabUtility.SaveAsPrefabAsset(root, GAME_UI_PATH);
            result.Log("Created {0}", GAME_UI_PATH);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    private static InputActionReference Find(InputActionReference[] references, string action)
    {
        InputActionReference found = references.FirstOrDefault(r =>
            r.action != null && r.action.actionMap.name == "UI" && r.action.name == action);
        if (found == null)
            throw new InvalidOperationException($"No UI/{action} reference in PlayerControls");
        return found;
    }

    private static void SetReference(Object target, string field, Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name} has no field {field}");
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
```

- [ ] **Шаг 3: тесты префаба.** В `UIPrefabTests.cs` добавить `using UnityEngine.InputSystem;`, константы и тесты:

```csharp
        private const string GAME_UI_PREFAB = "Assets/Game/Prefabs/UI/GameUI.prefab";
        private const string PLAYER_PREFAB = "Assets/Game/Prefabs/Characters/Player.prefab";

        [Test]
        public void GameUIPrefab_ReferencesAssigned()
        {
            var ui = Load(GAME_UI_PREFAB).GetComponent<GameUI>();

            Assert.IsNotNull(ui);
            foreach (string field in new[] { "_pauseAction", "_cancelAction", "_pauseScreen", "_confirmDialog" })
                Assert.IsNotNull(Reference(ui, field), field);
        }

        [Test]
        public void GameUIPrefab_ReadsUIPauseAndCancel()
        {
            var ui = Load(GAME_UI_PREFAB).GetComponent<GameUI>();

            AssertUIAction((InputActionReference)Reference(ui, "_pauseAction"), "Pause");
            AssertUIAction((InputActionReference)Reference(ui, "_cancelAction"), "Cancel");
        }

        [Test]
        public void GameUIPrefab_ScreensAreSwitchedOffChildren()
        {
            GameObject prefab = Load(GAME_UI_PREFAB);
            var ui = prefab.GetComponent<GameUI>();

            foreach (string field in new[] { "_pauseScreen", "_confirmDialog" })
            {
                var screen = (Component)Reference(ui, field);
                Assert.IsTrue(screen.transform.IsChildOf(prefab.transform), field);
                Assert.IsFalse(screen.gameObject.activeSelf, field);
            }
        }

        [Test]
        public void GameUIPrefab_DrawsOverPlayerHud()
        {
            var canvas = Load(GAME_UI_PREFAB).GetComponent<Canvas>();
            Canvas hud = null;
            foreach (Canvas candidate in Load(PLAYER_PREFAB).GetComponentsInChildren<Canvas>(true))
            {
                if (candidate.name == "HUD")
                    hud = candidate;
            }

            Assert.IsNotNull(hud, "the player prefab has no HUD canvas");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.Greater(canvas.sortingOrder, hud.sortingOrder);
        }

        private static void AssertUIAction(InputActionReference reference, string name)
        {
            Assert.IsNotNull(reference, $"UI/{name} is not assigned");
            Assert.AreEqual("UI", reference.action.actionMap.name);
            Assert.AreEqual(name, reference.action.name);
        }
```

- [ ] **Шаг 4: стенд UI** — `Assets/Game/Tests/PlayMode/UITestBase.cs`. Две особенности, найденные при выполнении:
  1. Модуль UI (`InputSystemUIInputModule`) принимает Submit и Cancel через `WasPerformedThisDynamicUpdate()` — раз за
     `Time.frameCount`. Синхронные шаги стенда персонажа идут внутри одного кадра Unity, и одно нажатие срабатывало на
     каждом следующем шаге (South открывал диалог, а шаг отпускания тут же жал в нём «No»). Поэтому тесты UI —
     `[UnityTest]`, шаг — реальный кадр; `EventSystem`, `GameUI` и экраны обновляет сам Unity, в стенде персонажа они
     не регистрируются, `InputSystem.Update()` руками не зовётся.
  2. Сброс `InputTestFixture` не переразрешает привязки общего ассета `PlayerControls` (это делается только для
     project-wide actions). Если карту UI уже разрешила настоящая система ввода — например, `GameServicesTests` со своим
     `[Services]`, — действия продолжают читать её мёртвые клавиатуру и мышь, геймпада нет вовсе. Стенд ограничивает
     ассет виртуальными устройствами (`InputActionAsset.devices`, не сериализуется), это заново разрешает привязки;
     после `TearDown` фикстуры маска снимается, и ассет разрешается по устройствам настоящей системы.

```csharp
using System.Collections;
using NUnit.Framework;
using ServiceLocation;
using ScreenNavigation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Tests
{
    /// <summary>
    /// Rig for UI tests: the real GameUI prefab, the EventSystem from the Services prefab (UI map of PlayerControls),
    /// GamePause with a fake audio engine in the ServiceLocator, a virtual keyboard and gamepad. Tests are
    /// [UnityTest]s and a step is a real frame: the UI input module takes Submit and Cancel once per Time.frameCount,
    /// so several steps inside one frame would repeat a press. Every step rebuilds layout (automatic navigation needs
    /// real button positions) and finishes screen transitions at once. The character rig is there for the player.
    /// </summary>
    public abstract class UITestBase : CharacterTestBase
    {
        private const string GAME_UI_PREFAB = "Assets/Game/Prefabs/UI/GameUI.prefab";
        private const string SERVICES_PREFAB = "Assets/Game/Prefabs/Services.prefab";

        private GameObject _eventSystem;
        private InputActionAsset _uiActions;

        protected InputTestFixture InputFixture { get; private set; }
        protected Keyboard Keyboard { get; private set; }
        protected Gamepad Gamepad { get; private set; }
        protected FakeAudioSystem Audio { get; private set; }
        protected GamePause Pause { get; private set; }
        protected GameUI UI { get; private set; }
        protected PauseScreen PauseScreen { get; private set; }
        protected ConfirmDialog ConfirmDialog { get; private set; }

        protected GameObject Selected =>
            EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        [UnitySetUp]
        public IEnumerator SetUpUI()
        {
            ServiceLocator.Clear();
            InputFixture = new InputTestFixture();
            InputFixture.Setup();
            Keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad = InputSystem.AddDevice<Gamepad>();

            Audio = new FakeAudioSystem();
            Pause = new GamePause(Audio);
            ServiceLocator.Register<IGamePause>(Pause);

            GameObject eventSystem = LoadPrefab(SERVICES_PREFAB).GetComponentInChildren<EventSystem>(true).gameObject;
            // The fixture's reset does not resolve the bindings of the shared PlayerControls asset again: resolved by
            // the real input system in an earlier test, the UI map would keep reading the real devices. Limiting the
            // asset to the virtual devices resolves it again
            _uiActions = eventSystem.GetComponent<InputSystemUIInputModule>().actionsAsset;
            _uiActions.devices = new InputDevice[] { Keyboard, Gamepad };

            _eventSystem = Object.Instantiate(eventSystem);
            UI = Object.Instantiate(LoadPrefab(GAME_UI_PREFAB)).GetComponent<GameUI>();
            PauseScreen = UI.GetComponentInChildren<PauseScreen>(true);
            ConfirmDialog = UI.GetComponentInChildren<ConfirmDialog>(true);

            yield return Step();   // the EventSystem picks its input module on the first update, processes input from the next
        }

        [TearDown]
        public void TearDownUI()
        {
            if (UI != null)
                Object.DestroyImmediate(UI.gameObject);
            Object.DestroyImmediate(_eventSystem);
            Pause.Dispose();
            ServiceLocator.Clear();
            InputFixture.TearDown();
            _uiActions.devices = null;   // back to all devices of the real input system
            Time.timeScale = 1f;
        }

        /// <summary>Frames: input queued before a frame is processed in it.</summary>
        protected IEnumerator Step(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                FinishTransitions();
            }
        }

        /// <summary>Press, one frame, release, one frame.</summary>
        protected IEnumerator Tap(ButtonControl button)
        {
            InputFixture.Press(button);
            yield return Step();
            InputFixture.Release(button);
            yield return Step();
        }

        protected IEnumerator StickDown()
        {
            InputFixture.Set(Gamepad.leftStick, new Vector2(0f, -1f));
            yield return Step();
            InputFixture.Set(Gamepad.leftStick, Vector2.zero);
            yield return Step();
        }

        protected static GameObject ButtonOf(Component screen, string name)
        {
            foreach (Button button in screen.GetComponentsInChildren<Button>(true))
            {
                if (button.name == name)
                    return button.gameObject;
            }

            Assert.Fail($"{screen.name} has no button {name}");
            return null;
        }

        private void FinishTransitions()
        {
            if (UI == null)
                return;

            foreach (FadeTransition fade in UI.GetComponentsInChildren<FadeTransition>(true))
                fade.Tick(float.MaxValue);
        }

        private static GameObject LoadPrefab(string path)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"No prefab at {path}");
            return prefab;
#else
            Assert.Ignore("UI tests load prefabs by path: editor only");
            return null;
#endif
        }
    }
}
```

- [ ] **Шаг 5: тесты меню** — `Assets/Game/Tests/PlayMode/GameUITests.cs`. Кроме тестов меню — два теста стенда: карта
  UI привязана только к виртуальным устройствам (ловит особенность 2 из шага 4) и South во время игры даёт прыжок
  (контроль для `ResumeWithA_DoesNotJump`: без него тест «прыжка нет» мог бы пройти впустую).

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gameplay.Tests
{
    /// <summary>
    /// Pause menu of gameplay scenes: open and close, back and close-all buttons, the quit dialog, pause on focus
    /// loss, no jump from the button that closes the menu.
    /// </summary>
    public class GameUITests : UITestBase
    {
        private IEnumerator OpenQuitDialog()
        {
            yield return Tap(Keyboard.escapeKey);
            yield return StickDown();
            yield return Tap(Gamepad.buttonSouth);
        }

        private void SpawnPlayerWithController()
        {
            Rig.AddFloor();
            Rig.SpawnGrounded(Vector2.zero);
            Rig.Player.transform.Find("Controllers").gameObject.SetActive(true);
        }

        [UnityTest]
        public IEnumerator UITestBase_SharedUIMap_BindsToVirtualDevices()
        {
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            InputActionMap map = module.actionsAsset.FindActionMap("UI", true);

            foreach (InputAction action in map.actions)
            {
                foreach (InputControl control in action.controls)
                {
                    Assert.IsTrue(control.device == Keyboard || control.device == Gamepad,
                        $"UI/{action.name} reads {control.path}, a device outside the test input system");
                }
            }

            CollectionAssert.Contains(map.FindAction("Submit", true).controls, Gamepad.buttonSouth);
            yield break;
        }

        // Control for GameUI_ResumeWithA_DoesNotJump: the rig sees a jump from South
        [UnityTest]
        public IEnumerator UITestBase_SouthWhilePlaying_Jumps()
        {
            SpawnPlayerWithController();
            bool jumped = false;
            Rig.Jump.OnJump += () => jumped = true;

            yield return Tap(Gamepad.buttonSouth);
            Rig.Step(10);

            Assert.IsTrue(jumped);
        }

        [UnityTest]
        public IEnumerator GameUI_Escape_PausesGame()
        {
            yield return Tap(Keyboard.escapeKey);

            Assert.IsTrue(Pause.IsPaused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(Audio.IsPaused);
            Assert.IsTrue(PauseScreen.IsShown);
        }

        [UnityTest]
        public IEnumerator GameUI_EscapeAgain_ResumesGame()
        {
            yield return Tap(Keyboard.escapeKey);

            yield return Tap(Keyboard.escapeKey);

            Assert.IsFalse(Pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(Audio.IsPaused);
            Assert.IsFalse(PauseScreen.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator GameUI_GamepadStart_PausesGame()
        {
            yield return Tap(Gamepad.startButton);

            Assert.IsTrue(Pause.IsPaused);
            Assert.IsTrue(PauseScreen.IsShown);
        }

        [UnityTest]
        public IEnumerator GameUI_Paused_SelectsResume()
        {
            yield return Tap(Keyboard.escapeKey);

            Assert.AreSame(ButtonOf(PauseScreen, "Resume"), Selected);
        }

        [UnityTest]
        public IEnumerator GameUI_StickDown_SelectsQuit()
        {
            yield return Tap(Keyboard.escapeKey);

            yield return StickDown();

            Assert.AreSame(ButtonOf(PauseScreen, "Quit"), Selected);
        }

        [UnityTest]
        public IEnumerator GameUI_Quit_AsksOverPauseMenu()
        {
            yield return OpenQuitDialog();

            Assert.IsTrue(ConfirmDialog.IsShown);
            Assert.AreEqual("Quit the game?", ConfirmDialog.Text);
            Assert.IsTrue(PauseScreen.IsShown, "the dialog is an overlay");
            Assert.IsFalse(PauseScreen.IsInteractable);
            Assert.AreSame(ButtonOf(ConfirmDialog, "No"), Selected);
        }

        [UnityTest]
        public IEnumerator GameUI_BackInDialog_ReturnsToQuit()
        {
            yield return OpenQuitDialog();

            yield return Tap(Gamepad.buttonEast);

            Assert.IsFalse(ConfirmDialog.IsShown);
            Assert.IsTrue(Pause.IsPaused);
            Assert.AreSame(ButtonOf(PauseScreen, "Quit"), Selected);
        }

        [UnityTest]
        public IEnumerator GameUI_NoInDialog_ReturnsToQuit()
        {
            yield return OpenQuitDialog();

            yield return Tap(Gamepad.buttonSouth);   // "No" is selected

            Assert.IsFalse(ConfirmDialog.IsShown);
            Assert.AreSame(ButtonOf(PauseScreen, "Quit"), Selected);
        }

        [UnityTest]
        public IEnumerator GameUI_StartInDialog_ClosesEverything()
        {
            yield return OpenQuitDialog();

            yield return Tap(Gamepad.startButton);

            Assert.IsFalse(Pause.IsPaused);
            Assert.IsFalse(ConfirmDialog.IsShown);
            Assert.IsFalse(PauseScreen.IsShown);
        }

        [UnityTest]
        public IEnumerator GameUI_ResumeWithA_DoesNotJump()
        {
            SpawnPlayerWithController();
            bool jumped = false;
            Rig.Jump.OnJump += () => jumped = true;
            yield return Tap(Keyboard.escapeKey);

            yield return Tap(Gamepad.buttonSouth);   // "Resume" is selected
            Rig.Step(10);                            // a jump pressed by that button would start here

            Assert.IsFalse(Pause.IsPaused);
            Assert.IsFalse(jumped, "the button that closed the menu must not jump");
        }

        [UnityTest]
        public IEnumerator GameUI_FocusLost_PausesGame()
        {
            UI.SendMessage("OnApplicationFocus", false);
            yield return Step();

            Assert.IsTrue(Pause.IsPaused);
            Assert.IsTrue(PauseScreen.IsShown);
        }

        [UnityTest]
        public IEnumerator GameUI_FocusLostWithSettingOff_KeepsPlaying()
        {
#if UNITY_EDITOR
            var serialized = new SerializedObject(UI);
            serialized.FindProperty("_pauseOnFocusLoss").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
#endif
            UI.SendMessage("OnApplicationFocus", false);
            yield return Step();

            Assert.IsFalse(Pause.IsPaused);
        }

        [UnityTest]
        public IEnumerator GameUI_DestroyedWhilePaused_ResumesGame()
        {
            yield return Tap(Keyboard.escapeKey);
            Assert.IsTrue(Pause.IsPaused, "precondition: the menu paused the game");

            Object.DestroyImmediate(UI.gameObject);

            Assert.IsFalse(Pause.IsPaused);
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
```

- [ ] **Шаг 6: Red.** `Refresh`, консоль, `Run All Tests`. Ожидание: `UIPrefabTests` новые — зелёные (префаб собран
  с заглушкой); `GameUITests` падают на проверках (заглушка не читает ввод), кроме `FocusLostWithSettingOff_KeepsPlaying`
  и двух тестов стенда `UITestBase_*` (от `GameUI` не зависят).

- [ ] **Шаг 7: реализация** — `Assets/Game/Scripts/UI/GameUI.cs` целиком:

```csharp
using System;
using ScreenNavigation;
using ServiceLocation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay
{
    /// <summary>
    /// Root of the UI in gameplay scenes: owns the screen stack, reads the pause and back buttons and wires screen
    /// events to the stack and the services. The game is paused exactly while the stack is not empty. Not a game
    /// entity: the screens are its children, there are no Controllers/Components.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("UI/Pause (Esc, gamepad Start): opens the pause menu, with a menu open — closes everything")]
        [SerializeField] private InputActionReference _pauseAction;
        [Tooltip("UI/Cancel (Esc, gamepad B): one step back; switched on by the UI input module")]
        [SerializeField] private InputActionReference _cancelAction;
        [Tooltip("Open the pause menu when the game window loses focus")]
        [SerializeField] private bool _pauseOnFocusLoss = true;

        [Header("Screens")]
        [SerializeField] private PauseScreen _pauseScreen;
        [SerializeField] private ConfirmDialog _confirmDialog;

        [Header("Texts")]
        [SerializeField] private string _quitQuestion = "Quit the game?";

        private readonly ScreenStack _stack = new();
        private IGamePause _pause;
        private Action _onConfirm;

        private void Awake()
        {
            Require(_pauseAction, nameof(_pauseAction));
            Require(_cancelAction, nameof(_cancelAction));
            Require(_pauseScreen, nameof(_pauseScreen));
            Require(_confirmDialog, nameof(_confirmDialog));
        }

        private void OnEnable()
        {
            // [Services] wakes up before every scene object; tests register the pause before creating GameUI
            _pause = ServiceLocator.Get<IGamePause>();

            _stack.Changed += HandleStackChanged;
            _pauseScreen.OnResume += HandleResume;
            _pauseScreen.OnQuit += HandleQuit;
            _confirmDialog.OnYes += HandleYes;
            _confirmDialog.OnNo += HandleNo;
            _confirmDialog.OnClosed += HandleDialogClosed;
            _pauseAction.action.Enable();
        }

        private void OnDisable()
        {
            _stack.Changed -= HandleStackChanged;
            _pauseScreen.OnResume -= HandleResume;
            _pauseScreen.OnQuit -= HandleQuit;
            _confirmDialog.OnYes -= HandleYes;
            _confirmDialog.OnNo -= HandleNo;
            _confirmDialog.OnClosed -= HandleDialogClosed;
            _pauseAction.action.Disable();
        }

        private void OnDestroy()
        {
            if (!_stack.IsEmpty)
                _pause?.Resume();
        }

        private void Update()
        {
            bool pause = _pauseAction.action.WasPressedThisFrame();
            bool cancel = _cancelAction.action.WasPressedThisFrame();

            // Esc is in both actions: with a menu open it is one step back
            if (_stack.IsEmpty)
            {
                if (pause)
                    _stack.Push(_pauseScreen);
            }
            else if (cancel)
            {
                _stack.Back();
            }
            else if (pause)
            {
                _stack.Clear();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _pauseOnFocusLoss && isActiveAndEnabled && _stack.IsEmpty)
                _stack.Push(_pauseScreen);
        }

        private void Ask(string question, Action onConfirm)
        {
            _onConfirm = onConfirm;
            _confirmDialog.SetText(question);
            _stack.Push(_confirmDialog);
        }

        private void HandleStackChanged()
        {
            if (_stack.IsEmpty)
                _pause.Resume();
            else
                _pause.Pause();
        }

        private void HandleResume() => _stack.Clear();

        private void HandleQuit() => Ask(_quitQuestion, Application.Quit);

        private void HandleYes()
        {
            // Closing the dialog forgets the action, so it is taken first
            Action onConfirm = _onConfirm;
            _stack.Pop();
            onConfirm?.Invoke();
        }

        private void HandleNo() => _stack.Pop();

        // The dialog may also close through Back or Start
        private void HandleDialogClosed() => _onConfirm = null;

        private static void Require(UnityEngine.Object reference, string field)
        {
            if (reference == null)
                throw new ArgumentNullException(field, $"{nameof(GameUI)}: {field} is not assigned");
        }
    }
}
```

- [ ] **Шаг 8: Green.** `Refresh`, консоль, `Run All Tests` — всё зелёное. Если `GameUI_StickDown_SelectsQuit` красный
  из-за навигации (выделение не сдвинулось), проверить, что в префабе `PauseScreen` у `Panel` есть
  `VerticalLayoutGroup` и кнопки стоят столбиком, а у кнопок навигация `Automatic`; не подгонять тест. Ошибка
  «Cached unprocessed value unexpectedly became outdated» и мёртвый геймпад — признак особенности 2 из шага 4 (стенд
  не ограничил ассет виртуальными устройствами); диалог, который открывается и сразу закрывается, — особенности 1.

- [ ] **Шаг 9: коммит.**

```bash
git add Assets/Game/Scripts/UI/GameUI.cs Assets/Game/Scripts/UI/GameUI.cs.meta Assets/Game/Prefabs/UI/GameUI.prefab Assets/Game/Prefabs/UI/GameUI.prefab.meta Assets/Game/Tests/EditMode/UIPrefabTests.cs Assets/Game/Tests/PlayMode/UITestBase.cs Assets/Game/Tests/PlayMode/UITestBase.cs.meta Assets/Game/Tests/PlayMode/GameUITests.cs Assets/Game/Tests/PlayMode/GameUITests.cs.meta
git commit -m "Add GameUI with pause menu" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Задача 8: меню в сценах и ручная проверка

**Файлы:**
- Изменить (через Unity): `Assets/Game/Scenes/Game.unity`, `Assets/Game/Scenes/Sandbox_Enemies.unity`
- Изменить (через Unity, только после ответа пользователя): `Assets/Game/Scenes/Sandbox_Locomotion.unity`

**Интерфейсы:**
- Использует: префаб `GameUI` (задача 7), `[Services]` в сценах.
- Отдаёт: объект `[GameUI]` в игровых сценах.

- [ ] **Шаг 1: спросить пользователя про `Sandbox_Locomotion.unity`.** В ней чужие незакоммиченные правки: закоммитить
  их отдельно, отложить сцену или поставить меню поверх них без коммита сцены. Без ответа сцену не трогать.

- [ ] **Шаг 2: поставить `[GameUI]`** — `Unity_RunCommand` (в `Scenes` добавить `Sandbox_Locomotion`, если пользователь
  разрешил):

```csharp
using System;
using System.Linq;
using Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

internal class CommandScript : IRunCommand
{
    private const string PREFAB_PATH = "Assets/Game/Prefabs/UI/GameUI.prefab";

    private static readonly string[] Scenes =
    {
        "Assets/Game/Scenes/Game.unity",
        "Assets/Game/Scenes/Sandbox_Enemies.unity",
    };

    public void Execute(ExecutionResult result)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty)
                throw new InvalidOperationException($"Save or discard changes in {open.path} first");
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
        if (prefab == null)
            throw new InvalidOperationException($"No {PREFAB_PATH}");

        string previous = SceneManager.GetActiveScene().path;
        foreach (string path in Scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();

            if (roots.Any(r => r.GetComponent<GameUI>() != null))
            {
                result.Log("{0}: [GameUI] is already there", path);
                continue;
            }

            if (!roots.Any(r => r.GetComponent<GameServices>() != null))
                throw new InvalidOperationException($"{path} has no [Services]: GameUI needs the pause service");
            if (roots.Any(r => r.GetComponent<GameServices>() == null && r.GetComponentInChildren<EventSystem>(true) != null))
                throw new InvalidOperationException($"{path} has its own EventSystem: remove it (task 5)");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "[GameUI]";
            result.RegisterObjectCreation(instance);
            EditorSceneManager.SaveScene(scene);
            result.Log("{0}: [GameUI] added", path);
        }

        if (!string.IsNullOrEmpty(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }
}
```

- [ ] **Шаг 3: тесты.** `Run All Tests` — всё зелёное (сцены тесты не грузят, прогон — страховка).

- [ ] **Шаг 4: ручная проверка с пользователем** (MCP нажимать клавиши не умеет). В `Sandbox_Enemies`, `Game.unity`
  (и `Sandbox_Locomotion`, если меню там есть) в Play Mode:
  1. Esc — меню паузы поверх затемнённой игры; встали игрок, враги, пила, платформы, анимации, игровые звуки.
  2. Стрелки, WASD, стик и D-pad двигают выделение; наведение мыши выделяет кнопку; Enter, Space и A нажимают.
  3. Esc и B — шаг назад; Start — закрыть всё.
  4. «Resume» по A и по Space — игра идёт, прыжка нет.
  5. «Quit» — диалог поверх паузы, выделена «No»; «No», Esc и B — назад, выделена «Quit»; «Yes» в эдиторе ничего не
     делает (`Application.Quit` в эдиторе игнорируется).
  6. Переключиться на другое окно — игра встаёт на паузу.
  7. В консоли нет ошибок и предупреждения «There are 2 event systems».
  8. Как ведёт себя звук на паузе: встаёт ли музыка, играют ли новые события (`IAudioSystem.Pause()` выключает
     `AudioSystem` и ставит на паузу играющие события). Записать в `UI.md`, раздел 5.4 — это вход для звуков UI
     в части 4.
  Найденное — тестом (Red) и правкой, затем повторить пункт.

- [ ] **Шаг 5: коммит.**

```bash
git add Assets/Game/Scenes/Game.unity Assets/Game/Scenes/Sandbox_Enemies.unity
git commit -m "Add pause menu to gameplay scenes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

  `Sandbox_Locomotion.unity` — в коммит только по ответу пользователя на шаге 1.

---

### Задача 9: документация и полный прогон

**Файлы:**
- Изменить: `Assets/AI/CLAUDE.md`, `Assets/AI/Docs/CompositionGuide.md`, `Assets/AI/Docs/UI.md`

**Интерфейсы:**
- Использует: всё сделанное в задачах 1–8.
- Отдаёт: правила UI в документации проекта.

- [ ] **Шаг 1: `CLAUDE.md`.**
  1. После абзаца «**Этап 5 — …**» добавить:

```markdown
**UI — [Docs/UI.md](Docs/UI.md):** меню на uGUI. Навигация — модуль `Modules/ScreenStack` (стек экранов: ввод только
у верхнего, оверлей оставляет нижние видимыми, «назад» — `Pop`). В игровых сценах — `[GameUI]` (оркестратор: стек,
кнопки паузы и «назад», связь событий экранов с сервисами), экраны `PauseScreen` и `ConfirmDialog`. Пауза — сервис
`IGamePause` в `[Services]`, игра на паузе ⇔ стек `GameUI` не пуст. Ветка `feature/ui`. Исследование —
[Docs/Research_UI.md](Docs/Research_UI.md), план части 1 — [Docs/UI_Plan.md](Docs/UI_Plan.md).
```

  2. В дереве `Assets/Game`: `Input/` — «PlayerControls.inputactions (карты Player и UI; клавиатура + геймпад)»;
     `Modules/` — добавить `ScreenStack`; `Prefabs/` — `Services ([Services]: GameServices + AudioSystem + EventSystem)`
     и `UI/ (GameUI, PauseScreen, ConfirmDialog)`; `Scripts/UI/` — «HealthBarView, GameUI (корень UI игровых сцен),
     PauseScreen, ConfirmDialog»; `Scripts/GameSystem/Services/` — «GameServices, IGamePause и GamePause (пауза)»;
     `Tests/PlayMode` — добавить `UITestBase` и `Scenes/EmptyTestScene.unity`.
  3. В «Сборки (asmdef)»: у `Game` в списке ссылок — `ScreenStack` и `Unity.TextMeshPro`; новый пункт:
     «`ScreenStack` (`Game/Scripts/Modules/ScreenStack/Runtime`) — стек экранов и экран на uGUI, ссылается только на
     `UnityEngine.UI`; `ScreenStack.Tests` (EditMode) и `ScreenStack.Tests.Runtime` (PlayMode) — тесты модуля, внутри
     модуля»; у `Game.Tests.EditMode` — ссылки на `ScreenStack`, `UnityEngine.UI`, `Unity.InputSystem`; у
     `Game.Tests.PlayMode` — ещё на `ScreenStack` и `UnityEngine.UI`.
  4. В «Архитектура» после абзаца «**Сервисы.**» добавить:

```markdown
**UI и пауза.** Модуль `Modules/ScreenStack` (namespace `ScreenNavigation`, описание — его `README.md`): `ScreenStack` —
обычный C# за контрактом `IScreen` (`Open`/`Close` — вход и выход из стека, `Show`/`Hide` — видимость, `Focus`/`Blur` —
ввод); `UIScreen` — экран на uGUI (`CanvasGroup`, первая кнопка, запоминание выделения, переход `FadeTransition`
на unscaled time); `SelectOnPointerEnter` — наведение мыши выделяет кнопку. Экран игры — префаб с наследником
`UIScreen` (суффикс `Screen` или `Dialog`, на корне префаба экрана): кнопки через `[SerializeField]`, наружу — только
события; стек, сервисы и другие экраны он не знает. Корень UI (`GameUI` в игровых сценах) — оркестратор, а не игровая
сущность: без `Controllers`/`Components`, как `[Services]`; держит `ScreenStack`, читает `UI/Pause` (Esc, Start)
и `UI/Cancel` (Esc, B), связывает события экранов со стеком и сервисами. Пауза — `IGamePause` (создаёт и регистрирует
`GameServices`): `Time.timeScale` меняет только он, загрузка сцены снимает паузу; игра на паузе ⇔ стек `GameUI`
не пуст; `PlayerInputController` на паузе выключает карту `Player`. Весь UI — на unscaled time. `EventSystem`
с `InputSystemUIInputModule` (карта `UI` в `PlayerControls`) — один, в `[Services]`; своих `EventSystem` в сцены
не ставить. Новый экран — префаб-наследник `UIScreen` и строки в оркестраторе (поле, подписки).
```

  5. В «Тестирование (TDD)» после абзаца про стенд врагов добавить:

```markdown
Стенд UI — `UITestBase`: реальный префаб `GameUI`, `EventSystem` из префаба `Services`, `GamePause` с `FakeAudioSystem`
в `ServiceLocator`, виртуальные клавиатура и геймпад. Тесты UI — `[UnityTest]`, шаг — реальный кадр: модуль UI
принимает Submit/Cancel раз за `Time.frameCount`, синхронные шаги `CharacterTestRig` повторяли бы нажатие (стенд
персонажа остаётся для игрока: `Rig.Step` — физика). Общий ассет `PlayerControls` стенд ограничивает виртуальными
устройствами (`devices`): сброс `InputTestFixture` его привязки не переразрешает. Переходы экранов доигрываются сразу,
раскладка пересчитывается каждый шаг. Кнопки тесты ищут по имени объекта (= текст кнопки).
```

  6. В «Сервисы» к фразе «Звук — `IAudioSystem`» добавить «, пауза — `IGamePause` (`GamePause` — обычный C#-класс:
     создаётся в `Awake` `GameServices`, освобождается в `OnDestroy`)».

- [ ] **Шаг 2: `CompositionGuide.md`** — в «Best Practices» после «10. Глобальные сервисы — через `ServiceLocator`»
  добавить:

```markdown
### 11. UI: экраны и корень UI

UI — не игровая сущность: правило `Controllers`/`Components` на него не распространяется (как на `[Services]`).

- **Экран** — префаб с компонентом-наследником `UIScreen` (модуль `ScreenStack`), суффикс `Screen` или `Dialog`, висит
  на корне префаба экрана. Кнопки — `[SerializeField]`, наружу — события `On…`; стек, сервисы и другие экраны он
  не знает. Это View: что делать по кнопке, решает оркестратор.
- **Корень UI** (`GameUI`, дальше `MainMenuUI`) — оркестратор экранов: держит `ScreenStack` обычным полем, читает кнопки
  «пауза» и «назад», подписывается на события экранов в `OnEnable`/`OnDisable` и зовёт стек и сервисы.
- Данные в экран — методами (`SetText`, `SetValues`), из экрана — событиями. Data binding не используем.
- Время UI — unscaled: на паузе `Time.timeScale = 0`.
```

  и строку «11. UI» в оглавление раздела, если оно перечисляет подпункты.

- [ ] **Шаг 3: `UI.md`** — строка «**Статус:**»: «часть 1 реализована (ветка `feature/ui`), части 2–5 — наброски»;
  если по ходу работы что-то разошлось со спеком, поправить спек под сделанное.

- [ ] **Шаг 4: полный прогон.** `Run All Tests` — всё зелёное; консоль без ошибок. Показать пользователю сводку: что
  сделано, какие тесты добавлены, что проверено руками.

- [ ] **Шаг 5: коммит.**

```bash
git add Assets/AI/CLAUDE.md Assets/AI/Docs/CompositionGuide.md Assets/AI/Docs/UI.md
git commit -m "Document UI and pause" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## Покрытие спека (часть 1)

| Требование спека | Задача |
|---|---|
| Модуль `ScreenStack`: `IScreen`, правила стека, ошибки, `Changed` | 1 |
| `UIScreen`, `ScreenTransition`, `FadeTransition`, `SelectOnPointerEnter`, README модуля | 2 |
| `IGamePause`: `timeScale`, звук, повторные вызовы, сброс при загрузке сцены, `Dispose`; регистрация в `GameServices` | 3 |
| `PlayerInputController` на паузе; «Resume» по A не прыгает | 4, 7 |
| Карта `UI` с `Pause`; один `EventSystem` в `[Services]`; `Deselect On Background Click` выключен | 5 |
| `PauseScreen`, `ConfirmDialog` (оверлей, «No» первой), префабы, проверка ссылок | 6 |
| `GameUI`: Esc/Start, Cancel = шаг назад, Start = закрыть всё, пауза ⇔ стек не пуст, диалог выхода, фокус окна, уничтожение на паузе | 7 |
| `GameUI` поверх HUD игрока (Sort Order 20 > 10) | 7 |
| Меню в `Game.unity`, `Sandbox_Enemies`, `Sandbox_Locomotion`; ручная проверка критерия готовности | 8 |
| Правила UI в `CLAUDE.md` и `CompositionGuide.md` | 9 |
