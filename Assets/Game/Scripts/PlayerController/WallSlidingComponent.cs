using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class WallSlidingComponent
    {
        private CapsuleCollider2D _collider;
        private readonly PlayerController _player;
        private ScriptableStats _stats;

        public WallSlidingComponent(CapsuleCollider2D collider, PlayerController player, ScriptableStats stats)
        {
            _collider = collider;
            _player = player;
            _stats = stats;
        }

        public Vector2 SurfaceNormal { get; set; }

        public Vector2 ScanWall()
        {
            float shortRayLength = _collider.bounds.extents.y / 2 + 1;

            Vector2 origin2 = new Vector2(_collider.bounds.center.x, _collider.bounds.min.y);
            Vector2 direction = _player.FrameInput.Move;

            Debug.DrawLine(origin2, origin2 + direction * shortRayLength, Color.green);
            RaycastHit2D hit = Physics2D.Raycast(origin2, direction, shortRayLength, _stats.PlayerLayer);
            if (hit.collider != null)
            {
                if (Mathf.Approximately(Vector2.Angle(Vector2.up, hit.normal.normalized), 90))
                {
                    float velocity = _player.Velocity.y / 0.9f * -1;
                    return new Vector2(0, velocity);
                }

                return Vector2.zero;
            }

            return Vector2.zero;
        }
    }
}