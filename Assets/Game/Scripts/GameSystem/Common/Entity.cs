using UnityEngine;

namespace Gameplay
{
    public class Entity : MonoBehaviour
    {
        [SerializeField] private GameObjectContext _context;

        public bool TryGetEntityComponent<T>(out T component)
        {
            component = _context.Container.Resolve<T>();

            if (component == null)
                return false;
            
            return true;
        }
    }
}