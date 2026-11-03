# Atomic: руководство по работе с фреймворком и его документацией

Это руководство по фреймворку [Atomic](https://github.com/StarKRE22/Atomic) и по его документации — 498 markdown-файлов апстрима плюс 8 страниц по генераторам. Оно не пересказывает документацию, а делает две вещи, которых документация не делает: **навигирует** по ней и **сверяет её с исходниками**.

Второе оказалось важнее, чем ожидалось. См. раздел «Чему здесь можно верить».

---

## Как этим пользоваться

| Я хочу… | Куда идти |
|---|---|
| понять модель фреймворка с нуля | [01-tutorials.md](01-tutorials.md), раздел «Модель» |
| писать идиоматично, а не «Atomic-образно» | [02-bestpractices.md](02-bestpractices.md) |
| найти, чем заменить конкретный тип | [90-MIGRATION.md](90-MIGRATION.md), раздел «Переименованные и изменённые типы» |
| понять, что не так с нашим кодом | [91-CODE-AUDIT.md](91-CODE-AUDIT.md) |
| найти нужную страницу апстрим-доков | раздел «Карта документации» ниже, затем «Где читать» в нужной главе |
| выжать производительность | раздел «Производительность» в каждой главе — там отделено замеренное от предположений |

Каждая глава устроена одинаково: **Модель** → **API** → **Идиомы** → **Производительность** → **Грабли** → **Где читать**. Последний раздел — это аннотированный индекс апстрим-страниц по теме главы, по строке на файл. Он и есть ответ на вопрос «как работать с документацией»: читать её целиком незачем, нужно знать, на какой из 498 страниц лежит ответ.

---

## Карта документации апстрима

Корень: `C:/!!!Work/_AtomicUpstream/Docs`. В скобках — число `.md` файлов.

**Точки входа**

- `Tutorials/` (7) — связный курс от «что такое атомарный подход» до «как убрать Unity из логики». Единственная часть документации, которую стоит прочесть последовательно и целиком.
- `BestPractices/` (29) — по одному решению на файл: когда Cooldown, а когда Timer; почему Request вместо Action; как итерировать коллекции без аллокаций. Самая полезная папка на единицу объёма.
- `README.md` в корне репозитория — установка, настройка генераторов, quick start.

**Elements** — атомарные примитивы: данные и поведение по отдельности.

| Папка | Файлов | Что внутри |
|---|---|---|
| `Elements/Manual.md` | 1 | оглавление модуля |
| `Elements/Values` | 6 | `IValue<T>`, `Const<T>`, неизменяемые значения |
| `Elements/Variables` | 12 | `IVariable<T>`, `Variable<T>`, реактивные переменные, прокси к Transform/GameObject/PlayerPrefs |
| `Elements/Setters` | 3 | `ISetter<T>`, `InlineSetter` |
| `Elements/Actions` | 45 | `IAction` и арности до 4 аргументов, композиты, Mono-обёртки |
| `Elements/Functions` | 19 | `IFunction`, `IPredicate`, инлайн-формы |
| `Elements/Expressions` | 38 | составные выражения: And/Or/Sum/Mul, `ExpressionBase` |
| `Elements/Events` | 36 | `ISignal`, `IEvent`, подписки как disposable-структуры |
| `Elements/Requests` | 13 | паттерн Request-Condition-Action-Event |
| `Elements/Commands` | 4 | `ICommand` |
| `Elements/Collections` | 17 | реактивные массив, список, словарь, связный список |
| `Elements/Time` | 32 | `Cooldown`, `DownTimer`, `UpTimer`, `Stopwatch`, `Period`, таймстемпы |
| `Elements/Utils` | 13 | `Optional<T>`, `DisposableComposite`, компараторы, диспетчер главного потока |
| `Elements/Performance` | 5 | бенчмарки — **только по реактивным коллекциям**, по остальным областям цифр нет |

**Entities** — сущности как контейнеры состояния плюс поведения.

| Папка | Файлов | Что внутри |
|---|---|---|
| `Entities/Manual.md` | 1 | оглавление модуля |
| `Entities/Entities` | 39 | `IEntity`, `Entity`, `MonoEntity`, синглтоны, прокси, отладка |
| `Entities/KeyStore` | 6 | `TagKey`, `ValueKey`, `EntityKeyStore`, алгоритмы ключей |
| `Entities/Behaviours` | 18 | `IEntityBehaviour` и интерфейсы жизненного цикла |
| `Entities/Lifecycle` | 2 + 4 + 7 | `Lifecycle/`, `Lifecycle/Sources/`, `Lifecycle/Subscriptions/` — источники Init/Enable/Tick и подписки на них |
| `Entities/Installers` | 8 | `IEntityInstaller`, `MonoEntityInstaller`, Scriptable-варианты |
| `Entities/Aspects` | 8 | `IEntityAspect` — обратимые наборы изменений сущности |
| `Entities/Baking` | 3 | `MonoEntityBaker` — превращение авторского `MonoEntity` в обычный `Entity` |
| `Entities/Bootstrap` | 2 | `ScriptableEntityBootstrapper` — спавн до/после загрузки сцены |
| `Entities/Factories` | 13 | `IEntityFactory<TEntity,TArgs>`, каталоги, `IArgs`/`NoArgs` |
| `Entities/Pooling` | 16 | `IEntityPool`, префабные и мульти-пулы, async-инициализация |
| `Entities/Worlds` | 9 | `IEntityWorld`, `MonoEntityWorld` |
| `Entities/Filters` | 25 | `EntityFilter<E>` и триггеры: tag/value/state/behaviour |
| `Entities/Collections` | 8 | `IEntityCollection<E>` со struct-энумератором |
| `Entities/Registry` | 1 | `EntityRegistry` — глобальный поиск по InstanceID |
| `Entities/Systems` | 6 | `EntitySystem<E>` — батчинг с бюджетом кадра |
| `Entities/UI` | 13 | `EntityView`, каталоги и пулы представлений |
| `Entities/Inspector` | 4 | атрибуты для инспектора |
| `Entities/CodeGen` | 4 | `[EntityExtensionsAPI]`, `[EntityDomainBehaviours]` |
| `Entities/EditMode` | 1 | поведения, работающие в редакторе |

**Events** — шина событий (модуля с таким назначением в нашей версии нет вообще).

| Папка | Файлов | Что внутри |
|---|---|---|
| `Events/Manual.md`, `Events/Extensions.md` | 2 | обзор и расширения |
| `Events/Bus` | 6 | `IEventBus`, `EventBus`, Mono- и thread-safe варианты |
| `Events/Keys` | 7 | `EventKey<TBus, …>`, алгоритмы ключей |
| `Events/Subscriptions` | 2 | disposable-подписки |
| `Events/CodeGen` | 3 | `[EventExtensionsAPI]` |

**Генераторы** — отдельно, не под `Docs/`: `Assets/Plugins/Atomic/SourceGenerators/Project~/Docs/` (8 файлов). Здесь лежит `Setup.md` — инструкция по расстановке лейблов `RoslynAnalyzer`, без которой ничего не компилируется.

---

## Порядок чтения с нуля

1. `Docs/Tutorials/1. What is Atomic Approach.md` … `7. Summary.md` — целиком, по порядку. Это полтора часа и единственный способ понять, зачем всё остальное выглядит так.
2. [01-tutorials.md](01-tutorials.md) этого руководства — та же модель, но со сверенными подписями и 23 идиомами.
3. `README.md` апстрима, раздел Code Generation Setup, и `Project~/Docs/Setup.md`.
4. [02-bestpractices.md](02-bestpractices.md) — 29 решений, которые иначе придётся принимать наугад.
5. Дальше — по задаче, через «Где читать» нужной главы.

---

## Чему здесь можно верить

Руководство собиралось в два прохода: первый агент читал документацию области целиком, второй сверял каждое утверждение с исходниками и тестами фреймворка. Числа за всю работу:

| Показатель | Значение |
|---|---|
| прочитано страниц документации | 498 + 8 |
| проверено фрагментов кода при написании | 781 |
| **утверждений документации отброшено как неподтверждённые** | **227** |
| ошибок исправлено на ревью глав | 183 |
| осталось оговорок (область/уровень доказанности) | 37 |

**227 отброшенных утверждений — это не шум.** Документация апстрима местами описывает API, которого в коде нет. Подтверждённые примеры:

- **`EntitySingleton<E>`** — есть страница `Docs/Entities/Entities/EntitySingleton.md` с описанием `SetFactory` / `DisposeInstance` / `ResetInstance`. В исходниках **ноль вхождений**. Существует только `MonoEntitySingleton<E>`.
- **`InlineEntityFactory`** — есть `Docs/Entities/Factories/InlineEntityFactory.md`. В исходниках **ноль вхождений**.
- **`MultiEntityFactory`** — есть страница; в коде это только строка в `CreateAssetMenu` у `ScriptableEntityCatalog`.
- **`[Unsafe(false)]`** — показано в `Docs/Entities/CodeGen/EntityExtensionsAPIAttribute.md`, но `UnsafeAttribute` объявлен без конструктора с аргументами. Не скомпилируется.
- **`ref int healthRef = ref entity.RefHealth()`** — пример оттуда же для `ValueKey<IEntity,int>`. `GetValueUnsafe<T>` ограничен `where T : class`, так что unsafe-доступ к `int` невозможен в принципе.
- **`EntityWorld<E>` нельзя предразмерить.** Все четыре конструктора уходят в `base()` с `DEFAULT_CAPACITY = 3`. Общий совет «задавайте ёмкости явно» для миров неисполним.
- **`EntitySystemBase<E>._batchSize` стартует с нуля** — первый `Update` обрабатывает ноль сущностей, батч растёт на 256 за кадр. Система разогревается несколько кадров.
- **`Docs/Entities/Filters/Manual.md`** утверждает ленивое вычисление фильтра и полный пересчёт по триггеру. Исходник строит состояние в конструкторе, а `Synchronize(E)` пересчитывает предикат только для изменившейся сущности.
- **`DownTimer.md:436`** утверждает, что `SetDuration` поднимает `OnDurationChanged` и `OnProgressChanged`. Код поднимает только первое.

**Практический вывод: не пишите код по примеру из документации, не найдя этот метод в исходниках.** Исходники лежат рядом — `Assets/Plugins/Atomic/` — и являются единственным источником истины. Каждая глава здесь уже прошла эту сверку; документация апстрима — нет.

### Границы проверки

Чтобы не создавать ложной уверенности:

- Проверка **статическая** — grep по объявлению плюс чтение файла. Снипеты не компилировались в Unity-проекте.
- Утверждения о производительности разделены в тексте на **замеренное**, **заявленное авторами**, **прочитанное в исходнике** и **вывод**. Бенчмарки в репозитории есть только по реактивным коллекциям; всё остальное — чтение кода.
- Сравнения с внутренностями Unity (например, совпадает ли `EqualityComparer` фреймворка с `Vector3.GetHashCode` самой Unity) проверены только со стороны фреймворка — исходников Unity под рукой нет.
- В главах имена generic-файлов местами записаны через апостроф (`EntityWorld'1.cs`), тогда как на диске через бэктик. Исключение — `TagKey'1.cs` и `ValueKey'1.cs`, где апостроф настоящий.
- **Генераторы нельзя пересобрать из поставки.** В `SourceGenerators/Project~/` лежат 25 `.cs` исходников и `Directory.Build.props`, но `.csproj` и `.sln` отсутствуют — остались только их `.meta`. `Setup.md` предлагает `dotnet build Atomic.SourceGenerators.sln`, выполнить это на текущем дереве невозможно. DLL придётся принимать как бинарные блобы либо восстанавливать файлы проекта руками.

---

## Главы

| # | Файл | Тема |
|---|---|---|
| 01 | [01-tutorials.md](01-tutorials.md) | подход Atomic: сущность, состояние, поведение; 23 идиомы |
| 02 | [02-bestpractices.md](02-bestpractices.md) | 29 прикладных решений апстрима |
| 03 | [03-elements-core.md](03-elements-core.md) | значения, переменные, сеттеры, утилиты |
| 04 | [04-elements-actions.md](04-elements-actions.md) | действия и команды |
| 05 | [05-elements-func.md](05-elements-func.md) | функции, предикаты, выражения |
| 06 | [06-elements-events.md](06-elements-events.md) | сигналы, события, реквесты |
| 07 | [07-elements-coll.md](07-elements-coll.md) | реактивные коллекции |
| 08 | [08-elements-time.md](08-elements-time.md) | кулдауны, таймеры, периоды |
| 09 | [09-entities-core.md](09-entities-core.md) | `IEntity`, ключи, хранилище значений |
| 10 | [10-entities-behave.md](10-entities-behave.md) | поведения и жизненный цикл |
| 11 | [11-entities-install.md](11-entities-install.md) | инсталлеры, аспекты, бейкинг, бутстрап |
| 12 | [12-entities-factory.md](12-entities-factory.md) | фабрики и пулы |
| 13 | [13-entities-world.md](13-entities-world.md) | миры, фильтры, триггеры, реестр |
| 14 | [14-entities-systems.md](14-entities-systems.md) | системы, UI-представления, инспектор |
| 15 | [15-codegen.md](15-codegen.md) | source generators, анализаторы, настройка |
| 16 | [16-events.md](16-events.md) | шина событий |

## Документы по нашему проекту

| # | Файл | Тема |
|---|---|---|
| 90 | [90-MIGRATION.md](90-MIGRATION.md) | миграция: 3 удалённых модуля, 28 изменённых типов, yaml → генераторы, 14 шагов, 12 рисков |
| 91 | [91-CODE-AUDIT.md](91-CODE-AUDIT.md) | аудит текущего кода: 116 находок (49 высокой важности) с файлом, строкой и исправлением |

---

## Что важно знать до начала миграции

Три факта из [90-MIGRATION.md](90-MIGRATION.md), которые меняют оценку работы:

1. **Имена сгенерированных методов не изменились.** `GetXxx` / `AddXxx` / `SetXxx` / `HasXxxTag` в новой версии те же. ~184 `Get`, 89 `Add`, 89 `Set` вызовов компилируются без правок. Это не переписывание проекта, а замена слоя контекстов и кодогенерации.
2. **GUID'ы `.meta` сохранены для типов, привязанных в сценах** — `SceneEntity` → `MonoEntity`, `SceneEntityInstaller` → `MonoEntityInstaller` и ещё шесть. Но у `EntityFilter`, `EntityView` и `SceneAction` → `MonoAction` GUID изменились, а `[SerializeReference]` хранит имя типа — значит переименованные классы за такими полями отвалятся в `null` при живом GUID файла. Частичная поломка сериализации опаснее полной.
3. **Проект не будет компилироваться между шагом 2 и шагом 12.** Инкрементального пути нет: модуль Context удаляется целиком, тестовый набор заменяется апстримовым в тот же момент. Отдельная ветка обязательна.
