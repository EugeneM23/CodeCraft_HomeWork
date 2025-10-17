using UnityEngine;

namespace Gameplay.Ability
{
    public class PushDebugDrawer
    {
        private const float DEBUG_DURATION = 0.1f;

        public void Draw(Vector2 origin, Vector2 dir, float distance, float radius, Color color)
        {
            Vector2 end = origin + dir * distance;
            Debug.DrawLine(origin, end, color, DEBUG_DURATION);

            DrawCircle(origin, radius, color, DEBUG_DURATION);
            DrawCircle(end, radius, color, DEBUG_DURATION);
        }

        private void DrawCircle(Vector2 center, float radius, Color color, float duration)
        {
            int segments = 20;
            Vector2 lastPoint = center + new Vector2(radius, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector2 newPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Debug.DrawLine(lastPoint, newPoint, color, duration);
                lastPoint = newPoint;
            }
        }
    }
}