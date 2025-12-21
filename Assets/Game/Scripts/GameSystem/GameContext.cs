using System;
using Inventories;
using UnityEngine;

namespace Game.Scripts.GameSystem
{
    public class GameContext : MonoBehaviour
    {
        [SerializeField] private TestCharacter _testCharacter;
        [SerializeField] private InventoryInstaller _inventoryPrefab;
        [SerializeField] private EquipmentInstaller _equipmentPrefab;
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private Canvas _canvas;

        private void Awake()
        {
            InventoryInstaller inventory = Instantiate(_inventoryPrefab, _canvas.GetComponent<RectTransform>());
            inventory.Initialize(_factory, _dragFSM, _testCharacter);

            EquipmentInstaller equipment = Instantiate(_equipmentPrefab, inventory.GetComponent<RectTransform>());
            equipment.Initialize(_testCharacter);

            _testCharacter.Inventory = inventory.Inventory;
            _testCharacter.Equipment = equipment.Equipment;

            _dragFSM.SetMainInventory(inventory.Inventory);
        }
    }
}