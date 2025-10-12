using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.Player
{
    public class TickableManager : MonoBehaviour
    {
        [SerializeField] private DiContainer _ioContainer;

        private List<ITickable> _tickables = new();
        private List<IFixedTickable> _fixedTickable = new();
        private List<IInitializeble> _initializeble = new();

        private void Start()
        {
            foreach (var item in _ioContainer.GetAll<ITickable>())
                _tickables.Add(item);

            foreach (var item in _ioContainer.GetAll<IFixedTickable>())
                _fixedTickable.Add(item);

            foreach (var item in _ioContainer.GetAll<IInitializeble>())
                _initializeble.Add(item);

            foreach (var item in _initializeble)
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
}