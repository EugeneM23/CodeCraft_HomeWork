# Audio Engine 1.0.0

Звуковой движок из Platformer (`module/AudioEngine`, коммит `6e1cb8de`). Нужен Odin Inspector 4.

Как это озвучивает нашу игру и куда класть клипы — `Docs/Audio/README.md`. Здесь — только сам модуль.

## Части

| Часть | Что это |
|---|---|
| `AudioSystem` | Компонент на сцене. Держит пул `AudioSource`, загруженные банки, параметры, играющие события. `Pause()` / `Resume()` замораживают все его события |
| `AudioBank` | ScriptableObject: идентификатор банка + список событий, параметры (`bool`, `int`, `float`) и колбэки. Полный ключ события — `<банк>.<событие>` |
| `AudioSourceEvent` | ScriptableObject одного звука: клип (один, случайный, без повтора, по весам, по очереди), громкость, высота, 2D/3D, группа микшера, петля, поведения и действия по времени |
| Поведения | Высота и громкость по кривой или по параметру банка, фильтры, проигрывание другого события в случайный момент |
| `AudioEventHandle` | Ручка на созданное событие: `Play`, `Stop(fade)`, `Dispose(fade)`, позиция |
| `MusicPlayer`, `MusicPlaylist`, `MusicCatalog` | Музыка: один `AudioSource`, плейлисты по порядку или случайно |
| `UISoundPlayer`, `UISoundCatalog`, `UISoundComponent` | Короткие звуки интерфейса через `PlayOneShot` |
| Генераторы (Editor) | Кнопка **Compile** в банке пишет `<Банк>BankAPI.cs` с константами ключей; генераторы UI пишут `Codegen/UI/UISoundAPI.cs` и `UISoundComponent.cs` |

## Быстрый старт

```csharp
AudioSystem audio = AudioSystem.Resolve(gameObject);      // AudioSystem сцены
audio.PlayEvent("tank.shot", muzzle.position);              // разовый звук в точке
audio.PlayEvent("tank.engine", hull, out AudioEventHandle engine); // петля, едет за объектом
audio.SetFloat("tank.rpm", 0.7f);                           // параметр банка
engine.Dispose(0.3f);                                       // погасить за 0.3 с
```

`maxFrequency` у `PlayEvent` — окно в секундах, в которое повтор того же события не проигрывается (защита от сотни одинаковых звуков за кадр).

## Длительность события

`duration = 0` — событие длится столько, сколько клип (с учётом высоты). Задайте длительность явно, только если звук надо оборвать раньше или держать дольше клипа.

## Наши правки ядра (только исправления ошибок)

| Файл | Что исправлено |
|---|---|
| `Scripts/Scene/Events/AudioSourceEvent.cs` | `using UnityEditor` без `#if UNITY_EDITOR` ломал сборку плеера. `duration = 0` завершал событие на первом кадре — теперь длина берётся из клипа. Петля без клипа не перезапускается каждый кадр. Повторное присваивание того же клипа больше не перезапускает петлю |
| `Scripts/Scene/System/Implementation/AudioSystem_Singleton.cs` | Устаревший `FindObjectOfType` → `FindAnyObjectByType`. `Resolve` не возвращал список в `ListPool`, когда находил систему |
| `Scripts/Scene/System/Implementation/AudioSystem_Events.cs` | `PlayEvent(id, position, rotation, …)` терял поворот. Добавлен `DisposeEvent(event, curve, fade)` для ручки |
| `Scripts/Scene/AudioEventHandle.cs` | `Dispose(fade)` гасил первое событие с тем же id, а не своё |
| `Scripts/Scene/Behaviours/Event/AudioEventBehaviour_PlayAudioEventInRandomInterval.cs` | Время срабатывания выбиралось в `Reset()`, который никто не звал: звук срабатывал сразу и один раз |
| `Scripts/AssemblyInfo.cs` | Новый: `InternalsVisibleTo("AudioEngine.Tests")` |

## Тесты

`Tests/AudioEngine.Tests.asmdef`, EditMode: загрузка и выгрузка банка, неизвестный ключ, `maxFrequency`, длительность по клипу, петля, `Dispose` своей ручки, пауза.
