using AIKit;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// The elite's "Bite" (a Code Runner ability of its graph): starts when the tank's hull is within <see cref="REACH"/>
    /// of the body (the distance to the hull's box, as the old AI measured to the vehicle's bounds — the tank's middle is
    /// metres inside its hull) and the held weapon is ready. It fires the held weapon (<c>FireAction</c>): the weapon's
    /// cooldown, the attack animation, and the hit at the animation's damage event go through the game's own weapon
    /// and <c>DamageUseCase</c>, as for every enemy. Phases by time: wind-up, the hit window, recovery; the body turns to
    /// the hull meanwhile. The ability's pause after it is the graph's (its Cooldown part).
    /// </summary>
    public sealed class RegaxBiteRunner : ITickedAbilityRunner
    {
        public const string ID = "Bite";

        /// <summary>Metres from the body's middle to the hull's side within which it bites.</summary>
        public const float REACH = 1.6f;

        public const float WIND_UP = 0.2f;
        public const float ACTIVE = 0.35f;
        public const float RECOVERY = 0.45f;

        private const float TOTAL = WIND_UP + ACTIVE + RECOVERY;

        private readonly Transform _self;
        private PlayerTank _tank;
        private IEntity _entity;
        private IReactiveVariable<IEntity> _weapon;
        private IAction _fire;
        private IExpression<bool> _fireCondition;
        private AgentContext _context;
        private float _time = TOTAL;
        private bool _running;

        public RegaxBiteRunner(Transform self, PlayerTank tank, IEntity entity = null)
        {
            _self = self;
            _tank = tank;
            if (entity != null)
                Bind(entity);
        }

        public string Id => ID;

        /// <summary>How many bites it has started (tests, the debugger).</summary>
        public int Bites { get; private set; }

        public void Bind(IEntity entity)
        {
            _entity = entity;
            _weapon = entity.TryGetWeapon(out IReactiveVariable<IEntity> weapon) ? weapon : null;
            _fire = entity.TryGetFireAction(out IAction fire) ? fire : null;
            _fireCondition = entity.TryGetFireCondition(out IExpression<bool> condition) ? condition : null;
        }

        /// <summary>The tank's hull is within reach of a body standing at <paramref name="position"/>.</summary>
        public bool InReach(Vector3 position) =>
            _tank != null && _tank.IsPresent && _tank.DistanceToHull(position) <= REACH;

        public bool CanStart(AgentContext context)
        {
            if (_running || _self == null || !InReach(_self.position))
                return false;
            if (_fireCondition != null && !_fireCondition.Invoke())
                return false;                                   // dead
            IEntity weapon = _weapon?.Value;
            return weapon == null || !weapon.TryGetFireCondition(out IExpression<bool> ready) || ready.Invoke();
        }

        public void Begin(AgentContext context)
        {
            _context = context;
            _time = 0f;
            _running = true;
            Bites++;
            TurnToHull(context);
            _fire?.Invoke();
        }

        public bool IsRunning => _running;

        public AbilityPhase Phase =>
            !_running ? AbilityPhase.None
            : _time < WIND_UP ? AbilityPhase.WindUp
            : _time < WIND_UP + ACTIVE ? AbilityPhase.Active
            : AbilityPhase.Recovery;

        public void Tick(float dt)
        {
            if (!_running)
                return;
            _time += dt;
            TurnToHull(_context);
            if (_time >= TOTAL)
                _running = false;
        }

        public void Cancel()
        {
            _time = TOTAL;
            _running = false;
        }

        // The bite lands where the body faces: it turns to the nearest side of the hull
        private void TurnToHull(AgentContext context)
        {
            if (_tank == null || !_tank.IsPresent || context?.Actuator == null)
                return;
            Vector3 to = _tank.AimPoint - _self.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1e-6f)
                context.Actuator.Face(to.normalized);
        }
    }
}
