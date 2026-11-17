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
