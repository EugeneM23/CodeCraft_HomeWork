using System;
using AudioEngine;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryAudioController : IInitializable, IDisposable
    {
        private readonly InventoryModel _inventoryModel;
        private AudioSystem _audioSystem;

        public InventoryAudioController(InventoryModel inventoryModel) =>
            _inventoryModel = inventoryModel;

        public void Initialize()
        {
            _audioSystem = AudioSystem.Instance;
            _inventoryModel.OnItemAdded += OnItemAdded;
            _inventoryModel.OnItemRemoved += OnItemRemoved;
            _inventoryModel.OnMoved += OnItemMoved;
        }

        public void Dispose()
        {
            _inventoryModel.OnItemAdded -= OnItemAdded;
            _inventoryModel.OnItemRemoved -= OnItemRemoved;
            _inventoryModel.OnMoved -= OnItemMoved;
        }

        private void OnItemAdded(Item item, Vector2Int[] positions)
        {
            Debug.Log(item.Settings.AddItemKey.EventId);
            _audioSystem.PlayEvent(InventoryBankAPI.AddItemToInventoryEvent);
        }

        private void OnItemRemoved(Item item, Vector2Int[] positions)
        {
            _audioSystem.PlayEvent(InventoryBankAPI.StartDragEvent);
        }

        private void OnItemMoved(Item item, Vector2Int position)
        {
            _audioSystem.PlayEvent(item.Settings.AddItemKey);
        }
    }
}