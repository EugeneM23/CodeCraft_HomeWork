using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class SceneSystemInstaller : MonoInstaller
    {
        [SerializeField] private GraphicRaycaster _raycaster;
        [SerializeField] private Canvas _canvas;

        public override void InstallBindings()
        {
            Container.Bind<Canvas>().FromInstance(_canvas).AsSingle().NonLazy();
        }
    }
}