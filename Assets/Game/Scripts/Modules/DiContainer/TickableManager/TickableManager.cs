using System.Collections.Generic;
using Game.Scripts.Player;
using Gameplay;
using UnityEngine;

public class TickableManager : MonoBehaviour
{
    public static TickableManager Instance { get; private set; }

    private readonly List<ITickable> _tickables = new();
    private readonly List<IFixedTickable> _fixedTickables = new();
    private readonly List<IInitializeble> _initializables = new();
    
    private readonly HashSet<DiContainer> _registeredContainers = new();

    private void Awake() => Instance = this;

    private void Start()
    {
        foreach (var ctx in FindObjectsOfType<Context>())
            RunAndInitialize(ctx.Container);
    }

    public void RunAndInitialize(DiContainer container)
    {
        if (container == null || !_registeredContainers.Add(container))
            return;

        foreach (var t in container.GetAll<ITickable>())
            AddUnique(_tickables, t);

        foreach (var t in container.GetAll<IFixedTickable>())
            AddUnique(_fixedTickables, t);

        foreach (var i in container.GetAll<IInitializeble>())
            if (AddUnique(_initializables, i))
                i.Initialize();
    }

    private bool AddUnique<T>(List<T> list, T item)
    {
        if (list.Contains(item)) return false;
        list.Add(item);
        return true;
    }

    private void Update()
    {
        foreach (var t in _tickables)
            t.Tick();
    }

    private void FixedUpdate()
    {
        foreach (var t in _fixedTickables)
            t.FixedTick();
    }
}