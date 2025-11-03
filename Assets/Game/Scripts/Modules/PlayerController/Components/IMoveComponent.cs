using UnityEngine;

namespace Modules.PlayerController
{
    public interface IMoveComponent
    {
        void Move(Vector2 directrion);
        
        bool CanMove();
    }
}