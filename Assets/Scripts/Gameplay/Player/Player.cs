using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay
{
    public sealed class Player : Character
    {
        public override void Move(Vector3 direction)
        {
            if (IsPlayerOnScreen(direction))
                base.Move(direction);
        }

        protected override void OnDeath()
        {
            if (_health <= 0)
                SceneManager.LoadScene("Game");
        }

        private bool IsPlayerOnScreen(Vector3 direction)
        {
            Vector3 viewportPos = Camera.main.WorldToViewportPoint(transform.position + direction);
            if (viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1)
                return false;

            return true;
        }
    }
}