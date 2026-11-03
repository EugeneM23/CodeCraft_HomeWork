# 07. Elements: реактивные коллекции

`Atomic.Elements` даёт пять семейств коллекций, которые заменяют `T[]`, `List<T>`, `Dictionary<K,V>`, `HashSet<T>` и односвязный список и при каждом изменении поднимают события. Это слой *состояния* сущности: компонент держит у себя `ReactiveList<ItemData>` или `ReactiveDictionary<string,int>`, а UI и поведения подписываются на `OnItemAdded` / `OnItemRemoved` / `OnItemChanged` / `OnStateChanged` вместо опроса в `Update`. Словарь и множество — не обёртки над BCL-типами: это написанные руками открытые хеш-таблицы над `Slot[]` + `int[] buckets` с prime-лестницей ёмкости, free-list и буферами `ArrayPool<T>.Shared` для отложенной рассылки событий. Все пять типов хранят данные в плоских массивах и выдают публичный `struct Enumerator`, поэтому `foreach` по конкретному типу не аллоцирует. Модуль при этом содержит несколько подтверждённых в исходниках дефектов, которые ломают не производительность, а корректность, — они перечислены в разделе «Грабли», и их нужно прочитать до того, как типы попадут в прод-код.

---

## Модель

### Что здесь есть

Пять конкретных классов, все в пространстве имён `Atomic.Elements`:

| Тип | Хранение | Индексный доступ | Где лежит |
|---|---|---|---|
| `ReactiveArray<T>` | `T[] items`, фиксированный размер | O(1) | `Collections/Array/ReactiveArray.cs` |
| `ReactiveList<T>` | `T[] items` + `int count`, рост удвоением | O(1) | `Collections/List/ReactiveList.cs` |
| `ReactiveLinkedList<T>` | `Node[] _nodes` + free-list, **односвязный** | O(N) | `Collections/List/ReactiveLinkedList.cs` |
| `ReactiveHashSet<T>` | `Slot[] _slots` + `int[] _buckets` | нет | `Collections/Set/ReactiveHashSet.cs` |
| `ReactiveDictionary<K,V>` | `Slot[] _slots` + `int[] _buckets` | по ключу | `Collections/Dictionary/ReactiveDictionary.cs` |

Отсортированных реактивных коллекций в поставке нет. Файлы тестов `Tests/EditMode/Collections/ReactiveSortedListTests.cs`, `ReactiveSortedSetTests.cs`, `ReactiveSortedDictionaryTests.cs` существуют, но закомментированы целиком (486 / 379 / 486 строк, в каждом файле каждая непустая строка начинается с `//`), а типов `ReactiveSortedList` / `ReactiveSortedSet` / `ReactiveSortedDictionary` в `Scripts/` нет. Если какая-то страница документации их упоминает — она описывает код, который не поставляется.

### Четыре события и два параллельных семейства

Базовый контракт — `IReadOnlyReactiveCollection<T>`: `OnStateChanged` (без аргументов), `OnItemAdded` и `OnItemRemoved` как `Action<T>`. Индексные коллекции добавляют вторую, *параллельную* тройку с индексом:

* `IReadOnlyReactiveArray<T>` — `event Action<int, T> OnItemChanged` и свой `OnStateChanged`;
* `IReadOnlyReactiveList<T>` — `new event Action<int, T> OnItemAdded`, `new event Action<int, T> OnItemRemoved`, `new event Action OnStateChanged`;
* `IReadOnlyReactiveDictionary<K,V>` — `new event Action<K, V> OnItemAdded`, `new event Action<K, V> OnItemRemoved`, `event Action<K, V> OnItemChanged`.

Ключевое слово `new` здесь несущее: у списка одновременно живут `Action<int,T> OnItemAdded` и `Action<T> OnItemAdded`, и **какое из них вы получите, определяет статический тип ссылки, через которую вы подписываетесь**. У `ReactiveList<T>` индексная тройка объявлена публичными полями-событиями, а неиндексная — явной реализацией интерфейса, то есть доступна только через приведение к `IReadOnlyReactiveCollection<T>`. То же у `ReactiveLinkedList<T>` и `ReactiveDictionary<K,V>` (там неиндексная пара имеет тип `Action<KeyValuePair<K,V>>`).

У множества `OnItemChanged` нет вообще — ни на `IReactiveSet<T>`, ни на `ReactiveHashSet<T>`. Пример в `IReactiveSet.md`, строка 104, подписывается на `reactiveSet.OnItemChanged` и не компилируется.

### Иерархия интерфейсов (по исходникам, не по докам)

```
IReadOnlyCollection<T>
  └ IReadOnlyReactiveCollection<T>          — OnStateChanged, OnItemAdded/Removed : Action<T>
       └ IReactiveCollection<T> : ICollection<T>          (маркер, своих членов нет)
            └ IReactiveSet<T> : ISet<T>                   (маркер, своих членов нет)

IReadOnlyList<T>
  ├ IArray<T>                               — Length, this[int] {get;set;}, Clear, Populate, Fill, Resize
  └ IReadOnlyReactiveArray<T>               — OnItemChanged : Action<int,T>, OnStateChanged, CopyTo×2
       └ IReactiveArray<T> : IArray<T>, IReadOnlyReactiveArray<T>
       └ IReadOnlyReactiveList<T> : IReadOnlyReactiveArray<T>, IReadOnlyReactiveCollection<T>
            └ IReactiveList<T> : IList<T>, IReadOnlyReactiveList<T>, IReactiveCollection<T>

IReadOnlyDictionary<K,V> + IReadOnlyReactiveCollection<KeyValuePair<K,V>>
  └ IReadOnlyReactiveDictionary<K,V>
       └ IReactiveDictionary<K,V> : IDictionary<K,V>, IReadOnlyReactiveDictionary<K,V>,
                                    IReactiveCollection<KeyValuePair<K,V>>
```

Две поправки к документации. Первая: `IReadOnlyReactiveCollection.md` (строка 98) печатает `public interface IReadOnlyReactiveCollection<out T>` — в исходнике `out` нет, ковариантности нет. Вторая: `IArray<T>` в `Docs/Elements/Collections` не документирован нигде, хотя `IReactiveArray<T>` наследуется именно от него, так что заявленная в доках база `IReactiveArray<T>` неполна.

Модуль опирается на default interface implementations C# 8: `int IReadOnlyCollection<T>.Count => this.Length;` в `IReadOnlyReactiveArray<T>` и `int IReadOnlyReactiveArray<T>.Length => this.Count;` в `IReadOnlyReactiveList<T>`. На языковом уровне ниже C# 8 эти файлы не скомпилируются — это проверяется до начала порта.

### Запись сравнивается перед тем, как стать событием

Каждый сеттер в модуле — no-op, если новое значение равно старому. Сравнение делается через закешированный per-type компарер, а не через `Equals`/`==`. Следствие для миграции: код, который каждый кадр переписывает состояние тем же значением, не создаёт шторма событий — он платит только за сравнение.

### Мутация и рассылка синхронны, но форма у них разная

Обработчики вызываются внутри того же стека, что и мутация. Есть две разные схемы:

* **Inline** — `ReactiveList`, `ReactiveLinkedList`, `ReactiveDictionary`: событие поднимается по ходу изменения. `ReactiveDictionary.Clear()` вызывает `OnItemRemoved` внутри обхода слотов — до того как `_count` обнулён и до `Array.Fill(_buckets, UNDEFINED_INDEX)`. Обработчик, который в этот момент полез обратно в словарь, увидит внутренне несогласованную таблицу: `Count` ещё не ноль, а бакеты указывают на слоты с уже сброшенным `exists`.
* **Staged** — все массовые операции `ReactiveHashSet` (`UnionWith`, `ExceptWith`, `IntersectWith`, `SymmetricExceptWith`, `ReplaceWith`) и `ReactiveHashSet.Clear()`: сначала структура приводится в конечное состояние молчаливыми `AddInternal`/`RemoveInternal`, элементы складываются в арендованный у `ArrayPool<T>.Shared` буфер, и только потом события проигрываются. Подписчик физически не может увидеть множество «посередине».

Копировать в своём коде нужно вторую схему. Первая — то, что придётся обходить.

### Порядок событий неодинаков

Это подтверждено по исходникам и это главный источник тонких багов при порте:

| Операция | Порядок |
|---|---|
| `ReactiveList.Add/Insert/Remove/RemoveAt` | `OnItemAdded`/`OnItemRemoved` (индексный) → неиндексный → `OnStateChanged` |
| `ReactiveList.Clear` | **`OnStateChanged` первым**, затем `OnItemRemoved` на каждый элемент |
| `ReactiveList.AddRange` | `OnItemAdded` на каждый элемент, **один** терминальный `OnStateChanged` |
| `ReactiveArray` setter / `Clear` / `Fill` / `Resize` / `Populate` | `OnItemChanged` по изменённым индексам → один `OnStateChanged` |
| `ReactiveDictionary` setter / `Add` / `TryAdd` | **`OnStateChanged` первым**, затем `OnItemChanged`/`OnItemAdded` |
| `ReactiveDictionary.Remove` | `OnItemRemoved` → `OnStateChanged` |
| `ReactiveDictionary.Clear` | `OnItemRemoved` по ходу обхода → `OnStateChanged` в конце |
| `ReactiveHashSet.Add/Remove` | событие элемента → `OnStateChanged` |
| `ReactiveHashSet` массовые | все события элементов → один `OnStateChanged` |
| `ReactiveLinkedList.Clear` | `OnItemRemoved` по ходу → `OnStateChanged` в конце |

Вывод простой: не писать обработчиков, которые зависят от порядка. `OnStateChanged` — не «финальный коммит», в двух местах он идёт первым.

### Четыре политики null

| Коллекция | `null` элемент/значение |
|---|---|
| `ReactiveArray<T>` | разрешён (но `Contains(null)` всегда `false`) |
| `ReactiveList<T>` | `ArgumentNullException` в `Add`, `AddRange`, `Insert`, сеттере индексатора |
| `ReactiveLinkedList<T>` | `Add(null)` / `Insert(_, null)` — **молчаливый no-op без событий** |
| `ReactiveHashSet<T>` | `Add(null)` возвращает `false` |
| `ReactiveDictionary<K,V>` | `Add(K,V)` бросает `ArgumentNullException` на null-значение, `TryAdd` возвращает `false`; **null-ключ даёт `NullReferenceException`**, потому что ключ не проверяется на `null` нигде, а `key.GetHashCode()` вызывается сразу после проверки значения |

Старый код, который хранил `null` как «пустой слот» или «ещё не разрешено», переписывается: либо `ReactiveArray<T>` (там `default` легален), либо отсутствие ключа как признак отсутствия значения.

### Конкретный тип против интерфейса — это не оптимизация

`foreach` в C# связывается по образцу, а не через интерфейс: при статическом типе `ReactiveList<string>` компилятор берёт публичный `public Enumerator GetEnumerator()` и держит перечислитель на стеке. При статическом типе `IReactiveList<string>` он берёт `IEnumerable<T>.GetEnumerator()` и получает `IEnumerator<T>` — одна куча-аллокация плюс интерфейсный вызов на каждый `MoveNext()`/`Current`.

Для `ReactiveArray<T>` это уже вопрос корректности, а не скорости: в `ReactiveArray.cs` написано

```csharp
IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator();
```

— без `this`. Поле `_array` внутри перечислителя остаётся `null`, и первый же `MoveNext()` бросает `NullReferenceException`. Необобщённый путь (`IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);`) и конкретный `GetEnumerator()` корректны. То есть любой `foreach` и любой LINQ по полю, объявленному как `IReactiveArray<T>`, `IReadOnlyReactiveArray<T>` или `IEnumerable<T>`, падает. Остальные четыре коллекции и оба view-типа словаря передают `this` правильно.

### Dispose() — не «отписаться»

```csharp
// ReactiveArray<T>
public void Dispose()
{
    this.OnItemChanged = null;
    this.OnStateChanged = null;
}

// ReactiveList<T>, ReactiveLinkedList<T>, ReactiveHashSet<T>, ReactiveDictionary<K,V>
public void Dispose()
{
    this.Clear();          // <- сначала полная очистка С СОБЫТИЯМИ
    /* затем обнуление полей событий */
}
```

Только `ReactiveArray.Dispose()` просто отцепляет подписчиков. Остальные четыре сначала вызывают `Clear()`, то есть рассылают ещё подключённым подписчикам полный шторм `OnItemRemoved` плюс `OnStateChanged`, и лишь затем обнуляют события. Если вы вызываете `Dispose()` при разрушении сущности, UI успеет получить N удалений по уже умирающему объекту.

И второе: `Dispose()` **не** решает проблему удержания ссылок. `ReactiveList.Clear()` только пишет `count = 0` и не обнуляет слоты `items`, а `Dispose()` — это буквально `Clear()` плюс обнуление событий, так что устаревшие ссылки в `this.items` остаются. То же у `ReactiveDictionary.Remove`/`Clear` и `ReactiveHashSet.Remove`/`RemoveInternal`/`Clear`: они сбрасывают `slot.exists`, но не обнуляют `slot.key`/`slot.value`. Отпускает ссылки только `Populate`/перезапись или выбрасывание самой коллекции. Исключение — `ReactiveLinkedList.Clear()`, который пишет `node.item = default`.

### Гарантий на мутацию во время перечисления нет

Ни у одной коллекции нет version counter. Документация говорит «avoid modifying the dictionary while enumerating», но при нарушении вы получаете не чистый `InvalidOperationException`, а молча пропущенные или повторённые элементы. Поскольку обработчики вызываются синхронно внутри мутаций, подписчик, который меняет ту же коллекцию, по которой идёт итерация, тихо портит итерацию.

---

## API

### Базовые интерфейсы

```csharp
public interface IReadOnlyReactiveCollection<T> : IReadOnlyCollection<T>
{
    event Action OnStateChanged;
    event Action<T> OnItemAdded;
    event Action<T> OnItemRemoved;

    bool Contains(T item);
    void CopyTo(T[] array, int arrayIndex);

    public readonly struct StateChangedSubscription : IDisposable
    {
        public StateChangedSubscription(IReadOnlyReactiveCollection<T> collection, Action handler);
        public void Dispose();
    }
    public readonly struct ItemAddedSubscription : IDisposable
    {
        public ItemAddedSubscription(IReadOnlyReactiveCollection<T> collection, Action<T> handler);
        public void Dispose();
    }
    public readonly struct ItemRemovedSubscription : IDisposable
    {
        public ItemRemovedSubscription(IReadOnlyReactiveCollection<T> collection, Action<T> handler);
        public void Dispose();
    }
}
```

Тип, которым коллекция публикуется наружу, когда потребителю не нужны индексы и не нужна мутация. Три вложенных `readonly struct` подписки — штатный механизм отписки: подписываются в конструкторе (с проверкой аргументов на `null`), отписываются в `Dispose()`. В `.md`-файлах их нет вообще.

```csharp
public interface IReactiveCollection<T> : IReadOnlyReactiveCollection<T>, ICollection<T>
{
}
```

Маркер без своих членов. В файле стоит `// ReSharper disable PossibleInterfaceMemberAmbiguity`: `Count`, `Contains`, `CopyTo` неоднозначны между `IReadOnlyCollection<T>` и `ICollection<T>`, поэтому через этот интерфейс часть вызовов требует приведения. Ещё один довод объявлять поля конкретным классом.

### Массив

```csharp
public interface IArray<T> : IReadOnlyList<T>
{
    int Length { get; }
    new T this[int index] { get; set; }

    void Clear();
    void Populate(IEnumerable<T> newItems);
    void Fill(T value);
    void Resize(int newSize);
}
```

Нереактивная изменяемая абстракция массива фиксированного размера. В документации отсутствует; знать о ней нужно потому, что `IReactiveArray<T>` наследуется от неё, и именно отсюда приходят `Fill`/`Resize`/`Populate`.

```csharp
public interface IReadOnlyReactiveArray<T> : IReadOnlyList<T>
{
    event Action<int, T> OnItemChanged;
    event Action OnStateChanged;

    int Length { get; }
    new T this[int index] { get; }

    int IReadOnlyCollection<T>.Count => this.Length;   // default interface implementation

    bool Contains(T item);
    void CopyTo(T[] array, int arrayIndex);
    void CopyTo(int sourceIndex, T[] destination, int destinationIndex, int length);

    public readonly struct StateChangedSubscription : IDisposable { /* ... */ }
    public readonly struct ItemChangedSubscription : IDisposable
    {
        public ItemChangedSubscription(IReadOnlyReactiveArray<T> array, Action<int, T> handler);
        public void Dispose();
    }
}
```

Читающая сторона с уведомлением по индексу. Диапазонная копия называется `CopyTo(int, T[], int, int)` — метода `Copy(...)`, который печатают примеры в пяти `.md`-файлах, не существует.

```csharp
public interface IReactiveArray<T> : IArray<T>, IReadOnlyReactiveArray<T>
{
    new int Length { get; }
    new T this[int index] { get; set; }
}
```

Пишущая сторона. Не используйте как тип поля, по которому делаете `foreach` (см. дефект перечислителя).

```csharp
[Serializable]
public class ReactiveArray<T> : IReactiveArray<T>, IDisposable
{
    public event Action<int, T> OnItemChanged;
    public event Action OnStateChanged;
    public int Length => this.items.Length;

    public ReactiveArray(int capacity);            // ArgumentOutOfRangeException при capacity < 0
    public ReactiveArray(params T[] elements);     // this.items = elements;  — БЕЗ КОПИИ
    public ReactiveArray(IEnumerable<T> elements); // this.items = elements.ToArray();

    public T this[int index] { get; set; }

    public void Clear();
    public bool Contains(T item);
    public void CopyTo(T[] array, int arrayIndex);
    public void CopyTo(int sourceIndex, T[] destination, int destinationIndex, int length);
    public void Populate(IEnumerable<T> newItems);
    public void Fill(T value);
    public void Resize(int newSize);

    public Enumerator GetEnumerator() => new(this);
    public void Dispose();

    public struct Enumerator : IEnumerator<T>
    {
        public T Current { get; }
        public bool MoveNext();
        public void Reset();
        public void Dispose();
    }
}
```

Базовый выбор для ограниченного набора слотов: хотбар, слоты способностей, сокеты экипировки. Самая быстрая реактивная коллекция на чтение по индексу (`For` 0.70 μs против 0.70 μs у обычного массива). Поведение, которое надо знать: `Clear()` ставит `default` в каждый элемент и поднимает `OnItemChanged` только для тех, что не были `default`, затем один `OnStateChanged` — но при `Length == 0` возвращается сразу и не поднимает вообще ничего. `Populate` бросает `ArgumentException("Item count does not match array length.")`, если источник длиннее массива; если короче — хвост обнуляется с `OnItemChanged`. `Resize` поднимает `OnItemChanged` только для новых хвостовых индексов, не для усечённых. `[Serializable]` с `[SerializeField] private T[] items` — единственная коллекция, которая **не** реализует `ISerializationCallbackReceiver` и в инспекторе показывает параметр `items`, а не `serializedItems`.

### Список

```csharp
public interface IReadOnlyReactiveList<T> : IReadOnlyReactiveArray<T>, IReadOnlyReactiveCollection<T>
{
    new event Action OnStateChanged;
    new event Action<int, T> OnItemAdded;
    new event Action<int, T> OnItemRemoved;

    int IReadOnlyReactiveArray<T>.Length => this.Count;   // default interface implementation

    public new readonly struct StateChangedSubscription : IDisposable { /* ... */ }
    public new readonly struct ItemAddedSubscription : IDisposable
    {
        public ItemAddedSubscription(IReadOnlyReactiveList<T> list, Action<int, T> handler);
        public void Dispose();
    }
    public new readonly struct ItemRemovedSubscription : IDisposable
    {
        public ItemRemovedSubscription(IReadOnlyReactiveList<T> list, Action<int, T> handler);
        public void Dispose();
    }
}
```

Тип для публикации списка во вьюшку: уведомления несут индекс строки. Обратите внимание: `IReadOnlyReactiveList<T>.ItemAddedSubscription` и `IReadOnlyReactiveCollection<T>.ItemAddedSubscription` — **разные типы с одинаковым именем**.

```csharp
public interface IReactiveList<T> : IList<T>, IReadOnlyReactiveList<T>, IReactiveCollection<T>
{
    new bool Contains(T item);
}
```

`new bool Contains` существует только чтобы снять неоднозначность между `IList<T>` и `IReadOnlyReactiveArray<T>`.

```csharp
[Serializable]
public partial class ReactiveList<T> : IReactiveList<T>, IDisposable
{
    public event Action OnStateChanged;
    public event Action<int, T> OnItemAdded;
    public event Action<int, T> OnItemRemoved;
    public event Action<int, T> OnItemChanged;

    public bool IsReadOnly => false;
    public int Count => this.count;
    public int Capacity => this.items.Length;

    public ReactiveList(int capacity = 0);
    public ReactiveList(params T[] items);        // items ?? Array.Empty<T>()  — БЕЗ КОПИИ
    public ReactiveList(IEnumerable<T> items);    // items == null ? Array.Empty<T>() : items.ToArray()

    public T this[int index] { get; set; }

    public void Add(T item);
    public void AddRange(IEnumerable<T> items);
    public void Insert(int index, T item);
    public bool Contains(T item);
    public bool Remove(T item);
    public void RemoveAt(int index);
    public void Clear();
    public int IndexOf(T item);
    public void CopyTo(T[] array, int arrayIndex = 0);
    public void CopyTo(int sourceIndex, T[] destination, int destinationIndex, int length);
    public void Populate(IEnumerable<T> newItems);
    public Enumerator GetEnumerator() => new(this);
    public void Dispose();

    public struct Enumerator : IEnumerator<T> { /* Current, MoveNext, Reset, Dispose */ }
}

#if UNITY_5_3_OR_NEWER
public partial class ReactiveList<T> : ISerializationCallbackReceiver
{
    [SerializeField] internal T[] serializedItems;
}
#endif
```

Выбор по умолчанию для упорядоченной, адресуемой по индексу игровой коллекции. `Capacity` публичен — полезно для проверки, что `AddRange` действительно предразместил массив. `Remove`/`RemoveAt`/`Insert` сдвигают через `Array.Copy`. `RemoveAt` бросает `IndexOutOfRangeException($"Index {index} out of range!")`.

```csharp
public partial class ReactiveLinkedList<T> : IReactiveList<T>, IDisposable
{
    private struct Node { public T item; public int next; }   // ОДНОСВЯЗНЫЙ

    public event Action OnStateChanged;
    public event Action<int, T> OnItemChanged;
    public event Action<int, T> OnItemAdded;
    public event Action<int, T> OnItemRemoved;

    public int Count => _count;
    public bool IsReadOnly => false;

    public ReactiveLinkedList(int capacity = INITIAL_CAPACITY);   // INITIAL_CAPACITY = 4
    public ReactiveLinkedList(params T[] items) : this(items.Length);
    public ReactiveLinkedList(IEnumerable<T> items) : this(items.Count());

    public T this[int index] { get; set; }      // O(N) обход от головы вперёд по next
    public void Add(T item);                    // if (item == null) return;  — молчаливый no-op
    public void AddRange(IEnumerable<T> items); // foreach { Add(item); }
    public void Insert(int index, T item);
    public bool Remove(T item);
    public void RemoveAt(int index);
    public int IndexOf(T item);
    public bool Contains(T item) => IndexOf(item) >= 0;
    public void CopyTo(T[] array, int arrayIndex);
    public void CopyTo(int sourceIndex, T[] destination, int destinationIndex, int length);
    public void Clear();
    public Enumerator GetEnumerator() => new(this);
    public void Dispose();

    public struct Enumerator : IEnumerator<T> { /* ... */ }
}
```

Берите его там, где коллекция живёт в режиме постоянного churn — снаряды, активные эффекты, очередь спавна: `Remove` 43.50 μs против 254.25 у `ReactiveList` и 307.40 у `List<T>`, `Insert At` 60.80 против 245.85/242.85. Индексный доступ держите от него подальше: `Get` 1277.70 μs, `Set` 1304.00, `For` 1273.55, `RemoveAt` 2546.10.

Три вещи, которых нет в документации. Во-первых, список **односвязный** — `Node` содержит только `next`; `BestPractices/IteratingReactiveCollections.md` (строки 117 и 268) называет его «a **doubly-linked list**», это неверно, и из этого прямо следует `RemoveAt`: он делает `GetNodeIndex(index)` и `GetNodeIndex(index - 1)`, то есть два полных обхода. Во-вторых, это единственная коллекция модуля, которая использует `EqualityComparer<T>.Default` из BCL (строка 21), а не `EqualityComparer.GetDefault<T>()` — значит покомпонентные компареры для Unity-структур здесь не применяются и семантика равенства отличается от всех остальных коллекций. В-третьих, `Clear()` передаёт в `OnItemRemoved(index, item)` **индекс слота в `_nodes`**, а не логический индекс списка:

```csharp
ref Node node = ref _nodes[current];
...
OnItemRemoved?.Invoke(current, removed);   // current — это индекс слота, не позиция в списке
```

После любого churn этот индекс для подписчика бессмысленен.

### Множество

```csharp
public interface IReactiveSet<T> : ISet<T>, IReactiveCollection<T>
{
}
```

Маркер. `OnItemChanged` здесь нет и не было.

```csharp
public partial class ReactiveHashSet<T> : IReactiveSet<T>, IDisposable
{
    private struct Slot { public T value; public int next; public bool exists; }

    public event Action OnStateChanged;
    public event Action<T> OnItemAdded;
    public event Action<T> OnItemRemoved;

    public int Count => _count;
    public bool IsReadOnly => false;

    public ReactiveHashSet(int capacity = 0);
    public ReactiveHashSet(params T[] elements) : this(elements.Length) => this.UnionWith(elements);
    public ReactiveHashSet(IReadOnlyCollection<T> elements) : this(elements.Count) => this.UnionWith(elements);
    public ReactiveHashSet(IEnumerable<T> elements) : this(elements.Count()) => this.UnionWith(elements);

    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool Contains(T item);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool Add(T item);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void UnionWith(IEnumerable<T> other);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Clear();
    public bool Remove(T item);
    public bool IsEmpty() => _count == 0;
    public bool IsNotEmpty() => _count > 0;
    public void CopyTo(T[] array, int arrayIndex = 0);
    public void ExceptWith(IEnumerable<T> other);
    public void IntersectWith(IEnumerable<T> other);
    public void SymmetricExceptWith(IEnumerable<T> other);
    public void ReplaceWith(IEnumerable<T> other);
    public bool IsSubsetOf(IEnumerable<T> other);
    public bool IsProperSubsetOf(IEnumerable<T> other);
    public bool IsSupersetOf(IEnumerable<T> other);
    public bool IsProperSupersetOf(IEnumerable<T> other);
    public bool Overlaps(IEnumerable<T> other);
    public bool SetEquals(IEnumerable<T> other);
    public Enumerator GetEnumerator() => new(this);
    public void Dispose();

    public struct Enumerator : IEnumerator<T>
    {
        public T Current { get; }
        public bool MoveNext();
        void IEnumerator.Reset();     // явная реализация: вызов требует боксинга
        public void Dispose();
    }
}
```

`ReplaceWith(IEnumerable<T>)`, `IsEmpty()` и `IsNotEmpty()` — публичный API, которого нет в документации. `ReplaceWith` — это идиоматичное «сделай множество равным этому» с одной рассылкой разницы вместо `Clear()` + `UnionWith()`.

`Contains` здесь в 5 раз быстрее BCL (`10.00` против `50.10` μs), `Add` немного быстрее (`64.30` против `69.30`). Но массовые операции содержат два подтверждённых дефекта размеров буферов — в `IntersectWith` и `SymmetricExceptWith` (раздел «Грабли»), — а `IntersectWith` ещё и `stackalloc` без ограничения.

### Словарь

```csharp
public interface IReadOnlyReactiveDictionary<K, V> :
    IReadOnlyDictionary<K, V>,
    IReadOnlyReactiveCollection<KeyValuePair<K, V>>
{
    new event Action<K, V> OnItemAdded;
    new event Action<K, V> OnItemRemoved;
    event Action<K, V> OnItemChanged;

    public new readonly struct StateChangedSubscription : IDisposable { /* ... */ }
    public new readonly struct ItemAddedSubscription : IDisposable { /* ... */ }
    public new readonly struct ItemRemovedSubscription : IDisposable { /* ... */ }
    public readonly struct ItemChangedSubscription : IDisposable
    {
        public ItemChangedSubscription(IReadOnlyReactiveDictionary<K, V> dictionary, Action<K, V> handler);
        public void Dispose();
    }
}
```

Тип, которым словарь публикуется наружу. События двухаргументные (`Action<K,V>`), а не `Action<KeyValuePair<K,V>>` — последние унаследованы от `IReadOnlyReactiveCollection<KeyValuePair<K,V>>` и затенены через `new`.

```csharp
public interface IReactiveDictionary<K, V> :
    IDictionary<K, V>,
    IReadOnlyReactiveDictionary<K, V>,
    IReactiveCollection<KeyValuePair<K, V>>
{
    new V this[K key] { get; set; }
    new int Count { get; }
    new bool ContainsKey(K key);
    new bool TryGetValue(K key, out V value);
}
```

Четыре `new`-члена существуют только для снятия неоднозначности `IDictionary<K,V>` / `IReadOnlyDictionary<K,V>`.

```csharp
public partial class ReactiveDictionary<K, V> : IReactiveDictionary<K, V>, IDisposable
{
    private struct Slot { public K key; public V value; public bool exists; public int next; }

    public event Action OnStateChanged;
    public event Action<K, V> OnItemChanged;
    public event Action<K, V> OnItemAdded;
    public event Action<K, V> OnItemRemoved;

    public ReadOnlyKeyCollection Keys => new(this);
    public ReadOnlyValueCollection Values => new(this);
    public int Count => _count;
    public bool IsReadOnly => false;

    public ReactiveDictionary(int capacity = 0);
    public ReactiveDictionary(IEnumerable<KeyValuePair<K, V>> source) : this(source.Count());
    public ReactiveDictionary(IEnumerable<(K, V)> source) : this(source.Count());
    public ReactiveDictionary(params KeyValuePair<K, V>[] source) : this(source.Length);
    public ReactiveDictionary(params (K, V)[] source) : this(source.Length);

    public V this[K key] { get; set; }          // геттер: KeyNotFoundException; сеттер: upsert

    public void Add(KeyValuePair<K, V> item);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public void Add(K key, V value);
    public bool TryAdd(KeyValuePair<K, V> item);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool TryAdd(K key, V value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)] public bool Remove(K key);
    public bool Remove(K key, out V value);
    public bool Remove(KeyValuePair<K, V> item);
    public bool ContainsKey(K key);
    public bool Contains(KeyValuePair<K, V> item);
    public bool TryGetValue(K key, out V value);
    public void Clear();
    public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex = 0);
    public Enumerator GetEnumerator() => new(this);
    public void Dispose();
}
```

Цель миграции для «типизированного мешка значений» старого контекста: ключевое хранилище, читатели которого подписываются на изменение по ключу, а не опрашивают. По замерам это чистый апгрейд над `Dictionary<K,V>` для чтения: `Set` 10.10 против 35.50, `ContainsKey` 5.70 против 7.10, `Get` 5.80 против 7.40, `Clear` 2.40 против 7.10; проигрыш на `Add` (64.40 против 34.10) и едва заметный на `Enumerator` (58.60 против 56.60).

Важная деталь, которой нет в XML-доке самого класса (он утверждает «A reactive wrapper around `Dictionary<K,V>`» — это неправда, там своя хеш-таблица): бакетирование **везде** идёт по `key.GetHashCode()`, включая `IncreaseCapacity`, поэтому рассогласования хешей, как у `ReactiveHashSet`, здесь нет. Зато `s_keyComparer.GetHashCode` не используется никогда, так что для ключа с кастомной семантикой равенства `GetHashCode` обязан быть согласован с `Equals` компарера.

```csharp
public readonly struct ReadOnlyKeyCollection : ICollection<K>
{
    public int Count => _dictionary._count;
    public bool IsReadOnly => true;

    public Enumerator GetEnumerator() => new(_dictionary);
    public bool Contains(K item) => item != null && _dictionary.ContainsKey(item);
    public void CopyTo(K[] array, int arrayIndex);

    void ICollection<K>.Add(K item)    => throw new NotSupportedException("KeyCollection is read-only.");
    void ICollection<K>.Clear()        => throw new NotSupportedException("KeyCollection is read-only.");
    bool ICollection<K>.Remove(K item) => throw new NotSupportedException("KeyCollection is read-only.");

    public struct Enumerator : IEnumerator<K> { /* Current, MoveNext, Reset, Dispose */ }
}

public readonly struct ReadOnlyValueCollection : ICollection<V>
{
    public int Count => _dictionary._count;
    public bool IsReadOnly => true;

    public Enumerator GetEnumerator() => new(_dictionary);
    public bool Contains(V item);        // O(N) обход слотов с s_valueComparer
    public void CopyTo(V[] array, int arrayIndex);

    void ICollection<V>.Add(V item)    => throw new NotSupportedException("ValueCollection is read-only.");
    void ICollection<V>.Clear()        => throw new NotSupportedException("ValueCollection is read-only.");
    bool ICollection<V>.Remove(V item) => throw new NotSupportedException("ValueCollection is read-only.");

    public struct Enumerator : IEnumerator<V> { /* ... */ }
}
```

Живые (не снапшот) `readonly struct`-представления над массивом слотов: `Count` читает `_dictionary._count`. Используйте `dict.Keys` / `dict.Values` при статическом типе `ReactiveDictionary<K,V>` — тогда обход не аллоцирует ничего. Через `IDictionary<K,V>.Keys` или `IReadOnlyDictionary<K,V>.Keys` структура боксируется на каждом чтении свойства, а сверху боксируется ещё и перечислитель.

```csharp
public struct Enumerator : IEnumerator<KeyValuePair<K, V>>
{
    public KeyValuePair<K, V> Current => _current;
    public bool MoveNext();
    void IEnumerator.Reset();     // явная реализация — вызов требует боксинга
    public void Dispose();
}
```

Пропускает мёртвые слоты по `slot.exists`, читает слот через `ref readonly`.

### Extensions

```csharp
public partial class Extensions
{
    public static void CopyFrom<T>(this ICollection<T> destination, IEnumerable<T> source);
    public static void CopyTo<T>(this IEnumerable<T> source, ICollection<T> destination);
    public static void AddRange<T>(this ICollection<T> it, params T[] items);
    public static void AddRange<T>(this ICollection<T> it, IEnumerable<T> items);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveCollection<T>.StateChangedSubscription SubscribeState<T>(
        this IReadOnlyReactiveCollection<T> it, Action action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveCollection<T>.ItemAddedSubscription SubscribeAdded<T>(
        this IReadOnlyReactiveCollection<T> it, Action<T> action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveCollection<T>.ItemRemovedSubscription SubscribeRemoved<T>(
        this IReadOnlyReactiveCollection<T> it, Action<T> action) => new(it, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveArray<T>.StateChangedSubscription SubscribeState<T>(
        this IReadOnlyReactiveArray<T> it, Action action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveArray<T>.ItemChangedSubscription SubscribeChanged<T>(
        this IReadOnlyReactiveArray<T> it, Action<int, T> action) => new(it, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveList<T>.StateChangedSubscription SubscribeState<T>(
        this IReadOnlyReactiveList<T> it, Action action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveList<T>.ItemAddedSubscription SubscribeAdded<T>(
        this IReadOnlyReactiveList<T> it, Action<int, T> action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveList<T>.ItemRemovedSubscription SubscribeRemoved<T>(
        this IReadOnlyReactiveList<T> it, Action<int, T> action) => new(it, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveDictionary<K, V>.StateChangedSubscription SubscribeState<K, V>(
        this IReadOnlyReactiveDictionary<K, V> it, Action action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveDictionary<K, V>.ItemAddedSubscription SubscribeAdded<K, V>(
        this IReadOnlyReactiveDictionary<K, V> it, Action<K, V> action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveDictionary<K, V>.ItemRemovedSubscription SubscribeRemoved<K, V>(
        this IReadOnlyReactiveDictionary<K, V> it, Action<K, V> action) => new(it, action);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyReactiveDictionary<K, V>.ItemChangedSubscription SubscribeChanged<K, V>(
        this IReadOnlyReactiveDictionary<K, V> it, Action<K, V> action) => new(it, action);

    public static ReactiveDictionary<K, V> ToReactiveDictionary<K, V>(
        this IEnumerable<V> collection, Func<V, K> keySelector);
}
```

Весь этот класс в документации коллекций отсутствует — команда его не найдёт, если не сказать. Двенадцать `SubscribeXxx` — однострочники с `AggressiveInlining`, возвращающие `readonly struct`, так что подписка не стоит ни одной куча-аллокации, пока вы не присвоили результат в `IDisposable`.

Два предупреждения. `Extensions` объявлен `public partial class`, а не `static class`, — его можно инстанцировать и наследовать; это то имя типа, на которое вы ссылаетесь. И `CopyFrom`/`CopyTo` — **деструктивные**: оба начинают с `destination.Clear()`, а затем добавляют по одному элементу через `ICollection<T>.Add`. На реактивном приёмнике это полный шторм удалений плюс по одному `OnStateChanged` на каждый добавленный элемент. Используйте `Populate` / `ReplaceWith`.

### Внутренние типы (знать, но вызвать нельзя)

```csharp
internal static class EqualityComparer
{
    public static readonly IEqualityComparer<Vector2> Vector2;
    public static readonly IEqualityComparer<Vector3> Vector3;
    public static readonly IEqualityComparer<Vector4> Vector4;
    public static readonly IEqualityComparer<Color> Color;
    public static readonly IEqualityComparer<Color32> Color32;
    public static readonly IEqualityComparer<Rect> Rect;
    public static readonly IEqualityComparer<Bounds> Bounds;
    public static readonly IEqualityComparer<Quaternion> Quaternion;
    public static readonly IEqualityComparer<Vector2Int> Vector2Int;
    public static readonly IEqualityComparer<Vector3Int> Vector3Int;
    public static readonly IEqualityComparer<RangeInt> RangeInt;
    public static readonly IEqualityComparer<RectInt> RectInt;
    public static readonly IEqualityComparer<BoundsInt> BoundsInt;

    static class Cache<T> { public static readonly IEqualityComparer<T> Comparer; }

    public static IEqualityComparer<T> GetDefault<T>() => Cache<T>.Comparer;
}
```

Помечен в исходнике комментарием `//Steal from UniRx! ;)`. Класс `internal`, поэтому игровой код не может вызвать `GetDefault<T>()` напрямую — он достаётся только транзитивно через коллекции. Диспетчеризация — чтение статического generic-поля (`Cache<T>.Comparer`), разрешаемое один раз на закрытый generic-тип через сравнения `RuntimeTypeHandle`; рефлексии на каждый вызов нет.

```csharp
internal static class InternalUtils
{
    internal static readonly int[] PrimeTable =
    {
        2, 3, 7, 17, 29, 53, 97, 193, 389, 769, 1543, 3079,
        6151, 12289, 24593, 49157, 98317, 196613, 393241, 786433,
        1572869, 3145739, 6291469, 12582917, 25165843, 50331653,
        100663319, 201326611, 402653189, 805306457, 1610612741
    };

    internal static int CeilToPrime(int value, out int index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Contains<T>(IEnumerable<T> other, T item, IEqualityComparer<T> comparer);
}
```

`CeilToPrime` — это `Array.BinarySearch` по фиксированной таблице, возвращающий и простое, и его индекс, поэтому рост ёмкости — `PrimeTable[++_primeIndex]`, индексация массива, а не поиск простого числа. `CeilToPrime(0)` даёт 2 — отсюда `hash % _capacity` никогда не делит на ноль. За последним простым (1610612741) рост бросает `InvalidOperationException` — жёсткий потолок для словаря и множества.

---

## Идиомы

### 1. Объявлять поле конкретным классом, а не интерфейсом

Пример взят из `Docs/BestPractices/IteratingReactiveCollections.md`.

```csharp
// ❌ Has Boxing (IReactiveList<E>)

// Assume we have an instance of interface type
IReactiveList<string> items ...
{
    "Axe",
    "Helmet",
    "Potion",
    "Sword"
};

// Reference type of "IEnumerator<string>"
foreach (string item in items)
{
    ...
}
```

```csharp
// ✅ No Boxing (ReactiveList<E>)

// Assume we have a concrete instance of collection implementation
ReactiveList<string> items = ...;

// Value type of "ReactiveList.Enumerator<string>"
foreach (string item in items)
{
    ...
}
```

Почему так: `foreach` связывается по образцу, поэтому при конкретном типе компилятор берёт `public Enumerator GetEnumerator()` и перечислитель остаётся на стеке; при интерфейсном типе — `IEnumerable<T>.GetEnumerator()`, одна аллокация в куче и интерфейсный вызов на каждый шаг, каждый кадр. То же правило распространяется на `dict.Keys`/`dict.Values`. А для `ReactiveArray<T>` это уже не оптимизация: интерфейсный путь бросает `NullReferenceException`.

### 2. `for` по `ReactiveArray` / `ReactiveList` с закешированным `Count`

```csharp
// ❌ Bad Practice

ReactiveList<string> items = ... // 1000+ elements

foreach (string item in items)
{
    ...
}
```

```csharp
// ✅ Good Practice

ReactiveList<string> items = ... // 1000+ elements

for (int i = 0, count = items.Count; i < count; i++)
{
    string item = items[i];
    ...
}
```

Почему так: обе коллекции лежат в массиве, поэтому чтение по индексу — одна загрузка с проверкой границ, тогда как `foreach` всё равно конструирует и копирует struct-перечислитель и платит вызов `MoveNext` на элемент. Замеры однозначны: `ReactiveArray` — `For` 0.70 μs против `Enumerator` 28.50 μs (сорокакратная разница); `ReactiveList` — `For` 1.70 против `Enumerator` 28.80. Вынос `Count` в заголовок цикла превращает вызов свойства на каждой итерации в одно чтение поля.

### 3. `foreach` по `ReactiveLinkedList` / `ReactiveDictionary` / `ReactiveHashSet` — и никогда `for`

```csharp
// ❌ Bad Practice

ReactiveLinkedList<string> items = ... // 1000+ elements

for (int i = 0, count = items.Count; i < count; i++)
{
    string item = items[i];
    ...
}
```

```csharp
// ✅ Good Practice

ReactiveLinkedList<string> items = ... // 1000+ elements

foreach (string item in items)
{
    ...
}
```

Почему так: у этих трёх нет O(1)-индекса. Индексатор `ReactiveLinkedList` вызывает `GetNodeIndex(index)`, который идёт от `_head`, поэтому индексный цикл — O(N²): 1273.55 μs против 28.50 μs у `foreach`. У словаря и множества целочисленного индексатора нет вовсе (у `ReactiveDictionary<K,V>` есть только `V this[K key]` — upsert по ключу), поэтому `for` там просто не выражается. Struct-перечислители вместо этого идут по `next` (связный список) или подметают массив слотов, проверяя `slot.exists`.

### 4. Один `AddRange` / `UnionWith` вместо цикла `Add`

```csharp
// ❌
foreach (var item in newItems)
    reactiveList.Add(item);   // N реаллокаций, N OnStateChanged, N перестроений UI
```

```csharp
// ✅
reactiveList.AddRange(new[] { "Kiwi", "Mango", "Pineapple" });
```

Почему так: `ReactiveList<T>.AddRange` проверяет `items is ICollection<T>` и один раз предразмещает массив через `Array.Resize(ref this.items, Math.Max(this.items.Length * 2, requiredCapacity))`, после чего поднимает ровно один `OnStateChanged` в конце (`if (this.count > initialCount)`). Цикл `Add` даёт log₂N удвоений и N раз `OnStateChanged`, то есть N перестроений у каждого подписчика состояния. `ReactiveHashSet.UnionWith` идёт дальше: добавляет через молчаливый `AddInternal`, складывает фактически добавленные элементы в аренду `ArrayPool<T>.Shared` и только потом проигрывает `OnItemAdded` и один `OnStateChanged`.

Две оговорки из исходников. Для `ReactiveLinkedList.AddRange` это **не** работает: там буквально `foreach (var item in items) Add(item);`, то есть полный цикл событий на элемент, несмотря на обратное утверждение в доке. И `ReactiveList.AddRange` бросает `ArgumentNullException` на первом же `null` в середине источника — уже добавленные элементы остаются в списке, события по ним уже разосланы, отката нет. Проверяйте источник на `null` до вызова.

### 5. `Populate` / `ReplaceWith` вместо `Clear` + повторного заполнения

```csharp
// ❌
list.Clear();
foreach (var item in newItems)
    list.Add(item);   // 2N событий, полный teardown и rebuild UI
```

```csharp
// ✅
list.Populate(newItems);          // ReactiveList<T> / ReactiveArray<T>
set.ReplaceWith(materializedArray); // ReactiveHashSet<T>
```

Это реализация `ReactiveList<T>.Populate` из фреймворка — форма, которую нужно повторять в своём коде:

```csharp
public void Populate(IEnumerable<T> newItems)
{
    if (newItems == null)
        throw new ArgumentNullException(nameof(newItems));

    using var enumerator = newItems.GetEnumerator();
    int index = 0;

    while (index < this.count && enumerator.MoveNext())
    {
        T newValue = enumerator.Current;
        ref T current = ref this.items[index];

        if (!s_comparer.Equals(current, newValue))
        {
            current = newValue;
            this.OnItemChanged?.Invoke(index, newValue);
        }

        index++;
    }

    while (enumerator.MoveNext())
        this.Add(enumerator.Current);

    while (index < this.count)
    {
        T removedItem = this.items[index];
        this.count--;
        for (int j = index; j < this.count; j++)
            this.items[j] = this.items[j + 1];

        this.OnItemRemoved?.Invoke(index, removedItem);
        this.onItemRemoved?.Invoke(removedItem);
    }

    this.OnStateChanged?.Invoke();
}
```

Почему так: пройти по пересечению, сравнивая через `s_comparer`, и поднять `OnItemChanged` **только там, где значение действительно отличается**; дописать излишек; срезать хвост; один терминальный `OnStateChanged`. `Clear()` + заполнение на списке из 100 элементов, изменившемся в одном слоте, даёт 200 событий и 200 операций UI вместо одной. Обратите внимание на `ref T current = ref this.items[index]` — одна проверка границ обслуживает и сравнение, и запись.

Одну деталь копировать не надо: срез хвоста сделан поэлементным внутренним циклом (O(N·count)), тогда как `Remove`/`RemoveAt`/`Insert` того же класса корректно используют `Array.Copy`.

### 6. Сравнение в сеттере, чтобы неизменившаяся запись не поднимала событие

```csharp
public T this[int index]
{
    get { return this.items[index]; }
    set
    {
        ref T item = ref this.items[index];
        if (s_comparer.Equals(item, value))
            return;

        item = value;
        this.OnItemChanged?.Invoke(index, value);
        this.OnStateChanged?.Invoke();
    }
}
```

Почему так: `array[i] = currentValue` стоит одно сравнение и больше ничего — код, который каждый кадр переутверждает состояние, не производит шторма. Механика, которую стоит перенести дословно: `ref T item = ref this.items[index]` — одна проверка границ на чтение и запись; `s_comparer` — закешированный per-type `EqualityComparer.GetDefault<T>()`, который для `Vector3`/`Quaternion`/`Color` является покомпонентным компарером, а не боксирующим `Equals`; `?.Invoke` полностью пропускает вызов, когда никто не подписан. Антипаттерн — поднимать `OnItemChanged(index, value)` безусловно или сравнивать через `Equals`/`==`.

### 7. Отписка через вложенные `readonly struct`-подписки, а не через `-=`

```csharp
// ❌
dict.OnItemChanged += (k, v) => Refresh(k, v);
// ...позже отцепить невозможно: у лямбды нет устойчивой идентичности.
// И `IDisposable sub = list.SubscribeAdded(h);` боксирует структуру.
```

```csharp
// ✅ подписка на время области видимости, без аллокаций
using IReadOnlyReactiveDictionary<string, int>.ItemChangedSubscription sub =
    dict.SubscribeChanged<string, int>((k, v) => Refresh(k, v));
```

```csharp
// ✅ подписка на время жизни компонента: поле конкретного struct-типа
private IReadOnlyReactiveList<ItemData>.ItemAddedSubscription _added;
private IReadOnlyReactiveList<ItemData>.ItemRemovedSubscription _removed;

private void OnEnable()
{
    _added = _items.SubscribeAdded((int i, ItemData v) => _view.InsertRow(i, v));
    _removed = _items.SubscribeRemoved((int i, ItemData v) => _view.RemoveRow(i));
}

private void OnDisable()
{
    _added.Dispose();
    _removed.Dispose();
}
```

Почему так: подписка подключается в конструкторе с проверкой аргументов и отключается в `Dispose()`, то есть время жизни обработчика привязано к области или к полю, а не к рукописному `-=`, который легко забыть или написать с другой лямбдой. Это `readonly struct`, возвращаемые из `AggressiveInlining`-фабрик, поэтому подписка не аллоцирует — при условии, что вы держите её в `var`/`using`-локали или в поле **конкретного** struct-типа. Присвоение в `IDisposable` боксирует.

Арность лямбды выбирает перегрузку: `(i, v) => ...` подходит только `Action<int,T>`-перегрузке (`IReadOnlyReactiveList<T>`), `v => ...` — только `Action<T>`-перегрузке (`IReadOnlyReactiveCollection<T>`). Это удобно, но на `ReactiveList<T>` и `ReactiveLinkedList<T>` **неиндексная** подписка не спасает от дефекта аксессора: `ItemAddedSubscription.Dispose()` вызывает `OnItemAdded -= handler` через тот же `IReadOnlyReactiveCollection<T>`, у которого `remove => this.onItemRemoved -= value`. На этих двух типах подписывайтесь индексно.

### 8. Правильные имена индексных событий списка

```csharp
// ❌ не компилируется — таких членов нет
list.OnItemInserted += (i, v) => ...;
list.OnItemDeleted  += (i, v) => ...;
```

```csharp
// ✅
var list = new ReactiveList<string>();

list.OnItemAdded   += (i, v) => Console.WriteLine($"Added {v} at {i}");
list.OnItemRemoved += (i, v) => Console.WriteLine($"Removed {v} from {i}");
list.OnItemChanged += (i, v) => Console.WriteLine($"Changed {i} to {v}");
list.OnStateChanged += () => Console.WriteLine("State changed");
```

Почему так: реальные члены — `OnItemAdded` и `OnItemRemoved`, оба `Action<int,T>`. Устаревшие `OnItemInserted`/`OnItemDeleted` фигурируют в `ReactiveList.md`, `ReactiveLinkedList.md` и `Manual.md`, причём секция «API Reference» того же `ReactiveList.md` документирует правильные имена — файл противоречит сам себе. При порте делайте поиск-замену по всем местам подписки: `OnItemInserted` → `OnItemAdded`, `OnItemDeleted` → `OnItemRemoved`.

### 9. Приведение к `IReadOnlyReactiveCollection<T>` для уведомлений без индекса

```csharp
// Assume we have a read-only reactive list
IReadOnlyReactiveList<int> reactiveList = GetReactiveList();

// Cast to IReadOnlyReactiveCollection to use non-indexed events
IReadOnlyReactiveCollection<int> reactiveCollection = reactiveList;

reactiveCollection.OnItemAdded += item =>
{
    Console.WriteLine($"Item {item} added (non-indexed event)");
};

reactiveCollection.OnItemRemoved += item =>
{
    Console.WriteLine($"Item {item} removed (non-indexed event)");
};
```

Почему так: явное приведение — единственный способ выбрать `Action<T>`-перегрузку, которую затеняют индексные события с `new`. Берите её там, где обработчику позиция не нужна: пересчитать сумму, зарегистрировать/снять поведение. Такой обработчик вдобавок остаётся применимым и к словарю, и к множеству.

Предупреждение из исходника: на `ReactiveList<T>` и `ReactiveLinkedList<T>` аксессор `remove` для неиндексного `OnItemAdded` реализован как `remove => this.onItemRemoved -= value;`. Отцепить добавленный так обработчик нельзя, и при попытке вы молча отцепите посторонний обработчик удаления. Пока это не исправлено, подписывайтесь неиндексно только теми обработчиками, которые живут столько же, сколько коллекция; в остальных случаях — индексные события.

### 10. Публиковать read-only, мутировать внутри

```csharp
public sealed class InventoryComponent
{
    private readonly ReactiveList<ItemData> _items = new(capacity: 16);

    // наружу — только чтение и уведомления
    public IReadOnlyReactiveList<ItemData> Items => _items;

    public void Give(ItemData item) => _items.Add(item);
    public void Reset(IReadOnlyList<ItemData> state) => _items.Populate(state);

    // горячая итерация — внутри, по конкретному типу
    private int TotalWeight()
    {
        int sum = 0;
        for (int i = 0, count = _items.Count; i < count; i++)
            sum += _items[i].Weight;
        return sum;
    }
}
```

Почему так: модуль поставляет read-only интерфейс для каждой коллекции (`IReadOnlyReactiveCollection`, `IReadOnlyReactiveArray`, `IReadOnlyReactiveList`, `IReadOnlyReactiveDictionary`) именно для того, чтобы компонент держал конкретный тип приватно и публиковал читающее лицо: подписчики получают уведомления и запросы, но не могут добавить, удалить или очистить. Документация `IReadOnlyReactiveArray` формулирует это как «Ideal for exposing reactive arrays without allowing external modifications». Важно не потерять вторую половину: сам компонент обходит коллекцию по конкретному полю, потому что обход по опубликованному интерфейсу — это боксирующий путь.

### 11. `TryAdd` / `TryGetValue` / `Remove(key, out value)` вместо потока на исключениях

```csharp
// ❌
if (dict.ContainsKey(key))      // проба 1
    value = dict[key];          // проба 2
...
if (dict.ContainsKey(key)) { var v = dict[key]; dict.Remove(key); }   // три пробы
```

```csharp
// ✅
var dict = new ReactiveDictionary<string, int>();

bool added = dict.TryAdd("One", 1); // true
added = dict.TryAdd("One", 11);     // false, ключ уже есть

if (dict.TryGetValue("One", out int value))
    Use(value);

if (dict.Remove("One", out int removed))
    Release(removed);
```

Почему так: `Add(K,V)` бросает `ArgumentException($"Value with key {key} is already added!")` на дубликате, а геттер — `KeyNotFoundException` на промахе. `TryAdd` и `TryGetValue` — одна проба, без аллокаций и без исключений; `AggressiveInlining` при этом стоит только на `TryAdd(K,V)`, у `TryGetValue` атрибута нет. `Remove(K key, out V value)` отдаёт удалённое значение из той единственной пробы, которая слот уже нашла, вместо пары `TryGetValue` + `Remove`. `Remove(KeyValuePair<K,V>)` же — это `Contains(item) && Remove(item.Key)`, то есть две полные пробы; избегайте этой перегрузки в горячем коде.

### 12. Конструировать с ёмкостью или из материализованной коллекции

```csharp
var dictFromPairs = new ReactiveDictionary<string, int>(new List<KeyValuePair<string, int>>
{
    new("One", 1),
    new("Two", 2)
});

var dictFromTuples = new ReactiveDictionary<string, int>(new (string, int)[]
{
    ("Three", 3),
    ("Four", 4)
});
```

Почему так: коллекционные конструкторы делегируют конструктору с ёмкостью (`: this(source.Count())`, `: this(source.Length)`), поэтому массивы слотов и бакетов разменяются один раз сразу, и prime-лестница при заполнении не раскручивается — ни рехеша, ни `Array.Resize`. Перегрузки с кортежами избавляют от шума `KeyValuePair`, а `params`-перегрузки позволяют писать данные литералом. Передавайте массив или `List<T>`, но не LINQ-запрос: `source.Count()` лишний раз прогоняет ленивую последовательность до `foreach`, который наполняет словарь. То же касается `new ReactiveList<T>(capacity)`, `new ReactiveArray<T>(capacity)` и `new ReactiveHashSet<T>(capacity)`, когда итоговый размер известен.

### 13. Обход ключей, значений и пар словаря без аллокаций

```csharp
var dict = new ReactiveDictionary<string, int>
{
    { "X", 10 },
    { "Y", 20 }
};

// ключи — без аллокаций
foreach (var key in dict.Keys)
{
    Console.WriteLine(key); // X, Y
}

// значения — без аллокаций
foreach (var val in dict.Values)
{
    Console.WriteLine(val); // 10, 20
}

// пары — без аллокаций
foreach (var kv in dict)
{
    Console.WriteLine($"{kv.Key}: {kv.Value}");
}
```

Почему так: `dict.Keys` и `dict.Values` возвращают `ReadOnlyKeyCollection` / `ReadOnlyValueCollection` — `readonly struct`-представления, которые держат одну ссылку на словарь и имеют собственный вложенный struct `Enumerator`. При статическом типе `ReactiveDictionary<K,V>` все три цикла не аллоцируют ничего. Гарантия держится **только** на конкретном типе: явные члены `ICollection<K> IDictionary<K,V>.Keys => new ReadOnlyKeyCollection(this)` боксируют представление на каждом чтении свойства, а затем боксируют перечислитель сверху. Представления живые, а не снапшоты, — не мутируйте словарь, пока по одному из них идёт обход.

### 14. `ref readonly` доступ к слоту в своих struct-перечислителях

```csharp
// ❌
Slot slot = _dictionary._slots[_index++];   // копия всей структуры на каждый посещённый слот, включая мёртвые
```

```csharp
// ✅ — так сделан ReactiveDictionary<K,V>.Enumerator.MoveNext
public bool MoveNext()
{
    while (_index < _dictionary._lastIndex)
    {
        ref readonly Slot slot = ref _dictionary._slots[_index++];
        if (slot.exists)
        {
            _current = new KeyValuePair<K, V>(slot.key, slot.value);
            return true;
        }
    }

    _current = default;
    return false;
}
```

Почему так: `ref readonly Slot slot = ref ...` читает слот на месте. Обычное `Slot slot = _slots[i]` скопировало бы всю структуру (key + value + bool + int) на каждый кандидат, включая мёртвые, которые тут же пропускаются, — чистые потери, пропорциональные `_lastIndex`. Тот же приём используется в `ReactiveHashSet.Contains`, `FindIndex`, `CopyTo`, `IntersectWith`, `ReplaceWith`, в индексаторе-геттере `ReactiveDictionary`, `Add`, `TryAdd`, `ContainsKey`, `TryGetValue`, `CopyTo` и в `CopyTo`/`Contains` оба view-типов. Там, где нужна запись, код берёт обычный `ref Slot` (`IncreaseCapacity`, `RemoveInternal`, `Remove`, `Clear`). Обратите внимание, что граница обхода — `_lastIndex`, высшая точка заполнения, а не `Count`.

### 15. Пул под буфер событий: сначала согласованность, потом рассылка

```csharp
// ❌
foreach (T item in other)
    this.Add(item);   // события перемешаны с мутацией; обработчики видят полусобранное множество и могут в него войти
```

```csharp
// ✅ — так сделан ReactiveHashSet<T>.UnionWith
public void UnionWith(IEnumerable<T> other)
{
    if (other == null)
        throw new ArgumentNullException(nameof(other));

    T[] addedItems = s_arrayPool.Rent(other.Count());
    int addedCount = 0;

    foreach (T item in other)
        if (item != null && this.AddInternal(item))
            addedItems[addedCount++] = item;

    try
    {
        if (addedCount > 0)
        {
            for (int i = 0; i < addedCount; i++)
                this.OnItemAdded?.Invoke(addedItems[i]);

            this.OnStateChanged?.Invoke();
        }
    }
    finally
    {
        s_arrayPool.Return(addedItems);
    }
}
```

Почему так: два отдельных выигрыша. Мутация разделена на молчаливый `AddInternal` (без событий) и фазу проигрывания, поэтому подписчик не может увидеть множество посередине и не может войти в полуобновлённую коллекцию; заодно цикл мутации не делает работы по цепочкам бакетов, пока работает обработчик. И буфер берётся из `private static readonly ArrayPool<T> s_arrayPool = ArrayPool<T>.Shared` и возвращается в `finally`, так что объединение с 1000 элементов не аллоцирует управляемых массивов.

Копируя структуру, исправьте три её дефекта. Во-первых, `s_arrayPool.Rent(other.Count())` прогоняет `other` второй раз: передавайте только материализованные коллекции и считайте размер аренды один раз. Во-вторых, аренду берите **внутри** `try`: здесь `Rent` стоит до входа в `try`, поэтому исключение в цикле мутации теряет аренду (то же в `ExceptWith`, `IntersectWith`, `ReplaceWith`). В-третьих, размер аренды должен происходить из той коллекции, которая ограничивает счётчик, — это ровно то, на чём ломаются `IntersectWith` и `SymmetricExceptWith` (см. «Грабли»).

### 16. Не использовать `Extensions.CopyFrom` / `Extensions.CopyTo` на реактивном приёмнике

```csharp
// ❌ — реальное тело из Collections/Extensions.cs
public static void CopyFrom<T>(this ICollection<T> destination, IEnumerable<T> source)
{
    destination.Clear();
    foreach (T item in source)
        destination.Add(item);
}
```

```csharp
// ✅
list.Populate(source);                 // диффом, с одним OnStateChanged
set.ReplaceWith(materializedArray);    // диффом, с одним OnStateChanged
```

Почему так: `CopyFrom`/`CopyTo` начинают с `Clear()` — на реактивном приёмнике это полный шторм удалений, а затем по одному `OnStateChanged` на каждый добавленный элемент, потому что `Add` идёт через `ICollection<T>.Add`. Это буквально антипаттерн из идиомы 5, оформленный как extension-метод. То же относится к `Extensions.AddRange` — предпочитайте конкретные `ReactiveList<T>.AddRange` и `ReactiveHashSet<T>.UnionWith`.

---

## Производительность

Все числа ниже — из `Docs/Elements/Performance/*.md`. Условия замеров заявлены там же: «measured on a **MacBook with Apple M1**» и «collections containing **1000 elements of type `object`**», медианное время в микросекундах. Это **относительные** величины для Mono на desktop; на целевом устройстве и под IL2CPP их надо перемерить. Там, где ниже сказано «вывод», это моя интерпретация исходника, а не заявление документации.

### Что заявлено замерами

`ReactiveArrayPerformance.md` — `Array` против `ReactiveArray`:

| Operation | Array | ReactiveArray |
|---|---|---|
| Get | 1.10 | 1.20 |
| Set | 28.00 | 76.90 |
| Copy | 0.70 | 0.50 |
| Enumerator | 0.80 | 28.50 |
| For | 0.70 | 0.70 |
| Clear | 0.40 | 41.50 |

Вердикт страницы: чтение почти как у обычного массива, но «**iterating** with `foreach` or **writing** to an element is **noticeably** **slower** due to event invocations». Сорокакратный штраф `foreach` — самое важное число для миграции.

`ReactiveListPerformance.md` — `List<T>` против `ReactiveList<T>`: паритет или близко к нему на `Add` (30.15 → 31.20), `Clear` (0.40 → 1.20), `CopyTo` (0.40 → 0.40), `Enumerator` (29.35 → 28.80), `For` (1.70 → 1.70), `Get` (1.50 → 1.75); медленнее на `Set` (30.40 → 42.00) «due to event invocation»; быстрее на `Remove` (307.40 → 254.25) и резко на `Remove At` (29.55 → 3.00) «thanks to internal optimizations». Выброс — `Contains`: **1821.35 → 33455.75 μs**, восемнадцатикратная регрессия. Вывод: `Contains`/`IndexOf`/`Remove(item)` на реактивном списке в горячем пути не вызывать; держать рядом `ReactiveHashSet<T>` или `ReactiveDictionary<K,int>` для проверки принадлежности.

`ReactiveLinkedListPerformance.md` — `Remove` 43.50 и `Insert At` 60.80 (против 307.40/254.25 и 242.85/245.85), «**Best for frequent insertions / removals** at any position, especially head or tail». И обратная сторона: `Get` 1277.70, `Set` 1304.00, `For` 1273.55, `Remove At` 2546.10, `CopyTo` 30.65, `Contains` 33605.80; «**Index-based access is slow** (`Get`/`Set`) due to traversal from the head», «**Always use `foreach` for iteration**; avoid `for` loops».

`ReactiveDictionaryPerformance.md` — своя хеш-таблица обходит BCL `Dictionary` на `Set` (35.50 → 10.10), `ContainsKey` (7.10 → 5.70), `Get` (7.40 → 5.80), `Remove` (7.40 → 6.80), `TryGetValue` (34.20 → 32.90), `Clear` (7.10 → 2.40); проигрывает на `Add` (34.10 → 64.40); паритет на `Enumerator` (56.60 → 58.60). Выигрыши страница объясняет «internal optimizations».

`ReactiveHashSetPerformance.md` — «**much lower median times for Add, Contains, and Enumerator**, thanks to internal optimizations and preallocated slots»: `Contains` 50.10 → 10.00, `Add` 69.30 → 64.30, `Enumerator` 29.60 → 29.60. «Operations like `Clear` and `Remove` are slightly more expensive due to **event invocation** and managing free lists»: `Clear` 1.10 → 32.00, `Remove` 50.30 → 54.80.

### Боксинг

`BestPractices/IteratingReactiveCollections.md` заявляет прямо: «When iterating via interfaces using `IEnumerator<T>`, the iterator may **box**, causing **heap allocations** and **GC pressure**. To avoid this, use the **concrete collection type** whenever possible.» Итоговая таблица той же страницы: интерфейсный обход — «⚠️ Causes boxing and GC pressure», конкретный тип — «✅ No boxing, faster».

Struct-перечислитель подтверждён у каждого конкретного типа: `ReactiveArray.Enumerator`, `ReactiveList.Enumerator`, `ReactiveLinkedList.Enumerator`, `ReactiveHashSet.Enumerator`, `ReactiveDictionary.Enumerator`, `ReadOnlyKeyCollection.Enumerator`, `ReadOnlyValueCollection.Enumerator`. `Reset()` объявлен явной реализацией (`void IEnumerator.Reset()`) у `ReactiveHashSet.Enumerator` и `ReactiveDictionary.Enumerator` — его вызов требует боксинга; у `ReactiveArray`, `ReactiveList`, `ReactiveLinkedList` и у обоих перечислителей представлений словаря `Reset()` публичен.

Второй источник боксинга — `ReactiveDictionary`: явные `ICollection<K> IDictionary<K,V>.Keys => new ReadOnlyKeyCollection(this)` и аналогичные члены `Values` для `IDictionary`/`IReadOnlyDictionary` боксируют `readonly struct`-представление на **каждом** чтении свойства, а потом боксируется перечислитель. `dict.Keys`/`dict.Values` на конкретном типе не аллоцируют.

Третий — подписки: `IDisposable sub = list.SubscribeAdded(h);` боксирует `readonly struct`. Держите их в `var`/`using`/поле конкретного типа.

### AggressiveInlining — где стоит

Подтверждено в исходниках: `ReactiveHashSet` — `Contains`, `Add`, `UnionWith`, `Clear`, `IncreaseCapacity`, `RemoveInternal`, `AddInternal`, `FindIndex`; `ReactiveDictionary` — `Add(K,V)`, `TryAdd(K,V)`, `Remove(K)`, `IncreaseCapacity`; `ReactiveLinkedList` — `AllocateNode`, `GetNodeIndex`, `RemoveNode`; `InternalUtils.Contains`; все двенадцать фабрик `Extensions.SubscribeXxx`. В `ReactiveArray` и `ReactiveList` этого атрибута нет **нигде**, включая их горячие индексаторы. Вывод: если вы оборачиваете эти вызовы своими методами, ставьте `[MethodImpl(MethodImplOptions.AggressiveInlining)]` и на обёртку, иначе цепочка инлайнинга обрывается на вашем методе.

### Хранение и рост

Плоские массивы везде, узлы поэлементно не аллоцируются нигде. `ReactiveArray`/`ReactiveList` — `T[]`. `ReactiveLinkedList` — пулованный `Node[]` с целочисленным free-list (односвязный: `Node` содержит только `item` и `next`). `ReactiveHashSet`/`ReactiveDictionary` — `Slot[]` + `int[] buckets`, free-list и фиксированная prime-лестница (`InternalUtils.PrimeTable`, 31 запись, `Array.BinarySearch` в `CeilToPrime`); рост — `_capacity = PrimeTable[++_primeIndex]`, индексация, а не поиск простого. Ни одна BCL-коллекция нигде не обёрнута, несмотря на XML-док `ReactiveDictionary`, который утверждает «A reactive wrapper around `Dictionary<K,V>`». `ReactiveList` и `ReactiveLinkedList` растут удвоением ёмкости; у `ReactiveList` есть зажим в `int.MaxValue`.

### Пулование и stack

`ArrayPool<T>.Shared` используется **только** `ReactiveHashSet` (`private static readonly ArrayPool<T> s_arrayPool`) — в `UnionWith`, `ExceptWith`, `IntersectWith`, `SymmetricExceptWith`, `ReplaceWith` и `Clear`, каждая аренда возвращается в `finally`. У `ReactiveDictionary` пулования нет вовсе: `Clear`/`Remove` поднимают события инлайн. Вывод: все шесть методов берут аренду **до** входа в `try`, поэтому исключение во время фазы мутации теряет буфер пула.

`IntersectWith` держит маркеры пересечения вне кучи: `Span<bool> intersectedItems = stackalloc bool[_lastIndex];` — быстро, но без ограничения размера, и граница — высшая точка заполнения, то есть множество, которое когда-то было большим, стек-аллоцирует под свой прежний размер.

### `_lastIndex`, а не `Count`

Итерация и `CopyTo` у обеих хеш-коллекций стоят O(`_lastIndex`), потому что подметают массив слотов с проверкой `slot.exists`. Оба типа сбрасывают `_lastIndex` в 0, когда `Remove`/`RemoveInternal` приводит `_count` к нулю, и `Clear` тоже сбрасывает, — то есть стоимость подметания «самозалечивается» только при полном опустошении. Коллекция, которая выросла и сжалась, продолжает платить за полный проход.

### Мелочи, подтверждённые в исходниках

* Каждое поднятие события — `?.Invoke(...)`, поэтому коллекция без подписчиков платит только проверку на `null`. Обратная сторона: приведённые выше штрафы `Set`/`Clear`/`Enumerator` — это цена **наличия** подписчиков, и один `OnStateChanged` на элемент (антипаттерн `Add` в цикле) умножает каждый downstream-обработчик.
* Диспетчеризация компарера — чтение статического generic-поля, не виртуальный поиск на вызов: `private static readonly IEqualityComparer<T> s_comparer = EqualityComparer.GetDefault<T>()`, одно значение на закрытый generic-тип, разрешаемое один раз в статическом конструкторе `Cache<T>`. `ReactiveHashSet.RemoveInternal` и `ReadOnlyValueCollection.Contains` дополнительно поднимают компарер (а `RemoveInternal` — ещё `_slots`, `_buckets`, `_capacity`) в локальные переменные до цикла, чтобы не читать статические поля на каждой итерации.
* Два места всё ещё копируют `Slot` целиком на каждый посещённый слот: `ReactiveHashSet.IsSubsetOf` и `IsProperSubsetOf` используют `Slot slot = _slots[i];` по значению вместо `ref readonly`. Это единственные пропуски `ref readonly` в модуле.
* `ReactiveLinkedList.RemoveAt(index)` делает два полных обхода от головы: `GetNodeIndex(index)` и `GetNodeIndex(index - 1)` (второй пропускается только при `index == 0`). Индексатор и `Insert(index, _)` обходят по одному разу — любой индексный цикл по этому типу O(N²).
* Сериализация Unity аллоцирует и боксирует: `ReactiveList`, `ReactiveLinkedList` и `ReactiveHashSet` реализуют `OnBeforeSerialize` как `this.serializedItems = this.ToArray();` — LINQ `ToArray` по пути `IEnumerable<T>`, то есть боксированный перечислитель плюс свежий массив на каждую сериализацию в инспекторе. `ReactiveDictionary` сделан лучше: аллоцирует `Pair[_count]` точного размера и обходит своим struct-перечислителем. `OnAfterDeserialize` переигрывает через `Populate` (список), `Clear` + `Add` на элемент (связный список), `Clear` + `UnionWith` (множество), `Clear` + `Add` на пару (словарь) — то есть десериализация поднимает полный набор событий.
* `ReactiveList.Populate` в ветке среза хвоста сдвигает элементы рукописным внутренним циклом (O(N·count)), тогда как `Remove`/`RemoveAt`/`Insert` того же класса используют `Array.Copy`.
* `ReactiveDictionary.Remove(KeyValuePair)` — это `Contains(item) && Remove(item.Key)`, две полные пробы; `Contains(KeyValuePair)` сам по себе — `TryGetValue` плюс сравнение значения.

---

## Грабли

* **`ReactiveArray<T>` нельзя перечислять через интерфейс.** `IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator();` в `Collections/Array/ReactiveArray.cs` не передаёт `this`, поле массива внутри перечислителя `null`, и `MoveNext()` бросает `NullReferenceException`. Любой `foreach` или LINQ по полю типа `IReactiveArray<T>` / `IReadOnlyReactiveArray<T>` / `IEnumerable<T>` падает в рантайме. Объявляйте поле как `ReactiveArray<T>` либо патчите строку на `new Enumerator(this)`.
* **Неиндексный `OnItemAdded` у двух типов списка не отписывается.** `ReactiveList<T>` и `ReactiveLinkedList<T>` оба реализуют его как `event Action<T> IReadOnlyReactiveCollection<T>.OnItemAdded { add => this.onItemAdded += value; remove => this.onItemRemoved -= value; }`. Отписка утекает обработчик добавления и молча отцепляет посторонний обработчик удаления. Это касается и `ItemAddedSubscription.Dispose()`, взятой через `IReadOnlyReactiveCollection<T>`: она идёт через тот же аксессор. Используйте индексные события и индексные подписки.
* **Рассогласование хеша и компарера в `ReactiveHashSet<T>`.** `Add`, `AddInternal`, `Contains`, `FindIndex` и публичный `Remove` бакетируют по `item.GetHashCode()`, а `IncreaseCapacity` (строка 706) и `RemoveInternal` (строка 725) — по `s_comparer.GetHashCode(item)`. Кусается это только там, где формула из `Utils/EqualityComparer.cs` расходится с собственным `GetHashCode` структуры. Для `Vector2`, `Vector3`, `Vector4`, `Color`, `Rect`, `Bounds`, `Quaternion`, `Vector2Int` и `RectInt` формулы совпадают с Unity байт в байт — эти типы не затронуты. Реально расходятся `Vector3Int` (Unity сдвигает на 4/28, компарер — на 2), `Color32` (Unity хеширует через `(Vector4)`), `BoundsInt` (построен на `Vector3Int`) и `RangeInt` (у которого в Unity своего `GetHashCode` нет вообще). `ReactiveHashSet<Vector3Int>` после роста ёмкости или удаления может начать возвращать `false` из `Contains` для присутствующих элементов; `ReactiveHashSet<Vector3>` — безопасен.
* **`ReactiveHashSet.IntersectWith` арендует буфер неверного размера.** `s_arrayPool.Rent(other.Count())`, но заполняется он элементами, которых в `other` **нет**, а их число ограничено размером **множества**. Множество из 100 элементов, пересечённое с одним совпадающим элементом, арендует массив минимального бакета (16) и пишет 99 записей → `IndexOutOfRangeException`. Существующий тест `Tests/EditMode/Collections/ReactiveHashSet/ReactiveHashSetTests_IntersectWith.cs` использует множество из 5 элементов, и минимальный бакет пула это скрывает.
* **`ReactiveHashSet.SymmetricExceptWith` арендует оба буфера по `_lastIndex`.** `addedCount` при этом ограничен размером `other`. `ArrayPool<T>.Shared.Rent(0)` возвращает массив нулевой длины, поэтому `SymmetricExceptWith` на **пустом** множестве с любым не-null элементом в `other` бросает `IndexOutOfRangeException` сразу; и любое множество, у которого слотов меньше, чем приходящих новых элементов, выйдет за границу. Передача материализованного массива здесь не помогает. Тест покрывает только множество из 5 элементов против 3 элементов.
* **`ReactiveHashSet.IntersectWith` делает `stackalloc` без ограничения.** `Span<bool> intersectedItems = stackalloc bool[_lastIndex];` — множество с большой высшей точкой заполнения переполняет стек; в Unity это неперехватываемое завершение процесса.
* **`ReactiveHashSet.IntersectWith` поднимает `OnStateChanged` всегда.** `this.OnStateChanged?.Invoke();` стоит **вне** охраны `if (removedCount > 0)`, поэтому пересечение с надмножеством даёт ложное изменение состояния и полное перестроение UI. У `UnionWith`, `ExceptWith`, `SymmetricExceptWith` и `ReplaceWith` охрана на месте.
* **Двойное перечисление `other`.** `UnionWith`, `ExceptWith`, `IntersectWith`, `ReplaceWith`, `SetEquals`, `IsSubsetOf`, `IsProperSubsetOf`, `IsSupersetOf` и `IsProperSupersetOf`, а также `IEnumerable`-конструкторы `ReactiveHashSet`, `ReactiveLinkedList` и `ReactiveDictionary` вызывают `other.Count()` и затем перечисляют `other` снова. LINQ-запрос или генератор выполняется дважды; если второй проход отдаст больше элементов, чем первый, аренда из `ArrayPool` будет проиндексирована за пределы. Всегда передавайте `T[]` или `List<T>`.
* **`ReplaceWith` — не дифф двух хеш-таблиц.** Он перечисляет `other` `2 + (число живых слотов)` раз: один раз на `other.Count()`, один раз на проход добавления и затем по разу на каждый выживший слот внутри `InternalUtils.Contains(other, item, s_comparer)`. Это O(множество × other). Та же форма в `IsSubsetOf` и `IsProperSubsetOf`.
* **Семейство subset/superset/`SetEquals` сравнивает с `other.Count()`, который считает дубликаты.** Корректное надмножество, у которого в `other` есть повторы, будет отвергнуто.
* **Четыре разные политики `null`** (см. раздел «Модель»). Самая опасная — `ReactiveLinkedList.Add(null)`: молчаливый no-op без событий и без исключения. Самая неожиданная — `null`-ключ в `ReactiveDictionary`: `NullReferenceException`, а не `ArgumentNullException`, потому что ключ не проверяется на `null` ни в одном методе, а `key.GetHashCode()` вызывается сразу.
* **`Contains(null)` всегда `false`.** `ReactiveArray.Contains`, `ReactiveList.Contains`/`IndexOf` и `ReactiveLinkedList.IndexOf` целиком завёрнуты в `if (item != null)`. Пустой (null) слот найти нельзя.
* **`params`-конструкторы присваивают массив вызывающего без копии.** `ReactiveArray(params T[] elements)` делает `this.items = elements;`, `ReactiveList(params T[] items)` — `this.items = items ?? Array.Empty<T>();`. Последующая мутация этого массива меняет реактивную коллекцию без единого события. Передавайте свежий массив или используйте `IEnumerable<T>`-конструктор, который делает `.ToArray()`.
* **Порядок событий не единообразен.** `ReactiveList.Clear()` поднимает `OnStateChanged` **первым**, затем per-item `OnItemRemoved`; `ReactiveDictionary.Clear()` и `ReactiveHashSet.Clear()` — наоборот. Сеттер индексатора и `Add`/`TryAdd` у `ReactiveDictionary` поднимают `OnStateChanged` **до** `OnItemAdded`/`OnItemChanged`, а `Remove` — после события элемента.
* **`ReactiveDictionary.Clear()` небезопасен для реентерабельности.** `OnItemRemoved` вызывается внутри обхода слотов, до обнуления `_count` и до `Array.Fill(_buckets, UNDEFINED_INDEX)`. Обработчик, который в этот момент читает словарь, видит `Count != 0` и бакеты, указывающие на слоты с уже сброшенным `exists`. `ReactiveHashSet.Clear()` — тот, который сначала складывает в пулованный буфер и реентерабельность переживает.
* **`Dispose()` — это шторм событий, а не отписка.** У `ReactiveList`, `ReactiveLinkedList`, `ReactiveHashSet` и `ReactiveDictionary` он сначала вызывает `Clear()` и только потом обнуляет поля событий. Единственный «тихий» — `ReactiveArray.Dispose()`.
* **Удержание ссылок.** `ReactiveList.Clear()` пишет `count = 0` и не обнуляет слоты; `Remove`/`RemoveAt` сдвигают `Array.Copy` и не обнуляют освободившийся хвост; `ReactiveDictionary.Remove`/`Clear` и `ReactiveHashSet.Remove`/`Clear` сбрасывают `slot.exists`, но не `slot.key`/`slot.value`. `Dispose()` ничего не исправляет — он и есть `Clear()`. Отпускает ссылки только `Populate`/перезапись или выбрасывание коллекции. Для списка тяжёлых объектов или ссылок на `GameObject` это настоящая утечка.
* **`ReactiveLinkedList.Clear()` отдаёт подписчику индекс слота, а не позицию в списке.** После любого churn этот индекс бессмыслен для вьюшки, которая удаляет строку по индексу.
* **`ReactiveLinkedList<T>` односвязный**, вопреки «doubly-linked list» в `BestPractices/IteratingReactiveCollections.md` (строки 117 и 268). Следствие — `RemoveAt` 2546.10 μs против 3.00 у `ReactiveList`. Пользуйтесь `Remove(item)` (43.50), а не `RemoveAt`.
* **`ReactiveLinkedList<T>` использует BCL-компарер**, поэтому его семантика равенства для Unity-математики отличается от всех остальных коллекций модуля.
* **`ReactiveLinkedList.CopyTo(int, T[], int, int)` валидирует `sourceIndex < 0 || sourceIndex >= _count`.** Копия нулевой длины из пустого списка — и любая копия, начинающаяся с `index == Count`, — бросает `ArgumentOutOfRangeException`, тогда как `ReactiveArray` и `ReactiveList` делегируют `Array.Copy` и проходят.
* **Валидация `CopyTo` неравномерна.** `ReactiveList.CopyTo(T[], int)` — это один `Array.Copy` без проверок, поэтому задокументированные `ArgumentNullException`/`ArgumentOutOfRangeException`/`ArgumentException` никогда не срабатывают: вы получите то, что бросит `Array.Copy`. `ReactiveDictionary.CopyTo`, `ReadOnlyKeyCollection.CopyTo` и `ReadOnlyValueCollection.CopyTo` проверяют `null` и отрицательный индекс, но не длину приёмника, и дают `IndexOutOfRangeException` вместо `ArgumentException`. `ReactiveArray.CopyTo` (оба) валидирует полностью.
* **`ReactiveList.AddRange` не атомарен.** `null` в середине источника бросает `ArgumentNullException` после того, как часть элементов уже добавлена и события по ним разосланы.
* **`ReactiveArray.Clear()` на массиве нулевой длины не поднимает ничего** — в начале стоит `if (length == 0) return;`, в том числе до `OnStateChanged`. Это зафиксировано тестом `Clear_EmptyArray_DoesNotThrowOrFireEvents` в `Tests/EditMode/Collections/ReactiveArrayTests.cs`.
* **Сдвиг хвоста в `ReactiveList.Populate` поэлементный** (O(N·count)), в отличие от `Array.Copy` в `Remove`/`RemoveAt`/`Insert`.
* **Все обходы и `CopyTo` у хеш-коллекций идут до `_lastIndex`, а не до `Count`.** Коллекция, выросшая и сжавшаяся, платит полную стоимость подметания, пока не опустеет полностью или не будет очищена.
* **Защиты от мутации во время перечисления нет.** Ни version counter, ни `InvalidOperationException` — только молча пропущенные или повторённые элементы. Поскольку обработчики выполняются синхронно внутри мутаций, подписчик, меняющий ту же коллекцию, портит текущий обход.
* **`Enumerator.Reset()` требует боксинга у хеш-коллекций** (явная реализация `void IEnumerator.Reset()`), но публичен у `ReactiveArray`, `ReactiveList`, `ReactiveLinkedList` и у перечислителей представлений словаря.
* **`InternalUtils.CeilToPrime` бросает `InvalidOperationException`** при выходе ёмкости за последнее простое таблицы (1610612741) — жёсткий потолок роста словаря и множества.
* **`Extensions.CopyFrom` и `Extensions.CopyTo` деструктивны** (`destination.Clear()` в первой строке), а `Extensions.AddRange` идёт через `ICollection<T>.Add` по одному элементу, поднимая `OnStateChanged` на каждый. Для реактивных приёмников — только `Populate`/`ReplaceWith`/конкретные `AddRange`/`UnionWith`.
* **`Extensions` — `public partial class`, а не `static class`.** Его можно инстанцировать и унаследовать.
* **`ReactiveArray<T>` сериализуется иначе, чем остальные.** Он не реализует `ISerializationCallbackReceiver` и показывает в инспекторе параметр `items`; `ReactiveList`, `ReactiveLinkedList`, `ReactiveHashSet` и `ReactiveDictionary` реализуют его и показывают `serializedItems`. Колбэки десериализации поднимают полный набор событий, поэтому поведение, подписавшееся в `Awake`, увидит очередь добавлений.
* **Вне Unity три коллекции не `[Serializable]`.** Атрибут у `ReactiveLinkedList`, `ReactiveHashSet` и `ReactiveDictionary` висит на `#if UNITY_5_3_OR_NEWER`-partial; у `ReactiveArray` и `ReactiveList` — на основном объявлении.
* **Примеры в документации не компилируются.** `.Copy(int, T[], int, int)` (реальное имя — `CopyTo`) в `IReactiveArray.md:139`, `IReadOnlyReactiveArray.md:116`, `IReadOnlyReactiveList.md:155`, `Manual.md:60`, `ReactiveArray.md:146`. `OnItemInserted`/`OnItemDeleted` в `ReactiveList.md`, `ReactiveLinkedList.md`, `Manual.md`. `reactiveSet.OnItemChanged` в `IReactiveSet.md:104`. `IReadOnlyReactiveCollection<out T>` в `IReadOnlyReactiveCollection.md:98`. Сдвоенный ```` ```csharp ```` в секции hash set в `Manual.md` (строки 152–154). Правило на время миграции: примеры из доков компилировать до того, как они попадут в код.

---

## Где читать

Пути относительно `C:/!!!Work/_AtomicUpstream`.

### Документация: обзор и лучшие практики

* `Docs/Elements/Collections/Manual.md` — входная страница области. Четыре блока «Examples of Usage» (array, list, dictionary, hash set), затем «API Reference» со ссылками на все 16 остальных страниц области, «Performance» со ссылками на 5 страниц замеров и «Best Practices». **Самый удобный вход, но код в нём сломан в трёх местах**: `.Copy(...)` в примере массива, `OnItemInserted`/`OnItemDeleted` в примере списка и сдвоенная открывающая ограда ```` ```csharp ```` в примере hash set.
* `Docs/BestPractices/IteratingReactiveCollections.md` — единственная страница, которая системно объясняет боксинг и выбор `for`/`foreach`. Три раздела с парами ❌/✅ (конкретный тип против интерфейса; `for` по array/list; `foreach` по linked list/dictionary/hash set), сводная таблица и совет кешировать `Count`. Здесь же утверждение, что `ReactiveLinkedList` — «doubly-linked list»; оно неверно. Во второй половине файла лежит старая версия того же текста, закомментированная в HTML-комментарий.

### Документация: интерфейсы

Все страницы интерфейсов построены одинаково: ToC → «Example of Usage» → «API Reference» (Type / Events / Properties / Indexers / Methods).

* `Docs/Elements/Collections/IReadOnlyReactiveCollection.md` — базовый read-only контракт: `OnStateChanged`, `OnItemAdded`/`OnItemRemoved` как `Action<T>`, `Contains`, `CopyTo`. Декларация типа на строке 98 печатает `<out T>` — ковариантности в исходнике нет. Вложенные struct-подписки не документированы.
* `Docs/Elements/Collections/IReactiveCollection.md` — мутабельный маркер поверх предыдущего; содержательно добавляет только пример `Add`/`Remove`/`Clear` и перечисление унаследованных членов.
* `Docs/Elements/Collections/IReadOnlyReactiveArray.md` — индексный read-only контракт, `OnItemChanged : Action<int,T>`, `Length`, две перегрузки `CopyTo`. В примере (строка 116) вызывается несуществующий `Copy(...)`.
* `Docs/Elements/Collections/IReactiveArray.md` — пишущая сторона массива. Заявленная база неполна: в исходнике между ними стоит недокументированный `IArray<T>`. В примере (строка 139) опять `Copy(...)`.
* `Docs/Elements/Collections/IReadOnlyReactiveList.md` — самая полезная страница про `new`-затенение событий: кроме «Basic usage» в ней есть отдельный пример «Cast to IReadOnlyReactiveCollection», который показывает, как добраться до неиндексной пары `Action<T>`. В примере базового использования — `Copy(...)` (строка 155).
* `Docs/Elements/Collections/IReactiveList.md` — полностью мутабельный индексный список; объясняет, зачем нужен `new bool Contains`.
* `Docs/Elements/Collections/IReactiveSet.md` — контракт множества плюс самый длинный набор примеров операций над множествами (`UnionWith`, `ExceptWith`, `IntersectWith`, `SymmetricExceptWith`, `IsSubsetOf`, `Overlaps`, `SetEquals`). Строка 104 подписывается на `OnItemChanged`, которого у множеств нет; `ReplaceWith`, `IsEmpty`, `IsNotEmpty` здесь не упомянуты.
* `Docs/Elements/Collections/IReadOnlyReactiveDictionary.md` — read-only контракт словаря и то, как `Action<K,V>`-события затеняют `Action<KeyValuePair<K,V>>`. Четвёртая подписка (`ItemChangedSubscription`) в документе не описана.
* `Docs/Elements/Collections/IReactiveDictionary.md` — мутабельный словарь; объясняет четыре `new`-члена, снимающих неоднозначность `IDictionary`/`IReadOnlyDictionary`.

### Документация: конкретные классы

Страницы классов дополнительно содержат «Constructors» и «Inspector Settings» — это те разделы, из-за которых их стоит открывать.

* `Docs/Elements/Collections/ReactiveArray.md` — конструкторы (включая `params`, где нет копии — об этом страница молчит), события, индексатор, `Clear`/`Fill`/`Resize`/`Populate`/`CopyTo`. «Inspector Settings» указывает параметр `items` — единственная коллекция с таким именем поля. В примере (строка 146) `Copy(...)`.
* `Docs/Elements/Collections/ReactiveList.md` — самая подробная страница по списку: 478 строк, конструкторы, `Capacity`, `AddRange`, `Populate`, обе `CopyTo`. «Inspector Settings» — `serializedItems`. Файл противоречит сам себе: пример использования подписывается на `OnItemInserted`/`OnItemDeleted`, а раздел «API Reference» того же файла документирует корректные `OnItemAdded`/`OnItemRemoved`.
* `Docs/Elements/Collections/ReactiveLinkedList.md` — конструкторы, индексатор, `Add`/`Insert`/`Remove`/`RemoveAt`, `Clear`, «Inspector Settings» (`serializedItems`). Повсюду в разделе методов использует устаревшие имена событий `OnItemInserted`/`OnItemDeleted`; ничего не говорит о том, что список односвязный, что `Clear` отдаёт индекс слота и что здесь используется BCL-компарер.
* `Docs/Elements/Collections/ReactiveHashSet.md` — конструкторы (все четыре), события, полный список операций над множествами. `ReplaceWith`, `IsEmpty()` и `IsNotEmpty()` отсутствуют, хотя они публичны. «Inspector Settings» — `serializedItems`; о том, что в исходнике это поле под Odin помечено `[HideInPlayMode]`, страница не говорит.
* `Docs/Elements/Collections/ReactiveDictionary.md` — 663 строки, самая большая страница области: шесть пронумерованных примеров (базовое использование, `TryAdd`, удаление, обход ключей и значений, подписка на события, инициализация из коллекций), все пять конструкторов, «Inspector Settings» и раздел «Nested Types». Начинать знакомство со словарём лучше отсюда, а не с `Manual.md`.
* `Docs/Elements/Collections/ReactiveDictionaryKeyCollection.md` — `ReadOnlyKeyCollection`: свойства, методы и отдельный раздел «❌ Unsupported Methods» (`Add`/`Clear`/`Remove` бросают `NotSupportedException`).
* `Docs/Elements/Collections/ReactiveDictionaryValueCollection.md` — то же для `ReadOnlyValueCollection`; стоит открыть, чтобы увидеть, что `Contains` здесь O(N)-обход, а не хеш-поиск.

### Документация: замеры

Пять однотипных страниц: условия замера, таблица медиан и краткий вывод. Все числа из них перечислены в разделе «Производительность» выше.

* `Docs/Elements/Performance/ReactiveArrayPerformance.md` — `Array` против `ReactiveArray`. Открывать за одним числом: `Enumerator` 0.80 → 28.50.
* `Docs/Elements/Performance/ReactiveListPerformance.md` — `List<T>` против `ReactiveList<T>`, 11 операций. Главное — регрессия `Contains` 1821.35 → 33455.75.
* `Docs/Elements/Performance/ReactiveLinkedListPerformance.md` — тройное сравнение `List` / `ReactiveList` / `ReactiveLinkedList`. Единственная страница с явными правилами применимости в разделе «Explanation».
* `Docs/Elements/Performance/ReactiveDictionaryPerformance.md` — `Dictionary` против `ReactiveDictionary`, 8 операций; обоснование того, почему реактивный словарь — апгрейд, а не компромисс.
* `Docs/Elements/Performance/ReactiveHashSetPerformance.md` — `HashSet<T>` против `ReactiveHashSet<T>`, 5 операций; здесь сформулирован механизм выигрыша («preallocated slots»).

### Исходники (единственный достоверный источник)

* `Assets/Plugins/Atomic/Elements/Scripts/Collections/IReadOnlyReactiveCollection.cs` — базовый интерфейс плюс три `readonly struct`-подписки целиком: смотреть здесь, как они подключаются и отключаются.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/IReactiveCollection.cs` — пятнадцать строк; интересен только комментарием `// ReSharper disable PossibleInterfaceMemberAmbiguity`.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Extensions.cs` — весь extension-слой: деструктивные `CopyFrom`/`CopyTo`, медленные `AddRange`, двенадцать `SubscribeXxx` и `ToReactiveDictionary`. В документации отсутствует полностью.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Array/IArray.cs` — недокументированный интерфейс, откуда у `IReactiveArray<T>` берутся `Fill`, `Resize`, `Populate`, `Clear`.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Array/IReadOnlyReactiveArray.cs`, `Array/IReactiveArray.cs` — реальные декларации (без `out`, с default interface implementation `Count => Length`).
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Array/ReactiveArray.cs` — дефект перечислителя (`new Enumerator()` без `this`), ранний выход `Clear` на нулевой длине, `params`-конструктор без копии, полная валидация обоих `CopyTo`, `Dispose`, который только отцепляет события.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/List/IReadOnlyReactiveList.cs`, `List/IReactiveList.cs` — `new`-затенение событий и три «новых» типа подписок с теми же именами, что у базового интерфейса.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/List/ReactiveList.cs` — читать целиком перед портом: дефект аксессора `remove` (строки 35–46), `Clear()` с `OnStateChanged` первым, `AddRange` с предразмещением, эталонный `Populate`, `CopyTo` без валидации, `Dispose() => Clear() + обнуление`, Unity-partial с `OnBeforeSerialize => this.ToArray()`.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/List/ReactiveLinkedList.cs` — `struct Node { T item; int next; }` (строка 76), BCL-компарер (строка 21), `Add(null)` как no-op, двойной обход в `RemoveAt` (строки 271–272), `Clear()` с индексом слота, строгая валидация `CopyTo`, `OnAfterDeserialize` как `Clear` + `Add` на элемент.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Set/IReactiveSet.cs` — тринадцать строк; подтверждает, что `OnItemChanged` у множеств нет.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Set/ReactiveHashSet.cs` — 901 строка и самый плотный по дефектам файл модуля: рассогласование хешей (`item.GetHashCode()` против `s_comparer.GetHashCode` в строках 706 и 725), неограниченный `stackalloc` (строка 407), неверный размер аренды в `IntersectWith` (строка 413) и `SymmetricExceptWith` (строки 448, 451), `OnStateChanged` вне охраны в `IntersectWith`, `ReplaceWith` с O(множество × other), `Slot` по значению в `IsSubsetOf`/`IsProperSubsetOf`. Здесь же эталонная staged-схема `UnionWith` и `Clear`.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Dictionary/IReadOnlyReactiveDictionary.cs`, `Dictionary/IReactiveDictionary.cs` — четыре типа подписок и `new`-члены для снятия неоднозначности.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Dictionary/ReactiveDictionary.cs` — порядок событий (`OnStateChanged` до `OnItemAdded` в сеттере, `Add`, `TryAdd`), `key.GetHashCode()` без проверки на `null`, `Clear()` с инлайновой рассылкой, `CopyTo` без проверки длины, `IncreaseCapacity` на `InternalUtils.PrimeTable`.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Dictionary/ReactiveDictionary_Enumerator.cs` — 85 строк и готовый к копированию образец struct-перечислителя с `ref readonly Slot`.
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Dictionary/ReactiveDictionary_KeyCollection.cs`, `Dictionary/ReactiveDictionary_ValueCollection.cs` — `readonly struct`-представления; во втором видно, как компарер поднимается в локальную переменную до цикла (`var comparer = s_valueComparer;`).
* `Assets/Plugins/Atomic/Elements/Scripts/Collections/Dictionary/ReactiveDictionary_Unity.cs` — `[Serializable] internal struct Pair`, `Pair[] serializedItems` и единственный `OnBeforeSerialize` в модуле, который не идёт через LINQ.
* `Assets/Plugins/Atomic/Elements/Scripts/Utils/EqualityComparer.cs` — `internal static class` с комментарием `//Steal from UniRx! ;)`: тринадцать покомпонентных компареров для Unity-структур, `Cache<T>` и `GetDefault<T>()`. Открывать, когда надо сверить формулу `GetHashCode` конкретного типа с Unity-овской (`Vector3Int` — строка 239, `Color32` — 207, `RangeInt` — 252, `BoundsInt` — 281).
* `Assets/Plugins/Atomic/Elements/Scripts/Utils/InternalUtils.cs` — `PrimeTable` (31 запись), `CeilToPrime` на `Array.BinarySearch`, `Contains<T>` с `AggressiveInlining`, и закомментированные `GetPrime`/`IsPrime` — следы того, что таблица заменила рантайм-поиск.

### Тесты как исполняемая спецификация

* `Assets/Plugins/Atomic/Elements/Tests/EditMode/Collections/ReactiveArrayTests.cs` — в частности `Clear_EmptyArray_DoesNotThrowOrFireEvents`, фиксирующий ранний выход `Clear()`.
* `Assets/Plugins/Atomic/Elements/Tests/EditMode/Collections/ReactiveDictionaryTests.cs`, `Collections/ReactiveList/`, `Collections/LinkedList/`, `Collections/ReactiveHashSet/` — покрытие по типам. Полезно смотреть, **чего** там нет: `ReactiveHashSetTests_IntersectWith.cs` работает с множеством из 5 элементов, и минимальный 16-элементный бакет `ArrayPool` прячет ошибку размера аренды.
* `Assets/Plugins/Atomic/Elements/Tests/EditMode/Collections/ReactiveSortedListTests.cs`, `ReactiveSortedSetTests.cs`, `ReactiveSortedDictionaryTests.cs` — закомментированы целиком; соответствующих типов в поставке нет.
* `Assets/Plugins/Atomic/Elements/Tests/EditMode/Utils/InternalUtilsTests.cs` — класс `InternalUtilsTests` существует, но все 38 из 45 строк (`[TestCase]` и тела тестов) закомментированы: `CeilToPrime` и `Contains<T>` не покрыты ничем.
