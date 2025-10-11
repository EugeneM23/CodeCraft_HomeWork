using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay
{
    public class RotationComponent : CompositCondition
    {
        private Transform _transform;

        public RotationComponent(Transform transform)
        {
            _transform = transform;
        }

        public void SetDiraction(Vector2 diraction)
        {
            if (AndCondition()) return;

            if (diraction == Vector2.zero) return;

            if (diraction == Vector2.right)
                _transform.localScale = new Vector3(diraction.x, 1, 1);

            if (diraction == Vector2.left)
                _transform.localScale = new Vector3(diraction.x, 1, 1);
        }
    }
}