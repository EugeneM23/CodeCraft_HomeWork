using Gameplay;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ImpulseProvider : MonoBehaviour, IImpulse
{
    private Rigidbody2D _rigidbody;

    private void OnEnable() => _rigidbody = GetComponent<Rigidbody2D>();

    public void AddImpulse(Vector3 power)
    {
        _rigidbody.AddForce(power, ForceMode2D.Impulse);
    }
}