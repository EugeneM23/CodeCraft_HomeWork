using UnityEngine;

public class DragItemPhysics : MonoBehaviour
{
    [SerializeField] private RectTransform _rt;

    private Collider2D[] _overlapResults = new Collider2D[10]; // увеличь размер, если нужно больше объектов

    void Update()
    {
        CheckOverlap();
    }

    private void CheckOverlap()
    {
        Vector2 pos = _rt.position;
        Vector2 size = _rt.rect.size * _rt.lossyScale;

        // NonAlloc версия — нет аллокаций
        int hitCount = Physics2D.OverlapBoxNonAlloc(pos, size, 0f, _overlapResults);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = _overlapResults[i];
            if (col.gameObject == this.gameObject)
                continue;

            Debug.Log("UI overlap (nonalloc): " + col.name);
        }
    }

    // В редакторе удобно видеть область
    private void OnDrawGizmos()
    {
        if (_rt == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(_rt.position, _rt.rect.size * transform.lossyScale);
    }
}