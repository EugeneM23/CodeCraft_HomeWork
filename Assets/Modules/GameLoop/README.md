# GameLoop

Игровой цикл с **честной паузой** для Unity. Игровой код не пишет `Update`/`FixedUpdate`/`LateUpdate`, а
подписывается на цикл; пауза — цикл перестаёт раздавать тики, а «морозилки» останавливают то, что Unity двигает сама
(физика, Animator, частицы, звук). `Time.timeScale` не трогается: меню, загрузка и всё на `unscaledDeltaTime` работают
как обычно, а мир стоит целиком и после паузы продолжает с того же места.

Модуль самостоятельный: не зависит ни от игры, ни от плагинов, только Unity. Проверено на Unity 6000.5.

## Состав

```
GameLoop/
├── Runtime/        (сборка GameLoop, namespace Ticking)
│   ├── IGameLoop            — сервис: подписка, пауза с держателями, игровое время
│   ├── GameLoop             — сам цикл (обычный C#): Update / FixedUpdate / LateUpdate от владельца
│   ├── IUpdateListener, IFixedUpdateListener, ILateUpdateListener, IPauseListener — подписчики
│   ├── GameLoopRunner       — MonoBehaviour: Unity → цикл, морозилки движка галками в инспекторе
│   └── Freezers/            — ComponentFreezer<T> (база сканирующих), Physics2DFreezer, PhysicsFreezer,
│                              AnimatorFreezer, LegacyAnimationFreezer, ParticleSystemFreezer, AudioListenerFreezer
├── Editor/         (GameLoop.Editor) — инспектор раннера: состояние, кто держит паузу, слушатели, кнопка паузы
└── Tests/
    ├── Editor/     (GameLoop.Tests) — ядро на фейковых слушателях
    └── Runtime/    (GameLoop.Tests.Runtime) — раннер на реальных кадрах, морозилки
```

## Быстрый старт

1. Компонент `GameLoopRunner` — на объект, который живёт всю игру (сервисы, `DontDestroyOnLoad`). Его `Loop`
   отдайте коду так, как принято в проекте (сервис-локатор, DI, синглтон).
2. Игровой компонент подписывается на нужные фазы:

```csharp
using Ticking;

public class Saw : MonoBehaviour, IFixedUpdateListener
{
    [SerializeField] private float _speed = 180f;
    private IGameLoop _loop;

    private void OnEnable()
    {
        _loop = Services.GameLoop;   // как ваш проект находит цикл
        _loop.Add(this);
    }

    private void OnDisable() => _loop.Remove(this);

    // Без паузы — каждый фиксированный шаг; в паузе не зовётся вовсе
    void IFixedUpdateListener.OnFixedUpdate(float deltaTime) => transform.Rotate(0f, 0f, _speed * deltaTime);
}
```

3. Пауза — с держателем: кто поставил, тот и снимает.

```csharp
_loop.Pause(this);    // меню открылось
_loop.Resume(this);   // меню закрылось: игра пойдёт, если паузу больше никто не держит
```

## Правила цикла

| Что | Как |
|---|---|
| Фазы | `OnFixedUpdate(dt)` — каждый фиксированный шаг до физики; `OnUpdate(dt)` — каждый кадр; `OnLateUpdate(dt)` — после всех `OnUpdate`. Объект реализует только нужные интерфейсы |
| Порядок | `[DefaultExecutionOrder]` класса, как у Unity; при равенстве — порядок `Add` |
| Изменения во время прохода | Добавленный тикает со следующего прохода, удалённый перестаёт сразу |
| Выключенный / уничтоженный | `Behaviour` с `isActiveAndEnabled == false` не тикает, уничтоженный выбрасывается из цикла |
| Исключение | Логируется (`Debug.LogException`), остальные тикают дальше |
| Повторный `Add` | Ничего не делает; объект без интерфейсов — `ArgumentException` |
| Игровое время | `Time` — сумма `dt` из `Update`, стоит в паузе |
| Профайлер | Каждый слушатель — свой образец с именем своего класса под `GameLoopRunner.FixedUpdate/Update/LateUpdate`: видно, сколько стоит каждый, а не один кусок на весь цикл. Только при `ENABLE_PROFILER` (эдитор, development-сборка) |

## Пауза

- **Держатели.** `Pause(source)` / `Resume(source)`, `IsPausedBy(source)`, `PauseSources` — кто держит сейчас.
  Игра стоит, пока держит хоть один; повторный `Pause` того же держателя ничего не делает. Меню, загрузка, катсцена,
  потеря фокуса — каждый со своей паузой и не снимает чужую.
- **Уничтоженный держатель** (Unity-объект) отпускает паузу сам — на ближайшем `Update`/`FixedUpdate` цикла.
  Меню, умершее вместе со сценой, не оставит игру на паузе навсегда.
- **Слушатели паузы** (`IPauseListener`) получают `OnPause` / `OnResume` на переходах. Добавленный в уже стоящую
  игру сразу получает `OnPause`. `PauseChanged(bool)` — то же событием.
- **Когда вступает.** Проход, во время которого поставили паузу, доходит до конца; следующий уже не начнётся.
- `ResumeAll()` — снять все паузы (владелец выключает цикл, тесты).

## Морозилки

То, что Unity двигает сама, цикл не остановит — это делают морозилки, обычные `IPauseListener`. На `OnPause`
морозилка останавливает подсистему и запоминает, что тронула; на `OnResume` возвращает ровно это.

| Морозилка | Что делает | В раннере |
|---|---|---|
| `Physics2DFreezer` | `Physics2D.simulationMode = Script` — мир стоит, скорости и контакты целы | `Physics 2D` ✔ |
| `PhysicsFreezer` | то же для 3D | `Physics 3D` ✔ |
| `AnimatorFreezer` | `Animator.speed = 0`, кроме `UnscaledTime` (меню) | `Animators` ✔ |
| `LegacyAnimationFreezer` | скорость состояний legacy `Animation` = 0 | `Legacy Animations` ✔ |
| `ParticleSystemFreezer` | `Pause` / `Play` играющих систем, кроме `useUnscaledTime` (меню) | `Particles` ✔ |
| `AudioListenerFreezer` | `AudioListener.pause` (звукам меню — `ignoreListenerPause`) | `Audio Listener` — выкл.: у игры обычно свой звук |

Сканирующие морозилки — наследники `ComponentFreezer<T>`: ищут компоненты загруженных сцен на паузу, а сцену,
загруженную во время паузы (смена сцены за затемнением, аддитивная загрузка), морозят на `sceneLoaded` — до первого
кадра её объектов. Своя подсистема (движок звука, свой аниматор, камера) — своя морозилка, добавленная в цикл:
`loop.Add(new MyFreezer())`. Для компонентов — наследник `ComponentFreezer<T>` с двумя методами:

```csharp
public sealed class VideoFreezer : ComponentFreezer<VideoPlayer>
{
    protected override bool Freeze(VideoPlayer video)   // true — остановлен, вернуть на OnResume
    {
        if (!video.isPlaying)
            return false;
        video.Pause();
        return true;
    }

    protected override void Thaw(VideoPlayer video) => video.Play();
}
```

Морозилки физики в редакторе дублируют прежний режим в `SessionState`: если Play Mode кончится без их `OnResume`
(перекомпиляция в Play Mode теряет цикл), режим вернётся на выходе в Edit Mode.

Сохранение, сделанное во время паузы, видит то, что поставили морозилки (`Animator.speed = 0`, частицы на паузе) —
если такие объекты сохраняются, оттаивайте морозилки перед сохранением.

Раннер при уничтожении снимает все паузы — режим физики не останется `Script` в Project Settings. Выключенный раннер
перестаёт тикать, но ничего не морозит: ставить паузу — только через цикл.

## Что остаётся вне цикла

UI, загрузка сцен, сохранения, отладочные клавиши — на обычном `Update` и `unscaledDeltaTime`: им пауза не нужна.
Корутины с `WaitForSeconds`, `Invoke` и твины идут по времени Unity и паузу не видят — в игровом коде вместо них
таймеры-поля, которые тикают от цикла. Шейдеры на `_Time` в паузе идут.

## Тесты

Цикл — обычный C#: тест создаёт `new GameLoop()`, добавляет объекты и шагает `FixedUpdate(dt)`/`Update(dt)` сам,
детерминированно и без кадров. Пауза в тесте — `loop.Pause(this)`. Физику в таком тесте шагает сам тест
(`Physics2D.Simulate`) — и не шагает, пока цикл на паузе.
