using System;
using Gameplay;
using UnityEngine;

public class BoxTest : MonoBehaviour
{
    public event Action<Vector3> OnPenitrationStart;
    public event Action<Vector3> OnPenitrationStay;
    public event Action OnPenetrationFinish;

    [SerializeField] private LayerMask layerMask;
    [SerializeField] private Color color = Color.yellow;
    [SerializeField] private bool outoResolve = true;
    [SerializeField] private bool smothResolve = true;

    [SerializeField] private Collider2D _coll;
    [SerializeField] private Collider2D _box;
    private Vector3 _correction;
    private bool resolvingCollision;

    private void Start()
    {
        OnPenitrationStart += _correction => { _correction.magnitude.Log(); };
        OnPenetrationFinish += () => { Debug.Log("Finish"); };
    }

    private void Update()
    {
        if (_coll.GetPenetrationLayer(layerMask, out var correction))
        {
            correction += correction.normalized;
            var delta = Vector3.Lerp(Vector3.zero, correction, 0.1f);
            transform.position += delta;
        }
    }

    private void OnDrawGizmos()
    {
        if (_correction != Vector3.zero)
        {
            Vector3 start = _coll.bounds.center;
            Vector3 finish = start + _correction;
            Gizmos.color = color;
            Gizmos.DrawLine(start, finish);
            Gizmos.DrawSphere(finish, 0.1f);
        }
    }
}