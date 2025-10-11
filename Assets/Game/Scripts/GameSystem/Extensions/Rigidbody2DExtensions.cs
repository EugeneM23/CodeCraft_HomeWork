using UnityEngine;

namespace Gameplay
{
    public static class Rigidbody2DExtensions
    {
        public static void ResetAndAddForce(this Rigidbody2D rb, Vector2 force, ForceMode2D mode = ForceMode2D.Impulse)
        {
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(force, mode);
        }
    }
}