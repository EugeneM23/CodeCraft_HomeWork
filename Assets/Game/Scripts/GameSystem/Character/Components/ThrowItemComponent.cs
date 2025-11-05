using Gameplay;
using Modules.PlayerController;
using UnityEngine;

public class ThrowItemComponent
{
    [Inject] private CharacterController2D _controller;

    private readonly LayerMask _entityMask;
    private readonly float _distance = 2f;

    private bool _searchDone = true;

    public ThrowItemComponent(LayerMask entityMask)
    {
        _entityMask = entityMask;
    }

    public void ThrowItem()
    {
        _searchDone = true;

        Vector2 position = _controller.transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(position, _distance, _entityMask);

        if (hits.Length > 0)
        {
            Collider2D item = hits[0];
            item.transform.position = _controller.Collider.transform.position + new Vector3(0f, 1, 0f);
            Vector2 direction = new Vector2(_controller.LookDirection, 0);
            item.GetComponent<ImpulseProvider>().AddImpulse(direction * 50);
            item.GetComponent<ImpulseProvider>().AddTorque(50);
        }
    }
}