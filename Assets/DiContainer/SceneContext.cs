using System;
using System.Diagnostics;
using System.Linq;
using Game.Scripts.Player;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Gameplay
{
    [DefaultExecutionOrder(-999)]
    public class SceneContext : Context
    {
        private void Awake() => Initialize();

        protected override void InstallBindings()
        {
            base.InstallBindings();

            MonoBehaviour[] allObjects = FindObjectsOfType<MonoBehaviour>();
            GameObject[] uniqueObjects = FindObjectsOfType<GameObject>();

            InjectScene(allObjects);
            InitializeGameObjectContexts(uniqueObjects);
        }

        private void InjectScene(MonoBehaviour[] allObjects)
        {
            foreach (MonoBehaviour component in allObjects)
                Container.Inject(component);
        }

        private void InitializeGameObjectContexts(GameObject[] allObjects)
        {
            foreach (var item in allObjects)
            {
                if (item.TryGetComponent(out GameObjectContext context))
                    context.Initialize(Container);
            }
        }
    }
}