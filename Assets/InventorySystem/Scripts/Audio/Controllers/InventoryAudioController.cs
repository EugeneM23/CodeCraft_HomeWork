using System;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryAudioController : IInitializable, IDisposable
    {
        private readonly Inventory _inventory;
        private readonly SignalBus _signalBus;

        public InventoryAudioController(SignalBus signalBus, Inventory inventory)
        {
            _signalBus = signalBus;
            _inventory = inventory;
        }

        public void Initialize()
        {
            _inventory.OnAdded += OnItemAdded;
        }

        private void OnItemAdded(Item item, Vector2Int[] _)
        {
            _signalBus.Fire(new DropItemAudioSignal
            {
                AudioKey = item.itemData.ItemAudioData.DropToInventory
            });
        }

        public void Dispose()
        {
        }
    }
}