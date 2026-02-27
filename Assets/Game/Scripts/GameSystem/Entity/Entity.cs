using UnityEngine;
using Zenject;

public class Entity : MonoBehaviour
{
    [SerializeField] private GameObjectContext _context;

    public T ResolveComponent<T>() => _context.Container.Resolve<T>();
    
    public T ResolveComponent<T>(object id) => _context.Container.ResolveId<T>(id);
}