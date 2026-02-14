using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class SceneSystemInstaller : MonoInstaller
    {
        [SerializeField] private GraphicRaycaster _raycaster;

        public override void InstallBindings()
        {
            Container.Bind<RaycastDetector>().FromNew().AsSingle().WithArguments(EventSystem.current).NonLazy();
            Container.BindInstance(_raycaster).AsSingle().NonLazy();
        }
    }
}