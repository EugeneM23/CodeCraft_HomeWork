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

            // 1️⃣ Сначала создаём все GameObjectContext, чтобы они зарегистрировали свои зависимости
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            InitializeGameObjectContexts(allObjects);

            // 2️⃣ Потом получаем все компоненты
            MonoBehaviour[] allObjectsMono = FindObjectsOfType<MonoBehaviour>();

            // 3️⃣ И только теперь инжектим
            InjectScene(allObjectsMono);
        }

        private void InjectScene(MonoBehaviour[] allObjects)
        {
            foreach (MonoBehaviour component in allObjects)
            {
                // если этот компонент находится под GameObjectContext — пропускаем
                if (component.GetComponentInParent<GameObjectContext>() != null)
                    continue;

                Container.Inject(component);
            }
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