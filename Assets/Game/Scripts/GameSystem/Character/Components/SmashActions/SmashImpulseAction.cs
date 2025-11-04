using Gameplay;
using Gameplay.Controllers;
using Modules.PlayerController;
using UnityEngine;

public class SmashImpulseAction : SmashComponent.IAction, ITickable
{
    [Inject] private CharacterController2D _controller;
    private readonly LayerMask _entityMask;

    private bool _impulseDone = true;
    private readonly float _radius = 25f;
    private readonly float _impulsePower = 35f;

    public SmashImpulseAction(LayerMask entityMask)
    {
        _entityMask = entityMask;
    }

    public void Invoke() => _impulseDone = false;

    public void Tick()
    {
        if (!_controller.IsGrounded || _impulseDone)
            return;

        _impulseDone = true;

        Vector2 position = _controller.transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, _radius, _entityMask);

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent(out Entity entity))
            {
                Vector2 impulse = GetImpulseVector(entity.transform);
                entity.GetEntityComponent<IImpulse>().AddImpulse(impulse);
            }

            if (hit.TryGetComponent(out ImpulseProvider component))
            {
                Vector2 impulse = GetImpulseVector(component.transform);
                component.AddImpulse(impulse);
            }
        }

        Debug.DrawLine(position, position + Vector2.up * 0.1f, Color.green, 0.5f);
        DebugDrawCircle(position, _radius, Color.yellow, 0.5f);
    }

    private Vector2 GetImpulseVector(Transform transform)
    {
        Vector2 direction = (transform.transform.position - _controller.transform.position).normalized;
        float distance = Vector2.Distance(transform.transform.position, _controller.transform.position);
        float falloff = Mathf.Clamp01(1f - (distance / _radius));
        Vector2 impulse = (direction / 2 + Vector2.up) * (_impulsePower * falloff);
        return impulse;
    }

    private void DebugDrawCircle(Vector2 center, float radius, Color color, float duration)
    {
        int segments = 20;
        float angle = 0f;
        Vector3 lastPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        for (int i = 1; i <= segments; i++)
        {
            angle += 2 * Mathf.PI / segments;
            Vector3 nextPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Debug.DrawLine(lastPoint, nextPoint, color, duration);
            lastPoint = nextPoint;
        }
    }
}