# Исследование: архитектура UI и меню

Сводка поиска 2026-10-23 по направлениям: выбор UI-системы под Unity 6.5, устройство меню в известных решениях,
ввод и навигация с геймпада, пауза, пиксель-арт, тестирование UI. Источники — в конце. Выводы, которых нет
в источниках напрямую, помечены «(вывод)». Мнения с форумов — опыт сообщества, а не позиция Unity. Две страницы
(девлог на itch.io, статья bugnet) напрямую не открылись — пункты о них по выдержкам поиска.

**Как использовать.** Раздел 1 — выжимка. Разделы 2–7 — что нашли. Раздел 8 — что из этого следует для нас
и что решили. Спек по итогам — [UI.md](UI.md).

## 1. Выжимка

1. В документации Unity 6.5 для рантайма рекомендован **uGUI**, UI Toolkit — альтернатива. Для «меню и HUD под
   много разрешений в проектах с большим UI» чаще берут UI Toolkit, для UI с keyframe-анимациями — uGUI.
2. UI Toolkit быстро растёт (world space — 6.2; SVG, фильтры USS, свои шейдеры, фреймворк тестов — 6.3), но его
   шейдеры UI (UI Shader Graph) работают **только в URP**. Keyframe-анимация и ссылки на элементы из MonoBehaviour —
   в планах к 6.7 LTS, частицы в UI — после 6.7.
3. Общий знаменатель всех разобранных решений — **стек экранов**: Push/Pop, «назад» = Pop, ввод получает только
   верхний экран, полупрозрачный экран оставляет нижние видимыми, модальное окно блокирует остальное.
4. **Слои:** экраны с историей (меню, диалоги) — отдельно от панелей без истории (HUD); затемнение и загрузка —
   поверх всего.
5. **Игра не лезет внутрь экранов, экран не знает игру** — только через фасад или оркестратор (deVoid UI, QuizU).
6. **MVP** (QuizU): View — обычный C#-класс, Presenter — MonoBehaviour, связь событиями. **Data binding (MVVM)**
   UI Toolkit удобен, но по умолчанию обновляется каждый кадр; ручные обновления по событиям Unity называет уместными,
   когда нужен контроль.
7. **Пауза:** `Time.timeScale = 0`, UI на unscaled time, звук — отдельно, игровой ввод выключить, `timeScale`
   сбрасывать при загрузке сцены. Ловушки — ввод в кадре снятия паузы и одно «назад», закрывающее два экрана.
8. **Пиксель-арт:** масштаб UI — только целый. У UI Toolkit при нецелом масштабе элементы масштабируются неравномерно
   (баг в трекере), обход — Constant Pixel Size с целым коэффициентом; у uGUI — свой расчёт целого коэффициента.
9. **Тесты:** у UI Toolkit есть UI Test Framework (с 6.3): `UITestFixture`, `simulate.Click`, `simulate.FrameUpdate`.
   Для uGUI такого инструмента нет.
10. (вывод) Для нас — сочная анимация, Built-in RP, DOTween и SSU в проекте, связи через инспектор — **uGUI и свой
    маленький модуль стека экранов**, контракт которого не зависит от UI-системы.

## 2. UI-система: uGUI или UI Toolkit

### 2.1. Позиция Unity (документация 6.5)

| Unity 6.5 | Рекомендация | Альтернатива |
|---|---|---|
| Рантайм | uGUI | UI Toolkit |
| Эдитор | UI Toolkit | IMGUI |

«Чаще используют»: меню и HUD под много разрешений в проектах с большим UI — UI Toolkit; world space и VR —
UI Toolkit; UI с особыми шейдерами и материалами — UI Toolkit; UI с keyframe-анимациями — uGUI. За uGUI отдельно —
«лёгкие ссылки из MonoBehaviour». UI Toolkit «в активной разработке, новые возможности выходят часто», uGUI —
«устоявшийся, проверенный в продакшене, обновляется редко». Рекомендации меняются между версиями: в 6.0 world space
числился за uGUI.

### 2.2. Возможности

| Возможность | UI Toolkit | uGUI |
|---|---|---|
| Ссылки на элементы из инспектора | нет, поиск по имени (в планах к 6.7) | да |
| Сборка в сцене | нет (в планах к 6.7) | да |
| Сериализованные события (`onClick` в инспекторе) | нет | да |
| Animation Clips и Timeline | нет (keyframe — к 6.7) | да |
| Частицы в UI | нет (после 6.7) | Canvas в Screen Space – Camera или сторонние пакеты (вывод) |
| Свои шейдеры и материалы | да, UI Shader Graph — только URP | да, обычные UI-шейдеры |
| Переходы | USS transitions | нет встроенных, код или Animator |
| Глобальные стили | USS | нет |
| Data binding | да | нет |
| Гибкая раскладка | flexbox | Layout Groups |
| SVG, антиалиасинг UI, RTL | да | нет |
| Фреймворк UI-тестов | да (с 6.3) | нет |
| Input System | да | да |

### 2.3. Планы Unity (статус разработки UI Toolkit, ноябрь 2025)

- **Выпущено.** 6.2 — world space UI. 6.3 — SVG, свои шейдеры через Shader Graph (только URP), фильтры USS (blur,
  grayscale, sepia и свои), улучшения авторинга, фреймворк UI-тестов, Advanced Text Generator по умолчанию в эдиторе.
- **В работе к 6.7 LTS.** Keyframe-анимация VisualElement в окне Animation, авторинг UI в Scene view, ссылки на
  элементы UXML из MonoBehaviour, градиенты, тени и blur в USS, инструменты профилирования.
- **После 6.7, без дат.** Частицы и 3D-объекты в UI, мягкие маски, Addressables, grid-раскладка, улучшения
  data binding, загрузка UXML/USS в рантайме.
- О судьбе uGUI в посте ничего. На форуме пересказывают, что Unity возобновляет развитие uGUI рядом с UI Toolkit, —
  официального подтверждения не нашли.

### 2.4. Опыт сообщества

Тред форума Unity «основная UI-система и почему» (декабрь 2025 — январь 2026) и девлог инди-разработчика:

- Большинство отвечавших держат игровой UI на uGUI: «результат сразу», проще чинить и расширять. Сторонники
  UI Toolkit — за темы и стили, отмечают новые возможности Unity 6.
- Жалобы на UI Toolkit: порог входа; UI Builder неудобен для частых правок (новая кнопка — это Builder плюс запрос
  в коде); слабые инструменты анимации; наткнулся на баг — обойти нечем.
- Жалобы на uGUI: нет стилей и data binding; из состояния `Selected` кнопка не возвращается в `Highlighted`; много
  объектов в иерархии.
- В плане разработки UI Toolkit пользователи просят явную навигацию «как в uGUI» и простые API фокуса.

## 3. Архитектура меню

### 3.1. Как устроены известные решения

| Решение | UI | Экраны | Навигация | Связь с игрой | Заметки |
|---|---|---|---|---|---|
| QuizU (сэмпл Unity, UI Toolkit, Unity 6) | Один `UIDocument` и одни `PanelSettings`, экраны собраны в общий UXML | `UIScreen` — обычный C#-класс; все экраны созданы заранее, скрываются `display: none` | Стек `m_History`, «домой» — главное меню; полупрозрачный экран оставляет нижние видимыми | `UIManager` ведёт экраны, `SequenceManager` — поток игры; оба реагируют на события | MVP: View — C#-класс, Presenter — MonoBehaviour, статические события по направлениям |
| Dragon Crashers (сэмпл Unity, UI Toolkit) | UI Toolkit | Главное меню из модальных экранов, вкладки, инвентарь | — | — | Темы для портрета и ландшафта, свои контролы |
| deVoid UI Framework (Yanko Oliveira, open source) | uGUI, Canvas в Screen Space – Camera (частицы, 3D, постэффекты) | Префаб на экран; `Panels` — без истории (HUD), `Windows` — с историей и очередью (диалоги) | Слои по типам экранов, пара-слои для порядка отрисовки | Одно правило: внешний код не лезет внутрь экранов; фасад `UI Frame`; глобальные сигналы; данные — объектом-пейлоадом при открытии | На время перехода — прозрачный блокатор ввода; DOTween; кнопки настраиваются ScriptableObject |
| UnityScreenNavigator (Haruma-K, MIT) | uGUI | `Page` — с историей, `Modal` — блокирует всё под собой, `Sheet` — вкладки без истории | Контейнеры, `Push/Pop`, жизненный цикл `WillPushEnter`, `DidPushEnter`, `WillPopExit`… | — | Загрузка по пути (Resources, Addressables); закрытые экраны уничтожаются; асинхронный цикл; переходы — ScriptableObject или MonoBehaviour, есть Timeline |
| Back Button Stack, ScreenManager (open source), статьи и форумы | uGUI | — | Стек обработчиков «назад», вызывается только последний | — | Подтверждают общий шаблон |

### 3.2. Общий шаблон

- Один явный стек экранов. «Назад» — Pop, слушает его только верхний экран; нажатие «съедается», чтобы одно нажатие
  не закрыло два экрана (bugnet: после Push/Pop игнорировать «назад» до следующего кадра).
- Колбэки жизненного цикла на показ, скрытие и возврат к экрану (gamedev.net, UnityScreenNavigator).
- Полупрозрачный экран (пауза поверх игры, диалог поверх настроек) — флаг экрана: нижние видимы, но не активны.
- Модальное окно блокирует всё под собой.
- Слои: экраны с историей отдельно от постоянных панелей (HUD) и от затемнения и загрузки.
- Переходы: на время анимации ввод заблокирован — иначе нажатия во время перехода ломают поток (deVoid);
  «маленькая грамматика» переходов вместо своего под каждый экран.
- deVoid о uGUI: любое изменение перестраивает меш до корневого Canvas — динамику держать в своих Canvas и не
  обновлять каждый кадр; якоря вместо абсолютных позиций; опорное разрешение `CanvasScaler` — с первого дня;
  `SetParent(parent, false)`; композиция вместо наследования от `Text`.

### 3.3. Связь UI с игрой

- **deVoid:** игра ⇄ UI только через фасад; игровые события — сигналами, заинтересованный экран обновляется сам;
  данные — пейлоадом при открытии или чтением напрямую. Сам StrangeIOC автор называет в основном шаблонным кодом.
- **QuizU (MVP):** модель не знает о View; View пересылает действия презентеру и не трогает модель; презентер —
  MonoBehaviour ради жизненного цикла, может быть и обычным классом; там, где проще, — прямые ссылки вместо событий.
- (вывод) В терминах нашего гайда: экран — компонент, поднимающий события; оркестратор корня UI связывает их со
  стеком и сервисами. Это тот же шаблон «компонент + оркестратор», что у `Character`.

### 3.4. Data binding (MVVM) в UI Toolkit

- Источник данных — любой C#-объект; свойства — `[CreateProperty]`; режимы `TwoWay`, `ToTarget`, `ToSource`,
  `ToTargetOnce`; конвертеры для отображения.
- По умолчанию привязки обновляются каждый кадр. Для редких обновлений (настройки) — `MarkDirty`, для статистики —
  отслеживание изменений (`INotifyBindablePropertyChanged`, `IDataSourceViewHashProvider`).
- Unity сравнивает с ручными обновлениями: привязки сокращают код синхронизации, ручные — явный контроль момента
  обновления.

### 3.5. Показ и скрытие экранов в UI Toolkit

| Способ | Пока скрыт | Показать снова |
|---|---|---|
| `display: none` | отрисовка пропускается, GPU не тратится | досчитать раскладку |
| `visibility: hidden` | стили и раскладка считаются | перетесселяция |
| `opacity: 0` | рисуется, вершины шейдятся | дёшево |
| `translate` + `DynamicTransform` | всё считается и рисуется | минимально |
| `RemoveFromHierarchy()` | ничего не стоит | дорого |

Созданные заранее и скрытые экраны показываются быстрее, но держат память (QuizU называет это пулом).

## 4. Ввод и навигация

- **UI Toolkit без `EventSystem`** создаёт при входе в Play Mode свою систему событий и берёт действия Input System
  из project-wide actions (`UI/Point`, `UI/Click`, `UI/ScrollWheel`, навигация, Submit, Cancel). **С `EventSystem`**
  ввод ведёт модуль uGUI (`InputSystemUIInputModule`), а панелям UI Toolkit добавляются `PanelRaycaster`
  и `PanelEventHandler`. Фокус у uGUI и UI Toolkit общий: `EventSystem.currentSelectedGameObject`.
- Действия модуля ввода общие для uGUI и UI Toolkit; Tab и Shift+Tab не переназначаются.
- **Фокус в UI Toolkit:** кольцо фокуса по обходу дерева, `tabIndex`, `focusable`, `delegatesFocus`. Пока ничего
  не в фокусе, навигация с клавиатуры не стартует предсказуемо — первый элемент ставят кодом (`Focus()`), иногда
  через кадр. Свои направления — обработчик `NavigationMoveEvent`. `NavigationCancelEvent` фокус не меняет.
- **Input System:** карты `Player` и `UI`; у `PlayerInput` — `SwitchCurrentActionMap`, `DeactivateInput`
  и `ActivateInput`; `PlayerInput` отдаёт свою конфигурацию действий модулю UI — одни действия управляют игроком и UI
  (сэмпл Warriors: меню паузы слушает только тот игрок, кто нажал паузу).
- **«Назад»:** Cancel слушает только верхний экран; защита от повторного срабатывания после Push/Pop.

## 5. Пауза

- `Time.timeScale = 0` останавливает движение и анимацию на игровом времени, `FixedUpdate` и физику,
  `WaitForSeconds`, `Invoke`; `Time.time` стоит. `Update` вызывается по-прежнему.
- Меню — на unscaled time: `unscaledDeltaTime`, `WaitForSecondsRealtime`, Animator — Update Mode Unscaled Time,
  частицы — `useUnscaledTime`, твины — с игнорированием `timeScale`.
- Звук от `timeScale` не встаёт: `AudioListener.pause` (с `ignoreListenerPause` для звуков меню) или своя система.
- Игровой ввод на паузе блокировать.
- Ловушки: нажатие в кадре снятия паузы может сработать в игре (порядок выполнения); после перезагрузки сцены
  `timeScale` остаётся 0 — сбрасывать при загрузке; видео не встаёт само.

## 6. Пиксель-арт и масштаб

- Нужен целый коэффициент: опорное 512×288 даёт 3,75 на 1920×1080 и ровно 5 на 2560×1440.
- **uGUI:** галочка Pixel Perfect у Canvas сдвигает отрисовку на пиксель и не чинит масштаб. Рабочие варианты —
  подобрать опорное разрешение, Constant Pixel Size с целым коэффициентом из своего скрипта (пример с опорным
  640×360 и запасным вариантом для узких экранов), Canvas Scaler из пакета Pixel Perfect.
- **UI Toolkit:** раскладка прилипает к пиксельной сетке, и при нецелом отношении к опорному разрешению элементы
  масштабируются неравномерно (баг в трекере). Обход — Constant Pixel Size с целым коэффициентом.

## 7. Тестирование UI

- **UI Test Framework** (`com.unity.ui.test-framework`, ставится из Unity Registry; сборки
  `Unity.UI.TestFramework.Runtime` и `Unity.UI.TestFramework.Editor`). `UITestFixture` даёт `rootVisualElement`
  и `simulate`; `simulate.Click` шлёт нажатие и отпускание мыши в точку элемента — перекрытый элемент не нажмётся;
  `simulate.FrameUpdate()` — один кадр обработки UI Toolkit. В 6.5 добавлена симуляция для world space UI.
  Документация пока тонкая; диалоги и анимации тестировать сложно (опыт автора статьи, Unity 6.3).
- **uGUI** (вывод): отдельного инструмента нет — `Button.onClick.Invoke()`, `ExecuteEvents`, а для настоящей
  навигации и нажатий — виртуальные устройства Input System (`InputTestFixture`), которые уже есть в наших
  PlayMode-тестах.

## 8. Что это значит для нас

### 8.1. Наш проект

- Unity 6.5, **Built-in RP**, Input System (`activeInputHandler: Both`), TMP.
- DOTween: UI-модули DOTween лежат в `Plugins` без asmdef, из наших сборок доступен только базовый API
  (`DOTween.To`, твины `Transform`). `Plugins` не трогаем.
- Sprite Shaders Ultimate — с шейдерами `GUI SSU` и `Additive GUI SSU` для картинок uGUI.
- UI нет. В `Game.unity` — `EventSystem` с `InputSystemUIInputModule` на дефолтных действиях пакета и 3D-текст
  `YouWin` (TMP в мире). В `PlayerControls` только карта `Player`.
- Опора: `ServiceLocator` и `[Services]`, `StateMachine<TId>`, `IAudioSystem.Pause()` и `Resume()`, режим реального
  времени у `SpriteAnimator`, спроектированный SaveSystem со слотами и `meta.json` для меню.

### 8.2. Решения (2026-10-23)

| Вопрос | Решение | Почему |
|---|---|---|
| Какой UI будет | Базовые меню, HUD, сочная анимация; сложных экранов (инвентарь, карта) не планируем | ответ пользователя |
| Стиль | Пиксельный или HD — на первом макете, заложены оба режима масштаба | ответ пользователя |
| UI-система | uGUI | частицы, keyframe-анимации и свои шейдеры доступны сейчас, у UI Toolkit в Built-in RP — нет; DOTween и SSU уже в проекте; связи через инспектор, как во всём проекте; рекомендация Unity 6.5 для рантайма |
| Навигация | Свой модуль `ScreenStack` | общий шаблон всех решений; немного кода с тестами; без внешней зависимости; контракт `IScreen` не зависит от UI-системы — отдельный экран можно потом сделать на UI Toolkit |
| Готовые библиотеки | Не берём | UnityScreenNavigator — загрузка по путям, асинхронный цикл, уничтожение закрытых экранов: избыточно для 6–8 экранов |
| Паттерны | Стек экранов; экран — View без знания игры (облегчённый MVP); оркестратор корня UI; глобальное — сервисы через `ServiceLocator`; события C# | совпадает с гайдом проекта: компонент + оркестратор |
| Data binding | Нет, данные в UI задаются явно, по событиям | в uGUI его нет; явный контроль |
| Гибрид UI Toolkit + uGUI | Нет | две UI-системы сразу — не «просто» |

## Источники

**Unity, официальное**
- [Comparison of UI systems in Unity — Unity 6.5](https://docs.unity.com/en-us/engine/6000.5/manual/uitoolkits/ui-system-compare.md)
- [UI Toolkit development status and next milestones — November 2025](https://discussions.unity.com/t/ui-toolkit-development-status-and-next-milestones-november-2025/1698009)
- [UI Shader Graph — Unity 6.5](https://docs.unity.com/en-us/engine/6000.5/manual/uitoolkits/uielements/uie-uss/ui-shader-graph.md)
- [QuizU: Managing menu screens in UI Toolkit (Post 2)](https://discussions.unity.com/t/quizu-managing-menu-screens-in-ui-toolkit-post-2/310272)
- [QuizU: The Model View Presenter pattern (Post 4)](https://discussions.unity.com/t/quizu-the-model-view-presenter-pattern-post-4/311043)
- [The QuizU UI Toolkit sample: updated for Unity 6 (Post 1)](https://discussions.unity.com/t/the-quizu-ui-toolkit-sample-now-updated-for-unity-6-post-1/308607)
- [UI Toolkit: New and updated demos for programmers and artists](https://unity.com/blog/engine-platform/new-ui-toolkit-demos-for-programmers-artists)
- [E-book: UI Toolkit for advanced Unity developers (Unity 6 edition)](https://discussions.unity.com/t/new-e-book-for-ui-artists-designers-and-programmers-ui-toolkit-for-advanced-unity-developers-unity-6-edition/1628703)
- [Best practices: Data binding](https://docs.unity.com/en-us/engine/6000.3/manual/best-practice-guides/bpg-uiad-index/data-binding)
- [Best practices for managing elements](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-best-practices-for-managing-elements.html)
- [Runtime UI event system and input handling](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Runtime-Event-System.html)
- [FAQ for event and input system](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-faq-event-and-input-system.html)
- [Navigation events](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-Navigation-Events.html)
- [Focus order of elements](https://docs.unity3d.com/Manual/UIE-focus-order.html)
- [Input System: GameObject components for input](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.3/manual/Components.html)
- [Learn the Input System… sample project Warriors](https://unity.com/blog/technology/learn-the-input-system-with-updated-tutorials-and-our-sample-project-warriors)
- [UI Test Framework: install and set up](https://docs.unity3d.com/Packages/com.unity.ui.test-framework@1.0/manual/install-and-set-up.html)
- [UI Test Framework 6.5 changelog](https://docs.unity3d.com/Packages/com.unity.ui.test-framework@6.5/changelog/CHANGELOG.html)
- [Issue tracker: VisualElements scaled inconsistently with Scale With Screen Size](https://issuetracker.unity.com/issues/365/visualelements-are-scaled-inconsistently-when-using-scale-with-screen-size-and-the-rendered-resolution-is-not-a-multiple-of-reference-resolution)

**Фреймворки и статьи**
- [A UI system architecture and workflow for Unity (deVoid UI, Game Developer)](https://www.gamedeveloper.com/programming/a-ui-system-architecture-and-workflow-for-unity) · [репозиторий](https://github.com/yankooliveira/uiframework)
- [UnityScreenNavigator (Haruma-K)](https://github.com/Haruma-K/UnityScreenNavigator/blob/master/README.md)
- [unity-back-button-stack](https://github.com/unitycoder/unity-back-button-stack) · [ScreenManager](https://github.com/forestrf/ScreenManager) · [Game screens (gamedev.net)](https://gamedev.net/blogs/entry/2250075-game-screens)
- [How to fix back button popping two menus (bugnet, по выдержке поиска)](https://bugnet.io/blog/how-to-fix-back-button-popping-two-menus-from-stacked-input)
- [The right way to pause the game in Unity (Game Dev Beginner)](https://gamedevbeginner.com/the-right-way-to-pause-the-game-in-unity/)
- [Unity 6.3: UI Test Framework — пример теста (Qiita)](https://qiita.com/tamutamuta/items/589f5dff68825ea4b4dc)

**Сообщество**
- [What is your main UI System for Unity Development and why?](https://discussions.unity.com/t/what-is-your-main-ui-system-for-unity-development-and-why/1701518)
- [Девлог: replacing UI Toolkit (itch.io, по выдержке поиска)](https://itch.io/devlog/943542/quick-update-of-behind-the-scenes-replacing-ui-toolkit)
- [Pixel Perfect Canvas (or lack thereof)](https://discussions.unity.com/t/pixel-perfect-canvas-or-lack-thereof/885516) · [Canvas scaling UI with Pixel Perfect camera](https://discussions.unity.com/t/canvas-scaling-ui-with-pixel-perfect-camera/817270)
