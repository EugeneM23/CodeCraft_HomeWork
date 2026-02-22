using System;
using AudioEngine;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryAudioController : IInitializable, IDisposable
    {
        private readonly Inventory _inventory;
        private AudioSystem _audioSystem;

        public InventoryAudioController(Inventory inventory)
        {
            _inventory = inventory;
        }

        public void Initialize()
        {
            _audioSystem = AudioSystem.Instance;

            _inventory.OnAdded += OnItemAdded;
            _inventory.OnRemoved += OnItemRemoved;
            _inventory.OnMoved += OnItemMoved;
        }

        public void Dispose()
        {
            _inventory.OnAdded -= OnItemAdded;
            _inventory.OnRemoved -= OnItemRemoved;
            _inventory.OnMoved -= OnItemMoved;
        }

        private void OnItemAdded(Item item, Vector2Int[] positions)
        {
            _audioSystem.PlayEvent(item.Settings.AddItemKey);
        }

        private void OnItemRemoved(Item item, Vector2Int[] positions)
        {
            _audioSystem.PlayEvent(item.Settings.DropToScene);
        }

        private void OnItemMoved(Item item, Vector2Int position)
        {
            _audioSystem.PlayEvent(item.Settings.AddItemKey);
        }
    }
}