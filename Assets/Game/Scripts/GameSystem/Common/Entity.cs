using UnityEngine;

namespace Gameplay
{
    public class Entity : MonoBehaviour
    {
        [SerializeField] private GameObjectContext _context;

        public T GetEntityComponent<T>() => _context.Container.Resolve<T>();
    }
}