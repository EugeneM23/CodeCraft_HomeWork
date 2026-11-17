using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The cannon's blast slams into the mud under the barrel: a shallow scoop is blown out of the
    /// ground there, clumps fly up and out of it — some back onto the tank — and the tank is
    /// splashed. Only over mud; a shot fired too high above the ground does nothing to it.
    /// </summary>
    public sealed class TankMuzzleMudBehaviour : IEntityInit, IEntityDispose
    {
        /// <summary>How far (m) below the muzzle the mud may be and still be blown about.</summary>
        private const float REACH = 2.5f;

        /// <summary>How far (m) ahead of the muzzle, along the ground, the blast hits it hardest.</summary>
        private const float AHEAD = 0.6f;

        /// <summary>Clumps thrown by one shot, at full strength.</summary>
        private const int CLUMPS = 28;

        /// <summary>Dirt (TankDirtBehaviour) one shot splashes onto the tank, at full strength.</summary>
        private const float DIRT = 0.15f;

        private readonly TankDirtBehaviour _dirt;

        private Transform _firePoint;
        private IEvent _fireEvent;

        public TankMuzzleMudBehaviour(TankDirtBehaviour dirt)
        {
            _dirt = dirt;
        }

        public void Init(IEntity entity)
        {
            if (!entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon) || weapon.Value == null)
                return;

            weapon.Value.TryGetFirePoint(out _firePoint);

            if (weapon.Value.TryGetFireEvent(out _fireEvent))
                _fireEvent.Subscribe(OnFire);
        }

        public void Dispose(IEntity entity)
        {
            _fireEvent?.Unsubscribe(OnFire);
            _fireEvent = null;
        }

        private void OnFire()
        {
            if (_firePoint == null)
                return;

            Vector3 muzzle = _firePoint.position;
            Vector3 flat = new(_firePoint.forward.x, 0f, _firePoint.forward.z);
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.forward;

            if (!MudImpactUseCase.TryFindBelow(muzzle + flat * AHEAD, REACH, out MudSurface surface, out Vector3 ground))
                return;

            // Weaker the higher the muzzle is above the mud.
            float strength = 1f - Mathf.Clamp01((muzzle.y - ground.y - 0.5f) / REACH);
            if (strength <= 0f)
                return;

            surface.Dent(ground, flat, 1f, 0.75f, 0.4f * strength, 2f, 0.5f, 1f);

            Vector3 side = Vector3.Cross(Vector3.up, flat);
            int count = Mathf.RoundToInt(CLUMPS * strength);
            for (int i = 0; i < count; i++)
            {
                // A wide burst: mostly out along the shot and well off to the sides; one in four
                // back at the tank.
                bool back = Random.value < 0.25f;
                Vector3 away = back
                    ? -flat * Random.Range(1.8f, 3.6f) + side * Random.Range(-1.5f, 1.5f)
                    : flat * Random.Range(1f, 5.5f) + side * Random.Range(-4.5f, 4.5f);
                Vector3 velocity = away + Vector3.up * Random.Range(3.5f, 6.5f);
                surface.Throw(ground, velocity, 0.6f, 1, Random.Range(0.8f, 1.3f));
            }

            _dirt?.Splash(DIRT * strength);
        }
    }
}
