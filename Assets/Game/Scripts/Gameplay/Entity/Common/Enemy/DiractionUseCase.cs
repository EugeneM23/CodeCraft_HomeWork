using UnityEngine;

namespace Game.Gameplay
{
    public static class DiractionUseCase
    {
        public static Vector3 Get(Transform source, Transform target) => target.position - source.position;
    }
}