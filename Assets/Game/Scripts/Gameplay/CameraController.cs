using System;
using UnityEngine;
using Zenject;

public class CameraController : IInitializable, IDisposable
{
    private readonly CameraMovement _cameraMovement;
    private readonly InventoryDebug _inventoryDebug;

    public CameraController(CameraMovement cameraMovement, InventoryDebug inventoryDebug)
    {
        _cameraMovement = cameraMovement;
        _inventoryDebug = inventoryDebug;
    }

    public void Initialize()
    {
        _inventoryDebug.OnInventoryOpened += HandleInventoryOpened;
        _inventoryDebug.OnInventoryClosed += HandleInventoryClosed;
    }

    public void Dispose()
    {
        _inventoryDebug.OnInventoryOpened -= HandleInventoryOpened;
        _inventoryDebug.OnInventoryClosed -= HandleInventoryClosed;
    }

    private void HandleInventoryOpened()
    {
        _cameraMovement.MoveToInventoryPosition();
    }

    private void HandleInventoryClosed()
    {
        _cameraMovement.MoveToGameplayPosition();
    }
}