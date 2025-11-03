using UnityEngine;

namespace Modules.PlayerController
{
    public interface IMoveController
    {
        Vector2 CurrentDirection { get; }
    }
}