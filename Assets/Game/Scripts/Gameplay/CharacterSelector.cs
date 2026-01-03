using System;
using UnityEngine;

public class CharacterSelector : MonoBehaviour
{
    public event Action<Entity> OnUnitChanged;

    private Entity _selectedEntity;

    public Entity SelectedEntity => _selectedEntity;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.TryGetComponent(out Entity entity))
            {
                _selectedEntity = entity;
                OnUnitChanged?.Invoke(entity);
            }
        }
    }
}