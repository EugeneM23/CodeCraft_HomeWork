using System;
using System.Collections.Generic;
using System.Linq;
using Game.Scripts.Player;
using Gameplay;
using UnityEngine;

namespace Gameplay
{
    public class TickableManager : MonoBehaviour
    {
        public static TickableManager Instance { get; private set; }

        private List<ITickable> _tickables = new();
        private List<IFixedTickable> _fixedTickable = new();
        private List<IInitializeble> _initializeble = new();

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            GameObjectContext[] contexts = FindObjectsOfType<GameObjectContext>();
            SceneContext[] contextsScene = FindObjectsOfType<SceneContext>();

            foreach (var item in contexts)
                Run(item.Container);

            foreach (var item in contextsScene)
            {
                Run(item.Container);
            }

            foreach (var item in _initializeble)
                item.Initialize();
        }

        public void Run(DiContainer diContainer)
        {
            foreach (var item in diContainer.GetAll<ITickable>())
                _tickables.Add(item);

            foreach (var item in diContainer.GetAll<IFixedTickable>())
            {
                "fixed".Log();
                _fixedTickable.Add(item);
            }

            foreach (var item in diContainer.GetAll<IInitializeble>())
            {
                _initializeble.Add(item);
            }
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

        public void Test(DiContainer diContainer)
        {
            foreach (var item in diContainer.GetAll<IInitializeble>())
            {
                item.Log();
            }

            Run(diContainer);
            foreach (var item in _initializeble)
                item.Initialize();
        }
    }
}