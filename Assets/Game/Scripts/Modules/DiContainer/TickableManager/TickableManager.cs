using System.Collections.Generic;
using Game.Scripts.Player;
using Gameplay;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class TickableManager : MonoBehaviour
{
    private readonly List<ITickable> _tickables = new();
    private readonly List<IFixedTickable> _fixedTickables = new();
    private readonly List<IInitializeble> _initializables = new();

    public void RunAndInitialize(DiContainer container)
    {
        if (container == null) return;

        foreach (var t in container.GetAll<ITickable>())
            _tickables.Add(t);

        foreach (var t in container.GetAll<IFixedTickable>())
            _fixedTickables.Add(t);

        foreach (var i in container.GetAll<IInitializeble>())
            if (!_initializables.Contains(i))
            {
                _initializables.Add(i);
                i.Initialize();
            }
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