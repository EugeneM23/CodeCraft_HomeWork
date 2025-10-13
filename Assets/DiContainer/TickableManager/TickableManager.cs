using System.Collections.Generic;
using Game.Scripts.Player;
using Gameplay;
using UnityEngine;

public class TickableManager : MonoBehaviour
{
    public static TickableManager Instance { get; private set; }

    private readonly List<ITickable> _tickables = new();
    private readonly List<IFixedTickable> _fixedTickable = new();
    private readonly List<IInitializeble> _initializeble = new();

    private readonly HashSet<DiContainer> _registeredContainers = new();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Проинициализировать контейнеры, которые уже на сцене
        GameObjectContext[] contexts = FindObjectsOfType<GameObjectContext>();
        SceneContext[] contextsScene = FindObjectsOfType<SceneContext>();

        foreach (var item in contexts)
            Run(item.Container);

        foreach (var item in contextsScene)
            Run(item.Container);

        // Выполнить Initialize только для тех, кто был добавлен в процессе Run
        foreach (var item in _initializeble)
            item.Initialize();
    }

    // Возвращает список только что добавленных IInitializeble
    public List<IInitializeble> Run(DiContainer diContainer)
    {
        if (diContainer == null)
        {
            Debug.LogWarning("[TickableManager] Run called with null container.");
            return new List<IInitializeble>();
        }

        if (_registeredContainers.Contains(diContainer))
            return new List<IInitializeble>();

        _registeredContainers.Add(diContainer);

        var newlyAddedInitializebles = new List<IInitializeble>();

        foreach (var item in diContainer.GetAll<ITickable>())
        {
            if (!_tickables.Contains(item))
                _tickables.Add(item);
        }

        foreach (var item in diContainer.GetAll<IFixedTickable>())
        {
            if (!_fixedTickable.Contains(item))
                _fixedTickable.Add(item);
        }

        foreach (var item in diContainer.GetAll<IInitializeble>())
        {
            if (!_initializeble.Contains(item))
            {
                _initializeble.Add(item);
                newlyAddedInitializebles.Add(item);
            }
        }

        return newlyAddedInitializebles;
    }

    // Вызывается при инстансе префаба: запускаем Run и инициализируем только вновь добавленные
    public void Test(DiContainer diContainer)
    {
        var newly = Run(diContainer);
        foreach (var item in newly)
            item.Initialize();
    }

    private void Update()
    {
        for (int i = 0; i < _tickables.Count; i++)
            _tickables[i].Tick();
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < _fixedTickable.Count; i++)
            _fixedTickable[i].FixedTick();
    }
}