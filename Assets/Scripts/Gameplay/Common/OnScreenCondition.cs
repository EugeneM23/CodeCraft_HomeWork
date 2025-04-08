using UnityEngine;

namespace Gameplay
{
    public class OnScreenCondition : IMoveCondition
    {
        private readonly Camera _camera;

        public OnScreenCondition(Camera camera) => _camera = camera;

        public bool Invoke(Vector3 position)
        {
            Vector3 viewPortPosition = _camera.WorldToViewportPoint(position);
            if (viewPortPosition.x < 0 || viewPortPosition.x > 1 || viewPortPosition.y < 0 || viewPortPosition.y > 1)
                return false;

            return true;
        }
    }
}