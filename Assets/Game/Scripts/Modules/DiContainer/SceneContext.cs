using UnityEngine;

namespace Gameplay
{
    [DefaultExecutionOrder(-999)]
    public class SceneContext : Context
    {
        public static SceneContext Instance { get; private set; }

        private void Awake() => Initialize();

        protected override void InstallBindings()
        {
            Instance = this;
            base.InstallBindings();

            var allObjects = FindObjectsOfType<GameObject>();
            InitializeGameObjectContexts(allObjects);
            InjectSceneObjects();
        }

        private void InitializeGameObjectContexts(GameObject[] allObjects)
        {
            foreach (var obj in allObjects)
                if (obj.TryGetComponent(out GameObjectContext context))
                    context.Initialize(Container);
        }

        private void InjectSceneObjects()
        {
            foreach (var component in FindObjectsOfType<MonoBehaviour>())
            {
                if (component.GetComponentInParent<GameObjectContext>() == null)
                    Container.Inject(component);
            }
        }
    }
}

