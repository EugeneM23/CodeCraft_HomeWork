using System.Collections.Generic;
using System.Linq;
using Game.Scripts.Player;
using Gameplay;
using UnityEngine;

namespace Gameplay
{
    public class TickableManager : MonoBehaviour
    {
        private List<ITickable> _tickables = new();
        private List<IFixedTickable> _fixedTickable = new();
        private List<IInitializeble> _initializeble = new();

        public void Run(diContainer diContainer)
        {
            foreach (var item in diContainer.GetAll<ITickable>())
                _tickables.Add(item);

            foreach (var item in diContainer.GetAll<IFixedTickable>())
                _fixedTickable.Add(item.Log());

            foreach (var item in diContainer.GetAll<IInitializeble>())
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