using UnityEngine;

namespace Gameplay
{
    public interface IMoveCondition
    {
        public bool Invoke(Vector3 position);
    }
}