using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal interface IMoveComponent
    {
        Vector2 Move(Vector2 direction);
    }
}