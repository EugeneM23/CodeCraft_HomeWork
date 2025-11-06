using UnityEngine;

namespace Modules.PlayerController
{

    internal class LedgeGrabComponent : ITickable
    {
        private readonly CharacterController2D _character;

        private const float WallCheckDistance = 0.5f;
        private const float LedgeCheckDistance = 0.5f;
        private const float MaxAngleDeviation = 30f;
        private const float GrabCooldown = 0.1f;

        private bool _isGrabbing;
        private Vector2 _grabPoint;
        private float _releaseTime;

        public bool IsGrabbing => _isGrabbing;

        public LedgeGrabComponent(CharacterController2D character)
        {
            _character = character;
        }

        public void Tick() => CheckLedges();

        public void CheckLedges()
        {
            // Проверяем кулдаун после отпускания
            if (Time.time < _releaseTime + GrabCooldown)
            {
                _isGrabbing = false;
                return;
            }

            // Проверяем только при падении
            if (_character.Velocity.y > 0)
            {
                _isGrabbing = false;
                return;
            }

            Vector2 playerCenter = (Vector2)_character.transform.position + _character.Collider.offset;
            float halfWidth = _character.Collider.size.x / 2f;
            float topY = playerCenter.y + _character.Collider.size.y / 2f;

            // Проверяем обе стороны
            bool leftGrab = CheckSide(new Vector2(playerCenter.x - halfWidth, topY), -1);
            bool rightGrab = CheckSide(new Vector2(playerCenter.x + halfWidth, topY), 1);

            _isGrabbing = leftGrab || rightGrab;
        }

        private bool CheckSide(Vector2 startPos, int direction)
        {
            Vector2 horizontalDir = Vector2.right * direction;

            // ШАГ 1: Проверяем стену сбоку
            RaycastHit2D wallHit = Physics2D.Raycast(
                startPos,
                horizontalDir,
                WallCheckDistance,
                _character.Stats.LayerMask
            );

            Debug.DrawLine(startPos, startPos + horizontalDir * WallCheckDistance, wallHit ? Color.green : Color.gray);

            if (!wallHit) return false;

            // ШАГ 2: Точка проверки сверху (над стеной, чуть впереди)
            Vector2 ledgeCheckStart = wallHit.point + new Vector2(0.15f * direction, 0.4f);

            // ШАГ 3: Ищем выступ сверху
            RaycastHit2D ledgeHit = Physics2D.Raycast(
                ledgeCheckStart,
                Vector2.down,
                LedgeCheckDistance,
                _character.Stats.LayerMask
            );

            Debug.DrawLine(ledgeCheckStart, ledgeCheckStart + Vector2.down * LedgeCheckDistance,
                ledgeHit ? Color.cyan : Color.gray);

            if (!ledgeHit) return false;

            // ШАГ 4: Проверяем углы поверхностей
            Vector2 expectedWallNormal = direction > 0 ? Vector2.left : Vector2.right;
            float wallAngle = Vector2.Angle(wallHit.normal, expectedWallNormal);
            float ledgeAngle = Vector2.Angle(ledgeHit.normal, Vector2.up);

            Debug.DrawRay(wallHit.point, wallHit.normal * 0.3f, Color.yellow);
            Debug.DrawRay(ledgeHit.point, ledgeHit.normal * 0.3f, Color.magenta);

            if (wallAngle > MaxAngleDeviation || ledgeAngle > MaxAngleDeviation)
                return false;

            // ШАГ 5: Выступ найден!
            _grabPoint = ledgeHit.point;
            Debug.DrawLine(ledgeHit.point, ledgeHit.point + Vector2.up * 0.5f, Color.red, 0.5f);

            return true;
        }

        public void ReleaseGrab()
        {
            _isGrabbing = false;
            _releaseTime = Time.time;
        }
    }
}