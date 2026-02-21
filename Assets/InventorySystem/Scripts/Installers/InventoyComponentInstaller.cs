using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class InventoyComponentInstaller : MonoInstaller
    {
        [SerializeField] private Image _higlightImage;

        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<CellHighlighter>()
                .AsSingle()
                .WithArguments(_higlightImage)
                .NonLazy();
        }
    }
}