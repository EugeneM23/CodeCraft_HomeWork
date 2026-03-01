using UnityEngine;
using Zenject;

public class Entity : MonoBehaviour
{
    [SerializeField] private GameObjectContext _context;

    public GameObjectContext Context => _context;

    public T ResolveComponent<T>() => _context.Container.Resolve<T>();

    public T ResolveComponent<T>(object id) => _context.Container.ResolveId<T>(id);

    public bool TryResolveComponent<T>(out T component) where T : class
    {
        component = default;

        if (_context == null || _context.Container == null)
            return false;

        component = _context.Container.TryResolve<T>();
        return component != null;
    }

    
}