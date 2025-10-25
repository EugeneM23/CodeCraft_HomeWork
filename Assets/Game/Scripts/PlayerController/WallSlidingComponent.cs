using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class WallSlidingComponent
    {
        private CapsuleCollider2D _collider;
        private readonly PlayerController _player;
        private ScriptableStats _stats;
        private CollisionComponent _collision;

        public WallSlidingComponent(CapsuleCollider2D collider, PlayerController player, ScriptableStats stats, CollisionComponent collision)
        {
            _collider = collider;
            _player = player;
            _stats = stats;
            _collision = collision;
        }


        public Vector2 ScanWall()
        {
            if (_collision.ScanWalls(out var hit))
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