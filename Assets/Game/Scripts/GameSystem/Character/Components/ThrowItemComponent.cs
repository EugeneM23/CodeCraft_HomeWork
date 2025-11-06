using System.Collections.Generic;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;

public class ThrowItemComponent : ITickable
{
    [Inject] private CharacterController2D _controller;
    [Inject] private List<IAction> _actions;

    private readonly LayerMask _entityMask;
    private readonly float _distance = 2f;
    private readonly float _throwDuration = 0.1f;

    private bool _searchDone = true;
    private float _throwTimer = 0f;
    private bool _isThrowing = false;

    public interface IAction
    {
        void Invoke();
    }

    public ThrowItemComponent(LayerMask entityMask)
    {
        _entityMask = entityMask;
    }

    public void Tick()
    {
        if (_isThrowing)
        {
            _throwTimer -= Time.deltaTime;

            if (_throwTimer <= 0f)
            {
                _isThrowing = false;
                _throwTimer = 0f;
            }
        }
    }

    public bool ThrowItem()
    {
        _searchDone = true;
        _isThrowing = true;
        _throwTimer = _throwDuration;

        Vector2 position = _controller.transform.position;

        Collider2D[] hits = Physics2D.OverlapCircleAll(position, _distance, _entityMask);

        if (hits.Length > 0)
        {
            Collider2D item = hits[0];
            item.transform.position = _controller.Collider.transform.position + new Vector3(0f, 1, 0f);
            Vector2 direction = new Vector2(_controller.LookDirection, 0);
            item.GetComponent<ImpulseProvider>().AddImpulse(direction * 50);
            item.GetComponent<ImpulseProvider>().AddTorque(50);
            foreach (var action in _actions)
                action.Invoke();
            return true;
        }

        return false;
    }

    public bool IsThrowing() => _isThrowing && _controller.IsGrounded;
}