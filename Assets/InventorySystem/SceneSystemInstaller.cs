using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class SceneSystemInstaller : MonoInstaller
    {
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Vector2Int _inventorySize;
        [SerializeField] private SceneItem[] _initialItems;

        public override void InstallBindings()
        {
            Container
                .Bind<Canvas>()
                .FromInstance(_canvas)
                .AsSingle()
                .NonLazy();

            var items = new List<ItemSettings>();

            foreach (var item in _initialItems)
                items.Add(item.itemSettings);

            Container
                .Bind<Inventory>()
                .FromMethod(() => new Inventory(_inventorySize.x, _inventorySize.y, items))
                .AsSingle();
        }
    }
}