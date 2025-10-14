using System.Collections.Generic;
using System.Linq;
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
            if (!_tickables.Contains(t))
                _tickables.Add(t);

        foreach (var t in container.GetAll<IFixedTickable>())
            if (!_fixedTickables.Contains(t))
                _fixedTickables.Add(t);

        foreach (var i in container.GetAll<IInitializeble>())
            if (i != null && !_initializables.Contains(i))
            {
                _initializables.Add(i);
                i.Initialize();
            }
    }

    public void RemoveServices(DiContainer container)
    {
        _tickables.RemoveAll(t => container.Contains(t));
        _fixedTickables.RemoveAll(t => container.Contains(t));
        _initializables.RemoveAll(i => container.Contains(i));
    }

    private void Update()
    {
        int count = _fixedTickables.Where(t => t is MoveComponent).ToArray().Length;
        
        for (int i = 0; i < _tickables.Count; i++)
        {
            if (_tickables[i] == null)
            {
                _tickables.RemoveAt(i);
                i--;
                continue;
            }

            _tickables[i].Tick();
        }
    }

    private void FixedUpdate()
    {
        int count = _fixedTickables.Where(t => t is GroundSyncComponent).ToArray().Length;

        for (int i = 0; i < _fixedTickables.Count; i++)
        {
            if (_fixedTickables[i] == null)
            {
                _fixedTickables.RemoveAt(i);
                i--;
                continue;
            }

            _fixedTickables[i].FixedTick();
        }
    }
}